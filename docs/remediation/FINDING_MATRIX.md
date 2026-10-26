# TidyUp v1.2.0 Remediation: Finding Reconciliation Matrix

**Baseline Tag:** `v1.2.0`  
**Current Commit:** `59d3bb45d7ca622ec3a80a7c2b3b0ff21e89358c`  
**Revalidation Date:** 2026-10-26  
**Source Review:** `TIDYUP_v1.2.0_FULL_REVIEW.md`

---

## Finding Closure State Definitions
- `NOT_REVALIDATED`: Finding recorded from review; source code not yet independently inspected on current checkout.
- `CONFIRMED`: Source code trace verifies the defect exists exactly as described in current checkout.
- `CONTAINED`: Temporary safe fail-closed guard or UI mitigation is active, preventing data loss or deception.
- `FIXED_UNVERIFIED`: Code has been modified to address root cause, pending full acceptance test evidence.
- `FIXED_VERIFIED`: Regression tests and platform acceptance tests confirm the defect is completely resolved.
- `DISPROVED_WITH_EVIDENCE`: Source inspection proves the defect does not exist or was misidentified.
- `DEFERRED_PRODUCT_DECISION`: Acknowledged limitation deferred pending product architecture choice.

---

## Master 44-Finding Reconciliation Table

| ID | Priority | Description | Target Files | Current Status | Revalidation Evidence on `59d3bb4` | Implementing Task |
| :--- | :---: | :--- | :--- | :---: | :--- | :---: |
| **TU-01** | P1 | Automatic organisation not connected to startup | `ServiceConfiguration.cs`, `App.xaml.cs`, `FileMonitorService.cs` | **CONFIRMED** | `FileMonitorService` registered in DI, but `StartAsync` never called in `App.OnStartup`; zero subscribers to `FileDetected`. | `TASK-REM-24` |
| **TU-02** | P1 | Add Folder reports success without persistence | `FoldersViewModel.cs` | **CONFIRMED** | `AddMonitoredFolder` only creates an in-memory `MonitoredFolderItemViewModel`; `LoadFoldersAsync` removes it on refresh. | `TASK-REM-07` |
| **TU-03** | P1 | Folder-level Pause/Resume is display-only | `FoldersViewModel.cs` | **CONFIRMED** | `TogglePauseWatcher` toggles local `IsPaused` bool; does not contact watcher service or persist state. | `TASK-REM-07` |
| **TU-04** | P2 | Two Scan commands perform different operations | `FoldersViewModel.cs`, `DashboardViewModel.cs` | **CONFIRMED** | Folders `ScanFolderNowAsync` counts files; Dashboard `ScanFolderAsync` reconciles watcher and emits events. | `TASK-REM-27` |
| **TU-05** | P1 | Disabled rules cannot be dry-run | `RuleEngine.cs`, `RuleWizardViewModel.cs` | **CONFIRMED** | `RuleEngine.EvaluateRule` immediately returns `false` if `rule.IsEnabled == false`. | `TASK-REM-13` |
| **TU-06** | P1 | Wizard simulation results collapsed by wrong converter | `Stage4ReviewView.xaml`, `StringToVisibilityConverter.cs` | **CONFIRMED** | Stage 4 binds `ExecutionPlan` through `StringToVisibilityConverter`, which returns `Collapsed` for non-string objects. | `TASK-REM-12` |
| **TU-07** | P2 | Boolean inversion incorrectly used for Visibility | `InverseBooleanConverter.cs`, `RuleWizardView.xaml`, `Stage4ReviewView.xaml` | **CONFIRMED** | `InverseBooleanConverter` returns `bool`, bound to `Visibility` properties causing WPF binding type errors. | `TASK-REM-12` |
| **TU-08** | P1 | Guided and shell Save paths do not preserve same data | `RuleWizardViewModel.cs`, `MainWindowViewModel.cs`, `RuleRepository.cs` | **CONFIRMED** | Wizard Save instantiates new `Rule` omitting priority, schedule, and stats; Shell Save updates in place. | `TASK-REM-08` |
| **TU-09** | P1 | Discard does not restore data; dirty tracking misses nested edits | `MainWindowViewModel.cs`, `RuleWizardViewModel.cs` | **CONFIRMED** | `ConfirmDiscardRuleEdits` resets boolean `IsRuleDirty` without restoring entity snapshot; nested condition edits untracked. | `TASK-REM-08` |
| **TU-10** | P1 | Failed shell Save clears unsaved-change protection | `MainWindowViewModel.cs` | **CONFIRMED** | `SaveRuleAsync` clears dirty flag in `finally` block even when repository throws exception. | `TASK-REM-08` |
| **TU-11** | P2 | Wizard Cancel has no application handler | `RuleWizardViewModel.cs`, `RuleWizardView.xaml.cs` | **CONFIRMED** | `Cancel` raises `RequestClose` event with no subscribers in shell UserControl context. | `TASK-REM-08` |
| **TU-12** | P1 | Preview shows wrong draft or old rule's plan | `MainWindowViewModel.cs`, `PreviewView.xaml` | **CONFIRMED** | `TestRuleAsync` copies only advanced tab editors; `PreviewView` caches previous plan when navigating back. | `TASK-REM-18` |
| **TU-13** | P1 | Normal multi-action chains fail preflight | `ExecutionPlanGenerator.cs`, `PreviewViewModel.cs` | **CONFIRMED** | Preflight requires `File.Exists(action.SourcePath)` for all actions; virtual paths for dependent steps do not yet exist. | `TASK-REM-14` |
| **TU-14** | P1 | Planner models Copy, Extract and Run Command chains incorrectly | `ExecutionPlanGenerator.cs`, `ActionExecutor.cs` | **CONFIRMED** | Planner blindly treats target string as next virtual file, breaking on extract dirs, command strings, and copy sources. | `TASK-REM-14` |
| **TU-15** | P1 | Execution recalculates destinations instead of executing approved plan | `BatchProcessingCoordinator.cs`, `ExecutionPlanGenerator.cs` | **CONFIRMED** | Batch coordinator passes `i + 1` counter to executor, causing numbering drift when subset of actions approved. | `TASK-REM-19` |
| **TU-16** | P1 | Conflict resolution cannot be completed through Preview | `ExecutionPlanGenerator.cs`, `PreviewViewModel.cs` | **CONFIRMED** | Planner marks all existing targets as conflicts (blocking Apply); does not detect collisions between planned files. | `TASK-REM-15` |
| **TU-17** | P1 | Overwrite and Prompt policies can lose destination data | `ActionExecutor.cs` | **CONFIRMED** | Move with Overwrite deletes destination before move succeeds; Prompt returns destination without prompting. | `TASK-REM-04` |
| **TU-18** | P1 | Extraction bypasses destructive-action safeguards | `ActionExecutor.cs`, `ExecutionPlanGenerator.cs`, `SafeguardConfirmationViewModel.cs` | **CONFIRMED** | `ExtractArchive` uses `File.Delete` permanently on archive; overwrite of existing extracted files unflagged. | `TASK-REM-02` |
| **TU-19** | P1 | Confirmed permanent delete fails due to checkbox contract | `ActionExecutor.cs`, `WindowsShellFileOperations.cs` | **CONFIRMED** | `DeleteFileSafelyAsync` requires `ConfirmBeforeDelete == false` even after user approved modal safeguard. | `TASK-REM-03` |
| **TU-20** | P1 | Undo Delete claims restoration without restoring anything | `RollbackEngine.cs`, `HistoryViewModel.cs` | **CONFIRMED** | `RollbackDeleteAsync` returns `Success = true` with text instructions; History marks row "RolledBack". | `TASK-REM-01` |
| **TU-21** | P1 | Journaling occurs after mutation and omits failed operations & rule ID | `ActionExecutor.cs`, `OperationJournalEntry.cs` | **CONFIRMED** | Journal entry only written on success; `RuleId`, `RuleName`, and failed attempts omitted. | `TASK-REM-22` |
| **TU-22** | P1 | Dashboard and Logs disconnected from executed file operations | `DashboardViewModel.cs`, `LogsViewModel.cs`, `ActionExecutor.cs` | **CONFIRMED** | Dashboard/Logs query `ActionLogEntity`; preview executor writes only `OperationJournalEntry`. | `TASK-REM-31` |
| **TU-23** | P2 | Execution summary fabricates actual results from preview rows | `PreviewViewModel.cs`, `ExecutionDiscrepancyAnalyzer.cs` | **CONFIRMED** | Viewmodel creates synthetic results using planned target paths; correlates by `SourcePath` rather than action ID. | `TASK-REM-23` |
| **TU-24** | P2 | Apply remains enabled after all approved actions execute | `PreviewViewModel.cs` | **CONFIRMED** | `CanApplyChanges` checks `IsApproved` but not `IsExecuted`; caption reads "Apply approved changes (none selected)". | `TASK-REM-21` |
| **TU-25** | P1 | UI responsiveness and scan completeness not guaranteed | `ExecutionPlanGenerator.cs`, `PreviewViewModel.cs` | **CONFIRMED** | `GeneratePlanForFolderAsync` executes synchronously; single inaccessible child returns empty plan for whole tree. | `TASK-REM-17` |
| **TU-26** | P1 | Settings expose controls that have no runtime effect | `AppSettings.cs`, `ActionEditorViewModel.cs`, `FileMonitorService.cs` | **CONFIRMED** | `MaxConcurrentOperations`, `ScanBatchSize`, `NetworkFolderPollingIntervalSeconds` have 0 runtime consumers. | `TASK-REM-35` |
| **TU-27** | P2 | Optimize Database is a simulated success | `SettingsViewModel.cs` | **CONFIRMED** | `VacuumDatabaseAsync` executes `await Task.Delay(300)` and reports success without running SQL. | `TASK-REM-06` |
| **TU-28** | P1 | Tray and notification settings not supplied to services | `ServiceConfiguration.cs`, `ToastNotificationService.cs`, `App.xaml.cs` | **CONFIRMED** | `AppSettings` not registered in DI; services receive `null` and fall back to hard-coded defaults. | `TASK-REM-28` |
| **TU-29** | P1 | Import uses older unsafe path rather than schema-aware service | `MainWindowViewModel.cs`, `ImportExportService.cs`, `RuleSerializationService.cs` | **CONFIRMED** | Shell calls deprecated `ImportExportService`, which preserves imported `IsEnabled = true` and lacks schema gate. | `TASK-REM-10` |
| **TU-30** | P2 | Rule drag-and-drop priority is not persisted | `MainWindow.xaml.cs` | **CONFIRMED** | `RuleListBox_Drop` modifies in-memory `ExecutionOrder` but dispatches no repository update to SQLite. | `TASK-REM-09` |
| **TU-31** | P2 | Preserve subfolder structure exposed but ignored | `ActionEditorControl.xaml`, `ActionExecutor.cs`, `ExecutionPlanGenerator.cs` | **CONFIRMED** | `PreserveSubfolderStructure` present on model but never used by planner or executor to compute relative paths. | `TASK-REM-16` |
| **TU-32** | P1 | Empty-folder cleanup can climb above monitored root | `ActionExecutor.cs` | **CONFIRMED** | `RemoveEmptyFoldersAsync` called with no `stopBoundaryPath`; deletes empty parents up to system root. | `TASK-REM-05` |
| **TU-33** | P2 | Network monitoring, scheduling, and auxiliary features not integrated | `FileMonitorService.cs`, `ScheduleManager.cs`, `RuleRevisionManager.cs` | **CONFIRMED** | Network paths skipped with TODO; `ScheduleManager` never instantiated; rule revisions not created on save. | `TASK-REM-36` |
| **TU-34** | P1 | Monitoring restart links itself to already cancelled token | `FileMonitorService.cs` | **CONFIRMED** | `UpdateRulesAsync` calls `StopAsync` (cancelling CTS) then passes same token into `StartAsync`. | `TASK-REM-25` |
| **TU-35** | P2 | Shared-folder watcher configuration depends on first rule | `FileMonitorService.cs` | **CONFIRMED** | `DistinctBy(Path)` keeps first rule's `IncludeSubfolders` and exclusions, discarding requirements of subsequent rules. | `TASK-REM-26` |
| **TU-36** | P2 | History pagination commands not notified when eligibility changes | `HistoryViewModel.cs` | **CONFIRMED** | `TotalRecords` updates but `NextPageCommand.NotifyCanExecuteChanged()` is never called. | `TASK-REM-32` |
| **TU-37** | P2 | History export ignores active filters | `HistoryViewModel.cs` | **CONFIRMED** | `ExportCsvAsync` and `ExportJsonAsync` query all records in `OperationJournal` ignoring active search filter. | `TASK-REM-33` |
| **TU-38** | P2 | Undo offered for operations that cannot be automatically reversed | `HistoryViewModel.cs`, `RollbackEngine.cs` | **CONFIRMED** | `CanRollback` enabled for `ExtractArchive` and `RunCommand`, which `RollbackEngine` explicitly refuses. | `TASK-REM-01` |
| **TU-39** | P2 | Help and onboarding sample use unsupported variables | `FirstRunViewModel.cs`, `VariableEngine.cs`, `HelpView.xaml` | **CONFIRMED** | Sample uses `{FileExtension}`; Help advertises `{CreationYear}`, `{DateTime}` not recognized by `VariableEngine`. | `TASK-REM-11` |
| **TU-40** | P2 | Time variables can generate invalid Windows filenames | `VariableEngine.cs`, `RuleValidator.cs` | **CONFIRMED** | `now_time` resolves to `HH:mm:ss`; colons are illegal in Windows filenames. | `TASK-REM-11` |
| **TU-41** | P2 | Window restore and close behaviour conflict | `App.xaml.cs`, `MainWindow.xaml.cs` | **CONFIRMED** | `App.OnStartup` forces `WindowState.Normal`; `MainWindow_Closing` in App ignores `e.Cancel` from dirty prompt. | `TASK-REM-29` |
| **TU-42** | P2 | Settings persistence non-atomic; corruption silently replaced | `SettingsRepository.cs`, `MainWindow.xaml.cs` | **CONFIRMED** | `SaveAsync` writes directly without atomic temp replacement; `LoadAsync` catches exception and returns defaults. | `TASK-REM-34` |
| **TU-43** | P2 | Tray health reflects last folder event, not aggregate health | `TrayIconService.cs` | **CONFIRMED** | Healthy event from folder B clears warning even if folder A is disconnected; `CreateStatusIcon` leaks native icon handles. | `TASK-REM-30` |
| **TU-44** | P2 | Batch lifetime and application state not held consistently | `BatchProcessingCoordinator.cs`, `ApplicationStateManager.cs` | **CONFIRMED** | State transitions back to `Idle` between individual serial actions in a batch loop. | `TASK-REM-20` |
