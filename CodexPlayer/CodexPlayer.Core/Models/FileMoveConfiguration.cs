namespace CodexPlayer.Core.Models
{
    /// <summary>
    /// Configuration for safe file move operations.
    /// </summary>
    public class FileMoveConfiguration
    {
        public bool VerifyChecksum { get; set; } = true;
        public int BufferSizeBytes { get; set; } = 1024 * 1024; // 1MB
        public int MaxConcurrentMoves { get; set; } = 2;
        public string PartialFileExtension { get; set; } = ".codex.partial";
        public bool DeleteSourceAfterVerification { get; set; } = true;
    }
}
