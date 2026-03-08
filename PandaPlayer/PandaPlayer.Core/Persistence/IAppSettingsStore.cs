using System.Threading.Tasks;
using PandaPlayer.Core.Models;

namespace PandaPlayer.Core.Persistence
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
