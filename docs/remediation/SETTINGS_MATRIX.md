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
| `SystemTrayEnabled` | bool | `true` | General | `App.OnStartup` | **DISCONNECTED** | Tray is initialized regardless of this setting. Wire in `TASK-REM-28`. |
| `NotificationsEnabled` | bool | `true` | General | `ToastNotificationService` | **DISCONNECTED** | `AppSettings` not in DI; service receives null. Wire in `TASK-REM-28`. |
| `ConfirmBeforeDelete` | bool | `true` | Safety | `ActionExecutor` | **CONFLICTED** | Blocked confirmed deletions. Decouple authorization in `TASK-REM-03`. |
| `DefaultConflictResolution` | enum | `Skip` | Safety | Action creation / Editor | **DISCONNECTED** | Action creation hard-codes `AutoRename`. Wire default in `TASK-REM-35`. |
| `RequireDestructiveConfirmations` | bool | `true` | Safety | `SafeguardConfirmationViewModel` | **DISCONNECTED** | Zero runtime consumers. Wire in `TASK-REM-35`. |
| `UseRecycleBinByDefault` | bool | `true` | Safety | Action creation | **DISCONNECTED** | Action creation hard-codes true. Wire as default in `TASK-REM-35`. |
| `MaxConcurrentOperations` | int | `4` | Performance | `BatchProcessingCoordinator` | **INERT** | Execution is serial. Lock to serial (1) and explain limit (`ADR-01`). |
| `ScanBatchSize` | int | `1000` | Performance | Directory Scanner | **INERT** | Directory.GetFiles ignores batch size. Wire into scanner (`TASK-REM-17`). |
| `DebounceDelayMs` | int | `500` | Performance | `FileMonitorService` | **INERT** | Debounce delay hard-coded. Wire into watcher in `TASK-REM-35`. |
| `NetworkFolderPollingIntervalSeconds` | int | `30` | Performance | None | **INERT** | Network polling is unimplemented TODO. Mark unavailable in UI (`TASK-REM-36`). |
| `JournalRetentionDays` | int | `30` | Storage | `DatabaseIntegrityChecker` | **WIRED** | Prunes old journal entries. Retain. |
| `DatabaseBackupPath` | string | `""` | Storage | `DatabaseBackupManager` | **DISCONNECTED** | Manager uses hard-coded `TidyUpPaths`. Wire custom path in `TASK-REM-35`. |
| `DebugLoggingEnabled` | bool | `false` | Advanced | Logger | **INERT** | Zero runtime consumers. Wire into Serilog/diagnostics in `TASK-REM-35`. |
| `AutoCleanupLogs` | bool | `true` | Advanced | Logger | **INERT** | Zero runtime consumers. Wire into log retention in `TASK-REM-35`. |
