param(
    [Parameter(Mandatory = $true)]
    [string]$Sia405Path,
    [string]$KekPath = "",
    [string]$ProjectRoot = "",
    [string]$StreetMapPath = "",
    [switch]$PromptStreet = $false,
    [string]$OutputPath = "F:\AuswertungPro\Tabellen\Auswertung_import.xlsx",
    [string]$TemplatePath = "F:\AuswertungPro\2_1_4_7 Vorgaben und Richtlinien\2_1_4_7_5 Sanierung Entscheidungsmatrix\Auswertungstabelle_leer.xlsx"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Get-ChildText {
    param(
        [System.Xml.XmlNode]$Node,
        [string]$LocalName
    )
    $child = $Node.SelectSingleNode("./*[local-name()='$LocalName']")
    if ($null -eq $child) { return "" }
    return $child.InnerText
}

function Get-RefAttr {
    param(
        [System.Xml.XmlNode]$Node,
        [string]$LocalName
    )
    $child = $Node.SelectSingleNode("./*[local-name()='$LocalName']")
    if ($null -eq $child) { return "" }
    return $child.GetAttribute("REF")
}

if (-not (Test-Path -Path $Sia405Path)) {
    throw "SIA405 file not found: $Sia405Path"
}
if (-not (Test-Path -Path $TemplatePath)) {
    throw "Template not found: $TemplatePath"
}
if ($KekPath -and (-not (Test-Path -Path $KekPath))) {
    throw "KEK file not found: $KekPath"
}
if ($StreetMapPath -and (-not (Test-Path -Path $StreetMapPath))) {
    throw "Street map file not found: $StreetMapPath"
}

Copy-Item -Path $TemplatePath -Destination $OutputPath -Force

[xml]$xml = Get-Content -Path $Sia405Path -Raw

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
    $laenge = (Get-ChildText -Node $n -LocalName "LaengeEffektiv")
    $dn = (Get-ChildText -Node $n -LocalName "Lichte_Hoehe")
    $material = (Get-ChildText -Node $n -LocalName "Material")
    $kanalRef = (Get-RefAttr -Node $n -LocalName "AbwasserbauwerkRef")
    $vonRef = (Get-RefAttr -Node $n -LocalName "vonHaltungspunktRef")
    $nachRef = (Get-RefAttr -Node $n -LocalName "nachHaltungspunktRef")
    $vonKnoten = ""
    $nachKnoten = ""
    if ($haltungspunktByTid.ContainsKey($vonRef)) {
        $knotenTid = $haltungspunktByTid[$vonRef]
        if ($knotenByTid.ContainsKey($knotenTid)) { $vonKnoten = $knotenByTid[$knotenTid] }
    }
    if ($haltungspunktByTid.ContainsKey($nachRef)) {
        $knotenTid = $haltungspunktByTid[$nachRef]
        if ($knotenByTid.ContainsKey($knotenTid)) { $nachKnoten = $knotenByTid[$knotenTid] }
    }
    $nutzungsart = ""
    $kanalBez = ""
    if ($kanalByTid.ContainsKey($kanalRef)) {
        $nutzungsart = $kanalByTid[$kanalRef].Nutzungsart
        $kanalBez = $kanalByTid[$kanalRef].Bezeichnung
    }
    if ([string]::IsNullOrWhiteSpace($bezeichnung)) { $bezeichnung = $kanalBez }

    $rows += [PSCustomObject]@{
        Nr = $nr
        Haltungsname = $bezeichnung
        Rohrmaterial = $material
        DN = $dn
        Nutzungsart = $nutzungsart
        Laenge = $laenge
        Fliessrichtung = ""
        Schaeden = ""
        Link = ""
        VonSchacht = $vonKnoten
        BisSchacht = $nachKnoten
        Strasse = ""
    }
    $nr++
}

$kekByBezeichnung = @{}
if ($KekPath) {
    [xml]$kekXml = Get-Content -Path $KekPath -Raw

    $untersuchungNodes = $kekXml.SelectNodes("//*[contains(local-name(),'.Untersuchung')]")
    $schadenNodes = $kekXml.SelectNodes("//*[contains(local-name(),'.Kanalschaden')]")
    $dateiNodes = $kekXml.SelectNodes("//*[contains(local-name(),'.Datei')]")

    $untersuchungByTid = @{}
    foreach ($u in $untersuchungNodes) {
        $tid = $u.GetAttribute("TID")
        if ([string]::IsNullOrWhiteSpace($tid)) { continue }
        $objId = (Get-ChildText -Node $u -LocalName "OBJ_ID")
        $bez = (Get-ChildText -Node $u -LocalName "Bezeichnung")
        $von = (Get-ChildText -Node $u -LocalName "vonPunktBezeichnung")
        $bis = (Get-ChildText -Node $u -LocalName "bisPunktBezeichnung")
        $inspLen = (Get-ChildText -Node $u -LocalName "Inspizierte_Laenge")
        $untersuchungByTid[$tid] = @{
            ObjId = $objId
            Bezeichnung = $bez
            Von = $von
            Bis = $bis
            InspLen = $inspLen
            Schaeden = @()
            VideoFile = ""
        }
    }

    foreach ($s in $schadenNodes) {
        $refNode = $s.SelectSingleNode("./*[local-name()='UntersuchungRef']")
        if ($null -eq $refNode) { continue }
        $ref = $refNode.GetAttribute("REF")
        if (-not $untersuchungByTid.ContainsKey($ref)) { continue }
        $code = (Get-ChildText -Node $s -LocalName "KanalSchadencode")
        $dist = (Get-ChildText -Node $s -LocalName "Distanz")
        $ann = (Get-ChildText -Node $s -LocalName "Anmerkung")
        $entry = $code
        if ($dist) { $entry += "@$dist" }
        if ($ann) { $entry += " ($ann)" }
        $untersuchungByTid[$ref].Schaeden += $entry
    }

    foreach ($d in $dateiNodes) {
        $art = (Get-ChildText -Node $d -LocalName "Art")
        $klasse = (Get-ChildText -Node $d -LocalName "Klasse")
        if ($art -ne "digitalesVideo" -or $klasse -ne "Untersuchung") { continue }
        $obj = (Get-ChildText -Node $d -LocalName "Objekt")
        $bez = (Get-ChildText -Node $d -LocalName "Bezeichnung")
        if (-not $obj) { continue }
        foreach ($u in $untersuchungByTid.Values) {
            if ($u.ObjId -eq $obj) {
                $u.VideoFile = $bez
                break
            }
        }
    }

    foreach ($u in $untersuchungByTid.Values) {
        if (-not $u.Bezeichnung) { continue }
        $kekByBezeichnung[$u.Bezeichnung] = $u
    }
}

$videoIndex = @{}
if ($ProjectRoot) {
    $videoDirs = @(
        Join-Path $ProjectRoot "Video\\Sec",
        Join-Path $ProjectRoot "Video\\Nod"
    )
    foreach ($dir in $videoDirs) {
        if (-not (Test-Path -Path $dir)) { continue }
        Get-ChildItem -Path $dir -Recurse -File -Filter *.mpg | ForEach-Object {
            $videoIndex[$_.Name] = $_.FullName
        }
    }
}

$streetMap = @{}
if ($StreetMapPath) {
    Import-Csv -Path $StreetMapPath | ForEach-Object {
        if ($_.Haltungsname -and $_.Strasse) {
            $streetMap[$_.Haltungsname] = $_.Strasse
        }
    }
}

$excel = New-Object -ComObject Excel.Application
$excel.Visible = $false
$excel.DisplayAlerts = $false
$wb = $excel.Workbooks.Open($OutputPath, $null, $false)
$ws = $wb.Worksheets.Item("Haltungen")

$used = $ws.UsedRange
$rowsCount = $used.Rows.Count
$colsCount = $used.Columns.Count

$headerRow = 0
for ($r = 1; $r -le [Math]::Min(30, $rowsCount); $r++) {
    for ($c = 1; $c -le $colsCount; $c++) {
        $v = $ws.Cells.Item($r, $c).Text
        if ($v -like "Haltungsname*" -or $v -like "Haltungsnahme*") {
            $headerRow = $r
            break
        }
    }
    if ($headerRow -gt 0) { break }
}
if ($headerRow -eq 0) { throw "Header row not found in sheet 'Haltungen'." }

$colMap = @{}
for ($c = 1; $c -le $colsCount; $c++) {
    $v = $ws.Cells.Item($headerRow, $c).Text
    if ($v -like "NR*") { $colMap.Nr = $c }
    elseif ($v -like "Haltungsname*" -or $v -like "Haltungsnahme*") { $colMap.Haltungsname = $c }
    elseif ($v -like "Strasse*") { $colMap.Strasse = $c }
    elseif ($v -like "Rohrmaterial*") { $colMap.Rohrmaterial = $c }
    elseif ($v -like "DN*") { $colMap.DN = $c }
    elseif ($v -like "Nutzungsart*") { $colMap.Nutzungsart = $c }
    elseif ($v -like "Haltungsl*") { $colMap.Laenge = $c }
    elseif ($v -like "Fliessrichtung*") { $colMap.Fliessrichtung = $c }
    elseif ($v -like "Prim*Sch*den*") { $colMap.Schaeden = $c }
    elseif ($v -like "Link*") { $colMap.Link = $c }
}

foreach ($key in @("Nr","Haltungsname","Rohrmaterial","DN","Nutzungsart","Laenge")) {
    if (-not $colMap.ContainsKey($key)) { throw "Missing column for $key." }
}

$streetMapOut = @()
$rowIndex = $headerRow + 1
foreach ($row in $rows) {
    if ($streetMap.ContainsKey($row.Haltungsname)) {
        $row.Strasse = $streetMap[$row.Haltungsname]
    }
    if ($kekByBezeichnung.ContainsKey($row.Haltungsname)) {
        $k = $kekByBezeichnung[$row.Haltungsname]
        if ($k.Von -and $k.Bis -and $row.VonSchacht -and $row.BisSchacht) {
            if ($k.Von -eq $row.VonSchacht -and $k.Bis -eq $row.BisSchacht) {
                $row.Fliessrichtung = "in"
            } elseif ($k.Von -eq $row.BisSchacht -and $k.Bis -eq $row.VonSchacht) {
                $row.Fliessrichtung = "gegen"
            }
        }
        if (-not $row.Laenge -and $k.InspLen) { $row.Laenge = $k.InspLen }
        if ($k.Schaeden.Count -gt 0) { $row.Schaeden = ($k.Schaeden -join "; ") }
        if ($k.VideoFile -and $videoIndex.ContainsKey($k.VideoFile)) {
            $row.Link = $videoIndex[$k.VideoFile]
        } elseif ($k.VideoFile) {
            $row.Link = $k.VideoFile
        }
    }
    if ($PromptStreet -and -not $StreetMapPath -and -not $row.Strasse) {
        $inputStreet = Read-Host "Strasse for $($row.Haltungsname) (Enter to skip)"
        if ($inputStreet) { $row.Strasse = $inputStreet }
    }
    $streetMapOut += [PSCustomObject]@{ Haltungsname = $row.Haltungsname; Strasse = $row.Strasse }

    $ws.Cells.Item($rowIndex, $colMap.Nr) = $row.Nr
    $ws.Cells.Item($rowIndex, $colMap.Haltungsname) = $row.Haltungsname
    if ($colMap.ContainsKey("Strasse")) { $ws.Cells.Item($rowIndex, $colMap.Strasse) = $row.Strasse }
    $ws.Cells.Item($rowIndex, $colMap.Rohrmaterial) = $row.Rohrmaterial
    $ws.Cells.Item($rowIndex, $colMap.DN) = $row.DN
    $ws.Cells.Item($rowIndex, $colMap.Nutzungsart) = $row.Nutzungsart
    $ws.Cells.Item($rowIndex, $colMap.Laenge) = $row.Laenge
    if ($colMap.ContainsKey("Fliessrichtung")) { $ws.Cells.Item($rowIndex, $colMap.Fliessrichtung) = $row.Fliessrichtung }
    if ($colMap.ContainsKey("Schaeden")) { $ws.Cells.Item($rowIndex, $colMap.Schaeden) = $row.Schaeden }
    if ($colMap.ContainsKey("Link")) { $ws.Cells.Item($rowIndex, $colMap.Link) = $row.Link }
    $rowIndex++
}

$ws.Columns.AutoFit() | Out-Null
$wb.Save()
$wb.Close($false)
$excel.Quit()
[System.Runtime.InteropServices.Marshal]::ReleaseComObject($excel) | Out-Null

if ($PromptStreet -and -not $StreetMapPath) {
    $mapOut = [System.IO.Path]::ChangeExtension($OutputPath, "streetmap.csv")
    $streetMapOut | Export-Csv -Path $mapOut -NoTypeInformation -Encoding UTF8
    Write-Output "Saved street map to $mapOut"
}

Write-Output "Exported $($rows.Count) Haltungen to $OutputPath"
