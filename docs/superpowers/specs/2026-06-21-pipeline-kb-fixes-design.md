# Spec: Pipeline- & KB-Fixes (Audit 2026-06-20/21)

**Status:** Freigegeben (User: „alles fixen", 2026-06-21)
**Branch:** feature/gis-karte
**Grundlage:** Tiefen-Audit 2026-06-20 (18 Lese-Agenten + adversariale Verifikation) + User-Audit 2026-06-21. Memory: [[deep-audit-2026-06-20]].

## Ziel

Sechs Fixes umsetzen, die den Kern-Defekt „Frame verschwindet trotz Klassifikator-Hinweis" beheben, die KI-Datenintegrität härten und Betriebs-/Doku-Altlasten entfernen. Architektur-Prinzipien aus CLAUDE.md bleiben gewahrt (Thin-AI, neue Logik als testbare Einheit, kein unnötiges Refactoring, Kommentare auf Deutsch, JSON-Schema für Qwen).

## Locked Design-Entscheidungen (Fix #1)

- **Aktivierung:** eigener Schalter `ClassifierOnlyStructuralEnabled`, **Default AN**, unabhängig von `ClassifierDecisionEnabled`. Env `SEWERSTUDIO_CLASSIFIER_ONLY_STRUCTURAL` (Rollback per Flag).
- **Code-Umfang:** nur Grundgerüst **{BCA, BCC, BCD, BCE}**.
- **Ausreißerschutz:** Temporal-Voting (`TemporalCodeVotingService`, reused) **+** Mindestkonfidenz `ClassifierOnlyMinConfidence` (Default 0,60).

## Reihenfolge

**#4** (trivial, de-riskt Betrieb) → **#1 + #2** (Kern) → **#3** (KB-Gold) → **#6** (Eval-Leakage) → **#5** (VRAM, zuletzt).

---

## Fix #1 — Box-loser Strukturbefund bei `DINO = 0` (HIGH)

**Problem:** [MultiModelAnalysisService.cs:440-448](../../../src/AuswertungPro.Next.Infrastructure/Ai/Pipeline/MultiModelAnalysisService.cs) bricht bei `dinoResult.Detections.Count == 0` mit `continue` ab; der Klassifikator-Code wird erst bei `findings.Count > 0` (Zeile ~591) angewandt. Folge: Klassifikator sieht BCA/BCC/BCD/BCE, DINO macht keine Box → kein Befund („null erkannt"). Im `SingleFrameMultiModelService` (Zeile ~159) korrekt gelöst.

**Lösung:**
1. Neue reine Policy `ClassifierOnlyStructuralPolicy` in `Application/Ai/...` (kein I/O):
   `ResolvedCode? TryResolve(IReadOnlyList<YoloClassifyPrediction>?, double meter, double reach, bool isBend, double minConf)`.
   Ruft `VsaCodeResolver.ResolveFromClassifier(...)` (erbt Bogen-Veto + Ortsgebunden-Gate), akzeptiert nur, wenn `IsGrundgeruest(code)` (BCA/BCC/BCD/BCE) **und** `Confidence ≥ minConf`; sonst `null`.
2. Im `dino_no_boxes`-Zweig vor `continue`: wenn `ClassifierOnlyStructuralEnabled` **und** `clsResult` Predictions → `TryResolve(...)`; bei Treffer `_codeVoting.RegisterAndVote(code, meter)`. Erst **bestätigter** Code erzeugt `EnhancedFinding` (Label via `VsaCodeTree`, `VsaCodeHint`=Code, `Severity`=1, keine Geometrie/Uhrlage, Notes „classifier-only, DINO 0 Boxen") → `deduplicator.Update([finding], meter, EvidenceVector(...), meterSource=LinearEstimate, isMeterEstimated=true)`. Trace `Path="classifier_only_structural"`. Sonst `AdvanceAll()` + `continue` (Alt-Verhalten).
3. Neue Settings `ClassifierOnlyStructuralEnabled` (Default true) + `ClassifierOnlyMinConfidence` (0,60) analog [:52-54](../../../src/AuswertungPro.Next.Infrastructure/Ai/Pipeline/MultiModelAnalysisService.cs).

**Betroffen:** `MultiModelAnalysisService.cs`; neu `Application/Ai/Pipeline/ClassifierOnlyStructuralPolicy.cs`; Config-Factory.

**Akzeptanzkriterien:**
- DINO=0 + bestätigter BCD (≥0,60) → genau 1 Befund mit `VsaCodeHint=BCD`, Severity 1, ohne Geometrie.
- DINO=0 + Schadenscode (z.B. BAB) → kein box-loser Befund (nur Grundgerüst).
- DINO=0 + Konfidenz < Schwelle ODER nur 1 Frame (Voting nicht bestätigt) → kein Befund.
- Flag aus → exaktes Alt-Verhalten (`AdvanceAll`+`continue`).

**Einordnung (ehrlich):** Verdrahtungs-Fix, kein Modell-Fix; rettet die Codes, die der Klassifikator sicher hat (Memory: BCD stark, BCE/BCA teils, BCC schwach). Früher `OTHER/NORMAL`-Skip ([:250](../../../src/AuswertungPro.Next.Infrastructure/Ai/Pipeline/MultiModelAnalysisService.cs)) bleibt bewusst unangetastet (konsistent: sicheres NORMAL = kein Strukturhinweis).

**Risiko/Rollback:** Flag auf 0. Risiko = zusätzliche box-lose Befunde mit niedriger Severity; durch Voting + Mindestkonfidenz begrenzt.

---

## Fix #2 — Regressionstests (Policy + End-to-End-Seam)

**Teil A — Policy-Test** (`Pipeline.Tests/ClassifierOnlyStructuralPolicyTests.cs`): BCD hoch+bestätigt → Code; BAB → null; Konfidenz<Schwelle → null; Bogen → BCC via Veto; 1 Frame ohne Voting → kein Befund.

**Teil B — End-to-End-Seam** (vom User mit „alles fixen" freigegeben):
- Interface `IVisionPipelineClient` extrahieren (Methoden: `ClassifyYoloAsync`, `DetectYoloAsync`, `DetectDinoAsync`, `SegmentSamAsync`, `HealthCheckAsync`); `VisionPipelineClient : IVisionPipelineClient`. `MultiModelAnalysisService` hängt am Interface.
- Frame-Quelle injizierbar machen: Delegate/Factory `Func<...,IAsyncEnumerable<VideoFrame>>` statt direktem `VideoFrameStream.Open` (Default = bestehende Implementierung).
- `MultiModelAnalysisServiceTests.cs`: Stub-Client + Stub-Frames → (a) ein Sweep/BCD-Frame mit DINO=0 + bestätigtem Klassifikator erzeugt einen BCD-Befund (Fix #1 end-to-end); (b) Flag aus → kein box-loser Befund (Alt-Verhalten); (c) DINO liefert Box → unveränderter Normalpfad (kein Doppel-Befund).

**Betroffen:** `VisionPipelineClient.cs` (+ neues Interface), `MultiModelAnalysisService.cs` (Konstruktor-Seam), `tests/AuswertungPro.Next.Pipeline.Tests/`.

**Akzeptanz:** Tests grün; `MultiModelAnalysisService` ohne echten Sidecar/ffmpeg testbar.

**Risiko:** Interface-Extraktion ist mechanisch (Klasse hat alle Methoden). Frame-Seam mit Default-Delegate hält Produktionsverhalten unverändert.

---

## Fix #3 — KB-Schema/Upsert: Gold-Metadaten

**Problem:** `UpsertSample` ([KnowledgeBaseManager.cs:331-351](../../../src/AuswertungPro.Next.Infrastructure/Ai/KnowledgeBase/KnowledgeBaseManager.cs)) schreibt nur Basisfelder + `SourceType` + `QualityGateLevel`. `HumanConfirmed`/`Corrected`/`ConfirmedByUser`/`ConfirmedAtUtc` (existieren in `TrainingSample`, [TrainingSampleModels.cs:114-122](../../../src/AuswertungPro.Next.Application/Ai/Training/TrainingSampleModels.cs)) gehen verloren → KB unterscheidet korrigiertes Gold nicht von normalem Approved.

**Lösung (additiv, vorhandenes Muster):**
- `EnsureSchema`: 4× `MigrateAddColumn("Samples", …)` ([Muster KnowledgeBaseContext.cs:82-87/153](../../../src/AuswertungPro.Next.Infrastructure/Ai/KnowledgeBase/KnowledgeBaseContext.cs)): `HumanConfirmed INTEGER`, `Corrected INTEGER`, `ConfirmedByUser TEXT`, `ConfirmedAtUtc TEXT` — **nullable** (alte Zeilen NULL = „nie beurteilt").
- `UpsertSample`: INSERT um die 4 Felder erweitern (bool? → 1/0/NULL; DateTime? → ISO-„O").
- Lese-Pfad `LoadAllEmbeddingsWithSamples` (RetrievalService): Felder mitlesen + auf das Sample/DTO mappen.

**Betroffen:** `KnowledgeBaseContext.cs`, `KnowledgeBaseManager.cs`, `RetrievalService.cs`.

**Akzeptanz:** alte DB migriert ohne Fehler (NULL-Defaults); ein über `ReviewApprovalService` korrigiertes Sample (`HumanConfirmed=true, Corrected=true`) ist nach Upsert in der DB lesbar unterscheidbar. Test: KnowledgeBaseManager-Test gegen Temp-DB schreibt+liest die Gold-Felder.

**Risiko:** niedrig (rein additiv). Backup-Mechanik unberührt.

---

## Fix #4 — Settings/Doku aufräumen

**Lösung:**
- `Start-KiMaximum4070.ps1`: entfernen ODER auf `qwen3-vl` umstellen (setzt heute `qwen2.5vl:7b`/`qwen2.5:7b` → verstößt gegen „NIE qwen2.5").
- Grep-Sweep `qwen3.5`, `qwen2.5vl`, `qwen2.5` in Settings/Docs/Kommentaren → bereinigen.
- Stale SAM2/`vit_h`-Fallback-Doku an Realität angleichen (produktiv nur SAM 2.1; SAM1/alt-SAM2 abgelehnt). `config.py`-Kommentar ist bereits korrekt — Ziel sind veraltete Stellen in `docs/`/Kommentaren.

**Betroffen:** Root-`.ps1`, `docs/`, vereinzelte Kommentare. **Keine Verhaltensänderung.**

**Akzeptanz:** keine Root-`.ps1` setzt qwen2.5; Doku nennt SAM 2.1 als einzigen Segmenter.

---

## Fix #6 — Eval-Leakage schließen (HIGH, M-2)

**Problem:** `FeedbackIngestionService.ProcessFeedbackAsync` ([:54-65](../../../src/AuswertungPro.Next.Infrastructure/Ai/SelfImproving/FeedbackIngestionService.cs)) baut KB-Sample ohne `FramePath` und mit `CaseId = FindingLabel` → beide Eval-Guards (`IsEvalContaminated`: Hash + Haltung) laufen ins Leere. `RetrievalService` hat keinen Lese-seitigen Eval-Filter ([:252-277](../../../src/AuswertungPro.Next.Infrastructure/Ai/KnowledgeBase/RetrievalService.cs)).

**Lösung (Defense-in-Depth, zwei Linien):**
1. **Schreib-Seite:** `FeedbackIngestionService` reicht echten `FramePath` + echte Haltungs-`CaseId` aus der Detection durch, ODER prüft explizit `IsEvalContaminated` vor `IndexSampleAsync` und verwirft Eval-Samples. (Bei Reject zusätzlich: Doku vs. Code angleichen — entweder Hard-Negative erzeugen oder Kommentar korrigieren; im Scope nur klären, kein neues Negativ-Lernen ohne Diskussion.)
2. **Lese-Seite:** zweite Filterlinie in `RetrievalService` — Kandidaten gegen Eval-Haltungs-Keys/Hash filtern, bevor sie ins Cosine-Ranking gehen (nutzt `AppSettings.EvalSetRoot`-Daten wie der Schreib-Guard).

**Betroffen:** `FeedbackIngestionService.cs`, `RetrievalService.cs`, evtl. `KnowledgeBaseManager` (Guard-Wiederverwendung).

**Akzeptanz:** Test — KB mit Eval-Haltung X; `ProcessFeedbackAsync(accept=true)` aus X → nicht indexiert; `RetrieveAsync` gibt ein (künstlich eingespieltes) Eval-Sample nicht zurück.

**Risiko:** mittel — Lese-Filter darf gültige Nicht-Eval-Samples nicht fälschlich ausschließen; Test deckt beide Richtungen ab.

---

## Fix #5 — VRAM-Budget aktiv erzwingen (Sidecar, zuletzt)

**Problem:** [gpu_manager.py:189-206](../../../sidecar/sidecar/gpu_manager.py) — Budget nur Warnung, `evict_lru()` nirgends aufgerufen; cls-Slot (außerhalb `GpuModelManager`) im Monitoring unsichtbar.

**Lösung (konservativ, respektiert „alle resident"-Strategie):**
- Reaktiv: im OOM-Handler [main.py:77-79](../../../sidecar/sidecar/main.py) `gpu_manager.evict_lru()` vor `empty_cache`, damit der nächste Frame Platz hat.
- Optionaler proaktiver Pre-Load-Budget-Check in `ensure_loaded` hinter Flag (Default aus).
- cls-VRAM in `get_status` aufnehmen (Sichtbarkeit).

**Betroffen:** `sidecar/sidecar/main.py`, `gpu_manager.py`, `yolo_wrapper.py` (cls-Status). Tests: pytest mit Fake-Loader (Budget überschritten → evict ausgelöst).

**Akzeptanz:** OOM-Pfad ruft `evict_lru`; `get_status` zeigt cls-Slot. **Default-Verhalten der Residenz unverändert** (kein proaktives Entladen ohne Flag).

**Risiko:** niedrig-mittel; reaktive Eviction nur im OOM-Fall.

---

## Querschnitt: Tests & Verifikation

- Pro Fix erst Test (rot), dann Implementierung (grün) — TDD wo sinnvoll (CLAUDE.md erlaubt Tests für Pipeline-/QualityGate-Logik; hier explizit vom User gewünscht).
- Abschluss: `dotnet build AuswertungPro.sln` + `dotnet test AuswertungPro.sln`; Sidecar `pytest` (default-Marker).
- Keine NuGet-Pakete ohne Rückfrage; keine Modell-/active.json-Änderungen.

## Out of Scope (bewusst nicht)

- God-Class `PlayerWindow`-Entflechtung (separate, größere Diskussion).
- Neues Hard-Negative-Lernen bei Reject (nur Doku/Code-Konsistenz in #6).
- Monotoner Meter-Schätzer, DINO-Transform-Optimierung, HttpClient-Disposable (Backlog).
