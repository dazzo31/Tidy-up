# TidyUp v1.2.0 Remediation: Active Handoff Record

**Repository / Branch / Current SHA:** `TidyUp` on `main` @ `87ca750`  
**Plan Revision & Location:** Revision 2 (`docs/REMEDIATION_TASK_CATALOG_v1.2.0.md`)  
**Last Verified Phase Gate:** Phase 2 Gate Passed (Tasks 12, 13, 14, 15, 16, 17, 18, 27 Complete)  
**Active Task & Current State:** `TASK-REM-27` (`DONE`) -> Next: Phase 3 Gate / `TASK-REM-22` (Two-Phase Durable Journaling & Rule Metadata Tracking)  
**Completed Changes & Files:**
- `FoldersViewModel.cs`:
  - Added `_isScanning` observable property with `CanScan` / `ScanTooltip` notification dispatch to `MonitoredFolderItemViewModel`.
  - Injected `IWatcherReconciler? watcherReconciler = null` into `FoldersViewModel`.
  - Updated `ScanFolderNowAsync` to set `folder.IsScanning = true`, delegate to `_watcherReconciler.ReconcileFolderAsync`, enumerate matching files on disk, report count via `StatusMessage`, and safely reset `IsScanning = false` in `finally`.
- `MainWindowViewModel.cs`:
  - Injected `IWatcherReconciler` into fallback instantiation of `FoldersViewModel`.
- `FoldersView.xaml`:
  - Added indeterminate circular `ProgressBar` bound to `IsScanning` with `BooleanToVisibilityConverter`.
- `FoldersViewModelTests.cs` & `FolderScanAvailabilityTests.cs`:
  - Added unit tests for watcher reconciler delegation, unreadable folder scan prevention, and `IsScanning` / `CanScan` property notifications.
- `docs/remediation/TRACKER.md` & `FINDING_MATRIX.md`: Marked `TASK-REM-27` as `DONE` and `TU-04` as `FIXED_VERIFIED`.

**Last Executed Checks & Actual Results:**
- `dotnet test .\Tidy-up\TidyUp.sln -c Release`: Passed! 703 passed, 0 failed, 0 skipped (3.0s).
- Zero compilation errors or styling guard warnings across solution.

**Evidence Paths & Tested SHA:**
- SHA: `87ca750`
- Tracking: `docs/remediation/TRACKER.md`
- Finding Matrix: `docs/remediation/FINDING_MATRIX.md`

**Unresolved Blocker or Product Decision:** None.

**Temporary Containment Still Active:**
- `TU-17`: Prompt conflict resolution fails closed/skips safely.
- `TU-20`: Undo Delete displays guidance without marking record "RolledBack".
- `TU-32`: Empty directory cleanup will not climb above immediate parent without boundary path.
- `TU-38`: Undo button disabled on irreversible ExtractArchive and RunCommand rows.

**Exact Next Command or Implementation Action:**
- Phase 2 complete. Await user review/confirmation to proceed to Phase 3 (`TASK-REM-22`: Two-Phase Durable Journaling & Rule Metadata Tracking).
