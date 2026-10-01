# Nachprüfung Nova-WPF-Etappe 1

6. September 2026 · Branch `feature/nova-etappe-1` · Commit `a09d414ec14d1ec9eac361bb79a4b68d285212ad` · Basis `69fd0a671`.

**Empfehlung: Option 3 – Branch vorerst behalten.** Die neun Commits sind vorhanden und der Nova-Arbeitsbaum ist sauber. Vor dem Zusammenführen müssen die Datenbearbeitung korrigiert und die offenen Bedienprüfungen abgeschlossen werden. Kein Merge, Push oder Produktcode-Eingriff wurde vorgenommen.

## Befunde

| Nr. | Priorität | Befund | Nachweis |
| --- | --- | --- | --- |
| W01 | Hoch | Eine Formulareingabe kann neuere Änderungen aus der Tabelle überschreiben. | Quelltext und isolierte Gegenprobe mit Produktklassen |
| W02 | Mittel | „Analyse bereit“ ist nicht an die Bereitschaft der KI gebunden. | Quelltext: Status hängt nur an Hardwaresensoren |
| W03 | Mittel | Zuklappen der Eingabefelder gibt keinen Tabellenplatz frei. | Quelltext und isolierte WPF-Messung: 220 px bleiben 220 px |

### W01 – Formular zeigt veraltete Werte

Die neue Arbeitsfläche zeigt Tabelle und Formular gleichzeitig. `AktualisiereFelderDrawer` erstellt eine Momentaufnahme der Feldwerte. Neu aufgebaut wird sie bei einer anderen ausgewählten Haltung oder bestimmten QGIS-/Katasterereignissen. Änderungen am bereits gewählten Datensatz sind nicht angeschlossen.

Die Gegenprobe beginnt mit „Alt“ im Feld `Bemerkungen`. Danach wird der Datensatz auf „Neue Tabellenkorrektur“ geändert. Das Formularmodell zeigt weiterhin „Alt“. Ein Zusatz im Formular speichert anschliessend „Alt + Zusatz im Formular“. Die neue Tabellenkorrektur ist verloren.

Verwendet wurden `HaltungRecord`, `DataPageDetailItemFactory`, `RecordDetailItem` und die echte Methode `CommitHaltungDetailField`. Ein ViewModel, Autosave und Projektladen waren nicht aktiv. Der Lauf belegt den Mechanismus; er ersetzt keinen vollständigen Klicktest am Programm.

Code im Nova-Arbeitsbaum:

- `src/AuswertungPro.Next.UI/Views/Pages/DataPage.NovaWorkspace.cs:38`: Erstellung des Formulars.
- `src/AuswertungPro.Next.UI/Views/Pages/DataPage.xaml.cs:132`: Änderungsereignisse; ab Zeile 168 nur Auswahlwechsel.
- `src/AuswertungPro.Next.UI/DataPage/DataPageDetailItemFactory.cs:38`: Feldwert wird kopiert.
- `src/AuswertungPro.Next.UI/Views/Windows/RecordDetailsModels.cs:154`: eigener Wert mit sofortigem Schreibaufruf.
- `src/AuswertungPro.Next.UI/Views/Pages/DataPage.xaml.cs:610`: Rückschreiben; danach Autosave bei vorhandenem ViewModel.
- Bereits Zeile 538 erklärt, weshalb das alte Detailfenster wegen genau dieses Risikos nur mit gesperrter Parallelbearbeitung geöffnet wird.

**Korrektur:** Einen gemeinsamen aktuellen Bearbeitungsstand verwenden. Externe Feldänderungen gezielt übernehmen, ohne gerade bearbeitete Texte unbemerkt zu ersetzen. Vor dem Schreiben einen inzwischen geänderten Ausgangswert erkennen. Ereignisse beim Haltungswechsel sauber abmelden. Nicht das gesamte Formular bei jedem Tastendruck neu aufbauen.

**Abnahme:** Tabelle → Formular, Formular → Tabelle, Feldänderung durch einen Dienst und Haltungswechsel prüfen. Der obige Ablauf darf keine neuere Korrektur verlieren.

### W02 – Bereitschaftsanzeige ist fachlich falsch angeschlossen

`MainWindow.xaml:421–428` setzt den Pulspunkt dauerhaft aktiv und standardmässig „Analyse bereit“. Nur `Monitor.IsSensorBlocked` schaltet auf „Prüfung nötig“. Laut `Services/SystemMonitorService.cs:62` beschreibt dieser Wert blockierte Hardwaresensoren. Er prüft weder Modelle noch KI-Dienste.

Eine Bereitschaftsmeldung ohne geprüfte KI-Bereitschaft ist damit möglich. Umgekehrt kann ein fehlender Temperatursensor eine unnötige Prüfmeldung auslösen. Das ist bereits im Umsetzungsplan vorgesehen: eine Schwäche des Plans, keine Abweichung Claudes davon.

**Korrektur:** Den Aufklapper zunächst neutral „Systemleistung“ nennen; Hilfetext und Pulspunkt passend gestalten. Eine echte Bereitschaftsanzeige an die für die jeweilige Analyse nötigen Prüfungen anschliessen. Sensorprobleme getrennt melden.

### W03 – Zuklappen lässt die Fläche stehen

`Views/Pages/Haltungsansicht/HaltungFelderDrawer.xaml:15` schaltet nur den Inhalt des Scrollbereichs ab (Zeile 40). Die umgebende Zeile in `Views/Pages/DataPage.xaml:427` behält ihre feste Höhe und Mindesthöhe. In einer isoliert aufgebauten DataPage betrug sie vor und nach dem Zuklappen jeweils 220 px. Das Fenster wurde nicht gestartet; die Messung erfolgte ohne ViewModel.

**Korrektur:** Beim Zuklappen die Zeile auf die Kopfhöhe verkleinern und die Trennlinie ausblenden. Beim Öffnen die vorherige zulässige Höhe wiederherstellen.

## Unabhängig geprüft

- Neun Commits, sauberer Nova-Arbeitsbaum und Änderungen seit der genannten Basis.
- Aufbau der neuen Haltungsseite mit den vorhandenen WPF-Ressourcen in einem isolierten Prozess: erfolgreich.
- 42 gezielte Tests aus sieben Testklassen: 42 bestanden, keine übersprungen. Bereiche: Spaltenansichten, Platzberechnung, XAML-Verdrahtung, Feldsuche, Navigationsgruppen, Zustandsfarben, Werkzeugleiste. Namen stehen in der TRX-Datei.
- Die ausführbaren Gegenproben W01 und W03 sowie der Abgleich mit dem Quelltext.

**Grenze:** Die Läufe nutzen vorhandene Debug-Programmdateien; deren SHA-256-Werte sind festgehalten. Ein eigener Neubau zur Zuordnung dieser Dateien zum Commit erfolgte nicht. Die Quelltextbefunde wurden getrennt am genannten Branch geprüft. Die gemeldete vollständige Testserie wurde nicht erneut ausgeführt.

## Plan bis zur Abnahme

1. W01 korrigieren und mit einem dauerhaften Verhaltenstest schützen. W02 und W03 nachbessern.
2. Mit künstlichem Projekt und getrennten Einstellungen bedienen: 1366 × 768, Windows-Skalierung 100/125/150 %, beide Themes, sieben sichtbare Zeilen, sämtliche „Weitere Aktionen“, Ansichtswechsel und gespeicherte Trennlinien nach Neustart. Ein vollständiger isolierter App-Start ist durch diese Nachprüfung noch nicht eingerichtet oder geprüft.
3. Vorgeschriebenen vollständigen Release-Build und alle vier Testprojekte ausführen; Befehle und Ergebnisdateien aufbewahren. `ABNAHME.md` nennt Befehle ohne `-c Release`; im geprüften Nachweisordner liegen keine Laufprotokolle. Das widerlegt die gemeldeten Testzahlen nicht, belegt aber noch keine Release-Abnahme.
4. Laufende Änderungen im Hauptbaum separat prüfen und sichern. Die gemeinsame Änderung an `CLAUDE.md` beim späteren Zusammenführen bewusst abgleichen. Eine Dateinamensüberschneidung ist noch kein bewiesener Merge-Konflikt. Anschliessend den vereinten Stand erneut prüfen.

Ein Niveau „9 von 10“ für SewerStudio ist damit weiterhin nicht belegt. Dieser Bericht bewertet die Nova-Etappe, nicht sämtliche Funktionen des Gesamtprogramms.

## Nachweise

- [HTML-Überblick mit Plan](ueberblick.html)
- [Gegenprobe als JSON](nachweise/gegenprobe.json)
- [Prüfcode](nachweise/Program.cs), [Projektdatei](nachweise/Probe.csproj)
- [42 gezielte Tests als TRX](nachweise/nova-fokussiert.trx)
- [Stand, Hashes und Überschneidung](nachweise/stand.json)

Wiederholung aus dem Hauptordner: `dotnet run --project .tmp/nova-wpf-review/Probe.csproj -c Release --no-restore`. Für einen Wiederaufbau die archivierten Quellen in einen neuen Prüf-Unterordner unter `.tmp` kopieren. Die Pfade in `Program.cs` beziehen sich ausdrücklich auf diese Arbeitsbäume. Die Probe lädt kein Projekt und ruft weder `Application.Run` noch den Anwendungsstart auf.
