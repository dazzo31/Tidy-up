<p align="center"><img src="TidyUp/Resources/Brand/tidyup.svg" alt="TidyUp logo" width="96" height="96"></p>

# TidyUp

[![Latest Release](https://img.shields.io/github/v/release/dazzo31/Tidy-up?logo=github&color=blue)](https://github.com/dazzo31/Tidy-up/releases/latest)
[![Build & Test](https://github.com/dazzo31/Tidy-up/actions/workflows/ci.yml/badge.svg)](https://github.com/dazzo31/Tidy-up/actions/workflows/ci.yml)
[![Tests](https://img.shields.io/badge/tests-777%20passed-brightgreen.svg)](docs/release-verification-1.2.1.md)
[![Target .NET](https://img.shields.io/badge/.NET-9.0--windows-blue.svg)](https://dotnet.microsoft.com/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![Sponsor on GitHub](https://img.shields.io/badge/Sponsor-GitHub%20Sponsors-ea4aaa?style=flat&logo=githubsponsors&logoColor=white)](https://github.com/sponsors/dazzo31)
[![Buy Me A Coffee](https://img.shields.io/badge/Buy%20Me%20A%20Coffee-dazzo31-orange?style=flat&logo=buymeacoffee&logoColor=white)](https://buymeacoffee.com/dazzo31)

**TidyUp** is a high-performance, hardened automated file organization tool for Windows. It provides intelligent rule-based sorting, active folder monitoring, duplicate detection, and automated cleanup with an uncompromising focus on **data safety, predictability, and reversibility**.

![TidyUp dashboard (Light theme)](docs/screenshots/dashboard-light.png)

Every destination is captured in both themes under [`docs/screenshots/`](docs/screenshots); regenerate them with `scripts/Capture-Screenshots.ps1`.

---

## 🚀 Downloads (Latest: v1.2.1)

Download pre-built Windows x64 packages directly from the [Latest Release](https://github.com/dazzo31/Tidy-up/releases/latest):

| Package | Format | Description | Prerequisites |
| :--- | :--- | :--- | :--- |
| **[TidyUp-1.2.1-win-x64-standalone.zip](https://github.com/dazzo31/Tidy-up/releases/download/v1.2.1/TidyUp-1.2.1-win-x64-standalone.zip)** | Standalone Portable | **Recommended:** Self-contained single-file executable. Extract and run `TidyUp.exe`. | None (Zero install) |
| **[TidyUp-1.2.1-win-x64.zip](https://github.com/dazzo31/Tidy-up/releases/download/v1.2.1/TidyUp-1.2.1-win-x64.zip)** | Framework-Dependent | Lightweight distribution archive. | [.NET 9 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/9.0) |

Cryptographic authenticity checksums are provided in [SHA256SUMS.txt](https://github.com/dazzo31/Tidy-up/releases/download/v1.2.1/SHA256SUMS.txt).

---

## Key Features

- 🖥️ **Modern Windows 11 User Experience:** Persistent left navigation rail across 6 core destinations (Dashboard, Rules, Folders, Preview & Review, History, Settings) plus a "More" group for Logs and Help, with accessible focus and keyboard shortcuts (`Ctrl+1` through `Ctrl+6`). WCAG AA compliant dark theme contrast across all screens and badges.
- 🪄 **4-Stage Guided Rule Wizard:** Seamless step-by-step rule creation (Folders, Conditions, Actions, Review) with prominent folder browsing, tactile action controls, and lossless synchronization to advanced tabbed editing.
- 📁 **Dedicated Folders Management Hub:** Centralized monitored folders workspace with real-time directory permission auditing (`Read/Write`, `Read Only`, `Access Denied`), system-protected path detection, persistent folder catalog, and exclusion patterns (`.git`, `node_modules`).
- 👁️ **Full-Page Preview & Review Workspace:** First-class dry-run simulation workspace with operation breakdown counters, virtualized DataGrid, side-by-side impact review, stale plan invalidation, and cooperative in-flight batch cancellation.
- 💡 **Explainability Inspector:** "Why Did This File Match?" inspection drawer evaluating and displaying condition-by-condition pass/fail evaluation breakdowns.
- 🛡️ **Zero-Loss Safety Model & Guarded Apply:** Deletions route to the Windows Shell Recycle Bin by default. Scoped execution authorization context protects permanent deletions; safe overwrite staging takes displaced backups before mutation; boundary clamping protects monitored roots during empty directory cleanup.
- ⏪ **1-Click Rollback Engine & Actionable History:** Two-phase durable SQLite journaling tracks every operation with SHA-256 hashes, rule metadata, user-actionable error explanations, technical diagnostic drawers, and anti-clobbering pre-rollback verification.
- ⚡ **Background Monitoring Coordinator & Tray Lifecycle:** Serialized watcher lifecycle with linked restart resilience, multilateral scope merging, aggregate tray icon health monitoring, native GDI handle cleanup, and clean window minimize/close-to-tray handling.
- ⚙️ **Categorized 5-Section Settings:** Structured preferences across General, Safety, Performance, Storage & Logs, and Advanced sections with atomic file replacement, corrupt profile recovery, and genuine database VACUUM optimization.
- 🔍 **Hash-Based Duplicate Detector:** High-speed 3-step duplicate detection (file-size bucketing $\rightarrow$ 4KB partial hash $\rightarrow$ full SHA-256 verification) with automated isolation into `_Duplicates`.
- 🌙 **Scheduled Execution & Quiet Hours:** Schedule background jobs by time of day, configure quiet hours (e.g. 22:00–06:00), and automatically throttle when running on battery ($\le 20\%$).
- 📄 **Deep Content & Metadata Inspection:** Match files by plain text/regex keywords (safe streaming up to 1MB), image EXIF metadata (dimensions, Date Taken, camera model), and audio/video tags (MP3 ID3, WAV duration).
- 📜 **Versioned Rule History & Diff:** Track rule edits, view side-by-side visual diffs, and restore previous rule versions with one click.
- 📦 **Safe Rule Sharing & Templates:** Import/export rule packages with JSON schema validation (`https://tidyup.app/schemas/rules-v1.json`), path sanitization against reserved Windows device names, and safe disabled-by-default import.
- 🗄️ **Database Disaster Recovery:** Automatic pre-migration and daily SQLite backups, startup `PRAGMA integrity_check`, and instant rollback on corruption.

---

## Safety Invariants

TidyUp is engineered with non-negotiable architectural safeguards:

1. **Recycle Bin Authority:** Files are sent to the Windows Shell Recycle Bin. Permanent deletion is rejected on volumes that do not support the Recycle Bin unless explicitly authorized.
2. **File Lock Safety:** Files currently open by other applications (such as incomplete browser downloads or ongoing large copies) are detected and delayed via exponential backoff until released.
3. **Pre-Execution Checksumming:** Original source files are verified with SHA-256 before disk writes and re-verified post-execution.
4. **Execution Choke Point:** All operations funnel through an audited pipeline (`BatchProcessingCoordinator` $\rightarrow$ `ActionExecutor` $\rightarrow$ `OperationJournal`).
5. **Pre-Migration Backups:** SQLite database schema upgrades always take an automated backup first; failures trigger automatic restoration to the pre-migration state.

---

## System Requirements

- **Operating System:** Windows 10 (Build 19041+) or Windows 11 (x64, ARM64)
- **Runtime:** [.NET 9.0 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/9.0) (included in standalone portable distribution)

---

## Getting Started

### 1. Prerequisites
Ensure the [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0) is installed:
```powershell
dotnet --version
```

### 2. Clone the Repository
```powershell
git clone https://github.com/dazzo31/Tidy-up.git
cd Tidy-up
```

### 3. Build & Run
```powershell
# Restore NuGet dependencies
dotnet restore

# Build in Release configuration
dotnet build -c Release

# Run the WPF application
dotnet run --project TidyUp -c Release
```

### 4. Run the Test Suite
The solution contains **777** comprehensive unit, integration, and acceptance tests (100% passing):
```powershell
dotnet test -c Release --verbosity normal
```

---

## Documentation

- 📋 [Release Verification Report](docs/release-verification-1.2.1.md) - Authoritative verification gates, cryptographic provenance, and remediation finding matrix.
- 📝 [Changelog](CHANGELOG.md) - Full version release history and detailed change log.
- 🏛️ [Architecture & Technical Design](docs/ARCHITECTURE.md) - Subsystem details, sequence diagrams, and design principles.
- 📋 [Feature Verification Matrix](docs/FEATURE_MATRIX.md) - Authoritative verification status and test coverage for all requirements.
- 📑 [Task Catalog](TASK_CATALOG.md) - Work breakdown structure, task specifications, and governance log.
- 📜 [Specifications](docs/spec/) - Original architectural plan and GUI design specifications.
- 🛠️ [Helper Scripts](scripts/) - Standalone utilities for Google Takeout photo organization and release packaging.
- 📂 [Historical Archive](docs/historical/) - Pre-v1.0 milestone logs and session notes.

---

## 💖 Support & Sponsorship

If TidyUp has helped organize your files, saved you time, or streamlined your cleanup workflows, consider supporting ongoing development:

[![Sponsor on GitHub](https://img.shields.io/badge/Sponsor%20on%20GitHub-ea4aaa?style=for-the-badge&logo=githubsponsors&logoColor=white)](https://github.com/sponsors/dazzo31)
[![Buy Me A Coffee](https://img.shields.io/badge/Buy%20Me%20A%20Coffee-FFDD00?style=for-the-badge&logo=buymeacoffee&logoColor=black)](https://buymeacoffee.com/dazzo31)

---

## License

This project is licensed under the MIT License. See [LICENSE](LICENSE) for details.
