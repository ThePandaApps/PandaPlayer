using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Windows.Input;
using CodexPlayer.UI.Models;

namespace CodexPlayer.UI.Services
{
    public class KeyBindingService
    {
        private List<PlayerKeyBinding> _bindings = new List<PlayerKeyBinding>();
        private readonly string _path;

        public KeyBindingService(string appDataPath)
        {
            _path = Path.Combine(appDataPath, "keybindings.json");
            Load();
        }

        public List<PlayerKeyBinding> Bindings => _bindings;

        public string GetShortcutText(string actionId)
        {
            var binding = _bindings.Find(b => b.ActionId == actionId);
            return binding?.ToString() ?? "";
        }
        
        public bool Matches(PlayerKeyBinding binding, Key key, ModifierKeys mods)
        {
            return binding.Key == key && binding.Modifiers == mods;
        }

        private void Load()
        {
            if (_bindings == null) _bindings = new List<PlayerKeyBinding>();
            try
            {
                if (File.Exists(_path))
                {
                    string json = File.ReadAllText(_path);
                    var loaded = JsonSerializer.Deserialize<List<PlayerKeyBinding>>(json);
                    if (loaded != null) _bindings = loaded;
                }
            }
            catch { }

            if (_bindings == null || _bindings.Count == 0)
                _bindings = GetDefaults();
            
            SyncWithDefaults();
        }

        public void Save()
        {
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(_bindings, options);
                File.WriteAllText(_path, json);
            }
            catch { }
        }

        private void SyncWithDefaults()
        {
            var defaults = GetDefaults();
            foreach (var def in defaults)
            {
                if (!_bindings.Exists(b => b.ActionId == def.ActionId))
                {
                    _bindings.Add(def);
                }
            }
        }

        private List<PlayerKeyBinding> GetDefaults()
        {
            return new List<PlayerKeyBinding>
            {
                new PlayerKeyBinding { ActionId = "PlayPause", Description = "Play / Pause", Key = Key.Space, Modifiers = ModifierKeys.None },
                new PlayerKeyBinding { ActionId = "Stop", Description = "Stop Playback", Key = Key.Escape, Modifiers = ModifierKeys.None },
                new PlayerKeyBinding { ActionId = "Next", Description = "Next Video", Key = Key.N, Modifiers = ModifierKeys.None },
                new PlayerKeyBinding { ActionId = "Previous", Description = "Previous Video", Key = Key.P, Modifiers = ModifierKeys.None },
                
                new PlayerKeyBinding { ActionId = "SeekFwd", Description = "Seek Forward", Key = Key.Right, Modifiers = ModifierKeys.None },
                new PlayerKeyBinding { ActionId = "SeekBack", Description = "Seek Backward", Key = Key.Left, Modifiers = ModifierKeys.None },
                
                new PlayerKeyBinding { ActionId = "VolUp", Description = "Volume Up", Key = Key.Up, Modifiers = ModifierKeys.None },
                new PlayerKeyBinding { ActionId = "VolDown", Description = "Volume Down", Key = Key.Down, Modifiers = ModifierKeys.None },
                new PlayerKeyBinding { ActionId = "Mute", Description = "Mute / Unmute", Key = Key.M, Modifiers = ModifierKeys.None },
                
                new PlayerKeyBinding { ActionId = "Fullscreen", Description = "Toggle Fullscreen", Key = Key.F, Modifiers = ModifierKeys.None },
                new PlayerKeyBinding { ActionId = "Screenshot", Description = "Take Screenshot", Key = Key.S, Modifiers = ModifierKeys.Control },
                new PlayerKeyBinding { ActionId = "Sidebar", Description = "Toggle Sidebar", Key = Key.L, Modifiers = ModifierKeys.Control },
                new PlayerKeyBinding { ActionId = "GotoTime", Description = "Go to Time...", Key = Key.G, Modifiers = ModifierKeys.Control },

                // Move Targets (Ctrl+1..Ctrl+9)
                new PlayerKeyBinding { ActionId = "MoveTarget1", Description = "Move to Target 1", Key = Key.D1, Modifiers = ModifierKeys.Control },
                new PlayerKeyBinding { ActionId = "MoveTarget2", Description = "Move to Target 2", Key = Key.D2, Modifiers = ModifierKeys.Control },
                new PlayerKeyBinding { ActionId = "MoveTarget3", Description = "Move to Target 3", Key = Key.D3, Modifiers = ModifierKeys.Control },
                new PlayerKeyBinding { ActionId = "MoveTarget4", Description = "Move to Target 4", Key = Key.D4, Modifiers = ModifierKeys.Control },
                new PlayerKeyBinding { ActionId = "MoveTarget5", Description = "Move to Target 5", Key = Key.D5, Modifiers = ModifierKeys.Control },
                new PlayerKeyBinding { ActionId = "MoveTarget6", Description = "Move to Target 6", Key = Key.D6, Modifiers = ModifierKeys.Control },
                new PlayerKeyBinding { ActionId = "MoveTarget7", Description = "Move to Target 7", Key = Key.D7, Modifiers = ModifierKeys.Control },
                new PlayerKeyBinding { ActionId = "MoveTarget8", Description = "Move to Target 8", Key = Key.D8, Modifiers = ModifierKeys.Control },
                new PlayerKeyBinding { ActionId = "MoveTarget9", Description = "Move to Target 9", Key = Key.D9, Modifiers = ModifierKeys.Control },
            };
        }
    }
}
