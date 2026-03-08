using System.Windows;
using PandaPlayer.UI.Models;

namespace PandaPlayer.UI.Services
{
    // Define a service interface for saving/loading bindings
    public interface IKeyBindingService
    {
        System.Collections.Generic.IEnumerable<PandaPlayer.UI.Models.PlayerKeyBinding> Bindings { get; }
        System.Windows.Input.KeyGesture GetGesture(string actionId);
        void Save();
    }
}
