# TidyUp v1.2.0 Remediation: Active Handoff Record

**Repository / Branch / Current SHA:** `TidyUp` on `main` @ `59d3bb45d7ca622ec3a80a7c2b3b0ff21e89358c`  
**Plan Revision & Location:** Revision 2 (`docs/REMEDIATION_TASK_CATALOG_v1.2.0.md`)  
**Last Verified Phase Gate:** Phase 0 (Baseline, Contracts & Containment) — PASSED  
**Active Task & Current State:** `TASK-BASE-03` (`DONE`) -> Next: Phase 1 (`TASK-REM-08`: Deep-cloned rule draft engine & shared save/discard)  
**Completed Changes & Uncommitted Files:**
- `HistoryViewModel.cs`: Disabled `CanRollback` for `ExtractArchive` and `RunCommand`; prevented false "RolledBack" marking for Recycle Bin delete.
- `ActionExecutor.cs`: Failed closed on `ConflictResolution.Prompt` (skip safely with warning); clamped `RemoveEmptyFoldersAsync` to only recurse when explicit `stopBoundaryPath` provided.
- `docs/remediation/TRACKER.md`: Marked `TASK-BASE-03` as `DONE` (Phase 0 complete).
- `docs/remediation/FINDING_MATRIX.md`: Marked `TU-17`, `TU-20`, `TU-32`, and `TU-38` as `CONTAINED`.

**Last Executed Checks & Actual Results:**
- `dotnet test .\Tidy-up\TidyUp.sln -c Release --no-restore`: Passed! 627 passed, 0 failed, 0 skipped (3.0s).
- Zero regressions across existing suite.

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
- Stage and commit `TASK-BASE-03`.
- Lock `TASK-REM-08` in Phase 1 (Deep-cloned rule draft engine & shared save/discard contract).

**Do Not Repeat / Do Not Assume:**
- Do not repeat the baseline test execution; it is recorded at 623 tests green.
- Do not assume `v1.2.0` has working automatic file watching (finding `TU-01` is confirmed unwired).
- Do not skip `TASK-BASE-02` contracts or `TASK-BASE-03` containment guards before editing Phase 1 consumers.
