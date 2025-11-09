# Common Examples

## 1. Organize Downloads by File Type

**Use Case**: Automatically sort downloads into category folders

**Folders**: `C:\Users\YourName\Downloads`

**Conditions**:
- File extension is ".pdf" → Move to `Documents\PDF`
- File extension is ".jpg" OR ".png" → Move to `Pictures`
- File extension is ".zip" OR ".rar" → Move to `Archives`

**Tip**: Create separate rules for each file type category

---

## 2. Backup Photos by Date

**Use Case**: Organize photos into yearly and monthly folders

**Folders**: `C:\Users\YourName\Pictures\Camera`

**Conditions**:
- File extension is ".jpg" OR ".png" OR ".heic"

**Actions**:
- Move to: `C:\Users\YourName\Pictures\Archive\{CreationYear}\{Month}-{MonthName}`

**Result**: `Pictures\Archive\2025\11-November\IMG_1234.jpg`

---

## 3. Auto-Archive Old Documents

**Use Case**: Move documents older than 90 days to archive

**Folders**: `C:\Users\YourName\Documents\Active`

**Conditions**:
- File date (Modified) is older than 90 days
- File extension is ".docx" OR ".pdf"

**Actions**:
- Move to: `C:\Users\YourName\Documents\Archive\{ModifiedYear}`

---

## 4. Screenshot Organizer

**Use Case**: Automatically rename and organize screenshots

**Folders**: `C:\Users\YourName\Pictures`

**Conditions**:
- File name contains "Screenshot"

**Actions**:
- Rename to: `Screenshot_{Date}_{Time}`
- Move to: `C:\Users\YourName\Pictures\Screenshots\{Year}\{Month}`

**Result**: `Screenshots\2025\11\Screenshot_2025-11-09_14-30-00.png`

---

## 5. Work Project Organizer

**Use Case**: Sort work files into client folders

**Folders**: `C:\Users\YourName\Desktop`

**Conditions**:
- File name contains "ClientA"

**Actions**:
- Move to: `C:\Work\ClientA\{Year}\{FileExtension}`

**Multiple Rules**: Create similar rules for each client

---

## 6. Download Cleanup

**Use Case**: Delete temporary files from downloads

**Folders**: `C:\Users\YourName\Downloads`

**Conditions**:
- File extension is ".tmp" OR ".temp"
- File date (Modified) is older than 7 days

**Actions**:
- Delete file

**Warning**: Always test delete actions with preview first!

---

## 7. Music Library Organizer

**Use Case**: Organize music by artist folders

**Folders**: `C:\Users\YourName\Music\Unsorted`

**Conditions**:
- File extension is ".mp3" OR ".flac" OR ".m4a"

**Actions**:
- Move to: `C:\Users\YourName\Music\Organized\{FileExtension}`

**Note**: For advanced ID3 tag-based sorting, use specialized music software

---

## 8. Duplicate File Handler

**Use Case**: Rename duplicate downloads automatically

**Folders**: `C:\Users\YourName\Downloads`

**Conditions**:
- Any file

**Actions**:
- Move to: `C:\Users\YourName\Downloads\Sorted`
- Conflict Resolution: **Rename New** (adds counter like "file(1).pdf")

---

## 9. Batch Rename by Date

**Use Case**: Rename files to include creation date

**Folders**: `C:\Users\YourName\Documents\ToRename`

**Conditions**:
- File extension is ".pdf"

**Actions**:
- Rename to: `{CreationDate}_{FileName}`

**Result**: `2025-11-09_report.pdf`

---

## 10. Email Attachment Organizer

**Use Case**: Sort saved email attachments

**Folders**: `C:\Users\YourName\Downloads`

**Conditions**:
- File name contains "invoice" OR "receipt"

**Actions**:
- Move to: `C:\Users\YourName\Documents\Receipts\{Year}`

---

[Back to Getting Started](getting-started.md) | [Variables Reference](variables-reference.md)
