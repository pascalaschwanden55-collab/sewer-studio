[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$PdfPath,
    [string]$OutputPath = "F:\AuswertungPro\Tabellen\Auswertung_import.xlsx",
    [string]$TemplatePath = "F:\AuswertungPro\Tabellen\Zone_1.09_.xlsx"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

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
    & $pdftotext -layout $InputPdf $OutputTxt
}

function Get-NormalizedHeader {
    param([string]$Value)
    $v = $Value.ToLowerInvariant()
    $v = $v -replace "?", "ae"
    $v = $v -replace "?", "oe"
    $v = $v -replace "?", "ue"
    $v = $v -replace "?", "ss"
    $v = $v -replace "[^a-z0-9]+", ""
    return $v
}

function ConvertFrom-Haltungsinspektionen {
    param([string]$TextPath)

    $lines = Get-Content -Path $TextPath
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

        $row = [ordered]@{
            "NR" = ""
            "Ausfuehrung_durch" = ""
            "Haltungsname" = $m.Groups["haltung"].Value.Trim()
            "Strasse" = ""
            "Rohrmaterial" = ""
            "DN mm" = ""
            "Nutzungsart" = ""
            "Haltungslaenge_m" = ""
            "Fliessrichtung" = ""
            "Primaere_Schaeden" = ""
            "Zustandsklasse" = ""
            "Pruefungsresultat" = ""
            "Sanieren Ja_Nein" = ""
            "Empfohlene Sanierungsmassnahmen" = ""
            "Kosten" = ""
            "Eigentuemer" = ""
            "Bemerkungen" = ""
            "Link" = ""
        }

        $codes = @()
        foreach ($b in $block) {
            $m = [regex]::Match($b, "^\s*\d{2}\.\d{2}\.\d{4}\s+.+\s+(?<haltung>\d+\S*-\d+\S*)\s+(?<nr>\d+)\s*$")
            if ($m.Success) {
                if (-not $row."Haltungsname") { $row."Haltungsname" = $m.Groups["haltung"].Value }
                $row."NR" = $m.Groups["nr"].Value
            }

            $m = [regex]::Match($b, "^\s*Strasse\s+(?<val>.+?)\s{2,}Schacht")
            if ($m.Success) {
                $row."Strasse" = $m.Groups["val"].Value.Trim()
            }

            $m = [regex]::Match($b, "^\s*Profil\s+(?<val>.+?)\s{2,}Grund")
            if ($m.Success) {
                $profilValue = $m.Groups["val"].Value.Trim()
                if ($profilValue -match "(\d+)\s*mm") { $row."DN mm" = $Matches[1] }
            }

            $m = [regex]::Match($b, "^\s*Material\s+(?<val>.+?)(\s{2,}|$)")
            if ($m.Success) {
                $row."Rohrmaterial" = $m.Groups["val"].Value.Trim()
            }

            $m = [regex]::Match($b, "^\s*Nutzungsart\s+(?<val>.+?)\s{2,}Inspektionsrichtung\s+(?<dir>.+?)\s*$")
            if ($m.Success) {
                $row."Nutzungsart" = $m.Groups["val"].Value.Trim()
                $dir = $m.Groups["dir"].Value.Trim()
                if ($dir -like "In Fliessrichtung*") { $row."Fliessrichtung" = "in" }
                elseif ($dir -like "Gegen Fliessrichtung*") { $row."Fliessrichtung" = "gegen" }
            }

            $m = [regex]::Match($b, "HL\s+\[m\]\s+(?<val>\d+(\.\d+)?)")
            if ($m.Success) {
                $row."Haltungslaenge_m" = $m.Groups["val"].Value
            }

            $m = [regex]::Match($b, "^\s*\S.*\s{2,}\S.*\s{2,}\S.*\s{2,}(?<operator>[^0-9].+?)\s{2,}\d+\s*$")
            if ($m.Success) {
                if (-not $row."Ausfuehrung_durch") {
                    $row."Ausfuehrung_durch" = $m.Groups["operator"].Value.Trim()
                }
            }

            $m = [regex]::Match($b, "^\s*\d+(\.\d+)?\s+(?<code>[A-Z]\d{2})\s+(?<desc>.+?)\s{2,}\d{2}:\d{2}:\d{2}")
            if ($m.Success) {
                $codes += ("{0} {1}" -f $m.Groups["code"].Value, $m.Groups["desc"].Value.Trim())
            }
        }

        if ($codes.Count -gt 0) {
            $row."Primaere_Schaeden" = ($codes -join "; ")
        }

        $rows += [PSCustomObject]$row
    }

    return $rows
}

function Write-Excel {
    param(
        [array]$Rows,
        [string]$TemplatePath,
        [string]$OutputPath
    )

    if (-not (Test-Path -Path $TemplatePath)) {
        throw "Template not found: $TemplatePath"
    }

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

    $used = $targetSheet.UsedRange
    $colsCount = $used.Columns.Count
    $colMap = @{}
    for ($c = 1; $c -le $colsCount; $c++) {
        $v = $targetSheet.Cells.Item($headerRow, $c).Text
        if (-not $v) { continue }
        $n = Get-NormalizedHeader -Value $v
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
        }
    }

    $rowIndex = $headerRow + 1
    foreach ($row in $Rows) {
        if ($colMap.NR) { $targetSheet.Cells.Item($rowIndex, $colMap.NR) = $row."NR" }
        if ($colMap.Ausfuehrung) { $targetSheet.Cells.Item($rowIndex, $colMap.Ausfuehrung) = $row."Ausfuehrung_durch" }
        if ($colMap.Haltungsname) { $targetSheet.Cells.Item($rowIndex, $colMap.Haltungsname) = $row."Haltungsname" }
        if ($colMap.Strasse) { $targetSheet.Cells.Item($rowIndex, $colMap.Strasse) = $row."Strasse" }
        if ($colMap.Rohrmaterial) { $targetSheet.Cells.Item($rowIndex, $colMap.Rohrmaterial) = $row."Rohrmaterial" }
        if ($colMap.DN) { $targetSheet.Cells.Item($rowIndex, $colMap.DN) = $row."DN mm" }
        if ($colMap.Nutzungsart) { $targetSheet.Cells.Item($rowIndex, $colMap.Nutzungsart) = $row."Nutzungsart" }
        if ($colMap.Laenge) { $targetSheet.Cells.Item($rowIndex, $colMap.Laenge) = $row."Haltungslaenge_m" }
        if ($colMap.Fliessrichtung) { $targetSheet.Cells.Item($rowIndex, $colMap.Fliessrichtung) = $row."Fliessrichtung" }
        if ($colMap.Schaeden) { $targetSheet.Cells.Item($rowIndex, $colMap.Schaeden) = $row."Primaere_Schaeden" }
        if ($colMap.Zustandsklasse) { $targetSheet.Cells.Item($rowIndex, $colMap.Zustandsklasse) = $row."Zustandsklasse" }
        if ($colMap.Pruefungsresultat) { $targetSheet.Cells.Item($rowIndex, $colMap.Pruefungsresultat) = $row."Pruefungsresultat" }
        if ($colMap.Sanieren) { $targetSheet.Cells.Item($rowIndex, $colMap.Sanieren) = $row."Sanieren Ja_Nein" }
        if ($colMap.Massnahmen) { $targetSheet.Cells.Item($rowIndex, $colMap.Massnahmen) = $row."Empfohlene Sanierungsmassnahmen" }
        if ($colMap.Kosten) { $targetSheet.Cells.Item($rowIndex, $colMap.Kosten) = $row."Kosten" }
        if ($colMap.Eigentuemer) { $targetSheet.Cells.Item($rowIndex, $colMap.Eigentuemer) = $row."Eigentuemer" }
        if ($colMap.Bemerkungen) { $targetSheet.Cells.Item($rowIndex, $colMap.Bemerkungen) = $row."Bemerkungen" }
        if ($colMap.Link) { $targetSheet.Cells.Item($rowIndex, $colMap.Link) = $row."Link" }
        $rowIndex++
    }

    $targetSheet.Columns.AutoFit() | Out-Null
    $wb.Save()
    $wb.Close($false)
    $excel.Quit()
    [System.Runtime.InteropServices.Marshal]::ReleaseComObject($excel) | Out-Null
}

if (-not (Test-Path -Path $PdfPath)) { throw "PDF not found: $PdfPath" }

$txtPath = Join-Path $env:TEMP ("pdf_extract_{0}.txt" -f ([Guid]::NewGuid().ToString("N")))
Write-Output "Extracting PDF text..."
Convert-PdfToText -InputPdf $PdfPath -OutputTxt $txtPath
Write-Output "Parsing Haltungsinspektion sections..."
$rows = ConvertFrom-Haltungsinspektionen -TextPath $txtPath
Remove-Item -Path $txtPath -Force

if ($rows.Count -eq 0) {
    throw "No Haltungsinspektion sections found in PDF."
}

Write-Output "Writing Excel output..."
Write-Excel -Rows $rows -TemplatePath $TemplatePath -OutputPath $OutputPath
Write-Output "Wrote $($rows.Count) rows to $OutputPath"
