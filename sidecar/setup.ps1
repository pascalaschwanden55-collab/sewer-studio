# Setup Sewer-Studio Vision Sidecar Environment
# This script creates a .venv and installs pinned dependencies.

$ErrorActionPreference = "Stop"
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $scriptDir
$torchCudaIndex = "https://download.pytorch.org/whl/cu121"
$env:UV_CACHE_DIR = Join-Path $scriptDir ".uv-cache"

function Invoke-Native {
    param(
        [Parameter(Mandatory = $true)]
        [string]$FilePath,
        [Parameter(ValueFromRemainingArguments = $true)]
        [string[]]$Arguments
    )

    & $FilePath @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Command failed with exit code ${LASTEXITCODE}: $FilePath $($Arguments -join ' ')"
    }
}

Write-Host ""
Write-Host "  ==============================================" -ForegroundColor Cyan
Write-Host "   Sewer-Studio Sidecar Setup" -ForegroundColor Cyan
Write-Host "  ==============================================" -ForegroundColor Cyan
Write-Host ""

# 1. Check Python
Write-Host "  [1/3] Pruefe Python ..." -ForegroundColor White
$pythonCommand = Get-Command python -ErrorAction SilentlyContinue
if (-not $pythonCommand) {
    Write-Host "  FEHLER: Python nicht gefunden. Bitte Python 3.10+ installieren." -ForegroundColor Red
    exit 1
}
$pythonVersion = & $pythonCommand.Source --version 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Host "  FEHLER: Python konnte nicht gestartet werden." -ForegroundColor Red
    exit 1
}
Write-Host "  Gefunden: $pythonVersion" -ForegroundColor Gray

# 2. Check uv (preferred)
$useUv = $false
$uvCommand = Get-Command uv -ErrorAction SilentlyContinue
if ($uvCommand) {
    $uvVersion = & $uvCommand.Source --version 2>&1
    $useUv = $true
    Write-Host "  uv gefunden ($($uvVersion.Split(' ')[1])), nutze uv fuer schnelles Setup." -ForegroundColor Gray
    Write-Host "  uv Cache: $env:UV_CACHE_DIR" -ForegroundColor Gray
} else {
    Write-Host "  uv nicht gefunden, nutze Standard pip (langsamer)." -ForegroundColor Gray
}

# 3. Create .venv
Write-Host ""
Write-Host "  [2/3] Erstelle/Pruefe .venv ..." -ForegroundColor White
if ($useUv) {
    Invoke-Native $uvCommand.Source "venv" ".venv" "--clear"
} else {
    if (-not (Test-Path ".venv")) {
        Invoke-Native $pythonCommand.Source "-m" "venv" ".venv"
    }
}
Write-Host "  .venv ist bereit." -ForegroundColor Green

# 4. Install Dependencies
Write-Host ""
Write-Host "  [3/3] Installiere Abhaengigkeiten aus requirements-lock.txt ..." -ForegroundColor White
if ($useUv) {
    Invoke-Native $uvCommand.Source "pip" "sync" "--extra-index-url" $torchCudaIndex "requirements-lock.txt"
} else {
    $pipPath = if ($IsWindows) { ".venv\Scripts\pip.exe" } else { ".venv/bin/pip" }
    Invoke-Native $pipPath "install" "--extra-index-url" $torchCudaIndex "-r" "requirements-lock.txt"
}

Write-Host ""
Write-Host "  ==============================================" -ForegroundColor Cyan
Write-Host "   Setup erfolgreich abgeschlossen!" -ForegroundColor Green
Write-Host "   Starte den Sidecar mit: .\start_sidecar.ps1" -ForegroundColor Yellow
Write-Host "  ==============================================" -ForegroundColor Cyan
Write-Host ""
