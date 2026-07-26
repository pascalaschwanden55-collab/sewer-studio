# YOLO-Detect-Klassenkarten

Dieser Ordner enthaelt nur versionierte Konfiguration. Kundenbilder und Modellgewichte
bleiben ausserhalb des Repositories unter `C:\KI_BRAIN\training\`.

## Dateien

- `detect_class_map_v2.json`: eingefrorene Detect-Klassen mit IDs 0 bis 13. Die Karte
  ist an den SHA-256-Wert des VSA-Katalogs gebunden und wird nicht mehr in-place
  veraendert.
- `detect_class_map_v3.json`: aktive Detect-Klassen mit IDs 0 bis 14. Identisch zur
  eingefrorenen v2 plus `BCC_bogen` mit fester ID 14; ebenfalls an den
  VSA-Katalog-Hash gebunden.
- `detect_class_migration_v2.candidate.json`: vollstaendige Kandidatentabelle aus
  74 Teacher-Codes, 35 alten Map-Schluesseln, 10 produktiven englischen Modellnamen
  und 5 auffaelligen Einzelannotation (Zielkarte v2, mit ihr eingefroren).
- `detect_class_migration_v3.candidate.json`: dieselbe Kandidatentabelle, auf die
  aktive Karte v3 ausgerichtet.
- `detect_class_migration_v2_review.md`: kurze fachliche Pruefliste fuer die offenen
  Entscheidungen (Stand v2, eingefroren).
- `detect_class_migration_v3_review.md`: Freigabedokument zum BCC-Pilot auf der
  aktiven Karte v3.

## Sicherheitsregel

Solange eine passende Zeile nicht `approved` ist, wird sie nicht exportiert. Es gibt
keine automatische neue ID und keinen stillen Rueckfall auf `SONST_schaden`.

Der taegliche Live-Teacher bleibt davon getrennt: Dort ist ein bewusst aufgerufener
`GetOrAddClassId` weiterhin erlaubt. Der eigentliche Trainings-Export liest dagegen
nur einen unveraenderlichen Snapshot.

## Freigabeablauf

1. Die fachlichen Entscheidungen in der Review-Datei pruefen.
2. Offene `review`-Zeilen eindeutig auf `map` oder `discard` setzen.
3. Erst nach der Gesamtpruefung jede Zeile mit `approval_status`, `approved_by` und
   `approved_utc` bestaetigen.
4. Die Kandidatendatei danach als freigegebene Migration aktivieren und die Tests
   erneut ausfuehren.

Aktuell ist nur der persoenlich bestaetigte BCC-Pilot freigegeben. Er wird als
`BCC_bogen` mit der festen ID 14 der aktiven Karte v3 exportiert. Das Pilotregister
begrenzt den Export zusaetzlich auf die darin einzeln aufgefuehrten Goldsample-IDs.
Alle anderen Migrationseintraege bleiben gesperrt.

`BBD_boden` ist eine erlaubte Detektorklasse. Ein gespeicherter VSA-Befund darf aber
nie nur `BBD` lauten. Die C#-Aufloesung verwendet fuer die allgemeine Klasse den
gueltigen Untercode `BBDZ`.

Der Sidecar besitzt bis AP 0.3 noch seine alte eigene Klassen- und Splitlogik. AP 0.3
ersetzt diese durch denselben verbindlichen Exportplan wie beim lokalen Weg.
