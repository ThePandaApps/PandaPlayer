using System;
using System.Threading.Tasks;
using PandaPlayer.Core.Models;

namespace PandaPlayer.Core.Services
{
    /// <summary>
    /// Interface for screenshot capture and management.
    /// </summary>
    public interface IScreenshotService
    {
        event EventHandler<EventArgs> ScreenshotCaptured;

        Task<string> CaptureScreenshotAsync(string videoPath, long positionMs);
        void SetOutputFolder(string folderPath);
        string GetOutputFolder();
    }
}
