using System;
using System.Collections.Generic;

namespace CodexPlayer.Core.Persistence
{
    public class FolderPlaybackState
    {
        public string FolderPath { get; set; } = string.Empty;
        public int PlayedCount { get; set; }
        public HashSet<string> PlayedFiles { get; set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    }

    public interface IFolderStateStore
    {
        FolderPlaybackState GetState(string folderPath);
        void SaveState(string folderPath, int playedCount, IEnumerable<string> playedFiles);
        void ClearState(string folderPath);
    }
}
