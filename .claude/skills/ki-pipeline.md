# SewerStudio KI-Pipeline Skill

## Beschreibung
Vollstaendige Referenz der SewerStudio KI-Pipeline fuer Kanalinspektion.
Nutze diesen Skill um den KI-Ablauf zu verstehen, zu debuggen und weiterzuentwickeln.

---

## Architektur-Ueberblick

SewerStudio ist ein **Computer-Vision-System mit LLM-Interface** (kein reines LLM-System).
Die visuelle Erkennung wird durch YOLO/DINO/SAM externalisiert — das LLM klassifiziert nur
vorverarbeitete, eingegrenzte Kandidaten nach VSA/EN 13508-2.

## Pipeline-Modi

### Modus A: Ollama-Only (Default)
```
Frame → Qwen Vision (Structured JSON) → ByteTrack Dedup → RawVideoDetection[]
```
- Einfachster Pfad, nur Ollama noetig
- Qwen macht Erkennung UND Klassifikation in einem Schritt
- Config: `SEWERSTUDIO_PIPELINE_MODE=ollamaonly`

### Modus B: Multi-Model Pipeline (via FastAPI Sidecar)
```
Frame → YOLO (Pre-Screening) → DINO (Verifikation) → SAM (Segmentierung)
      → MaskQuantification → Qwen (VSA-Code Klassifikation) → ByteTrack Dedup
      → RawVideoDetection[]
```
- Config: `SEWERSTUDIO_PIPELINE_MODE=multimodel` oder `auto`
- Sidecar: `SEWERSTUDIO_SIDECAR_URL=http://localhost:8100`

## Pipeline-Stufen (Multi-Model)

| Stufe | Modell | Aufgabe | Schwellwerte |
|-------|--------|---------|--------------|
| DETECT | YOLO | Pre-Screening, BBox | `SEWERSTUDIO_YOLO_CONFIDENCE=0.25` |
| VERIFY | Grounding DINO | Open-Vocabulary Verifikation | `SEWERSTUDIO_DINO_BOX_THRESHOLD=0.30`, `SEWERSTUDIO_DINO_TEXT_THRESHOLD=0.25` |
| SEGMENT | SAM | Pixel-genaue Segmentierung | - |
| QUANTIFY | MaskQuantification | Pixel→mm Konvertierung | `SEWERSTUDIO_PIPE_DIAMETER_MM` |
| CLASSIFY | Qwen (VL) | VSA-Code + Severity + JSON | Timeout: 300s |
| TRACK | ByteTrack-style | Temporal Voting + Dedup | `DedupWindowFrames=3` |

## QualityGate (Evidenz-Fusion)

Gewichteter Durchschnitt aus 8 Signalen mit automatischer Renormalisierung:

| Signal | Default-Gewicht | Quelle |
|--------|----------------|--------|
| YoloConf | 0.10 | YOLO Pre-Screening |
| DinoConf | 0.15 | Grounding DINO |
| SamMaskStability | 0.10 | SAM Segmentierung |
| QwenVisionConf | 0.15 | Qwen Vision Analyse |
| LlmCodeConf | 0.20 | LLM Code-Vorschlag |
| KbSimilarity | 0.10 | Knowledge-Base Match |
| KbCodeAgreement | 0.10 | KB stimmt mit Code ueberein |
| PlausibilityScore | 0.10 | Regel-basierte Plausibilitaet |

### Zonen-Schwellwerte
- **GREEN**: Composite Confidence >= 0.75 → Ergebnis akzeptieren
- **YELLOW**: Composite Confidence >= 0.45 → MC Dropout (3 Passes), ggf. Eskalation
- **RED**: Composite Confidence < 0.45 → Manuelle Pruefung (Review Queue)

### Auto-Approval (Green Zone)
Automatische Akzeptanz wenn ALLE Kriterien erfuellt:
- Confidence >= 0.92
- KB Code Agreement == true
- TrafficLight == Green
- EpistemicUncertainty < 0.15

## Selbsttraining (Self-Improving)

```
User akzeptiert/korrigiert Code
  → ValidationLog (SQLite)
  → Alle 25 Validierungen: WeightLearningService.ReLearnAsync()
  → Coordinate-Descent Optimierung der Kategorie-Gewichte
  → Neue Gewichte in CategoryWeights Tabelle
  → Naechste Inferenz nutzt optimierte Gewichte
```

- Min. 20 Samples pro Kategorie fuer Gewichts-Optimierung
- Kandidaten-Gewichte: {0.0, 0.05, 0.10, 0.15, 0.20, 0.30, 0.40}
- Optimiert: Binary Cross-Entropy

## Uncertainty Estimation

### Single-Pass (Standard)
- Epistemic = 1.0 - |2*confidence - 1|
- Aleatoric = 0.05 (Basis-Rauschen)
- NeedsReview = Epistemic >= 0.15 || Yellow Zone

### Monte Carlo Dropout (nur Yellow Zone)
- 3 LLM-Passes bei Temperaturen [0.1, 0.5, 0.9]
- Agreement Rate = Anteil uebereinstimmender Codes
- Epistemic = 1 - Agreement Rate

## Modell-Konfiguration

| Variable | Default | Beschreibung |
|----------|---------|-------------|
| `SEWERSTUDIO_AI_ENABLED` | false | KI Master-Schalter |
| `SEWERSTUDIO_OLLAMA_URL` | http://localhost:11434 | Ollama Server |
| `SEWERSTUDIO_AI_VISION_MODEL` | qwen2.5vl:3b | Vision-Modell |
| `SEWERSTUDIO_AI_TEXT_MODEL` | qwen2.5:3b | Text/Code-Modell |
| `SEWERSTUDIO_AI_EMBED_MODEL` | nomic-embed-text | Embedding-Modell |
| `SEWERSTUDIO_AI_TIMEOUT_MIN` | 5 | Ollama Timeout (Min) |
| `SEWERSTUDIO_OLLAMA_KEEP_ALIVE` | 24h | Modell im RAM halten |
| `SEWERSTUDIO_OLLAMA_NUM_CTX` | 8192 | Context-Window |
| `SEWERSTUDIO_MULTIMODEL_ENABLED` | false | Multi-Model Pipeline |
| `SEWERSTUDIO_PIPELINE_MODE` | ollamaonly | Pipeline-Modus |

## Aktuelle Limitierungen (Stand Maerz 2026)

1. **Kein Dual-Model Switching**: System nutzt EIN konfiguriertes Modell fuer alle Frames.
   Die geplante 8B-Default + 32B-Fallback Architektur ist NICHT implementiert.
2. **Kein automatisches Model-Hotswap**: Wechsel zwischen Modellen erfordert Neustart
   oder manuelle Umkonfiguration.
3. **Selbsttraining optimiert nur Gewichte**, nicht das Modell selbst (kein Fine-Tuning).

## Wichtige Dateien

### Core Pipeline
- `src/AuswertungPro.Next.UI/Ai/VideoAnalysisPipelineService.cs` — Haupt-Orchestrator
- `src/AuswertungPro.Next.UI/Ai/Pipeline/MultiModelAnalysisService.cs` — Multi-Model Pfad
- `src/AuswertungPro.Next.UI/Ai/VideoFullAnalysisService.cs` — Ollama-Only Pfad
- `src/AuswertungPro.Next.UI/Ai/EnhancedVisionAnalysisService.cs` — Strukturierte Vision
- `src/AuswertungPro.Next.UI/Ai/FullProtocolGenerationService.cs` — Code-Mapping

### QualityGate & Confidence
- `src/AuswertungPro.Next.UI/Ai/QualityGate/QualityGateService.cs` — Evidenz-Fusion
- `src/AuswertungPro.Next.UI/Ai/QualityGate/EvidenceVector.cs` — Signal-Struktur
- `src/AuswertungPro.Next.UI/Ai/QualityGate/WeightLearningService.cs` — Gewichts-Optimierung
- `src/AuswertungPro.Next.UI/Ai/QualityGate/McDropoutService.cs` — MC Dropout
- `src/AuswertungPro.Next.UI/Ai/QualityGate/CategoryWeights.cs` — Kategorie-Gewichte

### Self-Improving
- `src/AuswertungPro.Next.UI/Ai/SelfImproving/FeedbackIngestionService.cs` — Feedback
- `src/AuswertungPro.Next.UI/Ai/SelfImproving/AutoApprovalService.cs` — Auto-Approval
- `src/AuswertungPro.Next.UI/Ai/QualityGate/ValidationLogger.cs` — Logging

### Konfiguration
- `src/AuswertungPro.Next.UI/Ai/AiPlatformConfig.cs` — Master Config
- `src/AuswertungPro.Next.UI/Ai/AiRuntimeConfig.cs` — Runtime Config
- `src/AuswertungPro.Next.UI/Ai/Ollama/OllamaConfig.cs` — Modell-Defaults
- `src/AuswertungPro.Next.UI/Ai/Pipeline/PipelineConfig.cs` — Pipeline Config

### Tests
- `tests/AuswertungPro.Next.Pipeline.Tests/QualityGateServiceTests.cs`
- `tests/AuswertungPro.Next.Pipeline.Tests/AutoApprovalTests.cs`

## Debugging-Tipps

1. **Modell laeuft nicht**: Pruefe `SEWERSTUDIO_AI_ENABLED=true` und `ollama list`
2. **Falsches Modell aktiv**: Pruefe `SEWERSTUDIO_AI_VISION_MODEL` — kein Auto-Switch!
3. **Langsam**: Wenn 32B laeuft statt 8B → Umkonfigurieren auf kleineres Modell
4. **VRAM voll**: `nvidia-smi` pruefen, ggf. Keep-Alive reduzieren
5. **Sidecar nicht erreichbar**: `SEWERSTUDIO_PIPELINE_MODE=ollamaonly` als Fallback
6. **Schlechte Ergebnisse**: QualityGate-Gewichte pruefen, ggf. ValidationLog leeren
