# YOLO-Detect-Klassenkarte v2

Dieser Ordner enthaelt nur versionierte Konfiguration. Kundenbilder und Modellgewichte
bleiben ausserhalb des Repositories unter `C:\KI_BRAIN\training\`.

## Dateien

- `detect_class_map_v2.json`: feste Detect-Klassen mit IDs 0 bis 13. Die Karte ist an
  den SHA-256-Wert des VSA-Katalogs gebunden.
- `detect_class_migration_v2.candidate.json`: vollstaendige Kandidatentabelle aus
  74 Teacher-Codes, 35 alten Map-Schluesseln, 10 produktiven englischen Modellnamen
  und 5 auffaelligen Einzelannotation.
- `detect_class_migration_v2_review.md`: kurze fachliche Pruefliste fuer die offenen
  Entscheidungen.

## Sicherheitsregel

Solange eine passende Zeile nicht `approved` ist, wird sie nicht exportiert. Es gibt
keine automatische neue ID und keinen stillen Rueckfall auf `SONST_schaden`.

Der taegliche Live-Teacher bleibt davon getrennt: Dort ist ein bewusst aufgerufener
`GetOrAddClassId` weiterhin erlaubt. Der eigentliche Trainings-Export liest dagegen
nur einen unveraenderlichen Snapshot.

## Freigabeablauf

1. Die zehn fachlichen Entscheidungen in der Review-Datei pruefen.
2. Offene `review`-Zeilen eindeutig auf `map` oder `discard` setzen.
3. Erst nach der Gesamtpruefung jede Zeile mit `approval_status`, `approved_by` und
   `approved_utc` bestaetigen.
4. Die Kandidatendatei danach als freigegebene Migration aktivieren und die Tests
   erneut ausfuehren.

`BBD_boden` ist eine erlaubte Detektorklasse. Ein gespeicherter VSA-Befund darf aber
nie nur `BBD` lauten. Die C#-Aufloesung verwendet fuer die allgemeine Klasse den
gueltigen Untercode `BBDZ`.

Der Sidecar besitzt bis AP 0.3 noch seine alte eigene Klassen- und Splitlogik. AP 0.3
ersetzt diese durch denselben verbindlichen Exportplan wie beim lokalen Weg.
