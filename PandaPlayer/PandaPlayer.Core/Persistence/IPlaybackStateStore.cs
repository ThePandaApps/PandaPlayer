using System.Collections.Generic;
using System.Threading.Tasks;
using PandaPlayer.Core.Models;

namespace PandaPlayer.Core.Persistence
{
    /// <summary>
    /// Interface for persisting and retrieving playback progress per folder session.
    /// </summary>
    public interface IPlaybackStateStore
    {
        Task<PlaybackSession> LoadSessionAsync(string folderPath);
        Task SaveSessionAsync(PlaybackSession session);
        Task DeleteSessionAsync(string folderPath);
        Task<List<PlaybackSession>> LoadAllSessionsAsync();
    }
}
