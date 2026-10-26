# TidyUp v1.2.0 Remediation: Feature Integration Matrix

**Baseline Tag:** `v1.2.0`  
**Current Commit:** `59d3bb45d7ca622ec3a80a7c2b3b0ff21e89358c`  
**Target Task:** `TASK-REM-36`

---

## Component vs. Reachable Feature Truth Table

| Capability | Component Present in Code | Reachable in Live UI | Active at Startup / Background | Verification Status on `59d3bb4` | Remediation Plan |
| :--- | :---: | :---: | :---: | :---: | :--- |
| **Read-Only Rule Dry-Run** | Yes (`ExecutionPlanGenerator`) | Yes (`PreviewView`) | N/A | **VERIFIED** | Fixed in Phase 2 (`TASK-REM-12`, `14`, `18`). |
| **Manual Batch Apply** | Yes (`BatchProcessingCoordinator`) | Yes (`PreviewView`) | N/A | **VERIFIED** | Fixed in Phase 3 & 4 (`TASK-REM-19`, `20`, `21`). |
| **Automatic File Watcher** | Yes (`FileMonitorService`) | Yes (Toggle in UI) | **YES** | **VERIFIED** | Wired into startup and `MonitoringCoordinator` (`TASK-REM-24`). |
| **Recycle Bin Safe Delete** | Yes (`WindowsShellFileOperations`) | Yes (Action Editor) | N/A | **VERIFIED** | Decoupled scoped authorization context (`TASK-REM-03`). |
| **Multi-Action Chaining** | Yes (`ActionExecutor`) | Yes (Action Editor) | N/A | **VERIFIED** | Intermediate virtual path and dependency tracking fixed (`TASK-REM-14`). |
| **Operation Rollback (Undo)** | Yes (`RollbackEngine`) | Yes (`HistoryView`) | N/A | **VERIFIED** | Truthful `RollbackOutcome` implemented (`TASK-REM-01`). |
| **SQLite Migration & Backup** | Yes (`DatabaseMigrator`) | Automatic | Yes | **VERIFIED** | Genuine VACUUM and backup management verified (`TASK-REM-06`). |
| **Theme Switching (Light/Dark)** | Yes (`ThemeService`) | Yes (`SettingsView`) | Yes | **VERIFIED** | Win11 tokens and dark palette functional. |
| **Network Folder Monitoring** | No (TODO in watcher) | Yes (Can add path) | No | **DISCLOSED** | Disclosed as `[Integration Pending]` with badge in FoldersView and Settings (`TASK-REM-36`). |
| **Scheduled Monitoring (Cron)** | Yes (`ScheduleManager`) | Yes (`ScheduleSettingsView`) | No | **DISCLOSED** | Disclosed as `[Integration Pending]` badge in `ScheduleSettingsView` (`TASK-REM-36`). |
| **Rule Version Revisions** | Yes (`RuleRevisionManager`) | Yes (`RuleRevisionDiffView`) | No | **DISCLOSED** | Disclosed as `[Integration Pending]` badge in `RuleRevisionDiffView` (`TASK-REM-36`). |
| **Duplicate File Detection** | Yes (`DuplicateDetector`) | No (No UI view) | No | **DISCLOSED** | Underlying engine tested; marked integration pending in feature matrix (`TASK-REM-36`). |
