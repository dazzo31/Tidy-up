using System.Collections.ObjectModel;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using TidyUp.Configuration;
using TidyUp.Data.Repositories;
using TidyUp.Models.Folders;
using TidyUp.Services.Monitoring;

namespace TidyUp.Services.Folders;

/// <summary>
/// Persisted configuration record for standalone monitored folders.
/// </summary>
public class StandaloneFolderRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Path { get; set; } = string.Empty;
    public bool IncludeSubfolders { get; set; } = true;
    public List<string> ExclusionPatterns { get; set; } = new();
    public bool IsPaused { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Root persistent storage model for the MonitoredFolderCatalog.
/// </summary>
public class FolderCatalogStorageModel
{
    public int Version { get; set; } = 1;
    public List<StandaloneFolderRecord> StandaloneFolders { get; set; } = new();
    public Dictionary<string, bool> PausedFolderOverrides { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// Single source of truth for monitored folders across the application.
/// Aggregates folder configurations across all rules and standalone configured folders, canonicalizes paths,
/// performs physical existence/permission validation, and coordinates with watcher health.
/// </summary>
public class MonitoredFolderCatalog : IMonitoredFolderCatalog
{
    private readonly IRuleRepository _ruleRepository;
    private readonly IFolderPathNormalizer _pathNormalizer;
    private readonly IFolderValidator _folderValidator;
    private readonly IWatcherHealthMonitor _watcherHealthMonitor;
    private readonly TidyUpPaths _paths;
    private readonly string _storageFilePath;
    private readonly SemaphoreSlim _syncLock = new(1, 1);

    private readonly ObservableCollection<MonitoredFolderState> _foldersCollection = new();
    private bool _disposed;

    public ReadOnlyObservableCollection<MonitoredFolderState> Folders { get; }
    public MonitoredFolderSummary Summary { get; private set; } = new();

    public event EventHandler<FolderCatalogChangedEventArgs>? CatalogChanged;

    public MonitoredFolderCatalog(
        IRuleRepository ruleRepository,
        IFolderPathNormalizer pathNormalizer,
        IFolderValidator folderValidator,
        IWatcherHealthMonitor watcherHealthMonitor,
        TidyUpPaths? paths = null)
    {
        _ruleRepository = ruleRepository;
        _pathNormalizer = pathNormalizer;
        _folderValidator = folderValidator;
        _watcherHealthMonitor = watcherHealthMonitor;
        _paths = paths ?? TidyUpPaths.FromEnvironmentOrDefault();
        _storageFilePath = Path.Combine(_paths.AppDataRoot, "monitored_folders.json");

        Folders = new ReadOnlyObservableCollection<MonitoredFolderState>(_foldersCollection);
        _watcherHealthMonitor.FolderHealthChanged += OnWatcherHealthChanged;
        _ruleRepository.RulesChanged += OnRulesChanged;
        if (_ruleRepository is RuleRepository)
        {
            RuleRepository.AnyRulesChanged += OnAnyRulesChanged;
        }
    }

    private FolderCatalogStorageModel LoadStorageModel()
    {
        try
        {
            if (File.Exists(_storageFilePath))
            {
                var json = File.ReadAllText(_storageFilePath);
                var model = JsonSerializer.Deserialize<FolderCatalogStorageModel>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (model != null)
                {
                    model.PausedFolderOverrides ??= new(StringComparer.OrdinalIgnoreCase);
                    model.StandaloneFolders ??= new();
                    return model;
                }
            }
        }
        catch
        {
            // Fallback to empty model on corrupt or unreadable storage
        }

        return new FolderCatalogStorageModel();
    }

    private void SaveStorageModel(FolderCatalogStorageModel model)
    {
        try
        {
            var dir = Path.GetDirectoryName(_storageFilePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var json = JsonSerializer.Serialize(model, new JsonSerializerOptions { WriteIndented = true });
            var tempPath = _storageFilePath + $".tmp_{Guid.NewGuid():N}";
            File.WriteAllText(tempPath, json);
            File.Move(tempPath, _storageFilePath, overwrite: true);
        }
        catch
        {
            // Revert or ignore IO errors gracefully
        }
    }

    private static Guid GenerateFolderId(string canonicalPath)
    {
        var hash = MD5.HashData(Encoding.UTF8.GetBytes(canonicalPath.ToLowerInvariant()));
        return new Guid(hash);
    }

    private async void OnRulesChanged(object? sender, RulesChangedEventArgs e)
    {
        if (_disposed) return;
        try
        {
            await RefreshAsync();
        }
        catch
        {
            // Ignore background refresh failures
        }
    }

    private void OnAnyRulesChanged(object? sender, RulesChangedEventArgs e)
    {
        if (ReferenceEquals(sender, _ruleRepository)) return;
        OnRulesChanged(sender, e);
    }

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        await _syncLock.WaitAsync(cancellationToken);
        try
        {
            var storageModel = LoadStorageModel();
            var rules = await _ruleRepository.GetAllAsync();

            var folderDict = new Dictionary<string, (
                Guid Id,
                bool IsStandalone,
                string DisplayPath,
                List<Guid> RuleIds,
                List<string> RuleNames,
                int EnabledCount,
                bool IncludeSubfolders,
                HashSet<string> Exclusions,
                bool IsExplicitlyPaused)>(StringComparer.OrdinalIgnoreCase);

            // 1. Seed with standalone configured folders
            foreach (var standalone in storageModel.StandaloneFolders)
            {
                if (string.IsNullOrWhiteSpace(standalone.Path))
                    continue;

                string canonicalPath = _pathNormalizer.TryNormalize(standalone.Path, out var norm, out _)
                    ? norm
                    : standalone.Path.Trim();

                folderDict[canonicalPath] = (
                    Id: standalone.Id,
                    IsStandalone: true,
                    DisplayPath: standalone.Path,
                    RuleIds: new List<Guid>(),
                    RuleNames: new List<string>(),
                    EnabledCount: 0,
                    IncludeSubfolders: standalone.IncludeSubfolders,
                    Exclusions: new HashSet<string>(standalone.ExclusionPatterns ?? new(), StringComparer.OrdinalIgnoreCase),
                    IsExplicitlyPaused: standalone.IsPaused
                );
            }

            // 2. Aggregate rule folders
            foreach (var rule in rules)
            {
                foreach (var folder in rule.MonitoredFolders)
                {
                    if (string.IsNullOrWhiteSpace(folder.Path))
                        continue;

                    string canonicalPath;
                    if (!_pathNormalizer.TryNormalize(folder.Path, out canonicalPath, out _))
                    {
                        canonicalPath = folder.Path.Trim();
                    }

                    if (!folderDict.TryGetValue(canonicalPath, out var entry))
                    {
                        entry = (
                            Id: GenerateFolderId(canonicalPath),
                            IsStandalone: false,
                            DisplayPath: canonicalPath,
                            RuleIds: new List<Guid>(),
                            RuleNames: new List<string>(),
                            EnabledCount: 0,
                            IncludeSubfolders: false,
                            Exclusions: new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                            IsExplicitlyPaused: false
                        );
                        folderDict[canonicalPath] = entry;
                    }

                    if (!entry.RuleIds.Contains(rule.Id))
                    {
                        entry.RuleIds.Add(rule.Id);
                        entry.RuleNames.Add(rule.Name);
                    }

                    if (rule.IsEnabled)
                    {
                        entry.EnabledCount++;
                    }

                    if (folder.IncludeSubfolders)
                    {
                        entry.IncludeSubfolders = true;
                    }

                    foreach (var pattern in folder.ExclusionPatterns)
                    {
                        if (!string.IsNullOrWhiteSpace(pattern))
                        {
                            entry.Exclusions.Add(pattern.Trim());
                        }
                    }

                    folderDict[canonicalPath] = entry;
                }
            }

            var states = new List<MonitoredFolderState>();

            foreach (var kvp in folderDict)
            {
                var path = kvp.Key;
                var entry = kvp.Value;

                var validation = _folderValidator.Validate(path);
                WatcherHealthStatus watcherStatus;
                string? detail;

                // Determine effective pause state:
                // Persistent override wins; otherwise if standalone without rules, its saved pause wins;
                // otherwise rules with 0 enabled rules are paused.
                bool isPaused;
                if (storageModel.PausedFolderOverrides.TryGetValue(path, out var overridePaused))
                {
                    isPaused = overridePaused;
                }
                else if (entry.IsStandalone && entry.RuleIds.Count == 0)
                {
                    isPaused = entry.IsExplicitlyPaused;
                }
                else
                {
                    isPaused = entry.EnabledCount == 0;
                }

                if (validation.Status == FolderValidationStatus.Accessible)
                {
                    _watcherHealthMonitor.RegisterFolder(path, isPaused);
                    _watcherHealthMonitor.SetFolderPaused(path, isPaused);

                    var report = _watcherHealthMonitor.GetFolderHealth(path);
                    watcherStatus = report?.Status ?? (isPaused ? WatcherHealthStatus.Paused : WatcherHealthStatus.Healthy);
                    detail = report?.StatusDetail;
                }
                else
                {
                    _watcherHealthMonitor.UnregisterFolder(path);
                    watcherStatus = validation.Status == FolderValidationStatus.Missing
                        ? WatcherHealthStatus.Disconnected
                        : WatcherHealthStatus.Error;
                    detail = validation.Message;
                }

                var state = new MonitoredFolderState
                {
                    Id = entry.Id,
                    IsStandalone = entry.IsStandalone,
                    NormalizedPath = path,
                    DisplayPath = entry.DisplayPath,
                    Exists = validation.Exists,
                    HasReadPermission = validation.HasReadPermission,
                    HasWritePermission = validation.HasWritePermission,
                    IsSystemProtected = validation.IsSystemProtected,
                    ValidationStatus = validation.Status,
                    WatcherHealth = watcherStatus,
                    StatusDetail = detail,
                    LinkedRuleIds = entry.RuleIds.ToArray(),
                    LinkedRuleNames = entry.RuleNames.ToArray(),
                    EnabledRuleCount = entry.EnabledCount,
                    IncludeSubfolders = entry.IncludeSubfolders,
                    ExclusionPatterns = entry.Exclusions.OrderBy(x => x).ToArray(),
                    LastEvaluatedUtc = DateTime.UtcNow
                };

                states.Add(state);
            }

            // Clean up watchers for folders no longer referenced by any rule or standalone configuration
            var allWatchedPaths = _watcherHealthMonitor.GetAllFolderHealth().Keys.ToList();
            foreach (var watched in allWatchedPaths)
            {
                if (!folderDict.ContainsKey(watched))
                {
                    _watcherHealthMonitor.UnregisterFolder(watched);
                }
            }

            var summary = new MonitoredFolderSummary
            {
                TotalFolders = states.Count,
                HealthyFolders = states.Count(s => s.ValidationStatus == FolderValidationStatus.Accessible && s.WatcherHealth == WatcherHealthStatus.Healthy),
                MissingFolders = states.Count(s => s.ValidationStatus == FolderValidationStatus.Missing),
                AccessDeniedFolders = states.Count(s => s.ValidationStatus == FolderValidationStatus.AccessDenied),
                DegradedFolders = states.Count(s => s.WatcherHealth == WatcherHealthStatus.Degraded),
                PausedFolders = states.Count(s => s.WatcherHealth == WatcherHealthStatus.Paused),
                DistinctAlertCount = states.Count(s => s.HasAlert)
            };

            RunOnUi(() =>
            {
                _foldersCollection.Clear();
                foreach (var state in states.OrderBy(s => s.NormalizedPath))
                {
                    _foldersCollection.Add(state);
                }
            });

            Summary = summary;
            CatalogChanged?.Invoke(this, new FolderCatalogChangedEventArgs
            {
                Folders = states,
                Summary = summary
            });
        }
        finally
        {
            _syncLock.Release();
        }
    }

    public async Task<MonitoredFolderState> AddStandaloneFolderAsync(
        string path,
        bool includeSubfolders = true,
        IEnumerable<string>? exclusionPatterns = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Folder path cannot be empty", nameof(path));

        var normalized = _pathNormalizer.TryNormalize(path, out var norm, out _) ? norm : path.Trim();

        await _syncLock.WaitAsync(cancellationToken);
        try
        {
            var model = LoadStorageModel();
            var existing = model.StandaloneFolders.FirstOrDefault(f => _pathNormalizer.AreEqual(f.Path, normalized));
            if (existing == null)
            {
                existing = new StandaloneFolderRecord
                {
                    Id = Guid.NewGuid(),
                    Path = normalized,
                    IncludeSubfolders = includeSubfolders,
                    ExclusionPatterns = exclusionPatterns?.Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p.Trim()).ToList() ?? new(),
                    IsPaused = false,
                    CreatedDate = DateTime.UtcNow
                };
                model.StandaloneFolders.Add(existing);
                SaveStorageModel(model);
            }
        }
        finally
        {
            _syncLock.Release();
        }

        await RefreshAsync(cancellationToken);
        return GetFolder(normalized) ?? throw new InvalidOperationException($"Folder '{normalized}' could not be loaded into the catalog.");
    }

    public async Task<bool> RemoveStandaloneFolderAsync(string path, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;
        var normalized = _pathNormalizer.TryNormalize(path, out var norm, out _) ? norm : path.Trim();

        bool removed = false;
        await _syncLock.WaitAsync(cancellationToken);
        try
        {
            var model = LoadStorageModel();
            var existing = model.StandaloneFolders.FirstOrDefault(f => _pathNormalizer.AreEqual(f.Path, normalized));
            if (existing != null)
            {
                model.StandaloneFolders.Remove(existing);
                SaveStorageModel(model);
                removed = true;
            }
        }
        finally
        {
            _syncLock.Release();
        }

        if (removed)
        {
            await RefreshAsync(cancellationToken);
        }
        return removed;
    }

    public async Task SetFolderPausedAsync(string path, bool isPaused, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        var normalized = _pathNormalizer.TryNormalize(path, out var norm, out _) ? norm : path.Trim();

        await _syncLock.WaitAsync(cancellationToken);
        try
        {
            var model = LoadStorageModel();
            model.PausedFolderOverrides[normalized] = isPaused;
            var standalone = model.StandaloneFolders.FirstOrDefault(f => _pathNormalizer.AreEqual(f.Path, normalized));
            if (standalone != null)
            {
                standalone.IsPaused = isPaused;
            }
            SaveStorageModel(model);
        }
        finally
        {
            _syncLock.Release();
        }

        _watcherHealthMonitor.SetFolderPaused(normalized, isPaused);
        await RefreshAsync(cancellationToken);
    }

    public bool IsFolderExplicitlyPaused(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;
        var normalized = _pathNormalizer.TryNormalize(path, out var norm, out _) ? norm : path.Trim();
        var model = LoadStorageModel();
        return model.PausedFolderOverrides.TryGetValue(normalized, out var isPaused) && isPaused;
    }

    public MonitoredFolderState? GetFolder(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;

        var normalized = _pathNormalizer.TryNormalize(path, out var norm, out _) ? norm : path.Trim();
        return _foldersCollection.FirstOrDefault(f => string.Equals(f.NormalizedPath, normalized, StringComparison.OrdinalIgnoreCase));
    }

    private void OnWatcherHealthChanged(object? sender, FolderHealthReport report)
    {
        if (report == null || string.IsNullOrWhiteSpace(report.Path)) return;

        var normalized = _pathNormalizer.TryNormalize(report.Path, out var norm, out _) ? norm : report.Path.Trim();

        RunOnUi(() =>
        {
            var existing = _foldersCollection.FirstOrDefault(f =>
                string.Equals(f.NormalizedPath, normalized, StringComparison.OrdinalIgnoreCase));

            if (existing != null)
            {
                var index = _foldersCollection.IndexOf(existing);
                var updated = new MonitoredFolderState
                {
                    Id = existing.Id,
                    IsStandalone = existing.IsStandalone,
                    NormalizedPath = existing.NormalizedPath,
                    DisplayPath = existing.DisplayPath,
                    Exists = existing.Exists,
                    HasReadPermission = existing.HasReadPermission,
                    HasWritePermission = existing.HasWritePermission,
                    IsSystemProtected = existing.IsSystemProtected,
                    ValidationStatus = existing.ValidationStatus,
                    WatcherHealth = report.Status,
                    StatusDetail = report.StatusDetail ?? existing.StatusDetail,
                    LinkedRuleIds = existing.LinkedRuleIds,
                    LinkedRuleNames = existing.LinkedRuleNames,
                    EnabledRuleCount = existing.EnabledRuleCount,
                    IncludeSubfolders = existing.IncludeSubfolders,
                    ExclusionPatterns = existing.ExclusionPatterns,
                    LastEvaluatedUtc = DateTime.UtcNow
                };

                _foldersCollection[index] = updated;

                Summary = new MonitoredFolderSummary
                {
                    TotalFolders = _foldersCollection.Count,
                    HealthyFolders = _foldersCollection.Count(s => s.ValidationStatus == FolderValidationStatus.Accessible && s.WatcherHealth == WatcherHealthStatus.Healthy),
                    MissingFolders = _foldersCollection.Count(s => s.ValidationStatus == FolderValidationStatus.Missing),
                    AccessDeniedFolders = _foldersCollection.Count(s => s.ValidationStatus == FolderValidationStatus.AccessDenied),
                    DegradedFolders = _foldersCollection.Count(s => s.WatcherHealth == WatcherHealthStatus.Degraded),
                    PausedFolders = _foldersCollection.Count(s => s.WatcherHealth == WatcherHealthStatus.Paused),
                    DistinctAlertCount = _foldersCollection.Count(s => s.HasAlert)
                };

                CatalogChanged?.Invoke(this, new FolderCatalogChangedEventArgs
                {
                    Folders = _foldersCollection.ToList(),
                    Summary = Summary
                });
            }
        });
    }

    private static void RunOnUi(Action action)
    {
        var app = System.Windows.Application.Current;
        if (app != null && app.Dispatcher != null && !app.Dispatcher.CheckAccess())
        {
            app.Dispatcher.Invoke(action);
        }
        else
        {
            action();
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _watcherHealthMonitor.FolderHealthChanged -= OnWatcherHealthChanged;
        _ruleRepository.RulesChanged -= OnRulesChanged;
        RuleRepository.AnyRulesChanged -= OnAnyRulesChanged;
        _syncLock.Dispose();
    }
}
