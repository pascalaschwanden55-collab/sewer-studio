# Detect-Benchmark v1 (gemergt, eingefroren)

Stand: 2026-08-06. **Entwicklungs-Benchmark, keine Abnahmequelle.**

## Herkunft

- **Basis:** `detect_release_holdout_45b66da2c778` — 400 Bilder, bleibt
  unverändert eingefroren (Herkunftsbeleg).
- **Erweiterung:** `detect_benchmark_extension_v1` — 17 Bilder
  (14 BAH-Testrolle aus 10 Haltungen + 3 BAJ-Reservierung aus dem freien Pool:
  `34738-34741`, `35143-35889`, `2396-2397`).
- **Merge-Regel:** Byte-Union ohne Kollisionen (geprüft: 0 Überlappungen).
  Herkunft je Bild in `operator_references` der Kandidatenliste.

## Bestand

- Ort: `C:\KI_BRAIN\eval_set\subsets\detect_benchmark_v1`
- 417 Bilder, 417 vollständig reviewte Entscheidungen (241+74+85 Holdout,
  16+1 Erweiterung), Review `C:\KI_BRAIN\eval_review\detect_benchmark_v1_review.json`
- holdout_id `55cabe4fc444b47f…`, Manifest eingefroren, an Kandidat
  `detect_gold_3f45c1e945fe` und Klassenkarte v3 gebunden
- Schutz: Lage unter `eval_set/subsets` → Haltungs- und Byte-Schutz greifen
  automatisch
- `dataset_status: coverage_incomplete` (ehrlich: BAC 15, BBA 10, BBB 8,
  BBC 19, BBD 0, BBF 16, SONST 4 unter der 20er-Regel). Die Kandidatenklassen
  BAH 21, BAJ 20, BAI 26, BCA 53, BCC 37 sind gedeckt.

## Nutzung

Referenzmesslatte für alle künftigen Entwicklungsvergleiche. Messungen laufen
mit `--development-comparison` (der Kandidat muss nicht zum Benchmark gehören).
Die erste Messreihe darauf: drei Seeds (42/43/44) auf Datensatz `61370615b1c1`
als Referenzstand und Rauschmessung, danach BAH-Sammlung aus den 39 freien
Haltungen und Vergleich dagegen.
