# Startanimation 5.0 — Entscheid und Umsetzung (14.09.2026)

## Entscheid

Pascal hat aus drei rohen Vorschau-Fassungen (`startanimation-5-0.html`, im Browser
oeffnen; Knoepfe A/B/C, «Nochmals abspielen», Zeitlupe, Klick auf die Flaeche =
Ueberspringen) die **Variante C «Kugel, straffer»** gewaehlt: Die KI-Kugel bleibt das
Motiv, nur die Zeitplanung wird fest und straffer. Die Kamerafahrt (A) und das
Leitungsnetz (B) bleiben als Vorschau erhalten, sind aber nicht umgesetzt.

Fest aus frueheren Entscheiden: hell, Nova-Farben, kein Gold, kein Dunkel, Versions-Chip
nur aus `AppIdentity`, ganzer Bildschirm, per Klick oder Taste ueberspringbar.

## Choreografie (Sekunden seit Start der Animationsuhr)

| Zeit | Was passiert | Regel |
|---|---|---|
| 0,00 / 0,15 / 0,30 | Kernglühen, Aussen-, Mittel-, Innenring blenden je 1,2 s ein | `RingStartMillisekunden`, `RingDauerMillisekunden` |
| 0,15 – 1,50 | Knoten erscheinen in Spiralreihenfolge, je 0,35 s weich | `KnotenSichtbarkeit` |
| 0,70 – 2,05 | Verbindungen folgen ihren Knoten | `VerbindungSichtbarkeit` |
| ab 1,50 | Impulse und Aufleuchten laufen (vorher nicht) | `ImpulseErlaubt` |
| 3,00 – 4,50 | erste Inferenz-Welle mit Scanline | `Wellenfortschritt` |
| 5,40 – 6,90 | zweite Welle | `Wellenfortschritt` |
| 8,10 / 10,80 / … | weitere Wellen im Takt 2,7 s, nur solange das Programm noch laedt | `Wellenfortschritt` |
| Bereit | gruener Impulsstoss und Kernpuls wie bisher, neu dazu 0,56 s gruenes Aufleuchten von Knoten und Verbindungen | `Bereitanteil` |

Der Bereit-Moment kommt im Programm fruehestens nach 8 s (`MinimumDisplayTime`), weil
der Balken bei 90 % auf das echte Bereitsein wartet. In der Vorschau war er bei 7,4 s
fest eingezeichnet.

## Was sich im Code geaendert hat

- Neu `Views/Windows/StartupSplashChoreografie.cs`: reine Zeitregeln ohne WPF, neben der
  bestehenden `StartupSplashAnimationPolicy`.
- `StartupSplashWindow.Animation.cs`: Knoten und Verbindungen bekommen kein Storyboard
  mehr. Ihre Deckkraft setzt `RenderFrame` je Bild aus der Choreografie. Grund: `RenderFrame`
  setzte die Knoten-Deckkraft schon vorher jedes Bild, ein haltendes Storyboard haette diesen
  Wert ueberstimmt. Wellen kommen aus `Wellenfortschritt` statt aus einem Abklingzaehler;
  Impulse und Aufleuchten sind bis 1,5 s gesperrt; Knoten- und Linienfarbe mischen den
  Bereit-Anteil in Gruen.
- `StartupSplashWindow.xaml.cs`: `_bereitSeit` wird in `TriggerReadyBurst` gesetzt; die
  Konstanten `WaveIntervalSeconds`/`WaveDurationSeconds` und `_waveCooldown` sind entfernt.
- Unveraendert: Kugelaufbau (112 Knoten), Ringe, Satelliten, Staub, Akzentbogen, rechte
  Haelfte, Statusmeldungen, Fortschrittsbalken, Ueberspringen.

## Nachweise

- `StartupSplashChoreografieTests`: 25 Pruefungen (Spiralreihenfolge bis 1,5 s, weiche
  Einblendung, Verbindungen ab 0,7 s, Impulssperre, feste Wellen 3,0/5,4 s, Wiederholung
  alle 2,7 s, Ringstaffelung, gruener Bereit-Anteil). Rot gesehen vor der Umsetzung
  (Klasse fehlte), danach gruen.
- `dotnet test --filter StartupSplash`: 48 gruen, 1 planmaessig uebersprungen (die
  Kindprozess-Haelfte des isolierten Versions-Chip-Tests; die Elternhaelfte startet den
  Kindprozess und ist gruen).
- Build des UI-Testprojekts in `.tmp/testout-splash`, weil das laufende SewerStudio seine
  Programmdatei unter `bin\Debug` sperrt.
- Nicht gemessen: Sichtprobe im laufenden Programm. Die macht Pascal beim naechsten
  Programmstart (Aufbau in 1,5 s, Wellen bei 3 und 5,4 s, gruenes Aufleuchten am Ende).
- Nicht von dieser Aenderung: `DesignAuditNovaHaltungenTests.Das_Abo_des_Sprungs_von_aussen_ist_symmetrisch`
  war am 14.09. rot, weil `DataPage.AufklappListe.cs` in einer parallelen Sitzung
  ungespeichert umgebaut wird.

## Grenzen der Vorschau

Die Vorschau ist eine Browser-Nachbildung mit 96 Knoten, ohne Satelliten und Staub. Sie
zeigt die Zeitplanung, nicht die exakte Zeichnung des Programms.
