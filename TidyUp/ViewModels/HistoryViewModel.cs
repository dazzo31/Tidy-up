using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using TidyUp.Data;
using TidyUp.Data.Entities;
using TidyUp.Services.Rollback;

namespace TidyUp.ViewModels;

/// <summary>
/// Presentation wrapper for a single journal entry row in the History viewer.
/// Supports expandable details and action-level rollback.
/// </summary>
public partial class HistoryRowItemViewModel : ObservableObject
{
    public OperationJournalEntry Entry { get; }

    [ObservableProperty]
    private bool _isExpanded;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanRollback))]
    [NotifyPropertyChangedFor(nameof(StatusBadgeBackground))]
    [NotifyPropertyChangedFor(nameof(StatusBadgeForeground))]
    private string _status;

    [ObservableProperty]
    private DateTime? _rolledBackAt;

    [ObservableProperty]
    private string? _rollbackMessage;

    public HistoryRowItemViewModel(OperationJournalEntry entry)
    {
        Entry = entry;
        _status = entry.Status;
        _rolledBackAt = entry.RolledBackAt;
    }

    public Guid OperationId => Entry.OperationId;
    public Guid BatchId => Entry.BatchId;
    public DateTime Timestamp => Entry.Timestamp;
    public string ActionType => Entry.ActionType;
    public string OriginalPath => Entry.OriginalPath;
    public string? TargetPath => Entry.TargetPath;
    public string? RuleName => Entry.RuleName;
    public string? PreActionHash => Entry.PreActionHash;
    public string? PostActionHash => Entry.PostActionHash;
    public string? Details => Entry.Details;

    public bool CanRollback => string.Equals(Status, "Completed", StringComparison.OrdinalIgnoreCase);

    public string ActionBadgeBackground => ActionType switch
    {
        "Move" => "#E3F2FD",
        "Copy" => "#E0F2F1",
        "Rename" => "#F3E5F5",
        "ChangeExtension" => "#EDE7F6",
        "Delete" => "#FFEBEE",
        "ExtractArchive" => "#EFEBE9",
        "RunCommand" => "#ECEFF1",
        _ => "#EEEEEE"
    };

    public string ActionBadgeForeground => ActionType switch
    {
        "Move" => "#1976D2",
        "Copy" => "#00796B",
        "Rename" => "#7B1FA2",
        "ChangeExtension" => "#512DA8",
        "Delete" => "#D32F2F",
        "ExtractArchive" => "#4E342E",
        "RunCommand" => "#37474F",
        _ => "#424242"
    };

    public string StatusBadgeBackground => Status switch
    {
        "Completed" => "#E8F5E9",
        "RolledBack" => "#FFF3E0",
        "Failed" => "#FFEBEE",
        _ => "#F5F5F5"
    };

    public string StatusBadgeForeground => Status switch
    {
        "Completed" => "#2E7D32",
        "RolledBack" => "#E65100",
        "Failed" => "#C62828",
        _ => "#616161"
    };
}

/// <summary>
/// ViewModel for the Expandable History & Log Viewer.
/// Provides paginated querying over SQLite OperationJournalEntry, filtering, expandable details,
/// single & batch rollbacks, and memory-efficient streaming export.
/// </summary>
public partial class HistoryViewModel : ObservableObject
{
    private readonly IDbContextFactory<TidyUpDbContext>? _contextFactory;
    private readonly TidyUpDbContext? _directContext;
    private readonly IRollbackEngine _rollbackEngine;

    /// <summary>
    /// Delegate for confirmation before executing rollback. Testable in headless tests.
    /// </summary>
    public Func<string, string, bool> ConfirmRollbackHandler { get; set; }

    [ObservableProperty]
    private ObservableCollection<HistoryRowItemViewModel> _historyEntries = new();

    [ObservableProperty]
    private HistoryRowItemViewModel? _selectedEntry;

    // Pagination
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPreviousPage))]
    [NotifyPropertyChangedFor(nameof(HasNextPage))]
    [NotifyPropertyChangedFor(nameof(PageSummary))]
    private int _currentPage = 1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TotalPages))]
    [NotifyPropertyChangedFor(nameof(HasPreviousPage))]
    [NotifyPropertyChangedFor(nameof(HasNextPage))]
    [NotifyPropertyChangedFor(nameof(PageSummary))]
    private int _pageSize = 25;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TotalPages))]
    [NotifyPropertyChangedFor(nameof(HasPreviousPage))]
    [NotifyPropertyChangedFor(nameof(HasNextPage))]
    [NotifyPropertyChangedFor(nameof(PageSummary))]
    private int _totalRecords;

    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalRecords / PageSize) : 0;
    public bool HasPreviousPage => CurrentPage > 1;
    public bool HasNextPage => CurrentPage < TotalPages;
    public string PageSummary => TotalRecords == 0
        ? "No entries"
        : $"Page {CurrentPage} of {Math.Max(1, TotalPages)} ({TotalRecords} total entries)";

    public ObservableCollection<int> PageSizeOptions { get; } = new() { 25, 50, 100 };

    // Filters
    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private DateTime? _startDate;

    [ObservableProperty]
    private DateTime? _endDate;

    [ObservableProperty]
    private string _selectedRuleFilter = "All Rules";

    [ObservableProperty]
    private ObservableCollection<string> _ruleFilterOptions = new() { "All Rules" };

    [ObservableProperty]
    private string _selectedActionTypeFilter = "All Actions";

    [ObservableProperty]
    private ObservableCollection<string> _actionTypeFilterOptions = new()
    {
        "All Actions",
        "Move",
        "Copy",
        "Rename",
        "ChangeExtension",
        "Delete",
        "ExtractArchive",
        "RunCommand"
    };

    [ObservableProperty]
    private string _selectedStatusFilter = "All Statuses";

    [ObservableProperty]
    private ObservableCollection<string> _statusFilterOptions = new()
    {
        "All Statuses",
        "Completed",
        "RolledBack",
        "Failed"
    };

    // State
    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _statusMessage = "Ready";

    public HistoryViewModel(
        IRollbackEngine rollbackEngine,
        IDbContextFactory<TidyUpDbContext>? contextFactory = null,
        TidyUpDbContext? directContext = null)
    {
        _rollbackEngine = rollbackEngine ?? throw new ArgumentNullException(nameof(rollbackEngine));
        _contextFactory = contextFactory;
        _directContext = directContext;

        if (_contextFactory == null && _directContext == null)
        {
            throw new ArgumentException("Either contextFactory or directContext must be provided.");
        }

        ConfirmRollbackHandler = DefaultConfirmRollback;
    }

    private static bool DefaultConfirmRollback(string message, string title)
    {
        return System.Windows.MessageBox.Show(
            message,
            title,
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning) == System.Windows.MessageBoxResult.Yes;
    }

    private async Task<(TidyUpDbContext Context, bool MustDispose)> GetContextAsync(CancellationToken cancellationToken)
    {
        if (_contextFactory != null)
        {
            var ctx = await _contextFactory.CreateDbContextAsync(cancellationToken);
            return (ctx, true);
        }

        return (_directContext!, false);
    }

    /// <summary>
    /// Loads a paginated page of history journal entries applying current filter parameters.
    /// </summary>
    [RelayCommand]
    public async Task LoadPageAsync(CancellationToken cancellationToken = default)
    {
        IsLoading = true;
        StatusMessage = "Loading history records...";

        var (context, mustDispose) = await GetContextAsync(cancellationToken);
        try
        {
            var query = context.OperationJournal.AsNoTracking().AsQueryable();

            // Apply filters
            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                var term = SearchText.Trim().ToLower();
                query = query.Where(e =>
                    e.OriginalPath.ToLower().Contains(term) ||
                    (e.TargetPath != null && e.TargetPath.ToLower().Contains(term)) ||
                    (e.Details != null && e.Details.ToLower().Contains(term)) ||
                    (e.RuleName != null && e.RuleName.ToLower().Contains(term)));
            }

            if (StartDate.HasValue)
            {
                var startUtc = StartDate.Value.Date.ToUniversalTime();
                query = query.Where(e => e.Timestamp >= startUtc);
            }

            if (EndDate.HasValue)
            {
                var endUtc = EndDate.Value.Date.AddDays(1).AddTicks(-1).ToUniversalTime();
                query = query.Where(e => e.Timestamp <= endUtc);
            }

            if (!string.IsNullOrEmpty(SelectedRuleFilter) && SelectedRuleFilter != "All Rules")
            {
                query = query.Where(e => e.RuleName == SelectedRuleFilter);
            }

            if (!string.IsNullOrEmpty(SelectedActionTypeFilter) && SelectedActionTypeFilter != "All Actions")
            {
                query = query.Where(e => e.ActionType == SelectedActionTypeFilter);
            }

            if (!string.IsNullOrEmpty(SelectedStatusFilter) && SelectedStatusFilter != "All Statuses")
            {
                query = query.Where(e => e.Status == SelectedStatusFilter);
            }

            // Total count for pagination
            TotalRecords = await query.CountAsync(cancellationToken);

            // Adjust current page if past end
            if (CurrentPage > TotalPages && TotalPages > 0)
            {
                CurrentPage = TotalPages;
            }
            if (CurrentPage < 1)
            {
                CurrentPage = 1;
            }

            // Page query
            var skip = (CurrentPage - 1) * PageSize;
            var entries = await query
                .OrderByDescending(e => e.Timestamp)
                .Skip(skip)
                .Take(PageSize)
                .ToListAsync(cancellationToken);

            HistoryEntries.Clear();
            foreach (var entry in entries)
            {
                HistoryEntries.Add(new HistoryRowItemViewModel(entry));
            }

            // Populate unique rules in filter options if empty
            if (RuleFilterOptions.Count <= 1)
            {
                var ruleNames = await context.OperationJournal
                    .Select(e => e.RuleName)
                    .Where(r => !string.IsNullOrEmpty(r))
                    .Distinct()
                    .OrderBy(r => r)
                    .ToListAsync(cancellationToken);

                RuleFilterOptions.Clear();
                RuleFilterOptions.Add("All Rules");
                foreach (var r in ruleNames)
                {
                    RuleFilterOptions.Add(r!);
                }
            }

            StatusMessage = $"Showing {HistoryEntries.Count} of {TotalRecords} records.";
            OnPropertyChanged(nameof(TotalPages));
            OnPropertyChanged(nameof(HasPreviousPage));
            OnPropertyChanged(nameof(HasNextPage));
            OnPropertyChanged(nameof(PageSummary));
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading history: {ex.Message}";
        }
        finally
        {
            if (mustDispose)
            {
                await context.DisposeAsync();
            }
            IsLoading = false;
        }
    }

    [RelayCommand(CanExecute = nameof(HasNextPage))]
    public async Task NextPageAsync()
    {
        if (HasNextPage)
        {
            CurrentPage++;
            await LoadPageAsync();
        }
    }

    [RelayCommand(CanExecute = nameof(HasPreviousPage))]
    public async Task PreviousPageAsync()
    {
        if (HasPreviousPage)
        {
            CurrentPage--;
            await LoadPageAsync();
        }
    }

    [RelayCommand]
    public async Task FirstPageAsync()
    {
        CurrentPage = 1;
        await LoadPageAsync();
    }

    [RelayCommand]
    public async Task LastPageAsync()
    {
        CurrentPage = Math.Max(1, TotalPages);
        await LoadPageAsync();
    }

    [RelayCommand]
    public async Task ClearFiltersAsync()
    {
        SearchText = string.Empty;
        StartDate = null;
        EndDate = null;
        SelectedRuleFilter = "All Rules";
        SelectedActionTypeFilter = "All Actions";
        SelectedStatusFilter = "All Statuses";
        CurrentPage = 1;
        await LoadPageAsync();
    }

    /// <summary>
    /// Reverses a single operation in the journal.
    /// </summary>
    [RelayCommand]
    public async Task RollbackOperationAsync(HistoryRowItemViewModel item)
    {
        if (item == null || !item.CanRollback)
            return;

        var confirmMsg = $"Are you sure you want to rollback the '{item.ActionType}' operation for:\n{item.OriginalPath}?";
        if (!ConfirmRollbackHandler(confirmMsg, "Confirm Undo Operation"))
            return;

        IsLoading = true;
        StatusMessage = $"Rolling back operation {item.OperationId}...";

        try
        {
            var result = await _rollbackEngine.RollbackOperationAsync(item.OperationId);
            if (result.Success)
            {
                item.Status = "RolledBack";
                item.RolledBackAt = DateTime.UtcNow;
                item.RollbackMessage = "Operation successfully reversed.";
                StatusMessage = $"Rollback succeeded: {result.Message}";
            }
            else
            {
                item.RollbackMessage = $"Rollback blocked: {result.Message}";
                StatusMessage = $"Rollback conflict: {result.Message}";
            }
        }
        catch (Exception ex)
        {
            item.RollbackMessage = $"Rollback error: {ex.Message}";
            StatusMessage = $"Rollback failed: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Reverses all operations associated with a batch.
    /// </summary>
    [RelayCommand]
    public async Task RollbackBatchAsync(HistoryRowItemViewModel item)
    {
        if (item == null)
            return;

        var confirmMsg = $"Are you sure you want to rollback all operations in batch:\n{item.BatchId}?";
        if (!ConfirmRollbackHandler(confirmMsg, "Confirm Undo Batch"))
            return;

        IsLoading = true;
        StatusMessage = $"Rolling back batch {item.BatchId}...";

        try
        {
            var result = await _rollbackEngine.RollbackBatchAsync(item.BatchId);
            await LoadPageAsync();
            StatusMessage = $"Batch rollback completed: {result.RolledBackCount} reversed, {result.FailedCount} failed.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Batch rollback failed: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Memory-efficient streaming export of history journal records to CSV format.
    /// Queries the SQLite database in chunks to prevent unbounded RAM usage.
    /// </summary>
    public async Task ExportToCsvAsync(string filePath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(filePath);

        var (context, mustDispose) = await GetContextAsync(cancellationToken);
        try
        {
            using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
            using var writer = new StreamWriter(fileStream);

            // Write CSV Header
            await writer.WriteLineAsync("OperationId,BatchId,Timestamp,ActionType,RuleName,OriginalPath,TargetPath,PreActionHash,PostActionHash,Status,RolledBackAt,Details");

            const int chunkSize = 500;
            int offset = 0;
            bool hasMore = true;

            while (hasMore && !cancellationToken.IsCancellationRequested)
            {
                var chunk = await context.OperationJournal
                    .AsNoTracking()
                    .OrderByDescending(e => e.Timestamp)
                    .Skip(offset)
                    .Take(chunkSize)
                    .ToListAsync(cancellationToken);

                if (chunk.Count == 0)
                {
                    hasMore = false;
                    break;
                }

                foreach (var entry in chunk)
                {
                    var line = string.Join(",",
                        EscapeCsv(entry.OperationId.ToString()),
                        EscapeCsv(entry.BatchId.ToString()),
                        EscapeCsv(entry.Timestamp.ToString("o")),
                        EscapeCsv(entry.ActionType),
                        EscapeCsv(entry.RuleName ?? string.Empty),
                        EscapeCsv(entry.OriginalPath),
                        EscapeCsv(entry.TargetPath ?? string.Empty),
                        EscapeCsv(entry.PreActionHash ?? string.Empty),
                        EscapeCsv(entry.PostActionHash ?? string.Empty),
                        EscapeCsv(entry.Status),
                        EscapeCsv(entry.RolledBackAt?.ToString("o") ?? string.Empty),
                        EscapeCsv(entry.Details ?? string.Empty));

                    await writer.WriteLineAsync(line);
                }

                offset += chunk.Count;
                if (chunk.Count < chunkSize)
                {
                    hasMore = false;
                }
            }

            await writer.FlushAsync();
            StatusMessage = $"Exported {offset} records to {Path.GetFileName(filePath)}";
        }
        finally
        {
            if (mustDispose)
            {
                await context.DisposeAsync();
            }
        }
    }

    /// <summary>
    /// Memory-efficient streaming export of history journal records to JSON format.
    /// </summary>
    public async Task ExportToJsonAsync(string filePath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(filePath);

        var (context, mustDispose) = await GetContextAsync(cancellationToken);
        try
        {
            using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
            using var writer = new Utf8JsonWriter(fileStream, new JsonWriterOptions { Indented = true });

            writer.WriteStartArray();

            const int chunkSize = 500;
            int offset = 0;
            bool hasMore = true;

            while (hasMore && !cancellationToken.IsCancellationRequested)
            {
                var chunk = await context.OperationJournal
                    .AsNoTracking()
                    .OrderByDescending(e => e.Timestamp)
                    .Skip(offset)
                    .Take(chunkSize)
                    .ToListAsync(cancellationToken);

                if (chunk.Count == 0)
                {
                    hasMore = false;
                    break;
                }

                foreach (var entry in chunk)
                {
                    writer.WriteStartObject();
                    writer.WriteString("operationId", entry.OperationId);
                    writer.WriteString("batchId", entry.BatchId);
                    writer.WriteString("timestamp", entry.Timestamp);
                    writer.WriteString("actionType", entry.ActionType);
                    writer.WriteString("ruleName", entry.RuleName);
                    writer.WriteString("originalPath", entry.OriginalPath);
                    writer.WriteString("targetPath", entry.TargetPath);
                    writer.WriteString("preActionHash", entry.PreActionHash);
                    writer.WriteString("postActionHash", entry.PostActionHash);
                    writer.WriteString("status", entry.Status);
                    if (entry.RolledBackAt.HasValue)
                        writer.WriteString("rolledBackAt", entry.RolledBackAt.Value);
                    else
                        writer.WriteNull("rolledBackAt");
                    writer.WriteString("details", entry.Details);
                    writer.WriteEndObject();
                }

                offset += chunk.Count;
                if (chunk.Count < chunkSize)
                {
                    hasMore = false;
                }
            }

            writer.WriteEndArray();
            await writer.FlushAsync();
            StatusMessage = $"Exported {offset} records to {Path.GetFileName(filePath)}";
        }
        finally
        {
            if (mustDispose)
            {
                await context.DisposeAsync();
            }
        }
    }

    private static string EscapeCsv(string value)
    {
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }
        return value;
    }

    partial void OnPageSizeChanged(int value)
    {
        CurrentPage = 1;
        _ = LoadPageAsync();
    }
}

