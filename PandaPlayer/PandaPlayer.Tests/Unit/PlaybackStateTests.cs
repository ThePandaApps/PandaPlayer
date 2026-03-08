using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using PandaPlayer.Core.Models;
using PandaPlayer.Core.Persistence;

namespace PandaPlayer.Tests.Unit
{
    /// <summary>
    /// Tests for persistence and restoration of playback progress.
    /// </summary>
    public class PlaybackStateTests
    {
        [Fact]
        public async Task SavePlaybackState_CanBeRetrieved_AfterRestart()
        {
            // Arrange
            string tempFolder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempFolder);

            try
            {
                var store = new JsonPlaybackStateStore(tempFolder);

                var session = new PlaybackSession
                {
                    SessionId = Guid.NewGuid().ToString(),
                    RootFolderPath = "/videos",
                    AllVideos = new List<VideoItem>
                    {
                        new VideoItem { FileName = "video1.mp4", FilePath = "/videos/video1.mp4", IsPlayed = true },
                        new VideoItem { FileName = "video2.mp4", FilePath = "/videos/video2.mp4", IsPlayed = false }
                    },
                    TotalVideosPlayed = 1,
                    Mode = PlaybackMode.Sequential
                };

                // Act
                await store.SaveSessionAsync(session);
                var retrievedSession = await store.LoadSessionAsync("/videos");

                // Assert
                Assert.NotNull(retrievedSession);
                Assert.Equal(session.SessionId, retrievedSession.SessionId);
                Assert.Equal(1, retrievedSession.TotalVideosPlayed);
                Assert.True(retrievedSession.AllVideos.First().IsPlayed);
                Assert.False(retrievedSession.AllVideos.Last().IsPlayed);
            }
            finally
            {
                Directory.Delete(tempFolder, recursive: true);
            }
        }

        [Fact]
        public async Task ResetPlaybackState_ClearsAllProgression()
        {
            // Arrange
            string tempFolder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempFolder);

            try
            {
                var store = new JsonPlaybackStateStore(tempFolder);

                var session = new PlaybackSession
                {
                    SessionId = Guid.NewGuid().ToString(),
                    RootFolderPath = "/videos",
                    AllVideos = new List<VideoItem>
                    {
                        new VideoItem { FileName = "video1.mp4", FilePath = "/videos/video1.mp4", IsPlayed = true }
                    },
                    TotalVideosPlayed = 5
                };

                await store.SaveSessionAsync(session);

                // Act
                await store.DeleteSessionAsync("/videos");
                var retrievedSession = await store.LoadSessionAsync("/videos");

                // Assert
                Assert.Null(retrievedSession);
            }
            finally
            {
                Directory.Delete(tempFolder, recursive: true);
            }
        }

        [Fact]
        public async Task SaveSettings_WithMoveTargets_PersistCorrectly()
        {
            // Arrange
            string tempFolder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempFolder);

            try
            {
                var store = new JsonAppSettingsStore(tempFolder);

                var settings = new PlaybackSettings
                {
                    SeekForwardSeconds = 10,
                    SeekBackwardSeconds = 5,
                    ScreenshotFolder = "/screenshots",
                    MoveTargetFolders = new Dictionary<int, string>
                    {
                        { 1, "/folder1" },
                        { 2, "/folder2" }
                    }
                };

                // Act
                await store.SaveSettingsAsync(settings);
                var retrievedSettings = await store.LoadSettingsAsync();

                // Assert
                Assert.Equal(10, retrievedSettings.SeekForwardSeconds);
                Assert.Equal(2, retrievedSettings.MoveTargetFolders.Count);
                Assert.Equal("/folder1", retrievedSettings.MoveTargetFolders[1]);
            }
            finally
            {
                Directory.Delete(tempFolder, recursive: true);
            }
        }
    }
}
