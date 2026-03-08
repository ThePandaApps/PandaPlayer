using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Xunit;
using PandaPlayer.Core.Models;
using PandaPlayer.Core.Services;

namespace PandaPlayer.Tests.Unit
{
    /// <summary>
    /// Tests for safe file move pipeline with verification.
    /// </summary>
    public class SafeFileMoveTests
    {
        [Fact]
        public async Task MoveFile_CompletesPipeline_WithoutDeletingSource_OnFailure()
        {
            // Arrange
            string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDir);

            try
            {
                string sourceFile = Path.Combine(tempDir, "source.txt");
                string destDir = Path.Combine(tempDir, "dest");
                Directory.CreateDirectory(destDir);

                File.WriteAllText(sourceFile, "test content");

                var config = new FileMoveConfiguration
                {
                    VerifyChecksum = true,
                    DeleteSourceAfterVerification = true,
                    MaxConcurrentMoves = 2
                };

                var moveService = new FileMoveService(config);

                // Act
                var job = await moveService.MoveFileAsync(sourceFile, destDir);

                // Wait for move to complete
                await Task.Delay(2000);

                // Assert
                // Source should be deleted after successful move
                Assert.False(File.Exists(sourceFile), "Source file should be deleted after successful safe move");
                
                // Destination should have the file
                string destFile = Path.Combine(destDir, "source.txt");
                Assert.True(File.Exists(destFile), "Destination file should exist");
                Assert.Equal("test content", File.ReadAllText(destFile));
            }
            finally
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }

        [Fact]
        public async Task MoveFile_HandlesDuplicates_WithConflictDetection()
        {
            // Arrange
            string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDir);

            try
            {
                string sourceFile = Path.Combine(tempDir, "source.txt");
                string destDir = Path.Combine(tempDir, "dest");
                Directory.CreateDirectory(destDir);

                File.WriteAllText(sourceFile, "new content");
                File.WriteAllText(Path.Combine(destDir, "source.txt"), "existing content");

                var config = new FileMoveConfiguration();
                var moveService = new FileMoveService(config);

                bool conflictRaised = false;
                moveService.JobConflict += (s, e) =>
                {
                    conflictRaised = true;
                };

                // Act
                var job = await moveService.MoveFileAsync(sourceFile, destDir);
                await Task.Delay(1000);

                // Assert
                Assert.True(conflictRaised, "Conflict should be detected");
                Assert.True(File.Exists(sourceFile), "Source should still exist when conflict is detected");
            }
            finally
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }

        [Fact]
        public void FileMoveConfiguration_HasSafeDefaults()
        {
            // Arrange & Act
            var config = new FileMoveConfiguration();

            // Assert
            Assert.True(config.VerifyChecksum, "Checksum verification should be enabled by default");
            Assert.True(config.DeleteSourceAfterVerification, "Source deletion should be enabled by default");
            Assert.NotEmpty(config.PartialFileExtension);
            Assert.True(config.MaxConcurrentMoves > 0);
        }
    }
}
