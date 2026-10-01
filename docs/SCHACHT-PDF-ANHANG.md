# Einzelne PDF am ausgewählten Schacht ablegen

1. Den gewünschten Schacht auswählen.
2. **Protokoll importieren → einzelne PDF-Datei** wählen.
3. Die PDF auswählen, beispielsweise einen Bericht über manuelle Arbeiten.

Auch ohne automatische Protokollerkennung wird die Datei in den Projektordner
dieses Schachts kopiert und über dessen PDF-Feld verknüpft. Die Meldung lautet
**PDF verknüpft** und erklärt, dass keine Protokolldaten erkannt wurden.
Ein erkanntes Protokoll mit Schachtnummer verwendet weiterhin die bisherige Zuordnung.

Die zusätzliche Datei wird zur aktuellen PDF-Verknüpfung. Vorhandene Dateien im
Projekt werden nicht überschrieben oder gelöscht. Gleiche Dateiinhalte am Ziel
werden wiederverwendet; bei Namenskonflikten entsteht ein eigener Dateiname.
Das Original bleibt unverändert. Stammdaten, Beobachtungen und Handergänzungen
werden beim reinen Verknüpfen nicht ersetzt.

Ohne ausgewählten Schacht erfolgt keine Zuordnung anhand eines geratenen Dateinamens.
Ein Projektwechsel, ein entferntes/umbenanntes Ziel oder ein Kopierfehler verhindert
die Verknüpfung. Misslingt das Speichern, bleibt die Änderung im Projekt erhalten
und es erscheint eine entsprechende Meldung.

Prüfung: Release-Build erfolgreich; 88 UI-/Ablaufprüfungen und 25 Infrastrukturprüfungen
bestanden. Protokolle unter `.tmp/geoshop-pruefung/pdf-anhang-ui.trx` und
`pdf-anhang-infra.trx`. Keine Kunden-PDF und kein Kundenprojekt wurden verändert.
