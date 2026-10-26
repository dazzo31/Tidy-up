# TidyUp v1.2.0 Remediation: Feature Integration Matrix

**Baseline Tag:** `v1.2.0`  
**Current Commit:** `59d3bb45d7ca622ec3a80a7c2b3b0ff21e89358c`  
**Target Task:** `TASK-REM-36`

---

## Component vs. Reachable Feature Truth Table

| Capability | Component Present in Code | Reachable in Live UI | Active at Startup / Background | Verification Status on `59d3bb4` | Remediation Plan |
| :--- | :---: | :---: | :---: | :---: | :--- |
| **Read-Only Rule Dry-Run** | Yes (`ExecutionPlanGenerator`) | Yes (`PreviewView`) | N/A | **PARTIAL** | Fails for disabled rules (`TU-05`) and multi-action chains (`TU-13`). Fix in Phase 2. |
| **Manual Batch Apply** | Yes (`BatchProcessingCoordinator`) | Yes (`PreviewView`) | N/A | **FUNCTIONAL** | Minor issues with counter drift (`TU-15`) and post-apply button state (`TU-24`). Fix in Phase 3. |
| **Automatic File Watcher** | Yes (`FileMonitorService`) | Yes (Toggle in UI) | **NO** (`TU-01`) | **UNWIRED** | Startup does not call `StartAsync`; no subscriber to `FileDetected`. Wire in `TASK-REM-24`. |
| **Recycle Bin Safe Delete** | Yes (`WindowsShellFileOperations`) | Yes (Action Editor) | N/A | **FUNCTIONAL** | Permanent delete authorization blocked by checkbox contract (`TU-19`). Fix in `TASK-REM-03`. |
| **Multi-Action Chaining** | Yes (`ActionExecutor`) | Yes (Action Editor) | N/A | **PARTIAL** | Preflight check fails on intermediate virtual paths (`TU-13`). Fix in `TASK-REM-14`. |
| **Operation Rollback (Undo)** | Yes (`RollbackEngine`) | Yes (`HistoryView`) | N/A | **MISLEADING** | Deletions falsely marked "RolledBack" without actual restoration (`TU-20`). Fix in `TASK-REM-01`. |
| **SQLite Migration & Backup** | Yes (`DatabaseMigrator`) | Automatic | Yes | **VERIFIED** | Baseline detection and safe migration fully functional. Retain. |
| **Theme Switching (Light/Dark)** | Yes (`ThemeService`) | Yes (`SettingsView`) | Yes | **VERIFIED** | Win11 tokens and dark palette functional. Polish empty state glyph contrast in Phase 7. |
| **Network Folder Monitoring** | No (TODO in watcher) | Yes (Can add path) | No | **UNAVAILABLE** | Network polling is stubbed. Disclose limitation honestly in UI (`TASK-REM-36`). |
| **Scheduled Monitoring (Cron)** | Yes (`ScheduleManager`) | Yes (`ScheduleSettingsView`) | No | **UNINTEGRATED** | `ScheduleManager` never instantiated by composition root. Disclose in UI (`TASK-REM-36`). |
| **Rule Version Revisions** | Yes (`RuleRevisionManager`) | No (Orphan views) | No | **UNINTEGRATED** | Ordinary rule saves do not create revisions; no UI launcher. Integrate or mark planned. |
| **Duplicate File Detection** | Yes (`DuplicateDetector`) | No (No UI view) | No | **UNINTEGRATED** | Core engine exists, but no user workflow exists. Mark planned. |
