# Proberestore-Protokoll

Stand: **2026-07-12**
Ergebnis: **Technischer Proberestore bestanden. Drei Bedienprüfungen in der Oberfläche sind noch offen.**

| Prüfung | Ergebnis |
|---|---|
| Datum / Ausführung | 2026-07-12 / Codex, lokal auf diesem PC |
| Verwendete Sicherung | `G:\Systemschutz\12.07.2026\SewerStudio_Datensicherung` |
| Sauberes Zielverzeichnis | `C:\SewerStudio-Proberestore-20260712` |
| Vollsicherung | 695.109 Dateien, 97.156.599.838 Bytes laut Manifest; Videos bewusst ausgeschlossen |
| Dauer Vollsicherung | 28:11 Minuten |
| Zweiter Sicherungslauf | 4:03 Minuten; 695.105 unverändert, 229 geprüft, 225 Datenbank-Snapshots, 0 übersprungen |
| Dauer Wiederherstellung | ca. 7:30 Minuten |
| Vollständigkeit der Kopie | `robocopy /MIR /L`: 695.118 Dateien, 0 fehlend, 0 zusätzlich, 0 Unterschiede, 0 Fehler |
| Einstellungen | vorhanden und lesbar |
| Projekt | Projekt 61 erfolgreich geladen; 1 Haltung vorhanden |
| KnowledgeBase | Integritätsprüfung bestanden; 17.149 Beispiele lesbar |
| Wiederhergestelltes Programm | `dotnet restore` und `dotnet build` bestanden; 0 Fehler, 0 Warnungen |
| PDF-Import in der Oberfläche | **offen** |
| Video-Wiedergabe in der Oberfläche | **offen** |
| KI-Lauf in der Oberfläche | **offen** |

## Gefundener und behobener Fehler

Beim ersten Sicherungslauf wurden erfolgreich kopierte SQLite-Datenbanken fälschlich als übersprungen gemeldet. Nach dem Verschieben der temporären Datei wurde deren Größe über den alten Dateipfad abgefragt. Die Größe wird nun vor dem Verschieben gespeichert. Der zweite Lauf bestätigte 225 fehlerfreie Datenbank-Snapshots und 0 übersprungene Dateien.

## Einordnung

Der Rückweg für Programm, Einstellungen, Projekte und Wissensdatenbank ist praktisch nachgewiesen. AP-17 ist technisch erfüllt. Für die vollständige Bedien-Abnahme müssen noch PDF-Import, Video-Wiedergabe und ein KI-Lauf direkt in der wiederhergestellten Oberfläche ausgeführt werden.

Die Sicherung enthält bewusst den damals nicht eingecheckten Reparaturstand. Das Manifest verweist auf Git-Commit `e3f3df28`; die Reparatur am Datenbank-Backup und das Prüfwerkzeug lagen zusätzlich als lokale Änderungen im gesicherten Programmordner.
