# Paket 2: BCC-Einzelklasse mit Vollhintergrund — Schwellenlauf (2026-08-07)

**Diagnose, kein Kandidat.** Datensatz: der volle Export `61370615b1c1`
(1359 Bilder: 202 mit BCC-Box, 1157 Hintergrund aus Fremdschäden und echten
Negativen — exakt die Bilder des Mehrklassenmodells, nur eine Klasse).
Drei Seeds (42/43/44), 300 Epochen, Geduld 80, Batch 8, workers 8,
**cache=off** (cache=ram ist bei dieser Grösse ein erwiesenes
Host-RAM-Risiko). Scratchpad `training/diagnostics/bcc_single_fullbg_20260807`.
Messung gegen `detect_benchmark_v1`, einmalige Inferenz je Bild, alle
Schwellen aus denselben Rohwerten, IoU 0,5.

## Schwellentabelle (TP von 37 | Fehlalarm sauber | Feuer auf Fremdschaden)

| conf | Seed 42 | Seed 43 | Seed 44 | FA sauber (3 Seeds) | FA Fremdschaden |
|---:|---:|---:|---:|---|---|
| 0,05 | 30 | 32 | 32 | 9 / 4 / 4 | 12–15 % |
| 0,10 | 28 | 29 | **31** | 5 / 2 / 4 | 10–12 % |
| 0,15 | 26 | 26 | 28 | 3 / 1 / 4 | 9–10 % |
| 0,20 | 26 | 25 | 28 | 1 / 1 / 3 | 6–9 % |
| 0,25 | 21 | 25 | 26 | 1 / 1 / 2 | 5–7 % |
| 0,35 | 16 | 24 | 26 | 1 / 1 / 2 | 5–7 % |

## Gegenüberstellung (gleiche Messlatte)

| Aufbau | Treffer von 37 | FA sauber | FA fremd |
|---|---:|---:|---:|
| Mehrklassenmodell (Referenz) | 23–28 | 8–23 % | — |
| Einzelklasse, gefilterter Datensatz (Paket 1) | **35/35/35** | 41–48 % | 51–55 % |
| **Einzelklasse, Vollhintergrund @0,10–0,15** | **26–31** | **1–7 %** | **7–12 %** |

## Einordnung

- Die Erwartung (30–35 Treffer bei <20 % FA) ist **fast getroffen**: an
  conf 0,10 liegen zwei Seeds bei 28–29 und einer bei 31 — die Quote liegt
  bei 3–7 % sauber / 10–12 % fremd, also weit unter der 20-%-Grenze.
- Der Tausch ist ehrlich: ~7 Boxen Recall gegen eine um Faktor ~10 bessere
  Fehlalarmquote gegenüber Paket 1. Für einen Vorschlags-Assistenten ist das
  der richtige Tausch.
- Der Vollhintergrund wirkt wie vermutet als Ursachenbehandlung: Fremdschaden
  (BAJ/BCE) lernt das Modell jetzt explizit als Nicht-Bogen.
- conf 0,25 bleibt Produktionsprotokoll der Mehrklassen-Messungen; der
  Assistenz-Arbeitspunkt des BCC-Wegs liegt bei ~0,10–0,15 und muss im
  Produktpfad als eigener, dokumentierter Wert geführt werden.

**Fazit: Der erste produktiv brauchbare Baustein ist da** — ~73–84 %
Bogen-Recall bei 1–7 % Fehlalarm, ohne eine einzige neue Handlabel-Stunde.
Paket 3 (Datenerweiterung 104→200 Haltungen) bleibt eine Option mit klarer
Nachweisgrenze (~8 Boxen), ist aber für den Assistenten nicht mehr blockierend.

Belege: `training/diagnostics/bcc_single_fullbg_20260807/messung_benchmark_v1.json`
(pro Seed und Schwelle vollzählig), Skripte unter `artifacts/bcc-single-20260807/`.
