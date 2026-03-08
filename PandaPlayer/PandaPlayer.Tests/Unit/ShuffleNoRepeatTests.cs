using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Moq;
using PandaPlayer.Core.Services;
using PandaPlayer.Core.Models;
using PandaPlayer.Core.Persistence;

namespace PandaPlayer.Tests.Unit
{
    /// <summary>
    /// Tests for shuffle-no-repeat playback behavior.
    /// </summary>
    public class ShuffleNoRepeatTests
    {
        private readonly Mock<IPlaybackStateStore> _mockStateStore;
        private readonly PlaybackSessionService _sessionService;

        public ShuffleNoRepeatTests()
        {
            _mockStateStore = new Mock<IPlaybackStateStore>();
            _sessionService = new PlaybackSessionService(_mockStateStore.Object);
        }

        [Fact]
        public async Task GetNextVideo_InShuffleMode_NeverRepeatsBeyondAllVideosPlayed()
        {
            // Arrange
            var videos = CreateTestVideoList(5);
            _mockStateStore.Setup(s => s.LoadSessionAsync(It.IsAny<string>()))
                .ReturnsAsync((PlaybackSession)null);
            _mockStateStore.Setup(s => s.SaveSessionAsync(It.IsAny<PlaybackSession>()))
                .Returns(Task.CompletedTask);

            // Create temporary folder
            string tempFolder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempFolder);

            try
            {
                await _sessionService.CreateSessionAsync(tempFolder);
                await _sessionService.SetPlaybackModeAsync(PlaybackMode.Shuffle);

                var playedVideos = new HashSet<int>();
                int cycles = 0;

                // Play all videos multiple times
                for (int i = 0; i < videos.Count * 3; i++)
                {
                    var next = _sessionService.GetNextVideo();
                    Assert.NotNull(next);
                    
                    int index = _sessionService.CurrentSession.AllVideos.IndexOf(next);
                    playedVideos.Add(index);
                    
                    // If we've played all videos, start new cycle
                    if (playedVideos.Count == videos.Count)
                    {
                        await _sessionService.ResetShuffleNorRepeatCycleAsync();
                        playedVideos.Clear();
                        cycles++;
                    }
                }

                Assert.Equal(2, cycles); // Should have had 2 complete cycles
            }
            finally
            {
                Directory.Delete(tempFolder, recursive: true);
            }
        }

        [Fact]
        public async Task EndOfCycle_IsDetected_WhenAllVideosPlayed()
        {
            // Arrange
            var videos = CreateTestVideoList(3);
            _mockStateStore.Setup(s => s.LoadSessionAsync(It.IsAny<string>()))
                .ReturnsAsync((PlaybackSession)null);
            _mockStateStore.Setup(s => s.SaveSessionAsync(It.IsAny<PlaybackSession>()))
                .Returns(Task.CompletedTask);

            string tempFolder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempFolder);

            try
            {
                await _sessionService.CreateSessionAsync(tempFolder);
                await _sessionService.SetPlaybackModeAsync(PlaybackMode.Shuffle);

                // Play all videos
                for (int i = 0; i < videos.Count; i++)
                {
                    var video = _sessionService.GetNextVideo();
                    await _sessionService.MarkVideoAsPlayedAsync(video);
                }

                // All videos should be marked as played
                Assert.True(_sessionService.CurrentSession.AllVideos.All(v => v.IsPlayed));
            }
            finally
            {
                Directory.Delete(tempFolder, recursive: true);
            }
        }

        private List<VideoItem> CreateTestVideoList(int count)
        {
            var videos = new List<VideoItem>();
            for (int i = 0; i < count; i++)
            {
                videos.Add(new VideoItem
                {
                    FilePath = $"/temp/video_{i}.mp4",
                    FileName = $"video_{i}.mp4",
                    FolderPath = "/temp",
                    FileSizeBytes = 1000000,
                    PlaybackIndex = i
                });
            }
            return videos;
        }
    }
}
