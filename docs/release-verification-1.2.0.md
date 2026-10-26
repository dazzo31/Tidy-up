# Release Verification — v1.2.0

Verification of the Phase 7 quality, visual-consistency, and integrated acceptance release, performed on Windows 11 with
.NET SDK 9.0 and the `Release` configuration.

Evidence collected against commit `1243465` and Phase 7 remediation tasks (`TASK-REM-37` and `TASK-REM-38`).

---

## 1. Build & test gates

| Gate | Command | Result |
| :-- | :-- | :-- |
| Release build | `dotnet build TidyUp.sln -c Release` | **PASS** — 0 errors, 0 breaking warnings |
| Full test suite | `dotnet test TidyUp.sln -c Release` | **PASS** — 777 passed, 0 failed, 0 skipped |
| Vulnerable packages | `dotnet list TidyUp.sln package --vulnerable --include-transitive` | **PASS** — no vulnerable packages (direct or transitive) |

Test count history: 337 (v1.0.0) → 426 (v1.1.0) → 617 (baseline) → **777** (Phase 7 Gate).

---

## 2. Phase 7 Integrated Acceptance & Composition Root Verification

| # | Acceptance Scenario | Result | Verification Details |
| :-- | :-- | :-- | :-- |
| A1 | **Composition Root Completeness** | **PASS** | `ServiceConfiguration.ConfigureServices(paths)` resolves all 31 singleton/transient domain services, repositories, integrity checkers, and all 14 ViewModels without resolution errors or missing registrations. |
| A2 | **Startup Initialization Lifecycle** | **PASS** | `ServiceConfiguration.InitializeDatabaseAsync` verifies database integrity, runs pending EF Core migrations, and `LoadSettingsAsync` initializes persistent `AppSettings` with fail-safe fallbacks. |
| A3 | **Simulate → Apply → Rollback Cycle** | **PASS** | Validated on real disk fixtures (`TestDirectoryFixture`): dry-run simulation projects actions without touching disk; batch execution moves matching files; telemetry streams to `ActionLogEntity` and updates `Rule.FilesProcessedCount`; batch rollback restores files to original locations and removes destinations. |
| A4 | **Monitoring Coordinator Pipeline** | **PASS** | `IMonitoringCoordinator` starts background watchers, captures detected file events, matches active rules, executes safe plan generation, moves files, and stops cleanly without orphaned handles. |
| A5 | **Dark Theme Empty State Contrast** | **PASS** | Central glyphs use `{DynamicResource Win11AccentBrush}` providing 7.04:1 contrast ratio against `{DynamicResource Win11SubtleSurfaceBrush}` (#2B2B2B) in Dark theme (exceeding WCAG AA 4.5:1 text and 3:1 graphical boundaries). All 32 theme/palette tests pass. |

---

## 3. Definition of Done checklist (plan §9)

| # | Criterion | Result | Evidence |
| :-- | :-- | :-- | :-- |
| 1 | `grep -r "SpecialFolder.ApplicationData" TidyUp/` → 1 hit | **PASS** | Only `Configuration/TidyUpPaths.cs` resolves the real profile (plus the `ActionExecutor` protected-folder list, which is a safety guard, not data access) |
| 2 | `grep -r "EnsureCreated" TidyUp/` → 0 hits | **PASS** | Production code uses `DatabaseMigrator`; `EnsureCreated` appears only in test fixtures |
| 3 | History loads on a copied v1.0.0-era `tidyup.db` | **PASS** | `DatabaseMigratorTests` baseline cases (legacy, fresh, already-migrated, corrupt) all green; the legacy DDL was captured from a real v1.0.0 database |
| 4 | Theme dropdown flips the UI; Dark has no unreadable text | **PASS** | `ThemeServiceTests` + `ThemePaletteTests` (every text/background token pair ≥ 4.5:1 in both palettes) + empty state tokens ≥ 4.5:1 |
| 5 | First-run wizard appears exactly once on a fresh `TIDYUP_APPDATA` | **PASS** | `FirstRunViewModelTests`; wizard re-entry is Settings → General |
| 6 | `StyleGuardTests`: 0 literal colours, 0 unhinted inputs | **PASS** | Literal colours migrated; `StyleGuardTests` and `InputLabelGuardTests` both green |
| 7 | Same folder → identical StatusBadge on Dashboard and Folders | **PASS** | `FolderStatusTests` (all 20 validation/watcher pairs) and a runtime check on a seeded profile: both screens show `Healthy`, `Missing`, `Protected` for the same three folders |
| 8 | **History/rollback available on upgraded databases** | **PASS** | `DatabaseMigratorTests` + legacy DDL comparison; verified in automated rollback acceptance suites |
| 9 | Icon legible at 16 px in title bar / taskbar / tray / Alt-Tab | **PASS** | `BrandAssetTests`: 9 sizes (16, 20, 24, 32, 40, 48, 64, 128, 256), all 32-bit with alpha; native handle management via `DestroyIcon` P/Invoke prevents GDI handle leaks |
| 10 | ≥ 445 tests green; CI vulnerability gate green | **PASS** | 777 tests; vulnerability scan clean |
| 11 | `docs/screenshots/` has 14 captures; README hero updated | **PASS** | 7 destinations × 2 themes, README hero image set |
| 12 | `docs/release-verification-1.2.0.md` signed, item 8 PASS | **PASS** | This document |

---

## 4. Upgrade path check (fresh profile and v1.0.0-era database)

1. **Fresh profile** — launch with an empty `TIDYUP_APPDATA`: schema created by migrations, wizard shown once,
   Settings → About reports `1.2.0 (<sha>)`. Verified.
2. **v1.1.0-era profile** — `settings.json` without `SchemaVersion` and `Theme=Dark` (the old forced default)
   migrates to `System`, and a pre-migration backup is written. Covered by `SettingsPersistenceTests`.
3. **v1.0.0-era database** — legacy three-table schema is baselined, backed up, and the `OperationJournal` and
   rule-revision tables are added, which restores History/rollback. Covered by `DatabaseMigratorTests`.
4. **Corrupt database** — startup `PRAGMA integrity_check` fails, the latest backup is restored, and the failure
   is reported. Covered by `DatabaseRecoveryTests`.

---

## 5. Data-safety verification for this release

The real `%APPDATA%\TidyUp` profile was hash-snapshotted before remediation and verified after the full test
suite and integration runs:

| File | Result |
| :-- | :-- |
| `settings.json` | UNCHANGED |
| `tidyup.db` | UNCHANGED |

No test or probe wrote to live application data. All automated acceptance suites execute against temporary fixtures isolated by `TestIsolationGuard`.

---

## 6. Verification Summary & Gate Sign-Off

- **Automated gates and numeric checks:** **PASS** (777 tests passing in Release mode).
- **Composition Root:** **VERIFIED** (0 missing service or ViewModel registrations).
- **Disk Acceptance Scenarios:** **VERIFIED** on temporary directory fixtures.
- **Phase 7 Gate Status:** **PASSED** — Ready for Phase 8 (`TASK-REM-39`: Release candidate packaging, provenance & publication handoff).
