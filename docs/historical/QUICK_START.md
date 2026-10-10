# TidyUp - Quick Start Guide

## How to Create a Rule

### Step 1: Create a New Rule
- Click the **"+ New Rule"** button in the toolbar (or press **Ctrl+N**)
- A new rule named "New Rule" will appear in the left panel and be automatically selected

### Step 2: Configure Basic Settings
In the rule details panel on the right:
- **Name**: Change "New Rule" to something meaningful (e.g., "Organize Downloads")
- **Description**: Add optional description of what this rule does
- **Enabled**: Check to activate the rule immediately
- **Stop processing after match**: Check if you don't want other rules to run after this one matches

### Step 3: Add Folders to Monitor
Click the **"Folders"** tab:
1. Click **"Add Folder"** button
2. Click the **folder browse icon** (📁) to select a folder
3. Configure options:
   - ☑️ **Include Subfolders**: Monitor all subdirectories
   - **Exclusion Patterns**: Enter comma-separated patterns to exclude (e.g., `node_modules, .git, temp`)
4. Repeat to add more folders

### Step 4: Set Conditions
Click the **"Conditions"** tab:
1. Choose match logic:
   - **All of the following (AND)**: File must match ALL conditions
   - **Any of the following (OR)**: File matches ANY condition
2. Click **"Add Condition"** to add a rule:
   - **File Name**: Match by filename (e.g., contains "invoice")
   - **Extension**: Match by file type (e.g., `.pdf`, `.jpg`)
   - **File Size**: Match by size (e.g., greater than 10MB)
   - **Date**: Match by creation or modification date
3. Enter the value for each condition
4. Click **X** to remove unwanted conditions

### Step 5: Add Actions
Click the **"Actions"** tab:
1. Click **"Add Action"** button and select action type:
   - **Move File**: Relocate file to another folder
   - **Copy File**: Duplicate file to another location
   - **Rename File**: Change filename using patterns
   - **Delete File**: Remove file (optionally to Recycle Bin)
   - **Change Extension**: Modify file extension
2. Configure action settings:
   - For Move/Copy: Enter destination path (can use variables like `{filename}`, `{date}`)
   - For Rename: Enter name pattern (e.g., `{filename}_{date}`)
   - For Delete: Choose "Use Recycle Bin" for safety
3. Use **↑↓** arrows to reorder actions (they execute in order)
4. Add multiple actions if needed

### Step 6: Test Your Rule (Recommended!)
- Click **"Test Rule"** button in toolbar (or press **Ctrl+T**)
- Preview window shows:
  - Which files would match
  - What actions would be performed
  - Any warnings or conflicts
- **No files are modified** - this is completely safe!

### Step 7: Save the Rule
- Click **"Save Rule"** button at bottom (or press **Ctrl+S**)
- Rule is now saved to database and will run automatically

## Example Rule: Organize Photos

**Name**: Organize Photos by Year  
**Folders**: `C:\Users\YourName\Downloads`  
**Conditions**: 
- Extension is `.jpg` OR `.png` OR `.jpeg`
**Actions**:
1. Move File → `C:\Users\YourName\Pictures\{year}\{month}`
2. Rename File → `Photo_{date}_{filename}`

## Keyboard Shortcuts

| Shortcut | Action |
|----------|--------|
| **Ctrl+N** | New Rule |
| **Ctrl+S** | Save Rule |
| **Ctrl+T** | Test Rule |
| **Delete** | Delete Rule |
| **Space** | Toggle Enable/Disable |
| **F5** | Refresh Rules |

## Variables for Actions

Use these in action paths and rename patterns:

- `{filename}` - Original filename without extension
- `{extension}` - File extension (e.g., `.txt`)
- `{date}` - File modified date (YYYY-MM-DD)
- `{year}` - Year (YYYY)
- `{month}` - Month (MM)
- `{day}` - Day (DD)
- `{created_date}` - File creation date
- `{size}` - File size in bytes
- `{counter}` - Auto-incrementing number

**Format specifiers**: Use `:` for custom formatting
- `{date:yyyyMMdd}` → `20250108`
- `{size:F2}MB` → `2.50MB`

## Tips

- ✅ **Always test rules** before enabling them on important files
- ✅ **Use "Recycle Bin"** option for delete actions
- ✅ **Start with simple conditions**, add complexity as needed
- ✅ **Use exclusion patterns** to avoid monitoring system folders
- ⚠️ **Disable rules** when not needed to avoid unwanted processing
- ⚠️ **Check logs** (History button) to see what actions were performed

## Settings

Click the **Settings** button (⚙️) to configure:
- **General**: Default conflict resolution, confirmation settings
- **Monitoring**: File watcher behavior, polling intervals
- **Logs**: Log retention, database maintenance
- **Notifications**: (Coming soon)

---

Need help? Check the logs window (History icon) to see what TidyUp is doing!
