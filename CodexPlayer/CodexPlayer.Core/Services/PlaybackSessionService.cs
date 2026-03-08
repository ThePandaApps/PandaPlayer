using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CodexPlayer.Core.Models;
using CodexPlayer.Core.Events;
using CodexPlayer.Core.Persistence;

namespace CodexPlayer.Core.Services
{
    /// <summary>
    /// Manages playback sessions for folder-based video playback with shuffle-no-repeat support.
    /// </summary>
    public class PlaybackSessionService : IPlaybackSessionService
    {
        private readonly IPlaybackStateStore _stateStore;
        private PlaybackSession _currentSession;
        private List<VideoItem> _currentPlaylist;
        private Random _random;
        private HashSet<int> _playedIndicesInCurrentCycle;
        private bool _isShuffleNoRepeatCycleActive;

        public event EventHandler<EventArgs> SessionCreated;
        public event EventHandler<EventArgs> SessionEnded;
        public event EventHandler<EventArgs> PlaylistUpdated;
        public event EventHandler<EventArgs> PlayModeChanged;

        public PlaybackSession CurrentSession => _currentSession;
        public List<VideoItem> CurrentPlaylist => _currentPlaylist ?? new();
        public VideoItem CurrentVideo => _currentSession?.AllVideos?[_currentSession.CurrentVideoIndex];
        public int CurrentVideoIndex => _currentSession?.CurrentVideoIndex ?? -1;
        public PlaybackMode CurrentPlaybackMode => _currentSession?.Mode ?? PlaybackMode.Sequential;

        public PlaybackSessionService(IPlaybackStateStore stateStore)
        {
            _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
            _random = new Random();
        }

        public async Task CreateSessionAsync(string folderPath)
        {
            try
            {
                // Try to load existing session
                _currentSession = await _stateStore.LoadSessionAsync(folderPath);

                if (_currentSession == null)
                {
                    // Create new session
                    _currentSession = new PlaybackSession
                    {
                        SessionId = Guid.NewGuid().ToString(),
                        RootFolderPath = folderPath,
                        AllVideos = await GetAllVideosRecursiveAsync(folderPath),
                        CurrentVideoIndex = 0,
                        Mode = PlaybackMode.Sequential,
                        CreatedAt = DateTime.UtcNow,
                        LastModifiedAt = DateTime.UtcNow
                    };
                }

                _playedIndicesInCurrentCycle = new HashSet<int>();
                _isShuffleNoRepeatCycleActive = true;
                _currentPlaylist = _currentSession.AllVideos;

                SessionCreated?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to create playback session for folder: {folderPath}", ex);
            }
        }

        public async Task SetPlaybackModeAsync(PlaybackMode mode)
        {
            if (_currentSession != null)
            {
                _currentSession.Mode = mode;
                _currentSession.LastModifiedAt = DateTime.UtcNow;
                await _stateStore.SaveSessionAsync(_currentSession);
                PlayModeChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public async Task ResetShuffleNorRepeatCycleAsync()
        {
            _playedIndicesInCurrentCycle.Clear();
            _isShuffleNoRepeatCycleActive = true;
            await Task.CompletedTask;
        }

        public async Task MarkVideoAsPlayedAsync(VideoItem video)
        {
            if (_currentSession == null || video == null)
                return;

            video.IsPlayed = true;
            video.LastPlayedAt = DateTime.UtcNow;
            _currentSession.TotalVideosPlayed++;
            _currentSession.LastModifiedAt = DateTime.UtcNow;

            // Track played indices for shuffle-no-repeat
            int videoIndex = _currentSession.AllVideos.IndexOf(video);
            if (videoIndex >= 0 && !_playedIndicesInCurrentCycle.Contains(videoIndex))
            {
                _playedIndicesInCurrentCycle.Add(videoIndex);
            }

            // Check if all videos have been played in current cycle
            if (_playedIndicesInCurrentCycle.Count == _currentSession.AllVideos.Count)
            {
                _isShuffleNoRepeatCycleActive = false;
                // Cycle complete - would trigger prompt for new cycle in UI
            }

            await _stateStore.SaveSessionAsync(_currentSession);
        }

        public VideoItem GetNextVideo()
        {
            if (_currentSession?.AllVideos == null || _currentSession.AllVideos.Count == 0)
                return null;

            int nextIndex = _currentSession.CurrentVideoIndex + 1;

            if (_currentSession.Mode == PlaybackMode.Sequential)
            {
                if (nextIndex < _currentSession.AllVideos.Count)
                {
                    _currentSession.CurrentVideoIndex = nextIndex;
                    return _currentSession.AllVideos[nextIndex];
                }
                else
                {
                    // Reached end of playlist
                    return null;
                }
            }
            else // Shuffle mode with no-repeat
            {
                return GetShuffleNextVideo();
            }
        }

        public VideoItem GetPreviousVideo()
        {
            if (_currentSession?.AllVideos == null || _currentSession.AllVideos.Count == 0)
                return null;

            int prevIndex = _currentSession.CurrentVideoIndex - 1;

            if (prevIndex >= 0)
            {
                _currentSession.CurrentVideoIndex = prevIndex;
                return _currentSession.AllVideos[prevIndex];
            }

            return null;
        }

        public async Task ResetProgressAsync()
        {
            if (_currentSession != null)
            {
                _currentSession.TotalVideosPlayed = 0;
                foreach (var video in _currentSession.AllVideos)
                {
                    video.IsPlayed = false;
                    video.LastPlayedAt = DateTime.MinValue;
                }
                _playedIndicesInCurrentCycle.Clear();
                _isShuffleNoRepeatCycleActive = true;
                _currentSession.LastModifiedAt = DateTime.UtcNow;
                await _stateStore.SaveSessionAsync(_currentSession);
            }
        }

        public void EndSession()
        {
            if (_currentSession != null)
            {
                _currentSession = null;
                _currentPlaylist = null;
                SessionEnded?.Invoke(this, EventArgs.Empty);
            }
        }

        private VideoItem GetShuffleNextVideo()
        {
            if (_currentSession?.AllVideos == null || _currentSession.AllVideos.Count == 0)
                return null;

            // If all videos played in cycle, start new cycle
            if (_playedIndicesInCurrentCycle.Count >= _currentSession.AllVideos.Count)
            {
                _playedIndicesInCurrentCycle.Clear();
            }

            // Pick random unplayed video
            List<int> unplayedIndices = new();
            for (int i = 0; i < _currentSession.AllVideos.Count; i++)
            {
                if (!_playedIndicesInCurrentCycle.Contains(i))
                    unplayedIndices.Add(i);
            }

            if (unplayedIndices.Count == 0)
                return null;

            int randomIndex = unplayedIndices[_random.Next(unplayedIndices.Count)];
            _currentSession.CurrentVideoIndex = randomIndex;
            return _currentSession.AllVideos[randomIndex];
        }

        private async Task<List<VideoItem>> GetAllVideosRecursiveAsync(string folderPath)
        {
            var videos = new List<VideoItem>();
            var supportedExtensions = new[] { ".mp4", ".mkv", ".avi", ".mov", ".wmv", ".webm", ".m4v", ".ts", ".m2ts", ".flv" };

            try
            {
                var di = new DirectoryInfo(folderPath);
                var files = di.GetFiles("*.*", SearchOption.AllDirectories);

                foreach (var file in files)
                {
                    if (supportedExtensions.Contains(file.Extension.ToLower()))
                    {
                        videos.Add(new VideoItem
                        {
                            FilePath = file.FullName,
                            FileName = file.Name,
                            FolderPath = file.DirectoryName,
                            FileSizeBytes = file.Length,
                            PlaybackIndex = videos.Count
                        });
                    }
                }

                videos = videos.OrderBy(v => v.FilePath).ToList();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to scan folder for videos: {folderPath}", ex);
            }

            return await Task.FromResult(videos);
        }
    }
}
