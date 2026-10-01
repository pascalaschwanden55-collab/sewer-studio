# Paket 1: BCC-Einzelklassen-Modell — Messung gegen detect_benchmark_v1 (2026-08-07)

**Diagnose, kein Kandidat.** Drei Seeds (42/43/44), 300 Epochen, Geduld 80,
Batch 8, workers 8, cache ram, Basis `yolo26m.pt`, Scratchpad
(`training/diagnostics/bcc_single_20260807`). Datensatz: 233 Bilder (202 mit
BCC-Boxen aus dem 61370615b1c1-Export, 31 echte Negative), Klasse 14 → 0.
Protokoll `conf=0,25`, `imgsz=1280`, `IoU=0,5`, labelblind gegen
`detect_benchmark_v1` (417 Bilder).

## Ergebnis

| Seed | TP | FP | FN | **BCC-Treffer von 37** | Fehlalarm sauberes Rohr | Feuer auf Fremdschaden |
|---|---:|---:|---:|---:|---:|---:|
| 42 | 35 | 2 | 2 | **35** | 31/75 (41 %) | 120/220 (55 %) |
| 43 | 35 | 4 | 2 | **35** | 36/75 (48 %) | 113/220 (51 %) |
| 44 | 35 | 6 | 2 | **35** | 35/75 (47 %) | 117/220 (53 %) |

**Mittel: 35/37 = 94,6 % Recall.** Herkunftstrennung: alle 37 BCC-Sollboxen
liegen im Holdout-Teil; die Extension v1 enthält keine BCC-Ziele (0/0).

## Entscheidung nach der Regel (>30/37 im Mittel)

**Die Regel ist erfüllt — alle drei Seeds einzeln über der Schwelle.** Der
Einzelklassen-BCC schlägt das Mehrklassenmodell (23–28/37) um sieben bis
zwölf Boxen — über der gemessenen Streu-Spanne, also ein echter Effekt, kein
Rauschen.

## Der Preis: Fehlalarme (Paket 2 vorwegnehmend)

- Auf **41–48 %** der sauberen Negativbilder landet eine Box (Produktivgrenze
  für einen Assistenten: 20 %).
- Auf **51–55 %** der Bilder mit anderen Schäden feuert das Modell — BCC ist
  als Einzelklasse ein Sammelbecken für alles Runde/Dunkle (Rohrenden,
  Anschlüsse, Verbindungen).

Das bestätigt die Planaussage: Der BCC-Weg wird an Fehlalarmen scheitern,
nicht am Recall. Der Recall ist belegt; die Fehlalarmquote ist das
Arbeitspaket.

## Nächste Schritte (Paket 2/3-Vorbereitung)

1. Fehlalarm-Mitigation prüfen: Konfidenz-Arbeitspunkt für die Produktion
   separat suchen (Diagnose-Schwellenlauf, Produktionsprotokoll bleibt 0,25)
   und/oder kontrastierende Harte Negative (BCE-Rohrenden, BAJ-Verbindungen)
   in den Trainingshintergrund.
2. Erst danach Paket 3 (Datenerweiterung 104 → ~200 Haltungen) — mit der
   klaren Erwartungsregel: unter ~8 Boxen Zuwachs ist nichts nachweisbar.

Belege: `training/diagnostics/bcc_single_20260807/messung_benchmark_v1.json`
(pro Seed vollzählig), Skripte unter `artifacts/bcc-single-20260807/`.
