using System.IO;
using Moq;
using TidyUp.Services.Duplicates;
using TidyUp.Services.FileSystem;
using Xunit;

namespace TidyUp.Tests.DuplicateTests;

public class DuplicateDetectorTests : IDisposable
{
    private readonly string _testDir;
    private readonly Mock<ISafeFileSystem> _mockSafeFs;
    private readonly DuplicateDetector _detector;
    private readonly FileHashCalculator _hashCalculator;

    public DuplicateDetectorTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "TidyUp_DuplicateTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDir);

        _mockSafeFs = new Mock<ISafeFileSystem>();
        _hashCalculator = new FileHashCalculator();
        _detector = new DuplicateDetector(_hashCalculator, _mockSafeFs.Object);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
        {
            try
            {
                Directory.Delete(_testDir, true);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    [Fact]
    public async Task FindDuplicatesAsync_IdenticalFilesWithDifferentNames_IdentifiedAsDuplicates()
    {
        // Arrange
        var content = new byte[8192];
        Random.Shared.NextBytes(content);

        var file1 = Path.Combine(_testDir, "report_2026_q1.pdf");
        var file2 = Path.Combine(_testDir, "copy_of_report.pdf");
        await File.WriteAllBytesAsync(file1, content);
        await File.WriteAllBytesAsync(file2, content);

        // Act
        var result = await _detector.FindDuplicatesAsync([file1, file2]);

        // Assert
        Assert.Single(result.Groups);
        var group = result.Groups[0];
        Assert.Equal(8192, group.FileSizeBytes);
        Assert.Equal(2, group.AllFiles.Count);
        Assert.Single(group.DuplicateFiles);
        Assert.Equal(1, result.Metrics.DuplicateGroupsFound);
        Assert.Equal(1, result.Metrics.TotalDuplicateFilesFound);
    }

    [Fact]
    public async Task FindDuplicatesAsync_SingletonsWithDifferentSizes_SkippedWithoutHashing()
    {
        // Arrange: 3 files with completely different sizes
        var file1 = Path.Combine(_testDir, "small.txt");
        var file2 = Path.Combine(_testDir, "medium.txt");
        var file3 = Path.Combine(_testDir, "large.txt");

        await File.WriteAllBytesAsync(file1, new byte[128]);
        await File.WriteAllBytesAsync(file2, new byte[256]);
        await File.WriteAllBytesAsync(file3, new byte[512]);

        // Act
        var result = await _detector.FindDuplicatesAsync([file1, file2, file3]);

        // Assert
        Assert.Empty(result.Groups);
        Assert.Equal(3, result.Metrics.TotalFilesExamined);
        Assert.Equal(3, result.Metrics.SingletonsSkipped);
        Assert.Equal(0, result.Metrics.PartialHashesComputed);
        Assert.Equal(0, result.Metrics.FullHashesComputed);
    }

    [Fact]
    public async Task FindDuplicatesAsync_SameSizeDifferentFirst4KB_SkipsFullHash()
    {
        // Arrange: 2 files with identical size (6000 bytes) but different first 4KB
        var bytesA = new byte[6000];
        Array.Fill<byte>(bytesA, 0xAA);

        var bytesB = new byte[6000];
        Array.Fill<byte>(bytesB, 0xBB);

        var fileA = Path.Combine(_testDir, "data_a.bin");
        var fileB = Path.Combine(_testDir, "data_b.bin");
        await File.WriteAllBytesAsync(fileA, bytesA);
        await File.WriteAllBytesAsync(fileB, bytesB);

        // Act
        var result = await _detector.FindDuplicatesAsync([fileA, fileB]);

        // Assert
        Assert.Empty(result.Groups);
        Assert.Equal(1, result.Metrics.SizeCandidateGroups);
        Assert.Equal(2, result.Metrics.PartialHashesComputed);
        Assert.Equal(0, result.Metrics.FullHashesComputed); // Crucial: skipped full hashing!
    }

    [Fact]
    public async Task FindDuplicatesAsync_SameSizeSameFirst4KB_DifferentTail_FullHashDifferentiates()
    {
        // Arrange: 2 files of 8192 bytes with identical first 4096 bytes, but differing tail
        var bytesA = new byte[8192];
        var bytesB = new byte[8192];

        // Same head
        for (int i = 0; i < 4096; i++)
        {
            bytesA[i] = (byte)(i % 256);
            bytesB[i] = (byte)(i % 256);
        }

        // Differing tail
        for (int i = 4096; i < 8192; i++)
        {
            bytesA[i] = 1;
            bytesB[i] = 2;
        }

        var fileA = Path.Combine(_testDir, "head_match_a.bin");
        var fileB = Path.Combine(_testDir, "head_match_b.bin");
        await File.WriteAllBytesAsync(fileA, bytesA);
        await File.WriteAllBytesAsync(fileB, bytesB);

        // Act
        var result = await _detector.FindDuplicatesAsync([fileA, fileB]);

        // Assert
        Assert.Empty(result.Groups);
        Assert.Equal(2, result.Metrics.PartialHashesComputed);
        Assert.Equal(2, result.Metrics.PartialHashMatches);
        Assert.Equal(2, result.Metrics.FullHashesComputed);
        Assert.Equal(0, result.Metrics.DuplicateGroupsFound);
    }

    [Fact]
    public async Task ResolveDuplicatesAsync_MoveToDuplicatesFolder_MovesDuplicateAndPreservesOriginal()
    {
        // Arrange
        var content = new byte[1024];
        Random.Shared.NextBytes(content);

        var originalPath = Path.Combine(_testDir, "original.doc");
        var dupPath = Path.Combine(_testDir, "duplicate.doc");
        await File.WriteAllBytesAsync(originalPath, content);
        await File.WriteAllBytesAsync(dupPath, content);

        var scanResult = await _detector.FindDuplicatesAsync([originalPath, dupPath]);
        Assert.Single(scanResult.Groups);

        // Act
        var resResult = await _detector.ResolveDuplicatesAsync(
            scanResult.Groups,
            DuplicateAction.MoveToDuplicatesFolder);

        // Assert
        Assert.Equal(1, resResult.MovedCount);
        Assert.True(File.Exists(originalPath), "Original file must be preserved");
        Assert.False(File.Exists(dupPath), "Duplicate file must be moved out of source");

        var duplicatesDir = Path.Combine(_testDir, "_Duplicates");
        Assert.True(Directory.Exists(duplicatesDir));
        Assert.True(File.Exists(Path.Combine(duplicatesDir, "duplicate.doc")));
    }

    [Fact]
    public async Task ResolveDuplicatesAsync_DeleteToRecycleBin_CallsSafeFileSystem()
    {
        // Arrange
        var content = new byte[1024];
        var originalPath = Path.Combine(_testDir, "main.jpg");
        var dupPath = Path.Combine(_testDir, "main_copy.jpg");
        await File.WriteAllBytesAsync(originalPath, content);
        await File.WriteAllBytesAsync(dupPath, content);

        var scanResult = await _detector.FindDuplicatesAsync([originalPath, dupPath]);
        Assert.Single(scanResult.Groups);

        _mockSafeFs
            .Setup(fs => fs.DeleteFileSafelyAsync(It.IsAny<string>(), true, false))
            .Returns(Task.CompletedTask);

        // Act
        var resResult = await _detector.ResolveDuplicatesAsync(
            scanResult.Groups,
            DuplicateAction.DeleteToRecycleBin);

        // Assert
        Assert.Equal(1, resResult.RecycledCount);
        _mockSafeFs.Verify(fs => fs.DeleteFileSafelyAsync(dupPath, true, false), Times.Once);
    }

    [Fact]
    public async Task ResolveDuplicatesAsync_Skip_LeavesFilesUntouched()
    {
        // Arrange
        var content = new byte[512];
        var fileA = Path.Combine(_testDir, "one.txt");
        var fileB = Path.Combine(_testDir, "two.txt");
        await File.WriteAllBytesAsync(fileA, content);
        await File.WriteAllBytesAsync(fileB, content);

        var scanResult = await _detector.FindDuplicatesAsync([fileA, fileB]);

        // Act
        var resResult = await _detector.ResolveDuplicatesAsync(scanResult.Groups, DuplicateAction.Skip);

        // Assert
        Assert.Equal(1, resResult.SkippedCount);
        Assert.True(File.Exists(fileA));
        Assert.True(File.Exists(fileB));
    }
}

