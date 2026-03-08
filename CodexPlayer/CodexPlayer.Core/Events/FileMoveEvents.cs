using System;

namespace CodexPlayer.Core.Events
{
    /// <summary>
    /// Base event for file move operations.
    /// </summary>
    public class FileMoveEventArgs : EventArgs
    {
        public string JobId { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Event for move job started.
    /// </summary>
    public class MoveJobStartedEventArgs : FileMoveEventArgs
    {
        public string SourcePath { get; set; }
        public string DestinationPath { get; set; }
    }

    /// <summary>
    /// Event for move job progress updated.
    /// </summary>
    public class MoveJobProgressEventArgs : FileMoveEventArgs
    {
        public double ProgressPercentage { get; set; }
        public long BytesCopied { get; set; }
        public long TotalBytes { get; set; }
        public double SpeedMbps { get; set; }
        public TimeSpan EstimatedTimeRemaining { get; set; }
    }

    /// <summary>
    /// Event for move job completed.
    /// </summary>
    public class MoveJobCompletedEventArgs : FileMoveEventArgs
    {
        public string FinalDestinationPath { get; set; }
        public long TotalBytesMoved { get; set; }
    }

    /// <summary>
    /// Event for move job failed.
    /// </summary>
    public class MoveJobFailedEventArgs : FileMoveEventArgs
    {
        public string ErrorMessage { get; set; }
        public Exception Exception { get; set; }
    }

    /// <summary>
    /// Event for move job conflict detected.
    /// </summary>
    public class MoveJobConflictEventArgs : FileMoveEventArgs
    {
        public string ExistingFilePath { get; set; }
        public string ConflictingFilePath { get; set; }
    }
}
