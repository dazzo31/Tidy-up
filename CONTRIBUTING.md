# Contributing to TidyUp

Thank you for your interest in contributing to TidyUp! To preserve system stability, data safety, and codebase maintainability, all contributors and AI agents must follow these guidelines.

---

## 1. Architectural Invariants (Never Bypass)

The repository enforces a strict, top-down architecture:

```text
Presentation Layer (WPF XAML Views)
       ↓
ViewModel Layer (CommunityToolkit.Mvvm)
       ↓
Core Services / Orchestrators (BatchProcessingCoordinator, ActionExecutor)
       ↓
Engines (RuleEngine, DuplicateDetector, RollbackEngine, ScheduleManager)
       ↓
Infrastructure (EF Core SQLite, Windows Shell APIs, FileSystemWatcher)
```

- **No View-to-Engine Bypasses:** UI components must communicate through ViewModels and Services, never invoking file manipulation or DB engines directly.
- **Safety First:** Permanent file deletion is prohibited by default. All file operations must route through `ISafeFileSystem` to use the Windows Shell Recycle Bin.
- **Audited Execution:** All disk modifications must funnel through `BatchProcessingCoordinator` and record pre- and post-operation SHA-256 checksums in the `OperationJournal`.
- **Dry-Run Predictability:** Any rule execution feature must support full dry-run simulation via `IExecutionPlanGenerator` without side effects.

---

## 2. Development Setup

### Prerequisites
- Windows 10 (Build 19041+) or Windows 11
- [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- Visual Studio 2022 (v17.12+) or Visual Studio Code with C# Dev Kit

### Building the Project
```powershell
dotnet restore
dotnet build -c Release
```

### Running Tests
All pull requests must pass the complete automated test suite with zero failures:
```powershell
dotnet test -c Release --verbosity normal
```

---

## 3. Testing Discipline

- **Real Filesystem Harness:** File manipulation tests must run against real temporary directories created via `Path.GetTempPath()`, verifying real Windows filesystem behaviors (locking, attributes, paths).
- **Proper Teardown:** Tests implementing `IDisposable` must ensure test directories are cleanly removed in teardown logic.
- **Native xUnit Assertions:** Use standard xUnit assertions (`Assert.True`, `Assert.Equal`, `Assert.Contains`).
- **Isolation:** SQLite unit tests must use isolated connection strings or temp DB instances to prevent cross-test contamination.

---

## 4. Coding Standards

- **Target Framework:** `net9.0-windows`
- **Nullable Reference Types:** Enabled throughout the solution (`<Nullable>enable</Nullable>`). Avoid null-forgiving operators (`!`) unless strictly warranted by framework initialization patterns.
- **MVVM Pattern:** Use `[ObservableProperty]` and `[RelayCommand]` attributes from `CommunityToolkit.Mvvm`.
- **Async/Await:** Prefer non-blocking asynchronous APIs with `CancellationToken` support across all file I/O and database operations.

---

## 5. Git & Commit Guidelines

- **Atomic Commits:** One logical feature, bug fix, or refactor per commit.
- **Descriptive Messages:** Use clear imperative commit messages (e.g., `feat: implement hash-based duplicate detector`, `fix: enforce Recycle Bin on delete action`).
- **No Build Artifacts:** Never commit `bin/`, `obj/`, `.vs/`, or local SQLite databases to git. Verify `git status` before committing.
