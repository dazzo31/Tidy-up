# ============================================================
#  Merge all Google Photos subfolders from extracted takeouts
#  into a single D:\Takeout\Extract\Google Photos folder
#
#  Structure: D:\Takeout\Extract\takeout-*\Takeout\Google Photos\<album>\*
#  Target:    D:\Takeout\Extract\Google Photos\<album>\*
#
#  Handles duplicate filenames by appending a number suffix.
#  Deletes source folders after successful move.
# ============================================================

$ExtractRoot = "D:\Takeout\Extract"
$MergedDest  = Join-Path $ExtractRoot "Google Photos"
$LOG         = "D:\Takeout\MergeGooglePhotos.log"

function Write-Log {
    param([string]$Message, [string]$Color = "White")
    $ts = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    $line = "[$ts] $Message"
    Add-Content -Path $LOG -Value $line
    Write-Host $line -ForegroundColor $Color
}

# Create merged destination
if (-not (Test-Path $MergedDest)) {
    New-Item -ItemType Directory -Path $MergedDest -Force | Out-Null
}

# Find all Google Photos source folders
$takeoutDirs = Get-ChildItem $ExtractRoot -Directory | Where-Object { $_.Name -like "takeout-*" }
$gpFolders = @()
foreach ($td in $takeoutDirs) {
    $gp = Join-Path $td.FullName "Takeout\Google Photos"
    if (Test-Path $gp) { $gpFolders += $gp }
}

Write-Host ""
Write-Host "  Source folders : $($gpFolders.Count) Google Photos folders found" -ForegroundColor Cyan
Write-Host "  Destination    : $MergedDest" -ForegroundColor Cyan
Write-Host "  Log            : $LOG" -ForegroundColor Cyan
Write-Host ""

$totalFiles = 0
$movedFiles = 0
$skippedFiles = 0
$duplicateFiles = 0

foreach ($gpFolder in $gpFolders) {
    $parentName = Split-Path (Split-Path (Split-Path $gpFolder -Parent) -Parent) -Leaf
    Write-Host "--- $parentName ---" -ForegroundColor Yellow

    # Get all album subfolders
    $albums = Get-ChildItem $gpFolder -Directory -ErrorAction SilentlyContinue

    foreach ($album in $albums) {
        $destAlbum = Join-Path $MergedDest $album.Name
        if (-not (Test-Path $destAlbum)) {
            New-Item -ItemType Directory -Path $destAlbum -Force | Out-Null
        }

        $files = Get-ChildItem $album.FullName -File -ErrorAction SilentlyContinue
        foreach ($file in $files) {
            $totalFiles++
            $destFile = Join-Path $destAlbum $file.Name

            if (Test-Path $destFile) {
                $existingSize = (Get-Item $destFile).Length
                if ($existingSize -eq $file.Length) {
                    # Same name and size - likely duplicate, skip
                    $skippedFiles++
                    continue
                }
                # Different size - rename with suffix
                $baseName = [System.IO.Path]::GetFileNameWithoutExtension($file.Name)
                $ext = [System.IO.Path]::GetExtension($file.Name)
                $counter = 1
                do {
                    $newName = "${baseName}_${counter}${ext}"
                    $destFile = Join-Path $destAlbum $newName
                    $counter++
                } while (Test-Path $destFile)
                $duplicateFiles++
            }

            Move-Item -Path $file.FullName -Destination $destFile -Force
            $movedFiles++
        }

        # Also move any files directly in the album root (not in subfolders)
        # Move loose files at the Google Photos level (not in albums)
    }

    # Handle any files directly in the Google Photos folder (not in album subfolders)
    $looseFiles = Get-ChildItem $gpFolder -File -ErrorAction SilentlyContinue
    foreach ($file in $looseFiles) {
        $totalFiles++
        $destFile = Join-Path $MergedDest $file.Name

        if (Test-Path $destFile) {
            $existingSize = (Get-Item $destFile).Length
            if ($existingSize -eq $file.Length) {
                $skippedFiles++
                continue
            }
            $baseName = [System.IO.Path]::GetFileNameWithoutExtension($file.Name)
            $ext = [System.IO.Path]::GetExtension($file.Name)
            $counter = 1
            do {
                $newName = "${baseName}_${counter}${ext}"
                $destFile = Join-Path $MergedDest $newName
                $counter++
            } while (Test-Path $destFile)
            $duplicateFiles++
        }

        Move-Item -Path $file.FullName -Destination $destFile -Force
        $movedFiles++
    }

    Write-Log "Processed: $parentName"
}

# --- Clean up empty source directories ---
Write-Host ""
Write-Host "Cleaning up empty takeout directories..." -ForegroundColor Yellow
foreach ($gpFolder in $gpFolders) {
    # Remove empty album dirs
    Get-ChildItem $gpFolder -Directory -ErrorAction SilentlyContinue |
        Sort-Object { $_.FullName.Length } -Descending |
        ForEach-Object {
            if ((Get-ChildItem $_.FullName -Force -ErrorAction SilentlyContinue | Measure-Object).Count -eq 0) {
                Remove-Item $_.FullName -Force -ErrorAction SilentlyContinue
            }
        }
    # Remove Google Photos folder if empty
    if ((Get-ChildItem $gpFolder -Force -ErrorAction SilentlyContinue | Measure-Object).Count -eq 0) {
        Remove-Item $gpFolder -Force -ErrorAction SilentlyContinue
    }
}

# --- Summary ---
Write-Host ""
Write-Host "============================================================" -ForegroundColor Cyan
Write-Host "  COMPLETE" -ForegroundColor Green
Write-Host "  Total files found : $totalFiles"
Write-Host "  Moved             : $movedFiles"
Write-Host "  Skipped (dupes)   : $skippedFiles"
Write-Host "  Renamed (clash)   : $duplicateFiles"
Write-Host "  Destination       : $MergedDest"
Write-Host "  Log               : $LOG"
Write-Host "============================================================" -ForegroundColor Cyan
