# Detailplan — Training der KI-Pipeline SewerStudio

**Version:** 1.2 · **Datum:** 2026-07-16 · **Basis:** v1.1 + zweites Review (Code-/Datenabgleich)
**Charakter:** Arbeitsfähiger Umsetzungsplan. Reihenfolge: erst messbar & konsistent machen, dann labeln, dann trainieren.

---

## Änderungslog v1.1 → v1.2

1. **imgsz-Baseline korrigiert:** Die produktive Engine ist mit **1280** gebaut (`yolo26m.build.json`), Laufzeit nutzt 1280 (`config.py`). 640 war nur der Skript-Default. → Training startet bei 1280; 640/960 nur als kontrollierte Experimente.
2. **Split-Fix zielt jetzt auf den richtigen Ort:** Der UI-Export läuft über `TrainingYoloExportWorkflow` → **Sidecar-Route** `training.py` (eigener Shuffle + eigenes dynamisches Klassen-Mapping) mit lokalem Fallback. → **Ein gemeinsamer Exportplan** für alle Wege, statt Fix an einem ungenutzten Service.
3. **Datenzahlen korrigiert:** 704 gesamt → 533 mit Bild+Box → davon nur **245 mit verlässlicher Haltung**; **288 in Quarantäne** (nicht „nur Train" — Herkunft unbekannt = potenzielle Dev-Val-Kontamination).
4. **Schattenbetrieb realistisch:** Sidecar hat **einen** YOLO-Modellslot → Start mit **Offline-Doppellauf** (alt/neu nacheinander), kein Parallel-Schalter.
5. **YOLO-Detect (1280) und YOLO-cls (1024, Letterbox) getrennt geplant** — eigene Datensätze, Manifeste, Freigaben. cls-Skip greift nur bei `ClassifierDecisionEnabled` (Default aus), Schwelle 0.70 hardcoded. Für cls existiert bereits ein Promotionsweg (`models/active.json`, model-promotion-warden) — wird genutzt, nicht neu erfunden.
6. **Klassenkarte v2 muss Store-kompatibel sein:** `VsaYoloClassMapFileStore` liest das geplante Format nicht, fällt still zurück und **vergibt selbstständig neue IDs** (`GetClassId`). Ergänzt: `BBF_infiltration`, Auffangklasse, Migrationstabelle, BBD-Regel.
7. **Abnahme messbar gemacht:** Eval-Modell hat kein `ExpectedSeverity` — „0 übersehene Sev 4/5" ist derzeit nicht berechenbar. Neue Mess-Infrastruktur: Severity je Fall, Ereignis- statt Frame-Treffer, Mindestfallzahlen, Fehlergrenzen. Vorrangregel: **Sicherheit vor Skip-Quote.** Abnahme-Set wird nur je Release-Kandidat benutzt, nicht je Zyklus.
8. **Weitere Korrekturen:** KB-Embeddings sind **Text-only** (`nomic-embed-text`) → taugen nicht für „optisch ungewöhnliche Bilder"; Qwen-Durchstich in **Phase 0** vorgezogen (exakt aktuelles JSON-Schema); SAM braucht Masken-Editor + vollständig kompatiblen Checkpoint; Speicher-Layout `C:\KI_BRAIN\training\`; Phase 0 realistisch **4–5 Wochen**.

---

## A. Verifizierte Ist-Befunde (Stand HEAD, geprüft)

| Befund | Beleg |
|---|---|
| Engine gebaut mit **image_size 1280**, fp16, ultralytics 8.4.56, python-tensorrt-Builder | `sidecar/models/yolo26m/yolo26m.build.json` |
| Laufzeit: `yolo_imgsz=1280`; **cls getrennt:** `yolo_cls_imgsz=1024`, Letterbox, Modellwahl über `models/active.json` (model-promotion-warden) | `sidecar/sidecar/config.py` Z. 66–77 |
| UI-Export: Sidecar-Route bevorzugt (max. 500 Samples/Request), lokaler Fallback — **zwei verschiedene Split-/Klassen-Logiken** | `TrainingYoloExportWorkflow.cs` Z. 89 ff. |
| Sidecar-Export: eigenes `class_map` aus `sorted(set(class_names))` + eigener Shuffle-Split | `sidecar/sidecar/routes/training.py` Z. 58–67 |
| Lokaler Export: `Random(42)`-Shuffle pro Bild, dynamische Klassen-IDs | `YoloDatasetExportService.cs` Z. 42–64 |
| YOLO läuft im GPU-Manager auf **einem persistenten Slot** — kein zweites Detect-Modell parallel | `sidecar/sidecar/models/yolo_wrapper.py` Z. 235 ff. |
| cls-Skip (`OTHER`/`NORMAL` >0.70 → DINO/SAM/Qwen gespart) nur bei `ClassifierDecisionEnabled` (env-gesteuert, Default aus); 0.70 im Code | `MultiModelAnalysisService.cs` Z. 277–319, `PipelineEnvironmentOptions.cs` |
| Klassen-Map-Store: einfaches `{code→id}`-JSON; unbekannte Codes bekommen **automatisch** die nächste ID; Fallback bei Lesefehler | `VsaYoloClassMapFileStore.cs` Z. 55–71 |
| Eval-Modell: `ExpectedFullCode/MainCode/Category` — **kein ExpectedSeverity**, keine Ereignis-ID | `EvalSetBenchmarkModels.cs` |
| KB-Embeddings: Ollama `nomic-embed-text` = **Text**-Vektoren | `EmbeddingService.cs` |
| Datenbestand: 704 Teacher-Annotationen; 533 mit Bild + positiver Box; **245 mit verlässlicher Haltung, 288 ohne** (meist ohne Videopfad, nicht rekonstruierbar); `training_samples.json` ≈ leer; Eval-Set 120 Bilder / 17 Haltungen ohne Severity | Review-Audit |

---

## B. Speicher-Layout (vor allem anderen festlegen)

```
C:\KI_BRAIN\training\          # DATEN — außerhalb des Repos
├── frames\ , datasets\ , testset_gold\ , label_studio\ , models\candidates\ , reports\
C:\Sewer-Studio_KI_4.5\training\   # NUR Skripte, Configs, Vorlagen, experiments.md (versionierbar)
```

Regel: Kundenbilder, Modellgewichte, Label-Studio-Daten **nie** ins Repo; Skripte referenzieren `KI_BRAIN` über eine Config.

---

## C. Zwei YOLO-Strecken (getrennt planen, getrennt freigeben)

| | **YOLO-Detect** | **YOLO-cls** |
|---|---|---|
| Zweck | Boxen-Vorschläge + `IsRelevant`-Pre-Screening | Frame-Gate (NORMAL/OTHER-Skip), VSA-Frame-Klasse |
| Auflösung | **1280** (Engine & Laufzeit) | **1024**, Letterbox |
| Datensatz | Box-Labels (class_map v2) | Frame-Labels inkl. NORMAL/OTHER/LEER-Klassen |
| Manifest/Report | eigenes Dataset-Manifest + Freigabebericht | eigenes Dataset-Manifest + Freigabebericht |
| Deployment | Engine via `build_engine.ps1` (+ build.json) | **`models/active.json` via model-promotion-warden** (vorhandener Weg) |
| Gate-Relevanz | indirekt (`IsRelevant`) | direkt — nur aktiv wenn `ClassifierDecisionEnabled` |

**Vorrangregel (verbindlich):** Sicherheit schlägt Effizienz. Ein Kandidat mit besserer Skip-Quote, aber auch nur einem zusätzlich übersehenen Sev-4/5-Ereignis, wird abgelehnt. Sinkende Skip-Quote ist ein akzeptabler Preis für Recall.

**Kleiner Zusatz-AP:** cls-Skip-Schwelle (0.70) konfigurierbar machen (env/Settings statt hardcoded), damit der Operating Point pro Modellversion kalibrierbar ist — additiv, mit Test.

---

## D. Daten: Triage, Manifest, EIN Exportplan

### D.1 Triage (korrigiert)

| Bestand | Verwendung |
|---|---|
| 245 mit Bild + Box + verlässlicher Haltung | Train **oder** Dev-Val (Zuweisung je Haltung) |
| 288 mit Bild + Box, Herkunft unbekannt | **Quarantäne** — weder Train noch Val, bis Herkunft geklärt; Freigabe einzeln nur nach manueller Prüfung („stammt sicher nicht aus Dev-Val/Abnahme-Haltung") |
| Rest (171 ohne Bild/Box) | Archiv |
| Eval-Set 120 Bilder / 17 Haltungen | Dev-Val-Kern; die 17 Haltungen sind für Train gesperrt; wird um Severity + Ereignis-IDs ergänzt (F) |
| Abnahme-Set | neu: 15–25 unbenutzte Haltungen, vollständig menschlich gelabelt inkl. Severity, versiegelt |

Ehrliche Konsequenz: Der belastbare Startbestand fürs Training ist **~245 + Bootstrap-Ausbeute**, nicht 533. Das Labeling in Phase 1 trägt entsprechend mehr Gewicht.

### D.2 Gemeinsames Datenmanifest + ExportPlanner (der zentrale AP 0)

**Problem:** Drei Orte entscheiden heute unabhängig über Split & Klassen-IDs (Sidecar-Route, lokaler Fallback, Store-Automatik). Ergebnis hängt davon ab, ob der Sidecar erreichbar ist.

**Lösung:** Ein neuer, kleiner **`ITrainingExportPlanService`** (C#, additiv, mit Interface + Tests nach CLAUDE.md-Checkliste):
- Input: Teacher-Annotationen + TrainingSamples + class_map v2 + Haltungs-Registry (Dev-Val-/Abnahme-Sperrliste, Quarantäne-Flags).
- Output: **ExportPlan** (JSON): je Sample → Ziel (train/val/exclude), feste Klassen-ID, Zieldateiname; plus Manifest (Haltungslisten, Instanzen je Klasse, class_map-Version, Manifest-Hash, Datum).
- **Beide Exportwege konsumieren nur noch diesen Plan:** Die Sidecar-Route bekommt Split+IDs im Request mitgeliefert (statt selbst zu shuffeln/mappen); der lokale Fallback liest denselben Plan. Kein Exportweg trifft mehr eigene Entscheidungen.
- Tests: keine Haltung in zwei Splits; Quarantäne nie exportiert; IDs stabil über zwei Exporte; Sidecar- und Lokal-Export erzeugen identische Split-Zuordnung.

### D.3 Negativ-/Hintergrundbilder

Wie v1.1: normale Rohre, Wasserspiegel/Reflexionen, Muffen, schlechte Sicht, OSD-Artefakte — ~10–20 % des Detect-Train-Sets (leere Labeldatei) und als eigene Klassen im cls-Datensatz. Quelle: viele verschiedene Haltungen, nicht aus Dev-Val/Abnahme.

---

## E. Klassenkarte v2 (Store-kompatibel, vollständig)

1. **Kompatibilität:** `VsaYoloClassMapFileStore` wird erweitert (additiv): versioniertes Format `{version, vsa_manifest_hash, classes:{key→id}}`; Migration liest Alt-Format einmalig ein. **Lesefehler = harter Fehler** (kein stiller Fallback). Die **Auto-Vergabe neuer IDs wird im Trainings-/Exportkontext deaktiviert** — unbekannter Code ⇒ Fehler bzw. explizite Auffangklasse, nie stillschweigend ID anlegen. (Im Teacher-Alltag darf Auto-Anlage konfigurierbar bleiben.)
2. **Klassenliste v2 (Detect):** die 12 aus v1.1 **plus** `BBF_infiltration` **plus** Auffangklasse `SONST_schaden` (sichtbarer Schaden, keiner Klasse zuordenbar — verhindert erzwungene Falschlabels).
3. **BBD-Regel:** `BBD_boden` ist als **Detektorklasse** zulässig; beim Mapping in Befunde darf aber nie Basiscode `BBD` gespeichert werden — immer Untercode (Auflösung in C#, wie in CLAUDE.md vorgegeben).
4. **Migrationstabelle:** vollständige Zuordnung **aller** bisher vorkommenden Klassen (englische YOLO-Namen, VSA-Präfixe, Alt-Map-Einträge) → v2-Key oder `SONST_schaden`/verwerfen. Jede Zeile wird einmal menschlich abgenommen; ungemappte Alt-Labels werden **nicht** exportiert.
5. **cls-Klassen separat:** NORMAL / LEER / OTHER + VSA-Frame-Klassen als eigene, versionierte cls-Map (nicht mit der Detect-Map mischen).

---

## F. Mess-Infrastruktur (Voraussetzung für jedes Gate)

1. **Eval-Modell erweitern** (additiv): `ExpectedSeverity (1–5)`, `EventId` (Schadensereignis über Frames/Meterbereich), optional `MeterStart/End`. Bestehende 120 Fälle nachpflegen; Abnahme-Set von Anfang an damit erfassen.
2. **Ereignis-basierte Treffer:** Ein Schadensereignis gilt als gefunden, wenn ≥1 zugehöriger Frame korrekt durchs Gate kommt bzw. detektiert wird — Zählung **pro Ereignis**, nicht pro Bild (sonst dominieren lange Streckenschäden die Statistik).
3. **Mindestfallzahlen:** „0 übersehene Sev 4/5" ist nur aussagekräftig mit genug unabhängigen schweren Ereignissen — Ziel: **≥20 unabhängige Sev-4/5-Ereignisse** im Abnahme-Set (sonst ausweisen: „0 von N, N zu klein"). Fehlergrenzen (z. B. Wilson-Intervall) im Report angeben.
4. **Abnahme-Sparsamkeit:** Das versiegelte Set wird **nur je Release-Kandidat** ausgewertet (nicht je Flywheel-Zyklus). Zyklen messen auf Dev-Val + Schatten-Videos. Jede Abnahme-Nutzung wird protokolliert; nach ~5–6 Nutzungen Set-Rotation/Erweiterung erwägen.
5. **Gate-Bericht je Kandidat:** Gate-Miss (Ereignisse, nach Severity), Skip-Quote, Fehlalarme/Haltung, Konfidenzverteilungen — für Detect und cls **getrennt**.

---

## G. Kandidaten-Vergleich: Offline-Doppellauf (statt „Schatten-Schalter")

Da nur ein YOLO-Slot existiert:
1. Feste Videoliste (20–50 vollständige Videos, Mix bekannt/neu).
2. **Lauf A:** aktuelles Modellpaket → Ergebnisse/Traces archivieren. **Lauf B:** Kandidat (Slot wechselt, gleiche Frames/Configs) → dito.
3. Diff-Skript vergleicht auf Ereignis-Ebene: nur-A, nur-B, beide, Gate-Entscheidungen, Zeiten.
4. Später optional: zweiter Sidecar-Prozess auf anderem Port für echtes Parallel-Shadowing; **kein** Umbau des GPU-Managers in Phase 0–3.

**Modellpaket** (Einheit für Vergleich & Rollback): Gewichte (.pt) + ONNX + Engine + `names.json` + build.json + class_map-Version + Dataset-Manifest + Eval-/Gate-Report. Kandidaten liegen unter `KI_BRAIN\training\models\candidates\<id>\`.

---

## H. Active-Learning-Signale (ohne Bild-Embeddings)

Verfügbar ab Zyklus 1: Konfidenz-Graubereich (0.25–0.6), YOLO/DINO-Widerspruch, Fehler aus Doppellauf-Diffs, cls-Unsicherheit (Top-1 knapp), schwache Klassen laut Confusion-Matrix.
**Nicht verwenden:** KB-Text-Embeddings als „Bild-Neuartigkeit" — sie sehen das Bild nicht. Optional später: leichtes Bild-Embedding (z. B. CLIP/DINOv2 klein) als **Offline-Skript** im Trainings-Tooling — kein KB-Umbau.

---

## Phasen

### Phase 0 — Messbar & konsistent machen (4–5 Wochen bei 15–20 h/Wo)

| AP | Inhalt | DoD |
|---|---|---|
| 0.1 | **Inventar & Pfad-Reparatur:** Teacher-Bestand durchgehen, wiederherstellbare Bildpfade fixen, Haltung wo sicher möglich nachtragen, Quarantäne-Flag setzen | Triage-Tabelle D.1 als Datenbestand umgesetzt |
| 0.2 | **class_map v2 + Store-Update + Migrationstabelle** (E) | Tests: Lesefehler hart; keine Auto-ID im Export; Migration abgenommen |
| 0.3 | **ExportPlanner + Umbau beider Exportwege** (D.2) | Tests grün; Sidecar- und Lokal-Export identisch |
| 0.4 | **Mess-Infra** (F): Eval-Modelle erweitern, Ereignis-Zählung, Gate-Report | 120er-Set mit Severity/EventId nachgepflegt |
| 0.5 | **Abnahme-Haltungen ziehen + vollständig labeln** (größter Zeitblock; inkl. ≥20 Sev-4/5-Ereignisse — ggf. gezielt Haltungen mit bekannten schweren Schäden wählen) | Set versiegelt, `GOLD.md`, Severity komplett |
| 0.6 | Dev-Val ausbauen (≥25 Haltungen), Negativ-Pool (≥500 Frames/≥30 Haltungen) | eingefroren v1 |
| 0.7 | **Qwen-Durchstich:** 10–20 Beispiele mit dem **exakten produktiven JSON-Schema** → Unsloth-QLoRA → Merge → GGUF → Ollama-Import-Test (mmproj-Risiko); Fallbacks: llama.cpp-Server, Transformers-Batch | dokumentierter, funktionierender Pfad oder begründete Fallback-Wahl |
| 0.8 | Umgebung: `KI_BRAIN`-Layout (B), Trainings-venv, Label Studio, `experiments.md` | lauffähig |

*2–3 Wochen sind nur haltbar, wenn die Abnahme-Haltungen bereits gelabelt vorliegen.*

### Phase 1 — Bootstrap-Labeling (2–4 Wochen)

Wie v1.1: DINO(+SAM)-Vorlabels → Label-Studio-Review → Silber-Set; Pool ohne Dev-Val-/Abnahme-Haltungen; Klassen nach v2; Negativ-Frames im Review-Strom; Rare-Class-Mining (BAC, BAA, BBF). Ziel ≥3 000 geprüfte Labels, je Klasse ≥150 (Ausnahmen dokumentieren). **Parallel:** cls-Datensatz aus denselben Reviews ableiten (Frame-Klasse), eigenes Manifest.

### Phase 2 — Kandidaten trainieren (1–2 Wochen) — kein Rollout

- **2a Detect:** Warm-Start `yolo26m.pt`, **imgsz 1280**, `flipud=0.0`, `fliplr=0.0`, Licht-Augmentierung; Eval auf Dev-Val (Ereignis-Gate-Simulation + P/R je Klasse). 640/960 nur als dokumentiertes Vergleichsexperiment inkl. Latenz/VRAM.
- **2b cls:** eigener Lauf, **imgsz 1024**, Letterbox-konform; NORMAL/OTHER-Kalibrierung auf Dev-Val (Skip-Schwelle als Parameter, nicht 0.70 fix).
- Ergebnis: **zwei Modellpakete** (G) + getrennte Reports.

### Phase 3 — Offline-Doppellauf & E2E-Abnahme (1–2 Wochen je Kandidat)

1. Doppellauf A/B auf 20–50 Videos (G), Diff auf Ereignis-Ebene.
2. E2E auf versiegelter Abnahme: komplette Pipeline, Protokoll-Abgleich (Treffer/Fehlend/Zusatz je **Ereignis**, Meter ±0.5 m), QualityGate-Kalibrierung auf Dev-Val.
3. **Release-Gate** (unten). Detect via Engine-Tausch (+Backup), cls via `active.json`/Warden.

### Phase 4 — Flywheel (Zyklen à 2–3 Wochen)

Wie v1.1, mit: Auswahl-Signalen aus H, Zyklus-Messung nur auf Dev-Val + Doppellauf-Videos, **Abnahme nur je Release-Kandidat**. Jede Code-Berührung (Exporte, Diffs) bleibt im Offline-Tooling bzw. additiven Services.

### Phase 5 — Qwen3-VL LoRA (nach erstem Detect-Release)

5b Dataset 300–800 Gold-Paare (~20 % Negativ-/Korrekturbeispiele, exakt produktives Schema) → 5c QLoRA + Eval-Harness (JSON-Validität ≥99 %, Feld-Genauigkeit, Halluzinations-Stichprobe) + A/B auf 50 Fällen. Deployment über den in 0.7 verifizierten Pfad. Fallback bleibt Basis-`qwen3-vl` (nie qwen2.5); 2B nicht tunen.

### Phase 6 — SAM 2.1 (optional, strenge Startbedingung)

Zusätzlich zu v1.1 (Fehler nachweislich aus Masken): **Vorher** Masken-Korrektur-Workflow einrichten (Label Studio Brush o. ä. — existiert noch nicht) und sicherstellen, dass das Feintuning einen **vollständigen, `SAM2ImagePredictor`-kompatiblen Checkpoint** erzeugt (kein reiner Decoder-Diff). Sonst nicht starten.

---

## Zeitplan (zwei Spuren)

| Meilenstein | 15–20 h/Wo | 8–10 h/Wo |
|---|---|---|
| Phase 0 komplett (inkl. Abnahme-Labeling, Qwen-Durchstich) | Woche 4–5 | Woche 7–9 |
| Silber-Set (Phase 1) | Woche 7–9 | Woche 12–15 |
| Kandidaten + Doppellauf + Abnahme (2+3) | Woche 10–12 | Woche 16–19 |
| **Erstes produktives Release (Detect und/oder cls)** | **~Woche 12** | **~Woche 19** |
| Flywheel 1–2 + Qwen-LoRA | Woche 13–20 | Woche 20–32 |
| Belastbarer E2E-Nachweis | **~5 Monate** | **~7–9 Monate** |

---

## Release-Gate (für jede Modelländerung, Detect und cls getrennt)

- [ ] Modellpaket vollständig (G) — Datensatz-Manifest ↔ class_map-Version ↔ VSA-Manifest-Hash verknüpft
- [ ] Dev-Val: keine Klassen-Regression >1 Punkt ohne Begründung
- [ ] Doppellauf-Diff gesichtet; **Vorrangregel eingehalten:** kein zusätzliches übersehenes Sev-4/5-Ereignis, auch nicht für bessere Skip-Quote
- [ ] Abnahme (Ereignis-Ebene): 0 übersehene Sev-4/5 von ≥20 (sonst „N zu klein" ausweisen); Fehlergrenzen im Report
- [ ] Abnahme-Nutzung protokolliert (Sparsamkeitsregel F.4)
- [ ] Deployment korrekt: Detect-Engine + build.json + Backup bzw. cls über `active.json`/Warden; **Modellname im Sidecar-Log verifiziert** (COCO-Fallback-Lehre 2026-06-09)
- [ ] Latenz + VRAM ≤29 GB Laufzeit; `dotnet test` grün; QualityGate läuft; Rollback-Paket liegt bereit

---

## Offene Punkte (bewusst vertagt)

1. Zweiter Sidecar-Prozess / zweiter Modellslot für echtes Parallel-Shadowing.
2. Bild-Embeddings (CLIP/DINOv2) fürs Active Learning — Offline-Skript, kein KB-Umbau.
3. Sewer-ML-Vortraining; `fliplr`+Uhrlagen-Spiegelung; imgsz-Vergleich 640/960/1280; DINO-Feintuning; DVC — Kriterien wie v1.1.

---

## Quellen / Referenzen

**Code-Belege (Repo, verifiziert):**
- `sidecar/models/yolo26m/yolo26m.build.json` (image_size 1280) · `sidecar/sidecar/config.py` Z. 66–77 (imgsz 1280 / cls 1024, active.json)
- `src/AuswertungPro.Next.UI/Ai/Training/TrainingYoloExportWorkflow.cs` · `sidecar/sidecar/routes/training.py` Z. 58–67 (Doppel-Export, dynamische IDs)
- `src/AuswertungPro.Next.Infrastructure/Ai/Training/YoloDatasetExportService.cs` Z. 42–64
- `sidecar/sidecar/models/yolo_wrapper.py` Z. 235 ff. (ein Modellslot)
- `src/AuswertungPro.Next.Infrastructure/Ai/Pipeline/MultiModelAnalysisService.cs` Z. 277–319 · `PipelineEnvironmentOptions.cs` (ClassifierDecisionEnabled)
- `src/AuswertungPro.Next.Infrastructure/Ai/Teacher/VsaYoloClassMapFileStore.cs` Z. 55–71 (Auto-ID)
- `src/AuswertungPro.Next.Application/Ai/Evaluation/EvalSetBenchmarkModels.cs` (kein ExpectedSeverity)
- `src/AuswertungPro.Next.Infrastructure/Ai/KnowledgeBase/EmbeddingService.cs` (Text-Embeddings)

**Extern:**
- Ultralytics Train/Export/TensorRT — https://docs.ultralytics.com/modes/train · https://docs.ultralytics.com/integrations/tensorrt
- Unsloth Qwen3-VL (Windows, GGUF) — https://unsloth.ai/docs/models/tutorials/qwen3-how-to-run-and-fine-tune/qwen3-vl-how-to-run-and-fine-tune
- Qwen3-VL-8B GGUF (Modell + mmproj getrennt) — https://huggingface.co/Qwen/Qwen3-VL-8B-Instruct-GGUF · Ollama-Import — https://docs.ollama.com/import
- Autodistill — https://docs.autodistill.com/ · Label Studio ML-Backend — https://labelstud.io/guide/ml.html
- SAM-2.1-Feintuning — https://blog.roboflow.com/fine-tune-sam-2-1/
