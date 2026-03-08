using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;

namespace PandaPlayer.Core.Persistence
{
    public class JsonFolderStateStore : IFolderStateStore
    {
        private readonly string _storagePath;
        private Dictionary<string, FolderPlaybackState> _cache;
        private readonly object _lock = new object();

        public JsonFolderStateStore()
        {
            var appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PandaPlayer");
            Directory.CreateDirectory(appData);
            _storagePath = Path.Combine(appData, "folder_history.json");
            Load();
        }

        private void Load()
        {
            lock (_lock)
            {
                if (File.Exists(_storagePath))
                {
                    try
                    {
                        var json = File.ReadAllText(_storagePath);
                        _cache = JsonSerializer.Deserialize<Dictionary<string, FolderPlaybackState>>(json) 
                                 ?? new Dictionary<string, FolderPlaybackState>(StringComparer.OrdinalIgnoreCase);
                    }
                    catch 
                    { 
                        _cache = new Dictionary<string, FolderPlaybackState>(StringComparer.OrdinalIgnoreCase); 
                    }
                }
                else
                {
                    _cache = new Dictionary<string, FolderPlaybackState>(StringComparer.OrdinalIgnoreCase);
                }
            }
        }

        private void Save()
        {
            lock (_lock)
            {
                try
                {
                    var options = new JsonSerializerOptions { WriteIndented = true };
                    var json = JsonSerializer.Serialize(_cache, options);
                    File.WriteAllText(_storagePath, json);
                }
                catch { /* best effort */ }
            }
        }

        public FolderPlaybackState GetState(string folderPath)
        {
            if (string.IsNullOrEmpty(folderPath)) return new FolderPlaybackState();
            
            lock (_lock)
            {
                if (_cache.TryGetValue(folderPath, out var state))
                {
                    // Ensure set is case-insensitive if deserializer didn't set comparer
                    if (state.PlayedFiles.Comparer != StringComparer.OrdinalIgnoreCase)
                        state.PlayedFiles = new HashSet<string>(state.PlayedFiles, StringComparer.OrdinalIgnoreCase);
                    return state;
                }
                return new FolderPlaybackState { FolderPath = folderPath };
            }
        }

        public void SaveState(string folderPath, int playedCount, IEnumerable<string> playedFiles)
        {
            if (string.IsNullOrEmpty(folderPath)) return;

            lock (_lock)
            {
                var state = new FolderPlaybackState
                {
                    FolderPath = folderPath,
                    PlayedCount = playedCount,
                    PlayedFiles = new HashSet<string>(playedFiles, StringComparer.OrdinalIgnoreCase),
                    LastUpdated = DateTime.UtcNow
                };
                _cache[folderPath] = state;
                
                // Cleanup old entries if too large (> 100 folders)
                if (_cache.Count > 100)
                {
                    var oldest = _cache.OrderBy(x => x.Value.LastUpdated).First().Key;
                    _cache.Remove(oldest);
                }
                
                Save();
            }
        }

        public void ClearState(string folderPath)
        {
             if (string.IsNullOrEmpty(folderPath)) return;
             lock (_lock)
             {
                 if (_cache.Remove(folderPath)) Save();
             }
        }
    }
}
