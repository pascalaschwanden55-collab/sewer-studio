# Sicherung: Nachprüfung vom 12.09.2026

## Tatsächlicher Sicherungsstand

Die vorhandene Vollsicherung unter `G:\Systemschutz\SewerStudio_Datensicherung`
wurde laut Manifest am 12.09.2026 um 00:14 Uhr erstellt; der Abschluss wurde um
00:15 Uhr in den Einstellungen vermerkt. Umfang: 532'657'934'453 Bytes,
233'556 Einträge mit Inhaltsnachweisen. Es liegt kein offenes Rücksetzprotokoll vor.

Das Manifest enthält Projektvideos sowie die Zusatzordner
`D:\QGIS_V4.2\GeoShop` und die eigenen QGIS-Erweiterungen. Es meldet allerdings
125 übersprungene Verweise. Ein abgeschlossener Lauf bedeutet daher hier keine
vollständige Abdeckung aller Projektverknüpfungen.

Die vollständige SHA-256-Prüfung mit `FullBackupSmoke --verify-restore` ist
erfolgreich abgeschlossen: **233'556 Dateien geprüft, keine Inhaltsabweichungen**.
Die Wissensdatenbank besteht ihre SQLite-Prüfung und enthält 1'684 Beispiele.
Das gesicherte Projekt `Dorfstrasse_Seelisberg` lässt sich mit 41 Haltungen laden.
Die gespeicherten Einstellungen und die Programm-Lösungsdatei sind vorhanden.
Der Prüfprozess endet erfolgreich mit Rückgabewert 0.
Das ist eine vollständige Inhaltsprüfung des aktuellen Sicherungsstands und eine
Ladeprobe; die älteren Dateiversionen und ein kompletter Ersatz-PC wurden nicht geprüft.

## Gefundener und behobener Fehler

`BackupExternalReferences` behandelte das Feld `PDF_All` bisher wie einen
einzelnen Dateipfad. Das Programm speichert darin aber mehrere Protokolle,
getrennt durch Semikolons. Gespeicherte JSON-Listen wurden ebenfalls nicht
aufgelöst; bei echten Arrays gingen relative Verweise verloren.

Die Sicherung liest dieses Feld jetzt mit dem vorhandenen `StoredFileListParser`.
Jeder Eintrag wird einzeln aufgelöst und doppelte Pfade werden nur einmal gesichert.
Relative Angaben beziehen sich auf die Projektwurzel. Einzelpfade anderer Felder
behalten Semikolons im Dateinamen. Kundenprojekte und Originaldateien wurden nicht
verändert.

Vier neue Verhaltenstests prüfen drei Listenformate mit echten künstlichen
Sicherungskopien und SHA-256-Prüfung sowie einen einzelnen Dateinamen mit Semikolon.
Vor der Korrektur scheiterten die drei Listentests; danach bestehen alle vier.
Insgesamt bestehen **227 Sicherungstests, 0 Fehler, 0 übersprungen**.
Der Release-Build von `AuswertungPro.Dev.slnf` gelingt. Er meldet eine Warnung
in der unabhängig bearbeiteten `VsaFotoAblageTests.cs`; keine Buildfehler.
Die Architekturkarte wurde ergänzt und der Architektur-Skill erfolgreich validiert.

## Sofortige Ergänzung der echten Sicherung

23 der 125 Warnungen betreffen zusammengefasste PDF-Pfade. Nach Aufteilung aller
Warnungen ergeben sich 151 eindeutige Verweise: 24 Dateien sind vorhanden,
127 liegen nicht an ihrem gespeicherten Pfad. Darunter sind 16 Zwischenbilder
aus dem temporären Windows-Ordner. Die Liste steht in
`2026-09-12-sicherung-dateihinweise.csv`. Fehlende Dateien wurden weder erfunden
noch anhand ähnlicher Namen automatisch zugeordnet.

Eine anschliessende Namenssuche in `D:\Projekte` und `D:\Videoprojekte` fand
für 109 dieser 127 Verweise mindestens einen möglichen Treffer, für 46 genau
einen. 30 dieser eindeutigen Namenstreffer liegen unter `D:\Projekte`, 16 unter
`D:\Videoprojekte`. Die gespeicherten Pfade können somit teilweise veraltet sein;
127 fehlende Pfade bedeuten nicht nachgewiesenen Verlust von 127 Dateien.
Die Kandidaten stehen in `2026-09-12-sicherung-moegliche-dateitreffer.csv`.
Gleiche Dateinamen sind kein Identitätsnachweis. Es wurden keine Projektverweise
automatisch ersetzt und keine Kandidaten als bestätigte Originale ausgegeben.

Von den 24 vorhandenen PDFs waren zwölf bereits über andere Einzelverweise in
der Vollsicherung enthalten. Die übrigen **zwölf PDFs mit 11'256'619 Bytes** wurden
nach `G:\Systemschutz\Ergaenzung_PDF_2026-09-12` kopiert und einzeln durch SHA-256
geprüft. Dort stehen Originalpfade und Kopierziele in `manifest.json`, der Rückweg
in `WIEDERHERSTELLUNG.txt`. Diese Ergänzung ist keine weitere Vollsicherung.
Die geprüfte Vollsicherung selbst wurde dabei nicht verändert.

Die Programmkorrektur ist im lokalen Release-Stand gebaut. Eine folgende reguläre
Sicherung verwendet damit die richtige Aufteilung; die Ergänzung sollte bis dahin
zusammen mit der Vollsicherung aufbewahrt werden.

## Neue Programmsicherung

Der bestehende `ProgramSnapshotService` hat zusätzlich den aktuellen Programmstand
einschliesslich der Korrektur gesichert:

`G:\Programmsicherungen\SewerStudio\SewerStudio_Programm_2026-09-12_223403.zip`

- 19'303 Dateien, 5'123'594'873 Bytes.
- Vollständige Archivprüfung aktiviert und erfolgreich.
- Keine übersprungenen Verknüpfungen und keine unlesbaren Ordner.
- SHA-256: `6B8BFCEF6FBA3560594378D3074B348A677DCF0EAA7510302757DD2F7C51855A`.
- Die Prüfsumme liegt zusätzlich in der Datei mit Endung `.zip.sha256`.
- Korrigierter Sicherungsdienst und neuer Verhaltenstest wurden aus der ZIP gelesen
  und mit den aktuellen Quelldateien verglichen: identischer Inhalt.

Diese zusätzliche Programmsicherung ersetzt keine Projektsicherung. Ihr Inhalt
folgt den bestehenden Auswahlregeln: Quellcode, Git-Verlauf und Modellgewichte;
ableitbare Build-Ausgaben und Arbeitsreste werden ausgelassen.

## Speicher und Grenzen

Auf G: waren anfangs rund 66,6 GiB frei, nach den zusätzlichen Sicherungen noch
rund 61,9 GiB. Die erhaltenen Dateiversionen belegen:

| Stand | Bytes |
| --- | ---: |
| 09.09.2026, 21:06:54 | 376'082'199'413 |
| 11.09.2026, 22:04:05 | 501'177'767 |

Es wurden keine alten Sicherungen gelöscht. Der Platzbedarf eines weiteren Laufs
hängt von den Änderungen ab und wird vom Sicherungsdienst vor dem Kopieren geprüft.
Eine erfolgreiche Inhaltsprüfung belegt die vorhandenen Kopien, ersetzt aber keine
fehlenden Quelldateien. Ein vollständiger Ersatz-PC oder echter Stromausfall wurde
in dieser Nachprüfung nicht getestet.

Lokale Prüfprotokolle: `.tmp/backup-check-20260912-verify.log`,
`.tmp/backup-reference-before.log`, `.tmp/backup-reference-after.log`,
`.tmp/backup-reference-dev-build.log`, `.tmp/backup-program-20260912.log`.
