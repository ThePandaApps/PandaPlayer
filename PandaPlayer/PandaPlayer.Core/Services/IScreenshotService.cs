using System;
using System.Threading.Tasks;
using CodexPlayer.Core.Models;

namespace CodexPlayer.Core.Services
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
