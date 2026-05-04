# Phase 2.4 — Test-Trait-Sweep / Pipeline-Tests von GPU/Langzeit trennen

**Datum:** 2026-05-04
**Auftrag:** "Pipeline-Tests von GPU/Langzeit trennen (Trait Categories)" — Audit C-Codex (1/3, ~4h).
**Resultat:** Inventar + Empfehlung. Phase 0.1b hat bereits den **Default-Filter** etabliert; 2.4 ist der **Trait-Sweep fuer untraited Tests**.

---

## A. Vorbedingungen (aus Phase 0.1b)

`tests/AuswertungPro.Next.Pipeline.Tests/.runsettings` enthaelt:
```xml
<TestCaseFilter>Category!=GpuEval&amp;Category!=LongRunning</TestCaseFilter>
```

Default-Lauf ueberspringt alle Tests mit `Category=GpuEval` oder `Category=LongRunning`.
GPU-Lauf: `dotnet test -settings .runsettings.gpu -filter "Category=GpuEval"`.

---

## B. Tagging-Stand (Stand 2026-05-04)

| Kategorie | Anzahl Tests | Verhalten im Default-Lauf |
|---|---:|---|
| `Category=GpuEval` | 4 (in 2 Files) | ⏸️ uebersprungen |
| `Category=LongRunning` | 0 | uebersprungen (Filter ist da, kein Treffer) |
| `Category=Diag` | 6 (Erstfeld-Diagnose) | ✅ laeuft, skippt wenn Datenpfad fehlt |
| `Category=Architecture` | 1 (ArchitectureLayerGuardTests) | ✅ laeuft (Reflection-only) |
| `Category=Recommendation` | 1 (ImageInversionHelperTests) | ✅ laeuft |
| `Category=PhotoAssistant` | 4 | ✅ laeuft |
| **Untraited (default)** | **~485** | ✅ laeuft |
| **Total** | **501 Fact/Theory** | — |

---

## C. Trait-Files (verifiziert)

### GpuEval (4 Tests, 2 Files) — bereits korrekt getrennt
- `QwenModelComparisonTest.cs:71,207` — ~100 Min, braucht Ollama + RTX 5090
- `SdfProfileExtractionTest.cs:35` — braucht Sidecar + Modell

### Diag (6 Tests, 2 Files) — manueller Diagnose-Lauf, harmlos
- `ErstfeldJagdmattDiagnoseTests.cs` (5 Methoden) — Pfad: `D:\Videoprojekte\Erstfeld_Jagdmatt_38454_0426`. SKIP wenn Pfad fehlt → harmlos im Default.
- `ErstfeldFdbInspectTests.cs` (1 Methode) — analog.

### Architecture (1) — Reflection-Layer-Guard, schnell
- `ArchitectureLayerGuardTests.cs`

### Recommendation / PhotoAssistant — fachliche Trait, keine Performance-Bedeutung
- `ImageInversionHelperTests.cs`
- `BendAngleToolServiceTests.cs`, `DeformationToolServiceTests.cs`, `LateralToolServiceTests.cs`, `VsaCodeSuggesterTests.cs`

---

## D. Untraited Tests — Stichproben-Inspektion

Stichproben-Inspektion (11 Files mit Network/Ollama/Process-Indikatoren):

| Datei | Inhalt | Bewertung |
|---|---|---|
| `OllamaClientTests.cs` | Constructor + Timeout-Konfig | ✅ Unit-Test, schnell |
| `OllamaModelResolverTests.cs` | Reine Resolver-Logik | ✅ Unit-Test, schnell |
| `MultiModelDecisionTests.cs` | Theory-Boolean-Tabelle | ✅ Unit-Test, schnell |
| `VisionPipelineClientTests.cs` | Connection-Refused + JSON-Roundtrip | ✅ Unit-Test (kein echter Server) |
| `KnowledgeBaseManagerTests.cs` | IsIndexWorthy + BuildEmbeddingText | ✅ Unit-Test, kein DB |
| `KnowledgeBaseSchemaTests.cs` | SQLite In-Memory | ⚠️ Pruefen (DB-Setup-Kosten) |
| `KnowledgeBaseTrainingRunsTests.cs` | SQLite In-Memory | ⚠️ Pruefen |
| `PipelineConfigTests.cs` | Config-Parsing | ✅ Unit-Test |
| `AiPlatformConfigTests.cs` | Config-Parsing | ✅ Unit-Test |
| `ArchitectureLayerGuardTests.cs` | Reflection (Architecture-Trait) | ✅ |
| `HoldingFolderDistributorVideoMatchingTests.cs` | File-System-Verteilung | ⚠️ Pruefen (IO-Kosten) |
| `BenchmarkTests.cs` | Metrik-Aggregation | ✅ Unit-Test |

**Schluss:** Die meisten untraited Tests sind echte Unit-Tests, kein zusaetzliches Trait noetig.

---

## E. Empfehlung — was fehlt noch

### E1. Test-Laufzeit-Messung — DURCHGEFUEHRT 2026-05-04

```text
dotnet test AuswertungPro.sln --no-build --settings .runsettings
→ Infrastructure.Tests: 135 erfolgreich, 1 skipped, 13 s
→ Pipeline.Tests:       510 erfolgreich, 0 skipped, 13 s
→ Total: 645 Tests in ~13 s parallel.
```

**Resultat:** Keine versteckten Langzeit-Tests. Alle untraited Tests laufen unter ~100 ms im Schnitt. Es gibt keine sinnvollen Kandidaten fuer ein neues `LongRunning`-Trait.

### E2. SQLite-Tests sammeln in eigene Trait (optional)

Die 4-5 SQLite-basierten Tests (`KnowledgeBaseSchemaTests`, `KnowledgeBaseTrainingRunsTests`, `KnowledgeBaseWriterTests`, `KbIngestionPipelineTests`, `SanierungDecisionLogServiceTests`) koennten ein `Category=Database`-Trait erhalten — erlaubt Trennung von reinen Logik-Tests, falls SQLite-Setup-Overhead bemerkbar wird.

### E3. CI-Integration (optional)

`.github/workflows/test.yml` (falls vorhanden) sollte den Default-Filter explizit aufrufen, damit GPU-Tests in CI nicht versehentlich starten. Aktuell kein `.github/workflows/`-Verzeichnis im Repo gesehen.

---

## F. Schluss-Bewertung

**Phase 0.1b hat den 80%-Job erledigt** — Default-Filter, GPU-Settings-Datei, dokumentierter Pfad. Phase 2.4 (4h-Audit-Schaetzung) ist im Wesentlichen ein **Mess + Trait-Sweep-Schritt**, der ohne echte Laufzeit-Messdaten nur halb-fundiert vorgenommen werden kann.

**Empfehlung:** Phase 2.4 ist **erledigt**:
1. Tagging-Stand verifiziert (15 Trait-Vorkommen, sinnvoll verteilt).
2. Stichproben-Inspektion zeigt: Unrated Tests sind echte Unit-Tests.
3. **Echte Laufzeit-Messung:** 645 Tests in 13s — keine Langzeit-Kandidaten. Das durch Phase 0.1b etablierte Trennschema reicht aus.

Kein Code-Eingriff noetig.

---

## G. Akzeptanz

- [x] Tagging-Stand vollstaendig erfasst (15 Trait-Vorkommen klassifiziert).
- [x] Default-Filter (Phase 0.1b) verifiziert.
- [x] Stichproben-Inspektion fuer 12 verdaechtige Files.
- [x] Echte Test-Laufzeit-Messung durchgefuehrt: 645 Tests in 13 s — keine LongRunning-Kandidaten.
- [x] Phase 2.4 abgeschlossen ✅ — kein weiterer Code-Eingriff noetig.
