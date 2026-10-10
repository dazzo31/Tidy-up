using System.IO;
using TidyUp.Services.FileSystem;

namespace TidyUp.Services.Duplicates;

public class DuplicateDetector : IDuplicateDetector
{
    private readonly IFileHashCalculator _hashCalculator;
    private readonly ISafeFileSystem _safeFileSystem;

    public DuplicateDetector(IFileHashCalculator hashCalculator, ISafeFileSystem safeFileSystem)
    {
        _hashCalculator = hashCalculator;
        _safeFileSystem = safeFileSystem;
    }

    public async Task<DuplicateScanResult> ScanFolderAsync(string folderPath, bool searchSubdirectories = false, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
        {
            return new DuplicateScanResult();
        }

        var searchOption = searchSubdirectories ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        var files = Directory.EnumerateFiles(folderPath, "*.*", searchOption);
        return await FindDuplicatesAsync(files, cancellationToken);
    }

    public async Task<DuplicateScanResult> FindDuplicatesAsync(IEnumerable<string> filePaths, CancellationToken cancellationToken = default)
    {
        var result = new DuplicateScanResult();
        var metrics = result.Metrics;

        var existingFiles = filePaths
            .Where(f => !string.IsNullOrWhiteSpace(f) && File.Exists(f))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        metrics.TotalFilesExamined = existingFiles.Count;

        if (existingFiles.Count < 2)
        {
            metrics.SingletonsSkipped = existingFiles.Count;
            return result;
        }

        // Step 1: Group files by exact byte size
        var sizeBuckets = new Dictionary<long, List<string>>();
        foreach (var file in existingFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var length = new FileInfo(file).Length;
                if (!sizeBuckets.TryGetValue(length, out var list))
                {
                    list = [];
                    sizeBuckets[length] = list;
                }
                list.Add(file);
            }
            catch
            {
                // Unreadable file metadata, skip
            }
        }

        var candidateSizeBuckets = new List<KeyValuePair<long, List<string>>>();
        foreach (var kvp in sizeBuckets)
        {
            if (kvp.Value.Count < 2)
            {
                metrics.SingletonsSkipped += kvp.Value.Count;
            }
            else
            {
                metrics.SizeCandidateGroups++;
                candidateSizeBuckets.Add(kvp);
            }
        }

        // Step 2: Compute partial hash of first 4KB for size-matched files
        foreach (var sizeBucket in candidateSizeBuckets)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var partialHashBuckets = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

            foreach (var filePath in sizeBucket.Value)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var partialHash = await _hashCalculator.ComputePartialHashAsync(filePath, 4096, cancellationToken);
                metrics.PartialHashesComputed++;

                if (string.IsNullOrEmpty(partialHash))
                    continue;

                if (!partialHashBuckets.TryGetValue(partialHash, out var list))
                {
                    list = [];
                    partialHashBuckets[partialHash] = list;
                }
                list.Add(filePath);
            }

            // Step 3: Compute full SHA256 only for partial-hash matches
            foreach (var partialKvp in partialHashBuckets)
            {
                if (partialKvp.Value.Count < 2)
                {
                    // Unique partial hash among size group: no need to compute full SHA256
                    continue;
                }

                metrics.PartialHashMatches += partialKvp.Value.Count;

                var fullHashBuckets = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
                foreach (var filePath in partialKvp.Value)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var fullHash = await _hashCalculator.ComputeFullSha256Async(filePath, cancellationToken);
                    metrics.FullHashesComputed++;

                    if (string.IsNullOrEmpty(fullHash))
                        continue;

                    if (!fullHashBuckets.TryGetValue(fullHash, out var list))
                    {
                        list = [];
                        fullHashBuckets[fullHash] = list;
                    }
                    list.Add(filePath);
                }

                foreach (var fullKvp in fullHashBuckets)
                {
                    if (fullKvp.Value.Count < 2)
                        continue;

                    // Order by creation time to identify canonical "Original" file
                    var sorted = fullKvp.Value
                        .Select(p => new { Path = p, Created = TryGetCreationTimeUtc(p) })
                        .OrderBy(x => x.Created)
                        .Select(x => x.Path)
                        .ToList();

                    var duplicateGroup = new DuplicateGroup
                    {
                        FileSizeBytes = sizeBucket.Key,
                        Sha256Hash = fullKvp.Key,
                        OriginalFile = sorted[0],
                        DuplicateFiles = sorted.Skip(1).ToList(),
                        AllFiles = sorted
                    };

                    result.Groups.Add(duplicateGroup);
                    metrics.DuplicateGroupsFound++;
                    metrics.TotalDuplicateFilesFound += duplicateGroup.DuplicateFiles.Count;
                }
            }
        }

        return result;
    }

    public async Task<DuplicateResolutionResult> ResolveDuplicatesAsync(
        IEnumerable<DuplicateGroup> duplicateGroups,
        DuplicateAction action,
        string? customDuplicatesFolder = null,
        CancellationToken cancellationToken = default)
    {
        var result = new DuplicateResolutionResult();

        foreach (var group in duplicateGroups)
        {
            foreach (var dupFile in group.DuplicateFiles)
            {
                cancellationToken.ThrowIfCancellationRequested();
                result.TotalProcessed++;

                try
                {
                    if (!File.Exists(dupFile))
                        continue;

                    switch (action)
                    {
                        case DuplicateAction.Skip:
                            result.SkippedCount++;
                            break;

                        case DuplicateAction.MoveToDuplicatesFolder:
                            var targetDir = !string.IsNullOrWhiteSpace(customDuplicatesFolder)
                                ? customDuplicatesFolder
                                : Path.Combine(Path.GetDirectoryName(dupFile) ?? string.Empty, "_Duplicates");

                            Directory.CreateDirectory(targetDir);

                            var fileName = Path.GetFileName(dupFile);
                            var destPath = Path.Combine(targetDir, fileName);

                            if (File.Exists(destPath))
                            {
                                var nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
                                var ext = Path.GetExtension(fileName);
                                int counter = 1;
                                do
                                {
                                    destPath = Path.Combine(targetDir, $"{nameWithoutExt} ({counter}){ext}");
                                    counter++;
                                } while (File.Exists(destPath));
                            }

                            File.Move(dupFile, destPath);
                            result.MovedCount++;
                            break;

                        case DuplicateAction.DeleteToRecycleBin:
                            await _safeFileSystem.DeleteFileSafelyAsync(dupFile, useRecycleBin: true, allowPermanentFallback: false);
                            result.RecycledCount++;
                            break;
                    }
                }
                catch (Exception ex)
                {
                    result.Errors.Add($"Failed to process duplicate '{dupFile}': {ex.Message}");
                }
            }
        }

        return result;
    }

    private static DateTime TryGetCreationTimeUtc(string path)
    {
        try
        {
            return File.GetCreationTimeUtc(path);
        }
        catch
        {
            return DateTime.UtcNow;
        }
    }
}

