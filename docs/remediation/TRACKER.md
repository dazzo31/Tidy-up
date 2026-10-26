# TidyUp v1.2.0 Remediation: Master Task Tracker

**Baseline Tag:** `v1.2.0`  
**Current Commit:** `59d3bb45d7ca622ec3a80a7c2b3b0ff21e89358c`  
**Baseline Test Suite:** 623 passed, 0 failed, 0 skipped (.NET 9.0.320, Windows 11 Build 10.0.26300)  
**Execution Contract:** `docs/REMEDIATION_TASK_CATALOG_v1.2.0.md` (Revision 2)

---

## Canonical Task State Legend
- `NOT_STARTED`: Task is in the queue, prerequisites not yet verified or execution not begun.
- `IN_PROGRESS`: Actively undergoing implementation and testing under an explicit Task Lock.
- `IMPLEMENTED_UNVERIFIED`: Code modifications made, 0d97a5d automated regression and acceptance evidence.
- `BLOCKED_ENVIRONMENT`: Blocked by missing toolchain, permissions, or platform capabilities.
- `BLOCKED_DEPENDENCY`: Blocked by a prerequisite task or unmerged architectural contract.
- `BLOCKED_DECISION`: Blocked 0d97a5d an unresolved material product/architecture choice.
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
| `TASK-REM-34` | Atomic settings persistence & corrupt profile recovery | **DONE** | `TASK-REM-11` | `3d6b720` | Atomic temp write & File.Replace with .bak, corrupt timestamp preservation, backup recovery, concurrency guard, 661 tests pass | Proceed to `TASK-REM-28` |
| `TASK-REM-28` | Typed AppSettings DI registration & service config | **DONE** | `TASK-REM-34` | `d871dce` | AppSettings typed DI singleton, injected into TrayIconService & ToastNotificationService, SystemTrayEnabled honored, fail-safe notifications, state sync, 667 tests pass | Phase 1 Gate Passed $\to$ Proceed to Phase 2 (`TASK-REM-12`) |

### Phase 2: Truthful Read-Only Planning
| Task ID | Title / Focus | State | Dependencies | Current SHA | Evidence / Notes | Next Action |
| :--- | :--- | :---: | :--- | :---: | :--- | :--- |
| `TASK-REM-12` | XAML converter & visibility binding repairs | **DONE** | Phase 1 Gate | 0d97a5d | InverseBooleanToVisibilityConverter & NullToVisibilityConverter implemented and wired across Stage4, Wizard, and Preview, 676 tests pass | Proceed to `TASK-REM-13` |
| `TASK-REM-13` | Disabled rule dry-run simulation decoupling | **DONE** | `TASK-REM-12` | f491006 | RuleEvaluationPurpose enum and MatchesConditions decoupling simulation from live execution, 680 tests pass | Proceed to `TASK-REM-14` |
| `TASK-REM-14` | Multi-action virtual path continuation & dependency model | **DONE** | `TASK-REM-13` | 5ae68ac | InitialSourcePath and DependsOnActionId modeled, type-aware continuation, preflight dependency closure, 683 tests pass | Proceed to `TASK-REM-15` |
| `TASK-REM-15` | Target reservation & conflict resolution simulation | **DONE** | `TASK-REM-14` | `10c3e56` | In-memory target reservation ledger, RenameNew projected counter simulation, Skip simulation without false conflicts, batch-internal collision detection, 686 tests pass | Proceed to `TASK-REM-16` |
| `TASK-REM-16` | Preservation of subfolder directory hierarchy | **DONE** | `TASK-REM-15` | `9cd74c5` | Relative root calculation with confinement, CopyAction UI binding, MonitoredSourceRoot on domain model & planned action, 692 tests pass | Proceed to `TASK-REM-17` |
| `TASK-REM-17` | Responsive background enumeration & partial scan disclosure | **DONE** | `TASK-REM-16` | `bcd694c` | Resilient background folder enumeration, per-folder exception containment, IsIncomplete disclosure banner, generation IDs & cancellation, 696 tests pass | Proceed to `TASK-REM-18` |
| `TASK-REM-18` | Preview cache invalidation & explicit refresh triggers | **DONE** | `TASK-REM-17` | `c8165c1` | Stale plan invalidation on rule switch/save/discard, RefreshPreviewCommand, PlanHeaderInfo, Refresh button in header, 700 tests pass | Proceed to `TASK-REM-27` |
| `TASK-REM-27` | Scan command semantic harmonization | **DONE** | `TASK-REM-18` | `87ca750` | Reconciler unified across Folders & Dashboard, IsScanning visual feedback & progress bar, CanScan gating, 703 tests pass | Phase 2 Gate Passed $\to$ Proceed to Phase 3 (`TASK-REM-22`) |

### Phase 3: Durable Execution Foundation
| Task ID | Title / Focus | State | Dependencies | Current SHA | Evidence / Notes | Next Action |
| :--- | :--- | :---: | :--- | :---: | :--- | :--- |
| `TASK-REM-22` | Two-phase durable journaling & rule metadata tracking | DONE | Phase 2 Gate | `a38ec5b` | TU-21: durable journal success/failure, RuleId/RuleName/RuleRevisionId, displaced backup path in details, 706 tests passing | Proceed to `TASK-REM-19` |
| `TASK-REM-19` | Immutable planned parameter execution & {counter} fix | DONE | `TASK-REM-22` | `2ec3b27` | TU-15: immutable planned parameters, frozen counter & target path preservation on partial selection, 709 tests passing | Proceed to `TASK-REM-20` |
| `TASK-REM-20` | Atomic batch lifetime lease & state machine guarding | DONE | `TASK-REM-19` | `79b1ac2` | TU-44: exclusive batch execution lease, atomic admission guard, zero state flickering between files, 711 tests passing | Phase 3 Gate Passed $	o$ Proceed to Phase 4 (`TASK-REM-03`) |

### Phase 4: Safety, Recovery & Outcomes
| Task ID | Title / Focus | State | Dependencies | Current SHA | Evidence / Notes | Next Action |
| :--- | :--- | :---: | :--- | :---: | :--- | :--- |
| `TASK-REM-03` | Permanent deletion authorization decoupling | DONE | Phase 3 Gate | `9abbccd` | TU-19: scoped ExecutionAuthorizationContext, decoupled from ConfirmBeforeDelete preference, fails closed without auth, 714 tests pass | Proceed to `TASK-REM-04` |
| TASK-REM-04 | Overwrite & Prompt safe staging & displaced backup | DONE | TASK-REM-03 | 10f995e | TU-17: staged displaced backup on overwrite, restored on mutation failure, Prompt fails closed without data loss, 717 tests pass | Proceed to TASK-REM-02 |
| `TASK-REM-02` | Archive extraction safeguards & overwrite containment | DONE | `TASK-REM-04` | `f118035` | TU-18: safe archive recycling, overwrite collision detection in simulation, safeguard confirmation risk evaluation, 721 tests pass | Proceed to `TASK-REM-05` |
| `TASK-REM-05` | Monitored root boundary clamping for empty cleanup | DONE | `TASK-REM-02` | `68fdedd` | TU-32: strict stop boundary clamping at monitored root in RemoveEmptyFoldersAsync, root preservation verified, 723 tests pass | Proceed to `TASK-REM-01` |
| `TASK-REM-01` | Truthful rollback & recovery contract | DONE | `TASK-REM-05` | `761fcfc` | TU-20, TU-38: explicit RollbackOutcome enum, delete returns ManualRestoreRequired without marking RolledBack, CanRollback disabled for permanent delete, 727 tests pass | Proceed to `TASK-REM-23` |
| `TASK-REM-23` | Authoritative discrepancy reporting from batch results | DONE | `TASK-REM-01` | `993f832` | TU-23: feed BatchItemExecutionResult into analyzer, correlate by ActionId, report actual renamed paths, 729 tests pass | Proceed to `TASK-REM-21` |
| `TASK-REM-21` | Post-execution selection & Apply button predicate | DONE | `TASK-REM-23` | `187eca3` | TU-24: single eligibility predicate PendingApprovedCount, auto-disables Apply upon completion, terminal captions, 732 tests pass | Phase 4 Gate Passed → Proceed to Phase 5 (`TASK-REM-25`) |

### Phase 5: Application Monitoring & Lifecycle
| Task ID | Title / Focus | State | Dependencies | Current SHA | Evidence / Notes | Next Action |
| :--- | :--- | :---: | :--- | :---: | :--- | :--- |
| `TASK-REM-25` | Watcher cancellation token lifecycle & restart resilience | DONE | Phase 4 Gate | `9ed8679` | TU-34: serialized lifecycle, separate applicationStoppingToken and runCts, fresh linked token on UpdateRulesAsync, 735 tests pass | Proceed to `TASK-REM-26` |
| `TASK-REM-26` | Multilateral watcher scope merging for overlapping folders | DONE | `TASK-REM-25` | `f40f44b` | TU-35: merged watcher superset scope, per-folder subfolder recursion & wildcard exclusion preservation, 738 tests pass | Proceed to `TASK-REM-24` |
| `TASK-REM-24` | Application monitoring coordinator & startup activation | DONE | `TASK-REM-26` | `cf4f107` | TU-01: MonitoringCoordinator orchestrates background watcher events, generates single-file plans, processes through BatchProcessingCoordinator; wired into App.OnStartup, 739 tests pass | Proceed to `TASK-REM-07` |
| `TASK-REM-07` | Truthful folders management & persistent catalog actions | DONE | `TASK-REM-24` | `18afd1d` | TU-02, TU-03: AddStandaloneFolderAsync & SetFolderPausedAsync persisted in catalog, PauseFolder/ResumeFolder on IFileMonitorService, optimistic UI revert, 746 tests pass | Proceed to `TASK-REM-29` |
| `TASK-REM-29` | Window lifecycle & close/minimize conflict resolution | DONE | `TASK-REM-07` | `dee8400` | TU-41: IWindowCloseCoordinator harmonizes close/minimize/exit, respects e.Cancel, preserves restored WindowState, waits for batches, 755 tests pass | Proceed to `TASK-REM-30` |
| `TASK-REM-30` | Aggregate tray icon health & native handle management | DONE | `TASK-REM-29` | `e198ea1` | TU-43: per-folder health tracking in ConcurrentDictionary, aggregate status computation (warning not cleared by healthy sibling), branded icon preservation with status badge dot, DestroyIcon GDI handle cleanup, 757 tests pass | Phase 5 Gate Passed → Proceed to Phase 6 (`TASK-REM-31`) |

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
