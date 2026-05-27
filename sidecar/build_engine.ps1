# Rebuild Sewer-Studio YOLO TensorRT engine.
# This script is hardware-bound and should be run on the target GPU machine.

[CmdletBinding()]
param(
    [string]$ModelDir = "",
    [string]$WeightsName = "yolo26m.pt",
    [string]$OnnxName = "yolo26m.onnx",
    [string]$EngineName = "yolo26m.engine",
    [string]$TrtExecPath = "",
    [int]$ImageSize = 640,
    [int]$Opset = 17,
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $scriptDir

if ([string]::IsNullOrWhiteSpace($ModelDir)) {
    $ModelDir = Join-Path $scriptDir "models\yolo26m"
}

$pythonPath = Join-Path $scriptDir ".venv\Scripts\python.exe"
$weightsPath = Join-Path $ModelDir $WeightsName
$onnxPath = Join-Path $ModelDir $OnnxName
$enginePath = Join-Path $ModelDir $EngineName
$tempEnginePath = Join-Path $ModelDir "$EngineName.new"
$backupDir = Join-Path $ModelDir "engine_backups"
$tempScriptDir = Join-Path $scriptDir ".engine_build_tmp"

function Invoke-Native {
    param(
        [Parameter(Mandatory = $true)]
        [string]$FilePath,
        [Parameter(ValueFromRemainingArguments = $true)]
        [string[]]$Arguments
    )

    if ($DryRun) {
        Write-Host "DRY-RUN: $FilePath $($Arguments -join ' ')" -ForegroundColor DarkGray
        return
    }

    & $FilePath @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Command failed with exit code ${LASTEXITCODE}: $FilePath $($Arguments -join ' ')"
    }
}

function Get-CommandOutput {
    param(
        [string]$FilePath,
        [string[]]$Arguments
    )

    try {
        if ($DryRun -or -not (Test-Path $FilePath)) {
            return ""
        }

        $output = & $FilePath @Arguments 2>&1
        return ($output -join "`n").Trim()
    } catch {
        return $_.Exception.Message
    }
}

function Write-TempPythonScript {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name,
        [Parameter(Mandatory = $true)]
        [string]$ScriptText
    )

    New-Item -ItemType Directory -Force -Path $tempScriptDir | Out-Null
    $scriptPath = Join-Path $tempScriptDir $Name
    Set-Content -LiteralPath $scriptPath -Value $ScriptText -Encoding UTF8
    return $scriptPath
}

function Invoke-PythonBlock {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name,
        [Parameter(Mandatory = $true)]
        [string]$ScriptText
    )

    if ($DryRun) {
        Write-Host "DRY-RUN: $pythonPath .engine_build_tmp\$Name" -ForegroundColor DarkGray
        Write-Host $ScriptText -ForegroundColor DarkGray
        return
    }

    $scriptPath = Write-TempPythonScript -Name $Name -ScriptText $ScriptText
    Invoke-Native $pythonPath $scriptPath
}

function Resolve-TrtExec {
    if (-not [string]::IsNullOrWhiteSpace($TrtExecPath)) {
        return $TrtExecPath
    }

    $command = Get-Command "trtexec.exe" -ErrorAction SilentlyContinue
    if ($command) {
        return $command.Source
    }

    $command = Get-Command "trtexec" -ErrorAction SilentlyContinue
    if ($command) {
        return $command.Source
    }

    return ""
}

function Get-Sha256 {
    param([string]$Path)

    if (-not (Test-Path $Path)) {
        return ""
    }

    return (Get-FileHash -Algorithm SHA256 -Path $Path).Hash.ToLowerInvariant()
}

function Get-PythonInfo {
    if ($DryRun -or -not (Test-Path $pythonPath)) {
        return @{
            python = ""
            torch = ""
            ultralytics = ""
            tensorrt = ""
            cuda_available = ""
            gpu = ""
        }
    }

    $probe = @"
import json
info = {}
try:
    import sys
    info["python"] = sys.version.split()[0]
except Exception as exc:
    info["python"] = str(exc)
try:
    import torch
    info["torch"] = torch.__version__
    info["cuda_available"] = bool(torch.cuda.is_available())
    info["gpu"] = torch.cuda.get_device_name(0) if torch.cuda.is_available() else ""
except Exception as exc:
    info["torch"] = str(exc)
    info["cuda_available"] = ""
    info["gpu"] = ""
try:
    import ultralytics
    info["ultralytics"] = ultralytics.__version__
except Exception as exc:
    info["ultralytics"] = str(exc)
try:
    import tensorrt
    info["tensorrt"] = tensorrt.__version__
except Exception as exc:
    info["tensorrt"] = str(exc)
print(json.dumps(info))
"@

    $probePath = Write-TempPythonScript -Name "probe_versions.py" -ScriptText $probe
    $json = & $pythonPath $probePath
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($json)) {
        return @{
            python = ""
            torch = ""
            ultralytics = ""
            tensorrt = ""
            cuda_available = ""
            gpu = ""
        }
    }

    return ($json | ConvertFrom-Json)
}

Write-Host ""
Write-Host "  ==============================================" -ForegroundColor Cyan
Write-Host "   Sewer-Studio TensorRT Engine Rebuild" -ForegroundColor Cyan
Write-Host "  ==============================================" -ForegroundColor Cyan
Write-Host ""

if (-not (Test-Path $pythonPath) -and -not $DryRun) {
    throw "Python venv nicht gefunden: $pythonPath. Bitte zuerst .\setup.ps1 ausfuehren."
}

if (-not (Test-Path $weightsPath) -and -not $DryRun) {
    throw "YOLO-Gewichte nicht gefunden: $weightsPath"
}

$resolvedTrtExec = Resolve-TrtExec
$engineBuilder = if ([string]::IsNullOrWhiteSpace($resolvedTrtExec)) { "python-tensorrt" } else { "trtexec" }
if ([string]::IsNullOrWhiteSpace($resolvedTrtExec) -and $DryRun) {
    $resolvedTrtExec = ""
}

$timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
$pythonInfo = Get-PythonInfo
$trtExecVersion = if ([string]::IsNullOrWhiteSpace($resolvedTrtExec)) { "" } else { Get-CommandOutput $resolvedTrtExec @("--version") }

Write-Host "  Modellordner: $ModelDir" -ForegroundColor White
Write-Host "  Gewichte:     $weightsPath" -ForegroundColor White
Write-Host "  ONNX:         $onnxPath" -ForegroundColor White
Write-Host "  Engine:       $enginePath" -ForegroundColor White
Write-Host "  Builder:      $engineBuilder" -ForegroundColor White
Write-Host "  trtexec:      $(if ($resolvedTrtExec) { $resolvedTrtExec } else { '<nicht gefunden, Python-Fallback>' })" -ForegroundColor White
Write-Host ""

if (-not $DryRun) {
    New-Item -ItemType Directory -Force -Path $backupDir | Out-Null
} else {
    Write-Host "DRY-RUN: Ensure directory $backupDir" -ForegroundColor DarkGray
}

if (Test-Path $enginePath) {
    $backupEnginePath = Join-Path $backupDir "$([IO.Path]::GetFileNameWithoutExtension($EngineName))_$timestamp.engine"
    $backupMetaPath = Join-Path $backupDir "$([IO.Path]::GetFileNameWithoutExtension($EngineName))_$timestamp.backup.json"

    Write-Host "  Sichere alte Engine ..." -ForegroundColor Yellow
    if (-not $DryRun) {
        Copy-Item -LiteralPath $enginePath -Destination $backupEnginePath -Force
    } else {
        Write-Host "DRY-RUN: Copy-Item $enginePath -> $backupEnginePath" -ForegroundColor DarkGray
    }

    $engineInfo = if (Test-Path $enginePath) { Get-Item $enginePath } else { $null }
    $backupMeta = [ordered]@{
        created_utc = (Get-Date).ToUniversalTime().ToString("o")
        source_engine = $enginePath
        backup_engine = $backupEnginePath
        source_engine_sha256 = Get-Sha256 $enginePath
        source_engine_size_bytes = if ($engineInfo) { $engineInfo.Length } else { 0 }
        source_engine_last_write_utc = if ($engineInfo) { $engineInfo.LastWriteTimeUtc.ToString("o") } else { "" }
        python = $pythonInfo.python
        torch = $pythonInfo.torch
        ultralytics = $pythonInfo.ultralytics
        tensorrt_python = $pythonInfo.tensorrt
        cuda_available = $pythonInfo.cuda_available
        gpu = $pythonInfo.gpu
        engine_builder = $engineBuilder
        trtexec = $resolvedTrtExec
        trtexec_version = $trtExecVersion
    }

    if (-not $DryRun) {
        $backupMeta | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $backupMetaPath -Encoding UTF8
    } else {
        Write-Host "DRY-RUN: Write backup metadata $backupMetaPath" -ForegroundColor DarkGray
    }
}

if (Test-Path $tempEnginePath) {
    if (-not $DryRun) {
        Remove-Item -LiteralPath $tempEnginePath -Force
    } else {
        Write-Host "DRY-RUN: Remove old temp engine $tempEnginePath" -ForegroundColor DarkGray
    }
}

Write-Host "  Exportiere ONNX via Ultralytics ..." -ForegroundColor White
$exportScript = @"
from pathlib import Path
from ultralytics import YOLO

weights = Path(r"$weightsPath")
model = YOLO(str(weights))
model.export(format="onnx", imgsz=$ImageSize, opset=$Opset, simplify=False, dynamic=False, half=False, device="cpu")
"@
Invoke-PythonBlock -Name "export_onnx.py" -ScriptText $exportScript

Write-Host "  Baue TensorRT Engine mit fp16 ..." -ForegroundColor White
if ($engineBuilder -eq "trtexec") {
    Invoke-Native $resolvedTrtExec "--onnx=$onnxPath" "--saveEngine=$tempEnginePath" "--fp16"
} else {
    $buildScript = @"
from pathlib import Path
import sys
import tensorrt as trt

onnx_path = Path(r"$onnxPath")
engine_path = Path(r"$tempEnginePath")
logger = trt.Logger(trt.Logger.INFO)
builder = trt.Builder(logger)
network = builder.create_network(1 << int(trt.NetworkDefinitionCreationFlag.EXPLICIT_BATCH))
parser = trt.OnnxParser(network, logger)

data = onnx_path.read_bytes()
if not parser.parse(data):
    for index in range(parser.num_errors):
        print(parser.get_error(index), file=sys.stderr)
    raise SystemExit(1)

config = builder.create_builder_config()
if hasattr(trt, "MemoryPoolType"):
    config.set_memory_pool_limit(trt.MemoryPoolType.WORKSPACE, 4 << 30)
if builder.platform_has_fast_fp16:
    config.set_flag(trt.BuilderFlag.FP16)

serialized = builder.build_serialized_network(network, config)
if serialized is None:
    raise SystemExit("TensorRT did not return a serialized engine.")

engine_path.write_bytes(serialized)
"@
    Invoke-PythonBlock -Name "build_engine.py" -ScriptText $buildScript
}

if (-not $DryRun) {
    if (-not (Test-Path $tempEnginePath)) {
        throw "TensorRT hat keine Engine erzeugt: $tempEnginePath"
    }

    Move-Item -LiteralPath $tempEnginePath -Destination $enginePath -Force

    $newMetaPath = Join-Path $ModelDir "$([IO.Path]::GetFileNameWithoutExtension($EngineName)).build.json"
    $newEngineInfo = Get-Item $enginePath
    $newMeta = [ordered]@{
        created_utc = (Get-Date).ToUniversalTime().ToString("o")
        engine = $enginePath
        engine_sha256 = Get-Sha256 $enginePath
        engine_size_bytes = $newEngineInfo.Length
        weights = $weightsPath
        weights_sha256 = Get-Sha256 $weightsPath
        onnx = $onnxPath
        onnx_sha256 = Get-Sha256 $onnxPath
        image_size = $ImageSize
        opset = $Opset
        fp16 = $true
        python = $pythonInfo.python
        torch = $pythonInfo.torch
        ultralytics = $pythonInfo.ultralytics
        tensorrt_python = $pythonInfo.tensorrt
        cuda_available = $pythonInfo.cuda_available
        gpu = $pythonInfo.gpu
        engine_builder = $engineBuilder
        trtexec = $resolvedTrtExec
        trtexec_version = $trtExecVersion
    }
    $newMeta | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $newMetaPath -Encoding UTF8
}

if (-not $DryRun -and (Test-Path $tempScriptDir)) {
    Remove-Item -LiteralPath $tempScriptDir -Recurse -Force
}

Write-Host ""
Write-Host "  Engine-Rebuild abgeschlossen." -ForegroundColor Green
Write-Host "  Starte den Sidecar danach mit:" -ForegroundColor Yellow
Write-Host "    `$env:SEWER_SIDECAR_YOLO_MODEL_NAME = `"$EngineName`"" -ForegroundColor White
Write-Host "    .\start_sidecar.ps1" -ForegroundColor White
Write-Host ""
