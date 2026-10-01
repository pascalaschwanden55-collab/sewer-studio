<#
.SYNOPSIS
    Abgeschottete Testwelt fuer Absturz- und Wiederherstellungsversuche.

.BESCHREIBUNG
    SewerStudio laeuft dabei mit einem eigenen Anwendungsprofil, einer Projektkopie
    und einer eigenen Wissenswurzel. Das echte Profil, die echten Projekte und
    C:\KI_BRAIN werden nur gelesen — und nach dem Versuch wird belegt, dass sie
    unveraendert sind.

    Der Hebel ist die Umgebungsvariable SEWERSTUDIO_APPDATA_DIR (siehe
    AppDataPathResolver). Ist sie gesetzt, liegt settings.json samt zuletzt
    geoeffnetem Projekt, Wissenswurzel und Sicherungszielen im Testprofil.

.WICHTIG
    Die Elements-Platte muss abgesteckt sein. Der Spiegeldienst gleicht die GERADE
    AKTIVE Wissenswurzel nach <Elements>\Brain ab und loescht dort, was in der
    Quelle fehlt. Mit einer kleinen Testwurzel wuerde er den echten Spiegel leeren.
    Das Skript verweigert deshalb jede Aktion, die das Programm beruehrt, solange
    die Platte angeschlossen ist.

.ABLAUF
    1. Elements abstecken
    2. .\Absturzprobe.ps1 vorbereiten
    3. .\Absturzprobe.ps1 pruefen        (Trockenlauf: haelt die Abschottung?)
    4. .\Absturzprobe.ps1 starten        -> Szenario spielen
    5. .\Absturzprobe.ps1 abschiessen    (im gewaehlten Moment)
    6. .\Absturzprobe.ps1 starten        -> was sagt das Programm jetzt?
    7. .\Absturzprobe.ps1 zuruecksetzen  (vor dem naechsten Szenario)
    8. .\Absturzprobe.ps1 vergleichen    (Beleg: echte Bestaende unveraendert)
    9. .\Absturzprobe.ps1 aufraeumen, danach Elements wieder anschliessen
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true, Position = 0)]
    [ValidateSet('selbsttest', 'vorbereiten', 'pruefen', 'starten', 'abschiessen', 'zuruecksetzen', 'vergleichen', 'aufraeumen')]
    [string]$Aktion,

    # Testwelt. Muss ausserhalb von Projekten, Wissenswurzel und Programmordner liegen.
    [string]$Testwurzel = 'C:\SewerStudio-Absturzprobe',

    # Projekt, das kopiert wird. Standard: das zuletzt geoeffnete aus den echten Einstellungen.
    [string]$Projektquelle,

    # Kopiert die echte Wissenswurzel statt eine leere anzulegen.
    # Nur fuer Szenarien, die echte Golddaten brauchen (Gold-Gehirn-Trennung).
    [switch]$MitWissensKopie,

    # Zusaetzlich SHA-256 je Datei. Deutlich langsamer, bei D:\Projekte sehr langsam.
    [switch]$MitHash
)

$ErrorActionPreference = 'Stop'

# ---------------------------------------------------------------- Hilfsmittel

function Stoppe([string]$text) {
    Write-Host ''
    Write-Host "ABBRUCH: $text" -ForegroundColor Red
    exit 1
}

function Melde([string]$text) { Write-Host $text }
function Gut([string]$text) { Write-Host $text -ForegroundColor Green }
function Warne([string]$text) { Write-Host $text -ForegroundColor Yellow }

function IstUnterhalb([string]$pfad, [string]$moeglicherElter) {
    if ([string]::IsNullOrWhiteSpace($pfad) -or [string]::IsNullOrWhiteSpace($moeglicherElter)) { return $false }
    $a = [IO.Path]::GetFullPath($pfad).TrimEnd('\') + '\'
    $b = [IO.Path]::GetFullPath($moeglicherElter).TrimEnd('\') + '\'
    return $a.StartsWith($b, [StringComparison]::OrdinalIgnoreCase)
}

function PruefeElementsAbgesteckt {
    $platte = Get-Volume | Where-Object { $_.FileSystemLabel -eq 'Elements' }
    if ($platte) {
        Stoppe @"
Die Elements-Platte ist angeschlossen (Laufwerk $($platte.DriveLetter):).

Der Spiegeldienst gleicht die aktive Wissenswurzel nach <Elements>\Brain ab und
loescht dort alles, was in der Quelle fehlt. Mit der Testwurzel wuerde er den
echten Spiegel leeren.

Platte abstecken, dann erneut starten.
"@
    }
}

function EchteEinstellungen {
    $ordner = Join-Path $env:LOCALAPPDATA 'SewerStudio'
    $datei = Join-Path $ordner 'settings.json'
    if (-not (Test-Path $datei)) { Stoppe "Echte settings.json nicht gefunden: $datei" }

    $json = Get-Content $datei -Raw -Encoding UTF8 | ConvertFrom-Json
    return [pscustomobject]@{
        Ordner         = $ordner
        Datei          = $datei
        Json           = $json
        WissensWurzel  = $json.KnowledgeRootPath
        ProjektDatei   = $json.LastProjectPath
        ProjekteWurzel = $json.ProjectsRootDirectory
    }
}

# Projektstamm ist der Ordner UEBER "Projektdateien", sonst der Ordner der Projektdatei.
function ProjektStamm([string]$projektDatei) {
    if ([string]::IsNullOrWhiteSpace($projektDatei)) { return $null }
    $ordner = Split-Path $projektDatei -Parent
    if ((Split-Path $ordner -Leaf) -eq 'Projektdateien') { return (Split-Path $ordner -Parent) }
    return $ordner
}

function Spiegelkopie([string]$quelle, [string]$ziel) {
    # /MIR spiegelt, /XJ folgt keinen Junctions/Symlinks (Hausregel: nie hineinlaufen).
    $null = robocopy $quelle $ziel /MIR /XJ /R:2 /W:1 /NFL /NDL /NJH /NJS /NP
    if ($LASTEXITCODE -ge 8) { Stoppe "Kopieren fehlgeschlagen ($quelle -> $ziel), robocopy-Code $LASTEXITCODE" }
    $global:LASTEXITCODE = 0
}

function Bestandsliste([string]$wurzel, [bool]$mitHash) {
    if (-not (Test-Path $wurzel)) { return @() }
    $basis = [IO.Path]::GetFullPath($wurzel).TrimEnd('\') + '\'
    $liste = New-Object System.Collections.Generic.List[object]
    Get-ChildItem -LiteralPath $wurzel -Recurse -File -Force -ErrorAction SilentlyContinue |
        Where-Object { -not $_.Attributes.ToString().Contains('ReparsePoint') } |
        ForEach-Object {
            $satz = [ordered]@{
                Pfad    = $_.FullName.Substring($basis.Length)
                Groesse = $_.Length
                Zeit    = $_.LastWriteTimeUtc.ToString('o')
            }
            if ($mitHash) {
                try { $satz.Hash = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash }
                catch { $satz.Hash = 'NICHT_LESBAR' }
            }
            $liste.Add([pscustomobject]$satz)
        }
    return $liste
}

function BelegOrdner { Join-Path $Testwurzel '_belege' }

function SchreibeBestand([string]$name, [string[]]$wurzeln, [bool]$mitHash, [string]$ordner) {
    if ([string]::IsNullOrWhiteSpace($ordner)) { $ordner = BelegOrdner }
    $ziel = Join-Path $ordner "$name.json"
    $alles = [ordered]@{}
    foreach ($w in $wurzeln) {
        if ([string]::IsNullOrWhiteSpace($w)) { continue }
        Melde "  erfasse $w ..."
        $alles[$w] = @(Bestandsliste $w $mitHash)
        Melde "    $($alles[$w].Count) Dateien"
    }
    ($alles | ConvertTo-Json -Depth 6) | Out-File -FilePath $ziel -Encoding utf8
    return $ziel
}

function VergleicheBestaende([string]$vorherDatei, [string]$nachherDatei) {
    $vor = Get-Content $vorherDatei -Raw -Encoding UTF8 | ConvertFrom-Json
    $nach = Get-Content $nachherDatei -Raw -Encoding UTF8 | ConvertFrom-Json

    $abweichungen = 0
    foreach ($wurzel in $vor.PSObject.Properties.Name) {
        $a = @{}
        foreach ($e in $vor.$wurzel) { $a[$e.Pfad] = $e }
        $b = @{}
        foreach ($e in $nach.$wurzel) { $b[$e.Pfad] = $e }

        $fehlend = @($a.Keys | Where-Object { -not $b.ContainsKey($_) })
        $neu = @($b.Keys | Where-Object { -not $a.ContainsKey($_) })
        $geaendert = @($a.Keys | Where-Object {
                $b.ContainsKey($_) -and (
                    $a[$_].Groesse -ne $b[$_].Groesse -or
                    $a[$_].Zeit -ne $b[$_].Zeit -or
                    $a[$_].Hash -ne $b[$_].Hash)
            })

        $summe = $fehlend.Count + $neu.Count + $geaendert.Count
        $abweichungen += $summe
        if ($summe -eq 0) {
            Gut "  unveraendert: $wurzel"
        }
        else {
            Warne "  VERAENDERT: $wurzel"
            foreach ($f in ($fehlend | Select-Object -First 10)) { Warne "    fehlt:     $f" }
            foreach ($f in ($neu | Select-Object -First 10)) { Warne "    neu:       $f" }
            foreach ($f in ($geaendert | Select-Object -First 10)) { Warne "    geaendert: $f" }
            if ($summe -gt 30) { Warne "    ... insgesamt $summe Abweichungen" }
        }
    }
    return $abweichungen
}

# ---------------------------------------------------------------- Vorbereitung

$profilOrdner = Join-Path $Testwurzel 'profil'
$projektOrdner = Join-Path $Testwurzel 'projekt'
$wissensOrdner = Join-Path $Testwurzel 'wissen'
$sicherungOrdner = Join-Path $Testwurzel 'sicherung'
$urzustand = Join-Path $Testwurzel '_urzustand'
$programm = Join-Path $PSScriptRoot '..\..\src\AuswertungPro.Next.UI\bin\Debug\net10.0-windows10.0.19041\SewerStudio.exe'

$echt = EchteEinstellungen

# Die Testwelt darf nicht in einem geschuetzten Bestand liegen.
foreach ($verboten in @($echt.Ordner, $echt.WissensWurzel, $echt.ProjekteWurzel, (Join-Path $PSScriptRoot '..\..'))) {
    if ($verboten -and (IstUnterhalb $Testwurzel $verboten)) {
        Stoppe "Die Testwurzel liegt in einem geschuetzten Bestand: $verboten"
    }
}

$geschuetzt = @($echt.Ordner, $echt.WissensWurzel, (ProjektStamm $echt.ProjektDatei)) | Where-Object { $_ }

switch ($Aktion) {

    # ------------------------------------------------------------ selbsttest
    # Beweist, dass der Vorher-Nachher-Vergleich Aenderungen WIRKLICH bemerkt.
    # Ohne diesen Nachweis waere ein "unveraendert" nichts wert.
    'selbsttest' {
        $spiel = Join-Path ([IO.Path]::GetTempPath()) ("absturzprobe-selbsttest-" + [Guid]::NewGuid().ToString('N'))
        $daten = Join-Path $spiel 'daten'
        New-Item -ItemType Directory -Path $daten -Force | Out-Null
        try {
            'eins' | Out-File (Join-Path $daten 'a.txt') -Encoding utf8
            'zwei' | Out-File (Join-Path $daten 'b.txt') -Encoding utf8
            'drei' | Out-File (Join-Path $daten 'c.txt') -Encoding utf8

            $vor = SchreibeBestand 'selbsttest-vorher' @($daten) $true $spiel

            Melde ''
            Melde 'Gegenprobe 1: nichts veraendert — es darf nichts gemeldet werden.'
            $ohne = SchreibeBestand 'selbsttest-ohne' @($daten) $true $spiel
            $trefferOhne = VergleicheBestaende $vor $ohne

            # Drei echte Aenderungen: Inhalt, Loeschung, Zugang.
            'eins und noch viel mehr text' | Out-File (Join-Path $daten 'a.txt') -Encoding utf8
            Remove-Item (Join-Path $daten 'b.txt') -Force
            'vier' | Out-File (Join-Path $daten 'd.txt') -Encoding utf8

            Melde ''
            Melde 'Gegenprobe 2: drei Aenderungen — genau drei muessen auffallen.'
            $nach = SchreibeBestand 'selbsttest-nachher' @($daten) $true $spiel
            $trefferMit = VergleicheBestaende $vor $nach

            Melde ''
            if ($trefferOhne -ne 0) { Stoppe "Falscher Alarm: ohne Aenderung wurden $trefferOhne Abweichungen gemeldet." }
            if ($trefferMit -ne 3) { Stoppe "Der Vergleich ist blind: erwartet 3 Abweichungen, gemeldet $trefferMit." }
            Gut 'Selbsttest bestanden: 0 ohne Aenderung, 3 mit Aenderung.'
            Melde 'Der Vergleich taugt als Beleg.'
        }
        finally {
            if (Test-Path $spiel) { Remove-Item $spiel -Recurse -Force }
        }
    }

    # ------------------------------------------------------------ vorbereiten
    'vorbereiten' {
        PruefeElementsAbgesteckt
        if (Test-Path $Testwurzel) { Stoppe "Testwurzel existiert bereits: $Testwurzel — zuerst 'aufraeumen'." }

        $quelle = $Projektquelle
        if ([string]::IsNullOrWhiteSpace($quelle)) { $quelle = ProjektStamm $echt.ProjektDatei }
        if (-not (Test-Path $quelle)) { Stoppe "Projektquelle nicht gefunden: $quelle" }

        Melde "Testwelt:      $Testwurzel"
        Melde "Projektquelle: $quelle"
        Melde ''

        New-Item -ItemType Directory -Path $Testwurzel, $profilOrdner, $sicherungOrdner, (BelegOrdner), $urzustand -Force | Out-Null

        Melde 'Projekt kopieren ...'
        Spiegelkopie $quelle (Join-Path $urzustand 'projekt')

        if ($MitWissensKopie) {
            Melde 'Wissenswurzel kopieren (das dauert) ...'
            Spiegelkopie $echt.WissensWurzel (Join-Path $urzustand 'wissen')
        }
        else {
            New-Item -ItemType Directory -Path (Join-Path $urzustand 'wissen') -Force | Out-Null
            Melde 'Leere Wissenswurzel angelegt (mit -MitWissensKopie waere es eine Kopie).'
        }

        Spiegelkopie (Join-Path $urzustand 'projekt') $projektOrdner
        Spiegelkopie (Join-Path $urzustand 'wissen') $wissensOrdner

        # Testprofil: echte settings.json uebernehmen, damit das Schema stimmt,
        # danach ausschliesslich die Pfade umbiegen.
        $s = Get-Content $echt.Datei -Raw -Encoding UTF8 | ConvertFrom-Json
        $s.KnowledgeRootPath = $wissensOrdner
        $s.LastKnownKnowledgeRoot = $wissensOrdner
        if ($s.PSObject.Properties.Name -contains 'EvalSetRoot') { $s.EvalSetRoot = (Join-Path $wissensOrdner 'eval_set') }
        $s.ProjectsRootDirectory = $Testwurzel
        $s.LastProjectPath = ''
        $s.RecentProjectPaths = @()
        if ($s.PSObject.Properties.Name -contains 'HiddenProjectPaths') { $s.HiddenProjectPaths = @() }
        if ($s.PSObject.Properties.Name -contains 'LastFullBackupPath') { $s.LastFullBackupPath = $sicherungOrdner }
        ($s | ConvertTo-Json -Depth 12) | Out-File -FilePath (Join-Path $profilOrdner 'settings.json') -Encoding utf8

        Melde ''
        Melde 'Bestand der geschuetzten Ordner aufnehmen ...'
        if (-not $MitHash) { Warne '  Merkmale: Pfad, Groesse, Aenderungszeit. Kein SHA-256 (mit -MitHash zuschaltbar).' }
        $beleg = SchreibeBestand 'bestand-vorher' $geschuetzt $MitHash.IsPresent

        Melde ''
        Gut 'Testwelt steht.'
        Melde "  Beleg: $beleg"
        Melde ''
        Melde 'Naechster Schritt: .\Absturzprobe.ps1 pruefen'
    }

    # ------------------------------------------------------------ pruefen
    'pruefen' {
        PruefeElementsAbgesteckt
        if (-not (Test-Path $profilOrdner)) { Stoppe "Keine Testwelt vorhanden. Zuerst 'vorbereiten'." }

        $vorher = (Get-Item $echt.Datei).LastWriteTimeUtc
        Melde 'Trockenlauf: Programm startet gleich in der Testwelt.'
        Melde 'Erwartung: KEIN zuletzt geoeffnetes Projekt, leere Projektliste.'
        Melde ''
        Melde 'Pruefen Sie danach im Programm unter Einstellungen die Wissenswurzel:'
        Melde "  erwartet: $wissensOrdner"
        Melde ''
        Melde 'Programm schliessen, danach meldet dieses Skript das Ergebnis.'
        Melde ''

        $env:SEWERSTUDIO_APPDATA_DIR = $profilOrdner
        if (-not (Test-Path $programm)) { Stoppe "Programmdatei nicht gefunden: $programm" }
        Start-Process -FilePath $programm -Wait

        $nachher = (Get-Item $echt.Datei).LastWriteTimeUtc
        Melde ''
        if ($vorher -eq $nachher) {
            Gut 'Die echte settings.json wurde NICHT angefasst. Abschottung haelt.'
        }
        else {
            Stoppe @"
Die echte settings.json hat sich veraendert ($vorher -> $nachher).
Die Abschottung greift NICHT. Keine weiteren Versuche fahren.
"@
        }
    }

    # ------------------------------------------------------------ starten
    'starten' {
        PruefeElementsAbgesteckt
        if (-not (Test-Path $profilOrdner)) { Stoppe "Keine Testwelt vorhanden. Zuerst 'vorbereiten'." }
        if (Get-Process -Name 'SewerStudio' -ErrorAction SilentlyContinue) { Stoppe 'SewerStudio laeuft bereits.' }
        if (-not (Test-Path $programm)) { Stoppe "Programmdatei nicht gefunden: $programm" }

        $env:SEWERSTUDIO_APPDATA_DIR = $profilOrdner
        $p = Start-Process -FilePath $programm -PassThru
        Gut "Gestartet in der Testwelt (PID $($p.Id))."
        Melde "  Profil:  $profilOrdner"
        Melde "  Projekt: $projektOrdner"
        Melde ''
        Melde 'Abschuss im gewaehlten Moment: .\Absturzprobe.ps1 abschiessen'
    }

    # ------------------------------------------------------------ abschiessen
    'abschiessen' {
        $prozesse = Get-Process -Name 'SewerStudio' -ErrorAction SilentlyContinue
        if (-not $prozesse) { Stoppe 'SewerStudio laeuft nicht.' }
        $zeit = (Get-Date).ToUniversalTime().ToString('o')
        $prozesse | Stop-Process -Force
        Gut "Hart beendet um $zeit (kein sauberes Schliessen)."
        Melde 'Jetzt erneut starten und beobachten, was das Programm meldet.'
    }

    # ------------------------------------------------------------ zuruecksetzen
    'zuruecksetzen' {
        PruefeElementsAbgesteckt
        if (-not (Test-Path $urzustand)) { Stoppe "Kein Urzustand vorhanden. Zuerst 'vorbereiten'." }
        if (Get-Process -Name 'SewerStudio' -ErrorAction SilentlyContinue) { Stoppe 'SewerStudio laeuft noch — zuerst beenden.' }

        # Sicherheitsnetz: nur innerhalb der Testwurzel spiegeln.
        foreach ($ziel in @($projektOrdner, $wissensOrdner)) {
            if (-not (IstUnterhalb $ziel $Testwurzel)) { Stoppe "Ziel liegt ausserhalb der Testwelt: $ziel" }
        }

        Spiegelkopie (Join-Path $urzustand 'projekt') $projektOrdner
        Spiegelkopie (Join-Path $urzustand 'wissen') $wissensOrdner
        Gut 'Testprojekt und Testwissenswurzel stehen wieder im Urzustand.'
    }

    # ------------------------------------------------------------ vergleichen
    'vergleichen' {
        $vorherDatei = Join-Path (BelegOrdner) 'bestand-vorher.json'
        if (-not (Test-Path $vorherDatei)) { Stoppe "Kein Ausgangsbestand vorhanden: $vorherDatei" }

        Melde 'Bestand jetzt aufnehmen ...'
        $nachherDatei = SchreibeBestand 'bestand-nachher' $geschuetzt $MitHash.IsPresent

        $abweichungen = VergleicheBestaende $vorherDatei $nachherDatei

        Melde ''
        if ($abweichungen -eq 0) { Gut 'Beleg: Alle geschuetzten Bestaende sind unveraendert.' }
        else { Warne "$abweichungen Abweichungen — einzeln pruefen, bevor weitergemacht wird." }
    }

    # ------------------------------------------------------------ aufraeumen
    'aufraeumen' {
        if (Get-Process -Name 'SewerStudio' -ErrorAction SilentlyContinue) { Stoppe 'SewerStudio laeuft noch — zuerst beenden.' }
        if (-not (Test-Path $Testwurzel)) { Stoppe "Keine Testwelt vorhanden: $Testwurzel" }
        foreach ($verboten in @($echt.Ordner, $echt.WissensWurzel, $echt.ProjekteWurzel)) {
            if ($verboten -and (IstUnterhalb $Testwurzel $verboten)) { Stoppe "Sicherheitssperre: $Testwurzel liegt unter $verboten" }
        }

        Warne "Entfernt: $Testwurzel"
        Remove-Item -LiteralPath $Testwurzel -Recurse -Force
        Gut 'Testwelt entfernt. Elements-Platte kann wieder angeschlossen werden.'
    }
}
