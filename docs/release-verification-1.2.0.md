# Release Verification — v1.2.0

Verification of the Phase 7 quality/visual-consistency release, performed on Windows 11 (build 10.0.26300) with
.NET SDK 9.0.319 and the `Release` configuration.

Evidence was collected on 2026-10-17 against commit `53d24f2` and later Phase 7 commits (the version string in
each screenshot and in the About card records the exact commit).

---

## 1. Build & test gates

| Gate | Command | Result |
| :-- | :-- | :-- |
| Release build | `dotnet build TidyUp.sln -c Release` | **PASS** — 0 warnings, 0 errors |
| Full test suite | `dotnet test TidyUp.sln -c Release` | **PASS** — 617 passed, 0 failed, 0 skipped |
| Vulnerable packages | `dotnet list TidyUp.sln package --vulnerable --include-transitive` | **PASS** — no vulnerable packages (direct or transitive) for either project |

Test count history: 337 (v1.0.0) → 426 (v1.1.0) → **617** (v1.2.0).

---

## 2. Definition of Done checklist (plan §9)

| # | Criterion | Result | Evidence |
| :-- | :-- | :-- | :-- |
| 1 | `grep -r "SpecialFolder.ApplicationData" TidyUp/` → 1 hit | **PASS** | Only `Configuration/TidyUpPaths.cs` resolves the real profile (plus the `ActionExecutor` protected-folder list, which is a safety guard, not data access) |
| 2 | `grep -r "EnsureCreated" TidyUp/` → 0 hits | **PASS** | Production code uses `DatabaseMigrator`; `EnsureCreated` appears only in test fixtures |
| 3 | History loads on a copied v1.0.0-era `tidyup.db` | **PASS** | `DatabaseMigratorTests` baseline cases (legacy, fresh, already-migrated, corrupt) all green; the legacy DDL was captured from a real v1.0.0 database |
| 4 | Theme dropdown flips the UI; Dark has no unreadable text | **PASS** | `ThemeServiceTests` + `ThemePaletteTests` (every text/background token pair ≥ 4.5:1 in both palettes) + 14 captures: Dark averages 0.23–0.28 luminance, Light 0.91–0.95 |
| 5 | First-run wizard appears exactly once on a fresh `TIDYUP_APPDATA` | **PASS** | `FirstRunViewModelTests`; wizard re-entry is Settings → General |
| 6 | `StyleGuardTests`: 0 literal colours, 0 unhinted inputs | **PASS** | 175 literal colours migrated; `StyleGuardTests` (colours, with an expanded named-colour list) and `InputLabelGuardTests` (22 inputs named) both green |
| 7 | Same folder → identical StatusBadge on Dashboard and Folders | **PASS** | `FolderStatusTests` (all 20 validation/watcher pairs) and a runtime check on a seeded profile: both screens show `Healthy`, `Missing`, `Protected` for the same three folders |
| 8 | **History/rollback available on upgraded databases** | **PASS** | `DatabaseMigratorTests` + `docs/baseline_test_report.md` legacy-DDL comparison; this is the item that failed in v1.1.0 |
| 9 | Icon legible at 16 px in title bar / taskbar / tray / Alt-Tab | **PASS (numerically)** | `BrandAssetTests`: 9 sizes (16, 20, 24, 32, 40, 48, 64, 128, 256), all 32-bit with alpha; pixel sampling confirms the tile/folder/check render at every size and corners are transparent. **Not reviewed by eye** — see limitations |
| 10 | ≥ 445 tests green; CI vulnerability gate green | **PASS** | 617 tests; vulnerability scan clean |
| 11 | `docs/screenshots/` has 14 captures; README hero updated | **PASS** | 7 destinations × 2 themes, README hero image set |
| 12 | `docs/release-verification-1.2.0.md` signed, item 8 PASS | **PASS** | This document |

---

## 3. Upgrade path check (fresh profile and v1.0.0-era database)

1. **Fresh profile** — launch with an empty `TIDYUP_APPDATA`: schema created by migrations, wizard shown once,
   Settings → About reports `1.2.0 (<sha>)`. Verified.
2. **v1.1.0-era profile** — `settings.json` without `SchemaVersion` and `Theme=Dark` (the old forced default)
   migrates to `System`, and a pre-migration backup is written. Covered by `SettingsPersistenceTests`.
3. **v1.0.0-era database** — legacy three-table schema is baselined, backed up, and the `OperationJournal` and
   rule-revision tables are added, which restores History/rollback. Covered by `DatabaseMigratorTests`.
4. **Corrupt database** — startup `PRAGMA integrity_check` fails, the latest backup is restored, and the failure
   is reported. Covered by `DatabaseRecoveryTests`.

---

## 4. Data-safety verification for this release

The real `%APPDATA%\TidyUp` profile was hash-snapshotted before the phase and re-checked after the full test
suite, every runtime probe, and the screenshot run:

| File | Result |
| :-- | :-- |
| `settings.json` | UNCHANGED |
| `tidyup.db` | UNCHANGED |

No Phase 7 test, script, or probe wrote to live application data.

---

## 5. Limitations of this verification

Recorded honestly rather than glossed over:

- **No visual review by a human or a vision model.** The review environment could not display images, so every
  visual claim above is numeric (luminance, distinct-colour counts, WCAG contrast ratios, sampled icon pixels) or
  structural (UI Automation element trees, computed offsets). Layout aesthetics, icon legibility at 16 px, and
  Dark-mode polish still deserve a human pass.
- **Dark mode** was verified by token contrast and rendered luminance, not by reading each screen.
- **One intermittent test failure** was observed once
  (`RuleWizardViewModel_ToggleRuleEnabled_UpdatesEnabledSeparatelyFromSave` immediately after a rebuild) and did
  not reproduce in 15 subsequent runs. Root cause unknown; watch for it.
- The tray icon was not re-verified visually; only the executable's embedded icon was sampled.

Signed off: **automated gates and numeric checks PASS** — human visual review recommended before publishing.
