[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

$script:Theme = @{
    AppBg = [System.Drawing.Color]::FromArgb(243, 243, 243)
    CardBg = [System.Drawing.Color]::FromArgb(255, 255, 255)
    HeaderBg = [System.Drawing.Color]::FromArgb(248, 248, 248)
    Border = [System.Drawing.Color]::FromArgb(224, 224, 224)
    Text = [System.Drawing.Color]::FromArgb(26, 26, 26)
    MutedText = [System.Drawing.Color]::FromArgb(90, 90, 90)
    SelectionBg = [System.Drawing.Color]::FromArgb(220, 235, 255)
    AltRowBg = [System.Drawing.Color]::FromArgb(250, 250, 250)
    ButtonBg = [System.Drawing.Color]::FromArgb(255, 255, 255)
    ButtonHoverBg = [System.Drawing.Color]::FromArgb(245, 245, 245)
    ButtonPressedBg = [System.Drawing.Color]::FromArgb(237, 237, 237)
    ButtonBorder = [System.Drawing.Color]::FromArgb(208, 208, 208)
    Accent = [System.Drawing.Color]::FromArgb(15, 108, 189)
    AccentHover = [System.Drawing.Color]::FromArgb(17, 119, 209)
    AccentPressed = [System.Drawing.Color]::FromArgb(13, 94, 170)
    AccentText = [System.Drawing.Color]::White
    Danger = [System.Drawing.Color]::FromArgb(196, 43, 28)
    DangerHover = [System.Drawing.Color]::FromArgb(209, 52, 44)
    DangerPressed = [System.Drawing.Color]::FromArgb(179, 36, 25)
}

function New-UiFont {
    param(
        [float]$Size,
        [System.Drawing.FontStyle]$Style = [System.Drawing.FontStyle]::Regular
    )

    try {
        return New-Object System.Drawing.Font("Segoe UI Variable", $Size, $Style)
    } catch {
        return New-Object System.Drawing.Font("Segoe UI", $Size, $Style)
    }
}

$script:TemplatePath = Join-Path $PSScriptRoot "Haltungen.xlsx"
$script:Data = @()
$script:UiFont = New-UiFont -Size 9
$script:UiFontBold = New-UiFont -Size 9 -Style ([System.Drawing.FontStyle]::Bold)
$script:TitleFont = New-UiFont -Size 12 -Style ([System.Drawing.FontStyle]::Bold)
$script:ButtonFont = New-UiFont -Size 10 -Style ([System.Drawing.FontStyle]::Bold)
$script:AppVersion = "2026-01-21.10"
$script:AppRoot = $PSScriptRoot
$script:FieldOrder = @(
    "NR","Ausfuehrung_durch","Haltungsname","Strasse","Rohrmaterial","DN_mm","Nutzungsart",
    "Haltungslaenge_m","Fliessrichtung","Primaere_Schaeden","Zustandsklasse","Pruefungsresultat",
    "Sanieren_Ja_Nein","Empfohlene_Sanierungsmassnahmen","Kosten","Eigentuemer","Bemerkungen","Link",
    "Renovierung_Inliner_Stk","Renovierung_Inliner_m","Anschluesse_verpressen","Reparatur_Manschette",
    "Reparatur_Kurzliner","Erneuerung_Neubau_m","Offen_abgeschlossen","Datum_Jahr"
)
$script:GridFieldOrder = @(
    "NR","Haltungsname","Strasse","Rohrmaterial","DN_mm","Haltungslaenge_m","Fliessrichtung",
    "Primaere_Schaeden","Zustandsklasse","Sanieren_Ja_Nein","Empfohlene_Sanierungsmassnahmen",
    "Eigentuemer","Kosten","Bemerkungen"
)
$script:DataRows = @()
$script:FieldHeaders = @{
    NR = "NR"
    Ausfuehrung_durch = "Ausfuehrung durch"
    Haltungsname = "Haltungsname"
    Strasse = "Strasse"
    Rohrmaterial = "Rohrmaterial"
    DN_mm = "Durchmesser"
    Nutzungsart = "Nutzungsart"
    Haltungslaenge_m = ("Haltungsl" + [char]0x00E4 + "nge")
    Fliessrichtung = "Fiessrichtung"
    Primaere_Schaeden = ("Prim" + [char]0x00E4 + "re Sch" + [char]0x00E4 + "den")
    Zustandsklasse = "Zustandsklasse"
    Pruefungsresultat = "Pruefungsresultat"
    Sanieren_Ja_Nein = "Sanieren Ja/Nein"
    Empfohlene_Sanierungsmassnahmen = "Empfohlene Sanierungsmassnahmen"
    Kosten = "Kosten"
    Eigentuemer = ("Eigent" + [char]0x00FC + "mmer")
    Bemerkungen = "Bemerkungen"
    Link = "Link"
    Renovierung_Inliner_Stk = "Renovierung Inliner Stk."
    Renovierung_Inliner_m = "Renovierung Inliner m"
    Anschluesse_verpressen = "Anschluesse verpressen"
    Reparatur_Manschette = "Reparatur Manschette"
    Reparatur_Kurzliner = "Reparatur Kurzliner"
    Erneuerung_Neubau_m = "Erneuerung Neubau m"
    Offen_abgeschlossen = "offen/abgeschlossen"
    Datum_Jahr = "Datum/Jahr"
}
$script:FieldBoxes = @{}
$script:CurrentRow = $null
$script:IsLoadingForm = $false
$script:Binding = $null
$script:Grid = $null
$script:LogoBox = $null
$script:StartLogoBox = $null
$script:DetailsPanel = $null
$script:ToggleFormButton = $null
$script:ProjectPath = ""
$script:ProjectLabel = $null
$script:MetaNameBox = $null
$script:MetaZoneBox = $null
$script:StartProjectNameBox = $null
$script:StartZoneBox = $null
$script:StartFirmaNameBox = $null
$script:StartFirmaAdresseBox = $null
$script:StartFirmaTelefonBox = $null
$script:StartFirmaEmailBox = $null
$script:StartPanel = $null
$script:MainPanel = $null
$script:ProjectListBox = $null

function New-ReportMeta {
    return @{
        Auswertungsname = ""
        Zone = ""
        LogoPath = ""
        FirmaName = ""
        FirmaAdresse = ""
        FirmaTelefon = ""
        FirmaEmail = ""
    }
}

$script:ReportMeta = New-ReportMeta

function New-EmptyRow {
    return [PSCustomObject]@{
        NR = ""
        Ausfuehrung_durch = ""
        Haltungsname = ""
        Strasse = ""
        Rohrmaterial = ""
        DN_mm = ""
        Nutzungsart = ""
        Haltungslaenge_m = ""
        Fliessrichtung = ""
        Primaere_Schaeden = ""
        Zustandsklasse = ""
        Pruefungsresultat = ""
        Sanieren_Ja_Nein = ""
        Empfohlene_Sanierungsmassnahmen = ""
        Kosten = ""
        Eigentuemer = ""
        Bemerkungen = ""
        Link = ""
        Renovierung_Inliner_Stk = ""
        Renovierung_Inliner_m = ""
        Anschluesse_verpressen = ""
        Reparatur_Manschette = ""
        Reparatur_Kurzliner = ""
        Erneuerung_Neubau_m = ""
        Offen_abgeschlossen = ""
        Datum_Jahr = ""
    }
}

function Get-NextRowNumber {
    $max = 0
    foreach ($row in @($script:Data)) {
        if ($null -eq $row) { continue }
        $value = [string]$row.NR
        if ([string]::IsNullOrWhiteSpace($value)) { continue }
        $num = 0
        if ([int]::TryParse($value, [ref]$num)) {
            if ($num -gt $max) { $max = $num }
        }
    }
    return ($max + 1)
}

function New-EmptyDataRow {
    return [PSCustomObject]@{
        NR = ""
        Ausfuehrung_durch = ""
        Haltungsname = ""
        Strasse = ""
        Rohrmaterial = ""
        DN_mm = ""
        Nutzungsart = ""
        Haltungslaenge_m = ""
        Fliessrichtung = ""
        Primaere_Schaeden = ""
        Zustandsklasse = ""
        Pruefungsresultat = ""
        Sanieren_Ja_Nein = ""
        Empfohlene_Sanierungsmassnahmen = ""
        Kosten = ""
        Eigentuemer = ""
        Bemerkungen = ""
        Link = ""
    }
}

function Get-PdfToTextPath {
    $cmd = Get-Command -Name pdftotext -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }

    $root = Join-Path $env:LOCALAPPDATA "Microsoft\WinGet\Packages"
    $match = Get-ChildItem -Path $root -Recurse -Filter pdftotext.exe -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($match) { return $match.FullName }

    throw "pdftotext.exe not found. Install Poppler and try again."
}

function Convert-PdfToText {
    param(
        [Parameter(Mandatory = $true)][string]$InputPdf,
        [Parameter(Mandatory = $true)][string]$OutputTxt
    )
    $pdftotext = Get-PdfToTextPath
    & $pdftotext -enc UTF-8 -layout $InputPdf $OutputTxt
}

function Split-Fields {
    param([string]$Line)
    return @($Line -split "\s{2,}" | Where-Object { $_ -ne "" })
}

function Convert-Text {
    param([string]$Value)
    if ($null -eq $Value) { return "" }
    $v = $Value
    $v = $v -replace ([char]0x201E), "ae"
    $v = $v -replace ([char]0x201D), "oe"
    $v = $v -replace ([char]0x0081), "ue"
    $v = $v -replace ([char]0x00E1), "ss"
    $v = $v -replace ([char]0x00E4), "ae"
    $v = $v -replace ([char]0x00F6), "oe"
    $v = $v -replace ([char]0x00FC), "ue"
    $v = $v -replace ([char]0x00DF), "ss"
    return $v
}

function Convert-NumberString {
    param([string]$Value)
    if ($null -eq $Value) { return "" }
    return ($Value -replace ",", ".")
}

function Select-LongerLength {
    param([string]$Existing, [string]$Candidate)
    $candidateNorm = Convert-NumberString -Value $Candidate
    if (Test-BlankValue $candidateNorm) { return $Existing }
    if (Test-BlankValue $Existing) { return $candidateNorm }

    $existingVal = 0.0
    $candidateVal = 0.0
    $culture = [System.Globalization.CultureInfo]::InvariantCulture

    if (-not [double]::TryParse((Convert-NumberString -Value $Existing), [System.Globalization.NumberStyles]::Float, $culture, [ref]$existingVal)) {
        return $candidateNorm
    }
    if (-not [double]::TryParse($candidateNorm, [System.Globalization.NumberStyles]::Float, $culture, [ref]$candidateVal)) {
        return $Existing
    }
    if ($candidateVal -gt $existingVal) { return $candidateNorm }
    return $Existing
}

function Update-RowLength {
    param([pscustomobject]$Row, [string]$Value)
    $Row.Haltungslaenge_m = Select-LongerLength -Existing $Row.Haltungslaenge_m -Candidate $Value
}

function ConvertFrom-HaltungsberichtLines {
    param([string[]]$Lines)

    $rowsByName = @{}
    $codesByName = @{}
    $lastCodeIndex = @{}
    $currentName = $null
    $inHaltungsbericht = $false
    $pendingLength = ""
    $awaitLength = $false

    foreach ($line in $Lines) {
        $norm = Convert-Text -Value $line
        $trim = $norm.Trim()

        # Wenn in vorheriger Zeile eine Laengen-Ueberschrift stand (z.B. "Haltungslaenge"),
        # dann steht der Wert oft erst in der naechsten Zeile: "42.33 m"
        if ($awaitLength) {
            $mNext = [regex]::Match($norm, "^\s*(?<val>\d+([.,]\d+)?)\s*m\s*$")
            if ($mNext.Success) {
                if ($null -ne $row) {
                    Update-RowLength -Row $row -Value $mNext.Groups["val"].Value
                } else {
                    $pendingLength = Select-LongerLength -Existing $pendingLength -Candidate $mNext.Groups["val"].Value
                }
                $awaitLength = $false
                continue
            }
        }


        if ($trim -like "Haltungsbericht*") { $inHaltungsbericht = $true }
        if ($trim -like "Haltungsgrafik*") { $inHaltungsbericht = $false }
        if ($trim -like "Haltungsbildbericht*") { $inHaltungsbericht = $false }

        $m = [regex]::Match($line, "^\s*Haltung\s+(?<name>\S+)")
        if ($m.Success) {
            $currentName = $m.Groups["name"].Value.Trim()
            if (-not $rowsByName.ContainsKey($currentName)) {
                $row = New-EmptyRow
                $row.Haltungsname = $currentName
                $rowsByName[$currentName] = $row
                $codesByName[$currentName] = @()
            }
            if (-not (Test-BlankValue $pendingLength)) {
                Update-RowLength -Row $rowsByName[$currentName] -Value $pendingLength
                $pendingLength = ""
                $awaitLength = $false
            }
            continue
        }

        $row = $null
        if (-not [string]::IsNullOrWhiteSpace($currentName)) {
            $row = $rowsByName[$currentName]
        }

        $m = [regex]::Match($norm, "Haltungsl\\S*?nge\\s+(?<val>\\d+([.,]\\d+)?)\\s*m")
        if ($m.Success) {
            if ($null -ne $row) {
                Update-RowLength -Row $row -Value $m.Groups["val"].Value
            } else {
                $pendingLength = Select-LongerLength -Existing $pendingLength -Candidate $m.Groups["val"].Value
            }
        }

        $m = [regex]::Match($norm, "Inspektionsl\\S*?nge\\s+(?<val>\\d+([.,]\\d+)?)\\s*m")
        if ($m.Success) {
            if ($null -ne $row) {
                Update-RowLength -Row $row -Value $m.Groups["val"].Value
            } else {
                $pendingLength = Select-LongerLength -Existing $pendingLength -Candidate $m.Groups["val"].Value
            }
        }

        $m = [regex]::Match($norm, "Gesamtinsp\\.l\\S*?nge\\s+(?<val>\\d+([.,]\\d+)?)\\s*m")
        if ($m.Success) {
            if ($null -ne $row) {
                Update-RowLength -Row $row -Value $m.Groups["val"].Value
            } else {
                $pendingLength = Select-LongerLength -Existing $pendingLength -Candidate $m.Groups["val"].Value
            }
        }

        $m = [regex]::Match($norm, "HL\s*\[m\]\s+(?<val>\d+([.,]\d+)?)")
        if ($m.Success) {
            if ($null -ne $row) {
                Update-RowLength -Row $row -Value $m.Groups["val"].Value
            } else {
                $pendingLength = Select-LongerLength -Existing $pendingLength -Candidate $m.Groups["val"].Value
            }
        } else {
            # Manchmal steht der Wert vor dem Label: "7.08 HL [m]"
            $m2 = [regex]::Match($norm, "(?<val>\d+([.,]\d+)?)\s*HL\s*\[m\]")
            if ($m2.Success) {
                if ($null -ne $row) {
                    Update-RowLength -Row $row -Value $m2.Groups["val"].Value
                } else {
                    $pendingLength = Select-LongerLength -Existing $pendingLength -Candidate $m2.Groups["val"].Value
                }
            } elseif ($trim -match "^HL\s*\[m\]") {
                $awaitLength = $true
            }
        }

        if ($null -eq $row) { continue }

        $m = [regex]::Match($norm, "Material\s+(?<val>.+?)(\s{2,}|$)")
        if ($m.Success -and (Test-BlankValue $row.Rohrmaterial)) {
            $row.Rohrmaterial = $m.Groups["val"].Value.Trim()
        }

        $m = [regex]::Match($norm, "Nutzungsart\s+(?<val>.+?)(\s{2,}|$)")
        if ($m.Success -and (Test-BlankValue $row.Nutzungsart)) {
            $row.Nutzungsart = $m.Groups["val"].Value.Trim()
        }

        $m = [regex]::Match($norm, "Str.*?/\s*Standort\s+(?<val>.+?)(\s{2,}|$)")
        if ($m.Success -and (Test-BlankValue $row.Strasse)) {
            $row.Strasse = $m.Groups["val"].Value.Trim()
        }

        if (Test-BlankValue $row.DN_mm) {
            $m = [regex]::Match($norm, "Profilh\w*\s+(?<val>\d+)\s*mm")
            if ($m.Success) { $row.DN_mm = $m.Groups["val"].Value }
        }
        if (Test-BlankValue $row.DN_mm) {
            $m = [regex]::Match($norm, "Profilbreite\s+(?<val>\d+)\s*mm")
            if ($m.Success) { $row.DN_mm = $m.Groups["val"].Value }
        }
        if (Test-BlankValue $row.DN_mm) {
            $m = [regex]::Match($norm, "Dimension\s*\[mm\]\s+(?<val>\d+)")
            if ($m.Success) { $row.DN_mm = $m.Groups["val"].Value }
        }

        if (Test-BlankValue $row.Fliessrichtung) {
            $m = [regex]::Match($norm, "Inspektionsrichtung\s+(?<dir>.+?)(\s{2,}|$)")
            if ($m.Success) {
                $dir = $m.Groups["dir"].Value.Trim()
                if ($dir -like "In Fliessrichtung*") { $row.Fliessrichtung = "in" }
                elseif ($dir -like "Gegen Fliessrichtung*") { $row.Fliessrichtung = "gegen" }
            } elseif ($norm -match "(?i)\bin\s+flie") {
                $row.Fliessrichtung = "in"
            } elseif ($norm -match "(?i)\bgegen\s+flie") {
                $row.Fliessrichtung = "gegen"
            }
        }

        $m = [regex]::Match($norm, "Operateur\s+(?<val>.+?)(\s{2,}|$)")
        if ($m.Success) {
            $row.Ausfuehrung_durch = $m.Groups["val"].Value.Trim()
        } elseif (Test-BlankValue $row.Ausfuehrung_durch) {
            $m = [regex]::Match($norm, "Ausfuehrender\s+(?<val>.+?)(\s{2,}|$)")
            if ($m.Success) { $row.Ausfuehrung_durch = $m.Groups["val"].Value.Trim() }
        }

        if ($inHaltungsbericht) {
            $codeMatch = [regex]::Match(
                $norm,
                "^\s*(?:(?<photo>\d{2,3})\s+)?\d{2}:\d{2}:\d{2}\s+(?<dist>\d+([.,]\d+)?)\s+(?<code>[A-Z]{2,3}(?:\.[A-Z])?(?:\.[A-Z])?)\s+(?:[A-Z]\s+)?(?<desc>.+)$"
            )
            if ($codeMatch.Success) {
                $desc = ($codeMatch.Groups["desc"].Value.Trim() -replace "\s+", " ")
                $entry = ("{0} {1}" -f $codeMatch.Groups["code"].Value, $desc)
                if (-not $codesByName[$currentName].Contains($entry)) {
                    $codesByName[$currentName] += $entry
                }
                $lastCodeIndex[$currentName] = $codesByName[$currentName].Count - 1
                continue
            }
        }

        if ($inHaltungsbericht -and $lastCodeIndex.ContainsKey($currentName)) {
            $isNoise = $trim -match "^(Haltungsbericht|Haltungsgrafik|Haltungsbildbericht|Foto|Video|Entf\\./m|Zustand|Beschreibung|Inspektion|Gegenuntersuchung|Gedruckt am|Seite|Oberer Schacht|Unterer Schacht|Fahrzeug|Bezugspunkt|Kamerasystem|Untersuchungsart|Untersuchungsstatus|Wetter|Dauer|Ergebnis|Bemerkung|Gesamtinsp\\.|Inspektionsrichtung|Inspektionsl)"
            if ($trim -and $trim -notmatch "\d{2}:\d{2}:\d{2}" -and -not $isNoise) {
                $idx = $lastCodeIndex[$currentName]
                $append = ($trim -replace "\s+", " ")
                $codesByName[$currentName][$idx] = ($codesByName[$currentName][$idx] + " " + $append)
            }
        }
    }

    $rows = @()
    foreach ($name in $rowsByName.Keys) {
        if ($codesByName[$name].Count -gt 0) {
            $rowsByName[$name].Primaere_Schaeden = ($codesByName[$name] -join "; ")
        }
        $rows += $rowsByName[$name]
    }

    return $rows
}

function ConvertFrom-InhaltsverzeichnisLines {
    param([string[]]$Lines)

    $rows = @()
    foreach ($line in $Lines) {
        $norm = Normalize-Text -Value $line
        $fields = Split-Fields -Line $norm
        if ($null -eq $fields) { continue }
        if (@($fields).Count -lt 4) { continue }
        if ($fields[0] -notmatch "^\d{3,}-\d{3,}$") { continue }

        $row = New-EmptyRow
        $row.Haltungsname = $fields[0]
        if (@($fields).Count -ge 4) { $row.Strasse = $fields[3] }
        $rows += $row
    }

    return $rows
}

function ConvertFrom-TabellenLines {
    param([string[]]$Lines)

    function Get-HeaderForIndex {
        param([string]$Value)
        if ($null -eq $Value) { return "" }
        $v = $Value
        $v = $v -replace ([char]0x201E), "a"
        $v = $v -replace ([char]0x201D), "o"
        $v = $v -replace ([char]0x0081), "u"
        $v = $v -replace ([char]0x00E1), "s"
        $v = $v -replace ([char]0x00E4), "a"
        $v = $v -replace ([char]0x00F6), "o"
        $v = $v -replace ([char]0x00FC), "u"
        $v = $v -replace ([char]0x00DF), "s"
        return $v
    }

    $rows = @()
    $headerLine = $null
    $headerIndex = -1
    for ($i = 0; $i -lt $Lines.Count; $i++) {
        $lineNorm = Get-HeaderForIndex -Value $Lines[$i]
        if ($lineNorm -match "haltungsname" -and $lineNorm -match "strasse" -and $lineNorm -match "rohrmaterial") {
            $headerLine = $lineNorm
            $headerIndex = $i
            break
        }
    }
    if ($headerIndex -lt 0) { return $rows }

    $headerDefs = @(
        @{ Labels = @("NR"); Prop = "NR" }
        @{ Labels = @("Haltungsname"); Prop = "Haltungsname" }
        @{ Labels = @("Strasse", "Strase"); Prop = "Strasse" }
        @{ Labels = @("Rohrmaterial"); Prop = "Rohrmaterial" }
        @{ Labels = @("Durchmesser", "DN mm", "DN"); Prop = "DN_mm" }
        @{ Labels = @("Haltungslaenge", "Haltungslange"); Prop = "Haltungslaenge_m" }
        @{ Labels = @("Fliessrichtung", "Fliesrichtung", "Flussrichtung", "Fiessrichtung"); Prop = "Fliessrichtung" }
        @{ Labels = @("Primaere Schaeden", "Primare Schaden", "Primaere", "Primare"); Prop = "Primaere_Schaeden" }
        @{ Labels = @("Zustandsklasse"); Prop = "Zustandsklasse" }
        @{ Labels = @("Sanieren Ja/Nein", "Sanieren"); Prop = "Sanieren_Ja_Nein" }
        @{ Labels = @("Empfohlene Sanierungsmassnahmen", "Empfohlene Sanierungsmasnahmen", "Empfohlene"); Prop = "Empfohlene_Sanierungsmassnahmen" }
        @{ Labels = @("Eigentuemer", "Eigentumer"); Prop = "Eigentuemer" }
        @{ Labels = @("Kosten"); Prop = "Kosten" }
        @{ Labels = @("Bemerkungen"); Prop = "Bemerkungen" }
        @{ Labels = @("Link"); Prop = "Link" }
    )

    $positions = @()
    foreach ($def in $headerDefs) {
        $idx = -1
        foreach ($label in $def.Labels) {
            $idx = $headerLine.IndexOf($label, [System.StringComparison]::OrdinalIgnoreCase)
            if ($idx -ge 0) { break }
        }
        if ($idx -ge 0) {
            $positions += [PSCustomObject]@{ Index = $idx; Prop = $def.Prop }
        }
    }
    $positions = $positions | Sort-Object Index
    if ($positions.Count -lt 3) { return $rows }

    for ($j = $headerIndex + 1; $j -lt $Lines.Count; $j++) {
        $lineNorm = Get-HeaderForIndex -Value $Lines[$j]
        if ([string]::IsNullOrWhiteSpace($lineNorm)) {
            if ($rows.Count -gt 0) { break }
            continue
        }
        if ($lineNorm -match "haltungsname" -and $lineNorm -match "strasse" -and $lineNorm -match "rohrmaterial") {
            continue
        }
        if ($lineNorm -match "^import\s+pdf") { continue }

        $row = New-EmptyRow
        for ($k = 0; $k -lt $positions.Count; $k++) {
            $startPos = $positions[$k].Index
            $endPos = $lineNorm.Length
            if ($k + 1 -lt $positions.Count) { $endPos = $positions[$k + 1].Index }
            if ($startPos -ge $lineNorm.Length) { continue }
            $len = [Math]::Max(0, $endPos - $startPos)
            $val = $lineNorm.Substring($startPos, [Math]::Min($len, $lineNorm.Length - $startPos)).Trim()
            if ($val) { $row.($positions[$k].Prop) = $val }
        }

        if (Test-BlankValue $row.Haltungsname -and Test-BlankValue $row.NR) { continue }
        if (-not (Test-BlankValue $row.Haltungslaenge_m)) {
            $row.Haltungslaenge_m = Convert-NumberString -Value $row.Haltungslaenge_m
        }
        $rows += $row
    }

    return $rows
}

function ConvertFrom-PdfHaltungen {
    param([string]$PdfPath)

    $txtPath = Join-Path $env:TEMP ("pdf_extract_{0}.txt" -f ([Guid]::NewGuid().ToString("N")))
    Convert-PdfToText -InputPdf $PdfPath -OutputTxt $txtPath
    $lines = Get-Content -Path $txtPath -Encoding UTF8
    Remove-Item -Path $txtPath -Force

    $rows = @()
    for ($i = 0; $i -lt $lines.Count; $i++) {
        $line = $lines[$i]
        $m = [regex]::Match($line, "^\s*Haltungsinspektion\s+-\s+(?<date>\d{2}\.\d{2}\.\d{4})\s+-\s+(?<haltung>.+)$")
        if (-not $m.Success) { continue }

        $block = @()
        $i++
        while ($i -lt $lines.Count -and $lines[$i] -notmatch "^\s*Haltungsinspektion\s+-") {
            $block += $lines[$i]
            $i++
        }
        $i--

        $row = New-EmptyRow
        $row.Haltungsname = $m.Groups["haltung"].Value.Trim()
        $codes = @()
        $expectHaltungLine = $false
        $expectOperatorLine = $false
        $awaitLength = $false

        foreach ($b in $block) {
            $norm = Convert-Text -Value $b
            $trim = $norm.Trim()

            if ($awaitLength) {
                $mNext = [regex]::Match($trim, "^(?<val>\d+([.,]\d+)?)\s*m\s*$")
                if ($mNext.Success) {
                    Update-RowLength -Row $row -Value $mNext.Groups["val"].Value
                    $awaitLength = $false
                    continue
                }
            }

            if ($norm -match "^\s*Datum\s+Kamera\s+Witterung\s+Haltung\s+Nr\.\s*$") {
                $expectHaltungLine = $true
                continue
            }
            if ($expectHaltungLine) {
                $fields = Split-Fields -Line $norm
                if ($fields.Count -ge 5) {
                    $row.Haltungsname = $fields[$fields.Count - 2]
                    $row.NR = $fields[$fields.Count - 1]
                }
                $expectHaltungLine = $false
            }

            if ($norm -match "^\s*Projektname\s+Speicherstelle\s+Fahrzeug\s+Operator\s+Auftrags-Nr\.\s*$") {
                $expectOperatorLine = $true
                continue
            }
            if ($expectOperatorLine) {
                $fields = Split-Fields -Line $norm
                if ($fields.Count -ge 5) {
                    $row.Ausfuehrung_durch = $fields[3]
                }
                $expectOperatorLine = $false
            }

            $m = [regex]::Match($norm, "^\s*Strasse\s+(?<val>.+?)\s{2,}Schacht")
            if ($m.Success) { $row.Strasse = $m.Groups["val"].Value.Trim() }

            $m = [regex]::Match($norm, "^\s*Profil\s+(?<val>.+?)\s{2,}Grund")
            if ($m.Success) {
                $profileData = $m.Groups["val"].Value.Trim()
                if ($profileData -match "(\d+)\s*mm") { $row.DN_mm = $Matches[1] }
            }

            $m = [regex]::Match($norm, "^\s*Material\s+(?<val>.+?)(\s{2,}|$)")
            if ($m.Success) { $row.Rohrmaterial = $m.Groups["val"].Value.Trim() }

            $m = [regex]::Match($norm, "^\s*Nutzungsart\s+(?<val>.+?)\s{2,}Inspektionsrichtung\s+(?<dir>.+?)\s*$")
            if ($m.Success) {
                $row.Nutzungsart = $m.Groups["val"].Value.Trim()
                $dir = $m.Groups["dir"].Value.Trim()
                if ($dir -like "In Fliessrichtung*") { $row.Fliessrichtung = "in" }
                elseif ($dir -like "Gegen Fliessrichtung*") { $row.Fliessrichtung = "gegen" }
            }

            $m = [regex]::Match($norm, "Ausfuehrender\s+(?<val>.+?)(\s{2,}|$)")
            if ($m.Success) { $row.Ausfuehrung_durch = $m.Groups["val"].Value.Trim() }

            $m = [regex]::Match($norm, "Operateur\s+(?<val>.+?)(\s{2,}|$)")
            if ($m.Success) { $row.Ausfuehrung_durch = $m.Groups["val"].Value.Trim() }

            $m = [regex]::Match($norm, "HL\s+\[m\]\s+(?<val>\d+([.,]\d+)?)")
            if ($m.Success) {
                Update-RowLength -Row $row -Value $m.Groups["val"].Value
            } else {
                $m2 = [regex]::Match($norm, "(?<val>\d+([.,]\d+)?)\s*HL\s*\[m\]")
                if ($m2.Success) {
                    Update-RowLength -Row $row -Value $m2.Groups["val"].Value
                } elseif ($trim -match "^HL\s*\[m\]") {
                    $awaitLength = $true
                }
            }

            $m = [regex]::Match($norm, "Haltungsl\\S*?nge\\s+(?<val>\\d+([.,]\\d+)?)\\s*m")
            if ($m.Success) { Update-RowLength -Row $row -Value $m.Groups["val"].Value }
            $m = [regex]::Match($norm, "Inspektionsl\\S*?nge\\s+(?<val>\\d+([.,]\\d+)?)\\s*m")
            if ($m.Success) { Update-RowLength -Row $row -Value $m.Groups["val"].Value }
            $m = [regex]::Match($norm, "Gesamtinsp\\.l\\S*?nge\\s+(?<val>\\d+([.,]\\d+)?)\\s*m")
            if ($m.Success) { Update-RowLength -Row $row -Value $m.Groups["val"].Value }
            if ($trim -match "Haltungsl\\S*?nge\\s*\\[m\\]" -or $trim -match "Inspektionsl\\S*?nge\\s*\\[m\\]" -or $trim -match "Gesamtinsp\\.l\\S*?nge\\s*\\[m\\]") {
                $awaitLength = $true
            }

            $m = [regex]::Match($norm, "^\s*\d+(\.\d+)?\s+(?<code>[A-Z]\d{2})\s+(?<desc>.+?)\s{2,}\d{2}:\d{2}:\d{2}")
            if ($m.Success) {
                $codes += ("{0} {1}" -f $m.Groups["code"].Value, $m.Groups["desc"].Value.Trim())
            }
        }

        if ($codes.Count -gt 0) { $row.Primaere_Schaeden = ($codes -join "; ") }
        $rows += $row
    }

    $rows += ConvertFrom-HaltungsberichtLines -Lines $lines
    $rows += ConvertFrom-InhaltsverzeichnisLines -Lines $lines
    $rows += ConvertFrom-TabellenLines -Lines $lines

    $rows = Merge-Data -Existing @() -Incoming $rows
    return $rows
}

function Get-ChildText {
    param([System.Xml.XmlNode]$Node, [string]$LocalName)
    $child = $Node.SelectSingleNode("./*[local-name()='$LocalName']")
    if ($null -eq $child) { return "" }
    return $child.InnerText
}

function Get-RefAttr {
    param([System.Xml.XmlNode]$Node, [string]$LocalName)
    $child = $Node.SelectSingleNode("./*[local-name()='$LocalName']")
    if ($null -eq $child) { return "" }
    return $child.GetAttribute("REF")
}

function Import-SIA405 {
    param([string]$FilePath)

    [xml]$xml = Get-Content -Path $FilePath -Encoding UTF8

    $kanalNodes = $xml.SelectNodes("//*[contains(local-name(),'.Kanal')]")
    $knotenNodes = $xml.SelectNodes("//*[contains(local-name(),'.Abwasserknoten')]")
    $haltungspunktNodes = $xml.SelectNodes("//*[contains(local-name(),'.Haltungspunkt')]")
    $haltungNodes = $xml.SelectNodes("//*[contains(local-name(),'.Haltung')]")

    $kanalByTid = @{}
    foreach ($n in $kanalNodes) {
        $tid = $n.GetAttribute("TID")
        if ([string]::IsNullOrWhiteSpace($tid)) { continue }
        $kanalByTid[$tid] = @{
            Bezeichnung = (Get-ChildText -Node $n -LocalName "Bezeichnung")
            Nutzungsart = (Get-ChildText -Node $n -LocalName "Nutzungsart_Ist")
        }
    }

    $knotenByTid = @{}
    foreach ($n in $knotenNodes) {
        $tid = $n.GetAttribute("TID")
        if ([string]::IsNullOrWhiteSpace($tid)) { continue }
        $knotenByTid[$tid] = (Get-ChildText -Node $n -LocalName "Bezeichnung")
    }

    $haltungspunktByTid = @{}
    foreach ($n in $haltungspunktNodes) {
        $tid = $n.GetAttribute("TID")
        if ([string]::IsNullOrWhiteSpace($tid)) { continue }
        $haltungspunktByTid[$tid] = (Get-RefAttr -Node $n -LocalName "AbwassernetzelementRef")
    }

    $rows = @()
    $nr = 1
    foreach ($n in $haltungNodes) {
        if ($n.LocalName -notlike "*.Haltung") { continue }
        $bezeichnung = (Get-ChildText -Node $n -LocalName "Bezeichnung")
        if ([string]::IsNullOrWhiteSpace($bezeichnung)) { continue }

        $laenge = (Get-ChildText -Node $n -LocalName "LaengeEffektiv")
        $dn = (Get-ChildText -Node $n -LocalName "Lichte_Hoehe")
        $material = (Get-ChildText -Node $n -LocalName "Material")
        $kanalRef = (Get-RefAttr -Node $n -LocalName "AbwasserbauwerkRef")
        $nutzungsart = ""
        if ($kanalByTid.ContainsKey($kanalRef)) {
            $nutzungsart = $kanalByTid[$kanalRef].Nutzungsart
        }

        $row = New-EmptyRow
        $row.NR = $nr
        $row.Haltungsname = $bezeichnung
        $row.Rohrmaterial = $material
        $row.DN_mm = $dn
        $row.Nutzungsart = $nutzungsart
        $row.Haltungslaenge_m = $laenge
        $rows += $row
        $nr++
    }

    return $rows
}

function Get-XtfModelName {
    param([string]$FilePath)

    $head = Get-Content -Path $FilePath -TotalCount 80 -ErrorAction SilentlyContinue
    foreach ($line in $head) {
        if ($line -match 'MODEL\s+NAME="([^"]+)"') {
            return $Matches[1]
        }
    }

    try {
        [xml]$xml = Get-Content -Path $FilePath -Encoding UTF8
        $modelNode = $xml.SelectSingleNode("//*[local-name()='MODEL']")
        if ($null -ne $modelNode) { return $modelNode.GetAttribute("NAME") }
    } catch {
        return ""
    }

    return ""
}

function Import-VsaKek {
    param([string]$FilePath)

    [xml]$xml = Get-Content -Path $FilePath -Encoding UTF8

    $untersuchungen = $xml.SelectNodes("//*[contains(local-name(),'.KEK.Untersuchung')]")
    $schadenNodes = $xml.SelectNodes("//*[contains(local-name(),'.KEK.Kanalschaden')]")
    $dateiNodes = $xml.SelectNodes("//*[contains(local-name(),'.KEK.Datei')]")
    $datentraegerNodes = $xml.SelectNodes("//*[contains(local-name(),'.KEK.Datentraeger')]")

    $datentraegerByTid = @{}
    foreach ($node in $datentraegerNodes) {
        $tid = $node.GetAttribute("TID")
        if ([string]::IsNullOrWhiteSpace($tid)) { continue }
        $datentraegerByTid[$tid] = (Get-ChildText -Node $node -LocalName "Pfad")
    }

    $videoByUntersuchung = @{}
    foreach ($node in $dateiNodes) {
        $art = Get-ChildText -Node $node -LocalName "Art"
        if ($art -notlike "digitalesVideo*") { continue }

        $klasse = Get-ChildText -Node $node -LocalName "Klasse"
        if (-not (Test-BlankValue $klasse) -and $klasse -notlike "Untersuchung*") { continue }

        $objekt = Get-ChildText -Node $node -LocalName "Objekt"
        if (Test-BlankValue $objekt) { continue }

        $relativ = Get-ChildText -Node $node -LocalName "Relativpfad"
        $bezeichnung = Get-ChildText -Node $node -LocalName "Bezeichnung"
        if (Test-BlankValue $bezeichnung) { continue }

        $datRef = Get-RefAttr -Node $node -LocalName "DatentraegerRef"
        $basis = ""
        if (-not (Test-BlankValue $datRef) -and $datentraegerByTid.ContainsKey($datRef)) {
            $basis = $datentraegerByTid[$datRef]
        }

        $path = $bezeichnung
        if (-not (Test-BlankValue $relativ)) {
            $path = Join-Path $relativ $bezeichnung
        }
        if (-not (Test-BlankValue $basis)) {
            $path = Join-Path $basis $path
        }

        if (-not $videoByUntersuchung.ContainsKey($objekt)) {
            $videoByUntersuchung[$objekt] = $path
        }
    }

    $schadenByUntersuchung = @{}
    foreach ($node in $schadenNodes) {
        $untersuchungRef = Get-RefAttr -Node $node -LocalName "UntersuchungRef"
        if (Test-BlankValue $untersuchungRef) { continue }

        $code = Get-ChildText -Node $node -LocalName "KanalSchadencode"
        if (Test-BlankValue $code) { continue }

        $distanz = Convert-NumberString -Value (Get-ChildText -Node $node -LocalName "Distanz")
        $entry = $code
        if (-not (Test-BlankValue $distanz)) {
            $entry = "{0} {1}m" -f $code, $distanz
        }

        if (-not $schadenByUntersuchung.ContainsKey($untersuchungRef)) {
            $schadenByUntersuchung[$untersuchungRef] = @()
        }
        if (-not $schadenByUntersuchung[$untersuchungRef].Contains($entry)) {
            $schadenByUntersuchung[$untersuchungRef] += $entry
        }
    }

    $rows = @()
    foreach ($node in $untersuchungen) {
        $tid = $node.GetAttribute("TID")
        $row = New-EmptyRow

        $bezeichnung = (Get-ChildText -Node $node -LocalName "Bezeichnung")
        $von = (Get-ChildText -Node $node -LocalName "vonPunktBezeichnung")
        $bis = (Get-ChildText -Node $node -LocalName "bisPunktBezeichnung")
        if (-not (Test-BlankValue $bezeichnung)) {
            $row.Haltungsname = $bezeichnung
        } elseif (-not (Test-BlankValue $von) -and -not (Test-BlankValue $bis)) {
            $row.Haltungsname = "$von-$bis"
        }

        $row.Ausfuehrung_durch = (Get-ChildText -Node $node -LocalName "Ausfuehrender")
        $row.Haltungslaenge_m = Convert-NumberString -Value (Get-ChildText -Node $node -LocalName "Inspizierte_Laenge")

        if (-not (Test-BlankValue $tid) -and $schadenByUntersuchung.ContainsKey($tid)) {
            $row.Primaere_Schaeden = ($schadenByUntersuchung[$tid] -join "; ")
        }

        if (-not (Test-BlankValue $tid) -and $videoByUntersuchung.ContainsKey($tid)) {
            $row.Link = $videoByUntersuchung[$tid]
        }

        if (-not (Test-BlankValue $row.Haltungsname)) {
            $rows += $row
        }
    }

    return $rows
}

function Import-XtfFiles {
    param([string[]]$Paths)

    $rows = @()
    $unknown = @()
    foreach ($path in $Paths) {
        if (-not (Test-Path -Path $path)) { continue }
        $model = Get-XtfModelName -FilePath $path
        if ($model -match "SIA405") {
            $rows = Merge-Data -Existing $rows -Incoming (Import-SIA405 -FilePath $path)
        } elseif ($model -match "KEK") {
            $rows = Merge-Data -Existing $rows -Incoming (Import-VsaKek -FilePath $path)
        } else {
            $unknown += $path
        }
    }

    return [PSCustomObject]@{
        Rows = $rows
        Unknown = $unknown
    }
}

function Get-XtfModelType {
    param([string]$ModelName)

    if ([string]::IsNullOrWhiteSpace($ModelName)) { return "" }
    if ($ModelName -match "SIA405") { return "SIA405" }
    if ($ModelName -match "KEK") { return "KEK" }
    return ""
}

function Find-XtfCounterpart {
    param([string]$FilePath)

    if (-not (Test-Path -Path $FilePath)) { return "" }
    $modelName = Get-XtfModelName -FilePath $FilePath
    $modelType = Get-XtfModelType -ModelName $modelName
    if ([string]::IsNullOrWhiteSpace($modelType)) { return "" }

    $dir = Split-Path -Path $FilePath -Parent
    $name = Split-Path -Path $FilePath -Leaf
    if ($name -match "_SIA405\\.xtf$") {
        $base = ($name -replace "_SIA405\\.xtf$", "")
    } else {
        $base = [System.IO.Path]::GetFileNameWithoutExtension($name)
    }

    if ($modelType -eq "SIA405") {
        $candidate = Join-Path $dir ("{0}.xtf" -f $base)
        if (Test-Path -Path $candidate) {
            $candidateType = Get-XtfModelType -ModelName (Get-XtfModelName -FilePath $candidate)
            if ($candidateType -eq "KEK") { return $candidate }
        }

        $candidates = Get-ChildItem -Path $dir -Filter *.xtf -File | Where-Object { $_.FullName -ne $FilePath }
        foreach ($cand in $candidates) {
            $candidateType = Get-XtfModelType -ModelName (Get-XtfModelName -FilePath $cand.FullName)
            if ($candidateType -eq "KEK") { return $cand.FullName }
        }
    } else {
        $candidate = Join-Path $dir ("{0}_SIA405.xtf" -f $base)
        if (Test-Path -Path $candidate) {
            $candidateType = Get-XtfModelType -ModelName (Get-XtfModelName -FilePath $candidate)
            if ($candidateType -eq "SIA405") { return $candidate }
        }

        $candidates = Get-ChildItem -Path $dir -Filter *_SIA405.xtf -File | Where-Object { $_.FullName -ne $FilePath }
        foreach ($cand in $candidates) {
            $candidateType = Get-XtfModelType -ModelName (Get-XtfModelName -FilePath $cand.FullName)
            if ($candidateType -eq "SIA405") { return $cand.FullName }
        }
    }

    return ""
}

function Test-BlankValue {
    param([object]$Value)
    return ($null -eq $Value -or [string]::IsNullOrWhiteSpace([string]$Value))
}

function Merge-Data {
    param([array]$Existing, [array]$Incoming)

    $Existing = @($Existing)
    $Incoming = @($Incoming)

    $fields = @(
        "NR","Ausfuehrung_durch","Haltungsname","Strasse","Rohrmaterial","DN_mm","Nutzungsart",
        "Haltungslaenge_m","Fliessrichtung","Primaere_Schaeden","Zustandsklasse","Pruefungsresultat",
        "Sanieren_Ja_Nein","Empfohlene_Sanierungsmassnahmen","Kosten","Eigentuemer","Bemerkungen","Link",
        "Renovierung_Inliner_Stk","Renovierung_Inliner_m","Anschluesse_verpressen","Reparatur_Manschette",
        "Reparatur_Kurzliner","Erneuerung_Neubau_m","Offen_abgeschlossen","Datum_Jahr"
    )

    $map = @{}
    foreach ($row in $Existing) {
        if (-not (Test-BlankValue $row.Haltungsname)) {
            $map[$row.Haltungsname] = $row
        }
    }

    foreach ($row in $Incoming) {
        if (-not (Test-BlankValue $row.Haltungsname) -and $map.ContainsKey($row.Haltungsname)) {
            $target = $map[$row.Haltungsname]
            foreach ($f in $fields) {
                if (Test-BlankValue $target.$f -and -not (Test-BlankValue $row.$f)) {
                    $target.$f = $row.$f
                }
            }
        } else {
            $Existing += $row
            if (-not (Test-BlankValue $row.Haltungsname)) { $map[$row.Haltungsname] = $row }
        }
    }

    return $Existing
}

function Get-NormalizedHeader {
    param([string]$Value)
    $v = $Value.ToLowerInvariant()
    $v = $v -replace ([char]0x201E), "ae"
    $v = $v -replace ([char]0x201D), "oe"
    $v = $v -replace ([char]0x0081), "ue"
    $v = $v -replace ([char]0x00E1), "ss"
    $v = $v -replace ([char]0x00E4), "ae"
    $v = $v -replace ([char]0x00F6), "oe"
    $v = $v -replace ([char]0x00FC), "ue"
    $v = $v -replace ([char]0x00DF), "ss"
    $v = $v -replace "[^a-z0-9]+", ""
    return $v
}

function Export-Excel {
    param([array]$Rows, [string]$TemplatePath, [string]$OutputPath)

    if (-not (Test-Path -Path $TemplatePath)) { throw "Template not found: $TemplatePath" }
    Copy-Item -Path $TemplatePath -Destination $OutputPath -Force

    $excel = New-Object -ComObject Excel.Application
    $excel.Visible = $false
    $excel.DisplayAlerts = $false
    $wb = $excel.Workbooks.Open($OutputPath, $null, $false)

    $targetSheet = $null
    $headerRow = 0
    foreach ($ws in $wb.Worksheets) {
        $used = $ws.UsedRange
        $rowsCount = $used.Rows.Count
        $colsCount = $used.Columns.Count
        for ($r = 1; $r -le [Math]::Min(30, $rowsCount); $r++) {
            for ($c = 1; $c -le $colsCount; $c++) {
                $v = $ws.Cells.Item($r, $c).Text
                if ($v -like "Haltungsname*" -or $v -like "Haltungsnahme*") {
                    $targetSheet = $ws
                    $headerRow = $r
                    break
                }
            }
            if ($null -ne $targetSheet) { break }
        }
        if ($null -ne $targetSheet) { break }
    }
    if ($null -eq $targetSheet) { throw "No sheet with Haltungsname header found." }

    $targetSheet.Rows.Item(1).Insert() | Out-Null
    $targetSheet.Cells.Item(1, 1) = "Auswertungsname"
    $targetSheet.Cells.Item(1, 2) = $script:ReportMeta.Auswertungsname
    $targetSheet.Cells.Item(1, 3) = "Zone"
    $targetSheet.Cells.Item(1, 4) = $script:ReportMeta.Zone
    $headerRow++

    $used = $targetSheet.UsedRange
    $colsCount = $used.Columns.Count
    $colMap = @{}
    $headerNormByCol = @{}
    for ($c = 1; $c -le $colsCount; $c++) {
        $v = $targetSheet.Cells.Item($headerRow, $c).Text
        if (-not $v) { continue }
        $n = Get-NormalizedHeader -Value $v
        $headerNormByCol[$c] = $n
        switch ($n) {
            "nr" { $colMap.NR = $c }
            "ausfuehrungdurch" { $colMap.Ausfuehrung = $c }
            "haltungsname" { $colMap.Haltungsname = $c }
            "haltungsnahmeid" { $colMap.Haltungsname = $c }
            "haltungsnahme" { $colMap.Haltungsname = $c }
            "strasse" { $colMap.Strasse = $c }
            "rohrmaterial" { $colMap.Rohrmaterial = $c }
            "dnmm" { $colMap.DN = $c }
            "nutzungsart" { $colMap.Nutzungsart = $c }
            "haltungslaengem" { $colMap.Laenge = $c }
            "haltungslangem" { $colMap.Laenge = $c }
            "fliessrichtung" { $colMap.Fliessrichtung = $c }
            "primaereschaeden" { $colMap.Schaeden = $c }
            "zustandsklasse" { $colMap.Zustandsklasse = $c }
            "pruefungsresultat" { $colMap.Pruefungsresultat = $c }
            "sanierenjanein" { $colMap.Sanieren = $c }
            "empfohlenesanierungsmassnahmen" { $colMap.Massnahmen = $c }
            "kosten" { $colMap.Kosten = $c }
            "eigentuemer" { $colMap.Eigentuemer = $c }
            "bemerkungen" { $colMap.Bemerkungen = $c }
            "link" { $colMap.Link = $c }
            "renovierunginlinerstk" { $colMap.Renovierung_Inliner_Stk = $c }
            "renovierunginlinerm" { $colMap.Renovierung_Inliner_m = $c }
            "anschluesseverpressen" { $colMap.Anschluesse_verpressen = $c }
            "reparaturmanschette" { $colMap.Reparatur_Manschette = $c }
            "reparaturkurzliner" { $colMap.Reparatur_Kurzliner = $c }
            "erneuerungneubaum" { $colMap.Erneuerung_Neubau_m = $c }
            "offenabgeschlossen" { $colMap.Offen_abgeschlossen = $c }
            "datumjahr" { $colMap.Datum_Jahr = $c }
        }
    }
    if ($colMap.ContainsKey("Renovierung_Inliner_Stk") -and -not $colMap.ContainsKey("Renovierung_Inliner_m")) {
        $nextCol = $colMap["Renovierung_Inliner_Stk"] + 1
        if ($headerNormByCol.ContainsKey($nextCol) -and $headerNormByCol[$nextCol] -eq "m") {
            $colMap["Renovierung_Inliner_m"] = $nextCol
        }
    }

    $firstDataRow = $headerRow + 1
    $lastRow = 0
    $lastCol = 0
    try {
        $lastCell = $targetSheet.Cells.SpecialCells(11)
        $lastRow = $lastCell.Row
        $lastCol = $lastCell.Column
    } catch {
        $used = $targetSheet.UsedRange
        $lastRow = $used.Row + $used.Rows.Count - 1
        $lastCol = $used.Column + $used.Columns.Count - 1
    }
    if ($lastRow -ge $firstDataRow -and $lastCol -ge 1) {
        $targetSheet.Range(
            $targetSheet.Cells.Item($firstDataRow, 1),
            $targetSheet.Cells.Item($lastRow, $lastCol)
        ).ClearContents()
    }

    Apply-ReportMetaToSheet -Sheet $targetSheet

    $rowIndex = $headerRow + 1
    foreach ($row in $Rows) {
        if ($colMap.ContainsKey("NR")) { $targetSheet.Cells.Item($rowIndex, $colMap["NR"]) = $row.NR }
        if ($colMap.ContainsKey("Ausfuehrung")) { $targetSheet.Cells.Item($rowIndex, $colMap["Ausfuehrung"]) = $row.Ausfuehrung_durch }
        if ($colMap.ContainsKey("Haltungsname")) { $targetSheet.Cells.Item($rowIndex, $colMap["Haltungsname"]) = $row.Haltungsname }
        if ($colMap.ContainsKey("Strasse")) { $targetSheet.Cells.Item($rowIndex, $colMap["Strasse"]) = $row.Strasse }
        if ($colMap.ContainsKey("Rohrmaterial")) { $targetSheet.Cells.Item($rowIndex, $colMap["Rohrmaterial"]) = $row.Rohrmaterial }
        if ($colMap.ContainsKey("DN")) { $targetSheet.Cells.Item($rowIndex, $colMap["DN"]) = $row.DN_mm }
        if ($colMap.ContainsKey("Nutzungsart")) { $targetSheet.Cells.Item($rowIndex, $colMap["Nutzungsart"]) = $row.Nutzungsart }
        if ($colMap.ContainsKey("Laenge")) { $targetSheet.Cells.Item($rowIndex, $colMap["Laenge"]) = $row.Haltungslaenge_m }
        if ($colMap.ContainsKey("Fliessrichtung")) { $targetSheet.Cells.Item($rowIndex, $colMap["Fliessrichtung"]) = $row.Fliessrichtung }
        if ($colMap.ContainsKey("Schaeden")) { $targetSheet.Cells.Item($rowIndex, $colMap["Schaeden"]) = $row.Primaere_Schaeden }
        if ($colMap.ContainsKey("Zustandsklasse")) { $targetSheet.Cells.Item($rowIndex, $colMap["Zustandsklasse"]) = $row.Zustandsklasse }
        if ($colMap.ContainsKey("Pruefungsresultat")) { $targetSheet.Cells.Item($rowIndex, $colMap["Pruefungsresultat"]) = $row.Pruefungsresultat }
        if ($colMap.ContainsKey("Sanieren")) { $targetSheet.Cells.Item($rowIndex, $colMap["Sanieren"]) = $row.Sanieren_Ja_Nein }
        if ($colMap.ContainsKey("Massnahmen")) { $targetSheet.Cells.Item($rowIndex, $colMap["Massnahmen"]) = $row.Empfohlene_Sanierungsmassnahmen }
        if ($colMap.ContainsKey("Kosten")) { $targetSheet.Cells.Item($rowIndex, $colMap["Kosten"]) = $row.Kosten }
        if ($colMap.ContainsKey("Eigentuemer")) { $targetSheet.Cells.Item($rowIndex, $colMap["Eigentuemer"]) = $row.Eigentuemer }
        if ($colMap.ContainsKey("Bemerkungen")) { $targetSheet.Cells.Item($rowIndex, $colMap["Bemerkungen"]) = $row.Bemerkungen }
        if ($colMap.ContainsKey("Link")) { $targetSheet.Cells.Item($rowIndex, $colMap["Link"]) = $row.Link }
        if ($colMap.ContainsKey("Renovierung_Inliner_Stk")) { $targetSheet.Cells.Item($rowIndex, $colMap["Renovierung_Inliner_Stk"]) = $row.Renovierung_Inliner_Stk }
        if ($colMap.ContainsKey("Renovierung_Inliner_m")) { $targetSheet.Cells.Item($rowIndex, $colMap["Renovierung_Inliner_m"]) = $row.Renovierung_Inliner_m }
        if ($colMap.ContainsKey("Anschluesse_verpressen")) { $targetSheet.Cells.Item($rowIndex, $colMap["Anschluesse_verpressen"]) = $row.Anschluesse_verpressen }
        if ($colMap.ContainsKey("Reparatur_Manschette")) { $targetSheet.Cells.Item($rowIndex, $colMap["Reparatur_Manschette"]) = $row.Reparatur_Manschette }
        if ($colMap.ContainsKey("Reparatur_Kurzliner")) { $targetSheet.Cells.Item($rowIndex, $colMap["Reparatur_Kurzliner"]) = $row.Reparatur_Kurzliner }
        if ($colMap.ContainsKey("Erneuerung_Neubau_m")) { $targetSheet.Cells.Item($rowIndex, $colMap["Erneuerung_Neubau_m"]) = $row.Erneuerung_Neubau_m }
        if ($colMap.ContainsKey("Offen_abgeschlossen")) { $targetSheet.Cells.Item($rowIndex, $colMap["Offen_abgeschlossen"]) = $row.Offen_abgeschlossen }
        if ($colMap.ContainsKey("Datum_Jahr")) { $targetSheet.Cells.Item($rowIndex, $colMap["Datum_Jahr"]) = $row.Datum_Jahr }
        $rowIndex++
    }

    $targetSheet.Columns.AutoFit() | Out-Null
    $wb.Save()
    $wb.Close($false)
    $excel.Quit()
    [System.Runtime.InteropServices.Marshal]::ReleaseComObject($excel) | Out-Null
}

function Add-ComboColumn {
    param(
        [System.Windows.Forms.DataGridView]$Grid,
        [string]$Name,
        [string[]]$Items
    )

    if ($null -eq $Grid) { return }

    $existing = $Grid.Columns[$Name]
    if ($null -eq $existing) { return }
    if ($existing -is [System.Windows.Forms.DataGridViewComboBoxColumn]) {
        $existing.Items.Clear()
        if ($Items) { [void]$existing.Items.AddRange($Items) }
        return
    }

    $displayIndex = $existing.DisplayIndex
    $visible = $existing.Visible
    $width = $existing.Width
    $readOnly = $existing.ReadOnly
    $Grid.Columns.Remove($existing)

    $col = New-Object System.Windows.Forms.DataGridViewComboBoxColumn
    $col.Name = $Name
    $col.DataPropertyName = $Name
    if ($script:FieldHeaders.ContainsKey($Name)) {
        $col.HeaderText = $script:FieldHeaders[$Name]
    }
    $col.DisplayStyle = [System.Windows.Forms.DataGridViewComboBoxDisplayStyle]::DropDownButton
    $col.FlatStyle = [System.Windows.Forms.FlatStyle]::Flat
    if ($Items) { [void]$col.Items.AddRange($Items) }
    $col.SortMode = [System.Windows.Forms.DataGridViewColumnSortMode]::NotSortable
    $col.Resizable = [System.Windows.Forms.DataGridViewTriState]::True
    $col.Visible = $visible
    $col.ReadOnly = $readOnly

    [void]$Grid.Columns.Add($col)
    $col.DisplayIndex = $displayIndex
    if ($width -gt 0) { $col.Width = $width }
}

function Test-NumericValue {
    param([string]$Value)

    if ([string]::IsNullOrWhiteSpace($Value)) { return $true }
    $clean = ($Value -replace "[^0-9.,-]", "")
    if ([string]::IsNullOrWhiteSpace($clean)) { return $false }

    $num = 0.0
    $styles = [System.Globalization.NumberStyles]::Number
    $cultures = @(
        [System.Globalization.CultureInfo]::CurrentCulture,
        [System.Globalization.CultureInfo]::InvariantCulture
    )

    foreach ($culture in $cultures) {
        if ([double]::TryParse($clean, $styles, $culture, [ref]$num)) { return $true }
    }

    return $false
}

function Update-Grid {
    param([System.Windows.Forms.DataGridView]$Grid)

    if ($null -eq $script:Binding) {
        $script:Binding = New-Object System.Windows.Forms.BindingSource
    }

    # Re-bind
    $script:Binding.DataSource = $script:Data
    $Grid.DataSource = $script:Binding
    $Grid.AutoGenerateColumns = $true

    # Button-Spalten hinzufuegen, falls noetig
    if ($null -eq $Grid.Columns["Import_PDF"]) {
        $btnCol = New-Object System.Windows.Forms.DataGridViewButtonColumn
        $btnCol.Name = "Import_PDF"
        $btnCol.HeaderText = "Import PDF"
        $btnCol.Text = "Import PDF"
        $btnCol.UseColumnTextForButtonValue = $true
        $btnCol.Width = 95
        $btnCol.SortMode = [System.Windows.Forms.DataGridViewColumnSortMode]::NotSortable
        [void]$Grid.Columns.Add($btnCol)
    }

    $comboMap = @{
        Rohrmaterial = @("", "PVC", "PE", "PP", "GFK", "Beton", "Steinzeug", "Guss")
        Fliessrichtung = @("", "mit Gefaelle", "gegen Gefaelle", "unbekannt")
        Zustandsklasse = @("", "1", "2", "3", "4", "5")
        Sanieren_Ja_Nein = @("", "Ja", "Nein")
    }
    foreach ($kv in $comboMap.GetEnumerator()) {
        Add-ComboColumn -Grid $Grid -Name $kv.Key -Items $kv.Value
    }

    # Sichtbarkeit / Reihenfolge
    $visibleSet = @{}
    foreach ($name in $script:GridFieldOrder) { $visibleSet[$name] = $true }

    for ($i = 0; $i -lt $Grid.Columns.Count; $i++) {
        $col = $Grid.Columns[$i]
        $name = $col.Name
        if ($script:FieldHeaders.ContainsKey($name)) {
            $col.HeaderText = $script:FieldHeaders[$name]
        }
        # Nur definierte Felder + Button anzeigen
        if ($name -eq "Import_PDF") {
            $col.Visible = $true
        } else {
            $col.Visible = $visibleSet.ContainsKey($name)
        }
        $col.SortMode = [System.Windows.Forms.DataGridViewColumnSortMode]::NotSortable
        $col.Resizable = [System.Windows.Forms.DataGridViewTriState]::True
    }

    # Reihenfolge wie im PDF-Mock
    $index = 0
    foreach ($name in $script:GridFieldOrder) {
        $col = $Grid.Columns[$name]
        if ($null -ne $col) {
            $col.DisplayIndex = $index
            $index++
        }
    }
    $importCol = $Grid.Columns["Import_PDF"]
    if ($null -ne $importCol) {
        $importCol.DisplayIndex = $index
        $index++
    }

    # Breiten (damit alles in eine Zeile passt - wie Excel)
    $Grid.AutoSizeColumnsMode = [System.Windows.Forms.DataGridViewAutoSizeColumnsMode]::None
    $widths = @{
        "NR" = 35
        "Haltungsname" = 80
        "Strasse" = 80
        "Rohrmaterial" = 80
        "DN_mm" = 60
        "Haltungslaenge_m" = 70
        "Fliessrichtung" = 70
        "Primaere_Schaeden" = 100
        "Zustandsklasse" = 80
        "Sanieren_Ja_Nein" = 90
        "Empfohlene_Sanierungsmassnahmen" = 150
        "Eigentuemer" = 80
        "Kosten" = 70
        "Bemerkungen" = 100
    }
    foreach ($kv in $widths.GetEnumerator()) {
        $col = $Grid.Columns[$kv.Key]
        if ($null -ne $col) {
            $col.Width = [int]$kv.Value
        }
    }

    if ($null -ne $Grid.Columns["Import_PDF"]) {
        $Grid.Columns["Import_PDF"].Width = 95
    }
    if ($null -ne $Grid.Columns["Link"]) {
        $Grid.Columns["Link"].Visible = $false
    }
    if ($null -ne $Grid.Columns["NR"]) {
        $Grid.Columns["NR"].ReadOnly = $true
        $Grid.Columns["NR"].DefaultCellStyle.BackColor = $script:Theme.HeaderBg
    }
    if ($null -ne $Grid.Columns["NR"]) { $Grid.Columns["NR"].Frozen = $true }
    if ($null -ne $Grid.Columns["Haltungsname"]) { $Grid.Columns["Haltungsname"].Frozen = $true }

    $Grid.DefaultCellStyle.WrapMode = [System.Windows.Forms.DataGridViewTriState]::False
    $Grid.AllowUserToResizeRows = $false
    $Grid.RowTemplate.Height = 44
    $Grid.AutoSizeRowsMode = [System.Windows.Forms.DataGridViewAutoSizeRowsMode]::None
    $multilineCols = @("Empfohlene_Sanierungsmassnahmen", "Bemerkungen")
    foreach ($name in $multilineCols) {
        $col = $Grid.Columns[$name]
        if ($null -ne $col) {
            $col.DefaultCellStyle.WrapMode = [System.Windows.Forms.DataGridViewTriState]::True
        }
    }
    $Grid.ScrollBars = [System.Windows.Forms.ScrollBars]::Both
    Set-GridTheme -Grid $Grid
    Update-StatusSummary
}

function Set-RowIntoForm {
    param([pscustomobject]$Row)
    $script:IsLoadingForm = $true
    $script:CurrentRow = $Row
    foreach ($name in $script:FieldOrder) {
        if ($script:FieldBoxes.ContainsKey($name)) {
            if ($null -ne $Row) {
                $script:FieldBoxes[$name].Text = [string]$Row.$name
            } else {
                $script:FieldBoxes[$name].Text = ""
            }
        }
    }
    $script:IsLoadingForm = $false
}

function Set-FormToRow {
    param([pscustomobject]$Row)
    foreach ($name in $script:FieldOrder) {
        if ($script:FieldBoxes.ContainsKey($name)) {
            $Row.$name = $script:FieldBoxes[$name].Text
        }
    }
    if ($null -ne $script:Grid) { $script:Grid.Refresh() }
}

function Add-FormField {
    param(
        [System.Windows.Forms.TableLayoutPanel]$Table,
        [int]$Row,
        [int]$Col,
        [string]$LabelText,
        [string]$PropertyName,
        [switch]$ReadOnly,
        [switch]$Multiline
    )
    $label = New-Object System.Windows.Forms.Label
    $label.Text = $LabelText
    $label.AutoSize = $true
    $label.Anchor = [System.Windows.Forms.AnchorStyles]::Left

    $textbox = New-Object System.Windows.Forms.TextBox
    $textbox.Tag = $PropertyName
    $textbox.Dock = [System.Windows.Forms.DockStyle]::Fill
    $textbox.Font = $script:UiFont
    if ($ReadOnly) {
        $textbox.ReadOnly = $true
        $textbox.BackColor = $script:Theme.HeaderBg
    }
    if ($Multiline) {
        $textbox.Multiline = $true
        $textbox.ScrollBars = [System.Windows.Forms.ScrollBars]::Vertical
        $textbox.MinimumSize = New-Object System.Drawing.Size(0, 60)
    }

    $textbox.Add_TextChanged({
        param($senderObj, $e)
        if ($script:IsLoadingForm) { return }
        if ($null -eq $script:CurrentRow) { return }
        $prop = $senderObj.Tag
        $script:CurrentRow.$prop = $senderObj.Text
        if ($null -ne $script:Grid) { $script:Grid.Refresh() }
    })

    $script:FieldBoxes[$PropertyName] = $textbox
    $Table.Controls.Add($label, $Col, $Row)
    $Table.Controls.Add($textbox, $Col + 1, $Row)
}

function Find-CellByLabel {
    param(
        [object]$Sheet,
        [string]$Label,
        [int]$MaxRows = 40,
        [int]$MaxCols = 20
    )
    $target = Get-NormalizedHeader -Value $Label
    for ($r = 1; $r -le $MaxRows; $r++) {
        for ($c = 1; $c -le $MaxCols; $c++) {
            $text = $Sheet.Cells.Item($r, $c).Text
            if (-not $text) { continue }
            if ((Get-NormalizedHeader -Value $text) -eq $target) {
                return $Sheet.Cells.Item($r, $c)
            }
        }
    }
    return $null
}

function Set-ReportMetaToSheet {
    param([object]$Sheet)

    if (-not (Test-BlankValue $script:ReportMeta.LogoPath) -and (Test-Path -Path $script:ReportMeta.LogoPath)) {
        $cell = Find-CellByLabel -Sheet $Sheet -Label "Logo"
        if ($cell) {
            $targetCell = $Sheet.Cells.Item($cell.Row, $cell.Column + 1)
        } else {
            $targetCell = $Sheet.Cells.Item(1, 6)
        }
        $left = $targetCell.Left
        $top = $targetCell.Top
        $width = 140
        $height = 70
        $Sheet.Shapes.AddPicture($script:ReportMeta.LogoPath, $false, $true, $left, $top, $width, $height) | Out-Null
    }
}

function Clear-LogoBoxes {
    foreach ($box in @($script:LogoBox, $script:StartLogoBox)) {
        if ($null -ne $box -and $null -ne $box.Image) {
            $old = $box.Image
            $box.Image = $null
            $old.Dispose()
        }
    }
}

function Update-LogoBoxesFromPath {
    param([string]$Path)
    Clear-LogoBoxes
    if (-not (Test-BlankValue $Path) -and (Test-Path -Path $Path)) {
        $bytes = [System.IO.File]::ReadAllBytes($Path)
        $ms = New-Object System.IO.MemoryStream(,$bytes)
        $img = [System.Drawing.Image]::FromStream($ms)
        foreach ($box in @($script:LogoBox, $script:StartLogoBox)) {
            if ($null -ne $box) { $box.Image = $img.Clone() }
        }
        $img.Dispose()
        $ms.Dispose()
    }
}

function Set-LogoPath {
    param([string]$Path)
    $script:ReportMeta.LogoPath = $Path
    Update-LogoBoxesFromPath -Path $Path
}

function Convert-ProjectRow {
    param([object]$Row)
    $newRow = New-EmptyRow
    foreach ($name in $script:FieldOrder) {
        if ($Row.PSObject.Properties.Match($name)) {
            $newRow.$name = [string]$Row.$name
        }
    }
    return $newRow
}

function Update-ProjectLabel {
    if ($null -eq $script:ProjectLabel) { return }
    if ([string]::IsNullOrWhiteSpace($script:ProjectPath)) {
        $script:ProjectLabel.Text = "Projektseite: (kein)"
    } else {
        $script:ProjectLabel.Text = "Projektseite: " + [System.IO.Path]::GetFileName($script:ProjectPath)
    }
    Update-StatusSummary
}

function Update-StatusSummary {
    if ($null -eq $script:StatusRightLabel) { return }
    $projectName = if (Test-BlankValue $script:ReportMeta.Auswertungsname) { "(kein)" } else { $script:ReportMeta.Auswertungsname }
    $zone = if (Test-BlankValue $script:ReportMeta.Zone) { "-" } else { $script:ReportMeta.Zone }
    $count = @($script:Data).Count
    $script:StatusRightLabel.Text = "Projekt: $projectName | Zone: $zone | Datensaetze: $count"
}

function Test-ImportFolder {
    $importPath = Join-Path $script:AppRoot "PDF"
    if (-not (Test-Path -Path $importPath)) {
        New-Item -Path $importPath -ItemType Directory -Force | Out-Null
    }
    return $importPath
}

function Get-UniquePath {
    param([string]$TargetPath)
    if (-not (Test-Path -Path $TargetPath)) { return $TargetPath }
    $dir = [System.IO.Path]::GetDirectoryName($TargetPath)
    $name = [System.IO.Path]::GetFileNameWithoutExtension($TargetPath)
    $ext = [System.IO.Path]::GetExtension($TargetPath)
    for ($i = 1; $i -le 200; $i++) {
        $candidate = Join-Path $dir ("{0}_{1}{2}" -f $name, $i, $ext)
        if (-not (Test-Path -Path $candidate)) { return $candidate }
    }
    return $TargetPath
}

function Update-ProjectList {
    if ($null -eq $script:ProjectListBox) { return }
    $files = @()
    $files += Get-ChildItem -Path $script:AppRoot -File -Filter *.ausproj -ErrorAction SilentlyContinue
    $files += Get-ChildItem -Path $script:AppRoot -File -Filter *.json -ErrorAction SilentlyContinue
    $files = @($files | Sort-Object Name -Unique)
    $script:ProjectListBox.BeginUpdate()
    $script:ProjectListBox.Items.Clear()
    foreach ($file in $files) {
        [void]$script:ProjectListBox.Items.Add($file)
    }
    $script:ProjectListBox.EndUpdate()
}

function Show-StartScreen {
    if ($null -ne $script:StartProjectNameBox) { $script:StartProjectNameBox.Text = $script:ReportMeta.Auswertungsname }
    if ($null -ne $script:StartZoneBox) { $script:StartZoneBox.Text = $script:ReportMeta.Zone }
    if ($null -ne $script:StartFirmaNameBox) { $script:StartFirmaNameBox.Text = $script:ReportMeta.FirmaName }
    if ($null -ne $script:StartFirmaAdresseBox) { $script:StartFirmaAdresseBox.Text = $script:ReportMeta.FirmaAdresse }
    if ($null -ne $script:StartFirmaTelefonBox) { $script:StartFirmaTelefonBox.Text = $script:ReportMeta.FirmaTelefon }
    if ($null -ne $script:StartFirmaEmailBox) { $script:StartFirmaEmailBox.Text = $script:ReportMeta.FirmaEmail }
    if ($null -ne $script:StartPanel) { $script:StartPanel.Visible = $true }
    if ($null -ne $script:MainPanel) { $script:MainPanel.Visible = $false }
    Update-ProjectList
}

function Show-MainScreen {
    if ($null -ne $script:MetaNameBox) { $script:MetaNameBox.Text = $script:ReportMeta.Auswertungsname }
    if ($null -ne $script:MetaZoneBox) { $script:MetaZoneBox.Text = $script:ReportMeta.Zone }
    if ($null -ne $script:StartPanel) { $script:StartPanel.Visible = $false }
    if ($null -ne $script:MainPanel) { $script:MainPanel.Visible = $true }
    if ($null -ne $script:DetailsPanel) { $script:DetailsPanel.Visible = $false }
    if ($null -ne $script:ToggleFormButton) { $script:ToggleFormButton.Text = "Maske ein" }
}

function Set-StartMeta {
    if ($null -ne $script:StartProjectNameBox) { $script:ReportMeta.Auswertungsname = $script:StartProjectNameBox.Text.Trim() }
    if ($null -ne $script:StartZoneBox) { $script:ReportMeta.Zone = $script:StartZoneBox.Text.Trim() }
    if ($null -ne $script:StartFirmaNameBox) { $script:ReportMeta.FirmaName = $script:StartFirmaNameBox.Text.Trim() }
    if ($null -ne $script:StartFirmaAdresseBox) { $script:ReportMeta.FirmaAdresse = $script:StartFirmaAdresseBox.Text.Trim() }
    if ($null -ne $script:StartFirmaTelefonBox) { $script:ReportMeta.FirmaTelefon = $script:StartFirmaTelefonBox.Text.Trim() }
    if ($null -ne $script:StartFirmaEmailBox) { $script:ReportMeta.FirmaEmail = $script:StartFirmaEmailBox.Text.Trim() }
    if ($null -ne $script:MetaNameBox) { $script:MetaNameBox.Text = $script:ReportMeta.Auswertungsname }
    if ($null -ne $script:MetaZoneBox) { $script:MetaZoneBox.Text = $script:ReportMeta.Zone }
}

function Clear-ProjectData {
    $script:Data = @()
    $script:DataRows = @()
    $script:ProjectPath = ""
    Update-ProjectLabel
    if ($null -ne $script:Grid) { Update-Grid -Grid $script:Grid }
    Set-RowIntoForm -Row $null
}

function Select-FirstRow {
    param([System.Windows.Forms.DataGridView]$Grid)
    if ($null -eq $Grid) { return }
    if ($Grid.Rows.Count -gt 0) {
        $Grid.ClearSelection()
        $Grid.Rows[0].Selected = $true
        $Grid.CurrentCell = $Grid.Rows[0].Cells[0]
        $firstRow = $Grid.Rows[0].DataBoundItem
        if ($null -ne $firstRow) { Set-RowIntoForm -Row $firstRow }
    } else {
        Set-RowIntoForm -Row $null
    }
}

function Add-DataRow {
    param([pscustomobject]$Row)
    if ($null -eq $script:Data) { $script:Data = @() }
    $script:Data = @($script:Data) + @($Row)
    $script:DataRows = $script:Data
}

function Add-EmptyDataRowBelowSelection {
    param([System.Windows.Forms.DataGridView]$Grid)

    if ($null -eq $Grid) { $Grid = $script:Grid }
    if ($null -eq $Grid) { return }

    $Grid.EndEdit()
    if ($null -ne $script:Binding) { $script:Binding.EndEdit() }

    if ($null -eq $script:DataRows) { $script:DataRows = @($script:Data) }
    if ($script:DataRows -is [array]) {
        $list = New-Object System.Collections.Generic.List[object]
        $list.AddRange($script:DataRows)
        $script:DataRows = $list
    }

    $insertIndex = if ($null -ne $Grid.CurrentRow) { $Grid.CurrentRow.Index + 1 } elseif ($script:DataRows.Count -gt 0) { $script:DataRows.Count } else { 0 }
    if ($insertIndex -gt $script:DataRows.Count) { $insertIndex = $script:DataRows.Count }

    $newRow = New-EmptyRow
    $newRow.NR = Get-NextRowNumber
    $script:DataRows.Insert($insertIndex, $newRow)
    $script:Data = $script:DataRows
    Update-Grid -Grid $Grid

    if ($Grid.Rows.Count -gt 0 -and $insertIndex -lt $Grid.Rows.Count) {
        $Grid.ClearSelection()
        $Grid.CurrentCell = $Grid.Rows[$insertIndex].Cells[0]
        $Grid.Rows[$insertIndex].Selected = $true
        $Grid.FirstDisplayedScrollingRowIndex = [Math]::Max(0, $insertIndex - 1)
        $Grid.BeginEdit($true) | Out-Null
    }
    if ($insertIndex -lt $script:DataRows.Count) { Set-RowIntoForm -Row $script:DataRows[$insertIndex] }
}

function Add-EmptyRowAndSelect {
    param([switch]$ForceNewRow)

    if ($null -ne $script:Grid) { $script:Grid.EndEdit() }
    if ($null -ne $script:Binding) { $script:Binding.EndEdit() }

    if ($ForceNewRow -or @($script:Data).Count -eq 0) {
        $row = New-EmptyRow
        $row.NR = Get-NextRowNumber
        Add-DataRow -Row $row
        if ($null -ne $script:Grid) { Update-Grid -Grid $script:Grid }
        if ($null -ne $row) { Set-RowIntoForm -Row $row }
    }

    if ($null -ne $script:Grid -and $script:Grid.Rows.Count -gt 0) {
        $script:Grid.ClearSelection()
        $idx = $script:Grid.Rows.Count - 1
        $script:Grid.Rows[$idx].Selected = $true
        $script:Grid.CurrentCell = $script:Grid.Rows[$idx].Cells[0]
    }
}

function Merge-ImportedRowsIntoData {
    param(
        [int]$RowIndex,
        [pscustomobject[]]$ImportRows
    )

    if ($null -eq $ImportRows -or $ImportRows.Count -eq 0) { return }

    $data = @($script:Data)

    # Sicherstellen: es gibt mindestens diese Zeile
    if ($RowIndex -ge $data.Count) {
        while ($data.Count -le $RowIndex) { $data += (New-EmptyRow) }
    }

    # 1) Erste importierte Haltung -> in aktuelle Zeile mergen (leer wird gefuellt)
    $target = $data[$RowIndex]
    $first = $ImportRows[0]
    foreach ($f in $script:FieldOrder) {
        if (Test-BlankValue $target.$f -and -not (Test-BlankValue $first.$f)) {
            $target.$f = $first.$f
        }
    }

    # 2) Weitere importierte Haltungen -> als neue Zeilen direkt darunter einfuegen
    if ($ImportRows.Count -gt 1) {
        $tail = @(
            $ImportRows | Select-Object -Skip 1 | ForEach-Object {
                $row = New-EmptyRow
                foreach ($f in $script:FieldOrder) {
                    if (-not (Test-BlankValue $_.$f)) { $row.$f = $_.$f }
                }
                $row
            }
        )
        $before = @()
        if ($RowIndex -gt 0) { $before = $data[0..($RowIndex)] } else { $before = @($data[0]) }
        $after = @()
        if ($RowIndex -lt ($data.Count - 1)) { $after = $data[($RowIndex+1)..($data.Count-1)] }
        $data = @($before) + @($tail) + @($after)
    }

    $script:Data = $data
    $script:DataRows = $script:Data
    if ($null -ne $script:Grid) { Update-Grid -Grid $script:Grid }

    if ($null -ne $script:Grid -and $script:Grid.Rows.Count -gt 0 -and $RowIndex -lt $script:Grid.Rows.Count) {
        $script:Grid.ClearSelection()
        $script:Grid.Rows[$RowIndex].Selected = $true
        $script:Grid.CurrentCell = $script:Grid.Rows[$RowIndex].Cells[0]
    }
}

function Save-Project {
    param([string]$Path)
    $project = @{
        Version = 1
        ReportMeta = $script:ReportMeta
        Data = @($script:Data)
    }
    $json = $project | ConvertTo-Json -Depth 6
    Set-Content -Path $Path -Value $json -Encoding UTF8
    $script:ProjectPath = $Path
    Update-ProjectLabel
    Update-ProjectList
}

function Import-Project {
    param(
        [string]$Path,
        [System.Windows.Forms.TextBox]$AuswertungTextBox,
        [System.Windows.Forms.TextBox]$ZoneTextBox
    )
    $project = Get-Content -Raw -Path $Path | ConvertFrom-Json
    $script:ReportMeta = New-ReportMeta
    if ($null -ne $project.ReportMeta) {
        $script:ReportMeta.Auswertungsname = [string]$project.ReportMeta.Auswertungsname
        $script:ReportMeta.Zone = [string]$project.ReportMeta.Zone
        $script:ReportMeta.LogoPath = [string]$project.ReportMeta.LogoPath
        $script:ReportMeta.FirmaName = [string]$project.ReportMeta.FirmaName
        $script:ReportMeta.FirmaAdresse = [string]$project.ReportMeta.FirmaAdresse
        $script:ReportMeta.FirmaTelefon = [string]$project.ReportMeta.FirmaTelefon
        $script:ReportMeta.FirmaEmail = [string]$project.ReportMeta.FirmaEmail
    }
    $script:Data = @()
    $script:DataRows = @()
    if ($null -ne $project.Data) {
        foreach ($row in @($project.Data)) {
            Add-DataRow -Row (Normalize-ProjectRow -Row $row)
        }
    }
    $script:ProjectPath = $Path
    Update-ProjectLabel

    if ($null -ne $AuswertungTextBox) { $AuswertungTextBox.Text = $script:ReportMeta.Auswertungsname }
    if ($null -ne $ZoneTextBox) { $ZoneTextBox.Text = $script:ReportMeta.Zone }
    if ($null -ne $script:StartProjectNameBox) { $script:StartProjectNameBox.Text = $script:ReportMeta.Auswertungsname }
    if ($null -ne $script:StartZoneBox) { $script:StartZoneBox.Text = $script:ReportMeta.Zone }
    if ($null -ne $script:StartFirmaNameBox) { $script:StartFirmaNameBox.Text = $script:ReportMeta.FirmaName }
    if ($null -ne $script:StartFirmaAdresseBox) { $script:StartFirmaAdresseBox.Text = $script:ReportMeta.FirmaAdresse }
    if ($null -ne $script:StartFirmaTelefonBox) { $script:StartFirmaTelefonBox.Text = $script:ReportMeta.FirmaTelefon }
    if ($null -ne $script:StartFirmaEmailBox) { $script:StartFirmaEmailBox.Text = $script:ReportMeta.FirmaEmail }

    Update-LogoBoxesFromPath -Path $script:ReportMeta.LogoPath

    if ($null -ne $script:Grid) {
        Update-Grid -Grid $script:Grid
        Select-FirstRow -Grid $script:Grid
    }
}

function Set-ModernButton {
    param(
        [System.Windows.Forms.Button]$Button,
        [System.Drawing.Color]$BackColor = $script:Theme.ButtonBg,
        [System.Drawing.Color]$HoverColor = $script:Theme.ButtonHoverBg,
        [System.Drawing.Color]$PressedColor = $script:Theme.ButtonPressedBg,
        [System.Drawing.Color]$BorderColor = $script:Theme.ButtonBorder,
        [System.Drawing.Color]$TextColor = $script:Theme.Text
    )
    $Button.FlatStyle = [System.Windows.Forms.FlatStyle]::Flat
    $Button.FlatAppearance.BorderSize = 1
    $Button.FlatAppearance.BorderColor = $BorderColor
    $Button.FlatAppearance.MouseOverBackColor = $HoverColor
    $Button.FlatAppearance.MouseDownBackColor = $PressedColor
    $Button.BackColor = $BackColor
    $Button.ForeColor = $TextColor
    $Button.Font = $script:ButtonFont
    $Button.UseVisualStyleBackColor = $false
}

function Set-UiButtonStyle {
    param(
        [System.Windows.Forms.Button]$Button,
        [int]$Width = 140,
        [int]$Height = 40,
        [switch]$Small
    )

    $Button.AutoSize = $false
    if ($Small) {
        $Width = 56
        if ($Height -lt 40) { $Height = 40 }
    }

    $Button.Size = New-Object System.Drawing.Size($Width, $Height)
    $Button.MinimumSize = New-Object System.Drawing.Size($Width, $Height)

    if ($Small) {
        $Button.Font = New-Object System.Drawing.Font("Segoe UI", 11, [System.Drawing.FontStyle]::Bold)
    } else {
        $Button.Font = $script:ButtonFont
    }

    $Button.FlatStyle = [System.Windows.Forms.FlatStyle]::Flat
    $Button.FlatAppearance.BorderSize = 0

    # Hover/Down immer theme-clean
    $Button.FlatAppearance.MouseOverBackColor = $script:Theme.ButtonHoverBg
    $Button.FlatAppearance.MouseDownBackColor = $script:Theme.ButtonHoverBg

    # WICHTIG: BackColor nur setzen, wenn noch "leer"
    if ($Button.BackColor.IsEmpty -or $Button.BackColor.A -eq 0) {
        $Button.BackColor = $script:Theme.HeaderBg
    }
    $Button.ForeColor = $script:Theme.Text
    $Button.UseVisualStyleBackColor = $false
    $Button.Padding = New-Object System.Windows.Forms.Padding(10, 6, 10, 6)
    $Button.Margin = New-Object System.Windows.Forms.Padding(8, 0, 8, 0)
}

function Set-FormTheme {
    param([System.Windows.Forms.Form]$Form)
    if ($null -eq $Form) { return }
    $Form.BackColor = $script:Theme.AppBg
    $Form.ForeColor = $script:Theme.Text
    $Form.Font = $script:UiFont
}

function Set-GridTheme {
    param([System.Windows.Forms.DataGridView]$Grid)
    if ($null -eq $Grid) { return }
    $Grid.BackgroundColor = $script:Theme.CardBg
    $Grid.BorderStyle = [System.Windows.Forms.BorderStyle]::None
    $Grid.CellBorderStyle = [System.Windows.Forms.DataGridViewCellBorderStyle]::SingleHorizontal
    $Grid.GridColor = $script:Theme.Border
    $Grid.EnableHeadersVisualStyles = $false
    $Grid.ColumnHeadersDefaultCellStyle.BackColor = $script:Theme.HeaderBg
    $Grid.ColumnHeadersDefaultCellStyle.ForeColor = $script:Theme.Text
    $Grid.ColumnHeadersDefaultCellStyle.Font = $script:UiFontBold
    $Grid.ColumnHeadersDefaultCellStyle.Alignment = [System.Windows.Forms.DataGridViewContentAlignment]::MiddleLeft
    $Grid.ColumnHeadersBorderStyle = [System.Windows.Forms.DataGridViewHeaderBorderStyle]::None
    $Grid.ColumnHeadersHeight = 34
    $Grid.ColumnHeadersHeightSizeMode = [System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode]::DisableResizing
    $Grid.DefaultCellStyle.BackColor = $script:Theme.CardBg
    $Grid.DefaultCellStyle.ForeColor = $script:Theme.Text
    $Grid.DefaultCellStyle.Font = $script:UiFont
    $Grid.DefaultCellStyle.SelectionBackColor = $script:Theme.SelectionBg
    $Grid.DefaultCellStyle.SelectionForeColor = $script:Theme.Text
    $Grid.DefaultCellStyle.WrapMode = [System.Windows.Forms.DataGridViewTriState]::False
    $Grid.AlternatingRowsDefaultCellStyle.BackColor = $script:Theme.AltRowBg
    $Grid.RowHeadersVisible = $false
    $Grid.RowHeadersBorderStyle = [System.Windows.Forms.DataGridViewHeaderBorderStyle]::None
    $Grid.SelectionMode = [System.Windows.Forms.DataGridViewSelectionMode]::FullRowSelect
    $Grid.MultiSelect = $false
    $Grid.RowTemplate.Height = 26
    $Grid.AllowUserToResizeRows = $false
}

function Get-PdfFilesFromPaths {
    param([string[]]$Paths)
    $files = @()
    foreach ($p in $Paths) {
        if (-not $p) { continue }
        if (-not (Test-Path -Path $p)) { continue }
        $item = Get-Item -Path $p -ErrorAction SilentlyContinue
        if ($item -is [System.IO.DirectoryInfo]) {
            $files += Get-ChildItem -Path $item.FullName -Filter *.pdf -File -Recurse
        } elseif ($item -is [System.IO.FileInfo]) {
            if ($item.Extension -ieq ".pdf") { $files += $item }
        }
    }
    return $files | Sort-Object -Property FullName -Unique
}

function Import-PdfFiles {
    param(
        [System.IO.FileInfo[]]$PdfFiles,
        [System.Windows.Forms.DataGridView]$Grid,
        [object]$StatusLabel,
        [System.Windows.Forms.Form]$Form
    )

    $pdfList = @($PdfFiles)
    $pdfCount = @($pdfList).Count
    if ($pdfCount -eq 0) {
        [System.Windows.Forms.MessageBox]::Show("Keine PDF Dateien gefunden.", "Info")
        $StatusLabel.Text = "Keine PDFs gefunden"
        $StatusLabel.ForeColor = [System.Drawing.Color]::DarkOrange
        return
    }

    try {
        $importFolder = Ensure-ImportFolder
        foreach ($pdf in $pdfList) {
            try {
                $destPath = Join-Path $importFolder $pdf.Name
                $destPath = Get-UniquePath -TargetPath $destPath
                Copy-Item -LiteralPath $pdf.FullName -Destination $destPath -Force
            } catch {
                # If copying fails, keep importing from the original path.
                Write-Verbose "Kopieren von '$($pdf.Name)' fehlgeschlagen: $_"
            }
        }

        $StatusLabel.Text = "PDF import..."
        $StatusLabel.ForeColor = [System.Drawing.Color]::Orange
        $Form.Refresh()

        $totalRows = 0
        foreach ($pdf in $pdfList) {
            $rows = ConvertFrom-PdfHaltungen -PdfPath $pdf.FullName
            $script:Data = Merge-Data -Existing $script:Data -Incoming $rows
            $script:DataRows = $script:Data
            $totalRows += @($rows).Count
        }
        Update-Grid -Grid $Grid
        $firstRow = $null
        if ($Grid.Rows.Count -gt 0) {
            $Grid.ClearSelection()
            $Grid.Rows[0].Selected = $true
            $Grid.CurrentCell = $Grid.Rows[0].Cells[0]
            $firstRow = $Grid.Rows[0].DataBoundItem
        }
        if ($null -eq $firstRow -and @($script:Data).Count -gt 0) {
            $firstRow = @($script:Data)[0]
        }
        if ($null -ne $firstRow) { Set-RowIntoForm -Row $firstRow }
        if ($null -ne $script:DetailsPanel -and -not $script:DetailsPanel.Visible) {
            $script:DetailsPanel.Visible = $true
            if ($null -ne $script:ToggleFormButton) { $script:ToggleFormButton.Text = "Maske aus" }
        }

        if ($totalRows -eq 0) {
            $StatusLabel.Text = "Keine Haltungsdaten erkannt"
            $StatusLabel.ForeColor = [System.Drawing.Color]::DarkOrange
            [System.Windows.Forms.MessageBox]::Show("Keine Haltungsdaten in den PDFs erkannt.", "Info")
            return
        }

        $StatusLabel.Text = "PDF importiert: $pdfCount Dateien, $totalRows Haltungen"
        $StatusLabel.ForeColor = [System.Drawing.Color]::DarkGreen
    } catch {
        $StatusLabel.Text = "Fehler PDF: $($_.Exception.Message)"
        $StatusLabel.ForeColor = [System.Drawing.Color]::Red
    }
}

$form = New-Object System.Windows.Forms.Form
$form.Text = "Auswertung Tool v$($script:AppVersion)"
$form.Width = 1800
$form.Height = 900
$form.StartPosition = [System.Windows.Forms.FormStartPosition]::CenterScreen
$form.BackColor = $script:Theme.AppBg
$form.Font = $script:UiFont
$form.AllowDrop = $true
$form.WindowState = [System.Windows.Forms.FormWindowState]::Maximized
Set-FormTheme -Form $form

$mainPanel = New-Object System.Windows.Forms.Panel
$mainPanel.Dock = [System.Windows.Forms.DockStyle]::Fill
$mainPanel.BackColor = $script:Theme.AppBg
$mainPanel.Padding = New-Object System.Windows.Forms.Padding(0)
$script:MainPanel = $mainPanel

$btnPanel = New-Object System.Windows.Forms.Panel
$btnPanel.Dock = [System.Windows.Forms.DockStyle]::Top
$btnPanel.Height = 64
$btnPanel.Padding = New-Object System.Windows.Forms.Padding(8, 4, 8, 4)
$btnPanel.BackColor = $script:Theme.CardBg

$btnBar = New-Object System.Windows.Forms.TableLayoutPanel
$btnBar.Dock = [System.Windows.Forms.DockStyle]::Fill
$btnBar.ColumnCount = 2
$btnBar.RowCount = 1
$btnBar.ColumnStyles.Add((New-Object System.Windows.Forms.ColumnStyle([System.Windows.Forms.SizeType]::Percent, 100)))
$btnBar.ColumnStyles.Add((New-Object System.Windows.Forms.ColumnStyle([System.Windows.Forms.SizeType]::AutoSize)))
$btnPanel.Controls.Add($btnBar)

$btnBarLeft = New-Object System.Windows.Forms.FlowLayoutPanel
$btnBarLeft.Dock = [System.Windows.Forms.DockStyle]::Fill
$btnBarLeft.WrapContents = $false
$btnBarLeft.AutoSize = $false
$btnBarLeft.Height = 56
$btnBarLeft.FlowDirection = [System.Windows.Forms.FlowDirection]::LeftToRight
$btnBarLeft.Padding = New-Object System.Windows.Forms.Padding(0)
$btnBarLeft.Margin = New-Object System.Windows.Forms.Padding(0)
$btnBarLeft.BackColor = $script:Theme.CardBg

$btnBarRight = New-Object System.Windows.Forms.FlowLayoutPanel
$btnBarRight.Dock = [System.Windows.Forms.DockStyle]::Fill
$btnBarRight.WrapContents = $false
$btnBarRight.AutoSize = $false
$btnBarRight.Height = 56
$btnBarRight.FlowDirection = [System.Windows.Forms.FlowDirection]::RightToLeft
$btnBarRight.Padding = New-Object System.Windows.Forms.Padding(0)
$btnBarRight.Margin = New-Object System.Windows.Forms.Padding(0)
$btnBarRight.BackColor = $script:Theme.CardBg

$btnBar.Controls.Add($btnBarLeft, 0, 0)
$btnBar.Controls.Add($btnBarRight, 1, 0)

$btnAddRow = New-Object System.Windows.Forms.Button
$btnAddRow.Text = "+"
Set-UiButtonStyle -Button $btnAddRow -Width 44 -Height 50
Set-ModernButton -Button $btnAddRow

$btnPdf = New-Object System.Windows.Forms.Button
$btnPdf.Text = "PDF import"
Set-UiButtonStyle -Button $btnPdf -Width 120 -Height 50
Set-ModernButton -Button $btnPdf -BackColor $script:Theme.Accent -HoverColor $script:Theme.AccentHover -PressedColor $script:Theme.AccentPressed -BorderColor $script:Theme.Accent -TextColor $script:Theme.AccentText

$btnXtf = New-Object System.Windows.Forms.Button
$btnXtf.Text = "XTF import"
Set-UiButtonStyle -Button $btnXtf -Width 120 -Height 50
Set-ModernButton -Button $btnXtf -BackColor $script:Theme.Accent -HoverColor $script:Theme.AccentHover -PressedColor $script:Theme.AccentPressed -BorderColor $script:Theme.Accent -TextColor $script:Theme.AccentText

$btnExport = New-Object System.Windows.Forms.Button
$btnExport.Text = "Export Excel"
Set-UiButtonStyle -Button $btnExport -Width 130 -Height 50
Set-ModernButton -Button $btnExport -BackColor $script:Theme.Accent -HoverColor $script:Theme.AccentHover -PressedColor $script:Theme.AccentPressed -BorderColor $script:Theme.Accent -TextColor $script:Theme.AccentText

$btnBarLeft.Controls.Add($btnAddRow)
$btnBarLeft.Controls.Add($btnPdf)
$btnBarLeft.Controls.Add($btnXtf)
$btnBarLeft.Controls.Add($btnExport)

$metaPanel = New-Object System.Windows.Forms.Panel
$metaPanel.Dock = [System.Windows.Forms.DockStyle]::Top
$metaPanel.Height = 90
$metaPanel.BackColor = $script:Theme.CardBg
$metaPanel.Padding = New-Object System.Windows.Forms.Padding(10, 8, 10, 8)

$metaContainer = New-Object System.Windows.Forms.TableLayoutPanel
$metaContainer.Dock = [System.Windows.Forms.DockStyle]::Fill
$metaContainer.ColumnCount = 2
$metaContainer.RowCount = 1
$metaContainer.ColumnStyles.Add((New-Object System.Windows.Forms.ColumnStyle([System.Windows.Forms.SizeType]::Absolute, 180)))
$metaContainer.ColumnStyles.Add((New-Object System.Windows.Forms.ColumnStyle([System.Windows.Forms.SizeType]::Percent, 100)))

$logoPanel = New-Object System.Windows.Forms.TableLayoutPanel
$logoPanel.Dock = [System.Windows.Forms.DockStyle]::Fill
$logoPanel.RowCount = 3
$logoPanel.ColumnCount = 1
$logoPanel.RowStyles.Add((New-Object System.Windows.Forms.RowStyle([System.Windows.Forms.SizeType]::Absolute, 16)))
$logoPanel.RowStyles.Add((New-Object System.Windows.Forms.RowStyle([System.Windows.Forms.SizeType]::Absolute, 52)))
$logoPanel.RowStyles.Add((New-Object System.Windows.Forms.RowStyle([System.Windows.Forms.SizeType]::Absolute, 22)))

$lblLogo = New-Object System.Windows.Forms.Label
$lblLogo.Text = "Logo"
$lblLogo.AutoSize = $true
$lblLogo.Font = $script:UiFontBold

$logoBox = New-Object System.Windows.Forms.PictureBox
$logoBox.BorderStyle = [System.Windows.Forms.BorderStyle]::FixedSingle
$logoBox.SizeMode = [System.Windows.Forms.PictureBoxSizeMode]::Zoom
$logoBox.Dock = [System.Windows.Forms.DockStyle]::Fill
$logoBox.BackColor = [System.Drawing.Color]::White
$script:LogoBox = $logoBox

$btnLogo = New-Object System.Windows.Forms.Button
$btnLogo.Text = "Logo..."
Set-UiButtonStyle -Button $btnLogo -Width 80 -Height 28
Set-ModernButton -Button $btnLogo

$logoPanel.Controls.Add($lblLogo, 0, 0)
$logoPanel.Controls.Add($logoBox, 0, 1)
$logoPanel.Controls.Add($btnLogo, 0, 2)

$metaFields = New-Object System.Windows.Forms.TableLayoutPanel
$metaFields.Dock = [System.Windows.Forms.DockStyle]::Fill
$metaFields.RowCount = 1
$metaFields.ColumnCount = 4
$metaFields.ColumnStyles.Add((New-Object System.Windows.Forms.ColumnStyle([System.Windows.Forms.SizeType]::Absolute, 120)))
$metaFields.ColumnStyles.Add((New-Object System.Windows.Forms.ColumnStyle([System.Windows.Forms.SizeType]::Absolute, 260)))
$metaFields.ColumnStyles.Add((New-Object System.Windows.Forms.ColumnStyle([System.Windows.Forms.SizeType]::Absolute, 60)))
$metaFields.ColumnStyles.Add((New-Object System.Windows.Forms.ColumnStyle([System.Windows.Forms.SizeType]::Absolute, 180)))

$lblAuswertung = New-Object System.Windows.Forms.Label
$lblAuswertung.Text = "Projektname"
$lblAuswertung.AutoSize = $true
$lblAuswertung.Anchor = [System.Windows.Forms.AnchorStyles]::Left

$txtAuswertung = New-Object System.Windows.Forms.TextBox
$txtAuswertung.Dock = [System.Windows.Forms.DockStyle]::Fill
$txtAuswertung.Font = $script:UiFont

$lblZone = New-Object System.Windows.Forms.Label
$lblZone.Text = "Zone"
$lblZone.AutoSize = $true
$lblZone.Anchor = [System.Windows.Forms.AnchorStyles]::Left

$txtZone = New-Object System.Windows.Forms.TextBox
$txtZone.Dock = [System.Windows.Forms.DockStyle]::Fill
$txtZone.Font = $script:UiFont
$script:MetaNameBox = $txtAuswertung
$script:MetaZoneBox = $txtZone

$metaFields.Controls.Add($lblAuswertung, 0, 0)
$metaFields.Controls.Add($txtAuswertung, 1, 0)
$metaFields.Controls.Add($lblZone, 2, 0)
$metaFields.Controls.Add($txtZone, 3, 0)

$metaContainer.Controls.Add($logoPanel, 0, 0)
$metaContainer.Controls.Add($metaFields, 1, 0)
$metaPanel.Controls.Add($metaContainer)

$projectPanel = New-Object System.Windows.Forms.Panel
$projectPanel.Dock = [System.Windows.Forms.DockStyle]::Top
$projectPanel.Height = 40
$projectPanel.BackColor = $script:Theme.CardBg
$projectPanel.Padding = New-Object System.Windows.Forms.Padding(10, 5, 10, 5)

$projectTable = New-Object System.Windows.Forms.TableLayoutPanel
$projectTable.Dock = [System.Windows.Forms.DockStyle]::Fill
$projectTable.ColumnCount = 3
$projectTable.RowCount = 1
$projectTable.ColumnStyles.Add((New-Object System.Windows.Forms.ColumnStyle([System.Windows.Forms.SizeType]::AutoSize)))
$projectTable.ColumnStyles.Add((New-Object System.Windows.Forms.ColumnStyle([System.Windows.Forms.SizeType]::Percent, 100)))
$projectTable.ColumnStyles.Add((New-Object System.Windows.Forms.ColumnStyle([System.Windows.Forms.SizeType]::AutoSize)))

$lblProject = New-Object System.Windows.Forms.Label
$lblProject.Text = "Projektseite"
$lblProject.AutoSize = $true
$lblProject.Anchor = [System.Windows.Forms.AnchorStyles]::Left
$lblProject.Font = $script:UiFontBold

$lblProjectPath = New-Object System.Windows.Forms.Label
$lblProjectPath.Text = "Projektseite: (kein)"
$lblProjectPath.AutoSize = $true
$lblProjectPath.Anchor = [System.Windows.Forms.AnchorStyles]::Left
$lblProjectPath.ForeColor = $script:Theme.MutedText
$script:ProjectLabel = $lblProjectPath

$projectButtons = New-Object System.Windows.Forms.FlowLayoutPanel
$projectButtons.Dock = [System.Windows.Forms.DockStyle]::Fill
$projectButtons.AutoSize = $true
$projectButtons.AutoSizeMode = [System.Windows.Forms.AutoSizeMode]::GrowAndShrink
$projectButtons.WrapContents = $false
$projectButtons.FlowDirection = [System.Windows.Forms.FlowDirection]::LeftToRight

$btnProjectNew = New-Object System.Windows.Forms.Button
$btnProjectNew.Text = "Projektseite"
$btnProjectNew.Size = New-Object System.Drawing.Size(100, 28)
Set-UiButtonStyle -Button $btnProjectNew -Width 150 -Height 34
Set-ModernButton -Button $btnProjectNew

$btnProjectOpen = New-Object System.Windows.Forms.Button
$btnProjectOpen.Text = "Projektseite laden"
$btnProjectOpen.Size = New-Object System.Drawing.Size(110, 28)
Set-UiButtonStyle -Button $btnProjectOpen -Width 150 -Height 34
Set-ModernButton -Button $btnProjectOpen -BackColor $script:Theme.Accent -HoverColor $script:Theme.AccentHover -PressedColor $script:Theme.AccentPressed -BorderColor $script:Theme.Accent -TextColor $script:Theme.AccentText

$btnProjectSaveAs = New-Object System.Windows.Forms.Button
$btnProjectSaveAs.Text = "Projekt speichern unter"
$btnProjectSaveAs.Size = New-Object System.Drawing.Size(110, 28)
Set-UiButtonStyle -Button $btnProjectSaveAs -Width 150 -Height 34
Set-ModernButton -Button $btnProjectSaveAs -BackColor $script:Theme.Accent -HoverColor $script:Theme.AccentHover -PressedColor $script:Theme.AccentPressed -BorderColor $script:Theme.Accent -TextColor $script:Theme.AccentText

$projectButtons.Controls.Add($btnProjectNew)
$projectButtons.Controls.Add($btnProjectOpen)
$projectButtons.Controls.Add($btnProjectSaveAs)

$projectTable.Controls.Add($lblProject, 0, 0)
$projectTable.Controls.Add($lblProjectPath, 1, 0)
$projectPanel.Controls.Add($projectTable)

$btnBarRight.Controls.Add($btnProjectOpen)
$btnBarRight.Controls.Add($btnProjectSaveAs)
$btnBarRight.Controls.Add($btnProjectNew)

$grid = New-Object System.Windows.Forms.DataGridView
$grid.Dock = [System.Windows.Forms.DockStyle]::Fill
$grid.AutoSizeColumnsMode = [System.Windows.Forms.DataGridViewAutoSizeColumnsMode]::AllCells
$grid.AllowUserToAddRows = $false
$grid.AllowUserToDeleteRows = $false
$grid.DefaultCellStyle.Font = $script:UiFont
$grid.SelectionMode = [System.Windows.Forms.DataGridViewSelectionMode]::FullRowSelect
$grid.MultiSelect = $false
$grid.EditMode = [System.Windows.Forms.DataGridViewEditMode]::EditOnEnter
$grid.BackgroundColor = $script:Theme.CardBg
$grid.GridColor = $script:Theme.Border
$grid.BorderStyle = [System.Windows.Forms.BorderStyle]::None
$grid.RowHeadersVisible = $false
$grid.AllowDrop = $true

$script:Grid = $grid
Set-GridTheme -Grid $grid

$detailsPanel = New-Object System.Windows.Forms.Panel
$detailsPanel.Dock = [System.Windows.Forms.DockStyle]::Top
$detailsPanel.Height = 200
$detailsPanel.Padding = New-Object System.Windows.Forms.Padding(10)
$detailsPanel.BackColor = $script:Theme.CardBg
$detailsPanel.Visible = $true
$script:DetailsPanel = $detailsPanel

$detailsHeader = New-Object System.Windows.Forms.Label
$detailsHeader.Text = "Datensatz"
$detailsHeader.Font = $script:UiFontBold
$detailsHeader.AutoSize = $true
$detailsHeader.Dock = [System.Windows.Forms.DockStyle]::Top

$detailsButtons = New-Object System.Windows.Forms.FlowLayoutPanel
$detailsButtons.Dock = [System.Windows.Forms.DockStyle]::Top
$detailsButtons.Height = 35
$detailsButtons.WrapContents = $false
$detailsButtons.FlowDirection = [System.Windows.Forms.FlowDirection]::LeftToRight

$btnNewRow = New-Object System.Windows.Forms.Button
$btnNewRow.Text = "+"
$btnNewRow.Size = New-Object System.Drawing.Size(44, 34)
Set-UiButtonStyle -Button $btnNewRow -Small -Height 34
Set-ModernButton -Button $btnNewRow

$detailsButtons.Controls.Add($btnNewRow)

$detailsTable = New-Object System.Windows.Forms.TableLayoutPanel
$detailsTable.Dock = [System.Windows.Forms.DockStyle]::Fill
$detailsTable.ColumnCount = 28
$detailsTable.RowCount = 1
$detailsTable.AutoScroll = $true

for ($i = 0; $i -lt 28; $i++) {
    $detailsTable.ColumnStyles.Add((New-Object System.Windows.Forms.ColumnStyle([System.Windows.Forms.SizeType]::Absolute, 120)))
}

$detailsTable.RowStyles.Add((New-Object System.Windows.Forms.RowStyle([System.Windows.Forms.SizeType]::Absolute, 22)))

Add-FormField -Table $detailsTable -Row 0 -Col 0 -LabelText "NR" -PropertyName "NR" -ReadOnly
Add-FormField -Table $detailsTable -Row 0 -Col 1 -LabelText "Halts" -PropertyName "Haltungsname"
Add-FormField -Table $detailsTable -Row 0 -Col 2 -LabelText "Strasse" -PropertyName "Strasse"
Add-FormField -Table $detailsTable -Row 0 -Col 3 -LabelText "Material" -PropertyName "Rohrmaterial"
Add-FormField -Table $detailsTable -Row 0 -Col 4 -LabelText "DN" -PropertyName "DN_mm"
Add-FormField -Table $detailsTable -Row 0 -Col 5 -LabelText "Laenge" -PropertyName "Haltungslaenge_m"
Add-FormField -Table $detailsTable -Row 0 -Col 6 -LabelText "Richtung" -PropertyName "Fliessrichtung"
Add-FormField -Table $detailsTable -Row 0 -Col 7 -LabelText "Schaeden" -PropertyName "Primaere_Schaeden"
Add-FormField -Table $detailsTable -Row 0 -Col 8 -LabelText "Zustand" -PropertyName "Zustandsklasse"
Add-FormField -Table $detailsTable -Row 0 -Col 9 -LabelText "Sanieren" -PropertyName "Sanieren_Ja_Nein"
Add-FormField -Table $detailsTable -Row 0 -Col 10 -LabelText "Massn." -PropertyName "Empfohlene_Sanierungsmassnahmen" -Multiline
Add-FormField -Table $detailsTable -Row 0 -Col 11 -LabelText "Eigent." -PropertyName "Eigentuemer"
Add-FormField -Table $detailsTable -Row 0 -Col 12 -LabelText "Kosten" -PropertyName "Kosten"
Add-FormField -Table $detailsTable -Row 0 -Col 13 -LabelText "Bem." -PropertyName "Bemerkungen" -Multiline
Add-FormField -Table $detailsTable -Row 0 -Col 14 -LabelText "Link" -PropertyName "Link"
Add-FormField -Table $detailsTable -Row 0 -Col 15 -LabelText "Ausfuehrung" -PropertyName "Ausfuehrung_durch"
Add-FormField -Table $detailsTable -Row 0 -Col 16 -LabelText "Nutzungsart" -PropertyName "Nutzungsart"
Add-FormField -Table $detailsTable -Row 0 -Col 17 -LabelText "Pruef.Res" -PropertyName "Pruefungsresultat"
Add-FormField -Table $detailsTable -Row 0 -Col 18 -LabelText "Ren.Stk" -PropertyName "Renovierung_Inliner_Stk"
Add-FormField -Table $detailsTable -Row 0 -Col 19 -LabelText "Ren.m" -PropertyName "Renovierung_Inliner_m"
Add-FormField -Table $detailsTable -Row 0 -Col 20 -LabelText "Anschl." -PropertyName "Anschluesse_verpressen"
Add-FormField -Table $detailsTable -Row 0 -Col 21 -LabelText "Rep.Man" -PropertyName "Reparatur_Manschette"
Add-FormField -Table $detailsTable -Row 0 -Col 22 -LabelText "Rep.Kurz" -PropertyName "Reparatur_Kurzliner"
Add-FormField -Table $detailsTable -Row 0 -Col 23 -LabelText "Erneu.m" -PropertyName "Erneuerung_Neubau_m"
Add-FormField -Table $detailsTable -Row 0 -Col 24 -LabelText "Offen" -PropertyName "Offen_abgeschlossen"
Add-FormField -Table $detailsTable -Row 0 -Col 25 -LabelText "Datum" -PropertyName "Datum_Jahr"

$detailsPanel.Controls.Add($detailsTable)
$detailsPanel.Controls.Add($detailsButtons)
$detailsPanel.Controls.Add($detailsHeader)

$mainPanel.Controls.Add($grid)
$mainPanel.Controls.Add($detailsPanel)
$mainPanel.Controls.Add($projectPanel)
$mainPanel.Controls.Add($metaPanel)
$mainPanel.Controls.Add($btnPanel)


# Menueleiste (inkl. "+": neue Zeile)
$menu = New-Object System.Windows.Forms.MenuStrip
$menu.BackColor = $script:Theme.CardBg
$menu.ForeColor = $script:Theme.Text
$menu.RenderMode = [System.Windows.Forms.ToolStripRenderMode]::System
$miFile = New-Object System.Windows.Forms.ToolStripMenuItem("Datei")
$miAdd = New-Object System.Windows.Forms.ToolStripMenuItem("+  Neue Zeile")
$miAdd.ShortcutKeys = [System.Windows.Forms.Keys]::Control -bor [System.Windows.Forms.Keys]::N
$miAdd.Add_Click({
    Add-EmptyRowAndSelect -ForceNewRow
})
$miFile.DropDownItems.Add($miAdd) | Out-Null
$miExit = New-Object System.Windows.Forms.ToolStripMenuItem("Beenden")
$miExit.Add_Click({ $form.Close() })
$miFile.DropDownItems.Add($miExit) | Out-Null
$menu.Items.Add($miFile) | Out-Null
$menu.Dock = [System.Windows.Forms.DockStyle]::Top
$form.MainMenuStrip = $menu

# Controls in korrekter Reihenfolge hinzufügen: zuerst Fill, dann Top (umgekehrte Z-Order)
$form.Controls.Add($mainPanel)
$form.Controls.Add($menu)

$statusStrip = New-Object System.Windows.Forms.StatusStrip
$statusStrip.Dock = [System.Windows.Forms.DockStyle]::Bottom
$statusStrip.SizingGrip = $false
$statusStrip.BackColor = $script:Theme.CardBg
$statusStrip.ForeColor = $script:Theme.MutedText
$lblStatus = New-Object System.Windows.Forms.ToolStripStatusLabel
$lblStatus.Text = "Bereit"
$lblStatus.ForeColor = $script:Theme.MutedText
$statusFiller = New-Object System.Windows.Forms.ToolStripStatusLabel
$statusFiller.Spring = $true
$script:StatusRightLabel = New-Object System.Windows.Forms.ToolStripStatusLabel
$script:StatusRightLabel.Text = "Projekt: (kein) | Zone: - | Datensaetze: 0"
$script:StatusRightLabel.ForeColor = $script:Theme.MutedText
$statusStrip.Items.AddRange(@($lblStatus, $statusFiller, $script:StatusRightLabel))
$form.Controls.Add($statusStrip)

$startPanel = New-Object System.Windows.Forms.Panel
$startPanel.Dock = [System.Windows.Forms.DockStyle]::Fill
$startPanel.BackColor = $script:Theme.AppBg
$startPanel.Padding = New-Object System.Windows.Forms.Padding(20)
$script:StartPanel = $startPanel

$startTitlePanel = New-Object System.Windows.Forms.Panel
$startTitlePanel.Dock = [System.Windows.Forms.DockStyle]::Top
$startTitlePanel.Height = 50
$startTitlePanel.BackColor = $script:Theme.AppBg

$startTitle = New-Object System.Windows.Forms.Label
$startTitle.Text = "Projekt"
$startTitle.Font = $script:TitleFont
$startTitle.AutoSize = $true
$startTitle.Location = New-Object System.Drawing.Point(0, 5)
$startTitlePanel.Controls.Add($startTitle)

$startInfoPanel = New-Object System.Windows.Forms.Panel
$startInfoPanel.Dock = [System.Windows.Forms.DockStyle]::Top
$startInfoPanel.Height = 150
$startInfoPanel.Padding = New-Object System.Windows.Forms.Padding(0, 10, 0, 10)
$startInfoPanel.BackColor = $script:Theme.AppBg

$startInfoTable = New-Object System.Windows.Forms.TableLayoutPanel
$startInfoTable.Dock = [System.Windows.Forms.DockStyle]::Fill
$startInfoTable.ColumnCount = 2
$startInfoTable.RowCount = 1
$startInfoTable.ColumnStyles.Add((New-Object System.Windows.Forms.ColumnStyle([System.Windows.Forms.SizeType]::Absolute, 180)))
$startInfoTable.ColumnStyles.Add((New-Object System.Windows.Forms.ColumnStyle([System.Windows.Forms.SizeType]::Percent, 100)))

$startLogoPanel = New-Object System.Windows.Forms.TableLayoutPanel
$startLogoPanel.Dock = [System.Windows.Forms.DockStyle]::Fill
$startLogoPanel.RowCount = 3
$startLogoPanel.ColumnCount = 1
$startLogoPanel.RowStyles.Add((New-Object System.Windows.Forms.RowStyle([System.Windows.Forms.SizeType]::Absolute, 16)))
$startLogoPanel.RowStyles.Add((New-Object System.Windows.Forms.RowStyle([System.Windows.Forms.SizeType]::Absolute, 90)))
$startLogoPanel.RowStyles.Add((New-Object System.Windows.Forms.RowStyle([System.Windows.Forms.SizeType]::Absolute, 44)))

$lblStartLogo = New-Object System.Windows.Forms.Label
$lblStartLogo.Text = "Logo Import"
$lblStartLogo.AutoSize = $true
$lblStartLogo.Font = $script:UiFontBold

$startLogoBox = New-Object System.Windows.Forms.PictureBox
$startLogoBox.BorderStyle = [System.Windows.Forms.BorderStyle]::FixedSingle
$startLogoBox.SizeMode = [System.Windows.Forms.PictureBoxSizeMode]::Zoom
$startLogoBox.Dock = [System.Windows.Forms.DockStyle]::Fill
$startLogoBox.BackColor = [System.Drawing.Color]::White
$script:StartLogoBox = $startLogoBox

$btnStartLogo = New-Object System.Windows.Forms.Button
$btnStartLogo.Text = "Logo..."
Set-UiButtonStyle -Button $btnStartLogo -Width 80 -Height 42
Set-ModernButton -Button $btnStartLogo

$startLogoPanel.Controls.Add($lblStartLogo, 0, 0)
$startLogoPanel.Controls.Add($startLogoBox, 0, 1)
$startLogoPanel.Controls.Add($btnStartLogo, 0, 2)

$companyTable = New-Object System.Windows.Forms.TableLayoutPanel
$companyTable.Dock = [System.Windows.Forms.DockStyle]::Fill
$companyTable.ColumnCount = 2
$companyTable.RowCount = 4
$companyTable.ColumnStyles.Add((New-Object System.Windows.Forms.ColumnStyle([System.Windows.Forms.SizeType]::Absolute, 120)))
$companyTable.ColumnStyles.Add((New-Object System.Windows.Forms.ColumnStyle([System.Windows.Forms.SizeType]::Percent, 100)))
$companyTable.RowStyles.Add((New-Object System.Windows.Forms.RowStyle([System.Windows.Forms.SizeType]::Absolute, 26)))
$companyTable.RowStyles.Add((New-Object System.Windows.Forms.RowStyle([System.Windows.Forms.SizeType]::Absolute, 26)))
$companyTable.RowStyles.Add((New-Object System.Windows.Forms.RowStyle([System.Windows.Forms.SizeType]::Absolute, 26)))
$companyTable.RowStyles.Add((New-Object System.Windows.Forms.RowStyle([System.Windows.Forms.SizeType]::Absolute, 26)))

$lblFirmaName = New-Object System.Windows.Forms.Label
$lblFirmaName.Text = "Firmen Name"
$lblFirmaName.AutoSize = $true
$lblFirmaName.Anchor = [System.Windows.Forms.AnchorStyles]::Left

$txtFirmaName = New-Object System.Windows.Forms.TextBox
$txtFirmaName.Dock = [System.Windows.Forms.DockStyle]::Fill
$txtFirmaName.Font = $script:UiFont
$script:StartFirmaNameBox = $txtFirmaName

$lblFirmaAdresse = New-Object System.Windows.Forms.Label
$lblFirmaAdresse.Text = "Adresse"
$lblFirmaAdresse.AutoSize = $true
$lblFirmaAdresse.Anchor = [System.Windows.Forms.AnchorStyles]::Left

$txtFirmaAdresse = New-Object System.Windows.Forms.TextBox
$txtFirmaAdresse.Dock = [System.Windows.Forms.DockStyle]::Fill
$txtFirmaAdresse.Font = $script:UiFont
$script:StartFirmaAdresseBox = $txtFirmaAdresse

$lblFirmaTelefon = New-Object System.Windows.Forms.Label
$lblFirmaTelefon.Text = "Telefonnummer"
$lblFirmaTelefon.AutoSize = $true
$lblFirmaTelefon.Anchor = [System.Windows.Forms.AnchorStyles]::Left

$txtFirmaTelefon = New-Object System.Windows.Forms.TextBox
$txtFirmaTelefon.Dock = [System.Windows.Forms.DockStyle]::Fill
$txtFirmaTelefon.Font = $script:UiFont
$script:StartFirmaTelefonBox = $txtFirmaTelefon

$lblFirmaEmail = New-Object System.Windows.Forms.Label
$lblFirmaEmail.Text = "E-Mail"
$lblFirmaEmail.AutoSize = $true
$lblFirmaEmail.Anchor = [System.Windows.Forms.AnchorStyles]::Left

$txtFirmaEmail = New-Object System.Windows.Forms.TextBox
$txtFirmaEmail.Dock = [System.Windows.Forms.DockStyle]::Fill
$txtFirmaEmail.Font = $script:UiFont
$script:StartFirmaEmailBox = $txtFirmaEmail

$companyTable.Controls.Add($lblFirmaName, 0, 0)
$companyTable.Controls.Add($txtFirmaName, 1, 0)
$companyTable.Controls.Add($lblFirmaAdresse, 0, 1)
$companyTable.Controls.Add($txtFirmaAdresse, 1, 1)
$companyTable.Controls.Add($lblFirmaTelefon, 0, 2)
$companyTable.Controls.Add($txtFirmaTelefon, 1, 2)
$companyTable.Controls.Add($lblFirmaEmail, 0, 3)
$companyTable.Controls.Add($txtFirmaEmail, 1, 3)

$startInfoTable.Controls.Add($startLogoPanel, 0, 0)
$startInfoTable.Controls.Add($companyTable, 1, 0)
$startInfoPanel.Controls.Add($startInfoTable)

$startProjectPanel = New-Object System.Windows.Forms.Panel
$startProjectPanel.Dock = [System.Windows.Forms.DockStyle]::Fill
$startProjectPanel.Padding = New-Object System.Windows.Forms.Padding(0, 10, 0, 0)
$startProjectPanel.BackColor = $script:Theme.AppBg

$startProjectTable = New-Object System.Windows.Forms.TableLayoutPanel
$startProjectTable.Dock = [System.Windows.Forms.DockStyle]::Fill
$startProjectTable.ColumnCount = 2
$startProjectTable.RowCount = 1
$startProjectTable.ColumnStyles.Add((New-Object System.Windows.Forms.ColumnStyle([System.Windows.Forms.SizeType]::Percent, 55)))
$startProjectTable.ColumnStyles.Add((New-Object System.Windows.Forms.ColumnStyle([System.Windows.Forms.SizeType]::Percent, 45)))

$startLeftTable = New-Object System.Windows.Forms.TableLayoutPanel
$startLeftTable.Dock = [System.Windows.Forms.DockStyle]::Fill
$startLeftTable.ColumnCount = 2
$startLeftTable.RowCount = 5
$startLeftTable.ColumnStyles.Add((New-Object System.Windows.Forms.ColumnStyle([System.Windows.Forms.SizeType]::Absolute, 120)))
$startLeftTable.ColumnStyles.Add((New-Object System.Windows.Forms.ColumnStyle([System.Windows.Forms.SizeType]::Percent, 100)))
$startLeftTable.RowStyles.Add((New-Object System.Windows.Forms.RowStyle([System.Windows.Forms.SizeType]::Absolute, 56)))
$startLeftTable.RowStyles.Add((New-Object System.Windows.Forms.RowStyle([System.Windows.Forms.SizeType]::Absolute, 28)))
$startLeftTable.RowStyles.Add((New-Object System.Windows.Forms.RowStyle([System.Windows.Forms.SizeType]::Absolute, 28)))
$startLeftTable.RowStyles.Add((New-Object System.Windows.Forms.RowStyle([System.Windows.Forms.SizeType]::Absolute, 56)))
$startLeftTable.RowStyles.Add((New-Object System.Windows.Forms.RowStyle([System.Windows.Forms.SizeType]::Percent, 100)))

$btnStartNew = New-Object System.Windows.Forms.Button
$btnStartNew.Text = "Projektseite erstellen"
$btnStartNew.Size = New-Object System.Drawing.Size(200, 52)
Set-UiButtonStyle -Button $btnStartNew -Width 200 -Height 54
Set-ModernButton -Button $btnStartNew -BackColor $script:Theme.Accent -HoverColor $script:Theme.AccentHover -PressedColor $script:Theme.AccentPressed -BorderColor $script:Theme.Accent -TextColor $script:Theme.AccentText

$lblStartProjectName = New-Object System.Windows.Forms.Label
$lblStartProjectName.Text = "Projektname"
$lblStartProjectName.AutoSize = $true
$lblStartProjectName.Anchor = [System.Windows.Forms.AnchorStyles]::Left

$txtStartProjectName = New-Object System.Windows.Forms.TextBox
$txtStartProjectName.Dock = [System.Windows.Forms.DockStyle]::Fill
$txtStartProjectName.Font = $script:UiFont
$script:StartProjectNameBox = $txtStartProjectName

$lblStartZone = New-Object System.Windows.Forms.Label
$lblStartZone.Text = "Zone"
$lblStartZone.AutoSize = $true
$lblStartZone.Anchor = [System.Windows.Forms.AnchorStyles]::Left

$txtStartZone = New-Object System.Windows.Forms.TextBox
$txtStartZone.Dock = [System.Windows.Forms.DockStyle]::Fill
$txtStartZone.Font = $script:UiFont
$script:StartZoneBox = $txtStartZone

$startImportPanel = New-Object System.Windows.Forms.FlowLayoutPanel
$startImportPanel.Dock = [System.Windows.Forms.DockStyle]::Fill
$startImportPanel.FlowDirection = [System.Windows.Forms.FlowDirection]::LeftToRight
$startImportPanel.WrapContents = $false
$startImportPanel.Visible = $false

$startLeftTable.Controls.Add($btnStartNew, 0, 0)
$startLeftTable.SetColumnSpan($btnStartNew, 2)
$startLeftTable.Controls.Add($lblStartProjectName, 0, 1)
$startLeftTable.Controls.Add($txtStartProjectName, 1, 1)
$startLeftTable.Controls.Add($lblStartZone, 0, 2)
$startLeftTable.Controls.Add($txtStartZone, 1, 2)

$startRightTable = New-Object System.Windows.Forms.TableLayoutPanel
$startRightTable.Dock = [System.Windows.Forms.DockStyle]::Fill
$startRightTable.ColumnCount = 1
$startRightTable.RowCount = 4
$startRightTable.RowStyles.Add((New-Object System.Windows.Forms.RowStyle([System.Windows.Forms.SizeType]::Absolute, 20)))
$startRightTable.RowStyles.Add((New-Object System.Windows.Forms.RowStyle([System.Windows.Forms.SizeType]::Absolute, 110)))
$startRightTable.RowStyles.Add((New-Object System.Windows.Forms.RowStyle([System.Windows.Forms.SizeType]::Absolute, 52)))
$startRightTable.RowStyles.Add((New-Object System.Windows.Forms.RowStyle([System.Windows.Forms.SizeType]::Percent, 100)))

$lblProjectList = New-Object System.Windows.Forms.Label
$lblProjectList.Text = "Projektliste"
$lblProjectList.AutoSize = $true
$lblProjectList.Font = $script:UiFontBold

$projectList = New-Object System.Windows.Forms.ListBox
$projectList.Dock = [System.Windows.Forms.DockStyle]::Fill
$projectList.IntegralHeight = $false
$projectList.DisplayMember = "Name"
$script:ProjectListBox = $projectList

$btnStartLoad = New-Object System.Windows.Forms.Button
$btnStartLoad.Text = "Projektseite laden"
$btnStartLoad.Size = New-Object System.Drawing.Size(120, 48)
Set-UiButtonStyle -Button $btnStartLoad -Width 150 -Height 54
Set-ModernButton -Button $btnStartLoad

$startRightTable.Controls.Add($lblProjectList, 0, 0)
$startRightTable.Controls.Add($projectList, 0, 1)
$startRightTable.Controls.Add($btnStartLoad, 0, 2)

$startProjectTable.Controls.Add($startLeftTable, 0, 0)
$startProjectTable.Controls.Add($startRightTable, 1, 0)
$startProjectPanel.Controls.Add($startProjectTable)

$startPanel.Controls.Add($startProjectPanel)
$startPanel.Controls.Add($startInfoPanel)
$startPanel.Controls.Add($startTitlePanel)

$form.Controls.Add($startPanel)

$btnStartLogo.Add_Click({
    $dlg = New-Object System.Windows.Forms.OpenFileDialog
    $dlg.Filter = "Bilddateien (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp|Alle Dateien (*.*)|*.*"
    $dlg.InitialDirectory = "F:\AuswertungPro"
    if ($dlg.ShowDialog() -eq [System.Windows.Forms.DialogResult]::OK) {
        Set-LogoPath -Path $dlg.FileName
    }
})

$txtFirmaName.Add_TextChanged({ $script:ReportMeta.FirmaName = $txtFirmaName.Text })
$txtFirmaAdresse.Add_TextChanged({ $script:ReportMeta.FirmaAdresse = $txtFirmaAdresse.Text })
$txtFirmaTelefon.Add_TextChanged({ $script:ReportMeta.FirmaTelefon = $txtFirmaTelefon.Text })
$txtFirmaEmail.Add_TextChanged({ $script:ReportMeta.FirmaEmail = $txtFirmaEmail.Text })
$txtStartProjectName.Add_TextChanged({
    $script:ReportMeta.Auswertungsname = $txtStartProjectName.Text
    Update-StatusSummary
})
$txtStartZone.Add_TextChanged({
    $script:ReportMeta.Zone = $txtStartZone.Text
    Update-StatusSummary
})

$btnStartNew.Add_Click({
    Set-StartMeta
    if (Test-BlankValue $script:ReportMeta.Auswertungsname) {
        [System.Windows.Forms.MessageBox]::Show("Bitte zuerst den Projektname eingeben.", "Projektseite")
        return
    }
    Clear-ProjectData
    Show-MainScreen
    Add-EmptyRowAndSelect -ForceNewRow
})

$btnStartLoad.Add_Click({
    $item = $projectList.SelectedItem
    if ($null -eq $item) {
        [System.Windows.Forms.MessageBox]::Show("Bitte eine Projektseite auswaehlen.", "Projektseite")
        return
    }
    Import-Project -Path $item.FullName -AuswertungTextBox $txtAuswertung -ZoneTextBox $txtZone
    Show-MainScreen
    Add-EmptyRowAndSelect
})

$projectList.Add_DoubleClick({
    $item = $projectList.SelectedItem
    if ($null -eq $item) { return }
    Import-Project -Path $item.FullName -AuswertungTextBox $txtAuswertung -ZoneTextBox $txtZone
    Show-MainScreen
    Add-EmptyRowAndSelect
})

$txtAuswertung.Add_TextChanged({
    $script:ReportMeta.Auswertungsname = $txtAuswertung.Text
    Update-StatusSummary
})

$txtZone.Add_TextChanged({
    $script:ReportMeta.Zone = $txtZone.Text
    Update-StatusSummary
})

$btnLogo.Add_Click({
    $dlg = New-Object System.Windows.Forms.OpenFileDialog
    $dlg.Filter = "Bilddateien (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp|Alle Dateien (*.*)|*.*"
    $dlg.InitialDirectory = "F:\AuswertungPro"
    if ($dlg.ShowDialog() -eq [System.Windows.Forms.DialogResult]::OK) {
        Set-LogoPath -Path $dlg.FileName
    }
})

$btnProjectNew.Add_Click({
    $hasData = @($script:Data).Count -gt 0
    $hasMeta = -not (Test-BlankValue $script:ReportMeta.Auswertungsname) -or `
        -not (Test-BlankValue $script:ReportMeta.Zone) -or `
        -not (Test-BlankValue $script:ReportMeta.LogoPath) -or `
        -not (Test-BlankValue $script:ReportMeta.FirmaName) -or `
        -not (Test-BlankValue $script:ReportMeta.FirmaAdresse) -or `
        -not (Test-BlankValue $script:ReportMeta.FirmaTelefon) -or `
        -not (Test-BlankValue $script:ReportMeta.FirmaEmail)
    if ($hasData -or $hasMeta) {
        $result = [System.Windows.Forms.MessageBox]::Show("Neue Projektseite erstellen? Nicht gespeicherte Daten gehen verloren.", "Projektseite", [System.Windows.Forms.MessageBoxButtons]::YesNo)
        if ($result -ne [System.Windows.Forms.DialogResult]::Yes) { return }
    }
    $script:ReportMeta = New-ReportMeta
    if ($null -ne $script:MetaNameBox) { $script:MetaNameBox.Text = "" }
    if ($null -ne $script:MetaZoneBox) { $script:MetaZoneBox.Text = "" }
    if ($null -ne $script:StartProjectNameBox) { $script:StartProjectNameBox.Text = "" }
    if ($null -ne $script:StartZoneBox) { $script:StartZoneBox.Text = "" }
    if ($null -ne $script:StartFirmaNameBox) { $script:StartFirmaNameBox.Text = "" }
    if ($null -ne $script:StartFirmaAdresseBox) { $script:StartFirmaAdresseBox.Text = "" }
    if ($null -ne $script:StartFirmaTelefonBox) { $script:StartFirmaTelefonBox.Text = "" }
    if ($null -ne $script:StartFirmaEmailBox) { $script:StartFirmaEmailBox.Text = "" }
    Update-LogoBoxesFromPath -Path ""
    Clear-ProjectData
    Show-StartScreen
})

$btnProjectOpen.Add_Click({
    $dlg = New-Object System.Windows.Forms.OpenFileDialog
    $dlg.Filter = "Auswertung Projekt (*.ausproj)|*.ausproj|JSON (*.json)|*.json|Alle Dateien (*.*)|*.*"
    $dlg.InitialDirectory = "F:\AuswertungPro"
    if ($dlg.ShowDialog() -eq [System.Windows.Forms.DialogResult]::OK) {
        Import-Project -Path $dlg.FileName -AuswertungTextBox $txtAuswertung -ZoneTextBox $txtZone
    }
})

$btnProjectSaveAs.Add_Click({
    $dlg = New-Object System.Windows.Forms.SaveFileDialog
    $dlg.Filter = "Auswertung Projekt (*.ausproj)|*.ausproj|JSON (*.json)|*.json|Alle Dateien (*.*)|*.*"
    $dlg.InitialDirectory = "F:\AuswertungPro"
    $dlg.FileName = if ($script:ProjectPath) { [System.IO.Path]::GetFileName($script:ProjectPath) } else { "projekt.ausproj" }
    if ($dlg.ShowDialog() -eq [System.Windows.Forms.DialogResult]::OK) {
        Save-Project -Path $dlg.FileName -AuswertungTextBox $txtAuswertung -ZoneTextBox $txtZone
        $script:ProjectPath = $dlg.FileName
        Update-ProjectLabel
    }
})

Update-ProjectLabel

$grid.Add_CellContentClick({
    param($senderObj, $e)
    if ($e.RowIndex -lt 0) { return }
    $colName = $grid.Columns[$e.ColumnIndex].Name
    $rowIndex = $e.RowIndex

    if ($colName -eq "Import_PDF") {
        $dlg = New-Object System.Windows.Forms.OpenFileDialog
        $dlg.Filter = "PDF Dateien (*.pdf)|*.pdf|Alle Dateien (*.*)|*.*"
        $dlg.Title = "PDF importieren (fuer diese Zeile)"
        if ($dlg.ShowDialog() -ne [System.Windows.Forms.DialogResult]::OK) { return }

        $importRows = @()
        try {
            $importRows = ConvertFrom-PdfHaltungen -PdfPath $dlg.FileName
        } catch {
            [System.Windows.Forms.MessageBox]::Show("PDF konnte nicht importiert werden:`r`n$($_.Exception.Message)", "PDF Import", [System.Windows.Forms.MessageBoxButtons]::OK, [System.Windows.Forms.MessageBoxIcon]::Error) | Out-Null
            return
        }

        if ($null -eq $importRows -or $importRows.Count -eq 0) {
            [System.Windows.Forms.MessageBox]::Show("Im PDF wurden keine Haltungen erkannt.", "PDF Import", [System.Windows.Forms.MessageBoxButtons]::OK, [System.Windows.Forms.MessageBoxIcon]::Information) | Out-Null
            return
        }

        Merge-ImportedRowsIntoData -RowIndex $rowIndex -ImportRows $importRows
        return
    }
})

$grid.Add_SelectionChanged({
    if ($grid.SelectedRows.Count -gt 0) {
        $row = $grid.SelectedRows[0].DataBoundItem
        if ($null -ne $row) { Set-RowIntoForm -Row $row }
    }
})

$grid.Add_CellValidating({
    param($senderObj, $e)
    if ($e.RowIndex -lt 0) { return }
    $colName = $grid.Columns[$e.ColumnIndex].Name
    if ($colName -notin @("DN_mm", "Haltungslaenge_m", "Kosten")) { return }
    $value = [string]$e.FormattedValue
    if (Test-NumericValue -Value $value) { return }
    [System.Windows.Forms.MessageBox]::Show("Bitte nur Zahlen eingeben.", "Eingabepruefung") | Out-Null
    $e.Cancel = $true
})

$grid.Add_DataError({
    param($senderObj, $e)
    if ($e.RowIndex -ge 0 -and $e.ColumnIndex -ge 0) {
        $col = $grid.Columns[$e.ColumnIndex]
        if ($col -is [System.Windows.Forms.DataGridViewComboBoxColumn]) {
            $cell = $grid.Rows[$e.RowIndex].Cells[$e.ColumnIndex]
            $value = [string]$cell.EditedFormattedValue
            if (-not [string]::IsNullOrWhiteSpace($value) -and -not $col.Items.Contains($value)) {
                [void]$col.Items.Add($value)
                $cell.Value = $value
            }
        }
    }
    $e.ThrowException = $false
})

$grid.Add_EditingControlShowing({
    param($senderObj, $e)
    if ($null -eq $grid.CurrentCell) { return }
    $colName = $grid.Columns[$grid.CurrentCell.ColumnIndex].Name
    $tb = $e.Control -as [System.Windows.Forms.TextBox]
    if ($null -ne $tb) {
        if ($colName -in @("Empfohlene_Sanierungsmassnahmen", "Bemerkungen")) {
            $tb.Multiline = $true
            $tb.AcceptsReturn = $true
            $tb.ScrollBars = [System.Windows.Forms.ScrollBars]::Vertical
        } else {
            $tb.Multiline = $false
            $tb.AcceptsReturn = $false
            $tb.ScrollBars = [System.Windows.Forms.ScrollBars]::None
        }
    }
    $combo = $e.Control -as [System.Windows.Forms.ComboBox]
    if ($null -ne $combo) {
        if ($colName -in @("Rohrmaterial", "Fliessrichtung", "Zustandsklasse")) {
            $combo.DropDownStyle = [System.Windows.Forms.ComboBoxStyle]::DropDown
        } else {
            $combo.DropDownStyle = [System.Windows.Forms.ComboBoxStyle]::DropDownList
        }
    }
})

$btnAddRow.Add_Click({
    Add-EmptyRowAndSelect -ForceNewRow
})

$btnNewRow.Add_Click({
    Add-EmptyRowAndSelect -ForceNewRow
})

$btnPdf.Add_Click({
    $folderDialog = New-Object System.Windows.Forms.FolderBrowserDialog
    $folderDialog.Description = "PDF Ordner waehlen"
    $folderDialog.SelectedPath = "F:\AuswertungPro"

    if ($folderDialog.ShowDialog() -eq [System.Windows.Forms.DialogResult]::OK) {
        $pdfs = Get-PdfFilesFromPaths -Paths @($folderDialog.SelectedPath)
        Import-PdfFiles -PdfFiles $pdfs -Grid $grid -StatusLabel $lblStatus -Form $form
    }
})

$btnXtf.Add_Click({
    $choice = [System.Windows.Forms.MessageBox]::Show(
        "XTF Import: Dateien auswaehlen (Ja) oder Ordner waehlen (Nein)?",
        "XTF Import",
        [System.Windows.Forms.MessageBoxButtons]::YesNoCancel
    )
    if ($choice -eq [System.Windows.Forms.DialogResult]::Cancel) { return }

    $xtfPaths = @()
    if ($choice -eq [System.Windows.Forms.DialogResult]::Yes) {
        $openFileDialog = New-Object System.Windows.Forms.OpenFileDialog
        $openFileDialog.Filter = "XTF Dateien (*.xtf)|*.xtf|Alle Dateien (*.*)|*.*"
        $openFileDialog.InitialDirectory = "F:\AuswertungPro"
        $openFileDialog.Multiselect = $true
        if ($openFileDialog.ShowDialog() -ne [System.Windows.Forms.DialogResult]::OK) { return }
        $xtfPaths = @($openFileDialog.FileNames)
    } else {
        $folderDialog = New-Object System.Windows.Forms.FolderBrowserDialog
        $folderDialog.Description = "XTF Ordner waehlen"
        $folderDialog.SelectedPath = "F:\AuswertungPro"
        if ($folderDialog.ShowDialog() -ne [System.Windows.Forms.DialogResult]::OK) { return }
        $xtfPaths = @(Get-ChildItem -Path $folderDialog.SelectedPath -Filter *.xtf -File -Recurse | Select-Object -ExpandProperty FullName)
    }

    if (@($xtfPaths).Count -eq 0) {
        [System.Windows.Forms.MessageBox]::Show("Keine XTF Dateien gefunden.", "XTF Import")
        return
    }

    try {
        $lblStatus.Text = "XTF import..."
        $lblStatus.ForeColor = [System.Drawing.Color]::Orange
        $form.Refresh()

        $result = Import-XtfFiles -Paths $xtfPaths
        $rows = @($result.Rows)
        $script:Data = Merge-Data -Existing $script:Data -Incoming $rows
        $script:DataRows = $script:Data
        Update-Grid -Grid $grid
        if ($grid.Rows.Count -gt 0) {
            $grid.ClearSelection()
            $grid.Rows[0].Selected = $true
            $grid.CurrentCell = $grid.Rows[0].Cells[0]
            $firstRow = $grid.Rows[0].DataBoundItem
            if ($null -ne $firstRow) { Set-RowIntoForm -Row $firstRow }
        }

        if (@($result.Unknown).Count -gt 0) {
            $names = @($result.Unknown | ForEach-Object { [System.IO.Path]::GetFileName($_) }) -join "`r`n"
            [System.Windows.Forms.MessageBox]::Show("Unbekannte XTF Modelle ignoriert:`r`n$names", "XTF Import")
        }

        $lblStatus.Text = "XTF importiert: $(@($rows).Count) Haltungen"
        $lblStatus.ForeColor = [System.Drawing.Color]::DarkGreen
    } catch {
        $lblStatus.Text = "Fehler XTF: $($_.Exception.Message)"
        $lblStatus.ForeColor = [System.Drawing.Color]::Red
    }
})

$btnExport.Add_Click({
    # Commit alle Änderungen aus dem Grid und der Form
    if ($null -ne $grid) {
        $grid.EndEdit()
        if ($grid.IsCurrentCellDirty) {
            $grid.CommitEdit([System.Windows.Forms.DataGridViewDataErrorContexts]::Commit) | Out-Null
        }
    }
    if ($null -ne $script:Binding) {
        $script:Binding.EndEdit()
    }
    if ($null -ne $script:CurrentRow) {
        Set-FormToRow -Row $script:CurrentRow
    }
    if (@($script:Data).Count -eq 0) {
        [System.Windows.Forms.MessageBox]::Show("Keine Daten zum Exportieren!", "Info")
        return
    }

    $saveFileDialog = New-Object System.Windows.Forms.SaveFileDialog
    $saveFileDialog.Filter = "Excel Dateien (*.xlsx)|*.xlsx"
    $saveFileDialog.InitialDirectory = "F:\AuswertungPro\Export_Vorlage"
    $saveFileDialog.FileName = "Auswertung_import_$(Get-Date -Format 'yyyyMMdd').xlsx"

    if ($saveFileDialog.ShowDialog() -eq [System.Windows.Forms.DialogResult]::OK) {
        try {
            $lblStatus.Text = "Exportiere..."
            $form.Refresh()

            Export-Excel -Rows $script:Data -TemplatePath $script:TemplatePath -OutputPath $saveFileDialog.FileName

            $lblStatus.Text = "Exportiert: $($saveFileDialog.FileName)"
            $lblStatus.ForeColor = [System.Drawing.Color]::DarkGreen
            [System.Windows.Forms.MessageBox]::Show("Erfolgreich exportiert!", "Erfolg")
        } catch {
            $lblStatus.Text = "Fehler Export: $($_.Exception.Message)"
            $lblStatus.ForeColor = [System.Drawing.Color]::Red
            [System.Windows.Forms.MessageBox]::Show($_.Exception.Message, "Fehler")
        }
    }
})

$handleDragEnter = {
    param($senderObj, $e)
    if ($e.Data.GetDataPresent([System.Windows.Forms.DataFormats]::FileDrop)) {
        $e.Effect = [System.Windows.Forms.DragDropEffects]::Copy
    } else {
        $e.Effect = [System.Windows.Forms.DragDropEffects]::None
    }
}

$handleDragDrop = {
    param($senderObj, $e)
    $paths = $e.Data.GetData([System.Windows.Forms.DataFormats]::FileDrop)
    $pdfs = Get-PdfFilesFromPaths -Paths $paths
    Import-PdfFiles -PdfFiles $pdfs -Grid $grid -StatusLabel $lblStatus -Form $form
}

$form.Add_DragEnter($handleDragEnter)
$form.Add_DragDrop($handleDragDrop)
$grid.Add_DragEnter($handleDragEnter)
$grid.Add_DragDrop($handleDragDrop)

Show-StartScreen

$form.ShowDialog() | Out-Null
