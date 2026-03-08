using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using PandaPlayer.Core.Models;
using PandaPlayer.Core.Events;

namespace PandaPlayer.Core.Services
{
    /// <summary>
    /// Interface for managing playback sessions and folder queues.
    /// </summary>
    public interface IPlaybackSessionService
    {
        event EventHandler<EventArgs> SessionCreated;
        event EventHandler<EventArgs> SessionEnded;
        event EventHandler<EventArgs> PlaylistUpdated;
        event EventHandler<EventArgs> PlayModeChanged;

        PlaybackSession CurrentSession { get; }
        List<VideoItem> CurrentPlaylist { get; }
        VideoItem CurrentVideo { get; }
        int CurrentVideoIndex { get; }
        PlaybackMode CurrentPlaybackMode { get; }

        Task CreateSessionAsync(string folderPath);
        Task SetPlaybackModeAsync(PlaybackMode mode);
        Task ResetShuffleNorRepeatCycleAsync();
        Task MarkVideoAsPlayedAsync(VideoItem video);
        Task ResetProgressAsync();
        VideoItem GetNextVideo();
        VideoItem GetPreviousVideo();
        void EndSession();
    }
}
