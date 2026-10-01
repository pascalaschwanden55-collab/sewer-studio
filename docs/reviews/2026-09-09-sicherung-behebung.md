# Sicherung und PC-Ausfallschutz: Behebung

Stand: 09.09.2026. Grundlage: [Sicherungsaudit](2026-09-09-sicherung-ausfallschutz.md).

## Ergebnis

Die vier gefundenen Lücken sind im Programm behoben. Änderungen an Kundenprojekten waren dafür nicht erforderlich.

| Befund | Korrektur |
|---|---|
| Schachtfelder und Reihenfolge wurden nicht automatisch gespeichert | Beide Wege starten den vorhandenen Speichertimer. Ein ausstehender Timer bleibt an sein Projekt gebunden. |
| Beschädigte Sicherung konnte bei gleicher Grösse und Änderungszeit als gültig gelten | Auch solche Dateien werden inhaltlich verglichen. Eine abweichende Kopie wird neu aus der Quelle erstellt. |
| Videos und externe Quellen waren nicht vollständig abgedeckt | Videos sind aktiviert. Externe Dateiverweise aus `projekt.json` werden einzeln aufgenommen. Weitere Ordner sind in den Einstellungen erfassbar. |
| Unterbrochene Sicherung konnte einen gemischten Stand hinterlassen | Dauerhafte Vorherkopien ermöglichen das Zurücksetzen. Ein offener Lauf wird bei der Inhaltsprüfung nicht freigegeben. |

Zusätzlich bleiben bereits gesicherte Videos bei späterer Abwahl erhalten. Sie werden dann nicht aktualisiert. Ein Lauf ohne geänderte Nutzdaten verdrängt keine alte Dateiversion. Neue Versionsnamen bleiben auch bei zurückgestellter PC-Uhr zeitlich hinter den bisherigen Ständen. Zwei neue Sicherungsläufe auf dasselbe Ziel werden durch eine exklusive Dateisperre verhindert.

## Auf diesem PC eingerichtet

- Projektvideos aktiviert; bisherige Einstellungen vorher separat gesichert.
- `D:\QGIS_V4.2\GeoShop` als zusätzliche Sicherungsquelle aufgenommen.
- Eigene QGIS-Erweiterungen unter `C:\Users\Besitzer\AppData\Roaming\QGIS\QGIS3\profiles\default\python\plugins` aufgenommen.
- Zusätzliche Quellen liegen in `AppData\Local\SewerStudio\backup-additional-folders.json`. Eine noch laufende alte Version überschreibt diese Datei nicht durch ihren normalen Einstellungsspeicher.

Diese Einstellungen gelten für nachfolgende Sicherungsläufe mit dem aktualisierten Programm. Die bestehende grosse Sicherung auf `G:` wurde bei dieser Codekorrektur nicht neu erstellt oder vollständig überprüft.

## Nachweise

- Vollständiger Release-Build aller Projekte und Werkzeuge im getrennten Ausgabeordner: 0 Fehler, 0 Warnungen. Der getrennte Ordner war nötig, weil der laufende MCP-Hilfsserver seine bestehende Programmdatei sperrt. Er wurde nicht beendet.
- Normaler Release-Build von `AuswertungPro.Dev.slnf`: 0 Fehler, 0 Warnungen.
- Infrastruktur: 6'447 bestanden, 6 bewusst übersprungen, 0 Fehler.
- Pipeline: 2'658 bestanden, 3 bewusst übersprungen, 0 Fehler.
- Oberfläche: 6'949 bestanden, 21 bewusst übersprungen, 0 Fehler.
- ProjectModernizer: 62 bestanden, 0 Fehler.
- Architekturkarte aktualisiert und mit `quick_validate.py` erfolgreich validiert.

Gesamt: **16'116 bestandene Tests, 30 übersprungene Prüfungen, 0 Fehler**.

Die übersprungenen Prüfungen wurden nicht als Erfolg gezählt. Darunter sind optionale Tests mit echten externen Daten und Kindprozesse, deren eigentliche Fensterprüfung separat durch den jeweiligen Elterntest gesteuert wird.

Zusätzliche Ausfallprobe: Ein eigener .NET-Prüfprozess änderte eine künstliche Sicherungsdatei und beendete sich ohne `Dispose` oder Aufräumen. Danach lehnte die Inhaltsprüfung den unterbrochenen Stand ab. Ein anderer Prozess stellte den vorherigen Inhalt ausschliesslich aus dem Dateiprotokoll wieder her. Die anschliessende vollständige Prüfsummenprüfung des künstlichen Bestands bestand.

Weitere Verhaltenstests prüfen:

- Wiederherstellung nach Abbruch mitten im Kopierlauf, einschliesslich unverändertem vorherigem Manifest.
- Erkennen beschädigter Vorherkopien, ohne den aktuellen Zielinhalt zu überschreiben.
- Aufbewahrung alter Videos über mehrere Sicherungsläufe nach ihrer Abwahl.
- Tatsächliches Speichern von Schachtfeldwerten und geänderter Reihenfolge in eine Projektdatei.
- Sicherung eines externen Protokolls und der GeoShop-Zusatzquelle mit dokumentiertem Rückweg.
- Wiederherstellung einer künstlichen Projektdatei samt externem Protokoll, nachdem beide künstlichen Originale entfernt wurden.
- Dauerhafte Zusatzquellen, Dubletten, ungültige Pfade und beschädigte Konfiguration.

Prüfprotokolle liegen unter `.tmp/backup-fix-full-*.log`; die zusätzliche Prozessprobe unter `.tmp/backup-crash-probe.log`. Die dauerhaften Regressionstests liegen in `tests/AuswertungPro.Next.Infrastructure.Tests/Backup/` sowie den Schacht- und Sicherungstests des UI-Testprojekts.

## Technische Umsetzung und Grenzen

`BackupRunJournal` schreibt Vorherkopien und deren Prüfsummen vor der Änderung einer Zieldatei. Das Journal liegt unter `_Versionen/.unterbrochener-lauf`. Auch temporäre Kopien, SQLite-Begleitdateien und das Manifest werden berücksichtigt. Der Abschluss wird dauerhaft geschrieben, bevor Vorversionen freigegeben und ältere Stände ausgedünnt werden. Die Wiederherstellung prüft zunächst alle Vorherkopien und Pfadgrenzen; beschädigte oder fremde Zustände blockieren sie.

Nach einem Prozessausfall setzt der nächste Sicherungslauf den offenen Lauf zurück. Alternativ steht `FullBackupSmoke --recover-backup <Sicherungsordner>` zur Verfügung. Der Sicherungsordner muss dabei am ursprünglichen Pfad liegen. `--verify-restore` bietet danach die vorhandene Inhalts- und Ladeprüfung. Rückwege für Zusatzordner und externe Dateien stehen in `Extras/RESTORE-ANLEITUNG.txt` neu erstellter Sicherungen.

Der Vergleich unveränderter Dateien und die dauerhaften Vorherkopien benötigen zusätzliche Lesezeit und freien Speicher. Bei Platzmangel oder fehlenden Quellen darf die Sicherung keine vollständige Abdeckung vortäuschen. Übersprungene Dateien erzeugen eine Warnung; vorhandene ältere Kopien bleiben erhalten, neu fehlende Dateien sind nicht gesichert.

Dies ist eine Dateisicherung, kein startfähiges Windows-Abbild. Eine vollständige Wiederherstellung auf einem Ersatz-PC und ein echter Stromausfall wurden nicht durchgeführt. .NET, Python und gegebenenfalls Ollama/QGIS müssen auf einem Ersatz-PC weiterhin eingerichtet werden. Die Prüfungen belegen die beschriebenen Softwarefälle, keine absolute Fehlerfreiheit von Hardware und Dateisystem.
