# TidyUp v1.2.0 remediation — revised gated task catalog

**Plan revision:** 2 · **Updated:** 26 October 2026 · **Implementation status:** Not started by this document.

This replaces the supplied task catalog as an execution specification. All 39 `TASK-REM-*` IDs are retained for traceability; their execution order and unsafe instructions are corrected. Three prerequisite tasks are added. The product baseline is v1.2.0, not the version to publish after repairs. This is a plan improvement, not a new claim that the current application has been tested or fixed.

## What changed and why

| Original weakness | Revision |
| --- | --- |
| Phases started at 8 with no baseline or migration gate | Standalone Phases 0–8, repository preflight and durable execution records |
| Safety changes depended on a later journal task | Durable intent/reconciliation precedes overwrite and extraction repairs |
| Watcher called executor directly | All entry points share immutable planning, authorization, admission and journaling |
| `isUserAuthorized` boolean could authorize unrelated effects | Consent bound to plan revision, selected actions and concrete effects |
| “Two-phase” journal only described post-operation logging | Commit intent before mutation and reconcile crash windows explicitly |
| Overwrite policy cleared batch-internal collisions | Path ownership and dependency-aware reservation; ambiguous collisions block |
| Settings demanded concurrency while batches were serial | Retain serial execution; label unsupported concurrency honestly |
| Failed actions could be called “all applied” | Domain outcomes and explicit retry/reconciliation semantics |
| Documentation promised unwired schedules and a future release | Capability claims require actual lifecycle evidence |
| Fixed test-count/zero-warning goals implied completeness | Requirement coverage, baseline warning review and real Windows evidence |
| Release task assumed 404 and recreated historical v1.2.0 | Verify current state, preserve tag, prepare new candidate, separate publication |
| Immutable scopes blocked necessary prerequisite fixes | Controlled scope amendments with linked evidence; no unrelated refactoring |

## 1. LLM execution contract

1. **One active task.** Follow the canonical order below. Default to one task per focused commit when committing is authorized; large tasks may have numbered child commits. Do not force a broken intermediate commit merely to satisfy an arbitrary one-commit rule. Never mix unrelated cleanup.
2. **No invented completion.** Task states: `NOT_STARTED`, `IN_PROGRESS`, `IMPLEMENTED_UNVERIFIED`, `BLOCKED_ENVIRONMENT`, `BLOCKED_DEPENDENCY`, `BLOCKED_DECISION`, `DONE`. Only evidence-backed acceptance can produce DONE. Initially all tasks are NOT_STARTED.
3. **Scope discipline.** Read the active task, its dependencies, relevant code and tests. Adjacent unrelated issues go to a deferred ledger. A discovered dependency that prevents safe completion must be recorded and addressed as an explicit prerequisite/subtask; never ignore it because scope is “immutable.” Record the amendment, affected task IDs and rationale. Ask only for material unresolved product choices; proceed with independent authorized work.
4. **Source truth.** Read applicable repository instructions. Verify the checkout SHA, tag and current implementation. Old paths are search starting points, not guaranteed files; historical line numbers were removed. A class existing is not proof the real app constructs or calls it. Revalidate each original finding before changing code.
5. **Minimal implementation.** Preserve the existing architecture and framework unless evidence requires a change. Prefer extending canonical services over parallel implementations. No speculative rewrites, new framework, broad formatting, package upgrades or new features hidden inside remediation.
6. **Tests by risk.** Use unit tests for pure policy/state logic and deterministic fault injection; use real isolated Windows filesystem/SQLite fixtures for platform semantics. Mocks alone cannot prove locking, Recycle Bin, cross-volume behavior, shell integration or WPF wiring. Do not ban mocks where they provide useful failure injection.
7. **Environment honesty.** Missing Windows/SDK/permissions is a blocker for the relevant evidence, not a pass. Continue independent safe work when possible; do not activate automation or pass a release gate without required evidence. Never use a real user profile, personal files or production folders for tests.
8. **No deceptive success.** No dummy progress or hard-coded success. Legitimate debounce/retry delays remain valid; do not remove every Task.Delay mechanically. Disabled functionality is containment with an explicit limitation, not a completed implementation.
9. **No task drift.** Before editing, state task ID, objective, expected files and checks in a short update. After editing, summarize actual changes/evidence and the next eligible task. Provide concise decisions, not hidden reasoning transcripts. After two repeated failures with the same approach, investigate the cause and revise the approach rather than repeating blindly.
10. **Authorization boundary.** Working through this implementation plan does not by itself authorize public publishing, retagging, merging, pushing or messaging others. Existing explicit user authorization remains valid. Routine reversible implementation needs no extra approval ceremony.

### Required invariants

- Preview and Scan do not mutate files, save drafts, activate rules or enqueue automatic execution.
- Saved, enabled, opted-in for automation, eligible and currently watching are distinct states.
- Draft edits never mutate repository objects or overwrite newer runtime statistics accidentally.
- One authoritative configuration owner and one application-wide execution admission owner exist.
- The approved plan is immutable: concrete paths, variables, counter, rule revision and destructive effects are fixed.
- Selecting actions includes dependency closure; changing the plan invalidates consent.
- Preflight checks current initial inputs; execution checks each predecessor output and live preconditions.
- No runtime fallback silently renames, overwrites or executes a different plan.
- Intent is durably committed before mutation; uncertain outcomes are reconciled, never blindly replayed.
- Recycle Bin is the deletion default; permanent delete and overwrite need valid scoped authorization.
- Path containment accounts for Windows roots and reparse points; cleanup never deletes the monitored root.
- Backups and journal evidence remain available for incomplete/recoverable operations.
- Manual, watcher and scheduler entry points use the same safe operation pipeline.
- Automatic activity is not activated until the safety gate passes, including legacy-rule activation policy.
- Runtime UI state is derived from acknowledged services and durable outcomes, not optimistic booleans.
- Undo reports actual inverse capability/postconditions; manual recovery is not successful automatic restoration.

### Per-task execution loop

1. Verify previous gate and dependencies; select exactly one eligible task.
2. Inspect relevant callers, DI registrations, persistence model, UI bindings and tests. Reproduce or document why the finding no longer applies.
3. Add a meaningful regression or reproducible manual scenario for the failure. Do not write a test that only mirrors new implementation details.
4. Implement the smallest coherent change, including all affected callers/migrations and error paths.
5. Run focused checks and required platform tests. Review the diff for scope, behavior regressions, stale comments and bypass entry points.
6. Record commands, exit codes, test names/counts, environment, SHA, expected/actual results and evidence paths. Do not delete, weaken or skip a failing requirement to make tests green.
7. Update tracker/finding matrix/handoff. Mark DONE only after every acceptance item is proved. Continue to the next eligible task; at a blocked gate, leave a precise handoff and continue only independent work.

## 2. Canonical phases and gates

**This order replaces the original Phases 8–14.** Task numbers are stable identifiers, not execution order. Within each phase, follow the listed order; each task depends on the preceding task and the previous phase gate by default. This conservative serial graph avoids hidden dependency cycles. A justified independent reorder must be recorded before work; it must never bypass a safety gate.

| Phase | Ordered tasks | Exit gate |
| --- | --- | --- |
| 0 — Baseline, contracts and containment | BASE-01 → BASE-02 → BASE-03 | Reproducible baseline, selected ownership/policy contracts, dangerous paths contained |
| 1 — Rule/configuration integrity | 08 → 09 → 10 → 11 → 34 → 28 | Drafts/save/import/order and configuration persist correctly; automation remains gated |
| 2 — Truthful read-only planning | 12 → 13 → 14 → 15 → 16 → 17 → 18 → 27 | Complete, cancellable, revision-bound read-only plans; faithful chains and collisions |
| 3 — Durable execution foundation | 22 → 19 → 20 | Intent-before-mutation, exact plan execution and whole-batch admission/reconciliation |
| 4 — Safety, recovery and outcomes | 03 → 04 → 02 → 05 → 01 → 23 → 21 | Crash/failure/overwrite/delete/extract/cleanup/recovery checks pass on Windows |
| 5 — Application monitoring/lifecycle | 25 → 26 → 24 → 07 → 29 → 30 | Explicit activation; real acknowledged scope/pause; shared pipeline; graceful shutdown |
| 6 — Reporting and settings honesty | 31 → 32 → 33 → 06 → 35 → 36 | Metrics/history/exports agree with durable facts; settings/features have evidence |
| 7 — Visual and integrated acceptance | 37 → 38 | Actual application graph and populated Windows GUI pass the acceptance matrix |
| 8 — Candidate packaging/handoff | 39 | Tested candidate, provenance, checksums and accurate release notes; publication status explicit |

Early containment stays active until its replacement is verified. Task 24 may exercise safe automation after Phase 4, but unsupported features stay unavailable until their own acceptance passes. Full release readiness requires all gates and unresolved-finding disposition, not merely a working watcher.

## 3. Prerequisite tasks

### TASK-BASE-01 — Pin the baseline and create execution records

**Scope:** Checkout identity, environment, test isolation, original finding revalidation and tracking only.

- Read applicable AGENTS.md/repository instructions, current branch/status, version files, solution and build/test workflows. Preserve unrelated user changes; use an isolated branch/worktree if needed. Do not reset their checkout.
- Record the v1.2.0 tag commit and actual work SHA. If working on later code, record divergence and classify each TU finding as Confirmed / Already fixed with evidence / Not reproducible / Requires Windows. Do not label an untested finding resolved.
- Discover the repository’s supported SDK/build/test commands. Record existing failures, warnings, skipped tests and environment. No arbitrary target such as “640 tests.”
- Create a unique disposable test root and isolated app-data/database profile. Verify no watcher, backup, command or deletion target can escape it. Use explicit lab setup for UNC/cross-volume/Recycle Bin tests.
- Create `docs/remediation/TRACKER.md`, `FINDING_MATRIX.md`, `DECISIONS.md`, `SETTINGS_MATRIX.md`, `FEATURE_MATRIX.md`, `HANDOFF.md`. Store logs/screenshots in an appropriate ignored evidence directory; keep portable references and no secrets in committed docs.
- Record the attached catalog’s provenance and preserve the original review alongside the plan when available. If the original review is unavailable, record that limitation; do not fabricate its missing evidence.

**Done when:** A new agent can identify the exact code/environment, reproduce the baseline and distinguish all 44 findings from their current verified status. No feature is claimed repaired merely by completing this task.

### TASK-BASE-02 — Set shared contracts before touching consumers

**Scope:** Minimal interfaces/state definitions and short decision records, with focused pure-logic tests. Avoid a wholesale architecture rewrite.

- Define standalone folder persistence with stable identity; rule-specific recursion/exclusion options remain per-rule. Avoid two competing folder catalogs.
- Define RuleDraft baseline, dirty state, validation, saved revision and concurrent runtime-metadata ownership.
- Define PlanId, generation, rule/settings revisions, ActionId/dependencies, initial origin, concrete paths, scan completeness, action eligibility and authorization scope.
- Define action outcomes, batch lease, journal intent/outcome/reconciliation states and recovery capabilities. Database and filesystem are not one atomic transaction.
- Define activation policy: imports disabled; saving is not activation; legacy enabled rules require review before newly wired automation begins. Define which effects background policy may authorize and which must wait for manual review.
- Define supported action continuation, counter scope, metrics meaning, partial-scan policy, pause/restart behavior and backup retention. Prefer the least surprising safe behavior; record materially ambiguous product decisions.
- Define additional surfaced action risks such as RunCommand: explicit executable/arguments, timeout, cancellation, working directory, output/exit status and inability to infer filesystem changes. Unsupported unsafe execution stays unavailable; do not claim preview predicts arbitrary command side effects.

**Done when:** Subsequent tasks reference one coherent contract, no bypass boolean or competing service is required, migrations/compatibility impacts are listed, and open product decisions are explicit.

### TASK-BASE-03 — Apply narrow containment before permanent repairs

**Scope:** Reversible blocking/labeling for dangerous or misleading paths, with a containment ledger linking to permanent repair tasks.

- Block unconfirmed permanent deletion, unsafe overwrite/Prompt fallback, unbounded cleanup and unsafe archive side effects. Do not substitute a different destructive operation.
- Stop false automatic rollback success and false Add Folder/Pause/Maintenance success; show unavailable/action-required state until actual integration is complete.
- Keep legacy automatic activation, unsupported triggers and unsafe command execution gated. Manual read-only inspection remains usable.
- Record each temporary guard, why it exists, owner task and exact evidence required before removal. Test the guard at the actual UI/service entry point.

**Done when:** Unsafe flows fail closed with truthful messages and a follow-up owner. Status of underlying defects is `CONTAINED`, not `FIXED_VERIFIED`.

## 4. Implementation task specifications

The original objectives and useful scope detail are retained below with corrected instructions. Each task’s **Additional acceptance and regression requirements** is mandatory, not optional advice. Shared invariants apply even when not repeated in the task block. The canonical phase table controls order.

### TASK-REM-08: Deep-Cloned Rule Draft Engine & Shared Save/Discard Contract
```text
TASK ID: TASK-REM-08
REVIEW DEFECTS: TU-08 (P1), TU-09 (P1), TU-10 (P1), TU-11 (P2)
PRIORITY: P1
OBJECTIVE:
Implement a unified deep-cloned RuleDraft service shared by Guided and Advanced editors, preserving all metadata on Save, restoring baseline on Discard, and keeping dirty state on failed Save.

TARGET FILES:
- `TidyUp/ViewModels/MainWindowViewModel.cs`
- `TidyUp/ViewModels/RuleEditor/RuleWizardViewModel.cs`
- `TidyUp/Models/Domain/Rule.cs`
- `TidyUp.Tests/Unit/ViewModels/RuleWizardModeSynchronizationTests.cs`
- `TidyUp.Tests/Unit/ViewModels/MainWindowDirtyStateTests.cs`

IN SCOPE:
- Create `RuleDraft` or deep-clone helper (`Rule.DeepClone()`):
  - When a rule is selected or created, edit a deep clone, never the repository reference.
  - Track dirty state by comparing current draft against the cloned baseline (including nested conditions, action parameters, and folder options).
- Unify Save:
  - Both Guided Save and Shell Save (`Ctrl+S`) must call the identical `SaveDraftAsync()` logic.
  - Preserve all fields: `ExecutionOrder`, `StopProcessingAfterMatch`, `TriggerType`, `ScheduledTime`, `CreatedDate`, `LastRunDate`, `FilesProcessedCount`.
  - Clear dirty flags ONLY after repository save succeeds. If save throws validation or DB error, retain dirty state and draft edits.
- Unify Discard & Cancel:
  - Discard replaces the active draft with a fresh clone of the saved rule.
  - Wizard Cancel invokes Shell Cancel handler, resets draft state, and returns cleanly without orphaned state.

OUT OF SCOPE:
- Adding new rule trigger types.

DONE WHEN:
- Saving via Wizard preserves priority, stop-processing, and schedule metadata identical to Shell Save.
- Discarding changes completely rolls back in-memory edits to the database snapshot.
- A failed save preserves dirty state and warning banner.
- Wizard Cancel button resets editor and clears uncommitted state.
- Automated tests verify lossless dual-mode switching and discard restoration.
```

**Additional acceptance and regression requirements:** Include every nested condition/action/folder option in cloning and dirty tracking. Separate editable rule fields from runtime counters to prevent saving a stale draft over newer statistics. Test new-rule cancel, mode switch, selection/navigation/close Save–Discard–Cancel, failed save, enable-with-unsaved-edits and validation parity.

---

### TASK-REM-09: Rule List Drag-and-Drop Order Persistence
```text
TASK ID: TASK-REM-09
REVIEW DEFECTS: TU-30 (P2)
PRIORITY: P2
OBJECTIVE:
Persist execution priority order to SQLite database atomically when rules are reordered via drag-and-drop in the Rules view.

TARGET FILES:
- `TidyUp/MainWindow.xaml.cs`
- `TidyUp/ViewModels/MainWindowViewModel.cs`
- `TidyUp.Data/Repositories/IRuleRepository.cs`
- `TidyUp.Tests/Unit/ViewModels/MainWindowRulesListTests.cs`

IN SCOPE:
- Add `UpdateExecutionOrdersAsync(IEnumerable<(Guid RuleId, int Order)> orderings)` to `IRuleRepository` executing inside an atomic database transaction.
- When `RuleListBox_Drop` occurs:
  - Update `ExecutionOrder` on all affected rules in memory.
  - Dispatch persistence command to save updated orderings to SQLite.
  - Disable drag-and-drop reordering when an active search filter or non-priority sort is applied.

OUT OF SCOPE:
- Changing the rule execution priority algorithm.

DONE WHEN:
- Reordering rules via drag-and-drop persists across application restart.
- Drag-and-drop is cleanly disabled during active text search/filtering.
- Unit tests verify batch priority order persistence.
```

**Additional acceptance and regression requirements:** Failed reorder transaction must restore the previous visible order and surface error. Verify deterministic tie-breaking and stored priority used by execution. Include active filter and non-priority sort cases.

---

### TASK-REM-10: Unified Schema-Aware Rule Serialization & Import Gate
```text
TASK ID: TASK-REM-10
REVIEW DEFECTS: TU-29 (P1)
PRIORITY: P1
OBJECTIVE:
Consolidate rule import/export on the schema-aware RuleSerializationService, enforce disabled-on-import, validate versions, and provide atomic batch import.

TARGET FILES:
- `TidyUp/ViewModels/MainWindowViewModel.cs`
- `TidyUp.Core/Rules/RuleSerializationService.cs`
- `TidyUp.Core/Services/ImportExportService.cs`
- `TidyUp.Tests/RuleTests/SerializationTests.cs`

IN SCOPE:
- Retire deprecated `ImportExportService` in favor of `RuleSerializationService`.
- Enforce invariant: All imported rules MUST be saved in `IsEnabled = false` state.
- Enforce schema version check on import; reject unsupported future schema versions with clear error message.
- Validate the entire import first; commit the batch in one transaction. On any validation or persistence failure, import zero rules and show the reason. Partial import is a separate, explicitly designed feature, not a fallback.

OUT OF SCOPE:
- Remote web-based rule sharing.

DONE WHEN:
- Importing rules via MainWindow uses `RuleSerializationService`.
- Imported rules always import as Disabled.
- Invalid schema or corrupt JSON displays descriptive validation error.
- Unit tests verify schema check and disabled-on-import invariant.
```

**Additional acceptance and regression requirements:** Test old supported schema migration, future version rejection, malformed action parameters, duplicate IDs/names, batch failure rollback and complete round-trip field fidelity. Imports never start monitoring. Preserve compatibility adapters until all callers use the canonical serializer.

---

### TASK-REM-11: Variable Engine Normalization & Filename Sanitization
```text
TASK ID: TASK-REM-11
REVIEW DEFECTS: TU-39 (P2), TU-40 (P2)
PRIORITY: P2
OBJECTIVE:
Standardize supported variable tokens across Help, Onboarding, and VariableEngine, sanitize time variables against illegal Windows filename characters, and validate patterns.

TARGET FILES:
- `TidyUp.Core/Services/VariableEngine.cs`
- `TidyUp/ViewModels/FirstRunViewModel.cs`
- `TidyUp/Views/HelpView.xaml`
- `TidyUp.Core/Validation/RuleValidator.cs`
- `TidyUp.Tests/Unit/Services/VariableEngineTests.cs`

IN SCOPE:
- In `VariableEngine`:
  - Sanitize time tokens: `now_time`, `created_time`, `modified_time` must format as `HH-mm-ss` (hyphens) instead of `HH:mm:ss` (colons are illegal in Windows filenames).
  - Support case-insensitive variable matching for documented tokens (`{date}`, `{ext}`, `{extension}`, `{year}`, `{month}`, `{day}`).
  - Add `IsKnownVariable(string token)` method.
  - If an unknown variable token is encountered (e.g. `{foobar}`), block execution of that pattern and show a field-level validation error in `RuleValidator`; do not silently produce literal brace names.
- In `FirstRunViewModel`:
  - Align sample rule pattern with official supported tokens (`{extension}/{now_year}-{now_month}`).
- In `HelpView.xaml`:
  - Update variable reference table to match actual supported tokens.

OUT OF SCOPE:
- Custom user-defined regex scripting variables.

DONE WHEN:
- Time variables generate valid Windows filenames without colons.
- First-run wizard sample rule resolves without unexpanded literal braces.
- HelpView documents only real, functional variables.
- VariableEngineTests verify all documented variables and filename safety.
```

**Additional acceptance and regression requirements:** Create one token registry used by validation, help and onboarding. Document aliases, casing, clock/timezone and counter scope. Validate final filenames for reserved names, invalid characters, trailing dots/spaces, escaping paths and empty results without blindly sanitizing path separators in whole destination paths.

---

### TASK-REM-34: Atomic Settings Persistence & Corrupt Profile Recovery
```text
TASK ID: TASK-REM-34
REVIEW DEFECTS: TU-42 (P2)
PRIORITY: P2
OBJECTIVE:
Implement atomic file replacement for settings.json with backup preservation, preventing file corruption and presenting recovery feedback on bad JSON.

TARGET FILES:
- `TidyUp.Data/SettingsRepository.cs`
- `TidyUp.Tests/Unit/Data/SettingsPersistenceTests.cs`

IN SCOPE:
- In `SettingsRepository.SaveAsync`:
  - Write validated JSON to a uniquely named temporary file in the same directory; flush, close, then replace. Handle first save when no destination exists, maintain a known-good backup, and clean abandoned temporary files safely.
  - Atomically replace target using `File.Replace` (or atomic rename with backup).
  - Synchronize concurrent save calls using a semaphore or lock.
- In `SettingsRepository.LoadAsync`:
  - If `settings.json` is corrupt or invalid JSON:
    - Backup corrupt file to `settings.json.corrupt.<timestamp>`.
    - Do NOT silently replace with defaults without notification; throw or return structured `SettingsLoadResult` indicating corruption so `App.xaml.cs` can display the recovery warning to the user.

OUT OF SCOPE:
- Registry-based settings storage.

DONE WHEN:
- Fault-injection verifies a complete old or new settings document is retained at the supported interruption points. Document filesystem/durability limits; do not claim immunity to every process/power/storage failure.
- Corrupt settings files are backed up rather than clobbered.
- Unit tests prove atomic replacement and corrupt file backup.
```

**Additional acceptance and regression requirements:** Validate semantic values as well as JSON syntax. Recover from known-good backup where safe, preserve corrupt bytes, and keep automation paused until configuration is trustworthy. Test first save, concurrent save, interrupted replacement, invalid numeric ranges and failed backup.

---

### TASK-REM-28: Typed AppSettings DI Registration & Service Configuration
```text
TASK ID: TASK-REM-28
REVIEW DEFECTS: TU-28 (P1)
PRIORITY: P1
OBJECTIVE:
Register AppSettings in the dependency injection container as a typed singleton, supplying live user preferences to TrayIconService and ToastNotificationService.

TARGET FILES:
- `TidyUp/ServiceConfiguration.cs`
- `TidyUp/App.xaml.cs`
- `TidyUp/Services/ToastNotificationService.cs`
- `TidyUp/Services/TrayIconService.cs`
- `TidyUp.Tests/Unit/Services/ToastNotificationServiceTests.cs`

IN SCOPE:
- In `ServiceConfiguration.ConfigureServices()`:
  - Register `AppSettings` as a singleton instance (or register `IOptions<AppSettings>`).
  - Wire `AppSettings` into `ToastNotificationService` and `TrayIconService` constructors.
- In `App.xaml.cs`:
  - Load and validate settings BEFORE constructing consumers. All consumers use the same configuration owner and explicit update notification; replacing a singleton reference later must not strand old instances.
  - Respect `AppSettings.SystemTrayEnabled`: do NOT initialize tray icon if user has disabled system tray in settings.
  - Verify actual setting property names and supported notification/quiet behavior in the checkout; inject the authoritative configuration snapshot/service. Do not invent NotificationsEnabled or quiet-hours support if the product only exposes different controls.

OUT OF SCOPE:
- Changing Windows 11 Action Center notification toast templates.

DONE WHEN:
- Disabling system tray in settings prevents tray icon creation on startup.
- Toast notifications honor the actual supported notification/quiet settings exposed by the application; unsupported controls are disclosed and contained.
- Unit tests verify `ToastNotificationService` respects injected settings.
```

**Additional acceptance and regression requirements:** Test real composition-root construction with non-default saved settings before resolving consumers. Validate defaults and changes in-session and after restart; remove multiple competing settings instances. Notification failure must not change operation outcome.

---

### TASK-REM-12: XAML Converter & Visibility Binding Repairs
```text
TASK ID: TASK-REM-12
REVIEW DEFECTS: TU-06 (P1), TU-07 (P2)
PRIORITY: P1
OBJECTIVE:
Fix visibility converters across Stage 4 Review, Wizard, and Preview so simulation results, error panels, and icons render correctly without binding errors.

TARGET FILES:
- `TidyUp/Views/RuleEditor/Stages/Stage4ReviewView.xaml`
- `TidyUp/Views/RuleEditor/RuleWizardView.xaml`
- `TidyUp/Views/PreviewView.xaml`
- `TidyUp/Converters/StringToVisibilityConverter.cs`
- `TidyUp/Converters/InverseBooleanToVisibilityConverter.cs` (New)
- `TidyUp/App.xaml`

IN SCOPE:
- Create `InverseBooleanToVisibilityConverter` implementing `IValueConverter` (returns `Visibility.Collapsed` when true, `Visibility.Visible` when false).
- Register `InverseBooleanToVisibilityConverter` in `App.xaml`.
- In `Stage4ReviewView.xaml`:
  - Replace `StringToVisibilityConverter` on `ExecutionPlan` binding with `NullToVisibilityConverter` or bind to boolean `HasExecutionPlan`.
  - Fix Next button and error panel visibility bindings to use `BooleanToVisibilityConverter` / `InverseBooleanToVisibilityConverter`.
- In `PreviewView.xaml`: Fix failure icon visibility binding.

OUT OF SCOPE:
- Restyling review stage card templates.

DONE WHEN:
- Generating a wizard simulation plan displays the results table in Stage 4.
- WPF binding output log shows zero type-conversion errors on Wizard and Preview.
- Unit tests verify `InverseBooleanToVisibilityConverter`.
```

**Additional acceptance and regression requirements:** Exercise actual views with no plan, populated plan, validation error, loading, cancelled and failed states. Converter unit tests alone do not prove a WPF binding works. Capture relevant binding diagnostics with resource dictionaries loaded.

---

### TASK-REM-13: Disabled Rule Dry-Run Simulation Decoupling
```text
TASK ID: TASK-REM-13
REVIEW DEFECTS: TU-05 (P1)
PRIORITY: P1
OBJECTIVE:
Decouple dry-run simulation from automatic execution eligibility so disabled rules can be evaluated and dry-run without enabling them.

TARGET FILES:
- `TidyUp.Core/Services/RuleEngine.cs`
- `TidyUp/Services/Simulation/ExecutionPlanGenerator.cs`
- `TidyUp/ViewModels/RuleEditor/RuleWizardViewModel.cs`
- `TidyUp.Tests/Unit/Services/RuleEngineTests.cs`

IN SCOPE:
- In `IRuleEngine` / `RuleEngine`:
  - Separate condition matching from execution eligibility, using an explicit evaluation purpose or distinct API so a dry-run bypass cannot accidentally enable production execution.
  - In automatic watcher / execution pipeline, enforce automatic-execution eligibility (disabled rules do not execute).
  - In `ExecutionPlanGenerator` and `RuleWizardViewModel.SimulateRuleAsync`, use the preview matching API (disabled drafts simulate matching files without changing eligibility).
- Ensure condition matching and dry-run counts match for newly created disabled rules.

OUT OF SCOPE:
- Running background automated file monitoring on disabled rules.

DONE WHEN:
- A disabled rule with matching fixture files generates the same planned actions as its enabled equivalent in both Wizard and Preview, while remaining disabled.
- Background watcher still ignores disabled rules.
- RuleEngineTests verify explicit preview matching versus automatic eligibility.
```

**Additional acceptance and regression requirements:** Prefer an explicit evaluation purpose or separated eligibility/matching API over a boolean that can accidentally enable production execution. Draft preview must not save, enable, subscribe a watcher or mutate files.

---

### TASK-REM-14: Multi-Action Virtual Path Continuation & Action Dependency Model
```text
TASK ID: TASK-REM-14
REVIEW DEFECTS: TU-13 (P1), TU-14 (P1)
PRIORITY: P1
OBJECTIVE:
Model action chain continuation semantics correctly in ExecutionPlanGenerator and update PreFlight check to validate input origins rather than non-existent intermediate paths.

TARGET FILES:
- `TidyUp/Services/Simulation/ExecutionPlanGenerator.cs`
- `TidyUp/ViewModels/PreviewViewModel.cs`
- `TidyUp/Models/Domain/PlannedFileAction.cs`
- `TidyUp.Tests/Unit/Services/ExecutionPlanGeneratorTests.cs`

IN SCOPE:
- In `ExecutionPlanGenerator`:
  - Add `InitialSourcePath` (the real file on disk) and `DependsOnActionId` to `PlannedFileAction`.
  - Continuation semantics per action type:
    - `Move`: Next action source = destination path.
    - `Copy`: If `ApplyToSourceFile == true`, next action source = original source; else next action source = destination copy.
    - `Rename`: Next action source = directory + new name.
    - `ExtractArchive`: Do NOT treat extraction directory as a file path for subsequent file actions.
    - `RunCommand`: Do NOT treat command text as a source path for subsequent actions.
- In `PreviewViewModel.PerformPreFlightCheck`:
  - For initial actions in a chain, verify `File.Exists(action.SourcePath)`.
  - For dependent actions in a chain (`DependsOnActionId != null`), validate the initial origin and dependency graph rather than requiring future intermediate paths to exist. Immediately before each dependent action executes, verify its predecessor succeeded and its actual output satisfies that action’s preconditions.

OUT OF SCOPE:
- Dynamic branching logic inside a single rule.

DONE WHEN:
- Multi-action chains (e.g. Rename → Move) pass pre-flight checks cleanly.
- Copy with `ApplyToSourceFile = true` passes original file to subsequent actions.
- Unit tests verify multi-action chain path resolution and pre-flight validation.
```

**Additional acceptance and regression requirements:** Specify Copy source/copy continuation; Delete terminates file continuation; RunCommand output is not a path; extraction continuation remains the archive unless an explicit supported output model says otherwise. Reject cycles, missing predecessors and partial selection without dependency closure. Recheck predecessor outputs at execution.

---

### TASK-REM-15: In-Memory Target Reservation & Conflict Resolution Simulation
```text
TASK ID: TASK-REM-15
REVIEW DEFECTS: TU-16 (P1)
PRIORITY: P1
OBJECTIVE:
Simulate policy-based conflict resolution during preview planning, detect batch-internal collisions, and allow resolvable policies to proceed to execution.

TARGET FILES:
- `TidyUp/Services/Simulation/ExecutionPlanGenerator.cs`
- `TidyUp/ViewModels/PreviewViewModel.cs`
- `TidyUp.Tests/Unit/Services/ExecutionPlanGeneratorTests.cs`

IN SCOPE:
- In `ExecutionPlanGenerator`:
  - Maintain a batch target reservation ledger: a normalized path-to-action reservation ledger with read/write/dependency ownership (a HashSet alone is insufficient for planned overwrite conflicts).
  - If a target path is already on disk OR already reserved by another action in the batch:
    - If `ConflictResolution == ConflictResolution.RenameNew`: Project the actual unique name (e.g. `file (1).txt`), reserve it, update `plannedAction.TargetPath`, and set `HasConflict = false` with note "Auto-renamed due to conflict".
    - For Overwrite against an existing disk file: retain the target, describe displaced content and recovery, and require scoped authorization. For two independent batch actions targeting the same path: block the collision unless the plan explicitly defines and presents an intentional ordered replacement dependency. Never clear all batch collisions merely because Overwrite is selected.
    - If `ConflictResolution == ConflictResolution.Skip`: Set `IsSkipped = true`, `HasConflict = false`.
    - If unresolvable or `Prompt`: Set `HasConflict = true`.
- In `PreviewViewModel.CanApplyChanges`: Allow applying when conflicts are policy-resolved. Block only for truly unresolvable conflicts.

OUT OF SCOPE:
- Interactive in-preview renaming dialogs.

DONE WHEN:
- Two files in the same batch targeting the same name are detected and projected with unique names under `RenameNew`.
- Rules with `AutoRename` or `Overwrite` are not blocked by false conflict flags.
- Unit tests verify batch collision detection and conflict projection.
```

**Additional acceptance and regression requirements:** Reservations must model files versus directories, case-insensitive identity, RenameOld, same-source/target and dependencies that release a path. Freeze auto-renamed targets in the approved plan. New external collisions invalidate execution; they never trigger silent runtime name changes.

---

### TASK-REM-16: Preservation of Subfolder Directory Hierarchy
```text
TASK ID: TASK-REM-16
REVIEW DEFECTS: TU-31 (P2)
PRIORITY: P2
OBJECTIVE:
Implement PreserveSubfolderStructure in ExecutionPlanGenerator and ActionExecutor so recursive organization preserves relative subfolder trees.

TARGET FILES:
- `TidyUp/Services/Simulation/ExecutionPlanGenerator.cs`
- `TidyUp/Services/ActionExecutor.cs`
- `TidyUp/Models/Domain/PlannedFileAction.cs`
- `TidyUp.Tests/Unit/Services/ActionExecutorTests.cs`

IN SCOPE:
- In `PlannedFileAction`: Add `MonitoredSourceRoot` property.
- When `PreserveSubfolderStructure == true`:
  - Compute relative path: `Path.GetRelativePath(monitoredRoot, fileInfo.DirectoryName)`.
  - Destination directory becomes `Path.Combine(destinationBase, relativeSubfolder)`.
- Resolve this once in the planner. Executor uses the immutable concrete path and verifies confinement without independently recalculating it. Reject rooted/escaping relative paths and reparse-point escapes.

OUT OF SCOPE:
- Modifying folder exclusion regex evaluation.

DONE WHEN:
- Files in recursive subdirectories (e.g. `Root/SubA/doc.txt`) move to `Dest/SubA/doc.txt` when `PreserveSubfolderStructure` is enabled.
- Unit tests verify preserved subfolder calculation for nested paths.
```

**Additional acceptance and regression requirements:** Test multiple roots, source exactly at root, nested trees, UNC roots and overlapping roots with an explicit matched-root policy. Preview and actual destination must be identical.

---

### TASK-REM-17: Responsive Background Enumeration & Partial Scan Disclosure
```text
TASK ID: TASK-REM-17
REVIEW DEFECTS: TU-25 (P1)
PRIORITY: P1
OBJECTIVE:
Make folder enumeration in ExecutionPlanGenerator asynchronous and cancellable, handle per-directory access errors gracefully, and disclose incomplete scans in preview.

TARGET FILES:
- `TidyUp/Services/Simulation/ExecutionPlanGenerator.cs`
- `TidyUp/ViewModels/PreviewViewModel.cs`
- `TidyUp/Models/Domain/ExecutionPlan.cs`
- `TidyUp.Tests/Unit/Services/ExecutionPlanGeneratorTests.cs`

IN SCOPE:
- Change `GeneratePlanForFolderAsync` to use asynchronous/background file enumeration (`Directory.EnumerateFiles` with `Task.Run` offloaded from UI thread).
- Wrap subfolder enumeration in try-catch: if a subfolder throws `UnauthorizedAccessException`, log warning and continue scanning remaining accessible folders rather than failing the entire folder plan.
- Add `IsIncomplete` and `ScanErrors` list to `ExecutionPlan`.
- If any folder could not be scanned, display warning banner in `PreviewView`: "Scan partially completed. Some directories were inaccessible. Showing results for accessible files only."
- Support `CancellationToken` throughout enumeration loop.

OUT OF SCOPE:
- Elevating Windows process administrator privileges for restricted folders.

DONE WHEN:
- Generating preview for large or restricted trees does not freeze the UI.
- Inaccessible subfolders do not wipe out plans for accessible sibling folders.
- Incomplete scans are clearly disclosed to the user.
- Unit tests verify cancellable enumeration and permission error tolerance.
```

**Additional acceptance and regression requirements:** Enumerate directories independently so one inaccessible subtree does not abort siblings. Cancellation stops admission and discards executable partial plans; incomplete scans require explicit visible policy/acknowledgment before applying a subset. Generation IDs prevent a late scan overwriting a newer plan. Disclose sampling limits separately from errors.

---

### TASK-REM-18: Preview Cache Invalidation & Explicit Refresh Triggers
```text
TASK ID: TASK-REM-18
REVIEW DEFECTS: TU-12 (P1)
PRIORITY: P1
OBJECTIVE:
Invalidate cached preview plans when rules are edited or switched, and provide an explicit "Refresh / Generate Preview" command in PreviewView.

TARGET FILES:
- `TidyUp/ViewModels/MainWindowViewModel.cs`
- `TidyUp/ViewModels/PreviewViewModel.cs`
- `TidyUp/Views/PreviewView.xaml`
- `TidyUp.Tests/Unit/ViewModels/PreviewViewModelTests.cs`

IN SCOPE:
- In `MainWindowViewModel`:
  - Invalidate `PreviewViewModel.ExecutionPlan` when `SelectedRule` changes or when edits are saved/discarded.
  - When `TestRuleAsync` is invoked, pass the active draft snapshot (from either Guided or Advanced mode).
- In `PreviewViewModel`:
  - Add `RefreshPreviewCommand` allowing users to re-run simulation directly from the Preview workspace without returning to Rules.
  - Include rule name and plan generation timestamp in the preview header.
- In `PreviewView.xaml`: Add "Refresh Preview" button to header toolbar.

OUT OF SCOPE:
- Auto-refreshing preview continuously on every keystroke.

DONE WHEN:
- Switching rules dismisses previous rule's preview plan.
- PreviewView has a functional "Refresh Preview" button.
- Unit tests verify plan invalidation on rule selection change.
```

**Additional acceptance and regression requirements:** Plan identity includes rule/draft revision, folder scope, settings/policy revision, generation and selected dependencies. Rule edits, settings changes and changed file preconditions invalidate consent/Apply. Test overlapping refreshes and stale completion ordering.

---

### TASK-REM-27: Scan Command Semantic Harmonization
```text
TASK ID: TASK-REM-27
REVIEW DEFECTS: TU-04 (P2)
PRIORITY: P2
OBJECTIVE:
Harmonize Scan operations between Dashboard and Folders so both perform deterministic read-only discovery with consistent execution pathways.

TARGET FILES:
- `TidyUp/ViewModels/FoldersViewModel.cs`
- `TidyUp/ViewModels/DashboardViewModel.cs`
- `TidyUp.Tests/Unit/ViewModels/FoldersViewModelTests.cs`

IN SCOPE:
- Clearly define scan semantics:
  - "Scan / Reconcile": Re-scans disk to find existing files that match rules, and updates dashboard pending file counts.
  - "Preview": Opens Preview workspace with generated simulation plan.
- Ensure Folders → Scan and Dashboard → Scan call the same underlying folder reconciliation service.
- Provide clear visual feedback in both views when a scan completes.

OUT OF SCOPE:
- Executing destructive actions directly from a scan button without user confirmation.

DONE WHEN:
- Scanning a folder from Dashboard and Folders produces identical file detection results.
- Unit tests verify scan coordination.
```

**Additional acceptance and regression requirements:** Both Scan commands use read-only discovery and explicit results/count semantics. No mutation, automatic-run queue admission or activation occurs on scan completion. Counts disclose all-versus-sampled scope, inaccessible folders and cancellation.

---

### TASK-REM-22: Two-Phase Durable Journaling & Rule Metadata Tracking
```text
TASK ID: TASK-REM-22
REVIEW DEFECTS: TU-21 (P1)
PRIORITY: P1
OBJECTIVE:
Implement intent-and-outcome journaling so failed actions, rule identities, and displaced files are durably recorded in OperationJournal.

TARGET FILES:
- `TidyUp/Services/ActionExecutor.cs`
- `TidyUp/Data/Entities/OperationJournalEntry.cs`
- `TidyUp/Services/Rollback/IRollbackEngine.cs`
- `TidyUp/Services/Rollback/RollbackEngine.cs`
- `TidyUp.Tests/Unit/Services/RollbackEngineTests.cs`

IN SCOPE:
- In `OperationJournalEntry`: Ensure `RuleId`, `RuleName`, `RuleRevisionId`, and `BatchId` are populated for every entry.
- In `ActionExecutor`:
  - Persist a Planned/Intent record in a committed database transaction BEFORE the first filesystem mutation. If intent persistence fails, do not mutate. Transition through Executing to a durable terminal outcome using stable BatchId and ActionId.
  - Record applicable preconditions and hashes before mutation and verified postconditions after mutation. Hash checks do not make filesystem/database changes atomic; explicitly model the crash window and recheck identities immediately before use.
  - Distinguish Succeeded, FailedBeforeMutation, FailedPartial, Skipped, CancelledBeforeMutation and Uncertain. If mutation succeeds but final journal write fails, retain intent/recovery evidence, stop unsafe continuation and reconcile on restart. Never silently retry an uncertain effect.
  - For `Overwrite` and `RenameOld`, record the original displaced file's backup path in `Details`.

OUT OF SCOPE:
- Storing entire file binary blobs in SQLite database.

DONE WHEN:
- Intent is committed before every mutation; a process interruption at each boundary produces a recoverable or explicitly uncertain record on restart. Failed/partial attempts appear in History without false success.
- Every journal entry links to its originating RuleId and RuleName.
- Unit tests verify journal entry population for success and failure.
```

**Additional acceptance and regression requirements:** Specify schema migration, crash reconciliation and backup retention before enabling overwrite. SQLite and filesystem are not a distributed atomic transaction. Journal intent needs actual resolved paths and operation identity; startup reconciles pending records without blindly rerunning. Fault injection must include database failure before and after mutation.

---

### TASK-REM-19: Immutable Planned Parameter Execution & `{counter}` Sequence Fix
```text
TASK ID: TASK-REM-19
REVIEW DEFECTS: TU-15 (P1)
PRIORITY: P1
OBJECTIVE:
Execute concrete pre-resolved parameters from the approved PlannedFileAction instead of recalculating destinations with drifting counters during batch execution.

TARGET FILES:
- `TidyUp/Services/Processing/BatchProcessingCoordinator.cs`
- `TidyUp/Services/ActionExecutor.cs`
- `TidyUp/Models/Domain/PlannedFileAction.cs`
- `TidyUp.Tests/Unit/Services/BatchProcessingCoordinatorTests.cs`

IN SCOPE:
- In `PlannedFileAction`: Store fully resolved concrete `TargetPath`, `TargetFileName`, and `PlannedCounter`.
- In `BatchProcessingCoordinator.ExecuteBatchAsync`:
  - Pass the approved `PlannedFileAction` directly to `ActionExecutor`.
  - `ActionExecutor` must execute the exact `PlannedFileAction.TargetPath` agreed upon in preview, rather than re-evaluating `{counter}` against `i + 1`.
  - Resolve variables, timestamps, names, counters, policies and destinations before approval. No destination-affecting runtime re-expansion is allowed. Changed preconditions require a new preview, not silent renaming or overwrite.

OUT OF SCOPE:
- Changing variable expansion syntax.

DONE WHEN:
- Approving a subset of files (e.g. only file #5) preserves the exact target path and sequence number displayed in preview.
- Unit test proves counter stability when partial selections are executed.
```

**Additional acceptance and regression requirements:** Persist immutable resolved inputs, targets, policy, counter, timestamp expansion and rule revision. Revalidate source and destination immediately before mutation. Selection changes cannot renumber outputs. Retry creates a new validated plan; successful action IDs cannot replay.

---

### TASK-REM-20: Atomic Batch Lifetime Lease & State Machine Guarding
```text
TASK ID: TASK-REM-20
REVIEW DEFECTS: TU-44 (P2)
PRIORITY: P2
OBJECTIVE:
Hold the ExecutingBatch application lifecycle state lease for the entire duration of a batch, preventing premature transitions to Idle between serial actions.

TARGET FILES:
- `TidyUp/Services/Processing/BatchProcessingCoordinator.cs`
- `TidyUp/Services/State/ApplicationStateManager.cs`
- `TidyUp.Tests/Unit/Services/BatchCancellationAndPauseTests.cs`

IN SCOPE:
- In `BatchProcessingCoordinator.ExecuteBatchAsync`:
  - Acquire batch lifecycle lease: `_stateManager.TryTransitionTo(ApplicationLifecycleState.ExecutingBatch)`.
  - Do NOT allow individual serial action completions (`NotifyOperationCompleted()`) to transition the application back to `Idle` while `i < actions.Count`.
  - Release the owned batch lease in `finally` after execution and outcome persistence/reconciliation. Derive the resulting state from remaining activity; never force Idle over another owner or conceal an uncertain outcome.
- Make batch execution admission atomic via `Interlocked.CompareExchange` on an execution guard flag.

OUT OF SCOPE:
- Multi-threaded concurrent batch execution (batches execute sequentially and cooperatively).

DONE WHEN:
- Application state remains `ExecutingBatch` continuously without flickering to `Idle` between individual files.
- Simultaneous calls to `ExecuteBatchAsync` are safely rejected.
- Unit tests verify state stability throughout batch execution.
```

**Additional acceptance and regression requirements:** Use one application-wide execution admission owner for manual, watcher and scheduled runs. A per-coordinator flag is insufficient if multiple instances exist. Cancellation is cooperative between safe boundaries; finally must not release another run’s lease. Test reentrancy, simultaneous entry points and exceptions.

---

### TASK-REM-03: Permanent Deletion Authorization Decoupling
```text
TASK ID: TASK-REM-03
REVIEW DEFECTS: TU-19 (P1)
PRIORITY: P1
OBJECTIVE:
Decouple confirmed execution authorization from the persisted ConfirmBeforeDelete checkbox so approved permanent deletions execute without runtime rejection.

TARGET FILES:
- `TidyUp/Services/ActionExecutor.cs`
- `TidyUp/Services/FileSystem/WindowsShellFileOperations.cs`
- `TidyUp/Services/IActionExecutor.cs`
- `TidyUp/Models/Domain/PlannedFileAction.cs`
- `TidyUp.Tests/Unit/Services/SafeDeleteTests.cs`

IN SCOPE:
- Add an execution authorization context bound to PlanId, plan revision, selected ActionIds, destructive effects and resolved paths. It is issued by the confirmation/policy boundary, never read from persisted rule preferences.
- When called from `BatchProcessingCoordinator` after the user has confirmed the Safeguard confirmation dialog, pass the scoped authorization context for exactly the confirmed effects.
- In `WindowsShellFileOperations.DeleteFileSafelyAsync`: Allow permanent deletion when `useRecycleBin == false` AND the authorization context is valid for this action and its current preconditions, without requiring `ConfirmBeforeDelete == false`.
- Preserve `ConfirmBeforeDelete` strictly as a user configuration preference (whether to prompt), not an execution authorization token.

OUT OF SCOPE:
- Altering the Windows Shell COM deletion API.

DONE WHEN:
- A rule with `UseRecycleBin = false` and `ConfirmBeforeDelete = true` successfully permanently deletes the file after user approves the safeguard dialog.
- Missing, stale, mismatched or replayed authorization fails closed. Changing a preference cannot authorize an operation.
- Unit tests verify both authorized execution and unauthorized rejection.
```

**Additional acceptance and regression requirements:** Test consent for one action cannot authorize another action, changed destination, new plan revision or background event. Automatic destructive behavior requires an explicitly defined opt-in policy; otherwise queue for review. Never infer consent from IsEnabled.

---

### TASK-REM-04: Overwrite & Prompt Safe Staging & Displaced Backup
```text
TASK ID: TASK-REM-04
REVIEW DEFECTS: TU-17 (P1)
PRIORITY: P1
OBJECTIVE:
Prevent destination data loss during Overwrite operations by staging atomic replacements and capturing displaced files; fail closed for unimplemented Prompt policy.

TARGET FILES:
- `TidyUp/Services/ActionExecutor.cs`
- `TidyUp/Services/Rollback/RollbackEngine.cs`
- `TidyUp.Tests/Unit/Services/ActionExecutorTests.cs`

IN SCOPE:
- In `ActionExecutor`: When `ConflictResolution.Overwrite` is active:
  - Do NOT delete destination file before move/copy succeeds.
  - Use a tested same-volume staging/replacement strategy where available. Define a separate recoverable cross-volume procedure; do not describe cross-volume move/copy as atomic. Retain displaced bytes in a durable recovery location outside watched scopes.
  - Before any mutation, durably journal the intent and displaced-file recovery location; after success, persist the verified result. Use the contract implemented by TASK-REM-22.
  - If move/copy fails, restore the displaced destination file immediately.
- In `ResolveConflictAsync`:
  - If strategy is `ConflictResolution.Prompt`, do NOT return `destinationPath` blindly. Fail closed with `ActionResultType.Warning` ("Interactive prompt conflict resolution not supported in automated batch; operation skipped").
- For Copy operations: Ensure copy with overwrite follows the same backup staging logic.

OUT OF SCOPE:
- Interactive modal dialogs popping up inside headless background execution loops.

DONE WHEN:
- A failed move/copy with Overwrite does not delete the existing destination file.
- Displaced destination files are backed up and recorded in the journal.
- Prompt strategy skips safely rather than silently overwriting.
- Unit tests prove zero data loss on simulated disk error during overwrite.
```

**Additional acceptance and regression requirements:** Test failure before staging, during copy, during destination replacement, after mutation but before journal outcome, and during restoration. Verify both original and displaced bytes remain recoverable. Backups survive process restart and are never purged by ordinary log retention. Cover RenameOld and case-only renames.

---

### TASK-REM-02: Archive Extraction Safeguards & Overwrite Containment
```text
TASK ID: TASK-REM-02
REVIEW DEFECTS: TU-18 (P1)
PRIORITY: P1
OBJECTIVE:
Enforce destructive-action safeguards for archive extraction, recycle archives by default instead of permanently deleting them, and simulate extraction risks in preview.

TARGET FILES:
- `TidyUp/Services/ActionExecutor.cs`
- `TidyUp/Services/Simulation/ExecutionPlanGenerator.cs`
- `TidyUp/ViewModels/Dialogs/SafeguardConfirmationViewModel.cs`
- `TidyUp/Models/Domain/FileAction.cs` (ExtractArchiveAction)
- `TidyUp.Tests/Unit/Services/ActionExecutorTests.cs`

IN SCOPE:
- In ExtractArchiveAction: Add `UseRecycleBinForArchiveDeletion` property, defaulting to `true`.
- In ActionExecutor.ExecuteExtractArchiveAsync:
  - If `DeleteAfterExtraction` is true, use `_safeFileSystem.DeleteFileSafelyAsync` (Recycle Bin by default), NEVER `File.Delete`.
  - Check if any extracted files would overwrite existing files. If `OverwriteExisting` is true, treat as destructive overwrite.
- In ExecutionPlanGenerator.SimulateAction:
  - For ExtractArchiveAction, flag extraction target collisions if destination directory already contains files that match archive contents using a read-only archive manifest; an unreadable or unsupported manifest blocks Apply rather than guessing from a non-empty directory.
- In SafeguardConfirmationViewModel.EvaluateRisks:
  - Inspect ExtractArchiveAction items: flag risk if `DeleteAfterExtraction` is true or if `OverwriteExisting` is true.

OUT OF SCOPE:
- Full archive decompression during dry-run simulation.

DONE WHEN:
- Extracting with `DeleteAfterExtraction = true` moves archive to Recycle Bin, not permanent deletion.
- SafeguardConfirmationDialog is triggered when an extraction specifies overwrite or archive deletion.
- Unit tests verify safe deletion of archive and safeguard risk evaluation.
```

**Additional acceptance and regression requirements:** Inspect archive entries without extracting. Reject absolute paths, .. traversal, drive-qualified names and reparse-point escapes; preflight all target collisions. Partial extraction must never delete the source archive. Set explicit resource limits; unsupported archives fail closed. Include corrupt archive, malicious entry and disk-full fixtures.

---

### TASK-REM-05: Monitored Root Boundary Clamping for Empty Folder Cleanup
```text
TASK ID: TASK-REM-05
REVIEW DEFECTS: TU-32 (P1)
PRIORITY: P1
OBJECTIVE:
Ensure empty folder cleanup strictly respects monitored root boundaries and never ascends above the configured source directory.

TARGET FILES:
- `TidyUp/Services/ActionExecutor.cs`
- `TidyUp/Services/IActionExecutor.cs`
- `TidyUp.Tests/Unit/Services/ActionExecutorTests.cs`

IN SCOPE:
- Update `RemoveEmptyFoldersAsync` in `ActionExecutor` to require `string monitoredRootPath`.
- In `ExecuteMoveAsync` and `ExecuteDeleteAsync`: Pass the rule's monitored folder root path as `stopBoundaryPath`.
- Verify clamping: Ensure `currentDir` stops when `currentDir.FullName` equals or is outside `stopBoundaryPath`.
- Under no circumstances allow deletion of the monitored folder root itself or any ancestor directory.

OUT OF SCOPE:
- Deleting non-empty directories.

DONE WHEN:
- Empty folder cleanup stops exactly at the monitored root folder.
- Monitored folder root itself is never deleted even when empty.
- Unit test verifies nested folder cleanup with empty ancestors stops at boundary.
```

**Additional acceptance and regression requirements:** Test drive/UNC roots, sibling prefix paths (Root vs Root2), path casing, separators and junction/symlink escapes. Cleanup accepts the matched rule root, records planned directory effects, rechecks emptiness immediately before delete, and never follows a boundary escape.

---

### TASK-REM-01: Truthful Rollback & Recovery Contract
```text
TASK ID: TASK-REM-01
REVIEW DEFECTS: TU-20 (P1), TU-38 (P2)
PRIORITY: P1
OBJECTIVE:
Ensure RollbackEngine and HistoryViewModel never claim successful restoration when no restoration occurred, and accurately differentiate between Restored, ManualRestoreRequired, and Irreversible operations.

TARGET FILES:
- `TidyUp/Services/Rollback/RollbackEngine.cs`
- `TidyUp/ViewModels/HistoryViewModel.cs`
- `TidyUp.Tests/Unit/Services/RollbackEngineTests.cs`

IN SCOPE:
- Replace boolean Success return in RollbackResult with an explicit RollbackOutcome enum:
  - `Restored`: The operation-specific inverse completed and its postconditions were verified. A copied output is removed only if unchanged; a moved file is returned only if doing so cannot overwrite unrelated data.
  - `ManualRestoreRequired`: File was moved to Recycle Bin; user must restore via Windows Shell. Provide clear step-by-step guidance.
  - `NotRecoverable`: Operation cannot be automatically undone (ExtractArchive, RunCommand, permanent delete, or modified target).
- In RollbackEngine.RollbackDeleteAsync: Return `ManualRestoreRequired` if deleted via Recycle Bin. Return `NotRecoverable` with explicit error if deleted permanently. Never return `Restored` for delete.
- In HistoryViewModel: Update status to "Restored" ONLY if outcome is `Restored`. If `ManualRestoreRequired`, display a popup with exact Recycle Bin restoration instructions and keep status as "Manual Action Required" (do NOT mark database entry as "RolledBack").
- In HistoryRowItemViewModel.CanRollback: Disable Undo button for ExtractArchive, RunCommand, and permanent deletions.

OUT OF SCOPE:
- Native Windows Recycle Bin programmatic COM restoration.
- Modifying OperationJournal database schema.

DONE WHEN:
- Performing Undo on a deleted file never marks the database record as "RolledBack".
- Undo button is disabled for ExtractArchive, RunCommand, and permanent deletion rows.
- RollbackEngineTests verify distinct outcomes for Move, Copy, RecycleBin delete, and Permanent delete.
- Focused recovery regressions pass; the phase gate runs the affected integration suite.
```

**Additional acceptance and regression requirements:** Test unchanged and externally modified copy outputs, missing source, occupied original path and repeated Undo. Restored means the inverse postcondition, not that every action literally copies bytes back. Manual guidance is not an automatic rollback capability.

---

### TASK-REM-23: Authoritative Discrepancy Reporting from Batch Results
```text
TASK ID: TASK-REM-23
REVIEW DEFECTS: TU-23 (P2)
PRIORITY: P2
OBJECTIVE:
Feed actual BatchExecutionResult outcomes directly into ExecutionDiscrepancyAnalyzer, correlating by planned action ID rather than UI presentation rows.

TARGET FILES:
- `TidyUp/ViewModels/PreviewViewModel.cs`
- `TidyUp/Services/Diagnostics/ExecutionDiscrepancyAnalyzer.cs`
- `TidyUp/Services/Diagnostics/IExecutionDiscrepancyAnalyzer.cs`
- `TidyUp.Tests/Unit/Services/ExecutionDiscrepancyAnalyzerTests.cs`

IN SCOPE:
- Update `AnalyzeFromResults` in `ExecutionDiscrepancyAnalyzer` to correlate actions using `PlannedFileAction.Id` (Guid), NOT `SourcePath` strings.
- In `PreviewViewModel.ApplyChangesAsync`:
  - Pass `batchResult.ItemResults` directly into `_discrepancyAnalyzer.AnalyzeFromResults`.
  - Do NOT reconstruct synthetic `ActionResult` objects from UI viewmodel properties.
- Display actual renamed target paths in the Execution Summary dialog.

OUT OF SCOPE:
- Modifying UI styling of `ExecutionSummaryWindow.xaml`.

DONE WHEN:
- Discrepancy report reflects actual executor results and actual renamed paths.
- Multiple actions on a single source file correlate independently by Action ID.
- Unit tests verify discrepancy analysis using `BatchItemExecutionResult`.
```

**Additional acceptance and regression requirements:** One action ID maps to one recorded attempt/outcome; show actual targets and partial/uncertain effects. Do not correlate by source path or manufacture success from grid row state. Include multi-action same-source and skipped predecessor fixtures.

---

### TASK-REM-21: Post-Execution Selection & Apply Button Eligibility Predicate
```text
TASK ID: TASK-REM-21
REVIEW DEFECTS: TU-24 (P2)
PRIORITY: P2
OBJECTIVE:
Harmonize the CanApplyChanges predicate and ApplyButtonText so the Apply button disables immediately when all approved actions finish executing.

TARGET FILES:
- `TidyUp/ViewModels/PreviewViewModel.cs`
- `TidyUp.Tests/Unit/ViewModels/PreviewViewModelTests.cs`

IN SCOPE:
- Define single source of truth for pending unexecuted items:
  Use one domain eligibility predicate: selected, pending, complete dependency closure, current plan revision, passed preflight, authorized effects, no unresolved conflict, and no active execution lease. Count eligible pending actions; do not infer retryability from `!IsExecuted`.
- In `CanApplyChanges`:
  Apply is enabled only when the same eligibility service returns at least one executable approved action and the scan policy permits execution.
- In `ApplyButtonText`:
  Format caption using `PendingApprovedCount` instead of raw `ApprovedCount`.
- After batch execution completes: Notify property changes for `CanApplyChanges` and `ApplyButtonText`, ensuring completed actions cannot be replayed. Show separate succeeded, failed, skipped, cancelled and uncertain counts; never claim all changes applied if some failed.

OUT OF SCOPE:
- Modifying DataGrid row virtualization.

DONE WHEN:
- Apply button immediately disables when all approved actions complete.
- Caption states all approved changes applied only when all approved effects succeeded; failures/uncertain actions show their actual status and require a fresh plan or reconciliation.
- Unit tests verify button state after partial and full execution.
```

**Additional acceptance and regression requirements:** Test mixed success/failure/skip/cancel/uncertain outcomes and selection changes. Disable reapply for terminal rows; use explicit fresh-plan retry. Recompute eligibility whenever plan validity, authorization, dependency status or lifecycle state changes.

---

### TASK-REM-25: Watcher Cancellation Token Lifecycle & Restart Resilience
```text
TASK ID: TASK-REM-25
REVIEW DEFECTS: TU-34 (P1)
PRIORITY: P1
OBJECTIVE:
Fix UpdateRulesAsync in FileMonitorService so monitoring restarts instantiate a fresh internal CancellationTokenSource instead of binding to an already-cancelled token.

TARGET FILES:
- `TidyUp/Services/FileMonitorService.cs`
- `TidyUp.Tests/Unit/Services/FileMonitorServiceTests.cs`

IN SCOPE:
- In `FileMonitorService`:
  - Store external application lifetime token (`_applicationStoppingToken`) separately from internal run-loop token (`_runCts`).
  - Serialize Start/Update/Stop transitions. Stop cancels the run token, awaits producers/consumers and detaches handlers before disposing `_runCts`; shutdown cannot race into a fresh start.
  - In `StartAsync()`: Create a fresh `_runCts = CancellationTokenSource.CreateLinkedTokenSource(_applicationStoppingToken)`.
  - In `UpdateRulesAsync()`: Ensure `StopAsync()` followed by `StartAsync()` starts with a healthy, non-cancelled token source.

OUT OF SCOPE:
- Rewriting underlying `HardenedFileSystemWatcher`.

DONE WHEN:
- Calling `UpdateRulesAsync` successfully restarts watchers and resumes event processing.
- Linked token is non-cancelled after restart.
- Unit tests verify start → update → restart → stop cycle.
```

**Additional acceptance and regression requirements:** Test repeated start/update/stop, concurrent shutdown/update, token cancellation, handler duplication, stale generation callbacks and event delivery after restart. Watcher overflow/error must trigger bounded reconciliation with truthful health, not silent dropped monitoring.

---

### TASK-REM-26: Multilateral Watcher Scope Merging for Overlapping Folders
```text
TASK ID: TASK-REM-26
REVIEW DEFECTS: TU-35 (P2)
PRIORITY: P2
OBJECTIVE:
Build watcher folder scope as a superset of all linked rules, avoiding DistinctBy(Path) clobbering subfolder recursion or exclusion scopes.

TARGET FILES:
- `TidyUp/Services/FileMonitorService.cs`
- `TidyUp.Tests/Unit/Services/FileMonitorServiceTests.cs`

IN SCOPE:
- In `FileMonitorService.ConfigureWatchers`:
  - When multiple rules watch the same directory:
    - `IncludeSubfolders` must be `true` if ANY rule requires subfolder monitoring.
    - Exclusions must NOT be applied globally at the watcher level if another rule needs those files.
  - Apply individual rule exclusion patterns and subfolder filters during rule dispatch in `OnFileDetected`, not at the shared watcher level.
  - Use centralized Windows-aware canonicalization; preserve drive/UNC roots and separators. Revalidate reparse-point boundaries; do not implement confinement with string-prefix comparison or unconditional trailing-slash trimming.

OUT OF SCOPE:
- Kernel filesystem filter drivers.

DONE WHEN:
- Rule A (top-level only) and Rule B (recursive) on the same folder correctly watch subfolders for Rule B without breaking Rule A.
- Unit tests verify watcher configuration with conflicting subfolder and exclusion parameters.
```

**Additional acceptance and regression requirements:** Test nested overlapping roots, conflicting excludes, recursive/top-level rules and pause of one shared folder scope. A shared watcher is an observation superset; each dispatch still checks individual rule eligibility and confinement.

---

### TASK-REM-24: Application Monitoring Coordinator & Startup Activation
```text
TASK ID: TASK-REM-24
REVIEW DEFECTS: TU-01 (P1)
PRIORITY: P1
OBJECTIVE:
Create an application-owned monitoring coordinator that loads enabled rules at startup, activates FileMonitorService, processes detected files safely, and responds to rule changes.

TARGET FILES:
- `TidyUp/Services/Monitoring/IMonitoringCoordinator.cs` (New)
- `TidyUp/Services/Monitoring/MonitoringCoordinator.cs` (New)
- `TidyUp/App.xaml.cs`
- `TidyUp/ServiceConfiguration.cs`
- `TidyUp/ViewModels/MainWindowViewModel.cs`
- `TidyUp.Tests/Integration/UiWorkflowEndToEndIntegrationTests.cs`

IN SCOPE:
- Create `MonitoringCoordinator`:
  - Implements `IHostedService` or application-managed singleton.
  - Subscribes to `IFileMonitorService.FileDetected`.
  - On `FileDetected`: enqueue a deduplicated candidate, evaluate eligible rule revisions, generate an immutable plan and use the SAME preflight, authorization, batch admission, executor, journal and result pipeline as manual Apply. Never call IActionExecutor directly from watcher callbacks.
  - On startup: load validated settings and migrated rules, then activate only explicitly opted-in eligible rules. Since v1.2.0 automation was unwired, legacy enabled rules require a one-time activation review before automatic mutation. Fresh imports remain disabled.
  - On rule create/update/toggle/delete: Refreshes active monitoring scope via `UpdateRulesAsync()`.
  - On shutdown (`App.OnExit`): Gracefully awaits in-flight file operations and calls `StopAsync()`.
- Register `IMonitoringCoordinator` in `ServiceConfiguration.cs`.

OUT OF SCOPE:
- Running TidyUp as a standalone Windows Background NT Service (it runs within user desktop app session).

DONE WHEN:
- Saving persists a rule without silently granting automation permission. Explicit enable/activation updates real monitoring; an already opted-in rule revision is revalidated before use.
- Dropping a matching file into the monitored folder automatically triggers rule evaluation and action execution.
- App shutdown cleanly cancels and awaits file monitor completion.
- Automated integration test verifies startup monitor activation.
```

**Additional acceptance and regression requirements:** Activation gate requires all Phase 4 safety tests. Manual Scan never injects executable events. Deduplicate noisy events, bound queues, retry locked files according to settings and prevent output/recovery-folder loops. Stop-processing and priority semantics must match manual planning. Dispose application-owned subscriptions on shutdown.

---

### TASK-REM-07: Truthful Folders Management & Persistent Catalog Actions
```text
TASK ID: TASK-REM-07
REVIEW DEFECTS: TU-02 (P1), TU-03 (P1)
PRIORITY: P1
OBJECTIVE:
Ensure Add Folder and Pause/Resume in Folders view persist to the shared folder catalog and reflect real monitoring state rather than transient UI rows.

TARGET FILES:
- `TidyUp/ViewModels/FoldersViewModel.cs`
- `TidyUp.Core/Folders/IMonitoredFolderCatalog.cs`
- `TidyUp.Data/Repositories/IRuleRepository.cs`
- `TidyUp.Tests/Unit/ViewModels/FoldersViewModelTests.cs`

IN SCOPE:
- In `FoldersViewModel.AddMonitoredFolderAsync`:
  - Persist the added directory to `IMonitoredFolderCatalog` using the persistent standalone folder ownership model selected in TASK-BASE-02.
  - Provide option or guidance: "Create a rule for this folder" or save to active rule.
  - Ensure `LoadFoldersAsync` retains the added folder upon Refresh or view navigation.
- In `FoldersViewModel.TogglePauseWatcherAsync`:
  - Wire command directly to `IFileMonitorService.PauseFolder(path)` / `ResumeFolder(path)`.
  - Persist folder paused state in `IMonitoredFolderCatalog` with a documented restart policy, distinct from temporary global pause.
  - Derive badge and status directly from acknowledged monitor state.

OUT OF SCOPE:
- Changing Windows 11 Fluent styling of the Folders table.

DONE WHEN:
- Adding a folder in Folders view persists across Refresh and app restart.
- Pausing a folder invokes monitor pause and survives page refresh.
- Unit tests verify folder persistence and monitoring pause dispatch.
```

**Additional acceptance and regression requirements:** Standalone folders have stable IDs and durable configured state even without rules. Configured, watching, paused, disconnected and error are distinct. Monitor acknowledgment drives the runtime badge. Failed persistence/pause leaves a truthful error and reverts optimistic UI. Test restart, refresh and overlapping rule scopes.

---

### TASK-REM-29: Window Lifecycle & Close/Minimize Conflict Resolution
```text
TASK ID: TASK-REM-29
REVIEW DEFECTS: TU-41 (P2)
PRIORITY: P2
OBJECTIVE:
Harmonize window closing, minimize-to-tray, and exit lifecycle so unsaved discard prompts are respected and window state persistence is preserved.

TARGET FILES:
- `TidyUp/App.xaml.cs`
- `TidyUp/MainWindow.xaml.cs`
- `TidyUp/ViewModels/MainWindowViewModel.cs`

IN SCOPE:
- In `App.MainWindow_Closing`:
  - Check `e.Cancel`: if `MainWindow.Closing` already cancelled (e.g. user declined to discard unsaved edits), abort immediately and keep window visible.
- In `App.OnStartup`:
  - Restore saved `WindowState` (Maximized/Normal) cleanly without overriding it with a hard-coded `WindowState = WindowState.Normal` after `Show()`.
- In Tray Exit handler:
  - Check if `BatchProcessingCoordinator` is currently executing. If busy, prompt user or wait for current atomic file operation to complete before calling `Shutdown()`.

OUT OF SCOPE:
- Windows service headless operation.

DONE WHEN:
- Declining discard on close keeps the window open.
- Maximized window state persists across restarts.
- Tray exit waits for in-flight atomic file action to finish cleanly.
```

**Additional acceptance and regression requirements:** Use one async close coordinator rather than relying on event-handler registration order or e.Cancel alone. Cover close-to-tray, tray disabled, explicit Exit, dirty drafts, busy run and shutdown timeout. Cancel keeps the app visible. Restore geometry within available displays; minimized startup must remain discoverable.

---

### TASK-REM-30: Aggregate Tray Icon Health & Native Handle Management
```text
TASK ID: TASK-REM-30
REVIEW DEFECTS: TU-43 (P2)
PRIORITY: P2
OBJECTIVE:
Compute tray icon status from aggregate folder health, retain branded tray icon identity with status badges, and properly dispose native icon handles.

TARGET FILES:
- `TidyUp/Services/TrayIconService.cs`
- `TidyUp.Tests/Unit/Services/TrayIconServiceTests.cs`

IN SCOPE:
- In `TrayIconService`:
  - Maintain dictionary of monitored folder health states: `_folderStates`.
  - Calculate aggregate health: If ANY folder is `Error` or `Disconnected`, show Warning/Error badge. Return to Normal ONLY when all folders are healthy.
  - Retain the branded multi-resolution TidyUp application icon as the tray base, drawing a subtle status dot overlay rather than replacing it with a blank solid dot.
  - Define native handle ownership. Destroy only owned native handles once, after cloning/retaining a managed Icon as appropriate. Dispose owned managed icons separately; never DestroyIcon a shared resource handle or double-free a handle.

OUT OF SCOPE:
- Custom animated GIF tray icons.

DONE WHEN:
- A single disconnected folder keeps the tray in warning state even if other folders are healthy.
- Branded icon is retained in tray.
- Zero GDI handle leaks on repeated status changes.
- Unit tests verify aggregate health calculation.
```

**Additional acceptance and regression requirements:** Define precedence for stopped, paused, working, disconnected and error states; healthy events from one folder cannot clear another error. Verify owned native handle count stabilizes after repeated updates/disposal, allowing measurement noise rather than claiming literal zero from a unit test.

---

### TASK-REM-31: Operation Stream Convergence for Dashboard & Logs
```text
TASK ID: TASK-REM-31
REVIEW DEFECTS: TU-22 (P1)
PRIORITY: P1
OBJECTIVE:
Connect executed file operations to Dashboard metrics and Logs, publishing a unified operation stream and updating rule execution statistics.

TARGET FILES:
- `TidyUp/Services/ActionExecutor.cs`
- `TidyUp/Services/Diagnostics/IOperationTelemetryService.cs` (New)
- `TidyUp/Services/Diagnostics/OperationTelemetryService.cs` (New)
- `TidyUp/ViewModels/DashboardViewModel.cs`
- `TidyUp/ViewModels/LogsViewModel.cs`
- `TidyUp/Data/Repositories/RuleRepository.cs`
- `TidyUp.Tests/Unit/ViewModels/DashboardViewModelTests.cs`

IN SCOPE:
- Create `OperationTelemetryService`:
  - Whenever an action executes (in preview apply or automatic monitoring), publish execution event: `RuleId`, `RuleName`, `ActionType`, `SourcePath`, `TargetPath`, `Outcome`, `ErrorMessage`, `BatchId`, `ActionId`, `RuleRevisionId` and timestamp. Publish only committed outcomes, with replay/idempotency for missed notifications.
  - Derive metrics from durable outcomes with ActionId deduplication. Define FilesProcessedCount as distinct successfully processed input files per rule run, not action attempts; a Rename + Move chain must not count twice. Track attempts/failures separately.
  - Write corresponding `ActionLogEntity` for general log viewer.
- In `DashboardViewModel`:
  - Subscribe to execution telemetry stream and update metrics live (total organized, success rate, recent activity).
- In `LogsViewModel`:
  - Display executed actions alongside system events.

OUT OF SCOPE:
- Remote syslog / telemetry export.

DONE WHEN:
- Applying changes in Preview immediately updates Dashboard statistics and recent activity feed.
- Rule `LastRunDate` and `FilesProcessedCount` update after execution.
- Unit tests verify telemetry publishing and metric updates.
```

**Additional acceptance and regression requirements:** Metrics survive restart and can be rebuilt from durable facts. Separate file and action counts; document success-rate denominator and timezone/date boundaries. Notifications are not the authoritative journal. Test replay, missed events and duplicate delivery.

---

### TASK-REM-32: History Pagination Command Notification & Eligibility
```text
TASK ID: TASK-REM-32
REVIEW DEFECTS: TU-36 (P2)
PRIORITY: P2
OBJECTIVE:
Add explicit command notifications to History pagination so Next and Previous buttons update their enabled/disabled state correctly on data load.

TARGET FILES:
- `TidyUp/ViewModels/HistoryViewModel.cs`
- `TidyUp.Tests/Unit/ViewModels/HistoryViewModelTests.cs`

IN SCOPE:
- In `HistoryViewModel`:
  - On `CurrentPage`, `PageSize`, or `TotalRecords` change:
    - Notify `NextPageCommand.NotifyCanExecuteChanged()`.
    - Notify `PreviousPageCommand.NotifyCanExecuteChanged()`.
  - Ensure initial data load notifies pagination command states after calculating `TotalPages`.

OUT OF SCOPE:
- Infinite scrolling virtualization in HistoryView.

DONE WHEN:
- Next and Previous page buttons correctly enable and disable as pages are traversed or data loads.
- Unit tests verify command eligibility transitions.
```

**Additional acceptance and regression requirements:** Test zero rows, last-page deletion, changed page size, filters reducing total pages and asynchronous out-of-order queries. Reset/clamp the page correctly and ignore stale query completions.

---

### TASK-REM-33: Context-Sensitive Filtered History Export
```text
TASK ID: TASK-REM-33
REVIEW DEFECTS: TU-37 (P2)
PRIORITY: P2
OBJECTIVE:
Ensure CSV and JSON exports from HistoryView respect currently active filters and offer explicit "Export Filtered" vs "Export All" options.

TARGET FILES:
- `TidyUp/ViewModels/HistoryViewModel.cs`
- `TidyUp/Views/HistoryView.xaml`
- `TidyUp.Tests/Unit/ViewModels/HistoryViewModelTests.cs`

IN SCOPE:
- In `HistoryViewModel`:
  - Update `ExportCsvAsync` and `ExportJsonAsync` to apply the active search text, date range, and status filters to the export query.
  - Provide UI options: "Export Filtered Records ({n})" and "Export All History".
  - Export Filtered means ALL records matching the active filters across pages, with stable ordering/snapshot semantics; it does not mean only the visible page. Export All ignores filters explicitly. Quote CSV fields and apply a documented spreadsheet-formula safety policy.

OUT OF SCOPE:
- Exporting to proprietary Excel (.xlsx) binary format.

DONE WHEN:
- Exporting history when a search filter is applied exports only matching records.
- Export count matches the displayed filtered count.
- Unit tests verify filtered export queries.
```

**Additional acceptance and regression requirements:** Test more than one page of matching records, no matches, Unicode, commas, quotes/newlines and formula-like CSV values. Stream large exports, support cancellation, and never leave a partial output named as a successful complete export.

---

### TASK-REM-06: Genuine Database Maintenance & Vacuum
```text
TASK ID: TASK-REM-06
REVIEW DEFECTS: TU-27 (P2)
PRIORITY: P2
OBJECTIVE:
Replace the simulated Task.Delay in SettingsViewModel.VacuumDatabaseAsync with real SQLite database VACUUM execution and space measurement.

TARGET FILES:
- `TidyUp/ViewModels/SettingsViewModel.cs`
- `TidyUp.Data/TidyUpDbContext.cs`
- `TidyUp.Data/Recovery/IDbIntegrityChecker.cs`
- `TidyUp.Tests/Unit/ViewModels/SettingsViewModelTests.cs`

IN SCOPE:
- Add a typed maintenance result API to the existing canonical database maintenance service (reuse an established owner where possible):
  - Measure database file size before vacuum.
  - Execute `PRAGMA wal_checkpoint(TRUNCATE);` followed by `VACUUM;` via raw SQL on SQLite connection.
  - Measure database file size after vacuum.
  - Return structured success/failure with measured main-database, WAL and SHM sizes before/after and duration. Reclaimed bytes may be zero; file size reduction alone is not proof of success.
- In `SettingsViewModel.VacuumDatabaseAsync`:
  - Await the real maintenance operation and inspect its structured result.
  - Report exact bytes or kilobytes reclaimed in status message.

OUT OF SCOPE:
- Modifying EF Core migration tables.

DONE WHEN:
- Clicking "Optimize Database" executes real SQLite VACUUM and reports actual reclaimed bytes.
- Zero `Task.Delay(300)` simulation code remains.
- Real SQLite tests verify success, busy database, insufficient space and cancellation/error reporting under the maintenance lease. Never vacuum within an active transaction.
```

**Additional acceptance and regression requirements:** Acquire a maintenance lease excluding conflicting app database work; honor SQLite busy behavior and cancellation. Report real success/failure rather than a fixed delay or assumed reclaimed bytes. Measure main/WAL/SHM consistently and explain size increases.

---

### TASK-REM-35: Settings Surface Audit & Inert Control Containment
```text
TASK ID: TASK-REM-35
REVIEW DEFECTS: TU-26 (P1)
PRIORITY: P1
OBJECTIVE:
Audit all settings in SettingsView: wire functional properties into runtime services, and remove or visibly disable inert controls with honest explanations.

TARGET FILES:
- `TidyUp/Views/SettingsView.xaml`
- `TidyUp/ViewModels/SettingsViewModel.cs`
- `TidyUp/Models/AppSettings.cs`
- `TidyUp.Tests/Unit/ViewModels/SettingsViewModelTests.cs`

IN SCOPE:
- Inspect each control in Settings:
  - `MaxConcurrentOperations`: keep execution serial for this remediation. Disable the unsupported concurrency control and explain the fixed limit. Parallel execution requires a separate reviewed plan for dependencies, destination locking and recovery; do not add it here.
  - `ScanBatchSize`: Wire into `ExecutionPlanGenerator` and directory scanner.
  - `UseRecycleBinByDefault`: Wire as default property on newly created Delete actions.
  - `RequireDestructiveConfirmations`: governs prompt UX only within the approved policy. It never creates authorization or bypasses mandatory destructive-effect checks.
  - `DatabaseBackupPath`: Connect to `DatabaseBackupManager` (use configured path rather than ignoring it).
  - For any setting that cannot be wired: show it unavailable with an accurate limitation and record an unresolved item/product disposition. Do not promise a future release date. Hiding a control contains deception but does not fix its underlying feature.

OUT OF SCOPE:
- Redesigning the 5-section card layout of SettingsView.

DONE WHEN:
- Every enabled setting control in SettingsView directly modifies runtime application behavior.
- Database backup uses the user's configured backup path.
- Unit tests verify settings-to-behavior linkages.
```

**Additional acceptance and regression requirements:** Build a control→persisted field→runtime consumer→change timing→validation→test matrix for EVERY exposed setting. Include debounce/retries/network polling, notifications/tray, cleanup retention, theme, first-run, backup and window geometry. Log retention cannot delete recovery material for unfinished operations.

---

### TASK-REM-36: Component vs Feature Honest Matrix & Unwired Feature Stubs
```text
TASK ID: TASK-REM-36
REVIEW DEFECTS: TU-33 (P2)
PRIORITY: P2
OBJECTIVE:
Update docs and feature matrix to reflect honest component integration, and ensure unwired features (network polling, cron schedules) do not promise functionality in UI.

TARGET FILES:
- `docs/FEATURE_MATRIX.md`
- `README.md`
- `TidyUp/Views/RuleEditor/Stages/Stage3ActionsView.xaml`
- `TidyUp/Views/ScheduleSettingsView.xaml`

IN SCOPE:
- In `FEATURE_MATRIX.md`:
  - Mark features accurately: `[Verified]`, `[Component Implemented - Integration Pending]`, or `[Planned]`.
  - Verify network monitoring and schedules through the actual application lifecycle. Document verified capabilities and limitations; distinguish implemented services from reachable features and do not assume either working or broken status.
- In UI:
  - If a trigger has no dispatcher, disable activation and state "This trigger is not available; use manual Preview." Only say schedules run while TidyUp is open after an application-level test proves it. Do not invent a v1.3.0 commitment.

OUT OF SCOPE:
- Removing underlying `ScheduleManager` or `DuplicateDetector` engine code.

DONE WHEN:
- Documentation makes zero false claims about background automation.
- Feature matrix accurately differentiates tested components from end-to-end integrated workflows.
```

**Additional acceptance and regression requirements:** Inventory network polling, schedules, duplicate detection and every advertised component. Each retained feature has a reachable workflow and application-level test. Disabled/hidden is Contained, not Fixed. Do not add destructive duplicate cleanup as part of honesty remediation. If schedules are retained, test clock/DST, missed runs, pause and restart policy.

---

### TASK-REM-37: Dark Theme Glyph & Empty State Contrast Polish
```text
TASK ID: TASK-REM-37
PRIORITY: P2
OBJECTIVE:
Remediate black glyph contrast on dark circular badge backgrounds across empty states in Dashboard, Rules, Folders, Preview, and History in Dark theme.

TARGET FILES:
- `TidyUp/Controls/EmptyStateControl.xaml`
- `TidyUp/Styles/Colors.Dark.xaml`
- `TidyUp/Styles/AccessibilityStyles.xaml`
- `TidyUp.Tests/Unit/Theming/StyleGuardTests.cs`

IN SCOPE:
- In `EmptyStateControl.xaml`:
  - Ensure the central icon glyph uses `{DynamicResource Win11TextPrimaryBrush}` or `{DynamicResource Win11AccentBrush}`, NOT black or unstyled foreground.
  - Ensure the circular background badge uses `{DynamicResource Win11SubtleSurfaceBrush}` with visible contrast against dark card surfaces.
- Visually verify populated and empty states in both Light and Dark themes.

OUT OF SCOPE:
- Changing application vector branding icon.

DONE WHEN:
- Measure contrast using the applicable criterion: normal text at least 4.5:1, large text at least 3:1, and meaningful UI graphics/control boundaries at least 3:1 against adjacent colors. Record colors, ratios and element roles; decorative glyphs are not text.
- `StyleGuardTests` passes cleanly.
```

**Additional acceptance and regression requirements:** Extend visual QA to all screens in empty/loading/populated/error/disabled/busy states, keyboard traversal, visible focus, labels/automation names, screen reader semantics, 100/150/200% DPI and small windows. Check long paths, validation errors, column clipping and theme changes; use populated fixtures, not only style-string tests.

---

### TASK-REM-38: Full Windows Acceptance Test Execution & Composition Root Tests
```text
TASK ID: TASK-REM-38
PRIORITY: P1
OBJECTIVE:
Execute end-to-end Windows acceptance scenarios on isolated test profile, verifying fresh install, folder lifecycle, rule editing, multi-action chains, recovery, and fault injection.

TARGET FILES:
- `TidyUp.Tests/Integration/UiWorkflowEndToEndIntegrationTests.cs`
- `TidyUp.Tests/Integration/EndToEndFileActionTests.cs`
- `docs/release-verification-1.2.0.md`

IN SCOPE:
- Execute the discovered full suite; report pass/fail/skipped/not-run counts and compare to the recorded baseline. Test count is not acceptance evidence. Every required scenario below needs a concrete result.
- Add automated composition root tests:
  - Test real startup with `ServiceConfiguration.ConfigureServices()`.
  - Test `MonitoringCoordinator` activation and event handling.
  - Test complete `SimulateRuleAsync` → `ApplyChangesAsync` → `RollbackAsync` cycle on temporary disk fixtures.
- Document verification results in updated `docs/release-verification-1.2.0.md`.

OUT OF SCOPE:
- Testing on non-Windows platforms (WPF is Windows-specific).

DONE WHEN:
- All required checks pass on the recorded candidate SHA. New warnings are resolved; existing warnings have a reviewed baseline/disposition. Required skipped or unavailable Windows tests block their acceptance gate, regardless of other passing counts.
- Real composition root integration test passes on temp profile.
```

**Additional acceptance and regression requirements:** Run the acceptance matrix below against the actual DI graph and packaged candidate where applicable. Capture environment, exact command, exit code, SHA and evidence. A non-Windows build is not WPF acceptance. Reverify affected scenarios after fixes; invalidate evidence when its tested code changes.

---

### TASK-REM-39: Release Candidate Packaging, Provenance & Publication Handoff
```text
TASK ID: TASK-REM-39
REVIEW DEFECTS: Additional release/provenance review item
PRIORITY: P1
OBJECTIVE:
Prepare a reproducible, verified release candidate from the repaired commit without overwriting the historical v1.2.0 tag or publishing without authorization.

TARGET FILES:
- Existing packaging scripts and release workflows (discover actual paths)
- CHANGELOG.md and docs/release-verification-<candidate>.md

IN SCOPE:
- Inspect current tags, releases, version declarations and packaging workflows. Treat any prior 404 as an observation to recheck, not a current fact or authority to recreate a release.
- Preserve v1.2.0 and its original provenance. Select a new candidate version according to repository policy; if ambiguous, prepare artifacts and leave version selection as the concrete decision.
- Use the repository-supported Windows publish/package commands and runtime. Validate single-file/self-contained compatibility rather than assuming arbitrary dotnet flags work.
- Record commit SHA, tag/version, SDK/runtime, dependency inputs, build commands, SHA-256 checksums and packaged file inventory.
- Launch the packaged binary on a clean isolated Windows profile; verify first run, migration, preview, execution, recovery and shutdown. Source tests alone do not validate packaging.
- Prepare release notes listing verified fixes, migrations, activation changes, unresolved items and recovery limitations.
- Stop with a reviewable candidate and publication handoff unless the user has explicitly authorized publishing. A separate authorized publication step must verify the release tag points at the tested SHA and downloaded asset hashes match.

OUT OF SCOPE:
- Retagging v1.2.0; deleting or replacing historical release assets.
- Publishing, signing with unavailable credentials, merging, pushing or sending messages without applicable authorization.

DONE WHEN:
- Candidate artifacts and checksums exist; clean-profile packaged smoke tests pass on the recorded SHA.
- Release notes agree with the evidence and finding matrix.
- Publication status is explicit: Prepared / Authorized and Published / Blocked. Prepared is completion of this preparation task, not a claim of public release.
```

**Additional acceptance and regression requirements:** Check public release state at execution time. Authentication/visibility errors are not evidence a release does not exist. No automatic gh release create v1.2.0. Keep final publication a separately authorized action and record download verification only if actually performed.

---

## 5. Required acceptance scenarios

Every row needs a test/manual scenario ID, expected result, actual result, tested SHA and evidence. “Code looks correct” is not a result. Automated service checks and manual GUI checks complement each other.

| Scenario | Minimum assertions | Primary tasks |
| --- | --- | --- |
| Fresh profile and upgrade | No unexpected automation; settings loaded before consumers; migration preserves data; legacy rules await activation review | BASE, 10, 24, 28, 34 |
| Folder lifecycle | Add/refresh/restart persists; acknowledged pause/resume; no-rule folder state truthful; network/error state visible | 07, 24–26, 30 |
| Rule editing | Guided/Advanced parity; nested dirty tracking; Save/Discard/Cancel on navigation/close; failure retains edits; metadata preserved | 08, 09, 29 |
| Disabled draft preview | Matching disabled draft previews without persistence/activation; no disk writes | 12, 13, 18 |
| Plan invalidation | Rule/settings/selection/precondition change invalidates Apply and consent; old scan cannot replace new plan | 17–19, 21 |
| Action chains | Rename→Move, Copy→source/copy continuation, Delete terminal, command/archive semantics; dependency selection closure | 14, 16, 19 |
| Collision policies | Disk and same-batch collisions; case-only paths; Skip/RenameNew/RenameOld/Overwrite/Prompt; exact approved targets | 04, 15, 19 |
| Scoped authorization | Unconfirmed and mismatched permanent delete/overwrite rejected; correct plan consent accepted; background policy constrained | 03, 24 |
| Recovery safety | Move inverse; unchanged/changed copy output; occupied original; Recycle Bin manual restoration; irreversible effects honest | 01, 04, 22 |
| Archive/cleanup confinement | Traversal/reparse/root attacks, corrupt archive, partial extraction, boundaries, disk-full; no source archive deletion on failure | 02, 05 |
| Fault/crash windows | Intent failure, mutation failure, post-mutation journal failure, restart reconciliation, backup retention, no blind retry | 04, 20, 22 |
| Concurrent entry points | Manual Apply plus watcher/scheduler; one admission owner, no duplicate execution, stable busy state, safe cancellation | 20, 24–26 |
| Scan resilience | Large tree, inaccessible subtree, locked file, cancellation, sampling disclosure, read-only dashboard/folder parity | 17, 27 |
| Lifecycle | Dirty close Cancel remains visible; close-to-tray vs Exit; tray disabled; busy shutdown; display geometry/DPI | 29, 30, 37 |
| Result consistency | History/logs/dashboard/summary agree; files vs actions; duplicate event replay; no “all applied” after failure | 21–23, 31 |
| History/export | Page boundaries/filter changes; all filtered pages exported; stable order; CSV escaping/formula policy; cancellation | 32, 33 |
| Settings/maintenance | Every enabled control changes observed behavior; invalid values/corrupt JSON handled; real vacuum success/error | 06, 28, 34, 35 |
| Import/variables | Supported/unsupported schema, transactional failure, disabled import, complete round trip, documented tokens valid | 10, 11 |
| Feature reachability | Retained schedules/network/duplicates have real routes and evidence; unsupported triggers cannot activate | 24, 36 |
| GUI/accessibility | All screens populated/empty/error/busy; keyboard/focus/labels; light/dark; 100/150/200% DPI; long paths | 12, 37, 38 |
| Packaged application | Clean-profile launch, upgrade, core workflow and recovery from built binary; version and checksum match | 38, 39 |

### Fixture set

Create reusable fixture builders, not a pile of unmaintained hand-created files:

- **F01 Basic:** multiple extensions, matching/nonmatching files and source/destination roots.
- **F02 Chains:** ordered action sequences, selected subsets and two rules with priority/stop-processing.
- **F03 Conflicts:** occupied destinations, same-batch collisions, case-only names, locked targets and cross-volume setup.
- **F04 Recovery:** unchanged/modified copy outputs, displaced backup, occupied original and controlled recycle/permanent deletes.
- **F05 Archives/boundaries:** valid/corrupt archives, traversal entries, nested roots, sibling-prefix paths and reparse points.
- **F06 Variables:** fixed clock, counters, aliases, unknown tokens, reserved names and escaping patterns.
- **F07 Monitoring:** overlapping scopes, excludes, noisy events, output-loop prevention, overflow/reconnect and paused state.
- **F08 Persistence:** old/new schema, corrupt settings, interrupted save, batch import rollback and migration rollback/recovery.
- **F09 Scale/history:** enough files and records for multiple pages, cancellation and partial/error scans; no fixed test-count goal.
- **F10 Draft/UI:** nested edits, dirty navigation, validation failure, long names and empty/populated/error/busy states.
- **F11 Faults:** controllable failures before/after every durable/mutation boundary, process restart and journal reconciliation.
- **F12 Packaging/lifecycle:** fresh/upgraded profile, non-default DPI/theme, tray/close behavior and candidate binary.

## 6. Finding traceability and closure policy

Every TU finding retains its original identity. The following mapping is generated from the task catalog’s review references; it is a coverage map, not proof of resolution. TASK-BASE-01 must reconcile it with the original review and current checkout.

| Finding | Implementation owner(s) | Initial closure status |
| --- | --- | --- |
| TU-01 | TASK-REM-24 | NOT_REVALIDATED |
| TU-02 | TASK-REM-07 | NOT_REVALIDATED |
| TU-03 | TASK-REM-07 | NOT_REVALIDATED |
| TU-04 | TASK-REM-27 | NOT_REVALIDATED |
| TU-05 | TASK-REM-13 | NOT_REVALIDATED |
| TU-06 | TASK-REM-12 | NOT_REVALIDATED |
| TU-07 | TASK-REM-12 | NOT_REVALIDATED |
| TU-08 | TASK-REM-08 | NOT_REVALIDATED |
| TU-09 | TASK-REM-08 | NOT_REVALIDATED |
| TU-10 | TASK-REM-08 | NOT_REVALIDATED |
| TU-11 | TASK-REM-08 | NOT_REVALIDATED |
| TU-12 | TASK-REM-18 | NOT_REVALIDATED |
| TU-13 | TASK-REM-14 | NOT_REVALIDATED |
| TU-14 | TASK-REM-14 | NOT_REVALIDATED |
| TU-15 | TASK-REM-19 | NOT_REVALIDATED |
| TU-16 | TASK-REM-15 | NOT_REVALIDATED |
| TU-17 | TASK-REM-04 | NOT_REVALIDATED |
| TU-18 | TASK-REM-02 | NOT_REVALIDATED |
| TU-19 | TASK-REM-03 | NOT_REVALIDATED |
| TU-20 | TASK-REM-01 | NOT_REVALIDATED |
| TU-21 | TASK-REM-22 | NOT_REVALIDATED |
| TU-22 | TASK-REM-31 | NOT_REVALIDATED |
| TU-23 | TASK-REM-23 | NOT_REVALIDATED |
| TU-24 | TASK-REM-21 | NOT_REVALIDATED |
| TU-25 | TASK-REM-17 | NOT_REVALIDATED |
| TU-26 | TASK-REM-35 | NOT_REVALIDATED |
| TU-27 | TASK-REM-06 | NOT_REVALIDATED |
| TU-28 | TASK-REM-28 | NOT_REVALIDATED |
| TU-29 | TASK-REM-10 | NOT_REVALIDATED |
| TU-30 | TASK-REM-09 | NOT_REVALIDATED |
| TU-31 | TASK-REM-16 | NOT_REVALIDATED |
| TU-32 | TASK-REM-05 | NOT_REVALIDATED |
| TU-33 | TASK-REM-36 | NOT_REVALIDATED |
| TU-34 | TASK-REM-25 | NOT_REVALIDATED |
| TU-35 | TASK-REM-26 | NOT_REVALIDATED |
| TU-36 | TASK-REM-32 | NOT_REVALIDATED |
| TU-37 | TASK-REM-33 | NOT_REVALIDATED |
| TU-38 | TASK-REM-01 | NOT_REVALIDATED |
| TU-39 | TASK-REM-11 | NOT_REVALIDATED |
| TU-40 | TASK-REM-11 | NOT_REVALIDATED |
| TU-41 | TASK-REM-29 | NOT_REVALIDATED |
| TU-42 | TASK-REM-34 | NOT_REVALIDATED |
| TU-43 | TASK-REM-30 | NOT_REVALIDATED |
| TU-44 | TASK-REM-20 | NOT_REVALIDATED |

Findings use separate closure states: `NOT_REVALIDATED`, `CONFIRMED`, `CONTAINED`, `FIXED_UNVERIFIED`, `FIXED_VERIFIED`, `DISPROVED_WITH_EVIDENCE`, `DEFERRED_PRODUCT_DECISION`. A task can implement containment while its finding remains unresolved. No release can claim all findings fixed while any are merely contained, unverified or deferred.

Additional review concerns without a TU ID remain explicit: actual composition-root reachability (24/38), startup/migration activation policy (BASE-02/24), command preflight and execution limitations (BASE-02/36/38), complete visual/accessibility coverage (37/38), and release provenance (39). Do not invent TU-45+ and imply they came from the original review.

## 7. Tracking and handoff templates

Copy these templates into repository remediation records at BASE-01. Do not mark the plan itself DONE.

### Task tracker row

| Task ID | State | Dependencies/gate | Current SHA | Evidence | Open blocker | Next action |
| --- | --- | --- | --- | --- | --- | --- |
| TASK-BASE-01 | NOT_STARTED | None | Not recorded | None | None assessed | Inspect checkout/instructions and establish baseline |

Create one row for each of the 42 tasks. Child tasks use stable suffixes, such as TASK-REM-22.1. Keep the parent incomplete until all children and acceptance criteria pass.

### Completion record

```text
Task ID / title:
Code SHA and working-tree state:
Finding IDs and prior status:
Changed files and behavior:
Acceptance criterion -> check/scenario -> actual result -> evidence path:
Commands / exit codes / test counts including skipped:
Windows / SDK / runtime / profile / relevant fixture details:
Migration and compatibility result:
Scope amendments and decisions:
Known limitations / unresolved dependencies:
Finding closure status and justification:
Next eligible task / gate:
```

### Handoff after interruption or context reset

```text
Repository / branch / current SHA:
Plan revision and location:
Last verified phase gate:
Active task and current state:
Completed changes and uncommitted files:
Last executed checks and actual results:
Evidence paths and tested SHA:
Unresolved blocker or product decision:
Temporary containment still active:
Exact next command or implementation action:
Do not repeat / do not assume:
```

### Resume protocol

Read repository instructions, this contract, phase table, TRACKER, HANDOFF and relevant DECISIONS. Inspect git status/diff and verify the recorded SHA. Reconcile partial work before editing. Read only the active task and needed dependencies, then run the next concrete action. Do not restart the entire review, invent previous test results or skip a gate because a prior model wrote “done.” If the checkout changed, determine which evidence is invalid and rerun the affected checks.

## 8. Final completion and release-readiness rule

Implementation is complete only when all required tasks/gates are verified, the 44-finding matrix has evidence-backed dispositions, temporary containment has either been removed after repair or declared as a remaining limitation, and the packaged candidate is tested. Intentional feature deferral needs explicit product disposition and honest release notes; it must not be relabeled a fix.

A final implementation report must state: tested SHA/version; completed tasks/findings; remaining defects/limitations; exact verification performed and not performed; candidate artifact/checksum locations; migration/recovery notes; and whether publication actually occurred. Do not announce production readiness based only on compilation, test counts, a style guard or a successful release API response.

## 9. Copy-and-paste kickoff instruction

```text
Use REMEDIATION_TASK_CATALOG_v1.2.0.md revision 2 as the execution contract for
Tidy-up. Start with TASK-BASE-01. Read applicable repository instructions and
inspect the actual branch, SHA, working-tree changes and supported toolchain.
Preserve unrelated changes. Create the tracker, finding matrix, decision log
and handoff described in the plan.

Implement one eligible task at a time in canonical phase order. Continue through
ready tasks without asking for routine confirmation. Revalidate old findings,
follow shared safety contracts, and prove every acceptance criterion before
marking DONE. Record actual commands/results and evidence tied to the tested SHA.
Do not enable automatic mutation before the Phase 4 safety gate. Do not substitute
service unit tests for actual Windows application and packaged-binary acceptance.

If blocked, record the exact blocker and next action. Continue only independent
safe work; never bypass a prerequisite. Update HANDOFF before stopping or losing
context. Keep unsupported features visibly unavailable and unresolved, rather
than claiming they are repaired. Prepare a release candidate at the end; do not
retag v1.2.0, publish, merge, push or send messages unless explicitly authorized.
```

---

Source provenance: supplied catalog SHA-256 `a3a38e552b227b958de1feeeca8f6b7752c5323ef9e3b353720c201265aca142`. Revision 2 preserves all 39 original task IDs and adds three baseline/contract/containment tasks. No repository implementation or test execution is claimed by this document.
