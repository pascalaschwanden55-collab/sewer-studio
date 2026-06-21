# KI-Pipeline-Modernisierung — Vorschlag 2026-06-09

**Synthese aus 5 Agenten-Berichten:** Code-Ist-Stand (HEAD, zeilengenau belegt), Trainings-/Eval-Infrastruktur, Recherche Detektion/Klassifikation, Recherche VLM/OCR/Embeddings, Recherche Segmentierung/Grounding. Alle Web-Aussagen mit Quelle; Schaetzungen sind als solche markiert.

**Leitsatz des Vorschlags:** Der groesste Qualitaetsgewinn liegt NICHT in neuen Modellen, sondern darin, das Vorhandene richtig zu verdrahten. Der eigene VSA-Klassifikator (61,4% exakt auf dem 57er-clean) schlaegt das VLM (28,1%, nur LEER-Treffer) um Laengen — wird aber in der Produktion nur als Skip-Filter benutzt. Dazu kommen drei Stunden-Fixes (wirkungslose Schwellen-Map, zu strenge DINO-Schwellen, fehlendes SAM-Score-Gate), die sofort messbar sind. Modell-Tausche (SAM 2.1, OCR, VLM-Refresh) kommen erst danach und nur ueber den Kandidaten-Promotions-Weg.

---

## 1. Kandidaten-Pruefung gegen den Code-Ist-Stand

### 1.1 K.O.-Filter (harte Erkenntnisse)

Verworfen wurde alles, was gegen mindestens eine harte Regel verstoesst:

| K.O.-Kriterium | Rausgefallen |
|---|---|
| VLM-Tausch als VSA-Codier-Hebel (A/B Juni 2026: 8B 28% nur LEER, 2.5-VL 0%) | Jeder VLM-Tausch mit Codier-Begruendung. VLM-Kandidaten bleiben NUR fuer JSON-Stabilitaet/LEER/Beschreibung/OSD-Fallback im Rennen |
| Kein lokaler Betrieb | Grounding DINO 1.5/1.6 Pro, DINO-X, T-Rex2 (alle API-only, IDEA Research) |
| VRAM > 29 GB | Llama-4-Vision Scout (~109B MoE, >55GB Q4), Qwen3.6 35b (24GB Q4 — zu knapp neben Sidecar) |
| sm_120-inkompatibel / fragiler Stack | onnxruntime-gpu (kein Blackwell bis v1.24.0 — Beleg: GitHub-Issues #26177/#27875) → OCR bewusst CPU; MM-Grounding-DINO (MMCV-Build unter Windows+sm_120 notorisch fragil, unverifiziert); RT-DETRv3 (PaddlePaddle-Oekosystem) |
| Kein Ollama/GGUF-Pfad (Drop-in-Prinzip) | InternVL3.5, Molmo 2 (GGUF nicht gefunden — vor endgueltigem Verwerfen kurz auf HF pruefen), Pixtral |
| Forensik-Regel (keine KI-SR) | Real-ESRGAN (liegt ungenutzt in `sidecar/models/` — archivieren und als verboten markieren) |
| Von besseren Kandidaten ueberholt | D-FINE, YOLOv13, LW-DETR, EVA-02, OWLv2, MobileSAM/FastSAM/EfficientSAM/SAM-HQ, TrOCR/EasyOCR/docTR/Florence-2 (als OCR) |

### 1.2 Drop-in vs. Umbau (gegen den realen Code geprueft)

| Kandidat | Real Drop-in? | Befund aus dem Code | Verdikt |
|---|---|---|---|
| **YOLO26 (TRT-Engine)** | Bereits erledigt | `sidecar/models/yolo26m/yolo26m.build.json`: Engine wurde am 2026-06-02 auf der 5090 aus yolo26m gebaut (FP16, imgsz 1280, TRT 10.16). Der Recherche-Vorschlag "Engine-Refresh statt alter yolo11m.engine" ist gegenstandslos — **Agenten-Widerspruch, siehe Kap. 7** | Kein To-do ausser Fallback-Haertung (Paket 1) |
| **SAM 2.1 (hiera large)** | Fast: Gewichte liegen schon da (`sidecar/models/sam2/sam2.1_hiera_large.pt`), aber `sam_wrapper.py` ist hart auf `segment_anything.sam_model_registry` verdrahtet → neuer Wrapper noetig (API fast deckungsgleich: set_image/predict mit Box) | Kein Konfig-Tausch, kleiner Code-Tausch | Paket 4 (Tage) |
| **PP-OCRv5 via RapidOCR (CPU)** | Neuer Sidecar-Endpoint, keine C#-NuGets, 0 GB VRAM | OCR-Strategie 2 in `OsdMeterDetectionService.cs` ist ein leerer Stub; im Batch liest heute NUR Qwen das OSD | Paket 3 (Tage) |
| **Eigener Klassifikator produktiv** | Code existiert zu ~90%: `VsaCodeResolver.ResolveFromClassifier` ist fertig, hat aber **null Aufrufer**; Sidecar-`classify()` croppt (Ultralytics-Default) statt Letterbox; `active.json` existiert nirgends als Datei | Reine Verdrahtung + 30 Zeilen Python | Paket 2 — der Hebel |
| **Qwen3.5 9B / Gemma 4 (Ollama)** | Echter Drop-in (Modellname in Env/Settings); EvalSetBenchmark misst Kandidaten in Minuten (0,83 s/Frame) | Nur fuer LEER/JSON/Beschreibung testen, nicht als Codier-Hebel | Kleinmassnahme K1 |
| **Qwen3-Embedding 0.6b** | Ollama-Drop-in im Embedding-Pfad, ABER: Dimension aendert sich → Schema-Versionierung im KnowledgeBaseManager + Re-Index 21.860 Samples Pflicht (alt/neu nie mischen) | Kein blinder Tausch | Kleinmassnahme K2 |
| **YOLOE / YOLOE-26** | Klein (laeuft im installierten Ultralytics-8.4.56-Stack), aber neuer Sidecar-Pfad statt `dino_wrapper.py` | Zero-Shot auf VSA-Vokabular komplett unbelegt; **Agenten-Widerspruch Beobachten vs. Empfohlen, siehe Kap. 7** | Pilot NACH DINO-Schwellen-A/B |
| **SAM 2.1 Video-Modus** | Umbau: stateful Session passt nicht ins zustandslose Einzelframe-HTTP-Schema des Sidecars | Interessantester neuer Hebel fuer Streckenschaeden (MeterStart/End aus getrackten Frames statt Frame-Fenster-Heuristik) | Pilot nach Paket 4 (Wochen) |
| **RF-DETR (Apache 2.0)** | pip-Install, reines PyTorch cu128 — Windows unverifiziert; DETR-Output-Mapping im Sidecar noetig | Recall-Pilot fuer seltene Codes (BAI/BAJ/BCA), erst wenn Eval-Set v2 die Klassen messen kann | Pilot nach Paket 5 |
| **SAM 3 / 3.1** | Mittel: gated (HF-Antrag), Custom-SAM-License, ~2,9 s/Bild auf RTX PRO 6000 (Ultralytics-Messung) — fuer 3000-Video-Batches heftig | Zero-Shot-VSA unbelegt; A/B-Lektion gilt vermutlich analog | Beobachten |
| **DINOv2-Probe / ConvNeXt V2 / SigLIP 2** | Klein: eigener Letterbox-Dataloader umgeht die Ultralytics-Crop-Falle komplett | Backbone-Shootout fuer Klassifikator v2 | Paket 5 |
| **DEIMv2, MM-GDINO, EdgeTAM, MiniCPM-V 4.6, Qwen3.6 27B, arctic-embed2, bge-m3, Sewer-ML, ISWDS** | — | — | Beobachten (Begruendungen in den Recherche-Quellen, Kap. 8) |

---

## 2. Massnahmen-Pakete

### Paket 1 — Sofort-Fixes, Schwellen & ehrliche Telemetrie

**Ziel/Effekt:** Drei tote bzw. falsch wirkende Stellschrauben aktivieren und eine Messbasis fuer alle weiteren Pakete schaffen. Erwartet: mehr DINO-Recall (weniger `dino_no_boxes`), wirksame Per-Klassen-Schwellen, keine stillen Modell-Fallbacks mehr.
**Metrik (57er-clean):** EvalSetBenchmark `--yolo-detect-only` (Presence-Health-Sweep) vorher/nachher; Befund-Trefferquote darf nicht sinken; `dino_no_boxes`-Rate aus neuem Trace.

**Komponenten + Dateien:**
- `src/AuswertungPro.Next.Infrastructure/Ai/Pipeline/MultiModelAnalysisService.cs:238-254` (Schwellen-Map), `:149-186` (Sweep/Bypass), `:721-729` (50m-Fallback)
- `src/AuswertungPro.Next.Infrastructure/Ai/Configuration/AiSettingsFactory.cs:42-54, 75-76`
- `sidecar/sidecar/models/sam_wrapper.py:129-131`, `sidecar/sidecar/config.py`
- `sidecar/sidecar/telemetry.py:33-59`, `sidecar/sidecar/routes/dino.py`, `routes/sam.py`
- `src/AuswertungPro.Next.Infrastructure/Ai/Ollama/GpuModelSelector.cs:19-22`, `Ollama/OllamaConfig.cs`
- `src/AuswertungPro.Next.Infrastructure/Ai/VideoAnalysisPipelineService.cs` (Fallback-Warnung)

**Schritte:**
1. **Bug-Fix Schwellen-Map:** Mapping Klassenname→VSA-Hauptcode (`crack→BAB`, `fracture→BAC`, `root→BBA`, `deposit→BBC`, `connection→BCA`, …) einziehen — heute matcht wegen `ClassName.Split('_')[0]` gegen die realen names.json-Namen kein einziger Key, alles laeuft auf 0.25. Unit-Test gegen `yolo26m.names.json` (passt zur Regel "Tests fuer Recommendation-Logik").
2. **DINO-Schwellen-A/B:** `SEWERSTUDIO_DINO_BOX_THRESHOLD=0.25` / `TEXT=0.20` setzen (heute uebersteuert der C#-Default 0.30/0.25 immer die entschaerften Sidecar-Defaults), Lauf auf 57er-clean, Recall vs. False Positives messen; bei Erfolg C#-Default angleichen. Reiner Konfig-Tausch — das ist der bekannte "Maske zu selten"-Punkt.
3. **SAM-Score-Gate:** env-konfigurierbares `sam_min_score` (Start 0.5), Masken darunter als skipped/degraded — heute wird jede Maske akzeptiert, auch Score nahe 0 (~10 Zeilen).
4. **Telemetrie symmetrisch:** `write_yolo_detection` zu generischem `write_event` verallgemeinern, in DINO-/SAM-Routen aufrufen; TelemetrySummary als JSON neben den PipelineTrace. Heute ist der teuerste Pipeline-Teil (Qwen) der am schlechtesten vermessene.
5. **Sweep ehrlich machen:** Bypass-Frames `frame_class='sweep'` statt fake 'BCD'/'BCE'; cls-Vorfilter (CPU-billig) und Sidecar-Quality-Check auch fuer Sweep-Frames — heute koennen schwarze Frames bis zu Qwen (120s-Cap) durchlaufen.
6. **COCO-Fallback sichtbar machen:** `YoloResponse.ModelName` gegen 'yolo26m' pruefen, bei Fallback UI-Warnung (analog Ollama-Fallback). Die Telemetrie belegt einen realen stillen yolo11m.pt-Lauf am 2026-06-09 18:07.
7. **Modellwahl absichern:** GpuModelSelector/OllamaConfig von hartem `qwen2.5vl:32b/7b/3b` auf die freigegebenen Modelle heben (`qwen3-vl:8b-q8`, `nomic-embed-text`) und `AiVisionModel` in settings.json persistieren — sonst waehlt der Auto-Modus beim Wegfall der Env-Var still das laut A/B unbrauchbare qwen2.5-VL.
8. **EstimatedReachLengthM** aus der echten Haltungslaenge des Projekts setzen (HaltungRecord) statt fix 50.0 m.

**VRAM-Bilanz:** unveraendert. **Aufwand:** 2-3 Tage gesamt (Einzelschritte je Stunden).
**Risiko + Rollback:** niedrig. Schwellen-Senkung kann False Positives erhoehen → A/B entscheidet, Rollback = Env-Var zurueck bzw. Commit-Revert. Keine Modell-Promotion noetig.
**Abhaengigkeiten:** keine. Liefert die Telemetrie-Basis fuer Pakete 2-5.

---

### Paket 2 — Klassifikator produktiv schalten (DER Hebel)

**Ziel/Effekt:** Die Code-Entscheidung von VLM-Niveau (0/41 Befundcodes korrekt) auf Klassifikator-Niveau heben. Der beste vorhandene Erkenner (vsa_cls_v5_nocrop: 61,4% exakt, 70,7% Befund) wirkt heute nur als Skip-Filter; `ResolveFromClassifier` (Top-K + Meter + Import-Kontext, inkl. BCD/BCE-Regeln) hat keinen einzigen Aufrufer.
**Metrik (57er-clean):** exakt ≥ 61,4%, Befund ≥ 70,7%, LEER nicht unter Baseline 6/16; Schluesselklassen BAI/BAB/BBA/BDD/BAJ duerfen nicht sinken (Autopilot-Verdikt-Regeln). End-to-End-Paritaet: Sidecar-`/classify` muss auf denselben 57 Frames dasselbe liefern wie `eval_cls.py`.

**Komponenten + Dateien:**
- `sidecar/sidecar/models/yolo_wrapper.py:443-489` (classify), `sidecar/sidecar/config.py`
- NEU: `sidecar/models/active.json` (weights_path, sha256, imgsz, preprocessing, promoted_from)
- `src/AuswertungPro.Next.Infrastructure/Ai/VsaCodeResolver.cs` (Verdrahtung), `Pipeline/MultiModelAnalysisService.cs:190-213`
- NEU: testbarer C#-Voting-Service (separates Interface, gemaess Coding-Regeln)
- `training/vsa_classifier/train_autopilot.py:45-49` (Hash-Check)

**Schritte:**
1. **Preprocessing-Fix (kritisch, ~30 Zeilen Python):** `classify()` ruft heute `predict()` ohne imgsz/Letterbox auf — Ultralytics-Default ist Resize+CenterCrop. Das deployte no-crop-Modell wuerde in der Produktion genau die seitlichen Rand-Schaeden (BAI/BAJ/BCA) wieder verlieren, die der Fix retten sollte; die 61,4% gelten dann in der App nicht. Dasselbe Letterbox-Preprocessing wie `eval_cls.py` einziehen, imgsz/device/model_path als Settings + Metadaten in der Response. Device-Option GPU (heute hart CPU — bei imgsz 1024 unnoetig langsam neben einer 5090).
2. **active.json real machen:** `_resolve_cls_model_path()` liest zuerst `active.json`, loggt Modell+SHA256 beim Laden; stillen grundgeruest-v2/v1-Fallback durch klare Warnung/Fehler ersetzen. Die Datei existiert heute nirgends — der dokumentierte Promotions-Weg (Kandidat → Warden schreibt active.json → Sidecar laedt) wird damit erstmals real. Einziger Schreiber: model-promotion-warden.
3. **v5_nocrop promoten** (candidate→current via active.json), Paritaets-Messung Sidecar vs. eval_cls.py.
4. **ClassifierDecision-Pfad:** nach dem cls-Aufruf `ResolveFromClassifier` als fuehrende Code-Quelle; Qwen nur noch fuer OSD/Beschreibung/unsichere Faelle. Entscheidung inkl. Modellversion/Top-K/Schwelle in den PipelineTrace. Feature-Flag `ClassifierDecisionEnabled` (Default aus, bis Eval gruen).
5. **Temporal Voting:** kleines Mehrheits-Fenster (Code erst nach N konsistenten Frames pro Meterbereich) als eigener, getesteter C#-Service — daempft Einzelbild-Ausreisser (Hauptfehlerquelle der LEER→Befund-Kipper), reine C#-Logik, Thin-AI-konform.
6. **Autopilot-Leitplanke haerten:** Kontaminations-Check um SHA-256-Abgleich gegen `C:\KI_BRAIN\eval_set\_manifest.json` erweitern (der Builder fand 35 umbenannte Eval-Kopien, die der Namens-Check nicht sieht).
7. End-to-End-Lauf auf 57er-clean; Hidden-Set einmaliger Kontrollblick NACH der Entscheidung.

**VRAM-Bilanz:** +1-2 GB falls cls auf GPU (empfohlen), sonst 0.
**Aufwand:** 5-7 Tage. **Risiko + Rollback:** mittel — das Pipeline-Verhalten aendert sich grundlegend; die bekannte LEER-Schwaeche (37,5%) kann mehr False-Positive-Befunde erzeugen → Daempfer sind Voting + kalibrierte threshold_select-Schwelle (`vsa_cls_v6b_thr.json` liegt bereit). Rollback zweistufig: Feature-Flag aus (sofort) bzw. active.json auf vorherigen Eintrag (Modell).
**Abhaengigkeiten:** profitiert von Paket-1-Telemetrie, zwingend ist nur Schritt 1+2 vor Schritt 4. Paket 5 setzt dieses Paket voraus.

---

### Paket 3 — OSD-Meterstand deterministisch (PP-OCRv5, CPU)

**Ziel/Effekt:** Metrierung vom VLM entkoppeln. Heute liest im Batch NUR Qwen das OSD; faellt es aus, greift eine lineare Schaetzung mit fixer 50m-Annahme; der OCR-Fallback ist ein leerer Stub. Deterministisch, reproduzierbar, forensik-tauglich — Memory-Stand "Meter fragwuerdig" wird adressiert.
**Metrik:** **Ehrlich: das 57er-clean misst Codes, keine Meter.** Eigenes Mini-Meter-GT bauen (20-30 Frames mit lesbarem OSD, Meterstand von Hand abgelesen, eingefroren mit Hashes) und MAE/Trefferquote OCR vs. Qwen vs. linear messen. Sekundaermetrik: Anteil Frames mit Meter-Quelle "OCR" im Trace.

**Komponenten + Dateien:**
- NEU: `sidecar/sidecar/routes/ocr.py` — fixer ROI-Crop (OSD-Zone pro Kamerasystem konfigurierbar) + Threshold/Upscale + Digits-Whitelist via RapidOCR/ONNX, **bewusst CPU** (onnxruntime-gpu kann kein sm_120 bis v1.24.0 — belegt; PaddlePaddle-GPU-Blackwell-Status unverifiziert; ROI ist winzig, Millisekunden auf CPU, 0 GB VRAM)
- `sidecar/requirements*` (rapidocr — neue Python-Dependency, keine NuGet-Frage)
- `src/AuswertungPro.Next.Infrastructure/Ai/OsdMeterDetectionService.cs:63-65` (Stub ersetzen), `MultiModelAnalysisService.cs:492-505, 721-729`
- C#-Plausibilisierung (testbar): Regex aufs Meterformat, Monotonie bei Vorwaertsfahrt, Sprung-Erkennung

**Schritte:** Endpoint bauen → C#-Prioritaetskette OCR > Qwen > lineare Schaetzung (mit echter Haltungslaenge aus Paket 1) → Telemetrie-Event je Quelle → Mini-GT messen → MeterTimelineService/Training-Center mitziehen.
**VRAM-Bilanz:** 0 GB (CPU). **Aufwand:** 2-3 Tage.
**Risiko + Rollback:** niedrig. OSD-Fonts variieren je Kamerasystem → falls OCR versagt: Template-Matching auf den fixen Bitmap-Font (OpenCV) als 100% deterministischer Plan B. Rollback: Feature-Flag, Qwen-Pfad bleibt unangetastet bestehen.
**Abhaengigkeiten:** unabhaengig, voll parallel zu Paket 2 moeglich. Synergie: bessere Meterbasis fuer das Voting-Fenster aus Paket 2 und fuer Dedup/Streckenschaeden.

---

### Paket 4 — SAM 2.1 + kalibrierte Quantifizierung

**Ziel/Effekt:** Bessere Masken (SA-V 76,5-79,5 J&F vs. SAM 1), weniger VRAM, schnellere Segmentierung; und die mm/%-Werte von der festen 70%-Annahme loesen. Die SAM2.1-Gewichte liegen bereits ungenutzt im Repo.
**Metrik (57er-clean):** keine Verschlechterung der Befund-/QualityGate-Verteilung; degraded/skipped-Rate sinkt; SAM-Latenz (neue Telemetrie aus Paket 1) sinkt messbar. **Ehrlich: es gibt kein Masken-GT** — die Quantifizierungs-Verschiebung wird ueber Verteilungsvergleich + Stichproben-Sichtung (Audit-Bilder) bewertet, nicht ueber eine harte Masken-Metrik.

**Komponenten + Dateien:**
- NEU: `sidecar/sidecar/models/sam2_wrapper.py` (SAM2ImagePredictor, Gewichte `sidecar/models/sam2/sam2.1_hiera_large.pt`; Plan B: Ultralytics-Wrapper `SAM('sam2.1_b.pt')` — umgeht Windows-Setup-Fragen, gleicher Stack wie YOLO)
- `sidecar/sidecar/config.py`: Env-Switch `sam_backend=sam1|sam2` (Rollback-faehig)
- Box-Batching (`predict_torch` mit Box-Batches) statt der heutigen sequenziellen Boxen-Schleife
- `src/AuswertungPro.Next.Infrastructure/Ai/Pipeline/MaskQuantificationService.cs` + `MultiModelAnalysisService.cs:392,399-404`: AutoCalibrationService-Ergebnis (existiert unter Ai/Calibration) einmal pro Video bestimmen und an QuantifyAll/SegmentedFindingBuilder durchreichen — **die Ueberladungen existieren schon, es fehlt nur die Verdrahtung**
- Aufraeumen: `sidecar/models/README` mit Status je Ordner (aktiv/inaktiv/**verboten**: Real-ESRGAN), Ungenutztes nach `_unused/`; Hinweis: Ordnernamen `sam3/` und `grounding_dino_1.5/` sind irrefuehrend (enthalten SAM 1 vit_h bzw. Original-GDINO)

**Schritte:** Wrapper → A/B SAM1 vs. SAM2.1 auf 57er-clean (Masken, Quantifizierung, Findings, Latenz) → Kalibrierung verdrahten → zweiter A/B (unkalibriert vs. kalibriert) → Promotion per Backend-Switch.
**VRAM-Bilanz:** vit_h ~4-6 GB Laufzeit → SAM2.1-large ~2-4 GB (Schaetzung; Gewichte 2,4 GB → 0,9 GB). **Netto ca. -2 GB.**
**Aufwand:** 3-5 Tage. **Risiko + Rollback:** mittel-niedrig — bessere Kanten verschieben Extent-/Querschnitts-/Uhrlage-Werte leicht, Severity-Schaetzung haengt daran → deshalb der Verteilungsvergleich vor Promotion. Natives Windows offiziell nicht dokumentiert (Repo empfiehlt WSL) → Ultralytics-Pfad als Fallback. Rollback: `sam_backend=sam1`.
**Abhaengigkeiten:** Paket-1-Telemetrie fuer den Latenzvergleich. Unabhaengig von Paket 2/3. Oeffnet die Tuer fuer den spaeteren SAM2.1-Video-Piloten (Streckenschaeden).

---

### Paket 5 — Daten-Spur: Eval-Set v2, LEER-Gate, Klassen-Ausbau

**Ziel/Effekt:** Den Klassifikator ueber 61,4% hinaus heben und produktionsreif absichern. Drei bekannte Grenzen: (a) LEER nur 37,5% — jeder bessere Befund-Kandidat (v6b: 80,5% Befund) scheiterte am LEER-Ruecksetzer; (b) das 57er-Set ist statistisch zu schmal (BAB 2, BAJ 3 Frames — ein Frame kippt das Verdikt); (c) die 11-Klassen-Whitelist verwirft 11.797 gelabelte Frames, darunter **BCA (1867, seitlicher Anschluss!)**, BBC (793), BAA (679) — ein Codierer ohne diese Klassen kann zentrale Befunde prinzipiell nicht liefern.
**Metrik:** LEER-Accuracy (Ziel > 75% ohne Befund-Regression) auf 57er-clean; neue Klassen messbar erst via Eval-Set v2 (30-50 saubere Bilder pro Zielklasse, neues eingefrorenes Manifest mit Hashes — 120er/57er/63er bleiben unangetastet).

**Komponenten:** `tools/VideoLabelTool` (existiert, Port 8200, WYSIWYG-Frame + Code → `C:\KI_BRAIN\gold_labels`), ClassifierDatasetBuilder (Eval-Ausschluss per Name+Hash eingebaut), `train_autopilot.py`, `threshold_select.py`, eval-set-warden.

**Schritte:**
1. Quick-Win (Stunden): kalibrierte LEER-Schwelle aus `vsa_cls_v6b_thr.json` auf 57er-clean pruefen (top1<T → LEER).
2. LEER-/Hard-Negatives labeln (saubere Rohre, Reflexe, Wasser, Schaechte) — LEER hat heute nur 858 von 13.004 Frames. Gold kuratieren statt loeschen (Clean-Retrain-Erkenntnis: Auto-Loeschen bringt nichts).
3. Eval-Set v2 aus gold_labels bauen, einfrieren (Manifest + SHA-256), erst danach harte Promotions-Kriterien definieren.
4. Whitelist auf v2 erweitern (mind. BCA/BBC/BAA) → Datensatz-Neubau mit demselben Builder → Autopilot-Lauf.
5. Zweistufiges Gate als Kandidat: binaer Befund-ja/nein vor der Code-Klassifikation (loest den Zielkonflikt, an dem die einstufige 11-Klassen-Entscheidung nachweislich scheitert).
6. Backbone-Shootout fuer Klassifikator v2 im **eigenen Letterbox-Dataloader** (umgeht die Ultralytics-Crop-Falle vollstaendig): DINOv2-L frozen + Linear/Attentive-Probe (Apache, Training in Minuten, kaum Overfitting bei 25k Frames) vs. ConvNeXt V2 via timm (volle Transform-Kontrolle; Gewichte CC BY-NC 4.0 — dokumentieren) vs. Ultralytics-v5-nocrop-Baseline. SigLIP 2 nur nachziehen, falls Multi-Label (mehrere Schaeden pro Frame) limitiert. Entscheidung auf 57er-clean + v2.

**VRAM-Bilanz:** nur temporaer beim Training (Probe < 4 GB, ConvNeXt-Finetune ~10-16 GB); Inferenz-Ziel ≤ 2 GB.
**Aufwand:** 1-2+ Wochen, **Labeln dominiert** — Tooling ist komplett vorhanden.
**Risiko + Rollback:** technisch niedrig (alles laeuft ueber den Kandidaten-Mechanismus, nie direkt nach active.json); zeitlich das groesste Paket. Hidden-Set bleibt Kontrollblick.
**Abhaengigkeiten:** setzt Paket 2 zwingend voraus — ohne Produktiv-Verdrahtung und Promotions-Mechanik verpufft jeder Modellgewinn wieder.

---

### Kleinmassnahmen (kein eigenes Paket)

| # | Massnahme | Aufwand | Hinweis |
|---|---|---|---|
| K1 | **VLM-A/B mit `format=json`:** qwen3-vl:8b-q8 (Baseline, KEIN Fallback auf qwen3.5:9b oder qwen2.5-VL) via EvalSetBenchmark auf 57er-clean. Ollama erzwingt seit v0.5 grammatikbasiert valides JSON — der 0%-Parse-Fehler-Modus der Vorgaenger ist damit adressierbar; ab sofort Standard in jedem A/B. Gemessen wird NUR: LEER-Erkennung, JSON-Fehlerrate, OSD-Lesefaehigkeit, Beschreibungsqualitaet — **nicht** VSA-Codierung | Stunden | Drop-in (Modellname); sekundaere Kandidaten nur fuer Sekundaeraufgaben nach Verdikt |
| K2 | **Embedding-Tausch** nomic-embed-text → qwen3-embedding:0.6b (Platz 1 MTEB multilingual 70.58 fuer die Familie; nomic v1 ist primaer Englisch — die KB ist deutsch). Pflicht: Embedding-Modell-Versionierung im SQLite-Schema, kompletter Re-Index der 21.860 Samples, Retrieval-A/B davor/danach | 1-2 Tage | Re-Index-Dauer ist Hochrechnung (Minuten bis Stunden), nicht gemessen; alt/neu nie mischen |
| K3 | **models-Ordner aufraeumen:** README mit Status, Real-ESRGAN als "verboten (Forensik)" markieren/archivieren, Florence-2-Gewichte nach `_unused/` | Stunden | Verhindert Audit-Verwirrung |
| K4 | **Nightly-Wheels archivieren:** torch 2.12.0.dev+cu128-Wheels lokal sichern — der Lock-Header warnt, dass Nightly-Wheels vom Index verschwinden; das ist das stille Betriebsrisiko des sm_120-Stacks | Stunden | Reine Vorsorge |

---

## 3. Roadmap

| Phase | Zeitraum | Inhalt | Parallel moeglich |
|---|---|---|---|
| **1** | Woche 1 | **Paket 1** komplett (Bugfix Schwellen-Map zuerst — Stunden, sofort messbar), K3, K4 | K1 (VLM-A/B, Stunden) |
| **2** | Woche 1-2 | **Paket 2** (der Hebel — hoechster Effekt pro Aufwand nach Paket 1) | **Paket 3** (voellig unabhaengig) |
| **3** | Woche 2-3 | **Paket 4** | K2 (Embedding) |
| **4** | ab Woche 3, laufend | **Paket 5** (Labeln dominiert; Trainingslaeufe ~4,5h via Autopilot nachts) | — |
| **Piloten danach** (je nur nach Messung auf 57er-clean) | — | (a) YOLOE-26 vs. Grounding DINO A/B (Tage, gleicher Ultralytics-Stack) — erst NACH dem Schwellen-A/B aus Paket 1, sonst tauscht man eine unkalibrierte Komponente gegen eine andere; (b) SAM2.1-Video-Pilot fuer Streckenschaeden (Wochen, stateful Session noetig — liefert dem C#-Dedup ein echtes Instanz-Signal, Thin-AI-konform); (c) RF-DETR-S/M-Pilot fuer seltene Codes auf den Review-Queue-Boxlabels (braucht Eval-Set v2) | — |

### Bewusst NICHT tun (mit Begruendung)

- **VLM-Tausch als VSA-Codier-Hebel** — durch A/B Juni 2026 widerlegt (qwen3-vl:8b-q8 28% nur LEER, qwen2.5-VL 0%); jede VLM-Massnahme hier ist auf Sekundaeraufgaben begrenzt. Primary-Modell bleibt qwen3-vl:8b-q8.
- **SAM 3 / 3.1 jetzt** — gated, Custom-License, ~2,9 s/Bild auf Workstation-GPU (Ultralytics-Messung), Zero-Shot auf VSA-Vokabular unbelegt. Beobachten; fruehestens als Einzeltest auf dem 57er-clean.
- **DINO-X / GDINO 1.5/1.6 Pro / T-Rex2** — API-only, verletzt Lokal-Pflicht (3000 Videos, Datenschutz, Kosten).
- **MM-Grounding-DINO jetzt** — MMCV-Build unter Windows + PyTorch ≥2.7 + sm_120 ist die bekannte Schmerzstelle; YOLOE-26 erreicht das Ziel im vorhandenen Stack.
- **ByteTrack/OC-SORT-Umbau** — der SAM2.1-Video-Pilot ist der schlankere Weg zum Instanz-Signal; echtes MOT bleibt bewusst nicht in HEAD.
- **Sewer-ML-Pretraining jetzt** — erst Gold-Kuratierung ausreizen (Clean-Retrain-Erkenntnis: der Hebel ist Kuratieren, nicht mehr Rohdaten); CC BY-NC-SA und daenisches Coding-Mapping kosten zusaetzlich. ISWDS: kurze Autoren-Anfrage kostet nichts, mehr nicht.
- **YOLO-Detect-Retrain** — kein Orchestrator vorhanden, und die Evals weisen den Klassifikator als Hebel aus; erst wenn der Klassifikator-Pfad steht.
- **Qwen3.6 35b / Llama-4-Vision** — 24 GB Q4 zu knapp neben Sidecar bzw. komplett ausserhalb Budget.
- **Real-ESRGAN jemals aktivieren** — Forensik-Regel (KI-SR nie fuer Befund/Training).
- **Vierter Backbone im Klassifikator-Shootout (EVA-02 etc.)** — verbrennt nur Zeit; zwei Kandidaten + Baseline reichen fuer eine Entscheidung.

---

## 4. VRAM-Budgettabelle Ziel-Stack (max. 29 GB, nie alles gleichzeitig)

**Konstellation A — Multi-Model-Batchlauf (Standard):**

| Komponente | Heute | Ziel | VRAM Ziel | Delta |
|---|---|---|---|---|
| YOLO-Detect | yolo26m.engine (TRT FP16) | unveraendert | ~3 GB | 0 |
| YOLO-cls Vorfilter/Decider | grundgeruest/v5 auf CPU, croppt | v5_nocrop via active.json, Letterbox @1024, GPU | ~1-2 GB | +1-2 |
| Grounding | DINO SwinT, 0.30/0.25 | DINO SwinT, 0.25/0.20 (bis YOLOE-A/B) | ~4 GB (Schaetzung) | 0 |
| Segmentierung | SAM 1 vit_h | SAM 2.1 hiera-large | ~2-4 GB | **ca. -2** |
| VLM | qwen3-vl:8b-q8 (Ollama) | unveraendert (bzw. K1-Sieger gleicher Groesse) | 11,7 GB | 0 |
| Embeddings | nomic-embed-text | qwen3-embedding:0.6b | ~0,7 GB | +0,7 |
| OSD-OCR | — (Qwen) | PP-OCRv5 mobile, **CPU** | 0 GB | 0 |
| **Summe worst case** | | | **~22-25 GB** | Reserve 4-7 GB |

**Konstellation B — Referenz-/Zweitmeinungslauf (exklusiv, nie parallel zu A):** qwen3.6:27b (17 GB Q4) oder gemma4:26B-MoE (~19-22 GB) allein auf der GPU; DINO/SAM vorher evicten (GpuModelManager `evict_lru`). Anders als das heutige 32B-RAM-Modell laeuft die Referenz damit komplett auf der GPU.

**Konstellation C — Training (nachts, exklusiv):** Autopilot-Klassifikator-Training ~einstellig bis 16 GB; DINOv2-Probe < 4 GB; nichts davon parallel zum Batchlauf.

Hinweis: Der GpuModelManager haelt YOLO/DINO/SAM bewusst persistent (Tempo); das 29-GB-Budget ist nur Warnschwelle, Eviction nur nach OOM — Konstellation A bleibt mit Reserve darunter. Mehrere Laufzeit-VRAM-Werte sind Schaetzungen (siehe Kap. 7).

---

## 5. Mess-Strategie (vor jeder Promotion)

**Eiserne Regeln:**
1. Vor JEDEM Benchmark: eval-set-warden — Hash-Verifikation gegen `C:\KI_BRAIN\eval_set\_manifest.json` (frozen, sha256, 241 Hashes). Eval-Set NUR LESEN.
2. Entscheidungen fallen AUSSCHLIESSLICH auf dem 57er-clean. Das 63er-hidden ist einmaliger Kontrollblick NACH der Entscheidung — nie zum Tuning, nie iterativ ("nicht verbrennen").
3. Kein Modell geht ohne candidate→current-Weg produktiv: Kandidaten-JSON (Schema sewerstudio-model-candidate-v1, sha256 der Gewichte) → Mensch/model-promotion-warden schreibt `active.json` → Sidecar loggt Modell+Hash beim Laden. **Rollback ist immer: active.json auf den vorherigen Eintrag zuruecksetzen** (plus Feature-Flags fuer Verhaltenspfade).
4. Kein Eval-Frame in Trainingsdaten: Namens- UND Hash-Check (Paket 2, Schritt 6).

**Pro Paket:**

| Paket | Werkzeug | Akzeptanzkriterium (57er-clean) | Dauer Messung |
|---|---|---|---|
| 1 (DINO/Schwellen) | EvalSetBenchmark `--yolo-detect-only` + neuer Trace | Presence-Recall steigt, Befund-Quote sinkt nicht; `dino_no_boxes`-Rate sinkt | Minuten |
| 2 (Klassifikator) | eval_cls.py + Sidecar-Paritaetstest + End-to-End-Pipeline-Lauf | exakt ≥ 61,4%, Befund ≥ 70,7%, LEER ≥ 6/16; Schluesselklassen nicht schlechter; Sidecar == eval_cls auf identischen Frames | Minuten (Eval) |
| 3 (OSD-OCR) | NEUES Mini-Meter-GT (20-30 Frames, eingefroren) | Meter-MAE OCR < Qwen-Pfad; Monotonie-Verletzungen → 0 | Minuten |
| 4 (SAM 2.1) | Pipeline-Lauf auf 57er-Frames + Telemetrie + Audit-Bild-Stichprobe | Findings-/QualityGate-Verteilung ohne Regression; degraded-Rate und SAM-Latenz sinken | < 1 h |
| 5 (Daten) | Autopilot-Verdikt (Counts-Regeln) auf 57er-clean + Eval-Set v2 | LEER > 75% ohne Befund-Regression; neue Klassen via v2 messbar | ~4,5 h/Kandidat (Training inkl.) |
| K1 (VLM) | EvalSetBenchmark, `format=json` aktiv | JSON-Fehlerrate, LEER-Quote, OSD-Lesequote — Codier-Quote wird berichtet, entscheidet aber nicht | Minuten/Kandidat |
| K2 (Embedding) | Retrieval-A/B (feste Query-Liste) vor/nach Re-Index | Top-K-Relevanz auf deutschen Fachqueries besser oder gleich | Stunden |

**Luecken der Messbarkeit (ehrlich):** Fuer Meter (Paket 3) und Masken (Paket 4) existiert heute KEIN Ground-Truth — beide Pakete enthalten deshalb den Bau kleiner, eingefrorener Zusatz-Messsets bzw. Verteilungsvergleiche statt harter Metriken. Das 57er-Set ist fuer Architektur-Richtungsentscheide ok, fuer Produktiv-Promotionen statistisch duenn (1 Frame kippt Verdikte) — Paket 5 behebt das mit Eval-Set v2.

---

## 6. Erwartete Gesamtwirkung

| Dimension | Heute (belegt) | Nach Paket 1-4 (erwartet) |
|---|---|---|
| VSA-Code-Quelle im Batch | Qwen-VLM, 28,1% exakt (nur LEER) | Eigener Klassifikator, ≥ 61,4% exakt / ≥ 70,7% Befund (gemessene Baseline; mit Voting + Schwellen-Fixes plausibel darueber — unbelegt bis zur Messung) |
| Per-Klassen-YOLO-Schwellen | wirkungslos (Namens-Mismatch) | aktiv, getestet |
| DINO-Flaschenhals | 0.30/0.25, "Maske zu selten" | 0.25/0.20 nach A/B |
| Meterstand | nur LLM, sonst 50m-Annahme | deterministisches OCR + Plausibilisierung, LLM nur Fallback |
| Quantifizierung | feste 70%-Annahme | kalibriert pro Video |
| Modell-Governance | stiller COCO-/grundgeruest-Fallback, kein active.json | active.json + Hash-Logging + UI-Warnungen, Promotions-Weg real |
| Telemetrie | nur YOLO persistiert | alle Stufen, Tausch-Entscheidungen datenbasiert |
| VRAM Batchlauf | ~24-27 GB (Schaetzung) | ~22-25 GB mit mehr Reserve |

---

## 7. Ehrlichkeit: Widersprueche und unsichere Datenlage

### 7.1 Widersprueche zwischen den Agenten (explizit)

1. **YOLO-Engine:** Der Detektion-Recherche-Agent (und der Auftragsrahmen) nennen eine "alte yolo11m.engine" und empfehlen einen Engine-Refresh. Der Ist-Stand-Agent belegt per `yolo26m.build.json`, dass die produktive Engine bereits **yolo26m** ist (gebaut 2026-06-02 auf der 5090). **Aufloesung: Ist-Stand gewinnt (Primaerbeleg im Repo); der Engine-Refresh entfaellt.** Relevant bleibt nur: Die Telemetrie zeigt einen realen stillen yolo11m.pt-COCO-Fallback-Lauf am 2026-06-09 → Warnungs-Fix in Paket 1.
2. **YOLOE:** Detektion-Agent sagt "Beobachten", Segmentierung-Agent "Empfohlen" (als DINO-Ersatz). Beide nennen dieselbe Bedingung: erst DINO-Schwellen-A/B. **Aufloesung: Schwellen zuerst (Paket 1), YOLOE-26 danach als Pilot — kein Widerspruch in der Reihenfolge, nur im Label.**
3. **Florence-2:** Eine Auftrags-Annahme ("laeuft als Shadow im Sidecar") wurde vom Segmentierung-Agent widerlegt und vom Ist-Stand-Agent bestaetigt: null Code-Referenzen, nur ungenutzte Gewichte.
4. **Pruefdatum:** Mehrere Agenten datieren Env-/Web-Pruefungen auf den 2026-06-10, obwohl heute der 2026-06-09 ist — vermutlich Zeitzonen-/Protokollfehler, inhaltlich ohne Folge, aber notiert.
5. **sm_120/PyTorch:** Such-Zusammenfassungen behaupteten "stabile Builds nur bis sm_90"; lokal ist das Gegenteil fuer den Nightly-Kanal verifiziert (torch 2.12.0.dev+cu128, sm_120 in der Arch-Liste, 5090 erkannt, TRT 10.16). **Aufloesung: lokaler Befund gewinnt; Restrisiko = Nightly-Wheels verschwinden vom Index → K4.**

### 7.2 Unsichere/unverifizierte Punkte (uebernommen aus den Recherchen)

- **VRAM-Laufzeitwerte** fast aller Kandidaten (SAM 2.1, DINO SwinT, RF-DETR-Training, Gemma-4-Varianten) sind Schaetzungen aus Modellgroesse + Erfahrung, keine Messungen. Gemma-4-Drittquellen widersprechen sich (24 GB+ vs. 20-GB-Q4-Datei) — vor Einsatz selbst messen.
- **Hersteller-Benchmarks ohne unabhaengige Verifikation:** Qwen3.5 "uebertrifft Qwen3-VL", Qwen3.6-Claims, MiniCPM-OCRBench-Fuehrung, SAM-3.1-Durchsatz (nur Meta-Blog).
- **Q8-Tags** fuer qwen3.5:9b/qwen3.6:27b/gemma4 auf Ollama nicht einzeln verifiziert; genannte GB-Werte sind vermutlich Q4_K_M.
- **PaddlePaddle-GPU auf Blackwell:** unverifiziert — deshalb bewusst der CPU-Pfad (belegt ist nur der fehlende sm_120-Support der offiziellen onnxruntime-gpu-Builds bis 1.24.0).
- **RF-DETR/SAM2/SAM3 unter nativem Windows:** plausibel (reines PyTorch bzw. Ultralytics-Wrapper), aber ungetestet; SAM2-Repo empfiehlt offiziell WSL.
- **SAM-2-Trackqualitaet auf Kanal-TV** (fahrende Kamera, Zoom, schlechtes Licht): durch keine Quelle abgedeckt — der Streckenschaden-Nutzen MUSS per Pilot belegt werden.
- **Zero-Shot von SAM 3 / YOLOE-26 auf VSA-Vokabular:** komplett unbelegt; LVIS/COCO-Zahlen sind nicht uebertragbar — Pflicht-Eval vor jeder Entscheidung.
- **Re-Index-Dauer K2** (Minuten bis < 2 h fuer 21.860 Samples): Hochrechnung, nicht gemessen. KB-Bestand 21.860 selbst unverifiziert (SQLite wegen Lock-Risiko nicht geoeffnet).
- **Reale DINO-/SAM-/Qwen-Latenzen:** keine persistierten Messwerte (nur YOLO ~10 ms/~70 ms belegt) — genau deshalb Telemetrie in Paket 1 vor jedem Tausch-Urteil.
- **SAM-3-Parameterzahl** widerspruechlich (848M Paper vs. 473,6M Ultralytics) — ungeloest, fuer den Vorschlag irrelevant.
- **ISWDS-Verfuegbarkeit** (kein oeffentlicher Download gefunden) und **Molmo-2-GGUF** (koennte seit Dez 2025 nachgereicht sein): vor endgueltigem Abhaken je eine Kurz-Pruefung.
- **Trainings-/Eval-Laufzeiten** (~4,5 h Autopilot-Durchlauf, eval_cls Minuten): aus Report-Zeitstempeln geschaetzt, kein Log gelesen.
- **Nicht geprueft im Ist-Stand:** QualityGate-/EvidenceVector-Details, SingleFrameMultiModelService/Live-Pfad, VideoFullAnalysisService-Dedup-Details, Engine-zu-Runtime-Versionspassung (vermutlich konsistent, nicht durch Lauf verifiziert). Eine theoretische Restunschaerfe bei der Env-Var-Pruefung (PowerShell unterdrueckt Null-Zeilen) ist dokumentiert, aber unwahrscheinlich relevant.

---

## 8. Quellen (aus den Recherche-Agenten uebernommen)

**Detektion/Klassifikation:**
https://docs.ultralytics.com/models/yolo26 · https://arxiv.org/abs/2509.25164 · https://github.com/ultralytics/ultralytics/releases · https://github.com/THU-MIG/yoloe · https://arxiv.org/abs/2503.07465 · https://arxiv.org/html/2602.00168v1 · https://github.com/roboflow/rf-detr · https://rfdetr.roboflow.com/develop/ · https://blog.roboflow.com/rf-detr-segmentation/ · https://github.com/Intellindust-AI-Lab/DEIMv2 · https://github.com/Intellindust-AI-Lab/DEIM · https://arxiv.org/abs/2412.04234 · https://datature.io/blog/real-time-object-detection-d-fine · https://github.com/ArgoHA/D-FINE-seg · https://arxiv.org/abs/2602.23043 · https://github.com/clxia12/RT-DETRv3 · https://openaccess.thecvf.com/content/WACV2025/html/Wang_RT-DETRv3_Real-Time_End-to-End_Object_Detection_with_Hierarchical_Dense_Positive_Supervision_WACV_2025_paper.html · https://github.com/iMoonLab/yolov13 · https://arxiv.org/abs/2506.17733 · https://github.com/Atten4Vis/LW-DETR · https://github.com/facebookresearch/dinov3 · https://arxiv.org/abs/2508.10104 · https://ai.meta.com/resources/models-and-libraries/dinov3-license/ · https://arxiv.org/html/2509.06467v1 · https://github.com/facebookresearch/ConvNeXt-V2 · https://github.com/facebookresearch/ConvNeXt-V2/blob/main/LICENSE · https://github.com/huggingface/pytorch-image-models · https://arxiv.org/abs/2502.14786 · https://huggingface.co/blog/prithivMLmods/siglip2-finetune-image-classification · https://github.com/PRITHIVSAKTHIUR/FineTuning-SigLIP-2 · https://github.com/baaivision/EVA · https://vap.aau.dk/sewer-ml/ · https://arxiv.org/abs/2103.10895 · https://www.mdpi.com/2076-3417/15/20/11096

**VLM/OCR/Embeddings:**
https://github.com/PaddlePaddle/PaddleOCR · https://arxiv.org/html/2507.05595v1 · https://github.com/RapidAI/RapidOCR · https://huggingface.co/blog/baidu/ppocrv5 · https://github.com/microsoft/onnxruntime/issues/26177 · https://github.com/microsoft/onnxruntime/issues/27875 · https://github.com/timminator/VideOCR · https://blog.google/innovation-and-ai/technology/developers-tools/gemma-4/ · https://ollama.com/library/gemma4 · https://www.promptquorum.com/local-llms/top-open-source-models-ollama · https://aurigait.com/blog/gemma-4-features-benchmarks-guide/ · https://ollama.com/library/qwen3.5 · https://en.wikipedia.org/wiki/Qwen · https://codersera.com/blog/qwen-3-5-complete-guide-2026/ · https://ollama.com/library/qwen3-vl · https://ollama.com/library/qwen3.6 · https://github.com/QwenLM/Qwen3.6 · https://qwen.ai/blog?id=qwen3.7 · https://github.com/OpenBMB/MiniCPM-V · https://ollama.com/openbmb/minicpm-v4.6 · https://huggingface.co/openbmb/MiniCPM-V-4_5-gguf · https://huggingface.co/openbmb/MiniCPM-V-4.6-gguf · https://ollama.com/library/qwen3-embedding · https://arxiv.org/pdf/2506.05176 · https://awesomeagents.ai/leaderboards/embedding-model-leaderboard-mteb-march-2026/ · https://knowledgesdk.com/blog/embedding-model-comparison-2026 · https://ollama.com/library/snowflake-arctic-embed2 · https://arxiv.org/html/2412.04506v2 · https://www.snowflake.com/en/engineering-blog/snowflake-arctic-embed-2-multilingual/ · https://www.morphllm.com/ollama-embedding-models · https://zeroentropy.dev/articles/best-multilingual-embedding/ · https://www.bentoml.com/blog/a-guide-to-open-source-embedding-models · https://app.ailog.fr/en/blog/news/embedding-models-2026 · https://unstract.com/blog/best-opensource-ocr-tools/ · https://blog.roboflow.com/florence-2-ocr/ · https://www.unix-ag.uni-kl.de/~auerswal/ssocr/ · https://roboflow.com/compare/trocr-vs-easyocr · https://blog.roboflow.com/local-vision-language-models/ · https://huggingface.co/blog/daya-shankar/open-source-llm-models-to-run-locally · https://allenai.org/blog/molmo2 · https://github.com/allenai/molmo2

**Segmentierung/Grounding:**
https://github.com/facebookresearch/sam2 · https://docs.ultralytics.com/models/sam-2 · https://github.com/tier4/sam2_trt_inference · https://docs.pytorch.org/TensorRT/tutorials/_rendered_examples/dynamo/torch_export_sam2.html · https://github.com/pytorch/pytorch/issues/159207 · https://blog.roboflow.com/sam-2-video-segmentation/ · https://github.com/facebookresearch/sam3 · https://ai.meta.com/blog/segment-anything-model-3/ · https://arxiv.org/abs/2511.16719 · https://docs.ultralytics.com/models/sam-3 · https://blog.roboflow.com/what-is-sam3/ · https://github.com/facebookresearch/sam3/blob/main/LICENSE · https://github.com/dataplayer12/SAM3-TensorRT/ · https://docs.ultralytics.com/models/yoloe · https://arxiv.org/pdf/2602.00168 · https://learnopencv.com/yoloe-tutorial-real-time-open-vocabulary-detection/ · https://github.com/open-mmlab/mmdetection/blob/main/configs/mm_grounding_dino/README.md · https://github.com/IDEA-Research/Grounding-DINO-1.5-API · https://github.com/IDEA-Research/DINO-X-API · https://github.com/IDEA-Research/T-Rex · https://arxiv.org/abs/2405.10300 · https://www.labellerr.com/blog/advancing-object-detection-and-segmentation-a-deep-dive-into-owlv2-single-shot-detection/ · https://arxiv.org/pdf/2405.14874 · https://github.com/facebookresearch/EdgeTAM · https://openaccess.thecvf.com/content/CVPR2025/html/Zhou_EdgeTAM_On-Device_Track_Anything_Model_CVPR_2025_paper.html · https://arxiv.org/html/2501.07256v1 · https://github.com/ChaoningZhang/MobileSAM · https://docs.ultralytics.com/models/fast-sam · https://docs.ultralytics.com/models/mobile-sam · https://arxiv.org/html/2312.00863v1
