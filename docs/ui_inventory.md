# TidyUp: UI Control-to-Service Trace & Component Inventory

**Task Reference:** `TASK-BL-02`  
**Audit Date:** 2026-10-09  
**Target Solution:** `TidyUp.sln`  
**Scope:** All XAML views, controls, viewmodels, commands, and backing services.

---

## 1. Executive Summary of Findings

1. **Rule Engine & Editor are Wired and Functional:** The core rule management (CRUD), nested condition building (AND/OR), action configuration, and variable substitution are properly connected to the database and services.
2. **File Monitoring is 100% Disconnected (Major Architectural Gap):**
   - `FileMonitorService` is registered in DI as a singleton, but is **never injected, started, or stopped** anywhere in the application.
   - The system tray menu handlers in `App.xaml.cs` (`PauseMonitoring_Click`, `ResumeMonitoring_Click`) are empty stubs marked with `// TODO: Implement pause monitoring logic`.
   - The application does not monitor any folders at runtime despite displaying "Monitored Folders" in the UI.
3. **Orphaned & Placeholder Components Identified:**
   - `RuleTestViewModel.cs` is an orphaned ViewModel with full test logic but **no XAML View or Window**, and is not registered in DI.
   - `FirstRunWindow.xaml` / `FirstRunViewModel.cs` is a fully built 5-step wizard that is **never invoked or checked** on application startup.
   - `RulePreviewWindow.xaml` performs dry-run file scans, but has **no execution or apply button**.
   - `HelpView.xaml` is embedded in `MainWindow.xaml` with no `DataContext`, while `HelpWindow.xaml` is opened as a separate modal window.
   - `LogViewerWindow.xaml` and `SettingsWindow.xaml` exist as redundant standalone windows that are never invoked (embedded views are used instead).

---

## 2. Complete UI Control-to-Service Mapping Matrix

### A. Main Window (`MainWindow.xaml` / `MainWindowViewModel.cs`)

| UI Element / Shortcut | Event / Command | ViewModel Binding | Backing Service / Target | Status |
| :--- | :--- | :--- | :--- | :--- |
| **New Rule Button / Ctrl+N** | `CreateNewRuleCommand` | `MainWindowViewModel.CreateNewRule()` | In-memory `Rule` collection | **Functional** |
| **Save Rule Button / Ctrl+S** | `SaveRuleCommand` | `MainWindowViewModel.SaveRuleAsync()` | `IRuleRepository.AddAsync` / `UpdateAsync` | **Functional** |
| **Delete Rule / Delete Key** | `DeleteRuleCommand` | `MainWindowViewModel.DeleteRuleAsync()` | `MessageBox` -> `IRuleRepository.DeleteAsync` | **Functional** |
| **Toggle Rule / Spacebar** | `ToggleRuleCommand` | `MainWindowViewModel.ToggleRuleAsync()` | `Rule.IsEnabled` -> `SaveRuleAsync` | **Functional** |
| **Refresh / F5** | `LoadRulesCommand` | `MainWindowViewModel.LoadRulesAsync()` | `IRuleRepository.GetAllAsync` | **Functional** |
| **Import Rules Button** | `ImportRulesCommand` | `MainWindowViewModel.ImportRulesAsync()` | `IImportExportService.ImportFromFileAsync` | **Functional** |
| **Export Selected Rule** | `ExportRuleCommand` | `MainWindowViewModel.ExportRuleAsync()` | `IImportExportService.ExportToFileAsync` | **Functional** |
| **Export All Rules** | `ExportAllRulesCommand` | `MainWindowViewModel.ExportAllRulesAsync()` | `IImportExportService.ExportToFileAsync` | **Functional** |
| **Test Rule / Ctrl+T** | `TestRuleCommand` | `MainWindowViewModel.TestRuleAsync()` | Opens `Views.RulePreviewWindow` | **Partial** (Preview only; no execution) |
| **View Logs Button** | `ViewLogsCommand` | `MainWindowViewModel.ViewLogs()` | Switches `CurrentView` to `LogsView` | **Functional** |
| **Settings Button** | `OpenSettingsCommand` | `MainWindowViewModel.OpenSettings()` | Switches `CurrentView` to `SettingsView` | **Functional** |
| **Help Button / F1** | `ViewHelpCommand` | `MainWindowViewModel.ViewHelp()` | Spawns `new HelpWindow().ShowDialog()` | **Functional** (Opens modal window) |
| **Rule Search Box** | `TextChanged` | `MainWindowViewModel.SearchText` | In-memory filter on `FilteredRules` | **Functional** |
| **Rule ListBox Drag-Drop** | `PreviewMouseMove`, `Drop` | Code-behind (`MainWindow.xaml.cs`) | Reorders `Rules` & updates `ExecutionOrder` | **Functional** |
| **Add Folder Button** | `Click` | Code-behind (`MainWindow.xaml.cs`) | `SelectedRule.MonitoredFolders.Add` | **Functional** |
| **Browse Folder Button** | `Click` | Code-behind (`MainWindow.xaml.cs`) | `Microsoft.Win32.OpenFolderDialog` | **Functional** |
| **Remove Folder Button** | `Click` | Code-behind (`MainWindow.xaml.cs`) | `SelectedRule.MonitoredFolders.Remove` | **Functional** |

---

### B. Condition Editor (`Controls/ConditionEditorControl.xaml` / `ConditionEditorViewModel.cs`)

| UI Element | Command / Handler | ViewModel Binding | Backing Service / Target | Status |
| :--- | :--- | :--- | :--- | :--- |
| **Add Condition Button** | `AddConditionCommand` | `ConditionEditorViewModel.AddCondition()` | Appends `FileNameCondition` to tree | **Functional** |
| **Add Group Button** | `AddGroupCommand` | `ConditionEditorViewModel.AddGroup()` | Appends `ConditionGroup` (AND/OR) | **Functional** |
| **Remove Condition Button** | `RemoveConditionCommand` | `ConditionEditorViewModel.RemoveCondition()` | Removes node from condition tree | **Functional** |
| **Match Logic ComboBox** | Selection change | `RootCondition.Operator` | Sets LogicOperator (`And` / `Or`) | **Functional** |
| **Condition Templates** | DataTemplates | Bound to specific `Condition` types | Evaluates Name, Extension, Size, Date | **Functional** |
| **Live Preview Panel** | `RefreshPreviewCommand` | `ConditionEditorViewModel.RefreshPreviewAsync()` | Scans monitored folders (max 500 files) | **Functional** |

---

### C. Action Editor (`Controls/ActionEditorControl.xaml` / `ActionEditorViewModel.cs`)

| UI Element | Command / Handler | ViewModel Binding | Backing Service / Target | Status |
| :--- | :--- | :--- | :--- | :--- |
| **Add Move Action** | `AddMoveActionCommand` | `ActionEditorViewModel.AddMoveAction()` | Appends `MoveFileAction` | **Functional** |
| **Add Copy Action** | `AddCopyActionCommand` | `ActionEditorViewModel.AddCopyAction()` | Appends `CopyFileAction` | **Functional** |
| **Add Rename Action** | `AddRenameActionCommand` | `ActionEditorViewModel.AddRenameAction()` | Appends `RenameFileAction` | **Functional** |
| **Add Delete Action** | `AddDeleteActionCommand` | `ActionEditorViewModel.AddDeleteAction()` | Appends `DeleteFileAction` | **Functional** |
| **Add Change Ext Action** | `AddChangeExtensionActionCommand` | `ActionEditorViewModel.AddChangeExtensionAction()` | Appends `ChangeExtensionAction` | **Functional** |
| **Add Extract Archive** | `AddExtractArchiveActionCommand` | `ActionEditorViewModel.AddExtractArchiveAction()` | Appends `ExtractArchiveAction` | **Functional** |
| **Add Run Command** | `AddRunCommandActionCommand` | `ActionEditorViewModel.AddRunCommandAction()` | Appends `RunCommandAction` | **Functional** |
| **Move Up / Down** | `MoveActionUp/DownCommand`| `ActionEditorViewModel.MoveActionUp/Down()` | Swaps items and updates `Order` index | **Functional** |
| **Remove Action** | `RemoveActionCommand` | `ActionEditorViewModel.RemoveAction()` | Removes action and re-indexes | **Functional** |
| **Insert Variable Button** | `Click` -> Popup | Code-behind & `VariableInserterPopup` | Inserts variable token at CaretIndex | **Functional** |
| **Browse Dest Folder** | `Click` -> Dialog | Code-behind (`ActionEditorControl.xaml.cs`) | `OpenFolderDialog` | **Functional** |

---

### D. File Monitoring & System Tray (`FileMonitorService.cs` / `App.xaml.cs`)

| Component / Trigger | Handler | Implementation | Status |
| :--- | :--- | :--- | :--- |
| **Tray: Show Main Window** | `ShowMainWindow_Click` | `_mainWindow.Show(); _mainWindow.Activate();` | **Functional** |
| **Tray: Pause Monitoring** | `PauseMonitoring_Click` | `// TODO: Implement pause monitoring logic` | **STUB / INCOMPLETE** |
| **Tray: Resume Monitoring** | `ResumeMonitoring_Click` | `// TODO: Implement resume monitoring logic` | **STUB / INCOMPLETE** |
| **FileMonitorService Start**| N/A | Never called anywhere in application | **DISCONNECTED** |
| **File Detected Event** | `FileDetected` event | No subscribers registered | **DISCONNECTED** |
| **Network Share Polling** | `SetupFolderMonitoringAsync` | `// TODO: Implement periodic polling` | **STUB / INCOMPLETE** |
| **Watcher Buffer Overflow** | N/A | No `Error` event subscription on `FileSystemWatcher` | **MISSING** |

---

### E. Secondary Windows & Dialogs

| Window / View | ViewModel | Invocation Point | Status & Issues |
| :--- | :--- | :--- | :--- |
| **RulePreviewWindow.xaml** | `RulePreviewViewModel` | `MainWindowViewModel.TestRuleAsync()` | **Functional preview, but execution missing.** User cannot apply actions. |
| **RuleTestViewModel.cs** | `RuleTestViewModel` | None | **ORPHANED VIEWMODEL.** Contains folder test logic but has no XAML UI. |
| **FirstRunWindow.xaml** | `FirstRunViewModel` | None | **DISCONNECTED.** Built wizard never displayed on startup. |
| **HelpWindow.xaml** | `HelpViewModel` | `MainWindowViewModel.ViewHelp()` | **Functional.** Loads markdown files from disk. |
| **HelpView.xaml** | None | `MainWindow.xaml` | **DEAD XAML.** Embedded view has no DataContext; unreachable. |
| **LogsView.xaml** | `LogsViewModel` | `MainWindow.xaml` | **Functional.** Embedded view manages history, filters, CSV export. |
| **LogViewerWindow.xaml** | `LogViewerViewModel` | Registered in DI | **REDUNDANT & BROKEN.** Duplicate of LogsView; DataTrigger bug prevents details display. |
| **SettingsView.xaml** | `SettingsViewModel` | `MainWindow.xaml` | **Functional.** Embedded view manages settings and theme. |
| **SettingsWindow.xaml** | `SettingsViewModel` | Registered in DI | **REDUNDANT.** Redundant window wrapper; unused. |

---

## 3. Recommended Actions for Subsequent Phases

1. **Phase 1 (TASK-SAFE-01 & TASK-SAFE-06):**
   - Wire `FileSystemWatcher` error handling and reconciliation.
   - Establish execution engine so preview window can safely execute approved actions.
2. **Phase 2 (TASK-GUI-01, TASK-GUI-04, TASK-GUI-07):**
   - Replace disconnected rule-testing fragments with the unified Side-by-Side Impact Preview Window.
   - Remove dead XAML (`HelpView` in `MainWindow`, duplicate `SettingsWindow`, `LogViewerWindow`).
   - Wire `FileMonitorService` to application lifecycle and UI state manager.

