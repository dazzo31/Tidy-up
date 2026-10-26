# TidyUp v1.2.0 Remediation: Master Task Tracker

**Baseline Tag:** `v1.2.0`  
**Current Commit:** `59d3bb45d7ca622ec3a80a7c2b3b0ff21e89358c`  
**Baseline Test Suite:** 623 passed, 0 failed, 0 skipped (.NET 9.0.320, Windows 11 Build 10.0.26300)  
**Execution Contract:** `docs/REMEDIATION_TASK_CATALOG_v1.2.0.md` (Revision 2)

---

## Canonical Task State Legend
- `NOT_STARTED`: Task is in the queue, prerequisites not yet verified or execution not begun.
- `IN_PROGRESS`: Actively undergoing implementation and testing under an explicit Task Lock.
- `IMPLEMENTED_UNVERIFIED`: Code modifications made, pending automated regression and acceptance evidence.
- `BLOCKED_ENVIRONMENT`: Blocked by missing toolchain, permissions, or platform capabilities.
- `BLOCKED_DEPENDENCY`: Blocked by a prerequisite task or unmerged architectural contract.
- `BLOCKED_DECISION`: Blocked pending an unresolved material product/architecture choice.
- `DONE`: All acceptance criteria and regression tests verified with concrete evidence.

---

## Master Gated Task Tracker

### Phase 0: Baseline, Contracts & Containment
| Task ID | Title / Focus | State | Dependencies | Current SHA | Evidence / Notes | Next Action |
| :--- | :--- | :---: | :--- | :---: | :--- | :--- |
| `TASK-BASE-01` | Pin baseline, create records, revalidate 44 findings | **DONE** | None | `59d3bb4` | 623 tests green; baseline doc suite created | Proceed to `TASK-BASE-02` |
| `TASK-BASE-02` | Set shared contracts before touching consumers | **DONE** | `TASK-BASE-01` | `a654c32` | RollbackOutcome, ExecutionAuthorizationContext, RuleDraft, ActionContinuationResolver | Proceed to `TASK-BASE-03` |
| `TASK-BASE-03` | Apply narrow containment before permanent repairs | **DONE** | `TASK-BASE-02` | `0554f3e` | Fail-closed Prompt, clamped cleanup, Undo guards active | Phase 0 Gate Passed $\to$ Proceed to Phase 1 (`TASK-REM-08`) |

### Phase 1: Rule & Configuration Integrity
| Task ID | Title / Focus | State | Dependencies | Current SHA | Evidence / Notes | Next Action |
| :--- | :--- | :---: | :--- | :---: | :--- | :--- |
| `TASK-REM-08` | Deep-cloned rule draft engine & shared save/discard | **DONE** | Phase 0 Gate | `ac747af` | RuleDraft unified save/discard/cancel, metadata lossless, 633 tests pass | Proceed to `TASK-REM-09` |
| `TASK-REM-09` | Rule list drag-and-drop order persistence | **DONE** | `TASK-REM-08` | `ae9ffd4` | Atomic priority reorder in SQLite, UI drag gating, rollback on failure, 637 tests pass | Proceed to `TASK-REM-10` |
| `TASK-REM-10` | Unified schema-aware rule serialization & import gate | **DONE** | `TASK-REM-09` | `55d01b2` | Consolidated on RuleSerializationService, disabled-on-import invariant, atomic batch commit, 647 tests pass | Proceed to `TASK-REM-11` |
| `TASK-REM-11` | Variable engine normalization & filename sanitization | NOT_STARTED | `TASK-REM-10` | — | Targets: TU-39, TU-40 | Sanitize time colons & check tokens |
| `TASK-REM-34` | Atomic settings persistence & corrupt profile recovery | NOT_STARTED | `TASK-REM-11` | — | Target: TU-42 | Atomic replace & corrupt backup |
| `TASK-REM-28` | Typed AppSettings DI registration & service config | NOT_STARTED | `TASK-REM-34` | — | Target: TU-28 | DI singleton & quiet hours |

### Phase 2: Truthful Read-Only Planning
| Task ID | Title / Focus | State | Dependencies | Current SHA | Evidence / Notes | Next Action |
| :--- | :--- | :---: | :--- | :---: | :--- | :--- |
| `TASK-REM-12` | XAML converter & visibility binding repairs | NOT_STARTED | Phase 1 Gate | — | Targets: TU-06, TU-07 | Fix inverse bool & review plan bindings |
| `TASK-REM-13` | Disabled rule dry-run simulation decoupling | NOT_STARTED | `TASK-REM-12` | — | Target: TU-05 | Preview matching without activation |
| `TASK-REM-14` | Multi-action virtual path continuation & dependency model | NOT_STARTED | `TASK-REM-13` | — | Targets: TU-13, TU-14 | InitialOrigin & action dependencies |
| `TASK-REM-15` | Target reservation & conflict resolution simulation | NOT_STARTED | `TASK-REM-14` | — | Target: TU-16 | Batch target ledger & RenameNew sim |
| `TASK-REM-16` | Preservation of subfolder directory hierarchy | NOT_STARTED | `TASK-REM-15` | — | Target: TU-31 | Relative root path calculation |
| `TASK-REM-17` | Responsive background enumeration & partial scan disclosure | NOT_STARTED | `TASK-REM-16` | — | Target: TU-25 | Async enumeration & permission handling |
| `TASK-REM-18` | Preview cache invalidation & explicit refresh triggers | NOT_STARTED | `TASK-REM-17` | — | Target: TU-12 | Stale plan invalidation & refresh button |
| `TASK-REM-27` | Scan command semantic harmonization | NOT_STARTED | `TASK-REM-18` | — | Target: TU-04 | Harmonize Folders/Dashboard scan |

### Phase 3: Durable Execution Foundation
| Task ID | Title / Focus | State | Dependencies | Current SHA | Evidence / Notes | Next Action |
| :--- | :--- | :---: | :--- | :---: | :--- | :--- |
| `TASK-REM-22` | Two-phase durable journaling & rule metadata tracking | NOT_STARTED | Phase 2 Gate | — | Target: TU-21 | Intent-before-mutation & RuleId links |
| `TASK-REM-19` | Immutable planned parameter execution & {counter} fix | NOT_STARTED | `TASK-REM-22` | — | Target: TU-15 | Execute frozen plan paths, no drift |
| `TASK-REM-20` | Atomic batch lifetime lease & state machine guarding | NOT_STARTED | `TASK-REM-19` | — | Target: TU-44 | ExecutingBatch lease held across batch |

### Phase 4: Safety, Recovery & Outcomes
| Task ID | Title / Focus | State | Dependencies | Current SHA | Evidence / Notes | Next Action |
| :--- | :--- | :---: | :--- | :---: | :--- | :--- |
| `TASK-REM-03` | Permanent deletion authorization decoupling | NOT_STARTED | Phase 3 Gate | — | Target: TU-19 | Scoped execution authorization context |
| `TASK-REM-04` | Overwrite & Prompt safe staging & displaced backup | NOT_STARTED | `TASK-REM-03` | — | Target: TU-17 | Staged atomic replace & displaced backup |
| `TASK-REM-02` | Archive extraction safeguards & overwrite containment | NOT_STARTED | `TASK-REM-04` | — | Target: TU-18 | Safe archive recycling & manifest check |
| `TASK-REM-05` | Monitored root boundary clamping for empty cleanup | NOT_STARTED | `TASK-REM-02` | — | Target: TU-32 | StopBoundary clamping at monitored root |
| `TASK-REM-01` | Truthful rollback & recovery contract | NOT_STARTED | `TASK-REM-05` | — | Targets: TU-20, TU-38 | Restored vs ManualRestoreRequired |
| `TASK-REM-23` | Authoritative discrepancy reporting from batch results | NOT_STARTED | `TASK-REM-01` | — | Target: TU-23 | Correlate by ActionId, not UI rows |
| `TASK-REM-21` | Post-execution selection & Apply button predicate | NOT_STARTED | `TASK-REM-23` | — | Target: TU-24 | PendingApprovedCount auto-disables Apply |

### Phase 5: Application Monitoring & Lifecycle
| Task ID | Title / Focus | State | Dependencies | Current SHA | Evidence / Notes | Next Action |
| :--- | :--- | :---: | :--- | :---: | :--- | :--- |
| `TASK-REM-25` | Watcher cancellation token lifecycle & restart resilience | NOT_STARTED | Phase 4 Gate | — | Target: TU-34 | Fresh CTS on UpdateRulesAsync |
| `TASK-REM-26` | Multilateral watcher scope merging for overlapping folders | NOT_STARTED | `TASK-REM-25` | — | Target: TU-35 | Merged scope, per-rule event dispatch |
| `TASK-REM-24` | Application monitoring coordinator & startup activation | NOT_STARTED | `TASK-REM-26` | — | Target: TU-01 | Startup watching via shared pipeline |
| `TASK-REM-07` | Truthful folders management & persistent catalog actions | NOT_STARTED | `TASK-REM-24` | — | Targets: TU-02, TU-03 | Standalone folder persistence & pause |
| `TASK-REM-29` | Window lifecycle & close/minimize conflict resolution | NOT_STARTED | `TASK-REM-07` | — | Target: TU-41 | Respect e.Cancel & restore window state |
| `TASK-REM-30` | Aggregate tray icon health & native handle management | NOT_STARTED | `TASK-REM-29` | — | Target: TU-43 | Aggregate status & DestroyIcon cleanup |

### Phase 6: Reporting & Settings Honesty
| Task ID | Title / Focus | State | Dependencies | Current SHA | Evidence / Notes | Next Action |
| :--- | :--- | :---: | :--- | :---: | :--- | :--- |
| `TASK-REM-31` | Operation stream convergence for Dashboard & Logs | NOT_STARTED | Phase 5 Gate | — | Target: TU-22 | Live telemetry stream & rule stats |
| `TASK-REM-32` | History pagination command notification & eligibility | NOT_STARTED | `TASK-REM-31` | — | Target: TU-36 | NotifyCanExecute on total pages change |
| `TASK-REM-33` | Context-sensitive filtered history export | NOT_STARTED | `TASK-REM-32` | — | Target: TU-37 | Export filtered matches vs all |
| `TASK-REM-06` | Genuine database maintenance & vacuum | NOT_STARTED | `TASK-REM-33` | — | Target: TU-27 | Real SQLite PRAGMA & VACUUM |
| `TASK-REM-35` | Settings surface audit & inert control containment | NOT_STARTED | `TASK-REM-06` | — | Target: TU-26 | Wire settings or mark unavailable |
| `TASK-REM-36` | Component vs feature honest matrix & unwired stubs | NOT_STARTED | `TASK-REM-35` | — | Target: TU-33 | Disclose network & scheduling bounds |

### Phase 7: Visual & Integrated Acceptance
| Task ID | Title / Focus | State | Dependencies | Current SHA | Evidence / Notes | Next Action |
| :--- | :--- | :---: | :--- | :---: | :--- | :--- |
| `TASK-REM-37` | Dark theme glyph & empty state contrast polish | NOT_STARTED | Phase 6 Gate | — | Target: Review Visuals | Empty state icons >= 4.5:1 in dark mode |
| `TASK-REM-38` | Full Windows acceptance test execution & composition root | NOT_STARTED | `TASK-REM-37` | — | Target: Acceptance Matrix | End-to-end scenarios on temp fixtures |

### Phase 8: Candidate Packaging & Handoff
| Task ID | Title / Focus | State | Dependencies | Current SHA | Evidence / Notes | Next Action |
| :--- | :--- | :---: | :--- | :---: | :--- | :--- |
| `TASK-REM-39` | Release candidate packaging, provenance & handoff | NOT_STARTED | Phase 7 Gate | — | Target: Publication | Package win-x64, SHA-256 hashes |
