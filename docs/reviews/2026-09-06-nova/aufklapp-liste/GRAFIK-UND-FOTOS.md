# Haltungsübersicht: Grafik und Ereignisfotos

Stand: 10.09.2026, Fotoanzeige auf Vorschaukarte umgestellt.

Die Grafik zeigt die gesamte Haltungslänge auf der verfügbaren Höhe. Die Meterachse
zeigt Zwischenwerte; der Fehler bei Längen wie 16,85 m ist behoben. Rohr und
Flusspfeil verwenden die bestehende Nutzungsfarbe aus den Berichten. Die Ereignisse
stehen mit Klartext und Verbindungslinie neben ihrer tatsächlichen Meterstelle.
Bei vielen Ereignissen wird die Grafik scrollbar; die Schrift bleibt lesbar.
Eckdaten und Schadenliste sind unter der Grafik aufklappbar.

Die Maus kurz über Symbol oder Klartext halten: Nach 350 ms erscheint das Foto
als schwebende Karte ohne separates Bildfenster. Das Mausrad blättert bei mehreren
Fotos. Klick, Enter und Leertaste zeigen ebenfalls diese Karte. Beim Verlassen,
Seitenwechsel oder mit Escape schliesst sie sich. Ohne hinterlegtes Foto gibt es
keine Fotoaktion. Fehlende oder defekte Fotos erhalten einen Texthinweis.
Die Zuordnung bleibt auch bei
Gegenbefahrungen, gleicher Meterstelle und gleichem Code erhalten.

Die Anzeige verändert weder Protokoll noch Kundenfotos. Relative Fotopfade werden
über die bestehende Vorschau-Pfadprüfung zum aktuellen Projekt aufgelöst.

Der bestehende `PhotoHoverPreviewBehavior` unterstützt dafür zusätzlich einzelne
Grafikmarken. `ProjectRootProvider` wird vom Übersichtspanel vererbt und liest den
aktuellen Projektstand. `PhotoHoverPreviewPopup` bleibt die gemeinsame Bildkarte.
Die früheren Öffnungs-Callbacks bleiben im Code kompatibel, werden von der Grafik
aber nicht mehr aufgerufen.

Der neue Verhaltenstest `HaltungsgrafikFotoVorschauTests` verwendet selbst erzeugte
Bilder: Vorschauverzögerung, relative Pfade, Mausrad, Klick, Enter/Escape,
Verlassen, Entladen, Wiederverwenden sowie fehlende und beschädigte Bilder.
Die gezielte Prüfung vom 10.09.2026 besteht mit 54 Tests und einem vorgesehenen
übersprungenen Kindprozess-Einstieg. Der Release-Alltagsbuild hat null Warnungen
und Fehler. Die gesamte Oberflächenprüfung: 6’949 bestanden, 21 übersprungen,
ein Fehler beim bereits dokumentierten 60-Sekunden-Zeitabbruch von
`NachschlagKontextmenueTests.Das_Nachschlagmenue_haengt_an_den_richtigen_Feldern`.
Auch im Gesamtlauf besteht der neue Foto-Verhaltenstest. Nachweise liegen unter
`.tmp/foto-karte-tests.txt`, `.tmp/foto-karte-build.txt` und
`.tmp/foto-karte-ui-gesamt.txt`. Der Architektur-Skill ist abgeglichen und validiert.
Die laufende Debug-Anwendung wurde weder beendet noch ersetzt; die Änderung
braucht dort einen neuen Build nach dem Schliessen. Die folgenden Zahlen sind
die frühere Prüfung vom 08.09.2026.

## Prüfung

| Prüfung | Ergebnis |
|---|---:|
| Release-Alltagsbuild | 0 Warnungen, 0 Fehler |
| Debug-Alltagsbuild für F5 | 0 Warnungen, 0 Fehler |
| Gesamte Oberflächentests | 6’887 bestanden, 18 übersprungen |
| Gesamte Infrastrukturtests | 6’316 bestanden, 6 übersprungen |
| Gesamte Pipeline-Tests | 2’648 bestanden, 3 übersprungen |
| Isolierte Bild-/Bedienprobe, hell und dunkel | beide erfolgreich |

Gezielt geprüft: volle Länge und Zwischenstriche, Größenwechsel, Nutzungsfarben,
dichte Klartexte ohne Überlappung, unveränderte Protokolldaten, Klick auf Symbol und
Klartext, mehrere Ereignisse an derselben Stelle, fehlende Fotos und Öffnungsfehler,
Fotozuordnung nach Gegenbefahrung. Der Windows-Öffnungsdienst wurde dabei durch
einen aufzeichnenden Testdienst ersetzt; es wurde kein Kundenfoto geöffnet.

Die Bildprobe nutzt künstliche Daten und ein eigenes Profil. Geprüfte Bilder und
Laufprotokolle liegen lokal in `.tmp/grafik-massstab/`, insbesondere
`Light-auf.png`, `Dark-auf.png`, `ui-gesamt.txt`, `infrastruktur-gesamt.txt` und
`pipeline-gesamt.txt`. Dies ist eine Prüfung dieser Änderung, keine neue
Vollabnahme aller Funktionen des Programms. Nicht committet oder hochgeladen.
