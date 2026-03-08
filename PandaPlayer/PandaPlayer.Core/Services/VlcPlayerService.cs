using System;
using System.IO;
using System.Threading.Tasks;
using PandaPlayer.Core.Models;
using PandaPlayer.Core.Events;
using LibVLCSharp.Shared;
using System.Runtime.InteropServices;

namespace PandaPlayer.Core.Services
{
    /// <summary>
    /// VLC-based implementation of the player service.
    /// Handles playback of video files using LibVLCSharp.
    /// </summary>
    public class VlcPlayerService : IPlayerService, IDisposable
    {
        private static bool _coreInitialized = false;
        private LibVLC _libVLC;
        private MediaPlayer _mediaPlayer;
        private VideoItem _currentVideo;
        private bool _isInitialized = false;
        private bool _disposed = false;

        public event EventHandler<PlaybackStartedEventArgs> PlaybackStarted;
        public event EventHandler<PlaybackPausedEventArgs> PlaybackPaused;
        public event EventHandler<PlaybackEndedEventArgs> PlaybackEnded;
        public event EventHandler<PlaybackErrorEventArgs> PlaybackError;
        public event EventHandler<SeekEventArgs> PositionChanged;

        public VideoItem CurrentVideo => _currentVideo;
        public bool IsPlaying => _mediaPlayer?.IsPlaying ?? false;
        public bool IsPaused => _mediaPlayer?.IsPlaying == false && _currentVideo != null;
        public long CurrentPositionMs => _mediaPlayer?.Time ?? 0;
        public long DurationMs => _mediaPlayer?.Media != null ? (long)_mediaPlayer.Media.Duration : 0;

        /// <summary>Expose the native MediaPlayer so the WPF VideoView can attach to it.</summary>
        public MediaPlayer NativeMediaPlayer => _mediaPlayer;

        public float Volume
        {
            get => _mediaPlayer != null ? _mediaPlayer.Volume / 100f : 1f;
            set { if (_mediaPlayer != null) _mediaPlayer.Volume = (int)(value * 100); }
        }

        public bool IsFullscreen
        {
            get => _mediaPlayer?.Fullscreen ?? false;
            set
            {
                if (_mediaPlayer != null)
                    _mediaPlayer.Fullscreen = value;
            }
        }

        public VlcPlayerService()
        {
            Initialize();
        }

        private void Initialize()
        {
            try
            {
                WriteLog("VlcPlayerService.Initialize() starting");
                
                // Initialize LibVLC core - ensure path to native libraries is set
                if (!_coreInitialized)
                {
                    WriteLog("LibVLC core not initialized, setting up VLC path");
                    var vlcPath = FindVlcPath();
                    WriteLog($"VLC path resolved to: {vlcPath ?? "NOT FOUND"}");
                    
                    if (!string.IsNullOrEmpty(vlcPath))
                    {
                        WriteLog("Calling Core.Initialize with VLC path");
                        try
                        {
                            LibVLCSharp.Shared.Core.Initialize(vlcPath);
                            WriteLog("Core.Initialize succeeded");
                        }
                        catch (Exception ex)
                        {
                            WriteLog($"Core.Initialize failed: {ex.Message}, trying without path");
                            LibVLCSharp.Shared.Core.Initialize();
                        }
                    }
                    else
                    {
                        WriteLog("No VLC path found, calling Core.Initialize without path");
                        LibVLCSharp.Shared.Core.Initialize();
                    }
                    
                    _coreInitialized = true;
                }

                // Initialize LibVLC with reasonable defaults for Windows
                WriteLog("Creating new LibVLC instance");
                _libVLC = new LibVLC();
                WriteLog("LibVLC instance created successfully");
                
                WriteLog("Creating MediaPlayer");
                _mediaPlayer = new MediaPlayer(_libVLC);
                WriteLog("MediaPlayer created successfully");

                // Wire up basic event handlers if available
                _mediaPlayer.EndReached += (s, e) =>
                {
                    PlaybackEnded?.Invoke(this,
                        new PlaybackEndedEventArgs(_currentVideo));
                };
                _isInitialized = true;
                WriteLog("VlcPlayerService initialization completed successfully");
            }
            catch (Exception ex)
            {
                WriteLog($"ERROR in Initialize: {ex.GetType().Name}: {ex.Message}");
                WriteLog($"Stack trace: {ex.StackTrace}");
                
                PlaybackError?.Invoke(this, new PlaybackErrorEventArgs
                {
                    ErrorMessage = "Failed to initialize LibVLC: " + ex.Message,
                    Exception = ex
                });
                
                throw; // Re-throw so UI can catch it
            }
        }

        private static void WriteLog(string message)
        {
            try
            {
                var logDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "PandaPlayer", "logs");
                
                if (!Directory.Exists(logDir))
                    Directory.CreateDirectory(logDir);
                
                var logPath = Path.Combine(logDir, $"vlc_{DateTime.Now:yyyy-MM-dd}.log");
                var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
                var logEntry = $"[{timestamp}] {message}";
                
                File.AppendAllText(logPath, logEntry + Environment.NewLine);
            }
            catch { }
        }


        private string FindVlcPath()
        {
            try
            {
                WriteLog("FindVlcPath() starting");
                var appDir = AppDomain.CurrentDomain.BaseDirectory;
                WriteLog($"App directory: {appDir}");
                
                // Check for libvlc\win-x64 structure (LibVLCSharp.Forms standard)
                var libvlcWinPath = Path.Combine(appDir, "libvlc", "win-x64");
                var libvlcDllPath = Path.Combine(libvlcWinPath, "libvlc.dll");
                WriteLog($"Checking libvlc\\win-x64: {libvlcDllPath} - exists: {File.Exists(libvlcDllPath)}");
                if (Directory.Exists(libvlcWinPath) && File.Exists(libvlcDllPath))
                {
                    WriteLog("Found libvlc\\win-x64 structure, returning that path");
                    return libvlcWinPath;
                }

                // Check if libvlc.dll is directly in app directory
                var libvlcDllDirect = Path.Combine(appDir, "libvlc.dll");
                WriteLog($"Checking direct libvlc.dll: {libvlcDllDirect} - exists: {File.Exists(libvlcDllDirect)}");
                if (File.Exists(libvlcDllDirect))
                {
                    WriteLog("Found libvlc.dll directly in app folder");
                    return appDir;
                }
                
                // Check for VLC folder in application directory
                var localVlcPath = Path.Combine(appDir, "vlc");
                var localVlcDll = Path.Combine(localVlcPath, "libvlc.dll");
                WriteLog($"Checking vlc folder: {localVlcPath} - libvlc.dll exists: {File.Exists(localVlcDll)}");
                if (Directory.Exists(localVlcPath) && File.Exists(localVlcDll))
                {
                    WriteLog("Found vlc/libvlc.dll");
                    return localVlcPath;
                }

                // Check Program Files
                var programFilesPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                    "VideoLAN", "VLC");
                var programFilesVlcDll = Path.Combine(programFilesPath, "libvlc.dll");
                WriteLog($"Checking Program Files: {programFilesPath} - libvlc.dll exists: {File.Exists(programFilesVlcDll)}");
                if (Directory.Exists(programFilesPath) && File.Exists(programFilesVlcDll))
                {
                    WriteLog("Found Program Files VLC");
                    return programFilesPath;
                }

                // Check Program Files (x86)
                var programFilesX86 = Environment.GetEnvironmentVariable("ProgramFiles(x86)");
                WriteLog($"Program Files (x86): {programFilesX86}");
                if (!string.IsNullOrEmpty(programFilesX86))
                {
                    var vlcX86Path = Path.Combine(programFilesX86, "VideoLAN", "VLC");
                    var vlcX86Dll = Path.Combine(vlcX86Path, "libvlc.dll");
                    WriteLog($"Checking Program Files (x86): {vlcX86Path} - libvlc.dll exists: {File.Exists(vlcX86Dll)}");
                    if (Directory.Exists(vlcX86Path) && File.Exists(vlcX86Dll))
                    {
                        WriteLog("Found Program Files (x86) VLC");
                        return vlcX86Path;
                    }
                }

                WriteLog("VLC path not found in any checked location");
                return null;
            }
            catch (Exception ex)
            {
                WriteLog($"ERROR in FindVlcPath: {ex.Message}");
                return null;
            }
        }

        public async Task PlayAsync(VideoItem video)
        {
            if (!_isInitialized || video == null)
                return;

            try
            {
                _currentVideo = video;

                if (!File.Exists(video.FilePath))
                    throw new FileNotFoundException($"Video file not found: {video.FilePath}");

                var media = new Media(_libVLC, new Uri(video.FilePath, UriKind.Absolute));
                _mediaPlayer.Play(media);

                await Task.Delay(100);
                PlaybackStarted?.Invoke(this, new PlaybackStartedEventArgs(video));
            }
            catch (Exception ex)
            {
                PlaybackError?.Invoke(this, new PlaybackErrorEventArgs
                {
                    ErrorMessage = $"Failed to play video: {video.FileName}",
                    Exception = ex
                });
            }
        }

        public async Task PauseAsync()
        {
            if (_mediaPlayer?.IsPlaying == true)
            {
                _mediaPlayer.Pause();
                await Task.Delay(100);
            }
        }

        public async Task ResumeAsync()
        {
            if (_mediaPlayer?.IsPlaying == false && _currentVideo != null)
            {
                _mediaPlayer.Play();
                await Task.Delay(100);
            }
        }

        public async Task StopAsync()
        {
            if (_mediaPlayer != null)
            {
                _mediaPlayer.Stop();
                await Task.Delay(100);
            }
        }

        public async Task SeekAsync(long positionMs)
        {
            if (_mediaPlayer != null)
            {
                _mediaPlayer.Time = (long)(positionMs);
                await Task.Delay(50);
                PositionChanged?.Invoke(this, new SeekEventArgs { NewPositionMs = positionMs });
            }
        }

        public async Task NextAsync()
        {
            await Task.CompletedTask;
            // Handled by session service
        }

        public async Task PreviousAsync()
        {
            await Task.CompletedTask;
            // Handled by session service
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            try
            {
                _mediaPlayer?.Dispose();
                _libVLC?.Dispose();
            }
            catch { }

            _disposed = true;
        }
    }
}
