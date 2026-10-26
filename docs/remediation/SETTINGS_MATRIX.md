# TidyUp v1.2.0 Remediation: Settings Control-to-Runtime Matrix

**Baseline Tag:** `v1.2.0`  
**Current Commit:** `59d3bb45d7ca622ec3a80a7c2b3b0ff21e89358c`  
**Target Task:** `TASK-REM-35`

---

## Settings Audit Table

| Setting Key | Type | Default | UI Section | Runtime Consumer | Current Status on `59d3bb4` | Remediation Action (`TASK-REM-35`) |
| :--- | :---: | :---: | :--- | :--- | :---: | :--- |
| `Theme` | string | `"System"` | General | `ThemeService.Attach()` | **WIRED** | Retain; theme switching functional. |
| `MinimizeToTray` | bool | `true` | General | `MainWindow_Closing` in `App.xaml.cs` | **WIRED** | Retain; fix closing race condition (`TASK-REM-29`). |
| `StartWithWindows` | bool | `false` | General | Windows Registry Run key | **WIRED** | Retain. |
| `SystemTrayEnabled` | bool | `true` | General | `App.OnStartup` | **WIRED** | Startup respects setting (`TASK-REM-28`). |
| `NotificationsEnabled` | bool | `true` | General | `ToastNotificationService` | **WIRED** | Injected in DI container (`TASK-REM-28`). |
| `ConfirmBeforeDelete` | bool | `true` | Safety | `ActionExecutor` | **WIRED** | Decoupled authorization context (`TASK-REM-03`). |
| `DefaultConflictResolution` | enum | `RenameNew` | Safety | Action creation / Editor | **WIRED** | Injected in `ActionEditorViewModel` (`TASK-REM-35`). |
| `RequireDestructiveConfirmations` | bool | `true` | Safety | `PreviewViewModel` | **WIRED** | Gates Safeguard modal on batch apply (`TASK-REM-35`). |
| `UseRecycleBinByDefault` | bool | `true` | Safety | Action creation | **WIRED** | Injected in `ActionEditorViewModel` (`TASK-REM-35`). |
| `MaxConcurrentOperations` | int | `1` | Performance | `BatchProcessingCoordinator` | **CONTAINED** | Execution is serial (1) per `ADR-01`; UI control disabled and marked read-only with honest notice (`TASK-REM-35`). |
| `ScanBatchSize` | int | `500` | Performance | Directory Scanner | **WIRED** | Injected in `ExecutionPlanGenerator` (`TASK-REM-35`). |
| `DebounceDelayMs` | int | `500` | Performance | `FileMonitorService` | **WIRED** | Injected into watcher timer (`TASK-REM-35`). |
| `NetworkFolderPollingIntervalSeconds` | int | `30` | Performance | None | **CONTAINED** | Network polling marked `[Integration Pending]`, disabled in UI (`TASK-REM-35`). |
| `JournalRetentionDays` | int | `30` | Storage | `DatabaseIntegrityChecker` | **WIRED** | Prunes old journal entries. Retain. |
| `DatabaseBackupPath` | string | `""` | Storage | `DatabaseBackupManager` | **WIRED** | Made editable with Browse dialog; writes to custom path when specified (`TASK-REM-35`). |
| `DebugLoggingEnabled` | bool | `false` | Advanced | Logger / Telemetry | **WIRED** | Retained with diagnostic telemetry notice (`TASK-REM-35`). |
| `AutoCleanupLogs` | bool | `true` | Advanced | Logger / Retention | **WIRED** | Prunes expired action logs on application startup (`TASK-REM-35`). |
