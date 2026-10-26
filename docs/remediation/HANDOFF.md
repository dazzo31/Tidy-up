# TidyUp v1.2.0 Remediation: Active Handoff Record

**Repository / Branch / Current SHA:** `TidyUp` on `main` @ `ae9ffd4`  
**Plan Revision & Location:** Revision 2 (`docs/REMEDIATION_TASK_CATALOG_v1.2.0.md`)  
**Last Verified Phase Gate:** Phase 0 Gate Passed; Phase 1 in progress (`TASK-REM-09` completed)  
**Active Task & Current State:** `TASK-REM-09` (`DONE`) -> Next: `TASK-REM-10` (Unified schema-aware rule serialization & import gate)  
**Completed Changes & Files:**
- `IRuleRepository.cs` & `RuleRepository.cs`: Added `UpdateExecutionOrdersAsync(IEnumerable<(Guid RuleId, int Order)> orderings)` executing within an atomic SQLite/EF Core database transaction.
- `MainWindowViewModel.cs`:
  - Added `CanReorderRules` property disabling drag-and-drop reordering when text searching (`SearchText`), status filtering (`SelectedFilter != RuleFilterType.All`), or non-priority sorting (`SelectedSortOption.Type != RuleSortType.ExecutionPriority`) is active.
  - Added `ReorderRulesAsync(Rule sourceRule, Rule targetRule)` updating in-memory orders, committing atomic batch reordering to SQLite via `IRuleRepository`, restoring previous order on transaction failure, and surfacing user-facing status messages.
- `MainWindow.xaml.cs`:
  - Wired `RuleListBox_PreviewMouseMove`, `RuleListBox_Drop`, and `RuleListBox_DragOver` to respect `ViewModel.CanReorderRules`.
  - Replaced unpersisted in-memory index shifting in `RuleListBox_Drop` with `await ViewModel.ReorderRulesAsync(sourceRule, targetRule)`.
- `MainWindowRulesListTests.cs`:
  - Added unit test `CanReorderRules_Gating_ReflectsFilterSearchAndSortingState` testing filter, search, and sort gating.
  - Added unit test `ReorderRulesAsync_WhenCanReorderRulesIsFalse_AbortsAndSurfacesMessage`.
  - Added integration test `ReorderRulesAsync_WhenValid_PersistsOrdersAtomicallyToRepository` verifying atomic database persistence against real SQLite.
  - Added unit test `ReorderRulesAsync_WhenTransactionFails_RestoresPreviousOrderAndSurfacesError` verifying in-memory rollback and error surfacing on transaction exception.
- `docs/remediation/TRACKER.md` & `FINDING_MATRIX.md`: Marked `TASK-REM-09` as `DONE` and `TU-30` as `FIXED_VERIFIED`.

**Last Executed Checks & Actual Results:**
- `dotnet test .\Tidy-up\TidyUp.sln -c Release`: Passed! 637 passed, 0 failed, 0 skipped (3.0s).
- Zero warnings, zero regressions across entire suite.

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
- Stage and commit `TASK-REM-09`.
- Lock `TASK-REM-10` in Phase 1 (Unified schema-aware rule serialization & import gate).
