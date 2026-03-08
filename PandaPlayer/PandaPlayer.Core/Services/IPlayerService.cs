using System;
using System.Threading.Tasks;
using CodexPlayer.Core.Models;
using CodexPlayer.Core.Events;

namespace CodexPlayer.Core.Services
{
    /// <summary>
    /// Interface for VLC-based playback engine service.
    /// </summary>
    public interface IPlayerService
    {
        event EventHandler<PlaybackStartedEventArgs> PlaybackStarted;
        event EventHandler<PlaybackPausedEventArgs> PlaybackPaused;
        event EventHandler<PlaybackEndedEventArgs> PlaybackEnded;
        event EventHandler<PlaybackErrorEventArgs> PlaybackError;
        event EventHandler<SeekEventArgs> PositionChanged;

        VideoItem CurrentVideo { get; }
        bool IsPlaying { get; }
        bool IsPaused { get; }
        long CurrentPositionMs { get; }
        long DurationMs { get; }
        float Volume { get; set; }
        bool IsFullscreen { get; set; }

        Task PlayAsync(VideoItem video);
        Task PauseAsync();
        Task ResumeAsync();
        Task StopAsync();
        Task SeekAsync(long positionMs);
        Task NextAsync();
        Task PreviousAsync();
        void Dispose();
    }
}
