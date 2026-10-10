using FluentAssertions;
using Moq;
using TidyUp.Models.Domain;
using TidyUp.Models.Enums;
using TidyUp.Services;
using TidyUp.Services.FileSystem;
using Xunit;

namespace TidyUp.Tests.Unit.Services;

public class SafeDeleteTests : IDisposable
{
    private readonly string _testRoot;
    private readonly WindowsShellFileOperations _fileOperations;

    public SafeDeleteTests()
    {
        _testRoot = Path.Combine(Path.GetTempPath(), "TidyUp_SafeDeleteTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testRoot);
        _fileOperations = new WindowsShellFileOperations();
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testRoot))
            {
                Directory.Delete(_testRoot, recursive: true);
            }
        }
        catch
        {
            // Best effort temp cleanup
        }
    }

    [Theory]
    [InlineData(@"\\server\share\documents\file.pdf")]
    [InlineData(@"\\192.168.1.100\public\test.txt")]
    [InlineData("//nas-server/data/archive.zip")]
    public void CanSendToRecycleBin_WithUncPaths_ReturnsFalse(string uncPath)
    {
        // Act
        var result = _fileOperations.CanSendToRecycleBin(uncPath);

        // Assert
        result.Should().BeFalse("UNC and network share paths never support the Windows Recycle Bin");
    }

    [Fact]
    public void CanSendToRecycleBin_WithLocalTempPath_ReturnsTrue()
    {
        // Arrange
        var localFile = Path.Combine(_testRoot, "local.txt");

        // Act
        var result = _fileOperations.CanSendToRecycleBin(localFile);

        // Assert
        result.Should().BeTrue("Local system drive supports the Windows Recycle Bin");
    }

    [Fact]
    public async Task DeleteFileSafelyAsync_WhenRecycleBinUnavailableAndFallbackDenied_ThrowsRecycleBinUnavailableException()
    {
        // Arrange
        var uncPath = @"\\nas-storage\home\user_file.pdf";

        // Act
        var act = () => _fileOperations.DeleteFileSafelyAsync(
            uncPath,
            useRecycleBin: true,
            allowPermanentFallback: false);

        // Assert
        await act.Should().ThrowAsync<RecycleBinUnavailableException>()
            .WithMessage("*cannot be safely sent to the Recycle Bin*");
    }

    [Fact]
    public async Task DeleteFileSafelyAsync_WithoutRecycleBinAndFallbackDenied_ThrowsInvalidOperationException()
    {
        // Arrange
        var testFile = Path.Combine(_testRoot, "sensitive_data.txt");
        await File.WriteAllTextAsync(testFile, "Sensitive user content");

        // Act
        var act = () => _fileOperations.DeleteFileSafelyAsync(
            testFile,
            useRecycleBin: false,
            allowPermanentFallback: false);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*without explicit permanent fallback authorization*");

        File.Exists(testFile).Should().BeTrue("File must not be deleted when permanent fallback is unauthorized");
    }

    [Fact]
    public async Task DeleteFileSafelyAsync_WithLocalFile_DeletesViaRecycleBinSuccessfully()
    {
        // Arrange
        var testFile = Path.Combine(_testRoot, "recycle_me.txt");
        await File.WriteAllTextAsync(testFile, "To be recycled");

        // Act
        await _fileOperations.DeleteFileSafelyAsync(
            testFile,
            useRecycleBin: true,
            allowPermanentFallback: false);

        // Assert
        File.Exists(testFile).Should().BeFalse("File must be removed from original location");
    }

    [Fact]
    public async Task DeleteFileSafelyAsync_WithFallbackAllowed_DeletesPermanently()
    {
        // Arrange
        var testFile = Path.Combine(_testRoot, "permanent.txt");
        await File.WriteAllTextAsync(testFile, "Permanent delete");

        // Act
        await _fileOperations.DeleteFileSafelyAsync(
            testFile,
            useRecycleBin: false,
            allowPermanentFallback: true);

        // Assert
        File.Exists(testFile).Should().BeFalse();
    }

    [Fact]
    public async Task ActionExecutor_ExecuteDeleteAsync_WhenRecycleBinUnavailable_ProtectsFileFromPermanentLoss()
    {
        // Arrange
        var testFile = Path.Combine(_testRoot, "network_protected.txt");
        await File.WriteAllTextAsync(testFile, "Protected network content");

        var mockSafeFs = new Mock<ISafeFileSystem>();
        mockSafeFs.Setup(fs => fs.CanSendToRecycleBin(testFile)).Returns(false);
        mockSafeFs.Setup(fs => fs.DeleteFileSafelyAsync(testFile, true, false))
            .ThrowsAsync(new RecycleBinUnavailableException(testFile, "Network path does not support Recycle Bin"));

        var executor = new ActionExecutor(new VariableEngine(), mockSafeFs.Object);

        var action = new DeleteFileAction
        {
            UseRecycleBin = true,
            ConfirmBeforeDelete = false
        };

        // Act
        var result = await executor.ExecuteActionAsync(action, new FileInfo(testFile));

        // Assert
        result.Success.Should().BeFalse("Operation must fail rather than silently deleting permanently");
        result.Type.Should().Be(ActionResultType.Error);
        result.ErrorMessage.Should().Contain("cannot be safely sent to the Recycle Bin");
        File.Exists(testFile).Should().BeTrue("Source file must be preserved on disk");
    }
}

