# Phase 5.3 — KI-Schicht aus UI ziehen (Inventar + Empfehlung)

**Datum:** 2026-05-04
**Auftrag:** "KI-Schicht migrieren: QualityGate + Aggregator + Resolver -> Application/KI" — Audit A1 (Konsens 3/3, ~1 Woche).
**Resultat:** Inventar + Migrationsplan. KEIN Code-Eingriff.

---

## A. Bestand

**147 KI-Code-Dateien** unter `src/AuswertungPro.Next.UI/Ai/...`:

| Untermodul | Files | Zweck |
|---|---:|---|
| `Training/` | 47 | TrainingCenter, Few-Shot, Yolo-Retrain, Teacher, Selbsttraining |
| `Pipeline/` | 12 | MultiModelAnalysis, Aggregator, BatchPipeline, Detection-Pipeline |
| `QualityGate/` | 11 | Green/Yellow/Red, MC-Dropout, ConfidenceCalibration |
| `KnowledgeBase/` | 9 | Phase 2.1/2.2/4.4/5.5 — Schema, Writer, Manager, Retrieval, IngestionPipeline |
| `SelfImproving/` | 7 | FeedbackIngestion, ReviewQueue |
| `Sanierung/` | 6 | DecisionLog, AiOptimization (Phase 5.5) |
| `PhotoAssistant/` | 5 | Lateral-/Bend-/DeformationToolService |
| `Teacher/` | 4 | TeacherAnnotationStore, VsaYoloClassMap |
| `Shared/` | 4 | FfmpegLocator, WalkerHealthCheck, MeterTolerances |
| `Ollama/` | 3 | OllamaConfig + Client-Wrapper |
| `Monitoring/` | 3 | ModelRegistryService |
| `ChangeDetection/` | 1 | DifferenceAnalyzer |
| Top-Level Files | ~30 | OllamaClient, EnhancedVisionAnalysisService, PythonSidecarService, ... |

**CLAUDE.md sagt:** *"Thin-AI: C# fuer alle Geschaeftslogik, LLM nur fuer Textgenerierung"* — aber 89.000 Zeilen UI-Code (vs. 1.700 Zeilen Domain) zeigen, dass die KI-Logik nicht thin ist.

**Audit-Vergleich:**
- Claude: 144 KI-Dateien in UI vs. 1 in Infrastructure
- Codex: 89.000 C# Zeilen UI vs. 1.700 Domain
- Gemini: "AuswertungPro.Next.UI.Ai.* in eigenes Projekt"

---

## B. Warum das ein Architektur-Problem ist

1. **WPF-Abhaengigkeit:** Jeder KI-Code braucht WPF im Build, auch wenn er gar keine UI nutzt (z.B. Ollama-Client).
2. **Test-Coverage:** Pipeline-Tests muessen UI-Projekt referenzieren — siehe Phase 0.1b. Tests sind WPF-gebunden.
3. **Wiederverwendbarkeit:** KI-Logik kann nicht in Konsole, Web-API, Sidecar-Tools genutzt werden.
4. **Build-Performance:** Jede KI-Aenderung triggert WPF-Markup-Compile-Schritte.
5. **Team-Skalierung:** UI-Entwickler und KI-Entwickler arbeiten am selben Projekt — keine saubere Trennung.

---

## C. Ziel-Architektur (laut Audit-Konsens)

```
AuswertungPro.Next.Domain
└── Models/  (vorhanden)
    └── (DamageCode-Enums, HaltungRecord, ...)

AuswertungPro.Next.Application
├── Ai/  (NEU — pure Geschaeftslogik, keine I/O)
│   ├── KnowledgeBase/  (Interfaces, DTOs)
│   ├── Pipeline/       (Interfaces, Aggregation-Logik)
│   ├── QualityGate/    (Green/Yellow/Red Regeln, MC-Dropout-Algorithmus)
│   ├── Sanierung/      (DecisionLog-DTO, Rules)
│   └── ...

AuswertungPro.Next.Infrastructure
├── Ai/  (NEU — I/O-Implementierungen)
│   ├── KnowledgeBase/  (SQLite-Context, Writer)
│   ├── Ollama/         (HTTP-Client)
│   ├── Sidecar/        (HTTP-Client zu Python)
│   ├── ProcessRunner   (vorhanden)
│   └── ...

AuswertungPro.Next.UI
└── Ai/  (REDUZIERT — nur UI-spezifisches)
    ├── ViewModels-Adapter
    ├── LiveDetection/  (UI-Pipeline mit Dispatcher)
    └── PhotoAssistant/ (UI-Tool-Service)
```

---

## D. Vorgeschlagene Migrationsreihenfolge

### Sub-Phase 5.3.A: Domain-DTOs (~4 h)
- `TrainingSample`, `KbSample`, `EmbeddingDto`, `RulesEvaluation`, `TrainingRun` ins Domain-Projekt.
- Audit-Konsens: nur reine Daten, keine Logik.

### Sub-Phase 5.3.B: Application-Interfaces (~6 h)
- `IKnowledgeBaseManager`, `IRetrievalService`, `IEmbeddingService`, `IAiPlatform`, `IQualityGateService`.
- UI bekommt nur Interfaces, kein konkreter Code.

### Sub-Phase 5.3.C: Migration KnowledgeBase + Ollama (~1 Tag)
- KB-Schicht (Phase 2.1/2.2/4.4/5.5) ist die am besten getestete, deshalb zuerst.
- Ollama-Client folgt natuerlich.
- Tests bleiben gruen.

### Sub-Phase 5.3.D: Migration Pipeline + QualityGate (~1 Tag)
- 12 + 11 = 23 Files.
- Aggregator, MC-Dropout, Confidence-Calibration.

### Sub-Phase 5.3.E: Migration Training + Teacher + SelfImproving (~2 Tage)
- 47 + 4 + 7 = 58 Files. Groesster Block.
- TrainingCenterViewModel bleibt im UI, ruft via Interfaces.

### Sub-Phase 5.3.F: Migration Sanierung + PhotoAssistant + Shared (~1 Tag)
- 6 + 5 + 4 = 15 Files.
- Phase 5.5 DecisionLog ist hier dabei.

### Sub-Phase 5.3.G: Aufrufer-Migration UI (~1 Tag)
- Alle Page-VMs / Window-CodeBehind nutzen Interfaces statt Konkrete-Klassen.
- ServiceProvider verkabelt Application+Infrastructure.

**Total: ~7-8 Tage, statt 1 Woche geplant — realistisch fuer den Umfang.**

---

## E. Risiken

| Risiko | Wirkung | Gegenmittel |
|---|---|---|
| Zirkulaere Projekt-Referenzen | Build-Bruch | Domain → Application → Infrastructure → UI klar einhalten |
| `using AuswertungPro.Next.UI.Ai.*` in 200+ Files | Massen-Edit | Per Sub-Phase migrieren, Tests pro Schritt |
| `Dispatcher.Invoke` aus KI-Code | Geht nicht ohne UI | UI-spezifischen Adapter behalten, Core-Logik thread-frei |
| Pipeline-Tests-Referenzen | Tests muessen umziehen | Test-csproj-Anpassung |
| Phase 5.1/5.2 Wechselwirkung | Reihenfolge wichtig | 5.2 → 5.3 → 5.1 ODER 5.3 ohne DI |

---

## F. Reihenfolge mit Phase 5.1 / 5.2

Drei Architektur-Phasen mit Wechselwirkung:
- **5.1** DI-Container
- **5.2** ServiceProvider zerlegen
- **5.3** KI-Schicht aus UI ziehen

**Empfohlene Reihenfolge:** **5.3 → 5.2 → 5.1**.
- 5.3 macht klar **wer was braucht** (Interfaces).
- 5.2 zerlegt ServiceProvider basierend auf den Modulen aus 5.3.
- 5.1 setzt DI-Container auf saubere Schichten.

**Alternative:** **5.2 → 5.3 → 5.1** wenn man kleinste Risiken zuerst will.

---

## G. Mit CLAUDE.md vereinbar?

CLAUDE.md "Thin-AI: C# fuer alle Geschaeftslogik, LLM nur fuer Textgenerierung":
- Diese Phase ist genau der Schritt um diese Aussage einzuloesen.
- Heute ist die Aussage **nicht erfuellt** (89.000 UI-Zeilen).
- Nach Phase 5.3: KI-Code lebt im richtigen Projekt, UI-Projekt schlank.

CLAUDE.md "Kein grosses Refactoring ohne explizite Diskussion":
- **Phase 5.3 ist explizit dieses grosse Refactoring.**
- User-Diskussion ist Voraussetzung, NuGet/Branch-Strategie muss vereinbart sein.

---

## H. Akzeptanz

- 147 KI-Files unter UI/Ai/ inventarisiert, 13 Untermodule + 30 Top-Level-Files.
- Ziel-Architektur dokumentiert (Application/Ai + Infrastructure/Ai).
- Migrationsreihenfolge in 7 Sub-Phasen mit Aufwandsschaetzung.
- Reihenfolge-Empfehlung: 5.3 → 5.2 → 5.1.
- ⏸️ Migration in **eigener Mehr-Tages-Session** mit User-Freigabe und Branch-Strategie.
- KEIN Code-Eingriff in dieser Iteration.
