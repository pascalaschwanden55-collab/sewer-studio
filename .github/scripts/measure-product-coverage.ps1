# Misst die Zeilenabdeckung NUR des Produktcodes (src/), zusaetzlich zur Verlaufsgrenze in
# check-coverage.ps1. Wartbarkeitsplan 2026-09-27, AP04.
#
# Unterschied zur Verlaufsgrenze:
#   - gezaehlt werden nur Dateien unter src/, ohne erzeugten Code (obj/, *.g.cs, *.g.i.cs,
#     *.Designer.cs); Test- und Werkzeugcode bleibt draussen;
#   - eine Produktzeile, die mehrere Testprojekte ausfuehren, zaehlt EINMAL (vereinigt nach
#     normalisiertem Dateipfad und Zeilennummer; abgedeckt, wenn irgendein Bericht sie trifft).
#
# Die Messung ist vorerst nur eine Anzeige und setzt keine Grenze. Eine Grenze wird erst nach
# einem echten CI-Lauf festgelegt (siehe .github/coverage-baseline.json, wieMessen).
#
# Aufruf:  powershell -NoProfile -File .github/scripts/measure-product-coverage.ps1 -ResultsDirectory <ordner>
# Exit:    0 = gemessen, 2 = technischer Fehler (kein Bericht, unlesbar, keine Produktzeilen)

param(
    [Parameter(Mandatory = $true)][string]$ResultsDirectory,
    # Teilwerte fuer gerade umgebaute Bereiche (Pfadanfang relativ zur Repo-Wurzel).
    [string[]]$Bereiche = @(
        "src/AuswertungPro.Next.Infrastructure/Ai/Pipeline/",
        "src/AuswertungPro.Next.UI/Services/",
        "src/AuswertungPro.Next.Application/Ai/Training/ExportPlans/",
        "src/AuswertungPro.Next.Application/Cost/",
        "src/AuswertungPro.Next.UI/Ai/Coding/"
    ),
    [string]$JsonAusgabe = "",
    # Wurzel, gegen die die absoluten Pfade der Berichte relativ gemacht werden.
    [string]$RepoRoot = ""
)

$ErrorActionPreference = "Stop"

# Ueber "powershell -File" kommt eine Liste als ein Text mit Kommas an.
$Bereiche = @($Bereiche | ForEach-Object { $_ -split ',' } | ForEach-Object { $_.Trim() } | Where-Object { $_ })

if ([string]::IsNullOrWhiteSpace($RepoRoot)) {
    $RepoRoot = (& git rev-parse --show-toplevel 2>$null)
}
$wurzel = if ([string]::IsNullOrWhiteSpace($RepoRoot)) { "" } else { $RepoRoot.Replace('\', '/').TrimEnd('/') + '/' }

if (-not (Test-Path $ResultsDirectory)) {
    Write-Host "FEHLER: Ergebnisordner nicht gefunden: $ResultsDirectory"
    exit 2
}

$berichte = @(Get-ChildItem -Path $ResultsDirectory -Filter "*.cobertura.xml" -Recurse)
if ($berichte.Count -eq 0) {
    Write-Host "FEHLER: Keine Cobertura-Berichte gefunden."
    exit 2
}

function Get-RelativerPfad([string]$pfad) {
    $p = $pfad.Replace('\', '/')
    if ($wurzel.Length -gt 0 -and $p.StartsWith($wurzel, [StringComparison]::OrdinalIgnoreCase)) {
        return $p.Substring($wurzel.Length)
    }
    # Rueckfall, falls der Bericht auf einem anderen Rechner entstand: erster src/tests/tools-Abschnitt.
    $treffer = [regex]::Match($p, '(^|/)(src|tests|tools)/.*$', 'IgnoreCase')
    if (-not $treffer.Success) { return $null }
    return $treffer.Value.TrimStart('/')
}

function Test-Produktdatei([string]$relativ) {
    if ($null -eq $relativ) { return $false }
    $r = $relativ.ToLowerInvariant()
    if (-not $r.StartsWith("src/")) { return $false }
    if ($r.Contains("/obj/") -or $r.Contains("/bin/")) { return $false }
    if ($r.EndsWith(".g.cs") -or $r.EndsWith(".g.i.cs") -or $r.EndsWith(".designer.cs")) { return $false }
    return $true
}

# Schluessel "pfad|zeile" -> abgedeckt (bool). Pfad kleingeschrieben, damit Gross/Klein der
# Laufwerksbuchstaben und Ordner zwischen Berichten keine Doppelzaehlung ergibt.
$zeilen = @{}
$anzeigePfad = @{}

foreach ($bericht in $berichte) {
    try {
        [xml]$xml = Get-Content $bericht.FullName -Raw
    } catch {
        Write-Host "FEHLER: Bericht nicht lesbar ($($bericht.Name)): $($_.Exception.Message)"
        exit 2
    }
    if ($null -eq $xml.coverage) {
        Write-Host "FEHLER: Unerwartetes Berichtsformat in $($bericht.Name)."
        exit 2
    }

    foreach ($klasse in $xml.SelectNodes("//class")) {
        $relativ = Get-RelativerPfad $klasse.filename
        if (-not (Test-Produktdatei $relativ)) { continue }
        $schluesselPfad = $relativ.ToLowerInvariant()
        $anzeigePfad[$schluesselPfad] = $relativ
        # Nur die Zeilen der Klasse selbst; die Methodenzeilen wiederholen sie.
        foreach ($zeile in $klasse.SelectNodes("lines/line")) {
            $schluessel = "$schluesselPfad|$($zeile.number)"
            $getroffen = [int]$zeile.hits -gt 0
            if ($zeilen.ContainsKey($schluessel)) {
                $zeilen[$schluessel] = $zeilen[$schluessel] -or $getroffen
            } else {
                $zeilen[$schluessel] = $getroffen
            }
        }
    }
}

if ($zeilen.Count -eq 0) {
    Write-Host "FEHLER: Die Berichte enthalten keine Produktzeilen unter src/."
    exit 2
}

function Get-Kennzahl([string]$praefix) {
    $gesamt = 0; $abgedeckt = 0
    $p = $praefix.ToLowerInvariant()
    foreach ($eintrag in $zeilen.GetEnumerator()) {
        if ($p.Length -gt 0 -and -not $eintrag.Key.StartsWith($p)) { continue }
        $gesamt++
        if ($eintrag.Value) { $abgedeckt++ }
    }
    $prozent = if ($gesamt -gt 0) { [math]::Round(100.0 * $abgedeckt / $gesamt, 2) } else { $null }
    return [pscustomobject]@{ Bereich = $praefix; Abgedeckt = $abgedeckt; Gesamt = $gesamt; Prozent = $prozent }
}

$gesamtwert = Get-Kennzahl ""
$teilwerte = @($Bereiche | ForEach-Object { Get-Kennzahl $_ })

Write-Host "Produktabdeckung (nur src/, ohne erzeugten Code, Zeilen einmal gezaehlt)"
Write-Host "Berichte:     $($berichte.Count)"
Write-Host "Dateien:      $($anzeigePfad.Count)"
Write-Host ("Produktzeilen: {0} von {1} abgedeckt = {2} %" -f $gesamtwert.Abgedeckt, $gesamtwert.Gesamt, $gesamtwert.Prozent)
foreach ($t in $teilwerte) {
    if ($t.Gesamt -eq 0) {
        Write-Host ("  {0}: keine Zeilen" -f $t.Bereich)
    } else {
        Write-Host ("  {0}: {1} von {2} = {3} %" -f $t.Bereich, $t.Abgedeckt, $t.Gesamt, $t.Prozent)
    }
}
Write-Host "Hinweis: Anzeige ohne Grenze; nicht mit der Verlaufszahl aus check-coverage.ps1 vergleichen."

if (-not [string]::IsNullOrWhiteSpace($JsonAusgabe)) {
    [pscustomobject]@{
        gesamt = $gesamtwert
        bereiche = $teilwerte
        berichte = $berichte.Count
        dateien = $anzeigePfad.Count
    } | ConvertTo-Json -Depth 4 | Set-Content -Path $JsonAusgabe -Encoding utf8
}

exit 0
