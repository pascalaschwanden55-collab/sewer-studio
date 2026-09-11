# Seitenleiste vereinfacht

Auf Wunsch angepasst: fast weisse, deckende Seitenleiste, mehr Abstand zwischen
den Eintraegen und eine blaue Auswahl mit weisser Schrift. Projektname und
Speicherstand stehen gemeinsam einmal unter dem Logo. Allgemeine Meldungen
bleiben in der vorhandenen Statuszeile.

Der Arbeitsbereich zeichnet seinen eigenen Theme-Hintergrund. Der transparente
Mica-Fensterhintergrund kann damit keinen schwarzen Zwischenraum mehr erzeugen.
Das dunkle Design verwendet weiterhin seine dunkle Palette. Navigation,
KI-Aufklapper und gespeicherte Daten bleiben unveraendert.

Geaendert sind nur MainWindow.xaml und die beiden Theme-Dateien. Es gibt keine
neuen Dienste, Schnittstellen oder Aenderungen am gespeicherten Format.

Pruefung:

- Release-Alltagsbuild: keine Fehler oder Warnungen.
- 292 vorhandene Gestaltungs-, Ressourcen- und Navigationstests bestanden.
- Gesamtes UI-Testprojekt: 6900 bestanden, 18 vorgesehene Kindprozess-Einstiege
  uebersprungen, ein Fehler. `NachschlagKontextmenueTests` erreicht das bekannte
  60-Sekunden-Limit; dieser Fehler ist bereits in CLAUDE.md vom 06.09. dokumentiert.
- Isolierte WPF-Sichtprobe mit kuenstlichem Projekt: hell und dunkel, jeweils
  1920 x 1080 und 1280 x 720. Alle 15 Navigationseintraege vorhanden, die Auswahl
  erreichbar, genau eine Projekt-/Speicherstandanzeige in der Seitenleiste.
- Weiss auf der blauen Auswahl erreicht 5,83:1 im hellen und 5,17:1 im dunklen Design.
- `git diff --check` fuer die drei geaenderten XAML-Dateien bestanden.

Die [Messwerte](sichtpruefung.json) und [Bilder](bilder/) stammen aus dem echten
WPF-Fensterinhalt, ohne den Fensterhintergrund fuer die Aufnahme nachzufaerben.
Die Aufnahmen zeigen die Clientflaeche ohne Windows-Titelleiste. Die Mindesthoehe
der Leiste wurde nicht verkleinert: Bei einem kleinen Fenster ist die Liste scrollbar.
Windows-Skalierungen wurden in dieser Probe nicht gewechselt.

Das bereits laufende SewerStudio aus dem Debug-Ordner wurde nicht beendet oder
ersetzt. Die gepruefte neue Fassung liegt im Release-Ordner.

## Nachkorrektur: Ecke zwischen Menue und Seitenleiste

Das obere Menue hatte eine eigene, etwas groessere Breite und einen unteren Rand.
Dadurch entstand ein Versatz am Uebergang zur Seitenleiste. `ShellMenu` bindet
seine Breite jetzt an `SidebarPanel`; beide verwenden dieselbe Hintergrundfarbe
und dieselbe rechte Randlinie. Die versetzte Querlinie entfaellt. Die drei
Menuepunkte haben etwas weniger Innenabstand und bleiben in einer Zeile.

Erneut geprueft: Release-Alltagsbuild ohne Fehler/Warnungen, 292 gezielte Tests
bestanden, WPF-Sichtprobe hell/dunkel bei beiden Fenstergroessen bestanden.
Die rechte Kante liegt in allen vier Faellen bei 220 WPF-Einheiten, der Uebergang
ist lueckenlos, kein Menuepunkt bricht um oder ragt ueber die Leiste hinaus.
Die Ecke wurde auf den gerenderten Bildern nochmals angesehen. Die Gesamtprobe
mit 6900 bestandenen Tests und dem bekannten Kontextmenue-Zeitfehler oben stammt
vom Stand vor dieser reinen Menue-Anpassung und wurde nicht erneut ausgefuehrt.

[Korrigierte Ecke, hell](bilder/Light-1920x1080-ecke.png) ·
[Korrigierte Ecke, dunkel](bilder/Dark-1280x720-ecke.png)
