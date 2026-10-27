# Floating Clock — Build & Package Script
# Usage: .\build.ps1
$ErrorActionPreference = "Stop"

$Root       = $PSScriptRoot
$Project    = Join-Path $Root "src\FloatingClock\FloatingClock.csproj"
$PublishDir = Join-Path $Root "publish"
$OutputDir  = Join-Path $Root "output"
$ISCC       = "C:\Users\SlimShady\AppData\Local\Programs\Inno Setup 6\ISCC.exe"
$IssFile    = Join-Path $Root "installer\FloatingClock.iss"

Write-Host "`n=== Floating Clock Build ===" -ForegroundColor Cyan

# ── Clean ──
Write-Host "[1/3] Cleaning…" -ForegroundColor Yellow
if (Test-Path $PublishDir) { Remove-Item $PublishDir -Recurse -Force }
if (Test-Path $OutputDir)  { Remove-Item $OutputDir  -Recurse -Force }

# ── Publish (framework-dependent) ──
Write-Host "[2/3] Publishing…" -ForegroundColor Yellow
dotnet publish $Project -c Release -o $PublishDir --self-contained false
if ($LASTEXITCODE -ne 0) { Write-Host "Publish FAILED." -ForegroundColor Red; exit 1 }

# ── Installer ──
if (Test-Path $ISCC) {
    Write-Host "[3/3] Building installer…" -ForegroundColor Yellow
    & $ISCC $IssFile
    if ($LASTEXITCODE -ne 0) { Write-Host "Installer FAILED." -ForegroundColor Red; exit 1 }
    Write-Host "`nInstaller → $OutputDir\FloatingClockSetup.exe" -ForegroundColor Green
} else {
    Write-Host "[3/3] Inno Setup not found – skipping installer." -ForegroundColor Yellow
}

Write-Host "`n=== Done ===" -ForegroundColor Cyan
