# Sicherung vom 24./25.09.2026: fehlende verknüpfte Dateien

## Beleg und Umfang

Die Auswertung liest ausschließlich das lokale `E:\SewerStudio_Datensicherung\manifest.json`.
Der Bericht wurde am 25.09.2026 um 00:17 UTC angelegt und enthält 128 Einträge unter
`SkippedFiles`. Die 276.590 gemeldeten Kopien wurden laut Sicherungsanzeige geprüft.
Jeder der 128 Einträge meldet: „Verknüpfte Datei fehlt; keine aktuelle Kopie gesichert.“
Am 25.09. waren alle 128 angegebenen Quellpfade weiterhin nicht vorhanden.
Originaldateien und Sicherungsdateien wurden nicht geöffnet oder verändert.

| Laufwerk | PNG | JPG | PDF | MP4 | Gesamt |
| --- | ---: | ---: | ---: | ---: | ---: |
| C: | 19 | 0 | 0 | 0 | 19 |
| D: | 0 | 16 | 46 | 0 | 62 |
| F: | 0 | 0 | 4 | 0 | 4 |
| G: | 0 | 0 | 28 | 15 | 43 |
| Gesamt | 19 | 16 | 78 | 15 | 128 |

## Bedeutung für die Wiederherstellung

Die aktuelle Sicherung enthält für diese 128 Verknüpfungen keine neue Kopie.
Insbesondere können 15 Videos und 78 PDF-Belege bei einer Wiederherstellung fehlen.
Ob ältere Sicherungsstände einzelne Dateien enthalten, ist damit nicht bewiesen.
Das Laufwerk F: war bei der Nachprüfung nicht eingebunden. G: war eingebunden,
die dort genannten 43 Pfade fehlten trotzdem.

## Nächste sichere Prüfung

Vor einer Bereinigung zuerst ältere Sicherungsstände und die Verknüpfungen im
betroffenen Projekt nur lesend vergleichen. Keine fehlenden Dateien automatisch
löschen, ersetzen oder als erfolgreich gesichert kennzeichnen.
