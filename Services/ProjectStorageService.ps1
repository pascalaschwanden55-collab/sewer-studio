<#
.SYNOPSIS
    ProjectStorageService für AuswertungPro - Atomare JSON-Persistierung
.DESCRIPTION
    Speichert/lädt Projekte mit atomaren Writes (temp → replace) und Backups
#>

# ========== Konfiguration ==========
$script:ProjectsRoot = Join-Path $PSScriptRoot "..\Projekte"
$script:BackupsDir = "backups"
$script:KeepBackups = 20

# Ensure directories
@($script:ProjectsRoot) | ForEach-Object {
    if (-not (Test-Path $_)) { New-Item -Path $_ -ItemType Directory -Force | Out-Null }
}

# ========== JSON Serialisierung ==========
function ConvertTo-ProjectJson {
    param([Project] $Project)
    
    $projectData = @{
        Version = $Project.Version
        Id = $Project.Id.ToString()
        Name = $Project.Name
        Description = $Project.Description
        CreatedAt = $Project.CreatedAt.ToString("o")
        ModifiedAt = $Project.ModifiedAt.ToString("o")
        AppVersion = $Project.AppVersion
        Metadata = $Project.Metadata
        Data = @()
        ImportHistory = $Project.ImportHistory
        Conflicts = $Project.Conflicts
    }
    
    foreach ($record in $Project.Data) {
        $recordData = @{
            Id = $record.Id.ToString()
            CreatedAt = $record.CreatedAt.ToString("o")
            ModifiedAt = $record.ModifiedAt.ToString("o")
            Fields = $record.Fields
            FieldMeta = @{}
        }
        
        foreach ($fieldName in $record.FieldMeta.Keys) {
            $meta = $record.FieldMeta[$fieldName]
            $recordData.FieldMeta[$fieldName] = @{
                FieldName = $meta.FieldName
                Source = $meta.Source
                UserEdited = $meta.UserEdited
                LastUpdated = $meta.LastUpdated.ToString("o")
                Conflict = $meta.Conflict
            }
        }
        
        $projectData.Data += $recordData
    }
    
    return $projectData | ConvertTo-Json -Depth 10
}

function ConvertTo-DateTimeSafe {
    param(
        [object] $Value,
        [string] $Context = ""
    )
    
    if ($null -eq $Value) {
        return (Get-Date)
    }
    if ($Value -is [datetime]) {
        return $Value
    }
    if ($Value -is [System.DateTimeOffset]) {
        return $Value.DateTime
    }
    
    $text = [string]$Value
    if ([string]::IsNullOrWhiteSpace($text)) {
        return (Get-Date)
    }
    
    $parsed = [datetime]::MinValue
    $invariant = [System.Globalization.CultureInfo]::InvariantCulture
    $styles = [System.Globalization.DateTimeStyles]::AllowWhiteSpaces -bor `
        [System.Globalization.DateTimeStyles]::AssumeLocal
    
    if ([datetime]::TryParseExact($text, "o", $invariant, [System.Globalization.DateTimeStyles]::RoundtripKind, [ref]$parsed)) {
        return $parsed
    }
    
    $formats = @(
        "s",
        "yyyy-MM-dd HH:mm:ss",
        "yyyy-MM-ddTHH:mm:ss",
        "yyyy-MM-ddTHH:mm:ss.fff",
        "yyyy-MM-ddTHH:mm:ss.fffffff",
        "MM/dd/yyyy HH:mm:ss",
        "M/d/yyyy HH:mm:ss",
        "MM/dd/yyyy H:mm:ss",
        "M/d/yyyy H:mm:ss",
        "MM/dd/yyyy",
        "M/d/yyyy",
        "dd.MM.yyyy HH:mm:ss",
        "d.M.yyyy HH:mm:ss",
        "dd.MM.yyyy",
        "d.M.yyyy"
    )
    
    if ([datetime]::TryParseExact($text, $formats, $invariant, $styles, [ref]$parsed)) {
        return $parsed
    }
    
    if ([datetime]::TryParse($text, [System.Globalization.CultureInfo]::CurrentCulture, $styles, [ref]$parsed)) {
        return $parsed
    }
    if ([datetime]::TryParse($text, $invariant, $styles, [ref]$parsed)) {
        return $parsed
    }
    
    Log-Warn -Message "Datum konnte nicht geparst werden ($Context): $text" -Context "Storage"
    return (Get-Date)
}

function ConvertFrom-ProjectJson {
    param([string] $JsonContent)
    
    $projectData = $JsonContent | ConvertFrom-Json
    $project = New-Object Project
    
    $project.Version = $projectData.Version
    $project.Id = [guid]$projectData.Id
    $project.Name = $projectData.Name
    $project.Description = $projectData.Description
    $project.CreatedAt = ConvertTo-DateTimeSafe -Value $projectData.CreatedAt -Context "Project.CreatedAt"
    $project.ModifiedAt = ConvertTo-DateTimeSafe -Value $projectData.ModifiedAt -Context "Project.ModifiedAt"
    $project.AppVersion = $projectData.AppVersion
    
    # Konvertiere PSCustomObject zu Hashtable fuer Metadata
    if ($projectData.Metadata) {
        $project.Metadata = @{}
        foreach ($prop in $projectData.Metadata.PSObject.Properties) {
            $project.Metadata[$prop.Name] = $prop.Value
        }
    }
    
    # Lade Records
    foreach ($recordData in $projectData.Data) {
        $record = New-Object HaltungRecord
        $record.Id = [guid]$recordData.Id
        $record.CreatedAt = ConvertTo-DateTimeSafe -Value $recordData.CreatedAt -Context "Record.CreatedAt"
        $record.ModifiedAt = ConvertTo-DateTimeSafe -Value $recordData.ModifiedAt -Context "Record.ModifiedAt"
        
        # Lade Fields
        foreach ($fieldName in $recordData.Fields.PSObject.Properties.Name) {
            $record.Fields[$fieldName] = $recordData.Fields.$fieldName
        }
        
        # Lade FieldMeta
        foreach ($fieldName in $recordData.FieldMeta.PSObject.Properties.Name) {
            $metaData = $recordData.FieldMeta.$fieldName
            $meta = New-Object FieldMetadata
            $meta.FieldName = $metaData.FieldName
            $meta.Source = $metaData.Source
            $meta.UserEdited = $metaData.UserEdited
            $meta.LastUpdated = ConvertTo-DateTimeSafe -Value $metaData.LastUpdated -Context "FieldMeta.LastUpdated"
            $meta.Conflict = $metaData.Conflict
            
            $record.FieldMeta[$fieldName] = $meta
        }
        
        $null = $project.Data.Add($record)
    }
    
    # Lade ImportHistory
    if ($projectData.ImportHistory) {
        foreach ($entry in $projectData.ImportHistory) {
            # Konvertiere PSCustomObject zu Hashtable
            $historyEntry = @{}
            if ($entry -is [System.Management.Automation.PSCustomObject]) {
                $entry.PSObject.Properties | ForEach-Object { $historyEntry[$_.Name] = $_.Value }
            } else {
                $historyEntry = $entry
            }
            $null = $project.ImportHistory.Add($historyEntry)
        }
    }
    
    # Lade Conflicts
    if ($projectData.Conflicts) {
        foreach ($conflict in $projectData.Conflicts) {
            # Konvertiere PSCustomObject zu Hashtable
            $conflictEntry = @{}
            if ($conflict -is [System.Management.Automation.PSCustomObject]) {
                $conflict.PSObject.Properties | ForEach-Object { $conflictEntry[$_.Name] = $_.Value }
            } else {
                $conflictEntry = $conflict
            }
            $null = $project.Conflicts.Add($conflictEntry)
        }
    }
    
    $project.Dirty = $false
    return $project
}

# ========== Speichern/Laden ==========
<#
.SYNOPSIS
    Save-Project: Speichert Projekt atomar (temp → replace) mit Backup
.PARAMETER Project
    Projekt-Objekt
.PARAMETER Path
    Ziel-Pfad (optional, sonst aus Project.Id)
#>
function Save-Project {
    param(
        [Project] $Project,
        [string] $Path = ""
    )
    
    try {
        if ([string]::IsNullOrEmpty($Path)) {
            $Path = Join-Path $script:ProjectsRoot "$($Project.Id).haltproj"
        }
        
        # Stelle sicher dass Verzeichnis existiert
        $dir = Split-Path $Path -Parent
        if (-not (Test-Path $dir)) {
            New-Item -Path $dir -ItemType Directory -Force | Out-Null
        }
        
        # Backup erstellen falls Datei existiert
        if (Test-Path $Path) {
            New-ProjectBackup -Path $Path
        }
        
        # Konvertiere zu JSON
        $json = ConvertTo-ProjectJson -Project $Project
        
        # Schreibe atomar: temp → replace
        $tempPath = "$Path.tmp"
        $json | Out-File -FilePath $tempPath -Encoding UTF8 -Force
        
        # Validiere JSON vor replace
        try {
            Get-Content -Path $tempPath -Raw | ConvertFrom-Json | Out-Null
        } catch {
            Remove-Item $tempPath -Force -ErrorAction SilentlyContinue
            throw "JSON-Validierung fehlgeschlagen: $_"
        }
        
        # Atomare Replace
        if (Test-Path $Path) {
            Remove-Item $Path -Force
        }
        Move-Item -Path $tempPath -Destination $Path -Force
        
        $Project.Dirty = $false
        Log-Info -Message "Projekt gespeichert: $Path" -Context "Storage"
        
        return $true
    } catch {
        Log-Error -Message "Fehler beim Speichern von Projekt: $_" -Context "Storage" -Exception $_
        return $false
    }
}

<#
.SYNOPSIS
    Load-Project: Lädt Projekt aus Datei
.PARAMETER Path
    Pfad zur .haltproj Datei
#>
function Load-Project {
    param([string] $Path)
    
    try {
        if (-not (Test-Path $Path)) {
            throw "Datei nicht gefunden: $Path"
        }
        
        $json = Get-Content -Path $Path -Raw -Encoding UTF8
        $projectData = $json | ConvertFrom-Json
        
        # Pruefe ob altes Format (hat "Haltungen" statt "Data")
        if ($projectData.Haltungen -and -not $projectData.Data) {
            Log-Info -Message "Altes Projektformat erkannt - konvertiere..." -Context "Storage"
            $project = ConvertFrom-LegacyProject -LegacyData $projectData -SourcePath $Path
        } else {
            $project = ConvertFrom-ProjectJson -JsonContent $json
        }
        
        Log-Info -Message "Projekt geladen: $Path ($($project.Data.Count) Haltungen)" -Context "Storage"
        return $project
    } catch {
        Log-Error -Message "Fehler beim Laden von Projekt: $_" -Context "Storage" -Exception $_
        return $null
    }
}

# ========== Legacy-Projekt-Konvertierung ==========
function ConvertFrom-LegacyProject {
    param(
        [object] $LegacyData,
        [string] $SourcePath
    )
    
    $project = New-Object Project
    
    # Basis-Daten
    $project.Name = if ($LegacyData.Name) { $LegacyData.Name } else { [System.IO.Path]::GetFileNameWithoutExtension($SourcePath) }
    $project.Version = "2.0"
    $project.AppVersion = "2.1.0"
    
    # Zeitstempel
    if ($LegacyData.Erstellt) {
        try {
            $project.CreatedAt = [datetime]::ParseExact($LegacyData.Erstellt, "yyyy-MM-dd HH:mm", $null)
        } catch {
            $project.CreatedAt = (Get-Date).ToUniversalTime()
        }
    }
    if ($LegacyData.Geaendert) {
        try {
            $project.ModifiedAt = [datetime]::ParseExact($LegacyData.Geaendert, "yyyy-MM-dd HH:mm", $null)
        } catch {
            $project.ModifiedAt = (Get-Date).ToUniversalTime()
        }
    }
    
    # Metadata konvertieren
    if ($LegacyData.Meta) {
        $project.Metadata = @{}
        foreach ($prop in $LegacyData.Meta.PSObject.Properties) {
            $project.Metadata[$prop.Name] = $prop.Value
        }
    }
    
    # Haltungen konvertieren
    foreach ($legacyHaltung in $LegacyData.Haltungen) {
        $record = New-Object HaltungRecord
        
        # RowId als Id verwenden falls vorhanden
        if ($legacyHaltung.RowId) {
            try {
                $record.Id = [guid]$legacyHaltung.RowId
            } catch {
                $record.Id = [guid]::NewGuid()
            }
        }
        
        # Alle Felder uebernehmen
        foreach ($prop in $legacyHaltung.PSObject.Properties) {
            $fieldName = $prop.Name
            $value = $prop.Value
            
            # RowId ueberspringen (ist schon als Id gesetzt)
            if ($fieldName -eq 'RowId') { continue }
            
            if ($value -and $value -ne '') {
                $record.SetFieldValue($fieldName, $value, 'legacy', $false)
            }
        }
        
        $project.AddRecord($record)
    }
    
    $project.Dirty = $true  # Markiere als dirty damit im neuen Format gespeichert wird
    Log-Info -Message "Legacy-Projekt konvertiert: $($project.Name) ($($project.Data.Count) Haltungen)" -Context "Storage"
    
    return $project
}

# ========== Backup-Verwaltung ==========
<#
.SYNOPSIS
    New-ProjectBackup: Erstellt zeitgestempelte Kopie vor Änderung
#>
function New-ProjectBackup {
    param([string] $Path)
    
    try {
        $dir = Split-Path $Path -Parent
        $backupDir = Join-Path $dir $script:BackupsDir
        
        if (-not (Test-Path $backupDir)) {
            New-Item -Path $backupDir -ItemType Directory -Force | Out-Null
        }
        
        $baseName = [System.IO.Path]::GetFileNameWithoutExtension($Path)
        $timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
        $backupPath = Join-Path $backupDir "${baseName}_${timestamp}.haltproj"
        
        Copy-Item -Path $Path -Destination $backupPath -Force
        
        # Cleanup alte Backups
        Get-ChildItem -Path $backupDir -Filter "${baseName}_*.haltproj" -ErrorAction SilentlyContinue |
            Sort-Object -Property CreationTime -Descending |
            Select-Object -Skip $script:KeepBackups |
            Remove-Item -Force -ErrorAction SilentlyContinue
        
        Log-Debug -Message "Backup erstellt: $backupPath" -Context "Storage"
        return $backupPath
    } catch {
        Log-Warn -Message "Fehler beim Erstellen von Backup: $_" -Context "Storage"
        return $null
    }
}

<#
.SYNOPSIS
    Get-ProjectBackups: Listet alle Backups für ein Projekt auf
#>
function Get-ProjectBackups {
    param([string] $ProjectPath)
    
    try {
        $dir = Split-Path $ProjectPath -Parent
        $backupDir = Join-Path $dir $script:BackupsDir
        
        if (-not (Test-Path $backupDir)) {
            return @()
        }
        
        $baseName = [System.IO.Path]::GetFileNameWithoutExtension($ProjectPath)
        return Get-ChildItem -Path $backupDir -Filter "${baseName}_*.haltproj" -ErrorAction SilentlyContinue |
            Sort-Object -Property CreationTime -Descending
    } catch {
        return @()
    }
}

<#
.SYNOPSIS
    Restore-ProjectFromBackup: Stellt Projekt aus Backup wieder her
#>
function Restore-ProjectFromBackup {
    param(
        [string] $BackupPath,
        [string] $TargetPath
    )
    
    try {
        if (-not (Test-Path $BackupPath)) {
            throw "Backup nicht gefunden: $BackupPath"
        }
        
        # Backup des aktuellen Status
        if (Test-Path $TargetPath) {
            New-ProjectBackup -Path $TargetPath
        }
        
        Copy-Item -Path $BackupPath -Destination $TargetPath -Force
        Log-Info -Message "Projekt aus Backup wiederhergestellt: $BackupPath" -Context "Storage"
        return $true
    } catch {
        Log-Error -Message "Fehler beim Wiederherstellen aus Backup: $_" -Context "Storage" -Exception $_
        return $false
    }
}

# ========== Projekt-Browsing ==========
function Get-Projects {
    [CmdletBinding()]
    param()
    
    if (-not (Test-Path $script:ProjectsRoot)) {
        return @()
    }
    
    $projects = @()
    foreach ($file in Get-ChildItem -Path $script:ProjectsRoot -Filter "*.haltproj" -ErrorAction SilentlyContinue) {
        try {
            $project = Load-Project -Path $file.FullName
            if ($project) {
                $projects += @{
                    Path = $file.FullName
                    Id = $project.Id
                    Name = $project.Name
                    Records = $project.Data.Count
                    Modified = $project.ModifiedAt
                }
            }
        } catch {
            # Ignoriere fehlerhafte Projekte
        }
    }
    
    return $projects | Sort-Object -Property Modified -Descending
}

function Get-ProjectPath {
    [CmdletBinding()]
    param([string]$ProjectId)
    
    return Join-Path $script:ProjectsRoot "$ProjectId.haltproj"
}

# ========== Projekt-Import (Merge) ==========
<#
.SYNOPSIS
    Import-ProjectData: Importiert Haltungen aus einem anderen Projekt
.DESCRIPTION
    Merged Haltungen aus einer .haltproj Datei ins aktuelle Projekt.
    Bestehende Haltungen werden nach Merge-Regeln aktualisiert.
.PARAMETER TargetProject
    Das Ziel-Projekt, in das importiert wird
.PARAMETER SourcePath
    Pfad zur .haltproj Datei, aus der importiert wird
.PARAMETER MergeMode
    'merge' = Bestehende Haltungen aktualisieren
    'append' = Nur neue Haltungen hinzufuegen
    'replace' = Alle Haltungen ersetzen
#>
function Import-ProjectData {
    param(
        [Project] $TargetProject,
        [string] $SourcePath,
        [ValidateSet('merge', 'append', 'replace')]
        [string] $MergeMode = 'merge'
    )
    
    try {
        if (-not (Test-Path $SourcePath)) {
            throw "Quelldatei nicht gefunden: $SourcePath"
        }
        
        $sourceProject = Load-Project -Path $SourcePath
        if (-not $sourceProject) {
            throw "Konnte Quell-Projekt nicht laden"
        }
        
        $importedCount = 0
        $mergedCount = 0
        $skippedCount = 0
        
        if ($MergeMode -eq 'replace') {
            # Alle bestehenden Haltungen entfernen
            $TargetProject.Data.Clear()
        }
        
        foreach ($sourceRecord in $sourceProject.Data) {
            # Suche existierende Haltung nach Bezeichnung
            $haltungsBez = $sourceRecord.GetFieldValue('Haltungsbezeichnung')
            $existingRecord = $null
            
            if ($haltungsBez) {
                $existingRecord = $TargetProject.Data | Where-Object {
                    $_.GetFieldValue('Haltungsbezeichnung') -eq $haltungsBez
                } | Select-Object -First 1
            }
            
            if ($existingRecord) {
                if ($MergeMode -eq 'append') {
                    # Bei append: Bestehende ueberspringen
                    $skippedCount++
                    continue
                }
                
                # Merge: Felder aktualisieren (nur wenn nicht manuell bearbeitet)
                foreach ($fieldName in $sourceRecord.Fields.Keys) {
                    $newValue = $sourceRecord.GetFieldValue($fieldName)
                    if ($newValue) {
                        $sourceMeta = $sourceRecord.FieldMeta[$fieldName]
                        $sourceType = if ($sourceMeta) { $sourceMeta.Source } else { 'project' }
                        
                        Merge-Field -Record $existingRecord -FieldName $fieldName -NewValue $newValue -Source $sourceType
                    }
                }
                $mergedCount++
            } else {
                # Neue Haltung hinzufuegen
                $newRecord = $TargetProject.CreateNewRecord()
                
                foreach ($fieldName in $sourceRecord.Fields.Keys) {
                    $value = $sourceRecord.GetFieldValue($fieldName)
                    if ($value) {
                        $sourceMeta = $sourceRecord.FieldMeta[$fieldName]
                        $sourceType = if ($sourceMeta) { $sourceMeta.Source } else { 'project' }
                        $userEdited = if ($sourceMeta) { $sourceMeta.UserEdited } else { $false }
                        
                        $newRecord.SetFieldValue($fieldName, $value, $sourceType, $userEdited)
                    }
                }
                
                $TargetProject.AddRecord($newRecord)
                $importedCount++
            }
        }
        
        # Metadaten mergen (nur leere Felder fuellen)
        if ($sourceProject.Metadata) {
            foreach ($key in $sourceProject.Metadata.Keys) {
                $sourceValue = $sourceProject.Metadata[$key]
                if ($sourceValue -and -not $TargetProject.Metadata.ContainsKey($key)) {
                    $TargetProject.Metadata[$key] = $sourceValue
                } elseif ($sourceValue -and [string]::IsNullOrWhiteSpace($TargetProject.Metadata[$key])) {
                    $TargetProject.Metadata[$key] = $sourceValue
                }
            }
        }
        
        # Import-History aktualisieren
        $null = $TargetProject.ImportHistory.Add(@{
            Timestamp = (Get-Date).ToString('yyyy-MM-dd HH:mm:ss')
            Source = 'project'
            File = $SourcePath
            SourceProjectName = $sourceProject.Name
            RecordsImported = $importedCount
            RecordsMerged = $mergedCount
            RecordsSkipped = $skippedCount
            MergeMode = $MergeMode
        })
        
        $TargetProject.Dirty = $true
        $TargetProject.ModifiedAt = (Get-Date).ToUniversalTime()
        
        Log-Info -Message "Projekt-Import: $importedCount neu, $mergedCount gemerged, $skippedCount uebersprungen" -Context "Storage"
        
        return @{
            Success = $true
            Imported = $importedCount
            Merged = $mergedCount
            Skipped = $skippedCount
            SourceName = $sourceProject.Name
        }
    } catch {
        Log-Error -Message "Fehler beim Projekt-Import: $_" -Context "Storage"
        return @{
            Success = $false
            Error = $_.ToString()
        }
    }
}

<#
.SYNOPSIS
    Export-ProjectToFile: Exportiert Projekt in verschiedene Formate
#>
function Export-ProjectToFile {
    param(
        [Project] $Project,
        [string] $Path,
        [ValidateSet('json', 'csv', 'xml')]
        [string] $Format = 'json'
    )
    
    try {
        switch ($Format) {
            'json' {
                $json = ConvertTo-ProjectJson -Project $Project
                $json | Out-File -FilePath $Path -Encoding UTF8 -Force
            }
            'csv' {
                $csvData = @()
                foreach ($record in $Project.Data) {
                    $row = [ordered]@{}
                    foreach ($fieldName in $record.Fields.Keys) {
                        $row[$fieldName] = $record.GetFieldValue($fieldName)
                    }
                    $csvData += [PSCustomObject]$row
                }
                $csvData | Export-Csv -Path $Path -Encoding UTF8 -NoTypeInformation
            }
            'xml' {
                $xmlData = @{
                    Project = @{
                        Name = $Project.Name
                        Id = $Project.Id.ToString()
                        Haltungen = @()
                    }
                }
                foreach ($record in $Project.Data) {
                    $haltung = @{}
                    foreach ($fieldName in $record.Fields.Keys) {
                        $haltung[$fieldName] = $record.GetFieldValue($fieldName)
                    }
                    $xmlData.Project.Haltungen += $haltung
                }
                $xmlData | ConvertTo-Xml -Depth 5 | Out-File -FilePath $Path -Encoding UTF8
            }
        }
        
        Log-Info -Message "Projekt exportiert nach: $Path ($Format)" -Context "Storage"
        return $true
    } catch {
        Log-Error -Message "Export-Fehler: $_" -Context "Storage"
        return $false
    }
}

Write-Host "[ProjectStorageService] Loaded - Root: $script:ProjectsRoot" -ForegroundColor Green
