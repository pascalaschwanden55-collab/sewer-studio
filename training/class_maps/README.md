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
- `detect_class_migration_v3.candidate.json`: aktive Kandidatentabelle auf Karte v3.
  Sie enthaelt die exakt im freigegebenen Gold-Audit vom 2026-08-02 vorkommenden
  VSA-Codes und bindet deren Freigabe an Audit- und Sample-SHA-256.
- `detect_class_migration_v2_review.md`: kurze fachliche Pruefliste fuer die offenen
  Entscheidungen (Stand v2, eingefroren).
- `detect_class_migration_v3_review.md`: Freigabedokument fuer BCC und den
  nicht aktivierten Mehrklassen-Goldkandidaten auf Karte v3.

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

Der BCC-Pilot bleibt als `BCC_bogen` mit fester ID 14 erhalten. Fuer einen getrennten
Mehrklassen-Lernkandidaten sind genau die 72 Codes des gebundenen persoenlichen
Gold-Audits als `map` oder `discard` freigegeben. Nicht im Beleg
genannte sowie weiterhin offene Migrationseintraege bleiben gesperrt. Das jeweils
aktive Exportregister begrenzt den Export zusaetzlich auf einzeln aufgefuehrte
Goldsample-IDs.

Der aktuelle v3-Beleg umfasst 142 Zeilen: 92 Teacher-Codes, 35 alte
Map-Schluessel, 10 produktive Modellnamen und 5 Einzelannotation. Insgesamt sind
73 Zeilen freigegeben und 69 weiterhin offen. Die persoenliche Goldfreigabe bindet
72 Teacher-Codes an folgende unveraenderliche Quellen. Neu ist insbesondere
`BAFCZ -> BAF_oberflaeche`:

- Gold-Audit SHA-256:
  `bb7f01f6b3582029ad4393c7217e5c2bbbb4ed5770ab15c807a574972b4905ba`
- `training_samples.json` SHA-256:
  `bfcb3362762dc552861feb0680f1267e086e8d7d3fb71d70e5806841b82daa83`
- Klassenkarte v3 SHA-256:
  `58f1160f2411d5a583bd7a69d3b739be9d29ef7dce33052e61d583fa773a7468`
- Migrationsdatei v3 SHA-256:
  `99f9e00303480441ec8f988799aeea2883a7186060e3a82603c8892829d2e9bf`

Der gebundene Stand umfasst 898 freigegebene Goldinstanzen (713/185) und den
abgeleiteten Negativsatz `bcc_hn_c25fd2f9d33f` mit 9 Bildern (7/2). Der Plan
`9eb020e303225109849cc3a4036cd33288ff0120efd1557a910484f4bd2a61f8`
enthaelt nach Zusammenfuehrung bytegleicher Bilder 856 Bilder (689/167) und
898 Boxen in 13 der 15 festen Klassen. `detect_gold_9eb020e30322` hat 40/40
Epochen beendet und bleibt `not_deployed`. Die interne Validation ergibt
P 0,3917, R 0,3129, mAP50 0,3026 und mAP50-95 0,1726; sie ist keine
Release-Freigabe.

`derive_negative_set_for_gold_audit.py` leitet einen neuen audit-sicheren
Negativsatz ab, ohne den alten Satz oder seine Review zu veraendern.
`repair_gold_holding_ids.py` repariert `foto_*`-Haltungen nur ueber eindeutige,
bytegleiche Quelldateien; Standard ist jeweils ein schreibfreier Prueflauf.

`BBD_boden` ist eine erlaubte Detektorklasse. Ein gespeicherter VSA-Befund darf aber
nie nur `BBD` lauten. Die C#-Aufloesung verwendet fuer die allgemeine Klasse den
gueltigen Untercode `BBDZ`.

Sidecar und lokaler Export fuehren denselben verbindlichen AP-0.3-Plan aus; sie
treffen keine eigene Klassen- oder Splitentscheidung.
