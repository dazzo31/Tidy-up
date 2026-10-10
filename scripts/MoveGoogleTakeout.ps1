# ============================================================
#  Move Google Takeout from OneDrive to D:\Takeout
#  One file at a time: hydrate -> copy to D: -> delete source
#  Frees OneDrive local storage after each file
# ============================================================

$wj = [char]0x2060
$SRC = "C:\Users\dazzo\OneDrive\Apps 1\Google Download Your Data"
$DST = "D:\Takeout"
$LOG = "D:\Takeout_move.log"

# --- Verify source exists ---
if (-not (Test-Path $SRC)) {
    Write-Host "ERROR: Source folder not found: $SRC" -ForegroundColor Red
    exit 1
}

# --- Create destination ---
if (-not (Test-Path $DST)) { New-Item -ItemType Directory -Path $DST -Force | Out-Null }

function Write-Log {
    param([string]$Message)
    $ts = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    $line = "[$ts] $Message"
    Add-Content -Path $LOG -Value $line
    Write-Host $line
}

Write-Host ""
Write-Host "  Source : $SRC" -ForegroundColor Cyan
Write-Host "  Dest   : $DST" -ForegroundColor Cyan
Write-Host "  Log    : $LOG" -ForegroundColor Cyan
Write-Host ""
Write-Host "  Strategy: Hydrate one file at a time, copy to D:, delete source" -ForegroundColor Yellow
Write-Host ""

# --- Get file list ---
$files = Get-ChildItem -Path $SRC -Recurse -File
$total = $files.Count
$current = 0
$failed = @()
$totalBytesMoved = 0L

Write-Log "Starting move of $total files from OneDrive to $DST"

foreach ($file in $files) {
    $current++
    $sizeMB = [math]::Round($file.Length / 1MB, 1)
    $sizeGB = [math]::Round($file.Length / 1GB, 2)
    $relPath = $file.FullName.Substring($SRC.Length).TrimStart('\')
    $destFile = Join-Path $DST $relPath
    $destDir = Split-Path $destFile -Parent

    Write-Host ""
    Write-Host "=== [$current/$total] $relPath (${sizeGB} GB) ===" -ForegroundColor Cyan

    # Skip if already copied
    if (Test-Path $destFile) {
        $destInfo = Get-Item $destFile
        if ($destInfo.Length -eq $file.Length) {
            Write-Log "SKIP (already exists): $relPath"
            # Delete source since dest is complete
            Remove-Item $file.FullName -Force
            Write-Host "  Deleted source (dest already has it)" -ForegroundColor DarkGray
            continue
        }
    }

    # --- Create destination subdirectory ---
    if (-not (Test-Path $destDir)) {
        New-Item -ItemType Directory -Path $destDir -Force | Out-Null
    }

    # --- Step 1: Hydrate (download from OneDrive cloud) ---
    $maxRetries = 5
    $retryCount = 0
    $success = $false

    while (-not $success -and $retryCount -lt $maxRetries) {
        try {
            Write-Host ""
            Write-Host "  Triggering OneDrive download..." -ForegroundColor Yellow

            # Use robocopy for a single file - it handles OneDrive timeouts natively
            $srcDir = Split-Path $file.FullName -Parent
            $fileName = $file.Name

            $robocopyResult = robocopy $srcDir $destDir $fileName /Z /R:30 /W:60 /NP /COPY:DAT /J 2>&1
            $rc = $LASTEXITCODE

            if ($rc -lt 8) {
                # Verify copy
                $destInfo = Get-Item $destFile -ErrorAction SilentlyContinue
                if ($destInfo -and $destInfo.Length -eq $file.Length) {
                    $success = $true
                    Write-Log "OK: $relPath (${sizeGB} GB)"
                } else {
                    throw "Size mismatch or missing: source=$($file.Length) dest=$($destInfo.Length)"
                }
            } else {
                throw "Robocopy failed with exit code $rc"
            }
        } catch {
            $retryCount++
            # Remove partial dest file
            if (Test-Path $destFile) { Remove-Item $destFile -Force -ErrorAction SilentlyContinue }
            Write-Host ""
            Write-Host "  Error (attempt $retryCount/$maxRetries): $($_.Exception.Message)" -ForegroundColor Red
            if ($retryCount -lt $maxRetries) {
                Write-Host "  Waiting 60s before retry..." -ForegroundColor Yellow
                Start-Sleep -Seconds 60
            }
        }
    }

    if ($success) {
        # --- Step 2: Delete source to free OneDrive local storage ---
        Remove-Item $file.FullName -Force
        Write-Host "  Source deleted - local space freed" -ForegroundColor Green
        $totalBytesMoved += $file.Length
        $totalMovedGB = [math]::Round($totalBytesMoved / 1GB, 2)
        Write-Host "  Total moved so far: $totalMovedGB GB" -ForegroundColor DarkGray
    } else {
        Write-Log "FAILED: $relPath after $maxRetries retries"
        $failed += $relPath
    }
}

# --- Clean up empty source directories ---
Write-Host ""
Write-Host "Cleaning up empty source directories..." -ForegroundColor Yellow
Get-ChildItem -Path $SRC -Recurse -Directory | Sort-Object { $_.FullName.Length } -Descending | ForEach-Object {
    if ((Get-ChildItem $_.FullName -Force | Measure-Object).Count -eq 0) {
        Remove-Item $_.FullName -Force
    }
}

# --- Summary ---
$totalMovedGB = [math]::Round($totalBytesMoved / 1GB, 2)
Write-Host ""
Write-Host "============================================================" -ForegroundColor Cyan
Write-Host "  COMPLETE" -ForegroundColor Green
Write-Host "  Files moved: $($total - $failed.Count) / $total"
Write-Host "  Total moved: $totalMovedGB GB"
if ($failed.Count -gt 0) {
    Write-Host "  FAILED ($($failed.Count)):" -ForegroundColor Red
    $failed | ForEach-Object { Write-Host "    $_" -ForegroundColor Red }
}
Write-Host "  Log: $LOG"
Write-Host "============================================================" -ForegroundColor Cyan
