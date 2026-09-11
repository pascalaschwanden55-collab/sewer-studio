# Prüfung: Programmsicherung und PC-Ausfallschutz

Nachtrag: Die beschriebenen Softwarelücken wurden anschliessend behoben.
Ergebnisse und verbleibende Prüfgrenzen: [Behebung und Nachweise](2026-09-09-sicherung-behebung.md).

Stand: 09.09.2026, 21:26 Uhr, Europe/Zurich. Reine Prüfung; keine Programmänderung, keine Änderung der Sicherungseinstellungen und keine Wiederherstellung in echte Datenordner.

## Ergebnis

Die Sicherung hat gute Schutzmechanismen. Einen lückenlosen Schutz bei PC-Ausfall kann ich derzeit nicht bestätigen. Drei Befunde sollten zuerst behoben werden: fehlende automatische Speicherung bei Schachteingaben, eine nachgewiesene Lücke bei der Inhaltsprüfung und unvollständige Abdeckung der benötigten Dateien.

## Befunde nach Priorität

### 1. Hoch: Schachteingaben lösen keine automatische Speicherung aus

Normale Änderungen in der Schachttabelle und im Detailformular rufen `MarkProjectDirty()` auf. Diese Methode setzt nur Änderungsdatum und `Project.Dirty`. Sie speichert nicht und startet keinen Speichertimer. Die automatische Speicherung ist im Haltungs-ViewModel angebunden. Ein bereits zufällig laufender Haltungs-Timer kann das gesamte Projekt speichern; darauf darf der Schutz der Schächte nicht angewiesen sein.

Folge: Nach Schachteingaben und einem plötzlichen PC-Ausfall können Änderungen seit dem letzten tatsächlichen Speichern verloren gehen. Die Einstellung „Bei jeder Änderung“ deckt diesen Bedienweg nicht zuverlässig ab. Bis zur Korrektur nach Schachteingaben ausdrücklich speichern.

Belege: [SchaechtePage.xaml.cs:681](../../src/AuswertungPro.Next.UI/Views/Pages/SchaechtePage.xaml.cs#L681), Detailabschluss bei Zeile 840; [DataPageViewModel.cs:598](../../src/AuswertungPro.Next.UI/ViewModels/Pages/DataPageViewModel.cs#L598). Dies ist ein Codebefund, kein Stromausfallversuch am laufenden PC.

### 2. Hoch: Beschädigte Sicherungsdatei kann beim nächsten Lauf als gültig übernommen werden

`DirectoryMirror.IsUnchangedAsync()` akzeptiert gleiche Dateigrösse und exakt gleiche Änderungszeit ohne Inhaltsvergleich. Danach berechnet die Vollsicherung neue Prüfsummen aus dem Sicherungsziel. Sie vergleicht diese Dateien vorher nicht mit den bisherigen Prüfsummen. Damit kann eine bestehende Beschädigung im neuen Prüfverzeichnis als gültig erscheinen.

Mit dem echten Sicherungsdienst und ausschliesslich künstlichen Dateien nachgewiesen:

1. Quelldatei mit Inhalt `GOOD` gesichert.
2. Nur Sicherungskopie auf `EVIL` geändert; Grösse und Änderungszeit erhalten.
3. Bisherige Prüfsumme erkennt die Beschädigung korrekt.
4. Vollsicherung erneut ausgeführt: meldet Erfolg, Sicherung enthält weiterhin `EVIL`, Quelle weiterhin `GOOD`.
5. Prüfung gegen das neue Prüfverzeichnis meldet den beschädigten Inhalt als gültig.

Belege: [DirectoryMirror.cs:382](../../src/AuswertungPro.Next.Infrastructure/Backup/DirectoryMirror.cs#L382), [FullBackupService.cs:236](../../src/AuswertungPro.Next.Infrastructure/Backup/FullBackupService.cs#L236), [BackupManifestIntegrity.cs:43](../../src/AuswertungPro.Next.Infrastructure/Backup/BackupManifestIntegrity.cs#L43). Reproduktion unter `.tmp/backup-audit-probe/Program.cs`; ausschliesslich temporäre Testdaten. Kein Hinweis, dass die echte Sicherung bereits beschädigt ist.

### 3. Hoch: Sicherungsumfang hat Lücken

Die gespeicherte Einstellung `FullBackupIncludeProjectVideos` ist aktuell `false`. Die letzte abgeschlossene Sicherung von 09:30 Uhr enthält laut ihrem Prüfverzeichnis dagegen Videos, darunter 1'438 Videodateien in der Projektkomponente. Diese beiden Zustände dürfen nicht verwechselt werden.

Wird mit abgewählten Videos gesichert, fehlen diese Dateien im erwarteten Zielbestand. Bereits gesicherte Videos können dadurch nach `_Versionen` verschoben werden. Dort bleiben höchstens drei datierte Änderungsstände erhalten. Spätere Rotation kann diese alten Kopien endgültig entfernen.

Zusätzlich sichert der Dienst Ordner, aber verfolgt keine externen Dateiverweise innerhalb von Projekten. Die ermittelten Projektquellen liegen unter `D:\Projekte`. Verknüpfte Originale unter `D:\Videoprojekte` und die GeoShop-Ablage unter `D:\QGIS_V4.2\GeoShop` werden dadurch nicht automatisch abgedeckt. Ob hierfür andere unabhängige Sicherungen existieren, wurde nicht vollständig erhoben.

Belege: [FullBackupSourcesFactory.cs:48](../../src/AuswertungPro.Next.UI/Services/FullBackupSourcesFactory.cs#L48), [BackupPlanBuilder.cs:158](../../src/AuswertungPro.Next.Application/Backup/BackupPlanBuilder.cs#L158), [DirectoryMirror.cs:152](../../src/AuswertungPro.Next.Infrastructure/Backup/DirectoryMirror.cs#L152) und Zeile 244; [BackupVersionRetention.cs:23](../../src/AuswertungPro.Next.Application/Backup/BackupVersionRetention.cs#L23).

### 4. Mittel: Abbruch während der Vollsicherung kann einen gemischten Stand hinterlassen

Der bestehende Sicherungsordner wird Datei für Datei aktualisiert. Alte Dateien werden verschoben und alte Änderungsstände ausgedünnt, bevor das neue Prüfverzeichnis fertig geschrieben ist. Ein Abbruch zwischen diesen Schritten hinterlässt möglicherweise alte und neue Dateien zusammen. Ein Rücksetzen des gesamten Laufs auf den letzten abgeschlossenen Stand ist nicht umgesetzt.

Die Dateikopien selbst sind deutlich besser geschützt: temporäre Datei, vollständiger Inhaltsvergleich und Schreiben auf den Datenträger. Die Lücke betrifft den zusammenhängenden Gesamtstand. `_Versionen` erleichtert eine manuelle Wiederherstellung, bildet aber keine vollständigen unabhängigen Sicherungssätze.

Belege: [FullBackupService.cs:203](../../src/AuswertungPro.Next.Infrastructure/Backup/FullBackupService.cs#L203), Zeilen 209 und 236; [DirectoryMirror.cs:348](../../src/AuswertungPro.Next.Infrastructure/Backup/DirectoryMirror.cs#L348).

## Was bereits gut umgesetzt ist

- Die Sicherung auf `G:` liegt auf einer separaten SanDisk-USB-Festplatte. `C:` und `D:` sind zwei interne Samsung-NVMe-Laufwerke. Windows meldete diese Laufwerke gesund; das ersetzt keine umfassende Hardwareprüfung.
- Projektdateien werden zuerst vollständig temporär geschrieben, ausdrücklich auf den Datenträger übertragen und dann ersetzt. Die vorherige Datei bleibt als `.bak` erhalten. Beschädigte Projektdateien können aus Sicherungskopien und Wiederherstellungspunkten zurückgeholt werden.
- Datenbanken werden mit der SQLite-Sicherungsfunktion kopiert und auf Konsistenz geprüft. Neue normale Dateikopien erhalten einen vollständigen Inhaltsvergleich.
- Die Programmsicherung erstellt zunächst ein temporäres ZIP. Der geprüfte Ablauf kontrolliert dessen Einträge und erstellt eine SHA-256-Prüfsumme.
- Zielordner, Verknüpfungen und Pfadgrenzen werden geprüft. Die automatische KI-Spiegelung ist zusätzlich vorhanden, überträgt aber auch Löschungen und ersetzt keine unabhängige Sicherungshistorie.

Belege: `JsonProjectRepository.cs:148,191`, `ProjectRecoveryService.cs`, `SqliteSnapshotCopyService.cs`, `DirectoryMirror.cs:498`, `ProgramSnapshotService.cs:124,133,140`.

## Tatsächlicher Sicherungsstand und Prüfgrenzen

Das letzte abgeschlossene Prüfverzeichnis liegt unter `G:\Systemschutz\SewerStudio_Datensicherung\manifest.json`. Es meldet für 09.09.2026, 09:30 Uhr rund 522 GB, 238'393 Quelldateien und keine übersprungenen Dateien. Während der Untersuchung änderten sich Dateien im Sicherungsordner; dies deutet auf einen neuen laufenden Sicherungsvorgang hin. Das Abschlussverzeichnis trug zuletzt weiterhin 09:30 Uhr. Der laufende Vorgang wurde nicht angehalten.

301 vorhandene Infrastrukturtests und 55 Oberflächentests zu Sicherung, automatischem Speichern und Wiederherstellung bestanden: **356 erfolgreich, keine Fehler, keine übersprungenen Tests**. Ausgeführt wurden die bereits gebauten Release-Tests im isolierten Arbeitsordner `.tmp/push-eigen-final`; 63 relevante Sicherungs- und Wiederherstellungsquelldateien waren textlich mit dem aktuellen Arbeitsordner identisch. Das ersetzt keinen vollständigen Test aller aktuellen Oberflächenänderungen. Der zusätzliche Versuch oben bestätigt eine bisher nicht ausreichend abgedeckte Fehlerfolge.

Keine vollständige Prüfsummenprüfung der echten 522-GB-Sicherung während ihres laufenden Updates. Keine Wiederherstellung auf einem Ersatz-PC und kein echter Stromausfalltest. Das Sicherungsformat ist kein startfertiges Windows-Abbild: .NET, Python, Ollama und eigene QGIS-Erweiterungen müssen gemäss `RestoreAnleitungText.cs:23–73` zusätzlich berücksichtigt werden.

Sinnvolle Reihenfolge: zuerst Schacht-Speicherung und Inhaltsprüfung korrigieren; dann benötigte Videos und externe Quellen vollständig abdecken; danach eine getrennte Wiederherstellungsprobe durchführen und einen bei Abbruch unveränderten letzten Gesamtstand sicherstellen.
