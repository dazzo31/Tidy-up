[CmdletBinding()]
param(
    [string]$PublishPath = "$PSScriptRoot\..\publish\win-x64-framework-dependent\TidyUp.exe"
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path $PublishPath)) {
    throw "Executable not found at path: $PublishPath"
}

$tempProfile = Join-Path $env:TEMP ("TidyUp_Pkg_Test_" + [System.Guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path $tempProfile -Force | Out-Null

try {
    Write-Host "==========================================" -ForegroundColor Cyan
    Write-Host " Testing Packaged Binary Runtime Smoke" -ForegroundColor Cyan
    Write-Host " Target: $PublishPath" -ForegroundColor Cyan
    Write-Host " Isolated TIDYUP_APPDATA: $tempProfile" -ForegroundColor Cyan
    Write-Host "==========================================" -ForegroundColor Cyan

    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = (Resolve-Path $PublishPath).Path
    $psi.EnvironmentVariables["TIDYUP_APPDATA"] = $tempProfile
    $psi.UseShellExecute = $false

    $proc = [System.Diagnostics.Process]::Start($psi)
    if ($null -eq $proc) {
        throw "Failed to start process."
    }

    Write-Host "Process started successfully (PID: $($proc.Id))" -ForegroundColor Green

    # Wait up to 15 seconds for startup initialization and DB migration
    $dbPath = Join-Path $tempProfile "tidyup.db"
    $timeout = [System.DateTime]::UtcNow.AddSeconds(15)
    $dbSize = 0
    while ([System.DateTime]::UtcNow -lt $timeout) {
        if (Test-Path $dbPath) {
            $dbSize = (Get-Item $dbPath).Length
            if ($dbSize -ge 32768) {
                break
            }
        }
        Start-Sleep -Milliseconds 500
    }

    $dbExists = Test-Path $dbPath
    if (-not $dbExists) {
        throw "Packaged binary failed to create database on clean profile."
    }

    $dbSize = (Get-Item $dbPath).Length
    Write-Host "Database created: $dbExists (Size: $dbSize bytes)" -ForegroundColor Green
    if ($dbSize -lt 20480) {
        throw "Database size ($dbSize bytes) indicates incomplete schema migration."
    }

    # Verify process is still alive and responsive
    if ($proc.HasExited) {
        throw "Process exited prematurely with exit code: $($proc.ExitCode)"
    }
    Write-Host "Process is active and running cleanly." -ForegroundColor Green

    # Graceful stop
    $proc.Kill()
    $proc.WaitForExit(5000)
    Write-Host "Process terminated cleanly." -ForegroundColor Green

    Write-Host "Packaged binary verification PASSED!" -ForegroundColor Green
}
finally {
    [System.GC]::Collect()
    Start-Sleep -Milliseconds 500
    Remove-Item $tempProfile -Recurse -Force -ErrorAction SilentlyContinue
}
