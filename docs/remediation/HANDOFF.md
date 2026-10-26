# TidyUp v1.2.0 Remediation: Active Handoff Record

**Repository / Branch / Current SHA:** `TidyUp` on `main` @ `ac747af`  
**Plan Revision & Location:** Revision 2 (`docs/REMEDIATION_TASK_CATALOG_v1.2.0.md`)  
**Last Verified Phase Gate:** Phase 0 Gate Passed; Phase 1 in progress (`TASK-REM-08` completed)  
**Active Task & Current State:** `TASK-REM-08` (`DONE`) -> Next: `TASK-REM-09` (Rule list drag-and-drop order persistence)  
**Completed Changes & Files:**
- `RuleDraft.cs`: Extended with `IsNew` tracking, deep-cloned JSON equality comparison excluding runtime execution statistics (`FilesProcessedCount`, `LastRunDate`).
- `Rule.cs`: Added `DeepClone()` convenience helper.
- `RuleWizardViewModel.cs`: Preserved all metadata (`ExecutionOrder`, `StopProcessingAfterMatch`, `TriggerType`, `ScheduledTime`, `CreatedDate`, `LastRunDate`, `FilesProcessedCount`) across `LoadFromRule` and `BuildRule`; added `SaveHandler` and `CancelHandler` integration delegates.
- `RuleWizardView.xaml.cs`: Subscribed `RequestClose` to close parent dialog windows cleanly.
- `MainWindowViewModel.cs`: Implemented unified `SaveDraftAsync()` shared by Guided and Shell Save; ensured dirty flags are cleared ONLY upon successful database persistence; retained dirty state on failed save; implemented `RestoreRuleFromBaseline()` to roll back in-memory edits to database snapshot on Discard; implemented `HandleWizardCancel()` to cleanly reset new rule drafts without orphaned state; eliminated false dirty triggers via `_isSynchronizing`.
- `MainWindowDirtyStateTests.cs`: Added regression tests for saving rule, retaining dirty state on failed save, and snapshot restoration on discard.
- `RuleWizardModeSynchronizationTests.cs`: Added regression tests proving wizard metadata preservation, wizard cancellation delegation, new-rule cancel cleanup, and nested condition/action discard restoration.
- `docs/remediation/TRACKER.md` & `FINDING_MATRIX.md`: Marked `TASK-REM-08` as `DONE` and `TU-08`, `TU-09`, `TU-10`, `TU-11` as `FIXED_VERIFIED`.

**Last Executed Checks & Actual Results:**
- `dotnet test .\Tidy-up\TidyUp.sln -c Release`: Passed! 633 passed, 0 failed, 0 skipped (3.0s).
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
- Stage and commit `TASK-REM-08`.
- Lock `TASK-REM-09` in Phase 1 (Rule list drag-and-drop order persistence).
