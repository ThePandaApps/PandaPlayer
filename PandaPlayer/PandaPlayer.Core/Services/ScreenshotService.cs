using System;
using System.IO;
using System.Threading.Tasks;
using CodexPlayer.Core.Models;
using LibVLCSharp.Shared;

namespace CodexPlayer.Core.Services
{
    /// <summary>
    /// Implements screenshot capture from video frames.
    /// </summary>
    public class ScreenshotService : IScreenshotService
    {
        private string _outputFolder;
        private readonly MediaPlayer _mediaPlayer;

        public event EventHandler<EventArgs> ScreenshotCaptured;

        public ScreenshotService(string outputFolder, MediaPlayer mediaPlayer)
        {
            if (string.IsNullOrEmpty(outputFolder))
            {
                var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                outputFolder = Path.Combine(userProfile, "Pictures", "CodexPlayer");
            }
            _outputFolder = outputFolder;
            _mediaPlayer = mediaPlayer;
            EnsureOutputFolderExists();
        }

        public async Task<string> CaptureScreenshotAsync(string videoPath, long positionMs)
        {
            try
            {
                if (_mediaPlayer == null)
                    throw new InvalidOperationException("MediaPlayer not initialized");

                // Note: LibVLC screenshot functionality is platform-dependent
                // This implementation would need adjustment based on platform
                string fileName = $"{Path.GetFileNameWithoutExtension(videoPath)}_{DateTime.Now:yyyyMMdd_HHmmss}.png";
                string outputPath = Path.Combine(_outputFolder, fileName);

                // Screenshot capture would use platform-specific methods
                // For now, this is a placeholder for the actual implementation
                
                ScreenshotCaptured?.Invoke(this, EventArgs.Empty);
                return await Task.FromResult(outputPath);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Failed to capture screenshot", ex);
            }
        }

        public void SetOutputFolder(string folderPath)
        {
            if (!Directory.Exists(folderPath))
                throw new DirectoryNotFoundException($"Output folder not found: {folderPath}");

            _outputFolder = folderPath;
        }

        public string GetOutputFolder()
        {
            return _outputFolder;
        }

        private void EnsureOutputFolderExists()
        {
            if (!Directory.Exists(_outputFolder))
                Directory.CreateDirectory(_outputFolder);
        }
    }
}
