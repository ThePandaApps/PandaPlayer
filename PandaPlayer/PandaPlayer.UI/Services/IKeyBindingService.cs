using System.Windows;
using CodexPlayer.UI.Models;

namespace CodexPlayer.UI.Services
{
    // Define a service interface for saving/loading bindings
    public interface IKeyBindingService
    {
        System.Collections.Generic.IEnumerable<CodexPlayer.UI.Models.PlayerKeyBinding> Bindings { get; }
        System.Windows.Input.KeyGesture GetGesture(string actionId);
        void Save();
    }
}
