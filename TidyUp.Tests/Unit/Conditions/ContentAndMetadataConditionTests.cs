using System.IO;
using System.Text;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using TidyUp.Models.Domain;
using TidyUp.Models.Enums;
using TidyUp.Services;
using TidyUp.Services.Conditions;
using Xunit;

namespace TidyUp.Tests.Unit.Conditions;

public class ContentAndMetadataConditionTests : IDisposable
{
    private readonly string _testDir;
    private readonly ContentConditionEvaluator _contentEvaluator;
    private readonly MetadataExtractor _metadataExtractor;
    private readonly RuleEngine _ruleEngine;

    public ContentAndMetadataConditionTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "TidyUp_MetadataTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDir);

        _contentEvaluator = new ContentConditionEvaluator();
        _metadataExtractor = new MetadataExtractor();
        _ruleEngine = new RuleEngine();
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
                // Ignore cleanup
            }
        }
    }

    [Fact]
    public void FileContentCondition_PlainText_FindsKeyword()
    {
        // Arrange
        var filePath = Path.Combine(_testDir, "invoice_402.txt");
        File.WriteAllText(filePath, "Header info\nAccount Number: 12345\nStatus: CONFIDENTIAL\nTotal Due: $500.00");

        var condition = new FileContentCondition
        {
            SearchText = "CONFIDENTIAL",
            CaseSensitive = false
        };

        // Act
        var result = condition.Evaluate(new FileInfo(filePath));

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void FileContentCondition_CaseSensitivity_Respected()
    {
        // Arrange
        var filePath = Path.Combine(_testDir, "case_test.md");
        File.WriteAllText(filePath, "# Report\nUrgent Notice: Review today.");

        var insensitiveCondition = new FileContentCondition
        {
            SearchText = "urgent",
            CaseSensitive = false
        };

        var sensitiveCondition = new FileContentCondition
        {
            SearchText = "urgent",
            CaseSensitive = true
        };

        // Act & Assert
        Assert.True(insensitiveCondition.Evaluate(new FileInfo(filePath)));
        Assert.False(sensitiveCondition.Evaluate(new FileInfo(filePath)));
    }

    [Fact]
    public void FileContentCondition_Regex_MatchesPattern()
    {
        // Arrange
        var filePath = Path.Combine(_testDir, "data.csv");
        File.WriteAllText(filePath, "id,sku,qty\n1,SKU-98421,10\n2,SKU-10293,5");

        var condition = new FileContentCondition
        {
            SearchText = @"SKU-\d{5}",
            IsRegex = true
        };

        // Act
        var result = condition.Evaluate(new FileInfo(filePath));

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void ImageMetadataCondition_PngDimensions_EvaluatesCorrectly()
    {
        // Arrange: Generate a real 120x80 PNG image
        var filePath = Path.Combine(_testDir, "sample_banner.png");
        CreateTestPng(filePath, 120, 80);

        var matchCond = new ImageMetadataCondition
        {
            MinWidth = 100,
            MinHeight = 50
        };

        var failCond = new ImageMetadataCondition
        {
            MinWidth = 200,
            MinHeight = 150
        };

        // Act & Assert
        Assert.True(matchCond.Evaluate(new FileInfo(filePath)));
        Assert.False(failCond.Evaluate(new FileInfo(filePath)));
    }

    [Fact]
    public void MediaMetadataCondition_WavHeader_ExtractsDurationAccurately()
    {
        // Arrange: Create a valid 1-second WAV file
        var filePath = Path.Combine(_testDir, "sample_audio.wav");
        CreateTestWav(filePath, durationSeconds: 2);

        var condition = new MediaMetadataCondition
        {
            MinDuration = TimeSpan.FromSeconds(1),
            MaxDuration = TimeSpan.FromSeconds(3)
        };

        // Act
        var result = condition.Evaluate(new FileInfo(filePath));

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void MediaMetadataCondition_Mp3Id3v1_ExtractsArtistAndAlbum()
    {
        // Arrange: Create an MP3 file with ID3v1 tag trailer
        var filePath = Path.Combine(_testDir, "track01.mp3");
        CreateTestMp3WithId3v1(filePath, artist: "Ludwig van Beethoven", album: "Symphony No. 9");

        var condition = new MediaMetadataCondition
        {
            Artist = "Beethoven",
            Album = "Symphony"
        };

        // Act
        var result = condition.Evaluate(new FileInfo(filePath));

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void RuleEngine_EvaluatesCompositeConditionGroup_WithContentCondition()
    {
        // Arrange
        var filePath = Path.Combine(_testDir, "Quarterly_Tax_2026.txt");
        File.WriteAllText(filePath, "Quarterly Tax Summary\nTotal Tax Liability: $12,000");

        var rule = new Rule
        {
            Name = "Tax Document Organizer",
            IsEnabled = true,
            Conditions = new ConditionGroup
            {
                Operator = LogicOperator.And,
                Conditions =
                [
                    new FileExtensionCondition { Value = "txt" },
                    new FileContentCondition { SearchText = "Tax Liability" }
                ]
            }
        };

        // Act
        var isMatch = _ruleEngine.EvaluateRule(rule, new FileInfo(filePath));

        // Assert
        Assert.True(isMatch);
    }

    private static void CreateTestPng(string path, int width, int height)
    {
        var writeableBmp = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgr32, null);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(writeableBmp));
        using var stream = File.Create(path);
        encoder.Save(stream);
    }

    private static void CreateTestWav(string path, int durationSeconds)
    {
        int sampleRate = 44100;
        short channels = 2;
        short bitsPerSample = 16;
        int byteRate = sampleRate * channels * (bitsPerSample / 8);
        short blockAlign = (short)(channels * (bitsPerSample / 8));
        int dataSize = byteRate * durationSeconds;

        using var stream = File.Create(path);
        using var writer = new BinaryWriter(stream);

        // RIFF chunk
        writer.Write(Encoding.ASCII.GetBytes("RIFF"));
        writer.Write(36 + dataSize);
        writer.Write(Encoding.ASCII.GetBytes("WAVE"));

        // fmt subchunk
        writer.Write(Encoding.ASCII.GetBytes("fmt "));
        writer.Write(16); // subchunk size
        writer.Write((short)1); // PCM
        writer.Write(channels);
        writer.Write(sampleRate);
        writer.Write(byteRate);
        writer.Write(blockAlign);
        writer.Write(bitsPerSample);

        // data subchunk
        writer.Write(Encoding.ASCII.GetBytes("data"));
        writer.Write(dataSize);
        writer.Write(new byte[dataSize]); // silence
    }

    private static void CreateTestMp3WithId3v1(string path, string artist, string album)
    {
        using var stream = File.Create(path);
        // Write dummy audio bytes
        stream.Write(new byte[512]);

        // Write ID3v1 128 bytes trailer
        var id3Bytes = new byte[128];
        Encoding.ASCII.GetBytes("TAG").CopyTo(id3Bytes, 0);

        var artistBytes = Encoding.Latin1.GetBytes(artist);
        Array.Copy(artistBytes, 0, id3Bytes, 33, Math.Min(artistBytes.Length, 30));

        var albumBytes = Encoding.Latin1.GetBytes(album);
        Array.Copy(albumBytes, 0, id3Bytes, 63, Math.Min(albumBytes.Length, 30));

        stream.Write(id3Bytes);
    }
}

