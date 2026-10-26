# TidyUp v1.2.0 Remediation: Active Handoff Record

**Repository / Branch / Current SHA:** `TidyUp` on `main` @ 3d6b720  
**Plan Revision & Location:** Revision 2 (`docs/REMEDIATION_TASK_CATALOG_v1.2.0.md`)  
**Last Verified Phase Gate:** Phase 0 Gate Passed; Phase 1 in progress (`TASK-REM-34` completed)  
**Active Task & Current State:** `TASK-REM-34` (`DONE`) -> Next: `TASK-REM-28` (Typed AppSettings DI registration & service config)  
**Completed Changes & Files:**
- `SettingsRepository.cs`:
  - Implemented `SettingsLoadResult` encapsulating load outcomes: `Settings`, `IsCorrupt`, `RecoveredFromBackup`, `CorruptBackupPath`, `BackupPathUsed`, and `ErrorMessage`.
  - Added atomic temporary file persistence: `SaveAsync` writes validated JSON to uniquely named `settings.<guid>.tmp` with `FileOptions.WriteThrough | FileOptions.Asynchronous` and flushes before committing.
  - Implemented atomic file swap using Win32 `File.Replace(tempPath, SettingsFilePath, SettingsBackupPath, ignoreMetadataErrors: true)`, guaranteeing current settings are preserved in `.bak` without data loss or partial writes. On first save (when destination doesn't exist), falls back to `File.Move` + `File.Copy` backup.
  - Synchronized concurrent saves using `SemaphoreSlim(1, 1)`.
  - Added cleanup for stray temporary files (`settings.*.tmp`) before writes and in finally blocks.
  - In `LoadWithResultAsync`: preserves corrupted files byte-for-byte to `settings.json.corrupt.<timestamp>` before attempting recovery from known-good backup (`settings.json.bak`). If backup is invalid or missing, falls back to default settings with `IsCorrupt = true`.
  - Added semantic range normalization on load (e.g. `ScanBatchSize <= 0` resets to safe default `100`).
- `App.xaml.cs`:
  - Evaluates `settingsRepo.LastLoadResult` after loading settings on application startup.
  - Displays structured user warning dialogs if settings were corrupted and recovered from backup, or if defaults were loaded, informing the user of the corrupt backup file location.
- `MainWindowRulesListTests.cs`:
  - Resolved EF Core parallel test context collision in `ReorderRulesAsync_WhenValid_PersistsOrdersAtomicallyToRepository` by using atomic `repo.AddRangeAsync(new[] { r1, r2 })`.
- `SettingsPersistenceTests.cs`:
  - Added 7 unit tests: first save creates file and backup, subsequent save atomically updates and preserves previous in backup, stray temp file cleanup, concurrent saves synchronization, corrupt file timestamped byte-for-byte preservation, corrupt file recovery from backup, and semantic range normalization. All 20 persistence tests pass.
- `docs/remediation/TRACKER.md` & `FINDING_MATRIX.md`: Marked `TASK-REM-34` as `DONE` and `TU-42` as `FIXED_VERIFIED`.

**Last Executed Checks & Actual Results:**
- `dotnet test .\Tidy-up\TidyUp.sln -c Release`: Passed! 661 passed, 0 failed, 0 skipped (3.0s).
- Zero compilation errors across solution.

**Evidence Paths & Tested SHA:**
- SHA: working tree
- Tracking: `docs/remediation/TRACKER.md`
- Finding Matrix: `docs/remediation/FINDING_MATRIX.md`

**Unresolved Blocker or Product Decision:** None.

**Temporary Containment Still Active:**
- `TU-17`: Prompt conflict resolution fails closed/skips safely.
- `TU-20`: Undo Delete displays guidance without marking record "RolledBack".
- `TU-32`: Empty directory cleanup will not climb above immediate parent without boundary path.
- `TU-38`: Undo button disabled on irreversible ExtractArchive and RunCommand rows.

**Exact Next Command or Implementation Action:**
- Stage and commit `TASK-REM-34`.
- Lock `TASK-REM-28` in Phase 1 (Typed AppSettings DI registration & service config).
