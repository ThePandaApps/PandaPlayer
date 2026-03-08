using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using CodexPlayer.Core.Models;
using Newtonsoft.Json;

namespace CodexPlayer.Core.Persistence
{
    /// <summary>
    /// JSON-based implementation for persisting playback session progress.
    /// </summary>
    public class JsonPlaybackStateStore : IPlaybackStateStore
    {
        private readonly string _sessionsPath;

        public JsonPlaybackStateStore(string appDataFolderPath = null)
        {
            if (string.IsNullOrEmpty(appDataFolderPath))
                appDataFolderPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CodexPlayer");

            if (!Directory.Exists(appDataFolderPath))
                Directory.CreateDirectory(appDataFolderPath);

            _sessionsPath = Path.Combine(appDataFolderPath, "sessions");
            if (!Directory.Exists(_sessionsPath))
                Directory.CreateDirectory(_sessionsPath);
        }

        public async Task<PlaybackSession> LoadSessionAsync(string folderPath)
        {
            try
            {
                string sessionFileName = GenerateSessionFileName(folderPath);
                string sessionFilePath = Path.Combine(_sessionsPath, sessionFileName);

                if (!File.Exists(sessionFilePath))
                    return null;

                string json = await File.ReadAllTextAsync(sessionFilePath);
                var session = JsonConvert.DeserializeObject<PlaybackSession>(json);
                return session;
            }
            catch
            {
                return null;
            }
        }

        public async Task SaveSessionAsync(PlaybackSession session)
        {
            try
            {
                if (session == null)
                    throw new ArgumentNullException(nameof(session));

                string sessionFileName = GenerateSessionFileName(session.RootFolderPath);
                string sessionFilePath = Path.Combine(_sessionsPath, sessionFileName);

                string json = JsonConvert.SerializeObject(session, Formatting.Indented);
                await File.WriteAllTextAsync(sessionFilePath, json);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to save session for folder: {session?.RootFolderPath}", ex);
            }
        }

        public async Task DeleteSessionAsync(string folderPath)
        {
            try
            {
                string sessionFileName = GenerateSessionFileName(folderPath);
                string sessionFilePath = Path.Combine(_sessionsPath, sessionFileName);

                if (File.Exists(sessionFilePath))
                    File.Delete(sessionFilePath);

                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to delete session for folder: {folderPath}", ex);
            }
        }

        public async Task<List<PlaybackSession>> LoadAllSessionsAsync()
        {
            var sessions = new List<PlaybackSession>();

            try
            {
                if (!Directory.Exists(_sessionsPath))
                    return sessions;

                var files = Directory.GetFiles(_sessionsPath, "*.json");
                foreach (var file in files)
                {
                    string json = await File.ReadAllTextAsync(file);
                    var session = JsonConvert.DeserializeObject<PlaybackSession>(json);
                    if (session != null)
                        sessions.Add(session);
                }
            }
            catch { }

            return sessions;
        }

        private string GenerateSessionFileName(string folderPath)
        {
            // Create a stable hash from folder path
            using (var sha = System.Security.Cryptography.SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(folderPath));
                string hashHex = BitConverter.ToString(hash).Replace("-", "").Substring(0, 16);
                return $"session_{hashHex}.json";
            }
        }
    }
}
