# TidyUp v1.2.0 Remediation: Active Handoff Record

**Repository / Branch / Current SHA:** `TidyUp` on `main` @ `c8165c1`  
**Plan Revision & Location:** Revision 2 (`docs/REMEDIATION_TASK_CATALOG_v1.2.0.md`)  
**Last Verified Phase Gate:** Phase 1 Gate Passed; Phase 2 in progress (`TASK-REM-18` completed)  
**Active Task & Current State:** `TASK-REM-18` (`DONE`) -> Next: `TASK-REM-27` (Scan Command Semantic Harmonization)  
**Completed Changes & Files:**
- `PreviewViewModel.cs`:
  - Added `ClearPlan()` method that safely cancels active simulation CTS, sets `ExecutionPlan = null`, clears `AllItems`, and resets state.
  - Added `RefreshPreviewCommand` allowing users to re-run simulation directly from the Preview workspace without returning to Rules.
  - Added `PlanHeaderInfo` exposing rule identity, generation timestamp, and action count for the active plan.
- `MainWindowViewModel.cs`:
  - Wired `PreviewViewModel.ClearPlan()` on `SelectedRule` setter change, `SaveDraftAsync` success, and `DiscardActiveEdits`.
  - Updated `TestRuleAsync` to clear stale plans before simulating the current active rule draft.
- `PreviewView.xaml`:
  - Bound `PageHeader.Subtitle` to `PlanHeaderInfo`.
  - Added "Refresh Preview" button to header breakdown toolbar.
- `PreviewViewModelTests.cs` & `MainWindowRulesListTests.cs`:
  - Added unit tests for `ClearPlan()`, `RefreshPreviewCommand`, `PlanHeaderInfo`, and plan invalidation on rule selection changes.
- `docs/remediation/TRACKER.md` & `FINDING_MATRIX.md`: Marked `TASK-REM-18` as `DONE` and `TU-12` as `FIXED_VERIFIED`.

**Last Executed Checks & Actual Results:**
- `dotnet test .\Tidy-up\TidyUp.sln -c Release`: Passed! 700 passed, 0 failed, 0 skipped (3.0s).
- Zero compilation errors or styling guard warnings across solution.

**Evidence Paths & Tested SHA:**
- SHA: `c8165c1`
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
- Lock `TASK-REM-27` in Phase 2 (Scan Command Semantic Harmonization — `TU-04`).
