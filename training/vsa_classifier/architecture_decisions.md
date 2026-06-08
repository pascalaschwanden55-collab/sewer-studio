# Architektur-Entscheidungen: VSA-Klassifikator v1

Ziel: Ein kleines, eigenes Klassifikationsmodell, das auf Ganzbild-Frames den VSA-Hauptcode
trifft — als thin-AI-Ersatz für den VLM-Pfad, der auf Befundcodes gemessen 0 % hatte. Gemessen
wird gegen das eingefrorene 57er-Clean-Eval (Modell hat diese Frames nie gesehen, Kontamination = 0).

| Entscheidung | Was | Warum | Trade-off / v2-Hebel |
|---|---|---|---|
| **Modell** | `yolo11s-cls` | Klein (5,4 M Param), schnell (~18 s/Epoche auf 5090), bewährt in Ultralytics 8.4.56; solider Baseline für 11 Klassen / 13 k Bilder | `yolo11m-cls` hätte mehr Kapazität, aber Overfit-Risiko bei kleinen Klassen (BBA 134) und langsamer |
| **Auflösung** | `imgsz=224` | Klassifikations-Standard, schnell; die Zielcodes (BCD/BDD/BAJ/BDA …) sind eher strukturell/kontextuell, nicht winzige Risse | 320/448 fängt feinere Details, aber langsamer — Hebel falls feine Codes schwächeln |
| **Batch** | `64` | Läuft mit nur ~1,8 GB VRAM locker; auf 32 GB völlig unkritisch | größer ginge, bringt aber kaum etwas |
| **Reproduzierbarkeit** | `seed=42`, `deterministic=True` | Gleicher Seed wie der Datensatz-Split → ganzer Lauf reproduzierbar | — |
| **Mixed Precision** | AMP (bf16 auf Blackwell) | Ultralytics schaltet AMP automatisch; schneller, numerisch stabil auf RTX 50xx | — |
| **Laufzeit** | `epochs=60`, `patience=15` | Genug zum Konvergieren; Early Stopping stoppt bei Plateau | — |
| **Klassen-Imbalance** | v1 trainiert wie sie ist | BCE 2156 vs BBA 134 — zuerst Baseline messen, dann gezielt nachsteuern | v2: Class Weights / Oversampling, falls BBA/BAI in der Eval schwach |

## Bewusst NICHT in diesem Schritt
- **Kein Eval-Frame im Training** (Kontamination = 0 beim Datensatzbau verifiziert).
- **Kein TensorRT-Export** — erst sinnvoll, wenn das Modell die Eval besteht und produktiv gehen soll.
- **Keine Pipeline-Integration** — erst nach belegtem Eval-Vorsprung und Absprache (CLAUDE.md: kein großes Refactoring ohne Diskussion).

## Erfolgskriterium
Top-1-Accuracy auf **Befundcodes** (nicht-LEER) im 57er-Clean-Eval **> 0 %** (VLM-Baseline). Alles
deutlich über 0 % auf BCD/BDD/BAI/BAJ/BDA wäre der erste echte Beleg, dass der eigene Klassifikator
der richtige Weg ist. Messung: `eval_cls.py`.
