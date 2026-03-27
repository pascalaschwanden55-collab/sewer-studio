<#
.SYNOPSIS
    Haltungs-Auswertungs-Tool Pro mit Projektmanager
.DESCRIPTION
    Tool zur Erfassung und Auswertung von Haltungsdaten aus TV-Protokoll-PDFs.
    Inklusive Projektmanager zum Speichern, Laden und Verwalten von Projekten.
#>

[CmdletBinding()]
param()

Add-Type -AssemblyName PresentationFramework
Add-Type -AssemblyName PresentationCore
Add-Type -AssemblyName WindowsBase
Add-Type -AssemblyName System.Windows.Forms

# ========== Konfiguration ==========
$script:AppVersion = "2.1.0"
$script:ProjectFolder = Join-Path $PSScriptRoot "Projekte"
$script:RohdatenFolder = Join-Path $PSScriptRoot "Rohdaten"
$script:LogFolder = Join-Path $PSScriptRoot "logs"
$script:CurrentProject = $null
$script:CurrentProjectPath = ""
$script:RowControls = @{}  # Dictionary: RowId -> FieldControls hashtable
$script:RowFieldSources = @{}  # Dictionary: RowId -> @{ FieldName = "XTF"|"SIA405"|"PDF"|"Manual" }
$script:RowUserEdited = @{}  # Dictionary: RowId -> @{ FieldName = $true/$false }
$script:RowCounter = 0
$script:IsDirty = $false
$script:ImportConflicts = @()  # Konfliktprotokoll
$script:HasXtfBasisImport = $false  # Flag: Wurde Basis-XTF importiert?
$script:IsProgrammaticChange = $false  # Flag: Programmgesteuerte Änderung (nicht als UserEdit tracken)

# Ordner erstellen falls nicht vorhanden
if (-not (Test-Path $script:ProjectFolder)) {
    New-Item -Path $script:ProjectFolder -ItemType Directory -Force | Out-Null
}
if (-not (Test-Path $script:LogFolder)) {
    New-Item -Path $script:LogFolder -ItemType Directory -Force | Out-Null
}

# Feld-Definition (Reihenfolge wie im PDF "Anordnung Menü")
$script:FieldDefinitions = @(
    @{ Name = "NR"; Label = "NR."; Type = "Text"; Width = 60 }
    @{ Name = "Haltungsname"; Label = "Haltungsname (ID)"; Type = "Text"; Width = 150 }
    @{ Name = "Strasse"; Label = "Strasse"; Type = "Text"; Width = 200 }
    @{ Name = "Rohrmaterial"; Label = "Rohrmaterial"; Type = "ComboBox"; Items = @("", "PVC", "PE", "PP", "GFK", "Beton", "Steinzeug", "Guss", "Hartpolyethylen"); Width = 150 }
    @{ Name = "DN_mm"; Label = "DN mm"; Type = "Text"; Width = 80 }
    @{ Name = "Nutzungsart"; Label = "Nutzungsart"; Type = "ComboBox"; Items = @("", "Schmutzwasser", "Regenwasser", "Mischabwasser"); Width = 150 }
    @{ Name = "Haltungslaenge_m"; Label = "Haltungslänge m"; Type = "Text"; Width = 100 }
    @{ Name = "Fliessrichtung"; Label = "Fliessrichtung"; Type = "ComboBox"; Items = @("", "In Fliessrichtung", "Gegen Fliessrichtung"); Width = 150 }
    @{ Name = "Primaere_Schaeden"; Label = "Primäre Schäden"; Type = "MultiLine"; Width = 300; Height = 80 }
    @{ Name = "Pruefungsresultat"; Label = "Prüfungsresultat"; Type = "Text"; Width = 150 }
    @{ Name = "Sanieren_JaNein"; Label = "Sanieren Ja/Nein"; Type = "ComboBox"; Items = @("", "Ja", "Nein"); Width = 80 }
    @{ Name = "Empfohlene_Sanierungsmassnahmen"; Label = "Empfohlene Sanierungsmassnahmen"; Type = "MultiLine"; Width = 300; Height = 60 }
    @{ Name = "Kosten"; Label = "Kosten"; Type = "Text"; Width = 100 }
    @{ Name = "Eigentuemer"; Label = "Eigentümer"; Type = "Text"; Width = 150 }
    @{ Name = "Bemerkungen"; Label = "Bemerkungen"; Type = "MultiLine"; Width = 300; Height = 60 }
    @{ Name = "Link"; Label = "Link"; Type = "Text"; Width = 300 }
    @{ Name = "Renovierung_Inliner_Stk"; Label = "Renovierung Inliner Stk."; Type = "Text"; Width = 80 }
    @{ Name = "Renovierung_Inliner_m"; Label = "Renovierung Inliner m"; Type = "Text"; Width = 80 }
    @{ Name = "Anschluesse_verpressen"; Label = "Anschlüsse verpressen"; Type = "Text"; Width = 80 }
)

# ========== Projekt-Funktionen ==========
function New-Project {
    return @{
        Version = 2
        Name = "Neues Projekt"
        Erstellt = (Get-Date).ToString("yyyy-MM-dd HH:mm")
        Geaendert = (Get-Date).ToString("yyyy-MM-dd HH:mm")
        Meta = @{
            Zone = ""
            FirmaName = ""
            FirmaAdresse = ""
            FirmaTelefon = ""
            FirmaEmail = ""
            Bearbeiter = ""
        }
        Haltungen = @()
    }
}

function Save-Project {
    param([string]$Path)
    
    # Daten aus UI sammeln
    $haltungen = @()
    foreach ($rowId in $script:RowControls.Keys) {
        $controls = $script:RowControls[$rowId]
        $rowData = @{ RowId = $rowId }
        
        foreach ($fieldDef in $script:FieldDefinitions) {
            $control = $controls[$fieldDef.Name]
            if ($control -is [System.Windows.Controls.TextBox]) {
                $rowData[$fieldDef.Name] = $control.Text
            }
            elseif ($control -is [System.Windows.Controls.ComboBox]) {
                $rowData[$fieldDef.Name] = $control.Text
            }
        }
        $haltungen += $rowData
    }
    
    # Projekt-Metadaten aktualisieren
    if ($null -eq $script:CurrentProject) {
        $script:CurrentProject = New-Project
    }
    
    $script:CurrentProject.Geaendert = (Get-Date).ToString("yyyy-MM-dd HH:mm")
    $script:CurrentProject.Name = $script:txtProjectName.Text
    $script:CurrentProject.Meta.Zone = $script:txtZone.Text
    $script:CurrentProject.Meta.FirmaName = $script:txtFirma.Text
    $script:CurrentProject.Meta.Bearbeiter = $script:txtBearbeiter.Text
    $script:CurrentProject.Haltungen = $haltungen
    
    $script:CurrentProject.ImportConflicts = $script:ImportConflicts

    $targetFolder = $Path
    if ($Path -match '\.haltproj$' -or (Test-Path $Path -PathType Leaf)) {
        $baseName = [System.IO.Path]::GetFileNameWithoutExtension($Path)
        $parent = Split-Path $Path -Parent
        $targetFolder = Join-Path $parent $baseName
    }

    if (-not (Test-Path $targetFolder)) {
        New-Item -Path $targetFolder -ItemType Directory -Force | Out-Null
    }

    $projectPath = Join-Path $targetFolder "project.json"
    $haltungenPath = Join-Path $targetFolder "haltungen.json"

    $projectJson = $script:CurrentProject | ConvertTo-Json -Depth 10
    $haltungenJson = $haltungen | ConvertTo-Json -Depth 10
    Set-Content -Path $projectPath -Value $projectJson -Encoding UTF8
    Set-Content -Path $haltungenPath -Value $haltungenJson -Encoding UTF8

    $script:CurrentProjectPath = $targetFolder
    $script:IsDirty = $false
    Update-WindowTitle
}

function Open-Project {
    param([string]$Path)

    $project = $null
    $haltungen = $null
    $isFolder = Test-Path $Path -PathType Container

    if ($isFolder) {
        $projectPath = Join-Path $Path "project.json"
        $haltungenPath = Join-Path $Path "haltungen.json"
        if (-not (Test-Path $projectPath)) {
            throw "project.json fehlt in: $Path"
        }
        $json = Get-Content -Path $projectPath -Raw -Encoding UTF8
        $project = $json | ConvertFrom-Json
        if (Test-Path $haltungenPath) {
            $haltungenJson = Get-Content -Path $haltungenPath -Raw -Encoding UTF8
            $haltungen = $haltungenJson | ConvertFrom-Json
        }
        else {
            $haltungen = @()
        }
    }
    else {
        # Legacy .haltproj
        $json = Get-Content -Path $Path -Raw -Encoding UTF8
        $project = $json | ConvertFrom-Json
        $haltungen = $project.Haltungen
    }
    
    $script:CurrentProject = @{
        Version = $project.Version
        Name = $project.Name
        Erstellt = $project.Erstellt
        Geaendert = $project.Geaendert
        Meta = @{
            Zone = $project.Meta.Zone
            FirmaName = $project.Meta.FirmaName
            FirmaAdresse = $project.Meta.FirmaAdresse
            FirmaTelefon = $project.Meta.FirmaTelefon
            FirmaEmail = $project.Meta.FirmaEmail
            Bearbeiter = $project.Meta.Bearbeiter
        }
        Haltungen = @()
        ImportConflicts = if ($project.ImportConflicts) { $project.ImportConflicts } else { @() }
    }
    
    # UI leeren
    $script:rowContainer.Children.Clear()
    $script:RowControls.Clear()
    $script:RowCounter = 0
    
    # Metadaten in UI laden
    $script:txtProjectName.Text = $project.Name
    $script:txtZone.Text = $project.Meta.Zone
    $script:txtFirma.Text = $project.Meta.FirmaName
    $script:txtBearbeiter.Text = $project.Meta.Bearbeiter
    
    # Haltungen laden
    $script:IsProgrammaticChange = $true
    try {
    foreach ($h in $haltungen) {
        $newRow = Add-HaltungRow
        $controls = $script:RowControls[$newRow.RowId]
            
            foreach ($fieldDef in $script:FieldDefinitions) {
                $value = $h.($fieldDef.Name)
                if ($value) {
                    $control = $controls[$fieldDef.Name]
                    if ($control -is [System.Windows.Controls.TextBox]) {
                        $control.Text = $value
                    }
                    elseif ($control -is [System.Windows.Controls.ComboBox]) {
                        $control.Text = $value
                    }
                }
            }
            
            # Titel aktualisieren
            if ($h.Haltungsname) {
                $newRow.TitleLabel.Text = "Haltung: $($h.Haltungsname)"
            }
        }
    }
    finally {
        $script:IsProgrammaticChange = $false
    }
    
    $script:CurrentProjectPath = $Path
    $script:ImportConflicts = if ($project.ImportConflicts) { $project.ImportConflicts } else { @() }
    $script:IsDirty = $false
    Update-WindowTitle
    Update-ProjectList
}

function Update-WindowTitle {
    $title = "Haltungs-Auswertung Pro"
    if ($script:CurrentProjectPath) {
        $name = if (Test-Path $script:CurrentProjectPath -PathType Container) {
            Split-Path $script:CurrentProjectPath -Leaf
        }
        else {
            [System.IO.Path]::GetFileNameWithoutExtension($script:CurrentProjectPath)
        }
        $title = "$name - $title"
    }
    if ($script:IsDirty) {
        $title = "* $title"
    }
    $script:window.Title = $title
}

function Update-ProjectList {
    $script:projectList.Items.Clear()
    
    $folders = Get-ChildItem -Path $script:ProjectFolder -Directory -ErrorAction SilentlyContinue
    foreach ($folder in $folders) {
        $projectJson = Join-Path $folder.FullName "project.json"
        if (-not (Test-Path $projectJson)) { continue }
        $item = New-Object System.Windows.Controls.ListBoxItem
        $item.Content = $folder.Name
        $item.Tag = $folder.FullName
        $item.Padding = [System.Windows.Thickness]::new(8, 6, 8, 6)
        $script:projectList.Items.Add($item) | Out-Null
    }

    $legacyFiles = Get-ChildItem -Path $script:ProjectFolder -Filter "*.haltproj" -ErrorAction SilentlyContinue
    foreach ($file in $legacyFiles) {
        $item = New-Object System.Windows.Controls.ListBoxItem
        $item.Content = "$($file.BaseName) (legacy)"
        $item.Tag = $file.FullName
        $item.Padding = [System.Windows.Thickness]::new(8, 6, 8, 6)
        $script:projectList.Items.Add($item) | Out-Null
    }
}

# ========== PDF-Extraktion ==========
function Get-PdfToTextPath {
    $cmd = Get-Command -Name pdftotext -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }
    
    $root = Join-Path $env:LOCALAPPDATA "Microsoft\WinGet\Packages"
    $match = Get-ChildItem -Path $root -Recurse -Filter pdftotext.exe -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($match) { return $match.FullName }
    
    $paths = @(
        "C:\Program Files\poppler\bin\pdftotext.exe",
        "C:\Program Files (x86)\poppler\bin\pdftotext.exe",
        "$env:USERPROFILE\scoop\apps\poppler\current\bin\pdftotext.exe"
    )
    foreach ($p in $paths) {
        if (Test-Path $p) { return $p }
    }
    
    return $null
}

function Convert-PdfToText {
    param([string]$PdfPath)
    
    $pdftotext = Get-PdfToTextPath
    if (-not $pdftotext) {
        throw "pdftotext.exe nicht gefunden. Bitte Poppler installieren (winget install poppler)."
    }
    
    $txtPath = Join-Path $env:TEMP ("pdf_extract_{0}.txt" -f [Guid]::NewGuid().ToString("N"))
    & $pdftotext -enc UTF-8 -layout $PdfPath $txtPath
    
    if (-not (Test-Path $txtPath)) {
        throw "PDF-Textextraktion fehlgeschlagen."
    }
    
    $content = Get-Content -Path $txtPath -Raw -Encoding UTF8
    Remove-Item -Path $txtPath -Force -ErrorAction SilentlyContinue
    return $content
}

function ConvertFrom-PdfProtokoll {
    param([string]$PdfPath)
    
    $text = Convert-PdfToText -PdfPath $PdfPath
    $lines = $text -split "`r?`n"
    
    $haltungen = @()
    $currentHaltung = $null
    $currentBlock = @()
    
    foreach ($line in $lines) {
        # Suche nach "Haltung" ODER "Leitung" am Zeilenanfang
        $match = [regex]::Match($line, "^\s*(?:Haltung|Leitung)\s+(?<id>\S+)")
        if (-not $match.Success) {
            $match = [regex]::Match($line, "Haltungsinspektion\s+-\s+\d{2}\.\d{2}\.\d{4}\s+-\s+(?<id>\S+)")
        }
        if ($match.Success) {
            if ($null -ne $currentHaltung) {
                $haltungen += @{
                    Id = $currentHaltung
                    Lines = $currentBlock
                }
            }
            $currentHaltung = $match.Groups["id"].Value
            $currentBlock = @($line)
        }
        elseif ($null -ne $currentHaltung) {
            $currentBlock += $line
        }
    }
    
    if ($null -ne $currentHaltung) {
        $haltungen += @{
            Id = $currentHaltung
            Lines = $currentBlock
        }
    }
    
    # Fallback: Suche nach anderen Formaten
    if ($haltungen.Count -eq 0) {
        # Format: "Haltungsinspektion - DD.MM.YYYY - ID" (mehrere Treffer möglich)
        $matches = [regex]::Matches($text, "Haltungsinspektion\s+-\s+\d{2}\.\d{2}\.\d{4}\s+-\s+(?<id>\S+)")
        if ($matches.Count -gt 0) {
            foreach ($m in $matches) {
                $haltungen += @{
                    Id = $m.Groups["id"].Value
                    Lines = $lines
                }
            }
        }
    }
    
    # Deduplizieren: Gleiche IDs zusammenführen (mehrere Seiten pro Leitung)
    $uniqueHaltungen = @{}
    foreach ($h in $haltungen) {
        if (-not $uniqueHaltungen.ContainsKey($h.Id)) {
            $uniqueHaltungen[$h.Id] = @{
                Id = $h.Id
                Lines = @()
            }
        }
        $uniqueHaltungen[$h.Id].Lines += $h.Lines
    }
    
    return @{
        Haltungen = @($uniqueHaltungen.Values)
        FullText = $text
        Lines = $lines
    }
}

function Get-HaltungDataFromBlock {
    param(
        [string]$HaltungId,
        [string[]]$Lines
    )
    
    $data = @{
        Haltungsname = $HaltungId
        Strasse = ""
        Rohrmaterial = ""
        DN_mm = ""
        Nutzungsart = ""
        Haltungslaenge_m = ""
        Fliessrichtung = ""
        Primaere_Schaeden = ""
        Zustandsklasse = ""
        'Dichtheits-Pruefungsresultat' = ""
        Eigentuemer = ""
        Bemerkungen = ""
    }
    
    $schaeden = @()
    $fullText = $Lines -join "`n"
    
    foreach ($line in $Lines) {
        # Strasse - mehrere Patterns (inkl. "Straße/ Standort")
        if (-not $data.Strasse) {
            $m = [regex]::Match($line, "Stra(?:ss|ß)e[/\s]*(?:Standort)?\s+(?<val>.+?)(?:\s{2,}|$)")
            if ($m.Success) { $data.Strasse = $m.Groups["val"].Value.Trim() }
            
            $m = [regex]::Match($line, "Standort\s+(?<val>.+?)(?:\s{2,}|$)")
            if ($m.Success -and -not $data.Strasse) { $data.Strasse = $m.Groups["val"].Value.Trim() }
        }
        
        # Rohrmaterial - mehrere Patterns (HL [m] ausschließen)
        if (-not $data.Rohrmaterial) {
            # Regex angepasst: ^\s* erlaubt Einrückungen vor "Material"
            $m = [regex]::Match($line, "^\s*Material\s+(?<val>.+?)(?:\s{2,}|$)")
            if ($m.Success) { 
                $val = $m.Groups["val"].Value.Trim()
                # Zusätzlicher Filter: nur gültige Materialien akzeptieren
                if ($val -notmatch "^\d+|^HL|^\[") {
                    $data.Rohrmaterial = $val
                }
            }
            
            $m = [regex]::Match($line, "Werkstoff\s+(?<val>.+?)(?:\s{2,}|$)")
            if ($m.Success -and -not $data.Rohrmaterial) { 
                $val = $m.Groups["val"].Value.Trim()
                if ($val -notmatch "^\d+|^HL|^\[") {
                    $data.Rohrmaterial = $val
                }
            }
        }
        
        # DN mm - mehrere Patterns (inkl. "Dimension [mm]  150 / 150" und "Dimension   150 / 150")
        if (-not $data.DN_mm) {
            # Pattern für "Dimension [mm]  150 / 150" oder "Dimension   150 / 150"
            $m = [regex]::Match($line, "Dimension\s*(?:\[mm\])?\s+(?<val>\d+)\s*/")
            if ($m.Success) { $data.DN_mm = $m.Groups["val"].Value }
            
            $m = [regex]::Match($line, "(?:Profilh[oö]he|Profilbreite|DN|Nennweite)\s*(?:\[mm\])?\s*(?<val>\d+)")
            if ($m.Success -and -not $data.DN_mm) { $data.DN_mm = $m.Groups["val"].Value }
            
            # Pattern: "300 mm" oder "DN 300"
            $m = [regex]::Match($line, "(?:DN\s*)?(?<val>\d{2,4})\s*mm")
            if ($m.Success -and -not $data.DN_mm) { $data.DN_mm = $m.Groups["val"].Value }
        }
        
        # Nutzungsart (inkl. "Kanalart")
        if (-not $data.Nutzungsart) {
            $m = [regex]::Match($line, "(?:Nutzungsart|Kanalart)\s+(?<val>.+?)(?:\s{2,}|$)")
            if ($m.Success) { $data.Nutzungsart = $m.Groups["val"].Value.Trim() }
            
            # Direkte Erkennung
            if ($line -match "Schmutzwasser|Regenwasser|Mischabwasser|Regenabwasser") {
                $val = $Matches[0]
                # Mapping: Regenabwasser -> Regenwasser
                if ($val -eq "Regenabwasser") { $val = "Regenwasser" }
                $data.Nutzungsart = $val
            }
        }
        
        # Haltungslänge - mehrere Patterns (inkl. "Leitungslänge")
        if (-not $data.Haltungslaenge_m) {
            $m = [regex]::Match($line, "(?:Haltungsl[aä]nge|Leitungsl[aä]nge|Inspektionsl[aä]nge|Gesamtinsp\.l[aä]nge)\s*(?<val>\d+[.,]?\d*)\s*m")
            if ($m.Success) { $data.Haltungslaenge_m = ($m.Groups["val"].Value -replace ",", ".") }
            
            # Pattern: "HL  5.60 m" oder "Inspektionslänge  5.60 m"
            $m = [regex]::Match($line, "(?:HL|Inspektionsl[aä]nge)\s+(?<val>\d+[.,]\d+)\s*m")
            if ($m.Success -and -not $data.Haltungslaenge_m) { 
                $data.Haltungslaenge_m = ($m.Groups["val"].Value -replace ",", ".") 
            }
        }
        
        # Fliessrichtung (inkl. "Inspektionsrichtung")
        if (-not $data.Fliessrichtung) {
            $m = [regex]::Match($line, "(?:Inspektionsrichtung|Flie[sß]richtung)\s+(?<val>.+?)(?:\s{2,}|$)")
            if ($m.Success) {
                $dir = $m.Groups["val"].Value.Trim()
                if ($dir -match "(?i)in\s*flie|stromab|In Flie") { $data.Fliessrichtung = "In Fliessrichtung" }
                elseif ($dir -match "(?i)gegen\s*flie|stromauf|Gegen Flie") { $data.Fliessrichtung = "Gegen Fliessrichtung" }
                else { $data.Fliessrichtung = $dir }
            }
        }
        
        # Zustandsklasse
        if (-not $data.Zustandsklasse) {
            $m = [regex]::Match($line, "Zustandsklasse\s*(?:ZK)?\s*(?<val>[0-5])")
            if ($m.Success) { $data.Zustandsklasse = $m.Groups["val"].Value }
            
            $m = [regex]::Match($line, "ZK\s*(?<val>[0-5])")
            if ($m.Success -and -not $data.Zustandsklasse) { $data.Zustandsklasse = $m.Groups["val"].Value }
        }
        
        # Eigentümer
        if (-not $data.Eigentuemer) {
            $m = [regex]::Match($line, "Eigent[uü]mer\s+(?<val>.+?)(?:\s{2,}|$)")
            if ($m.Success) { $data.Eigentuemer = $m.Groups["val"].Value.Trim() }
        }
        
        # Schäden-Codes aus "Zustand XXX.X" Pattern (z.B. "Zustand BAF.C.E")
        $m = [regex]::Match($line, "Zustand\s+(?<code>[A-Z]{2,3}(?:\.[A-Z])?(?:\.[A-Z])?)\s+.*?(?<dist>\d+[.,]\d+)\s*m")
        if ($m.Success) {
            $code = $m.Groups["code"].Value
            $dist = $m.Groups["dist"].Value -replace ",", "."
            # Ausschluss von Start/Ende-Codes
            if ($code -notin @("BCD", "BDB", "BDE", "BDF", "BCE")) {
                $schaeden += "$code (${dist}m)"
            }
        }
        
        # Alternative: Schäden-Codes (z.B. "BCD Rohranfang", "BDB Beginn der Inspektion")
        $m = [regex]::Match($line, "^\s*(?:\d+\s+)?(?:\d{2}:\d{2}:\d{2}\s+)?(?:\d+[.,]?\d*\s+)?(?<code>[A-Z]{2,3}(?:\.[A-Z])?(?:\.[A-Z])?)\s+(?<desc>.+?)(?:\s{2,}|\s*$)")
        if ($m.Success) {
            $code = $m.Groups["code"].Value
            $desc = $m.Groups["desc"].Value.Trim()
            # Nur echte Schadencodes (nicht BCD=Beginn, BDB=Beginn der Inspektion)
            if ($code -match "^[A-Z]{2,3}(\.[A-Z])?$" -and $desc.Length -gt 2) {
                # Ausschluss von Start/Ende-Codes
                if ($code -notin @("BCD", "BDB", "BDE", "BDF", "BCE")) {
                    $schaeden += "$code $desc"
                }
            }
        }
    }
    
    if ($schaeden.Count -gt 0) {
        $data.Primaere_Schaeden = ($schaeden | Select-Object -First 15) -join "`r`n"
    }
    
    return $data
}

# ========== XTF-Import (INTERLIS) ==========
function ConvertFrom-XtfFile {
    param([string]$XtfPath)
    
    if (-not (Test-Path $XtfPath)) {
        throw "XTF-Datei nicht gefunden: $XtfPath"
    }
    
    [xml]$xml = Get-Content -Path $XtfPath -Encoding UTF8
    $ns = New-Object System.Xml.XmlNamespaceManager($xml.NameTable)
    $ns.AddNamespace("ili", "http://www.interlis.ch/INTERLIS2.3")
    
    $haltungen = @()
    $kanaele = @{}
    $schaeden = @{}
    $schaedenRaw = @{}
    $findingsByUntersuchung = @{}
    
    # Bestimme das Schema (SIA405 oder VSA_KEK)
    $isSIA405 = $xml.OuterXml -match "SIA405_ABWASSER"
    $isVSA = $xml.OuterXml -match "VSA_KEK"
    $useVsaHelpers = (Get-Command ConvertTo-Double -ErrorAction SilentlyContinue) -ne $null
    $ignoreCodes = @("BCD", "BDB", "BDE", "BCE", "BDF")
    if ($script:VsaRules -and $script:VsaRules.ignoreCodes) {
        $ignoreCodes += @($script:VsaRules.ignoreCodes)
        $ignoreCodes = $ignoreCodes | Select-Object -Unique
    }
    
    # Parse Kanäle (für Nutzungsart) - nur Nodes die mit .Kanal enden
    $kanalNodes = $xml.SelectNodes("//*[substring(local-name(), string-length(local-name()) - 5) = '.Kanal']")
    foreach ($kanal in $kanalNodes) {
        $tid = $kanal.GetAttribute("TID")
        if ($tid) {
            $kanaele[$tid] = @{
                Bezeichnung = $kanal.Bezeichnung
                Nutzungsart = $kanal.Nutzungsart_Ist
                Status = $kanal.Status
            }
        }
    }
    
    # Parse Schäden aus VSA_KEK
    if ($isVSA) {
        $schadNodes = $xml.SelectNodes("//*[contains(local-name(), '.Kanalschaden')]")
        foreach ($schad in $schadNodes) {
            $untersuchungRef = $schad.UntersuchungRef.REF
            if (-not $schaeden.ContainsKey($untersuchungRef)) {
                $schaeden[$untersuchungRef] = @()
            }
            if (-not $schaedenRaw.ContainsKey($untersuchungRef)) {
                $schaedenRaw[$untersuchungRef] = @()
            }
            $code = $schad.KanalSchadencode
            $anmerkung = $schad.Anmerkung
            $distanz = $schad.Distanz
            $quant1 = $schad.Quantifizierung1
            $quant2 = $schad.Quantifizierung2
            $lageVon = $schad.SchadenlageAnfang
            $lageBis = $schad.SchadenlageEnde
            $strecke = $schad.Streckenschaden

            if ($code) {
                $distVal = if ($useVsaHelpers) { ConvertTo-Double $distanz } else { $null }
                if (-not $distVal -and $distanz) { $distVal = [double]($distanz -replace ',', '.') }
                $q1 = if ($useVsaHelpers) { ConvertTo-Double $quant1 } else { $null }
                if ($null -eq $q1 -and $quant1) { $q1 = [double]($quant1 -replace ',', '.') }
                $q2 = if ($useVsaHelpers) { ConvertTo-Double $quant2 } else { $null }
                if ($null -eq $q2 -and $quant2) { $q2 = [double]($quant2 -replace ',', '.') }
                $lVon = if ($useVsaHelpers) { ConvertTo-Int $lageVon } else { $null }
                if ($null -eq $lVon -and $lageVon) { [int]::TryParse($lageVon, [ref]$lVon) | Out-Null }
                $lBis = if ($useVsaHelpers) { ConvertTo-Int $lageBis } else { $null }
                if ($null -eq $lBis -and $lageBis) { [int]::TryParse($lageBis, [ref]$lBis) | Out-Null }

                $schaedenRaw[$untersuchungRef] += @{
                    Code = $code
                    Distanz = $distVal
                    Quant1 = $q1
                    Quant2 = $q2
                    LageVon = $lVon
                    LageBis = $lBis
                    Streckenschaden = $strecke
                    Anmerkung = $anmerkung
                }
            }

            if ($code -and $code -notin $ignoreCodes) {
                $schadenText = "$code"
                if ($distanz) { $schadenText += " (${distanz}m)" }
                if ($anmerkung) { $schadenText += " - $anmerkung" }
                $schaeden[$untersuchungRef] += $schadenText
            }
        }
    }

    if ($isVSA -and $schaedenRaw.Count -gt 0 -and (Get-Command ConvertTo-VsaFindings -ErrorAction SilentlyContinue)) {
        $minLength = 3.0
        if ($script:VsaRules -and $script:VsaRules.defaults -and $script:VsaRules.defaults.minLengthKanal_m) {
            $minLength = [double]$script:VsaRules.defaults.minLengthKanal_m
        }
        foreach ($tid in $schaedenRaw.Keys) {
            $findingsByUntersuchung[$tid] = ConvertTo-VsaFindings -Schaeden $schaedenRaw[$tid] -MinLength $minLength
        }
    }
    
    # Parse Haltungen - nur Nodes die mit .Haltung enden (nicht .Haltungspunkt)
    $haltungNodes = $xml.SelectNodes("//*[substring(local-name(), string-length(local-name()) - 7) = '.Haltung']")
    foreach ($haltung in $haltungNodes) {
        $tid = $haltung.GetAttribute("TID")
        $bezeichnung = $haltung.Bezeichnung
        
        # Überspringe leere Einträge
        if (-not $bezeichnung) { continue }
        
        # Material-Mapping
        $materialRaw = $haltung.Material
        $material = switch -Wildcard ($materialRaw) {
            "*Beton*" { "Beton" }
            "*PVC*" { "PVC" }
            "*PE*" { "PE" }
            "*PP*" { "PP" }
            "*GFK*" { "GFK" }
            "*Steinzeug*" { "Steinzeug" }
            "*Guss*" { "Guss" }
            default { $materialRaw }
        }
        
        # Kanal-Referenz für Nutzungsart
        $kanalRef = $haltung.AbwasserbauwerkRef.REF
        $nutzungsart = ""
        if ($kanalRef -and $kanaele.ContainsKey($kanalRef)) {
            $nutzungsartRaw = $kanaele[$kanalRef].Nutzungsart
            $nutzungsart = switch -Wildcard ($nutzungsartRaw) {
                "*Schmutzabwasser*" { "Schmutzwasser" }
                "*Schmutzwasser*" { "Schmutzwasser" }
                "*Regenabwasser*" { "Regenwasser" }
                "*Regenwasser*" { "Regenwasser" }
                "*Mischabwasser*" { "Mischabwasser" }
                default { $nutzungsartRaw }
            }
        }
        
        $haltungen += @{
            TID = $tid
            Haltungsname = $bezeichnung
            Rohrmaterial = $material
            DN_mm = $haltung.Lichte_Hoehe
            Haltungslaenge_m = $haltung.LaengeEffektiv
            Nutzungsart = $nutzungsart
            KanalRef = $kanalRef
        }
    }
    
    # Parse Untersuchungen (VSA_KEK) für zusätzliche Infos
    if ($isVSA) {
        $untersuchungNodes = $xml.SelectNodes("//*[contains(local-name(), '.Untersuchung')]")
        foreach ($unters in $untersuchungNodes) {
            $tid = $unters.GetAttribute("TID")
            $bezeichnung = $unters.Bezeichnung
            $inspLaenge = $unters.Inspizierte_Laenge
            $vonPunkt = $unters.vonPunktBezeichnung
            $bisPunkt = $unters.bisPunktBezeichnung
            $zeitpunkt = $unters.Zeitpunkt
            $kanalRef = $unters.AbwasserbauwerkRef.REF
            
            # Finde passende Haltung oder erstelle neue
            $existing = $haltungen | Where-Object { $_.Haltungsname -eq $bezeichnung }
            $znResult = $null
            if ($script:VsaRules -and $findingsByUntersuchung.ContainsKey($tid) -and (Get-Command Compute-VsaZustandsnote -ErrorAction SilentlyContinue)) {
                $findings = $findingsByUntersuchung[$tid]
                $lenVal = if ($useVsaHelpers) { ConvertTo-Double $inspLaenge } else { $null }
                if ($null -eq $lenVal -and $inspLaenge) { $lenVal = [double]($inspLaenge -replace ',', '.') }

                if (($null -eq $lenVal -or $lenVal -le 0) -and $existing -and $existing.Haltungslaenge_m) {
                    $lenVal = if ($useVsaHelpers) { ConvertTo-Double $existing.Haltungslaenge_m } else { $null }
                    if ($null -eq $lenVal -and $existing.Haltungslaenge_m) { $lenVal = [double]($existing.Haltungslaenge_m -replace ',', '.') }
                }

                $dnVal = $null
                if ($existing -and $existing.DN_mm) {
                    $dnVal = if ($useVsaHelpers) { ConvertTo-Int $existing.DN_mm } else { $null }
                    if ($null -eq $dnVal) { [int]::TryParse($existing.DN_mm, [ref]$dnVal) | Out-Null }
                }

                $znResult = Compute-VsaZustandsnote -Findings $findings -Laenge $lenVal -Dn $dnVal -Rules $script:VsaRules
            }
            if (-not $existing) {
                $nutzungsart = ""
                if ($kanalRef -and $kanaele.ContainsKey($kanalRef)) {
                    $nutzungsartRaw = $kanaele[$kanalRef].Nutzungsart
                    $nutzungsart = switch -Wildcard ($nutzungsartRaw) {
                        "*Schmutzwasser*" { "Schmutzwasser" }
                        "*Regenwasser*" { "Regenwasser" }
                        "*Mischabwasser*" { "Mischabwasser" }
                        default { $nutzungsartRaw }
                    }
                }
                
                $haltungen += @{
                    TID = $tid
                    Haltungsname = $bezeichnung
                    Haltungslaenge_m = $inspLaenge
                    Nutzungsart = $nutzungsart
                    VonPunkt = $vonPunkt
                    BisPunkt = $bisPunkt
                    Zeitpunkt = $zeitpunkt
                    Schaeden = if ($schaeden.ContainsKey($tid)) { $schaeden[$tid] -join "`r`n" } else { "" }
                    Zustandsnote = if ($znResult) { $znResult.Zustandsnote } else { $null }
                    Zustandsklasse = if ($znResult) { $znResult.Zustandsklasse } else { "" }
                    Pruefungsresultat = if ($znResult) { $znResult.Pruefungsresultat } else { "" }
                }
            }
            else {
                # Ergänze Schäden zur bestehenden Haltung
                if ($schaeden.ContainsKey($tid)) {
                    $existing.Schaeden = $schaeden[$tid] -join "`r`n"
                }
                if ($znResult) {
                    $existing.Zustandsnote = $znResult.Zustandsnote
                    $existing.Zustandsklasse = $znResult.Zustandsklasse
                    $existing.Pruefungsresultat = $znResult.Pruefungsresultat
                }
            }
        }
    }
    
    return @{
        Haltungen = $haltungen
        IsSIA405 = $isSIA405
        IsVSA = $isVSA
        FileName = [System.IO.Path]::GetFileName($XtfPath)
    }
}

function Get-HaltungDataFromXtf {
    param([hashtable]$XtfHaltung)
    
    $data = @{
        Haltungsname = $XtfHaltung.Haltungsname
        Strasse = ""
        Rohrmaterial = $XtfHaltung.Rohrmaterial
        DN_mm = $XtfHaltung.DN_mm
        Nutzungsart = $XtfHaltung.Nutzungsart
        Haltungslaenge_m = $XtfHaltung.Haltungslaenge_m
        Fliessrichtung = ""
        Primaere_Schaeden = $XtfHaltung.Schaeden
        Zustandsklasse = $XtfHaltung.Zustandsklasse
        'Dichtheits-Pruefungsresultat' = $XtfHaltung.Pruefungsresultat
        Eigentuemer = ""
        Bemerkungen = ""
    }
    
    return $data
}

function Merge-HaltungData {
    param(
        [array]$HaltungData,
        [ValidateSet("XTF", "SIA405", "PDF")]
        [string]$ImportSource = "XTF"
    )

    # Import-Hierarchie: XTF = Basis (darf setzen), SIA405/PDF = nur ergänzen (nie überschreiben)
    $isBasisImport = ($ImportSource -eq "XTF")

    # Konfliktprotokoll für diesen Import
    $conflicts = @()

    # Bestehende Haltungen nach Key indexieren
    $existingByName = @{}
    foreach ($rowId in $script:RowControls.Keys) {
        $controls = $script:RowControls[$rowId]
        $nameValue = ""
        $nameControl = $controls["Haltungsname"]
        if ($nameControl -is [System.Windows.Controls.TextBox]) {
            $nameValue = $nameControl.Text
        }
        elseif ($nameControl -is [System.Windows.Controls.ComboBox]) {
            $nameValue = $nameControl.Text
        }
        $key = Get-HaltungsnameKey -Name $nameValue
        if ($key -and -not $existingByName.ContainsKey($key)) {
            $existingByName[$key] = $rowId
        }
    }

    $newCount = 0
    $mergeCount = 0
    $conflictCount = 0

    foreach ($data in $HaltungData) {
        if (-not $data -or -not $data.Haltungsname) { continue }
        $key = Get-HaltungsnameKey -Name $data.Haltungsname
        $rowId = $null
        $isNewRow = $false

        if ($key -and $existingByName.ContainsKey($key)) {
            $rowId = $existingByName[$key]
            $mergeCount++
        }
        else {
            $newRow = Add-HaltungRow
            $rowId = $newRow.RowId
            if ($key) {
                $existingByName[$key] = $rowId
            }
            $newCount++
            $isNewRow = $true

            # Initialisiere FieldSources und UserEdited für neue Zeile
            if (-not $script:RowFieldSources.ContainsKey($rowId)) {
                $script:RowFieldSources[$rowId] = @{}
            }
            if (-not $script:RowUserEdited.ContainsKey($rowId)) {
                $script:RowUserEdited[$rowId] = @{}
            }
        }

        # Sicherstellen dass Tracking-Dictionaries existieren
        if (-not $script:RowFieldSources.ContainsKey($rowId)) {
            $script:RowFieldSources[$rowId] = @{}
        }
        if (-not $script:RowUserEdited.ContainsKey($rowId)) {
            $script:RowUserEdited[$rowId] = @{}
        }

        $controls = $script:RowControls[$rowId]
        $fieldSources = $script:RowFieldSources[$rowId]
        $userEdited = $script:RowUserEdited[$rowId]

        foreach ($fieldDef in $script:FieldDefinitions) {
            $fieldName = $fieldDef.Name
            if (-not $data.ContainsKey($fieldName)) { continue }

            $value = $data[$fieldName]
            if ([string]::IsNullOrWhiteSpace([string]$value)) { continue }

            $control = $controls[$fieldName]
            $currentValue = ""

            if ($control -is [System.Windows.Controls.TextBox]) {
                $currentValue = $control.Text
            }
            elseif ($control -is [System.Windows.Controls.ComboBox]) {
                $currentValue = $control.Text
            }
            else {
                continue
            }

            # Prüfe ob Feld manuell editiert wurde
            $isUserEdited = $userEdited.ContainsKey($fieldName) -and $userEdited[$fieldName] -eq $true

            # Entscheidungslogik
            $shouldSet = $false
            $shouldLog = $false

            if ([string]::IsNullOrWhiteSpace($currentValue)) {
                # Feld ist leer
                if (-not $isUserEdited) {
                    # Nicht manuell editiert -> setzen
                    $shouldSet = $true
                }
                # Wenn manuell editiert (User hat es bewusst geleert) -> nicht setzen
            }
            elseif ($fieldName -eq "Primaere_Schaeden") {
                # Schäden werden immer ergänzt (nicht ersetzt)
                $incoming = [string]$value
                if (-not [string]::IsNullOrWhiteSpace($incoming) -and $currentValue -notmatch [regex]::Escape($incoming)) {
                    if ($control -is [System.Windows.Controls.TextBox]) {
                        $script:IsProgrammaticChange = $true
                        try {
                            $null = Set-TextSafe -Control $control -Value ($currentValue.TrimEnd() + "`r`n" + $incoming) -Context "Import:${ImportSource}:$fieldName"
                        }
                        finally {
                            $script:IsProgrammaticChange = $false
                        }
                    }
                }
                continue
            }
            elseif ($currentValue -ne $value) {
                # Feld hat bereits einen anderen Wert
                if ($isBasisImport -and -not $isUserEdited) {
                    # Basis-Import (XTF) darf überschreiben, wenn nicht manuell editiert
                    $shouldSet = $true
                    $shouldLog = $true
                }
                else {
                    # Ergänzungs-Import (SIA405/PDF) -> nie überschreiben, nur loggen
                    $shouldLog = $true
                }
            }
            # Wenn gleicher Wert -> nichts tun

            if ($shouldSet) {
                $script:IsProgrammaticChange = $true
                try {
                    if ($control -is [System.Windows.Controls.TextBox]) {
                        $null = Set-TextSafe -Control $control -Value $value -Context "Import:${ImportSource}:$fieldName"
                    }
                    elseif ($control -is [System.Windows.Controls.ComboBox]) {
                        $null = Set-TextSafe -Control $control -Value $value -Context "Import:${ImportSource}:$fieldName"
                    }
                }
                finally {
                    $script:IsProgrammaticChange = $false
                }
                $fieldSources[$fieldName] = $ImportSource
            }

            if ($shouldLog -and $currentValue -ne $value) {
                $aktion = if ($shouldSet) { "Ueberschrieben" } else { "Beibehalten" }
                $conflicts += [PSCustomObject]@{
                    HaltungKey = $key
                    Feld = $fieldName
                    Altwert = $currentValue
                    Neuwert = $value
                    Quelle = $ImportSource
                    Aktion = $aktion
                }
                if (-not $shouldSet) {
                    $conflictCount++
                }
            }
        }

        if ($data.Haltungsname) {
            Set-RowTitle -RowId $rowId -Haltungsname $data.Haltungsname
        }
    }

    # Konflikte zum globalen Log hinzufügen und speichern
    if ($conflicts.Count -gt 0) {
        $script:ImportConflicts += $conflicts

        # Konfliktlog speichern
        $logPath = Join-Path $script:LogFolder "import_conflicts.log"
        $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
        $logLines = @("=== Import: $timestamp - $ImportSource ===")
        foreach ($c in $conflicts) {
            $logLines += "$($c.HaltungKey) | $($c.Feld) | Alt: $($c.Altwert) | Neu: $($c.Neuwert) | $($c.Aktion)"
        }
        $logLines += ""
        Add-Content -Path $logPath -Value ($logLines -join "`r`n") -Encoding UTF8
    }

    return @{
        NewCount = $newCount
        MergeCount = $mergeCount
        ConflictCount = $conflictCount
        Conflicts = $conflicts
    }
}

function Merge-XtfHaltungen {
    param(
        [array]$Haltungen,
        [ValidateSet("XTF", "SIA405", "PDF")]
        [string]$ImportSource = "XTF"
    )

    $dataList = foreach ($xtfHaltung in $Haltungen) {
        Get-HaltungDataFromXtf -XtfHaltung $xtfHaltung
    }

    return Merge-HaltungData -HaltungData $dataList -ImportSource $ImportSource
}

# ========== XAML UI Definition ==========
$xaml = @"
<Window xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="Haltungs-Auswertung Pro" 
        Height="900" Width="1200"
        WindowStartupLocation="CenterScreen"
        Background="#F0F0F0">
    <Window.Resources>
        <Style TargetType="Button" x:Key="ModernButton">
            <Setter Property="Background" Value="#0078D4"/>
            <Setter Property="Foreground" Value="White"/>
            <Setter Property="Padding" Value="12,6"/>
            <Setter Property="BorderThickness" Value="0"/>
            <Setter Property="Cursor" Value="Hand"/>
            <Setter Property="FontWeight" Value="SemiBold"/>
            <Style.Triggers>
                <Trigger Property="IsMouseOver" Value="True">
                    <Setter Property="Background" Value="#1084D9"/>
                </Trigger>
            </Style.Triggers>
        </Style>
        <Style TargetType="Button" x:Key="SecondaryButton">
            <Setter Property="Background" Value="#E0E0E0"/>
            <Setter Property="Foreground" Value="#333333"/>
            <Setter Property="Padding" Value="10,5"/>
            <Setter Property="BorderThickness" Value="0"/>
            <Setter Property="Cursor" Value="Hand"/>
        </Style>
    </Window.Resources>
    
    <Grid>
        <Grid.ColumnDefinitions>
            <ColumnDefinition Width="280"/>
            <ColumnDefinition Width="*"/>
        </Grid.ColumnDefinitions>
        
        <!-- Linke Sidebar: Projektmanager -->
        <Border Grid.Column="0" Background="White" BorderBrush="#D0D0D0" BorderThickness="0,0,1,0">
            <Grid>
                <Grid.RowDefinitions>
                    <RowDefinition Height="Auto"/>
                    <RowDefinition Height="Auto"/>
                    <RowDefinition Height="*"/>
                    <RowDefinition Height="Auto"/>
                </Grid.RowDefinitions>
                
                <!-- Projekt-Header -->
                <Border Grid.Row="0" Background="#0078D4" Padding="15">
                    <StackPanel>
                        <TextBlock Text="PROJEKTMANAGER" FontWeight="Bold" Foreground="White" FontSize="12"/>
                    </StackPanel>
                </Border>
                
                <!-- Projekt-Metadaten -->
                <StackPanel Grid.Row="1" Margin="10">
                    <TextBlock Text="Projektname:" FontSize="11" Foreground="#666" Margin="0,0,0,3"/>
                    <TextBox Name="txtProjectName" Text="Neues Projekt" Padding="6,4" Margin="0,0,0,8"/>
                    
                    <TextBlock Text="Zone:" FontSize="11" Foreground="#666" Margin="0,0,0,3"/>
                    <TextBox Name="txtZone" Padding="6,4" Margin="0,0,0,8"/>
                    
                    <TextBlock Text="Firma:" FontSize="11" Foreground="#666" Margin="0,0,0,3"/>
                    <TextBox Name="txtFirma" Padding="6,4" Margin="0,0,0,8"/>
                    
                    <TextBlock Text="Bearbeiter:" FontSize="11" Foreground="#666" Margin="0,0,0,3"/>
                    <TextBox Name="txtBearbeiter" Padding="6,4" Margin="0,0,0,8"/>
                    
                    <Separator Margin="0,5"/>
                    
                    <StackPanel Orientation="Horizontal" Margin="0,5,0,0">
                        <Button Name="btnNewProject" Content="Neu" Style="{StaticResource SecondaryButton}" Margin="0,0,5,0" Width="75"/>
                        <Button Name="btnSaveProject" Content="Speichern" Style="{StaticResource ModernButton}" Margin="0,0,5,0" Width="85"/>
                        <Button Name="btnSaveAs" Content="..." Style="{StaticResource SecondaryButton}" Width="30" ToolTip="Speichern unter..."/>
                    </StackPanel>
                </StackPanel>
                
                <!-- Projektliste -->
                <Border Grid.Row="2" Margin="10,0,10,10">
                    <Grid>
                        <Grid.RowDefinitions>
                            <RowDefinition Height="Auto"/>
                            <RowDefinition Height="*"/>
                        </Grid.RowDefinitions>
                        <TextBlock Grid.Row="0" Text="Gespeicherte Projekte:" FontSize="11" Foreground="#666" Margin="0,0,0,5"/>
                        <ListBox Name="projectList" Grid.Row="1" BorderBrush="#D0D0D0"/>
                    </Grid>
                </Border>
                
                <!-- Projekt-Aktionen -->
                <StackPanel Grid.Row="3" Margin="10,0,10,10">
                    <Button Name="btnOpenProject" Content="Projekt öffnen" Style="{StaticResource SecondaryButton}" Margin="0,0,0,5"/>
                    <Button Name="btnDeleteProject" Content="Projekt löschen" Background="#FFCDD2" Foreground="#C62828"/>
                </StackPanel>
            </Grid>
        </Border>
        
        <!-- Rechter Hauptbereich -->
        <Grid Grid.Column="1">
            <Grid.RowDefinitions>
                <RowDefinition Height="Auto"/>
                <RowDefinition Height="*"/>
                <RowDefinition Height="Auto"/>
            </Grid.RowDefinitions>
            
            <!-- Toolbar -->
            <Border Grid.Row="0" Background="White" Padding="12" BorderBrush="#E0E0E0" BorderThickness="0,0,0,1">
                <StackPanel Orientation="Horizontal">
                    <Button Name="btnAdd" Content="+ Neue Haltung" Style="{StaticResource ModernButton}" Margin="0,0,10,0"/>
                    <Button Name="btnImportXtf" Content="📥 XTF (Basis)" Style="{StaticResource ModernButton}" Margin="0,0,10,0" ToolTip="Basis-Import: Legt Haltungen an und setzt Werte"/>
                    <Button Name="btnImportXtfSia405" Content="📥 SIA405 (Ergänzung)" Style="{StaticResource SecondaryButton}" Margin="0,0,10,0" ToolTip="Ergänzungs-Import: Füllt nur leere Felder"/>
                    <Button Name="btnImportPdfGlobal" Content="📄 PDF (global)" Style="{StaticResource SecondaryButton}" Margin="0,0,10,0" ToolTip="PDF Import: Alle Haltungen im PDF importieren"/>
                    <Button Name="btnExport" Content="Excel Export" Style="{StaticResource SecondaryButton}" Margin="0,0,10,0"/>
                    <Separator Width="1" Background="#D0D0D0" Margin="5,0"/>
                    <TextBlock Name="lblHaltungCount" Text="0 Haltungen" VerticalAlignment="Center" Foreground="#666" Margin="10,0,0,0"/>
                </StackPanel>
            </Border>
            
            <!-- Scrollbarer Bereich für Haltungs-Zeilen -->
            <ScrollViewer Grid.Row="1" VerticalScrollBarVisibility="Auto" Padding="15">
                <StackPanel Name="rowContainer" Margin="0,0,15,0"/>
            </ScrollViewer>
            
            <!-- Statusleiste -->
            <Border Grid.Row="2" Background="White" Padding="10" BorderBrush="#E0E0E0" BorderThickness="0,1,0,0">
                <Grid>
                    <TextBlock Name="lblStatus" Text="Bereit" Foreground="#666"/>
                    <TextBlock Name="lblVersion" Text="v2.0" HorizontalAlignment="Right" Foreground="#999"/>
                </Grid>
            </Border>
        </Grid>
    </Grid>
</Window>
"@

# ========== UI Laden ==========
$reader = [System.Xml.XmlReader]::Create([System.IO.StringReader]::new($xaml))
$script:window = [System.Windows.Markup.XamlReader]::Load($reader)

# Controls referenzieren (script scope für Zugriff aus Funktionen)
$script:btnAdd = $script:window.FindName("btnAdd")
$script:btnImportXtf = $script:window.FindName("btnImportXtf")
$script:btnImportXtfSia405 = $script:window.FindName("btnImportXtfSia405")
$script:btnImportPdfGlobal = $script:window.FindName("btnImportPdfGlobal")
$script:btnExport = $script:window.FindName("btnExport")
$script:rowContainer = $script:window.FindName("rowContainer")
$script:lblStatus = $script:window.FindName("lblStatus")
$script:lblHaltungCount = $script:window.FindName("lblHaltungCount")

# Projektmanager Controls
$script:txtProjectName = $script:window.FindName("txtProjectName")
$script:txtZone = $script:window.FindName("txtZone")
$script:txtFirma = $script:window.FindName("txtFirma")
$script:txtBearbeiter = $script:window.FindName("txtBearbeiter")
$script:btnNewProject = $script:window.FindName("btnNewProject")
$script:btnSaveProject = $script:window.FindName("btnSaveProject")
$script:btnSaveAs = $script:window.FindName("btnSaveAs")
$script:projectList = $script:window.FindName("projectList")
$script:btnOpenProject = $script:window.FindName("btnOpenProject")
$script:btnDeleteProject = $script:window.FindName("btnDeleteProject")

# ========== Hilfsfunktionen ==========
function Get-ParentObject {
    param([object]$Obj)
    
    if ($null -eq $Obj) {
        return $null
    }
    if ($Obj -is [System.Windows.Media.Visual] -or $Obj -is [System.Windows.Media.Media3D.Visual3D]) {
        return [System.Windows.Media.VisualTreeHelper]::GetParent($Obj)
    }
    if ($Obj -is [System.Windows.FrameworkContentElement]) {
        return $Obj.Parent
    }
    if ($Obj -is [System.Windows.DependencyObject]) {
        return [System.Windows.LogicalTreeHelper]::GetParent($Obj)
    }
    
    return $null
}

function Get-RowIdFromSender {
    param(
        $SenderObj,
        $EventArgs
    )
    
    if ($SenderObj -is [System.Windows.FrameworkElement] -or $SenderObj -is [System.Windows.FrameworkContentElement]) {
        $tag = $SenderObj.Tag
        if ($tag -is [string] -and -not [string]::IsNullOrWhiteSpace($tag)) {
            return $tag
        }
        if ($tag -is [hashtable] -and $tag.ContainsKey("RowId")) {
            return [string]$tag.RowId
        }
    }
    
    if ($null -eq $EventArgs) {
        return $null
    }
    
    $source = $EventArgs.OriginalSource
    while ($null -ne $source) {
        if ($source -is [System.Windows.FrameworkElement] -or $source -is [System.Windows.FrameworkContentElement]) {
            $tag = $source.Tag
            if ($tag -is [string] -and -not [string]::IsNullOrWhiteSpace($tag)) {
                return $tag
            }
            if ($tag -is [hashtable] -and $tag.ContainsKey("RowId")) {
                return [string]$tag.RowId
            }
        }
        
        $source = Get-ParentObject -Obj $source
    }
    
    return $null
}

function Get-HaltungsnameKey {
    param([string]$Name)
    
    if ([string]::IsNullOrWhiteSpace($Name)) {
        return $null
    }
    
    return $Name.Trim().ToUpperInvariant()
}

function Set-RowTitle {
    param(
        [string]$RowId,
        [string]$Haltungsname
    )
    
    if ([string]::IsNullOrWhiteSpace($Haltungsname)) {
        return
    }
    
    foreach ($child in $script:rowContainer.Children) {
        if ($child.Tag -eq $RowId) {
            $mainStack = $child.Child
            foreach ($elem in $mainStack.Children) {
                if ($elem -is [System.Windows.Controls.Grid]) {
                    foreach ($gridChild in $elem.Children) {
                        if ($gridChild -is [System.Windows.Controls.TextBlock] -and $gridChild.FontWeight -eq [System.Windows.FontWeights]::SemiBold) {
                            Set-TextSafe -Control $gridChild -Value "Haltung: $Haltungsname" -Context "RowTitle"
                            break
                        }
                    }
                    break
                }
            }
            break
        }
    }
}

function Write-ImportErrorLog {
    param(
        [string]$Context,
        [System.Exception]$Exception
    )
    
    if (-not $script:LogFolder) {
        return
    }
    
    try {
        $logPath = Join-Path $script:LogFolder "import_errors.log"
        $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
        $line = "$timestamp | $Context | $($Exception.Message)"
        Add-Content -Path $logPath -Value $line -Encoding UTF8
    }
    catch {
        # Logging should never block the import.
    }
}

function Set-TextSafe {
    param(
        [object]$Control,
        [string]$Value,
        [string]$Context = ""
    )
    
    if ($null -eq $Control) {
        return $false
    }
    
    if ($Control.PSObject.Properties.Match("Text").Count -eq 0) {
        return $false
    }
    
    try {
        $Control.Text = $Value
        return $true
    }
    catch {
        Write-ImportErrorLog -Context $Context -Exception $_.Exception
        return $false
    }
}

# ========== Haltungs-Zeile erstellen ==========
function Add-HaltungRow {
    $script:RowCounter++
    $rowId = [Guid]::NewGuid().ToString()
    $rowNumber = $script:RowCounter
    
    # Border als Container
    $border = New-Object System.Windows.Controls.Border
    $border.Background = [System.Windows.Media.Brushes]::White
    $border.BorderBrush = [System.Windows.Media.SolidColorBrush]::new([System.Windows.Media.Color]::FromRgb(224, 224, 224))
    $border.BorderThickness = [System.Windows.Thickness]::new(1)
    $border.Margin = [System.Windows.Thickness]::new(0, 0, 0, 12)
    $border.Padding = [System.Windows.Thickness]::new(15)
    $border.Tag = $rowId
    $border.AllowDrop = $true
    
    # Shadow-Effekt
    $shadow = New-Object System.Windows.Media.Effects.DropShadowEffect
    $shadow.BlurRadius = 8
    $shadow.ShadowDepth = 2
    $shadow.Opacity = 0.1
    $border.Effect = $shadow
    
    $mainStack = New-Object System.Windows.Controls.StackPanel
    
    # Header
    $headerGrid = New-Object System.Windows.Controls.Grid
    $col1 = New-Object System.Windows.Controls.ColumnDefinition
    $col1.Width = [System.Windows.GridLength]::new(1, [System.Windows.GridUnitType]::Star)
    $col2 = New-Object System.Windows.Controls.ColumnDefinition
    $col2.Width = [System.Windows.GridLength]::Auto
    $headerGrid.ColumnDefinitions.Add($col1)
    $headerGrid.ColumnDefinitions.Add($col2)
    
    $titleLabel = New-Object System.Windows.Controls.TextBlock
    $titleLabel.Text = "Haltung #$rowNumber"
    $titleLabel.FontSize = 15
    $titleLabel.FontWeight = [System.Windows.FontWeights]::SemiBold
    $titleLabel.Foreground = [System.Windows.Media.SolidColorBrush]::new([System.Windows.Media.Color]::FromRgb(51, 51, 51))
    [System.Windows.Controls.Grid]::SetColumn($titleLabel, 0)
    
    $buttonPanel = New-Object System.Windows.Controls.StackPanel
    $buttonPanel.Orientation = [System.Windows.Controls.Orientation]::Horizontal
    [System.Windows.Controls.Grid]::SetColumn($buttonPanel, 1)
    
    # PDF Import Button
    $btnImport = New-Object System.Windows.Controls.Button
    $btnImport.Content = "📄 PDF import"
    $btnImport.Background = [System.Windows.Media.SolidColorBrush]::new([System.Windows.Media.Color]::FromRgb(0, 120, 212))
    $btnImport.Foreground = [System.Windows.Media.Brushes]::White
    $btnImport.Padding = [System.Windows.Thickness]::new(12, 6, 12, 6)
    $btnImport.BorderThickness = [System.Windows.Thickness]::new(0)
    $btnImport.Margin = [System.Windows.Thickness]::new(0, 0, 8, 0)
    $btnImport.Cursor = [System.Windows.Input.Cursors]::Hand
    $btnImport.Tag = $rowId
    $btnImport.ToolTip = "PDF auswählen oder per Drag && Drop auf diese Zeile ziehen"
    
    # Verschieben nach oben Button
    $btnMoveUp = New-Object System.Windows.Controls.Button
    $btnMoveUp.Content = "▲"
    $btnMoveUp.Background = [System.Windows.Media.SolidColorBrush]::new([System.Windows.Media.Color]::FromRgb(224, 224, 224))
    $btnMoveUp.Foreground = [System.Windows.Media.SolidColorBrush]::new([System.Windows.Media.Color]::FromRgb(80, 80, 80))
    $btnMoveUp.Padding = [System.Windows.Thickness]::new(8, 4, 8, 4)
    $btnMoveUp.BorderThickness = [System.Windows.Thickness]::new(0)
    $btnMoveUp.Margin = [System.Windows.Thickness]::new(0, 0, 2, 0)
    $btnMoveUp.Cursor = [System.Windows.Input.Cursors]::Hand
    $btnMoveUp.Tag = $rowId
    $btnMoveUp.ToolTip = "Nach oben verschieben"
    
    # Verschieben nach unten Button
    $btnMoveDown = New-Object System.Windows.Controls.Button
    $btnMoveDown.Content = "▼"
    $btnMoveDown.Background = [System.Windows.Media.SolidColorBrush]::new([System.Windows.Media.Color]::FromRgb(224, 224, 224))
    $btnMoveDown.Foreground = [System.Windows.Media.SolidColorBrush]::new([System.Windows.Media.Color]::FromRgb(80, 80, 80))
    $btnMoveDown.Padding = [System.Windows.Thickness]::new(8, 4, 8, 4)
    $btnMoveDown.BorderThickness = [System.Windows.Thickness]::new(0)
    $btnMoveDown.Margin = [System.Windows.Thickness]::new(0, 0, 8, 0)
    $btnMoveDown.Cursor = [System.Windows.Input.Cursors]::Hand
    $btnMoveDown.Tag = $rowId
    $btnMoveDown.ToolTip = "Nach unten verschieben"
    
    # Löschen Button
    $btnDelete = New-Object System.Windows.Controls.Button
    $btnDelete.Content = "✕"
    $btnDelete.Background = [System.Windows.Media.SolidColorBrush]::new([System.Windows.Media.Color]::FromRgb(239, 154, 154))
    $btnDelete.Foreground = [System.Windows.Media.SolidColorBrush]::new([System.Windows.Media.Color]::FromRgb(198, 40, 40))
    $btnDelete.Padding = [System.Windows.Thickness]::new(10, 4, 10, 4)
    $btnDelete.BorderThickness = [System.Windows.Thickness]::new(0)
    $btnDelete.Cursor = [System.Windows.Input.Cursors]::Hand
    $btnDelete.Tag = $rowId
    $btnDelete.ToolTip = "Haltung löschen"
    
    $buttonPanel.Children.Add($btnMoveUp)
    $buttonPanel.Children.Add($btnMoveDown)
    $buttonPanel.Children.Add($btnImport)
    $buttonPanel.Children.Add($btnDelete)
    
    $headerGrid.Children.Add($titleLabel)
    $headerGrid.Children.Add($buttonPanel)
    $mainStack.Children.Add($headerGrid)
    
    # Separator
    $sep = New-Object System.Windows.Controls.Separator
    $sep.Margin = [System.Windows.Thickness]::new(0, 10, 0, 10)
    $mainStack.Children.Add($sep)
    
    # Felder-Container (2-spaltig)
    $fieldsGrid = New-Object System.Windows.Controls.Grid
    $fieldsGrid.ColumnDefinitions.Add((New-Object System.Windows.Controls.ColumnDefinition -Property @{ Width = [System.Windows.GridLength]::new(1, [System.Windows.GridUnitType]::Star) }))
    $fieldsGrid.ColumnDefinitions.Add((New-Object System.Windows.Controls.ColumnDefinition -Property @{ Width = [System.Windows.GridLength]::new(1, [System.Windows.GridUnitType]::Star) }))
    
    $fieldControls = @{}
    $fieldIndex = 0
    
    foreach ($fieldDef in $script:FieldDefinitions) {
        $row = [Math]::Floor($fieldIndex / 2)
        $col = $fieldIndex % 2
        
        while ($fieldsGrid.RowDefinitions.Count -le $row) {
            $fieldsGrid.RowDefinitions.Add((New-Object System.Windows.Controls.RowDefinition -Property @{ Height = [System.Windows.GridLength]::Auto }))
        }
        
        $fieldPanel = New-Object System.Windows.Controls.StackPanel
        $fieldPanel.Margin = [System.Windows.Thickness]::new(0, 0, 15, 8)
        [System.Windows.Controls.Grid]::SetRow($fieldPanel, $row)
        [System.Windows.Controls.Grid]::SetColumn($fieldPanel, $col)
        
        # Label
        $label = New-Object System.Windows.Controls.TextBlock
        $label.Text = $fieldDef.Label
        $label.FontSize = 11
        $label.Foreground = [System.Windows.Media.SolidColorBrush]::new([System.Windows.Media.Color]::FromRgb(100, 100, 100))
        $label.Margin = [System.Windows.Thickness]::new(0, 0, 0, 3)
        $fieldPanel.Children.Add($label)
        
        # Eingabefeld
        $control = $null
        switch ($fieldDef.Type) {
            "MultiLine" {
                $control = New-Object System.Windows.Controls.TextBox
                $control.AcceptsReturn = $true
                $control.TextWrapping = [System.Windows.TextWrapping]::Wrap
                $control.Height = $fieldDef.Height
                $control.VerticalScrollBarVisibility = [System.Windows.Controls.ScrollBarVisibility]::Auto
            }
            "ComboBox" {
                $control = New-Object System.Windows.Controls.ComboBox
                $control.IsEditable = $true
                foreach ($item in $fieldDef.Items) {
                    $control.Items.Add($item) | Out-Null
                }
            }
            default {
                $control = New-Object System.Windows.Controls.TextBox
            }
        }
        
        $control.BorderBrush = [System.Windows.Media.SolidColorBrush]::new([System.Windows.Media.Color]::FromRgb(200, 200, 200))
        $control.Padding = [System.Windows.Thickness]::new(6, 4, 6, 4)
        $control.Tag = @{ RowId = $rowId; FieldName = $fieldDef.Name }
        
        # Change-Event für Dirty-Flag und UserEdited-Tracking
        if ($control -is [System.Windows.Controls.TextBox]) {
            $control.Add_TextChanged({
                param($sender, $e)
                $script:IsDirty = $true
                Update-WindowTitle
                # Markiere Feld als manuell editiert (nur wenn NICHT programmgesteuert)
                if (-not $script:IsProgrammaticChange) {
                    $tagInfo = $sender.Tag
                    if ($tagInfo -is [hashtable] -and $tagInfo.ContainsKey("RowId") -and $tagInfo.ContainsKey("FieldName")) {
                        $rid = $tagInfo.RowId
                        $fname = $tagInfo.FieldName
                        if (-not $script:RowUserEdited.ContainsKey($rid)) {
                            $script:RowUserEdited[$rid] = @{}
                        }
                        $script:RowUserEdited[$rid][$fname] = $true
                    }
                }
            }.GetNewClosure())
        }
        elseif ($control -is [System.Windows.Controls.ComboBox]) {
            $control.Add_SelectionChanged({
                param($sender, $e)
                $script:IsDirty = $true
                Update-WindowTitle
                # Markiere Feld als manuell editiert (nur wenn NICHT programmgesteuert)
                if (-not $script:IsProgrammaticChange) {
                    $tagInfo = $sender.Tag
                    if ($tagInfo -is [hashtable] -and $tagInfo.ContainsKey("RowId") -and $tagInfo.ContainsKey("FieldName")) {
                        $rid = $tagInfo.RowId
                        $fname = $tagInfo.FieldName
                        if (-not $script:RowUserEdited.ContainsKey($rid)) {
                            $script:RowUserEdited[$rid] = @{}
                        }
                        $script:RowUserEdited[$rid][$fname] = $true
                    }
                }
            }.GetNewClosure())
        }
        
        $fieldPanel.Children.Add($control)
        $fieldsGrid.Children.Add($fieldPanel)
        
        $fieldControls[$fieldDef.Name] = $control
        $fieldIndex++
    }
    
    $mainStack.Children.Add($fieldsGrid)
    $border.Child = $mainStack
    
    # RowControls und Tracking initialisieren
    $script:RowControls[$rowId] = $fieldControls
    $script:RowFieldSources[$rowId] = @{}
    $script:RowUserEdited[$rowId] = @{}
    
    # Drag & Drop Events
    $border.Add_DragEnter({
        param($senderObj, $eventArgs)
        if ($eventArgs.Data.GetDataPresent([System.Windows.DataFormats]::FileDrop)) {
            $files = $eventArgs.Data.GetData([System.Windows.DataFormats]::FileDrop)
            $hasPdf = $false
            foreach ($f in $files) {
                if ($f -like "*.pdf") { $hasPdf = $true; break }
            }
            if ($hasPdf) {
                $eventArgs.Effects = [System.Windows.DragDropEffects]::Copy
                $senderObj.BorderBrush = [System.Windows.Media.SolidColorBrush]::new([System.Windows.Media.Color]::FromRgb(0, 120, 212))
                $senderObj.BorderThickness = [System.Windows.Thickness]::new(2)
            } else {
                $eventArgs.Effects = [System.Windows.DragDropEffects]::None
            }
        }
        $eventArgs.Handled = $true
    })
    
    $border.Add_DragLeave({
        param($senderObj, $eventArgs)
        $senderObj.BorderBrush = [System.Windows.Media.SolidColorBrush]::new([System.Windows.Media.Color]::FromRgb(224, 224, 224))
        $senderObj.BorderThickness = [System.Windows.Thickness]::new(1)
    })
    
    $border.Add_DragOver({
        param($senderObj, $eventArgs)
        if ($eventArgs.Data.GetDataPresent([System.Windows.DataFormats]::FileDrop)) {
            $eventArgs.Effects = [System.Windows.DragDropEffects]::Copy
        } else {
            $eventArgs.Effects = [System.Windows.DragDropEffects]::None
        }
        $eventArgs.Handled = $true
    })
    
    $border.Add_Drop({
        param($senderObj, $eventArgs)
        $senderObj.BorderBrush = [System.Windows.Media.SolidColorBrush]::new([System.Windows.Media.Color]::FromRgb(224, 224, 224))
        $senderObj.BorderThickness = [System.Windows.Thickness]::new(1)
        
        if ($eventArgs.Data.GetDataPresent([System.Windows.DataFormats]::FileDrop)) {
            $files = $eventArgs.Data.GetData([System.Windows.DataFormats]::FileDrop)
            $pdfFile = $null
            foreach ($f in $files) {
                if ($f -like "*.pdf") { $pdfFile = $f; break }
            }
            if ($pdfFile) {
                $dropRowId = $senderObj.Tag
                Import-PdfForRowFromPath -RowId $dropRowId -PdfPath $pdfFile
            }
        }
        $eventArgs.Handled = $true
    }.GetNewClosure())
    
    # Button Events - RowId direkt in Closure einfangen
    $capturedRowId = $rowId
    
    $btnImport.Add_Click({
        param($senderObj, $eventArgs)
        Import-PdfForRow -RowId $capturedRowId
    }.GetNewClosure())
    
    $btnMoveUp.Add_Click({
        param($senderObj, $eventArgs)
        $currentIndex = -1
        for ($i = 0; $i -lt $script:rowContainer.Children.Count; $i++) {
            if ($script:rowContainer.Children[$i].Tag -eq $capturedRowId) {
                $currentIndex = $i
                break
            }
        }
        if ($currentIndex -gt 0) {
            $element = $script:rowContainer.Children[$currentIndex]
            $script:rowContainer.Children.RemoveAt($currentIndex)
            $script:rowContainer.Children.Insert($currentIndex - 1, $element)
            $script:IsDirty = $true
            Update-WindowTitle
            if ($null -ne $script:lblStatus) { $script:lblStatus.Text = "Haltung nach oben verschoben" }
        }
    }.GetNewClosure())
    
    $btnMoveDown.Add_Click({
        param($senderObj, $eventArgs)
        $currentIndex = -1
        for ($i = 0; $i -lt $script:rowContainer.Children.Count; $i++) {
            if ($script:rowContainer.Children[$i].Tag -eq $capturedRowId) {
                $currentIndex = $i
                break
            }
        }
        if ($currentIndex -ge 0 -and $currentIndex -lt ($script:rowContainer.Children.Count - 1)) {
            $element = $script:rowContainer.Children[$currentIndex]
            $script:rowContainer.Children.RemoveAt($currentIndex)
            $script:rowContainer.Children.Insert($currentIndex + 1, $element)
            $script:IsDirty = $true
            Update-WindowTitle
            if ($null -ne $script:lblStatus) { $script:lblStatus.Text = "Haltung nach unten verschoben" }
        }
    }.GetNewClosure())
    
    $btnDelete.Add_Click({
        param($senderObj, $eventArgs)
        
        # Sicherstellen, dass die ID gültig ist
        if ([string]::IsNullOrWhiteSpace($capturedRowId)) {
             [System.Windows.MessageBox]::Show("Fehler: Interne ID fehlt.", "Fehler")
             return
        }

        # Lösche die Zeile mit der erfassten RowId
        $confirmDelete = [System.Windows.MessageBox]::Show("Haltung wirklich löschen?", "Bestätigung", [System.Windows.MessageBoxButton]::YesNo)
        if ($confirmDelete -eq [System.Windows.MessageBoxResult]::Yes) {
            # Finde den Border mit der capturedRowId
            $borderToRemove = $null
            foreach ($child in $script:rowContainer.Children) {
                if ($child.Tag -eq $capturedRowId) {
                    $borderToRemove = $child
                    break
                }
            }
            if ($null -ne $borderToRemove) {
                [void]$script:rowContainer.Children.Remove($borderToRemove)
                [void]$script:RowControls.Remove($capturedRowId)
                if ($script:RowFieldSources.ContainsKey($capturedRowId)) {
                    [void]$script:RowFieldSources.Remove($capturedRowId)
                }
                if ($script:RowUserEdited.ContainsKey($capturedRowId)) {
                    [void]$script:RowUserEdited.Remove($capturedRowId)
                }
                $script:IsDirty = $true
                Update-WindowTitle
                Update-HaltungCount
                if ($null -ne $script:lblStatus) {
                    $script:lblStatus.Text = "Haltung gelöscht"
                }
            } else {
                [System.Windows.MessageBox]::Show("Fehler: Zeile UI-Element nicht gefunden für ID $capturedRowId", "Fehler")
            }
        }
    }.GetNewClosure())
    
    # Zur UI hinzufügen
    $script:rowContainer.Children.Add($border)
    Update-HaltungCount
    
    return @{
        Border = $border
        FieldControls = $fieldControls
        RowId = $rowId
        TitleLabel = $titleLabel
    }
}

function Update-HaltungCount {
    $count = $script:rowContainer.Children.Count
    $script:lblHaltungCount.Text = "$count Haltung$(if($count -ne 1){'en'})"
}

# ========== PDF Import ==========
function Import-GlobalPdf {
    $dialog = New-Object System.Windows.Forms.OpenFileDialog
    $dialog.Filter = "PDF Dateien (*.pdf)|*.pdf|Alle Dateien (*.*)|*.*"
    $dialog.Title = "TV-Protokoll PDF auswählen (globaler Import)"
    $dialog.InitialDirectory = "F:\AuswertungPro"

    if ($dialog.ShowDialog() -ne [System.Windows.Forms.DialogResult]::OK) {
        return
    }

    try {
        if ($null -ne $script:lblStatus) {
            $script:lblStatus.Text = "PDF wird analysiert: $([System.IO.Path]::GetFileName($dialog.FileName))..."
        }
        $script:window.Dispatcher.Invoke([Action]{}, [System.Windows.Threading.DispatcherPriority]::Background)

        $result = ConvertFrom-PdfProtokoll -PdfPath $dialog.FileName
        if ($result.Haltungen.Count -eq 0) {
            [System.Windows.MessageBox]::Show("Keine Haltungen im PDF gefunden.", "Hinweis")
            if ($null -ne $script:lblStatus) { $script:lblStatus.Text = "Keine Haltungen gefunden" }
            return
        }

        $haltungData = @()
        foreach ($h in $result.Haltungen) {
            $data = Get-HaltungDataFromBlock -HaltungId $h.Id -Lines $h.Lines
            if ($data) { $haltungData += $data }
        }

        if ($haltungData.Count -eq 0) {
            [System.Windows.MessageBox]::Show("Keine verwertbaren Daten im PDF gefunden.", "Hinweis")
            if ($null -ne $script:lblStatus) { $script:lblStatus.Text = "Keine Daten gefunden" }
            return
        }

        $mergeResult = Merge-HaltungData -HaltungData $haltungData -ImportSource "PDF"
        $script:IsDirty = $true
        Update-WindowTitle

        $statusMsg = "PDF Import: $($result.Haltungen.Count) Haltungen | Neu: $($mergeResult.NewCount) | Aktualisiert: $($mergeResult.MergeCount) | Konflikte: $($mergeResult.ConflictCount)"
        if ($null -ne $script:lblStatus) { $script:lblStatus.Text = $statusMsg }
        [System.Windows.MessageBox]::Show($statusMsg, "PDF Import (global)")
    }
    catch {
        [System.Windows.MessageBox]::Show("Fehler beim PDF-Import:`n$($_.Exception.Message)", "Fehler")
        if ($null -ne $script:lblStatus) { $script:lblStatus.Text = "PDF Import fehlgeschlagen" }
    }
}

function Import-PdfForRowFromPath {
    param(
        [string]$RowId,
        [string]$PdfPath
    )
    
    $controls = $script:RowControls[$RowId]
    if (-not $controls) {
        [System.Windows.MessageBox]::Show("Zeile nicht gefunden.", "Fehler")
        return
    }
    
    try {
        if ($null -ne $script:lblStatus) {
            $script:lblStatus.Text = "PDF wird analysiert: $([System.IO.Path]::GetFileName($PdfPath))..."
        }
        $script:window.Dispatcher.Invoke([Action]{}, [System.Windows.Threading.DispatcherPriority]::Background)
        
        $result = ConvertFrom-PdfProtokoll -PdfPath $PdfPath
        
        if ($result.Haltungen.Count -eq 0) {
            [System.Windows.MessageBox]::Show("Keine Haltungen im PDF gefunden.", "Hinweis")
            if ($null -ne $script:lblStatus) { $script:lblStatus.Text = "Keine Haltungen gefunden" }
            return
        }
        
        # Auswahl bei mehreren Haltungen
        $selectedHaltung = $null
        if ($result.Haltungen.Count -eq 1) {
            $selectedHaltung = $result.Haltungen[0]
        }
        else {
            $selectWindow = New-Object System.Windows.Window
            $selectWindow.Title = "Haltung auswählen"
            $selectWindow.Width = 400
            $selectWindow.Height = 300
            $selectWindow.WindowStartupLocation = [System.Windows.WindowStartupLocation]::CenterOwner
            $selectWindow.Owner = $script:window
            
            $stack = New-Object System.Windows.Controls.StackPanel
            $stack.Margin = [System.Windows.Thickness]::new(15)
            
            $infoLabel = New-Object System.Windows.Controls.TextBlock
            $infoLabel.Text = "Das PDF enthält $($result.Haltungen.Count) Haltungen. Bitte wählen:"
            $infoLabel.Margin = [System.Windows.Thickness]::new(0, 0, 0, 10)
            $stack.Children.Add($infoLabel)
            
            $listBox = New-Object System.Windows.Controls.ListBox
            $listBox.Height = 180
            foreach ($h in $result.Haltungen) {
                $listBox.Items.Add($h.Id) | Out-Null
            }
            $listBox.SelectedIndex = 0
            $stack.Children.Add($listBox)
            
            $btnOk = New-Object System.Windows.Controls.Button
            $btnOk.Content = "Auswählen"
            $btnOk.Margin = [System.Windows.Thickness]::new(0, 10, 0, 0)
            $btnOk.Padding = [System.Windows.Thickness]::new(20, 8, 20, 8)
            $btnOk.Add_Click({ $selectWindow.DialogResult = $true; $selectWindow.Close() })
            $stack.Children.Add($btnOk)
            
            $selectWindow.Content = $stack
            
            if ($selectWindow.ShowDialog() -eq $true -and $listBox.SelectedIndex -ge 0) {
                $selectedHaltung = $result.Haltungen[$listBox.SelectedIndex]
            }
        }
        
        if ($null -eq $selectedHaltung) {
            if ($null -ne $script:lblStatus) { $script:lblStatus.Text = "Import abgebrochen" }
            return
        }
        
        # Daten extrahieren und befüllen (nur leere Felder, die nicht manuell editiert wurden)
        $data = Get-HaltungDataFromBlock -HaltungId $selectedHaltung.Id -Lines $selectedHaltung.Lines
        
        # Sicherstellen dass Tracking existiert
        if (-not $script:RowFieldSources.ContainsKey($RowId)) {
            $script:RowFieldSources[$RowId] = @{}
        }
        if (-not $script:RowUserEdited.ContainsKey($RowId)) {
            $script:RowUserEdited[$RowId] = @{}
        }
        
        $fieldSources = $script:RowFieldSources[$RowId]
        $userEdited = $script:RowUserEdited[$RowId]
        $filledCount = 0
        $skippedCount = 0
        
        foreach ($fieldDef in $script:FieldDefinitions) {
            $fieldName = $fieldDef.Name
            if (-not $data.ContainsKey($fieldName) -or [string]::IsNullOrWhiteSpace($data[$fieldName])) {
                continue
            }
            
            $control = $controls[$fieldName]
            $currentValue = ""
            
            if ($control -is [System.Windows.Controls.TextBox]) {
                $currentValue = $control.Text
            }
            elseif ($control -is [System.Windows.Controls.ComboBox]) {
                $currentValue = $control.Text
            }
            else {
                continue
            }
            
            # Prüfe ob Feld manuell editiert wurde
            $isUserEdited = $userEdited.ContainsKey($fieldName) -and $userEdited[$fieldName] -eq $true
            
            # Nur leere Felder füllen, die nicht manuell editiert wurden
            if ([string]::IsNullOrWhiteSpace($currentValue) -and -not $isUserEdited) {
                $script:IsProgrammaticChange = $true
                try {
                    if ($control -is [System.Windows.Controls.TextBox]) {
                        $control.Text = $data[$fieldName]
                    }
                    elseif ($control -is [System.Windows.Controls.ComboBox]) {
                        $control.Text = $data[$fieldName]
                    }
                }
                finally {
                    $script:IsProgrammaticChange = $false
                }
                $fieldSources[$fieldName] = "PDF"
                $filledCount++
            }
            elseif ($fieldName -eq "Primaere_Schaeden" -and -not [string]::IsNullOrWhiteSpace($data[$fieldName])) {
                # Schäden werden ergänzt
                $incoming = $data[$fieldName]
                if ($control -is [System.Windows.Controls.TextBox] -and $currentValue -notmatch [regex]::Escape($incoming)) {
                    $script:IsProgrammaticChange = $true
                    try {
                        $control.Text = ($currentValue.TrimEnd() + "`r`n" + $incoming)
                    }
                    finally {
                        $script:IsProgrammaticChange = $false
                    }
                    $filledCount++
                }
            }
            else {
                $skippedCount++
            }
        }
        
        # Titel aktualisieren
        foreach ($child in $script:rowContainer.Children) {
            if ($child.Tag -eq $RowId) {
                $mainStack = $child.Child
                foreach ($elem in $mainStack.Children) {
                    if ($elem -is [System.Windows.Controls.Grid]) {
                        foreach ($gridChild in $elem.Children) {
                            if ($gridChild -is [System.Windows.Controls.TextBlock] -and $gridChild.FontWeight -eq [System.Windows.FontWeights]::SemiBold) {
                                if (-not [string]::IsNullOrWhiteSpace($data.Haltungsname)) {
                                    $gridChild.Text = "Haltung: $($data.Haltungsname)"
                                }
                                break
                            }
                        }
                        break
                    }
                }
                break
            }
        }
        
        $script:IsDirty = $true
        Update-WindowTitle
        if ($null -ne $script:lblStatus) { $script:lblStatus.Text = "PDF Import: $filledCount Felder ergänzt, $skippedCount übersprungen" }
    }
    catch {
        [System.Windows.MessageBox]::Show("Fehler beim Import:`n$($_.Exception.Message)", "Fehler")
        if ($null -ne $script:lblStatus) { $script:lblStatus.Text = "Import fehlgeschlagen" }
    }
}

function Import-PdfForRow {
    param([string]$RowId)
    
    $controls = $script:RowControls[$RowId]
    if (-not $controls) {
        [System.Windows.MessageBox]::Show("Zeile nicht gefunden.", "Fehler")
        return
    }
    
    $dialog = New-Object System.Windows.Forms.OpenFileDialog
    $dialog.Filter = "PDF Dateien (*.pdf)|*.pdf|Alle Dateien (*.*)|*.*"
    $dialog.Title = "TV-Protokoll PDF auswählen"
    $dialog.InitialDirectory = "F:\AuswertungPro"
    
    if ($dialog.ShowDialog() -ne [System.Windows.Forms.DialogResult]::OK) {
        return
    }
    
    try {
        if ($null -ne $script:lblStatus) {
            $script:lblStatus.Text = "PDF wird analysiert..."
        }
        $script:window.Dispatcher.Invoke([Action]{}, [System.Windows.Threading.DispatcherPriority]::Background)
        
        $result = ConvertFrom-PdfProtokoll -PdfPath $dialog.FileName
        
        if ($result.Haltungen.Count -eq 0) {
            [System.Windows.MessageBox]::Show("Keine Haltungen im PDF gefunden.", "Hinweis")
            if ($null -ne $script:lblStatus) { $script:lblStatus.Text = "Keine Haltungen gefunden" }
            return
        }
        
        # Auswahl bei mehreren Haltungen
        $selectedHaltung = $null
        if ($result.Haltungen.Count -eq 1) {
            $selectedHaltung = $result.Haltungen[0]
        }
        else {
            $selectWindow = New-Object System.Windows.Window
            $selectWindow.Title = "Haltung auswählen"
            $selectWindow.Width = 400
            $selectWindow.Height = 300
            $selectWindow.WindowStartupLocation = [System.Windows.WindowStartupLocation]::CenterOwner
            $selectWindow.Owner = $script:window
            
            $stack = New-Object System.Windows.Controls.StackPanel
            $stack.Margin = [System.Windows.Thickness]::new(15)
            
            $infoLabel = New-Object System.Windows.Controls.TextBlock
            $infoLabel.Text = "Das PDF enthält $($result.Haltungen.Count) Haltungen. Bitte wählen:"
            $infoLabel.Margin = [System.Windows.Thickness]::new(0, 0, 0, 10)
            $stack.Children.Add($infoLabel)
            
            $listBox = New-Object System.Windows.Controls.ListBox
            $listBox.Height = 180
            foreach ($h in $result.Haltungen) {
                $listBox.Items.Add($h.Id) | Out-Null
            }
            $listBox.SelectedIndex = 0
            $stack.Children.Add($listBox)
            
            $btnOk = New-Object System.Windows.Controls.Button
            $btnOk.Content = "Auswählen"
            $btnOk.Margin = [System.Windows.Thickness]::new(0, 10, 0, 0)
            $btnOk.Padding = [System.Windows.Thickness]::new(20, 8, 20, 8)
            $btnOk.Add_Click({ $selectWindow.DialogResult = $true; $selectWindow.Close() })
            $stack.Children.Add($btnOk)
            
            $selectWindow.Content = $stack
            
            if ($selectWindow.ShowDialog() -eq $true -and $listBox.SelectedIndex -ge 0) {
                $selectedHaltung = $result.Haltungen[$listBox.SelectedIndex]
            }
        }
        
        if ($null -eq $selectedHaltung) {
            if ($null -ne $script:lblStatus) { $script:lblStatus.Text = "Import abgebrochen" }
            return
        }
        
        # DEBUG: Zeige was ausgewählt wurde
        Write-Host "DEBUG: Ausgewählte Haltung: $($selectedHaltung.Id), Lines: $($selectedHaltung.Lines.Count)"
        
        # Daten extrahieren und befüllen
        $data = Get-HaltungDataFromBlock -HaltungId $selectedHaltung.Id -Lines $selectedHaltung.Lines
        
        # DEBUG: Zeige extrahierte Daten
        Write-Host "DEBUG: Extrahierte Daten:"
        $data.GetEnumerator() | ForEach-Object { if ($_.Value) { Write-Host "  $($_.Key): $($_.Value)" } }
        
        # Sicherstellen dass Tracking existiert
        if (-not $script:RowFieldSources.ContainsKey($RowId)) {
            $script:RowFieldSources[$RowId] = @{}
        }
        if (-not $script:RowUserEdited.ContainsKey($RowId)) {
            $script:RowUserEdited[$RowId] = @{}
        }
        
        $fieldSources = $script:RowFieldSources[$RowId]
        $userEdited = $script:RowUserEdited[$RowId]
        $filledCount = 0
        $skippedCount = 0
        
        foreach ($fieldDef in $script:FieldDefinitions) {
            $fieldName = $fieldDef.Name
            if (-not $data.ContainsKey($fieldName) -or [string]::IsNullOrWhiteSpace($data[$fieldName])) {
                continue
            }
            
            $control = $controls[$fieldName]
            $currentValue = ""
            
            if ($control -is [System.Windows.Controls.TextBox]) {
                $currentValue = $control.Text
            }
            elseif ($control -is [System.Windows.Controls.ComboBox]) {
                $currentValue = $control.Text
            }
            else {
                continue
            }
            
            # Prüfe ob Feld manuell editiert wurde
            $isUserEdited = $userEdited.ContainsKey($fieldName) -and $userEdited[$fieldName] -eq $true
            
            # Nur leere Felder füllen, die nicht manuell editiert wurden
            if ([string]::IsNullOrWhiteSpace($currentValue) -and -not $isUserEdited) {
                $script:IsProgrammaticChange = $true
                try {
                    if ($control -is [System.Windows.Controls.TextBox]) {
                        $control.Text = $data[$fieldName]
                    }
                    elseif ($control -is [System.Windows.Controls.ComboBox]) {
                        $control.Text = $data[$fieldName]
                    }
                }
                finally {
                    $script:IsProgrammaticChange = $false
                }
                $fieldSources[$fieldName] = "PDF"
                $filledCount++
            }
            elseif ($fieldName -eq "Primaere_Schaeden" -and -not [string]::IsNullOrWhiteSpace($data[$fieldName])) {
                # Schäden werden ergänzt
                $incoming = $data[$fieldName]
                if ($control -is [System.Windows.Controls.TextBox] -and $currentValue -notmatch [regex]::Escape($incoming)) {
                    $script:IsProgrammaticChange = $true
                    try {
                        $control.Text = ($currentValue.TrimEnd() + "`r`n" + $incoming)
                    }
                    finally {
                        $script:IsProgrammaticChange = $false
                    }
                    $filledCount++
                }
            }
            else {
                $skippedCount++
            }
        }
        
        # Titel in der Zeile finden und aktualisieren
        foreach ($child in $script:rowContainer.Children) {
            if ($child.Tag -eq $RowId) {
                $mainStack = $child.Child
                foreach ($elem in $mainStack.Children) {
                    if ($elem -is [System.Windows.Controls.Grid]) {
                        foreach ($gridChild in $elem.Children) {
                            if ($gridChild -is [System.Windows.Controls.TextBlock] -and $gridChild.FontWeight -eq [System.Windows.FontWeights]::SemiBold) {
                                if (-not [string]::IsNullOrWhiteSpace($data.Haltungsname)) {
                                    $gridChild.Text = "Haltung: $($data.Haltungsname)"
                                }
                                break
                            }
                        }
                        break
                    }
                }
                break
            }
        }
        
        $script:IsDirty = $true
        Update-WindowTitle
        if ($null -ne $script:lblStatus) { $script:lblStatus.Text = "PDF Import: $filledCount Felder ergänzt, $skippedCount übersprungen" }
    }
    catch {
        [System.Windows.MessageBox]::Show("Fehler beim Import:`n$($_.Exception.Message)", "Fehler")
        if ($null -ne $script:lblStatus) { $script:lblStatus.Text = "Import fehlgeschlagen" }
    }
}

# ========== XTF Import UI ==========
function Import-XtfFile {
    param(
        [string]$DialogTitle = "XTF-Datei auswählen (SIA405 / VSA_KEK)",
        [ValidateSet("Any", "SIA405", "VSA_KEK")]
        [string]$RequiredSchema = "Any",
        [string]$ImportLabel = "XTF Import",
        [bool]$IsBasisImport = $true
    )
    $dialog = New-Object System.Windows.Forms.OpenFileDialog
    $dialog.Filter = "XTF Dateien (*.xtf)|*.xtf|Alle Dateien (*.*)|*.*"
    $dialog.Title = $DialogTitle
    
    # Startpfad: .\Rohdaten\ falls vorhanden
    if (Test-Path $script:RohdatenFolder) {
        $dialog.InitialDirectory = $script:RohdatenFolder
    } else {
        $dialog.InitialDirectory = $PSScriptRoot
    }
    
    if ($dialog.ShowDialog() -ne [System.Windows.Forms.DialogResult]::OK) {
        return
    }
    
    try {
        if ($null -ne $script:lblStatus) {
        $null = Set-TextSafe -Control $script:lblStatus -Value "$ImportLabel wird analysiert: $([System.IO.Path]::GetFileName($dialog.FileName))..." -Context "XTF:Status"
        }
        $script:window.Dispatcher.Invoke([Action]{}, [System.Windows.Threading.DispatcherPriority]::Background)
        
        $result = ConvertFrom-XtfFile -XtfPath $dialog.FileName
        
        if ($result.Haltungen.Count -eq 0) {
            [System.Windows.MessageBox]::Show("Keine Haltungen in der XTF-Datei gefunden.", "Hinweis")
            if ($null -ne $script:lblStatus) { $script:lblStatus.Text = "Keine Haltungen gefunden" }
            return
        }
        
        # Schema-Info ermitteln
        $schemaInfo = if ($result.IsSIA405) { "SIA405" } elseif ($result.IsVSA) { "VSA_KEK" } else { "Unbekannt" }
        if ($RequiredSchema -eq "SIA405" -and -not $result.IsSIA405) {
            [System.Windows.MessageBox]::Show("Die Datei ist keine SIA405-XTF.`nErkanntes Schema: $schemaInfo", "Hinweis")
            if ($null -ne $script:lblStatus) { $script:lblStatus.Text = "Import abgebrochen" }
            return
        }
        if ($RequiredSchema -eq "VSA_KEK" -and -not $result.IsVSA) {
            [System.Windows.MessageBox]::Show("Die Datei ist keine VSA_KEK-XTF.`nErkanntes Schema: $schemaInfo", "Hinweis")
            if ($null -ne $script:lblStatus) { $script:lblStatus.Text = "Import abgebrochen" }
            return
        }
        
        # Bestätigungsdialog
        $confirmResult = [System.Windows.MessageBox]::Show(
            "Datei: $($result.FileName)`nSchema: $schemaInfo`n`n$($result.Haltungen.Count) Haltungen gefunden.`n`nAlle importieren?`n`n(Die Reihenfolge kann nachträglich mit den ▲/▼ Buttons angepasst werden)",
            $ImportLabel,
            [System.Windows.MessageBoxButton]::YesNo,
            [System.Windows.MessageBoxImage]::Question)
        
        if ($confirmResult -ne [System.Windows.MessageBoxResult]::Yes) {
            if ($null -ne $script:lblStatus) { $script:lblStatus.Text = "Import abgebrochen" }
            return
        }
        
        # Prüfen ob nur leere Haltungen vorhanden sind
        $hasOnlyEmptyRows = $true
        foreach ($rowId in $script:RowControls.Keys) {
            $controls = $script:RowControls[$rowId]
            $nameControl = $controls["Haltungsname"]
            $nameValue = ""
            if ($nameControl -is [System.Windows.Controls.TextBox]) {
                $nameValue = $nameControl.Text
            } elseif ($nameControl -is [System.Windows.Controls.ComboBox]) {
                $nameValue = $nameControl.Text
            }
            if (-not [string]::IsNullOrWhiteSpace($nameValue)) {
                $hasOnlyEmptyRows = $false
                break
            }
        }
        
        # Bestehende Haltungen behandeln
        if ($script:rowContainer.Children.Count -gt 0) {
            if ($hasOnlyEmptyRows) {
                # Nur leere Haltungen - automatisch löschen ohne Nachfrage
                $script:rowContainer.Children.Clear()
                $script:RowControls.Clear()
                $script:RowCounter = 0
            } else {
                $clearResult = [System.Windows.MessageBox]::Show(
                    "Es sind bereits $($script:rowContainer.Children.Count) Haltung(en) mit Daten vorhanden.`n`nJa = löschen`nNein = ergänzen`nAbbrechen = Import abbrechen",
                    "Bestehende Haltungen",
                    [System.Windows.MessageBoxButton]::YesNoCancel,
                    [System.Windows.MessageBoxImage]::Question)
                
                if ($clearResult -eq [System.Windows.MessageBoxResult]::Cancel) {
                    if ($null -ne $script:lblStatus) { $script:lblStatus.Text = "Import abgebrochen" }
                    return
                }
                
                if ($clearResult -eq [System.Windows.MessageBoxResult]::Yes) {
                    $script:rowContainer.Children.Clear()
                    $script:RowControls.Clear()
                    $script:RowFieldSources.Clear()
                    $script:RowUserEdited.Clear()
                    $script:RowCounter = 0
                }
            }
        }
        
        # Haltungen nach Bezeichnung sortieren
        $sortedHaltungen = $result.Haltungen | Sort-Object { $_.Haltungsname }
        
        # Import-Quelle für Merge bestimmen
        $importSource = if ($IsBasisImport) { "XTF" } elseif ($result.IsSIA405) { "SIA405" } else { "XTF" }
        
        $mergeResult = Merge-XtfHaltungen -Haltungen $sortedHaltungen -ImportSource $importSource
        
        # Basis-Import-Flag setzen
        if ($IsBasisImport) {
            $script:HasXtfBasisImport = $true
        }
        
        $script:IsDirty = $true
        Update-WindowTitle
        Update-HaltungCount
        
        # Statusmeldung erstellen
        $statusMsg = "${ImportLabel}: $($result.FileName) | Neu: $($mergeResult.NewCount) | Aktualisiert: $($mergeResult.MergeCount)"
        if ($mergeResult.ConflictCount -gt 0) {
            $statusMsg += " | Konflikte: $($mergeResult.ConflictCount)"
        }
        if ($null -ne $script:lblStatus) { $script:lblStatus.Text = $statusMsg }
        
        # Ergebnis-Dialog
        $resultMsg = "Importierte Datei: $($result.FileName)`n`n"
        $resultMsg += "Haltungen neu: $($mergeResult.NewCount)`n"
        $resultMsg += "Haltungen aktualisiert: $($mergeResult.MergeCount)`n"
        if ($mergeResult.ConflictCount -gt 0) {
            $resultMsg += "Konflikte (nicht ueberschrieben): $($mergeResult.ConflictCount)`n`n"
            $resultMsg += "Konfliktlog gespeichert unter:`n$($script:LogFolder)\import_conflicts.log"
        }
        [System.Windows.MessageBox]::Show($resultMsg, $ImportLabel)
    }
    catch {
        [System.Windows.MessageBox]::Show("Fehler beim XTF-Import:`n$($_.Exception.Message)", "Fehler")
        if ($null -ne $script:lblStatus) { $script:lblStatus.Text = "$ImportLabel fehlgeschlagen" }
    }
}

# ========== Export ==========
function Export-ToExcel {
    $dialog = New-Object System.Windows.Forms.SaveFileDialog
    $dialog.Filter = "Excel Dateien (*.xlsx)|*.xlsx"
    $dialog.FileName = "$($script:txtProjectName.Text)_$(Get-Date -Format 'yyyyMMdd').xlsx"
    $dialog.InitialDirectory = "F:\AuswertungPro"
    
    if ($dialog.ShowDialog() -ne [System.Windows.Forms.DialogResult]::OK) { return }
    
    try {
        $excel = New-Object -ComObject Excel.Application
        $excel.Visible = $false
        $excel.DisplayAlerts = $false
        $wb = $excel.Workbooks.Add()
        $ws = $wb.Worksheets.Item(1)
        $ws.Name = "Haltungen"
        
        # Header
        $col = 1
        foreach ($fieldDef in $script:FieldDefinitions) {
            $ws.Cells.Item(1, $col) = $fieldDef.Label
            $ws.Cells.Item(1, $col).Font.Bold = $true
            $ws.Cells.Item(1, $col).Interior.Color = 0xD0D0D0
            $col++
        }
        
        # Daten
        $rowIndex = 2
        foreach ($rowId in $script:RowControls.Keys) {
            $controls = $script:RowControls[$rowId]
            $col = 1
            foreach ($fieldDef in $script:FieldDefinitions) {
                $control = $controls[$fieldDef.Name]
                $value = ""
                if ($control -is [System.Windows.Controls.TextBox]) {
                    $value = $control.Text
                }
                elseif ($control -is [System.Windows.Controls.ComboBox]) {
                    $value = $control.Text
                }
                $ws.Cells.Item($rowIndex, $col) = $value
                $col++
            }
            $rowIndex++
        }
        
        $ws.Columns.AutoFit() | Out-Null
        $wb.SaveAs($dialog.FileName)
        $wb.Close($false)
        $excel.Quit()
        [System.Runtime.InteropServices.Marshal]::ReleaseComObject($excel) | Out-Null
        
        $script:lblStatus.Text = "Export erfolgreich: $($dialog.FileName)"
        [System.Windows.MessageBox]::Show("Export erfolgreich!", "Erfolg")
    }
    catch {
        [System.Windows.MessageBox]::Show("Export fehlgeschlagen:`n$($_.Exception.Message)", "Fehler")
    }
}

# ========== Events ==========
$script:btnAdd.Add_Click({
    Add-HaltungRow | Out-Null
    $script:IsDirty = $true
    Update-WindowTitle
    $script:lblStatus.Text = "Neue Haltung hinzugefügt"
})

$script:btnImportXtf.Add_Click({
    Import-XtfFile -DialogTitle "XTF-Datei auswählen (Basis)" -RequiredSchema "Any" -ImportLabel "XTF Basis-Import" -IsBasisImport $true
})

$script:btnImportXtfSia405.Add_Click({
    Import-XtfFile -DialogTitle "SIA405-XTF auswählen (Ergänzung)" -RequiredSchema "SIA405" -ImportLabel "SIA405 Ergänzung" -IsBasisImport $false
})

$script:btnImportPdfGlobal.Add_Click({
    Import-GlobalPdf
})

$script:btnExport.Add_Click({
    if ($script:rowContainer.Children.Count -eq 0) {
        [System.Windows.MessageBox]::Show("Keine Haltungen zum Exportieren.", "Hinweis")
        return
    }
    Export-ToExcel
})

$script:btnNewProject.Add_Click({
    if ($script:IsDirty) {
        $result = [System.Windows.MessageBox]::Show("Ungespeicherte Änderungen. Trotzdem neues Projekt?", "Warnung", [System.Windows.MessageBoxButton]::YesNo)
        if ($result -ne [System.Windows.MessageBoxResult]::Yes) { return }
    }
    
    $script:rowContainer.Children.Clear()
    $script:RowControls.Clear()
    $script:RowCounter = 0
    $script:CurrentProject = New-Project
    $script:CurrentProjectPath = ""
    $script:txtProjectName.Text = "Neues Projekt"
    $script:txtZone.Text = ""
    $script:txtFirma.Text = ""
    $script:txtBearbeiter.Text = ""
    $script:IsDirty = $false
    Update-WindowTitle
    Update-HaltungCount
    Add-HaltungRow | Out-Null
    $script:lblStatus.Text = "Neues Projekt erstellt"
})

$script:btnSaveProject.Add_Click({
    if (-not $script:CurrentProjectPath) {
        # Speichern unter...
        $dialog = New-Object System.Windows.Forms.SaveFileDialog
        $dialog.Filter = "Haltungs-Projekt (*.haltproj)|*.haltproj"
        $dialog.FileName = $script:txtProjectName.Text
        $dialog.InitialDirectory = $script:ProjectFolder
        
        if ($dialog.ShowDialog() -ne [System.Windows.Forms.DialogResult]::OK) { return }
        $script:CurrentProjectPath = $dialog.FileName
    }
    
    Save-Project -Path $script:CurrentProjectPath
    Update-ProjectList
    $script:lblStatus.Text = "Projekt gespeichert"
})

$script:btnSaveAs.Add_Click({
    $dialog = New-Object System.Windows.Forms.SaveFileDialog
    $dialog.Filter = "Haltungs-Projekt (*.haltproj)|*.haltproj"
    $dialog.FileName = $script:txtProjectName.Text
    $dialog.InitialDirectory = $script:ProjectFolder
    
    if ($dialog.ShowDialog() -eq [System.Windows.Forms.DialogResult]::OK) {
        Save-Project -Path $dialog.FileName
        Update-ProjectList
        $script:lblStatus.Text = "Projekt gespeichert unter: $($script:CurrentProjectPath)"
    }
})

$script:btnOpenProject.Add_Click({
    if ($script:projectList.SelectedItem -eq $null) {
        [System.Windows.MessageBox]::Show("Bitte wählen Sie ein Projekt aus der Liste.", "Hinweis")
        return
    }
    
    if ($script:IsDirty) {
        $result = [System.Windows.MessageBox]::Show("Ungespeicherte Änderungen. Trotzdem öffnen?", "Warnung", [System.Windows.MessageBoxButton]::YesNo)
        if ($result -ne [System.Windows.MessageBoxResult]::Yes) { return }
    }
    
    $path = $script:projectList.SelectedItem.Tag
    Open-Project -Path $path
    $script:lblStatus.Text = "Projekt geladen"
})

$script:projectList.Add_MouseDoubleClick({
    if ($script:projectList.SelectedItem -ne $null) {
        $script:btnOpenProject.RaiseEvent((New-Object System.Windows.RoutedEventArgs([System.Windows.Controls.Primitives.ButtonBase]::ClickEvent)))
    }
})

$script:btnDeleteProject.Add_Click({
    if ($script:projectList.SelectedItem -eq $null) {
        [System.Windows.MessageBox]::Show("Bitte wählen Sie ein Projekt aus der Liste.", "Hinweis")
        return
    }
    
    $name = $script:projectList.SelectedItem.Content
    $result = [System.Windows.MessageBox]::Show("Projekt '$name' wirklich löschen?", "Bestätigung", [System.Windows.MessageBoxButton]::YesNo)
    if ($result -eq [System.Windows.MessageBoxResult]::Yes) {
        $path = $script:projectList.SelectedItem.Tag
        Remove-Item -Path $path -Force
        Update-ProjectList
        $script:lblStatus.Text = "Projekt gelöscht"
    }
})

# Metadaten-Änderungen tracken
$script:txtProjectName.Add_TextChanged({ $script:IsDirty = $true; Update-WindowTitle })
$script:txtZone.Add_TextChanged({ $script:IsDirty = $true; Update-WindowTitle })
$script:txtFirma.Add_TextChanged({ $script:IsDirty = $true; Update-WindowTitle })
$script:txtBearbeiter.Add_TextChanged({ $script:IsDirty = $true; Update-WindowTitle })

# Fenster-Schließen abfangen
$script:window.Add_Closing({
    param($senderObj, $eventArgs)
    if ($script:IsDirty) {
        $result = [System.Windows.MessageBox]::Show("Ungespeicherte Änderungen. Wirklich beenden?", "Warnung", [System.Windows.MessageBoxButton]::YesNo)
        if ($result -ne [System.Windows.MessageBoxResult]::Yes) {
            $eventArgs.Cancel = $true
        }
    }
})

# ========== Start ==========
Update-ProjectList
Add-HaltungRow | Out-Null
$script:IsDirty = $false
Update-WindowTitle

$script:window.ShowDialog() | Out-Null
