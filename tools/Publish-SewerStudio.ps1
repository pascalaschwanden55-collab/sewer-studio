[CmdletBinding()]
param(
    [string]$OutputDirectory = "",
    [switch]$WithoutModels
)

$ErrorActionPreference = "Stop"
$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$projectPath = Join-Path $repoRoot "src\AuswertungPro.Next.UI\AuswertungPro.Next.UI.csproj"
$sidecarSource = Join-Path $repoRoot "sidecar"

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $stamp = Get-Date -Format "yyyyMMdd-HHmmss"
    $OutputDirectory = Join-Path $repoRoot "artifacts\SewerStudio-4.5.0-win-x64-$stamp"
}

$outputPath = [System.IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $outputPath) {
    if ((Get-ChildItem -LiteralPath $outputPath -Force | Select-Object -First 1)) {
        throw "Der Ausgabeordner ist nicht leer: $outputPath"
    }
} else {
    New-Item -ItemType Directory -Path $outputPath -Force | Out-Null
}

function Invoke-Native {
    param(
        [Parameter(Mandatory = $true)]
        [string]$FilePath,
        [Parameter(ValueFromRemainingArguments = $true)]
        [string[]]$Arguments
    )

    & $FilePath @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Befehl fehlgeschlagen ($LASTEXITCODE): $FilePath $($Arguments -join ' ')"
    }
}

function Copy-RequiredFile {
    param([string]$RelativePath)

    $source = Join-Path $sidecarSource $RelativePath
    if (-not (Test-Path -LiteralPath $source -PathType Leaf)) {
        throw "Pflichtdatei des Sidecars fehlt: $source"
    }

    $target = Join-Path (Join-Path $outputPath "sidecar") $RelativePath
    $targetDirectory = Split-Path -Parent $target
    New-Item -ItemType Directory -Path $targetDirectory -Force | Out-Null
    Copy-Item -LiteralPath $source -Destination $target -Force
}

function Write-Utf8NoBom {
    param(
        [string]$Path,
        [string]$Content
    )

    $encoding = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText($Path, $Content, $encoding)
}

Write-Host "[1/4] Erzeuge eigenstaendige Windows-App ..."
$publishArguments = @(
    "publish",
    $projectPath,
    "--configuration", "Release",
    "--runtime", "win-x64",
    "--self-contained", "true",
    "--output", $outputPath,
    "-p:RestoreLockedMode=true",
    "-p:PublishSingleFile=false",
    "-p:DebugType=None",
    "-p:DebugSymbols=false"
)
Invoke-Native -FilePath "dotnet" -Arguments $publishArguments

$requiredAppFiles = @(
    "SewerStudio.exe",
    "SewerStudio.runtimeconfig.json",
    "coreclr.dll",
    "hostfxr.dll",
    "libvlc\win-x64\libvlc.dll",
    "libvlc\win-x64\libvlccore.dll"
)
foreach ($relativePath in $requiredAppFiles) {
    $fullPath = Join-Path $outputPath $relativePath
    if (-not (Test-Path -LiteralPath $fullPath -PathType Leaf)) {
        throw "Release unvollstaendig, Datei fehlt: $relativePath"
    }
}

Write-Host "[2/4] Kopiere Sidecar-Programm und feste Abhaengigkeitsliste ..."
$sidecarTarget = Join-Path $outputPath "sidecar"
New-Item -ItemType Directory -Path $sidecarTarget -Force | Out-Null

foreach ($file in @(
    "start_sidecar.ps1",
    "setup.ps1",
    "build_engine.ps1",
    "requirements-lock.txt",
    "requirements.txt",
    "pyproject.toml",
    "README.md"
)) {
    Copy-RequiredFile $file
}

Copy-Item -LiteralPath (Join-Path $sidecarSource "sidecar") -Destination $sidecarTarget -Recurse -Force

$modelsIncluded = -not $WithoutModels
if ($modelsIncluded) {
    Write-Host "[3/4] Kopiere produktive KI-Modelle ..."
    $modelsSource = Join-Path $sidecarSource "models"
    $modelsTarget = Join-Path $sidecarTarget "models"
    New-Item -ItemType Directory -Path $modelsTarget -Force | Out-Null

    foreach ($directoryName in @("yolo26m", "grounding_dino_swinb", "sam2.1")) {
        $sourceDirectory = Join-Path $modelsSource $directoryName
        if (-not (Test-Path -LiteralPath $sourceDirectory -PathType Container)) {
            throw "Produktiver Modellordner fehlt: $sourceDirectory"
        }
        Copy-Item -LiteralPath $sourceDirectory -Destination $modelsTarget -Recurse -Force
    }

    $activeSource = Join-Path $modelsSource "active.json"
    if (-not (Test-Path -LiteralPath $activeSource -PathType Leaf)) {
        throw "Modellfreigabe fehlt: $activeSource"
    }

    $active = Get-Content -LiteralPath $activeSource -Raw | ConvertFrom-Json
    $classifierPathText = [string]$active.classifier.weights_path
    if ([string]::IsNullOrWhiteSpace($classifierPathText)) {
        throw "active.json enthaelt keinen classifier.weights_path."
    }

    $classifierSource = if ([System.IO.Path]::IsPathRooted($classifierPathText)) {
        $classifierPathText
    } else {
        Join-Path $modelsSource $classifierPathText
    }
    if (-not (Test-Path -LiteralPath $classifierSource -PathType Leaf)) {
        throw "Freigegebenes Klassifikator-Modell fehlt: $classifierSource"
    }

    $expectedHash = [string]$active.classifier.sha256
    $actualHash = (Get-FileHash -LiteralPath $classifierSource -Algorithm SHA256).Hash.ToLowerInvariant()
    if (-not [string]::IsNullOrWhiteSpace($expectedHash) -and
        -not [string]::Equals($expectedHash, $actualHash, [StringComparison]::OrdinalIgnoreCase)) {
        throw "SHA-256 des freigegebenen Klassifikators stimmt nicht mit active.json ueberein."
    }

    $classifierTargetDirectory = Join-Path $modelsTarget "classifier"
    New-Item -ItemType Directory -Path $classifierTargetDirectory -Force | Out-Null
    $classifierFileName = Split-Path -Leaf $classifierSource
    Copy-Item -LiteralPath $classifierSource -Destination (Join-Path $classifierTargetDirectory $classifierFileName) -Force

    $active.classifier.weights_path = "classifier/$classifierFileName"
    $activeTarget = Join-Path $modelsTarget "active.json"
    Write-Utf8NoBom $activeTarget ($active | ConvertTo-Json -Depth 20)

    $modelChecks = @(
        (Get-ChildItem -LiteralPath (Join-Path $modelsTarget "yolo26m") -File | Where-Object { $_.Extension -in ".pt", ".engine" } | Select-Object -First 1),
        (Get-ChildItem -LiteralPath (Join-Path $modelsTarget "grounding_dino_swinb") -File -Filter "*.pth" | Select-Object -First 1),
        (Get-ChildItem -LiteralPath (Join-Path $modelsTarget "sam2.1") -File | Where-Object { $_.Extension -in ".pt", ".pth" } | Select-Object -First 1),
        (Get-Item -LiteralPath (Join-Path $classifierTargetDirectory $classifierFileName))
    )
    if ($modelChecks.Count -ne 4 -or $modelChecks -contains $null) {
        throw "Mindestens ein produktives KI-Modell fehlt im Release."
    }
} else {
    Write-Host "[3/4] Modelle auf Wunsch ausgelassen."
}

Write-Host "[4/4] Schreibe Installationshinweise und Manifest ..."
$installation = @"
SewerStudio 4.5 - Installation

1. Fuer die normale App SewerStudio.exe starten. .NET ist im Paket enthalten.
2. Fuer die lokale KI muss Python 3.10 oder neuer installiert sein.
3. Einmal Install-Sidecar.ps1 ausfuehren. Das Skript erstellt sidecar\.venv
   und installiert exakt die Versionen aus requirements-lock.txt.
4. Danach SewerStudio erneut starten. Der Sidecar wird automatisch erkannt.

Hinweis: Die Python-Installation benoetigt beim ersten Mal Internetzugang.
"@
Write-Utf8NoBom (Join-Path $outputPath "INSTALLATION.txt") $installation

$sidecarInstaller = @'
$ErrorActionPreference = "Stop"
$setup = Join-Path $PSScriptRoot "sidecar\setup.ps1"
& powershell.exe -NoProfile -ExecutionPolicy Bypass -File $setup
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
Write-Host "Sidecar-Installation abgeschlossen." -ForegroundColor Green
'@
Write-Utf8NoBom (Join-Path $outputPath "Install-Sidecar.ps1") $sidecarInstaller

$commit = (& git -C $repoRoot rev-parse HEAD 2>$null | Select-Object -First 1)
$sourceDirty = -not [string]::IsNullOrWhiteSpace(
    [string]((& git -C $repoRoot status --porcelain 2>$null) -join "`n"))
$manifest = [ordered]@{
    product = "SewerStudio"
    version = "4.5.0"
    runtime = "win-x64"
    self_contained = $true
    sidecar_included = $true
    models_included = $modelsIncluded
    source_commit = [string]$commit
    source_dirty = $sourceDirty
    created_utc = [DateTime]::UtcNow.ToString("o")
}
Write-Utf8NoBom (Join-Path $outputPath "release-manifest.json") ($manifest | ConvertTo-Json)

Write-Host "Release bereit: $outputPath" -ForegroundColor Green
Write-Output $outputPath
