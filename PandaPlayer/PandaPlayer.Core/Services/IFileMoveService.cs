using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PandaPlayer.Core.Models;
using PandaPlayer.Core.Events;

namespace PandaPlayer.Core.Services
{
    /// <summary>
    /// Interface for safe background file move operations.
    /// </summary>
    public interface IFileMoveService
    {
        event EventHandler<MoveJobStartedEventArgs> JobStarted;
        event EventHandler<MoveJobProgressEventArgs> JobProgress;
        event EventHandler<MoveJobCompletedEventArgs> JobCompleted;
        event EventHandler<MoveJobFailedEventArgs> JobFailed;
        event EventHandler<MoveJobConflictEventArgs> JobConflict;

        IReadOnlyList<FileMoveJob> ActiveJobs { get; }
        IReadOnlyList<FileMoveJob> CompletedJobs { get; }

        Task<FileMoveJob> MoveFileAsync(string sourceFilePath, string destinationFolderPath, CancellationToken cancellationToken = default);
        Task RetryJobAsync(string jobId);
        Task CancelJobAsync(string jobId);
        Task ResolveConflictAsync(string jobId, ConflictResolution resolution, string newFileName = null);
    }
}
