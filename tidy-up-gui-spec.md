# File Management Rule GUI Tool Specification

## Project Overview
Build a desktop graphical user interface tool that enables users to create, manage, and test file organisation rules using an if-then logic system. The tool should monitor specified folders for file changes and automatically perform actions (move, copy, rename, delete) based on user-defined conditions.

### System Requirements
- **Platform**: Windows 10/11 (64-bit)
- **Minimum RAM**: 4GB
- **Disk Space**: 100MB for application, additional space for logs
- **Permissions**: User-level access (elevated permissions prompt when needed)
- **.NET Runtime**: .NET 8.0 or higher

## Core Architecture

### Technology Stack
- **Framework**: .NET 8.0 with WPF (Windows Presentation Foundation)
- **UI Framework**: Modern WPF UI with Material Design or Fluent Design
- **Database**: SQLite for rule storage and logging
- **File Watching**: FileSystemWatcher for real-time monitoring
- **Libraries**:
  - iTextSharp or PdfPig for PDF text extraction
  - DocumentFormat.OpenXml for Word document reading
  - SharpCompress for archive extraction
  - Serilog for structured logging

### Data Model
- **Rule**: Composite object containing:
  - Unique ID (GUID)
  - Rule name and description
  - Monitored folders (array with subfolder toggle per folder)
  - Conditions (nested tree: All/Any of following, with child conditions)
  - Actions (ordered list of executable operations)
  - Enabled state (boolean)
  - Execution order (integer for rule priority)
  - Stop processing after match (boolean - if true, matched files skip remaining rules)
  - Created date and modified date
  - Last run timestamp

- **Condition Types** (hierarchical):
  - Container conditions: "All of the following" (AND), "Any of the following" (OR)
  - File metadata: File Name, File Extension, File Path, Folder Path
  - File properties: File Size, Date Created, Date Modified
  - File contents: Text search in documents (Word docx, PDF)
  - Text search operators: Is, Is not, Contains, Does not contain, Is nothing (case-insensitive)

- **Action Types** (sequential):
  - Move file (with subfolder structure preservation, conflict handling)
  - Copy file (with subfolder structure preservation, conflict handling)
  - Rename file (with variable interpolation, conflict handling)
  - Change extension
  - Delete / Send to recycle bin
  - Extract files (zip/rar archives)
  - Run command (with variable support)
  - Remove empty folders (option on delete/move actions)

- **Conflict Resolution Strategies**:
  - Skip (leave original, don't perform action)
  - Overwrite (replace existing file)
  - Rename new file (append counter: filename(1).ext, filename(2).ext, etc.)
  - Rename old file (move existing file to backup name)
  - Prompt user (pause automation for manual decision)

- **Variables** (metadata for conditions/actions):
  - **Built-in Variables**:
    - `{filename}` - File name without extension
    - `{extension}` - File extension (without dot)
    - `{fullname}` - Complete filename with extension
    - `{filesize}` - Size in bytes
    - `{filesize_kb}`, `{filesize_mb}` - Size in KB/MB
    - `{created_date}`, `{modified_date}` - ISO format dates
    - `{created_year}`, `{created_month}`, `{created_day}` - Date components
    - `{modified_year}`, `{modified_month}`, `{modified_day}` - Date components
    - `{folder_path}` - Parent folder path
    - `{folder_name}` - Parent folder name only
    - `{counter}` - Auto-incrementing number for batch operations
  - **Custom Variables**: User-defined via Variables Manager UI
    - Date format patterns (e.g., `{custom_date:yyyy-MM-dd}`)
    - Text extraction from file content (with regex support)
    - Lookup tables (map values from one set to another)
    - Environment variables (e.g., `{env:USERNAME}`)

### User Interface Sections

#### 1. Rule List View (Main Window - Left Panel)
- **Display Grid/List**: Show all rules with columns
  - Status icon (enabled/disabled/error)
  - Rule name
  - Monitored folder count
  - Last run time
  - Files processed count
- **Toolbar Actions**:
  - New Rule (Ctrl+N)
  - Edit Rule (Double-click or Enter)
  - Delete Rule (Delete key, with confirmation)
  - Duplicate Rule (Ctrl+D)
  - Enable/Disable Toggle (Space bar)
  - Import/Export Rules (JSON format)
- **Context Menu**: Right-click for quick actions
- **Drag-to-reorder**: Visual feedback for rule priority (1, 2, 3...)
- **Search/Filter Bar**: Real-time filtering by rule name or folder path
- **Bulk Actions**: Multi-select for enable/disable/delete operations

#### 2. Rule Editor (Main Panel)
Three-tab interface:

**Tab 1: Folders**
- **Folder List**: DataGrid with columns
  - Path (with folder picker button)
  - Include subfolders (checkbox)
  - Active file count (live counter)
  - Status icon (accessible/inaccessible)
- **Add/Remove Buttons**: Browse for folder with validation
- **Exclusion Patterns**: Optional text field for folder exclusion (e.g., `node_modules`, `.git`)
- **Network Drive Warning**: Visual indicator with refresh interval setting
- **Validation**: Check folder accessibility and show warnings for permission issues

**Tab 2: Conditions**
- **Visual Tree Control**: Hierarchical tree view with indentation
  - Root node: "All of the following (AND)" or "Any of the following (OR)" or "All Files"
  - Child nodes can be groups or individual conditions
  - Expand/collapse nodes for complex rules
- **Add Condition Controls**:
  - "+ Add Condition" button at each level
  - "+ Add Group" button to create nested AND/OR logic
- **Condition Editor**: Each condition displays
  - Property dropdown (File Name, Extension, Size, Date Created, etc.)
  - Operator dropdown (context-sensitive based on property type)
    - Text: Is, Is not, Contains, Does not contain, Starts with, Ends with, Matches regex
    - Numeric: Equals, Not equals, Greater than, Less than, Between
    - Date: Is, Before, After, Between, Older than X days
  - Value input (text box, date picker, or numeric spinner)
  - Remove button (X icon)
- **Drag-and-Drop Reordering**: Visual indicators for drop zones
- **Live Preview Panel** (bottom of tab):
  - Matching file count with throttled updates (debounced to 1 second)
  - "Show Files" button to open detailed match list
  - "Refresh" button to force re-evaluation
- **Performance Settings**: 
  - Option to disable live preview for large folder sets
  - Maximum files to scan setting (default: 10,000)

**Tab 3: Actions**
- **Action List**: DataGrid showing sequential actions with numbering (1, 2, 3...)
- **Drag-to-Reorder**: Up/down arrows and drag handles for reordering
- **Add Action Dropdown**: Select action type to add
- **Action Configuration Panels** (expand/collapse per action):
  - **Move File**:
    - Destination folder path (browse button + variable inserter button)
    - Preserve subfolder structure (checkbox)
    - Conflict resolution (dropdown)
    - Remove empty source folders (checkbox)
  - **Copy File**:
    - Destination folder path (browse button + variable inserter button)
    - Preserve subfolder structure (checkbox)
    - Conflict resolution (dropdown)
    - Apply subsequent actions to: Source file / Copied file (radio buttons)
  - **Rename File**:
    - Name pattern (text box with variable inserter button)
    - Preview example (shows result for sample file)
    - Conflict resolution (dropdown)
  - **Change Extension**:
    - New extension (text input, without dot)
    - Conflict resolution (dropdown)
  - **Delete File**:
    - Method: Send to Recycle Bin / Permanent Delete (radio buttons)
    - Remove empty parent folders (checkbox)
    - Confirmation prompt (checkbox for safety)
  - **Extract Archive**:
    - Destination folder (browse button + variable inserter)
    - Delete archive after extraction (checkbox)
    - Overwrite existing files (checkbox)
  - **Run Command**:
    - Command line (text box with variable inserter)
    - Working directory (optional)
    - Wait for completion (checkbox)
    - Timeout (seconds)
- **Variable Inserter**: Button opens popup with variable list and preview
- **Test Action Button**: Simulate action on sample file without execution

#### 3. File Preview/Matching Window (Separate Dialog)
- **File List DataGrid**: 
  - Columns: Name, Path, Size, Date Modified, Status
  - Status values: New (unprocessed), Pending (marked for reprocessing), Processed, Error
  - Color coding for status
  - Sortable and filterable columns
  - Multi-select for batch operations
- **Filter Bar**: Quick filters for status, date range, file type
- **Action Preview**: Select file(s) and click "Preview Actions" to see transformation results
- **Test Mode Toggle**: Enable to show action results without execution
- **Batch Operations**:
  - Mark selected as pending (force reprocess)
  - Reset status to unprocessed
  - Exclude from rule
- **Export List**: Save file list to CSV for analysis
- **Pagination**: Show 100 files per page (configurable)
- **Performance**: Virtualized scrolling for large file lists

#### 4. Logging/History Window (Separate View)
- **Log Table**: DataGrid with columns
  - Timestamp (sortable)
  - Rule name
  - File path (original)
  - Action performed
  - Result path (if moved/copied)
  - Status (Success/Warning/Error)
  - Error message (if failed)
- **Filtering Options**:
  - Date range picker (today, last 7 days, last 30 days, custom)
  - Rule filter (dropdown)
  - Status filter (success/warning/error checkboxes)
  - Text search across file paths
- **Action Buttons**:
  - Export to CSV/JSON
  - Clear logs (with date range option)
  - Refresh
- **Recovery Features**:
  - "Undo Last Action" - reverses most recent file operation
  - "Restore File" - attempts to restore selected log entry (if file was moved/renamed)
  - Limitations: Cannot undo deletes unless Recycle Bin was used
- **Log Retention**: Auto-delete logs older than X days (configurable in settings)
- **Performance**: Indexed database queries, lazy loading with pagination

#### 5. Settings/Configuration Window (Menu > Settings)
**General Tab:**
- Enable/disable all monitoring (master switch)
- Start with Windows (checkbox)
- Minimize to system tray (checkbox)
- Show notifications for completed actions (checkbox)
- Notification types: Success summary, Errors only, All actions

**Performance Tab:**
- Max files to process per batch (default: 100)
- Processing delay between files (milliseconds, default: 0)
- Max concurrent file operations (default: 1 for safety)
- Disable live preview for large folders (threshold setting)
- Network drive scan interval (minutes, default: 30)

**Safety Tab:**
- Always use Recycle Bin for deletions (checkbox, default: true)
- Require confirmation for destructive actions (checkbox, default: true)
- Backup reminder on startup (checkbox, default: true)
- Enable undo history (checkbox, default: true)
- Undo history retention days (default: 30)

**Logging Tab:**
- Log level: Error only, Warning, Info, Debug
- Log file location (path with browse button)
- Max log file size (MB, default: 50)
- Log retention days (default: 90)
- Enable detailed action logging (includes file content hashes)

**Advanced Tab:**
- Database location (SQLite file path)
- Max database size (MB)
- Optimize database on startup (checkbox)
- Enable experimental features (checkbox)
- Reset all settings to defaults (button with confirmation)

## Processing Logic

### File Monitoring
1. **FileSystemWatcher Integration**: 
   - Monitor Created, Changed, Renamed, and Deleted events
   - Debounce rapid file changes (wait 500ms after last change)
   - Queue events for batch processing
2. **Startup Behavior**:
   - Perform full folder scan on application startup
   - Scan all monitored folders when rule is enabled
   - Optionally scan on rule modification (setting)
3. **Network Drive Handling**:
   - Detect network paths (UNC or mapped drives)
   - Use scheduled polling instead of FileSystemWatcher (less reliable on network)
   - Configurable poll interval (default: 30 minutes)
   - Cache file metadata to detect changes
4. **File Status Tracking**:
   - Store file hash (SHA-256) and last processed date in database
   - Skip reprocessing if file unchanged (unless marked pending)
   - "Mark as Pending" option forces reprocessing
5. **Error Handling**:
   - Retry on locked files (3 attempts with exponential backoff)
   - Skip inaccessible files and log warning
   - Continue processing remaining files on error

### Rule Processing
1. **Rule Execution Order**:
   - Process rules in priority order (drag-to-reorder in UI)
   - For each file, evaluate rules sequentially
   - If rule matches and "Stop processing after match" is enabled, skip remaining rules for that file
   - If disabled, continue checking all rules (file may match multiple rules)

2. **Condition Evaluation**:
   - Start at root condition group (All/Any)
   - Recursively evaluate child conditions and groups
   - Short-circuit evaluation for performance (AND stops on first false, OR stops on first true)
   - Cache expensive operations (file content search) per file

3. **Action Execution**:
   - Execute actions in sequence (order matters)
   - Pass file context through action chain
   - For Copy actions: Can apply subsequent actions to source or target file
   - Stop action chain on error (unless "Continue on error" is enabled)
   - Wrap all file operations in try-catch with detailed logging

4. **File Conflict Resolution**:
   - Check destination before move/copy/rename
   - Apply user-selected strategy:
     - Skip: Log warning and continue to next file
     - Overwrite: Delete existing file and proceed (with safety confirmation)
     - Rename new: Append (1), (2), etc. to filename
     - Rename old: Backup existing file with timestamp suffix
     - Prompt: Show dialog if application is in foreground, otherwise use default

5. **Empty Folder Cleanup**:
   - After move/delete, traverse up directory tree
   - Remove empty directories up to (but not including) monitored folder
   - Skip if folder contains hidden/system files
   - Log cleanup actions separately

6. **Transaction Safety**:
   - Log action before execution (with "In Progress" status)
   - Update log on completion/failure
   - For critical operations, create backup metadata for undo

### Variable Interpolation
- **Supported Contexts**: Move/Copy destination paths, Rename patterns, Run command arguments
- **Syntax**: `{variable_name}` or `{variable_name:format}`
- **Examples**:
  - `{filename}_{created_year}-{created_month}` → `document_2025-11`
  - `{folder_name}\{filename}` → `Documents\report`
  - `{counter:000}_{fullname}` → `001_image.jpg`
- **Format Specifiers**:
  - Dates: `{created_date:yyyy-MM-dd}`, `{modified_date:yyyyMMdd_HHmmss}`
  - Numbers: `{filesize:0.00}`, `{counter:000}` (padding)
  - Text: `{filename:upper}`, `{filename:lower}`, `{filename:title}`
- **Evaluation Order**: Variables resolved at action execution time (not at rule creation)
- **Error Handling**: If variable fails to resolve, log error and use fallback (e.g., empty string or skip action)
- **Variable Manager UI**: 
  - Window for creating/editing custom variables
  - Test panel showing variable resolution for sample files
  - Import/export variable definitions

## User Experience Considerations

### Safety Features
- **First Run Wizard**:
  - Welcome screen with overview
  - Backup recommendation dialog (with checkbox "Don't show again")
  - Quick setup: Create first rule with guided steps
  - Link to documentation/video tutorial
- **Safe Defaults**:
  - All delete actions use Recycle Bin by default
  - Confirmation required for destructive operations
  - Test mode enabled for new rules
- **Preview Mode**:
  - "Dry Run" button in rule editor
  - Shows exactly what would happen without executing
  - Color-coded results: Green (success), Yellow (warning), Red (error)
  - "Apply Changes" button only enabled after successful preview
- **Undo System**:
  - Maintains operation log with file state snapshots
  - "Undo Last Action" in toolbar and Edit menu
  - Shows undo history with thumbnails for image files
  - Cannot undo permanent deletes (warning shown)
- **Confirmation Dialogs**:
  - Always confirm before: Permanent delete, overwrite, batch operations >10 files
  - Show impact summary (X files will be moved/deleted)
  - "Don't ask again for this session" checkbox

### Typical User Workflow
1. **Create Rule**: Click "New Rule" button, enter name and description
2. **Add Folders**: Select one or more folders to monitor, choose subfolder inclusion
3. **Build Conditions**: 
   - Add condition groups (AND/OR logic)
   - Add individual conditions with property/operator/value
   - Watch live match count update in preview panel
   - Click "Show Matching Files" to verify correct files are selected
4. **Configure Actions**:
   - Add actions in desired sequence
   - Configure each action with paths, variables, conflict resolution
   - Use Variable Inserter for complex naming patterns
   - Test individual actions with "Test Action" button
5. **Test Rule**:
   - Click "Preview Rule" to see full dry run
   - Review what would happen to each file
   - Make adjustments if needed
6. **Enable Rule**: Toggle rule to active state
7. **Monitor Results**: Check log history to verify actions executed correctly

### Keyboard Shortcuts
- `Ctrl+N`: New Rule
- `Ctrl+S`: Save Rule
- `Ctrl+D`: Duplicate Rule
- `Delete`: Delete Selected Rule/Condition/Action
- `Space`: Toggle Enable/Disable Rule
- `Ctrl+T`: Test/Preview Rule
- `Ctrl+Z`: Undo Last Action (file operation)
- `F5`: Refresh File List/Log
- `Ctrl+F`: Search/Filter
- `Ctrl+,`: Open Settings

### Tooltips & Help System
- **Contextual Tooltips**: 
  - Hover over any UI element for brief explanation
  - Extended tooltips with Shift+Hover for detailed information
  - Examples included in tooltips for complex features
- **Help Icons**: Question mark icon next to complex settings opens help panel
- **In-App Documentation**:
  - Help menu with searchable documentation
  - "Getting Started" tutorial
  - "Rule Examples" library with common use cases
  - Regex pattern reference guide
  - Variable reference with examples
- **Smart Suggestions**:
  - Suggest common conditions when adding to tree
  - Suggest variable patterns for common renaming scenarios
  - Warn about potentially dangerous configurations (e.g., recursive moves)
- **Error Messages**:
  - Clear, actionable error messages
  - Suggest solutions for common problems
  - Link to relevant help topics
- **Status Bar**: Shows current operation, file count, last action result

## Technical Requirements

### Application Architecture
- **Framework**: .NET 8.0 with WPF using MVVM pattern
- **UI Library**: WPF with Modern UI framework (MaterialDesignInXaml or ModernWpf)
- **Dependency Injection**: Microsoft.Extensions.DependencyInjection
- **Reactive Extensions**: System.Reactive for event stream handling

### Data Layer
- **Database**: SQLite with Entity Framework Core
- **Tables**:
  - Rules (id, name, description, enabled, order, config_json)
  - ProcessedFiles (id, rule_id, file_path, file_hash, last_processed)
  - ActionLog (id, timestamp, rule_id, file_path, action, result, error_message)
  - Settings (key-value pairs)
- **Migrations**: EF Core migrations for schema versioning
- **Backups**: Daily automatic backup of SQLite database

### File Operations
- **System.IO**: Core file operations (File, Directory, Path classes)
- **FileSystemWatcher**: Real-time file monitoring
- **Microsoft.VisualBasic.FileIO**: For Recycle Bin operations
- **File Locking**: Retry logic with Polly library for transient failures
- **Hashing**: SHA256 for file change detection
- **Async/Await**: All file I/O operations asynchronous

### Content Extraction
- **PDF**: iTextSharp or PdfPig for text extraction
- **Word**: DocumentFormat.OpenXml for .docx reading
- **Archives**: SharpCompress for zip/rar extraction
- **Limits**: Max file size for content search: 50MB (configurable)

### Performance
- **Threading**: Background worker threads for file monitoring and processing
- **UI Thread**: All UI updates via Dispatcher
- **Throttling/Debouncing**: Limit condition preview updates to max once per second
- **Pagination**: Virtualized controls for large lists (VirtualizingStackPanel)
- **Memory**: Target max 200MB RAM usage under normal load
- **Responsiveness**: 
  - UI remains responsive during file operations (async processing)
  - Progress indicators for long-running operations
  - Cancellation support for all background tasks

### Security & Permissions
- **UAC**: Request elevation only when needed (accessing system folders)
- **Sandboxing**: Validate all file paths to prevent directory traversal
- **Input Validation**: Sanitize all user inputs, especially in Run Command action
- **Credential Storage**: Use Windows Credential Manager for any stored credentials

### Testing
- **Unit Tests**: xUnit for business logic (>80% coverage goal)
- **Integration Tests**: Test file operations with temporary directories
- **UI Tests**: Limited WPF UI automation tests for critical workflows
- **Mock FileSystem**: Use System.IO.Abstractions for testable file operations

### Logging & Diagnostics
- **Logging Framework**: Serilog with file and debug output sinks
- **Structured Logging**: JSON format for easy parsing
- **Log Levels**: Configurable (Error, Warning, Info, Debug, Trace)
- **Performance Counters**: Track rules executed, files processed, errors encountered
- **Crash Reporting**: Unhandled exception logging with stack traces

## Implementation Priority

### Phase 1: Foundation (MVP - Minimum Viable Product)
1. **Project Setup**
   - Create .NET 8.0 WPF solution with proper structure
   - Set up dependency injection and MVVM framework
   - Configure SQLite database with EF Core
   - Implement basic logging with Serilog

2. **Core Data Models**
   - Rule, Condition, Action entity classes
   - Database schema and migrations
   - Settings management

3. **Basic UI Shell**
   - Main window layout with rule list
   - Rule editor dialog with three tabs
   - Settings window

4. **Rule Management**
   - CRUD operations for rules
   - Rule list display with enable/disable
   - Basic validation

### Phase 2: Core Functionality
5. **Folder Monitoring**
   - FileSystemWatcher integration
   - Monitored folder configuration UI
   - Basic file scanning

6. **Condition System**
   - Condition tree builder UI
   - AND/OR logic implementation
   - Basic conditions: File Name, Extension, Size, Date
   - Condition evaluation engine

7. **File Matching Preview**
   - Live file matching (with debouncing)
   - File list display with status
   - Match count indicator

8. **Basic Actions**
   - Move file action
   - Copy file action
   - Rename file action
   - Delete action (Recycle Bin only)
   - Action execution engine with error handling

### Phase 3: Enhanced Features
9. **Variable System**
   - Built-in variables implementation
   - Variable interpolation in actions
   - Variable inserter UI component

10. **Advanced Conditions**
    - Folder Path and File Path conditions
    - Text content search (basic)
    - Regex support

11. **Conflict Resolution**
    - All conflict strategies implemented
    - User prompts for interactive resolution

12. **Logging System**
    - Action logging to database
    - Log viewer UI with filtering
    - Export logs to CSV

### Phase 4: Polish & Safety
13. **Preview/Test Mode**
    - Dry run functionality
    - Visual preview of actions
    - Test with sample files

14. **Undo System**
    - Track file operations for undo
    - Undo last action implementation
    - Undo history UI

15. **Safety Features**
    - First run wizard
    - Confirmation dialogs
    - Backup reminders

16. **Performance Optimization**
    - Optimize file scanning for large folders
    - Implement pagination and virtualization
    - Network drive handling

### Phase 5: Advanced Features
17. **Additional Actions**
    - Change extension action
    - Extract archive action
    - Run command action
    - Empty folder removal

18. **Advanced Variables**
    - Custom variable creation UI
    - Date format patterns
    - Text extraction from file content
    - Lookup tables

19. **Content Search**
    - PDF text extraction
    - Word document text extraction
    - Performance optimization for large files

20. **Import/Export**
    - Export rules to JSON
    - Import rules from JSON
    - Rule templates library

### Phase 6: Final Polish
21. **Help System**
    - In-app documentation
    - Contextual help and tooltips
    - Tutorial/wizard for common scenarios

22. **Notifications**
    - System tray integration
    - Toast notifications for actions
    - Status indicators

23. **Settings & Configuration**
    - All settings pages implemented
    - Performance tuning options
    - Advanced options

24. **Testing & Bug Fixes**
    - Comprehensive testing
    - Performance testing with large file sets
    - User acceptance testing
    - Bug fixes and refinements

### Future Enhancements (Post-Release)
- Multi-language support
- Cloud storage integration (OneDrive, Dropbox)
- Scheduled rule execution
- Email notifications
- File tagging and metadata editing
- Integration with external services

## Error Handling & Edge Cases

### File System Errors
- **File Locked/In Use**: 
  - Retry with exponential backoff (3 attempts: 100ms, 500ms, 2000ms)
  - Log warning if all retries fail
  - Continue processing other files
- **Access Denied**:
  - Log error with file path and required permissions
  - Show notification suggesting running as administrator
  - Skip file and continue
- **File Not Found**: 
  - Handle race condition where file deleted between detection and processing
  - Log as info (not error)
  - Continue processing
- **Disk Full**:
  - Detect before move/copy operation
  - Show error dialog with disk space information
  - Pause rule execution until space available
- **Invalid Path Characters**:
  - Validate destination paths before operations
  - Sanitize variable interpolation results
  - Show clear error message with problematic characters

### Network & Performance
- **Network Drive Disconnected**:
  - Detect disconnection and pause monitoring
  - Attempt reconnection every 5 minutes
  - Show warning in UI (status bar icon)
  - Resume when connection restored
- **Large File Operations**:
  - Show progress bar for files >100MB
  - Allow cancellation of long operations
  - Use buffered I/O for efficiency
- **High CPU/Memory Usage**:
  - Monitor resource usage
  - Throttle processing if limits exceeded
  - Show warning in settings if system struggling

### Rule Configuration Errors
- **Circular Dependencies**:
  - Detect rules that could cause infinite loops (e.g., moving file back to monitored folder)
  - Show warning when saving rule
  - Implement max processing count per file (default: 10)
- **Invalid Regex**:
  - Validate regex patterns when entered
  - Show error inline with helpful message
  - Prevent saving rule with invalid regex
- **Missing Destination Folder**:
  - Option to create destination folder if not exists
  - Show warning if variable results in empty path
  - Validation before enabling rule
- **Variable Resolution Failure**:
  - Fallback to safe default (e.g., original filename)
  - Log warning with details
  - Continue processing with fallback

### Data Integrity
- **Database Corruption**:
  - Detect on startup with integrity check
  - Restore from latest backup if available
  - Graceful degradation (disable logging if DB fails)
- **Configuration File Corruption**:
  - Validate JSON on load
  - Keep previous version as backup (.bak file)
  - Reset to defaults if unrecoverable
- **Concurrent Access**:
  - Single instance enforcement (mutex)
  - Show error if another instance running
  - Option to force quit existing instance

### User Input Validation
- **Path Validation**:
  - Check for valid Windows path format
  - Warn about reserved names (CON, PRN, AUX, etc.)
  - Prevent paths longer than 260 characters (unless long path support enabled)
- **Variable Syntax**:
  - Validate variable syntax in real-time
  - Highlight unknown variables in red
  - Suggest corrections for typos
- **Action Configuration**:
  - Ensure all required fields filled
  - Validate cross-dependencies (e.g., Copy action's "Apply to" setting)
  - Prevent saving incomplete configurations

## UI/UX Design Guidelines

### Visual Design
- **Color Scheme**:
  - Primary: Blue (#0078D4 - Windows accent color)
  - Success: Green (#107C10)
  - Warning: Yellow/Orange (#FFB900)
  - Error: Red (#E81123)
  - Neutral: Gray scale (#F3F3F3 background, #323130 text)
- **Typography**:
  - Font: Segoe UI (Windows standard)
  - Headers: 16-20pt Bold
  - Body: 12pt Regular
  - Code/Paths: Consolas 11pt
- **Icons**:
  - Use Segoe MDL2 Assets or Material Design Icons
  - Consistent 16x16px for toolbar, 24x24px for main actions
  - Meaningful and recognizable symbols

### Layout Principles
- **Main Window**:
  - Minimum size: 1024x768px
  - Remember window size and position
  - Resizable with proper layout scaling
  - Left panel: Rule list (300px min width)
  - Right panel: Rule details/preview (flexible)
- **Spacing**:
  - 8px padding for containers
  - 16px margins between major sections
  - 4px gaps between related controls
- **Grouping**:
  - Use GroupBox or Cards for related settings
  - Collapsible sections for advanced options
  - Clear visual hierarchy

### Interaction Patterns
- **Buttons**:
  - Primary action: Filled button (blue)
  - Secondary: Outlined button
  - Destructive: Red outline or filled
  - Icon + text for clarity (can hide text on small screens)
- **Forms**:
  - Label above input fields
  - Inline validation with real-time feedback
  - Required fields marked with asterisk
  - Helper text below fields for guidance
- **Lists & Grids**:
  - Alternating row colors for readability
  - Hover highlight
  - Selection state clearly visible
  - Context menu on right-click
  - Double-click to edit
- **Drag & Drop**:
  - Visual drag handle icons
  - Drop zone highlighting
  - Smooth animations (150-300ms)
  - Cancel with Escape key

### Feedback & Communication
- **Progress Indicators**:
  - Indeterminate spinner for unknown duration
  - Progress bar with percentage for known duration
  - Estimated time remaining for long operations
  - Cancel button always visible during operations
- **Notifications**:
  - Toast notifications for background actions
  - Dismissible with auto-hide (5 seconds default)
  - Click to view details in log
  - Don't interrupt user workflow
- **Status Information**:
  - Status bar always visible at bottom
  - Show current state (Idle, Processing, Error)
  - File count and last action
  - Hover for detailed tooltip
- **Empty States**:
  - Friendly message when no rules exist
  - Call-to-action button to create first rule
  - Illustration or icon to fill space
  - Help text explaining what rules do

### Accessibility
- **Keyboard Navigation**:
  - All features accessible via keyboard
  - Visible focus indicators
  - Tab order follows visual layout
  - Access keys for common actions (Alt+N for New)
- **Screen Readers**:
  - ARIA labels for all interactive elements
  - Descriptive button text (not just icons)
  - Status announcements for background actions
- **Visual Accessibility**:
  - Minimum contrast ratio 4.5:1 for text
  - Don't rely solely on color to convey information
  - Adjustable font size in settings
  - High contrast theme support

### Performance Perception
- **Instant Feedback**:
  - UI responds to clicks within 100ms
  - Show loading state immediately for async operations
  - Optimistic UI updates where safe
- **Lazy Loading**:
  - Load rule details only when selected
  - Paginate large file lists
  - Defer loading of logs until viewed
- **Background Processing**:
  - Keep UI responsive during file operations
  - Show subtle progress in background
  - Allow continued interaction with other rules

## Testing Strategy

### Unit Testing
- **Test Coverage Goals**: Minimum 80% code coverage for business logic
- **Key Areas**:
  - Condition evaluation logic (all operators and data types)
  - Variable interpolation and formatting
  - Path validation and sanitization
  - Conflict resolution strategies
  - File hash calculation and comparison
- **Mocking**: Use System.IO.Abstractions for testable file system operations
- **Test Frameworks**: xUnit with FluentAssertions and Moq

### Integration Testing
- **File Operations**:
  - Create temporary test directory structure
  - Test actual file move/copy/rename/delete operations
  - Verify folder cleanup after operations
  - Test with various file sizes and types
- **Database Operations**:
  - Use in-memory SQLite for fast tests
  - Test CRUD operations for all entities
  - Verify migrations work correctly
- **FileSystemWatcher**:
  - Test event detection and debouncing
  - Verify proper cleanup and disposal

### UI Testing
- **Automated UI Tests** (Limited scope):
  - Test critical workflows: Create rule, add condition, save
  - Verify error messages display correctly
  - Test keyboard navigation through main forms
- **Manual Testing Checklist**:
  - All buttons and menus functional
  - Drag-and-drop operations smooth
  - Window resize and layout adaptation
  - Theme and high-contrast support
  - Keyboard shortcuts work

### Performance Testing
- **Load Tests**:
  - 1,000+ files in monitored folder
  - 100+ rules active simultaneously
  - Complex nested conditions (10+ levels deep)
  - Large files (1GB+) for move/copy operations
- **Metrics to Track**:
  - Memory usage over time (target: <200MB)
  - CPU usage during scanning (target: <25% sustained)
  - UI responsiveness (target: <100ms for interactions)
  - File processing throughput (target: 10+ files/second)

### Edge Case Testing
- **File Names**:
  - Unicode characters (emoji, foreign languages)
  - Special characters (spaces, dots, symbols)
  - Very long filenames (255 characters)
  - Reserved names (CON, PRN, etc.)
- **Paths**:
  - Deep nesting (>10 levels)
  - Network UNC paths
  - Mapped drives
  - Long paths (>260 characters with long path support)
- **Conditions**:
  - Empty folder (no files to process)
  - All files match
  - No files match
  - Circular rule references
- **Actions**:
  - Move to same location (no-op)
  - Rename to same name
  - Copy to monitored folder
  - Multiple actions on same file

### User Acceptance Testing
- **Test Scenarios**:
  1. Organize downloads folder by file type
  2. Sort photos by date into year/month folders
  3. Move completed projects to archive
  4. Rename files with standard naming convention
  5. Extract and organize compressed files
- **User Profiles**:
  - Novice user (first time using application)
  - Power user (creating complex rules)
  - IT administrator (deploying to multiple machines)
- **Success Criteria**:
  - Users can create working rule within 5 minutes
  - No data loss during testing period
  - Positive feedback on UI clarity and ease of use

### Regression Testing
- **Automated Suite**: Run after every code change
- **Test Data**: Maintain standard test file set
- **Version Testing**: Test upgrade path from previous versions
- **Configuration Testing**: Test with various settings combinations

### Security Testing
- **Path Injection**:
  - Attempt directory traversal with ../
  - Test with malicious path variables
- **Command Injection**:
  - Test Run Command action with shell metacharacters
  - Verify proper escaping and validation
- **Permission Escalation**:
  - Verify UAC prompts shown when needed
  - Test access to system folders

### Beta Testing Plan
- **Phase 1**: Internal team testing (2 weeks)
  - Core functionality validation
  - Bug identification and fixes
- **Phase 2**: Closed beta (4 weeks)
  - 20-50 external users
  - Feedback collection on usability
  - Performance testing on various systems
- **Phase 3**: Open beta (4 weeks)
  - Public release for testing
  - Community feedback and bug reports
  - Documentation refinement
- **Release**: After successful beta with no critical bugs

## Developer Appendix: Using Ollama API from VS Code

This appendix shows two simple ways to use the local Ollama API in VS Code.

### 1) Install and run Ollama locally
- Download/install: https://ollama.com/download
- Pull a model and verify the service:
```powershell
ollama pull llama3.1
curl http://localhost:11434/api/tags
```

### 2) Option A — Continue extension (chat + code)
- In VS Code, install: “Continue - Code Assistant” (id: Continue.continue).
- Command Palette → “Continue: Open Config”, then add a model entry:
```json
{
  "models": [
    {
      "title": "Llama 3.1 (Ollama)",
      "provider": "ollama",
      "model": "llama3.1",
      "completionOptions": { "temperature": 0.2 }
    }
  ]
}
```
- Use the Continue side panel (chat, inline edits, autocomplete).

### 3) Option B — REST Client inside VS Code (direct API calls)
- Install the “REST Client” extension.
- Create a file (e.g., requests.http) and add:
```http
POST http://localhost:11434/api/chat
Content-Type: application/json

{
  "model": "llama3.1",
  "messages": [
    { "role": "user", "content": "Write a C# method to compute SHA-256 of a file." }
  ],
  "stream": false
}
```
- Click “Send Request” above the request to execute.

### Notes
- Ollama also exposes an OpenAI-compatible API at http://localhost:11434/v1 for extensions that let you set a custom OpenAI Base URL; use any non-empty API key if required by the extension.
- Models must be pulled before use: `ollama pull <model>`.

### Troubleshooting
- Ensure http://localhost:11434 responds (service running).
- Allow port 11434 through firewall if blocked.
- If using WSL/VMs, ensure host-to-guest networking allows localhost access.