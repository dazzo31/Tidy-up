# TidyUp v1.2.0 Remediation: Active Handoff Record

**Repository / Branch / Current SHA:** `TidyUp` on `main` @ `55d01b2`  
**Plan Revision & Location:** Revision 2 (`docs/REMEDIATION_TASK_CATALOG_v1.2.0.md`)  
**Last Verified Phase Gate:** Phase 0 Gate Passed; Phase 1 in progress (`TASK-REM-10` completed)  
**Active Task & Current State:** `TASK-REM-10` (`DONE`) -> Next: `TASK-REM-11` (Variable engine normalization & filename sanitization)  
**Completed Changes & Files:**
- `IRuleRepository.cs` & `RuleRepository.cs`: Added `AddRangeAsync(IEnumerable<Rule> rules)` performing atomic batch persistence within an EF Core SQLite database transaction, rolling back on failure.
- `RuleSerializationService.cs`:
  - Extended schema validation to verify package root object, version property presence, supported version 1.0 check (rejecting future versions with clear error message), non-empty rules array, rule name presence, monitored folder path presence, and valid action parameters (e.g. non-empty destination paths on move/copy).
  - Enforced strict invariant: `rule.IsEnabled = false` on every imported rule.
  - Assigned fresh unique `Guid.NewGuid()` to each imported rule to eliminate collisions.
  - Disambiguated duplicate rule names within an imported package.
  - Reset runtime stats and timestamps (`CreatedDate`, `ModifiedDate`, `LastRunDate = null`, `FilesProcessedCount = 0`).
- `IImportExportService.cs` & `ImportExportService.cs`: Marked `[Obsolete("Use IRuleSerializationService instead.")]` and transitioned `ImportExportService` to an adapter delegating to `RuleSerializationService`.
- `MainWindowViewModel.cs`:
  - Injected `IRuleSerializationService` (with backward-compatible fallback).
  - Consolidated `ExportRuleAsync` and `ExportAllRulesAsync` on `RuleSerializationService.ExportRules`.
  - Added `ImportRulesFromJsonAsync` validating schema first, enforcing disabled state, prompting for user confirmation via `ConfirmImportCallback` / `MessageBox`, assigning sequential execution priorities, and committing the batch atomically via `_ruleRepository.AddRangeAsync`.
- `SerializationTests.cs`: Added tests for legacy package migration, future version rejection (2.0 and 9.9), missing version rejection, malformed action parameter rejection, duplicate ID re-assignment, duplicate name disambiguation, and round-trip field fidelity.
- `ImportExportServiceTests.cs`: Updated round-trip test to assert `importedRule.IsEnabled.Should().BeFalse()`.
- `MainWindowRulesListTests.cs`: Added integration and unit tests for `ImportRulesFromJsonAsync` covering atomic persistence against real SQLite, cancellation gating, validation failure abort, and persistence exception rollback.
- `docs/remediation/TRACKER.md` & `FINDING_MATRIX.md`: Marked `TASK-REM-10` as `DONE` and `TU-29` as `FIXED_VERIFIED`.

**Last Executed Checks & Actual Results:**
- `dotnet test .\Tidy-up\TidyUp.sln -c Release`: Passed! 647 passed, 0 failed, 0 skipped (3.0s).
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
- Stage and commit `TASK-REM-10`.
- Lock `TASK-REM-11` in Phase 1 (Variable engine normalization & filename sanitization).
