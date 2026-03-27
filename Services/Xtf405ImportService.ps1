<#
.SYNOPSIS
    Xtf405ImportService für AuswertungPro - XTF SIA405 Parsing
.DESCRIPTION
    Spezialisiert auf SIA405-XTF mit höherer Priorität als Standard-XTF
    Mappt erweiterte Felder aus VSA_KEK Schema
#>

# ========== XTF SIA405 Field Mapping ==========
# Erweiterte Felder mit besserer Datenqualität
$script:Xtf405FieldMapping = @{
    'Haltungsname' = @{ 
        XPaths = @(
            './/Haltung/@NAME'
            './/Haltung/@Bezeichnung'
            './/Haltung/@Nummer'
        )
        Type = 'Attribute'
    }
    'Strasse' = @{
        XPaths = @(
            './/Lage/Strasse'
            './/Adresse/Strasse'
        )
        Type = 'Element'
    }
    'Rohrmaterial' = @{
        XPaths = @(
            './/Haltung/Rohrmaterial'
            './/Profilelement/Material'
        )
        Type = 'Element'
    }
    'DN_mm' = @{
        XPaths = @(
            './/Haltung/Nennweite'
            './/Haltung/ProfilDurchmesser'
            './/Profilelement/Durchmesser'
        )
        Type = 'Element'
    }
    'Nutzungsart' = @{
        XPaths = @(
            './/Haltung/Zweck'
            './/Haltung/Nutzungsart'
        )
        Type = 'Element'
    }
    'Haltungslaenge_m' = @{
        XPaths = @(
            './/Haltung/Laenge'
            './/Haltung/Haenge'
        )
        Type = 'Element'
    }
    'Fliessrichtung' = @{
        XPaths = @(
            './/Haltung/Fliessrichtung'
            './/Haltung/Stromrichtung'
        )
        Type = 'Element'
    }
    'Eigentuemer' = @{
        XPaths = @(
            './/Objekt/Eigentuemer'
            './/Owner/Name'
        )
        Type = 'Element'
    }
}

# ========== SIA405-spezifisches Parsing ==========
<#
.SYNOPSIS
    Parse-Xtf405File: Parst SIA405-XTF-Datei mit erweiterten Feldern
#>
function Parse-Xtf405File {
    param([string] $XtfPath)
    
    $result = @{
        Haltungen = @()
        IsSIA405 = $true
        IsVSA = $true
        FileName = (Split-Path $XtfPath -Leaf)
        Error = ""
    }
    
    try {
        if (-not (Test-Path $XtfPath)) {
            throw "XTF-Datei nicht gefunden: $XtfPath"
        }
        
        [xml]$xml = Get-Content -Path $XtfPath -Encoding UTF8
        
        # Finde Haltungs-Objekte mit erweiterten XPaths
        $haltungElements = $xml.SelectNodes('.//Haltung')
        
        foreach ($hElement in $haltungElements) {
            $haltung = @{
                Id = ""
                Felder = @{}
            }
            
            # Extrahiere Felder mit mehreren XPath-Optionen
            foreach ($fieldName in $script:Xtf405FieldMapping.Keys) {
                $mapping = $script:Xtf405FieldMapping[$fieldName]
                $value = ""
                
                foreach ($xpath in $mapping.XPaths) {
                    try {
                        if ($mapping.Type -eq 'Attribute') {
                            $node = $hElement.SelectSingleNode($xpath)
                            if ($node) {
                                $value = $node.Value
                                break
                            }
                        } else {
                            $element = $hElement.SelectSingleNode($xpath)
                            if ($element) {
                                $value = $element.InnerText
                                break
                            }
                        }
                    } catch {
                        # Try next XPath
                    }
                }
                
                if (-not [string]::IsNullOrWhiteSpace($value)) {
                    $haltung.Felder[$fieldName] = $value.Trim()
                }
            }
            
            # Versuche ID zu extrahieren
            $haltung.Id = if ($haltung.Felder['Haltungsname']) { $haltung.Felder['Haltungsname'] } else { "Unbekannt" }
            
            if (-not [string]::IsNullOrWhiteSpace($haltung.Id)) {
                $result.Haltungen += $haltung
            }
        }
        
        Log-Info -Message "SIA405-XTF geparst: $($result.Haltungen.Count) Haltungen gefunden" -Context "Xtf405Import"
    } catch {
        $result.Error = $_.Exception.Message
        Log-Error -Message "Fehler beim SIA405-XTF-Parsing: $_" -Context "Xtf405Import" -Exception $_
    }
    
    return $result
}

# ========== Import Integration ==========
<#
.SYNOPSIS
    Import-Xtf405RecordsToProject: Importiert SIA405-XTF-Records (höhere Priorität)
.PARAMETER Project
    Projekt
.PARAMETER Xtf405Records
    Array von XTF405-Records
.RETURNS
    Statistik-Objekt
#>
function Import-Xtf405RecordsToProject {
    param(
        [Project] $Project,
        [object[]] $Xtf405Records
    )
    
    # SIA405 hat Priorität = "xtf405" statt "xtf"
    return Import-XtfRecordsToProject -Project $Project -XtfRecords $Xtf405Records -ImportSource "xtf405"
}

Write-Host "[Xtf405ImportService] Loaded - Priorität über Standard-XTF" -ForegroundColor Green
