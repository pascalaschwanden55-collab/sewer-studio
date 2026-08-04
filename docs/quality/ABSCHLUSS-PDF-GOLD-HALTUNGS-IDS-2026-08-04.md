# Abschlussbeleg: Reparatur der PDF-Gold-Haltungs-IDs (2026-08-04)

## Ausgangslage

Der schreibfreie Prüflauf vom 2026-08-03
(`docs/quality/PRUEFBERICHT-PDF-GOLD-HALTUNGS-IDS-2026-08-03.json`) hatte 239 von
1025 bewerteten PDF-Goldsamples mit falscher Haltungs-ID gefunden. Dreizehn
davon zeigten auf zwei Haltungen des Release-Holdouts
`detect_release_holdout_45b66da2c778` (siehe Nachtrag in
`DETECT-RELEASE-DIAGNOSTIC-2026-08-03.md`).

## Durchführung

Werkzeug: `training/scripts/repair_pdf_gold_holding_ids.py --execute`
(fail-closed, Vorflug-Schutzprüfung vor jeder Reparatur, Sicherung und
atomares Schreiben von Gold-JSON, Teacher-JSON, SQLite und Exportregister,
bytegenaue Nachprüfung). Bildbeleg je Fall über
`tools/PdfImageAnalyzer` (`--normalize-json`): bytegleicher SHA-256 gegen den
rohen PDF-Bildstrom (Gruppe 1) oder gegen das Ergebnis derselben
CMYK-Normalisierung wie der App-Import (Gruppe 4).

## Ergebnis

| Kategorie | Anzahl |
|---|---:|
| repariert mit Byte-Beweis (Gruppe 1) | 122 |
| repariert mit Normalisierer-Beweis (Gruppe 4) | 47 |
| dekontaminiert in diesem Lauf (Vorflug) | 5 |
| bereits zuvor dekontaminiert (`eval_decontamination_20260804_182721`) | 8 |
| Quarantäne (unverändert, Ordner `06.691078-691070`) | 57 |
| bereits korrekt | 786 |

- **169 CaseIds korrigiert** (samt Signatur, Teacher-Eintrag und
  `Samples.CaseId` in der Wissensdatenbank), jede mit Bildbeleg.
- **13 kontaminierte Samples** stehen jetzt alle als
  `eval-holdout-contamination-precaution` ausserhalb des Trainingswegs;
  das Exportregister steht bei 886 freigegebenen Samples.
- **3 Samples mit alter Fehl-CaseId bleiben bewusst unberuehrt**: Es sind
  Entwuerfe (Status Draft) ohne SAM-Maske — kein Gold, kein Trainingsbezug.
- Die 57 Quarantäne-Fälle (ein Ordner, Zielhaltungen nach Vorprüfung alle
  frei) gehen als eine manuelle Entscheidung an den Betreiber.

## Belege

- Ausführungsbeleg: `C:\KI_BRAIN\training\repairs\pdf_gold_holding_id_repair_20260804_185032\repair_result.json`,
  SHA-256 `3676aacb34275a629bea03a5d18951cb00a744a9302e8590d3575de3120b95e6`
  (enthält je Fall alte/neue CaseId, Beweisart und alle Ausgabe-Hashes).
- Sicherungen (Vorher-Zustand von Gold-JSON, Teacher-JSON, Register, SQLite)
  liegen im selben Ordner.
- Dekontaminationsbeleg: `C:\KI_BRAIN\training\repairs\eval_decontamination_20260804_182721\receipt.json`.
- Fokussierte Tests: `training/scripts/tests/test_repair_pdf_gold_holding_ids.py`
  (6 Fälle: je Gruppe einer, inkl. geschuetzter Zielhaltung und
  Signaturkollision; 199 Tests der training/scripts-Suite gruen).

## Offene Punkte

1. Manuelle Entscheidung: 57 Quarantäne-Fälle aus `D:\Haltungen\06.691078-691070`.
2. Register neu aufbauen (nächster `prepare_detect_gold`-Lauf) — die 13
   dekontaminierten Samples fallen dann automatisch heraus.
3. Frischer, unberührter Release-Holdout für die nächste Modellabnahme
   (der bisherige ist durch Diagnose-Nutzung und die hier belegte
   Kontamination verbraucht).
