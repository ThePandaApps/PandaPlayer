using System.Collections.Generic;

namespace CodexPlayer.Core.Models
{
    /// <summary>
    /// Represents playback control settings.
    /// </summary>
    public class PlaybackSettings
    {
        public int SeekForwardSeconds { get; set; } = 5;
        public int SeekBackwardSeconds { get; set; } = 5;
        public float Volume { get; set; } = 1.0f;
        public bool HardwareAccelerationEnabled { get; set; } = true;
        public string ScreenshotFolder { get; set; }
        public Dictionary<int, string> MoveTargetFolders { get; set; } = new();
        public PlaybackMode DefaultPlaybackMode { get; set; } = PlaybackMode.Sequential;
        public Dictionary<string, string> KeyboardShortcuts { get; set; } = new();
    }
}
