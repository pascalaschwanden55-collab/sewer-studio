<#
.SYNOPSIS
    PdfImportService für AuswertungPro - PDF-Parsing mit Regex-Mapping
.DESCRIPTION
    - Extrahiert Text aus PDF
    - Parst mit Regex-Tabelle (25+ Felder)
    - Merge nach Merge-Service-Logik
#>

# ========== PDF-Feld-Mapping (REGEX-TABELLE) ==========
# Dies ist die zentrale Mapping-Definition für alle PDF-Felder
# Format: FieldName -> @{ Regexes = @(...); PostProcessor = scriptblock; Multiline = $true/$false }

$script:PdfFieldMapping = @{
    'Haltungsname' = @{
        Regexes = @(
            '(?im)^\s*(Haltungsname|Haltungsnahme|Haltungs?nummer|Haltung\s*Nr\.?|Haltung[\s\-]?ID|Haltungs[-\s]?ID|Leitung[\s\-]?ID|Leitung\s*Nr\.?|Leitungsnummer)\s*[:\-]?\s*(.+?)\s*$',
            '(?im)^Leitung\s{2,}([\w\.\-]+)\s{2,}',
            '(?im)^Leitung\s+([\d]+[\-\.][\d\.]+)\s'
        )
        Multiline = $false
        PostProcessor = { param($v) $v.Trim() }
    }
    
    'Strasse' = @{
        Regexes = @(
            '(?im)^Straße[/\s]*Standort\s{2,}([A-Za-zäöüÄÖÜß][A-Za-zäöüÄÖÜß\s]*?)(?=\s{3,}|$)',
            '(?im)^\s*(Strasse|Straße)\s*[:\-]?\s*(\S.+?)\s*$'
        )
        Multiline = $false
        PostProcessor = { param($v) $v.Trim() }
    }
    
    'Rohrmaterial' = @{
        Regexes = @(
            '(?im)^\s*(Rohrmaterial|Material|Leitungsmaterial)\s*[:\-]?\s*(.+?)\s*$',
            '(?im)^\s*Material\s{2,}(.+?)\s*$'
        )
        Multiline = $false
        PostProcessor = { 
            param($v)
            $v = $v.Trim()
            # Materialtypen normalisieren
            switch -Regex ($v) {
                'Normalbeton|Beton' { 'Beton' }
                'Polypropylen|PP' { 'PP' }
                'Polyethylen|PE' { 'PE' }
                'Polyvinylchlorid|PVC' { 'PVC' }
                'Steinzeug' { 'Steinzeug' }
                'GFK|Glasfaser' { 'GFK' }
                default { $v }
            }
        }
    }
    
    'DN_mm' = @{
        Regexes = @(
            '(?im)\bDN\s*[:\-]?\s*(\d{2,4})\b',
            '(?im)^\s*(Durchmesser|Ø|Durchm\.?)\s*[:\-]?\s*(\d{2,4})\s*mm?\b',
            '(?im)^\s*Dimension\s*\[mm\]\s{2,}(\d{2,4})\s*[/\s]',
            '(?im)Dim\s*\[?mm\]?\s*(\d{2,4})'
        )
        Multiline = $false
        PostProcessor = { param($v) $v.Trim() }
    }
    
    'Nutzungsart' = @{
        Regexes = @(
            '(?im)^\s*(Nutzungsart|Nutzung|Verwendung|Kanalart)\s*[:\-]?\s*(.+?)\s*$',
            '(?im)^\s*(Nutzungsart|Kanalart)\s{2,}(.+?)\s*$',
            '(?im)Nutzungsart\s{2,}(Schmutzabwasser|Regenabwasser|Mischabwasser|Regenwasser|Schmutzwasser)'
        )
        Multiline = $false
        PostProcessor = { 
            param($v)
            $v = $v.Trim()
            switch -Regex ($v) {
                'Schmutzabwasser|Schmutzwasser' { 'Schmutzwasser' }
                'Regenabwasser|Regenwasser' { 'Regenwasser' }
                'Mischabwasser' { 'Mischabwasser' }
                default { $v }
            }
        }
    }
    
    'Haltungslaenge_m' = @{
        Regexes = @(
            '(?im)^\s*(Haltungsl[äa]nge|Leitungsl[äa]nge|L[äa]nge|Rohrl[äa]nge|L\.)\s*[:\-]?\s*([0-9]+(?:[.,][0-9]+)?)\s*m\b',
            '(?im)^\s*Leitungsl[äa]nge\s{2,}([0-9]+(?:[.,][0-9]+)?)\s*m',
            '(?im)Insp\.?L\.?\s*\[m\]\s*([0-9]+(?:[.,][0-9]+)?)'
        )
        Multiline = $false
        PostProcessor = { param($v) ($v -replace ',', '.').Trim() }
    }
    
    'Fliessrichtung' = @{
        Regexes = @(
            '(?im)Inspektionsrichtung\s{2,}(.+?)$',
            '(?im)^\s*(Fliessrichtung|Flie[ßs]richtung|Strömung)\s*[:\-]?\s*(.+?)\s*$',
            '(?im)^\s*(von)\s*[:\-]?\s*(.+?)\s+(nach)\s*[:\-]?\s*(.+?)\s*$'
        )
        Multiline = $false
        PostProcessor = { param($v) $v.Trim() }
    }
    
    'Primaere_Schaeden' = @{
        Regexes = @(
            '(?im)^\s*(Prim[äa]re\s*Sch[äa]den|Hauptsch[äa]den|Sch[äa]den|Defekte?)\s*[:\-]?\s*(.+?)\s*$'
        )
        Multiline = $true
        MaxLines = 5
        PostProcessor = { param($v) $v.Trim() }
    }
    
    'Zustandsklasse' = @{
        Regexes = @(
            '(?im)^\s*(Zustandsklasse|Zustand|ZK)\s*[:\-]?\s*([0-9])\b'
        )
        Multiline = $false
        PostProcessor = { param($v) $v.Trim() }
    }
    
    'Pruefungsresultat' = @{
        Regexes = @(
            '(?im)^\s*(Pr[üu]fungsresultat|Pr[üu]fergebnis|Prüfung|Resultat)\s*[:\-]?\s*(.+?)\s*$'
        )
        Multiline = $true
        MaxLines = 3
        PostProcessor = { param($v) $v.Trim() }
    }
    
    'Sanieren_JaNein' = @{
        Regexes = @(
            '(?im)^\s*(Sanieren|Sanierung[\s\-]*(ja[./]nein)?|Sanierungsbedarf|Renovieren)\s*[:\-]?\s*(Ja|Nein)\b'
        )
        Multiline = $false
        PostProcessor = { 
            param($v)
            if ($v -imatch 'ja|yes') { 'Ja' }
            elseif ($v -imatch 'nein|no') { 'Nein' }
            else { $v.Trim() }
        }
    }
    
    'Empfohlene_Sanierungsmassnahmen' = @{
        Regexes = @(
            '(?im)^\s*(Empfohlene\s*Sanierungsmassnahmen|Sanierungsvorschlag|Ma[ßs]nahmen|Empfehlung)\s*[:\-]?\s*(.+?)\s*$'
        )
        Multiline = $true
        MaxLines = 5
        PostProcessor = { param($v) $v.Trim() }
    }
    
    'Kosten' = @{
        Regexes = @(
            "(?im)^\s*(Kosten|Kostenschaetzung|Kosten\s*\(CHF\)|CHF)\s*[:\-]?\s*(CHF)?\s*([0-9''.,]+)\b"
        )
        Multiline = $false
        PostProcessor = { 
            param($v)
            # Entferne CHF und Waehrungs-Symbole
            $v = $v -replace 'CHF|Fr\.|Sfr\.', ''
            # Tausendertrenner entfernen
            $v = $v -replace "'", ""
            # Komma durch Punkt
            $v = $v -replace ',', '.'
            $v.Trim()
        }
    }
    
    'Eigentuemer' = @{
        Regexes = @(
            '(?im)^\s*(Eigent[üu]mer|Besitzer|Eigentuemer|Eigentümer)\s*[:\-]?\s*(.+?)\s*$'
        )
        Multiline = $false
        PostProcessor = { param($v) $v.Trim() }
    }
    
    'Bemerkungen' = @{
        Regexes = @(
            '(?im)^\s*(Bemerkungen?|Bemerkung|Kommentare?|Notizen?|Anmerkungen?)\s*[:\-]?\s*(.+?)\s*$'
        )
        Multiline = $true
        MaxLines = 5
        PostProcessor = { param($v) $v.Trim() }
    }
    
    'Link' = @{
        Regexes = @(
            '\bhttps?://[^\s<>\[\]{}|\\^`\)]*'
        )
        Multiline = $false
        PostProcessor = { param($v) $v.Trim() }
    }
    
    'Renovierung_Inliner_Stk' = @{
        Regexes = @(
            '(?im)^\s*(Renovierung.*Inliner.*Stk|Inliner.*Stk[.ck]*)\s*[:\-]?\s*(\d+)\b'
        )
        Multiline = $false
        PostProcessor = { param($v) $v.Trim() }
    }
    
    'Renovierung_Inliner_m' = @{
        Regexes = @(
            '(?im)^\s*(Renovierung.*Inliner.*m(?![a-z])|Inliner.*m\b)\s*[:\-]?\s*([0-9]+(?:[.,][0-9]+)?)\s*m?\b'
        )
        Multiline = $false
        PostProcessor = { param($v) ($v -replace ',', '.').Trim() }
    }
    
    'Anschluesse_verpressen' = @{
        Regexes = @(
            '(?im)^\s*(Anschl[üu]sse[\s\-]*verpressen|Verpressen)\s*[:\-]?\s*(\d+)\b'
        )
        Multiline = $false
        PostProcessor = { param($v) $v.Trim() }
    }
    
    'Reparatur_Manschette' = @{
        Regexes = @(
            '(?im)^\s*(Reparatur[\s\-]*Manschette|Manschette)\s*[:\-]?\s*(\d+)\b'
        )
        Multiline = $false
        PostProcessor = { param($v) $v.Trim() }
    }
    
    'Reparatur_Kurzliner' = @{
        Regexes = @(
            '(?im)^\s*(Reparatur[\s\-]*Kurzliner|Kurzliner)\s*[:\-]?\s*(\d+)\b'
        )
        Multiline = $false
        PostProcessor = { param($v) $v.Trim() }
    }
    
    'Erneuerung_Neubau_m' = @{
        Regexes = @(
            '(?im)^\s*(Erneuerung.*Neubau|Neubau)\s*[:\-]?\s*([0-9]+(?:[.,][0-9]+)?)\s*m?\b'
        )
        Multiline = $false
        PostProcessor = { param($v) ($v -replace ',', '.').Trim() }
    }
    
    'Offen_abgeschlossen' = @{
        Regexes = @(
            '(?im)^\s*(Status|Zustand(?!sklasse)|Offen[\s\-]*abgeschlossen)\s*[:\-]?\s*(offen|abgeschlossen)\b'
        )
        Multiline = $false
        PostProcessor = { 
            param($v)
            if ($v -imatch 'offen') { 'offen' }
            elseif ($v -imatch 'abgeschlossen') { 'abgeschlossen' }
            else { $v.Trim() }
        }
    }
    
    'Datum_Jahr' = @{
        Regexes = @(
            '(?im)^\s*(Datum|Pr[üu]fdatum|Inspektionsdatum|Insp\.?\-?datum|Pr[üu]fungsdatum|Jahr)\s*[:\-]?\s*((\d{1,2}\.\d{1,2}\.\d{2,4})|(\d{4}))\b',
            '(?im)Insp\.?\-?[Dd]atum\s{2,}(\d{1,2}\.\d{1,2}\.\d{2,4})'
        )
        Multiline = $false
        PostProcessor = { param($v) $v.Trim() }
    }
}

# ========== PDF-Extraktionsfunktionen ==========
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
    param(
        [string] $PdfPath,
        [string] $OutputPath
    )
    
    $pdftotext = Get-PdfToTextPath
    if (-not $pdftotext) {
        throw "pdftotext.exe nicht gefunden. Bitte Poppler installieren (winget install poppler)."
    }
    
    & $pdftotext -enc UTF-8 -layout $PdfPath $OutputPath
    
    if (-not (Test-Path $OutputPath)) {
        throw "PDF-Textextraktion fehlgeschlagen."
    }
}

<#
.SYNOPSIS
    Extract-PdfText: Extrahiert Text aus PDF-Datei
.PARAMETER PdfPath
    Pfad zur PDF-Datei
.RETURNS
    String mit Text oder $null bei Fehler
#>
function Extract-PdfText {
    param([string] $PdfPath)
    
    try {
        if (-not (Test-Path $PdfPath)) {
            throw "PDF nicht gefunden: $PdfPath"
        }
        
        $txtPath = Join-Path $env:TEMP ("pdf_extract_{0}.txt" -f [Guid]::NewGuid().ToString("N"))
        
        try {
            Convert-PdfToText -PdfPath $PdfPath -OutputPath $txtPath
            $content = Get-Content -Path $txtPath -Raw -Encoding UTF8
            Remove-Item -Path $txtPath -Force -ErrorAction SilentlyContinue
            return $content
        } catch {
            # Fallback: PowerShell COM + Adobe Reader (falls vorhanden)
            try {
                $pdf = New-Object -ComObject AcroExch.PDFDoc
                if ($pdf.Open($PdfPath)) {
                    $output = [System.IO.Path]::GetTempFileName() + ".txt"
                    $pdf.SaveAs($output, 0)
                    if (Test-Path $output) {
                        $content = Get-Content $output -Raw -Encoding UTF8
                        Remove-Item -Path $output -Force -ErrorAction SilentlyContinue
                        return $content
                    }
                }
            } catch {
                # ignore
            }
        }
        
        Log-Warn -Message "PDF-Text-Extraktion: Externe Tools nötig (pdftotext, PdfSharp, etc.)" -Context "PdfImport"
        return ""
    } catch {
        Log-Error -Message "Fehler beim Extrahieren von PDF-Text: $_" -Context "PdfImport" -Exception $_
        return $null
    }
}

function ExtractPdfTextByPage {
    param([string] $PdfPath)
    
    $pages = @()
    
    try {
        if (-not (Test-Path $PdfPath)) {
            throw "PDF nicht gefunden: $PdfPath"
        }
        
        $txtPath = Join-Path $env:TEMP ("pdf_extract_pages_{0}.txt" -f [Guid]::NewGuid().ToString("N"))
        Convert-PdfToText -PdfPath $PdfPath -OutputPath $txtPath
        
        $content = Get-Content -Path $txtPath -Raw -Encoding UTF8
        Remove-Item -Path $txtPath -Force -ErrorAction SilentlyContinue
        
        if ([string]::IsNullOrWhiteSpace($content)) {
            return @()
        }
        
        $content = $content -replace "`r`n", "`n"
        $rawPages = $content -split "[\f]"
        
        foreach ($page in $rawPages) {
            if (-not [string]::IsNullOrWhiteSpace($page)) {
                $pages += $page.Trim()
            }
        }
        
        if ($pages.Count -eq 0 -and -not [string]::IsNullOrWhiteSpace($content)) {
            $pages += $content.Trim()
        }
        
        return $pages
    } catch {
        Log-Warn -Message "Seiten-Extraktion fehlgeschlagen, fallback auf Gesamttext: $_" -Context "PdfImport"
        $fullText = Extract-PdfText -PdfPath $PdfPath
        if (-not [string]::IsNullOrWhiteSpace($fullText)) {
            return @($fullText)
        }
        return @()
    }
}

<#
.SYNOPSIS
    Parse-PdfText: Parst Text nach Regex-Mapping
.PARAMETER Text
    Extrahierter PDF-Text
.RETURNS
    Hashtable mit Field -> Value Mappings
#>
function Parse-PdfText {
    param([string] $Text)
    
    $result = @{}
    
    if ([string]::IsNullOrWhiteSpace($Text)) {
        return $result
    }
    
    # Normalisiere Text
    $Text = $Text -replace "`r`n", "`n"
    $lines = $Text -split "`n"
    
    foreach ($fieldName in $script:PdfFieldMapping.Keys) {
        $mapping = $script:PdfFieldMapping[$fieldName]
        $regexes = $mapping['Regexes']
        $multiline = $mapping['Multiline'] -eq $true
        $maxLines = if ($mapping.ContainsKey('MaxLines') -and $mapping['MaxLines']) { $mapping['MaxLines'] } else { 1 }
        $postProc = $mapping['PostProcessor']
        
        foreach ($regex in $regexes) {
            if ($multiline) {
                # Mehrzeiliges Parsing: Label finden, dann nächste N Zeilen sammeln
                for ($i = 0; $i -lt $lines.Count; $i++) {
                    if ($lines[$i] -match $regex) {
                        $value = $lines[$i]
                        
                        # Sammle nachfolgende Zeilen bis nächstes Label oder MaxLines
                        $lineCount = 1
                        for ($j = $i + 1; $j -lt $lines.Count -and $lineCount -lt $maxLines; $j++) {
                            $nextLine = $lines[$j]
                            # Prüfe ob nächste Zeile ein neues Label ist
                            $isLabel = $false
                            foreach ($checkMap in $script:PdfFieldMapping.Values) {
                                foreach ($checkRegex in $checkMap['Regexes']) {
                                    if ($nextLine -match $checkRegex) {
                                        $isLabel = $true
                                        break
                                    }
                                }
                                if ($isLabel) { break }
                            }
                            
                            if ($isLabel -or [string]::IsNullOrWhiteSpace($nextLine)) {
                                break
                            }
                            
                            $value += "`n" + $nextLine
                            $lineCount++
                        }
                        
                        # Extrahiere Captured Groups
                        $regexMatches = [regex]::Matches($value, $regex, [System.Text.RegularExpressions.RegexOptions]::Multiline)
                        if ($regexMatches.Count -gt 0) {
                            $match = $regexMatches[0]
                            # Letzte nicht-leere Gruppe ist der Wert
                            for ($g = $match.Groups.Count - 1; $g -gt 0; $g--) {
                                if ($match.Groups[$g].Value) {
                                    $value = $match.Groups[$g].Value
                                    break
                                }
                            }
                            
                            if (-not [string]::IsNullOrWhiteSpace($value)) {
                                $value = & $postProc $value
                                if (-not [string]::IsNullOrWhiteSpace($value)) {
                                    $result[$fieldName] = $value
                                    break
                                }
                            }
                        }
                    }
                }
            } else {
                # Single-line parsing
                foreach ($line in $lines) {
                    $match = [regex]::Match($line, $regex)
                    if ($match.Success) {
                        # Letzte nicht-leere Gruppe ist der Wert
                        $value = ""
                        for ($g = $match.Groups.Count - 1; $g -gt 0; $g--) {
                            if ($match.Groups[$g].Value) {
                                $value = $match.Groups[$g].Value
                                break
                            }
                        }
                        
                        if (-not [string]::IsNullOrWhiteSpace($value)) {
                            $value = & $postProc $value
                            if (-not [string]::IsNullOrWhiteSpace($value)) {
                                $result[$fieldName] = $value
                                break  # Nächstes Feld
                            }
                        }
                    }
                }
            }
            
            if ($result.ContainsKey($fieldName)) {
                break  # Regex erfolgreich, nächstes Feld
            }
        }
    }
    
    return $result
}

# ========== Chunking / Batch Import ==========
function GetHaltungKeyFromChunk {
    param([string] $TextChunk)
    
    if ([string]::IsNullOrWhiteSpace($TextChunk)) {
        return $null
    }
    
    # Reuse Mapping (Haltungsname)
    $parsed = Parse-PdfText -Text $TextChunk
    if ($parsed.ContainsKey('Haltungsname') -and -not [string]::IsNullOrWhiteSpace($parsed['Haltungsname'])) {
        $val = ($parsed['Haltungsname'] -split "`n")[0].Trim()
        if ($val) { return $val }
    }
    
    $regexes = @(
        '(?im)^\s*(Haltungsname|Haltungsnahme|Haltungs?nummer|Haltung\s*Nr\.?|Haltung[\s\-]?ID|Haltungs[-\s]?ID|Leitung[\s\-]?ID|Leitung\s*Nr\.?|Leitungsnummer)\s*[:\-]?\s*(?<id>.+?)\s*$',
        '(?im)^\s*(Haltung|Leitung)\s+(?<id>[\w\.\-\/]+)\s*$',
        '(?im)Haltungsinspektion\s+-\s+\d{2}\.\d{2}\.\d{4}\s+-\s+(?<id>\S+)'
    )
    
    foreach ($regex in $regexes) {
        $match = [regex]::Match($TextChunk, $regex)
        if ($match.Success) {
            $id = ""
            if ($match.Groups["id"].Value) {
                $id = $match.Groups["id"].Value.Trim()
            } else {
                for ($g = $match.Groups.Count - 1; $g -gt 0; $g--) {
                    if ($match.Groups[$g].Value) {
                        $id = $match.Groups[$g].Value.Trim()
                        break
                    }
                }
            }
            if ($id) {
                $id = ($id -split "`n")[0].Trim()
                return $id
            }
        }
    }
    
    return $null
}

function ParseFieldsFromChunk {
    param([string] $TextChunk)
    
    $fields = Parse-PdfText -Text $TextChunk
    return $fields
}

function SplitIntoHaltungChunks {
    param(
        [string[]] $PagesText
    )
    
    $chunks = @()
    if (-not $PagesText -or $PagesText.Count -eq 0) {
        return $chunks
    }
    
    $chunkIndex = 0
    $current = $null
    
    for ($p = 0; $p -lt $PagesText.Count; $p++) {
        $pageNumber = $p + 1
        $pageText = ($PagesText[$p] -replace "`r`n", "`n").Trim()
        if ([string]::IsNullOrWhiteSpace($pageText)) {
            continue
        }
        
        $lines = $pageText -split "`n"
        $markers = @()
        
        for ($i = 0; $i -lt $lines.Count; $i++) {
            $line = $lines[$i]
            $id = $null
            
            # Pattern 1: "Haltungsname: ID" oder "Leitung-ID: ID"
            $m1 = [regex]::Match($line, '(?im)^\s*(Haltungsname|Haltungsnahme|Haltungs?nummer|Haltung\s*Nr\.?|Haltung[\s\-]?ID|Haltungs[-\s]?ID|Leitung[\s\-]?ID|Leitung\s*Nr\.?|Leitungsnummer)\s*[:\-]?\s*(?<id>.+?)\s*$')
            if ($m1.Success -and $m1.Groups['id'].Value) {
                $id = $m1.Groups['id'].Value.Trim()
            } else {
                # Pattern 2: "Haltung ID" oder "Leitung ID" mit Leerzeichen
                $m2 = [regex]::Match($line, '(?im)^\s*(Haltung|Leitung)\s+(?<id>[\w\.\-\/]+)\s*$')
                if ($m2.Success -and $m2.Groups['id'].Value) {
                    $id = $m2.Groups['id'].Value.Trim()
                } else {
                    # Pattern 3: "Haltungsinspektion - Datum - ID"
                    $m3 = [regex]::Match($line, '(?im)Haltungsinspektion\s+-\s+\d{2}\.\d{2}\.\d{4}\s+-\s+(?<id>\S+)')
                    if ($m3.Success -and $m3.Groups['id'].Value) {
                        $id = $m3.Groups['id'].Value.Trim()
                    } else {
                        # Pattern 4: WinCan/IKAS Format "Leitung                   59965-10.62293"
                        $m4 = [regex]::Match($line, '(?im)^Leitung\s{2,}(?<id>\d+[\-\.]\d+[\.\d]*)')
                        if ($m4.Success -and $m4.Groups['id'].Value) {
                            $id = $m4.Groups['id'].Value.Trim()
                        } else {
                            # Pattern 5: Leitungsgrafik / Leitungsbildbericht Titel-Zeile
                            $m5 = [regex]::Match($line, '(?im)^(Leitungsgrafik|Leitungsbildbericht)')
                            if ($m5.Success) {
                                # ID auf der gleichen Zeile nach "Leitung"
                                $m5b = [regex]::Match($line, '(?im)Leitung\s{2,}(?<id>\d+[\-\.]\d+[\.\d]*)')
                                if ($m5b.Success -and $m5b.Groups['id'].Value) {
                                    $id = $m5b.Groups['id'].Value.Trim()
                                }
                            }
                        }
                    }
                }
            }
            
            if ($id) {
                $markers += @{
                    LineIndex = $i
                    Id = $id
                }
            }
        }
        
        $segments = @()
        
        if ($markers.Count -le 1) {
            $segId = $null
            if ($markers.Count -eq 1) {
                $segId = $markers[0].Id
            }
            $segments += @{
                Text = $pageText
                Id = $segId
                Pages = @($pageNumber)
            }
        } else {
            $firstIndex = $markers[0].LineIndex
            for ($m = 0; $m -lt $markers.Count; $m++) {
                $start = $markers[$m].LineIndex
                $end = if ($m -lt ($markers.Count - 1)) { $markers[$m + 1].LineIndex - 1 } else { $lines.Count - 1 }
                
                $segmentLines = @()
                if ($m -eq 0 -and $firstIndex -gt 0) {
                    $segmentLines += $lines[0..($firstIndex - 1)]
                }
                if ($start -le $end) {
                    $segmentLines += $lines[$start..$end]
                }
                
                $segments += @{
                    Text = ($segmentLines -join "`n")
                    Id = $markers[$m].Id
                    Pages = @($pageNumber)
                }
            }
        }
        
        foreach ($segment in $segments) {
            $segmentText = $segment.Text
            $segmentId = if ($segment.Id) { $segment.Id } else { GetHaltungKeyFromChunk -TextChunk $segmentText }
            
            if ($current -and $segmentId -and $current.DetectedId -and ($segmentId -eq $current.DetectedId)) {
                $current.Text += "`n" + $segmentText
                $current.Pages += $segment.Pages
            } elseif ($current -and -not $segmentId) {
                $current.Text += "`n" + $segmentText
                $current.Pages += $segment.Pages
            } else {
                if ($current) {
                    $chunks += $current
                }
                
                $chunkIndex++
                $current = @{
                    Index = $chunkIndex
                    Pages = @($segment.Pages)
                    Text = $segmentText
                    DetectedId = $segmentId
                    IsUncertain = $false
                }
            }
        }
    }
    
    if ($current) {
        $chunks += $current
    }
    
    foreach ($chunk in $chunks) {
        if (-not $chunk.DetectedId) {
            $chunk.IsUncertain = $true
        }
        $pageList = $chunk.Pages | Sort-Object
        $chunk.PageRange = if ($pageList.Count -gt 0) {
            if ($pageList.Count -eq 1) { "$($pageList[0])" } else { "$($pageList[0])-$($pageList[$pageList.Count - 1])" }
        } else { "" }
    }
    
    return $chunks
}

function ImportPdfBatch {
    param(
        [string] $PdfPath,
        [Project] $Project,
        [object[]] $Chunks = $null
    )
    
    $stats = @{
        Found = 0
        Created = 0
        UpdatedRecords = 0
        UpdatedFields = 0
        Conflicts = 0
        Errors = 0
        Uncertain = 0
        ConflictDetails = @()
        Chunks = @()
    }
    
    try {
        if (-not $Project) {
            throw "Projekt ist null"
        }
        if (-not (Test-Path $PdfPath)) {
            throw "PDF nicht gefunden: $PdfPath"
        }
        
        if (-not $Chunks) {
            $pages = ExtractPdfTextByPage -PdfPath $PdfPath
            $Chunks = SplitIntoHaltungChunks -PagesText $pages
        }
        
        $stats.Found = $Chunks.Count
        $stats.Chunks = $Chunks
        
        $unknownPrefix = "UNBEKANNT_{0}_" -f (Get-Date -Format "yyyyMMdd_HHmmss")
        $index = 0
        
        foreach ($chunk in $Chunks) {
            $index++
            try {
                $chunkText = $chunk.Text
                $pageRange = if ($chunk.PageRange) { $chunk.PageRange } else { "" }
                
                $fields = ParseFieldsFromChunk -TextChunk $chunkText
                $haltungId = $null
                
                if ($fields.ContainsKey('Haltungsname') -and -not [string]::IsNullOrWhiteSpace($fields['Haltungsname'])) {
                    $haltungId = ($fields['Haltungsname'] -split "`n")[0].Trim()
                }
                if (-not $haltungId) {
                    $haltungId = if ($chunk.DetectedId) { $chunk.DetectedId } else { GetHaltungKeyFromChunk -TextChunk $chunkText }
                }
                
                $isUnknown = $false
                if ([string]::IsNullOrWhiteSpace($haltungId)) {
                    $haltungId = "{0}{1}" -f $unknownPrefix, $index
                    $chunk.IsUncertain = $true
                    $stats.Uncertain++
                    $isUnknown = $true
                }
                
                $fields['Haltungsname'] = $haltungId
                
                if ($isUnknown) {
                    $note = "Zu pruefen: keine Haltung-ID im PDF"
                    if ($fields.ContainsKey('Bemerkungen') -and -not [string]::IsNullOrWhiteSpace($fields['Bemerkungen'])) {
                        $fields['Bemerkungen'] = $fields['Bemerkungen'].Trim() + "`n" + $note
                    } else {
                        $fields['Bemerkungen'] = $note
                    }
                }
                
                $targetRecord = $Project.Data | Where-Object {
                    $_.GetFieldValue('Haltungsname') -ieq $haltungId
                } | Select-Object -First 1
                
                $createdRecord = $false
                if (-not $targetRecord) {
                    $targetRecord = $Project.CreateNewRecord()
                    $Project.AddRecord($targetRecord)
                    $createdRecord = $true
                    $stats.Created++
                }
                
                $sourceRecord = New-Object HaltungRecord
                foreach ($fieldName in $fields.Keys) {
                    $value = $fields[$fieldName]
                    if ($null -ne $value) {
                        $sourceRecord.SetFieldValue($fieldName, [string]$value, "pdf", $false)
                    }
                }
                
                $mergeStats = Merge-Record -TargetRecord $targetRecord -SourceRecord $sourceRecord -ImportSource "pdf"
                
                if (-not $createdRecord -and $mergeStats.Updated -gt 0) {
                    $stats.UpdatedRecords++
                }
                
                $stats.UpdatedFields += $mergeStats.Updated
                $stats.Conflicts += $mergeStats.Conflicts
                $stats.Errors += $mergeStats.Errors
                
                if ($mergeStats.ConflictDetails) {
                    foreach ($conflict in $mergeStats.ConflictDetails) {
                        $conflictCopy = @{}
                        foreach ($key in $conflict.Keys) { $conflictCopy[$key] = $conflict[$key] }
                        $conflictCopy.ChunkIndex = $chunk.Index
                        $conflictCopy.PageRange = $pageRange
                        $conflictCopy.PdfPath = $PdfPath
                        $conflictCopy.Haltungsname = $haltungId
                        
                        $stats.ConflictDetails += $conflictCopy
                        $null = $Project.Conflicts.Add($conflictCopy)
                    }
                }
                
                $fieldList = if ($fields.Keys.Count -gt 0) { ($fields.Keys -join ", ") } else { "-" }
                Log-Info -Message "PDF-Chunk $($chunk.Index) Seiten $pageRange ID '$haltungId' Felder: $fieldList | Merge=$($mergeStats.Updated) Konflikte=$($mergeStats.Conflicts)" -Context "PdfImport"
                
            } catch {
                $stats.Errors++
                Log-Error -Message "Fehler in PDF-Chunk $($index): $_" -Context "PdfImport" -Exception $_
            }
        }
        
        $Project.Dirty = $true
    } catch {
        $stats.Errors++
        Log-Error -Message "Fehler beim PDF-Batch-Import: $_" -Context "PdfImport" -Exception $_
    }
    
    return $stats
}

# ========== Import-Integration ==========
<#
.SYNOPSIS
    Import-PdfToRecord: Importiert PDF in einen HaltungRecord
.PARAMETER Record
    Ziel-Record
.PARAMETER PdfPath
    PDF-Datei
.PARAMETER AllowConflicts
    Wenn true: Konflikte protokollieren statt zu fehlen
.RETURNS
    PSCustomObject mit Statistik (Merged, Conflicts, Errors)
#>
function Import-PdfToRecord {
    param(
        [HaltungRecord] $Record,
        [string] $PdfPath,
        [bool] $AllowConflicts = $true
    )
    
    $stats = @{
        Merged = 0
        Conflicts = 0
        Errors = 0
        ConflictDetails = @()
    }
    
    try {
        # Extrahiere Text
        $pdfText = Extract-PdfText -PdfPath $PdfPath
        if ([string]::IsNullOrWhiteSpace($pdfText)) {
            Log-Warn -Message "PDF enthält keinen lesbaren Text: $PdfPath" -Context "PdfImport"
            $stats.Errors++
            return $stats
        }
        
        # Parse Text
        $parsedFields = Parse-PdfText -Text $pdfText
        
        # Merge pro Feld
        foreach ($fieldName in $parsedFields.Keys) {
            try {
                $sourceValue = $parsedFields[$fieldName]
                $currentValue = $Record.GetFieldValue($fieldName)
                $fieldMeta = $Record.FieldMeta[$fieldName]
                
                $mergeResult = Merge-Field -FieldName $fieldName `
                    -CurrentValue $currentValue `
                    -NewValue $sourceValue `
                    -FieldMeta $fieldMeta `
                    -NewSource "pdf" `
                    -AllowConflicts $AllowConflicts
                
                if ($mergeResult.Merged) {
                    $Record.SetFieldValue($fieldName, $mergeResult.NewValue, "pdf", $false)
                    $stats.Merged++
                } elseif ($mergeResult.Conflict) {
                    $stats.Conflicts++
                    $stats.ConflictDetails += $mergeResult.Conflict
                }
            } catch {
                $stats.Errors++
                Log-Error -Message "Fehler beim PDF-Merge von Feld '$fieldName': $_" -Context "PdfImport"
            }
        }
        
    } catch {
        $stats.Errors++
        Log-Error -Message "Fehler beim PDF-Import: $_" -Context "PdfImport" -Exception $_
    }
    
    return $stats
}

<#
.SYNOPSIS
    Parse-PdfFile: Parst eine PDF-Datei und extrahiert Haltungen
.PARAMETER PdfPath
    Pfad zur PDF-Datei
.RETURNS
    Hashtable mit Haltungen und FullText
#>
function Parse-PdfFile {
    param([string] $PdfPath)
    
    $result = @{
        Haltungen = @()
        FullText = ""
        Error = $null
    }
    
    try {
        if (-not (Test-Path $PdfPath)) {
            $result.Error = "PDF nicht gefunden: $PdfPath"
            return $result
        }
        
        # Text extrahieren
        $pdfText = Extract-PdfText -PdfPath $PdfPath
        $result.FullText = $pdfText
        
        if ([string]::IsNullOrWhiteSpace($pdfText)) {
            # Kein Text extrahierbar - versuche mit Dateiname
            $fileName = [System.IO.Path]::GetFileNameWithoutExtension($PdfPath)
            Log-Warn -Message "PDF enthält keinen lesbaren Text, nutze Dateiname: $fileName" -Context "PdfImport"
            
            # Erstelle eine Haltung basierend auf Dateiname
            $parsedFields = @{}
            
            # Versuche Haltungsname aus Dateiname zu extrahieren
            if ($fileName -match '(H[_\-\s]?\d+[\-\d]*)') {
                $parsedFields['Haltungsname'] = $matches[1]
            } elseif ($fileName -match '(\d{5,})') {
                $parsedFields['Haltungsname'] = $matches[1]
            } else {
                $parsedFields['Haltungsname'] = $fileName
            }
            
            if ($parsedFields.Count -gt 0) {
                $result.Haltungen += @{
                    Felder = $parsedFields
                    Source = 'pdf'
                }
            }
        } else {
            # Text parsen
            $parsedFields = Parse-PdfText -Text $pdfText
            
            if ($parsedFields.Count -gt 0) {
                $result.Haltungen += @{
                    Felder = $parsedFields
                    Source = 'pdf'
                }
            }
            
            # Prüfe ob mehrere Haltungen im PDF sind (Tabellen-Format)
            # Suche nach mehreren Haltungsnamen
            $haltungMatches = [regex]::Matches($pdfText, '(?im)^\s*H[_\-]?\s*(\d+[\-\d/]*)', [System.Text.RegularExpressions.RegexOptions]::Multiline)
            
            if ($haltungMatches.Count -gt 1) {
                # Mehrere Haltungen gefunden - split und parse einzeln
                Log-Info -Message "PDF enthält $($haltungMatches.Count) Haltungen" -Context "PdfImport"
                
                # Für jede Haltung im PDF
                $lines = $pdfText -split "`n"
                $currentHaltung = $null
                $currentText = ""
                
                foreach ($line in $lines) {
                    if ($line -match '^\s*H[_\-]?\s*(\d+[\-\d/]*)') {
                        # Neue Haltung gefunden
                        if ($currentHaltung -and $currentText) {
                            $fields = Parse-PdfText -Text $currentText
                            if ($fields.Count -gt 0) {
                                $fields['Haltungsname'] = $currentHaltung
                                $result.Haltungen += @{
                                    Felder = $fields
                                    Source = 'pdf'
                                }
                            }
                        }
                        $currentHaltung = $matches[0].Trim()
                        $currentText = $line
                    } else {
                        $currentText += "`n$line"
                    }
                }
                
                # Letzte Haltung
                if ($currentHaltung -and $currentText) {
                    $fields = Parse-PdfText -Text $currentText
                    if ($fields.Count -gt 0) {
                        $fields['Haltungsname'] = $currentHaltung
                        $result.Haltungen += @{
                            Felder = $fields
                            Source = 'pdf'
                        }
                    }
                }
            }
        }
        
        Log-Info -Message "PDF geparst: $($result.Haltungen.Count) Haltungen gefunden" -Context "PdfImport"
        
    } catch {
        $result.Error = $_.ToString()
        Log-Error -Message "PDF-Parse-Fehler: $_" -Context "PdfImport"
    }
    
    return $result
}

# ========== PDF BATCH IMPORT ==========
<#
.SYNOPSIS
    Extracts PDF text page by page
.DESCRIPTION
    Tries to extract text from PDF using iText7, PdfSharp, or external tools.
    Returns array of page texts for chunk detection.
.PARAMETER PdfPath
    Path to PDF file
.RETURNS
    Array of page texts (string[])
#>
function Extract-PdfTextByPage {
    param([string] $PdfPath)
    
    $pages = @()
    
    try {
        if (-not (Test-Path $PdfPath)) {
            throw "PDF nicht gefunden: $PdfPath"
        }
        
        # Versuche iText7 (wenn vorhanden)
        try {
            Add-Type -Path "$PSScriptRoot\..\lib\itext.kernel.dll" -ErrorAction SilentlyContinue
            Add-Type -Path "$PSScriptRoot\..\lib\itext.io.dll" -ErrorAction SilentlyContinue
            
            $pdfReader = New-Object iText.Kernel.Pdf.PdfReader($PdfPath)
            $pdfDoc = New-Object iText.Kernel.Pdf.PdfDocument($pdfReader)
            $numPages = $pdfDoc.GetNumberOfPages()
            
            for ($i = 1; $i -le $numPages; $i++) {
                $page = $pdfDoc.GetPage($i)
                $strategy = New-Object iText.Kernel.Pdf.Canvas.Parser.Listener.SimpleTextExtractionStrategy
                $pageText = [iText.Kernel.Pdf.Canvas.Parser.PdfTextExtractor]::GetTextFromPage($page, $strategy)
                $pages += $pageText
            }
            
            $pdfDoc.Close()
            $pdfReader.Close()
            
            Log-Info -Message "PDF extrahiert mit iText7: $numPages Seiten" -Context "PdfBatchImport"
            return $pages
        } catch {
            # iText7 nicht verfügbar, weiter
        }
        
        # Fallback: pdftotext (Poppler)
        $pdftotext = "pdftotext"
        $pdftotextPath = Get-Command $pdftotext -ErrorAction SilentlyContinue
        
        if ($pdftotextPath) {
            $tempDir = [System.IO.Path]::GetTempPath()
            $tempBase = [System.IO.Path]::Combine($tempDir, [System.IO.Path]::GetRandomFileName())
            
            # Extrahiere alle Seiten
            & $pdftotext -layout -nopgbrk $PdfPath "$tempBase.txt" 2>$null
            
            if (Test-Path "$tempBase.txt") {
                $fullText = Get-Content "$tempBase.txt" -Raw -Encoding UTF8
                Remove-Item "$tempBase.txt" -Force
                
                # Versuche Seitenumbrüche zu erkennen (FF = Form Feed)
                $pageBreaks = $fullText -split "`f"
                
                if ($pageBreaks.Count -gt 1) {
                    $pages = $pageBreaks | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
                } else {
                    $pages = @($fullText)
                }
                
                Log-Info -Message "PDF extrahiert mit pdftotext: $($pages.Count) Seiten" -Context "PdfBatchImport"
                return $pages
            }
        }
        
        # Letzte Option: Existierende Extract-PdfText nutzen
        $fullText = Extract-PdfText -PdfPath $PdfPath
        if (-not [string]::IsNullOrWhiteSpace($fullText)) {
            $pages = @($fullText)
        }
        
    } catch {
        Log-Error -Message "Fehler beim Extrahieren der PDF-Seiten: $_" -Context "PdfBatchImport"
    }
    
    return $pages
}

<#
.SYNOPSIS
    Splits PDF text into Haltung chunks
.DESCRIPTION
    Detects boundaries between different Haltungen in PDF text.
    Uses multiple detection strategies: page headers, IDs, table rows.
.PARAMETER PagesText
    Array of page texts from Extract-PdfTextByPage
.RETURNS
    Array of @{ ChunkIndex, PageRange, Text, HaltungKey }
#>
function Split-IntoHaltungChunks {
    param([string[]] $PagesText)
    
    $chunks = @()
    
    if (-not $PagesText -or $PagesText.Count -eq 0) {
        return $chunks
    }
    
    # Kombiniere alle Seiten für Analyse
    $fullText = ($PagesText -join "`n---PAGE-BREAK---`n")
    
    # Regex-Patterns für Haltungs-Erkennung
    $haltungPatterns = @(
        # Standard: H_61474-59652 oder H__61474-59652
        '(?im)^\s*H[_\-]?\s*(\d{5,}[\-\d/]*)',
        # Alternativer Format: Haltungsname: 61474-59652
        '(?im)^\s*Haltungsname\s*[:\-]?\s*(\d{5,}[\-\d/]*)',
        # Haltungsnummer / Haltung Nr
        '(?im)^\s*(Haltungs?nummer|Haltung\s*Nr\.?|Leitung\s*Nr\.?|Leitungsnummer)\s*[:\-]?\s*([\w\.\-\/]+)',
        # Haltung Nr 123
        '(?im)^\s*Haltung\s*(?:Nr\.?|Nummer)?\s*[:\-]?\s*(\d{3,}[\-\d/]*)',
        # Kanalinspektion für H_123
        '(?im)Kanalinspektion\s+(?:f[üu]r\s+)?H[_\-]?\s*(\d{3,})',
        # Tabellen-Header-basiert
        '(?im)^\s*(\d{5,}[\-\d/]*)\s+\S+\s+(?:PVC|PE|PP|GFK|Beton)',
        # ID in Klammern
        '\((\d{5,}[\-\d/]*)\)'
    )
    
    # Finde alle Haltungs-IDs und ihre Positionen
    $foundMatches = @()
    foreach ($pattern in $haltungPatterns) {
        $regexMatches = [regex]::Matches($fullText, $pattern, [System.Text.RegularExpressions.RegexOptions]::Multiline)
        foreach ($m in $regexMatches) {
            $haltungId = ""
            # Finde die nicht-leere Capture-Group
            for ($g = 1; $g -lt $m.Groups.Count; $g++) {
                if ($m.Groups[$g].Value) {
                    $haltungId = $m.Groups[$g].Value
                    break
                }
            }
            
            if ($haltungId -and $haltungId.Length -ge 3) {
                $foundMatches += @{
                    Position = $m.Index
                    Length = $m.Length
                    HaltungId = $haltungId.Trim()
                    FullMatch = $m.Value
                }
            }
        }
    }
    
    # Sortiere nach Position und entferne Duplikate
    $sortedMatches = $foundMatches | Sort-Object { $_.Position }
    $uniqueMatches = @()
    $lastPos = -100
    
    foreach ($m in $sortedMatches) {
        # Ignoriere Matches die zu nahe beieinander liegen (gleiche Zeile)
        if (($m.Position - $lastPos) -gt 50) {
            $uniqueMatches += $m
            $lastPos = $m.Position
        }
    }
    
    Log-Info -Message "PDF-Scan: $($uniqueMatches.Count) potenzielle Haltungen gefunden" -Context "PdfBatchImport"
    
    if ($uniqueMatches.Count -eq 0) {
        # Keine Haltungen erkannt - gib gesamten Text als einen Chunk zurück
        $chunks += @{
            ChunkIndex = 0
            PageRange = "1-$($PagesText.Count)"
            Text = $fullText
            HaltungKey = $null
        }
        return $chunks
    }
    
    # Erstelle Chunks basierend auf gefundenen Positionen
    for ($i = 0; $i -lt $uniqueMatches.Count; $i++) {
        $current = $uniqueMatches[$i]
        $startPos = $current.Position
        
        # Endposition ist entweder nächste Haltung oder Ende
        if ($i -lt ($uniqueMatches.Count - 1)) {
            $endPos = $uniqueMatches[$i + 1].Position
        } else {
            $endPos = $fullText.Length
        }
        
        # Extrahiere Text für diesen Chunk
        $chunkText = $fullText.Substring($startPos, $endPos - $startPos)
        
        # Bestimme Page-Range
        $pageStart = 1
        $pageEnd = $PagesText.Count
        $currentLen = 0
        for ($p = 0; $p -lt $PagesText.Count; $p++) {
            $pageLen = $PagesText[$p].Length + 18  # +18 für "---PAGE-BREAK---\n"
            if ($currentLen -le $startPos -and $startPos -lt ($currentLen + $pageLen)) {
                $pageStart = $p + 1
            }
            if ($currentLen -le $endPos -and $endPos -lt ($currentLen + $pageLen)) {
                $pageEnd = $p + 1
            }
            $currentLen += $pageLen
        }
        
        $chunks += @{
            ChunkIndex = $i
            PageRange = if ($pageStart -eq $pageEnd) { "$pageStart" } else { "$pageStart-$pageEnd" }
            Text = $chunkText
            HaltungKey = $current.HaltungId
        }
    }
    
    Log-Info -Message "PDF in $($chunks.Count) Chunks aufgeteilt" -Context "PdfBatchImport"
    return $chunks
}

<#
.SYNOPSIS
    Extracts Haltung key from chunk text
.DESCRIPTION
    Tries to find a unique Haltung identifier in the text chunk.
.PARAMETER Text
    Chunk of PDF text
.RETURNS
    String with Haltung key/ID or $null
#>
function Get-HaltungKeyFromChunk {
    param([string] $Text)
    
    if ([string]::IsNullOrWhiteSpace($Text)) {
        return $null
    }
    
    # Prioritized patterns
    $patterns = @(
        # Explizites Haltungsname-Label
        '(?im)^\s*Haltungsname\s*[:\-]?\s*([^\r\n]+)',
        # Haltungsnummer / Haltung Nr / Leitung Nr
        '(?im)^\s*(Haltungs?nummer|Haltung\s*Nr\.?|Leitung\s*Nr\.?|Leitungsnummer)\s*[:\-]?\s*([^\r\n]+)',
        # H_XXXXX-XXXXX Format
        '(?im)H[_\-]?\s*(\d{5,}[\-\d/]*)',
        # Haltung Nr...
        '(?im)Haltung\s*(?:Nr\.?|Nummer)?\s*[:\-]?\s*(\d{3,}[\-\d/]*)',
        # Reine Nummern am Zeilenanfang (5+ Ziffern)
        '(?m)^\s*(\d{5,}[\-\d/]*)\s'
    )
    
    foreach ($pattern in $patterns) {
        $match = [regex]::Match($Text, $pattern)
        if ($match.Success) {
            for ($g = 1; $g -lt $match.Groups.Count; $g++) {
                if ($match.Groups[$g].Value) {
                    $key = $match.Groups[$g].Value.Trim()
                    # Bereinige Key
                    $key = $key -replace '^\s*H[_\-]?\s*', ''
                    $key = $key.Trim()
                    if ($key.Length -ge 3) {
                        return $key
                    }
                }
            }
        }
    }
    
    return $null
}

<#
.SYNOPSIS
    Parse fields from a chunk of PDF text
.DESCRIPTION
    Uses the existing Parse-PdfText function to extract all fields.
.PARAMETER Text
    Chunk of PDF text
.RETURNS
    Hashtable with field -> value mappings
#>
function Parse-FieldsFromChunk {
    param([string] $Text)
    
    $fields = Parse-PdfText -Text $Text
    
    # Wenn Haltungsname fehlt, versuche aus Text zu extrahieren
    if (-not $fields.ContainsKey('Haltungsname') -or [string]::IsNullOrWhiteSpace($fields['Haltungsname'])) {
        $key = Get-HaltungKeyFromChunk -Text $Text
        if ($key) {
            $fields['Haltungsname'] = $key
        }
    }
    
    return $fields
}

<#
.SYNOPSIS
    Main PDF Batch Import function
.DESCRIPTION
    Imports multiple Haltungen from a single PDF file.
    - Extracts text by page
    - Splits into Haltung chunks
    - Parses fields from each chunk
    - Matches or creates HaltungRecords
    - Uses MergeService for field updates
    - Never overwrites UserEdited fields
.PARAMETER PdfPath
    Path to PDF file
.PARAMETER Project
    Target Project object
.PARAMETER Mode
    Import mode: "merge" (default), "append", "replace"
.RETURNS
    PSCustomObject with import statistics
#>
function Import-PdfBatch {
    param(
        [string] $PdfPath,
        [Project] $Project,
        [string] $Mode = "merge"
    )
    
    $stats = @{
        Success = $false
        PdfPath = $PdfPath
        TotalChunks = 0
        HaltungenFound = 0
        HaltungenNew = 0
        HaltungenUpdated = 0
        FieldsMerged = 0
        Conflicts = 0
        Errors = 0
        ChunkDetails = @()
        ConflictDetails = @()
        ErrorDetails = @()
        StartTime = (Get-Date)
        EndTime = $null
    }
    
    try {
        Log-Info -Message "=== PDF Batch Import Start: $PdfPath ===" -Context "PdfBatchImport"
        
        # 1. Extrahiere Text seitenweise
        $pagesText = Extract-PdfTextByPage -PdfPath $PdfPath
        
        if (-not $pagesText -or $pagesText.Count -eq 0) {
            # Fallback: Alte Methode
            $fullText = Extract-PdfText -PdfPath $PdfPath
            if ($fullText) {
                $pagesText = @($fullText)
            } else {
                throw "PDF enthält keinen lesbaren Text"
            }
        }
        
        Log-Info -Message "PDF-Seiten extrahiert: $($pagesText.Count)" -Context "PdfBatchImport"
        
        # 2. Aufteilen in Chunks
        $chunks = Split-IntoHaltungChunks -PagesText $pagesText
        $stats.TotalChunks = $chunks.Count
        
        if ($chunks.Count -eq 0) {
            throw "Keine Haltungen im PDF erkannt"
        }
        
        # 3. Jeden Chunk verarbeiten
        foreach ($chunk in $chunks) {
            $chunkDetail = @{
                Index = $chunk.ChunkIndex
                PageRange = $chunk.PageRange
                HaltungKey = $chunk.HaltungKey
                ExtractedFields = @{}
                Action = ""
                FieldsUpdated = 0
                Conflicts = @()
                Error = $null
            }
            
            try {
                # Parse Felder aus Chunk
                $parsedFields = Parse-FieldsFromChunk -Text $chunk.Text
                $chunkDetail.ExtractedFields = $parsedFields
                
                # Bestimme Haltungsname
                $haltungName = if ($chunk.HaltungKey) { $chunk.HaltungKey } 
                              elseif ($parsedFields['Haltungsname']) { $parsedFields['Haltungsname'] }
                              else { $null }
                
                $chunkDetail.HaltungKey = $haltungName
                
                if (-not $haltungName) {
                    $chunkDetail.Error = "Kein Haltungsname im Chunk gefunden"
                    $stats.Errors++
                    $stats.ErrorDetails += $chunkDetail.Error
                    $stats.ChunkDetails += $chunkDetail
                    continue
                }
                
                Log-Info -Message "Verarbeite Chunk $($chunk.ChunkIndex): Haltung '$haltungName', Seiten $($chunk.PageRange), $($parsedFields.Count) Felder" -Context "PdfBatchImport"
                
                # Suche existierende Haltung
                $existingRecord = $null
                foreach ($rec in $Project.Data) {
                    $recName = $rec.GetFieldValue('Haltungsname')
                    if ($recName -and ($recName -eq $haltungName -or $recName -match [regex]::Escape($haltungName))) {
                        $existingRecord = $rec
                        break
                    }
                }
                
                if ($existingRecord) {
                    # UPDATE existierenden Record
                    $chunkDetail.Action = "update"
                    $stats.HaltungenUpdated++
                    $stats.HaltungenFound++
                    
                    foreach ($fieldName in $parsedFields.Keys) {
                        try {
                            $newValue = $parsedFields[$fieldName]
                            $currentValue = $existingRecord.GetFieldValue($fieldName)
                            $fieldMeta = $existingRecord.FieldMeta[$fieldName]
                            
                            if (-not $fieldMeta) {
                                $fieldMeta = New-Object FieldMetadata
                                $fieldMeta.FieldName = $fieldName
                                $existingRecord.FieldMeta[$fieldName] = $fieldMeta
                            }
                            
                            $mergeResult = Merge-Field -FieldName $fieldName `
                                -CurrentValue $currentValue `
                                -NewValue $newValue `
                                -FieldMeta $fieldMeta `
                                -NewSource "pdf" `
                                -AllowConflicts $true
                            
                            if ($mergeResult.Merged) {
                                $existingRecord.SetFieldValue($fieldName, $mergeResult.NewValue, "pdf", $false)
                                $chunkDetail.FieldsUpdated++
                                $stats.FieldsMerged++
                            } elseif ($mergeResult.Conflict) {
                                $chunkDetail.Conflicts += $mergeResult.Conflict
                                $stats.Conflicts++
                                $stats.ConflictDetails += $mergeResult.Conflict
                            }
                        } catch {
                            Log-Warn -Message "Fehler beim Merge von Feld '$fieldName': $_" -Context "PdfBatchImport"
                        }
                    }
                } else {
                    # CREATE neuen Record
                    $chunkDetail.Action = "create"
                    $stats.HaltungenNew++
                    $stats.HaltungenFound++
                    
                    $newRecord = $Project.CreateNewRecord()
                    
                    foreach ($fieldName in $parsedFields.Keys) {
                        $newRecord.SetFieldValue($fieldName, $parsedFields[$fieldName], "pdf", $false)
                        $chunkDetail.FieldsUpdated++
                        $stats.FieldsMerged++
                    }
                    
                    $Project.AddRecord($newRecord)
                    Log-Info -Message "Neue Haltung erstellt: $haltungName (NR: $($newRecord.GetFieldValue('NR')))" -Context "PdfBatchImport"
                }
                
            } catch {
                $chunkDetail.Error = $_.ToString()
                $stats.Errors++
                $stats.ErrorDetails += "Chunk $($chunk.ChunkIndex): $_"
                Log-Error -Message "Fehler bei Chunk $($chunk.ChunkIndex): $_" -Context "PdfBatchImport"
            }
            
            $stats.ChunkDetails += $chunkDetail
        }
        
        # 4. Import-History aktualisieren
        $historyEntry = @{
            Timestamp = (Get-Date).ToUniversalTime()
            Source = "pdf"
            FilePath = $PdfPath
            FileName = [System.IO.Path]::GetFileName($PdfPath)
            RecordsFound = $stats.HaltungenFound
            RecordsNew = $stats.HaltungenNew
            RecordsUpdated = $stats.HaltungenUpdated
            FieldsMerged = $stats.FieldsMerged
            Conflicts = $stats.Conflicts
        }
        
        $Project.ImportHistory.Add($historyEntry)
        $Project.Dirty = $true
        $Project.ModifiedAt = (Get-Date).ToUniversalTime()
        
        $stats.Success = $true
        
    } catch {
        $stats.Errors++
        $stats.ErrorDetails += $_.ToString()
        Log-Error -Message "PDF Batch Import Fehler: $_" -Context "PdfBatchImport" -Exception $_
    }
    
    $stats.EndTime = (Get-Date)
    
    # Log Summary
    Log-Info -Message ("=== PDF Batch Import Abgeschlossen ===" +
        "`n  Datei: $PdfPath" +
        "`n  Chunks: $($stats.TotalChunks)" +
        "`n  Haltungen gefunden: $($stats.HaltungenFound)" +
        "`n  - Neu erstellt: $($stats.HaltungenNew)" +
        "`n  - Aktualisiert: $($stats.HaltungenUpdated)" +
        "`n  Felder gemerged: $($stats.FieldsMerged)" +
        "`n  Konflikte: $($stats.Conflicts)" +
        "`n  Fehler: $($stats.Errors)") -Context "PdfBatchImport"
    
    return $stats
}

<#
.SYNOPSIS
    Shows import result summary dialog
.DESCRIPTION
    Displays a WPF MessageBox with import statistics
.PARAMETER Stats
    Statistics hashtable from Import-PdfBatch
#>
function Show-PdfBatchImportSummary {
    param([hashtable] $Stats)
    
    $duration = if ($Stats.EndTime -and $Stats.StartTime) {
        ($Stats.EndTime - $Stats.StartTime).TotalSeconds
    } else { 0 }
    
    $icon = if ($Stats.Success) { 
        [System.Windows.MessageBoxImage]::Information 
    } else { 
        [System.Windows.MessageBoxImage]::Warning 
    }
    
    $message = @"
PDF Batch Import abgeschlossen

Datei: $([System.IO.Path]::GetFileName($Stats.PdfPath))
Dauer: $([math]::Round($duration, 1)) Sekunden

Ergebnis:
- Chunks verarbeitet: $($Stats.TotalChunks)
- Haltungen gefunden: $($Stats.HaltungenFound)
  - Neu erstellt: $($Stats.HaltungenNew)
  - Aktualisiert: $($Stats.HaltungenUpdated)
- Felder gemerged: $($Stats.FieldsMerged)
- Konflikte (UserEdited): $($Stats.Conflicts)
- Fehler: $($Stats.Errors)
"@
    
    if ($Stats.Conflicts -gt 0) {
        $message += "`n`nHinweis: $($Stats.Conflicts) Felder wurden nicht uberschrieben, da sie manuell bearbeitet wurden."
    }
    
    if ($Stats.Errors -gt 0) {
        $message += "`n`nFehler-Details:`n"
        $Stats.ErrorDetails | Select-Object -First 3 | ForEach-Object {
            $message += "- $_`n"
        }
    }
    
    [System.Windows.MessageBox]::Show($message, "PDF Batch Import", 
        [System.Windows.MessageBoxButton]::OK, $icon)
}

# ========== WinCanVX-spezifischer Parser ==========
<#
.SYNOPSIS
    Parse-WinCanVxPage: Parst eine WinCanVX Haltungsinspektion-Seite
.DESCRIPTION
    Erkennt das strukturierte Layout von WinCanVX PDFs und extrahiert alle Felder
#>
function Parse-WinCanVxPage {
    param([string] $PageText)
    
    $result = @{
        IsWinCanVx = $false
        Haltungsname = ""
        Felder = @{}
        Schaeden = @()
    }
    
    if ([string]::IsNullOrWhiteSpace($PageText)) {
        return $result
    }
    
    # Prüfe ob es eine WinCanVX Haltungsinspektion-Seite ist
    if ($PageText -notmatch 'Haltungsinspektion\s*-\s*(\d{2}\.\d{2}\.\d{4})\s*-\s*(\S+)') {
        return $result
    }
    
    $result.IsWinCanVx = $true
    $result.Felder['Datum_Jahr'] = $matches[1]
    $result.Haltungsname = $matches[2]
    $result.Felder['Haltungsname'] = $matches[2]
    
    # Extrahiere Felder aus dem strukturierten Layout
    # Format: "Label        Wert" oder "Label  Wert  Label2  Wert2"
    
    # Ort
    if ($PageText -match '(?m)^Ort\s+(\d{4}\s+\S+)') {
        $result.Felder['Bemerkungen'] = "Ort: $($matches[1].Trim())"
    }
    
    # Strasse
    if ($PageText -match '(?m)^Strasse\s+(.+?)(?:\s{2,}|$)') {
        $result.Felder['Strasse'] = $matches[1].Trim()
    }
    
    # Schacht oben/unten -> Fliessrichtung
    $schachtOben = ""
    $schachtUnten = ""
    if ($PageText -match '(?m)Schacht\s+oben\s+(\S+)') {
        $schachtOben = $matches[1].Trim()
    }
    if ($PageText -match '(?m)Schacht\s+unten\s+(\S+)') {
        $schachtUnten = $matches[1].Trim()
    }
    if ($schachtOben -and $schachtUnten) {
        $result.Felder['Fliessrichtung'] = "Von $schachtOben nach $schachtUnten"
    }
    
    # Rohrlänge / HL [m]
    if ($PageText -match '(?m)HL\s*\[m\]\s+([0-9]+(?:[.,][0-9]+)?)') {
        $result.Felder['Haltungslaenge_m'] = $matches[1] -replace ',', '.'
    } elseif ($PageText -match '(?m)Rohrl.nge\s*\[m\]\s+([0-9]+(?:[.,][0-9]+)?)') {
        $result.Felder['Haltungslaenge_m'] = $matches[1] -replace ',', '.'
    }
    
    # Profil (DN)
    if ($PageText -match '(?m)Profil\s+(?:Kreisprofil\s+)?(\d+)\s*mm') {
        $result.Felder['DN_mm'] = $matches[1]
    } elseif ($PageText -match '(?m)Kreisprofil\s+(\d+)\s*mm') {
        $result.Felder['DN_mm'] = $matches[1]
    }
    
    # Material
    if ($PageText -match '(?m)^Material\s+(\S+)') {
        $material = $matches[1].Trim()
        # Mapping
        switch -Regex ($material) {
            'Polyethylen'       { $material = 'Kunststoff PE' }
            'Polyvinylchlorid'  { $material = 'Kunststoff PVC' }
            'Polypropylen'      { $material = 'Kunststoff PP' }
            'Asbestzement'      { $material = 'Asbestzement' }
        }
        $result.Felder['Rohrmaterial'] = $material
    }
    
    # Nutzungsart
    if ($PageText -match '(?m)^Nutzungsart\s+(\S+)') {
        $nutzung = $matches[1].Trim()
        switch -Regex ($nutzung) {
            'Schmutzabwasser' { $nutzung = 'Schmutzwasser' }
            'Regenabwasser'   { $nutzung = 'Regenwasser' }
        }
        $result.Felder['Nutzungsart'] = $nutzung
    }
    
    # Baujahr
    if ($PageText -match '(?m)Baujahr\s+(\d{4})') {
        # Nur als zusätzliche Info, Datum_Jahr ist Inspektionsdatum
        if ($result.Felder.ContainsKey('Bemerkungen')) {
            $result.Felder['Bemerkungen'] += "`nBaujahr: $($matches[1])"
        } else {
            $result.Felder['Bemerkungen'] = "Baujahr: $($matches[1])"
        }
    }
    
    # Grund der Inspektion
    if ($PageText -match '(?m)Grund\s+(Zustandskontrolle|Abnahme|Wartung|Sanierung)') {
        if ($result.Felder.ContainsKey('Bemerkungen')) {
            $result.Felder['Bemerkungen'] += "`nGrund: $($matches[1])"
        }
    }
    
    # Gereinigt
    if ($PageText -match '(?m)Gereinigt\s+(Ja|Nein)') {
        if ($result.Felder.ContainsKey('Bemerkungen')) {
            $result.Felder['Bemerkungen'] += "`nGereinigt: $($matches[1])"
        }
    }
    
    # Bemerkungen aus PDF
    if ($PageText -match '(?m)^Bemerkungen\s+(.+?)(?:\s{2,}|$)') {
        $bem = $matches[1].Trim()
        if ($bem -and $bem -ne 'n') {
            if ($result.Felder.ContainsKey('Bemerkungen')) {
                $result.Felder['Bemerkungen'] += "`n$bem"
            } else {
                $result.Felder['Bemerkungen'] = $bem
            }
        }
    }
    
    # Schäden extrahieren (Zeilen mit Schadencode wie A01, B02, etc.)
    $schadenPattern = '(?m)^\s*([0-9.]+)\s+(A\d{2}|B\d{2})\s+(.+?)(?:\s+\d{2}:\d{2}:\d{2})'
    $schadenMatches = [regex]::Matches($PageText, $schadenPattern)
    
    foreach ($sm in $schadenMatches) {
        $distanz = $sm.Groups[1].Value
        $code = $sm.Groups[2].Value
        $beschreibung = $sm.Groups[3].Value.Trim()
        
        # Nur Start-Schäden (A-Codes sind Start, B-Codes sind Ende)
        if ($code -match '^A') {
            $result.Schaeden += "$code @${distanz}m: $beschreibung"
        }
    }
    
    # Primäre Schäden zusammenfassen
    if ($result.Schaeden.Count -gt 0) {
        if ($result.Schaeden.Count -gt 5) {
            $result.Felder['Primaere_Schaeden'] = ($result.Schaeden | Select-Object -First 5) -join "`n"
            $result.Felder['Primaere_Schaeden'] += "`n... ($($result.Schaeden.Count) Befunde)"
        } else {
            $result.Felder['Primaere_Schaeden'] = $result.Schaeden -join "`n"
        }
        
        # Zustandsklasse aus Schadencodes berechnen (WinCanVX-Format: A01, A02, B01, etc.)
        # A-Codes sind Anfang, B-Codes sind Ende von Streckenschäden
        # Die Schwere wird aus dem Beschreibungstext abgeleitet
        $maxZustandsklasse = 0
        foreach ($schaden in $result.Schaeden) {
            # Prüfe auf kritische Begriffe
            if ($schaden -match 'deformiert|einbruch|einsturz|verstopf') {
                if ($maxZustandsklasse -lt 4) { $maxZustandsklasse = 4 }
            } elseif ($schaden -match 'riss|bruch|wurzel|ablagerung|korrosion') {
                if ($maxZustandsklasse -lt 3) { $maxZustandsklasse = 3 }
            } elseif ($schaden -match 'versatz|verbindung|undicht') {
                if ($maxZustandsklasse -lt 2) { $maxZustandsklasse = 2 }
            } elseif ($maxZustandsklasse -lt 1) {
                $maxZustandsklasse = 1
            }
        }
        
        if ($maxZustandsklasse -gt 0) {
            $result.Felder['Zustandsklasse'] = $maxZustandsklasse.ToString()
            # Sanieren Ja/Nein basierend auf Zustandsklasse
            if ($maxZustandsklasse -ge 3) {
                $result.Felder['Sanieren_JaNein'] = 'Ja'
            } else {
                $result.Felder['Sanieren_JaNein'] = 'Nein'
            }
        }
    }
    
    # Status: Nach Inspektion = offen (zur Bearbeitung)
    if (-not $result.Felder.ContainsKey('Offen_abgeschlossen')) {
        $result.Felder['Offen_abgeschlossen'] = 'offen'
    }
    
    # Video-Link (Film-Feld)
    if ($PageText -match '(?m)^Film\s+(\S+\.mpg)') {
        $result.Felder['Link'] = $matches[1]
    }
    
    return $result
}

<#
.SYNOPSIS
    Parse-WinCanVxAufmassliste: Parst die Aufmassliste-Tabelle aus WinCanVX PDFs
.DESCRIPTION
    Extrahiert Haltungen aus der tabellarischen Aufmassliste
#>
function Parse-WinCanVxAufmassliste {
    param([string] $PageText)
    
    $haltungen = @()
    
    if ($PageText -notmatch 'Aufmassliste') {
        return $haltungen
    }
    
    # Tabellenzeilen-Pattern:
    # Nr.  Schacht_oben  Schacht_unten  Datum  Strasse  Funktion  Material  HL[m]  Insp.Länge[m]
    # Beispiel: 24    7.999003    35920      11.06.2019         Wilerstrasse                    Polyvinylchlorid   11.22      11.22
    
    $rowPattern = '(?m)^\s*(\d+)\s+(\S+)\s+(\S+)\s+(\d{2}\.\d{2}\.\d{4})\s+(\S+)\s+(\S*)\s+(\S+)\s+([0-9.,]+)\s+([0-9.,]+)'
    $matches = [regex]::Matches($PageText, $rowPattern)
    
    foreach ($m in $matches) {
        $nr = $m.Groups[1].Value
        $schachtOben = $m.Groups[2].Value
        $schachtUnten = $m.Groups[3].Value
        $datum = $m.Groups[4].Value
        $strasse = $m.Groups[5].Value
        $funktion = $m.Groups[6].Value
        $material = $m.Groups[7].Value
        $hl = $m.Groups[8].Value -replace ',', '.'
        $inspLaenge = $m.Groups[9].Value -replace ',', '.'
        
        # Haltungsname aus Schacht oben - Schacht unten
        $haltungsname = "$schachtOben-$schachtUnten"
        
        # Material-Mapping
        switch -Regex ($material) {
            'Polyethylen'       { $material = 'Kunststoff PE' }
            'Polyvinylchlorid'  { $material = 'Kunststoff PVC' }
            'Polypropylen'      { $material = 'Kunststoff PP' }
            'Asbestzement'      { $material = 'Asbestzement' }
        }
        
        $haltungen += @{
            Id = $haltungsname
            Felder = @{
                'Haltungsname' = $haltungsname
                'NR' = $nr
                'Strasse' = $strasse
                'Rohrmaterial' = $material
                'Haltungslaenge_m' = $hl
                'Datum_Jahr' = $datum
                'Fliessrichtung' = "Von $schachtOben nach $schachtUnten"
            }
            Source = 'pdf'
        }
    }
    
    return $haltungen
}

# ========== IBAK/IKAS-spezifischer Parser ==========
<#
.SYNOPSIS
    Parse-IbakPage: Parst eine IBAK/IKAS Leitungsgrafik-Seite
.DESCRIPTION
    Erkennt das strukturierte Layout von IBAK PDFs und extrahiert alle Felder
#>
function Parse-IbakPage {
    param([string] $PageText)
    
    $result = @{
        IsIbak = $false
        Haltungsname = ""
        Felder = @{}
        Schaeden = @()
    }
    
    if ([string]::IsNullOrWhiteSpace($PageText)) {
        return $result
    }
    
    # Prüfe ob es eine IBAK-Seite ist (Leitungsgrafik oder Leitungsbildbericht)
    if ($PageText -notmatch 'Leitungsgrafik|Leitungsbildbericht') {
        # Prüfe auf Inhaltsverzeichnis-Zeile
        if ($PageText -match 'Inhaltsverzeichnis') {
            return $result
        }
        return $result
    }
    
    $result.IsIbak = $true
    
    # Leitung / Haltungsname (Format: "Leitung    59965-10.62293")
    if ($PageText -match '(?m)^Leitung\s+(\S+)') {
        $result.Haltungsname = $matches[1].Trim()
        $result.Felder['Haltungsname'] = $matches[1].Trim()
    }
    
    # Oberer Punkt / Schacht oben
    $schachtOben = ""
    $schachtUnten = ""
    if ($PageText -match '(?m)Oberer\s+(?:Schacht|Punkt)\s+(\S+)') {
        $schachtOben = $matches[1].Trim()
    }
    if ($PageText -match '(?m)Unterer\s+(?:Schacht|Punkt)\s+(\S+)') {
        $schachtUnten = $matches[1].Trim()
    }
    if ($schachtOben -and $schachtUnten) {
        $result.Felder['Fliessrichtung'] = "Von $schachtOben nach $schachtUnten"
    }
    
    # Strasse / Standort
    if ($PageText -match '(?m)Stra(?:ß|ss)e[/\s]*Standort\s+(.+?)(?:\s{2,}|$)') {
        $result.Felder['Strasse'] = $matches[1].Trim()
    }
    
    # Ort
    if ($PageText -match '(?m)^Ort\s+(\d{4}\s+.+?)(?:\s{2,}|$)') {
        if ($result.Felder.ContainsKey('Bemerkungen')) {
            $result.Felder['Bemerkungen'] += "`nOrt: $($matches[1].Trim())"
        } else {
            $result.Felder['Bemerkungen'] = "Ort: $($matches[1].Trim())"
        }
    }
    
    # Inspektionsdatum
    if ($PageText -match '(?m)Insp\.?[-\s]?[Dd]atum\s+(\d{2}\.\d{2}\.\d{4})') {
        $result.Felder['Datum_Jahr'] = $matches[1]
    }
    
    # Dimension [mm] (Format: "150 / 150" oder "Dimension [mm]    150 / 150")
    if ($PageText -match '(?m)Dimension\s*\[mm\]\s+(\d+)\s*/\s*\d+') {
        $result.Felder['DN_mm'] = $matches[1]
    } elseif ($PageText -match '(?m)Dimension\s+(\d+)\s*/\s*\d+') {
        $result.Felder['DN_mm'] = $matches[1]
    }
    
    # Leitungslänge / Inspektionslänge
    if ($PageText -match '(?m)Leitungsl[äa]nge\s+([0-9]+(?:[.,][0-9]+)?)\s*m') {
        $result.Felder['Haltungslaenge_m'] = $matches[1] -replace ',', '.'
    } elseif ($PageText -match '(?m)Inspektionsl[äa]nge\s+([0-9]+(?:[.,][0-9]+)?)\s*m') {
        $result.Felder['Haltungslaenge_m'] = $matches[1] -replace ',', '.'
    }
    
    # Material
    if ($PageText -match '(?m)^Material\s+(\S+)') {
        $material = $matches[1].Trim()
        # Material-Mapping
        switch -Regex ($material) {
            'Normalbeton'       { $material = 'Beton' }
            'Polyethylen'       { $material = 'Kunststoff PE' }
            'Polyvinylchlorid'  { $material = 'Kunststoff PVC' }
            'Polypropylen'      { $material = 'Kunststoff PP' }
            'Asbestzement'      { $material = 'Asbestzement' }
            'Steinzeug'         { $material = 'Steinzeug' }
        }
        $result.Felder['Rohrmaterial'] = $material
    }
    
    # Nutzungsart / Kanalart
    if ($PageText -match '(?m)(?:Nutzungsart|Kanalart)\s+(\S+)') {
        $nutzung = $matches[1].Trim()
        switch -Regex ($nutzung) {
            'Schmutzabwasser' { $nutzung = 'Schmutzwasser' }
            'Regenabwasser'   { $nutzung = 'Regenwasser' }
        }
        $result.Felder['Nutzungsart'] = $nutzung
    }
    
    # Inspektionsgrund
    if ($PageText -match '(?m)Insp\.?[-\s]?Grund\s+(.+?)(?:\s{2,}|$)') {
        $grund = $matches[1].Trim()
        if ($result.Felder.ContainsKey('Bemerkungen')) {
            $result.Felder['Bemerkungen'] += "`nGrund: $grund"
        } else {
            $result.Felder['Bemerkungen'] = "Grund: $grund"
        }
    }
    
    # Operateur
    if ($PageText -match '(?m)Operateur\s+(.+?)(?:\s{2,}|$)') {
        $op = $matches[1].Trim()
        if ($result.Felder.ContainsKey('Bemerkungen')) {
            $result.Felder['Bemerkungen'] += "`nOperateur: $op"
        }
    }
    
    # Schäden extrahieren (Zustandscodes wie BAA, BAB, BBA, BBC, etc.)
    # Format: "Zustand  BAJ.A Entf. gegen Fließr.  0.20 m" oder "Zustand BBA.C"
    $schadenPattern = '(?m)Zustand\s+(B[A-Z]{2}(?:\.[A-Z])?\.?)\s+.*?([0-9]+(?:[.,][0-9]+)?)\s*m'
    $schadenMatches = [regex]::Matches($PageText, $schadenPattern)
    
    foreach ($sm in $schadenMatches) {
        $code = $sm.Groups[1].Value.Trim()
        $distanz = $sm.Groups[2].Value -replace ',', '.'
        
        # Beschreibung aus der gleichen Zeile extrahieren
        $beschreibung = ""
        if ($PageText -match "Zustand\s+$([regex]::Escape($code)).*?$([regex]::Escape($distanz))\s*m\s*(.+?)(?:\r?\n|$)") {
            # Nächste Zeile enthält oft die Beschreibung
        }
        
        $result.Schaeden += "$code @${distanz}m"
    }
    
    # Alternative: Schadensbeschreibungen aus Bildunterschriften
    # Format: "Pos: 4 - 8; Komplexes Wurzelwerk, Querschnittsreduzierung = 20%"
    $beschrPattern = '(?m)Pos:\s*\d+(?:\s*-\s*\d+)?;\s*(.+?)(?:\r?\n|$)'
    $beschrMatches = [regex]::Matches($PageText, $beschrPattern)
    
    foreach ($bm in $beschrMatches) {
        $beschreibung = $bm.Groups[1].Value.Trim()
        if ($beschreibung -and $beschreibung -notmatch 'Allgemeinzustand|Fotobeispiel|Rohranfang|Rohrende') {
            $result.Schaeden += $beschreibung
        }
    }
    
    # Primäre Schäden zusammenfassen
    if ($result.Schaeden.Count -gt 0) {
        # Entferne Duplikate und limitiere
        $uniqueSchaeden = $result.Schaeden | Select-Object -Unique
        if ($uniqueSchaeden.Count -gt 5) {
            $result.Felder['Primaere_Schaeden'] = ($uniqueSchaeden | Select-Object -First 5) -join "`n"
            $result.Felder['Primaere_Schaeden'] += "`n... ($($uniqueSchaeden.Count) Befunde)"
        } else {
            $result.Felder['Primaere_Schaeden'] = $uniqueSchaeden -join "`n"
        }
        
        # Zustandsklasse aus DIN-EN-13508-2 Schadencodes berechnen
        # Format: BXY wobei X die Hauptkategorie und Y der Schadentyp ist
        # BAA-BAJ = Deformation (Zustandsklasse je nach Schwere 2-4)
        # BBA-BBF = Bruch/Riss (Zustandsklasse 3-4)
        # BCA-BCE = Defekte Rohrverbindung (Zustandsklasse 2-3)
        # BDA = Oberflächenschaden (Zustandsklasse 2)
        $maxZustandsklasse = 0
        $empfohlene = @{}  # Sammelt empfohlene Massnahmen
        
        foreach ($schaden in $result.Schaeden) {
            $zk = 1  # Default
            $massnahme = $null
            
            # DIN-Codes analysieren und Massnahme vorschlagen
            if ($schaden -match 'BAA|BAB\.A|Deformation.*stark|Einsturz') {
                $zk = 4
                $massnahme = 'Erneuerung/Neubau'
            } elseif ($schaden -match 'BBA|BBB|BBC|Riss.*breit|Bruch|Scherbe') {
                $zk = 4
                $massnahme = 'Kurzliner/Manschette'
            } elseif ($schaden -match 'BAB|BAC|BAD|Deformation|Bogen') {
                $zk = 3
                $massnahme = 'Inliner'
            } elseif ($schaden -match 'BBD|BBE|Riss|Längsriss|Querriss') {
                $zk = 3
                $massnahme = 'Kurzliner/Inliner'
            } elseif ($schaden -match 'BCA|BCB|BCC|Verbindung|Versatz|Spalt') {
                $zk = 2
                $massnahme = 'Anschlüsse verpressen'
            } elseif ($schaden -match 'Wurzel|Einwuchs') {
                $zk = 3
                $massnahme = 'Wurzelfräsen + Inliner'
            } elseif ($schaden -match 'Ablagerung|Inkrustation') {
                $zk = 2
                $massnahme = 'HD-Reinigung'
            } elseif ($schaden -match 'BDA|BDB|Oberfläche|Korrosion|Zuschlag') {
                $zk = 2
                $massnahme = 'Inliner'
            } elseif ($schaden -match 'BAJ|Undicht|Infiltration|Exfiltration') {
                $zk = 3
                $massnahme = 'Inliner/Verpressen'
            }
            
            if ($massnahme -and -not $empfohlene.ContainsKey($massnahme)) {
                $empfohlene[$massnahme] = $true
            }
            
            if ($zk -gt $maxZustandsklasse) {
                $maxZustandsklasse = $zk
            }
        }
        
        if ($maxZustandsklasse -gt 0) {
            $result.Felder['Zustandsklasse'] = $maxZustandsklasse.ToString()
            # Sanieren Ja/Nein basierend auf Zustandsklasse (ZK ≥ 3 = Sanieren)
            if ($maxZustandsklasse -ge 3) {
                $result.Felder['Sanieren_JaNein'] = 'Ja'
            } else {
                $result.Felder['Sanieren_JaNein'] = 'Nein'
            }
        }
        
        # Empfohlene Sanierungsmassnahmen aus Schadenanalyse
        if ($empfohlene.Count -gt 0) {
            # Prioritätsreihenfolge für Massnahmen
            $prioritaet = @{
                'Erneuerung/Neubau' = 1
                'Inliner' = 2
                'Kurzliner/Manschette' = 3
                'Kurzliner/Inliner' = 4
                'Wurzelfräsen + Inliner' = 5
                'Inliner/Verpressen' = 6
                'Anschlüsse verpressen' = 7
                'HD-Reinigung' = 8
            }
            $sortiert = $empfohlene.Keys | Sort-Object { if ($prioritaet.ContainsKey($_)) { $prioritaet[$_] } else { 99 } }
            $result.Felder['Empfohlene_Sanierungsmassnahmen'] = $sortiert -join ", "
        }
    }
    
    # Status: Nach Inspektion = offen (zur Bearbeitung)
    if (-not $result.Felder.ContainsKey('Offen_abgeschlossen')) {
        $result.Felder['Offen_abgeschlossen'] = 'offen'
    }
    
    return $result
}

<#
.SYNOPSIS
    Parse-IbakStatistik: Parst die Statistik-Tabelle aus IBAK PDFs
.DESCRIPTION
    Extrahiert Haltungen aus der Anschlussleitung-Statistik Tabelle
#>
function Parse-IbakStatistik {
    param([string] $PageText)
    
    $haltungen = @()
    
    if ($PageText -notmatch 'Statistik|Anschlussleitung') {
        return $haltungen
    }
    
    # Tabellenzeilen-Pattern:
    # Datum  Leitung  Straße/Standort  Material  Insp.L.[m]  Dim[mm]  Fotos
    # 12.12.2025   59965-10.62293   Klausenstrasse   Normalbeton   1.30   150 / 150   6
    
    $rowPattern = '(?m)^(\d{2}\.\d{2}\.\d{4})\s+(\S+)\s+(\S+)\s+(\S+)\s+([0-9.,]+)\s+(\d+)\s*/\s*\d+'
    $rowMatches = [regex]::Matches($PageText, $rowPattern)
    
    foreach ($m in $rowMatches) {
        $datum = $m.Groups[1].Value
        $leitung = $m.Groups[2].Value
        $strasse = $m.Groups[3].Value
        $material = $m.Groups[4].Value
        $inspLaenge = $m.Groups[5].Value -replace ',', '.'
        $dn = $m.Groups[6].Value
        
        # Material-Mapping
        switch -Regex ($material) {
            'Normalbeton'       { $material = 'Beton' }
            'Polyethylen'       { $material = 'Kunststoff PE' }
            'Polyvinylchlorid'  { $material = 'Kunststoff PVC' }
            'Polypropylen'      { $material = 'Kunststoff PP' }
        }
        
        $haltungen += @{
            Id = $leitung
            Felder = @{
                'Haltungsname' = $leitung
                'Strasse' = $strasse
                'Rohrmaterial' = $material
                'Haltungslaenge_m' = $inspLaenge
                'DN_mm' = $dn
                'Datum_Jahr' = $datum
            }
            Source = 'pdf'
        }
    }
    
    return $haltungen
}

<#
.SYNOPSIS
    Enrich-HaltungenFromXtf: Ergänzt Haltungen mit Daten aus begleitendem XTF
.DESCRIPTION
    Sucht nach XTF-Datei neben der PDF und extrahiert Eigentuemer, Baujahr etc.
#>
function Enrich-HaltungenFromXtf {
    param(
        [string] $PdfPath,
        [hashtable[]] $Haltungen
    )
    
    $folder = Split-Path $PdfPath -Parent
    $baseName = [System.IO.Path]::GetFileNameWithoutExtension($PdfPath)
    
    # Suche nach passender XTF-Datei (SIA405 bevorzugt)
    $xtfPath = $null
    $possibleXtfs = @(
        (Join-Path $folder "${baseName}_SIA405.xtf"),
        (Join-Path $folder "${baseName}.xtf")
    )
    
    foreach ($candidate in $possibleXtfs) {
        if (Test-Path $candidate) {
            $xtfPath = $candidate
            break
        }
    }
    
    if (-not $xtfPath) {
        return $Haltungen
    }
    
    try {
        Log-Info -Message "Versuche XTF-Anreicherung: $xtfPath" -Context "PdfImport"
        
        # XTF parsen
        [xml]$xml = Get-Content -Path $xtfPath -Encoding UTF8
        
        # Eigentuemer-Mapping aufbauen (Bezeichnung -> Eigentuemer)
        $eigentuemerMap = @{}
        
        # Finde alle Kanaele/Haltungen mit Eigentuemer
        $allNodes = $xml.SelectNodes('//*')
        foreach ($node in $allNodes) {
            $nodeName = $node.LocalName
            
            if ($nodeName -match '\.Haltung$' -or $nodeName -match '\.Kanal$') {
                $bezeichnung = ""
                $eigentuemer = ""
                
                foreach ($child in $node.ChildNodes) {
                    if ($child.LocalName -eq 'Bezeichnung') {
                        $bezeichnung = $child.InnerText.Trim()
                    }
                    if ($child.LocalName -eq 'Eigentuemer') {
                        $eigentuemer = $child.InnerText.Trim()
                    }
                }
                
                if ($bezeichnung -and $eigentuemer) {
                    $eigentuemerMap[$bezeichnung] = $eigentuemer
                }
            }
        }
        
        # Anreichern
        $enriched = 0
        foreach ($h in $Haltungen) {
            $hName = $h.Felder['Haltungsname']
            if ($hName -and $eigentuemerMap.ContainsKey($hName)) {
                $h.Felder['Eigentuemer'] = $eigentuemerMap[$hName]
                $enriched++
            }
        }
        
        if ($enriched -gt 0) {
            Log-Info -Message "XTF-Anreicherung: $enriched Haltungen mit Eigentuemer ergänzt" -Context "PdfImport"
        }
        
    } catch {
        Log-Warning -Message "XTF-Anreicherung fehlgeschlagen: $($_.Exception.Message)" -Context "PdfImport"
    }
    
    return $Haltungen
}

<#
.SYNOPSIS
    Enrich-HaltungenWithVideoLinks: Sucht Video-Dateien und setzt Link-Feld
.DESCRIPTION
    Sucht nach H__HALTUNGSNAME.mpg Dateien in Film/ Ordner
#>
function Enrich-HaltungenWithVideoLinks {
    param(
        [string] $PdfPath,
        [hashtable[]] $Haltungen
    )
    
    $folder = Split-Path $PdfPath -Parent
    
    # Mögliche Video-Ordner
    $videoFolders = @(
        (Join-Path $folder 'Film'),
        (Join-Path $folder 'Video'),
        (Join-Path $folder '..' 'Film'),
        (Join-Path $folder '..' 'Video')
    )
    
    $videoFiles = @()
    foreach ($vf in $videoFolders) {
        if (Test-Path $vf) {
            $videoFiles += Get-ChildItem -Path $vf -Filter "*.mpg" -File -ErrorAction SilentlyContinue
            $videoFiles += Get-ChildItem -Path $vf -Filter "*.mp4" -File -ErrorAction SilentlyContinue
            $videoFiles += Get-ChildItem -Path $vf -Filter "*.avi" -File -ErrorAction SilentlyContinue
        }
    }
    
    if ($videoFiles.Count -eq 0) {
        return $Haltungen
    }
    
    # Index aufbauen (Dateiname ohne Extension -> Pfad)
    $videoIndex = @{}
    foreach ($v in $videoFiles) {
        $name = [System.IO.Path]::GetFileNameWithoutExtension($v.Name)
        $videoIndex[$name] = $v.FullName
    }
    
    $linked = 0
    foreach ($h in $Haltungen) {
        $hName = $h.Felder['Haltungsname']
        if (-not $hName) { continue }
        if ($h.Felder.ContainsKey('Link') -and $h.Felder['Link']) { continue }
        
        # Suche nach verschiedenen Namensmustern
        # H__59965-10.62293.mpg, H__59965-10.62293~G.mpg, 59965-10.62293.mpg
        $patterns = @(
            "H__$hName",
            "H__" + ($hName -replace '-', '_'),
            $hName,
            ($hName -replace '-', '_')
        )
        
        foreach ($pattern in $patterns) {
            if ($videoIndex.ContainsKey($pattern)) {
                $h.Felder['Link'] = $videoIndex[$pattern]
                $linked++
                break
            }
            # Auch mit ~G Suffix prüfen (Grafik-Version)
            $patternG = "${pattern}~G"
            if ($videoIndex.ContainsKey($patternG)) {
                $h.Felder['Link'] = $videoIndex[$patternG]
                $linked++
                break
            }
        }
    }
    
    if ($linked -gt 0) {
        Log-Info -Message "Video-Verlinkung: $linked Haltungen mit Video verknüpft" -Context "PdfImport"
    }
    
    return $Haltungen
}

<#
.SYNOPSIS
    Parse-PdfFile: Haupt-Parsing-Funktion für PDFs (erkennt WinCanVX und IBAK automatisch)
#>
function Parse-PdfFile {
    param([string] $PdfPath)
    
    $result = @{
        Haltungen = @()
        IsWinCanVx = $false
        IsIBAK = $false
        FileName = (Split-Path $PdfPath -Leaf)
        Error = ""
    }
    
    try {
        if (-not (Test-Path $PdfPath)) {
            throw "PDF nicht gefunden: $PdfPath"
        }
        
        # Extrahiere Seiten
        $pages = ExtractPdfTextByPage -PdfPath $PdfPath
        
        if ($pages.Count -eq 0) {
            throw "Keine Seiten im PDF gefunden"
        }
        
        # Prüfe Format anhand des Inhalts
        $fullText = $pages -join "`n"
        
        # WinCanVX-Format erkennen
        if ($fullText -match 'Haltungsinspektion\s*-' -or ($fullText -match 'Aufmassliste' -and $fullText -match 'Fretz Kanal-Service')) {
            $result.IsWinCanVx = $true
            Log-Info -Message "WinCanVX-Format erkannt" -Context "PdfImport"
        }
        # IBAK/IKAS-Format erkennen (Leitungsgrafik, Oberer Punkt/Unterer Punkt)
        elseif ($fullText -match 'Leitungsgrafik|Leitungsbildbericht' -or 
                ($fullText -match 'Oberer\s+(Schacht|Punkt)' -and $fullText -match 'Unterer\s+(Schacht|Punkt)')) {
            $result.IsIBAK = $true
            Log-Info -Message "IBAK/IKAS-Format erkannt" -Context "PdfImport"
        }
        
        if ($result.IsWinCanVx) {
            # WinCanVX-spezifisches Parsing
            $haltungenDict = @{}
            
            foreach ($page in $pages) {
                $aufmassHaltungen = Parse-WinCanVxAufmassliste -PageText $page
                foreach ($h in $aufmassHaltungen) {
                    if (-not $haltungenDict.ContainsKey($h.Id)) {
                        $haltungenDict[$h.Id] = @{ Id = $h.Id; Felder = @{}; Source = 'pdf' }
                    }
                    foreach ($key in $h.Felder.Keys) {
                        if (-not $haltungenDict[$h.Id].Felder.ContainsKey($key) -or 
                            [string]::IsNullOrWhiteSpace($haltungenDict[$h.Id].Felder[$key])) {
                            $haltungenDict[$h.Id].Felder[$key] = $h.Felder[$key]
                        }
                    }
                }
                
                $wincanResult = Parse-WinCanVxPage -PageText $page
                if ($wincanResult.IsWinCanVx -and $wincanResult.Haltungsname) {
                    $hId = $wincanResult.Haltungsname
                    if (-not $haltungenDict.ContainsKey($hId)) {
                        $haltungenDict[$hId] = @{ Id = $hId; Felder = @{}; Source = 'pdf' }
                    }
                    foreach ($key in $wincanResult.Felder.Keys) {
                        $haltungenDict[$hId].Felder[$key] = $wincanResult.Felder[$key]
                    }
                }
            }
            
            $result.Haltungen = @($haltungenDict.Values)
            
        } elseif ($result.IsIBAK) {
            # IBAK/IKAS-spezifisches Parsing
            $haltungenDict = @{}
            
            foreach ($page in $pages) {
                $ibakResult = Parse-IbakPage -PageText $page
                if ($ibakResult.IsIbak -and $ibakResult.Haltungsname) {
                    $hId = $ibakResult.Haltungsname
                    if (-not $haltungenDict.ContainsKey($hId)) {
                        $haltungenDict[$hId] = @{ Id = $hId; Felder = @{}; Source = 'pdf' }
                    }
                    # Merge mit speziellem Handling für Zustandsklasse
                    foreach ($key in $ibakResult.Felder.Keys) {
                        $newVal = $ibakResult.Felder[$key]
                        $existingVal = $haltungenDict[$hId].Felder[$key]
                        
                        $shouldUpdate = $false
                        
                        if (-not $haltungenDict[$hId].Felder.ContainsKey($key)) {
                            $shouldUpdate = $true
                        } elseif ([string]::IsNullOrWhiteSpace($existingVal)) {
                            $shouldUpdate = $true
                        } elseif ($key -eq 'Zustandsklasse') {
                            # Behalte die höhere Zustandsklasse
                            $newZk = 0
                            $existZk = 0
                            [int]::TryParse($newVal, [ref]$newZk) | Out-Null
                            [int]::TryParse($existingVal, [ref]$existZk) | Out-Null
                            if ($newZk -gt $existZk) {
                                $shouldUpdate = $true
                            }
                        } elseif ($key -eq 'Sanieren_JaNein') {
                            # "Ja" hat Priorität über "Nein"
                            if ($newVal -eq 'Ja' -and $existingVal -eq 'Nein') {
                                $shouldUpdate = $true
                            }
                        } elseif ($key -eq 'Primaere_Schaeden') {
                            # Sammle alle Schäden
                            if ($newVal -and $existingVal -notmatch [regex]::Escape($newVal.Split("`n")[0])) {
                                $haltungenDict[$hId].Felder[$key] = $existingVal + "`n" + $newVal
                                $shouldUpdate = $false  # Bereits manuell gemerged
                            }
                        } elseif ($newVal.Length -gt $existingVal.Length) {
                            # Längerer Wert für andere Felder
                            $shouldUpdate = $true
                        }
                        
                        if ($shouldUpdate -and $newVal) {
                            $haltungenDict[$hId].Felder[$key] = $newVal
                        }
                    }
                }
                
                # Prüfe auch auf Statistik-Tabelle
                $statsHaltungen = Parse-IbakStatistik -PageText $page
                foreach ($h in $statsHaltungen) {
                    if (-not $haltungenDict.ContainsKey($h.Id)) {
                        $haltungenDict[$h.Id] = @{ Id = $h.Id; Felder = @{}; Source = 'pdf' }
                    }
                    foreach ($key in $h.Felder.Keys) {
                        if (-not $haltungenDict[$h.Id].Felder.ContainsKey($key) -or 
                            [string]::IsNullOrWhiteSpace($haltungenDict[$h.Id].Felder[$key])) {
                            $haltungenDict[$h.Id].Felder[$key] = $h.Felder[$key]
                        }
                    }
                }
            }
            
            $result.Haltungen = @($haltungenDict.Values)
            
        } else {
            # Fallback: Standard-Chunk-Parsing
            $chunks = SplitIntoHaltungChunks -PagesText $pages
            
            foreach ($chunk in $chunks) {
                $fields = ParseFieldsFromChunk -TextChunk $chunk.Text
                $haltungId = if ($fields.ContainsKey('Haltungsname')) { $fields['Haltungsname'] } else { $chunk.DetectedId }
                
                if ($haltungId) {
                    $result.Haltungen += @{
                        Id = $haltungId
                        Felder = $fields
                        Source = 'pdf'
                    }
                }
            }
        }
        
        Log-Info -Message "PDF geparst: $($result.Haltungen.Count) Haltungen gefunden" -Context "PdfImport"
        
        # Anreicherung mit XTF-Daten (Eigentuemer etc.)
        if ($result.Haltungen.Count -gt 0) {
            $result.Haltungen = @(Enrich-HaltungenFromXtf -PdfPath $PdfPath -Haltungen $result.Haltungen)
            $result.Haltungen = @(Enrich-HaltungenWithVideoLinks -PdfPath $PdfPath -Haltungen $result.Haltungen)
        }
        
    } catch {
        $result.Error = $_.Exception.Message
        Log-Error -Message "Fehler beim PDF-Parsing: $_" -Context "PdfImport" -Exception $_
    }
    
    return $result
}

Write-Host "[PdfImportService] Loaded - $($script:PdfFieldMapping.Count) Felder definiert, Batch-Import verfuegbar" -ForegroundColor Green
