# Korrekturplan Shipping-Reife - 10.07.2026

Ziel: Die im Code-Review belegten Schwaechen so beheben, dass SewerStudio im
Alltag keine ungeprueften KI-Befunde speichert und bei PDF, Backup und Release
keine vermeidbaren Daten- oder Betriebsrisiken bleiben.

## Reihenfolge und Status

| Paket | Korrektur | Status |
|---|---|---|
| 1 | Nur freigegebene KI-Ereignisse ins Protokoll uebernehmen | Erledigt |
| 2 | Automatische KI-Freigabe braucht einen unabhaengigen Beleg | Erledigt |
| 3 | PDF-Dateien validiert und atomar ersetzen; Fehler anzeigen | Erledigt |
| 4 | Gleich grosse Dateiaenderungen im Backup sicher erkennen | Erledigt |
| 5 | Lern-Belege vollstaendig speichern; ungepruefte Gewichte nicht aktivieren | Erledigt |
| 6 | Eigenstaendiges Windows-Release samt Sidecar erstellen | Erledigt |
| 7 | Veraltete absolute Trainingspfade entfernen | Erledigt |

## Abnahmeregeln

1. Abgelehnte und offene KI-Vorschlaege veraendern das Fachprotokoll nicht.
2. Hohe Konfidenz und gruene Ampel allein ergeben keine automatische Freigabe.
3. Bei einem PDF-Fehler bleibt das Original oder mindestens dessen Sicherung erhalten.
4. Eine gleich grosse, aber inhaltlich geaenderte Datei wird ins Backup kopiert.
5. Feedback speichert die wirklichen Modellbelege unter dem vorgeschlagenen Code.
6. Gelernte Gewichte laufen nur im Schattenbetrieb, bis ein getrenntes Eval sie freigibt.
7. Das Release startet ohne installiertes .NET und enthaelt den Sidecar-Installationsweg.
8. Trainingsskripte funktionieren unabhaengig vom Namen des Repo-Ordners.

## Pruefung

- Zu jedem Paket gezielte Regressionstests.
- Danach kompletter Build und alle automatisierten Tests.
- Release-Paket testweise erzeugen und Inhalt kontrollieren.

## Ergebnis

- Alle vier Testprojekte vollstaendig ausgefuehrt: 8.152 bestanden, 1 bewusst uebersprungen.
- Der erste gemeinsame Lauf hatte nur eine gesperrte WPF-Compilerdatei; das betroffene
  UI-Testprojekt wurde danach separat und vollstaendig mit 4.113/4.113 Tests geprueft.
- Vollstaendiges Windows-Paket erzeugt: 3.319.175.330 Bytes inklusive .NET, VLC,
  Sidecar-Code, YOLO, DINO, SAM und freigegebenem Klassifikator.
- Klassifikator-SHA-256 stimmt mit `active.json` ueberein; Python liest die umgeschriebene
  relative Modellfreigabe erfolgreich.
- Acht geaenderte Python-Dateien mit `py_compile` geprueft; kein aktives Trainingsskript
  verweist mehr auf `Sewer-Studio_KI_4.4`.
