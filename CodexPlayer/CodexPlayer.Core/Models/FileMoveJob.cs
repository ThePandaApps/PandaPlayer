using System;

namespace CodexPlayer.Core.Models
{
    /// <summary>
    /// Represents the status of a background file move operation.
    /// </summary>
    public class FileMoveJob
    {
        public string JobId { get; set; }
        public string SourceFilePath { get; set; }
        public string DestinationFolderPath { get; set; }
        public FileMoveStatus Status { get; set; }
        public double ProgressPercentage { get; set; }
        public long BytesCopied { get; set; }
        public long TotalBytes { get; set; }
        public double SpeedMbps { get; set; }
        public TimeSpan EstimatedTimeRemaining { get; set; }
        public string ErrorMessage { get; set; }
        public ConflictResolution ConflictResolution { get; set; }
        public DateTime StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
    }

    public enum FileMoveStatus
    {
        Pending,
        Copying,
        Verifying,
        Finalizing,
        Completed,
        Failed,
        Cancelled,
        ConflictDetected
    }

    public enum ConflictResolution
    {
        None,
        Rename,
        Overwrite,
        Skip
    }
}
