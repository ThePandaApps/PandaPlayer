using System.Threading.Tasks;
using CodexPlayer.Core.Models;

namespace CodexPlayer.Core.Persistence
{
    /// <summary>
    /// Interface for persisting and retrieving application settings.
    /// </summary>
    public interface IAppSettingsStore
    {
        Task<PlaybackSettings> LoadSettingsAsync();
        Task SaveSettingsAsync(PlaybackSettings settings);
        Task ResetSettingsAsync();
    }
}
