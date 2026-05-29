# GroundTruthPipeScaleProbe

Reines **Analyse-Tool** (kein Produktionscode, keine KI-Pipeline, kein Aspect-Fix).
Vergleicht den heutigen **globalen** `pxToMm` (`DN / (image_width * 0.70)`, wie der
`MaskQuantificationService` rechnet) gegen den **lokalen** `pxToMm`
(`DN / (pipe_right_x - pipe_left_x)`) aus vorab gemessenen Rohrkanten.

Ziel: **schwarz auf weiss sehen, ob der lokale Faktor näher an der Realität liegt**,
BEVOR irgendetwas am Messmodell geändert wird (Stufe 3).

## Ablauf

1. Ground-Truth anlegen unter `tools/GroundTruthFrames/pipe-scale/`:
   - 1× 16:9-Frame, 1× 4:3-Frame (PNG/JPG)
   - eine `manifest.json` (Format siehe `sample-manifest.json`):
     - `image_width`, `dn_mm`
     - `pipe_left_x` / `pipe_right_x` = Rohrkanten-Pixel auf einer klaren Bildzeile
     - optional `damage_height_px` + `damage_known_mm` für einen End-to-End-Check
2. Laufen lassen:
   ```
   python tools/GroundTruthPipeScaleProbe/probe.py
   # oder explizit:
   python tools/GroundTruthPipeScaleProbe/probe.py pfad/zu/manifest.json
   ```
3. Ausgabe ist eine Markdown-Tabelle: global vs. lokal px→mm, Δ% und (falls bekannt)
   der Messfehler beider Methoden gegen das echte Maß.

## Entscheidung danach

- Wenn `local px→mm` über beide Formate (16:9 **und** 4:3) klar näher am bekannten Maß
  liegt → Branch `fix/local-pipe-scale-quantification` (enger Scope: nur
  `MaskQuantificationService`, lokaler Faktor wenn Rohrkanten stabil, sonst alter
  Fallback, Tests + Vorher/Nachher).
- Wenn nicht eindeutig → nichts am Messmodell ändern.

## Test
```
python -m pytest tools/GroundTruthPipeScaleProbe/test_probe.py -q
```
