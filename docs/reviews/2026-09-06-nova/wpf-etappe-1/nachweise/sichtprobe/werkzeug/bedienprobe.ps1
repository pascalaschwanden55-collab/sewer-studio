# Bedienpruefung der Nova-Etappe 1 am laufenden Programm, isoliert:
# eigener Einstellungsordner (SEWERSTUDIO_APPDATA_DIR), kuenstliches Projekt, echter Wissensordner.
# Phase 1: oeffnen, Haltungen, Zeilen zaehlen, Zuklappen, Feldsuche, Weitere Aktionen, Spaltenansicht,
#          Trennlinien verschieben, Haltungsansicht-Toggle, dann schliessen.
# Phase 2: neu starten und pruefen, ob Spaltenansicht und Trennlinien erhalten sind.
param(
    [int]$Phase = 1,
    [string]$Out = "",
    [string]$Exe = "C:\Sewer-Studio_KI_4.5-nova\src\AuswertungPro.Next.UI\bin\Release\net10.0-windows10.0.19041\SewerStudio.exe"
)
$ErrorActionPreference = "Stop"
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms
Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class Win32 {
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] public static extern void mouse_event(uint flags, uint dx, uint dy, uint data, UIntPtr extra);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    public const uint LEFTDOWN = 0x0002, LEFTUP = 0x0004;
}
"@
$basis = Split-Path -Parent $MyInvocation.MyCommand.Path
if ($Out -eq "") { $Out = Join-Path $basis "ergebnis" }
New-Item -ItemType Directory -Force $Out | Out-Null
$env:SEWERSTUDIO_APPDATA_DIR = Join-Path $basis "profil"
$env:SEWERSTUDIO_KNOWLEDGE_ROOT = "C:\KI_BRAIN"
$bericht = [ordered]@{ phase = $Phase; start = (Get-Date).ToString("s"); schritte = @() }
function Notiere($name, $wert) { $script:bericht.schritte += [ordered]@{ schritt = $name; wert = $wert }; Write-Host "  $name = $wert" }

$AE = [System.Windows.Automation.AutomationElement]
$TS = [System.Windows.Automation.TreeScope]
function Suche($root, $name, $typ = $null) {
    $c = New-Object System.Windows.Automation.PropertyCondition($AE::NameProperty, $name)
    if ($typ) { $c = New-Object System.Windows.Automation.AndCondition($c, (New-Object System.Windows.Automation.PropertyCondition($AE::ControlTypeProperty, $typ))) }
    return $root.FindFirst($TS::Descendants, $c)
}
function SucheId($root, $id) {
    return $root.FindFirst($TS::Descendants, (New-Object System.Windows.Automation.PropertyCondition($AE::AutomationIdProperty, $id)))
}
function Warte($sek) { Start-Sleep -Milliseconds ([int]($sek * 1000)) }
function Vorn() { try { [Win32]::SetForegroundWindow((New-Object IntPtr ($script:fenster.Current.NativeWindowHandle))) | Out-Null } catch { }; Warte 0.2 }
function Klick($el, $dx = 0, $dy = 0) {
    Vorn
    $r = $el.Current.BoundingRectangle
    # Virtualisierte Tabellenzeilen sind breiter als der Bildschirm: nie die Mitte, sondern links im Rechteck klicken.
    $breite = [Math]::Min($r.Width, 400)
    $x = [int]($r.X + $breite / 2 + $dx); $y = [int]($r.Y + $r.Height / 2 + $dy)
    [Win32]::SetCursorPos($x, $y) | Out-Null; Warte 0.15
    [Win32]::mouse_event([Win32]::LEFTDOWN, 0, 0, 0, [UIntPtr]::Zero); Warte 0.08
    [Win32]::mouse_event([Win32]::LEFTUP, 0, 0, 0, [UIntPtr]::Zero); Warte 0.4
}
function Ziehe($el, $dx, $dy) {
    Vorn
    $r = $el.Current.BoundingRectangle
    $x = [int]($r.X + $r.Width / 2); $y = [int]($r.Y + $r.Height / 2)
    [Win32]::SetCursorPos($x, $y) | Out-Null; Warte 0.2
    [Win32]::mouse_event([Win32]::LEFTDOWN, 0, 0, 0, [UIntPtr]::Zero); Warte 0.2
    for ($i = 1; $i -le 10; $i++) { [Win32]::SetCursorPos($x + [int]($dx * $i / 10), $y + [int]($dy * $i / 10)) | Out-Null; Warte 0.04 }
    Warte 0.2; [Win32]::mouse_event([Win32]::LEFTUP, 0, 0, 0, [UIntPtr]::Zero); Warte 0.5
}
function Foto($fenster, $name) {
    Vorn; Warte 0.3
    $r = $fenster.Current.BoundingRectangle
    $bmp = New-Object System.Drawing.Bitmap([int]$r.Width, [int]$r.Height)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.CopyFromScreen([int]$r.X, [int]$r.Y, 0, 0, $bmp.Size); $g.Dispose()
    $pfad = Join-Path $Out $name; $bmp.Save($pfad, [System.Drawing.Imaging.ImageFormat]::Png); $bmp.Dispose()
    Write-Host "  Foto $name ($([int]$r.Width)x$([int]$r.Height))"
}
function Hauptfenster($prozessId) {
    for ($i = 0; $i -lt 60; $i++) {
        $c = New-Object System.Windows.Automation.PropertyCondition($AE::ProcessIdProperty, $prozessId)
        $kinder = $AE::RootElement.FindAll($TS::Children, $c)
        $best = $null
        foreach ($k in $kinder) { if ($k.Current.BoundingRectangle.Width -gt 900 -and $k.Current.Name -like "*SewerStudio*") { $best = $k } }
        if ($best) { return $best }
        Warte 1
    }
    throw "Hauptfenster nicht gefunden"
}
function SichtbareZeilen($grid) {
    $gr = $grid.Current.BoundingRectangle
    $rows = $grid.FindAll($TS::Children, (New-Object System.Windows.Automation.PropertyCondition($AE::ControlTypeProperty, [System.Windows.Automation.ControlType]::DataItem)))
    $n = 0
    foreach ($row in $rows) { $rr = $row.Current.BoundingRectangle; if (-not $row.Current.IsOffscreen -and $rr.Height -gt 0 -and $rr.Bottom -le ($gr.Bottom + 1)) { $n++ } }
    return $n
}
function Schliesse($proc, $fenster) {
    try { ($fenster.GetCurrentPattern([System.Windows.Automation.WindowPattern]::Pattern)).Close() } catch { }
    for ($i = 0; $i -lt 20; $i++) {
        if ($proc.HasExited) { return "sauber beendet" }
        Warte 1
        # Nachfrage "Aenderungen verwerfen?" -> Nein / Nicht speichern
        $c = New-Object System.Windows.Automation.PropertyCondition($AE::ProcessIdProperty, $proc.Id)
        foreach ($w in $AE::RootElement.FindAll($TS::Children, $c)) {
            foreach ($txt in @("Nicht speichern", "Nein", "Verwerfen")) { $b = Suche $w $txt ([System.Windows.Automation.ControlType]::Button); if ($b) { Klick $b; break } }
        }
    }
    if (-not $proc.HasExited) { $proc.Kill(); return "erzwungen beendet" }
    return "beendet nach Dialog"
}

# ---------- Start ----------
$proc = Start-Process -FilePath $Exe -WorkingDirectory (Split-Path -Parent $Exe) -PassThru
Warte 6
$fenster = Hauptfenster $proc.Id
Warte 6
[Win32]::SetForegroundWindow((New-Object IntPtr ($fenster.Current.NativeWindowHandle))) | Out-Null
Warte 1
$r0 = $fenster.Current.BoundingRectangle
Notiere "fenster" ("{0}x{1} Titel={2}" -f [int]$r0.Width, [int]$r0.Height, $fenster.Current.Name)
Foto $fenster ("p{0}-00-start.png" -f $Phase)

# Projekt oeffnen: Eintrag in der Uebersicht waehlen, dann Oeffnen
$eintrag = Suche $fenster "Nova-Sichtprobe"
if ($eintrag) { Klick $eintrag; Warte 0.5 }
$oeffnen = Suche $fenster "Öffnen" ([System.Windows.Automation.ControlType]::Button)
Notiere "knopf_oeffnen_gefunden" ($null -ne $oeffnen)
if ($oeffnen) { Klick $oeffnen } elseif ($eintrag) { Klick $eintrag; Klick $eintrag }
Warte 5
$fenster = Hauptfenster $proc.Id
Notiere "projekt_offen" $fenster.Current.Name

# Haltungen ueber die Leiste (nicht die gleichnamige Kennzahl der Uebersicht)
$leiste = SucheId $fenster "SidebarNavList"
$nav = $null
if ($leiste) { $nav = Suche $leiste "Haltungen" } else { $nav = Suche $fenster "Haltungen" ([System.Windows.Automation.ControlType]::ListItem) }
if (-not $nav) { throw "Navigation Haltungen nicht gefunden" }
Klick $nav; Warte 3
Foto $fenster ("p{0}-01-haltungen-1366x768.png" -f $Phase)

$grid = SucheId $fenster "Grid"
$drawer = SucheId $fenster "FelderDrawer"
$ueb = SucheId $fenster "Uebersicht"
if ($grid) { Notiere "sichtbare_zeilen" (SichtbareZeilen $grid); Notiere "tabelle_hoehe" ([int]$grid.Current.BoundingRectangle.Height) }
if ($drawer) { Notiere "eingabefelder_hoehe" ([int]$drawer.Current.BoundingRectangle.Height) }
$toggle0 = Suche $fenster "Eingabefelder auf- oder zuklappen"
if ($toggle0) { try { Notiere "eingabefelder_offen" (($toggle0.GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern)).Current.ToggleState) } catch { } }
if ($ueb) { Notiere "uebersicht_breite" ([int]$ueb.Current.BoundingRectangle.Width) }
$kompakt = Suche $fenster "Kompakt"
if ($kompakt) { try { Notiere "chip_kompakt_aktiv" (($kompakt.GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern)).Current.ToggleState) } catch { Notiere "chip_kompakt_aktiv" "kein TogglePattern" } }

function Offen() { $t = Suche $fenster "Eingabefelder auf- oder zuklappen"; return (($t.GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern)).Current.ToggleState -eq "On") }
function SetzeOffen($soll) { if ((Offen) -ne $soll) { Klick (Suche $fenster "Eingabefelder auf- oder zuklappen"); Warte 1.2 } }

if ($Phase -eq 2) {
    # Nach dem Neustart: Spaltenansicht und Uebersichtsbreite oben gemessen; jetzt die gespeicherte Hoehe der Eingabefelder
    $rows = $grid.FindAll($TS::Children, (New-Object System.Windows.Automation.PropertyCondition($AE::ControlTypeProperty, [System.Windows.Automation.ControlType]::DataItem)))
    if ($rows.Count -ge 3) { Klick $rows.Item(2) (-150) 0; Warte 1.5 }
    SetzeOffen $true
    $drawer = SucheId $fenster "FelderDrawer"
    Notiere "eingabefelder_hoehe_nach_neustart_geoeffnet" ([int]$drawer.Current.BoundingRectangle.Height)
    Notiere "sichtbare_zeilen_geoeffnet" (SichtbareZeilen (SucheId $fenster "Grid"))
    Foto $fenster ("p{0}-02-nach-neustart-geoeffnet.png" -f $Phase)
}

if ($Phase -eq 1) {
    # Dritte Zeile waehlen -> Uebersicht und Eingabefelder folgen
    $rows = $grid.FindAll($TS::Children, (New-Object System.Windows.Automation.PropertyCondition($AE::ControlTypeProperty, [System.Windows.Automation.ControlType]::DataItem)))
    if ($rows.Count -ge 3) { Klick $rows.Item(2) (-150) 0; Warte 1.5 }
    Foto $fenster ("p{0}-02-zeile3-uebersicht.png" -f $Phase)
    $ueb = SucheId $fenster "Uebersicht"
    $texte = @(); if ($ueb) { foreach ($t in $ueb.FindAll($TS::Descendants, (New-Object System.Windows.Automation.PropertyCondition($AE::ControlTypeProperty, [System.Windows.Automation.ControlType]::Text)))) { $texte += $t.Current.Name } }
    Notiere "uebersicht_texte" (($texte | Select-Object -First 14) -join " | ")

    # Zuklappen / Aufklappen gezielt: zuerst zu, dann auf (Endzustand offen fuer die Feldsuche)
    SetzeOffen $false
    $drawer = SucheId $fenster "FelderDrawer"
    Notiere "eingabefelder_hoehe_zu" ([int]$drawer.Current.BoundingRectangle.Height)
    Notiere "sichtbare_zeilen_zu" (SichtbareZeilen (SucheId $fenster "Grid"))
    Foto $fenster ("p{0}-03-eingabefelder-zu.png" -f $Phase)
    SetzeOffen $true
    $drawer = SucheId $fenster "FelderDrawer"
    Notiere "eingabefelder_hoehe_auf" ([int]$drawer.Current.BoundingRectangle.Height)
    Notiere "sichtbare_zeilen_auf" (SichtbareZeilen (SucheId $fenster "Grid"))
    Foto $fenster ("p{0}-03b-eingabefelder-auf.png" -f $Phase)

    # Feldsuche
    $suche = Suche $fenster "Feld suchen" ([System.Windows.Automation.ControlType]::Edit)
    if ($suche) {
        ($suche.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern)).SetValue("Baujahr"); Warte 1.2
        Foto $fenster ("p{0}-04-feldsuche-baujahr.png" -f $Phase)
        $drawer = SucheId $fenster "FelderDrawer"
        $edits = $drawer.FindAll($TS::Descendants, (New-Object System.Windows.Automation.PropertyCondition($AE::ControlTypeProperty, [System.Windows.Automation.ControlType]::Edit)))
        Notiere "feldsuche_baujahr_editfelder" ($edits.Count - 1)
        ($suche.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern)).SetValue(""); Warte 0.8
    }

    # Weitere Aktionen
    $wa = Suche $fenster "Weitere Aktionen" ([System.Windows.Automation.ControlType]::Button)
    if ($wa) {
        Klick $wa; Warte 1.2
        Foto $fenster ("p{0}-05-weitere-aktionen.png" -f $Phase)
        $c = New-Object System.Windows.Automation.PropertyCondition($AE::ProcessIdProperty, $proc.Id)
        $items = @()
        foreach ($w in $AE::RootElement.FindAll($TS::Children, $c)) { foreach ($m in $w.FindAll($TS::Descendants, (New-Object System.Windows.Automation.PropertyCondition($AE::ControlTypeProperty, [System.Windows.Automation.ControlType]::MenuItem)))) { $items += $m.Current.Name } }
        Notiere "weitere_aktionen_punkte" (($items | Where-Object { $_ }) -join " | ")
        [System.Windows.Forms.SendKeys]::SendWait("{ESC}"); Warte 0.6
    }

    # Spaltenansicht Kompakt
    if ($kompakt) {
        Klick $kompakt; Warte 1.2
        Foto $fenster ("p{0}-06-spaltenansicht-kompakt.png" -f $Phase)
        $grid = SucheId $fenster "Grid"
        $koepfe = @(); foreach ($h in $grid.FindAll($TS::Descendants, (New-Object System.Windows.Automation.PropertyCondition($AE::ControlTypeProperty, [System.Windows.Automation.ControlType]::HeaderItem)))) { if (-not $h.Current.IsOffscreen) { $koepfe += $h.Current.Name } }
        Notiere "kompakt_spalten" ($koepfe -join " | ")
    }

    # Trennlinien verschieben
    $sp1 = Suche $fenster "Höhe der Eingabefelder"
    if ($sp1) { Ziehe $sp1 0 (-70); $drawer = SucheId $fenster "FelderDrawer"; Notiere "eingabefelder_hoehe_nach_ziehen" ([int]$drawer.Current.BoundingRectangle.Height) }
    $sp2 = Suche $fenster "Breite der Übersicht"
    if ($sp2) { Ziehe $sp2 (-60) 0; $ueb = SucheId $fenster "Uebersicht"; Notiere "uebersicht_breite_nach_ziehen" ([int]$ueb.Current.BoundingRectangle.Width) }
    Notiere "sichtbare_zeilen_nach_ziehen" (SichtbareZeilen (SucheId $fenster "Grid"))
    Foto $fenster ("p{0}-07-trennlinien-verschoben.png" -f $Phase)

    # Haltungsansicht-Toggle
    $ha = Suche $fenster "Haltungsansicht"
    if ($ha) {
        Klick $ha; Warte 2
        Foto $fenster ("p{0}-08-haltungsansicht.png" -f $Phase)
        $drawer = SucheId $fenster "FelderDrawer"
        Notiere "haltungsansicht_eingabefelder_sichtbar" ($(if ($drawer) { -not $drawer.Current.IsOffscreen } else { "nicht im Baum" }))
        Klick $ha; Warte 2
        Notiere "sichtbare_zeilen_zurueck" (SichtbareZeilen (SucheId $fenster "Grid"))
    }
}

Notiere "beendet" (Schliesse $proc $fenster)
$bericht.ende = (Get-Date).ToString("s")
$bericht | ConvertTo-Json -Depth 5 | Out-File -Encoding utf8 (Join-Path $Out ("bericht-phase{0}.json" -f $Phase))
Write-Host "Phase $Phase fertig"
