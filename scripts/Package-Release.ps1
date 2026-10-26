[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [string]$CandidateVersion = "1.2.1-rc.1"
)

$ErrorActionPreference = "Stop"

Write-Host "==========================================" -ForegroundColor Cyan
Write-Host " TidyUp Release Candidate Packaging" -ForegroundColor Cyan
Write-Host " Candidate Version: $CandidateVersion" -ForegroundColor Cyan
Write-Host " Configuration:     $Configuration" -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan

$repoRoot = (Resolve-Path "$PSScriptRoot\..").Path
$projectPath = Join-Path $repoRoot "TidyUp\TidyUp.csproj"
$publishDir = Join-Path $repoRoot "publish"

if (Test-Path $publishDir) {
    Remove-Item $publishDir -Recurse -Force
}
New-Item -ItemType Directory -Path $publishDir -Force | Out-Null

$commitSha = git -C $repoRoot rev-parse HEAD
$shortSha = git -C $repoRoot rev-parse --short HEAD
Write-Host "Git Commit: $commitSha ($shortSha)" -ForegroundColor Green

# 1. Publish Framework-Dependent win-x64
$fdTarget = Join-Path $publishDir "win-x64-framework-dependent"
Write-Host "`n--> Publishing win-x64 (Framework-Dependent)..." -ForegroundColor Yellow
dotnet publish $projectPath -c $Configuration -r win-x64 --self-contained false -o $fdTarget
if ($LASTEXITCODE -ne 0) { throw "Framework-dependent publish failed with exit code $LASTEXITCODE" }

# 2. Publish Self-Contained Single-File win-x64
$scTarget = Join-Path $publishDir "win-x64-self-contained"
Write-Host "`n--> Publishing win-x64 (Self-Contained Single-File)..." -ForegroundColor Yellow
dotnet publish $projectPath -c $Configuration -r win-x64 --self-contained true /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true -o $scTarget
if ($LASTEXITCODE -ne 0) { throw "Self-contained publish failed with exit code $LASTEXITCODE" }

# 3. Create Zip Packages
$packagesDir = Join-Path $publishDir "packages"
New-Item -ItemType Directory -Path $packagesDir -Force | Out-Null

$fdZip = Join-Path $packagesDir "TidyUp-$CandidateVersion-win-x64.zip"
$scZip = Join-Path $packagesDir "TidyUp-$CandidateVersion-win-x64-standalone.zip"

Write-Host "`n--> Creating Release Archives..." -ForegroundColor Yellow
Compress-Archive -Path "$fdTarget\*" -DestinationPath $fdZip -CompressionLevel Optimal
Compress-Archive -Path "$scTarget\*" -DestinationPath $scZip -CompressionLevel Optimal

# 4. Compute SHA-256 Checksums
Write-Host "`n--> Computing SHA-256 Hashes..." -ForegroundColor Yellow
$checksumsFile = Join-Path $packagesDir "SHA256SUMS.txt"
$hashes = @()

foreach ($zip in @($fdZip, $scZip)) {
    $hash = (Get-FileHash -Path $zip -Algorithm SHA256).Hash.ToLowerInvariant()
    $name = [System.IO.Path]::GetFileName($zip)
    $line = "$hash  $name"
    $hashes += $line
    Write-Host "  $line" -ForegroundColor Cyan
}

$hashes | Set-Content -Path $checksumsFile -Encoding UTF8

Write-Host "`n--> Packaging Complete!" -ForegroundColor Green
Write-Host "Artifacts located in: $packagesDir" -ForegroundColor Green

