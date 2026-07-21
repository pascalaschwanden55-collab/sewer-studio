# Trainingskonzept — KI-Pipeline SewerStudio

**Version:** 1.0 · **Datum:** 2026-07-16 · **Autor:** erstellt für Pascal (Solo-Entwicklung)
**Scope:** Strategisches Konzept (Roadmap, Phasen, Datenstrategie, Aufwand) für alle vier Pipeline-Modelle
**Bezug:** `CLAUDE.md`, aktive Taxonomie `vsa_kek_2020_catalog_manifest.json`

---

## 1. Management-Summary (Kernidee in 5 Sätzen)

1. Die vier Modelle werden **nicht gleich trainiert** — nur YOLO ist der echte „Lerner", DINO und SAM sind vor allem **Label-Werkzeuge**, Qwen3-VL wird per LoRA für **Text/Plausibilität** angepasst.
2. Das eigentliche Projekt ist nicht ein einzelner Trainingslauf, sondern eine **Daten-Engine** (Flywheel): auto-labeln → korrigieren → nachtrainieren → bessere Vorschläge → schneller labeln.
3. Bei „einigen hundert Labels" ist der Hebel Nr. 1 das **Bootstrapping** per Grounding DINO + SAM (Autodistill-Prinzip), um in Tagen auf Tausende Labels zu kommen.
4. Der teuerste Fehler wäre ein **falscher Datensplit**: getrennt wird immer nach **Haltung/Video**, nie nach Einzelframe — sonst sind die Metriken wertlos.
5. Erfolg wird an **Recall bei schweren Schäden (Severity 4–5)** und einem klassen-gewichteten F2-Score gemessen, nicht an einer nackten mAP-Zahl.

---

## 2. Ausgangslage & Ziel

### Ist-Zustand
- ~3000 Rohvideos aus Kanal-TV-Exporten, OSD mit Meterstand/Haltungsname/Datum.
- Einige hundert manuell/halbautomatisch gelabelte Frames; Pipeline erzeugt bereits Vorschläge.
- Vorhandene Bausteine, die als Daten-Infrastruktur dienen: `TrainingSamplesStore` (JSON-Samples), `KnowledgeBaseManager` (SQLite + Embeddings), `TemporalFindingDeduplicator`, `TemporalCodeVotingService`, `QualityGateService`.
- Modelle im Sidecar: `yolo26m` (bzw. TensorRT-Engine), Grounding DINO Swin-B, SAM 2.1 hiera-large, Qwen3-VL (8b-q8 / 2b Fallback).

### Zielbild
Eine trainierte, reproduzierbare Pipeline, die auf ungesehenen Haltungen VSA-KEK-Codes zuverlässig **erkennt** (YOLO/DINO), **abgrenzt/quantifiziert** (SAM), **beschreibt und plausibilisiert** (Qwen), mit **kalibriertem QualityGate** und einem **eingefrorenen Test-Set** als ehrlichem Maßstab.

### Rahmenbedingungen (nicht verhandelbar)
- **Thin-AI beibehalten:** Training ist ein **offline, separater Prozess** — die C#-Laufzeitlogik bleibt unberührt. Es wandern nur *Gewichte* (YOLO/TensorRT, LoRA-Adapter) zurück in den Sidecar.
- **VRAM-Budget 29 GB** gilt für die **Laufzeit**. Training darf die volle 32-GB-Karte nutzen, aber **nie gleichzeitig** mit dem produktiven Inferenz-Betrieb.
- Solo-Entwicklung, kein kommerzielles Ziel → Werkzeuge und Aufwand müssen „ein-Mensch-tauglich" bleiben.

---

## 3. Trainingsphilosophie: Rollenverteilung der vier Modelle

Der wichtigste konzeptionelle Schritt ist zu verstehen, dass die Modelle **unterschiedliche Jobs** haben und deshalb **unterschiedlich viel Training** brauchen.

| Modell | Rolle in der Pipeline | Trainingsart | Datenbedarf | Priorität |
|---|---|---|---|---|
| **YOLO** (`yolo26m`) | Primär-Detektor auf VSA-Klassen, produktives Arbeitspferd | Supervised **Fine-Tuning** (echtes Training) | **Hoch** — Hunderte–Tausende Instanzen je Klasse | **P1** |
| **Qwen3-VL** | Befund-Beschreibung, Code-Begründung, Plausibilitätsprüfung, strukturierter JSON-Output | **LoRA/QLoRA** Instruction-Tuning | **Mittel** — kuratierte Bild→JSON-Paare | **P2** |
| **Grounding DINO** (Swin-B) | Auto-Labeler (Bootstrapping) + Zero-Shot für seltene/neue Klassen | Meist **Zero-Shot** (Prompt-Engineering); optional leichtes Fine-Tuning | **Niedrig** | **P3** |
| **SAM 2.1** | Quantifizierungs-Masken (Ausdehnung %, Querschnitt %), box-getrieben | **Kein Training** initial; optional später Mask-Decoder-Feintuning | **Niedrig** | **P4** |

**Warum diese Verteilung?**
- YOLO ist schnell, deterministisch und läuft im Dauerbetrieb → hier lohnt jedes Label. Es ist das einzige Modell, das man mit eigenen Daten „richtig" trainiert.
- Grounding DINO kann Objekte per Textprompt *ohne* Training finden. Sein größter Wert liegt **vor** dem Training: es labelt die Rohframes vor. Als Laufzeit-Detektor ist es zu langsam/teuer für alles außer seltenen Fällen.
- SAM segmentiert generisch schon sehr gut, sobald es eine Box als Prompt bekommt. Ein Feintuning betrifft nur den leichten **Mask-Decoder** und lohnt erst, wenn die Quantifizierung messbar hakt.
- Qwen3-VL soll **nicht detektieren**, sondern das *Ergebnis* der Detektoren in sauberen, schema-konformen Text/Code gießen und grobe Unplausibilitäten abfangen. Dafür reichen vergleichsweise wenige, aber sehr saubere Instruktionspaare.

---

## 4. Herzstück: Daten-Strategie (die „Data Engine")

Bei einigen hundert Labels ist **Daten**, nicht Modellarchitektur, der Engpass. Dieser Abschnitt ist der Kern des Konzepts.

### 4.1 Label-Taxonomie an VSA-KEK verankern
- **Single Source of Truth:** `vsa_kek_2020_catalog_manifest.json`. Jede Detektor-Klasse muss eindeutig auf Hauptcode (+ ggf. Char1) mappen.
- **Strategische Entscheidung — Granularität:** Der **Detektor** sollte auf **Hauptcode / Char1-Ebene** trainiert werden (überschaubar viele, visuell trennbare Klassen, z. B. BAB-Riss, BAC-Bruch, BBA-Wurzeln, BCA-Anschluss …). Die feine Auflösung (Char2/Lage, Uhrlage, Severity, Ausdehnung) wird **nicht** dem Detektor aufgebürdet, sondern nachgelagert durch C#-Logik + SAM-Geometrie + Qwen bestimmt.
  → Grund: 100+ Blatt-Codes als YOLO-Klassen sind mit ein paar tausend Bildern **nicht lernbar** (extremes Long-Tail). Wenige robuste Klassen + regelbasierte Verfeinerung schlagen viele schwache Klassen.

### 4.2 Bootstrapping per Auto-Labeling (Autodistill-Prinzip)
- **Idee:** Große Foundation-Modelle (Grounding DINO + SAM) erzeugen aus Textprompts automatisch Boxen/Masken → Mensch prüft/korrigiert nur → daraus wächst das YOLO-Trainingsset. Genau dafür ist das Muster „Foundation-Modell labelt, kleines Modell lernt" gedacht (Autodistill).
- **Wirkung:** aus „einigen hundert" werden in kurzer Zeit **Tausende** review-fertige Labels. Der Mensch wird vom Zeichner zum Prüfer.
- **Prompt-Bibliothek:** deutsch/englische Schadensvokabeln pflegen (z. B. „root intrusion", „crack", „displaced joint", „lateral connection"), pro VSA-Klasse getunt.

### 4.3 Active-Learning-Schleife (das Flywheel)
Kreislauf, der nach jedem YOLO-Stand die *wertvollsten* nächsten Frames zum Labeln auswählt:

1. Aktuelles YOLO über ungelabelte Frames laufen lassen.
2. **Informative Frames** priorisieren: niedrige Konfidenz, Modell-Uneinigkeit, oder per KB-Embedding (`KnowledgeBaseManager`) als *neuartig/selten* erkannt.
3. Diese gezielt von Hand korrigieren (nicht zufällig labeln).
4. In `TrainingSamplesStore` mergen, YOLO nachtrainieren, zurück zu 1.

→ So fließt Aufwand dorthin, wo das Modell wirklich schwach ist (v. a. seltene, schwere Schäden), statt in das 1000. Bild eines Rohranfangs.

### 4.4 Frame-Sampling aus Video
- **Nahe-Duplikate vermeiden:** aufeinanderfolgende Frames sind fast identisch → nur diverse Frames ziehen (Meterstand-Schritte aus OSD, Szenenwechsel, Dedup-Logik analog `TemporalFindingDeduplicator`).
- **Diversität erzwingen** über DN (150/300/600+), Kameratypen, Betreiber/Jahrgänge, Lichtverhältnisse — sonst lernt das Modell nur „eine Kamera".

### 4.5 Split-Politik — **kritisch**
- **Immer nach Haltung/Video splitten, nie nach Frame.** Sonst landen quasi-identische Nachbarframes gleichzeitig in Train und Test → geschöntes Ergebnis, das in der Praxis zusammenbricht (dokumentierter, klassischer Leakage-Fehler bei Video-Defekterkennung).
- **Test-Set einfrieren:** ein Satz **kompletter Haltungen**, die nie ins Training gelangen und idealerweise **nur von Menschen** gelabelt sind (Gold-Standard).
- Bei seltenen Klassen zusätzlich **stratifizieren**, damit jede Klasse in Val/Test überhaupt vorkommt.

### 4.6 Klassen-Ungleichgewicht / Long-Tail
VSA-Codes sind extrem unbalanciert (BCD/BCA häufig, BAC-B Totalbruch selten). Gegenmaßnahmen:
- **Gezieltes Mining** seltener Klassen über DINO-Prompts (aktiv danach suchen statt abwarten).
- **Oversampling & domänengerechte Augmentierung** der seltenen Klassen.
- **Klassen-gewichteter Loss** und **pro-Klasse-Recall-Ziele** statt Gesamt-Accuracy.
- **Warm-Start mit öffentlichen Daten:** `Sewer-ML` (1,3 Mio. annotierte Kanalbilder, 17 Defektklassen) als Vortraining/Feature-Warmstart nutzen, bevor auf die eigenen VSA-Daten fein-getunt wird.

### 4.7 Versionierung & Reproduzierbarkeit
- **Datensatz-Versionierung** (z. B. DVC oder klare Snapshot-Ordner): jeder Trainingslauf referenziert einen eingefrorenen Datenstand.
- **Kopplung:** Modellversion ↔ Datensatzversion ↔ Manifest-Version (`vsa_kek_2020…`) zusammen protokollieren — sonst ist später nicht rekonstruierbar, *warum* ein Modell etwas tut.

---

## 5. Modell-für-Modell Trainingsplan

### 5.1 YOLO — Primär-Detektor (P1)
- **Start:** von `yolo26m` warm starten; optional zuerst auf Sewer-ML vortrainieren, dann auf eigene VSA-Klassen fein-tunen.
- **Augmentierung mit Domänen-Vorsicht:** **kein vertikales Spiegeln**, wenn Uhrlage/Lage (12:00 Scheitel vs. 6:00 Sohle) semantisch zählt — sonst werden Lage-Informationen zerstört. Helligkeit/Kontrast/leichtes Rauschen sind dagegen sinnvoll (Kanal-Videos sind dunkel/verrauscht).
- **Datengefühl (Richtwerte aus der Literatur):** ~1800 Bilder → ~71 % AP, ~3800 → ~75 % mAP; ernstzunehmende Systeme nutzen mehrere tausend Bilder aus vielen Haltungen. Ziel also: über das Flywheel Richtung **mehrere tausend diverse, per-Haltung-getrennte** Instanzen je relevanter Klasse.
- **Auslieferung:** nach jedem stabilen Stand **TensorRT-Engine neu bauen** (GPU-spezifisch) und in den Sidecar geben.
- **Iteration:** dieses Modell durchläuft das Flywheel (Abschnitt 4.3) am häufigsten.

### 5.2 Qwen3-VL — Beschreibung, Begründung, Plausibilität (P2)
- **Methode:** LoRA/QLoRA-Feintuning — das 8B-Modell passt mit QLoRA locker auf die RTX 5090 (QLoRA-8B läuft schon ab ~16 GB). Werkzeuge: LLaMA-Factory, ms-swift oder Unsloth.
- **Trainingsdaten:** kuratierte Paare *(Frame + Kontext aus der Pipeline) → strikter JSON-Output* nach eurem Schema. Startgröße realistisch **einige hundert** sehr saubere Beispiele, dann über das Flywheel wachsen (Annotationsqualität schlägt Menge).
- **Rolle scharf halten:** Qwen **detektiert nicht** und setzt keine Codes autoritativ — es beschreibt, begründet, quantifiziert *sprachlich* und markiert Unplausibilitäten. Die **Code-Hoheit bleibt in C#**. Strikte JSON-Schema-Erzwingung (kein Freitext) verhindert Halluzinationen.

### 5.3 Grounding DINO — Auto-Labeler & Seltene Klassen (P3)
- **Primärnutzen:** Labeling-Engine für Phase 1 (Bootstrapping) und laufendes Rare-Class-Mining — **nicht** als Dauer-Laufzeitdetektor.
- **Training:** zunächst **keins** — Wert kommt aus gutem Prompt-Engineering. Ein **leichtes Fine-Tuning** von Swin-B lohnt nur, wenn für bestimmte Klassen der Recall beim Vorlabeln nachweislich zu niedrig ist.

### 5.4 SAM 2.1 — Quantifizierungs-Masken (P4)
- **Betrieb:** box-getrieben (Box von YOLO/DINO als Prompt), **Zero-Shot**, kein Training nötig für den Start.
- **Optionaler Ausbau:** wenn Ausdehnung-/Querschnitts-Messung systematisch ungenau ist, gezielt den **Mask-Decoder** auf kanalspezifische Masken fein-tunen (leichtgewichtig, wenige hundert Masken). Niedrigste Priorität.

---

## 6. Phasen-Roadmap

Aufwand als grobe Bandbreite für **Solo, Teilzeit**. Phasen 3–4 laufen danach **dauerhaft** weiter (Flywheel).

| Phase | Ziel | Ergebnis (Deliverable) | Aufwand (grob) | Abhängig von |
|---|---|---|---|---|
| **0 — Fundament** | Taxonomie-Mapping, Split-Politik, Labeling-Tooling, Datensatz-Versionierung | Klassen-Definition, eingefrorene Test-Haltungen, Ordner-/Versionsschema | 1–2 Wochen | — |
| **1 — Bootstrap-Labeling** | Rohframes per DINO+SAM vorlabeln, Mensch prüft | Erstes „Silber"-Set (Tausende Labels) | 2–4 Wochen | 0 |
| **2 — YOLO v1** | Erste echte Baseline auf VSA-Klassen | trainiertes YOLO + TensorRT-Engine, erste Metriken | 1–2 Wochen | 1 |
| **3 — Active-Learning-Schleife** | gezielt seltene/unsichere Frames labeln & nachtrainieren | YOLO v2, v3 … (laufend besser) | fortlaufend | 2 |
| **4 — Qwen3-VL LoRA** | Befund-/Code-Text & Plausibilität | LoRA-Adapter, JSON-valide Ausgaben | 2–3 Wochen (dann fortlaufend) | 2 |
| **5 — SAM/Quantifizierung** *(optional)* | Mask-Decoder für Ausdehnung/Querschnitt | verbesserte Quantifizierung | 1–2 Wochen | 2 |
| **6 — Eval & QualityGate-Kalibrierung** | ehrliche Bewertung + Schwellen tunen + Deployment | Test-Report, kalibriertes Green/Yellow/Red | 1 Woche/Runde | 2–5 |

**Empfohlene Reihenfolge zum Loslegen:** 0 → 1 → 2 möglichst schnell (erste Baseline schafft Momentum), dann 3 als Dauerbetrieb, parallel 4. 5 nur bei Bedarf.

---

## 7. Evaluation & Metriken

Eine einzelne mAP-Zahl ist irreführend. Gemessen wird mehrschichtig:

- **Detektion:** mAP@50 und mAP@50–95, aber vor allem **Precision/Recall pro Klasse** und eine **Confusion-Matrix zwischen VSA-Codes** (welche Codes verwechselt das Modell?).
- **Sicherheitsfokus:** **Recall bei Severity 4–5** (Bruch, kritische Schäden) ist die wichtigste Einzelgröße — ein übersehener Totalbruch ist teurer als ein Fehlalarm.
- **Domänen-Metrik:** **F2-CIW** (class-importance-weighted F2, aus dem Sewer-ML-Benchmark) gewichtet Klassen nach wirtschaftlicher/technischer Bedeutung — passt besser zur Kanalinspektion als reine Accuracy.
- **Quantifizierung:** IoU/Dice der SAM-Masken; Fehler auf Ausdehnung (%) und Uhrlage.
- **Qwen3-VL:** JSON-Schema-Validitätsrate (Ziel ~100 %), Übereinstimmung der begründeten Codes mit Ground Truth, Halluzinationsrate.
- **System-Ebene:** Übereinstimmung des generierten **Haltungsprotokolls** mit dem menschlichen Inspektor; separat die **QualityGate-Kalibrierung** (Green/Yellow/Red-Schwellen auf dem Val-Set einstellen, auf dem eingefrorenen Test-Set berichten).

Grundregel: **Test-Set eingefroren, pro-Haltung, menschlich gelabelt.** Ergebnisse mit Streuung/Unsicherheit berichten, nicht als einzelne Punktzahl.

---

## 8. Hardware & Ressourcen

- **RTX 5090 32 GB:** YOLO-Training problemlos; Qwen3-VL-8B per QLoRA passt bequem; DINO/SAM-Inferenz zum Labeln unkritisch.
- **Trennung Training/Betrieb:** offline trainieren, **nicht** parallel zum produktiven Inferenz-Dienst — das 29-GB-Budget gilt der Laufzeit. Nach jedem YOLO-Update **TensorRT-Engine neu bauen**.
- **Speicher/Storage:** 3000 Videos + extrahierte Frames + mehrere Datensatz-Versionen brauchen Plattenplatz — früh einplanen (Frame-Extrakte und Snapshots wachsen schnell auf mehrere hundert GB).
- **RAM (64 GB):** ausreichend für Daten-Pipelines und Label-Tooling.

---

## 9. Risiken & Fallstricke

| Risiko | Auswirkung | Gegenmaßnahme |
|---|---|---|
| **Frame-basierter Split** | Metriken zu optimistisch, Praxis bricht ein | strikt nach Haltung/Video splitten (Abschnitt 4.5) |
| **Confirmation Bias im Flywheel** | Modell lernt seine eigenen Auto-Label-Fehler | Pflicht-Review für seltene/unsichere Fälle; menschliches Gold-Test-Set nie ins Training |
| **Long-Tail seltener schwerer Schäden** | kritische Codes werden untertrainiert | gezieltes Mining, Oversampling, pro-Klasse-Recall-Gates |
| **Domain-Shift** (Kameras/Betreiber/DN) | schlechte Generalisierung | Diversität beim Sampling erzwingen |
| **Falsche Augmentierung** | Lage/Uhrlage-Semantik zerstört | kein vertikales Spiegeln; Aug-Policy dokumentieren |
| **Qwen-Halluzination** | erfundene Codes | strikte JSON-Schemata, C# behält Code-Hoheit, Qwen nur beschreibend/plausibilisierend |
| **VRAM-Kollision** | Betrieb + Training gleichzeitig sprengt Budget | zeitlich trennen; Engines nach Retrain neu bauen |
| **Architektur-Drift** | Thin-AI-Prinzip aufgeweicht | Training bleibt offline/separat; nur Gewichte/Adapter wandern zurück |

---

## 10. Governance / MLOps-light (Solo-tauglich)

- **Datensatz-Versionierung:** DVC oder disziplinierte Snapshot-Ordner.
- **Experiment-Tracking:** Weights & Biases oder minimal eine CSV/Markdown-Historie (Config, Datenstand, Metriken).
- **Modell-Registry:** versionierte Gewichte + Hash des `vsa_kek…`-Manifests je Release.
- **Reproduzierbarkeit:** feste Seeds, eingefrorene Configs, Changelog, das **Modell ↔ Datensatz ↔ Manifest** verknüpft.

---

## 11. Nächste konkrete Schritte (Quick Wins)

1. **Test-Haltungen einfrieren** — einige komplette Haltungen als menschlich gelabeltes Gold-Set beiseitelegen (schützt alle künftigen Metriken).
2. **Detektor-Taxonomie festlegen** — Hauptcode/Char1-Klassenliste aus dem VSA-Manifest ableiten (bewusst grob halten).
3. **Bootstrap-Lauf** — DINO+SAM über einen Video-Querschnitt, Vorlabels erzeugen, Review starten.
4. **YOLO v1** — erste Baseline trainieren, Metriken pro Klasse ansehen.
5. **Flywheel-Haken setzen** — Active-Learning-Auswahl an `KnowledgeBaseManager`-Embeddings und `TrainingSamplesStore` anbinden.

---

## Quellen / Referenzen

**Domäne & Datensätze**
- Sewer-ML: Multi-Label Sewer Defect Classification Dataset & Benchmark (1,3 Mio. Bilder, F2-CIW-Metrik) — https://arxiv.org/abs/2103.10895 · https://vap.aau.dk/sewer-ml/
- Automated defect classification/localization in sewer pipelines (ResNet50–Swin + modified YOLOv8, CCTV) — https://www.nature.com/articles/s41598-025-27765-5
- Transfer-Learning-YOLO für Sewer-Defekterkennung — https://www.sciencedirect.com/science/article/pii/S266616592300073X
- Benchmarking YOLO & RT-DETR (Istanbul Sewer Dataset) — https://www.mdpi.com/2076-3417/15/20/11096

**Modell-Training**
- Fine-Tuning Qwen3-VL (praktischer Leitfaden) — https://medium.com/@aminfadaeinejad.edu/fine-tuning-qwen3-vl-a-practical-guide-for-vision-language-model-adaptation-d66d3f61e888
- Fine-Tuning Qwen3-VL 8B (Schritt-für-Schritt) — https://www.datacamp.com/tutorial/fine-tuning-qwen3-vl-8b
- Fine-Tuning Grounding DINO (Open-Vocabulary) — https://learnopencv.com/fine-tuning-grounding-dino/
- Fine-Tune SAM-2.1 auf Custom Dataset — https://blog.roboflow.com/fine-tune-sam-2-1/ · https://www.datacamp.com/tutorial/sam2-fine-tuning

**Daten-Engine / Auto-Labeling**
- Autodistill (Foundation-Modelle labeln, kleines Modell lernt) — https://github.com/autodistill/autodistill · https://docs.autodistill.com/
- Grounding DINO + SAM + Autodistill für Datensatz-Erstellung — https://blog.roboflow.com/autodistill/
- Human-in-the-Loop / Active-Learning-Annotation — https://encord.com/blog/active-learning-machine-learning-guide/ · https://labelstud.io/guide/ml.html
- Datenleckage bei Video-Splits vermeiden (nach Video/Ort splitten) — https://latticeflow.ai/news/engineers-guide-to-data-leakage
