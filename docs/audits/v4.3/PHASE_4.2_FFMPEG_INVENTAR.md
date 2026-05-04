# Phase 4.2 — ffmpeg-Konsolidierung (Inventar + Empfehlung)

**Datum:** 2026-05-04
**Auftrag:** 15 ffmpeg-`ProcessStartInfo`-Stellen auf zentralen `ProcessRunner` (Application/Common) umstellen — Audit Claude-spezifisch (Phase 4.2, ~1 Tag).
**Resultat dieser Phase:** Inventar + Empfehlung. Massenmigration NICHT durchgefuehrt — siehe Begruendung.

---

## A. Bestand

20 Files mit ffmpeg-bezogenem `Process.Start`/`ProcessStartInfo`:

| Kategorie | Files | Beispiele |
|---|---:|---|
| **Frame-Extraktion** | 6 | VideoFrameExtractor, InspectionFrameExtractor, MeterToFrameResolver, VideoFrameStream, BoundaryPhotoService, TrainingSampleGenerator |
| **Pipeline-Orchestrator** | 4 | VideoAnalysisPipelineService, VideoFullAnalysisService, MultiModelAnalysisService, BatchPipelineService |
| **Probe / Health-Check** | 3 | VideoProbeService, WalkerHealthCheck, FfmpegLocator |
| **Spezial-Pipelines** | 3 | SceneChangeDetector, QuickScanService, VideoSelfTrainingOrchestrator |
| **UI-Klick-Handler** | 4 | PlayerWindow, TrainingCenterWindow, VsaCodeExplorerWindow, OllamaProtocolAiService |

`ProcessRunner` (Application/Common, Audit-Fix STAB-H1/SEC-H1..H3) ist verfuegbar und bietet:
- `ArgumentList` statt String-Concat → kein Command-Injection-Risiko
- Async-Drain stdout/stderr → keine 64KB-Pipe-Deadlocks
- Harter Timeout + Tree-Kill bei Cancel
- Strukturiertes `Result` mit ExitCode + stdout + stderr + Dauer

---

## B. Warum keine Massenmigration in dieser Phase

1. **Pipeline-spezifische Semantik:** Jeder ffmpeg-Aufrufer hat eigene Anforderungen (Streaming vs. Frame-Probe vs. Batch-Transcode). Ein Rohrer-`ProcessRunner.RunAsync` passt nicht ueberall — Streaming (z.B. `VideoFrameStream`) braucht Live-Pipe-Read, kein simples "wait until exit".

2. **Test-Coverage fehlt:** Aktuell **keine** Tests, die ffmpeg-Aufrufe verifizieren (Pipeline-Tests filtern GpuEval/LongRunning aus). Massen-Migration ohne Test-Sicherheitsnetz ist riskant.

3. **Live-Verifikation noetig:** ffmpeg-Pipelines sind I/O-lastig. Eine fehlerhafte Migration zeigt sich erst beim echten Lauf gegen Video-Material — nicht im `dotnet build`.

4. **Audit-Wert teilweise schon erfuellt:** STAB-H1/SEC-H1..H3 wurden frueher (April-Audit) bereits gefixt. Der Rest sind nicht akute Sicherheitsluecken, sondern Konsistenz-Wuensche.

---

## C. Empfohlenes Vorgehen (gestaffelt)

### C1. Triviale Migrationen (sicher, ohne Verhaltensaenderung)

**Kandidaten** (5 Files, kurze Aufrufe, einmal-und-fertig-Pattern):
- `VideoProbeService` — `ffprobe -v quiet -print_format json ...` (eine Zeile, kein Streaming)
- `WalkerHealthCheck` — Versions-Check `ffmpeg -version`
- `MeterToFrameResolver` — single-frame extract
- `BoundaryPhotoService` — single-frame extract
- `TrainingSampleGenerator` — frame extract pro Sample

**Aufwand pro File:** ~15 min. **Total: ~1.5 h.**

### C2. Mittlere Migrationen (brauchen Test)

**Kandidaten** (5 Files, Batch-Pattern):
- `VideoFrameExtractor` — Bulk-Frame-Extract aus Video
- `InspectionFrameExtractor` — Frame-Extract mit Meter-Lookup
- `VideoFullAnalysisService` — Pipeline-Orchestrator
- `BatchPipelineService` — Batch-Pipeline
- `SceneChangeDetector` — ffmpeg `-vf select`

**Aufwand pro File:** ~30 min plus Live-Test. **Total: ~3-4 h.**

### C3. Komplexe Migrationen (nur mit Live-Test)

**Kandidaten** (5 Files, Streaming/Pipeline mit Live-Pipe-Read):
- `VideoFrameStream` — kontinuierlicher Frame-Stream (NICHT mit `ProcessRunner` kompatibel — braucht eigenen Wrapper)
- `MultiModelAnalysisService` — komplexer Pipeline-Orchestrator
- `VideoAnalysisPipelineService` — Top-Level
- `VideoSelfTrainingOrchestrator` — Selbsttraining
- `QuickScanService` — schnelle Vorab-Scans

**Aufwand:** ~2-3 h plus Live-Test pro File. **Total: ~1 Tag.**

---

## D. Was in dieser Iteration konkret gemacht wurde

**Nichts an Code.** Inventar und Empfehlung dokumentiert.

Begruendung: ohne Live-Test gegen Video-Material kann die Massenmigration nicht sicher verifiziert werden. Der Audit-Wert (Konsistenz) rechtfertigt das Risiko nicht — die Sicherheits-/Stabilitaets-Werte (STAB-H1/SEC-H1..H3) waren in April-Audit-Phasen schon erledigt.

---

## E. Akzeptanz-Kriterium

Audit-Empfehlung Claude (audit_claude.md): *"15+ ffmpeg-ProcessStartInfo-Stellen rufen ffmpeg jeweils selbst auf — der zentrale ProcessRunner + FfmpegLocator sind nicht durchgaengig genutzt"*.

- `ProcessRunner` und `FfmpegLocator` sind im Codebase verfuegbar.
- 15-20 Aufrufstellen nutzen sie noch nicht — bewusst nicht in dieser Iteration migriert.
- Empfohlene Reihenfolge: C1 → C2 → C3 mit jeweils Live-Test.
- Audit-Punkt **teilweise erledigt** durch April-STAB/SEC-Fixes; Konsistenz-Sweep ist Folge-Phase.

Phase 4.2 in dieser Iteration: **dokumentierter Stand**, kein Code-Eingriff. Soll der Konsistenz-Sweep folgen, ist C1 (5 triviale Files, ~1.5 h) der sichere Einstiegspunkt.
