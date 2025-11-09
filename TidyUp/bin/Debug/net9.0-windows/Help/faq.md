# Frequently Asked Questions

## General

### What happens if TidyUp is closed?
TidyUp only processes files when the application is running. Rules will not execute if TidyUp is closed.

### Can I undo changes?
Currently, TidyUp does not have a built-in undo feature. Always use the Test/Preview feature before enabling rules.

### Are my files safe?
Yes! TidyUp includes several safety features:
- Test/Preview mode (dry run)
- Confirmation dialogs for destructive actions
- Detailed logging of all operations
- Rules are disabled by default

### Where are rules stored?
Rules are stored in a local SQLite database at: `%APPDATA%\TidyUp\tidyup.db`

---

## Rules & Conditions

### How many rules can I create?
There's no hard limit. You can create as many rules as needed.

### Can rules run in a specific order?
Yes! Rules have an Execution Order property. Lower numbers run first.

### What's the difference between AND and OR conditions?
- **AND**: All conditions must be true
- **OR**: At least one condition must be true

Example:
- `Extension is .pdf AND Size > 1MB` - Only large PDFs
- `Extension is .pdf OR Extension is .docx` - All PDFs and Word docs

### Can I disable a rule temporarily?
Yes! Uncheck the "Enabled" checkbox and save the rule.

---

## Actions

### What happens if a file already exists at the destination?
TidyUp uses the Conflict Resolution strategy:
- **Skip**: Don't move/copy the file
- **Overwrite**: Replace the existing file
- **Rename New**: Add (1), (2), etc. to the new file
- **Rename Old**: Rename the existing file with timestamp
- **Prompt**: Ask what to do (not yet implemented)

### Can I move files to network drives?
Yes! Use UNC paths like `\\\\ServerName\\Share\\Folder`

### Why did my rename action fail?
Common reasons:
- Invalid characters in the new name (/ \\ : * ? " < > |)
- Path too long (Windows has a 260 character limit)
- File is locked by another program

---

## Variables

### Why isn't my variable working?
Check:
- Spelling (case-sensitive)
- Curly braces: `{VariableName}` not `${VariableName}`
- Variable is supported (see Variables Reference)

### Can I create custom variables?
Not yet, but this feature is planned for a future update.

---

## Performance

### Will TidyUp slow down my computer?
No. TidyUp uses file system watchers which are very efficient. It only processes files when they're created or modified.

### Can I monitor multiple folders with one rule?
Yes! Add multiple folders in the Folders tab.

### How often does TidyUp check for files?
TidyUp uses real-time file system notifications. Files are processed almost instantly.

---

## Troubleshooting

### Files aren't being processed
Check:
1. Rule is **Enabled**
2. Monitored folder path is correct
3. File matches your conditions
4. Check the Logs view for errors

### "Access Denied" errors
- Run TidyUp as Administrator (if monitoring system folders)
- Check folder permissions
- Ensure destination folders exist

### Variables showing {VariableName} literally
- Variable spelling is incorrect
- Variable doesn't exist (see Variables Reference)

### Rules not saving
- Check database file isn't read-only
- Ensure `%APPDATA%\TidyUp` folder exists and is writable

---

## Features

### Can TidyUp...
- **Monitor network folders?** Yes
- **Watch multiple folders?** Yes
- **Run on startup?** Yes (set in Windows Task Scheduler)
- **Process existing files?** Use Test/Preview on existing files, then enable
- **Send notifications?** Yes (enable in Settings)
- **Export/import rules?** Yes (Import/Export buttons in toolbar)

---

## Support

### Where can I get help?
1. Check this FAQ
2. Review the Getting Started guide
3. Check the Examples page
4. Review the Logs view for error messages

### How do I report a bug?
Please include:
- TidyUp version
- Windows version
- Steps to reproduce
- Error messages from Logs view

---

[Back to Getting Started](getting-started.md)
