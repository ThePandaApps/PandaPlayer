using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using PandaPlayer.Core.Models;
using PandaPlayer.Core.Events;

namespace PandaPlayer.Core.Services
{
    /// <summary>
    /// Safe background file move with SHA-256 verification and conflict resolution.
    /// Source is NEVER deleted before the destination is fully verified.
    /// </summary>
    public class FileMoveService : IFileMoveService
    {
        private readonly FileMoveConfiguration _configuration;
        private readonly List<FileMoveJob>     _activeJobs    = new();
        private readonly List<FileMoveJob>     _completedJobs = new();
        private readonly SemaphoreSlim         _semaphore;

        private readonly Dictionary<string, CancellationTokenSource> _jobCts       = new();
        private readonly Dictionary<string, TaskCompletionSource<(ConflictResolution, string?)>> _conflictAwaiters = new();

        public event EventHandler<MoveJobStartedEventArgs>?   JobStarted;
        public event EventHandler<MoveJobProgressEventArgs>?  JobProgress;
        public event EventHandler<MoveJobCompletedEventArgs>? JobCompleted;
        public event EventHandler<MoveJobFailedEventArgs>?    JobFailed;
        public event EventHandler<MoveJobConflictEventArgs>?  JobConflict;

        public IReadOnlyList<FileMoveJob> ActiveJobs    => _activeJobs.AsReadOnly();
        public IReadOnlyList<FileMoveJob> CompletedJobs => _completedJobs.AsReadOnly();

        public FileMoveService(FileMoveConfiguration configuration)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _semaphore     = new SemaphoreSlim(_configuration.MaxConcurrentMoves);
        }

        // ─── Public API ───────────────────────────────────────────────────────

        public async Task<FileMoveJob> MoveFileAsync(
            string sourceFilePath,
            string destinationFolderPath,
            CancellationToken cancellationToken = default)
        {
            if (!File.Exists(sourceFilePath))
                throw new FileNotFoundException($"Source file not found: {sourceFilePath}");
            if (!Directory.Exists(destinationFolderPath))
                throw new DirectoryNotFoundException($"Destination folder not found: {destinationFolderPath}");

            var job = new FileMoveJob
            {
                JobId                  = Guid.NewGuid().ToString(),
                SourceFilePath         = sourceFilePath,
                DestinationFolderPath  = destinationFolderPath,
                Status                 = FileMoveStatus.Pending,
                StartedAt              = DateTime.UtcNow
            };

            _activeJobs.Add(job);
            var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _jobCts[job.JobId] = cts;

            JobStarted?.Invoke(this, new MoveJobStartedEventArgs
            {
                JobId           = job.JobId,
                SourcePath      = sourceFilePath,
                DestinationPath = destinationFolderPath
            });

            // Fire-and-forget on a background thread; callers can monitor via events
            _ = Task.Run(() => ExecuteSafeMoveAsync(job, cts.Token), cts.Token);
            return job;
        }

        public async Task RetryJobAsync(string jobId)
        {
            var job = _completedJobs.FirstOrDefault(j => j.JobId == jobId);
            if (job?.Status == FileMoveStatus.Failed)
            {
                _completedJobs.Remove(job);
                job.Status       = FileMoveStatus.Pending;
                job.ErrorMessage = null;
                await MoveFileAsync(job.SourceFilePath, job.DestinationFolderPath);
            }
        }

        public async Task CancelJobAsync(string jobId)
        {
            if (_jobCts.TryGetValue(jobId, out var cts))
                cts.Cancel();
            await Task.CompletedTask;
        }

        /// <summary>
        /// Called by the UI to resolve a detected conflict and resume the paused move.
        /// </summary>
        public async Task ResolveConflictAsync(string jobId, ConflictResolution resolution, string? newFileName = null)
        {
            if (_conflictAwaiters.TryGetValue(jobId, out var tcs))
            {
                _conflictAwaiters.Remove(jobId);
                tcs.SetResult((resolution, newFileName));
            }
            await Task.CompletedTask;
        }

        // ─── Core move pipeline ───────────────────────────────────────────────

        private async Task ExecuteSafeMoveAsync(FileMoveJob job, CancellationToken ct)
        {
            await _semaphore.WaitAsync(ct);
            try
            {
                // ── Step 1: Copy to a temporary partial file ──────────────────
                job.Status     = FileMoveStatus.Copying;
                job.TotalBytes = new FileInfo(job.SourceFilePath).Length;

                string tempPath = await CopyToTemporaryFileAsync(job, ct);

                if (ct.IsCancellationRequested)
                {
                    job.Status = FileMoveStatus.Cancelled;
                    CleanupPartial(tempPath);
                    return;
                }

                // ── Step 2: Verify the copy ───────────────────────────────────
                job.Status = FileMoveStatus.Verifying;
                if (!await VerifyCopyAsync(job.SourceFilePath, tempPath, ct))
                {
                    job.Status       = FileMoveStatus.Failed;
                    job.ErrorMessage = "Copy verification failed — sizes or checksums differ";
                    CleanupPartial(tempPath);
                    JobFailed?.Invoke(this, new MoveJobFailedEventArgs
                        { JobId = job.JobId, ErrorMessage = job.ErrorMessage });
                    return;
                }

                // ── Step 3: Conflict detection ────────────────────────────────
                string finalPath       = Path.Combine(job.DestinationFolderPath, Path.GetFileName(job.SourceFilePath));
                bool   overwriteTarget = false;

                if (File.Exists(finalPath))
                {
                    job.Status = FileMoveStatus.ConflictDetected;

                    var tcs = new TaskCompletionSource<(ConflictResolution, string?)>();
                    _conflictAwaiters[job.JobId] = tcs;

                    JobConflict?.Invoke(this, new MoveJobConflictEventArgs
                    {
                        JobId               = job.JobId,
                        ExistingFilePath    = finalPath,
                        ConflictingFilePath = tempPath
                    });

                    // Suspend until UI calls ResolveConflictAsync
                    var (resolution, newName) = await tcs.Task.WaitAsync(ct);

                    switch (resolution)
                    {
                        case ConflictResolution.Skip:
                            job.Status = FileMoveStatus.Cancelled;
                            CleanupPartial(tempPath);
                            _activeJobs.Remove(job);
                            _completedJobs.Add(job);
                            return;

                        case ConflictResolution.Rename:
                            if (!string.IsNullOrWhiteSpace(newName))
                            {
                                finalPath = Path.Combine(job.DestinationFolderPath, newName);
                            }
                            else
                            {
                                string stem = Path.GetFileNameWithoutExtension(job.SourceFilePath);
                                string ext  = Path.GetExtension(job.SourceFilePath);
                                finalPath   = Path.Combine(job.DestinationFolderPath,
                                    $"{stem}_{DateTime.Now:yyyyMMddHHmmss}{ext}");
                            }
                            overwriteTarget = false;
                            break;

                        case ConflictResolution.Overwrite:
                            overwriteTarget = true;
                            break;
                    }
                }

                // ── Step 4: Rename partial → final ───────────────────────────
                job.Status = FileMoveStatus.Finalizing;
                try
                {
                    File.Move(tempPath, finalPath, overwrite: overwriteTarget);
                }
                catch (IOException ex)
                {
                    job.Status       = FileMoveStatus.Failed;
                    job.ErrorMessage = $"Failed to finalize move: {ex.Message}";
                    CleanupPartial(tempPath);
                    JobFailed?.Invoke(this, new MoveJobFailedEventArgs
                        { JobId = job.JobId, ErrorMessage = job.ErrorMessage, Exception = ex });
                    return;
                }

                // ── Step 5: Delete source ONLY after successful finalization ──
                try
                {
                    File.Delete(job.SourceFilePath);
                }
                catch (Exception ex)
                {
                    // Destination is safe; log but continue
                    job.ErrorMessage = $"Warning: source could not be deleted — {ex.Message}";
                }

                job.Status             = FileMoveStatus.Completed;
                job.BytesCopied        = job.TotalBytes;
                job.ProgressPercentage = 100.0;
                job.CompletedAt        = DateTime.UtcNow;

                _activeJobs.Remove(job);
                _completedJobs.Add(job);

                JobCompleted?.Invoke(this, new MoveJobCompletedEventArgs
                {
                    JobId                 = job.JobId,
                    FinalDestinationPath  = finalPath,
                    TotalBytesMoved       = job.TotalBytes
                });
            }
            catch (OperationCanceledException)
            {
                job.Status = FileMoveStatus.Cancelled;
                _activeJobs.Remove(job);
                _completedJobs.Add(job);
            }
            catch (Exception ex)
            {
                job.Status       = FileMoveStatus.Failed;
                job.ErrorMessage = ex.Message;
                JobFailed?.Invoke(this, new MoveJobFailedEventArgs
                    { JobId = job.JobId, ErrorMessage = ex.Message, Exception = ex });
            }
            finally
            {
                _semaphore.Release();
                _jobCts.Remove(job.JobId);
                _conflictAwaiters.Remove(job.JobId);
            }
        }

        // ─── Helpers ─────────────────────────────────────────────────────────

        private async Task<string> CopyToTemporaryFileAsync(FileMoveJob job, CancellationToken ct)
        {
            string tempName = Path.GetFileName(job.SourceFilePath) + _configuration.PartialFileExtension;
            string tempPath = Path.Combine(job.DestinationFolderPath, tempName);

            long totalBytes   = job.TotalBytes;
            long copiedBytes  = 0;
            var  sw           = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                using var src  = File.OpenRead(job.SourceFilePath);
                using var dest = File.Create(tempPath, _configuration.BufferSizeBytes);

                var buffer = new byte[_configuration.BufferSizeBytes];
                int bytesRead;

                while ((bytesRead = await src.ReadAsync(buffer, 0, buffer.Length, ct)) > 0)
                {
                    await dest.WriteAsync(buffer, 0, bytesRead, ct);
                    copiedBytes += bytesRead;

                    job.BytesCopied        = copiedBytes;
                    job.ProgressPercentage = copiedBytes * 100.0 / totalBytes;

                    double elapsed = sw.Elapsed.TotalSeconds;
                    if (elapsed > 0)
                        job.SpeedMbps = (copiedBytes / 1_048_576.0) / elapsed;

                    if (job.SpeedMbps > 0)
                        job.EstimatedTimeRemaining = TimeSpan.FromSeconds(
                            (totalBytes - copiedBytes) / 1_048_576.0 / job.SpeedMbps);

                    JobProgress?.Invoke(this, new MoveJobProgressEventArgs
                    {
                        JobId                  = job.JobId,
                        ProgressPercentage     = job.ProgressPercentage,
                        BytesCopied            = copiedBytes,
                        TotalBytes             = totalBytes,
                        SpeedMbps              = job.SpeedMbps,
                        EstimatedTimeRemaining = job.EstimatedTimeRemaining
                    });
                }
            }
            catch
            {
                CleanupPartial(tempPath);
                throw;
            }

            return tempPath;
        }

        private async Task<bool> VerifyCopyAsync(string srcPath, string destPath, CancellationToken ct)
        {
            var srcInfo  = new FileInfo(srcPath);
            var destInfo = new FileInfo(destPath);

            if (srcInfo.Length != destInfo.Length) return false;

            if (!_configuration.VerifyChecksum) return true;

            using var hasherSrc  = SHA256.Create();
            using var hasherDest = SHA256.Create();
            using var srcStream  = File.OpenRead(srcPath);
            using var destStream = File.OpenRead(destPath);

            var srcHash  = await hasherSrc.ComputeHashAsync(srcStream,  ct);
            var destHash = await hasherDest.ComputeHashAsync(destStream, ct);
            return srcHash.SequenceEqual(destHash);
        }

        private static void CleanupPartial(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { }
        }
    }
}
