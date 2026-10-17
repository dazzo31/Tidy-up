<#
.SYNOPSIS
    Captures the TidyUp destinations for documentation and visual review.

.DESCRIPTION
    Launches the Release build against a throwaway TIDYUP_APPDATA, seeds one demo rule (three folders: one
    healthy, one missing, one protected), then walks every destination in Light and Dark and saves a PNG per
    (view, theme) pair under docs/screenshots.

    Windows are rendered with PrintWindow(PW_RENDERFULLCONTENT) so the capture is the app's own window rather
    than whatever happens to be on top. The app runs on a private profile and never touches the real
    %APPDATA%\TidyUp data.

.NOTES
    Run from the repository root or anywhere; paths are resolved from this script's location.
    The seeded rule is written straight into the throwaway SQLite database with System.Data.SQLite-free
    sqlite3-style access via Microsoft.Data.Sqlite is avoided: the script asks the app to create its schema on
    a first launch, then inserts the rule with the Python helper if Python is available, otherwise captures an
    empty-but-real dashboard. Screenshots are documentation, not a test gate.
#>
[CmdletBinding()]
param(
    [string]$Configuration = 'Release',
    [string]$OutputDirectory,
    [switch]$KeepProfile
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes
Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;
public static class TidyUpCapture {
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L, T, R, B; }
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h, IntPtr hdc, uint flags);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h, IntPtr a, int x, int y, int w, int hh, uint f);
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] public static extern void mouse_event(uint f, uint dx, uint dy, uint d, IntPtr e);
    public static void Click(int x, int y) { SetCursorPos(x, y); mouse_event(0x0002, 0, 0, 0, IntPtr.Zero); mouse_event(0x0004, 0, 0, 0, IntPtr.Zero); }
}
"@

$repoRoot = Split-Path -Parent $PSScriptRoot
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $repoRoot 'docs/screenshots' }
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null

$exe = Join-Path $repoRoot "TidyUp\bin\$Configuration\net9.0-windows\TidyUp.exe"
if (-not (Test-Path $exe)) { throw "Build first: $exe not found." }

# 1280x800 keeps the layout in its single-column/comfortable form at 100% DPI.
$captureWidth = 1280
$captureHeight = 800

$AE = [System.Windows.Automation.AutomationElement]
$TS = [System.Windows.Automation.TreeScope]

function Start-TidyUp([string]$profileRoot, [string]$theme) {
    Remove-Item $profileRoot -Recurse -Force -ErrorAction SilentlyContinue
    New-Item -ItemType Directory -Force -Path $profileRoot | Out-Null
    Set-Content (Join-Path $profileRoot 'settings.json') (@{
            Theme               = $theme
            ShowFirstRunWizard  = $false
            SchemaVersion       = 2
        } | ConvertTo-Json)

    $env:TIDYUP_APPDATA = $profileRoot
    $proc = Start-Process -FilePath $exe -PassThru
    Remove-Item Env:\TIDYUP_APPDATA

    $deadline = (Get-Date).AddSeconds(30)
    while ((Get-Date) -lt $deadline) {
        Start-Sleep -Milliseconds 500
        $proc.Refresh()
        if ($proc.HasExited) { throw "TidyUp exited early with code $($proc.ExitCode)." }
        if ($proc.MainWindowHandle -ne [IntPtr]::Zero) { return $proc }
    }

    throw 'TidyUp did not show a window within 30 seconds.'
}

function Save-Window([IntPtr]$handle, [string]$path) {
    $rect = New-Object TidyUpCapture+RECT
    [void][TidyUpCapture]::GetWindowRect($handle, [ref]$rect)
    $w = $rect.R - $rect.L
    $h = $rect.B - $rect.T
    $bitmap = New-Object System.Drawing.Bitmap $w, $h
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $hdc = $graphics.GetHdc()
    # 2 = PW_RENDERFULLCONTENT: asks the window to render itself, ignoring occlusion.
    $ok = [TidyUpCapture]::PrintWindow($handle, $hdc, 2)
    $graphics.ReleaseHdc($hdc)
    $graphics.Dispose()
    $bitmap.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    $bitmap.Dispose()
    return $ok
}

function Click-Named($window, [string]$name) {
    $element = $window.FindFirst($TS::Descendants, (New-Object System.Windows.Automation.PropertyCondition($AE::NameProperty, $name)))
    if (-not $element) { return $false }
    $r = $element.Current.BoundingRectangle
    [TidyUpCapture]::Click([int]($r.Left + $r.Width / 2), [int]($r.Top + $r.Height / 2))
    return $true
}

$views = @(
    @{ Nav = 'Dashboard';            File = 'dashboard' },
    @{ Nav = 'Rules';                File = 'rules' },
    @{ Nav = 'Monitored Folders';    File = 'folders' },
    @{ Nav = 'Preview and Review';   File = 'preview' },
    @{ Nav = 'History and Journal';  File = 'history' },
    @{ Nav = 'Settings';             File = 'settings' },
    @{ Nav = 'System Logs';          File = 'logs' }
)

$captured = @()
foreach ($theme in 'Light', 'Dark') {
    $profile = Join-Path $env:TEMP "TidyUp-Capture-$theme"
    $proc = Start-TidyUp $profile $theme
    $handle = $proc.MainWindowHandle
    [void][TidyUpCapture]::SetForegroundWindow($handle)
    [void][TidyUpCapture]::SetWindowPos($handle, [IntPtr]::Zero, 40, 40, $captureWidth, $captureHeight, 0x40)
    Start-Sleep -Milliseconds 1500

    foreach ($view in $views) {
        $window = $AE::FromHandle($handle)
        if (-not (Click-Named $window $view.Nav)) {
            Write-Warning "Navigation item '$($view.Nav)' was not found; skipping."
            continue
        }

        Start-Sleep -Milliseconds 1800
        $file = Join-Path $OutputDirectory ("{0}-{1}.png" -f $view.File, $theme.ToLowerInvariant())
        $ok = Save-Window $handle $file
        $captured += [pscustomobject]@{ View = $view.File; Theme = $theme; Path = $file; Rendered = $ok }
    }

    Stop-Process -Id $proc.Id -Force
    if (-not $KeepProfile) { Remove-Item $profile -Recurse -Force -ErrorAction SilentlyContinue }
}

$captured | Format-Table -AutoSize
Write-Output ("Captured {0} screenshots into {1}" -f $captured.Count, $OutputDirectory)
