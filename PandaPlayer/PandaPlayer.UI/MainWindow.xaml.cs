using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Threading.Tasks;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using PandaPlayer.Core.Services;
using PandaPlayer.Core.Persistence;
using PandaPlayer.Core.Models;
using PandaPlayer.UI.Models;
using PandaPlayer.UI.Services;
using PandaPlayer.UI.Views;
using WinForms = System.Windows.Forms;

namespace PandaPlayer.UI
{
    public partial class MainWindow : Window
    {
        // Services
        private VlcPlayerService   _vlcService;
        private IPlayerService     _playerService;
        private IAppSettingsStore  _settingsStore;
        private IFolderStateStore  _folderStore;
        private KeyBindingService  _keyBindingService;

        // Playlist state
        private string       _currentFolderPath   = "";
        private List<string> _currentPlaylist      = new List<string>();
        private int          _currentPlaylistIndex = -1;
        private bool         _sidebarVisible       = true;
        private int          _seekJumpSeconds      = 5;
        private bool         _seekFromTimer        = false;
        private DispatcherTimer _positionTimer;

        // Playback mode
        private enum PlayMode { Sequential, Shuffle, NoRepeatShuffle }
        private PlayMode          _playMode    = PlayMode.Sequential;
        private readonly Random   _rng         = new Random();
        private readonly HashSet<string> _shufflePlayed = new HashSet<string>();
        private int _playedCount = 0;

        // Fullscreen
        private bool         _isFullscreen;
        private WindowState  _prevWindowState;
        private WindowStyle  _prevWindowStyle;
        private GridLength   _prevPanelWidth;
        private DispatcherTimer _autoHideTimer;
        private DispatcherTimer _fullscreenMouseTimer;
        private System.Drawing.Point _lastMousePos;

        // ─── Constructor ──────────────────────────────────────────────────────

        public MainWindow()
        {
            Log("MainWindow constructor starting");
            InitializeComponent();
            Log("InitializeComponent completed");

            try
            {
                InitializeServices();

                // Wire VLC MediaPlayer to the WPF VideoView surface
                VideoView.MediaPlayer = _vlcService.NativeMediaPlayer;
                Log("VideoView.MediaPlayer wired");

                // Folder-level progress persistence
                _folderStore = new JsonFolderStateStore();

                // Position-update timer (200ms tick)
                _positionTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
                _positionTimer.Tick += PositionTimer_Tick;
                _positionTimer.Start();

                // Auto-advance when a video finishes
                _vlcService.PlaybackEnded += async (s, e) =>
                    await Dispatcher.InvokeAsync(async () => await AdvanceAndPlay(forward: true));

                UpdateStatus("Ready — open a folder or file to begin");

                // Dark title bar + Win32 message hook (double-click / right-click on VLC surface)
                SourceInitialized += (s, _) =>
                {
                    var hwnd = new WindowInteropHelper(this).Handle;
                    EnableDarkTitleBar(hwnd);
                    HwndSource.FromHwnd(hwnd)?.AddHook(MainWndProc);
                };

                // Load persisted settings then handle CLI args
                Loaded += async (s, _) =>
                {
                    await LoadAndApplySettingsAsync();
                    ProcessCommandLineArgs();
                };
            }
            catch (Exception ex)
            {
                Log($"FATAL: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
                MessageBox.Show(
                    $"Failed to initialize Panda Player\n\nError: {ex.Message}\n\nCheck logs in:\n%LOCALAPPDATA%\\PandaPlayer\\logs",
                    "Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Close();
            }
        }

        // ─── Initialisation ───────────────────────────────────────────────────

        private void InitializeServices()
        {
            Log("Creating VlcPlayerService");
            _vlcService    = new VlcPlayerService();
            _playerService = _vlcService;
            Log("VlcPlayerService created");

            var appData   = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var storePath = Path.Combine(appData, "PandaPlayer");
            Directory.CreateDirectory(storePath);

            _keyBindingService = new KeyBindingService(storePath);
            _settingsStore     = new JsonAppSettingsStore(storePath);
            Log("Services initialised");
        }

        private void ProcessCommandLineArgs()
        {
            try
            {
                string[] args = Environment.GetCommandLineArgs();
                if (args.Length < 2) return;

                string target = args[1];
                Log($"CLI arg: {target}");

                if (Directory.Exists(target))
                {
                    LoadVideosFromFolder(target);
                }
                else if (File.Exists(target))
                {
                    _currentPlaylist.Clear();
                    _currentPlaylist.Add(target);
                    _currentPlaylistIndex  = 0;
                    _currentFolderPath = Path.GetDirectoryName(target) ?? "";
                    RefreshPlaylist();
                    _ = PlayCurrentFile();
                }
            }
            catch (Exception ex) { Log($"CLI arg error: {ex.Message}"); }
        }

        // ─── Settings Persistence ────────────────────────────────────────────

        private async Task LoadAndApplySettingsAsync()
        {
            try
            {
                var s = await _settingsStore.LoadSettingsAsync();

                // Volume: settings stores 0.0–1.0, slider is 0–100
                VolumeSlider.Value = Math.Clamp((int)(s.Volume * 100), 0, 100);

                // Seek jump
                _seekJumpSeconds         = Math.Max(1, s.SeekForwardSeconds);
                SeekJumpText.Text        = $"{_seekJumpSeconds}s";
                SeekJumpBadge.ToolTip    = $"Arrow-key seek: {_seekJumpSeconds}s — click to change";

                // Playback mode
                _playMode = s.DefaultPlaybackMode switch
                {
                    PandaPlayer.Core.Models.PlaybackMode.Shuffle          => PlayMode.Shuffle,
                    PandaPlayer.Core.Models.PlaybackMode.NoRepeatShuffle  => PlayMode.NoRepeatShuffle,
                    _                                                      => PlayMode.Sequential
                };
                string modeStr = _playMode switch
                {
                    PlayMode.Shuffle          => "Shuffle",
                    PlayMode.NoRepeatShuffle  => "No-Repeat Shuffle",
                    _                         => "Sequential"
                };
                foreach (ComboBoxItem item in PlaybackModeBox.Items)
                {
                    if (item.Content?.ToString() == modeStr)
                    { PlaybackModeBox.SelectedItem = item; break; }
                }

                // Target folders
                TargetsBox.Items.Clear();
                foreach (var kvp in s.MoveTargetFolders.OrderBy(k => k.Key))
                    TargetsBox.Items.Add(kvp.Value);

                Log($"Settings loaded: vol={s.Volume:F2} seek={_seekJumpSeconds}s mode={modeStr} targets={s.MoveTargetFolders.Count}");
            }
            catch (Exception ex) { Log($"LoadSettings error: {ex.Message}"); }
        }

        private PlaybackSettings CollectCurrentSettings()
        {
            var targets = new Dictionary<int, string>();
            for (int i = 0; i < TargetsBox.Items.Count; i++)
                targets[i] = TargetsBox.Items[i]?.ToString() ?? "";

            var coreMode = _playMode switch
            {
                PlayMode.Shuffle         => PandaPlayer.Core.Models.PlaybackMode.Shuffle,
                PlayMode.NoRepeatShuffle => PandaPlayer.Core.Models.PlaybackMode.NoRepeatShuffle,
                _                        => PandaPlayer.Core.Models.PlaybackMode.Sequential
            };

            return new PlaybackSettings
            {
                Volume              = (float)(VolumeSlider.Value / 100.0),
                SeekForwardSeconds  = _seekJumpSeconds,
                SeekBackwardSeconds = _seekJumpSeconds,
                DefaultPlaybackMode = coreMode,
                MoveTargetFolders   = targets,
                HardwareAccelerationEnabled = true
            };
        }

        // ─── Position Timer ───────────────────────────────────────────────────

        private void PositionTimer_Tick(object sender, EventArgs e)
        {
            if (!_vlcService.IsPlaying && !_vlcService.IsPaused) return;

            var mp  = _vlcService.NativeMediaPlayer;
            var pos = mp?.Position ?? 0f;
            var dur = mp?.Length   ?? 0L;
            var t   = mp?.Time     ?? 0L;

            _seekFromTimer    = true;
            SeekSlider.Value  = pos;
            _seekFromTimer    = false;

            TimeDisplay.Text     = FormatMs(t);
            DurationDisplay.Text = FormatMs(dur);
        }

        private static string FormatMs(long ms)
        {
            if (ms <= 0) return "0:00";
            var ts = TimeSpan.FromMilliseconds(ms);
            return ts.Hours > 0
                ? $"{ts.Hours}:{ts.Minutes:D2}:{ts.Seconds:D2}"
                : $"{ts.Minutes}:{ts.Seconds:D2}";
        }

        // ─── File / Folder Opening ────────────────────────────────────────────

        private void OpenFolder_Click(object sender, RoutedEventArgs e)
        {
            using var dlg = new WinForms.FolderBrowserDialog
            {
                Description = "Select folder containing videos"
            };
            if (dlg.ShowDialog() == WinForms.DialogResult.OK)
                LoadVideosFromFolder(dlg.SelectedPath);
        }

        private void OpenFile_Click(object sender, RoutedEventArgs e)
        {
            using var dlg = new WinForms.OpenFileDialog
            {
                Title  = "Open Video File",
                Filter = "Video Files|*.mp4;*.avi;*.mkv;*.mov;*.flv;*.wmv;*.webm;*.m4v;*.ts;*.m2ts|All Files|*.*"
            };
            if (dlg.ShowDialog() != WinForms.DialogResult.OK) return;

            _currentPlaylist.Clear();
            _currentPlaylist.Add(dlg.FileName);
            _currentPlaylistIndex = 0;
            RefreshPlaylist();
            _ = PlayCurrentFile();
        }

        // ─── Sidebar / Settings ───────────────────────────────────────────────

        private void ToggleSidebar_Click(object sender, RoutedEventArgs e)
        {
            _sidebarVisible = !_sidebarVisible;
            RightPanelColumn.Width = _sidebarVisible ? new GridLength(260) : new GridLength(0);
            UpdateStatus(_sidebarVisible ? "Sidebar shown" : "Sidebar hidden");
        }

        private void Settings_Click(object sender, RoutedEventArgs e)
        {
            var win = new KeyBindingsWindow(_keyBindingService) { Owner = this };
            win.ShowDialog();
        }

        private void SeekJump_Click(object sender, RoutedEventArgs e) => OpenSeekJumpDialog();

        private void OpenSeekJumpDialog()
        {
            string? result = ShowInputDialog(
                "Seek jump (arrow keys)",
                "Seconds to skip with ← / → keys:",
                _seekJumpSeconds.ToString());
            if (result == null) return;
            if (int.TryParse(result.Trim(), out int v) && v > 0)
            {
                _seekJumpSeconds = v;
                SeekJumpText.Text        = $"{v}s";
                SeekBackBtn.ToolTip      = $"Seek −{v}s (←)";
                SeekFwdBtn.ToolTip       = $"Seek +{v}s (→)";
                SeekJumpBadge.ToolTip    = $"Arrow-key seek: {v}s — click to change";
                UpdateStatus($"Seek jump set to {v}s");
            }
        }

        // ─── Playback Controls ────────────────────────────────────────────────

        private async void PlayPause_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPlaylist.Count == 0) { UpdateStatus("No videos loaded"); return; }
            try
            {
                if (_vlcService.IsPlaying)
                {
                    await _playerService.PauseAsync();
                    PlayPausePath.Data = (Geometry)FindResource("IcoPlay");
                    UpdateStatus("Paused");
                }
                else if (_vlcService.IsPaused)
                {
                    await _playerService.ResumeAsync();
                    PlayPausePath.Data = (Geometry)FindResource("IcoPause");
                    UpdateStatus($"Playing: {Path.GetFileName(_currentPlaylist[_currentPlaylistIndex])}");
                }
                else
                {
                    await PlayCurrentFile();
                }
            }
            catch (Exception ex) { UpdateStatus($"Error: {ex.Message}"); }
        }

        private async void Next_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPlaylist.Count == 0) return;
            await AdvanceAndPlay(forward: true);
        }

        private async void Previous_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPlaylist.Count == 0) return;
            await AdvanceAndPlay(forward: false);
        }

        private async void Stop_Click(object sender, RoutedEventArgs e)
            => await Stop_Click_Internal();

        // Advance playlist by mode and play
        private async Task AdvanceAndPlay(bool forward)
        {
            if (_currentPlaylist.Count == 0) return;

            switch (_playMode)
            {
                case PlayMode.Sequential:
                    _currentPlaylistIndex = forward
                        ? (_currentPlaylistIndex + 1) % _currentPlaylist.Count
                        : (_currentPlaylistIndex - 1 + _currentPlaylist.Count) % _currentPlaylist.Count;
                    break;

                case PlayMode.Shuffle:
                    _currentPlaylistIndex = _rng.Next(_currentPlaylist.Count);
                    break;

                case PlayMode.NoRepeatShuffle:
                    // Mark current as played
                    if (_currentPlaylistIndex >= 0 && _currentPlaylistIndex < _currentPlaylist.Count)
                        _shufflePlayed.Add(Path.GetFileName(_currentPlaylist[_currentPlaylistIndex]));

                    // Collect unplayed
                    var remaining = _currentPlaylist
                        .Select((f, i) => (f, i))
                        .Where(x => !_shufflePlayed.Contains(Path.GetFileName(x.f)))
                        .Select(x => x.i)
                        .ToList();

                    if (remaining.Count == 0)
                    {
                        var answer = MessageBox.Show(
                            "All videos in this folder have been played.\nStart a new cycle?",
                            "Cycle Complete", MessageBoxButton.YesNo, MessageBoxImage.Question);

                        _shufflePlayed.Clear();

                        if (answer != MessageBoxResult.Yes)
                        {
                            await Stop_Click_Internal();
                            return;
                        }
                        remaining = Enumerable.Range(0, _currentPlaylist.Count).ToList();
                    }

                    _currentPlaylistIndex = remaining[_rng.Next(remaining.Count)];
                    break;
            }

            await PlayCurrentFile();
        }

        private async Task Stop_Click_Internal()
        {
            await _playerService.StopAsync();
            PlayPausePath.Data           = (Geometry)FindResource("IcoPlay");
            TimeDisplay.Text             = "0:00";
            DurationDisplay.Text         = "0:00";
            SeekSlider.Value             = 0;
            PlaceholderPanel.Visibility  = Visibility.Visible;
            UpdateStatus("Stopped");
        }

        private void PlaylistItem_DoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (PlaylistBox.SelectedIndex >= 0)
            {
                _currentPlaylistIndex = PlaylistBox.SelectedIndex;
                _ = PlayCurrentFile();
            }
        }

        private async Task PlayCurrentFile()
        {
            if (_currentPlaylistIndex < 0 || _currentPlaylistIndex >= _currentPlaylist.Count) return;
            var path = _currentPlaylist[_currentPlaylistIndex];
            try
            {
                await _playerService.PlayAsync(new VideoItem
                {
                    FilePath      = path,
                    FileName      = Path.GetFileName(path),
                    FolderPath    = Path.GetDirectoryName(path) ?? "",
                    PlaybackIndex = _currentPlaylistIndex
                });

                PlayPausePath.Data          = (Geometry)FindResource("IcoPause");
                PlaceholderPanel.Visibility = Visibility.Collapsed;
                PlaylistBox.SelectedIndex   = _currentPlaylistIndex;
                PlaylistBox.ScrollIntoView(PlaylistBox.SelectedItem);
                _playedCount++;
                UpdatePlayCounter();

                var fileName = Path.GetFileName(path);
                Title = $"{fileName} — Panda Player";
                UpdateStatus($"Playing ({_currentPlaylistIndex + 1}/{_currentPlaylist.Count}): {fileName}");
            }
            catch (Exception ex) { UpdateStatus($"Error: {ex.Message}"); }
        }

        // ─── Seek ─────────────────────────────────────────────────────────────

        private void SeekSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_seekFromTimer) return;
            CommitSeek((float)e.NewValue);
        }

        private void CommitSeek(float position)
        {
            var mp = _vlcService.NativeMediaPlayer;
            if (mp == null) return;
            if (!mp.IsPlaying && mp.State != LibVLCSharp.Shared.VLCState.Paused) return;
            mp.Position = Math.Max(0f, Math.Min(1f, position));
        }

        // ─── Volume ───────────────────────────────────────────────────────────

        private void VolumeSlider_PreviewMouseLeftButtonDown(
            object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            double ratio = e.GetPosition(VolumeSlider).X / VolumeSlider.ActualWidth;
            ratio = Math.Max(0, Math.Min(1, ratio));
            VolumeSlider.Value = VolumeSlider.Minimum + ratio * (VolumeSlider.Maximum - VolumeSlider.Minimum);
            e.Handled = true;
        }

        private void Volume_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            int vol = (int)e.NewValue;
            if (VolumeDisplay != null) VolumeDisplay.Text = vol.ToString();
            if (_vlcService?.NativeMediaPlayer != null)
                _vlcService.NativeMediaPlayer.Volume = vol;
        }

        // ─── Playback Mode ────────────────────────────────────────────────────

        private void PlaybackMode_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (PlaybackModeBox?.SelectedItem == null) return;
            var label = (PlaybackModeBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "";
            _playMode = label switch
            {
                "Shuffle"           => PlayMode.Shuffle,
                "No-Repeat Shuffle" => PlayMode.NoRepeatShuffle,
                _                   => PlayMode.Sequential
            };
            _shufflePlayed.Clear();
            UpdatePlayCounter();
            UpdateStatus($"Playback mode: {label}");
        }

        private void UpdatePlayCounter()
        {
            int total = _currentPlaylist.Count;
            if (total == 0) { PlayCountDisplay.Text = "—"; return; }

            int played;
            if (_playMode == PlayMode.NoRepeatShuffle)
            {
                played = _currentPlaylist.Count(f => _shufflePlayed.Contains(Path.GetFileName(f)));
                // Count the currently-playing file even if not yet in history
                if (_currentPlaylistIndex >= 0 && _currentPlaylistIndex < _currentPlaylist.Count)
                {
                    var cur = Path.GetFileName(_currentPlaylist[_currentPlaylistIndex]);
                    if (!_shufflePlayed.Contains(cur)) played++;
                }
            }
            else
            {
                played = _playedCount;
            }

            int left = Math.Max(0, total - played);
            PlayCountDisplay.Text = $"{played}/{total} · {left} left";
        }

        private void ResetPlayCounter_Click(object sender, RoutedEventArgs e)
        {
            _playedCount = 0;
            _shufflePlayed.Clear();
            UpdatePlayCounter();
            UpdateStatus("Play counter reset");
        }

        // ─── Move Targets ─────────────────────────────────────────────────────

        private void AddTarget_Click(object sender, RoutedEventArgs e)
        {
            using var dlg = new WinForms.FolderBrowserDialog
            {
                Description = "Select destination folder for moved videos"
            };
            if (dlg.ShowDialog() == WinForms.DialogResult.OK)
            {
                // Don't add duplicates
                if (!TargetsBox.Items.Cast<string>().Any(t => t == dlg.SelectedPath))
                {
                    TargetsBox.Items.Add(dlg.SelectedPath);
                    UpdateStatus($"Target added: {dlg.SelectedPath}");
                }
                else
                {
                    UpdateStatus("Target already exists");
                }
            }
        }

        private void RemoveTarget_Click(object sender, RoutedEventArgs e)
        {
            if (TargetsBox.SelectedIndex >= 0)
            {
                var removed = TargetsBox.Items[TargetsBox.SelectedIndex]?.ToString() ?? "";
                TargetsBox.Items.RemoveAt(TargetsBox.SelectedIndex);
                UpdateStatus($"Target removed: {removed}");
            }
            else
            {
                UpdateStatus("Select a target to remove");
            }
        }

        // ─── File Move ────────────────────────────────────────────────────────

        private async void MoveToTarget(int targetIndex)
        {
            if (TargetsBox.Items.Count <= targetIndex)
            {
                UpdateStatus($"No target #{targetIndex + 1} configured");
                return;
            }
            if (_currentPlaylistIndex < 0 || _currentPlaylistIndex >= _currentPlaylist.Count) return;

            var src    = _currentPlaylist[_currentPlaylistIndex];
            var target = TargetsBox.Items[targetIndex]?.ToString();
            if (string.IsNullOrEmpty(target)) return;

            if (!Directory.Exists(target))
            {
                UpdateStatus($"Target folder not found: {target}");
                return;
            }

            var fileName = Path.GetFileName(src);
            var dest     = Path.Combine(target, fileName);

            // Handle filename conflicts with a timestamp suffix
            if (File.Exists(dest))
            {
                var stem = Path.GetFileNameWithoutExtension(src);
                var ext  = Path.GetExtension(src);
                dest = Path.Combine(target, $"{stem}_{DateTime.Now:yyyyMMddHHmmss}{ext}");
            }

            try
            {
                // Stop VLC to release the file handle before moving
                await _playerService.StopAsync();
                await Task.Delay(150);

                UpdateStatus($"Moving {fileName}…");

                int  moveIdx = _currentPlaylistIndex;
                int  nextIdx = moveIdx < _currentPlaylist.Count - 1
                                   ? moveIdx
                                   : Math.Max(0, moveIdx - 1);

                // Background move — supports cross-drive transfers
                await Task.Run(() =>
                {
                    bool sameDrive = string.Equals(
                        Path.GetPathRoot(src),
                        Path.GetPathRoot(dest),
                        StringComparison.OrdinalIgnoreCase);

                    if (sameDrive)
                    {
                        File.Move(src, dest);          // Same drive: instant rename
                    }
                    else
                    {
                        File.Copy(src, dest, overwrite: false);  // Cross-drive: copy …
                        File.Delete(src);                          // … then delete source
                    }
                });

                // Update playlist
                _currentPlaylist.RemoveAt(moveIdx);
                _currentPlaylistIndex = Math.Min(nextIdx, _currentPlaylist.Count - 1);
                RefreshPlaylist();
                UpdatePlayCounter();

                // Log to the sidebar Jobs panel (newest first)
                JobsBox.Items.Insert(0, $"✓  {Path.GetFileName(dest)}  →  {target}");
                UpdateStatus($"Moved to {target}");

                // Auto-play next if playlist is non-empty
                if (_currentPlaylist.Count > 0)
                    await PlayCurrentFile();
                else
                    UpdateStatus("Playlist empty");
            }
            catch (Exception ex)
            {
                UpdateStatus($"Move failed: {ex.Message}");
                Log($"MoveToTarget error: {ex}");
            }
        }

        // ─── Screenshot ───────────────────────────────────────────────────────

        private void Screenshot_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var mp = _vlcService.NativeMediaPlayer;
                if (mp == null || (!mp.IsPlaying && _vlcService.CurrentVideo == null))
                {
                    UpdateStatus("No video loaded for screenshot");
                    return;
                }

                var folder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
                    "PandaPlayer");
                Directory.CreateDirectory(folder);

                var file = Path.Combine(folder, $"shot_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.png");
                bool ok  = mp.TakeSnapshot(0, file, 0, 0);

                if (ok)
                {
                    FlashScreenshotOverlay();
                    UpdateStatus($"Screenshot saved: {file}");
                }
                else
                {
                    UpdateStatus("Screenshot failed (VLC returned false)");
                }
            }
            catch (Exception ex) { UpdateStatus($"Screenshot error: {ex.Message}"); }
        }

        /// <summary>Brief white-flash animation over the video to confirm screenshot capture.</summary>
        private void FlashScreenshotOverlay()
        {
            var anim = new DoubleAnimation
            {
                From     = 0.7,
                To       = 0.0,
                Duration = new Duration(TimeSpan.FromMilliseconds(350)),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            ScreenshotFlash.BeginAnimation(OpacityProperty, anim);
        }

        // ─── Go to Time ───────────────────────────────────────────────────────

        private void GotoTime_Click(object sender, RoutedEventArgs e)
        {
            var mp = _vlcService.NativeMediaPlayer;
            if (mp == null || (!mp.IsPlaying && !_vlcService.IsPaused))
            {
                UpdateStatus("No media loaded");
                return;
            }
            var input = ShowInputDialog(
                "Jump to time",
                "Format:  1:30  or  90  (seconds)  or  1:23:45",
                FormatMs(_vlcService.CurrentPositionMs));
            if (input == null) return;
            long ms = ParseTimeInput(input);
            if (ms < 0) { UpdateStatus("Invalid time format"); return; }
            mp.Time = ms;
            UpdateStatus($"Jumped to {FormatMs(ms)}");
        }

        // ─── Input Dialog ─────────────────────────────────────────────────────

        private string? ShowInputDialog(string title, string hint, string defaultValue)
        {
            var dlg = new Window
            {
                Title                  = title,
                Width                  = 360, Height = 165,
                WindowStartupLocation  = WindowStartupLocation.CenterOwner,
                Owner                  = this,
                ResizeMode             = ResizeMode.NoResize,
                Background             = new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x1E))
            };

            var sp = new StackPanel { Margin = new Thickness(16, 12, 16, 12) };
            sp.Children.Add(new TextBlock
            {
                Text       = hint,
                Foreground = Brushes.Gray,
                FontSize   = 11,
                Margin     = new Thickness(0, 0, 0, 6)
            });
            var tb = new TextBox
            {
                Text            = defaultValue ?? "",
                Background      = new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x33)),
                Foreground      = Brushes.White,
                BorderBrush     = Brushes.Gray,
                CaretBrush      = Brushes.White,
                Padding         = new Thickness(6, 4, 6, 4),
                FontSize        = 14,
                Margin          = new Thickness(0, 0, 0, 10)
            };
            tb.Loaded += (s, _) => { tb.Focus(); tb.SelectAll(); };
            sp.Children.Add(tb);

            var btnRow = new StackPanel
            {
                Orientation         = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            var btnOk = new Button
            {
                Content         = "OK", Width = 72, Height = 28, IsDefault = true,
                Margin          = new Thickness(0, 0, 6, 0),
                Background      = new SolidColorBrush(Color.FromRgb(0x00, 0x78, 0xD4)),
                Foreground      = Brushes.White,
                BorderThickness = new Thickness(0)
            };
            var btnCancel = new Button
            {
                Content         = "Cancel", Width = 72, Height = 28, IsCancel = true,
                Background      = new SolidColorBrush(Color.FromRgb(0x3A, 0x3A, 0x3A)),
                Foreground      = Brushes.White,
                BorderThickness = new Thickness(0)
            };
            btnOk.Click     += (s, _) => dlg.DialogResult = true;
            btnCancel.Click += (s, _) => dlg.DialogResult = false;
            btnRow.Children.Add(btnOk);
            btnRow.Children.Add(btnCancel);
            sp.Children.Add(btnRow);
            dlg.Content = sp;

            return dlg.ShowDialog() == true ? tb.Text : null;
        }

        private static long ParseTimeInput(string s)
        {
            s = s?.Trim() ?? "";
            var parts = s.Split(':');
            try
            {
                if (parts.Length == 1 && long.TryParse(s, out var sec)) return sec * 1000;
                if (parts.Length == 2)
                    return (int.Parse(parts[0]) * 60 + int.Parse(parts[1])) * 1000L;
                if (parts.Length == 3)
                    return (int.Parse(parts[0]) * 3600 + int.Parse(parts[1]) * 60 + int.Parse(parts[2])) * 1000L;
            }
            catch { }
            return -1;
        }

        // ─── Seek buttons ─────────────────────────────────────────────────────

        private void SeekFwd_Click(object sender, RoutedEventArgs e)  => SeekRelative(_seekJumpSeconds);
        private void SeekBack_Click(object sender, RoutedEventArgs e) => SeekRelative(-_seekJumpSeconds);

        private void SeekRelative(int seconds)
        {
            ShowFullscreenControls();
            var mp = _vlcService.NativeMediaPlayer;
            if (mp == null) return;
            long dur = mp.Length;
            if (dur > 0)
                mp.Time = Math.Max(0, Math.Min(dur, mp.Time + seconds * 1000L));
            else if (mp.IsPlaying)
                mp.Position = Math.Max(0f, Math.Min(1f, mp.Position + seconds / 300f));
            UpdateStatus($"Seek {(seconds > 0 ? "+" : "")}{seconds}s");
        }

        // ─── Volume helpers ───────────────────────────────────────────────────

        private void AdjustVolume(int delta)
        {
            ShowFullscreenControls();
            VolumeSlider.Value = Math.Max(0, Math.Min(100, (int)VolumeSlider.Value + delta));
        }

        private bool _isMuted;
        private void Mute_Click(object sender, RoutedEventArgs e)
        {
            _isMuted = !_isMuted;
            if (_vlcService?.NativeMediaPlayer != null)
                _vlcService.NativeMediaPlayer.Mute = _isMuted;
            MutePath.Data = (Geometry)FindResource(_isMuted ? "IcoMute" : "IcoVolume");
            UpdateStatus(_isMuted ? "Muted" : "Unmuted");
        }

        // ─── Fullscreen ───────────────────────────────────────────────────────

        private void Fullscreen_Click(object sender, RoutedEventArgs e) => ToggleFullscreen();

        private void Window_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (!_isFullscreen) return;

            // Only show controls if mouse is near bottom
            var pos = e.GetPosition(this);
            if (pos.Y >= ActualHeight - 110)
            {
                ShowFullscreenControls();
            }
            else if (SeekRow.Visibility == Visibility.Visible)
            {
                // Reset auto-hide timer if mouse is not at bottom
                _autoHideTimer?.Stop();
                _autoHideTimer?.Start();
            }
        }

        private void ShowFullscreenControls()
        {
            if (!_isFullscreen) return;
            if (SeekRow.Visibility != Visibility.Visible)
            {
                SeekRow.Visibility      = Visibility.Visible;
                TransportBar.Visibility = Visibility.Visible;
            }
            _autoHideTimer?.Stop();
            _autoHideTimer?.Start();
        }

        private void ToggleFullscreen()
        {
            if (!_isFullscreen)
            {
                _prevWindowState = WindowState;
                _prevWindowStyle = WindowStyle;
                _prevPanelWidth  = RightPanelColumn.Width;

                WindowStyle              = WindowStyle.None;
                WindowState              = WindowState.Maximized;
                MainMenu.Visibility      = Visibility.Collapsed;
                MainStatusBar.Visibility = Visibility.Collapsed;
                SeekRow.Visibility       = Visibility.Collapsed;
                TransportBar.Visibility  = Visibility.Collapsed;
                MainSplitter.Visibility  = Visibility.Collapsed;
                RightPanelColumn.Width   = new GridLength(0, GridUnitType.Pixel);
                _isFullscreen            = true;

                // Mouse-position polling (100ms) to detect bottom-edge proximity
                if (_fullscreenMouseTimer == null)
                {
                    _fullscreenMouseTimer = new DispatcherTimer
                    {
                        Interval = TimeSpan.FromMilliseconds(100)
                    };
                    _fullscreenMouseTimer.Tick += (s, _) =>
                    {
                        if (!_isFullscreen) return;
                        var cur    = WinForms.Cursor.Position;
                        var screen = WinForms.Screen.FromPoint(cur);
                        
                        // Use screen-relative coordinates for bottom detection
                        bool atBottom = cur.Y >= screen.Bounds.Bottom - 110;
                        
                        if (atBottom) 
                        {
                            ShowFullscreenControls();
                        }
                        
                        _lastMousePos = cur;
                    };
                }
                _lastMousePos = WinForms.Cursor.Position;
                _fullscreenMouseTimer.Start();

                // Auto-hide timer (1.5s after last show)
                if (_autoHideTimer == null)
                {
                    _autoHideTimer = new DispatcherTimer
                    {
                        Interval = TimeSpan.FromSeconds(1.5)
                    };
                    _autoHideTimer.Tick += (s, _) =>
                    {
                        _autoHideTimer.Stop();
                        if (!_isFullscreen) return;

                        var cur    = WinForms.Cursor.Position;
                        var screen = WinForms.Screen.FromPoint(cur);
                        
                        // If mouse is still at the bottom, don't hide yet
                        if (cur.Y < screen.Bounds.Bottom - 110)
                        {
                            SeekRow.Visibility      = Visibility.Collapsed;
                            TransportBar.Visibility = Visibility.Collapsed;
                        }
                        else
                        {
                            _autoHideTimer.Start(); 
                        }
                    };
                }
                UpdateStatus("Fullscreen — press F or double-click to exit");
            }
            else
            {
                _fullscreenMouseTimer?.Stop();
                _autoHideTimer?.Stop();

                WindowStyle              = _prevWindowStyle;
                WindowState              = _prevWindowState;
                MainMenu.Visibility      = Visibility.Visible;
                MainStatusBar.Visibility = Visibility.Visible;
                SeekRow.Visibility       = Visibility.Visible;
                TransportBar.Visibility  = Visibility.Visible;
                MainSplitter.Visibility  = Visibility.Visible;
                RightPanelColumn.Width   = _sidebarVisible ? _prevPanelWidth : new GridLength(0);
                _isFullscreen            = false;
                UpdateStatus("Windowed");
            }
        }

        // ─── Win32 hook: intercept clicks on VLC's native HWND ───────────────
        // VLC renders to a native Win32 child window; WPF can't receive mouse events
        // directly from it (airspace limitation). WM_PARENTNOTIFY bridges the gap.

        private const int WM_PARENTNOTIFY = 0x0210;
        private const int WM_LBUTTONDOWN  = 0x0201;
        private const int WM_RBUTTONDOWN  = 0x0204;

        [DllImport("user32.dll")] private static extern uint GetDoubleClickTime();

        private DateTime _lastVideoLeftClick = DateTime.MinValue;

        private IntPtr MainWndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg != WM_PARENTNOTIFY) return IntPtr.Zero;

            int   eventId  = wParam.ToInt32() & 0xFFFF;
            int   lp       = lParam.ToInt32();
            short cx       = (short)(lp & 0xFFFF);
            short cy       = (short)((lp >> 16) & 0xFFFF);
            var   clientPt = new System.Windows.Point(cx, cy);

            var videoPos  = VideoView.TranslatePoint(new System.Windows.Point(0, 0), this);
            var videoRect = new Rect(videoPos, new System.Windows.Size(VideoView.ActualWidth, VideoView.ActualHeight));

            if (eventId == WM_LBUTTONDOWN && videoRect.Contains(clientPt))
            {
                var now     = DateTime.UtcNow;
                var elapsed = (now - _lastVideoLeftClick).TotalMilliseconds;
                _lastVideoLeftClick = now;

                if (elapsed <= GetDoubleClickTime())
                {
                    _lastVideoLeftClick = DateTime.MinValue;
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        if (_currentPlaylist.Count == 0)
                            OpenFolder_Click(null!, null!);
                        else
                            ToggleFullscreen();
                    }));
                    handled = true;
                }
            }
            else if (eventId == WM_RBUTTONDOWN && videoRect.Contains(clientPt))
            {
                // Use DispatcherPriority.Input so VLC has released mouse before we open the menu
                Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(ShowVideoContextMenu));
                handled = true;
            }

            return IntPtr.Zero;
        }

        private void ShowVideoContextMenu()
        {
            VideoContextMenu.Items.Clear();
            BuildVideoContextMenuItems(VideoContextMenu);
            // PlacementTarget must be a WPF element, not the VLC HWND surface.
            // Activate() ensures the WPF window holds focus so the menu's
            // mouse-capture works and it doesn't immediately close.
            VideoContextMenu.Placement       = PlacementMode.MousePoint;
            VideoContextMenu.PlacementTarget = VideoOverlay;
            Activate();
            VideoContextMenu.IsOpen          = true;
        }

        private void VideoOverlay_MouseLeftButtonDown(
            object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ClickCount != 2) return;
            if (_currentPlaylist.Count == 0)
                OpenFolder_Click(null!, null!);
            else
                ToggleFullscreen();
            e.Handled = true;
        }

        private void VideoOverlay_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            VideoContextMenu.Items.Clear();
            BuildVideoContextMenuItems(VideoContextMenu);
        }

        private void BuildVideoContextMenuItems(ContextMenu menu)
        {
            menu.Items.Add(Mni(
                _vlcService.IsPlaying ? "⏸  Pause" : "▶  Play",
                (_, _) => PlayPause_Click(null!, null!), "Space"));
            menu.Items.Add(new Separator());

            // Move-to sub-menu
            var moveMenu = new MenuItem { Header = "📁  Move to…" };
            if (TargetsBox.Items.Count == 0)
                moveMenu.Items.Add(new MenuItem { Header = "(no targets — add via Tools)", IsEnabled = false });
            else
                for (int i = 0; i < TargetsBox.Items.Count; i++)
                {
                    int idx = i;
                    moveMenu.Items.Add(Mni($"{i + 1}: {TargetsBox.Items[i]}", (_, _) => MoveToTarget(idx), $"Ctrl+{i + 1}"));
                }
            menu.Items.Add(moveMenu);
            menu.Items.Add(new Separator());

            menu.Items.Add(Mni("📷  Screenshot",   (_, _) => Screenshot_Click(null!, null!), "Ctrl+S"));
            menu.Items.Add(Mni("📂  Open Folder…", (_, _) => OpenFolder_Click(null!, null!), "Ctrl+F"));
            menu.Items.Add(new Separator());
            menu.Items.Add(Mni("⏮  Previous", (_, _) => Previous_Click(null!, null!), "P"));
            menu.Items.Add(Mni("⏭  Next",     (_, _) => Next_Click(null!, null!), "N"));
        }

        private void PlaylistBox_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            PlaylistContextMenu.Items.Clear();
            PlaylistContextMenu.Items.Add(Mni("▶  Play selected", (_, _) =>
            {
                if (PlaylistBox.SelectedIndex >= 0)
                {
                    _currentPlaylistIndex = PlaylistBox.SelectedIndex;
                    _ = PlayCurrentFile();
                }
            }));
            PlaylistContextMenu.Items.Add(new Separator());

            if (TargetsBox.Items.Count == 0)
            {
                PlaylistContextMenu.Items.Add(new MenuItem
                    { Header = "(no targets — add via Tools)", IsEnabled = false });
            }
            else
            {
                for (int i = 0; i < TargetsBox.Items.Count; i++)
                {
                    int idx = i;
                    PlaylistContextMenu.Items.Add(Mni(
                        $"📁  Move to {i + 1}: {TargetsBox.Items[i]}",
                        (_, _) => MoveToTarget(idx), $"Ctrl+{i + 1}"));
                }
            }
        }

        private static MenuItem Mni(string header, RoutedEventHandler click, string? gesture = null)
        {
            var mi = new MenuItem { Header = header };
            if (gesture != null) mi.InputGestureText = gesture;
            mi.Click += click;
            return mi;
        }

        // ─── Keyboard Shortcuts ───────────────────────────────────────────────

        private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            // Don't intercept when user is typing in a text box
            if (Keyboard.FocusedElement is System.Windows.Controls.TextBox) return;

            var mods = Keyboard.Modifiers;
            foreach (var binding in _keyBindingService.Bindings)
            {
                if (!_keyBindingService.Matches(binding, e.Key, mods)) continue;
                ExecuteAction(binding.ActionId);
                e.Handled = true;
                return;
            }
        }

        private void ExecuteAction(string actionId)
        {
            switch (actionId)
            {
                case "PlayPause":   PlayPause_Click(null!, null!);  break;
                case "Stop":
                    if (_isFullscreen) ToggleFullscreen();
                    else Stop_Click(null!, null!);
                    break;
                case "Next":        Next_Click(null!, null!);       break;
                case "Previous":    Previous_Click(null!, null!);   break;
                case "SeekFwd":     SeekRelative(_seekJumpSeconds); break;
                case "SeekBack":    SeekRelative(-_seekJumpSeconds);break;
                case "VolUp":       AdjustVolume(10);               break;
                case "VolDown":     AdjustVolume(-10);              break;
                case "Mute":        Mute_Click(null!, null!);       break;
                case "Fullscreen":  ToggleFullscreen();             break;
                case "Screenshot":  Screenshot_Click(null!, null!); break;
                case "Sidebar":     ToggleSidebar_Click(null!, null!); break;
                case "GotoTime":    GotoTime_Click(null!, null!);   break;
                default:
                    if (actionId.StartsWith("MoveTarget") &&
                        int.TryParse(actionId.Substring(10), out int n))
                    {
                        MoveToTarget(n - 1);
                    }
                    break;
            }
        }

        // ─── Folder Loading & Playlist ────────────────────────────────────────

        private void LoadVideosFromFolder(string folderPath)
        {
            if (string.IsNullOrEmpty(folderPath)) return;
            Log($"Scanning: {folderPath}");

            var exts = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                { ".mp4", ".avi", ".mkv", ".mov", ".flv", ".wmv", ".webm", ".m4v", ".ts", ".m2ts" };

            List<string> files;
            try
            {
                files = Directory
                    .GetFiles(folderPath, "*.*", SearchOption.AllDirectories)
                    .Where(f => exts.Contains(Path.GetExtension(f)))
                    .OrderBy(f => f)
                    .ToList();
            }
            catch (Exception ex)
            {
                UpdateStatus($"Error scanning folder: {ex.Message}");
                return;
            }

            if (files.Count == 0)
            {
                UpdateStatus("No video files found in selected folder");
                return;
            }

            // Save state of previous folder
            if (!string.IsNullOrEmpty(_currentFolderPath) && _playedCount > 0)
                _folderStore.SaveState(_currentFolderPath, _playedCount, _shufflePlayed);

            // Switch to new folder
            _currentFolderPath    = folderPath;
            _currentPlaylist      = files;
            _currentPlaylistIndex = 0;
            _shufflePlayed.Clear();

            // Restore persisted progress
            var state = _folderStore.GetState(folderPath);
            if (state?.PlayedFiles != null && state.PlayedFiles.Any())
            {
                _playedCount = state.PlayedCount;
                foreach (var fn in state.PlayedFiles) _shufflePlayed.Add(fn);
                Log($"Restored {_playedCount} played items");
            }
            else
            {
                _playedCount = 0;
            }

            UpdatePlayCounter();
            RefreshPlaylist();

            Title = $"{Path.GetFileName(folderPath)} — Panda Player";
            _ = PlayCurrentFile();
            UpdateStatus($"Loaded {files.Count} video(s) · {_playedCount} previously played");
        }

        private void RefreshPlaylist()
        {
            PlaylistBox.Items.Clear();
            foreach (var f in _currentPlaylist)
                PlaylistBox.Items.Add(Path.GetFileName(f));
            int c = _currentPlaylist.Count;
            PlaylistCountText.Text = $"{c} video{(c == 1 ? "" : "s")}";
        }

        // ─── App Lifecycle ────────────────────────────────────────────────────

        private void Quit_Click(object sender, RoutedEventArgs e) => Close();

        protected override void OnClosed(EventArgs e)
        {
            // Save folder playback state
            if (!string.IsNullOrEmpty(_currentFolderPath))
                _folderStore?.SaveState(_currentFolderPath, _playedCount, _shufflePlayed);

            // Save app settings (volume, mode, targets, seek jump)
            try
            {
                _settingsStore?.SaveSettingsAsync(CollectCurrentSettings()).GetAwaiter().GetResult();
                Log("Settings saved on close");
            }
            catch (Exception ex) { Log($"SaveSettings error: {ex.Message}"); }

            _positionTimer?.Stop();
            _fullscreenMouseTimer?.Stop();
            _autoHideTimer?.Stop();
            _playerService?.Dispose();
            base.OnClosed(e);
        }

        // ─── Status & Logging ─────────────────────────────────────────────────

        private void UpdateStatus(string msg)
        {
            StatusText.Text = msg;
            Log($"[Status] {msg}");
        }

        private void Log(string message)
        {
            var method = typeof(App).GetMethod("LogMessage",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            method?.Invoke(null, new object[] { message });
        }

        // ─── Win32: Dark title bar ────────────────────────────────────────────

        [DllImport("dwmapi.dll", PreserveSig = false)]
        private static extern void DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        private static void EnableDarkTitleBar(IntPtr hwnd)
        {
            try
            {
                int v = 1;
                DwmSetWindowAttribute(hwnd, 20 /* DWMWA_USE_IMMERSIVE_DARK_MODE */, ref v, sizeof(int));
            }
            catch { /* older Windows — gracefully ignore */ }
        }
    }
}
