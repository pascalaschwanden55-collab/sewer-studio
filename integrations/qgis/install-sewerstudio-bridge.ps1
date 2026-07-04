[CmdletBinding()]
param(
    [string]$ProfileRoot,
    [switch]$AllProfiles
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

Write-Host "QGIS neu starten und Plugin 'SewerStudio Bridge' aktivieren."
