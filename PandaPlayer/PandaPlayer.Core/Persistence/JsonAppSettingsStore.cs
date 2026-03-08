using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using PandaPlayer.Core.Models;
using Newtonsoft.Json;

namespace PandaPlayer.Core.Persistence
{
    /// <summary>
    /// JSON-based implementation for persisting application settings.
    /// </summary>
    public class JsonAppSettingsStore : IAppSettingsStore
    {
        private readonly string _settingsPath;

        public JsonAppSettingsStore(string appDataFolderPath = null)
        {
            if (string.IsNullOrEmpty(appDataFolderPath))
                appDataFolderPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PandaPlayer");

            if (!Directory.Exists(appDataFolderPath))
                Directory.CreateDirectory(appDataFolderPath);

            _settingsPath = Path.Combine(appDataFolderPath, "settings.json");
        }

        public async Task<PlaybackSettings> LoadSettingsAsync()
        {
            try
            {
                if (!File.Exists(_settingsPath))
                    return GetDefaultSettings();

                string json = await File.ReadAllTextAsync(_settingsPath);
                var settings = JsonConvert.DeserializeObject<PlaybackSettings>(json);
                return settings ?? GetDefaultSettings();
            }
            catch
            {
                return GetDefaultSettings();
            }
        }

        public async Task SaveSettingsAsync(PlaybackSettings settings)
        {
            try
            {
                if (settings == null)
                    throw new ArgumentNullException(nameof(settings));

                string json = JsonConvert.SerializeObject(settings, Formatting.Indented);
                await File.WriteAllTextAsync(_settingsPath, json);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to save settings to {_settingsPath}", ex);
            }
        }

        public async Task ResetSettingsAsync()
        {
            try
            {
                if (File.Exists(_settingsPath))
                    File.Delete(_settingsPath);
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Failed to reset settings", ex);
            }
        }

        private PlaybackSettings GetDefaultSettings()
        {
            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var screenshotFolder = Path.Combine(userProfile, "Pictures", "PandaPlayer");
            
            return new PlaybackSettings
            {
                SeekForwardSeconds = 5,
                SeekBackwardSeconds = 5,
                Volume = 1.0f,
                HardwareAccelerationEnabled = true,
                ScreenshotFolder = screenshotFolder,
                MoveTargetFolders = new Dictionary<int, string>(),
                DefaultPlaybackMode = PlaybackMode.Sequential,
                KeyboardShortcuts = GetDefaultKeyboardShortcuts()
            };
        }

        private Dictionary<string, string> GetDefaultKeyboardShortcuts()
        {
            return new Dictionary<string, string>
            {
                { "PlayPause", "Space" },
                { "Next", "N" },
                { "Previous", "P" },
                { "SeekForward", "Right" },
                { "SeekBackward", "Left" },
                { "Screenshot", "Ctrl+S" },
                { "MoveToTarget1", "Ctrl+1" },
                { "MoveToTarget2", "Ctrl+2" },
                { "MoveToTarget3", "Ctrl+3" },
                { "MoveToTarget4", "Ctrl+4" },
                { "MoveToTarget5", "Ctrl+5" },
                { "MoveToTarget6", "Ctrl+6" },
                { "MoveToTarget7", "Ctrl+7" },
                { "MoveToTarget8", "Ctrl+8" },
                { "MoveToTarget9", "Ctrl+9" },
                { "Fullscreen", "F" },
                { "VolumeUp", "Up" },
                { "VolumeDown", "Down" }
            };
        }
    }
}
