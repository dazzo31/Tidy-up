# TidyUp Feature Verification Matrix

This document is the authoritative verification matrix for TidyUp. It reflects the live, verified implementation status across all functional and architectural capabilities and links each requirement to automated test suites proving compliance.

**Test Suite Status:** 337 Passing | 0 Failing | 0 Skipped (100% Pass Rate)

---

## 1. Safety & Reversibility Subsystem

| Feature / Capability | Status | Implementation Target | Automated Test Suite |
|---|:---:|---|---|
| **Recycle Bin Enforcement**<br>Directs file deletion through Windows Shell Recycle Bin (`SHFileOperation` / `IFileOperation`); blocks permanent deletion on non-supported media without explicit override | `[Verified]` | `ISafeFileSystem`<br>`WindowsShellFileOperations` | `TidyUp.Tests/Unit/Services/SafeDeleteTests.cs` |
| **Atomic Multi-Action Rollback**<br>One-click rollback for batches, computing inverse actions and verifying SHA-256 pre-reversal checksums | `[Verified]` | `IRollbackEngine`<br>`RollbackEngine` | `TidyUp.Tests/Unit/Services/RollbackEngineTests.cs` |
| **Two-Phase Journaling**<br>Tracks batches and individual file operations in SQLite with operation status, checksums, and timestamps | `[Verified]` | `OperationJournalEntry`<br>`TidyUpDbContext` | `TidyUp.Tests/Integration/EndToEndFileActionTests.cs` |
| **Locked File Detection & Exponential Backoff**<br>Detects actively written or locked files (downloads, copies); backs off exponentially | `[Verified]` | `IFileLockDetector`<br>`FileLockDetector` | `TidyUp.Tests/Unit/Services/FileLockTests.cs` |
| **Execution Plan Dry-Run Simulation**<br>Virtual path simulation detecting collisions, overwrite risks, and invalid destinations prior to execution | `[Verified]` | `IExecutionPlanGenerator`<br>`ExecutionPlanGenerator` | `TidyUp.Tests/Unit/Services/ExecutionPlanGeneratorTests.cs` |
| **Rule Cycle & Recursion Validator**<br>Detects direct and indirect circular rule dependencies and self-referential folder targets | `[Verified]` | `IRuleValidator`<br>`RuleValidator` | `TidyUp.Tests/Unit/Services/RuleCycleValidatorTests.cs` |
| **Safeguard Confirmation Dialog**<br>Presents high-risk batch prompts with operation breakdown, destructive counts, and required user confirmation | `[Verified]` | `SafeguardConfirmationViewModel`<br>`SafeguardConfirmationDialog` | `TidyUp.Tests/Unit/ViewModels/SafeguardConfirmationViewModelTests.cs` |
| **Execution Discrepancy Analyzer**<br>Compares planned execution actions against post-run realities to flag missing files, altered sizes, or lock timeouts | `[Verified]` | `IExecutionDiscrepancyAnalyzer`<br>`ExecutionDiscrepancyAnalyzer` | `TidyUp.Tests/Unit/Services/ExecutionDiscrepancyAnalyzerTests.cs` |

---

## 2. File Monitoring & Health Subsystem

| Feature / Capability | Status | Implementation Target | Automated Test Suite |
|---|:---:|---|---|
| **Hardened File Watcher**<br>Internal 64KB buffer, bounded debouncing (500ms), and error recovery | `[Verified]` | `HardenedFileSystemWatcher` | `TidyUp.Tests/Unit/Services/HardenedWatcherTests.cs` |
| **Buffer Overflow Self-Healing**<br>Catches `InternalBufferOverflowException` and initiates directory reconciliation scan | `[Verified]` | `HardenedFileSystemWatcher`<br>`FileMonitorService` | `TidyUp.Tests/Unit/Services/HardenedWatcherTests.cs` |
| **Watcher Health Monitor**<br>Tracks real-time health (`Healthy`, `Degraded`, `Paused`, `Faulted`), handles reconnects on network folders | `[Verified]` | `IWatcherHealthMonitor`<br>`WatcherHealthMonitor` | `TidyUp.Tests/Unit/Services/WatcherHealthMonitorTests.cs` |
| **Batch Processing Coordinator**<br>Orchestrates processing pipeline with cancellation tokens, pause/resume capability, and progress reporting | `[Verified]` | `IBatchProcessingCoordinator`<br>`BatchProcessingCoordinator` | `TidyUp.Tests/Unit/Services/BatchCancellationAndPauseTests.cs` |
| **Application State Funnel**<br>Centralizes application lifecycle state transitions (`Starting`, `Running`, `Paused`, `ShuttingDown`) | `[Verified]` | `IApplicationStateManager`<br>`ApplicationStateManager` | `TidyUp.Tests/Unit/Services/ApplicationStateManagerTests.cs` |

---

## 3. Rule Engine & Condition Evaluation

| Feature / Capability | Status | Implementation Target | Automated Test Suite |
|---|:---:|---|---|
| **Composite Condition Trees**<br>Recursive nested condition groups supporting `And` and `Or` logic | `[Verified]` | `ConditionGroup`<br>`RuleEngine` | `TidyUp.Tests/Unit/Services/RuleEngineTests.cs` |
| **Name & Extension Matching**<br>Case-insensitive/sensitive string checks, prefixes, suffixes, exact matches, regex patterns | `[Verified]` | `FileNameCondition`<br>`FileExtensionCondition` | `TidyUp.Tests/Unit/Services/RuleEngineTests.cs` |
| **Size & Date Conditions**<br>Byte size comparisons (equal, greater, less, between) and timestamp operators (created/modified, older than $N$ days) | `[Verified]` | `FileSizeCondition`<br>`FileDateCondition` | `TidyUp.Tests/Unit/Services/RuleEngineTests.cs` |
| **Streaming Document Content Matching**<br>Scans plain text, CSV, Markdown, and JSON files for keywords and regex safely streaming up to 1MB | `[Verified]` | `IContentConditionEvaluator`<br>`FileContentCondition` | `TidyUp.Tests/Unit/Conditions/ContentAndMetadataConditionTests.cs` |
| **EXIF & Media Metadata Evaluation**<br>Extracts photo dimensions, EXIF Date Taken, camera models, WAV durations, and MP3 ID3 tags | `[Verified]` | `IMetadataExtractor`<br>`ImageMetadataCondition`<br>`MediaMetadataCondition` | `TidyUp.Tests/Unit/Conditions/ContentAndMetadataConditionTests.cs` |
| **Rule Diagnostics & Explainability**<br>Evaluates files and provides detailed condition-by-condition pass/fail reasoning | `[Verified]` | `RuleEngine.ExplainEvaluation`<br>`RuleEvaluationInspectorViewModel` | `TidyUp.Tests/Unit/Services/ExplainabilityTests.cs` |
| **Natural Language Summary Generator**<br>Translates rule condition and action trees into readable plain English summaries | `[Verified]` | `IRuleSummaryGenerator`<br>`RuleSummaryGenerator` | `TidyUp.Tests/Unit/Services/RuleSummaryGeneratorTests.cs` |

---

## 4. File Actions & Variable Engine

| Feature / Capability | Status | Implementation Target | Automated Test Suite |
|---|:---:|---|---|
| **File Move, Copy & Rename**<br>Executes moves, copies, and renames with conflict resolution strategies (`Overwrite`, `Skip`, `RenameNew`) | `[Verified]` | `IActionExecutor`<br>`ActionExecutor` | `TidyUp.Tests/Unit/Services/ActionExecutorTests.cs`<br>`TidyUp.Tests/Integration/EndToEndFileActionTests.cs` |
| **Archive Extraction**<br>Extracts ZIP, TAR, GZ, and 7Z archives with directory traversal protection | `[Verified]` | `ExtractArchiveAction`<br>`ActionExecutor` | `TidyUp.Tests/Unit/Services/ActionExecutorTests.cs` |
| **Variable Engine & Path Formatting**<br>Resolves dynamic naming tokens (`{Date:yyyy-MM}`, `{Ext}`, `{OriginalName}`, `{SizeKB}`, `{Counter}`) | `[Verified]` | `IVariableEngine`<br>`VariableEngine` | `TidyUp.Tests/Unit/Services/VariableEngineTests.cs` |

---

## 5. Advanced Engines & Extensibility

| Feature / Capability | Status | Implementation Target | Automated Test Suite |
|---|:---:|---|---|
| **Multi-Rule Conflict Analyzer**<br>Detects competing destination paths and shadowed rules that never receive files | `[Verified]` | `IMultiRuleConflictAnalyzer`<br>`MultiRuleConflictAnalyzer` | `TidyUp.Tests/Unit/Rules/MultiRuleConflictAnalyzerTests.cs` |
| **Versioned Rule History & Diff**<br>Immutable JSON rule revisions, side-by-side visual diffs, and 1-click historical restoration | `[Verified]` | `IRuleRevisionManager`<br>`RuleRevisionManager` | `TidyUp.Tests/Unit/Rules/RuleRevisionManagerTests.cs` |
| **Pre-Packaged Rule Templates**<br>Curated starter templates for downloads, screenshot archiving, and photo organization | `[Verified]` | `DefaultRuleTemplates`<br>`RuleSerializationService` | `TidyUp.Tests/RuleTests/SerializationTests.cs` |
| **Versioned JSON Import/Export**<br>Schema validation (`https://tidyup.app/schemas/rules-v1.json`), Windows reserved device sanitization, forced disabled import | `[Verified]` | `IRuleSerializationService`<br>`RuleSerializationService` | `TidyUp.Tests/RuleTests/SerializationTests.cs` |
| **Hash-Based Duplicate File Detector**<br>3-step pipeline (size group $\rightarrow$ 4KB partial hash $\rightarrow$ full SHA-256) with isolation into `_Duplicates` or Recycle Bin | `[Verified]` | `IDuplicateDetector`<br>`FileHashCalculator` | `TidyUp.Tests/DuplicateTests/DuplicateDetectorTests.cs` |
| **Scheduled Monitoring & Quiet Hours**<br>Configurable time-of-day execution, overnight quiet hours spanning midnight, and Win32 low-battery pausing | `[Verified]` | `IScheduleManager`<br>`ScheduleManager`<br>`WindowsPowerStatusProvider` | `TidyUp.Tests/Unit/Scheduling/ScheduleManagerTests.cs` |
| **SQLite Disaster Recovery & Integrity**<br>Automatic pre-migration backups, daily snapshots, startup `PRAGMA integrity_check`, and instant corruption rollback | `[Verified]` | `IDatabaseBackupManager`<br>`IDbIntegrityChecker` | `TidyUp.Tests/Unit/Data/DatabaseRecoveryTests.cs` |

---

## 6. User Interface & Presentation (WPF MVVM)

| Feature / Capability | Status | Implementation Target | Automated Test Suite |
|---|:---:|---|---|
| **Main Dashboard & Rule Management**<br>Displays active rules, folder statuses, execution controls, and real-time activity metrics | `[Verified]` | `MainWindow`<br>`DashboardViewModel` | `TidyUp.Tests/Unit/ViewModels/DashboardViewModelTests.cs` |
| **Multi-Stage Rule Wizard**<br>3-stage wizard (Details & Folders $\rightarrow$ Conditions $\rightarrow$ Actions) with validation gates | `[Verified]` | `RuleWizardView`<br>`RuleWizardViewModel` | `TidyUp.Tests/Unit/ViewModels/RuleWizardViewModelTests.cs` |
| **History & Journal Viewer**<br>Audited log view with action filtering, batch group rollup, and 1-click rollback buttons | `[Verified]` | `HistoryView`<br>`HistoryViewModel` | `TidyUp.Tests/Unit/ViewModels/HistoryViewModelTests.cs` |
| **Dry-Run Preview Window**<br>Displays simulated execution plans with conflict indicators and approval controls | `[Verified]` | `PreviewWindow`<br>`PreviewViewModel` | `TidyUp.Tests/Unit/ViewModels/PreviewViewModelTests.cs` |
| **Dirty State Tracking & Navigation Guards**<br>Prevents accidental loss of unsaved rule modifications with user confirmation prompts | `[Verified]` | `MainWindowViewModel`<br>`RuleWizardViewModel` | `TidyUp.Tests/Unit/ViewModels/MainWindowDirtyStateTests.cs` |
| **System Tray & Toast Notifications**<br>System tray icon with context menu, health status indicators, and Windows toast alerts | `[Verified]` | `ITrayIconService`<br>`IToastNotificationService` | `TidyUp.Tests/Unit/Services/TrayIconAndToastNotificationTests.cs` |
| **Scheduled Settings View**<br>Dedicated interface for configuring Quiet Hours, weekend rules, and battery thresholds | `[Verified]` | `ScheduleSettingsView`<br>`ScheduleSettingsViewModel` | `TidyUp.Tests/Unit/Scheduling/ScheduleManagerTests.cs` |
| **Database Backup Action in Settings**<br>Manual backup trigger in settings window connecting to `IDatabaseBackupManager` | `[Verified]` | `SettingsWindow`<br>`SettingsViewModel` | `TidyUp.Tests/Unit/Data/DatabaseRecoveryTests.cs` |

