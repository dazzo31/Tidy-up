# WARP.md

This file provides guidance to WARP (warp.dev) when working with code in this repository.

## Project Overview

**Tidy-up** is a Windows desktop application for automated file organization using rule-based file management. Users create rules with conditions (file name, size, date, content) and actions (move, copy, rename, delete) to automatically organize files in monitored folders.

## Technology Stack

- **Framework**: .NET 9.0 with WPF (Windows Presentation Foundation)
- **Architecture Pattern**: MVVM (Model-View-ViewModel)
- **UI Framework**: Modern WPF UI with Material Design or Fluent Design
- **Database**: SQLite with Entity Framework Core
- **File Monitoring**: FileSystemWatcher for real-time file change detection
- **Logging**: Serilog with structured logging
- **Dependency Injection**: Microsoft.Extensions.DependencyInjection
- **Reactive Programming**: System.Reactive for event stream handling

## Key Libraries

- **iTextSharp** or **PdfPig**: PDF text extraction for content search
- **DocumentFormat.OpenXml**: Word document (.docx) reading
- **SharpCompress**: Archive extraction (zip/rar)
- **Polly**: Retry logic for file locking and transient failures
- **System.IO.Abstractions**: Testable file system operations

## Development Commands

### Initial Setup
```powershell
# Create solution and projects
dotnet new sln -n TidyUp
dotnet new wpf -n TidyUp -f net9.0  # Creates with net9.0-windows automatically
dotnet new xunit -n TidyUp.Tests
# Update test project to net9.0-windows in TidyUp.Tests.csproj
dotnet sln add TidyUp/TidyUp.csproj TidyUp.Tests/TidyUp.Tests.csproj
dotnet add TidyUp.Tests reference TidyUp/TidyUp.csproj

# Install core packages
dotnet add TidyUp package Microsoft.EntityFrameworkCore.Sqlite
dotnet add TidyUp package Microsoft.Extensions.DependencyInjection
dotnet add TidyUp package Serilog.Sinks.File
dotnet add TidyUp package System.Reactive
dotnet add TidyUp package CommunityToolkit.Mvvm

# Install UI packages (choose one)
dotnet add TidyUp package MaterialDesignThemes
# OR
dotnet add TidyUp package ModernWpfUI

# Install testing packages
dotnet add TidyUp.Tests package FluentAssertions
dotnet add TidyUp.Tests package Moq
dotnet add TidyUp.Tests package System.IO.Abstractions.TestingHelpers
```

### Building
```powershell
# Restore and build
dotnet restore
dotnet build

# Build for Release
dotnet build -c Release

# Publish standalone executable
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

### Running
```powershell
# Run from source
dotnet run --project TidyUp/TidyUp.csproj

# Run with hot reload for XAML changes
dotnet watch --project TidyUp/TidyUp.csproj
```

### Testing
```powershell
# Run all tests
dotnet test

# Run with coverage
dotnet test /p:CollectCoverage=true /p:CoverageReportsFormat=opencover

# Run specific test
dotnet test --filter "FullyQualifiedName~RuleEngineTests"

# Watch mode for TDD
dotnet watch test --project TidyUp.Tests
```

### Code Quality
```powershell
# Format code
dotnet format

# Analyze with all warnings as errors
dotnet build /p:RunAnalyzers=true /p:TreatWarningsAsErrors=true
```

### Database Migrations
```powershell
# Install EF tools (first time only)
dotnet tool install --global dotnet-ef

# Add migration
dotnet ef migrations add <MigrationName> --project TidyUp

# Update database
dotnet ef database update --project TidyUp

# Rollback to specific migration
dotnet ef database update <MigrationName> --project TidyUp

# Generate SQL script
dotnet ef migrations script --project TidyUp
```

## Project Architecture

### Core Components

1. **Rule Engine**: Evaluates conditions and executes actions on files
   - Supports nested AND/OR logic for complex conditions
   - Sequential action execution with error handling
   - "Stop processing after match" flag to prevent multiple rules from processing the same file

2. **File Monitor**: Watches folders for changes using FileSystemWatcher
   - Debounces rapid file changes (500ms wait after last change)
   - Queues events for batch processing
   - Special handling for network drives (polling instead of watching)

3. **Variable System**: Interpolates metadata into file paths and names
   - Built-in variables: `{filename}`, `{extension}`, `{created_date}`, `{filesize}`, etc.
   - Custom variables with format specifiers (e.g., `{created_date:yyyy-MM-dd}`)
   - Supports text extraction, lookup tables, and environment variables

4. **Action Pipeline**: Executes ordered file operations
   - Move, Copy, Rename, Delete, Extract Archive, Run Command
   - Conflict resolution strategies: Skip, Overwrite, Rename (with counter), Prompt
   - Preserves subfolder structure when moving/copying

5. **Undo System**: Tracks operations for rollback
   - Maintains operation log with file state snapshots
   - Cannot undo permanent deletes (only Recycle Bin deletions)
   - 30-day retention by default

### Data Model

**Rule Structure**:
- Unique ID (GUID)
- Monitored folders (array with subfolder toggle per folder)
- Conditions (nested tree: All/Any of following)
- Actions (ordered list of operations)
- Execution order (integer for rule priority)
- Stop processing after match flag
- Enabled state

**Database Tables**:
- `Rules`: Rule configurations stored as JSON
- `ProcessedFiles`: Tracks file hashes and last processed timestamp
- `ActionLog`: Audit trail of all file operations
- `Settings`: Key-value configuration pairs

### File Processing Logic

1. **File Change Detection**: FileSystemWatcher triggers on Created, Changed, Renamed events
2. **Debouncing**: Wait 500ms after last change before processing
3. **Rule Matching**: Evaluate rules in priority order
4. **Condition Evaluation**: Recursively evaluate nested condition tree (short-circuit optimization)
5. **Action Execution**: Execute actions sequentially, stop on error unless "Continue on error" enabled
6. **Logging**: Record all operations to database with timestamp, file path, action, and result
7. **Status Tracking**: Store file hash (SHA-256) to prevent reprocessing unchanged files

### UI Structure

- **Main Window**: Split view with rule list (left) and rule editor (right)
- **Rule Editor Tabs**:
  - **Folders**: Configure monitored directories with subfolder inclusion
  - **Conditions**: Visual tree builder for AND/OR logic with live preview
  - **Actions**: Sequential action list with drag-to-reorder
- **Supporting Windows**: Log viewer, file preview, settings, variable manager

## Key Design Patterns

### Safety-First Approach

- **Recycle Bin by default**: All deletes use Recycle Bin unless explicitly changed
- **Test mode**: Dry-run preview shows exactly what will happen without executing
- **Confirmation dialogs**: Required for destructive operations and batch operations >10 files
- **Undo support**: Track operations for rollback (except permanent deletes)

### Performance Optimizations

- **Async file operations**: All I/O operations use async/await to keep UI responsive
- **Debouncing**: Limit condition preview updates to max once per second
- **Caching**: Cache expensive operations (file content search) per file
- **Short-circuit evaluation**: AND logic stops on first false, OR stops on first true
- **Virtualized UI controls**: Use VirtualizingStackPanel for large lists
- **Lazy loading**: Load rule details only when selected

### Error Handling

- **Retry logic**: 3 attempts with exponential backoff for locked files (100ms, 500ms, 2000ms)
- **Graceful degradation**: Skip inaccessible files and log warning, continue processing
- **Circular dependency detection**: Prevent rules that could cause infinite loops (max 10 processing iterations per file)
- **Path validation**: Sanitize all user inputs, validate before operations

## Implementation Phases

The specification defines 6 phases of development:

1. **Phase 1 (MVP)**: Project setup, data models, basic UI shell, rule CRUD
2. **Phase 2**: Folder monitoring, condition system, file matching, basic actions
3. **Phase 3**: Variable system, advanced conditions, conflict resolution, logging
4. **Phase 4**: Preview/test mode, undo system, safety features, performance optimization
5. **Phase 5**: Additional actions, advanced variables, content search, import/export
6. **Phase 6**: Help system, notifications, comprehensive settings, testing

## Testing Requirements

- **Unit Tests**: xUnit with FluentAssertions and Moq (target 80% coverage)
- **Integration Tests**: Test actual file operations with temporary directories
- **UI Tests**: Limited WPF automation for critical workflows
- **Mock FileSystem**: Use System.IO.Abstractions for testable code
- **Performance Testing**: Test with 1,000+ files and 100+ rules

## Important Constraints

- **Platform**: Windows 10/11 (64-bit) only
- **File size limit**: 50MB max for content search (configurable)
- **Path length**: Handle Windows 260-character limit (with long path support)
- **Memory target**: <200MB RAM under normal load
- **UI responsiveness**: <100ms response to user interactions

## Variable System Examples

Variables are interpolated in action paths and names:
- `{filename}_{created_year}-{created_month}` → `document_2025-11`
- `{folder_name}\{filename}` → `Documents\report`
- `{counter:000}_{fullname}` → `001_image.jpg`
- `{created_date:yyyy-MM-dd}` → `2025-11-07`

## Common Patterns

### Creating a Condition Tree
```csharp
// Root group: All of the following (AND)
var rootCondition = new ConditionGroup { Logic = LogicOperator.And };

// Add file extension condition
rootCondition.Conditions.Add(new FileExtensionCondition 
{ 
    Operator = StringOperator.Is, 
    Value = "pdf" 
});

// Nested group: Any of the following (OR) for size
var sizeGroup = new ConditionGroup { Logic = LogicOperator.Or };
sizeGroup.Conditions.Add(new FileSizeCondition 
{ 
    Operator = NumericOperator.GreaterThan, 
    Value = 1048576 // 1MB
});
rootCondition.Conditions.Add(sizeGroup);
```

### Action Conflict Resolution
When moving/copying files, always specify conflict resolution:
- **Skip**: Don't perform action if file exists
- **Overwrite**: Replace existing file (requires confirmation)
- **Rename new**: Append (1), (2), etc. to new filename
- **Rename old**: Backup existing file with timestamp

### Network Drive Handling
FileSystemWatcher is unreliable on network drives. Use polling:
- Detect UNC paths or mapped drives
- Switch to scheduled scanning (default: 30-minute interval)
- Cache file metadata (hash, modified date) to detect changes

## Code Organization

### Project Structure
```
TidyUp/
├── Models/
│   ├── Domain/              # Core domain models
│   │   ├── Rule.cs
│   │   ├── Condition.cs
│   │   ├── Action.cs
│   │   └── ProcessedFile.cs
│   └── Enums/               # Enumerations
│       ├── LogicOperator.cs
│       ├── StringOperator.cs
│       └── ConflictResolution.cs
├── ViewModels/
│   ├── MainWindowViewModel.cs
│   ├── RuleEditorViewModel.cs
│   ├── ConditionTreeViewModel.cs
│   ├── ActionListViewModel.cs
│   └── SettingsViewModel.cs
├── Views/
│   ├── MainWindow.xaml/.cs
│   ├── RuleEditorView.xaml/.cs
│   ├── ConditionTreeView.xaml/.cs
│   ├── LogViewerWindow.xaml/.cs
│   └── SettingsWindow.xaml/.cs
├── Services/
│   ├── IRuleEngine.cs / RuleEngine.cs
│   ├── IFileMonitorService.cs / FileMonitorService.cs
│   ├── IVariableEngine.cs / VariableEngine.cs
│   ├── IActionExecutor.cs / ActionExecutor.cs
│   └── IUndoService.cs / UndoService.cs
├── Data/
│   ├── TidyUpDbContext.cs
│   ├── Entities/            # EF entities
│   │   ├── RuleEntity.cs
│   │   ├── ActionLogEntity.cs
│   │   └── ProcessedFileEntity.cs
│   ├── Repositories/
│   │   ├── IRuleRepository.cs
│   │   └── RuleRepository.cs
│   └── Migrations/          # EF migrations
├── Utilities/
│   ├── PathValidator.cs
│   ├── FileHasher.cs
│   ├── VariableInterpolator.cs
│   └── FileSystemHelper.cs
├── Converters/              # WPF value converters
│   ├── BoolToVisibilityConverter.cs
│   └── FileSizeFormatter.cs
├── Controls/                # Custom WPF controls
│   ├── ConditionTreeControl.xaml/.cs
│   └── VariableInserterControl.xaml/.cs
├── Resources/
│   ├── Styles.xaml          # Global styles
│   ├── Icons.xaml           # Icon resources
│   └── Themes/              # Theme dictionaries
├── App.xaml/.cs             # Application entry point
└── ServiceConfiguration.cs  # DI container setup

TidyUp.Tests/
├── Unit/
│   ├── Services/
│   │   ├── RuleEngineTests.cs
│   │   ├── VariableEngineTests.cs
│   │   └── ActionExecutorTests.cs
│   ├── ViewModels/
│   │   └── RuleEditorViewModelTests.cs
│   └── Utilities/
│       └── PathValidatorTests.cs
├── Integration/
│   ├── FileOperationTests.cs
│   ├── DatabaseTests.cs
│   └── FileMonitorTests.cs
└── TestHelpers/
    ├── MockFileSystem.cs
    ├── TestDataBuilder.cs
    └── TempDirectoryFixture.cs
```

### File Naming Conventions
- **Views**: `[Name]Window.xaml` or `[Name]View.xaml`
- **ViewModels**: `[Name]ViewModel.cs`
- **Services**: Interface `I[Name]Service.cs`, Implementation `[Name]Service.cs`
- **Tests**: `[ClassUnderTest]Tests.cs`

## WPF Development Guidelines

### MVVM Implementation

**Use CommunityToolkit.Mvvm for boilerplate reduction:**
```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

public partial class RuleEditorViewModel : ObservableObject
{
    [ObservableProperty]
    private string _ruleName;
    
    [ObservableProperty]
    private bool _isEnabled;
    
    [RelayCommand]
    private async Task SaveRule()
    {
        // Save logic
    }
}
```

**Data Binding Patterns:**
- Use `{Binding Property, Mode=TwoWay}` for user input
- Use `{Binding Property, UpdateSourceTrigger=PropertyChanged}` for real-time validation
- Use `INotifyDataErrorInfo` for validation feedback
- Avoid code-behind except for view-specific logic (animations, focus management)

### Dependency Injection Setup

**Configure services in App.xaml.cs:**
```csharp
public partial class App : Application
{
    private ServiceProvider _serviceProvider;
    
    protected override void OnStartup(StartupEventArgs e)
    {
        var services = new ServiceCollection();
        
        // Register services
        services.AddSingleton<IRuleEngine, RuleEngine>();
        services.AddSingleton<IFileMonitorService, FileMonitorService>();
        services.AddDbContext<TidyUpDbContext>(options =>
            options.UseSqlite("Data Source=tidyup.db"));
        
        // Register ViewModels
        services.AddTransient<MainWindowViewModel>();
        services.AddTransient<RuleEditorViewModel>();
        
        // Register Views
        services.AddTransient<MainWindow>();
        
        _serviceProvider = services.BuildServiceProvider();
        
        var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }
}
```

### UI Performance Best Practices

**For large lists (>100 items):**
- Use `VirtualizingStackPanel` in ListBox/ListView
- Enable `VirtualizingPanel.IsVirtualizing="True"`
- Set `VirtualizingPanel.VirtualizationMode="Recycling"`
- Implement pagination for file lists

**For expensive updates:**
- Use `Dispatcher.InvokeAsync()` for background-to-UI thread communication
- Debounce search/filter inputs with `System.Reactive`
- Show progress indicators for operations >1 second

**Example debouncing:**
```csharp
private IDisposable _searchSubscription;

public void InitializeSearch()
{
    _searchSubscription = Observable
        .FromEventPattern<TextChangedEventArgs>(SearchBox, nameof(TextBox.TextChanged))
        .Throttle(TimeSpan.FromMilliseconds(300))
        .ObserveOnDispatcher()
        .Subscribe(_ => PerformSearch());
}
```

### Custom Controls

**Create reusable controls for complex UI:**
- `ConditionTreeControl`: TreeView with drag-drop for condition building
- `VariableInserterControl`: Popup with variable list and preview
- `ActionConfigPanel`: Expandable panel for each action type

**Example control structure:**
```xml
<UserControl x:Class="TidyUp.Controls.ConditionTreeControl"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation">
    <TreeView ItemsSource="{Binding Conditions}"
              AllowDrop="True"
              Drop="OnDrop">
        <!-- TreeView templates -->
    </TreeView>
</UserControl>
```

### Data Validation

**Implement validation using INotifyDataErrorInfo:**
```csharp
public class RuleEditorViewModel : ObservableValidator
{
    private string _ruleName;
    
    [Required(ErrorMessage = "Rule name is required")]
    [MinLength(3, ErrorMessage = "Rule name must be at least 3 characters")]
    public string RuleName
    {
        get => _ruleName;
        set => SetProperty(ref _ruleName, value, true); // true = validate
    }
}
```

**Display errors in XAML:**
```xml
<TextBox Text="{Binding RuleName, Mode=TwoWay, ValidatesOnNotifyDataErrors=True}" />
<TextBlock Text="{Binding Errors[RuleName][0].ErrorMessage}" 
           Foreground="Red"
           Visibility="{Binding HasErrors, Converter={StaticResource BoolToVisibilityConverter}}" />
```

### Async Operations in WPF

**Always use async/await for file operations:**
```csharp
[RelayCommand]
private async Task ProcessFiles()
{
    IsProcessing = true;
    try
    {
        await Task.Run(() => _ruleEngine.ProcessAllFiles());
        await ShowSuccessNotification();
    }
    catch (Exception ex)
    {
        await ShowErrorDialog(ex.Message);
    }
    finally
    {
        IsProcessing = false;
    }
}
```

**For cancellable operations:**
```csharp
private CancellationTokenSource _cts;

[RelayCommand]
private async Task StartMonitoring()
{
    _cts = new CancellationTokenSource();
    await _fileMonitor.StartAsync(_cts.Token);
}

[RelayCommand]
private void StopMonitoring()
{
    _cts?.Cancel();
}
```

### Resource Management

**Define styles in resource dictionaries:**
```xml
<!-- Resources/Styles.xaml -->
<ResourceDictionary>
    <Style x:Key="PrimaryButton" TargetType="Button">
        <Setter Property="Background" Value="#0078D4" />
        <Setter Property="Foreground" Value="White" />
        <Setter Property="Padding" Value="16,8" />
    </Style>
</ResourceDictionary>
```

**Merge in App.xaml:**
```xml
<Application.Resources>
    <ResourceDictionary>
        <ResourceDictionary.MergedDictionaries>
            <ResourceDictionary Source="Resources/Styles.xaml" />
            <ResourceDictionary Source="Resources/Icons.xaml" />
        </ResourceDictionary.MergedDictionaries>
    </ResourceDictionary>
</Application.Resources>
```

## Special Considerations

### File Content Search
- Only supports PDF and Word (.docx) documents
- Extract text and perform case-insensitive search
- Limit to first 50MB of file (configurable)
- Cache results per file to avoid repeated extraction

### Empty Folder Cleanup
After move/delete operations:
- Traverse up directory tree from source file
- Remove empty directories up to (but not including) monitored folder
- Skip if folder contains hidden/system files
- Log cleanup actions separately

### UAC and Permissions
- Run with user-level permissions by default
- Prompt for elevation only when accessing system folders
- Validate folder accessibility before monitoring
- Show clear error messages for permission issues
