# Startanimation 5.0 — Entscheid und Umsetzung (14.09.2026)

## Entscheid

Pascal hat aus vier rohen Vorschau-Fassungen (`startanimation-5-0.html`, im Browser
oeffnen; Knoepfe A/B/C/D, «Nochmals abspielen», Zeitlupe, Klick auf die Flaeche =
Ueberspringen) zuerst die Kugel gewaehlt und die zurueckhaltende Fassung C als
«deutlich zu wenig neu» zurueckgewiesen. Umgesetzt ist die **Variante D «Kugel, deutlich
neu»**: dieselbe KI-Kugel, aber mit erzaehltem Aufbau. Zusaetzlich faellt die Ueberzeile
«KI-GESTUETZTE KANALINSPEKTION» ueber der Wortmarke weg (Entscheid Pascal 14.09.2026).
Die Kamerafahrt (A) und das Leitungsnetz (B) bleiben als Vorschau erhalten.

Fest aus frueheren Entscheiden: hell, Nova-Farben, kein Gold, kein Dunkel, Versions-Chip
nur aus `AppIdentity`, ganzer Bildschirm, per Klick oder Taste ueberspringbar.

## Choreografie (Sekunden seit Start der Animationsuhr)

| Zeit | Was passiert | Regel |
| --- | --- | --- |
| 0,05 – 1,85 | Die Knoten fliegen von weit aussen (2,0 bis 3,3 Kugelradien, alle Himmelsrichtungen) mit Leuchtspur ein; jeder braucht 0,8 s, Start in Spiralreihenfolge | `EinflugFortschritt`, `EinflugRichtung`, `EinflugAbstand` |
| 0 – 2,0 | Die Kugel waechst von 72 auf 100 Prozent | `KugelMassstab` |
| 0,3 / 0,5 / 0,7 | Aussen-, Mittel-, Innenring zeichnen sich in 1,4 s als wachsender Bogen; der Kern glueht ab 0,4 s auf | `RingBogen`, `RingSichtbarkeit`, `KernGluehen` |
| ab Ankunft | Eine Verbindung erscheint in 0,25 s, sobald beide Enden da sind, und blitzt dabei auf | `VerbindungSichtbarkeit`, `VerbindungBlitz` |
| ab 1,9 | Impulse und Aufleuchten laufen | `ImpulseErlaubt` |
| 3,0 – 4,5 | Scanwelle von links nach rechts mit Scanline | `Wellenfortschritt` |
| 5,0 – 6,2 | Ringwelle vom Kern nach aussen; Knoten leuchten auf, wenn sie passiert werden | `Ringwellenfortschritt` |
| 8,4 / 10,4 / … | Scan- und Ringwelle wiederholen sich im Takt 5,4 s, nur solange das Programm noch laedt | beide |
| Bereit | Gruene Ringwelle laeuft in 0,9 s nach aussen; was sie passiert hat, bleibt gruen (Knoten, Verbindungen, Ringe), dazu gruener Impulsstoss und Kernpuls wie bisher | `Bereitwelle` |

Der Bereit-Moment kommt im Programm fruehestens nach 8 s (`MinimumDisplayTime`), weil
der Balken bei 90 % auf das echte Bereitsein wartet. In der Vorschau war er bei 7,4 s
fest eingezeichnet.

## Was sich im Code geaendert hat

- `Views/Windows/StartupSplashChoreografie.cs`: reine Zeitregeln ohne WPF (siehe Tabelle),
  neben der bestehenden `StartupSplashAnimationPolicy`.
- `StartupSplashWindow.Animation.cs`: Knoten werden je Bild zwischen ihrem Startpunkt
  weit aussen und dem Platz auf der Kugel interpoliert; je Knoten eine `Line` als
  Leuchtspur (`UpdateLeuchtspur`). Zwei Kreise fuer Ringwelle und Bereit-Welle
  (`BuildWellenkreise`, `SetzeWellenkreis`). Ringe zeichnen sich ueber
  `StrokeDashArray` als Bogen (`UpdateRing`; Bitmap-Cache waehrend des Bogens aus).
  Kein Storyboard mehr auf Kern, Ringen, Knoten oder Linien: `RenderFrame` und
  `UpdateBackdrop` setzen Deckkraft und Farbe je Bild. Grund: ein haltendes
  WPF-Storyboard auf `Opacity` ueberstimmt jeden spaeteren direkten Wert.
- `StartupSplashWindow.xaml.cs`: neue Puffer (`_trails`, `_prevX/_prevY`,
  `_nodeGruen`, `_ringBogenFertig`, `_ringwelleT`), `_bereitSeit` in `TriggerReadyBurst`;
  Aufruf der Ueberzeile entfernt.
- `StartupSplashWindow.xaml`: Ueberzeile entfernt.
- Unveraendert: Kugel mit 112 Knoten, Satelliten, Staub, Akzentbogen, Scanline, rechte
  Haelfte (Wortmarke, Untertitel, Chips, Statusmeldungen), Fortschrittsbalken, Ueberspringen.

## Nachweise

- `StartupSplashChoreografieTests`: 33 Pruefungen (Einflug in Spiralreihenfolge bis
  1,85 s, weicher Anfang/Ende, Startpunkte in allen acht Himmelsrichtungen, Kugelwachstum,
  Verbindungen erst nach Ankunft beider Enden samt Blitz, Bogenringe, Kern, Impulssperre
  bis 1,9 s, Scanwelle 3,0 s und alle 5,4 s, Ringwelle 5,0 s und alle 5,4 s, Bereit-Welle).
  Rot gesehen vor der Umsetzung (Regeln fehlten), danach gruen.
- `dotnet test --filter StartupSplash`: 56 gruen, 1 planmaessig uebersprungen (die
  Kindprozess-Haelfte des isolierten Versions-Chip-Tests; die Elternhaelfte baut das
  Fenster in einem eigenen WPF-Prozess auf und ist gruen).
- Build des UI-Testprojekts in `.tmp/testout-splash`, weil ein laufendes SewerStudio
  seine Programmdatei unter `bin\Debug` sperrt. Erst nach dem Schliessen wurde
  `bin\Debug` neu gebaut; vorher zeigte ein Neustart noch die alte Fassung (real passiert).
- Nicht gemessen: Sichtprobe im laufenden Programm. Die macht Pascal beim naechsten
  Programmstart.
- Nicht von dieser Aenderung: `DesignAuditNovaHaltungenTests.Das_Abo_des_Sprungs_von_aussen_ist_symmetrisch`
  war am 14.09. rot, weil `DataPage.AufklappListe.cs` in einer parallelen Sitzung
  ungespeichert umgebaut wird.

## Grenzen der Vorschau

Die Vorschau ist eine Browser-Nachbildung mit 96 Knoten, ohne Satelliten und Staub. Sie
zeigt die Zeitplanung, nicht die exakte Zeichnung des Programms.
