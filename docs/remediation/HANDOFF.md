# TidyUp v1.2.0 Remediation: Active Handoff Record

**Repository / Branch / Current SHA:** `TidyUp` on `main` @ `f25833b`  
**Plan Revision & Location:** Revision 2 (`docs/REMEDIATION_TASK_CATALOG_v1.2.0.md`)  
**Last Verified Phase Gate:** Phase 0 Gate Passed; Phase 1 in progress (`TASK-REM-11` completed)  
**Active Task & Current State:** `TASK-REM-11` (`DONE`) -> Next: `TASK-REM-34` (Atomic settings persistence & corrupt profile recovery)  
**Completed Changes & Files:**
- `IVariableEngine.cs` & `VariableEngine.cs`:
  - Implemented unified token registry supporting case-insensitive matching for all documented variables and aliases (`ext`, `fileextension`, `fullname`, `filenamewithextension`, `date`, `time`, `datetime`, `year`, `month`, `monthname`, `day`, `hour`, `minute`, `second`, `creationdate`, `creationyear`, `creationtime`, `modifieddate`, `modifiedyear`, `modifiedtime`, `folderpath`, `foldername`, `counter`, etc.).
  - Sanitized all time variables (`now_time`, `created_time`, `modified_time`, `time`, `datetime`) to format with hyphens (`HH-mm-ss`) instead of Windows-illegal colons (`HH:mm:ss`), automatically replacing colons in custom user formats as well.
  - Added `IsKnownVariable(string token)` and `GetUnknownVariables(string template)` methods to `IVariableEngine`.
- `FirstRunViewModel.cs`: Aligned sample rule template pattern with canonical supported tokens (`{extension}/{now_year}-{now_month}`).
- `HelpView.xaml`: Documented `{Counter}` sequence variable and confirmed all advertised variables resolve with legal filename syntax.
- `RuleValidationResult.cs`: Added `RuleValidationErrorCode.UnknownVariableToken`.
- `RuleValidator.cs`: Added validation for template strings in `MoveFileAction`, `CopyFileAction`, and `RenameFileAction`, blocking execution and generating field-level errors if unknown variable tokens are present.
- `VariableEngineTests.cs`:
  - Verified time tokens never produce colons.
  - Verified custom formats with colons sanitize to hyphens.
  - Verified case-insensitive resolution for all documented variables and aliases.
  - Verified `IsKnownVariable` and `GetUnknownVariables`.
  - Verified `RuleValidator` catches unknown variable tokens.
  - Verified `FirstRunViewModel` sample rule resolves cleanly without unexpanded literal braces.
- `docs/remediation/TRACKER.md` & `FINDING_MATRIX.md`: Marked `TASK-REM-11` as `DONE` and `TU-39`, `TU-40` as `FIXED_VERIFIED`.

**Last Executed Checks & Actual Results:**
- `dotnet test .\Tidy-up\TidyUp.sln -c Release`: Passed! 654 passed, 0 failed, 0 skipped (3.0s).
- Zero errors across entire solution.

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
- Stage and commit `TASK-REM-11`.
- Lock `TASK-REM-34` in Phase 1 (Atomic settings persistence & corrupt profile recovery).
