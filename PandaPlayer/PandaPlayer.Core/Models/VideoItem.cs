using System;

namespace CodexPlayer.Core.Models
{
    /// <summary>
    /// Represents a single video file for playback.
    /// </summary>
    public class VideoItem
    {
        public string FilePath { get; set; }
        public string FileName { get; set; }
        public string FolderPath { get; set; }
        public long FileSizeBytes { get; set; }
        public TimeSpan Duration { get; set; }
        public bool IsPlayed { get; set; }
        public DateTime LastPlayedAt { get; set; }
        public int PlaybackIndex { get; set; }
    }
}
