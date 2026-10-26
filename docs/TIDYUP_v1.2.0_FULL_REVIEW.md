# TidyUp v1.2.0 — GUI, workflow and consistency review

Reviewed: 25 October 2026. Source tag: `v1.2.0`. Commit: `59d3bb45d7ca622ec3a80a7c2b3b0ff21e89358c`.

**Verdict: not ready to describe as a reliable automatic file organiser.** The largest problems are incomplete integration, disagreement between preview and execution, and misleading completion/recovery feedback. Visual polish alone will not resolve them.

## Scope and evidence

This is a source-level review of the tagged application, XAML, view models, runtime services, persistence, test design and release verification, plus visual inspection of all 14 committed screenshots (seven destinations in Light and Dark). It is not a live Windows acceptance test. This environment has no .NET SDK or Windows WPF desktop, so I did not run the application or its test suite. Reproduction steps below are proposed Windows verification procedures unless described as a direct source trace.

The tag resolves to the commit above. The supplied release URL’s API endpoint returned 404; the release collection exposed v1.1.0 as its latest stable entry. I could review v1.2.0 source, but could not verify a published v1.2.0 binary or its equivalence to the tag. This is a release-publication discrepancy, not proof that a binary the user already has is identical or absent everywhere.

The repository’s release-verification document reports 617 passing tests, but explicitly states that no human or vision-model visual review was performed. Those are historical project claims, not test results reproduced in this review. The captured screens are predominantly empty states and cannot establish populated workflow correctness.

[Tagged source](https://github.com/dazzo31/Tidy-up/blob/59d3bb45d7ca622ec3a80a7c2b3b0ff21e89358c/) · [Release verification](https://github.com/dazzo31/Tidy-up/blob/59d3bb45d7ca622ec3a80a7c2b3b0ff21e89358c/docs/release-verification-1.2.0.md) · [CI workflow](https://github.com/dazzo31/Tidy-up/blob/59d3bb45d7ca622ec3a80a7c2b3b0ff21e89358c/.github/workflows/ci.yml)

## Prioritised defect register

P1 = fix before relying on affected file operations or advertised automation. P2 = significant usability, consistency or integration defect. Severity describes impact, not a claim of live reproduction. Findings about monitoring update/policy are explicitly latent until the missing runtime wiring is completed.

44 source-backed findings: 25 P1, 19 P2. Visual recommendations and unverified runtime checks are separate below.

| ID | Priority | Finding |
|---|---|---|
| TU-01 | P1 | Automatic organisation is not connected to application startup |
| TU-02 | P1 | Add Folder reports success without persisting or monitoring the folder |
| TU-03 | P1 | Folder-level Pause/Resume is a display-only toggle |
| TU-04 | P2 | The two Scan commands perform different operations |
| TU-05 | P1 | Disabled rules cannot be meaningfully dry-run |
| TU-06 | P1 | Wizard simulation results are always collapsed by the wrong converter |
| TU-07 | P2 | Boolean inversion is incorrectly used for Visibility bindings |
| TU-08 | P1 | Guided and shell Save paths do not preserve the same rule data |
| TU-09 | P1 | Discard does not restore saved data, and dirty tracking misses nested edits |
| TU-10 | P1 | A failed shell Save clears unsaved-change protection |
| TU-11 | P2 | Wizard Cancel has no application handler |
| TU-12 | P1 | Preview can show the wrong draft or an old rule’s plan |
| TU-13 | P1 | Normal multi-action chains fail preflight |
| TU-14 | P1 | The planner models Copy, Extract and Run Command chains incorrectly |
| TU-15 | P1 | Execution recalculates destinations instead of executing the approved plan |
| TU-16 | P1 | Conflict resolution cannot be completed through Preview |
| TU-17 | P1 | Overwrite and Prompt policies can lose destination data |
| TU-18 | P1 | Extraction bypasses destructive-action safeguards |
| TU-19 | P1 | Confirmed permanent deletion can still fail due to the checkbox contract |
| TU-20 | P1 | Undo Delete claims restoration without restoring anything |
| TU-21 | P1 | Journalling occurs after mutation and omits failed operations and rule identity |
| TU-22 | P1 | Dashboard and Logs are disconnected from executed file operations |
| TU-23 | P2 | Execution summary fabricates actual results from preview rows |
| TU-24 | P2 | Apply remains enabled after all approved actions execute |
| TU-25 | P1 | UI responsiveness and scan completeness are not guaranteed |
| TU-26 | P1 | Settings expose controls that have no runtime effect |
| TU-27 | P2 | Optimize Database is a simulated success |
| TU-28 | P1 | Tray and notification settings are not supplied to their services |
| TU-29 | P1 | Import uses the older unsafe path rather than the schema-aware service |
| TU-30 | P2 | Rule drag-and-drop priority is not persisted |
| TU-31 | P2 | Preserve subfolder structure is exposed but ignored |
| TU-32 | P1 | Empty-folder cleanup can climb above the monitored root |
| TU-33 | P2 | Network monitoring, scheduling and several auxiliary features are not integrated |
| TU-34 | P1 | Monitoring restart links itself to an already cancelled token |
| TU-35 | P2 | Shared-folder watcher configuration depends on the first rule |
| TU-36 | P2 | History pagination commands are not notified when eligibility changes |
| TU-37 | P2 | History export ignores active filters |
| TU-38 | P2 | Undo is offered for operations that cannot be automatically reversed |
| TU-39 | P2 | Help and the onboarding sample use unsupported variables |
| TU-40 | P2 | Time variables can generate invalid Windows filenames |
| TU-41 | P2 | Window restore and close behaviour conflict |
| TU-42 | P2 | Settings persistence is non-atomic and corruption is silently replaced with defaults |
| TU-43 | P2 | Tray health reflects the last folder event, not aggregate health |
| TU-44 | P2 | Batch lifetime and application state are not held consistently |

### TU-01 · P1 · Automatic organisation is not connected to application startup

The DI container registers FileMonitorService, but the production application has no caller of its StartAsync/UpdateRulesAsync methods and no subscriber to IFileMonitorService.FileDetected that evaluates and executes rules. Registration is not activation. Enabling a rule therefore does not establish the advertised automatic processing pipeline. Dashboard Scan All also operates on the service’s initially empty rule list.

**Verify:** Save and enable a valid rule, then create a matching file in its source directory. Trace startup through App and MainWindow: no monitoring-start or detected-file execution path is reached.

**Required correction:** Introduce one application-owned monitoring coordinator. Load enabled rules, subscribe before starting, refresh on rule changes, enforce folder scope and scheduling, and await shutdown. Test through the real application composition root.

**Evidence:** [TidyUp/ServiceConfiguration.cs:70](https://github.com/dazzo31/Tidy-up/blob/59d3bb45d7ca622ec3a80a7c2b3b0ff21e89358c/TidyUp/ServiceConfiguration.cs#L70) · [TidyUp/App.xaml.cs:27](https://github.com/dazzo31/Tidy-up/blob/59d3bb45d7ca622ec3a80a7c2b3b0ff21e89358c/TidyUp/App.xaml.cs#L27) · [TidyUp/Services/FileMonitorService.cs:94](https://github.com/dazzo31/Tidy-up/blob/59d3bb45d7ca622ec3a80a7c2b3b0ff21e89358c/TidyUp/Services/FileMonitorService.cs#L94)

### TU-02 · P1 · Add Folder reports success without persisting or monitoring the folder

FoldersViewModel.AddMonitoredFolder only adds a presentation row. It does not write a rule, a folder record, or the shared catalog. LoadFoldersAsync rebuilds rows from the catalog/rules, so Refresh, revisiting the page, or a catalog event removes the added row. The message “Added monitored directory” is false operational feedback.

**Verify:** Folders → add an existing directory → Refresh → observe that the directory disappears unless it was already linked through a saved rule.

**Required correction:** Persist standalone folders in the canonical catalog, or explicitly create a draft rule with the chosen folder. Provide an obvious Link/Create rule action and distinguish Configured from Actively watched.

**Evidence:** [TidyUp/ViewModels/FoldersViewModel.cs:497](https://github.com/dazzo31/Tidy-up/blob/59d3bb45d7ca622ec3a80a7c2b3b0ff21e89358c/TidyUp/ViewModels/FoldersViewModel.cs#L497) · [TidyUp/ViewModels/FoldersViewModel.cs:193](https://github.com/dazzo31/Tidy-up/blob/59d3bb45d7ca622ec3a80a7c2b3b0ff21e89358c/TidyUp/ViewModels/FoldersViewModel.cs#L193)

### TU-03 · P1 · Folder-level Pause/Resume is a display-only toggle

TogglePauseWatcher changes IsPaused, WatcherStatus and the row’s badge only. It does not contact a watcher, monitoring service, catalog or persisted configuration. Refresh reconstructs the row and loses the change.

**Verify:** Use Pause on a folder, refresh the page, and compare the service state with the displayed state. Once background processing is wired, this control would not stop it.

**Required correction:** Add real per-folder pause/resume commands to the monitoring coordinator and derive all badges from acknowledged runtime state.

**Evidence:** [TidyUp/ViewModels/FoldersViewModel.cs:609](https://github.com/dazzo31/Tidy-up/blob/59d3bb45d7ca622ec3a80a7c2b3b0ff21e89358c/TidyUp/ViewModels/FoldersViewModel.cs#L609)

### TU-04 · P2 · The two Scan commands perform different operations

Folders → Scan only enumerates and counts files. Dashboard → Scan uses watcher reconciliation and emits file-detected events. Neither provides the same reviewable execution plan. Dashboard completion also means detection has finished, not that event-driven operations have completed.

**Verify:** Scan the same folder from Folders and Dashboard. Follow ScanFolderNowAsync versus ScanFolderAsync: one counts files; the other invokes ReconcileFolderAsync.

**Required correction:** Use Scan and preview for read-only planning everywhere. Make an execution command explicitly separate, with a shared job and result model.

**Evidence:** [TidyUp/ViewModels/FoldersViewModel.cs:566](https://github.com/dazzo31/Tidy-up/blob/59d3bb45d7ca622ec3a80a7c2b3b0ff21e89358c/TidyUp/ViewModels/FoldersViewModel.cs#L566) · [TidyUp/ViewModels/DashboardViewModel.cs:480](https://github.com/dazzo31/Tidy-up/blob/59d3bb45d7ca622ec3a80a7c2b3b0ff21e89358c/TidyUp/ViewModels/DashboardViewModel.cs#L480)

### TU-05 · P1 · Disabled rules cannot be meaningfully dry-run

New rules deliberately default to disabled. Both wizard dry-run and the main execution planner pass that disabled rule to RuleEngine.EvaluateRule, which immediately returns false. The condition editor can show matches while the dry-run reports zero. This contradicts Help’s instruction to test before enabling.

**Verify:** Create a disabled rule matching a known file and run Simulate Now. Enable it without changing conditions and compare counts.

**Required correction:** Separate “would this rule match?” from automatic execution eligibility. Dry-run must evaluate disabled drafts without enabling them.

**Evidence:** [TidyUp/Services/RuleEngine.cs:15](https://github.com/dazzo31/Tidy-up/blob/59d3bb45d7ca622ec3a80a7c2b3b0ff21e89358c/TidyUp/Services/RuleEngine.cs#L15) · [TidyUp/ViewModels/RuleEditor/RuleWizardViewModel.cs:378](https://github.com/dazzo31/Tidy-up/blob/59d3bb45d7ca622ec3a80a7c2b3b0ff21e89358c/TidyUp/ViewModels/RuleEditor/RuleWizardViewModel.cs#L378) · [TidyUp/Views/HelpView.xaml:232](https://github.com/dazzo31/Tidy-up/blob/59d3bb45d7ca622ec3a80a7c2b3b0ff21e89358c/TidyUp/Views/HelpView.xaml#L232)

### TU-06 · P1 · Wizard simulation results are always collapsed by the wrong converter

Stage4ReviewView binds an ExecutionPlan object through StringToVisibilityConverter. That converter only makes nonempty strings visible; all other objects return Collapsed. A successfully generated plan therefore remains hidden.

**Verify:** Generate a non-null wizard ExecutionPlan and evaluate the visibility binding; it resolves to Collapsed regardless of results.

**Required correction:** Use a null-to-visibility converter or HasExecutionPlan boolean and test the rendered Review stage with nonempty results.

**Evidence:** [TidyUp/Views/RuleEditor/Stages/Stage4ReviewView.xaml:189](https://github.com/dazzo31/Tidy-up/blob/59d3bb45d7ca622ec3a80a7c2b3b0ff21e89358c/TidyUp/Views/RuleEditor/Stages/Stage4ReviewView.xaml#L189) · [TidyUp/Converters/StringToVisibilityConverter.cs:15](https://github.com/dazzo31/Tidy-up/blob/59d3bb45d7ca622ec3a80a7c2b3b0ff21e89358c/TidyUp/Converters/StringToVisibilityConverter.cs#L15)

### TU-07 · P2 · Boolean inversion is incorrectly used for Visibility bindings

InverseBooleanConverter returns bool, not System.Windows.Visibility. It is used on the wizard Next button, validation error panel, simulation icon and preview failure icon. These bindings cannot supply the requested Visibility value and can leave the default visibility in place, including Next on the final stage or an error panel on a valid rule.

**Verify:** Exercise all four wizard stages and open preview explanations with binding tracing enabled. Check the referenced Visibility bindings for type-conversion errors.

**Required correction:** Use a dedicated inverse-boolean-to-visibility converter and fail GUI smoke tests on binding errors.

**Evidence:** [TidyUp/Converters/InverseBooleanConverter.cs:16](https://github.com/dazzo31/Tidy-up/blob/59d3bb45d7ca622ec3a80a7c2b3b0ff21e89358c/TidyUp/Converters/InverseBooleanConverter.cs#L16) · [TidyUp/Views/RuleEditor/RuleWizardView.xaml:233](https://github.com/dazzo31/Tidy-up/blob/59d3bb45d7ca622ec3a80a7c2b3b0ff21e89358c/TidyUp/Views/RuleEditor/RuleWizardView.xaml#L233) · [TidyUp/Views/RuleEditor/Stages/Stage4ReviewView.xaml:119](https://github.com/dazzo31/Tidy-up/blob/59d3bb45d7ca622ec3a80a7c2b3b0ff21e89358c/TidyUp/Views/RuleEditor/Stages/Stage4ReviewView.xaml#L119) · [TidyUp/Views/PreviewView.xaml:259](https://github.com/dazzo31/Tidy-up/blob/59d3bb45d7ca622ec3a80a7c2b3b0ff21e89358c/TidyUp/Views/PreviewView.xaml#L259)

### TU-08 · P1 · Guided and shell Save paths do not preserve the same rule data

Wizard Save builds a new Rule with only ID, name, description, enabled state, folders, conditions and actions. Existing ExecutionOrder, StopProcessingAfterMatch, TriggerType, ScheduledTime, CreatedDate, LastRunDate and FilesProcessedCount are not copied. Repository Update serializes that replacement. Shell Save instead syncs into the existing rule, so the two Save Rule buttons/Ctrl+S can have different outcomes.

**Verify:** Load a rule with nondefault priority, stop-processing and scheduling metadata, then save using the wizard’s final button. Compare persisted values with saving through Ctrl+S.

**Required correction:** Use a single draft/update service and preserve fields outside the active editor. Bind all Save entry points to it and test full round-trip equality of unchanged fields.

**Evidence:** [TidyUp/ViewModels/RuleEditor/RuleWizardViewModel.cs:334](https://github.com/dazzo31/Tidy-up/blob/59d3bb45d7ca622ec3a80a7c2b3b0ff21e89358c/TidyUp/ViewModels/RuleEditor/RuleWizardViewModel.cs#L334) · [TidyUp/ViewModels/RuleEditor/RuleWizardViewModel.cs:402](https://github.com/dazzo31/Tidy-up/blob/59d3bb45d7ca622ec3a80a7c2b3b0ff21e89358c/TidyUp/ViewModels/RuleEditor/RuleWizardViewModel.cs#L402) · [TidyUp/ViewModels/MainWindowViewModel.cs:679](https://github.com/dazzo31/Tidy-up/blob/59d3bb45d7ca622ec3a80a7c2b3b0ff21e89358c/TidyUp/ViewModels/MainWindowViewModel.cs#L679) · [TidyUp/Data/Repositories/RuleRepository.cs:71](https://github.com/dazzo31/Tidy-up/blob/59d3bb45d7ca622ec3a80a7c2b3b0ff21e89358c/TidyUp/Data/Repositories/RuleRepository.cs#L71)

### TU-09 · P1 · Discard does not restore saved data, and dirty tracking misses nested edits

ConfirmDiscardRuleEdits only clears IsRuleDirty. It does not reload or restore a snapshot. Editors also share condition/action objects with the selected rule. MainWindow watches top-level Rule changes and folder collection membership, but not wizard name/description changes, folder item properties, action properties/collection changes or nested condition changes. Existing-rule edits can bypass the discard prompt or remain in memory after “Discard”.

**Verify:** Edit an existing action destination or wizard name, navigate away, return and switch rules. Repeat after an explicit Discard. Compare in-memory state with the database.

**Required correction:** Edit a deep-cloned draft, compute dirty state against a baseline, and implement Save/Discard/Cancel centrally. Discard must replace the draft with the saved snapshot.

**Evidence:** [TidyUp/ViewModels/MainWindowViewModel.cs:272](https://github.com/dazzo31/Tidy-up/blob/59d3bb45d7ca622ec3a80a7c2b3b0ff21e89358c/TidyUp/ViewModels/MainWindowViewModel.cs#L272) · [TidyUp/ViewModels/MainWindowViewModel.cs:218](https://github.com/dazzo31/Tidy-up/blob/59d3bb45d7ca622ec3a80a7c2b3b0ff21e89358c/TidyUp/ViewModels/MainWindowViewModel.cs#L218) · [TidyUp/ViewModels/RuleEditor/RuleWizardViewModel.cs:260](https://github.com/dazzo31/Tidy-up/blob/59d3bb45d7ca622ec3a80a7c2b3b0ff21e89358c/TidyUp/ViewModels/RuleEditor/RuleWizardViewModel.cs#L260)

### TU-10 · P1 · A failed shell Save clears unsaved-change protection

MainWindowViewModel.SaveRuleAsync clears IsRuleDirty and SelectedRule.IsDirty and calls NotifyRuleSaved in finally, including after repository validation or database failures. The UI can then let the user leave without warning even though the edit was not saved.

**Verify:** Cause a save validation error, such as an invalid move destination, then navigate away. Check dirty state after the exception path.

**Required correction:** Only clear dirty state and publish RuleSaved after successful persistence; retain the draft and inline validation errors on failure.

**Evidence:** [TidyUp/ViewModels/MainWindowViewModel.cs:722](https://github.com/dazzo31/Tidy-up/blob/59d3bb45d7ca622ec3a80a7c2b3b0ff21e89358c/TidyUp/ViewModels/MainWindowViewModel.cs#L722) · [TidyUp/ViewModels/MainWindowViewModel.cs:734](https://github.com/dazzo31/Tidy-up/blob/59d3bb45d7ca622ec3a80a7c2b3b0ff21e89358c/TidyUp/ViewModels/MainWindowViewModel.cs#L734)

### TU-11 · P2 · Wizard Cancel has no application handler

Cancel raises RuleWizardViewModel.RequestClose, but the embedded RuleWizardView and MainWindow do not subscribe. Confirming discard therefore does not reset the wizard or navigate away. CreatedRule also is not cleared by LoadFromRule, so previous successful creation can affect subsequent cancel prompting.

**Verify:** Open an existing rule in the guided editor, press Cancel and confirm. Inspect whether the editor actually closes/resets.

**Required correction:** Replace the window-style close event with an explicit editor cancellation contract handled by the shell; reset draft-specific state on each load.

**Evidence:** [TidyUp/ViewModels/RuleEditor/RuleWizardViewModel.cs:467](https://github.com/dazzo31/Tidy-up/blob/59d3bb45d7ca622ec3a80a7c2b3b0ff21e89358c/TidyUp/ViewModels/RuleEditor/RuleWizardViewModel.cs#L467) · [TidyUp/Views/RuleEditor/RuleWizardView.xaml.cs:11](https://github.com/dazzo31/Tidy-up/blob/59d3bb45d7ca622ec3a80a7c2b3b0ff21e89358c/TidyUp/Views/RuleEditor/RuleWizardView.xaml.cs#L11) · [TidyUp/ViewModels/MainWindowViewModel.cs:365](https://github.com/dazzo31/Tidy-up/blob/59d3bb45d7ca622ec3a80a7c2b3b0ff21e89358c/TidyUp/ViewModels/MainWindowViewModel.cs#L365)

### TU-12 · P1 · Preview can show the wrong draft or an old rule’s plan

TestRuleAsync always copies the advanced ConditionEditor/ActionEditor into SelectedRule, even in guided mode; wizard-specific changes are not synchronized there. Navigation only generates a plan when ExecutionPlan is null, so a previously generated plan survives selecting or editing another rule. There is no refresh/generate command in PreviewView itself.

**Verify:** Preview rule A, select rule B and enter Preview via the rail. Also edit guided actions and use Ctrl+T before saving. Compare the displayed plan with the current draft.

**Required correction:** Plan from one immutable editor snapshot, carry rule ID/revision and input fingerprint, invalidate on any relevant edit/selection, and provide Generate/Refresh preview on the page.

**Evidence:** [TidyUp/ViewModels/MainWindowViewModel.cs:870](https://github.com/dazzo31/Tidy-up/blob/59d3bb45d7ca622ec3a80a7c2b3b0ff21e89358c/TidyUp/ViewModels/MainWindowViewModel.cs#L870) · [TidyUp/ViewModels/MainWindowViewModel.cs:1153](https://github.com/dazzo31/Tidy-up/blob/59d3bb45d7ca622ec3a80a7c2b3b0ff21e89358c/TidyUp/ViewModels/MainWindowViewModel.cs#L1153) · [TidyUp/Views/PreviewView.xaml:306](https://github.com/dazzo31/Tidy-up/blob/59d3bb45d7ca622ec3a80a7c2b3b0ff21e89358c/TidyUp/Views/PreviewView.xaml#L306)

### TU-13 · P1 · Normal multi-action chains fail preflight

The planner assigns the next action the virtual target path of the preceding action. PerformPreFlightCheck requires every approved SourcePath to already exist before any operation runs. Rename→Move, Move→Rename and Copy→operate-on-copy chains therefore fail because their intermediate files have not been created yet.

**Verify:** Plan Rename old.txt→new.txt followed by Move new.txt to another folder. Approve both with the app’s enabled preflight setting. The second source fails File.Exists.

**Required correction:** Represent per-file dependencies explicitly. Validate original inputs before execution and intermediate outputs when their predecessor completes; keep dependent approval atomic or regenerate the chain.

**Evidence:** [TidyUp/Services/Simulation/ExecutionPlanGenerator.cs:139](https://github.com/dazzo31/Tidy-up/blob/59d3bb45d7ca622ec3a80a7c2b3b0ff21e89358c/TidyUp/Services/Simulation/ExecutionPlanGenerator.cs#L139) · [TidyUp/ViewModels/PreviewViewModel.cs:625](https://github.com/dazzo31/Tidy-up/blob/59d3bb45d7ca622ec3a80a7c2b3b0ff21e89358c/TidyUp/ViewModels/PreviewViewModel.cs#L625) · [TidyUp/ViewModels/MainWindowViewModel.cs:396](https://github.com/dazzo31/Tidy-up/blob/59d3bb45d7ca622ec3a80a7c2b3b0ff21e89358c/TidyUp/ViewModels/MainWindowViewModel.cs#L396)

### TU-14 · P1 · The planner models Copy, Extract and Run Command chains incorrectly

Every nonempty TargetPath becomes the next virtual source. This ignores Copy.ApplyToSourceFile=true, treats an extraction directory as a file, and treats command text as a filesystem path. ActionExecutor.ExecuteActionsAsync has different continuation rules, while the preview batch uses fixed per-action sources rather than that chain executor.

**Verify:** Plan Copy with “Apply subsequent actions to source file” enabled, followed by Rename; inspect the second source. Repeat Extract→Rename or Run Command→Move.

**Required correction:** Define one continuation contract for each action type and reuse it in planning and execution. Distinguish file outputs, directory outputs and command descriptions.

**Evidence:** [TidyUp/Services/Simulation/ExecutionPlanGenerator.cs:137](https://github.com/dazzo31/Tidy-up/blob/59d3bb45d7ca622ec3a80a7c2b3b0ff21e89358c/TidyUp/Services/Simulation/ExecutionPlanGenerator.cs#L137) · [TidyUp/Services/ActionExecutor.cs:240](https://github.com/dazzo31/Tidy-up/blob/59d3bb45d7ca622ec3a80a7c2b3b0ff21e89358c/TidyUp/Services/ActionExecutor.cs#L240) · [TidyUp/Services/ActionExecutor.cs:30](https://github.com/dazzo31/Tidy-up/blob/59d3bb45d7ca622ec3a80a7c2b3b0ff21e89358c/TidyUp/Services/ActionExecutor.cs#L30)

### TU-15 · P1 · Execution recalculates destinations instead of executing the approved plan

BatchProcessingCoordinator passes FileAction and counter i+1 to ActionExecutor, which resolves variables again. It does not execute PlannedFileAction.TargetPath. Planning numbers files per folder; execution numbers selected actions across the batch. Approving only file 2 can turn a preview counter of 002 into an actual 001; multiple actions and time variables add further differences. Planned actions also retain mutable action objects.

**Verify:** Create two files with a rename pattern containing {counter}, approve only the second, and compare planned and actual filenames. Also test multiple source folders and multiple actions per file.

**Required correction:** Freeze concrete approved parameters and counters in an immutable execution plan. Any required replan must be shown and approved again; never silently reinterpret an approved action.

**Evidence:** [TidyUp/Services/Processing/BatchProcessingCoordinator.cs:107](https://github.com/dazzo31/Tidy-up/blob/59d3bb45d7ca622ec3a80a7c2b3b0ff21e89358c/TidyUp/Services/Processing/BatchProcessingCoordinator.cs#L107) · [TidyUp/Services/Simulation/ExecutionPlanGenerator.cs:88](https://github.com/dazzo31/Tidy-up/blob/59d3bb45d7ca622ec3a80a7c2b3b0ff21e89358c/TidyUp/Services/Simulation/ExecutionPlanGenerator.cs#L88) · [TidyUp/Services/Simulation/ExecutionPlanGenerator.cs:161](https://github.com/dazzo31/Tidy-up/blob/59d3bb45d7ca622ec3a80a7c2b3b0ff21e89358c/TidyUp/Services/Simulation/ExecutionPlanGenerator.cs#L161)

### TU-16 · P1 · Conflict resolution cannot be completed through Preview

CheckConflict flags every existing target regardless of Skip/RenameNew/RenameOld/Overwrite. Preview blocks applying approved conflicts and batch selection skips conflict rows; no conflict-resolution command is offered there. Conversely, conflicts between two planned outputs are not detected because only the current disk is checked.

**Verify:** Select RenameNew with an existing destination: the plan is blocked despite a resolvable policy. Then plan two source files with the same target name in an initially empty destination: both can appear conflict-free.

**Required correction:** Resolve policies in the planner, reserve targets across the complete plan, expose explicit unresolved decisions and revalidate targets at execution.

**Evidence:** [TidyUp/Services/Simulation/ExecutionPlanGenerator.cs:239](https://github.com/dazzo31/Tidy-up/blob/59d3bb45d7ca622ec3a80a7c2b3b0ff21e89358c/TidyUp/Services/Simulation/ExecutionPlanGenerator.cs#L239) · [TidyUp/ViewModels/PreviewViewModel.cs:288](https://github.com/dazzo31/Tidy-up/blob/59d3bb45d7ca622ec3a80a7c2b3b0ff21e89358c/TidyUp/ViewModels/PreviewViewModel.cs#L288) · [TidyUp/ViewModels/PreviewViewModel.cs:602](https://github.com/dazzo31/Tidy-up/blob/59d3bb45d7ca622ec3a80a7c2b3b0ff21e89358c/TidyUp/ViewModels/PreviewViewModel.cs#L602)

### TU-17 · P1 · Overwrite and Prompt policies can lose destination data

Move/Rename overwrite deletes the destination before the move is known to succeed; the old destination is not backed up or journalled. Copy always calls File.Copy with overwrite:true after conflict resolution. Prompt currently just returns the original target without prompting; if a target appears after preview, Copy with Prompt can silently overwrite it. These are reachable execution paths even though pre-existing preview conflicts are blocked.

**Verify:** In a disposable fixture, create the destination after preview and apply a Copy configured with Prompt. Separately inject a move failure after overwrite removes the old destination.

**Required correction:** Fail closed for unimplemented Prompt; recheck conflicts immediately before mutation. Stage backups/atomic replacement and journal both the new file and displaced original so rollback can restore the complete operation.

**Evidence:** [TidyUp/Services/ActionExecutor.cs:599](https://github.com/dazzo31/Tidy-up/blob/59d3bb45d7ca622ec3a80a7c2b3b0ff21e89358c/TidyUp/Services/ActionExecutor.cs#L599) · [TidyUp/Services/ActionExecutor.cs:236](https://github.com/dazzo31/Tidy-up/blob/59d3bb45d7ca622ec3a80a7c2b3b0ff21e89358c/TidyUp/Services/ActionExecutor.cs#L236) · [TidyUp/Services/ActionExecutor.cs:162](https://github.com/dazzo31/Tidy-up/blob/59d3bb45d7ca622ec3a80a7c2b3b0ff21e89358c/TidyUp/Services/ActionExecutor.cs#L162)

### TU-18 · P1 · Extraction bypasses destructive-action safeguards

ExtractArchive can overwrite existing contents and permanently delete the archive with File.Delete. The plan contains only an extraction directory, sets HasConflict=false, and the safeguard evaluator only identifies Delete actions and ConflictResolution-based overwrite. A small extraction can therefore receive ordinary confirmation without disclosing archive deletion or overwritten entries. Extraction rollback is unsupported.

**Verify:** Configure Extract Archive with OverwriteExisting and DeleteAfterExtraction, then preview a small archive. Compare the risk summary with actual executor behaviour.

**Required correction:** Expand extraction into entry-level planned effects, include overwrite/deletion risks, recycle the archive by default, and disclose exact rollback limitations before Apply.

**Evidence:** [TidyUp/Services/ActionExecutor.cs:466](https://github.com/dazzo31/Tidy-up/blob/59d3bb45d7ca622ec3a80a7c2b3b0ff21e89358c/TidyUp/Services/ActionExecutor.cs#L466) · [TidyUp/Services/Simulation/ExecutionPlanGenerator.cs:215](https://github.com/dazzo31/Tidy-up/blob/59d3bb45d7ca622ec3a80a7c2b3b0ff21e89358c/TidyUp/Services/Simulation/ExecutionPlanGenerator.cs#L215) · [TidyUp/ViewModels/Dialogs/SafeguardConfirmationViewModel.cs:120](https://github.com/dazzo31/Tidy-up/blob/59d3bb45d7ca622ec3a80a7c2b3b0ff21e89358c/TidyUp/ViewModels/Dialogs/SafeguardConfirmationViewModel.cs#L120)

### TU-19 · P1 · Confirmed permanent deletion can still fail due to the checkbox contract

ExecuteDeleteAsync authorizes permanent deletion only when both UseRecycleBin=false and ConfirmBeforeDelete=false. Confirming the safeguard dialog does not change this authorization. With the normal Confirm before deleting checkbox still checked, WindowsShellFileOperations rejects the operation after the user already confirmed it.

**Verify:** Uncheck Use Recycle Bin, keep Confirm before deleting checked, approve the safeguard, and apply to a disposable file. The permanent-deletion authorization exception is expected.

**Required correction:** Pass an explicit confirmed execution authorization from the UI to the executor; do not encode consent as the inverse of a persisted checkbox.

**Evidence:** [TidyUp/Services/ActionExecutor.cs:413](https://github.com/dazzo31/Tidy-up/blob/59d3bb45d7ca622ec3a80a7c2b3b0ff21e89358c/TidyUp/Services/ActionExecutor.cs#L413) · [TidyUp/Services/FileSystem/WindowsShellFileOperations.cs:97](https://github.com/dazzo31/Tidy-up/blob/59d3bb45d7ca622ec3a80a7c2b3b0ff21e89358c/TidyUp/Services/FileSystem/WindowsShellFileOperations.cs#L97)

### TU-20 · P1 · Undo Delete claims restoration without restoring anything

RollbackDeleteAsync always supplies Recycle Bin instructions and returns Success=true, including for permanently deleted files. HistoryViewModel treats that success as an actual reversal, marks the row RolledBack and says “Operation successfully reversed”. The database entry itself remains Completed. Batch rollback also counts guidance as reversed, and the restoration instructions are not presented by the single-operation success path.

**Verify:** Delete a disposable file, click Undo, and check the disk, row and database. Repeat for permanent deletion. A refresh can restore the old Completed status because no reversal happened.

**Required correction:** Return distinct Restored, ManualRestoreRequired and NotRecoverable outcomes. Never claim restoration until verified, and do not offer Recycle Bin guidance for permanent deletion.

**Evidence:** [TidyUp/Services/Rollback/RollbackEngine.cs:313](https://github.com/dazzo31/Tidy-up/blob/59d3bb45d7ca622ec3a80a7c2b3b0ff21e89358c/TidyUp/Services/Rollback/RollbackEngine.cs#L313) · [TidyUp/ViewModels/HistoryViewModel.cs:538](https://github.com/dazzo31/Tidy-up/blob/59d3bb45d7ca622ec3a80a7c2b3b0ff21e89358c/TidyUp/ViewModels/HistoryViewModel.cs#L538) · [TidyUp/Services/Rollback/RollbackEngine.cs:375](https://github.com/dazzo31/Tidy-up/blob/59d3bb45d7ca622ec3a80a7c2b3b0ff21e89358c/TidyUp/Services/Rollback/RollbackEngine.cs#L375)

### TU-21 · P1 · Journalling occurs after mutation and omits failed operations and rule identity

ActionExecutor writes a journal entry only after Success and does not supply RuleId, RuleName or RuleRevisionId. Failed attempts are absent from History. A journal write failure after a successful disk mutation escapes to the batch as an execution failure without a durable record of what changed. Overwrite backups and RenameOld’s displaced target are also not represented.

**Verify:** Apply a successful operation and inspect its rule fields; apply a failing operation and inspect History. Inject a journal persistence failure after a file move and compare disk state with reported batch state.

**Required correction:** Write a durable intent before mutation, finalize with actual outputs/failure/partial outcome, carry rule and plan identity, and reconcile incomplete intents at startup.

**Evidence:** [TidyUp/Services/ActionExecutor.cs:76](https://github.com/dazzo31/Tidy-up/blob/59d3bb45d7ca622ec3a80a7c2b3b0ff21e89358c/TidyUp/Services/ActionExecutor.cs#L76) · [TidyUp/Services/ActionExecutor.cs:81](https://github.com/dazzo31/Tidy-up/blob/59d3bb45d7ca622ec3a80a7c2b3b0ff21e89358c/TidyUp/Services/ActionExecutor.cs#L81) · [TidyUp/Data/Entities/OperationJournalEntry.cs:44](https://github.com/dazzo31/Tidy-up/blob/59d3bb45d7ca622ec3a80a7c2b3b0ff21e89358c/TidyUp/Data/Entities/OperationJournalEntry.cs#L44)

### TU-22 · P1 · Dashboard and Logs are disconnected from executed file operations

Dashboard metrics and Logs read ActionLogs, while the reachable preview executor writes OperationJournal. There is no production call creating an ActionLogEntity for those executions, nor an execution path updating Rule.LastRunDate/FilesProcessedCount. Successful manual changes can appear in History while Dashboard still says zero and Logs remains empty. The activity feed is refreshed on explicit load rather than receiving live execution events.

**Verify:** Apply a simple successful operation, then refresh History, Dashboard and Logs. Compare journal records with ActionLogs and rule counters.

**Required correction:** Publish one durable operation outcome stream and derive History, activity metrics and rule counters from it. Keep operational diagnostics separate and clearly named.

**Evidence:** [TidyUp/ViewModels/DashboardViewModel.cs:329](https://github.com/dazzo31/Tidy-up/blob/59d3bb45d7ca622ec3a80a7c2b3b0ff21e89358c/TidyUp/ViewModels/DashboardViewModel.cs#L329) · [TidyUp/ViewModels/LogsViewModel.cs:17](https://github.com/dazzo31/Tidy-up/blob/59d3bb45d7ca622ec3a80a7c2b3b0ff21e89358c/TidyUp/ViewModels/LogsViewModel.cs#L17) · [TidyUp/Services/ActionExecutor.cs:81](https://github.com/dazzo31/Tidy-up/blob/59d3bb45d7ca622ec3a80a7c2b3b0ff21e89358c/TidyUp/Services/ActionExecutor.cs#L81)

### TU-23 · P2 · Execution summary fabricates actual results from preview rows

PreviewViewModel discards the coordinator’s actual ActionResult values when building discrepancies. It creates new results using item.TargetPath for both result and target paths and status from UI progress callbacks. This hides actual renamed targets and makes summary accuracy depend on asynchronous UI callback timing. The analyzer also correlates by SourcePath, not action ID, collapsing multiple actions on one source.

**Verify:** Cause RenameNew to choose a different actual target after preview, then inspect the discrepancy report. Test a multi-action source and cancellation.

**Required correction:** Use BatchExecutionResult.ItemResults directly, correlate by planned action ID, and update UI rows from those authoritative results before displaying the summary.

**Evidence:** [TidyUp/ViewModels/PreviewViewModel.cs:558](https://github.com/dazzo31/Tidy-up/blob/59d3bb45d7ca622ec3a80a7c2b3b0ff21e89358c/TidyUp/ViewModels/PreviewViewModel.cs#L558) · [TidyUp/Services/Diagnostics/ExecutionDiscrepancyAnalyzer.cs:72](https://github.com/dazzo31/Tidy-up/blob/59d3bb45d7ca622ec3a80a7c2b3b0ff21e89358c/TidyUp/Services/Diagnostics/ExecutionDiscrepancyAnalyzer.cs#L72)

### TU-24 · P2 · Apply remains enabled after all approved actions execute

CanApplyChanges tests IsApproved but not IsExecuted. UpdateApprovedCount correctly excludes executed rows, so the caption can say none selected while Apply remains enabled. The click handler then filters out executed rows and reports that no approved actions remain. Command eligibility and the pending count use different predicates.

**Verify:** Apply all selected actions successfully, leave the page open, and inspect the Apply button/count before clicking it again.

**Required correction:** Use one PendingApprovedActions predicate for count, caption, eligibility and execution, and expose a completed state with Refresh preview.

**Evidence:** [TidyUp/ViewModels/PreviewViewModel.cs:288](https://github.com/dazzo31/Tidy-up/blob/59d3bb45d7ca622ec3a80a7c2b3b0ff21e89358c/TidyUp/ViewModels/PreviewViewModel.cs#L288) · [TidyUp/ViewModels/PreviewViewModel.cs:712](https://github.com/dazzo31/Tidy-up/blob/59d3bb45d7ca622ec3a80a7c2b3b0ff21e89358c/TidyUp/ViewModels/PreviewViewModel.cs#L712)

### TU-25 · P1 · UI responsiveness and scan completeness are not guaranteed

ExecutionPlanGenerator.GeneratePlanForFolderAsync performs Directory.GetFiles and the entire evaluation loop synchronously before returning Task.FromResult. Awaiting it from a GUI command does not move the work off the UI thread. A recursive enumeration error returns an empty plan for the entire folder, silently hiding readable files. There is no preview cancellation control.

**Verify:** Preview a large tree or one with an inaccessible child. Check UI input responsiveness and whether the plan distinguishes incomplete scan from zero matches.

**Required correction:** Use cancellable background enumeration, handle per-directory errors, stream progress and explicitly mark incomplete plans. Do not present an incomplete scan as Ready to apply without disclosure.

**Evidence:** [TidyUp/Services/Simulation/ExecutionPlanGenerator.cs:80](https://github.com/dazzo31/Tidy-up/blob/59d3bb45d7ca622ec3a80a7c2b3b0ff21e89358c/TidyUp/Services/Simulation/ExecutionPlanGenerator.cs#L80) · [TidyUp/Services/Simulation/ExecutionPlanGenerator.cs:72](https://github.com/dazzo31/Tidy-up/blob/59d3bb45d7ca622ec3a80a7c2b3b0ff21e89358c/TidyUp/Services/Simulation/ExecutionPlanGenerator.cs#L72) · [TidyUp/ViewModels/PreviewViewModel.cs:430](https://github.com/dazzo31/Tidy-up/blob/59d3bb45d7ca622ec3a80a7c2b3b0ff21e89358c/TidyUp/ViewModels/PreviewViewModel.cs#L430)

### TU-26 · P1 · Settings expose controls that have no runtime effect

Production usage searches show no runtime consumption of MaxConcurrentOperations, ScanBatchSize, NetworkFolderPollingIntervalSeconds, UseRecycleBinByDefault, RequireDestructiveConfirmations, DebugLoggingEnabled and AutoCleanupLogs. Retry/debounce values are hard-coded. DefaultConflictResolution is chosen in onboarding but action creation uses hard-coded policies. DatabaseBackupPath is editable but DatabaseBackupManager uses TidyUpPaths. These are persisted options, not functioning controls.

**Verify:** Change each setting, save and follow its runtime consumer. Compare new action defaults, scan batch behaviour, concurrency and actual backup destination.

**Required correction:** Either wire each setting into a validated runtime configuration or hide/disable it with an explicit explanation. Add a settings-to-behaviour acceptance matrix rather than persistence-only tests.

**Evidence:** [TidyUp/Models/AppSettings.cs:79](https://github.com/dazzo31/Tidy-up/blob/59d3bb45d7ca622ec3a80a7c2b3b0ff21e89358c/TidyUp/Models/AppSettings.cs#L79) · [TidyUp/ViewModels/ActionEditorViewModel.cs:30](https://github.com/dazzo31/Tidy-up/blob/59d3bb45d7ca622ec3a80a7c2b3b0ff21e89358c/TidyUp/ViewModels/ActionEditorViewModel.cs#L30) · [TidyUp/Services/FileMonitorService.cs:22](https://github.com/dazzo31/Tidy-up/blob/59d3bb45d7ca622ec3a80a7c2b3b0ff21e89358c/TidyUp/Services/FileMonitorService.cs#L22) · [TidyUp/Data/Recovery/DatabaseBackupManager.cs:23](https://github.com/dazzo31/Tidy-up/blob/59d3bb45d7ca622ec3a80a7c2b3b0ff21e89358c/TidyUp/Data/Recovery/DatabaseBackupManager.cs#L23)

### TU-27 · P2 · Optimize Database is a simulated success

VacuumDatabaseAsync waits 300 milliseconds, then reports database optimization complete and space reclaimed. It executes no SQL and calls no maintenance service.

**Verify:** Click Optimize Database and follow the method: only Task.Delay occurs.

**Required correction:** Run a real coordinated SQLite VACUUM/maintenance operation with measured results, or remove the control until implemented.

**Evidence:** [TidyUp/ViewModels/SettingsViewModel.cs:184](https://github.com/dazzo31/Tidy-up/blob/59d3bb45d7ca622ec3a80a7c2b3b0ff21e89358c/TidyUp/ViewModels/SettingsViewModel.cs#L184)

### TU-28 · P1 · Tray and notification settings are not supplied to their services

TrayIconService and ToastNotificationService accept optional AppSettings, but AppSettings is not registered in DI. LoadSettingsAsync only returns an object. The notification service consequently uses fallback defaults, and tray initialization occurs regardless of SystemTrayEnabled. The tray’s pause/resume changes the monitor and icon but not ApplicationStateManager, so shell state can disagree.

**Verify:** Disable notifications/system tray in Settings and restart; inspect injected settings and tray initialization. Pause from the tray and compare the shell state badge.

**Required correction:** Inject the shared settings instance explicitly, honor tray visibility, route notifications and pause state through the application coordinator, and test the real DI graph.

**Evidence:** [TidyUp/ServiceConfiguration.cs:149](https://github.com/dazzo31/Tidy-up/blob/59d3bb45d7ca622ec3a80a7c2b3b0ff21e89358c/TidyUp/ServiceConfiguration.cs#L149) · [TidyUp/Services/ToastNotificationService.cs:33](https://github.com/dazzo31/Tidy-up/blob/59d3bb45d7ca622ec3a80a7c2b3b0ff21e89358c/TidyUp/Services/ToastNotificationService.cs#L33) · [TidyUp/App.xaml.cs:112](https://github.com/dazzo31/Tidy-up/blob/59d3bb45d7ca622ec3a80a7c2b3b0ff21e89358c/TidyUp/App.xaml.cs#L112) · [TidyUp/Services/TrayIconService.cs:71](https://github.com/dazzo31/Tidy-up/blob/59d3bb45d7ca622ec3a80a7c2b3b0ff21e89358c/TidyUp/Services/TrayIconService.cs#L71)

### TU-29 · P1 · Import uses the older unsafe path rather than the schema-aware service

The shell uses ImportExportService, which preserves imported IsEnabled and does not enforce the package version. RuleSerializationService implements disabled-on-import and version checks but is not the UI import path. Import then saves rules one by one without a transaction; a later error leaves earlier imports committed while reporting a generic error.

**Verify:** Import a package containing enabled rules or an unsupported version. Then import two rules where the second fails repository validation and inspect what was persisted.

**Required correction:** Unify import/export behind one schema-aware service, force imported rules disabled, show validation warnings and make the batch transactional or explicitly report partial results.

**Evidence:** [TidyUp/ViewModels/MainWindowViewModel.cs:982](https://github.com/dazzo31/Tidy-up/blob/59d3bb45d7ca622ec3a80a7c2b3b0ff21e89358c/TidyUp/ViewModels/MainWindowViewModel.cs#L982) · [TidyUp/Services/ImportExportService.cs:54](https://github.com/dazzo31/Tidy-up/blob/59d3bb45d7ca622ec3a80a7c2b3b0ff21e89358c/TidyUp/Services/ImportExportService.cs#L54) · [TidyUp/Services/Rules/RuleSerializationService.cs:124](https://github.com/dazzo31/Tidy-up/blob/59d3bb45d7ca622ec3a80a7c2b3b0ff21e89358c/TidyUp/Services/Rules/RuleSerializationService.cs#L124)

### TU-30 · P2 · Rule drag-and-drop priority is not persisted

RuleListBox_Drop moves in-memory rows and changes ExecutionOrder on each rule, but does not save the reorder. Saving one selected rule is not a transaction updating all priorities. Restart can undo the apparent order or leave mixed priorities.

**Verify:** Reorder several rules, restart, and compare priorities. Repeat after saving only one selected rule.

**Required correction:** Persist the complete reordered ID list atomically and disable drag reorder while a non-priority sort is active.

**Evidence:** [TidyUp/MainWindow.xaml.cs:193](https://github.com/dazzo31/Tidy-up/blob/59d3bb45d7ca622ec3a80a7c2b3b0ff21e89358c/TidyUp/MainWindow.xaml.cs#L193)

### TU-31 · P2 · Preserve subfolder structure is exposed but ignored

Move and Copy models contain PreserveSubfolderStructure and the Move editor exposes it, but planner/executor destinations always use destination directory plus the current filename. The source-root-relative directory is never applied. Files from different subfolders flatten into the same destination and may collide.

**Verify:** Enable Preserve subfolder structure on a recursive source with A/report.txt and B/report.txt, then inspect the planned targets.

**Required correction:** Carry source-root identity into the plan and calculate the relative path once; test nested and overlapping monitored roots.

**Evidence:** [TidyUp/Controls/ActionEditorControl.xaml:162](https://github.com/dazzo31/Tidy-up/blob/59d3bb45d7ca622ec3a80a7c2b3b0ff21e89358c/TidyUp/Controls/ActionEditorControl.xaml#L162) · [TidyUp/Services/ActionExecutor.cs:143](https://github.com/dazzo31/Tidy-up/blob/59d3bb45d7ca622ec3a80a7c2b3b0ff21e89358c/TidyUp/Services/ActionExecutor.cs#L143) · [TidyUp/Services/Simulation/ExecutionPlanGenerator.cs:174](https://github.com/dazzo31/Tidy-up/blob/59d3bb45d7ca622ec3a80a7c2b3b0ff21e89358c/TidyUp/Services/Simulation/ExecutionPlanGenerator.cs#L174)

### TU-32 · P1 · Empty-folder cleanup can climb above the monitored root

RemoveEmptyFoldersAsync supports a stopBoundaryPath, but Move/Delete callers omit it. After removing the last source file it recursively deletes empty ancestors until a drive root or a small exact protected-folder set is reached. It can remove the monitored folder and unrelated empty parent structure. These directory effects are not in preview or the journal.

**Verify:** In a disposable nested tree outside protected folders, move the last file with Remove empty source folders enabled. Check whether ancestors above the intended source are removed.

**Required correction:** Pass and enforce the monitored source boundary, plan directory deletions explicitly, and include them in recovery semantics.

**Evidence:** [TidyUp/Services/ActionExecutor.cs:172](https://github.com/dazzo31/Tidy-up/blob/59d3bb45d7ca622ec3a80a7c2b3b0ff21e89358c/TidyUp/Services/ActionExecutor.cs#L172) · [TidyUp/Services/ActionExecutor.cs:681](https://github.com/dazzo31/Tidy-up/blob/59d3bb45d7ca622ec3a80a7c2b3b0ff21e89358c/TidyUp/Services/ActionExecutor.cs#L681)

### TU-33 · P2 · Network monitoring, scheduling and several auxiliary features are not integrated

Network paths deliberately skip FileSystemWatcher, with periodic polling still TODO. ScheduleManager/Settings exist but are not registered or invoked by production execution. Duplicate detection has no user workflow/DI registration. Rule-revision services/windows exist, but ordinary rule saves do not create revisions and there is no shell launch path for that editor. These are components, not completed end-user features.

**Verify:** Look for actual UI entry points and runtime callers, then try a network directory and a scheduled rule. Distinguish unsupported workflows from services exercised directly by unit tests.

**Required correction:** Publish an honest feature-status matrix. Integrate and test each complete workflow before advertising it; otherwise label it unavailable.

**Evidence:** [TidyUp/Services/FileMonitorService.cs:192](https://github.com/dazzo31/Tidy-up/blob/59d3bb45d7ca622ec3a80a7c2b3b0ff21e89358c/TidyUp/Services/FileMonitorService.cs#L192) · [TidyUp/Services/Scheduling/ScheduleManager.cs:39](https://github.com/dazzo31/Tidy-up/blob/59d3bb45d7ca622ec3a80a7c2b3b0ff21e89358c/TidyUp/Services/Scheduling/ScheduleManager.cs#L39) · [TidyUp/ServiceConfiguration.cs:83](https://github.com/dazzo31/Tidy-up/blob/59d3bb45d7ca622ec3a80a7c2b3b0ff21e89358c/TidyUp/ServiceConfiguration.cs#L83) · [TidyUp/Services/Rules/RuleRevisionManager.cs:78](https://github.com/dazzo31/Tidy-up/blob/59d3bb45d7ca622ec3a80a7c2b3b0ff21e89358c/TidyUp/Services/Rules/RuleRevisionManager.cs#L78)

### TU-34 · P1 · Monitoring restart links itself to an already cancelled token

UpdateRulesAsync calls StopAsync, which cancels _cancellationTokenSource, then passes that cancelled source’s Token into StartAsync. The new linked token is already cancelled, so scan loops exit. This is currently latent behind the missing application integration, but wiring rule refresh to this method would expose it.

**Verify:** At service level: StartAsync → UpdateRulesAsync → inspect the linked cancellation token and initial rescan. Do not treat this as proof that automatic monitoring currently starts in the UI.

**Required correction:** Retain the external lifetime token separately; create a fresh internal CTS for each run and dispose the old one. Add a start/update/stop/restart integration test.

**Evidence:** [TidyUp/Services/FileMonitorService.cs:147](https://github.com/dazzo31/Tidy-up/blob/59d3bb45d7ca622ec3a80a7c2b3b0ff21e89358c/TidyUp/Services/FileMonitorService.cs#L147) · [TidyUp/Services/FileMonitorService.cs:160](https://github.com/dazzo31/Tidy-up/blob/59d3bb45d7ca622ec3a80a7c2b3b0ff21e89358c/TidyUp/Services/FileMonitorService.cs#L160)

### TU-35 · P2 · Shared-folder watcher configuration depends on the first rule

FileMonitorService deduplicates folders using DistinctBy(Path), retaining the first folder’s recursion and exclusions. The UI catalog aggregates multiple rules, so displayed scope can differ from the watcher. One rule’s exclusions can suppress events needed by another; a nonrecursive first rule can suppress required subfolder monitoring.

**Verify:** Create two enabled rules for the same path with different IncludeSubfolders/exclusions, change their order and compare the configured watcher. This becomes operationally relevant after monitoring is wired.

**Required correction:** Build watcher scope as a superset of all linked rules and apply each rule’s own scope/exclusions during dispatch. Normalize paths case-insensitively.

**Evidence:** [TidyUp/Services/FileMonitorService.cs:109](https://github.com/dazzo31/Tidy-up/blob/59d3bb45d7ca622ec3a80a7c2b3b0ff21e89358c/TidyUp/Services/FileMonitorService.cs#L109) · [TidyUp/ViewModels/DashboardViewModel.cs:275](https://github.com/dazzo31/Tidy-up/blob/59d3bb45d7ca622ec3a80a7c2b3b0ff21e89358c/TidyUp/ViewModels/DashboardViewModel.cs#L275)

### TU-36 · P2 · History pagination commands are not notified when eligibility changes

CurrentPage/PageSize/TotalRecords notify dependent properties, but there are no NotifyCanExecuteChangedFor attributes or explicit NextPageCommand/PreviousPageCommand notifications. Toolkit relay commands do not automatically requery from property changes. Buttons can retain initial disabled/enabled state after data loads or paging.

**Verify:** Open History with more than one page of entries, then change filters and page size. Observe Next/Previous command state, not only HasNextPage/HasPreviousPage values.

**Required correction:** Notify command eligibility whenever the pagination inputs change and test the rendered buttons with populated data.

**Evidence:** [TidyUp/ViewModels/HistoryViewModel.cs:190](https://github.com/dazzo31/Tidy-up/blob/59d3bb45d7ca622ec3a80a7c2b3b0ff21e89358c/TidyUp/ViewModels/HistoryViewModel.cs#L190) · [TidyUp/ViewModels/HistoryViewModel.cs:455](https://github.com/dazzo31/Tidy-up/blob/59d3bb45d7ca622ec3a80a7c2b3b0ff21e89358c/TidyUp/ViewModels/HistoryViewModel.cs#L455)

### TU-37 · P2 · History export ignores active filters

CSV and JSON export query all OperationJournal rows rather than reusing the currently filtered query. The buttons simply say Export CSV/JSON, so a user viewing a subset can export unrelated records without being told.

**Verify:** Apply a rule/date/status filter, export and compare the exported rows with the displayed filtered total.

**Required correction:** Offer Export filtered results and Export all explicitly, backed by the same query builder and a stable export ordering/snapshot.

**Evidence:** [TidyUp/ViewModels/HistoryViewModel.cs:595](https://github.com/dazzo31/Tidy-up/blob/59d3bb45d7ca622ec3a80a7c2b3b0ff21e89358c/TidyUp/ViewModels/HistoryViewModel.cs#L595) · [TidyUp/ViewModels/HistoryViewModel.cs:668](https://github.com/dazzo31/Tidy-up/blob/59d3bb45d7ca622ec3a80a7c2b3b0ff21e89358c/TidyUp/ViewModels/HistoryViewModel.cs#L668)

### TU-38 · P2 · Undo is offered for operations that cannot be automatically reversed

HistoryRowItemViewModel.CanRollback only checks Completed and RolledBackAt. It enables Undo for ExtractArchive and RunCommand, which RollbackEngine explicitly rejects. Copy rollback also permits permanent deletion fallback on a non-recyclable destination without a distinct warning.

**Verify:** Complete an extraction/command and inspect Undo. For a copied file on an unsupported Recycle Bin volume, inspect rollback confirmation versus DeleteFileSafelyAsync arguments.

**Required correction:** Expose a per-operation recovery capability and label Undo, Open restore instructions, or Not reversible accurately. Require explicit disclosure/authorization for permanent rollback deletion.

**Evidence:** [TidyUp/ViewModels/HistoryViewModel.cs:120](https://github.com/dazzo31/Tidy-up/blob/59d3bb45d7ca622ec3a80a7c2b3b0ff21e89358c/TidyUp/ViewModels/HistoryViewModel.cs#L120) · [TidyUp/Services/Rollback/RollbackEngine.cs:117](https://github.com/dazzo31/Tidy-up/blob/59d3bb45d7ca622ec3a80a7c2b3b0ff21e89358c/TidyUp/Services/Rollback/RollbackEngine.cs#L117) · [TidyUp/Services/Rollback/RollbackEngine.cs:279](https://github.com/dazzo31/Tidy-up/blob/59d3bb45d7ca622ec3a80a7c2b3b0ff21e89358c/TidyUp/Services/Rollback/RollbackEngine.cs#L279)

### TU-39 · P2 · Help and the onboarding sample use unsupported variables

The sample rule uses {FileExtension}/{Year}-{Month}, whereas VariableEngine supports extension/now_year/now_month. Help advertises {CreationYear}, {ModifiedYear}, {DateTime}, {MonthName} and other unsupported names. Unknown variables are retained as literal braces instead of rejected. Help also puts {FileName} into a folder destination although Move appends the filename itself. Its About screen says 1.0.0 and ©2025 while the project is 1.2.0/2026.

**Verify:** Create the sample rule and resolve its destination using VariableEngine. Compare every advertised token with IsKnownVariable; open Help → About.

**Required correction:** Generate Help, variable picker, samples and validation from one variable registry. Use AppVersionInfo in every About surface and test sample rules against real fixtures.

**Evidence:** [TidyUp/ViewModels/FirstRunViewModel.cs:179](https://github.com/dazzo31/Tidy-up/blob/59d3bb45d7ca622ec3a80a7c2b3b0ff21e89358c/TidyUp/ViewModels/FirstRunViewModel.cs#L179) · [TidyUp/Services/VariableEngine.cs:96](https://github.com/dazzo31/Tidy-up/blob/59d3bb45d7ca622ec3a80a7c2b3b0ff21e89358c/TidyUp/Services/VariableEngine.cs#L96) · [TidyUp/Views/HelpView.xaml:256](https://github.com/dazzo31/Tidy-up/blob/59d3bb45d7ca622ec3a80a7c2b3b0ff21e89358c/TidyUp/Views/HelpView.xaml#L256)

### TU-40 · P2 · Time variables can generate invalid Windows filenames

created_time, modified_time and now_time resolve to HH:mm:ss. Colons are invalid inside ordinary Windows filename components. The common rename hint also suggests {date}, which is not a supported token. RuleValidator validates Move/Copy destinations but not all rename/extension/command/extraction parameters or unknown tokens.

**Verify:** Use {filename}_{now_time} as a rename pattern. Compare the resolved name with Windows filename rules and when the user receives validation feedback.

**Required correction:** Provide filename-safe default time formatting, validate all resolved action types and unknown tokens before planning, and show a live resolved example.

**Evidence:** [TidyUp/Services/VariableEngine.cs:87](https://github.com/dazzo31/Tidy-up/blob/59d3bb45d7ca622ec3a80a7c2b3b0ff21e89358c/TidyUp/Services/VariableEngine.cs#L87) · [TidyUp/Services/Validation/RuleValidator.cs:71](https://github.com/dazzo31/Tidy-up/blob/59d3bb45d7ca622ec3a80a7c2b3b0ff21e89358c/TidyUp/Services/Validation/RuleValidator.cs#L71) · [TidyUp/Controls/ActionEditorControl.xaml:225](https://github.com/dazzo31/Tidy-up/blob/59d3bb45d7ca622ec3a80a7c2b3b0ff21e89358c/TidyUp/Controls/ActionEditorControl.xaml#L225)

### TU-41 · P2 · Window restore and close behaviour conflict

MainWindow restores saved WindowState, but App.OnStartup immediately forces Normal after Show, defeating maximized-state persistence. MainWindow’s Closing handler can cancel due to unsaved edits, but App’s later Closing handler does not check e.Cancel and still hides the window when MinimizeToTray is true. Tray Exit directly calls Shutdown without waiting for the batch coordinator.

**Verify:** Restart after saving a maximized window. Then close a dirty rule and decline discard: verify the window remains visible. Test tray Exit during a controlled batch on Windows.

**Required correction:** Centralize close/minimize/exit decisions, respect an existing cancellation, preserve restored state and wait for safe operation completion on exit.

**Evidence:** [TidyUp/App.xaml.cs:137](https://github.com/dazzo31/Tidy-up/blob/59d3bb45d7ca622ec3a80a7c2b3b0ff21e89358c/TidyUp/App.xaml.cs#L137) · [TidyUp/App.xaml.cs:183](https://github.com/dazzo31/Tidy-up/blob/59d3bb45d7ca622ec3a80a7c2b3b0ff21e89358c/TidyUp/App.xaml.cs#L183) · [TidyUp/App.xaml.cs:261](https://github.com/dazzo31/Tidy-up/blob/59d3bb45d7ca622ec3a80a7c2b3b0ff21e89358c/TidyUp/App.xaml.cs#L261) · [TidyUp/MainWindow.xaml.cs:34](https://github.com/dazzo31/Tidy-up/blob/59d3bb45d7ca622ec3a80a7c2b3b0ff21e89358c/TidyUp/MainWindow.xaml.cs#L34)

### TU-42 · P2 · Settings persistence is non-atomic and corruption is silently replaced with defaults

SaveAsync writes settings.json directly, without a temporary file/atomic replacement or serialization gate. Window geometry saves are fire-and-forget and can overlap Settings Save. LoadAsync catches all failures and returns defaults, so App’s intended load-warning path never sees a corrupt-file exception. A later save can overwrite the original bad file without presenting recovery.

**Verify:** Load a deliberately malformed settings file in an isolated profile. Observe the lack of warning, then save. Exercise overlapping window-close/settings-save operations.

**Required correction:** Serialize writes, atomically replace with a recoverable backup, preserve corrupt files and return an explicit recovery result to the UI.

**Evidence:** [TidyUp/Data/SettingsRepository.cs:96](https://github.com/dazzo31/Tidy-up/blob/59d3bb45d7ca622ec3a80a7c2b3b0ff21e89358c/TidyUp/Data/SettingsRepository.cs#L96) · [TidyUp/Data/SettingsRepository.cs:80](https://github.com/dazzo31/Tidy-up/blob/59d3bb45d7ca622ec3a80a7c2b3b0ff21e89358c/TidyUp/Data/SettingsRepository.cs#L80) · [TidyUp/MainWindow.xaml.cs:116](https://github.com/dazzo31/Tidy-up/blob/59d3bb45d7ca622ec3a80a7c2b3b0ff21e89358c/TidyUp/MainWindow.xaml.cs#L116)

### TU-43 · P2 · Tray health reflects the last folder event, not aggregate health

OnFolderHealthChanged sets Warning for an unhealthy event but resets Normal for any healthy event when not paused. A healthy folder update can therefore clear the tray warning while another folder is still disconnected. CreateStatusIcon also replaces the branded icon with an unlabelled coloured dot; native icon handles are created without a matching ownership cleanup path.

**Verify:** Report a disconnected folder A followed by a healthy folder B and inspect the tray status. Repeat health updates while checking icon handle ownership.

**Required correction:** Compute the highest-priority aggregate status across all folders and global pause, retain the recognizable app icon with an overlay, and explicitly manage/dispose native icons.

**Evidence:** [TidyUp/Services/TrayIconService.cs:89](https://github.com/dazzo31/Tidy-up/blob/59d3bb45d7ca622ec3a80a7c2b3b0ff21e89358c/TidyUp/Services/TrayIconService.cs#L89) · [TidyUp/Services/TrayIconService.cs:129](https://github.com/dazzo31/Tidy-up/blob/59d3bb45d7ca622ec3a80a7c2b3b0ff21e89358c/TidyUp/Services/TrayIconService.cs#L129)

### TU-44 · P2 · Batch lifetime and application state are not held consistently

The coordinator enters ExecutingBatch once, but NotifyOperationCompleted returns the state to Idle/previous state whenever the per-action count reaches zero, which happens after each serial action. _isExecuting is a flag rather than an atomic admission guard and rejected state transitions are ignored. The shell’s editing guards rely on the global state, while some embedded wizard actions do not use it.

**Verify:** Trace state-change events for a multi-item batch and attempt UI edits/navigation between progress updates. Exercise simultaneous coordinator calls in an isolated test.

**Required correction:** Hold one batch lease until the whole batch terminates, make admission atomic, preserve original state and derive command availability from a shared busy contract.

**Evidence:** [TidyUp/Services/Processing/BatchProcessingCoordinator.cs:49](https://github.com/dazzo31/Tidy-up/blob/59d3bb45d7ca622ec3a80a7c2b3b0ff21e89358c/TidyUp/Services/Processing/BatchProcessingCoordinator.cs#L49) · [TidyUp/Services/Processing/BatchProcessingCoordinator.cs:113](https://github.com/dazzo31/Tidy-up/blob/59d3bb45d7ca622ec3a80a7c2b3b0ff21e89358c/TidyUp/Services/Processing/BatchProcessingCoordinator.cs#L113) · [TidyUp/Services/State/ApplicationStateManager.cs:147](https://github.com/dazzo31/Tidy-up/blob/59d3bb45d7ca622ec3a80a7c2b3b0ff21e89358c/TidyUp/Services/State/ApplicationStateManager.cs#L147)

## GUI review by screen

The following are visual observations and design recommendations, not additional claims of runtime execution. Screenshot evidence is linked to the reviewed commit.

| Screen | Observation | Recommended change |
|---|---|---|
| Dashboard | Empty installation shows a 100% success rate and leads with operational metrics before setup. “Manage in Rules” competes with the dedicated Folders destination. Text promises real-time updates that the execution integration does not supply. | Show a setup checklist first: Add source → Create disabled rule → Preview → Apply once → Enable monitoring. Use “No runs yet” rather than 100%. Link folder management to Folders. |
| Rules | Two large empty panels each offer Create/New Rule, plus a third New Rule button in the rail. Small filter chips and icon-only actions are crowded at 1280px. The page lacks the clear heading hierarchy used elsewhere. | Use one primary Create rule empty state; label Test, Enable and Delete; retain a compact rule list once populated. Unify the two Save paths before styling them. |
| Guided editor | Result visibility/type-converter defects undermine the final review step. Wizard Save and shell Save coexist. Cancel has no shell handler. | Provide one persistent Back/Next/Save draft footer; use Preview changes as a separate explicit step and make enabling independent of saving. Test real populated stages. |
| Advanced editor | Editable nested objects are not reliably included in dirty tracking. Copy/Rename/Change Extension do not expose conflict configuration consistently with Move. | Use the same draft and validation engine as Guided; expose equivalent options and an action-specific resolved-path preview. |
| Folders | Add looks like a complete persisted workflow, although it only creates a temporary row. The single horizontal toolbar compresses paths and exclusion hints. “Accessible” mixes filesystem access with other health properties. | Persist first, then provide linked-rule selection and real monitoring status. Put long paths/exclusions on separate rows and distinguish Access, Rule linkage and Watcher state. |
| Preview | A first-class navigation destination has no rule picker/generate/refresh action. Empty-state instructions send users elsewhere without a direct action. “All Rules” is a filter over a single-rule plan, not multi-rule simulation. All Delete actions are counted under “Recycle”, including permanent deletes. | Add source rule/draft selection and Refresh preview; show plan age and identity. Separate Move/Copy/Rename/Recycle/Permanent delete/Extract/Command counts. Present conflicts as decisions the user can resolve. |
| Preview selection | “Approve All”/“Reject All” affect only filtered rows; Apply uses all approved rows, including hidden approvals. | Label Select visible/Deselect visible; show total selected and hidden selected counts, with a complete final summary. |
| History | The subtitle promises every operation although only successful operations are journalled. Export scope is ambiguous. Undo capability and outcome are inaccurate. Filter controls are compact and Clear wraps into a second row in the committed image. | Use one filter layout with explicit date labels and Apply/Clear. Show recoverability per row; distinguish restored, manual action required, failed and irreversible. |
| Logs | Unlike other pages it has a blank grid rather than a helpful empty state. The dark screenshot shows black footer status text on a dark-grey panel. Refresh placement differs from History. | Explain what Logs contains and how it differs from History. Apply theme text resources to the footer and use consistent toolbar placement and empty/loading/error states. |
| Settings | General cards occupy roughly half the available content width while the rest is empty, yet content scrolls vertically. Many settings are operationally inert. Save/Reset behaviour is not consistently explained. | Remove inert settings first. Use a readable full-width column or responsive two-column layout; provide per-field validation and explicit saved/unsaved feedback. |
| Dark theme | Empty-state glyphs remain black against very dark circular badges, making them hard to distinguish across Dashboard, Rules, Folders, Preview and History. Token-palette contrast tests did not catch inherited icon foregrounds. | Verify the actual icon Foreground dependency property/resource assignment, then check every populated and empty surface visually; do not treat screenshots captured at earlier Phase 7 commits as proof of the current live rendering. |
| Shell/tray | Navigation is generally understandable, but the 220px rail plus rule list constrain editor width. Global keybindings include Delete and Space; their interaction with focused inputs needs Windows testing. Tray identity becomes a coloured dot. | Test compact width and keyboard focus; scope destructive shortcuts to the rule list, retain branded tray identity and show a truthful aggregate runtime status. |

Screenshot evidence: [dashboard-light](https://github.com/dazzo31/Tidy-up/blob/59d3bb45d7ca622ec3a80a7c2b3b0ff21e89358c/docs/screenshots/dashboard-light.png) · [rules-light](https://github.com/dazzo31/Tidy-up/blob/59d3bb45d7ca622ec3a80a7c2b3b0ff21e89358c/docs/screenshots/rules-light.png) · [folders-dark](https://github.com/dazzo31/Tidy-up/blob/59d3bb45d7ca622ec3a80a7c2b3b0ff21e89358c/docs/screenshots/folders-dark.png) · [preview-dark](https://github.com/dazzo31/Tidy-up/blob/59d3bb45d7ca622ec3a80a7c2b3b0ff21e89358c/docs/screenshots/preview-dark.png) · [history-light](https://github.com/dazzo31/Tidy-up/blob/59d3bb45d7ca622ec3a80a7c2b3b0ff21e89358c/docs/screenshots/history-light.png) · [logs-dark](https://github.com/dazzo31/Tidy-up/blob/59d3bb45d7ca622ec3a80a7c2b3b0ff21e89358c/docs/screenshots/logs-dark.png) · [settings-light](https://github.com/dazzo31/Tidy-up/blob/59d3bb45d7ca622ec3a80a7c2b3b0ff21e89358c/docs/screenshots/settings-light.png)

## Other inconsistencies and verification gaps

- Condition-editor matching is capped at the first 500 enumerated files per folder, whereas final planning scans all files. The UI should label sample coverage, total inspected and skipped errors; it must not imply both counts cover the same population.
- “Healthy/Watching” is partly inferred from directory access and enabled rule counts. Neither establishes that an actual watcher exists or is processing events. Runtime state must be measured, not inferred from configuration.
- Rule enablement has multiple representations: immediate shell toggle and a wizard-local IsRuleEnabled value. Editing, enabling and saving need one explicit contract so users know when an operation takes effect.
- Preflight checks only existence and modification-time differences greater than two seconds. It does not verify file size/hash or revalidate destination effects, so it is not a complete stale-plan guarantee. Stronger validation is particularly important for destructive actions.
- Preview plans exclude nonmatching files, so the explanation drawer cannot explain why an absent file did not match. Keep a separate scan-decision view that includes excluded, skipped, inaccessible and nonmatching files.
- Settings Reset has no confirmation/undo and replacing settings can re-enable first-run setup. Make the reset scope explicit; protect lifecycle/profile settings from an accidental cosmetic reset.
- The application has several parallel implementations: two import services, two plan/preview paths, two save paths, two scan semantics, two execution histories and two explanation engines. The observed defects cluster at those boundaries. Consolidation should be targeted around shared contracts, not a wholesale rewrite.
- Run Command uses cmd.exe with string interpolation and a blacklist, rather than a structured executable/argument contract. The blacklist is not a comprehensive shell-escaping boundary, and nonwaiting commands redirect output without the normal drain/wait path. Treat this as a separate hardening review before advertising safe handling of arbitrary filenames or long-running commands.
- There is no justification to repeat historical “complete” claims from docs or to equate presence of a service/unit test with a working user workflow. Audit feature documentation against reachable UI and production composition.

## Intended workflow after correction

1. First launch explains whether monitoring is stopped, running or paused and offers one clear setup action.
2. Add and persist source folders. Show existence, access, recursion, exclusions, linked rules and actual watcher state independently.
3. Create/edit a disabled draft using either Guided or Advanced. Both edit the same deep-cloned draft, validate the same fields and preserve hidden metadata.
4. Generate a cancellable preview from the draft without enabling automation. Show incomplete scans, matched/unmatched files, resolved concrete paths, action dependencies and every destructive side effect.
5. Resolve conflicts, select complete file-action chains, review hidden selections and approve an immutable plan.
6. Revalidate the plan and execute through one coordinator. Hold busy state for the entire batch, journal intent/outcome and stop dependent actions after a failure.
7. Show authoritative actual results, including skipped/cancelled/partial outcomes. Update History, Dashboard, rule statistics and notifications from the same outcomes.
8. Save and explicitly enable automatic monitoring only when desired. Pause controls must affect the same runtime coordinator; scheduling and power constraints must be enforced there.
9. Undo only supported effects. Show manual restoration instructions or irreversible status honestly. Never mark guidance as restored.

## Recommended implementation order

1. **Contain misleading/destructive behaviours:** correct Undo Delete, extraction safeguards, overwrite handling, fake maintenance success and nonfunctional Pause/Add feedback. Keep unsupported features visibly unavailable.
2. **Unify drafts and planning:** fix save/discard, disabled-rule simulation, wizard bindings, immutable plans, chains and conflict decisions. Add regression tests for the exact triggers above.
3. **Build the real execution lifecycle:** durable operation coordinator, monitoring startup/change handling, folder scope, settings injection, scheduling, pause and shutdown. Do not merely call StartAsync and assume the rest is wired.
4. **Unify reporting and recovery:** journal all outcomes with rule identity, consume actual results, refresh metrics, correct rollback capabilities and export scope.
5. **Polish the GUI on populated fixtures:** layouts, labels, dark icons, keyboard focus, DPI, long paths, status clarity and empty/error states. Update Help/samples from shared metadata.
6. **Verify and package the exact tag:** build/test on Windows, perform acceptance scenarios below, then publish an asset with a checksum and commit identity that matches the reviewed release.

## Windows acceptance tests still required

These were not executed here. Use an isolated TIDYUP_APPDATA and disposable filesystem fixtures, never the user’s live organising folders.

| Scenario | Required evidence |
|---|---|
| Fresh install and upgraded profile | Correct migration, truthful setup state, saved choices honored, one successful disabled-draft preview before any activation. |
| Folder lifecycle | Add → refresh → restart retains the folder; pause stops actual dispatch; resume is controlled; network/missing/read-only folders show truthful state. |
| Rule editing | Guided ↔ Advanced preserves every field; Cancel restores baseline; failed save keeps dirty state; both Save buttons/keyboard route produce identical records. |
| Chains and variables | Rename→Move, Copy→source Rename, Copy→copy Rename, extraction/command continuation and counter subsets produce exactly the approved paths. |
| Conflicts and changes after preview | Existing target, two planned identical targets, newly created target, changed source, vanished source and overlapping folders all have deterministic, disclosed outcomes. |
| Destructive actions | Recycle, permanent delete, overwrite, extraction-delete and empty-directory cleanup show all effects and stay within explicit boundaries. |
| Recovery | Move/copy undo really changes disk state; modified targets are protected; Delete guidance is never marked restored; irreversible operations are accurately labelled. |
| Fault injection | Disk-full, denied access, SQLite journal failure, interrupted operation and process restart retain evidence of partial completion and offer recovery. |
| History and metrics | Failures/successes/skips appear consistently; rule names populate; pagination works; filtered exports have declared scope; statistics update. |
| Concurrency/lifecycle | Batch cancellation stops future work safely; no conflicting batch/edit races; tray Exit waits or prompts; update monitoring does not reuse a cancelled token. |
| Visual and accessibility | Populated and empty screens at 900×600 and 1280×720, 100/125/150/200% scaling, Light/Dark/System/high contrast, keyboard-only navigation and screen-reader names. |
| Long/large data | Long UNC paths and rule names, hundreds of rules, thousands of planned actions, large history, inaccessible descendants and large files remain responsive. |

The existing UiWorkflowEndToEndIntegrationTests constructs a separate DI graph and inserts a rule through a test helper; it does not launch the app or prove startup monitoring, a full Apply/Undo cycle or rendered bindings. Keep unit tests, but add real composition-root and Windows GUI acceptance coverage. The CI vulnerability check should also fail on tool/audit failure, not only on a matched English output phrase.

[Workflow integration test](https://github.com/dazzo31/Tidy-up/blob/59d3bb45d7ca622ec3a80a7c2b3b0ff21e89358c/TidyUp.Tests/Integration/UiWorkflowEndToEndIntegrationTests.cs) · [Screenshot capture script](https://github.com/dazzo31/Tidy-up/blob/59d3bb45d7ca622ec3a80a7c2b3b0ff21e89358c/scripts/Capture-Screenshots.ps1)

## What is worth retaining

The persistent navigation rail, disabled-by-default new rules, shared folder-normalization/catalog direction, explicit dry-run concept, separate execution journal, rollback hash checks, migration/backups and centralized theme resources are useful foundations. The repair should make these foundations agree at runtime. It should not replace working modules merely to create a new architecture.

No repository code, issues, pull requests or release settings were changed during this review. This report is a bounded, evidence-based review, not a claim that every possible defect has been discovered.
