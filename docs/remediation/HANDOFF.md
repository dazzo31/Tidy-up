# TidyUp v1.2.0 Remediation: Active Handoff Record

**Repository / Branch / Current SHA:** `TidyUp` on `main` @ `9cd74c5`  
**Plan Revision & Location:** Revision 2 (`docs/REMEDIATION_TASK_CATALOG_v1.2.0.md`)  
**Last Verified Phase Gate:** Phase 1 Gate Passed; Phase 2 in progress (`TASK-REM-16` completed)  
**Active Task & Current State:** `TASK-REM-16` (`DONE`) -> Next: `TASK-REM-17` (Responsive Background Enumeration & Partial Scan Disclosure)  
**Completed Changes & Files:**
- `PlannedFileAction.cs` & `FileAction.cs`:
  - Added `MonitoredSourceRoot` capturing the monitored root folder from which a file was discovered.
- `ActionEditorControl.xaml`:
  - Exposed `PreserveSubfolderStructure` checkbox and `ConflictResolution` dropdown in `CopyFileAction` template, aligning with `MoveFileAction`.
- `ExecutionPlanGenerator.cs`:
  - Added `GetSafeRelativeSubfolder` verifying directory confinement (case-insensitive prefix check, rejecting empty, rooted, or escaping `..` relative paths).
  - Populated `MonitoredSourceRoot` and nested target directory paths when `PreserveSubfolderStructure == true`.
- `IActionExecutor.cs` & `ActionExecutor.cs`:
  - Added `ExecutePlannedActionAsync` executing frozen planned target paths directly with destination confinement checks.
  - Added `PreserveSubfolderStructure` support in `ExecuteMoveAsync` and `ExecuteCopyAsync` when `MonitoredSourceRoot` is present.
- `ActionExecutorTests.cs` & `ExecutionPlanGeneratorTests.cs`:
  - Added unit tests for nested subfolder hierarchy preservation in Move/Copy, root file handling, flattening when disabled, exact planned execution, and destination confinement failure.
- `docs/remediation/TRACKER.md` & `FINDING_MATRIX.md`: Marked `TASK-REM-16` as `DONE` and `TU-31` as `FIXED_VERIFIED`.

**Last Executed Checks & Actual Results:**
- `dotnet test .\Tidy-up\TidyUp.sln -c Release`: Passed! 692 passed, 0 failed, 0 skipped (3.0s).
- Zero compilation errors across solution.

**Evidence Paths & Tested SHA:**
- SHA: `9cd74c5`
- Tracking: `docs/remediation/TRACKER.md`
- Finding Matrix: `docs/remediation/FINDING_MATRIX.md`

**Unresolved Blocker or Product Decision:** None.

**Temporary Containment Still Active:**
- `TU-17`: Prompt conflict resolution fails closed/skips safely.
- `TU-20`: Undo Delete displays guidance without marking record "RolledBack".
- `TU-32`: Empty directory cleanup will not climb above immediate parent without boundary path.
- `TU-38`: Undo button disabled on irreversible ExtractArchive and RunCommand rows.

**Exact Next Command or Implementation Action:**
- Commit doc updates.
- Lock `TASK-REM-17` in Phase 2 (Responsive Background Enumeration & Partial Scan Disclosure — `TU-25`).
