# TidyUp: Master Implementation Task Catalog & LLM Execution Contract

> **Governance Notice for AI Agents & Developers:**  
> This document translates the strategic architectural roadmap from `TidyUp Plan.pdf` into bounded, modular, self-contained implementation tasks.  
> Every task adheres strictly to the **Task Lock Governance Protocol**. Any LLM picking up a task from this catalog MUST execute only what is defined in `IN SCOPE`, must NEVER perform out-of-scope refactoring, and must verify all `DONE WHEN` criteria before reporting completion.

---

## Architecture & Governance Rules for All Tasks

1. **One Task, One Commit:** Never combine multiple tasks into a single pull request or implementation session.
2. **Architectural Hierarchy (Never Bypass):**
   ```text
   UI / Views (WPF XAML)
         ↓
   ViewModels (MVVM)
         ↓
   Core Services / Orchestrators
         ↓
   Engine (File Operations / Rule Evaluator)
         ↓
   Infrastructure (SQLite / EF Core, Windows Shell APIs, FileSystemWatcher)
   ```
3. **Safety First Invariant:** No file operation may execute without pre-flight validation. Previews must be 100% read-only with zero side-effects. Deletions must use the Windows Recycle Bin by default.
4. **Testing Invariant:** All file operations must be tested using real temporary filesystem structures (`System.IO.Path.GetTempPath()`), never mock abstractions that conceal Windows file locking, permissions, or cross-volume behaviors.

---

## Table of Contents

- [Phase 0: Baseline Audit & Truth Reconciliation](#phase-0-baseline-audit--truth-reconciliation)
  - [TASK-BL-01: Release Build & Test Suite Verification](#task-bl-01-release-build--test-suite-verification)
  - [TASK-BL-02: UI Control-to-Service Trace & Placeholder Inventory](#task-bl-02-ui-control-to-service-trace--placeholder-inventory)
  - [TASK-BL-03: Filesystem Engine & Watcher Audit](#task-bl-03-filesystem-engine--watcher-audit)
  - [TASK-BL-04: Baseline Defect Catalog & Historical Doc Reconciliation](#task-bl-04-baseline-defect-catalog--historical-doc-reconciliation)
- [Phase 1: Safety Foundation & Core Engine Hardening](#phase-1-safety-foundation--core-engine-hardening)
  - [TASK-SAFE-01: Read-Only Dry-Run Simulation Engine](#task-safe-01-read-only-dry-run-simulation-engine)
  - [TASK-SAFE-02: Safe Deletion via Windows Shell Recycle Bin](#task-safe-02-safe-deletion-via-windows-shell-recycle-bin)
  - [TASK-SAFE-03: Pre-Flight File Lock Detection & Non-Blocking Retry](#task-safe-03-pre-flight-file-lock-detection--non-blocking-retry)
  - [TASK-SAFE-04: Application-Level Journal & Conflict-Safe Rollback](#task-safe-04-application-level-journal--conflict-safe-rollback)
  - [TASK-SAFE-05: Rule Cycle & Self-Triggering Directory Loop Detector](#task-safe-05-rule-cycle--self-triggering-directory-loop-detector)
  - [TASK-SAFE-06: FileSystemWatcher Overflow Reconciliation & Burst Debounce](#task-safe-06-filesystemwatcher-overflow-reconciliation--burst-debounce)
  - [TASK-SAFE-07: Real-Filesystem Integration Test Harness](#task-safe-07-real-filesystem-integration-test-harness)
- [Phase 2: End-to-End Workflow & GUI Implementation](#phase-2-end-to-end-workflow--gui-implementation)
  - [TASK-GUI-01: Operational Dashboard View](#task-gui-01-operational-dashboard-view)
  - [TASK-GUI-02: Guided 4-Stage Rule Creation Workflow](#task-gui-02-guided-4-stage-rule-creation-workflow)
  - [TASK-GUI-03: Plain-Language Rule Natural Language Summarizer](#task-gui-03-plain-language-rule-natural-language-summarizer)
  - [TASK-GUI-04: Side-by-Side Impact Preview Window](#task-gui-04-side-by-side-impact-preview-window)
  - [TASK-GUI-05: Destructive-Action Safeguards & Batch Confirmations](#task-gui-05-destructive-action-safeguards--batch-confirmations)
  - [TASK-GUI-06: Expandable History & Log Viewer](#task-gui-06-expandable-history--log-viewer)
  - [TASK-GUI-07: Explicit State Machine Enforcement](#task-gui-07-explicit-state-machine-enforcement)
  - [TASK-GUI-08: Accessibility, Empty States & Dirty-State Tracking](#task-gui-08-accessibility-empty-states--dirty-state-tracking)
- [Phase 3: Monitoring, Health & Explainability](#phase-3-monitoring-health--explainability)
  - [TASK-MON-01: File Watcher Health & Degraded State Monitor](#task-mon-01-file-watcher-health--degraded-state-monitor)
  - [TASK-MON-02: Explainable Decision Engine (Condition Inspector)](#task-mon-02-explainable-decision-engine-condition-inspector)
  - [TASK-MON-03: Preview vs. Actual Execution Discrepancy Analyzer](#task-mon-03-preview-vs-actual-execution-discrepancy-analyzer)
  - [TASK-MON-04: In-Flight Operation Cancellation & Safe Pause Protocol](#task-mon-04-in-flight-operation-cancellation--safe-pause-protocol)
  - [TASK-MON-05: System Tray & Background Notification Service](#task-mon-05-system-tray--background-notification-service)
- [Phase 4: Advanced Capabilities & Feature Expansion](#phase-4-advanced-capabilities--feature-expansion)
  - [TASK-ADV-01: Multi-Rule Conflict & Dependency Analyzer](#task-adv-01-multi-rule-conflict--dependency-analyzer)
  - [TASK-ADV-02: Versioned Rule History & Configuration Diff](#task-adv-02-versioned-rule-history--configuration-diff)
  - [TASK-ADV-03: Pre-Packaged Rule Templates & Versioned Export/Import](#task-adv-03-pre-packaged-rule-templates--versioned-exportimport)
  - [TASK-ADV-04: Hash-Based Duplicate File Detection Engine](#task-adv-04-hash-based-duplicate-file-detection-engine)
  - [TASK-ADV-05: Scheduled Monitoring & Quiet Hours Engine](#task-adv-05-scheduled-monitoring--quiet-hours-engine)
  - [TASK-ADV-06: Document Content & Metadata Conditions](#task-adv-06-document-content--metadata-conditions)
  - [TASK-ADV-07: SQLite Database Disaster Recovery & Migration Engine](#task-adv-07-sqlite-database-disaster-recovery--migration-engine)
- [Phase 5: Repository Governance, CI & Documentation](#phase-5-repository-governance-ci--documentation)
  - [TASK-DOC-01: Authoritative README & Architecture Documentation](#task-doc-01-authoritative-readme--architecture-documentation)
  - [TASK-DOC-02: Authoritative Feature Verification Matrix](#task-doc-02-authoritative-feature-verification-matrix)
  - [TASK-CI-01: GitHub Actions CI Matrix Build & Automated Test Gate](#task-ci-01-github-actions-ci-matrix-build--automated-test-gate)
- [Phase 6: UI/UX Modernization & Workflow Experience](#phase-6-uiux-modernization--workflow-experience)
  - [TASK-UI-01: View Redundancy Elimination & Registration Cleanup](#task-ui-01-view-redundancy-elimination--registration-cleanup)
  - [TASK-UI-02: Persistent Modern Navigation Rail](#task-ui-02-persistent-modern-navigation-rail)
  - [TASK-UI-03: Real-Time Monitoring Status Bar & Health Indicator](#task-ui-03-real-time-monitoring-status-bar--health-indicator)
  - [TASK-UI-04: Enhanced Rules List with Search, Filter & Unsaved Dirty Indicators](#task-ui-04-enhanced-rules-list-with-search-filter--unsaved-dirty-indicators)
  - [TASK-UI-05: Integrated Guided Wizard & Advanced Rule Editor Toggle](#task-ui-05-integrated-guided-wizard--advanced-rule-editor-toggle)
  - [TASK-UI-06: Condition Builder Nested Expression Visualization](#task-ui-06-condition-builder-nested-expression-visualization)
  - [TASK-UI-07: Dedicated Folders Management View](#task-ui-07-dedicated-folders-management-view)
  - [TASK-UI-08: Folder Picker & Exclusion Pattern Configuration](#task-ui-08-folder-picker--exclusion-pattern-configuration)
  - [TASK-UI-09: Integrated Preview & Review Workspace](#task-ui-09-integrated-preview--review-workspace)
  - [TASK-UI-10: File-Level Explainability Inspector & Two-Step Apply Workflow](#task-ui-10-file-level-explainability-inspector--two-step-apply-workflow)
  - [TASK-UI-11: History View Actionable Explanations & Safe Rollback Controls](#task-ui-11-history-view-actionable-explanations--safe-rollback-controls)
  - [TASK-UI-12: Categorized 5-Section Settings Experience](#task-ui-12-categorized-5-section-settings-experience)
  - [TASK-UI-13: Windows 11 Styling & Centralized Design Tokens](#task-ui-13-windows-11-styling--centralized-design-tokens)
  - [TASK-UI-14: Keyboard Navigation, High-DPI Scaling & Screen Reader Accessibility](#task-ui-14-keyboard-navigation-high-dpi-scaling--screen-reader-accessibility)
  - [TASK-UI-15: Full Integration Regression & Verification Release Gate](#task-ui-15-full-integration-regression--verification-release-gate)

---

# Phase 0: Baseline Audit & Truth Reconciliation

### TASK-BL-01: Release Build & Test Suite Verification
```text
TASK ID: TASK-BL-01
OBJECTIVE:
Build the entire solution in Release configuration and run all existing automated tests to verify the true technical baseline.

DEPENDENCIES: None.
TARGET FILES: Solution file (*.sln), all *.csproj files, test projects.

IN SCOPE:
- Restoring NuGet packages.
- Compiling in Release configuration (`dotnet build -c Release`).
- Executing all existing test projects (`dotnet test -c Release --verbosity normal`).
- Logging compilation warnings, obsolete API usages, and test outcomes.

OUT OF SCOPE:
- Fixing failing tests or compiler warnings (record them only).
- Refactoring project structures or updating target .NET versions.

DONE WHEN:
- Build logs captured.
- Exact count of passing, failing, and skipped tests documented.
- Verified test baseline report added to docs/baseline_test_report.md.
```

---

### TASK-BL-02: UI Control-to-Service Trace & Placeholder Inventory
```text
TASK ID: TASK-BL-02
OBJECTIVE:
Trace every UI control, command, and window in the WPF application down to its underlying ViewModel and Core service to identify incomplete handlers and placeholder views.

DEPENDENCIES: TASK-BL-01
TARGET FILES: All *.xaml, *.xaml.cs, and ViewModels.

IN SCOPE:
- Inspecting MainWindow and all secondary dialogs/views.
- Checking every button, menu item, toolbar command, and binding.
- Identifying empty RelayCommands, `throw new NotImplementedException()`, and placeholder controls (such as the folder editor and test window noted in the plan).
- Documenting disconnected UI elements.

OUT OF SCOPE:
- Implementing the missing handlers or views.
- Redesigning the UI layout.

DONE WHEN:
- Complete matrix mapping UI Control -> Command -> ViewModel -> Backend Service generated.
- List of placeholder/disconnected views documented in docs/ui_inventory.md.
```

---

### TASK-BL-03: Filesystem Engine & Watcher Audit
```text
TASK ID: TASK-BL-03
OBJECTIVE:
Audit the filesystem operations, FileSystemWatcher implementation, and SQLite data access layer for known edge-case vulnerabilities.

DEPENDENCIES: TASK-BL-01
TARGET FILES: Core file services, Watcher services, EF Core DbContext, Data repositories.

IN SCOPE:
- Verifying whether FileSystemWatcher handles Error/buffer overflow events.
- Checking whether file deletion permanently deletes or routes to Recycle Bin.
- Checking handling of file locking, cross-volume moves, and long paths (MAX_PATH).
- Verifying thread safety of DbContext across background worker threads.

OUT OF SCOPE:
- Fixing discovered defects.
- Rewriting watcher or database code.

DONE WHEN:
- Audit report produced detailing specific lines of code where edge-case vulnerabilities exist.
- Audit document saved in docs/filesystem_audit.md.
```

---

### TASK-BL-04: Baseline Defect Catalog & Historical Doc Reconciliation
```text
TASK ID: TASK-BL-04
OBJECTIVE:
Reconcile all previous completion reports and historical status documents with the findings from BL-01, BL-02, and BL-03 to produce a single authoritative defect backlog.

DEPENDENCIES: TASK-BL-01, TASK-BL-02, TASK-BL-03
TARGET FILES: Existing markdown reports in repository, docs/*.

IN SCOPE:
- Comparing claims in historical completion documents against code reality.
- Compiling a consolidated, prioritized defect list categorized by P0, P1, P2.
- Annotating stale claims in historical docs as superseded.

OUT OF SCOPE:
- Modifying production code.

DONE WHEN:
- Consolidated baseline defect backlog created in docs/baseline_defects.md.
- Stale claims catalogued with clear evidence of current state.
```

---

# Phase 1: Safety Foundation & Core Engine Hardening

### TASK-SAFE-01: Read-Only Dry-Run Simulation Engine
```text
TASK ID: TASK-SAFE-01
OBJECTIVE:
Implement a simulation engine that evaluates rules against target directories and produces an in-memory Execution Plan without modifying files or database state.

DEPENDENCIES: TASK-BL-03
TARGET FILES:
- `TidyUp.Core/Simulation/IExecutionPlanGenerator.cs` (New or Updated)
- `TidyUp.Core/Simulation/ExecutionPlan.cs` (New)
- `TidyUp.Core/Simulation/PlannedFileAction.cs` (New)
- `TidyUp.Tests/SimulationTests/ExecutionPlanGeneratorTests.cs` (New)

IN SCOPE:
- Evaluating rule conditions against real files in target directories.
- Generating a `PlannedFileAction` for each matching file containing:
  - SourcePath, TargetPath, ActionType (Move, Copy, Rename, Delete).
  - Condition evaluation results that triggered the match.
  - Potential conflict state (e.g. TargetAlreadyExists, AccessDenied).
- Calculating summary statistics: TotalMatched, MoveCount, CopyCount, RenameCount, DeleteCount, SkipCount, ConflictCount.
- Enforcing read-only operation: no file moves, writes, deletes, or processed-file flags updated.

OUT OF SCOPE:
- Executing the plan.
- WPF UI preview controls (covered in TASK-GUI-04).

DONE WHEN:
- Unit tests verify that running simulation leaves filesystem timestamps, hashes, and directory contents 100% unchanged.
- Simulation correctly identifies conflicts (such as destination collision) before execution.
```

---

### TASK-SAFE-02: Safe Deletion via Windows Shell Recycle Bin
```text
TASK ID: TASK-SAFE-02
OBJECTIVE:
Replace permanent deletion (`File.Delete`) with Windows Shell Recycle Bin deletion by default, with explicit detection and warnings when Recycle Bin is unavailable.

DEPENDENCIES: TASK-BL-03
TARGET FILES:
- `TidyUp.Core/FileSystem/ISafeFileSystem.cs`
- `TidyUp.Core/FileSystem/WindowsShellFileOperations.cs`
- `TidyUp.Tests/FileSystemTests/SafeDeleteTests.cs`

IN SCOPE:
- Implementing safe deletion using `Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(path, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin)` or Shell COM API (`SHFileOperation` / `IFileOperation`).
- Detecting drive types where Recycle Bin is unsupported (network shares / UNC paths, certain USB/FAT32 volumes).
- Returning explicit failure or requiring explicit confirmation when Recycle Bin is unavailable, rather than silently deleting permanently.

OUT OF SCOPE:
- Implementing UI prompt dialogs (handled via service abstraction in Phase 2).

DONE WHEN:
- Automated tests prove deleted test files are sent to the Windows Recycle Bin on local NTFS drives.
- Attempting to delete a file on a drive without Recycle Bin support triggers `RecycleBinUnavailableException` or flag.
```

---

### TASK-SAFE-03: Pre-Flight File Lock Detection & Non-Blocking Retry
```text
TASK ID: TASK-SAFE-03
OBJECTIVE:
Implement pre-flight file availability verification and a non-blocking retry queue for files locked by external processes (e.g., in-progress downloads or open office files).

DEPENDENCIES: TASK-BL-03
TARGET FILES:
- `TidyUp.Core/FileSystem/IFileLockDetector.cs`
- `TidyUp.Core/Processing/RetryQueueManager.cs`
- `TidyUp.Tests/ProcessingTests/FileLockTests.cs`

IN SCOPE:
- Implementing `IsFileReady(string path)` that tests exclusive read/write access (`FileStream` with `FileMode.Open, FileAccess.ReadWrite, FileShare.None`).
- Detecting temporary download files (`.crdownload`, `.part`, `~$*`) and skipping until completed.
- Implementing a non-blocking retry queue with configurable exponential backoff (e.g. 1s, 2s, 5s, max 3 retries).
- Emitting log events when a file is locked rather than throwing unhandled exceptions.

OUT OF SCOPE:
- UI retry buttons (covered in Phase 2/3).

DONE WHEN:
- Tests verify locked file is not moved or corrupted while held open by another process stream.
- File is successfully processed once external lock is released.
```

---

### TASK-SAFE-04: Application-Level Journal & Conflict-Safe Rollback
```text
TASK ID: TASK-SAFE-04
OBJECTIVE:
Build a durable SQLite execution journal and rollback engine capable of reversing completed operations without clobbering newer user modifications.

DEPENDENCIES: TASK-BL-03, TASK-SAFE-02
TARGET FILES:
- `TidyUp.Data/Entities/OperationJournalEntry.cs`
- `TidyUp.Core/Rollback/IRollbackEngine.cs`
- `TidyUp.Core/Rollback/RollbackEngine.cs`
- `TidyUp.Tests/RollbackTests/RollbackEngineTests.cs`

IN SCOPE:
- Recording every file operation atomically in SQLite: `OperationId`, `BatchId`, `Timestamp`, `ActionType`, `OriginalPath`, `TargetPath`, `SHA256Hash`, `Status`.
- Implementing `RollbackBatch(Guid batchId)` and `RollbackOperation(Guid operationId)`.
- Validating target file state before reversing: check target still exists, verify hash matches post-operation hash (has not been edited by user since).
- Restoring from Recycle Bin or reversing Move/Rename.
- Safe abort if destination is occupied or target file has been modified.

OUT OF SCOPE:
- GUI History / Rollback view (covered in TASK-GUI-06).

DONE WHEN:
- Comprehensive integration tests demonstrate:
  1. Move operation is reversed to exact original path.
  2. If user edited the target file after the move, rollback refuses to overwrite and reports conflict.
  3. Deletion sent to Recycle Bin is documented with restoration instructions.
```

---

### TASK-SAFE-05: Rule Cycle & Self-Triggering Directory Loop Detector
```text
TASK ID: TASK-SAFE-05
OBJECTIVE:
Implement a static and runtime rule validator to detect recursive moves, source/destination folder nesting, and cyclic rule triggers.

DEPENDENCIES: TASK-BL-03
TARGET FILES:
- `TidyUp.Core/Validation/IRuleValidator.cs`
- `TidyUp.Core/Validation/RuleValidator.cs`
- `TidyUp.Tests/ValidationTests/RuleCycleValidatorTests.cs`

IN SCOPE:
- Checking whether target destination folder is identical to, or a subfolder of, the monitored source folder.
- Detecting cross-rule cycles (Rule A moves *.pdf from X to Y, Rule B moves *.pdf from Y to X).
- Checking invalid paths, reserved Windows filenames (CON, PRN, AUX, NUL), and path lengths exceeding MAX_PATH unless normalized.
- Preventing saving or enabling of any rule that contains a cycle or illegal path.

OUT OF SCOPE:
- UI validation error display (covered in Phase 2).

DONE WHEN:
- Validation engine returns explicit validation errors for nested source/destination, identical source/destination, and multi-rule ping-pong cycles.
- Full suite of unit tests passing with positive and negative path permutations.
```

---

### TASK-SAFE-06: FileSystemWatcher Overflow Reconciliation & Burst Debounce
```text
TASK ID: TASK-SAFE-06
OBJECTIVE:
Harden FileSystemWatcher to handle event bursts via debouncing and recover from InternalBufferOverflow by triggering a scheduled directory reconciliation scan.

DEPENDENCIES: TASK-BL-03
TARGET FILES:
- `TidyUp.Core/Watcher/HardenedFileSystemWatcher.cs`
- `TidyUp.Core/Watcher/IWatcherReconciler.cs`
- `TidyUp.Tests/WatcherTests/HardenedWatcherTests.cs`

IN SCOPE:
- Increasing `InternalBufferSize` to appropriate maximum (e.g., 64KB).
- Subscribing to `Error` event on `FileSystemWatcher` to catch buffer overflows.
- Implementing sliding-window debounce (e.g. 500ms) for burst events on the same file path (e.g. rapid writes).
- Triggering a full reconciliation scan of the watched folder when an overflow occurs to ensure no dropped events are lost.
- Handling Windows `Renamed` events properly by pairing OldFullPath and FullPath.

OUT OF SCOPE:
- GUI status icons (covered in Phase 3).

DONE WHEN:
- Unit/integration tests simulate rapid bursts of 500 files created simultaneously without dropped processing.
- Mocking a buffer overflow properly invokes the reconciliation scan.
```

---

### TASK-SAFE-07: Real-Filesystem Integration Test Harness
```text
TASK ID: TASK-SAFE-07
OBJECTIVE:
Create a robust integration test harness that executes rule simulation, actions, and rollbacks on real temporary disk directories.

DEPENDENCIES: TASK-SAFE-01, TASK-SAFE-02, TASK-SAFE-03, TASK-SAFE-04, TASK-SAFE-05
TARGET FILES:
- `TidyUp.Tests/Integration/TestDirectoryFixture.cs`
- `TidyUp.Tests/Integration/EndToEndFileActionTests.cs`

IN SCOPE:
- Setting up isolated temp directories per test run and ensuring 100% cleanup in `Dispose()`.
- Creating realistic test files (0-byte, multi-megabyte, read-only, unicode filenames, deep folder hierarchies).
- Testing multi-action chains (e.g., Rename then Move, or Copy then Delete).
- Testing partial failure mid-chain (e.g. 2nd action fails because destination directory is read-only) and verifying clean rollback.

OUT OF SCOPE:
- Testing UI rendering or WPF views.

DONE WHEN:
- Full integration test suite runs and passes cleanly via `dotnet test`.
- All temp files and folders are safely deleted upon test completion.
```

---

# Phase 2: End-to-End Workflow & GUI Implementation

### TASK-GUI-01: Operational Dashboard View
```text
TASK ID: TASK-GUI-01
OBJECTIVE:
Implement the primary Dashboard view answering: What is monitored? What needs attention? What happened recently?

DEPENDENCIES: TASK-BL-02, TASK-SAFE-01
TARGET FILES:
- `TidyUp/Views/DashboardView.xaml`
- `TidyUp/ViewModels/DashboardViewModel.cs`
- `TidyUp/Controls/StatusCard.xaml`

IN SCOPE:
- Displaying Monitored Folders list with active status and quick "Clean" / "Scan" action.
- Displaying aggregate operational statistics (Files Cleaned, Space Saved, Time Saved / Last Run).
- Displaying Recent Activity summary log with status indicators (Success, Warning, Error).
- Wiring navigation to Rule Editor, Preview, and History.

OUT OF SCOPE:
- Editing rules or complex condition building.

DONE WHEN:
- Dashboard serves as the default startup view.
- Real data from SQLite and Watcher service populates the cards without UI thread freezing.
```

---

### TASK-GUI-02: Guided 4-Stage Rule Creation Workflow
```text
TASK ID: TASK-GUI-02
OBJECTIVE:
Implement a guided 4-stage rule editor: Folders -> Conditions -> Actions -> Review & Dry Run.

DEPENDENCIES: TASK-BL-02, TASK-SAFE-01, TASK-SAFE-05
TARGET FILES:
- `TidyUp/Views/RuleEditor/RuleWizardView.xaml`
- `TidyUp/ViewModels/RuleEditor/RuleWizardViewModel.cs`
- `TidyUp/Views/RuleEditor/Stages/*.xaml`

IN SCOPE:
- Stage 1: Select monitored folders, recursive toggle, folder exclusion list.
- Stage 2: Nested condition builder (All/Any matching, extension, size, name pattern, dates).
- Stage 3: Ordered action list (Move, Copy, Rename, Safe Delete) with destination picker and conflict strategy (Skip, Overwrite, Rename (1)).
- Stage 4: Review summary with dry-run trigger and cycle validation check.
- Rule starts in `Disabled` state upon creation.

OUT OF SCOPE:
- Background automated monitoring activation (requires explicit user toggle).

DONE WHEN:
- User can complete the 4 stages from scratch, save a valid rule to SQLite, and see it appear in the rule list disabled by default.
```

---

### TASK-GUI-03: Plain-Language Rule Natural Language Summarizer
```text
TASK ID: TASK-GUI-03
OBJECTIVE:
Build a natural-language rule summary generator that translates condition and action configurations into human-readable sentences.

DEPENDENCIES: None (Domain logic).
TARGET FILES:
- `TidyUp.Core/Rules/RuleSummaryGenerator.cs`
- `TidyUp.Tests/RuleTests/RuleSummaryGeneratorTests.cs`

IN SCOPE:
- Generating plain text summaries, e.g.:
  "When a PDF file larger than 10MB is created in Downloads, move it to Documents/LargePDFs and rename using {Date}_{FileName}."
- Handling nested AND/OR condition groups cleanly in readable prose.
- Displaying summary in Rule Editor Review stage and Rule List cards.

OUT OF SCOPE:
- Parsing natural language back into rules (summary is strictly 1-way rule -> text).

DONE WHEN:
- Unit tests verify accurate sentence generation for standard, multi-condition, and multi-action rules.
```

---

### TASK-GUI-04: Side-by-Side Impact Preview Window
```text
TASK ID: TASK-GUI-04
OBJECTIVE:
Build a dedicated Side-by-Side Impact Preview window comparing original file paths with proposed destination paths before execution.

DEPENDENCIES: TASK-SAFE-01, TASK-GUI-02
TARGET FILES:
- `TidyUp/Views/PreviewWindow.xaml`
- `TidyUp/ViewModels/PreviewViewModel.cs`

IN SCOPE:
- Grid display showing: Original Path, Action Type, Target Path, Rule Matched, Status/Conflict.
- Color-coded badges for Move, Copy, Rename, Delete, Conflict.
- Filter controls: Filter by rule, file extension, action type, or conflicts only.
- Execution button disabled if conflicts remain unaddressed or simulation failed.
- "Apply Changes" button requires explicit confirmation.

OUT OF SCOPE:
- Modifying files without user clicking "Apply Changes".

DONE WHEN:
- Preview accurately displays all planned actions generated by `TASK-SAFE-01`.
- Clicking "Apply Changes" runs only approved actions and reports completion progress.
```

---

### TASK-GUI-05: Destructive-Action Safeguards & Batch Confirmations
```text
TASK ID: TASK-GUI-05
OBJECTIVE:
Implement modal confirmation safeguards for permanent deletion, file overwrites, and large batch operations.

DEPENDENCIES: TASK-SAFE-02, TASK-GUI-04
TARGET FILES:
- `TidyUp/Views/Dialogs/SafeguardConfirmationDialog.xaml`
- `TidyUp/ViewModels/Dialogs/SafeguardConfirmationViewModel.cs`

IN SCOPE:
- Threshold check: prompt explicit warning if batch affects more than 50 files (configurable).
- Dedicated warning dialog when an action specifies Overwrite existing file or Permanent Deletion.
- Showing list of files at risk inside the modal before confirmation.
- Default button focus set to "Cancel".

OUT OF SCOPE:
- Non-destructive operations (simple moves without overwrite).

DONE WHEN:
- Destructive operations cannot proceed without explicit dialog confirmation.
- Esc key or Cancel button safely aborts the batch with zero filesystem modifications.
```

---

### TASK-GUI-06: Expandable History & Log Viewer
```text
TASK ID: TASK-GUI-06
OBJECTIVE:
Build a paginated, memory-efficient history viewer with expandable rows detailing conditions matched, action sequence, and one-click rollback.

DEPENDENCIES: TASK-SAFE-04
TARGET FILES:
- `TidyUp/Views/HistoryView.xaml`
- `TidyUp/ViewModels/HistoryViewModel.cs`
- `TidyUp/Controls/HistoryRowControl.xaml`

IN SCOPE:
- Paginated query from SQLite `OperationJournalEntry` (25/50/100 records per page).
- Expanding a row displays: Original Path, Resulting Path, Rule Name, Condition evaluation details, SHA256 hash, Error message (if failed).
- "Undo / Rollback" button on eligible operations and batch runs.
- Search and filter by Date, Rule, Action Type, and Status (Success/Failed/RolledBack).
- Export log to CSV / JSON without loading entire database into memory.

OUT OF SCOPE:
- Live streaming log terminal (this is historical audit viewer).

DONE WHEN:
- History UI smoothly renders without UI lag.
- Clicking "Undo" on a past operation triggers `TASK-SAFE-04` rollback with status update.
```

---

### TASK-GUI-07: Explicit State Machine Enforcement
```text
TASK ID: TASK-GUI-07
OBJECTIVE:
Enforce application-wide lifecycle states across the UI: Rule Saved, Monitoring Active, Operation Running, and Operation Completed.

DEPENDENCIES: TASK-GUI-01, TASK-GUI-02, TASK-GUI-04
TARGET FILES:
- `TidyUp.Core/State/ApplicationStateManager.cs`
- `TidyUp/ViewModels/MainViewModel.cs`

IN SCOPE:
- Defining state enum: `Idle`, `ConfiguringRule`, `Simulating`, `PreviewReady`, `ExecutingBatch`, `MonitoringRunning`, `MonitoringPaused`.
- Enforcing workflow rules:
  1. Saving a rule does not start monitoring.
  2. Modifying a rule transitions it to unvalidated state requiring fresh preview.
  3. Pausing monitoring prevents new jobs while letting active file action complete cleanly.
- Reflecting state clearly in main window title bar and status bar.

OUT OF SCOPE:
- Adding external scheduling engines.

DONE WHEN:
- State transitions strictly follow the state matrix.
- Illegal transitions (e.g. editing an active rule during file execution) are disabled in UI.
```

---

### TASK-GUI-08: Accessibility, Empty States & Dirty-State Tracking
```text
TASK ID: TASK-GUI-08
OBJECTIVE:
Implement UI polish, accessible navigation, friendly empty states, and dirty-state tracking to prevent accidental data loss.

DEPENDENCIES: TASK-GUI-01, TASK-GUI-02, TASK-GUI-06
TARGET FILES:
- `TidyUp/Styles/AccessibilityStyles.xaml`
- `TidyUp/Controls/EmptyStateControl.xaml`
- Window and View code-behinds.

IN SCOPE:
- Empty states for: No Monitored Folders, No Rules Configured, No Matching Files, No History.
- Dirty tracking: Prompt confirmation if user attempts to close rule wizard or navigation with unsaved edits.
- Keyboard navigation (Tab index, Enter/Esc handling, Alt shortcuts).
- Window size and position persistence in user settings (prevent forced maximized-only mode).
- Accessible contrast and readable typography.

OUT OF SCOPE:
- Complete theme engine rewrite.

DONE WHEN:
- Empty states appear cleanly when database is empty.
- Closing rule editor with edits displays "Discard unsaved changes?" prompt.
- Window position persists across restarts.
```

---

# Phase 3: Monitoring, Health & Explainability

### TASK-MON-01: File Watcher Health & Degraded State Monitor (STATUS: COMPLETE)
```text
TASK ID: TASK-MON-01
STATUS: COMPLETE
OBJECTIVE:
Build a monitoring health service that detects disconnected folders, inaccessible network shares, and buffer degradation, reporting state to the UI.

DEPENDENCIES: TASK-SAFE-06
TARGET FILES:
- `TidyUp.Core/Monitoring/WatcherHealthMonitor.cs`
- `TidyUp.Core/Monitoring/WatcherHealthStatus.cs`
- `TidyUp/ViewModels/MonitoringHealthViewModel.cs`

IN SCOPE:
- Monitoring status enum: `Healthy`, `Paused`, `Degraded`, `Disconnected`, `Error`.
- Detecting when a watched directory is deleted, renamed, or network connection is severed.
- Periodic health heartbeat (every 30s) testing folder accessibility.
- Automatic reconnection attempt with backoff when network shares return.
- Emitting status updates to Dashboard.

OUT OF SCOPE:
- Email/SMS alerting.

DONE WHEN:
- Unplugging/disconnecting a watched folder immediately reflects `Degraded` or `Disconnected` status in UI without application crash.
```

---

### TASK-MON-02: Explainable Decision Engine (Condition Inspector) (STATUS: COMPLETE)
```text
TASK ID: TASK-MON-02
STATUS: COMPLETE
OBJECTIVE:
Provide detailed condition-by-condition match diagnostics explaining why a specific file was matched or rejected by a rule.

DEPENDENCIES: TASK-SAFE-01
TARGET FILES:
- `TidyUp.Core/Diagnostics/FileEvaluationDiagnostics.cs`
- `TidyUp/Views/Dialogs/RuleDiagnosticsDialog.xaml`

IN SCOPE:
- Recording pass/fail outcome for every condition node in the tree:
  - Condition: File Extension == ".pdf" -> TRUE
  - Condition: File Size > 10MB -> FALSE (File Size: 2.4MB)
  - Result: Rule Did Not Match.
- Allowing user to pick any file in a monitored folder and view the rule evaluation breakdown.

OUT OF SCOPE:
- Automatically rewriting rules.

DONE WHEN:
- Diagnostics dialog clearly displays tree view of conditions with green checkmarks and red crossmarks explaining why a file did or did not trigger.
```

---

### TASK-MON-03: Preview vs. Actual Execution Discrepancy Analyzer (STATUS: COMPLETE)
```text
TASK ID: TASK-MON-03
STATUS: COMPLETE
OBJECTIVE:
Compare the planned simulation actions against actual post-execution results, highlighting files that were skipped, locked, or changed during execution.

DEPENDENCIES: TASK-SAFE-01, TASK-SAFE-04
TARGET FILES:
- `TidyUp.Core/Diagnostics/ExecutionDiscrepancyAnalyzer.cs`
- `TidyUp/Views/ExecutionSummaryWindow.xaml`

IN SCOPE:
- Comparing `ExecutionPlan` items against executed `OperationJournalEntry` items.
- Categorizing outcomes: Executed Exactly as Planned, Skipped Due to Lock, Failed Due to Permission, File Disappeared Before Move.
- Presenting discrepancy summary to user after batch run finishes.

OUT OF SCOPE:
- Unattended automatic retry of failed items (requires user review).

DONE WHEN:
- Discrepancy report highlights any divergence between preview and reality.
- Integration tests verify detection of files modified between preview and execute.
```

---

### TASK-MON-04: In-Flight Operation Cancellation & Safe Pause Protocol (STATUS: COMPLETE)
```text
TASK ID: TASK-MON-04
STATUS: COMPLETE
OBJECTIVE:
Implement cooperative cancellation (`CancellationToken`) across file operations and a safe pause protocol for monitored watchers.

DEPENDENCIES: TASK-SAFE-01, TASK-SAFE-06
TARGET FILES:
- `TidyUp.Core/Processing/BatchProcessingCoordinator.cs`

IN SCOPE:
- Propagating `CancellationToken` through batch processing loop.
- "Stop / Cancel" button stops scheduling next file, waits for active file copy/move to safely finish (atomic file completion), and updates status to `PartiallyCompleted`.
- "Pause Monitoring" stops processing new watcher events, buffering them safely until resumed.

OUT OF SCOPE:
- Abruptly killing threads (`Thread.Abort` is prohibited).

DONE WHEN:
- Canceling a 100-file batch at file 15 stops after file 15 completes, leaving files 1-15 journaled and 16-100 untouched.
- No partially written corrupt files remain.
```

---

### TASK-MON-05: System Tray & Background Notification Service (STATUS: COMPLETE)
```text
TASK ID: TASK-MON-05
STATUS: COMPLETE
OBJECTIVE:
Implement system tray minimization, tray context menu (Pause, Resume, Status), and Windows 10/11 toast notifications for completed runs.

DEPENDENCIES: TASK-GUI-01, TASK-MON-01
TARGET FILES:
- `TidyUp/Services/TrayIconService.cs`
- `TidyUp/Services/ToastNotificationService.cs`

IN SCOPE:
- Minimize to tray option in settings.
- Tray icon reflects status (Active = Normal, Paused = Gray, Warning = Amber).
- Right-click tray menu: Open TidyUp, Pause Monitoring, Resume, Exit.
- Windows Toast notifications for: Batch completed with errors, Watched folder disconnected.
- Quiet mode toggle (suppress notifications).

OUT OF SCOPE:
- Windows service daemon (TidyUp runs as user desktop app).

DONE WHEN:
- Minimizing window hides to tray when enabled.
- Notifications trigger properly using native Windows notification APIs.
```

---

# Phase 4: Advanced Capabilities & Feature Expansion

### TASK-ADV-01: Multi-Rule Conflict & Dependency Analyzer (STATUS: COMPLETE)
```text
TASK ID: TASK-ADV-01
STATUS: COMPLETE
OBJECTIVE:
Analyze all active rules in the system for conflicting conditions, competing destinations, and execution priority ambiguities.

DEPENDENCIES: TASK-SAFE-05
TARGET FILES:
- `TidyUp.Core/Rules/MultiRuleConflictAnalyzer.cs`
- `TidyUp/Views/RuleConflictsWindow.xaml`

IN SCOPE:
- Detecting if two active rules match identical file patterns in the same folder with different destination targets.
- Evaluating rule priority order and highlighting shadowed rules (rules that will never run because a prior rule always catches the file first).
- Presenting visual conflict report with resolution recommendations (reorder rules or refine conditions).

OUT OF SCOPE:
- Automatic rule modification without user approval.

DONE WHEN:
- Unit tests verify accurate detection of shadowed rules and conflicting destination targets across multiple rules.
```

---

### TASK-ADV-02: Versioned Rule History & Configuration Diff (STATUS: COMPLETE)
```text
TASK ID: TASK-ADV-02
STATUS: COMPLETE
OBJECTIVE:
Store historical revisions of rules whenever edited, providing a visual configuration diff and one-click rollback to prior rule versions.

DEPENDENCIES: TASK-GUI-02
TARGET FILES:
- `TidyUp.Data/Entities/RuleRevision.cs`
- `TidyUp.Core/Rules/RuleRevisionManager.cs`
- `TidyUp/Views/RuleRevisionDiffView.xaml`

IN SCOPE:
- Storing immutable snapshot of rule JSON/entity upon every save.
- Linking each batch execution in the journal to the specific `RuleRevisionId` used.
- Visual side-by-side diff showing changed folders, conditions, or actions between revisions.
- "Restore this revision" button.

OUT OF SCOPE:
- Git integration for rules.

DONE WHEN:
- Modifying and saving a rule creates a revision.
- Past revisions can be viewed and restored.
```

---

### TASK-ADV-03: Pre-Packaged Rule Templates & Versioned Export/Import (STATUS: COMPLETE)
```text
TASK ID: TASK-ADV-03
STATUS: COMPLETE
OBJECTIVE:
Build a library of common starter rule templates and a versioned JSON import/export mechanism for sharing rules safely.

DEPENDENCIES: TASK-GUI-02
TARGET FILES:
- `TidyUp.Core/Templates/DefaultRuleTemplates.cs`
- `TidyUp.Core/Rules/RuleSerializationService.cs`
- `TidyUp.Tests/RuleTests/SerializationTests.cs`

IN SCOPE:
- Starter templates:
  1. "Organize Downloads by File Type (PDFs, Images, Archives, Installers)"
  2. "Archive Screenshots older than 30 days"
  3. "Sort Camera Photos by Year/Month taken"
- Export rules to JSON file with schema version header (`$schema`, `version: "1.0"`).
- Import validation: validate schema, sanitize paths, and import in `Disabled` state.

OUT OF SCOPE:
- Cloud rule sharing or public web repository.

DONE WHEN:
- User can create rule from template in 1 click.
- Exporting and importing preserves conditions and actions intact while validating schema.
```

---

### TASK-ADV-04: Hash-Based Duplicate File Detection Engine (STATUS: COMPLETE)
```text
TASK ID: TASK-ADV-04
STATUS: COMPLETE
OBJECTIVE:
Implement a high-performance hash-based duplicate file detector using quick file-size grouping, partial hash, and full SHA256 verification.

DEPENDENCIES: TASK-SAFE-01
TARGET FILES:
- `TidyUp.Core/Duplicates/DuplicateDetector.cs`
- `TidyUp.Core/Duplicates/FileHashCalculator.cs`
- `TidyUp.Tests/DuplicateTests/DuplicateDetectorTests.cs`

IN SCOPE:
- Step 1: Group files by exact byte size (skip singletons).
- Step 2: Compute partial hash of first 4KB for size-matched files.
- Step 3: Compute full SHA256 only for partial-hash matches.
- Action options for duplicates: Move to `_Duplicates` folder, Delete to Recycle Bin, or Skip.

OUT OF SCOPE:
- Perceptual image hashing or fuzzy matching.

DONE WHEN:
- Unit tests verify identical files with different names are recognized as duplicates without rehashing unique files.
```

---

### TASK-ADV-05: Scheduled Monitoring & Quiet Hours Engine (STATUS: COMPLETE)
```text
TASK ID: TASK-ADV-05
STATUS: COMPLETE
OBJECTIVE:
Add configurable schedule triggers (daily/weekly times) and quiet hours during which background file organization is paused.

DEPENDENCIES: TASK-MON-01
TARGET FILES:
- `TidyUp.Core/Scheduling/ScheduleManager.cs`
- `TidyUp/Views/ScheduleSettingsView.xaml`

IN SCOPE:
- Rule trigger options: `Continuous (Watcher)`, `Scheduled (Cron/Time of day)`, `Manual Only`.
- Quiet hours configuration (e.g., 09:00 - 17:00 pause automatic operations).
- Windows power state awareness: pause execution when running on low battery if configured.

OUT OF SCOPE:
- Windows Task Scheduler XML export (runs within app lifecycle).

DONE WHEN:
- Rules configured with quiet hours defer execution until quiet period ends.
```

---

### TASK-ADV-06: Document Content & Metadata Conditions (STATUS: COMPLETE)
```text
TASK ID: TASK-ADV-06
STATUS: COMPLETE
OBJECTIVE:
Extend rule condition engine beyond name/date/size to evaluate document text content and EXIF/media metadata.

DEPENDENCIES: TASK-SAFE-01
TARGET FILES:
- `TidyUp.Core/Conditions/ContentConditionEvaluator.cs`
- `TidyUp.Core/Conditions/MetadataExtractor.cs`

IN SCOPE:
- Text/Content conditions: Contains text, Matches regex in plain text, CSV, or Markdown.
- Image EXIF metadata: Date Taken, Camera Model, Dimensions (Width/Height).
- Audio/Video metadata: Duration, Artist, Album.
- Safe streaming: Read only required header/metadata without reading entire multi-gigabyte files into RAM.

OUT OF SCOPE:
- Heavy OCR engine integration.

DONE WHEN:
- Rules can accurately route photos based on EXIF Date Taken and text documents based on content keyword.
```

---

### TASK-ADV-07: SQLite Database Disaster Recovery & Migration Engine (STATUS: COMPLETE)
```text
TASK ID: TASK-ADV-07
STATUS: COMPLETE
OBJECTIVE:
Implement automated SQLite database backups, schema migration verification, and integrity check upon application startup.

DEPENDENCIES: TASK-SAFE-04
TARGET FILES:
- `TidyUp.Data/Recovery/DatabaseBackupManager.cs`
- `TidyUp.Data/Recovery/DbIntegrityChecker.cs`

IN SCOPE:
- Automated daily backup of SQLite database file before applying migrations.
- Executing `PRAGMA integrity_check` on startup.
- Migration safe-recovery: If migration fails, restore pre-migration backup and notify user.
- Export/Backup manual button in Settings.

OUT OF SCOPE:
- External cloud database sync.

DONE WHEN:
- Database corruption or failed migration is safely intercepted, pre-migration backup restored, and error logged.
```

---

# Phase 5: Repository Governance, CI & Documentation

### TASK-DOC-01: Authoritative README & Architecture Documentation (STATUS: COMPLETE)
```text
TASK ID: TASK-DOC-01
STATUS: COMPLETE
OBJECTIVE:
Replace the minimal 1-line README.md with comprehensive project documentation including purpose, supported Windows/.NET versions, installation, safety model, and architecture.

DEPENDENCIES: TASK-BL-04
TARGET FILES:
- `README.md`
- `docs/ARCHITECTURE.md`

IN SCOPE:
- Project overview, screenshots placeholder, supported OS (Windows 10/11) and .NET version.
- Step-by-step developer build & run instructions (`dotnet build`, `dotnet test`).
- Detailed explanation of Safety Invariants (Dry-run, Recycle Bin, Application Journal, Rollback).
- Clear architectural diagram and component layer explanation.

OUT OF SCOPE:
- Writing marketing materials.

DONE WHEN:
- README.md provides a complete, clear onboarding guide for new developers and users.
```

---

### TASK-DOC-02: Authoritative Feature Verification Matrix (STATUS: COMPLETE)
```text
TASK ID: TASK-DOC-02
STATUS: COMPLETE
OBJECTIVE:
Establish a single authoritative feature status document marking every capability as Implemented, Partially Implemented, Planned, or Verified with test evidence.

DEPENDENCIES: TASK-BL-04, TASK-DOC-01
TARGET FILES:
- `docs/FEATURE_MATRIX.md`

IN SCOPE:
- Tabular matrix listing every feature from the original GUI specification and TidyUp Plan.
- Status column: `[Verified]`, `[Implemented]`, `[Partial]`, `[Planned]`.
- Column linking to automated test file proving verification.
- Superseding historical fragmented completion reports.

OUT OF SCOPE:
- Deleting historical git commit logs.

DONE WHEN:
- Feature matrix accurately reflects current state and is linked directly from README.md.
```

---

### TASK-DOC-03: GitHub Actions CI Matrix Build & Automated Test Gate (STATUS: COMPLETE)
```text
TASK ID: TASK-CI-01
STATUS: COMPLETE
OBJECTIVE:
Configure a GitHub Actions CI workflow that builds the solution in Release mode, executes all tests, and runs dependency/security checks on every pull request.

DEPENDENCIES: TASK-BL-01, TASK-SAFE-07
TARGET FILES:
- `.github/workflows/ci.yml`

IN SCOPE:
- Windows-latest runner environment.
- Setup .NET SDK matching global.json or solution target.
- `dotnet restore`, `dotnet build --configuration Release --no-restore`.
- `dotnet test --configuration Release --no-build --verbosity normal --collect:"XPlat Code Coverage"`.
- Enforcing zero test failures as a merge prerequisite.

OUT OF SCOPE:
- Automated deployment to Windows Store or Chocolatey.

DONE WHEN:
- `.github/workflows/ci.yml` passes cleanly on GitHub Actions runner.
```

---

# Phase 6: UI/UX Modernization & Workflow Experience

### TASK-UI-01: View Redundancy Elimination & Registration Cleanup (STATUS: COMPLETE)
```text
TASK ID: TASK-UI-01
STATUS: COMPLETE
OBJECTIVE:
Consolidate duplicate Window and UserControl files across the Views directory into single-source UserControls.

DEPENDENCIES: None
TARGET FILES:
- `TidyUp/Views/HelpWindow.xaml` / `.cs`
- `TidyUp/Views/SettingsWindow.xaml` / `.cs`
- `TidyUp/Views/LogViewerWindow.xaml` / `.cs`
- `TidyUp/Views/RulePreviewWindow.xaml` / `.cs`
- `TidyUp/ViewModels/LogViewerViewModel.cs`
- `TidyUp/ViewModels/RulePreviewViewModel.cs`
- `TidyUp/ServiceConfiguration.cs`
- `TidyUp/ViewModels/MainWindowViewModel.cs`

IN SCOPE:
- Identify and remove redundant standalone Window wrappers (`HelpWindow`, `SettingsWindow`, `LogViewerWindow`, `RulePreviewWindow`) in favor of canonical UserControls (`HelpView`, `SettingsView`, `LogsView`, `PreviewWindow`).
- Remove dead ViewModels (`LogViewerViewModel`, `RulePreviewViewModel`).
- Update DI registrations in `ServiceConfiguration.cs` to eliminate orphan registrations.
- Update `MainWindowViewModel.cs` so `ViewHelp()` and `TestRuleAsync` use the canonical views cleanly.

OUT OF SCOPE:
- Redesigning the interior layout of the consolidated views.
- Modifying underlying ViewModel logic.
- Navigation rail changes.

DONE WHEN:
- All duplicate Window files are cleanly removed or consolidated.
- Application builds cleanly with zero compile errors.
- Full test suite passes.
```

---

### TASK-UI-02: Persistent Modern Navigation Rail (STATUS: NOT STARTED)
```text
TASK ID: TASK-UI-02
STATUS: NOT STARTED
OBJECTIVE:
Replace the legacy ToolBarTray in MainWindow.xaml with a persistent, modern Windows 11 navigation rail supporting the 6 core destinations.

DEPENDENCIES: TASK-UI-01
TARGET FILES:
- `TidyUp/MainWindow.xaml`
- `TidyUp/MainWindow.xaml.cs`
- `TidyUp/ViewModels/MainWindowViewModel.cs`
- `TidyUp/Models/Enums/NavigationView.cs`

IN SCOPE:
- Update `NavigationView` enum to include: `Dashboard`, `Rules`, `Folders`, `PreviewReview`, `History`, `Settings`.
- Replace `ToolBarTray` with a persistent left navigation sidebar or modern rail styled with subtle borders, consistent icon sizes, and accessible labels.
- Implement responsive resizing behavior (compact icon-only mode when window width < 1000px, expanded mode with labels when width >= 1000px).
- Add keyboard shortcuts for all 6 destinations (`Ctrl+1` through `Ctrl+6`).

OUT OF SCOPE:
- Modifying the internal content of individual views.
- Changing business logic or service interfaces.

DONE WHEN:
- Navigation rail renders cleanly with 6 active destinations and accessible focus.
- Clicking any destination smoothly switches the main content area.
- Resizing the main window handles layout adjustments cleanly.
- Unit tests verify `MainWindowViewModel.CurrentView` switching commands and shortcuts.
```

---

### TASK-UI-03: Real-Time Monitoring Status Bar & Health Indicator (STATUS: NOT STARTED)
```text
TASK ID: TASK-UI-03
STATUS: NOT STARTED
OBJECTIVE:
Upgrade the main window status bar to reflect real-time monitoring service state, active operations, and unresolved health warnings.

DEPENDENCIES: TASK-UI-02
TARGET FILES:
- `TidyUp/MainWindow.xaml`
- `TidyUp/ViewModels/MainWindowViewModel.cs`
- `TidyUp/Services/Monitoring/IWatcherHealthMonitor.cs`

IN SCOPE:
- Connect the status bar badge directly to `HardenedFileSystemWatcher` and `IWatcherHealthMonitor`.
- Visually distinguish states: `Running`, `Paused`, `Stopped`, `Starting`, `Recovering`, `Degraded`, and `Error`.
- Include an active work indicator (file progress ticker during batch execution) and an alert counter for inaccessible folders or buffer overflows.
- Provide tooltip summaries detailing the exact reason for degraded or paused states.

OUT OF SCOPE:
- Modifying watcher buffer internals or debounce timers.
- Modal error popups.

DONE WHEN:
- Status bar displays accurate service state without manual refresh.
- State transitions update badge background, icon, text, and accessible tooltips.
- Automated tests verify status bar properties reflect mocked health state changes.
```

---

### TASK-UI-04: Enhanced Rules List with Search, Filter & Unsaved Dirty Indicators (STATUS: NOT STARTED)
```text
TASK ID: TASK-UI-04
STATUS: NOT STARTED
OBJECTIVE:
Enhance the Rules view with multi-attribute filtering, sorting, plain-language summaries, and visual unsaved change indicators.

DEPENDENCIES: TASK-UI-02
TARGET FILES:
- `TidyUp/MainWindow.xaml` (Rules view section)
- `TidyUp/ViewModels/MainWindowViewModel.cs`

IN SCOPE:
- Add filter chips: All, Enabled Only, Disabled Only, Has Errors.
- Add sort dropdown: Execution Priority, Name, Last Run, Modification Date.
- Render a concise natural-language rule summary beneath each rule name in the list.
- Display a modified dirty badge (`*` / Unsaved icon) on rules with unsaved edits.
- Prompt with confirmation before discarding unsaved edits when switching rules.
- Ensure newly created rules default to Disabled.

OUT OF SCOPE:
- Redesigning condition logic or execution operators.
- Modifying SQLite rule persistence schema.

DONE WHEN:
- Search and filter chips dynamically filter the list without UI lag.
- Unsaved edits are clearly signaled visually and protected by discard prompts.
- Unit tests verify filtering, sorting, dirty-state detection, and discard protection.
```

---

### TASK-UI-05: Integrated Guided Wizard & Advanced Rule Editor Toggle (STATUS: NOT STARTED)
```text
TASK ID: TASK-UI-05
STATUS: NOT STARTED
OBJECTIVE:
Integrate the 4-stage guided wizard directly into the Rules view with an in-place toggle between Guided Workflow and Advanced Tabbed Editor.

DEPENDENCIES: TASK-UI-04
TARGET FILES:
- `TidyUp/MainWindow.xaml`
- `TidyUp/Views/RuleEditor/RuleWizardView.xaml`
- `TidyUp/ViewModels/MainWindowViewModel.cs`
- `TidyUp/ViewModels/RuleEditor/RuleWizardViewModel.cs`

IN SCOPE:
- Convert `RuleWizardView` into an embeddable UserControl usable inside `MainWindow`.
- Provide an editor mode switch: "Guided Workflow (4-Stage)" vs "Advanced Editor (Tabs)".
- Synchronize state between both modes so users can switch seamlessly without data loss.
- Provide explicit "Save Rule" and separate "Enable Rule" actions in both modes.

OUT OF SCOPE:
- Introducing new action types or condition properties.
- Changing rule serialization format.

DONE WHEN:
- Users can create or edit rules using either the 4-stage wizard or advanced tabs.
- Switching modes preserves all entered folders, conditions, and actions.
- Unit tests verify state synchronization between wizard VM and rule entity.
```

---

### TASK-UI-06: Condition Builder Nested Expression Visualization (STATUS: NOT STARTED)
```text
TASK ID: TASK-UI-06
STATUS: NOT STARTED
OBJECTIVE:
Improve visual hierarchy, inline validation, and regex assistance in Stage 2 of the Condition Builder.

DEPENDENCIES: TASK-UI-05
TARGET FILES:
- `TidyUp/Controls/ConditionEditorControl.xaml`
- `TidyUp/Views/RuleEditor/Stages/Stage2ConditionsView.xaml`
- `TidyUp/ViewModels/ConditionEditorViewModel.cs`

IN SCOPE:
- Visual indentation and group borders for nested AND/OR container conditions.
- Context-sensitive value editors (file picker, size units, date pickers) matching the selected property.
- Inline regex validation with real-time syntax checking and explanatory error messages.
- Add/remove buttons for condition groups with safety checks against accidental deletion.

OUT OF SCOPE:
- Changing the underlying boolean AST evaluator.

DONE WHEN:
- Nested condition trees render with clear visual boundaries and readable logical operators.
- Invalid regex strings display immediate inline warning banners.
- Unit tests verify nested group modifications preserve evaluation semantics.
```

---

### TASK-UI-07: Dedicated Folders Management View (STATUS: NOT STARTED)
```text
TASK ID: TASK-UI-07
STATUS: NOT STARTED
OBJECTIVE:
Create a dedicated top-level Folders view displaying all monitored directories, permissions, recursive flags, and linked rules.

DEPENDENCIES: TASK-UI-02
TARGET FILES:
- `TidyUp/Views/FoldersView.xaml` (New)
- `TidyUp/Views/FoldersView.xaml.cs` (New)
- `TidyUp/ViewModels/FoldersViewModel.cs` (New)
- `TidyUp/ServiceConfiguration.cs`

IN SCOPE:
- Aggregate all monitored folders from all rules into a centralized table/card layout.
- Display folder path, existence, read/write permission status, watcher health badge, and linked rules count.
- Provide actions: "Scan Folder Now", "Open in Explorer", "Edit Linked Rules", "Pause Watching".
- Clear empty state when no folders are monitored with quick-add action.

OUT OF SCOPE:
- Filesystem watcher engine modifications.

DONE WHEN:
- Folders view renders all watched directories across rules.
- Permission warnings are clearly displayed for inaccessible paths.
- Unit tests cover folder aggregation, scanning trigger, and empty states.
```

---

### TASK-UI-08: Folder Picker & Exclusion Pattern Configuration (STATUS: NOT STARTED)
```text
TASK ID: TASK-UI-08
STATUS: NOT STARTED
OBJECTIVE:
Provide an enhanced folder picker with recursive depth options and exclusion pattern management.

DEPENDENCIES: TASK-UI-07
TARGET FILES:
- `TidyUp/Views/FoldersView.xaml`
- `TidyUp/Views/RuleEditor/Stages/Stage1FoldersView.xaml`
- `TidyUp/ViewModels/FoldersViewModel.cs`

IN SCOPE:
- Windows native folder browse dialog integration.
- UI controls for configuring folder exclusion patterns (e.g. `node_modules`, `.git`, `*.tmp`).
- Validation against invalid drive roots, system protected directories, and disconnected network shares.

OUT OF SCOPE:
- Virtual filesystem abstraction layers.

DONE WHEN:
- Users can add folders and define exclusion lists with immediate syntax validation.
- Unit tests verify exclusion pattern storage and validation rules.
```

---

### TASK-UI-09: Integrated Preview & Review Workspace (STATUS: NOT STARTED)
```text
TASK ID: TASK-UI-09
STATUS: NOT STARTED
OBJECTIVE:
Promote the Preview dialog into a first-class, navigable Preview & Review workspace within the primary application shell.

DEPENDENCIES: TASK-UI-02, TASK-UI-05
TARGET FILES:
- `TidyUp/Views/PreviewView.xaml` (Convert from PreviewWindow)
- `TidyUp/Views/PreviewView.xaml.cs`
- `TidyUp/ViewModels/PreviewViewModel.cs`
- `TidyUp/MainWindow.xaml`

IN SCOPE:
- Host the preview table as a full-page navigable destination under `NavigationView.PreviewReview`.
- Display original path, planned action, proposed destination, matching rule name, and conflict status.
- Summary bar grouping planned operations by action type (Move: X, Copy: Y, Rename: Z, Recycle: W).
- Search box and filter toggles: "Conflicts Only", "Deletions Only", "Specific Rule".
- Virtualized DataGrid supporting 10,000+ simulated files smoothly.

OUT OF SCOPE:
- Executing actual filesystem changes from this view (handled in TASK-UI-10).

DONE WHEN:
- Preview view displays simulation results with sub-second virtualization response.
- Filter chips and search refine table items instantly.
- Automated tests verify preview data population without touching disk.
```

---

### TASK-UI-10: File-Level Explainability Inspector & Two-Step Apply Workflow (STATUS: NOT STARTED)
```text
TASK ID: TASK-UI-10
STATUS: NOT STARTED
OBJECTIVE:
Add an expandable "Why Did This File Match?" inspection drawer and a guarded two-step Apply workflow with pre-flight re-check.

DEPENDENCIES: TASK-UI-09
TARGET FILES:
- `TidyUp/Views/PreviewView.xaml`
- `TidyUp/ViewModels/PreviewViewModel.cs`
- `TidyUp/Services/Simulation/IExplainableDecisionEngine.cs`

IN SCOPE:
- Expandable side panel showing exactly which conditions passed or failed for a selected file.
- "Apply Changes" button requiring two-step explicit confirmation:
  1. Pre-flight re-check (verify files haven't changed or become locked since preview).
  2. Final impact confirmation dialog displaying total counts and Recycle Bin safety badge.
- Explicit cancellation support during batch execution.

OUT OF SCOPE:
- Automated rule adjustments.

DONE WHEN:
- Clicking any previewed file displays clear evaluation breakdown of all conditions.
- Applying changes requires confirmation, revalidates file states, and reports progress.
- Unit and integration tests verify pre-flight verification and cancellation handling.
```

---

### TASK-UI-11: History View Actionable Explanations & Safe Rollback Controls (STATUS: NOT STARTED)
```text
TASK ID: TASK-UI-11
STATUS: NOT STARTED
OBJECTIVE:
Enhance HistoryView with plain-language actionable error explanations and safe one-click rollback controls.

DEPENDENCIES: TASK-UI-02
TARGET FILES:
- `TidyUp/Views/HistoryView.xaml`
- `TidyUp/ViewModels/HistoryViewModel.cs`

IN SCOPE:
- Replace raw technical exceptions with user-actionable explanations (e.g. "Destination folder is read-only; check permissions").
- Expandable technical diagnostic drawer containing stack traces and error codes.
- Contextual Rollback button enabled only for operations marked reversible in the journal.
- Pre-rollback safety check: ensure the file has not been modified since the operation before reversing.

OUT OF SCOPE:
- Altering SQLite journal schema.

DONE WHEN:
- History view displays friendly explanations alongside expandable diagnostics.
- Rollback button verifies file hash integrity prior to reversal.
- Unit tests verify actionable error mappings and safe rollback triggers.
```

---

### TASK-UI-12: Categorized 5-Section Settings Experience (STATUS: NOT STARTED)
```text
TASK ID: TASK-UI-12
STATUS: NOT STARTED
OBJECTIVE:
Reorganize SettingsView into 5 structured sections with contextual risk explanations.

DEPENDENCIES: TASK-UI-02
TARGET FILES:
- `TidyUp/Views/SettingsView.xaml`
- `TidyUp/ViewModels/SettingsViewModel.cs`

IN SCOPE:
- Organize settings into tabbed/navigable sections:
  1. **General:** App startup, minimize to tray, notifications, theme.
  2. **Safety:** Deletion policy (Recycle Bin default), conflict defaults, confirmation prompts.
  3. **Performance:** Concurrency limits, scan batch size, debounce delay.
  4. **Storage & Logs:** Journal retention days, log verbosity, database backup location.
  5. **Advanced:** Database vacuum, debug logging, schema maintenance.
- Add descriptive subtitle and impact warning to every setting control.

OUT OF SCOPE:
- Modifying underlying configuration storage keys.

DONE WHEN:
- Settings view renders all 5 sections cleanly.
- Changes persist across app restarts and take immediate effect.
- Unit tests verify all settings bindings, persistence, and default values.
```

---

### TASK-UI-13: Windows 11 Styling & Centralized Design Tokens (STATUS: NOT STARTED)
```text
TASK ID: TASK-UI-13
STATUS: NOT STARTED
OBJECTIVE:
Standardize borders, typography, accent colors, and control sizes into centralized resource dictionaries.

DEPENDENCIES: TASK-UI-05, TASK-UI-09, TASK-UI-12
TARGET FILES:
- `TidyUp/Styles/Colors.xaml` (New/Updated)
- `TidyUp/Styles/Typography.xaml` (New/Updated)
- `TidyUp/Styles/Controls.xaml` (New/Updated)
- `TidyUp/App.xaml`

IN SCOPE:
- Adopt Windows 11 styling cues: subtle 1px borders, 4px/8px corner radii, standard 8px grid spacing.
- Centralize all brushes, font sizes, and card styles in `TidyUp/Styles/`.
- Ensure consistent appearance in both Light and Dark modes.
- Replace ad-hoc inline styles across views with theme-aware StaticResource references.

OUT OF SCOPE:
- Introducing external third-party styling packages.

DONE WHEN:
- All views render with visual consistency across fonts, paddings, and card elevations.
- Theme switching works seamlessly without unreadable low-contrast text.
```

---

### TASK-UI-14: Keyboard Navigation, High-DPI Scaling & Screen Reader Accessibility (STATUS: NOT STARTED)
```text
TASK ID: TASK-UI-14
STATUS: NOT STARTED
OBJECTIVE:
Audit and remediate keyboard focus, tab ordering, accessible automation names, and DPI scaling.

DEPENDENCIES: TASK-UI-13
TARGET FILES:
- `TidyUp/MainWindow.xaml`
- All Views in `TidyUp/Views/`

IN SCOPE:
- Set explicit, logical `TabIndex` across all primary workflows.
- Provide visible focus indicator borders on focused interactive controls.
- Add `AutomationProperties.Name` and `AutomationProperties.HelpText` to all icon buttons and input fields.
- Test and verify window layout at 100%, 125%, 150%, and 175% Windows display scaling.
- Ensure all dialogs fit within minimum supported window dimensions (900x600).

OUT OF SCOPE:
- Custom third-party accessibility tools.

DONE WHEN:
- All primary workflows can be fully operated via keyboard alone (Tab, Enter, Space, Esc, Arrows).
- Screen readers announce all controls and status updates clearly.
- Resizing and display scaling preserve layout readability without clipped buttons.
```

---

### TASK-UI-15: Full Integration Regression & Verification Release Gate (STATUS: NOT STARTED)
```text
TASK ID: TASK-UI-15
STATUS: NOT STARTED
OBJECTIVE:
Execute full automated test suite, perform multi-resolution smoke tests, and document completion against all 17 acceptance criteria.

DEPENDENCIES: All previous tasks (TASK-UI-01 through TASK-UI-14)
TARGET FILES:
- `TidyUp.Tests/`
- `TASK_CATALOG.md`
- `CHANGELOG.md`
- `README.md`

IN SCOPE:
- Run complete `dotnet test -c Release` suite, ensuring 100% pass rate.
- Add UI workflow regression tests for navigation switching, rule wizard persistence, and preview filtering.
- Validate against all 17 Acceptance Criteria defined in Section 14 of the specification.
- Update documentation and master task catalog with complete verification proof.

OUT OF SCOPE:
- Feature additions or new scope.

DONE WHEN:
- 100% automated test suite passes cleanly.
- All 17 acceptance criteria are verified and marked complete with concrete evidence.
- Clean git working tree and focused release commit.
```

