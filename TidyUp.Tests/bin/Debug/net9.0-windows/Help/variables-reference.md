# Variables Reference

Variables allow you to create dynamic file names and paths. Use them in action destinations and rename patterns.

## Syntax

Variables are enclosed in curly braces: `{VariableName}`

Example: `C:\Documents\{Year}\{Month}\{FileName}`

## Available Variables

### File Information
- **{FileName}** - Original file name without extension
- **{FileNameWithExtension}** - Original file name with extension
- **{FileExtension}** - File extension (e.g., "pdf", "jpg")
- **{FileSize}** - File size in bytes
- **{FileSizeKB}** - File size in kilobytes
- **{FileSizeMB}** - File size in megabytes

### Date & Time
- **{Year}** - Current year (4 digits, e.g., "2025")
- **{Month}** - Current month number (2 digits, e.g., "01")
- **{MonthName}** - Current month name (e.g., "January")
- **{Day}** - Current day (2 digits, e.g., "09")
- **{Hour}** - Current hour (24-hour format, 2 digits)
- **{Minute}** - Current minute (2 digits)
- **{Second}** - Current second (2 digits)
- **{Date}** - Full date (YYYY-MM-DD)
- **{Time}** - Full time (HH-MM-SS)
- **{DateTime}** - Full date and time (YYYY-MM-DD_HH-MM-SS)

### File Dates
- **{CreationDate}** - File creation date (YYYY-MM-DD)
- **{ModifiedDate}** - File modification date (YYYY-MM-DD)
- **{CreationYear}** - Year file was created
- **{ModifiedYear}** - Year file was modified

### Counters
- **{Counter}** - Auto-incrementing number (001, 002, 003...)
- **{Random}** - Random 6-digit number

## Examples

### Organize by Date
```
Destination: C:\Photos\{Year}\{MonthName}\{FileName}
Result: C:\Photos\2025\November\vacation.jpg
```

### Add Timestamp to Name
```
Rename: {FileName}_{DateTime}
Result: document_2025-11-09_14-30-45.pdf
```

### Organize by File Type
```
Destination: C:\Files\{FileExtension}\{FileName}
Result: C:\Files\pdf\report.pdf
```

### Monthly Folders
```
Destination: C:\Documents\{Year}-{Month}\{FileNameWithExtension}
Result: C:\Documents\2025-11\report.pdf
```

---

## Tips

1. **Case Sensitive**: Variable names are case-sensitive
2. **Path Separators**: Use backslash `\` on Windows, forward slash `/` on other systems
3. **Invalid Characters**: Variables are sanitized to remove invalid path characters
4. **Missing Values**: If a variable can't be resolved, the original value is used

---

[Back to Getting Started](getting-started.md) | [View Examples](examples.md)
