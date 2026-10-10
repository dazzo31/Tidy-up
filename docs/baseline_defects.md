# TidyUp: Baseline Defect Catalog & Historical Documentation Reconciliation

**Task Reference:** `TASK-BL-04`  
**Execution Date:** 2026-10-09  
**Dependencies:** `TASK-BL-01`, `TASK-BL-02`, `TASK-BL-03`  
**Scope:** Historical documentation audit, defect reconciliation, and authoritative defect backlog.

---

## 1. Executive Summary

A critical reason for project misalignment in previous development sessions was the proliferation of historical status documents claiming full completion:
- `FINAL_STATUS.md` claimed: *"ALL PLANNED FEATURES COMPLETE... 100% complete with all originally planned features"*.
- `COMPLETED.md` claimed: *"PROJECT COMPLETE... FULLY FUNCTIONAL"*.
- `SPECIFICATION_PROGRESS.md` claimed: *"Folder Monitoring: COMPLETE... Testing: 100% COMPLETE"*.

The Phase 0 code audits (`TASK-BL-01`, `TASK-BL-02`, and `TASK-BL-03`) demonstrate that while the core rule-editor UI and service-level unit tests are functional, **the actual file monitoring engine is completely disconnected, critical data loss hazards exist in file execution, and multiple UI components are orphaned or unreachable.**

This document establishes the **single authoritative source of truth** regarding known defects and supersedes all prior historical completion claims.

---

## 2. Historical Claims vs. Code Reality Reconciliation

| Historical Document | Document Claim | Code Reality (Verified by Audit) | Severity Gap |
| :--- | :--- | :--- | :--- |
| `FINAL_STATUS.md`<br>`COMPLETED.md` | **"Folder Monitoring: 100% Complete"** | `FileMonitorService` is registered in DI as a singleton, but is **never started or called** anywhere in the application. Tray pause/resume handlers are empty `TODO` stubs. No directory is monitored at runtime. | **Critical Disconnect** |
| `FINAL_STATUS.md`<br>Line 64 | **"LogViewerWindow accessible via History button"** | The toolbar button is named "Logs" and switches views to embedded `LogsView.xaml`. `LogViewerWindow.xaml` is never displayed, and has a broken XAML DataTrigger. | **Stale Claim** |
| `SPECIFICATION_PROGRESS.md`<br>Line 66 | **"Preview/Test Mode: Complete dry run"** | `RulePreviewWindow.xaml` scans files and displays matches, but has **no execution or apply button**. The user cannot run actions from the preview window. | **Partial / Incomplete** |
| `SPECIFICATION_PROGRESS.md`<br>Line 96 | **"First-Run Wizard: COMPLETE"** | `FirstRunWindow.xaml` and `FirstRunViewModel.cs` exist, but `App.xaml.cs` never checks `AppSettings.ShowFirstRunWizard` and never displays the wizard. It is 100% dead code. | **Dead Code** |
| `COMPLETED.md`<br>Line 47 | **"Delete File: Recycle Bin support"** | If Recycle Bin is disabled or fails (network share), deletion defaults to permanent `File.Delete()` with no warning, no transaction, and no rollback journal. | **Safety Hazard** |
| `FINAL_STATUS.md`<br>Line 85 | **"Unit Tests: 9 (all passing)"** | The test suite actually contains 61 unit tests (all passing in `TidyUp.Tests.Unit.Services`), but 0 tests cover real disk I/O, watcher events, or UI bindings. | **Documentation Out of Date** |

---

## 3. Authoritative Baseline Defect Backlog

### Priority P0: Release Blockers (Data Loss & Safety Hazards)

| Defect ID | Component | Description & Line Citations | Required Resolution Task |
| :--- | :--- | :--- | :--- |
| **DEF-P0-01** | `ActionExecutor.cs`<br>Lines 85-87, 187-189, 212-214 | **Pre-Move Deletion Hazard:** In `ExecuteMoveAsync` and `ExecuteRenameAsync`, when `ConflictResolution.Overwrite` is active, `File.Delete(destPath)` is called before attempting `File.Move()`. If the move fails, the destination file is permanently destroyed. | `TASK-SAFE-01`, `TASK-SAFE-04` |
| **DEF-P0-02** | `ActionExecutor.cs`<br>Lines 402-425 | **Unbounded Empty Directory Recursion:** `RemoveEmptyFoldersAsync` climbs parent folders without checking the monitored root boundary, risking deletion of user system folders (`Downloads`, `Documents`). | `TASK-SAFE-05` |
| **DEF-P0-03** | `FileMonitorService.cs`<br>`App.xaml.cs`<br>Lines 122-132 | **Disconnected Monitoring Engine:** `FileMonitorService.StartAsync()` is never called on app startup or rule save. System tray pause/resume options are empty stubs. Background automation is non-functional. | `TASK-SAFE-06`, `TASK-GUI-07` |
| **DEF-P0-04** | `FileMonitorService.cs`<br>Lines 116-121 | **Watcher Event Loss on Bursts:** `FileSystemWatcher` buffer size defaults to 8KB; does not listen to `watcher.Error`. Bursts drop events without notice or recovery scan. | `TASK-SAFE-06` |
| **DEF-P0-05** | Solution-wide | **Missing Rule Cycle & Nesting Detection:** No validation prevents moving files into subfolders of monitored directories, risking infinite processing loops. | `TASK-SAFE-05` |
| **DEF-P0-06** | `TidyUp.Data`<br>`ActionLogEntity.cs` | **Missing Rollback Journal:** SQLite schema lacks batch IDs, file hashes, and restoration status. Reversing accidental or flawed operations is unsupported. | `TASK-SAFE-04` |

---

### Priority P1: Usability, Workflow & Architectural Disconnections

| Defect ID | Component | Description & Line Citations | Required Resolution Task |
| :--- | :--- | :--- | :--- |
| **DEF-P1-01** | `RulePreviewWindow.xaml` | **Preview Lacks Execution Trigger:** Users can run dry-run scans, but cannot execute approved actions from the preview window. | `TASK-GUI-04` |
| **DEF-P1-02** | `RuleTestViewModel.cs` | **Orphaned Test ViewModel:** Contains folder-testing logic but has no XAML View, is not in DI, and is completely unused. | `TASK-GUI-04` |
| **DEF-P1-03** | `FirstRunWindow.xaml` | **Unreachable Onboarding Wizard:** Wizard code exists but is never instantiated or displayed on application launch. | `TASK-GUI-01` / `TASK-GUI-08` |
| **DEF-P1-04** | `FileMonitorService.cs`<br>Lines 198-208 | **Flawed File Lock Detection:** Checks readiness using `FileShare.ReadWrite` instead of exclusive `FileShare.None`. Locked files are discarded with no retry queue. | `TASK-SAFE-03` |
| **DEF-P1-05** | `PathValidator.cs` | **Dead Validation Logic:** Reserved Windows device name checks and path length checks are defined but never invoked. | `TASK-SAFE-05` |
| **DEF-P1-06** | `ServiceConfiguration.cs`<br>Lines 49-53 | **DbContext Concurrency Hazard:** Repositories inject raw `TidyUpDbContext` directly instead of `IDbContextFactory`, risking concurrency crashes across worker threads. | `TASK-SAFE-04` |
| **DEF-P1-07** | `LogViewerWindow.xaml`<br>Lines 188-199 | **Broken Detail Panel Trigger:** Binding `SelectedLog` directly to `Visibility.Visible` prevents the details panel from opening. | `TASK-GUI-06` |
| **DEF-P1-08** | `MainWindow.xaml`<br>Lines 500-511 | **Dead Embedded HelpView:** Embedded `HelpView` in `MainWindow` has no DataContext and is never switched to. | `TASK-GUI-08` |

---

### Priority P2: Code Polish, Governance & Maintenance

| Defect ID | Component | Description & Line Citations | Required Resolution Task |
| :--- | :--- | :--- | :--- |
| **DEF-P2-01** | `LogViewerWindow`, `SettingsWindow` | **Redundant Window Wrappers:** Duplicate standalone windows exist alongside embedded views. | `TASK-GUI-08` |
| **DEF-P2-02** | `TidyUp.csproj` | **Vulnerable Dependency:** Package `SharpCompress 0.41.0` triggers moderate vulnerability NU1902 (`GHSA-6c8g-7p36-r338`). | `TASK-DOC-03` / Phase 5 |
| **DEF-P2-03** | `ServiceConfiguration.cs`<br>Line 78 | **Missing EF Core Migrations:** `EnsureDatabaseCreatedAsync()` used instead of database migrations, preventing schema updates. | `TASK-ADV-07` |
| **DEF-P2-04** | Repository Root | **Contradictory Historical Documentation:** 6+ markdown reports contain conflicting, out-of-date claims of project completion. | `TASK-DOC-01`, `TASK-DOC-02` |

---

## 4. Documentation Superseding Directives

The following historical documents in the repository are **SUPERSEDED** by the Phase 0 audit deliverables:

1. `FINAL_STATUS.md` $\rightarrow$ **SUPERSEDED** by `docs/baseline_defects.md` and `docs/ui_inventory.md`.
2. `COMPLETED.md` $\rightarrow$ **SUPERSEDED** by `docs/baseline_defects.md`.
3. `SPECIFICATION_PROGRESS.md` $\rightarrow$ **SUPERSEDED** by `docs/baseline_defects.md`.
4. `GUI_FIXES_COMPLETE.md` $\rightarrow$ **SUPERSEDED** by `docs/ui_inventory.md`.
5. `TESTING.md` $\rightarrow$ **SUPERSEDED** by `docs/baseline_test_report.md`.

---

## 5. Phase 0 Completion Assessment

With the completion of `TASK-BL-01`, `TASK-BL-02`, `TASK-BL-03`, and `TASK-BL-04`:
- **Phase 0 (Establish the Baseline) is 100% COMPLETE.**
- All historical ambiguities are reconciled.
- The project is fully prepared to enter **Phase 1: Safety Foundation & Core Engine Hardening**, starting with `TASK-SAFE-01` (Read-Only Dry-Run Simulation Engine).

