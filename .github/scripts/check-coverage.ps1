# Prueft die gemessene Testabdeckung gegen eine Mindestgrenze (Gesamtaudit 2026-08-14, Prio 2).
#
# Vorher gab es sehr viele Tests, aber keine gemessene Abdeckung und keine Untergrenze.
# Gemessen wird mit dem im .NET-SDK enthaltenen Code-Coverage-Sammler (kein zusaetzliches
# NuGet-Paket): dotnet test --collect:"Code Coverage;Format=cobertura"
#
# Die Grenze steht in .github/coverage-baseline.json und darf nur STEIGEN (Ratchet).
# Faellt die Abdeckung darunter, schlaegt die Pruefung fehl. Liegt sie deutlich darueber,
# wird das Anheben der Grenze angemahnt - sonst verrottet die Zahl.
#
# Aufruf:  powershell -NoProfile -File .github/scripts/check-coverage.ps1 -ResultsDirectory <ordner>
# Exit:    0 = Grenze gehalten, 1 = darunter oder Grenze veraltet, 2 = technischer Fehler

param(
    [Parameter(Mandatory = $true)][string]$ResultsDirectory,
    [string]$BaselineFile = ".github/coverage-baseline.json",
    [string]$ReferenceRef = "",
    # Ab dieser Ueberschreitung gilt die Grenze als veraltet.
    [double]$RatchetToleranz = 0.5
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path $ResultsDirectory)) {
    Write-Host "FEHLER: Ergebnisordner nicht gefunden: $ResultsDirectory"
    exit 2
}

if (-not (Test-Path $BaselineFile)) {
    Write-Host "FEHLER: Grenzdatei nicht gefunden: $BaselineFile"
    exit 2
}

$berichte = Get-ChildItem -Path $ResultsDirectory -Filter "*.cobertura.xml" -Recurse
if ($berichte.Count -eq 0) {
    Write-Host "FEHLER: Keine Cobertura-Berichte gefunden. Lief der Testlauf mit"
    Write-Host "--collect:`"Code Coverage;Format=cobertura`" ?"
    exit 2
}

# Zeilen ueber ALLE Berichte summieren. Die einzelnen line-rate-Werte zu mitteln waere
# falsch: ein kleines Projekt mit hoher Rate wuerde ein grosses mit niedriger ausgleichen.
$abgedeckt = 0
$gesamt = 0

foreach ($bericht in $berichte) {
    try {
        [xml]$xml = Get-Content $bericht.FullName -Raw
    } catch {
        Write-Host "FEHLER: Bericht nicht lesbar ($($bericht.Name)): $($_.Exception.Message)"
        exit 2
    }

    $wurzel = $xml.coverage
    if ($null -eq $wurzel) {
        Write-Host "FEHLER: Unerwartetes Berichtsformat in $($bericht.Name)."
        exit 2
    }

    $abgedeckt += [int]$wurzel.'lines-covered'
    $gesamt += [int]$wurzel.'lines-valid'
}

if ($gesamt -le 0) {
    Write-Host "FEHLER: Die Berichte enthalten keine auswertbaren Zeilen."
    exit 2
}

$prozent = [math]::Round(100.0 * $abgedeckt / $gesamt, 2)

try {
    $baseline = Get-Content $BaselineFile -Raw | ConvertFrom-Json
    $grenze = [double]$baseline.minimumLinePercent
} catch {
    Write-Host "FEHLER: Grenzdatei nicht lesbar: $($_.Exception.Message)"
    exit 2
}

# Eine Aenderung darf die bereits erreichte Grenze nicht einfach mit absenken.
# Im Pull Request vergleichen wir mit dem Zielbranch, sonst mit dem direkten
# Vorgaenger. Die CI holt dafuer die vollstaendige Historie.
if ([string]::IsNullOrWhiteSpace($ReferenceRef)) {
    $ReferenceRef = if (-not [string]::IsNullOrWhiteSpace($env:GITHUB_BASE_REF)) {
        "origin/$($env:GITHUB_BASE_REF)"
    } else {
        "HEAD^"
    }
}

$baselinePathForGit = $BaselineFile.Replace('\', '/')
$referenceLines = @(& git show "${ReferenceRef}:${baselinePathForGit}" 2>$null)
$gitShowExitCode = $LASTEXITCODE
$referenceText = $referenceLines -join [Environment]::NewLine
if ($gitShowExitCode -ne 0 -or [string]::IsNullOrWhiteSpace($referenceText)) {
    Write-Host "FEHLER: Vergleichsgrenze aus $ReferenceRef konnte nicht gelesen werden."
    exit 2
}
try {
    $referenceBaseline = $referenceText | ConvertFrom-Json
    $referenceGrenze = [double]$referenceBaseline.minimumLinePercent
} catch {
    Write-Host "FEHLER: Vergleichsgrenze aus $ReferenceRef ist nicht lesbar: $($_.Exception.Message)"
    exit 2
}

if ($grenze -lt $referenceGrenze) {
    Write-Host "ABDECKUNGSGRENZE DARF NICHT SINKEN: $grenze % statt bisher $referenceGrenze %."
    exit 1
}

Write-Host "Berichte:   $($berichte.Count)"
Write-Host "Zeilen:     $abgedeckt von $gesamt abgedeckt"
Write-Host "Abdeckung:  $prozent %"
Write-Host "Mindestens: $grenze %"
Write-Host "Vorher:     $referenceGrenze % ($ReferenceRef)"

if ($prozent -lt $grenze) {
    Write-Host ""
    Write-Host "ABDECKUNG UNTER DER GRENZE. Fehlende Tests ergaenzen oder die Aenderung pruefen."
    exit 1
}

if ($prozent -gt ($grenze + $RatchetToleranz)) {
    Write-Host ""
    Write-Host "Die Abdeckung liegt mehr als $RatchetToleranz Punkte ueber der Grenze."
    Write-Host "minimumLinePercent in $BaselineFile auf $prozent anheben (Ratchet),"
    Write-Host "damit die Grenze nicht verrottet."
    exit 1
}

Write-Host ""
Write-Host "Ergebnis: Grenze gehalten."
exit 0
