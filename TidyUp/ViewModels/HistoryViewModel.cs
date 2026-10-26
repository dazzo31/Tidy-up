using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using TidyUp.Data;
using TidyUp.Data.Entities;
using TidyUp.Services.FileSystem;
using TidyUp.Services.Rollback;

namespace TidyUp.ViewModels;

/// <summary>
/// Provides user-actionable explanations for file operation failures and rollback conflicts.
/// </summary>
public static class ActionableErrorExplainer
{
    public static string ExplainError(string? rawError)
    {
        if (string.IsNullOrWhiteSpace(rawError))
            return string.Empty;

        var lower = rawError.ToLowerInvariant();

        if (lower.Contains("unauthorized") || lower.Contains("access is denied") || lower.Contains("access denied"))
            return "Permission Denied: The destination folder or file requires elevated permissions or is marked read-only. Check permissions in Windows Explorer.";

        if (lower.Contains("sharing violation") || lower.Contains("used by another process") || lower.Contains("locked"))
            return "File In Use: The file is currently open in another program (e.g. Office, media player, editor). Close the application and retry.";

        if (lower.Contains("directory not found") || lower.Contains("path not found") || lower.Contains("could not find a part of the path"))
            return "Folder Not Found: The destination drive or folder no longer exists or is disconnected. Check network drive connection.";

        if (lower.Contains("path too long") || lower.Contains("pathtoolongexception") || lower.Contains("filename or extension is too long"))
            return "Path Too Long: Path exceeds Windows 260-character limit. Move to a shorter folder or enable long paths in Windows.";

        if (lower.Contains("there is not enough space") || lower.Contains("disk full"))
            return "Disk Full: The destination drive does not have enough free disk space to complete this file operation.";

        if (lower.Contains("target file") && lower.Contains("was modified"))
            return "Safety Lock: The file was modified externally after being moved. Rollback was aborted to protect your changes.";

        if (lower.Contains("occupied") || lower.Contains("already exists"))
            return "Destination Conflict: Another file with the same name already exists at the destination path.";

        if (lower.Contains("file not found") || lower.Contains("no longer exists"))
            return "File Missing: The source file no longer exists or was moved prior to processing.";

        if (lower.Contains("already rolled back") || lower.Contains("already been rolled back"))
            return "Already Rolled Back: This operation was already reversed.";

        return $"Execution Issue: {rawError.Trim()}";
    }
}

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
    [NotifyPropertyChangedFor(nameof(HasError))]
    [NotifyPropertyChangedFor(nameof(ActionableExplanation))]
    [NotifyPropertyChangedFor(nameof(StatusBadgeBackground))]
    [NotifyPropertyChangedFor(nameof(StatusBadgeForeground))]
    private string _status;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanRollback))]
    private DateTime? _rolledBackAt;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    [NotifyPropertyChangedFor(nameof(ActionableExplanation))]
    private string? _rollbackMessage;

    [ObservableProperty]
    private bool _isDiagnosticsExpanded;

    [RelayCommand]
    public void ToggleDiagnostics()
    {
        IsDiagnosticsExpanded = !IsDiagnosticsExpanded;
    }

    public bool HasError => string.Equals(Status, "Failed", StringComparison.OrdinalIgnoreCase) ||
                            (!string.IsNullOrEmpty(RollbackMessage) && (RollbackMessage.Contains("conflict", StringComparison.OrdinalIgnoreCase) || RollbackMessage.Contains("error", StringComparison.OrdinalIgnoreCase) || RollbackMessage.Contains("blocked", StringComparison.OrdinalIgnoreCase)));

    public string ActionableExplanation => ActionableErrorExplainer.ExplainError(
        !string.IsNullOrEmpty(RollbackMessage) ? RollbackMessage : (Details ?? string.Empty));

    public string TechnicalDiagnostics => !string.IsNullOrEmpty(Details) ? Details : (RollbackMessage ?? "No additional diagnostic telemetry recorded.");

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

    public bool CanRollback => string.Equals(Status, "Completed", StringComparison.OrdinalIgnoreCase) 
        && RolledBackAt == null 
        && !string.Equals(ActionType, "ExtractArchive", StringComparison.OrdinalIgnoreCase) 
        && !string.Equals(ActionType, "RunCommand", StringComparison.OrdinalIgnoreCase)
        && !(string.Equals(ActionType, "Delete", StringComparison.OrdinalIgnoreCase) && Details != null && (Details.Contains("Permanently Deleted", StringComparison.OrdinalIgnoreCase) || Details.Contains("Permanent", StringComparison.OrdinalIgnoreCase)));

    public string ActionBadgeBackground => ActionType switch
    {
        "Move" => TidyUp.Styles.ThemeTokens.InfoBackground,
        "Copy" => TidyUp.Styles.ThemeTokens.TealBackground,
        "Rename" => TidyUp.Styles.ThemeTokens.PurpleBackground,
        "ChangeExtension" => TidyUp.Styles.ThemeTokens.PurpleBackground,
        "Delete" => TidyUp.Styles.ThemeTokens.ErrorBackground,
        "ExtractArchive" => TidyUp.Styles.ThemeTokens.NeutralBackground,
        "RunCommand" => TidyUp.Styles.ThemeTokens.NeutralBackground,
        _ => TidyUp.Styles.ThemeTokens.NeutralBackground
    };

    public string ActionBadgeForeground => ActionType switch
    {
        "Move" => TidyUp.Styles.ThemeTokens.Info,
        "Copy" => TidyUp.Styles.ThemeTokens.Teal,
        "Rename" => TidyUp.Styles.ThemeTokens.Purple,
        "ChangeExtension" => TidyUp.Styles.ThemeTokens.Purple,
        "Delete" => TidyUp.Styles.ThemeTokens.Error,
        "ExtractArchive" => TidyUp.Styles.ThemeTokens.Neutral,
        "RunCommand" => TidyUp.Styles.ThemeTokens.Neutral,
        _ => TidyUp.Styles.ThemeTokens.Neutral
    };

    public string StatusBadgeBackground => Status switch
    {
        "Completed" => TidyUp.Styles.ThemeTokens.SuccessBackground,
        "RolledBack" => TidyUp.Styles.ThemeTokens.WarningBackground,
        "Failed" => TidyUp.Styles.ThemeTokens.ErrorBackground,
        _ => TidyUp.Styles.ThemeTokens.NeutralBackground
    };

    public string StatusBadgeForeground => Status switch
    {
        "Completed" => TidyUp.Styles.ThemeTokens.Success,
        "RolledBack" => TidyUp.Styles.ThemeTokens.Warning,
        "Failed" => TidyUp.Styles.ThemeTokens.Error,
        _ => TidyUp.Styles.ThemeTokens.Neutral
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
    [NotifyCanExecuteChangedFor(nameof(NextPageCommand))]
    [NotifyCanExecuteChangedFor(nameof(PreviousPageCommand))]
    private int _currentPage = 1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TotalPages))]
    [NotifyPropertyChangedFor(nameof(HasPreviousPage))]
    [NotifyPropertyChangedFor(nameof(HasNextPage))]
    [NotifyPropertyChangedFor(nameof(PageSummary))]
    [NotifyCanExecuteChangedFor(nameof(NextPageCommand))]
    [NotifyCanExecuteChangedFor(nameof(PreviousPageCommand))]
    private int _pageSize = 25;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TotalPages))]
    [NotifyPropertyChangedFor(nameof(HasPreviousPage))]
    [NotifyPropertyChangedFor(nameof(HasNextPage))]
    [NotifyPropertyChangedFor(nameof(PageSummary))]
    [NotifyCanExecuteChangedFor(nameof(NextPageCommand))]
    [NotifyCanExecuteChangedFor(nameof(PreviousPageCommand))]
    private int _totalRecords;



    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalRecords / PageSize) : 0;
    public bool HasPreviousPage => CurrentPage > 1;
    public bool HasNextPage => CurrentPage < TotalPages;

    /// <summary>Set when the journal could not be read; the view shows an error card instead of an empty list.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasLoadError))]
    private string? _loadErrorMessage;

    /// <summary>Raw exception text, shown only in the collapsible details panel.</summary>
    [ObservableProperty]
    private string? _loadErrorDetails;

    public bool HasLoadError => !string.IsNullOrWhiteSpace(LoadErrorMessage);
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

    /// <summary>Copies the full failure text so a user can paste it into a bug report.</summary>
    [RelayCommand]
    public void CopyErrorDetails()
    {
        var text = string.IsNullOrWhiteSpace(LoadErrorDetails)
            ? (LoadErrorMessage ?? StatusMessage)
            : LoadErrorDetails!;

        try
        {
            System.Windows.Clipboard.SetText(text);
            StatusMessage = "Error details copied to the clipboard.";
        }
        catch (Exception ex)
        {
            // The clipboard can be locked by another process; not worth failing over.
            StatusMessage = $"Could not copy details: {ex.Message}";
        }
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
            LoadErrorMessage = null;
            LoadErrorDetails = null;
            OnPropertyChanged(nameof(TotalPages));
            OnPropertyChanged(nameof(HasPreviousPage));
            OnPropertyChanged(nameof(HasNextPage));
            OnPropertyChanged(nameof(PageSummary));
            NextPageCommand.NotifyCanExecuteChanged();
            PreviousPageCommand.NotifyCanExecuteChanged();
        }
        catch (Exception ex)
        {
            LoadErrorMessage = "History could not be read from the database.";
            LoadErrorDetails = ex.ToString();
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

        // Pre-rollback safety check: ensure the file has not been modified since the operation before reversing
        if (!string.IsNullOrWhiteSpace(item.TargetPath) && File.Exists(item.TargetPath) && !string.IsNullOrWhiteSpace(item.PostActionHash))
        {
            var currentHash = FileChecksumHelper.ComputeSha256(item.TargetPath);
            if (!string.Equals(currentHash, item.PostActionHash, StringComparison.OrdinalIgnoreCase))
            {
                var explanation = ActionableErrorExplainer.ExplainError("Target file was modified by an external process after operation.");
                item.RollbackMessage = $"Rollback blocked: {explanation}";
                StatusMessage = "Rollback aborted: Target file was modified after operation.";
                return;
            }
        }

        IsLoading = true;
        StatusMessage = $"Rolling back operation {item.OperationId}...";

        try
        {
            var result = await _rollbackEngine.RollbackOperationAsync(item.OperationId);
            if (result.Outcome == Models.Enums.RollbackOutcome.ManualRestoreRequired || result.ConflictReason == RollbackConflictReason.RecycleBinRestorationGuidance)
            {
                item.Status = "Manual Action Required";
                item.RollbackMessage = result.RestorationInstructions ?? result.Message;
                StatusMessage = "Manual action required: restore file from Windows Recycle Bin.";
            }
            else if (result.Outcome == Models.Enums.RollbackOutcome.Restored || result.Success)
            {
                item.Status = "RolledBack";
                item.RolledBackAt = DateTime.UtcNow;
                item.RollbackMessage = "Operation successfully reversed.";
                StatusMessage = $"Rollback succeeded: {result.Message}";
            }
            else
            {
                if (result.Outcome == Models.Enums.RollbackOutcome.NotRecoverable)
                {
                    item.Status = "Not Recoverable";
                }
                var explanation = ActionableErrorExplainer.ExplainError(result.Message);
                item.RollbackMessage = $"Rollback blocked: {explanation}";
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

    partial void OnCurrentPageChanged(int value)
    {
        NextPageCommand.NotifyCanExecuteChanged();
        PreviousPageCommand.NotifyCanExecuteChanged();
    }

    partial void OnTotalRecordsChanged(int value)
    {
        NextPageCommand.NotifyCanExecuteChanged();
        PreviousPageCommand.NotifyCanExecuteChanged();
    }

    partial void OnPageSizeChanged(int value)
    {
        CurrentPage = 1;
        NextPageCommand.NotifyCanExecuteChanged();
        PreviousPageCommand.NotifyCanExecuteChanged();
        _ = LoadPageAsync();
    }
}

