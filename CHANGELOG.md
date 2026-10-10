# Changelog

All notable changes to the **TidyUp** application are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---

## [1.0.0] - 2026-10-10

### Summary
Initial production release of TidyUp, completing all 34 tasks across Phases 0 through 5 of the Master Implementation Plan with 337 passing automated tests and zero failures.

---

### Phase 0: Baseline Audit & Truth Reconciliation
- **Baseline Build Verification (`TASK-BL-01`):** Verified .NET 9 Release build and cataloged existing test baseline in `docs/baseline_test_report.md`.
- **UI Inventory (`TASK-BL-02`):** Mapped all XAML controls to underlying viewmodels and services in `docs/ui_inventory.md`.
- **Filesystem Audit (`TASK-BL-03`):** Conducted end-to-end audit of file operations and lock handling in `docs/filesystem_audit.md`.
- **Defect Catalog (`TASK-BL-04`):** Reconciled historical documentation and produced `docs/baseline_defects.md`.

---

### Phase 1: Safety Foundation & Core Engine Hardening
- **Dry-Run Simulation Engine (`TASK-SAFE-01`):** Implemented `ExecutionPlanGenerator` for read-only preview of proposed file operations, virtual path calculation, and collision pre-detection.
- **Safe Deletion (`TASK-SAFE-02`):** Implemented `WindowsShellFileOperations` using Windows Shell `SHFileOperation` to route file deletions safely to the Recycle Bin.
- **File Lock Detection (`TASK-SAFE-03`):** Added `FileLockDetector` and `RetryQueueManager` with non-blocking exponential backoff polling for ongoing downloads and writes.
- **Transactional Journal & Rollback Engine (`TASK-SAFE-04`):** Created `OperationJournalEntry` in SQLite with pre- and post-run SHA-256 hashes, paired with `RollbackEngine` for 1-click inverse operations.
- **Rule Cycle & Loop Detector (`TASK-SAFE-05`):** Created `RuleValidator` graph cycle detection preventing infinite file bouncing between rules.
- **Hardened File Watcher (`TASK-SAFE-06`):** Engineered `HardenedFileSystemWatcher` with 64KB buffers, 500ms debouncing, and automated `InternalBufferOverflowException` directory reconciliation.
- **Real Filesystem Test Harness (`TASK-SAFE-07`):** Built `TestDirectoryFixture` to validate all file operations against genuine temporary disk hierarchies.

---

### Phase 2: End-to-End Workflow & GUI Implementation
- **Operational Dashboard (`TASK-GUI-01`):** Redesigned `DashboardView` with live folder health cards, active rule toggles, and metrics.
- **4-Stage Rule Wizard (`TASK-GUI-02`):** Implemented guided wizard workflow with step-by-step validation.
- **Natural Language Rule Summarizer (`TASK-GUI-03`):** Added `RuleSummaryGenerator` converting complex nested condition trees into plain English sentences.
- **Side-by-Side Impact Preview Window (`TASK-GUI-04`):** Added `PreviewWindow` displaying planned executions with conflict indicators.
- **Safeguard Confirmations (`TASK-GUI-05`):** Implemented `SafeguardConfirmationDialog` prompting user before executing destructive or high-volume batches.
- **Expandable History Viewer (`TASK-GUI-06`):** Created `HistoryView` displaying journal logs with rollback buttons.
- **Application State Funnel (`TASK-GUI-07`):** Implemented `ApplicationStateManager` centralizing lifecycle states.
- **Accessibility & Dirty-State Guard (`TASK-GUI-08`):** Added unsaved-changes protection preventing accidental data loss on window close or tab switch.

---

### Phase 3: Monitoring, Health & Diagnostics
- **Watcher Health Monitor (`TASK-MON-01`):** Added `WatcherHealthMonitor` with real-time status transitions and network reconnect handling.
- **Condition Inspector & Explainability (`TASK-MON-02`):** Implemented `RuleEvaluationInspectorViewModel` offering condition-by-condition diagnostic evaluations.
- **Execution Discrepancy Analyzer (`TASK-MON-03`):** Implemented `ExecutionDiscrepancyAnalyzer` detecting differences between planned and executed actions.
- **In-Flight Cancellation Protocol (`TASK-MON-04`):** Added `CancellationTokenSource` integration for pausing and canceling batches mid-execution.
- **System Tray & Toast Notifications (`TASK-MON-05`):** Implemented `TrayIconService` and `ToastNotificationService` for desktop alerts.

---

### Phase 4: Advanced Engines & Extensibility
- **Multi-Rule Conflict Analyzer (`TASK-ADV-01`):** Added `MultiRuleConflictAnalyzer` detecting competing destinations and shadowed rules.
- **Versioned Rule History & Diff (`TASK-ADV-02`):** Implemented `RuleRevisionManager` and `RuleRevisionDiffView` for side-by-side visual diffs and 1-click rollback.
- **Rule Templates & Versioned Export/Import (`TASK-ADV-03`):** Added starter templates and `RuleSerializationService` with schema validation and path sanitization.
- **Hash-Based Duplicate File Detector (`TASK-ADV-04`):** Built 3-step duplicate detection engine (file-size grouping $\rightarrow$ 4KB partial hash $\rightarrow$ full SHA-256) with auto-isolation into `_Duplicates`.
- **Scheduled Monitoring & Quiet Hours (`TASK-ADV-05`):** Added `ScheduleManager`, overnight quiet hours, and Win32 low-battery throttling.
- **Content & EXIF Metadata Conditions (`TASK-ADV-06`):** Added safe streaming text/regex searching, image EXIF dimensions/Date Taken, and MP3 ID3/WAV duration extraction.
- **SQLite Disaster Recovery (`TASK-ADV-07`):** Implemented `DatabaseBackupManager` and `DbIntegrityChecker` with pre-migration backups and startup `PRAGMA integrity_check`.

---

### Phase 5: Repository Governance, CI & Documentation
- **Authoritative README & Architecture (`TASK-DOC-01`):** Comprehensive `README.md` and `docs/ARCHITECTURE.md`.
- **Feature Verification Matrix (`TASK-DOC-02`):** Created `docs/FEATURE_MATRIX.md` with complete test proof mapping.
- **GitHub Actions CI Workflow (`TASK-DOC-03`):** Added `.github/workflows/ci.yml` running matrix builds, tests, and coverage on Windows runners.
- **Repository Hygiene:** Added `.gitignore`, `CONTRIBUTING.md`, and `LICENSE`.
