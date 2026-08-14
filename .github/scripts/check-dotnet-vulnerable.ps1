# Prueft die NuGet-Abhaengigkeiten der Gesamtloesung auf bekannte Sicherheitsluecken.
#
# Warum ein eigenes Skript: `dotnet list package --vulnerable` meldet Funde nur im Text
# und gibt trotzdem Exit-Code 0 zurueck. Ohne Auswertung wuerde die CI gruen bleiben,
# obwohl verwundbare Pakete gefunden wurden (Gesamtaudit 2026-08-14, P1-1).
#
# Ausgewertet wird die JSON-Ausgabe, nicht der Text: Die Textmeldungen sind uebersetzt
# ("keine anfaelligen Pakete") und ein englischer Textvergleich wuerde auf einem
# deutschen System stillschweigend nichts finden.
#
# Aufruf:  powershell -NoProfile -File .github/scripts/check-dotnet-vulnerable.ps1
# Exit:    0 = keine Funde, 1 = Funde, 2 = technischer Fehler

param(
    [string]$Solution = "AuswertungPro.sln",
    # Nur fuer Selbsttests: fertige JSON-Ausgabe auswerten statt dotnet aufzurufen.
    [string]$JsonFile
)

$ErrorActionPreference = "Stop"

if ($JsonFile) {
    if (-not (Test-Path $JsonFile)) {
        Write-Host "FEHLER: JSON-Datei '$JsonFile' nicht gefunden."
        exit 2
    }
    Write-Host "Werte vorhandene JSON-Ausgabe aus: $JsonFile"
    $roh = Get-Content $JsonFile -Raw
} else {
    if (-not (Test-Path $Solution)) {
        Write-Host "FEHLER: Loesung '$Solution' nicht gefunden."
        exit 2
    }

    Write-Host "Pruefe NuGet-Pakete von $Solution (inklusive transitiver Abhaengigkeiten)..."

    $rohzeilen = & dotnet list $Solution package --vulnerable --include-transitive --format json
    $code = $LASTEXITCODE
    $roh = $rohzeilen -join "`n"

    if ($code -ne 0 -or [string]::IsNullOrWhiteSpace($roh)) {
        Write-Host "FEHLER: 'dotnet list package' brach mit Code $code ab."
        Write-Host $roh
        exit 2
    }
}

try {
    $bericht = $roh | ConvertFrom-Json
} catch {
    Write-Host "FEHLER: JSON-Ausgabe nicht lesbar: $($_.Exception.Message)"
    Write-Host $roh
    exit 2
}

if ($null -eq $bericht.projects -or $bericht.projects.Count -eq 0) {
    Write-Host "FEHLER: Bericht enthaelt kein einziges Projekt - Pruefung war unvollstaendig."
    exit 2
}

# Ein Projekt ohne Funde enthaelt nur 'path'. Erst bei Funden kommen 'frameworks' mit
# topLevelPackages/transitivePackages und deren 'vulnerabilities' dazu.
$funde = New-Object System.Collections.Generic.List[string]

foreach ($projekt in $bericht.projects) {
    if ($null -eq $projekt.frameworks) { continue }
    foreach ($framework in $projekt.frameworks) {
        foreach ($liste in @($framework.topLevelPackages, $framework.transitivePackages)) {
            if ($null -eq $liste) { continue }
            foreach ($paket in $liste) {
                if ($null -eq $paket.vulnerabilities) { continue }
                foreach ($luecke in $paket.vulnerabilities) {
                    $projektname = Split-Path $projekt.path -Leaf
                    $funde.Add("$projektname : $($paket.id) $($paket.resolvedVersion) [$($luecke.severity)] $($luecke.advisoryurl)")
                }
            }
        }
    }
}

Write-Host "Geprueft: $($bericht.projects.Count) Projekte."

if ($funde.Count -eq 0) {
    Write-Host ""
    Write-Host "Ergebnis: keine verwundbaren NuGet-Pakete."
    exit 0
}

Write-Host ""
Write-Host "VERWUNDBARE NUGET-PAKETE GEFUNDEN ($($funde.Count)):"
foreach ($zeile in $funde) { Write-Host "  $zeile" }
Write-Host ""
Write-Host "Betroffenes Paket aktualisieren oder das Advisory bewerten."
exit 1
