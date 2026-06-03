# Setup Sewer-Studio Vision Sidecar Environment
# This script creates a .venv and installs pinned dependencies.

$ErrorActionPreference = "Stop"
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $scriptDir
# cu128-Nightly-Index: RTX 5090 (sm_120) braucht cu128. NIEMALS auf cu121 zuruecksetzen
# (cu121-Torch laeuft nicht auf sm_120). Siehe requirements-lock.txt-Header.
$torchCudaIndex = "https://download.pytorch.org/whl/nightly/cu128"
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

# 3b. Schutz-Check: cu121-Lock auf einer RTX-50xx (sm_120) wuerde die GPU-Pipeline
#     brechen. Wenn der Lockfile cu121 pinnt UND eine 50er-Karte erkannt wird -> hart ab.
$gpuName = ""
try {
    $gpuName = (& nvidia-smi --query-gpu=name --format=csv,noheader 2>$null | Select-Object -First 1)
} catch { $gpuName = "" }
$lockHasCu121 = Select-String -Path "requirements-lock.txt" -Pattern '\+cu121' -Quiet -ErrorAction SilentlyContinue
if ($lockHasCu121 -and $gpuName -match '50\d0') {
    Write-Host "  FEHLER: requirements-lock.txt pinnt torch +cu121, GPU ist '$gpuName' (sm_120)." -ForegroundColor Red
    Write-Host "  cu121-Torch laeuft NICHT auf dieser GPU und wuerde die Pipeline brechen." -ForegroundColor Yellow
    Write-Host "  Lock auf cu128 aktualisieren (siehe requirements-lock.txt-Header)." -ForegroundColor Yellow
    exit 1
}

# 4. Install Dependencies
Write-Host ""
Write-Host "  [3/3] Installiere Abhaengigkeiten aus requirements-lock.txt ..." -ForegroundColor White
if ($useUv) {
    Invoke-Native $uvCommand.Source "pip" "sync" "--extra-index-url" $torchCudaIndex "requirements-lock.txt"
} else {
    $pipPath = if ($IsWindows) { ".venv\Scripts\pip.exe" } else { ".venv/bin/pip" }
    Invoke-Native $pipPath "install" "--extra-index-url" $torchCudaIndex "-r" "requirements-lock.txt"
}

# 5. GPU-Check nach Installation: der cu128-Torch muss die GPU wirklich sehen,
#    sonst wurde ein falscher Build installiert (z.B. cu121) -> laut abbrechen statt still bricken.
Write-Host ""
Write-Host "  Pruefe GPU / Torch ..." -ForegroundColor White
$venvPython = if ($IsWindows) { ".venv\Scripts\python.exe" } else { ".venv/bin/python" }
$gpuCheck = @"
import sys
try:
    import torch
except Exception as exc:
    print('  TORCH-IMPORT-FEHLER:', exc); sys.exit(2)
ok = torch.cuda.is_available()
name = torch.cuda.get_device_name(0) if ok else 'CPU'
cap = 'sm_' + ''.join(map(str, torch.cuda.get_device_capability(0))) if ok else '-'
print(f'  torch={torch.__version__}  cuda_build={torch.version.cuda}  available={ok}  device={name}  capability={cap}')
if not ok:
    print('  FEHLER: torch.cuda.is_available()=False - falscher Torch-/CUDA-Build fuer diese GPU?')
    sys.exit(3)
"@
$gpuCheckPath = Join-Path $scriptDir "_gpu_check.py"
Set-Content -LiteralPath $gpuCheckPath -Value $gpuCheck -Encoding UTF8
& $venvPython $gpuCheckPath
$gpuOk = ($LASTEXITCODE -eq 0)
Remove-Item -LiteralPath $gpuCheckPath -Force -ErrorAction SilentlyContinue
if (-not $gpuOk) {
    Write-Host "  FEHLER: GPU-Check fehlgeschlagen - vermutlich falscher CUDA-/Torch-Stand." -ForegroundColor Red
    Write-Host "  Erwartet: cu128 fuer RTX 5090 (sm_120). Siehe requirements-lock.txt-Header." -ForegroundColor Yellow
    exit 1
}

Write-Host ""
Write-Host "  ==============================================" -ForegroundColor Cyan
Write-Host "   Setup erfolgreich abgeschlossen!" -ForegroundColor Green
Write-Host "   Starte den Sidecar mit: .\start_sidecar.ps1" -ForegroundColor Yellow
Write-Host "  ==============================================" -ForegroundColor Cyan
Write-Host ""
