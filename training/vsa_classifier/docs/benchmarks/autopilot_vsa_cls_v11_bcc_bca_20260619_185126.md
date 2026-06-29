# Autopilot-Report: vsa_cls_v11_bcc_bca

Stand 20260619_185126 | Datensatz `C:\KI_BRAIN\yolo_vsa_cls_dataset_v11` | LEER-Ziel 38%

## VERDIKT (Entscheidung NUR auf 57er-clean): **NICHT UEBERNEHMEN**

> 63er-hidden ist reiner Kontrollblick, fliesst NICHT in die Entscheidung ein.

| Eval | Kandidat | Baseline (v5) |
|---|---|---|
| 57er-clean | Gesamt 23/57=40.4% | Befund 21/41=51.2% | LEER 2/16=12.5% | Gesamt 35/57=61.4% | Befund 29/41=70.7% | LEER 6/16=37.5% |
| 63er-hidden | Gesamt 9/58=15.5% | Befund 4/44=9.1% | LEER 5/14=35.7% | Gesamt 23/58=39.7% | Befund 17/44=38.6% | LEER 6/14=42.9% |

### 57er Headline (Kandidat vs Baseline, Counts)
- Gesamt: 23 vs 35 (-12)
- Befund: 21 vs 29 (-8)
- LEER: 2 vs 6 (-4)

### 57er Schluessel-Klassen (BAI/BAB/BBA/BDD/BAJ)
- BAI: 0 vs 4 (-4)
- BAB: 0 vs 0 (+0)
- BBA: 0 vs 1 (-1)
- BDD: 2 vs 5 (-3)
- BAJ: 0 vs 0 (+0)

### Kontrollblick 63er-hidden (NICHT entscheidungsrelevant)
- Kandidat: Gesamt 9/58=15.5% | Befund 4/44=9.1% | LEER 5/14=35.7%
- Baseline: Gesamt 23/58=39.7% | Befund 17/44=38.6% | LEER 6/14=42.9%
- (Nur finaler Blick. Das Hidden-Set ist KEIN Optimierungsziel — sonst verliert es seinen Wert.)

---
**LEITPLANKE:** Nur ein KANDIDAT. NICHT produktiv. `active.json` unberuehrt. Produktions-Freigabe nur durch den Menschen (model-promotion-warden).
