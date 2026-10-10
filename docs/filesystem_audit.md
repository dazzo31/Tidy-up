# TidyUp: Filesystem Engine, Watcher & Persistence Layer Technical Audit

**Task Reference:** `TASK-BL-03`  
**Audit Date:** 2026-10-09  
**Target Solution:** `TidyUp.sln`  
**Scope:** Core filesystem services, FileSystemWatcher implementation, SQLite database context, repositories, and path validation utilities.

---

## 1. Executive Summary of Audit Findings

The audit confirms severe safety, concurrency, and reliability edge cases across file execution, monitoring, and database layers:

1. **Catastrophic Pre-Move Overwrite Deletion (`ActionExecutor.cs`):** Before moving or renaming a file with `ConflictResolution.Overwrite`, the engine calls `File.Delete(fullDestPath)` *prior* to attempting `File.Move()`. If the move fails (due to source file locks, disk quota, or permissions), the destination file is **permanently destroyed and unrecoverable**.
2. **Unbounded Recursive Parent Folder Deletion (`ActionExecutor.cs`):** `RemoveEmptyFoldersAsync` recursively deletes empty parent directories without stopping at the monitored root folder, risking accidental deletion of user folders (e.g., `Downloads` or user home).
3. **Inadequate File Lock Probing (`FileMonitorService.cs`):** The lock check uses `FileShare.ReadWrite` rather than exclusive access (`FileShare.None`), falsely identifying in-progress browser downloads as "ready".
4. **Watcher Event Dropping & Buffer Overflow (`FileMonitorService.cs`):** `FileSystemWatcher` uses the default 8KB buffer and does not subscribe to the `Error` event, dropping burst events without notice or reconciliation.
5. **Dead Validation & Loop Hazards (`PathValidator.cs`):** `PathValidator` is never invoked anywhere in the codebase, and no detection exists for self-triggering directory nesting (source containing destination).
6. **Concurrency & Thread Safety Risks (`TidyUpDbContext.cs` / `ServiceConfiguration.cs`):** Repositories inject raw `TidyUpDbContext` rather than using `IDbContextFactory`, creating high risk of concurrency exceptions when background watcher threads access the database.

---

## 2. Line-by-Line Vulnerability Inventory

### A. Core File Operation Engine (`TidyUp/Services/ActionExecutor.cs`)

| File & Lines | Category | Technical Defect & Vulnerability | Severity |
| :--- | :--- | :--- | :--- |
| `ActionExecutor.cs`<br>Lines 85-87, 187-189, 212-214 | **Data Loss** | **Pre-Move Deletion Hazard:** In `ExecuteMoveAsync`, `ExecuteRenameAsync`, and `ExecuteChangeExtensionAsync`: If `ConflictResolution.Overwrite` is selected, `File.Delete(destinationPath)` is executed *before* `File.Move()`. If the subsequent move fails, the pre-existing destination file is permanently destroyed. | **P0 (Critical)** |
| `ActionExecutor.cs`<br>Lines 402-425 | **Data Loss** | **Unbounded Directory Recursion:** `RemoveEmptyFoldersAsync(DirectoryInfo? directory)` executes `await RemoveEmptyFoldersAsync(directory.Parent)` with no boundary check. It will climb up the folder hierarchy and delete empty parent folders above the monitored root. | **P0 (Critical)** |
| `ActionExecutor.cs`<br>Lines 233-248 | **Safety** | **Permanent Deletion Without Fallback Guard:** If `action.UseRecycleBin` is false, `File.Delete` permanently erases files without a journal record. If `UseRecycleBin` is true on network shares or FAT32 drives, the Shell API either shows an interactive prompt or throws without fallback handling. | **P0 (Critical)** |
| `ActionExecutor.cs`<br>Lines 298-340 | **Security** | **Unsanitized Command Execution:** `RunCommandAction` executes arbitrary shell commands with variable interpolation without strict input sanitization or process isolation. | **P1 (High)** |
| `ActionExecutor.cs`<br>Lines 427-446 | **Reliability** | **Naive Retry Logic:** `RetryAsync` uses fixed short delays (100ms, 200ms, 500ms). While helpful for transient locks, it is insufficient for multi-second file downloads. | **P2 (Medium)** |

---

### B. File Watcher & Monitoring Service (`TidyUp/Services/FileMonitorService.cs`)

| File & Lines | Category | Technical Defect & Vulnerability | Severity |
| :--- | :--- | :--- | :--- |
| `FileMonitorService.cs`<br>Lines 116-121 | **Data Integrity** | **Default Buffer Size & Missing Error Handler:** `FileSystemWatcher` `InternalBufferSize` is not set (defaults to 8KB). The service does not listen to `watcher.Error`. On burst operations (extracting a zip or batch downloads), buffer overflows cause silent event loss. | **P0 (Critical)** |
| `FileMonitorService.cs`<br>Lines 198-208 | **Race Condition** | **Ineffective File Lock Detection:** Probes file readiness using `FileShare.ReadWrite`. This succeeds even while another application (e.g. Chrome, Office) is actively writing to the file. | **P1 (High)** |
| `FileMonitorService.cs`<br>Line 206 | **Reliability** | **Silent Drop on Lock Contention:** If `IOException` is caught during initial lock probe, the method simply returns with no deferred retry queue. The file is never processed. | **P1 (High)** |
| `FileMonitorService.cs`<br>Lines 109-114 | **Incomplete Code** | **Network Path Monitoring Stub:** If `IsNetworkPath(folder.Path)` returns true, it hits `// TODO: Implement periodic polling` and returns `Task.CompletedTask`, silently failing to monitor network shares. | **P1 (High)** |

---

### C. Validation & Cycle Detection (`TidyUp/Utilities/PathValidator.cs`)

| File & Lines | Category | Technical Defect & Vulnerability | Severity |
| :--- | :--- | :--- | :--- |
| `PathValidator.cs`<br>Lines 8-80 | **Architecture** | **Dead Code:** `PathValidator.Validate()` is defined with reserved Windows names (CON, PRN, etc.) and traversal checks, but is **never called anywhere** in the solution. | **P1 (High)** |
| Solution-wide | **Infinite Loop** | **Missing Cycle & Nesting Detection:** No validation exists to prevent a rule from monitoring folder `A` and moving files to subfolder `A\Sorted`, creating an infinite recursive processing loop. | **P0 (Critical)** |
| Solution-wide | **Path Limits** | **No Long Path (`\\?\`) Normalization:** Paths exceeding 260 characters generate warnings in `PathValidator` but are never normalized before passing to `File.Move` or `File.Copy`. | **P2 (Medium)** |

---

### D. Database, Concurrency & Persistence (`TidyUp/Data/`)

| File & Lines | Category | Technical Defect & Vulnerability | Severity |
| :--- | :--- | :--- | :--- |
| `ServiceConfiguration.cs`<br>Lines 49-53 | **Concurrency** | **Thread Safety Risk with Scoped DbContext:** `RuleRepository` and `ActionLogRepository` inject raw `TidyUpDbContext`. If multiple background threads attempt database access simultaneously, EF Core throws concurrency violations. | **P1 (High)** |
| `ServiceConfiguration.cs`<br>Line 78 | **Schema Migration** | **Lack of EF Core Migrations:** `EnsureDatabaseCreatedAsync()` is used instead of migrations. Schema modifications in subsequent phases will not migrate existing user databases safely. | **P1 (High)** |
| `Data/Entities/`<br>`ActionLogEntity.cs` | **Safety / Recovery** | **Missing Rollback Metadata:** The entity records `FilePath` and `ResultPath`, but lacks `BatchId`, `PreActionHash`, `PostActionHash`, and `RollbackStatus`, making automated safe rollback impossible. | **P0 (Critical)** |

---

## 3. Prioritized Mitigation Requirements for Phase 1

1. **`TASK-SAFE-01` (Dry-Run Simulation):** Must generate read-only plans with conflict reporting without deleting or moving any file.
2. **`TASK-SAFE-02` (Shell Recycle Bin):** Replace raw `File.Delete` in `ActionExecutor` with safe Shell API deletion and fallback detection.
3. **`TASK-SAFE-03` (Lock Detection & Retry Queue):** Replace `FileShare.ReadWrite` with exclusive `FileShare.None` probe and introduce a non-blocking retry queue.
4. **`TASK-SAFE-04` (Journal & Conflict-Safe Rollback):** Upgrade SQLite schema with `BatchId` and hashes; implement atomic rollback. Fix the pre-move delete hazard by using modern `File.Move(..., overwrite: true)` and rollback snapshots.
5. **`TASK-SAFE-05` (Loop & Cycle Detector):** Activate `PathValidator`, bound `RemoveEmptyFoldersAsync` to monitored root, and block nested source/destination configurations.
6. **`TASK-SAFE-06` (Hardened Watcher):** Expand buffer to 64KB, handle `watcher.Error`, and trigger directory reconciliation scans.

