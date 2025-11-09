# Getting Started with TidyUp

Welcome to TidyUp! This guide will help you create your first file organization rule.

## What is TidyUp?

TidyUp is an intelligent file organization manager that automatically organizes your files based on rules you define. It monitors folders for new or modified files, checks them against your conditions, and performs actions like moving, copying, or renaming them.

## Quick Start: Create Your First Rule

### Step 1: Click "New Rule" (or press Ctrl+N)

In the left panel, click the **New Rule** button in the toolbar, or press **Ctrl+N**.

### Step 2: Give Your Rule a Name

Enter a descriptive name like "Organize Downloads" or "Sort Photos by Date".

### Step 3: Add a Monitored Folder

1. Go to the **Folders** tab
2. Click **Add Folder**
3. Browse to the folder you want to monitor (e.g., Downloads)
4. Optionally check **Include Subfolders** to monitor subdirectories

### Step 4: Define Conditions

Conditions determine which files the rule applies to.

1. Go to the **Conditions** tab
2. Click **Add Condition**
3. Choose a condition type:
   - **File Name**: Match files by name pattern
   - **File Extension**: Match specific file types (.pdf, .jpg, etc.)
   - **File Size**: Match files by size range
   - **File Date**: Match files by creation/modification date

4. Set the condition value (e.g., extension = ".pdf")
5. Add more conditions and use AND/OR logic to combine them

### Step 5: Define Actions

Actions specify what happens to files that match your conditions.

1. Go to the **Actions** tab
2. Click **Add Action** and choose:
   - **Move**: Move files to a new location
   - **Copy**: Copy files to a new location
   - **Rename**: Rename files using variables
   - **Delete**: Remove files (use with caution!)
   - **Change Extension**: Change the file extension

3. Configure the action (e.g., set destination folder for Move)
4. You can add multiple actions that run in sequence

### Step 6: Test Your Rule (IMPORTANT!)

Before enabling a rule, **always test it first**:

1. Click **Test Rule** (or press Ctrl+T)
2. The preview window shows what would happen without actually making changes
3. Review the results to ensure the rule works as expected

### Step 7: Save and Enable

1. Click **Save Rule** (or press Ctrl+S)
2. Check the **Enabled** checkbox to activate the rule
3. Save again

That's it! Your rule is now active and will automatically process matching files.

---

## Example Rule: Organize Downloads

**Name**: Organize Documents from Downloads

**Monitored Folder**: `C:\Users\YourName\Downloads`

**Conditions**:
- File extension is ".pdf" OR ".docx" OR ".xlsx"

**Actions**:
- Move to: `C:\Users\YourName\Documents\{Year}\{Month}`

**Result**: All PDF, Word, and Excel files in your Downloads folder are automatically moved to Documents, organized by year and month!

---

## Tips for Beginners

1. **Start Small**: Create simple rules first, then build complexity
2. **Always Test**: Use the Test/Preview feature before enabling rules
3. **Use Variables**: Variables like {Year}, {Month}, {FileName} make rules dynamic
4. **Keep Rules Disabled**: New rules are disabled by default for safety
5. **Check Logs**: View the Logs to see what TidyUp has done

---

## Keyboard Shortcuts

- **Ctrl+N**: Create New Rule
- **Ctrl+S**: Save Rule
- **Ctrl+T**: Test/Preview Rule
- **F5**: Refresh Rules List
- **Delete**: Delete Selected Rule
- **Space**: Toggle Enable/Disable

---

## Next Steps

- Learn about [Variables](variables-reference.md) for dynamic file naming
- Explore [Examples](examples.md) for common use cases
- Read the [FAQ](faq.md) for common questions

---

**Need more help?** Check the other help topics or visit our documentation.
