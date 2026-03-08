using System;
using System.Collections.Generic;

namespace CodexPlayer.Core.Models
{
    /// <summary>
    /// Represents a folder session with multiple video items for playback.
    /// </summary>
    public class PlaybackSession
    {
        public string SessionId { get; set; }
        public string RootFolderPath { get; set; }
        public List<VideoItem> AllVideos { get; set; }
        public int CurrentVideoIndex { get; set; }
        public PlaybackMode Mode { get; set; }
        public int TotalVideosPlayed { get; set; }
        public int TotalVideos => AllVideos?.Count ?? 0;
        public int RemainingVideos => TotalVideos - TotalVideosPlayed;
        public DateTime CreatedAt { get; set; }
        public DateTime LastModifiedAt { get; set; }
    }

    public enum PlaybackMode
    {
        Sequential,
        Shuffle,
        NoRepeatShuffle
    }
}
