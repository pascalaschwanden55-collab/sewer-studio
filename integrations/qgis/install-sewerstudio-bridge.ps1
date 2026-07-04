[CmdletBinding()]
param(
    [string]$ProfileRoot,
    [switch]$AllProfiles,
    # Zentrales Plugin-Archiv: dort landet IMMER eine Kopie (Ordner + versioniertes ZIP).
    [string]$BackupDir = "D:\QGIS_V4.03\AWU_Plugins"
)

$ErrorActionPreference = "Stop"

$source = Join-Path $PSScriptRoot "sewerstudio_bridge"
if (-not (Test-Path -LiteralPath (Join-Path $source "metadata.txt"))) {
    throw "Plugin-Quelle nicht gefunden: $source"
}

function Get-QgisPluginRoots {
    if (-not [string]::IsNullOrWhiteSpace($ProfileRoot)) {
        return @((Join-Path $ProfileRoot "python\plugins"))
    }

    $qgisRoot = Join-Path $env:APPDATA "QGIS"
    if (-not (Test-Path -LiteralPath $qgisRoot)) {
        return @((Join-Path $qgisRoot "QGIS3\profiles\default\python\plugins"))
    }

    $roots = New-Object System.Collections.Generic.List[string]
    Get-ChildItem -LiteralPath $qgisRoot -Directory -Filter "QGIS*" -ErrorAction SilentlyContinue |
        Sort-Object Name -Descending |
        ForEach-Object {
            $profilesRoot = Join-Path $_.FullName "profiles"
            if (-not (Test-Path -LiteralPath $profilesRoot)) {
                return
            }

            Get-ChildItem -LiteralPath $profilesRoot -Directory -ErrorAction SilentlyContinue |
                ForEach-Object {
                    $roots.Add((Join-Path $_.FullName "python\plugins"))
                }
        }

    if ($roots.Count -eq 0) {
        $roots.Add((Join-Path $qgisRoot "QGIS3\profiles\default\python\plugins"))
    }

    return $roots.ToArray()
}

$pluginRoots = @(Get-QgisPluginRoots)
if (-not $AllProfiles -and $pluginRoots.Count -gt 1) {
    $pluginRoots = @($pluginRoots[0])
}

foreach ($pluginRoot in $pluginRoots) {
    New-Item -ItemType Directory -Force -Path $pluginRoot | Out-Null
    $target = Join-Path $pluginRoot "sewerstudio_bridge"

    if (Test-Path -LiteralPath $target) {
        Remove-Item -LiteralPath $target -Recurse -Force
    }

    Copy-Item -LiteralPath $source -Destination $target -Recurse
    Write-Host "SewerStudio Bridge installiert: $target"
}

# ── Sicherung ins zentrale Plugin-Archiv (AWU_Plugins) ─────────────────────────
# Konvention: entpackter Ordner + versioniertes ZIP (sewerstudio_bridge_vX.Y.Z.zip),
# wie bei den uebrigen AWU-Plugins. Fehlt das Laufwerk, nur warnen - nie abbrechen.
$backupParent = Split-Path -Path $BackupDir -Parent
if (-not [string]::IsNullOrWhiteSpace($BackupDir) -and (Test-Path -LiteralPath $backupParent)) {
    New-Item -ItemType Directory -Force -Path $BackupDir | Out-Null

    $backupFolder = Join-Path $BackupDir "sewerstudio_bridge"
    if (Test-Path -LiteralPath $backupFolder) {
        Remove-Item -LiteralPath $backupFolder -Recurse -Force
    }
    Copy-Item -LiteralPath $source -Destination $backupFolder -Recurse

    $versionLine = Get-Content -LiteralPath (Join-Path $source "metadata.txt") |
        Where-Object { $_ -match '^version=' } | Select-Object -First 1
    $version = if ($versionLine) { $versionLine.Substring(8).Trim() } else { "unbekannt" }
    $zipPath = Join-Path $BackupDir "sewerstudio_bridge_v$version.zip"
    Compress-Archive -Path $source -DestinationPath $zipPath -Force

    Write-Host "Sicherung aktualisiert: $backupFolder"
    Write-Host "Sicherung aktualisiert: $zipPath"
}
else {
    Write-Warning "Plugin-Archiv nicht erreichbar (kein Backup): $BackupDir"
}

Write-Host "QGIS neu starten und Plugin 'SewerStudio Bridge' aktivieren."
