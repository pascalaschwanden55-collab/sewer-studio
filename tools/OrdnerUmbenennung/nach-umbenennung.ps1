<#
.SYNOPSIS
  Zieht nach dem Umbenennen des Programmordners Sewer-Studio_KI_4.5 -> Sewer-Studio_KI_5.0
  alle Stellen nach, die den festen Pfad tragen. Bewusst OHNE Neuaufbau der Python-Umgebung:
  Beide Namen sind exakt 19 Zeichen lang, darum wird der Pfad byteweise ersetzt - auch in den
  37 Launcher-EXEs der venv, deren Groesse sich dadurch nicht aendert.

.DESCRIPTION
  Reihenfolge (siehe ANLEITUNG.md): alle Sitzungen/Editoren/Sidecar schliessen, Ordner im
  Explorer umbenennen, NEUE Shell im neuen Ordner oeffnen, dieses Skript zuerst mit -Pruefen,
  dann mit -Ausfuehren starten. -Rueckgaengig dreht die Ersetzung wieder um.

  Das Skript verweigert den Lauf, wenn es nicht aus einem Ordner mit "_5.0" aufgerufen wird
  (sonst wuerde es die Umgebung kaputt schreiben, solange der Ordner noch 4.5 heisst) und wenn
  SewerStudio, ein Python-Prozess, VS Code, Codex oder ein MCP-Server laeuft (gesperrte Dateien).

.PARAMETER Pruefen      Nur zeigen, was geaendert wuerde (Standard).
.PARAMETER Ausfuehren   Aenderungen wirklich schreiben.
.PARAMETER Rueckgaengig 5.0 -> 4.5 zurueckschreiben (nur mit -Ausfuehren wirksam).
.PARAMETER Selbsttest   Ersetzung auf Kopien von zwei venv-Dateien in einem Temp-Ordner beweisen.
#>
[CmdletBinding()]
param(
    [switch]$Pruefen,
    [switch]$Ausfuehren,
    [switch]$Rueckgaengig,
    [switch]$Selbsttest
)
$ErrorActionPreference = 'Stop'
$alt = 'Sewer-Studio_KI_4.5'; $neu = 'Sewer-Studio_KI_5.0'
if ($Rueckgaengig) { $t = $alt; $alt = $neu; $neu = $t }
if ($alt.Length -ne $neu.Length) { throw "Alt und Neu muessen gleich lang sein ($($alt.Length) vs $($neu.Length))." }
$enc = [System.Text.Encoding]::ASCII
$script:altB = $enc.GetBytes($alt); $script:neuB = $enc.GetBytes($neu)

function Ersetze-Bytes {
    # Ersetzt jedes Vorkommen von altB durch neuB (gleich lang) - liefert die Trefferzahl.
    param([byte[]]$Daten)
    $n = 0; $i = 0
    while ($true) {
        $i = [Array]::IndexOf($Daten, $script:altB[0], $i)
        if ($i -lt 0 -or $i + $script:altB.Length -gt $Daten.Length) { break }
        $ok = $true
        for ($k = 1; $k -lt $script:altB.Length; $k++) { if ($Daten[$i + $k] -ne $script:altB[$k]) { $ok = $false; break } }
        if ($ok) { [Array]::Copy($script:neuB, 0, $Daten, $i, $script:neuB.Length); $n++; $i += $script:altB.Length } else { $i++ }
    }
    return $n
}
function Zaehle-Bytes {
    param([byte[]]$Daten)
    $kopie = [byte[]]::new($Daten.Length); [Array]::Copy($Daten, $kopie, $Daten.Length)
    return (Ersetze-Bytes $kopie)
}
function Bearbeite-Datei {
    param([string]$Pfad, [bool]$Schreiben)
    if (-not (Test-Path -LiteralPath $Pfad)) { return 0 }
    $bytes = [System.IO.File]::ReadAllBytes($Pfad)
    $treffer = Zaehle-Bytes $bytes
    if ($treffer -eq 0) { return 0 }
    if ($Schreiben) {
        $null = Ersetze-Bytes $bytes
        if ($bytes.Length -ne (Get-Item -LiteralPath $Pfad).Length) { throw "Groesse veraendert bei $Pfad - abgebrochen." }
        [System.IO.File]::WriteAllBytes($Pfad, $bytes)
    }
    return $treffer
}
function Tausche-Richtung { $t = $script:altB; $script:altB = $script:neuB; $script:neuB = $t }

# --- Selbsttest: Beweis auf Kopien, nichts Echtes wird angefasst -------------------------
if ($Selbsttest) {
    $repo = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
    $tmp = Join-Path $env:TEMP ('umbenennung-selbsttest-' + [guid]::NewGuid())
    New-Item -ItemType Directory -Path $tmp | Out-Null
    try {
        $quellen = @('sidecar\.venv\Scripts\uvicorn.exe', 'sidecar\.venv\Scripts\activate.bat') |
            ForEach-Object { Join-Path $repo $_ } | Where-Object { Test-Path $_ }
        if ($quellen.Count -eq 0) { throw 'Selbsttest: keine venv-Dateien gefunden.' }
        foreach ($q in $quellen) {
            $k = Join-Path $tmp (Split-Path -Leaf $q); Copy-Item -LiteralPath $q -Destination $k
            $vorher = (Get-Item $k).Length; $sha1 = (Get-FileHash $k).Hash
            $t1 = Bearbeite-Datei -Pfad $k -Schreiben $true
            $nachher = (Get-Item $k).Length
            $rest = Zaehle-Bytes ([System.IO.File]::ReadAllBytes($k))
            Tausche-Richtung
            $t2 = Bearbeite-Datei -Pfad $k -Schreiben $true
            Tausche-Richtung
            $sha2 = (Get-FileHash $k).Hash
            $ok = ($t1 -gt 0) -and ($vorher -eq $nachher) -and ($rest -eq 0) -and ($t1 -eq $t2) -and ($sha1 -eq $sha2)
            $urteil = 'FEHLER'; if ($ok) { $urteil = 'OK' }
            '{0,-14} Treffer {1}  Groesse {2}->{3}  Rest {4}  Rueckweg bytegleich: {5}  => {6}' -f (Split-Path -Leaf $q), $t1, $vorher, $nachher, $rest, ($sha1 -eq $sha2), $urteil
            if (-not $ok) { exit 1 }
        }
        'Selbsttest bestanden.'
    } finally { Remove-Item -Recurse -Force $tmp -ErrorAction SilentlyContinue }
    exit 0
}

# --- Sicherungen --------------------------------------------------------------------------
$repo = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
if (-not $Rueckgaengig -and $repo -notlike "*$neu*") { throw "Dieses Skript muss aus dem UMBENANNTEN Ordner ($neu) laufen. Aktuell: $repo" }
$blocker = Get-Process -ErrorAction SilentlyContinue | Where-Object { $_.ProcessName -in @('SewerStudio', 'SewerStudio.McpServer', 'python', 'Code', 'codex') }
if ($blocker) { throw ('Zuerst schliessen: ' + (($blocker | Select-Object -ExpandProperty ProcessName -Unique) -join ', ')) }
$schreiben = [bool]$Ausfuehren
if (-not $schreiben) { 'PRUEFMODUS - es wird nichts geschrieben. Mit -Ausfuehren schreiben.' }

# --- 1) Python-Umgebung: Aktivierungsskripte und Launcher ----------------------------------
$venv = Join-Path $repo 'sidecar\.venv\Scripts'
$dateien = Get-ChildItem -LiteralPath $venv -File | Where-Object { $_.Name -like 'activate*' -or $_.Extension -eq '.exe' -or $_.Name -eq 'pygrun' }
$summe = 0; $anz = 0
foreach ($f in $dateien) { $n = Bearbeite-Datei -Pfad $f.FullName -Schreiben $schreiben; if ($n -gt 0) { $summe += $n; $anz++ } }
'1) venv: {0} Dateien mit {1} Vorkommen' -f $anz, $summe

# --- 2) Repo-Konfiguration und Desktop-Skript -----------------------------------------------
$einzeln = @((Join-Path $repo '.claude\settings.local.json'), (Join-Path $env:USERPROFILE 'Desktop\Coding.bat'))
foreach ($p in $einzeln) { $n = Bearbeite-Datei -Pfad $p -Schreiben $schreiben; '2) {0}: {1} Vorkommen' -f (Split-Path -Leaf $p), $n }

# --- 3) Git-Worktrees ------------------------------------------------------------------------
$innere = @('.tmp\push-eigen-final', '.tmp\push-geoshop-reihenfolge', '.claude\worktrees\jagdmatt-leitungen-feldli-50ad64') |
    ForEach-Object { Join-Path $repo $_ } | Where-Object { Test-Path $_ }
$geschwister = @('C:\Sewer-Studio_KI_4.5-nova', 'C:\Sewer-Studio_KI_5.0-nova', 'C:\Sewer-Studio_KI_4.5-optik', 'C:\Sewer-Studio_KI_5.0-optik') | Where-Object { Test-Path $_ }
$alle = @($innere) + @($geschwister)
'3) git worktree repair fuer: {0}' -f (($alle | ForEach-Object { Split-Path -Leaf $_ }) -join ', ')
if ($schreiben) {
    Push-Location $repo
    try {
        # git meldet jede Reparatur auf stderr ("repair: gitdir incorrect: ..."). Mit
        # $ErrorActionPreference=Stop wuerde PowerShell 5.1 daraus einen Abbruch machen -
        # real passiert am 13.09.2026, die Reparatur selbst war da schon erledigt.
        $ErrorActionPreference = 'Continue'
        & git worktree repair @alle 2>&1 | ForEach-Object { "   $($_.ToString())" }
        $ErrorActionPreference = 'Stop'
        if ($LASTEXITCODE -ne 0) { throw "git worktree repair meldete Exit-Code $LASTEXITCODE." }
        & git worktree list | ForEach-Object { "   $_" }
    } finally { Pop-Location }
}

# --- 4) Claude-Gedaechtnis (kopieren, nie loeschen) -----------------------------------------
$projekte = Join-Path $env:USERPROFILE '.claude\projects'
$von = Join-Path $projekte ('c--' + ($alt -replace '[_.]', '-'))
$nach = Join-Path $projekte ('c--' + ($neu -replace '[_.]', '-'))
if ((Test-Path $von) -and -not (Test-Path $nach)) {
    '4) Gedaechtnis: {0} -> {1} (Kopie)' -f (Split-Path -Leaf $von), (Split-Path -Leaf $nach)
    if ($schreiben) { Copy-Item -LiteralPath $von -Destination $nach -Recurse }
} elseif (Test-Path $nach) { '4) Gedaechtnis: Ziel existiert schon - nichts kopiert.' } else { '4) Gedaechtnis: Quelle nicht gefunden - nichts zu tun.' }

# --- 5) Restkontrolle -------------------------------------------------------------------------
$rest = (Get-ChildItem -LiteralPath $venv -File | Where-Object { (Zaehle-Bytes ([System.IO.File]::ReadAllBytes($_.FullName))) -gt 0 } | Measure-Object).Count
'5) Restliche venv-Dateien mit altem Pfad: {0}' -f $rest
'Danach von Hand: dotnet build AuswertungPro.sln  |  sidecar\start_sidecar.ps1 und /health pruefen  |  VS Code neu oeffnen.'
