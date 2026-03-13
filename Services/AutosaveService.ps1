<#
.SYNOPSIS
    AutosaveService für AuswertungPro - Autosave und Crash-Recovery
.DESCRIPTION
    - Autosave alle 3 Minuten wenn Dirty=true
    - Backup vor jedem manuellen Save
    - Crash-Recovery beim Start
#>

# ========== Konfiguration ==========
$script:AutosaveIntervalSeconds = 180  # 3 Minuten
$script:AutosaveTimer = $null
$script:AutosaveProject = $null
$script:AutosaveSaveCallback = $null
$script:LastAutosavePath = ""
$script:CrashRecoveryFile = ""  # Wird gesetzt beim Save

# ========== Autosave-Manager ==========
<#
.SYNOPSIS
    Start-Autosave: Startet Autosave-Timer (WPF-basiert)
.PARAMETER Project
    Projekt-Objekt
.PARAMETER SaveCallback
    Scriptblock zum Speichern: { param($project) Save-Project $project }
#>
function Start-Autosave {
    param(
        [Project] $Project,
        [scriptblock] $SaveCallback
    )
    
    try {
        if ($script:AutosaveTimer) {
            Stop-Autosave
        }
        
        # Speichere Callback im Script-Scope (nicht Project - das wird dynamisch geholt)
        $script:AutosaveSaveCallback = $SaveCallback
        
        # WPF DispatcherTimer
        $script:AutosaveTimer = New-Object System.Windows.Threading.DispatcherTimer
        $script:AutosaveTimer.Interval = [System.TimeSpan]::FromSeconds($script:AutosaveIntervalSeconds)
        
        $script:AutosaveTimer.Add_Tick({
            # Hole aktuelles Projekt dynamisch aus dem globalen Scope
            $currentProject = $null
            $currentPath = ""
            
            try {
                $currentProject = $script:CurrentProject
                $currentPath = $script:CurrentProjectPath
            } catch {
                # Variablen nicht verfuegbar
                return
            }
            
            if ($currentProject -and $currentProject.Dirty -and $currentPath) {
                try {
                    Save-Project -Project $currentProject -Path $currentPath
                    $currentProject.Dirty = $false
                    Log-Info -Message "Autosave erfolgreich ($($currentProject.Data.Count) Haltungen)" -Context "Autosave"
                } catch {
                    Log-Error -Message "Autosave fehlgeschlagen: $_" -Context "Autosave" -Exception $_
                }
            }
        })
        
        $script:AutosaveTimer.Start()
        Log-Info -Message "Autosave gestartet (Intervall: $script:AutosaveIntervalSeconds Sekunden)" -Context "Autosave"
    } catch {
        Log-Error -Message "Fehler beim Starten von Autosave: $_" -Context "Autosave" -Exception $_
    }
}

function Stop-Autosave {
    if ($script:AutosaveTimer) {
        try {
            $script:AutosaveTimer.Stop()
            $script:AutosaveTimer = $null
            Log-Info -Message "Autosave gestoppt" -Context "Autosave"
        } catch {
            Log-Warn -Message "Fehler beim Stoppen von Autosave: $_" -Context "Autosave"
        }
    }
}

# ========== Crash Recovery ==========
<#
.SYNOPSIS
    Get-CrashRecoveryProject: Prüft auf Crash-Recovery-Daten
.RETURNS
    PSCustomObject mit Info über vorhandenes Crash-Recovery oder $null
#>
function Get-CrashRecoveryProject {
    param([string] $ProjectPath)
    
    try {
        $dir = Split-Path $ProjectPath -Parent
        $crashFile = "$ProjectPath.crash"
        
        if (-not (Test-Path $crashFile)) {
            return $null
        }
        
        $crashInfo = Get-Item $crashFile
        
        return @{
            Path = $crashFile
            ProjectPath = $ProjectPath
            CreatedAt = $crashInfo.CreationTime
            SizeKB = [Math]::Round($crashInfo.Length / 1KB)
            AgeMinutes = [Math]::Round(((Get-Date) - $crashInfo.CreationTime).TotalMinutes)
        }
    } catch {
        return $null
    }
}

<#
.SYNOPSIS
    Set-CrashRecoveryMarker: Setzt Crash-Recovery-Marker (wird beim erfolgreichen Save gelöscht)
#>
function Set-CrashRecoveryMarker {
    param(
        [string] $ProjectPath,
        [Project] $Project
    )
    
    try {
        $crashFile = "$ProjectPath.crash"
        
        # Konvertiere zu JSON
        $json = ConvertTo-ProjectJson -Project $Project
        $json | Out-File -FilePath $crashFile -Encoding UTF8 -Force
        
        Log-Debug -Message "Crash-Recovery-Marker gesetzt: $crashFile" -Context "Autosave"
    } catch {
        Log-Warn -Message "Fehler beim Setzen von Crash-Recovery-Marker: $_" -Context "Autosave"
    }
}

<#
.SYNOPSIS
    Clear-CrashRecoveryMarker: Löscht Crash-Recovery-Marker (nach erfolgreichem Save)
#>
function Clear-CrashRecoveryMarker {
    param([string] $ProjectPath)
    
    try {
        $crashFile = "$ProjectPath.crash"
        if (Test-Path $crashFile) {
            Remove-Item $crashFile -Force
            Log-Debug -Message "Crash-Recovery-Marker gelöscht" -Context "Autosave"
        }
    } catch {
        Log-Warn -Message "Fehler beim Löschen von Crash-Recovery-Marker: $_" -Context "Autosave"
    }
}

<#
.SYNOPSIS
    Recover-ProjectFromCrash: Stellt Projekt aus Crash-Recovery wieder her
#>
function Recover-ProjectFromCrash {
    param(
        [string] $ProjectPath,
        [string] $TargetPath = $ProjectPath
    )
    
    try {
        $crashFile = "$ProjectPath.crash"
        
        if (-not (Test-Path $crashFile)) {
            return $null
        }
        
        $project = Load-Project -Path $crashFile
        if ($project) {
            # Backup des beschädigten Originals
            if (Test-Path $ProjectPath) {
                $brokenBackup = "$ProjectPath.broken_$(Get-Date -Format 'yyyyMMdd_HHmmss')"
                Copy-Item -Path $ProjectPath -Destination $brokenBackup -Force
                Log-Warn -Message "Beschädigtes Original gespeichert als: $brokenBackup" -Context "CrashRecovery"
            }
            
            # Speichere wiederhergestelltes Projekt
            Save-Project -Project $project -Path $TargetPath
            
            # Lösche Crash-Recovery-Marker
            Remove-Item $crashFile -Force -ErrorAction SilentlyContinue
            
            Log-Info -Message "Projekt aus Crash-Recovery wiederhergestellt" -Context "CrashRecovery"
            return $project
        }
    } catch {
        Log-Error -Message "Fehler beim Recovery aus Crash: $_" -Context "CrashRecovery" -Exception $_
    }
    
    return $null
}

# ========== Hilfsfunktionen ==========
function Test-CrashRecoveryAvailable {
    param([string] $ProjectPath)
    
    $crashFile = "$ProjectPath.crash"
    return (Test-Path $crashFile)
}

function Show-CrashRecoveryDialog {
    param([string] $ProjectPath)
    
    $crashInfo = Get-CrashRecoveryProject -ProjectPath $ProjectPath
    
    if (-not $crashInfo) {
        return $false  # Kein Crash-Recovery vorhanden
    }
    
    $message = @"
Beim letzten Arbeitsgang wurde möglicherweise ein Fehler nicht ordnungsgemäß gespeichert.

Crash-Recovery-Datei gefunden:
- Erstellt: $($crashInfo.CreatedAt)
- Alter: $($crashInfo.AgeMinutes) Minuten
- Größe: $($crashInfo.SizeKB) KB

Möchten Sie das Projekt aus der Crash-Recovery wiederherstellen?

Ja = Wiederherstellen
Nein = Verwerfen
Abbrechen = Projekt normalerweise laden
"@
    
    $result = [System.Windows.MessageBox]::Show(
        $message,
        "Crash-Recovery erkannt",
        [System.Windows.MessageBoxButton]::YesNoCancel,
        [System.Windows.MessageBoxImage]::Warning
    )
    
    return $result -eq [System.Windows.MessageBoxResult]::Yes
}

Write-Host "[AutosaveService] Loaded - Intervall: $script:AutosaveIntervalSeconds Sekunden" -ForegroundColor Green
