# TidyUp v1.2.0 Remediation: Active Handoff Record

**Repository / Branch / Current SHA:** TidyUp on main @ ae0380f  
**Plan Revision & Location:** Revision 2 (docs/REMEDIATION_TASK_CATALOG_v1.2.0.md)  
**Last Verified Phase Gate:** Phase 6 Gate Passed; Ready for Phase 7 (Visual & Integrated Acceptance)  
**Active Task & Current State:** TASK-REM-36 (DONE) -> Next: Phase 7 Gate / TASK-REM-37 (Dark theme glyph & empty state contrast polish)  
**Completed Changes & Files:**
- TASK-REM-36 (SHA: ae0380f):
  - ScheduleSettingsView.xaml & RuleRevisionDiffView.xaml: Added explicit `[Integration Pending]` badges and honest disclaimer notices to headers disclosing that scheduled monitoring, quiet hours, power throttling, and automated rule revision snapshots are integration-pending background components.
  - FoldersViewModel.cs: Flags remote UNC network shares (`\\server\share`) with `"Pending (Network Share)"` watcher status rather than misleadingly indicating "Watching"; displays `[Integration Pending]` notification upon adding a network directory.
  - docs/FEATURE_MATRIX.md & docs/remediation/FEATURE_MATRIX.md: Systematically updated feature matrix to truthfully distinguish verified runtime features from integration-pending components (network share monitoring, scheduled execution loop, rule revision history, duplicate file detection workflow).
  - FoldersViewModelTests.cs: Unit tests verify `[Integration Pending]` notice on network folder addition and catalog synchronization. 772 tests passing.
- TASK-REM-35 (SHA: dbc0650):
  - AppSettings.cs & SettingsView.xaml: Locked `MaxConcurrentOperations` to 1 (enforced serial per ADR-01) with disabled controls and honest explanatory text. Contained `NetworkFolderPollingIntervalSeconds` with `[Integration Pending]` badge and disabled state. Made `DatabaseBackupPath` editable with directory path hint and "Browse..." button.
  - FileMonitorService.cs: Injected `AppSettings` and wired `DebounceDelayMs` into watcher debounce timer.
  - ActionExecutor.cs: Injected `AppSettings` and wired `FileLockRetryCount` and `FileLockRetryDelayMs` into `WaitForFileReadyAsync`.
  - ExecutionPlanGenerator.cs: Injected `AppSettings` and wired `ScanBatchSize`.
  - ActionEditorViewModel.cs: Injected `AppSettings` and wired `DefaultConflictResolution` and `UseRecycleBinByDefault` into action creation (`AddMoveAction`, `AddCopyAction`, `AddDeleteAction`).
  - RuleWizardViewModel.cs & PreviewViewModel.cs: Injected `AppSettings` into Rule Wizard and wired `RequireDestructiveConfirmations` into `PreviewViewModel.ApplyChangesAsync` to gate Safeguard modal evaluation.
  - DatabaseBackupManager.cs: Injected `AppSettings` and honors custom `DatabaseBackupPath` for database snapshots.
  - SettingsViewModel.cs: Added `DefaultBackupsDirectory` and `BrowseBackupDirectoryCommand` with `OpenFolderDialog`.
  - App.xaml.cs: Wires background log cleanup on startup when `AutoCleanupLogs` is true using `LogRetentionDays`.
  - SettingsCategorizedExperienceTests.cs: Unit tests verify `BrowseBackupDirectoryCommand`, `ActionEditorViewModel` defaults from `AppSettings`, and `DatabaseBackupManager` custom backup path. 770 tests passing.
- TASK-REM-06 (SHA: 0608e7b):
  - IDbIntegrityChecker.cs & DbIntegrityChecker.cs: Implemented `VacuumDatabaseAsync(string dbPath)` returning `DatabaseMaintenanceResult`. Executes `PRAGMA wal_checkpoint(TRUNCATE);` followed by `VACUUM;` to checkpoint all dirty pages into the main database and truncate the WAL file before vacuuming. Measures pre/post file and WAL/SHM sizes and execution duration.
  - SettingsViewModel.cs: Injected `IDbIntegrityChecker` and `TidyUpPaths`. Replaced dummy `Task.Delay(300)` simulation in `VacuumDatabaseAsync` with real SQLite vacuum call. Reports exact reclaimed bytes/KB and elapsed duration, or honestly displays "Database already compact (0 bytes reclaimed)".
  - SettingsCategorizedExperienceTests.cs: Added unit tests verifying pre/post size reduction on populated and deleted SQLite databases, compact database handling, and status message reporting. 767 tests passing.
- TASK-REM-33 (SHA: 74bd9b9):
  - HistoryViewModel.cs: Unified query building in `BuildFilteredQuery` shared by pagination and streaming exports. Added `filteredOnly` parameter (default true when filters active) to `ExportToCsvAsync` and `ExportToJsonAsync` streaming matching records across all pages. Mitigated CSV spreadsheet formula injection in `EscapeCsv` by escaping leading `=`, `+`, `-`, `@`, and `\t` characters.
  - HistoryView.xaml & HistoryView.xaml.cs: Updated export buttons to display dynamic labels ("Export Filtered CSV ({n})") and added context menus offering explicit choices between "Export Filtered Records" and "Export All History".
  - HistoryViewModelTests.cs: Unit tests verify filtered CSV/JSON queries across pages, export-all bypass, and spreadsheet formula escaping. 765 tests passing.
- TASK-REM-32 (SHA: bad0f7b):
  - HistoryViewModel.cs: Added `[NotifyCanExecuteChangedFor(nameof(NextPageCommand))]` and `[NotifyCanExecuteChangedFor(nameof(PreviousPageCommand))]` on `_currentPage`, `_pageSize`, and `_totalRecords`. Added explicit `NotifyCanExecuteChanged()` calls on page change, page size change, total records change, and upon completion of `LoadPageAsync()`.
  - HistoryViewModelTests.cs: Added unit test `PaginationCommands_UpdateCanExecuteEligibilityOnDataLoadAndTraversal` testing command eligibility across zero records, boundary transitions, next/prev executions, and page size modifications. 762 tests passing.
- TASK-REM-31 (SHA: 7ad63cb):
  - IOperationTelemetryService.cs & OperationTelemetryService.cs: Created operation telemetry service that streams committed executed operations, deduplicates duplicate events by ActionId, and writes durable `ActionLogEntity` records to `IActionLogRepository`.
  - ActionExecutor.cs: Injected `IOperationTelemetryService` and emits telemetry events across both `ExecuteActionAsync` and `ExecutePlannedActionAsync` on completion/failure.
  - BatchProcessingCoordinator.cs: Injected `IRuleRepository` / `IServiceScopeFactory`; updates `Rule.LastRunDate = DateTime.UtcNow` and increments `Rule.FilesProcessedCount` by distinct input files processed per rule run (preventing multi-action chains from duplicate counting).
  - DashboardViewModel.cs: Injected `IOperationTelemetryService` and subscribes to live execution events, immediately updating `TotalFilesProcessed`, `TotalSuccess`, `TotalWarnings`, `TotalErrors`, `SuccessRateFormatted`, and `RecentActivity` feed.
  - LogsViewModel.cs: Injected `IOperationTelemetryService` and appends incoming execution events matching active filters live into the log feed.
  - OperationTelemetryConvergenceTests.cs: Unit tests verify telemetry deduplication, durable persistence, live dashboard metrics reactivity, live log streaming, and distinct input file counting. 761 tests passing.
- TASK-REM-30 (SHA: e198ea1):
  - ITrayIconService.cs & TrayIconService.cs: Implemented per-folder health tracking in `ConcurrentDictionary<string, WatcherHealthStatus>`. `RecomputeAggregateStatus` calculates aggregate status prioritizing Disconnected/Error/Degraded over Healthy; a healthy event on one folder never clears an active warning or error on another folder.
  - Retained branded base application icon with subtle 6x6 anti-aliased badge overlay dot in bottom-right corner instead of replacing the entire icon with a solid colored circle.
  - Added native P/Invoke `DestroyIcon` to explicitly clean up unmanaged HICON handles allocated via `bitmap.GetHicon()` on every icon swap and on `Dispose()`, preventing GDI handle resource leaks.
  - TrayIconAndToastNotificationTests.cs: Unit tests verify multi-folder health aggregation (healthy sibling does not clear warning) and native HICON creation and disposal. 757 tests passing.
- TASK-REM-29 (SHA: dee8400):
  - IWindowCloseCoordinator.cs & WindowCloseCoordinator.cs: Implemented unified window close coordinator eliminating event-handler races between MainWindow and App. Evaluates close actions (CloseAndExit, MinimizeToTray, Cancel) across active batches, dirty drafts, explicit exit, and tray preferences.
  - App.xaml.cs: On startup, preserves restored WindowState (e.g. Maximized) without forcing WindowState.Normal; RequestApplicationExitAsync polls IApplicationStateManager to wait for active batch completion with timeout protection; static TrayIcon and IsExiting properties exposed for coordination.
  - MainWindow.xaml.cs: Injected IWindowCloseCoordinator into MainWindow.OnMainWindowClosing; aborts closing when user declines discard or waits for batch, preserving window visibility; minimizes to tray only when permitted by coordinator.
  - WindowLifecycleTests.cs: Unit tests verify dirty discard cancellation, batch wait prompt, minimize-to-tray gating, explicit exit overrides, and shutdown timeout protection. 755 tests passing.
- TASK-REM-07 (SHA: 18afd1d):
  - MonitoredFolderCatalog.cs & IMonitoredFolderCatalog.cs: Implemented persistent standalone monitored folder catalog storage in monitored_folders.json with stable GUID identifiers, user exclusions, and recursion preferences. Standalone folders survive catalog refreshes and process restarts without requiring attached rules. Added persistent folder pause overrides surviving restart.
  - IFileMonitorService.cs & FileMonitorService.cs: Added PauseFolder, ResumeFolder, and IsFolderPaused methods. Watchers ignore and suppress file detection events for paused folders, and automated scans skip paused directories.
  - FoldersViewModel.cs: AddMonitoredFolderAsync persists added directory to catalog and provides guidance to create rules. TogglePauseWatcherAsync dispatches directly to IFileMonitorService.PauseFolder/ResumeFolder and persists state in catalog; on persistence failure, reverts optimistic UI state and displays a truthful error message.
  - MonitoredFolderCatalogTests.cs & FoldersViewModelTests.cs: Unit tests verify folder persistence, pause/resume dispatch, optimistic UI revert on failure, and reconstruction across application restarts. 746 tests passing.
- TASK-REM-24 (SHA: cf4f107):
  - IMonitoringCoordinator.cs & MonitoringCoordinator.cs: Implemented application monitoring coordinator orchestrating live watcher events from IFileMonitorService. Evaluates active rules, filters manual-only triggers, generates single-file plans via ExecutionPlanGenerator.GeneratePlanForFileAsync, and queues plans to IBatchProcessingCoordinator with automatic fallback authorization for non-destructive actions.
  - IExecutionPlanGenerator.cs & ExecutionPlanGenerator.cs: Implemented GeneratePlanForFileAsync(string filePath, IEnumerable<Rule> rules, CancellationToken ct) producing scoped single-file execution plans.
  - ServiceConfiguration.cs: Registered IMonitoringCoordinator as a singleton in the DI container.
  - App.xaml.cs: Activated IMonitoringCoordinator.StartAsync on startup, and clean asynchronous shutdown in OnExit.
  - MainWindowViewModel.cs: Injected IMonitoringCoordinator and triggered NotifyRulesChangedAsync() on rule save, delete, and reorder.
  - MonitoringCoordinatorIntegrationTests.cs: Integration tests prove end-to-end event subscription, plan generation, and safe execution through BatchProcessingCoordinator writing durable journal entries. 739 tests passing.
- `TASK-REM-26` (SHA: `f40f44b`):
  - `FileMonitorService.cs`: Merged watcher folder scope across rules watching the same canonical directory. Configures `IncludeSubdirectories = true` if ANY rule requires recursion; filters files against individual rule subfolder and exclusion scopes during dispatch rather than dropping files prematurely at the shared watcher level.
  - `HardenedFileSystemWatcher.cs`: Exposed `IncludeSubdirectories` property reflecting underlying watcher state.
  - `FileMonitorServiceTests.cs`: Unit tests verify watcher superset configuration, subfolder dispatch for recursive rules alongside top-level rules, and wildcard exclusion matching. 738 tests passing.
- `TASK-REM-25` (SHA: `9ed8679`):
  - `FileMonitorService.cs`: Separated external application lifetime token (`_applicationStoppingToken`) from internal run-loop token (`_runCts`). Serialized lifecycle transitions via `SemaphoreSlim`. In `UpdateRulesAsync`, stops internal watchers and re-instantiates a healthy, non-cancelled linked `_runCts` so file monitoring resumes reliably after rule edits.
  - `FileMonitorServiceTests.cs`: Unit tests verify start → update → stop lifecycle, healthy token state after restart, and lifetime cancellation propagation. 735 tests passing.
- `TASK-REM-21` (SHA: `187eca3`):
  - `PreviewViewModel.cs` & `PlannedActionItemViewModel`: Enforced single domain eligibility predicate `CanBeApplied` (`IsApproved && !HasConflict && !IsExecuted && !IsSkipped`) and `CanToggleApproval`. Introduced `PendingApprovedCount` and wired `CanApplyChanges` directly to `PendingApprovedCount > 0`.
  - Harmonized `ApplyButtonText` to reflect pending approved changes, terminal success ("All approved changes applied"), failures ("Execution completed with failures"), or cancellation without permitting completed actions to be replayed.
  - `PreviewView.xaml`: Bound approval checkbox `IsEnabled` to `CanToggleApproval`, disabling execution re-toggling on completed or conflicted rows.
  - `PreviewViewModelTests.cs`: Unit tests verify automatic Apply button disabling, terminal caption accuracy, failure handling, and execution immutability. 732 tests passing.
- `TASK-REM-23` (SHA: `993f832`):
  - `IExecutionDiscrepancyAnalyzer.cs` & `ExecutionDiscrepancyAnalyzer.cs`: Added `AnalyzeFromResults` overload accepting `IEnumerable<BatchItemExecutionResult>`, correlating actions by `PlannedFileAction.Id` (Guid) instead of collapsing duplicates by `SourcePath`. Captures actual renamed target paths in discrepancy reports.
  - `PreviewViewModel.cs`: Feeds actual `batchResult.ItemResults` directly into `_discrepancyAnalyzer.AnalyzeFromResults` instead of fabricating synthetic `ActionResult` records from UI properties. Updates viewmodel target paths upon physical execution completion.
  - `ExecutionDiscrepancyAnalyzerTests.cs`: Unit tests prove correlation by Action ID with identical source paths and verification of actual renamed destination paths. 729 tests passing.
- `TASK-REM-01` (SHA: `761fcfc`):
  - `RollbackEngine.cs`: Enforced truthful rollback contract by populating `RollbackOutcome` across all operations (`Restored`, `ManualRestoreRequired`, `NotRecoverable`, `Failed`). `RollbackDeleteAsync` returns `ManualRestoreRequired` with instructions for Recycle Bin files and `NotRecoverable` for permanently deleted files, never falsely claiming restoration.
  - `HistoryViewModel.cs`: `HistoryRowItemViewModel.CanRollback` disables Undo for `ExtractArchive`, `RunCommand`, and permanent deletion entries. `RollbackOperationAsync` transitions status to "Manual Action Required" instead of "RolledBack" when `Outcome == ManualRestoreRequired`.
  - `RollbackEngineTests.cs` & `HistoryViewModelTests.cs`: Unit tests verify accurate outcomes, database record preservation, and UI button gating. 727 tests passing.
- `TASK-REM-05` (SHA: `68fdedd`):
  - `ActionExecutor.cs`: Rewrote `RemoveEmptyFoldersAsync` to strictly clamp recursive ascent to `stopBoundaryPath`. Deletes empty subdirectories up to the boundary, but never deletes the boundary itself, its ancestors, or folders outside the boundary.
  - `ActionExecutor.cs` & `ExecutionPlanGenerator.cs`: Propagate `MonitoredSourceRoot` into `MoveFileAction` and `DeleteFileAction` and pass it to `RemoveEmptyFoldersAsync` in `ExecuteMoveAsync` and `ExecuteDeleteAsync`.
  - `ActionExecutorTests.cs`: Unit tests verify empty parent cleanup stops exactly at the monitored root boundary and preserves the monitored root folder even when it becomes completely empty. 723 tests passing.
- `TASK-REM-02` (SHA: `f118035`):
  - `FileAction.cs` (`ExtractArchiveAction`): Added `UseRecycleBinForArchiveDeletion` (default true) and `ConfirmBeforeDelete` (default true).
  - `ActionExecutor.cs` (`ExecuteExtractArchiveAsync`): Switched post-extraction archive deletion from raw `File.Delete` to `_safeFileSystem.DeleteFileSafelyAsync(..., useRecycleBin: action.UseRecycleBinForArchiveDeletion, allowPermanentFallback: allowPermanentFallback)`, preventing unrecoverable data loss.
  - `ExecutionPlanGenerator.cs` (`SimulateAction`): Flags extraction conflict when `OverwriteExisting == true` and destination directory already contains files.
  - `SafeguardConfirmationViewModel.cs` (`EvaluatePlannedActions`): Flags permanent deletion/archive deletion risk for `ExtractArchiveAction` with `DeleteAfterExtraction`, and flags overwrite risk for `ExtractArchiveAction` with `OverwriteExisting`.
  - `ActionExecutorTests.cs` & `SafeguardConfirmationViewModelTests.cs`: Unit tests verify safe deletion routing via `ISafeFileSystem` and safeguard risk evaluation. 721 tests passing.
- TASK-REM-04 (SHA: 10f995e):
  - ActionExecutor.cs: Overwrite conflict strategy stages displaced backups of existing destination files before mutation across ExecuteMoveAsync, ExecuteCopyAsync, ExecuteRenameAsync, and ExecuteChangeExtensionAsync. If mutation throws, displaced backup is restored automatically to prevent data loss. ConflictResolution.Prompt returns ActionResultType.Warning with fail-closed message in automated batch runs instead of silently overwriting.
  - ActionExecutorTests.cs: Unit tests verify safe displaced backup creation and restore on mutation failure, as well as Prompt conflict strategy fail-closed behavior. 717 tests passing.

- `TASK-REM-03` (SHA: `9abbccd`):
  - `ExecutionAuthorizationContext.cs`: Added execution authorization context validating plan, action ID, permanent delete paths, and overwrite paths.
  - `PlannedFileAction.cs`: Added `IsAuthorizedForPermanentDeletion` and `AuthorizationContext`.
  - `ActionExecutor.cs`: Decoupled `allowPermanentFallback` from `ConfirmBeforeDelete` preference; authorized permanent deletion succeeds even when `ConfirmBeforeDelete == true`, while unauthorized deletions fail closed.
  - `PreviewViewModel.cs`: Issues scoped execution authorization context upon safeguard modal confirmation.
  - `SafeDeleteTests.cs`: Unit tests verify authorized permanent delete with `ConfirmBeforeDelete = true`, and unauthorized/mismatched fail-closed rejection. 714 tests passing.

- `TASK-REM-20` (SHA: `79b1ac2`):
  - `IApplicationStateManager.cs` & `ApplicationStateManager.cs`: Added `TryAcquireBatchLease` and `ReleaseBatchLease`. State machine holds `ExecutingBatch` lifecycle state across all files during a batch; `NotifyOperationCompleted()` no longer prematurely transitions state back to `Idle` while an active batch lease is held.
  - `BatchProcessingCoordinator.cs`: Added atomic admission guard via `Interlocked.CompareExchange(ref _executingFlag, 1, 0)` rejecting concurrent batch executions; acquires exclusive batch lease at batch startup and releases lease in `finally` without leaking state or leaving application locked.
  - `BatchCancellationAndPauseTests.cs`: Unit tests verify continuous `ExecutingBatch` state across serial files with zero intermediate flickering, and confirm atomic rejection of concurrent batch executions. 711 tests passing.

- `TASK-REM-19` (SHA: `2ec3b27`):
  - `PlannedFileAction.cs`: Added `TargetFileName` and `PlannedCounter` properties to store pre-resolved immutable parameters.
  - `ExecutionPlanGenerator.cs`: Populated `PlannedCounter` and `TargetFileName` during simulation.
  - `ActionExecutor.cs`: Updated `ExecutePlannedActionAsync` to execute immutable pre-resolved target paths and frozen counters across Move, Copy, Rename, and ChangeExtension actions.
  - `BatchProcessingCoordinator.cs`: Delegated batch item execution directly to `_actionExecutor.ExecutePlannedActionAsync(currentAction, batchId)` with fallback.
  - `BatchProcessingCoordinatorTests.cs`: Unit tests prove that partial batch selection (e.g. executing file #5 out of 5) preserves the exact frozen sequence number and target path approved in preview without counter drift. 709 tests passing.

- `TASK-REM-22` (SHA: `a38ec5b`):
  - `OperationJournalEntry.cs` & `PlannedFileAction.cs` & `FileAction.cs`: Rule metadata properties (`RuleId`, `RuleName`, `RuleRevisionId`) propagated from plan generator through file actions to durable journal entries.
  - `ActionExecutor.cs`: Journal entries recorded on both success (`Completed`) and failure (`Failed`) with pre-action and post-action checksums and error messages.
  - On `Overwrite` and `RenameOld` conflict resolutions, existing target files are safely moved to timestamped displaced backup paths, avoiding data loss and recording backup locations in `Details`.
  - `RollbackEngine.cs`: Failed execution entries reject rollback gracefully with `TargetMissing` reason without throwing; batch rollback only reverses completed actions.
  - `RollbackEngineTests.cs`: Unit tests verify journal entry persistence for success, failure, and displaced overwrite backup. 706 tests passing.

- `FoldersViewModel.cs`:
  - Added `_isScanning` observable property with `CanScan` / `ScanTooltip` notification dispatch to `MonitoredFolderItemViewModel`.
  - Injected `IWatcherReconciler? watcherReconciler = null` into `FoldersViewModel`.
  - Updated `ScanFolderNowAsync` to set `folder.IsScanning = true`, delegate to `_watcherReconciler.ReconcileFolderAsync`, enumerate matching files on disk, report count via `StatusMessage`, and safely reset `IsScanning = false` in `finally`.
- `MainWindowViewModel.cs`:
  - Injected `IWatcherReconciler` into fallback instantiation of `FoldersViewModel`.
- `FoldersView.xaml`:
  - Added indeterminate circular `ProgressBar` bound to `IsScanning` with `BooleanToVisibilityConverter`.
- `FoldersViewModelTests.cs` & `FolderScanAvailabilityTests.cs`:
  - Added unit tests for watcher reconciler delegation, unreadable folder scan prevention, and `IsScanning` / `CanScan` property notifications.
- `docs/remediation/TRACKER.md` & `FINDING_MATRIX.md`: Marked `TASK-REM-27` as `DONE` and `TU-04` as `FIXED_VERIFIED`.

**Last Executed Checks & Actual Results:**
- `dotnet test .\Tidy-up\TidyUp.sln -c Release`: Passed! 757 passed, 0 failed, 0 skipped (3.0s).
- Zero compilation errors or styling guard warnings across solution.

**Evidence Paths & Tested SHA:**
- SHA: `e198ea1`
- Tracking: `docs/remediation/TRACKER.md`
- Finding Matrix: `docs/remediation/FINDING_MATRIX.md`

**Unresolved Blocker or Product Decision:** None.

**Temporary Containment Still Active:**
- `TU-17`: Prompt conflict resolution fails closed/skips safely.
- `TU-20`: Undo Delete displays guidance without marking record "RolledBack".
- `TU-32`: Empty directory cleanup will not climb above immediate parent without boundary path.
- `TU-38`: Undo button disabled on irreversible ExtractArchive and RunCommand rows.

**Exact Next Command or Implementation Action:**
- Phase 5 complete. Await user review/confirmation to proceed to Phase 6 (`TASK-REM-31`: Operation stream convergence for Dashboard & Logs).
