<#
.SYNOPSIS
    Models und Data Classes für AuswertungPro
.DESCRIPTION
    Zentrale Datenstrukturen: HaltungRecord, FieldMeta, Project
#>

# ========== Konstanten ==========
$script:AppVersion = "2.1.0"
$script:FieldColumnOrder = @(
    'NR', 'Haltungsname', 'Strasse', 'Rohrmaterial', 'DN_mm', 'Nutzungsart',
    'Haltungslaenge_m', 'Fliessrichtung', 'Primaere_Schaeden', 'Zustandsklasse',
    'VSA_Zustandsnote_D',
    'Pruefungsresultat', 'Sanieren_JaNein', 'Empfohlene_Sanierungsmassnahmen', 'Kosten',
    'Eigentuemer', 'Bemerkungen', 'Link', 'Renovierung_Inliner_Stk', 'Renovierung_Inliner_m',
    'Anschluesse_verpressen', 'Reparatur_Manschette', 'Reparatur_Kurzliner', 'Erneuerung_Neubau_m',
    'Offen_abgeschlossen', 'Datum_Jahr'
)

$script:FieldLabels = @{
    'NR' = 'NR.'
    'Haltungsname' = 'Haltungsname (ID)'
    'Strasse' = 'Strasse'
    'Rohrmaterial' = 'Rohrmaterial'
    'DN_mm' = 'DN mm'
    'Nutzungsart' = 'Nutzungsart'
    'Haltungslaenge_m' = 'Haltungslänge m'
    'Fliessrichtung' = 'Fliessrichtung'
    'Primaere_Schaeden' = 'Primäre Schäden'
    'Zustandsklasse' = 'Zustandsklasse'
    'VSA_Zustandsnote_D' = 'VSA-Zustandsnote D'
    'Pruefungsresultat' = 'Prüfungsresultat'
    'Sanieren_JaNein' = 'Sanieren Ja/Nein'
    'Empfohlene_Sanierungsmassnahmen' = 'Empfohlene Sanierungsmassnahmen'
    'Kosten' = 'Kosten'
    'Eigentuemer' = 'Eigentümer'
    'Bemerkungen' = 'Bemerkungen'
    'Link' = 'Link'
    'Renovierung_Inliner_Stk' = 'Renovierung Inliner Stk.'
    'Renovierung_Inliner_m' = 'Renovierung Inliner m'
    'Anschluesse_verpressen' = 'Anschlüsse verpressen'
    'Reparatur_Manschette' = 'Reparatur Manschette'
    'Reparatur_Kurzliner' = 'Reparatur Kurzliner'
    'Erneuerung_Neubau_m' = 'Erneuerung Neubau m'
    'Offen_abgeschlossen' = 'offen/abgeschlossen'
    'Datum_Jahr' = 'Datum/Jahr'
}

$script:FieldTypes = @{
    'NR' = 'int'
    'Haltungsname' = 'text'
    'Strasse' = 'text'
    'Rohrmaterial' = 'combo'
    'DN_mm' = 'int'
    'Nutzungsart' = 'combo'
    'Haltungslaenge_m' = 'decimal'
    'Fliessrichtung' = 'combo'
    'Primaere_Schaeden' = 'multiline'
    'Zustandsklasse' = 'combo'
    'VSA_Zustandsnote_D' = 'decimal'
    'Pruefungsresultat' = 'text'
    'Sanieren_JaNein' = 'combo'
    'Empfohlene_Sanierungsmassnahmen' = 'multiline'
    'Kosten' = 'decimal'
    'Eigentuemer' = 'text'
    'Bemerkungen' = 'multiline'
    'Link' = 'text'
    'Renovierung_Inliner_Stk' = 'int'
    'Renovierung_Inliner_m' = 'decimal'
    'Anschluesse_verpressen' = 'int'
    'Reparatur_Manschette' = 'int'
    'Reparatur_Kurzliner' = 'int'
    'Erneuerung_Neubau_m' = 'decimal'
    'Offen_abgeschlossen' = 'combo'
    'Datum_Jahr' = 'text'
}

$script:ComboBoxItems = @{
    'Rohrmaterial' = @('', 'PVC', 'PE', 'PP', 'GFK', 'Beton', 'Steinzeug', 'Guss', 'Hartpolyethylen')
    'Nutzungsart' = @('', 'Schmutzwasser', 'Regenwasser', 'Mischabwasser')
    'Fliessrichtung' = @('', 'In Fliessrichtung', 'Gegen Fliessrichtung')
    'Zustandsklasse' = @('', '0', '1', '2', '3', '4', '5')
    'Sanieren_JaNein' = @('', 'Ja', 'Nein')
    'Offen_abgeschlossen' = @('', 'offen', 'abgeschlossen')
}

# ========== Class: FieldMetadata ==========
class FieldMetadata {
    [string] $FieldName
    [string] $Source        # "manual" | "xtf" | "xtf405" | "pdf"
    [bool]   $UserEdited
    [datetime] $LastUpdated
    [hashtable] $Conflict   # @{ Source = "pdf"; Value = "2"; Reason = "..." }
    
    FieldMetadata() {
        $this.Source = "manual"
        $this.UserEdited = $false
        $this.LastUpdated = (Get-Date).ToUniversalTime()
        $this.Conflict = $null
    }
}

# ========== Class: HaltungRecord ==========
class HaltungRecord {
    [guid] $Id
    [hashtable] $Fields        # Field values (25 Felder)
    [hashtable] $FieldMeta    # FieldName -> FieldMetadata
    [datetime] $CreatedAt
    [datetime] $ModifiedAt
    
    HaltungRecord() {
        $this.Id = [guid]::NewGuid()
        $this.Fields = @{}
        $this.FieldMeta = @{}
        $this.CreatedAt = (Get-Date).ToUniversalTime()
        $this.ModifiedAt = (Get-Date).ToUniversalTime()
        
        # Initialisiere alle Felder mit leerem String + Metadata
        foreach ($fieldName in $script:FieldColumnOrder) {
            $this.Fields[$fieldName] = ""
            $meta = New-Object FieldMetadata
            $meta.FieldName = $fieldName
            $this.FieldMeta[$fieldName] = $meta
        }
    }
    
    [string] GetFieldValue([string]$fieldName) {
        if ($this.Fields.ContainsKey($fieldName)) {
            return $this.Fields[$fieldName]
        }
        return ""
    }
    
    SetFieldValue([string]$fieldName, [string]$value, [string]$source, [bool]$userEdited) {
        $this.Fields[$fieldName] = $value
        if (-not $this.FieldMeta.ContainsKey($fieldName)) {
            $this.FieldMeta[$fieldName] = New-Object FieldMetadata
            $this.FieldMeta[$fieldName].FieldName = $fieldName
        }
        $this.FieldMeta[$fieldName].Source = $source
        $this.FieldMeta[$fieldName].UserEdited = $userEdited
        $this.FieldMeta[$fieldName].LastUpdated = (Get-Date).ToUniversalTime()
        $this.ModifiedAt = (Get-Date).ToUniversalTime()
    }
}

# ========== Class: Project ==========
class Project {
    [int] $Version = 2
    [string] $Name
    [string] $Description
    [guid] $Id
    [datetime] $CreatedAt
    [datetime] $ModifiedAt
    [string] $AppVersion
    [hashtable] $Metadata       # Zone, Firma, Bearbeiter, etc.
    [System.Collections.Generic.List[HaltungRecord]] $Data
    [System.Collections.Generic.List[hashtable]] $ImportHistory
    [System.Collections.Generic.List[hashtable]] $Conflicts
    [bool] $Dirty
    
    Project() {
        $this.Id = [guid]::NewGuid()
        $this.Name = "Neues Projekt"
        $this.Description = ""
        $this.CreatedAt = (Get-Date).ToUniversalTime()
        $this.ModifiedAt = (Get-Date).ToUniversalTime()
        $this.AppVersion = $script:AppVersion
        $this.Metadata = @{
            Zone = ""
            Gemeinde = ""
            Strasse = ""
            FirmaName = ""
            FirmaAdresse = ""
            FirmaTelefon = ""
            FirmaEmail = ""
            Bearbeiter = ""
            Auftraggeber = ""
            AuftragNr = ""
            InspektionsDatum = ""
        }
        $this.Data = New-Object System.Collections.Generic.List[HaltungRecord]
        $this.ImportHistory = New-Object System.Collections.Generic.List[hashtable]
        $this.Conflicts = New-Object System.Collections.Generic.List[hashtable]
        $this.Dirty = $false
    }
    
    [HaltungRecord] CreateNewRecord() {
        $record = New-Object HaltungRecord
        # Auto-generate NR
        $maxNr = 0
        foreach ($rec in $this.Data) {
            $nr = [int]$rec.GetFieldValue('NR')
            if ($nr -gt $maxNr) { $maxNr = $nr }
        }
        $record.SetFieldValue('NR', ($maxNr + 1).ToString(), "manual", $false)
        return $record
    }
    
    [void] AddRecord([HaltungRecord]$record) {
        $this.Data.Add($record)
        $this.ModifiedAt = (Get-Date).ToUniversalTime()
        $this.Dirty = $true
    }
    
    [void] RemoveRecord([guid]$recordId) {
        $idx = $this.Data.FindIndex([System.Predicate[HaltungRecord]]{ $args[0].Id -eq $recordId })
        if ($idx -ge 0) {
            $this.Data.RemoveAt($idx)
            $this.ModifiedAt = (Get-Date).ToUniversalTime()
            $this.Dirty = $true
        }
    }
    
    [HaltungRecord] GetRecord([guid]$recordId) {
        return $this.Data.Find([System.Predicate[HaltungRecord]]{ $args[0].Id -eq $recordId })
    }
}

# ========== Factory-Funktionen ==========
function New-HaltungRecord {
    return New-Object HaltungRecord
}

function New-Project {
    return New-Object Project
}

function Get-FieldLabel {
    param([string]$fieldName)
    if ($script:FieldLabels.ContainsKey($fieldName)) {
        return $script:FieldLabels[$fieldName]
    }
    return $fieldName
}

function Get-FieldType {
    param([string]$fieldName)
    if ($script:FieldTypes.ContainsKey($fieldName)) {
        return $script:FieldTypes[$fieldName]
    }
    return "text"
}

function Get-ComboBoxItems {
    param([string]$fieldName)
    if ($script:ComboBoxItems.ContainsKey($fieldName)) {
        return $script:ComboBoxItems[$fieldName]
    }
    return @()
}

Write-Host "[Models] Loaded: HaltungRecord, Project, FieldMetadata" -ForegroundColor Green
