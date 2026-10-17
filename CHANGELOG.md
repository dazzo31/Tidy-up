# Changelog

All notable changes to the **TidyUp** application are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---

## [1.2.0] - 2026-10-17

### Summary
Quality and visual-consistency release (Phase 7): five defects found while reviewing the live v1.1.0 build, a full
theme-token migration, a unified status vocabulary, a responsive layout pass, real branding, and the About
surface. **617 automated tests passing with 0 failures.**

### Fixed
- **History and rollback were unavailable on databases created by earlier releases (`F-01`, `FIX-01`).** The
  database was created with `EnsureCreated`, so it had no migration history and later schema additions
  (`OperationJournal`, rule revisions) never applied. The migrator now recognises a legacy schema and baselines
  it, backs up before applying anything, and refuses to baseline if a legacy column is missing.
- **Tests could write to the live `%APPDATA%\TidyUp` profile (`F-02`, `FIX-02`).** A test-process module
  initializer redirects `TIDYUP_APPDATA` to a temp folder, and a source-scanning guard fails the build if a test
  resolves the real application-data path.
- **Repeated transient paths were not flagged (`F-03`, `FIX-03`).** `TransientLocations` now identifies Temp-style
  folders and the Folders hub warns when a rule watches one.
- **The theme could not follow Windows and settings reverted on close (`F-04`, `FIX-04`).** `System` is the new
  default, Dark is a real palette, and one shared `AppSettings` instance replaces per-view copies that re-saved a
  stale object on exit. A schema-version migration turns the old forced-Dark default into `System`.
- **The first-run wizard appeared on every launch, and Skip did not skip (`F-05`, `FIX-05`).** It now shows once,
  records that it ran, reuses Settings → General for "run again", and no longer ends the app when it is the only
  window.
- **Folders grid text was clipped** and its Scope/Exclusions wording was ambiguous; empty cells now show an em
  dash and Scan is disabled on folders that cannot be read, with a tooltip saying why.
- **Rule list items were hard to read:** they now carry an enabled/disabled pill and a `{n} folders · summary`
  subtitle instead of a bare sentence.
- **Filter inputs had no visible hints or accessible names** (22 inputs), and fixed heights clipped Material's
  outlined controls.
- Auxiliary navigation items are grouped under a "More" caption; Logs is no longer dimmed as if disabled.

### Added
- **Theme tokens and a style guard (`DS-10`).** Every hard-coded colour (175 of them) now comes from
  `Styles/Colors.xaml` / `Colors.Dark.xaml` via `{DynamicResource}` or `ThemeBrush`, so badges follow the active
  theme. `StyleGuardTests` fails the build if a literal colour is reintroduced.
- **One folder-status vocabulary (`DS-02`).** `FolderStatus` + `FolderStatusResolver` replace the separate
  dashboard and folders wordings; a shared `StatusBadge` shows the same icon, text and colours on every screen.
- **`PageHeader` (`DS-01`)** so all destinations share one title size and position, with `MORE`-grouped
  navigation (`DS-09`).
- **Real branding (`BRAND-01`, `BRAND-02`)**: a 9-size multi-resolution icon (16–256 px) built by
  `scripts/Build-Icon.ps1`, a theme-aware in-app logo, `ApplicationIcon` embedded in the executable, and complete
  assembly metadata (Product, Company, Copyright, `1.2.0`).
- **About section (`BRAND-03`)** in Settings → Advanced: version and short commit, runtime line, links to
  releases/source/licence, and a "copy diagnostics" button.
- **Error empty-state (`DS-06`).** `EmptyStateControl` gained an `Error` variant with a details panel and a
  secondary action; History uses it when the journal cannot be read.
- **Responsive Settings (`DS-05`)**: cards reflow from one column to three depending on window width.
- **`docs/screenshots/` and `scripts/Capture-Screenshots.ps1`:** all 7 destinations in Light and Dark.
- Guard tests: `PageHeaderTests`, `BrandAssetTests`, `InputLabelGuardTests`, `ResponsiveLayoutTests`,
  `FolderStatusTests`, `FolderScanAvailabilityTests`, `SettingsAboutTests`, `ThemePaletteTests` (WCAG AA contrast
  of every token pair), `ThemeTokenTests`.

### Changed
- `FolderStatus` (7 values) replaces the two overlapping status vocabularies; "Disconnected" now reads "Missing"
  consistently.
- Settings, Folders, Preview, History and Logs use 24 px page padding (previously a mix of 0/16/24).
- The Logs statistics and status chips, the rule wizard stepper, and the Folders stats icons now use semantic
  tokens rather than named colours, several of which failed contrast.
- README destination list, hero screenshot, and the screenshot documentation.

---

## [1.1.0] - 2026-10-16

### Summary
Comprehensive UI/UX modernization, accessibility overhaul, and workflow experience release (Phase 6: `TASK-UI-01` through `TASK-UI-15`). 426 automated tests passing with 0 failures.

### Added & Enhanced (Phase 6: UI/UX Modernization & Workflow Experience)
- **Persistent Navigation Rail (`TASK-UI-02`):** Modern Windows 11 left rail navigation with 6 core destinations (Dashboard, Rules, Folders, Preview & Review, History, Settings) and keyboard shortcuts (`Ctrl+1` through `Ctrl+6`).
- **Real-Time Monitoring Status Bar (`TASK-UI-03`):** Interactive health badge with animated status indicators, degraded state alerts, and dynamic folder telemetry.
- **Rules List Filtering & Dirty Tracking (`TASK-UI-04`):** Filter chips (All, Enabled, Disabled, Has Errors), sorting dropdown, plain-language summaries, and unsaved-changes dirty indicator.
- **Dual-Mode Rule Editor & 4-Stage Wizard (`TASK-UI-05`):** Lossless dual-mode synchronization between 4-stage guided wizard and tabbed advanced rule editor.
- **Nested Condition Builder (`TASK-UI-06`):** Visual tree grouping cards, context-sensitive date/number editors, `FileSizeUnit` selector (`Bytes`, `KB`, `MB`, `GB`), and inline regex validation error feedback.
- **Dedicated Folders Management Hub (`TASK-UI-07` & `TASK-UI-08`):** Full-page folders workspace with directory permission audit (`Read/Write`, `Read Only`, `Access Denied`), system-protected path detection, and exclusion patterns (`.git`, `node_modules`).
- **Integrated Preview & Review Workspace (`TASK-UI-09` & `TASK-UI-10`):** Full-page dry-run simulation workspace with operation breakdown counters, virtualized DataGrid, side-by-side impact review, and cooperative batch cancellation.
- **Explainability Inspector (`TASK-UI-10`):** "Why Did This File Match?" drawer evaluating and presenting per-condition pass/fail decision breakdowns.
- **Two-Step Guarded Apply Workflow (`TASK-UI-10`):** Pre-flight filesystem mutation re-check verifying files haven't been deleted or altered since simulation before applying disk operations.
- **Actionable History & Safe Rollback (`TASK-UI-11`):** Plain-language actionable failure explanations, expandable technical diagnostic telemetry drawer, and pre-rollback SHA-256 target verification preventing clobbering external modifications.
- **Categorized 5-Section Settings Experience (`TASK-UI-12`):** Reorganized settings into General, Safety, Performance, Storage & Logs, and Advanced sections with contextual risk explanations, database VACUUM optimization, and manual backups.
- **Centralized Windows 11 Tokens & Accessibility (`TASK-UI-13` & `TASK-UI-14`):** Standardized design tokens in `Styles/Colors.xaml`, `Styles/Typography.xaml`, `Styles/Controls.xaml`, and `Styles/AccessibilityStyles.xaml` with high-contrast focus rings, explicit `TabIndex`, and high-DPI scaling.
- **Dead Code Cleanup (`TASK-UI-01`):** Removed 10 dead legacy views (-1,463 lines) from codebase.
- **Full Integration Regression Gate (`TASK-UI-15`):** End-to-end UI workflow regression suite verifying all 17 Acceptance Criteria.

### Hardening & Governance (pre-release)
- **Shared Monitored-Folder Catalog:** Introduced `MonitoredFolderCatalog` as the single source of truth for monitored folders, backed by `FolderPathNormalizer` (canonical path casing/slashes/UNC) and `FolderValidator` (existence + permission audit). A `RulesChanged` event keeps catalog, Dashboard, Folders hub, and Rule Wizard synchronized.
- **Dependency Security:** Resolved SharpCompress zip-slip vulnerability (GHSA-6c8g-7p36-r338) by bumping 0.41.0 → 0.48.0, and cleared the transitive `SQLitePCLRaw.lib.e_sqlite3` High severity advisory (GHSA-2m69-gcr7-jv3q) by aligning EF Core Sqlite / Microsoft.Extensions.* to 9.0.21. Adapted `ActionExecutor` to the renamed `ArchiveFactory.OpenArchive` API.
- **CI Security Gate:** Added a `dotnet list package --vulnerable` step that fails the build workflow when any vulnerable package (direct or transitive) is present.

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
