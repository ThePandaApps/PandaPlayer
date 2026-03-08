using System;
using PandaPlayer.Core.Models;

namespace PandaPlayer.Core.Events
{
    /// <summary>
    /// Base event for playback state changes.
    /// </summary>
    public class PlaybackEventArgs : EventArgs
    {
        public PlaybackEventArgs(VideoItem currentVideo)
        {
            CurrentVideo = currentVideo;
            Timestamp = DateTime.UtcNow;
        }

        public VideoItem CurrentVideo { get; set; }
        public DateTime Timestamp { get; set; }
    }

    /// <summary>
    /// Event for playback started.
    /// </summary>
    public class PlaybackStartedEventArgs : PlaybackEventArgs
    {
        public PlaybackStartedEventArgs(VideoItem video) : base(video) { }
    }

    /// <summary>
    /// Event for playback paused.
    /// </summary>
    public class PlaybackPausedEventArgs : PlaybackEventArgs
    {
        public long CurrentPositionMs { get; set; }
        public PlaybackPausedEventArgs(VideoItem video, long position) : base(video)
        {
            CurrentPositionMs = position;
        }
    }

    /// <summary>
    /// Event for playback ended.
    /// </summary>
    public class PlaybackEndedEventArgs : PlaybackEventArgs
    {
        public PlaybackEndedEventArgs(VideoItem video) : base(video) { }
    }

    /// <summary>
    /// Event for playback error.
    /// </summary>
    public class PlaybackErrorEventArgs : EventArgs
    {
        public string ErrorMessage { get; set; }
        public Exception Exception { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Event for seek position changed.
    /// </summary>
    public class SeekEventArgs : EventArgs
    {
        public long NewPositionMs { get; set; }
        public int SeekDirectionSeconds { get; set; } // Positive for forward, negative for backward
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}
