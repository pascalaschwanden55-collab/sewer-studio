<#
.SYNOPSIS
    XtfImportService für AuswertungPro - XTF/Interlis Parsing
.DESCRIPTION
    Parst VSA_KEK XTF (Untersuchungen/Schaeden) und SIA405 XTF (Haltungen/Kanaele)
    und merged beide zu vollstaendigen Haltungs-Records
#>

# ========== VSA_KEK Parsing (Untersuchungen + Schaeden) ==========
function Parse-VsaKekXtf {
    param([xml] $Xml)
    
    $untersuchungen = @{}
    $schaeden = @{}
    
    # Hilfsfunktion: SelectSingleNode mit local-name() (wegen XML-Namespaces)
    function Get-NodeText($node, $elementName) {
        $found = $node.SelectSingleNode("*[local-name()='$elementName']")
        if ($found) { return $found.InnerText }
        return ""
    }
    
    # Finde alle Untersuchung-Elemente (verschiedene Namenskonventionen)
    $untersuchungNodes = $Xml.SelectNodes('//*[contains(local-name(), "Untersuchung")]')
    
    foreach ($node in $untersuchungNodes) {
        $tid = $node.GetAttribute("TID")
        if (-not $tid) { continue }
        
        $untersuchungen[$tid] = @{
            TID = $tid
            Bezeichnung = Get-NodeText $node 'Bezeichnung'
            Ausfuehrender = Get-NodeText $node 'Ausfuehrender'
            Zeitpunkt = Get-NodeText $node 'Zeitpunkt'
            InspizierteLaenge = Get-NodeText $node 'Inspizierte_Laenge'
            Erfassungsart = Get-NodeText $node 'Erfassungsart'
            Fahrzeug = Get-NodeText $node 'Fahrzeug'
            Geraet = Get-NodeText $node 'Geraet'
            Witterung = Get-NodeText $node 'Witterung'
            Grund = Get-NodeText $node 'Grund'
            VonPunkt = Get-NodeText $node 'vonPunktBezeichnung'
            BisPunkt = Get-NodeText $node 'bisPunktBezeichnung'
            Schaeden = @()
        }
    }
    
    # Finde alle Kanalschaden-Elemente
    $schadenNodes = $Xml.SelectNodes('//*[contains(local-name(), "Kanalschaden")]')
    
    foreach ($node in $schadenNodes) {
        $untersuchungRef = $node.SelectSingleNode("*[local-name()='UntersuchungRef']")
        if (-not $untersuchungRef) { continue }
        
        $refTid = $untersuchungRef.GetAttribute("REF")
        if (-not $refTid -or -not $untersuchungen.ContainsKey($refTid)) { continue }
        
        $schaden = @{
            Schadencode = Get-NodeText $node 'KanalSchadencode'
            Distanz = Get-NodeText $node 'Distanz'
            Anmerkung = Get-NodeText $node 'Anmerkung'
            Einzelschadenklasse = Get-NodeText $node 'Einzelschadenklasse'
            Streckenschaden = Get-NodeText $node 'Streckenschaden'
            Quantifizierung1 = Get-NodeText $node 'Quantifizierung1'
            Quantifizierung2 = Get-NodeText $node 'Quantifizierung2'
            SchadenlageAnfang = Get-NodeText $node 'SchadenlageAnfang'
            SchadenlageEnde = Get-NodeText $node 'SchadenlageEnde'
        }
        
        # Berechne LL (Schadenslänge) für Streckenschäden
        $ll = 0.0
        if ($schaden['Streckenschaden'] -eq 'true') {
            # Versuche Länge aus SchadenlageAnfang/Ende
            $anfang = $schaden['SchadenlageAnfang']
            $ende = $schaden['SchadenlageEnde']
            if ($anfang -and $ende) {
                $anf = [double]$anfang
                $end = [double]$ende
                if ($end -gt $anf) { $ll = $end - $anf }
            } elseif ($schaden['Quantifizierung1']) {
                $ll = [double]$schaden['Quantifizierung1']
            }
        }
        $schaden['LL'] = $ll
        
        $untersuchungen[$refTid].Schaeden += $schaden
    }
    
    return $untersuchungen
}

# ========== SIA405 Parsing (Haltungen + Kanaele) ==========
function Parse-Sia405Xtf {
    param([xml] $Xml)
    
    $kanaele = @{}
    $kanaeleByBezeichnung = @{}  # Zusaetzlicher Index fuer Verknuepfung
    $haltungen = @{}
    
    # Parse Kanal-Elemente (SIA405_ABWASSER_2020_LV95.SIA405_Abwasser.Kanal)
    $allNodes = $Xml.SelectNodes('//*')
    
    foreach ($node in $allNodes) {
        $nodeName = $node.Name
        $localName = $node.LocalName
        
        # Kanal-Elemente (enden auf .Kanal)
        if ($nodeName -match '\.Kanal$' -or $localName -eq 'Kanal') {
            $tid = $node.GetAttribute("TID")
            if (-not $tid) { continue }
            
            # Lese Kindelemente direkt via ChildNodes
            $kanalData = @{
                TID = $tid
                Bezeichnung = ""
                Standortname = ""
                Status = ""
                Nutzungsart = ""
                Bemerkung = ""
                Zugaenglichkeit = ""
                Eigentuemer = ""
                Baujahr = ""
                Rohrlaenge = ""
            }
            
            foreach ($child in $node.ChildNodes) {
                if ($child.NodeType -eq 'Element') {
                    switch ($child.LocalName) {
                        'Bezeichnung'      { $kanalData.Bezeichnung = $child.InnerText }
                        'Standortname'     { $kanalData.Standortname = $child.InnerText }
                        'Status'           { $kanalData.Status = $child.InnerText }
                        'Nutzungsart_Ist'  { $kanalData.Nutzungsart = $child.InnerText }
                        'Bemerkung'        { $kanalData.Bemerkung = $child.InnerText }
                        'Zugaenglichkeit'  { $kanalData.Zugaenglichkeit = $child.InnerText }
                        'Eigentuemer'      { $kanalData.Eigentuemer = $child.InnerText }
                        'Baujahr'          { $kanalData.Baujahr = $child.InnerText }
                        'Rohrlaenge'       { $kanalData.Rohrlaenge = $child.InnerText }
                    }
                }
            }
            
            $kanaele[$tid] = $kanalData
            # Index nach Bezeichnung fuer spaetere Verknuepfung
            if ($kanalData['Bezeichnung']) {
                $kanaeleByBezeichnung[$kanalData['Bezeichnung']] = $kanalData
            }
        }
        
        # Haltung-Elemente (enden auf .Haltung, aber NICHT Haltungspunkt)
        if (($nodeName -match '\.Haltung$' -or $localName -eq 'Haltung') -and $nodeName -notmatch 'Haltungspunkt') {
            $tid = $node.GetAttribute("TID")
            if (-not $tid) { continue }
            
            # Lese Kindelemente direkt via ChildNodes
            $haltungData = @{
                TID = $tid
                Bezeichnung = ""
                Laenge = ""
                LichteHoehe = ""
                LichteBreite = ""
                Material = ""
                KanalRef = ""
                VonRef = ""
                NachRef = ""
                LetzteAenderung = ""
            }
            
            foreach ($child in $node.ChildNodes) {
                if ($child.NodeType -eq 'Element') {
                    switch ($child.LocalName) {
                        'Bezeichnung'         { $haltungData.Bezeichnung = $child.InnerText }
                        'LaengeEffektiv'      { $haltungData.Laenge = $child.InnerText }
                        'Lichte_Hoehe'        { $haltungData.LichteHoehe = $child.InnerText }
                        'Lichte_Breite'       { $haltungData.LichteBreite = $child.InnerText }
                        'Material'            { $haltungData.Material = $child.InnerText }
                        'Letzte_Aenderung'    { $haltungData.LetzteAenderung = $child.InnerText }
                        'AbwasserbauwerkRef'  { 
                            $ref = $child.GetAttribute("REF")
                            if ($ref) { $haltungData.KanalRef = $ref }
                        }
                        'vonHaltungspunktRef' { 
                            $ref = $child.GetAttribute("REF")
                            if ($ref) { $haltungData.VonRef = $ref }
                        }
                        'nachHaltungspunktRef' { 
                            $ref = $child.GetAttribute("REF")
                            if ($ref) { $haltungData.NachRef = $ref }
                        }
                    }
                }
            }
            
            $haltungen[$tid] = $haltungData
        }
    }
    
    Log-Info -Message "SIA405 geparst: $($haltungen.Count) Haltungen, $($kanaele.Count) Kanaele" -Context "XtfImport"
    
    return @{
        Kanaele = $kanaele
        KanaeleByBezeichnung = $kanaeleByBezeichnung
        Haltungen = $haltungen
    }
}

# ========== Haupt-Parse-Funktion ==========
function Parse-XtfFile {
    param([string] $XtfPath)
    
    $result = @{
        Haltungen = @()
        IsSIA405 = $false
        IsVSA = $false
        FileName = (Split-Path $XtfPath -Leaf)
        Error = ""
    }
    
    try {
        if (-not (Test-Path $XtfPath)) {
            throw "XTF-Datei nicht gefunden: $XtfPath"
        }
        
        # Lade XML
        [xml]$xml = Get-Content -Path $XtfPath -Encoding UTF8
        
        # Erkenne Schema anhand XML-Inhalt
        $xmlContent = $xml.OuterXml
        if ($xmlContent -match 'SIA405') {
            $result.IsSIA405 = $true
        }
        if ($xmlContent -match 'VSA_KEK') {
            $result.IsVSA = $true
        }
        
        # Parse basierend auf Schema
        if ($result.IsSIA405) {
            # SIA405 XTF: Haltungen + Kanaele
            $sia405Data = Parse-Sia405Xtf -Xml $xml
            
            foreach ($haltungTid in $sia405Data.Haltungen.Keys) {
                $haltungData = $sia405Data.Haltungen[$haltungTid]
                
                # Finde zugehoerigen Kanal (erst via REF, dann via Bezeichnung)
                $kanalData = $null
                if ($haltungData['KanalRef'] -and $sia405Data.Kanaele.ContainsKey($haltungData['KanalRef'])) {
                    $kanalData = $sia405Data.Kanaele[$haltungData['KanalRef']]
                } elseif ($haltungData['Bezeichnung'] -and $sia405Data.KanaeleByBezeichnung.ContainsKey($haltungData['Bezeichnung'])) {
                    # Fallback: Verknuepfung ueber gleiche Bezeichnung
                    $kanalData = $sia405Data.KanaeleByBezeichnung[$haltungData['Bezeichnung']]
                }
                
                # Material-Mapping (z.B. "Kunststoff_Hartpolyethylen" -> "Kunststoff PE-HD")
                $material = $haltungData['Material']
                if ($material) {
                    switch -Regex ($material) {
                        'Kunststoff_Hartpolyethylen' { $material = 'Kunststoff PE-HD' }
                        'Kunststoff_Polyethylen'     { $material = 'Kunststoff PE' }
                        'Kunststoff_Polyvinylchlorid'{ $material = 'Kunststoff PVC' }
                        'Beton_Normalbeton'          { $material = 'Beton' }
                        'Beton_.*'                   { $material = 'Beton' }
                        'Steinzeug'                  { $material = 'Steinzeug' }
                        default {
                            $material = $material -replace '_', ' '
                            if ($material.Length -gt 0) {
                                $material = $material.Substring(0,1).ToUpper() + $material.Substring(1)
                            }
                        }
                    }
                }
                
                # Nutzungsart-Mapping
                $nutzungsart = ""
                if ($kanalData -and $kanalData['Nutzungsart']) {
                    switch -Regex ($kanalData['Nutzungsart']) {
                        'Schmutzabwasser' { $nutzungsart = 'Schmutzwasser' }
                        'Regenabwasser'   { $nutzungsart = 'Regenwasser' }
                        'Mischabwasser'   { $nutzungsart = 'Mischabwasser' }
                        default           { $nutzungsart = $kanalData['Nutzungsart'] }
                    }
                }
                
                $felder = @{
                    'Haltungsname' = $haltungData['Bezeichnung']
                    'Haltungslaenge_m' = $haltungData['Laenge']
                    'Rohrmaterial' = $material
                }
                
                # DN_mm: Verwende LichteHoehe, oder LichteBreite falls Hoehe fehlt
                if ($haltungData['LichteHoehe']) {
                    $felder['DN_mm'] = $haltungData['LichteHoehe']
                } elseif ($haltungData['LichteBreite']) {
                    $felder['DN_mm'] = $haltungData['LichteBreite']
                }
                
                # Fliessrichtung aus Von/Nach-Referenzen
                if ($haltungData['VonRef'] -or $haltungData['NachRef']) {
                    $von = if ($haltungData['VonRef']) { $haltungData['VonRef'] } else { "?" }
                    $nach = if ($haltungData['NachRef']) { $haltungData['NachRef'] } else { "?" }
                    $felder['Fliessrichtung'] = "Von $von nach $nach"
                }
                
                # Datum aus LetzteAenderung (YYYYMMDD -> DD.MM.YYYY)
                if ($haltungData['LetzteAenderung'] -match '^(\d{4})(\d{2})(\d{2})$') {
                    $felder['Datum_Jahr'] = "$($matches[3]).$($matches[2]).$($matches[1])"
                }
                if ($kanalData) {
                    $felder['Strasse'] = $kanalData['Standortname']
                    $felder['Nutzungsart'] = $nutzungsart
                    if ($kanalData['Bemerkung']) {
                        $felder['Bemerkungen'] = $kanalData['Bemerkung']
                    }
                    # Eigentuemer
                    if ($kanalData['Eigentuemer']) {
                        $felder['Eigentuemer'] = $kanalData['Eigentuemer']
                    }
                    # Baujahr -> Datum_Jahr (falls noch leer)
                    if ($kanalData['Baujahr'] -and -not $felder.ContainsKey('Datum_Jahr')) {
                        $felder['Datum_Jahr'] = $kanalData['Baujahr']
                    }
                    # Status -> offen/abgeschlossen
                    if ($kanalData['Status']) {
                        if ($kanalData['Status'] -match 'in_Betrieb|aktiv') {
                            $felder['Offen_abgeschlossen'] = 'abgeschlossen'
                        } elseif ($kanalData['Status'] -match 'ausser_Betrieb|stillgelegt') {
                            $felder['Offen_abgeschlossen'] = 'offen'
                        }
                    }
                    # Zugaenglichkeit als Bemerkung ergaenzen
                    if ($kanalData['Zugaenglichkeit'] -and $kanalData['Zugaenglichkeit'] -ne 'unbekannt') {
                        if ($felder.ContainsKey('Bemerkungen') -and $felder['Bemerkungen']) {
                            $felder['Bemerkungen'] += "`nZugaenglichkeit: $($kanalData['Zugaenglichkeit'])"
                        } else {
                            $felder['Bemerkungen'] = "Zugaenglichkeit: $($kanalData['Zugaenglichkeit'])"
                        }
                    }
                }
                
                # === VSA-Rili Zustandsbeurteilung (Dichtheit, Beispiel) ===
                # VSA-Rili-Berechnung nur, wenn Schaeden-Liste existiert (SIA405 hat meist keine)
                try {
                    Import-Module -Name (Join-Path $PSScriptRoot 'VsaRiliZustandService.ps1') -Force -ErrorAction Stop
                    if ($haltungData.ContainsKey('Schaeden') -and $haltungData['Schaeden'] -and $haltungData['Schaeden'].Count -gt 0) {
                        $findings = ConvertTo-VsaFindings -Schaeden $haltungData['Schaeden'] -Kriterium 'D'
                        $laenge = if ($haltungData['Laenge']) { [double]$haltungData['Laenge'] } else { 0.0 }
                        $zn = Compute-VsaZustandsnote -SchadensListe $findings -Haltungslaenge $laenge
                        if ($zn) { $felder['VSA_Zustandsnote_D'] = $zn }
                    }
                } catch {
                    $felder['VSA_Zustandsnote_D'] = "Fehler: $_"
                }
                
                $result.Haltungen += @{
                    Id = $haltungData['Bezeichnung']
                    Felder = $felder
                    Source = 'xtf405'
                }
            }
        }
        
        if ($result.IsVSA) {
            # VSA_KEK XTF: Untersuchungen + Schaeden
            $vsakekData = Parse-VsaKekXtf -Xml $xml
            
            foreach ($untersuchungTid in $vsakekData.Keys) {
                $unterData = $vsakekData[$untersuchungTid]
                
                # Formatiere Zeitpunkt (YYYYMMDD -> DD.MM.YYYY)
                $zeitpunkt = $unterData['Zeitpunkt']
                if ($zeitpunkt -match '^(\d{4})(\d{2})(\d{2})$') {
                    $zeitpunkt = "$($matches[3]).$($matches[2]).$($matches[1])"
                }
                
                # Sammle Schadencodes mit Details
                $schadencodes = @()
                $primaereSchaeden = @()
                $maxSchadenklasse = 0
                
                foreach ($schaden in $unterData['Schaeden']) {
                    if ($schaden['Schadencode']) {
                        $schadencodes += $schaden['Schadencode']
                        
                        # Schadendetail mit Distanz
                        $detail = $schaden['Schadencode']
                        if ($schaden['Distanz'] -and $schaden['Distanz'] -ne '0.00') {
                            $detail += " @$($schaden['Distanz'])m"
                        }
                        if ($schaden['Anmerkung']) {
                            $detail += " ($($schaden['Anmerkung']))"
                        }
                        $primaereSchaeden += $detail
                    }
                    
                    # Bestimme maximale Schadenklasse
                    if ($schaden['Einzelschadenklasse'] -and $schaden['Einzelschadenklasse'] -match '(\d)') {
                        $klasse = [int]$matches[1]
                        if ($klasse -gt $maxSchadenklasse) {
                            $maxSchadenklasse = $klasse
                        }
                    }
                }
                
                $felder = @{
                    'Haltungsname' = $unterData['Bezeichnung']
                    'Haltungslaenge_m' = $unterData['InspizierteLaenge']
                    'Datum_Jahr' = $zeitpunkt
                }

                # === VSA-Rili Zustandsnote berechnen (analog SIA405) ===
                try {
                    Import-Module -Name (Join-Path $PSScriptRoot 'VsaRiliZustandService.ps1') -Force -ErrorAction Stop
                    if ($unterData.ContainsKey('Schaeden') -and $unterData['Schaeden'] -and $unterData['Schaeden'].Count -gt 0) {
                        $findings = ConvertTo-VsaFindings -Schaeden $unterData['Schaeden'] -Kriterium 'D'
                        $inspizierteLaenge = if ($unterData['InspizierteLaenge']) { [double]$unterData['InspizierteLaenge'] } else { 0.0 }
                        $zn = Compute-VsaZustandsnote -SchadensListe $findings -Haltungslaenge $inspizierteLaenge
                        if ($zn) {
                            $felder['VSA_Zustandsnote_D'] = $zn
                            $klassifizierung = Get-ZustandsklasseUndBeschreibung -Zustandsnote $zn
                            $felder['VSA_Zustandsklasse_D'] = $klassifizierung.Klasse
                            $felder['VSA_Zustandsbeschreibung_D'] = $klassifizierung.Beschreibung
                        }
                    }
                } catch {
                    $felder['VSA_Zustandsnote_D'] = "Fehler: $_"
                }
                
                # Primaere Schaeden (max 10, dann "...")
                if ($primaereSchaeden.Count -gt 0) {
                    if ($primaereSchaeden.Count -gt 10) {
                        $felder['Primaere_Schaeden'] = (($primaereSchaeden | Select-Object -First 10) -join "`n") + "`n... ($($primaereSchaeden.Count) Schaeden)"
                    } else {
                        $felder['Primaere_Schaeden'] = $primaereSchaeden -join "`n"
                    }
                }
                
                # Zustandsklasse aus max Schadenklasse
                if ($maxSchadenklasse -gt 0) {
                    $felder['Zustandsklasse'] = $maxSchadenklasse.ToString()
                }
                
                # Fliessrichtung aus von/bis Punkt
                if ($unterData['VonPunkt'] -and $unterData['BisPunkt']) {
                    $felder['Fliessrichtung'] = "Von $($unterData['VonPunkt']) nach $($unterData['BisPunkt'])"
                }
                
                # Erfassungsart als Bemerkung
                if ($unterData['Erfassungsart']) {
                    $bemerkung = "Erfassung: $($unterData['Erfassungsart'])"
                    if ($unterData['Fahrzeug']) { $bemerkung += ", Fahrzeug: $($unterData['Fahrzeug'])" }
                    if ($unterData['Geraet']) { $bemerkung += ", Geraet: $($unterData['Geraet'])" }
                    $felder['Bemerkungen'] = $bemerkung
                }
                
                # Pruefungsresultat aus Erfassungsart ableiten (Status existiert nicht in VSA_KEK)
                if ($unterData['Erfassungsart']) {
                    $felder['Pruefungsresultat'] = $unterData['Erfassungsart']
                }
                
                # Grund der Inspektion
                if ($unterData['Grund']) {
                    if ($felder['Bemerkungen']) {
                        $felder['Bemerkungen'] += "`nGrund: $($unterData['Grund'])"
                    } else {
                        $felder['Bemerkungen'] = "Grund: $($unterData['Grund'])"
                    }
                }
                $result.Haltungen += @{
                    Id = $unterData['Bezeichnung']
                    Felder = $felder
                    Source = 'xtf'
                }
            }
        }
        
        Log-Info -Message "XTF geparst: $($result.Haltungen.Count) Haltungen gefunden (SIA405=$($result.IsSIA405), VSA=$($result.IsVSA))" -Context "XtfImport"
        
    } catch {
        $result.Error = $_.Exception.Message
        Log-Error -Message "Fehler beim XTF-Parsing: $_" -Context "XtfImport" -Exception $_
    }
    
    return $result
}

# ========== Record-Matching ==========
function Find-MatchingRecord {
    param(
        [Project] $Project,
        [hashtable] $XtfRecord
    )
    
    $xtfId = if ($XtfRecord.Felder['Haltungsname']) { $XtfRecord.Felder['Haltungsname'] } else { "" }
    
    if ([string]::IsNullOrWhiteSpace($xtfId)) {
        return $null
    }
    
    # Primaer: Match by Haltungsname (exakt)
    foreach ($record in $Project.Data) {
        $recordId = $record.GetFieldValue('Haltungsname')
        if ($recordId -eq $xtfId) {
            return $record
        }
    }
    
    # Fallback: Partial match (Bezeichnung ohne Suffix)
    $xtfIdBase = $xtfId -replace '-[^-]+$', ''
    foreach ($record in $Project.Data) {
        $recordId = $record.GetFieldValue('Haltungsname')
        $recordIdBase = $recordId -replace '-[^-]+$', ''
        if ($recordIdBase -eq $xtfIdBase -and $xtfIdBase.Length -ge 5) {
            return $record
        }
    }
    
    return $null
}

# ========== Import-Integration ==========
function Import-XtfRecordsToProject {
    param(
        [Project] $Project,
        [object[]] $XtfRecords,
        [string] $ImportSource = "xtf"
    )
    
    $stats = @{
        Created = 0
        Updated = 0
        Conflicts = 0
        Errors = 0
        ConflictDetails = @()
    }
    
    try {
        foreach ($xtfRecord in $XtfRecords) {
            try {
                # Finde oder erstelle matching Record
                $targetRecord = Find-MatchingRecord -Project $Project -XtfRecord $xtfRecord
                
                if (-not $targetRecord) {
                    # Neue Haltung erstellen
                    $targetRecord = $Project.CreateNewRecord()
                    $Project.AddRecord($targetRecord)
                    $stats.Created++
                }
                
                # Merge Fields
                $sourceRecord = New-Object HaltungRecord
                foreach ($fieldName in $xtfRecord.Felder.Keys) {
                    $sourceRecord.SetFieldValue($fieldName, $xtfRecord.Felder[$fieldName], $ImportSource, $false)
                }
                
                $mergeStats = Merge-Record -TargetRecord $targetRecord -SourceRecord $sourceRecord -ImportSource $ImportSource
                
                $stats.Updated += $mergeStats.Updated
                $stats.Conflicts += $mergeStats.Conflicts
                $stats.Errors += $mergeStats.Errors
                if ($mergeStats.ConflictDetails) {
                    $stats.ConflictDetails += $mergeStats.ConflictDetails
                }
                
            } catch {
                $stats.Errors++
                Log-Error -Message "Fehler beim Import von XTF-Record: $_" -Context "XtfImport" -Exception $_
            }
        }
        
        $Project.Dirty = $true
    } catch {
        $stats.Errors++
        Log-Error -Message "Fehler beim XTF-Import: $_" -Context "XtfImport" -Exception $_
    }
    
    return $stats
}

Write-Host "[XtfImportService] Loaded" -ForegroundColor Green
