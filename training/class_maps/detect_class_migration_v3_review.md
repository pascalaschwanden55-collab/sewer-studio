# Fachliche Freigabe der Detect-Migration v3

Stand: 2026-07-24

Die maschinenlesbare Tabelle enthaelt 124 Zeilen: 68 vorgeschlagene Zuordnungen,
45 vorgeschlagene Ausschluesse und 11 Review-Zeilen. `BAG` kommt in zwei Quellen vor;
damit bleiben zehn fachliche Entscheidungen.

Die zehn BCC-Zeilen sind fuer einen getrennten Bogen-Pilot durch `Besitzer`
freigegeben. Alle anderen 114 Zeilen bleiben absichtlich auf `pending`.
Der Pilot darf deshalb nur die im Exportregister einzeln genannten BCC-Goldsamples
verwenden. Ein allgemeiner Mehrklassen-Export bleibt weiterhin gesperrt.

Die v3-Tabelle richtet dieselbe Kandidatenmenge auf die aktive Karte v3 aus
(`BCC_bogen` mit fester ID 14). Die eingefrorene v2-Kandidatentabelle behaelt den
Stand vom 2026-07-16 (alle Zeilen `pending`) und wird nicht mehr veraendert.

## BCC-Pilot

- Detect-Klasse: `BCC_bogen`, feste ID 14.
- Umfang: nur persoenlich bestaetigte BCC-Goldbilder mit BBox und SAM-Maske.
- Das Exportregister nennt jede erlaubte Sample-ID einzeln.
- Das bestehende produktive Modell wird nicht ersetzt.
- Ohne Negativbilder ist das Ergebnis nur ein Lern- und Erkennungstest.

## Zehn Entscheidungen

Die zehn fachlichen Entscheidungen sind in der v2-Review-Datei beschrieben
(`detect_class_migration_v2_review.md`) und gelten fuer v3 unveraendert fort.
