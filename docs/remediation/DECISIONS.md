# TidyUp v1.2.0 Remediation: Architectural Decision Records (ADRs)

**Baseline Tag:** `v1.2.0`  
**Current Commit:** `59d3bb45d7ca622ec3a80a7c2b3b0ff21e89358c`  
**Date:** 2026-10-26

---

## ADR-01: Serial Execution Integrity over Speculative Parallelism
- **Context:** `AppSettings` defines `MaxConcurrentOperations = 4`, but the underlying file processing architecture (`BatchProcessingCoordinator`) operates sequentially. File operations have inter-action dependencies (e.g. Rename $\to$ Move), shared destination conflicts, and single-resource filesystem locking.
- **Decision:** Keep batch execution serial and deterministic for the v1.2.0 remediation. Concurrently running file operations across multiple threads requires complex cross-volume destination file locking and atomic rollback barriers that are out of scope.
- **Consequences:** The UI setting for concurrency will be locked to serial (1) with an honest explanation in the UI ("Parallel processing planned for future major release"). Zero race conditions during batch execution.

---

## ADR-02: Scoped Authorization Context for Destructive File Operations
- **Context:** `ExecuteDeleteAsync` previously checked `ConfirmBeforeDelete == false`, which blocked confirmed operations if the user kept the safety setting checked. A proposed alternative was passing a raw `bool isUserAuthorized = true`.
- **Decision:** Reject raw booleans. Implement an immutable `ExecutionAuthorizationContext` bound explicitly to `PlanId`, `RuleId`, the exact set of approved `ActionIds`, and concrete target paths. The authorization is issued only by the confirmation dialog boundary and is valid only for that specific plan execution.
- **Consequences:** Unrelated operations, background watcher events, or stale plans cannot inherit or spoof authorization.

---

## ADR-03: Two-Phase Intent Journaling (Intent Before Mutation)
- **Context:** `OperationJournalEntry` was previously written only *after* an action succeeded. If an action failed, crashed, or was terminated mid-write, no durable record existed, making rollback impossible and leaving History inconsistent.
- **Decision:** Write an `Intent` record to SQLite inside a transaction *before* executing the filesystem mutation. Upon completion, update the journal record to `Completed` or `Failed` with actual hashes and outcome details. If process termination occurs during mutation, startup integrity check reconciles pending intents.
- **Consequences:** Eliminates silent filesystem mutations; guarantees auditability even across sudden process crashes.

---

## ADR-04: Separation of Preview Matching from Automation Activation
- **Context:** Newly created rules default to `Disabled`. `RuleEngine.EvaluateRule` returned `false` for any disabled rule, which completely broke dry-run simulations in the Rule Wizard and Preview workspace.
- **Decision:** Separate the question "Does this file match this rule's conditions?" from "Is this rule authorized for automatic background execution?". Introduce an evaluation mode parameter (`ignoreEnabledState = true` for read-only preview/wizard simulations).
- **Consequences:** Users can test and dry-run draft rules against real folders before enabling them. Automated background watchers continue to enforce `IsEnabled == true`.

---

## ADR-05: True Rollback Capabilities vs Manual Recycle Bin Guidance
- **Context:** Undoing a deleted file previously returned `Success = true`, causing History to falsely mark the record as "RolledBack" and say "Operation successfully reversed", when in reality the file was sitting in the Windows Recycle Bin untouched.
- **Decision:** Differentiate rollback capabilities via `RollbackOutcome` (`Restored`, `ManualRestoreRequired`, `NotRecoverable`). Recycle Bin deletions return `ManualRestoreRequired` with instructions; permanent deletions return `NotRecoverable`. Only verifiable inverse actions (such as moving a file back from its destination) return `Restored` and update the database status to `RolledBack`.
- **Consequences:** Eliminates deceptive UI claims; provides honest and accurate recovery status to the user.
