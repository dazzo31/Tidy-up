using System.IO;
using System.Text;
using System.Windows.Media.Imaging;

namespace TidyUp.Services.Conditions;

public class ImageMetadata
{
    public int? Width { get; set; }
    public int? Height { get; set; }
    public DateTime? DateTaken { get; set; }
    public string? CameraModel { get; set; }
    public string? CameraMake { get; set; }
}

public class MediaMetadata
{
    public TimeSpan? Duration { get; set; }
    public string? Artist { get; set; }
    public string? Album { get; set; }
    public string? Title { get; set; }
}

public interface IMetadataExtractor
{
    ImageMetadata ExtractImageMetadata(string filePath);
    MediaMetadata ExtractMediaMetadata(string filePath);
}

public class MetadataExtractor : IMetadataExtractor
{
    public ImageMetadata ExtractImageMetadata(string filePath)
    {
        var result = new ImageMetadata();
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            return result;

        try
        {
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096);
            var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None);
            if (decoder.Frames.Count > 0)
            {
                var frame = decoder.Frames[0];
                result.Width = frame.PixelWidth;
                result.Height = frame.PixelHeight;

                if (frame.Metadata is BitmapMetadata metadata)
                {
                    result.CameraModel = metadata.CameraModel;
                    result.CameraMake = metadata.CameraManufacturer;

                    if (!string.IsNullOrWhiteSpace(metadata.DateTaken))
                    {
                        if (DateTime.TryParse(metadata.DateTaken, out var parsedDate))
                        {
                            result.DateTaken = parsedDate;
                        }
                    }
                }
            }
        }
        catch
        {
            // Fallback: If image cannot be decoded by WPF decoder or is corrupt, return partial/empty metadata
        }

        return result;
    }

    public MediaMetadata ExtractMediaMetadata(string filePath)
    {
        var result = new MediaMetadata();
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            return result;

        try
        {
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096);
            var ext = Path.GetExtension(filePath).ToLowerInvariant();

            if (ext == ".mp3")
            {
                ExtractMp3Metadata(stream, result);
            }
            else if (ext == ".wav")
            {
                ExtractWavMetadata(stream, result);
            }
        }
        catch
        {
            // Ignored on corrupt or inaccessible media
        }

        return result;
    }

    private static void ExtractMp3Metadata(Stream stream, MediaMetadata result)
    {
        // 1. Try ID3v1 at the end of the file (last 128 bytes)
        if (stream.Length >= 128)
        {
            stream.Seek(-128, SeekOrigin.End);
            var buffer = new byte[128];
            int read = stream.Read(buffer, 0, 128);
            if (read == 128 && buffer[0] == 'T' && buffer[1] == 'A' && buffer[2] == 'G')
            {
                var enc = Encoding.Latin1;
                result.Title = enc.GetString(buffer, 3, 30).Trim('\0', ' ');
                result.Artist = enc.GetString(buffer, 33, 30).Trim('\0', ' ');
                result.Album = enc.GetString(buffer, 63, 30).Trim('\0', ' ');
            }
        }

        // 2. Try ID3v2 header at the start
        if (stream.Length >= 10)
        {
            stream.Seek(0, SeekOrigin.Begin);
            var header = new byte[10];
            if (stream.Read(header, 0, 10) == 10 && header[0] == 'I' && header[1] == 'D' && header[2] == '3')
            {
                // Has ID3v2 header
                int tagSize = ((header[6] & 0x7F) << 21) |
                              ((header[7] & 0x7F) << 14) |
                              ((header[8] & 0x7F) << 7) |
                              (header[9] & 0x7F);

                if (tagSize > 0 && tagSize < 1024 * 1024) // Sanity check < 1MB tag
                {
                    var tagBytes = new byte[tagSize];
                    int bytesRead = stream.Read(tagBytes, 0, tagSize);
                    ParseId3v2Frames(tagBytes, bytesRead, result);
                }
            }
        }
    }

    private static void ParseId3v2Frames(byte[] tagBytes, int length, MediaMetadata result)
    {
        int offset = 0;
        while (offset + 10 <= length)
        {
            var frameId = Encoding.ASCII.GetString(tagBytes, offset, 4);
            if (string.IsNullOrWhiteSpace(frameId) || frameId[0] == '\0')
                break;

            int frameSize = (tagBytes[offset + 4] << 24) |
                            (tagBytes[offset + 5] << 16) |
                            (tagBytes[offset + 6] << 8) |
                            tagBytes[offset + 7];

            offset += 10;
            if (frameSize <= 0 || offset + frameSize > length)
                break;

            if (frameId is "TIT2" or "TPE1" or "TALB" or "TLEN")
            {
                // First byte of frame content is encoding: 0 = ISO-8859-1, 1 = UTF-16 with BOM, 3 = UTF-8
                var content = Encoding.UTF8.GetString(tagBytes, offset + 1, frameSize - 1).Trim('\0', ' ');
                if (frameId == "TIT2" && string.IsNullOrEmpty(result.Title))
                    result.Title = content;
                else if (frameId == "TPE1" && string.IsNullOrEmpty(result.Artist))
                    result.Artist = content;
                else if (frameId == "TALB" && string.IsNullOrEmpty(result.Album))
                    result.Album = content;
                else if (frameId == "TLEN" && long.TryParse(content, out var msLength))
                    result.Duration = TimeSpan.FromMilliseconds(msLength);
            }

            offset += frameSize;
        }
    }

    private static void ExtractWavMetadata(Stream stream, MediaMetadata result)
    {
        if (stream.Length < 44)
            return;

        using var reader = new BinaryReader(stream, Encoding.ASCII, leaveOpen: true);
        var riff = new string(reader.ReadChars(4));
        if (riff != "RIFF")
            return;

        reader.ReadInt32(); // File size - 8
        var wave = new string(reader.ReadChars(4));
        if (wave != "WAVE")
            return;

        int channels = 0;
        int sampleRate = 0;
        int byteRate = 0;
        long dataSize = 0;

        while (stream.Position + 8 <= stream.Length)
        {
            var chunkId = new string(reader.ReadChars(4));
            int chunkSize = reader.ReadInt32();

            if (chunkId == "fmt " && chunkSize >= 16)
            {
                reader.ReadInt16(); // format tag
                channels = reader.ReadInt16();
                sampleRate = reader.ReadInt32();
                byteRate = reader.ReadInt32();
                reader.ReadInt16(); // block align
                reader.ReadInt16(); // bits per sample
                if (chunkSize > 16)
                    reader.ReadBytes(chunkSize - 16);
            }
            else if (chunkId == "data")
            {
                dataSize = chunkSize;
                break;
            }
            else
            {
                if (chunkSize > 0 && stream.Position + chunkSize <= stream.Length)
                    stream.Seek(chunkSize, SeekOrigin.Current);
                else
                    break;
            }
        }

        if (byteRate > 0 && dataSize > 0)
        {
            var totalSeconds = (double)dataSize / byteRate;
            result.Duration = TimeSpan.FromSeconds(totalSeconds);
        }
    }
}

