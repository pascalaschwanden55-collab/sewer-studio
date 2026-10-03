# Teststabilität – 02.10.2026

## Kleines Paket aus aktuellem master

Acht Testdateien wurden gezielt stabilisiert. Produktcode, Datenformate und
Pakete bleiben unverändert. Vorherige uncommittete Wartbarkeitswellen aus der
älteren Hauptkopie gehören nicht zu diesem Paket.

- **Prozessstart:** Der eigene PowerShell-Testprozess meldet seine Bereitschaft
  und wartet auf Eingabe. Bei fehlgeschlagenem Start werden Eingabe, Prozess und
  Handles auch bei einem weiteren Fehler begrenzt freigegeben.
- **PDF-Kennung:** Die Prüfung verschiedener Dokument-IDs verwendet festgelegte
  Eingaben. Sie hängt nicht mehr von einer Wartezeit von 20 ms ab. Die vorhandene
  Prüfung des extrahierten Seiteninhalts bleibt erhalten.
- **Befehlssperre:** Signale bestätigen Beginn und Abschluss. Ein zweiter Aufruf
  bleibt während des ersten gesperrt; die Prüfung wartet auf das Freigabeereignis.
- **Spiegeldatei:** Ein zwischen Existenzprüfung und Lesen auftretender Dateifehler
  gilt im vorhandenen Wartehelfer als noch nicht fertiger Stand.
- **Unlesbarer Trainingsbestand:** Alle Leser-/Backupsperren bleiben bis zum
  erwarteten Fehler gehalten. Hauptdatei und Backup müssen danach bytegleich sein.
- **Atomare Ersetzung:** Eine echte Windows-Lesersperre löst den ersten
  Ersetzungsfehler aus. Der injizierbare Retry-Callback gibt den Leser dann frei.
  Anschließend müssen beide Datensätze über die Fassade lesbar sein. Dies prüft
  die Ersetzung, nicht einen vollständigen Speichervorgang bei dauerhaft offenem Leser.
- **Dossier-Sperre:** Die erste Sperre bleibt nachweislich gehalten. Ein eigener
  zweiter Thread wird erst nach beobachtetem Wartezustand abgebrochen. Freigabe
  und begrenztes Join laufen auch im Fehlerfall. Der Threadzustand beweist nicht
  die genaue interne Mutex-Wartestelle.
- **XTF-Fenster:** Ein Dispatcher-Timer beendet das Warten nach der Frist, ohne
  auf ApplicationIdle angewiesen zu sein. Der Kindprozess prüft auch Timeout und
  anschließende Dispatcher-Fortsetzung. Fenster, Anwendung und Kontext werden
  im finally freigegeben. Der Bildordner wird in einer frischen Arbeitskopie angelegt.

## Befunde während der Prüfung

Die unabhängige Gegenprüfung fand einen Cleanupfehler am neuen Testprozess:
Ein Fehler beim Beenden hätte die Handlefreigabe übersprungen und den Startfehler
verdeckt. Die Korrektur gibt Dispose im finally frei und erhält beide Fehler.

Der erste gezielte Lauf widerlegte die Annahme, FileShare.Delete erlaube in diesem
Windows-Test eine Ersetzung bei dauerhaft offenem Leser. Der korrigierte Test
prüft deshalb den tatsächlichen Retry nach Freigabe. Der XTF-Lauf fand zudem
den bisher vorausgesetzten, aber in frischen Arbeitskopien fehlenden Bildordner.
Beide Befunde wurden vor der Lieferung korrigiert.

## Grenzen und weitere Arbeit

Die Suche betraf kurze feste Pausen, sofortiges Lesen und Testfreigaben in den
.NET-Testprojekten sowie die sichtbaren Wartefälle in Sidecar/Trainingsskripten.
Das ist kein Nachweis, dass alle zeitabhängigen Tests beseitigt sind. Der bereits
auf master korrigierte CodingReplay-Fall wurde nicht erneut verändert. Der
Sidecar-VRAM-Zeitfall bleibt ein getrenntes Folgepaket.

Vor dem Commit gelten der vollständige Release-Build und die vier vollständigen
.NET-Testprojekte. Der unveränderte Pre-Push-Hook prüft zusätzlich seinen eigenen
Testweg. Eine unabhängige Gegenprüfung findet vor dem Push statt.

## Gemessener Release-Endstand

Vollständiger Build: **0 Warnungen, 0 Fehler**.

| Prüfung | Bestanden | Übersprungen | Fehler |
|---|---:|---:|---:|
| Infrastruktur | 8.199 | 6 | 0 |
| Pipeline | 3.048 | 3 | 0 |
| Oberfläche, einschließlich isolierter Fensterläufe | 7.894 | 48 | 0 |
| Projektmodernisierung | 62 | 0 | 0 |
| **Gesamt** | **19.203** | **57** | **0** |

Die gezielten Endläufe davor bestanden mit 74 Infrastrukturtests und sechs
Oberflächentests. Der zusätzliche Kindprozess-Einstieg ist im Elternlauf bewusst
übersprungen; das tatsächliche XTF-Fensterszenario wurde über den Elternfall gestartet.
Die Quellprüfsummen der acht Testdateien wurden vor und nach dem vollständigen
Release-Lauf abgeglichen. TRX-Dateien und Laufprotokolle bleiben lokal erhalten.

Die abschließende unabhängige Prüfung fand keine weiteren belegten Fehler.
Die Verbindung von SaveInternalAsync zum Ersetzungshelfer wurde gelesen und
bestätigt; der direkte Helfertest allein schützt diese Verbindung nicht.
