using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;
using System.Windows.Media;
using CodexPlayer.UI.Models;
using CodexPlayer.UI.Services;

namespace CodexPlayer.UI.Views
{
    public partial class KeyBindingsWindow : Window
    {
        private readonly KeyBindingService  _service;
        private List<PlayerKeyBinding>      _viewList = new();

        public KeyBindingsWindow(KeyBindingService service)
        {
            InitializeComponent();
            _service = service;
            Reload();
        }

        private void Reload()
        {
            _viewList                 = _service.Bindings.OrderBy(b => b.Description).ToList();
            BindingsGrid.ItemsSource  = _viewList;
        }

        // ─── Grid double-click ────────────────────────────────────────────────

        private void BindingsGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (BindingsGrid.SelectedItem is PlayerKeyBinding binding)
                EditBinding(binding);
        }

        // ─── Edit dialog ──────────────────────────────────────────────────────

        private void EditBinding(PlayerKeyBinding binding)
        {
            Key          recordedKey  = Key.None;
            ModifierKeys recordedMods = ModifierKeys.None;

            // ── Build the dialog ─────────────────────────────────────────────
            var dlg = new Window
            {
                Title                 = $"Reassign: {binding.Description}",
                Width                 = 400, Height = 200,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner                 = this,
                ResizeMode            = ResizeMode.NoResize,
                ShowInTaskbar         = false,
                Background            = new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x1E))
            };

            var sp = new StackPanel { Margin = new Thickness(24, 20, 24, 20) };

            var hint = new TextBlock
            {
                Text              = "Press the key combination you want to assign.\n" +
                                    "Press  Esc  to cancel  ·  Backspace  to clear the shortcut.",
                TextAlignment     = TextAlignment.Center,
                FontSize          = 12,
                Foreground        = new SolidColorBrush(Color.FromRgb(0x90, 0x90, 0x90)),
                TextWrapping      = TextWrapping.Wrap,
                Margin            = new Thickness(0, 0, 0, 16)
            };

            // Live display of the currently-pressed combo
            var display = new TextBlock
            {
                Text          = "—",
                FontSize      = 26,
                FontWeight    = FontWeights.Bold,
                TextAlignment = TextAlignment.Center,
                Foreground    = new SolidColorBrush(Color.FromRgb(0x00, 0x78, 0xD4))
            };

            sp.Children.Add(hint);
            sp.Children.Add(display);
            dlg.Content = sp;

            // ── Key capture handler ──────────────────────────────────────────
            dlg.KeyDown += (s, e) =>
            {
                e.Handled = true;

                // Cancel
                if (e.Key == Key.Escape) { dlg.DialogResult = false; return; }

                // Clear binding
                if (e.Key is Key.Back or Key.Delete)
                {
                    recordedKey  = Key.None;
                    recordedMods = ModifierKeys.None;
                    display.Text = "(none)";
                    dlg.DialogResult = true;
                    return;
                }

                // Ignore pure modifier keys
                if (e.Key is Key.LeftCtrl or Key.RightCtrl
                         or Key.LeftAlt  or Key.RightAlt  or Key.System
                         or Key.LeftShift or Key.RightShift
                         or Key.LWin or Key.RWin)
                {
                    // Update display to show current modifiers
                    display.Text = ModsToString(Keyboard.Modifiers) is { Length: > 0 } m
                        ? m + "+…"
                        : "—";
                    return;
                }

                recordedKey  = e.Key;
                recordedMods = Keyboard.Modifiers;

                // Show the captured combo immediately
                display.Text = FormatCombo(recordedKey, recordedMods);

                dlg.DialogResult = true;
            };

            // Also update display on KeyUp (shows mods being held before a key is pressed)
            dlg.KeyUp += (s, e) =>
            {
                if (recordedKey == Key.None)
                    display.Text = "—";
            };

            dlg.Loaded += (s, _) => dlg.Focus();

            // ── Process result ───────────────────────────────────────────────
            if (dlg.ShowDialog() != true) return;

            if (recordedKey != Key.None)
            {
                // Conflict check
                var conflict = _service.Bindings.FirstOrDefault(b =>
                    b.ActionId != binding.ActionId &&
                    b.Key      == recordedKey      &&
                    b.Modifiers == recordedMods);

                if (conflict != null)
                {
                    MessageBox.Show(
                        $"\"{FormatCombo(recordedKey, recordedMods)}\" is already used by:\n\n  {conflict.Description}",
                        "Shortcut Conflict", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }

            binding.Key       = recordedKey;
            binding.Modifiers = recordedMods;
            _service.Save();
            BindingsGrid.Items.Refresh();
        }

        // ─── Reset / Close ────────────────────────────────────────────────────

        private void Reset_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show(
                    "Reset all keyboard shortcuts to their defaults?",
                    "Reset Shortcuts",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question) != MessageBoxResult.Yes) return;

            var path = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "CodexPlayer", "keybindings.json");

            try { File.Delete(path); } catch { }

            // Reload service defaults in-memory
            var fresh = new KeyBindingService(
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CodexPlayer"));

            _service.Bindings.Clear();
            _service.Bindings.AddRange(fresh.Bindings);
            Reload();
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();

        // ─── Formatting helpers ───────────────────────────────────────────────

        private static string ModsToString(ModifierKeys mods)
        {
            var parts = new List<string>();
            if (mods.HasFlag(ModifierKeys.Control)) parts.Add("Ctrl");
            if (mods.HasFlag(ModifierKeys.Alt))     parts.Add("Alt");
            if (mods.HasFlag(ModifierKeys.Shift))   parts.Add("Shift");
            return string.Join("+", parts);
        }

        private static string FormatCombo(Key key, ModifierKeys mods)
        {
            var m = ModsToString(mods);
            var k = key.ToString();
            return m.Length > 0 ? $"{m}+{k}" : k;
        }
    }
}
