# TidyUp: Release Verification Checklist — v1.1.0

**Status:** PENDING manual execution
**Build under test:** `main` after REL-01..REL-04 (folder-catalog feature + dependency fixes + CI gate + docs)
**Artifact:** `dotnet publish TidyUp/TidyUp.csproj -c Release -r win-x64`

> Run these against the **published** executable (`TidyUp-win-x64\TidyUp.exe`), not the IDE build, on a clean test profile. Each item traces back to a baseline defect that must be confirmed dead. Record PASS/FAIL with notes; any FAIL is a P0 blocker for the `v1.1.0` tag.

---

## 1. Folder Monitoring (was DEF-P0-03 "disconnected engine")
- [ ] App launches and `FileMonitorService` starts automatically.
- [ ] Adding a rule with a monitored folder registers the watcher.
- [ ] Dropping a matching file into the folder triggers processing.
- [ ] Tray → "Pause Monitoring" actually pauses (no processing while paused).
- [ ] Tray → "Resume Monitoring" resumes.

## 2. Delete Safety (was DEF-P0-01 / DEF-P0-02)
- [ ] A Delete action lands files in the Windows **Recycle Bin** (verify in Explorer), not permanent delete.
- [ ] Overwrite-conflict Move does **not** destroy the destination file when the move fails.
- [ ] `RemoveEmptyFolders` never climbs above the monitored root (no `Downloads`/`Documents` collateral).

## 3. Preview Honesty (was DEF-P1-01 "preview has no apply")
- [ ] Preview → Review → **Two-step Apply** flow is reachable end-to-end.
- [ ] Simulation is read-only: source file timestamps/hashes unchanged after preview.

## 4. Rollback (was DEF-P0-06 "missing journal")
- [ ] Execute a batch, then roll it back from History.
- [ ] SHA-256 pre-check **refuses** rollback when the target was externally modified.

## 5. Watcher Resilience (was DEF-P0-04 "buffer overflow loss")
- [ ] Drop ~500 files rapidly; health badge shows degraded → recovered; no silent loss.

## 6. Tray & State (was DEF-P0-03 tray stubs)
- [ ] Minimize-to-tray works; quiet-hours and battery-throttle (simulated ≤20%) behave.

## 7. Empty-Folder Loop Guard (was DEF-P0-05)
- [ ] No infinite reprocessing loop when a rule's destination is inside a monitored folder.

## 8. First-Run & Onboarding (was DEF-P1-03 "unreachable wizard")
- [ ] Fresh profile shows the wizard exactly once.

## 9. Folder Catalog (REL-01 feature)
- [ ] Folders Hub, Dashboard, and Rule Wizard stay in sync after adding/removing a monitored folder.
- [ ] Path casing/slashes/UNC duplicates collapse to a single canonical entry.
- [ ] Missing directory shows "does not exist"; access-denied shows the correct permission status.

## 10. Accessibility Spot Check
- [ ] Keyboard-only pass over the 6 navigation destinations (Ctrl+1..Ctrl+6, Tab/Enter/Space/Esc).
- [ ] Screen reader announces icon buttons and status updates.

---

**Result:** `____ / 10 areas PASS` — Signature: ______ — Date: ______
