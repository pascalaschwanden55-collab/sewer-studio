# Deep-Dive-Audit SewerStudio

## 1. Executive Summary

SewerStudio ist in einem reifen, ueberraschend disziplinierten Zustand fuer ein Solo-Projekt: Die Kernprinzipien (Thin-AI, C#-framebasiertes Dedup ohne Tracking, doppelter Eval-Kontaminationsschutz per Hash und Haltungs-CaseId, strict-JSON-Schema im Hauptpfad) sind real im Code verankert und breit getestet (705 Tests, Pipeline-Projekt 424 gruen real ausgefuehrt). Sicherheitstechnisch ist das Repo sauber: keine echten Secrets, keine Command-/SQL-Injection, alle Netz-Server loopback-only mit Pflicht-Token und Konstante-Zeit-Vergleich, Pfad-Traversal und Zip-Slip zentral abgewehrt. Das groesste bestaetigte Einzelrisiko ist ein deterministischer Laufzeit-Abbruch des Multi-Model-Hauptpfades durch BaseAddress-Mutation auf einem geteilten HttpClient (HIGH, CONFIRMED) — die Videoanalyse bricht bei aktivem Sidecar hart ab. Daneben ein realer Datenverlust-Pfad bei gleichzeitig offenen Fenstern (TeacherDelete umgeht die Store-Sperre, HIGH, CONFIRMED). Die wiederkehrenden strukturellen Themen sind Konsistenz-Luecken in der zweiten Verteidigungslinie (Few-Shot/YOLO-Export ohne Eval-Guard, QualityGate nicht im LLM-Fallback) und nicht-atomares JSON-Schreiben in mehreren Stores. Mehrere zunaechst hoch eingestufte Funde wurden bei der Verifikation entschaerft oder widerlegt (SAM-Race REFUTED, PdfPig-Restore REFUTED, Few-Shot-Kontamination auf LOW). Reifegrad insgesamt: produktionsnah mit klar umrissener, ueberschaubarer Restschuld — kein einziger CRITICAL-Fund ueber alle Subsysteme.

## 2. Gesundheit pro Subsystem

| Subsystem | Health | Dateien | Kernaussage |
|---|---|---|---|
| UI-Views-Windows | OK | 11 | Robuste Lebenszyklus-Choreografie; 2 echte Maengel: TeacherDelete-Race (HIGH) + HttpClient-Leak im Coding-Pfad |
| UI-Views-Rest (Pages/Controls) | OK | 22 | Solide, null-sicher; Reflection-Aufruf privater VM-Methode, DataPage/SchaechtePage stark dupliziert |
| UI-ViewModels | OK | 35 | Gute MVVM-Hygiene; YOLO-Export ohne Eval-Guard + Sidecar-Dummy-BBox entgegen lokalem Pfad |
| UI-Services-und-Rest | GUT | 28 | Sicherheitsbewusst (LiveControl/Backup/SAM-RLE); Memory-Leak im PipeGraphTimeline, UI-Thread-Polling |
| Infra-Ai-Pipeline | OK | 12 | Saubere Multi-Model-Pipeline; HIGH: BaseAddress-Mutation crasht den Hauptpfad (CONFIRMED) |
| Infra-Ai-Training-KB | OK | 26 | Atomare Stores, doppelter Eval-Schutz; Few-Shot/YOLO-Export ohne Guard (latent), Teacher-Store nicht atomar |
| Infra-Ai-Rest | OK | 18 | QualityGate korrekt, strict-Schema vorbildlich; 3 Qwen-Pfade nutzen Freitext, ffmpeg-Pipe-Deadlock latent |
| Infra-Import | OK | 13 | Defensiv, Result-Pattern, SQL gequotet; XTF-Load ohne try/catch, MDB-via-PowerShell |
| Infra-Rest (Costs/Map/Offers) | GUT | 31 | Geldlogik korrekt+getestet; Video-Zuordnung per Dateigroesse, nicht-atomare Katalog-Speicherung |
| Application | GUT | 28 | Sehr gut getestet, reine Logik ausgelagert; 3 XML-Ladestellen umgehen SafeXmlLoader |
| Domain | OK | 26 | Saubere POCO-Schicht; Substring-Match "bogen" erzeugt evtl. falsche Labels, toter ILI-Pfad mit IO |
| Sidecar-Python | OK | 14 | Sicherheitsbewusst (Auth/Sandbox/Decode); Event-Loop-Blocking, kein VRAM-Budget, SAM-Race REFUTED |
| Training-Scripts | OK | 14 | Diszipliniert, Leitplanken echt; Kontaminations-Check nur clean statt hidden (durch Builder entschaerft) |
| Tools | OK | 16 | Eval-Schutz vorbildlich, DB-Tools defensiv; Malformed-JSONL bricht Builds/Gates ab, MCP-Tests leer |
| Tests | GUT | 22 | 705 Tests, 0 Skip, beide Pflichtbereiche abgedeckt; MultiModelDecision testet nachgebaute Kopie |
| Konferenz-Extension | OK | 17 | Sauber getrennt, nicht im Repo getrackt; marked.js per CDN + innerHTML (durch CSP entschaerft) |
| Querschnitt-Security | GUT | 22 | Keine Secrets/Injection/unsichere Deserialisierung; nur Firebird-Default-Credentials (LOW) |
| Querschnitt-Architektur | OK | 22 | Prinzipien ueberwiegend treu; QualityGate nicht im Fallback, VRAM-Budget nicht erzwungen |
| Querschnitt-Build/DeadCode | OK | 35 | Diszipliniert; verwaistes WPF-Projekt, ~40 Orphan-Tools, net6.0-EOL-Tool, PdfPig-Restore REFUTED |

## 3. Top-Risiken (bestaetigt, priorisiert)

### R1 — HIGH · Multi-Model-Hauptpfad bricht deterministisch ab (CONFIRMED)
- **Datei:** `src/AuswertungPro.Next.Infrastructure/Ai/Pipeline/VisionPipelineClient.cs:32`
- **Wirkung:** `_http.BaseAddress = baseUri` auf einem GETEILTEN HttpClient. Nach dem ersten Health-Check wirft das zweite Setzen (zweiter VisionPipelineClient bzw. OllamaClient) `InvalidOperationException: This instance has already started...`. Verifiziert per Mini-Repro auf net10. Der dokumentierte YOLO->DINO->SAM-Hauptpfad ist bei aktivem Sidecar nicht lauffaehig; die Videoanalyse bricht hart ab (App crasht nicht, da Catch in der UI, aber kein Ergebnis).
- **Fix:** Zeile 32 entfernen (BuildUri erzeugt ohnehin absolute URIs) bzw. nur setzen wenn `_http.BaseAddress == null`. Test `Constructor_SetsBaseAddress` anpassen. Mittelfristig pro Zielhost ein eigener HttpClient / IHttpClientFactory.

### R2 — HIGH · Stiller Datenverlust an Lehrer-Trainingsdaten (CONFIRMED)
- **Datei:** `src/AuswertungPro.Next.UI/Views/Windows/TrainingCenterWindow.xaml.cs:585-596`
- **Wirkung:** `TeacherDelete_Click` macht Read-Modify-Write auf `teacher_annotations.json` per direktem `File.WriteAllTextAsync` AUSSERHALB des `SemaphoreSlim _fileLock` des Stores. Parallele `AppendAsync` (aus PlayerWindow Live-Detection, Coding, CodingModeWindow — alle ohne ConfigureAwait, also auf dem Dispatcher) fuehrt zu Lost-Update: entweder lebt die geloeschte Annotation wieder auf oder die frisch angehaengte wird ueberschrieben. Trifft die hochwertigsten Trainingsdaten (quality=1.0), ohne Fehlermeldung, durch normale Bedienung zweier offener Fenster.
- **Fix:** `DeleteAsync(annotationId)` im `TeacherAnnotationStore` ergaenzen, das Load+Filter+Save komplett innerhalb `_fileLock.WaitAsync()` ausfuehrt (analog AppendAsync); UI darauf umstellen. Kein direkter `File.WriteAllText` aus der UI.

### R3 — MEDIUM · QualityGate laeuft nicht IMMER durch (Prinzip-Bruch)
- **Datei:** `src/AuswertungPro.Next.Infrastructure/Ai/FullProtocolGenerationService.cs:180-197` (Fallback) vs. `:234` (Happy-Path)
- **Wirkung:** `_qualityGate.Evaluate` wird nur auf dem Happy-Path aufgerufen. Bei LLM-Fehler/KB-Fallback bleibt `QualityGateResult = null`; die UI mappt null still auf Gelb (`VideoAnalysisPipelineModels.cs:181`). Ein nie bewerteter Fallback-Befund sieht aus wie bewertetes Gelb. AutoApprovalService faengt null-QG zwar ab (lehnt ab), aber die Anzeige bleibt irrefuehrend.
- **Fix:** In beiden Fallback-Zweigen einen EvidenceVector bauen und `Evaluate(...)` aufrufen (bei 0 Signalen liefert der Service korrekt Rot) oder explizit `QualityGateResult(0, Red, ...)` setzen.

### R4 — MEDIUM · YOLO-Trainings-Export ohne zweite Eval-Verteidigungslinie
- **Datei:** `src/AuswertungPro.Next.UI/ViewModels/Windows/TrainingCenterViewModel.cs:1029-1158` (lokal) und `957-983` (Sidecar)
- **Wirkung:** Beide YOLO-Export-Pfade kopieren Frames+Labels auf Platte OHNE EvalContaminationGuard (weder Hash noch CaseId), obwohl jeder KB-Index-Pfad in derselben Klasse den Guard anwendet. Laut MEMORY war `yolo_seg_dataset` historisch eval-kontaminiert (118/120) — genau dieser Pfad kann erneut kontaminieren. Zusaetzlich (`968-979`): Sidecar-Export vergibt Dummy-BBox 0.5/0.5/0.8/0.8 fuer jedes Sample mit Code OHNE HasBbox-Gate, entgegen der dokumentierten Regel des lokalen Pfads (`1111`).
- **Fix:** Vor dem Kopieren jedes Frames Hash + CaseId gegen den Guard pruefen und kontaminierte Samples skippen+loggen; im Sidecar-Pfad nur `HasBbox`-Samples mit echten Boxen exportieren. Gemeinsamer Filter-/Konvertierungs-Helper.

### R5 — MEDIUM · Video-Zuordnung per Dateigroesse verlinkt falsches Video
- **Datei:** `src/AuswertungPro.Next.Infrastructure/Media/MediaConflictCenterService.cs:679-681`
- **Wirkung:** `FindExistingVideo` akzeptiert jede Video-Datei mit identischer Byte-Groesse als "vorhanden" und verlinkt sie ohne Namens-/Inhaltsabgleich. Zwei Videos gleicher Groesse fuehren zu stiller Falschzuordnung des Protokolls zur Haltung; der Konflikt-Marker wird geloescht, der Fehler bleibt unbemerkt.
- **Fix:** Groessen-Gleichheit nicht als Identitaet werten; zusaetzlich Dateinamen oder Teilhash (erste+letzte 1 MB) pruefen, sonst als "ambiguous" markieren statt automatisch verlinken.

### R6 — MEDIUM · Nicht-atomares Schreiben von Geld-/Trainingsdaten (mehrere Stores)
- **Dateien:** `Infrastructure/Costs/CostCalculationService.cs:118-123, 164-169` (Preiskataloge); `Infrastructure/Ai/Teacher/TeacherAnnotationStore.cs:101-126` (Lehrer-Annotationen); diverse UI-Stores (`ProtocolTrainingStore`, `PresetCatalogStore`, `DropdownOptionsStore`)
- **Wirkung:** Direktes `File.WriteAllText` auf die Zieldatei. Absturz/Stromausfall waehrend des Schreibens hinterlaesst halb geschriebene JSON; beim naechsten Start liefert der Lese-Pfad ueber den catch-Fallback ein leeres Ergebnis — gepflegte Preise / Gold-Standard-Annotationen sind weg. Der Teacher-Store sichert die korrupte Datei nicht einmal (anders als TrainingSamplesStore).
- **Fix:** Atomares temp+`File.Replace`/`File.Move` mit `.bak`-Sicherung (analog `JsonProjectRepository.Save`), idealerweise als gemeinsamer Helper.

## 4. Funde nach Kategorie

### Security
- **MEDIUM** `Application/Protocol/WinCanCatalogDiscoveryService.cs:131` und `XmlCodeCatalogProvider.cs:102,778` — VSA-/WinCan-Katalog-XML wird per rohem `XDocument.Load` statt `SafeXmlLoader` geladen, umgeht die bewusste XXE-Haertung (durch .NET-Default DtdProcessing.Prohibit faktisch entschaerft, widerspricht aber der dokumentierten Entscheidung).
- **MEDIUM** `Konferenz/extension/src/hubViewProvider.ts:174,704,973` — marked.js ungepinnt per CDN + Agent-Output per `innerHTML` ohne Sanitizer (durch strikte CSP stark entschaerft; img-src https: erlaubt Bild-Beacons).
- **LOW** `Application/Reports/HaltungsDossierPdfBuilder.cs:587` — zweiter, ungehaerteter Pfad-Resolver ohne Containment-Pruefung (Duplikat zu `ProjectPathResolver`).
- **LOW** Firebird-Default-Credentials `SYSDBA/masterkey` als Fallback (`IbakExportImportService.cs:463-464`, `KiasFdbTopologyReader.cs`, `tools/CadasterDbReader`) — env-ueberschreibbar, Embedded-Read-only, vertretbar.

### Bugs
- **MEDIUM** `Infrastructure/Ai/Pipeline/MultiModelAnalysisService.cs:491-505` — ImageQuality='schlecht' verwirft alle Findings, uebernimmt aber den Qwen-OSD-Meter trotzdem in `lastMeter` -> halluzinierter Meterstand vergiftet die fortlaufende Timeline.
- **MEDIUM** `Infrastructure/Ai/EnhancedVisionAnalysisService.cs:417-419` — keine Meter-Plausibilisierung; eine als Meter fehlgelesene Knotennummer laeuft ungeprueft in die Timeline (inkonsistent zu LiveDetectionService Clamp 0-500).
- **MEDIUM** `Infrastructure/Ai/VideoFrameExtractor.cs:29-47` — stdout/stderr umgeleitet aber nie gelesen vor `WaitForExitAsync` -> latenter Pipe-Deadlock (mit `-loglevel error` selten).
- **MEDIUM** `Infrastructure/Import/Xtf/XtfHelper.cs:20-24` — `XDocument.Load` ohne try/catch im ungeschuetzten PDF-Import-Pfad -> eine kaputte .xtf bricht das Parsen einer gueltigen WinCan-PDF ab.
- **MEDIUM** `tools/video_ai/dataset_builder.py:85` und `tools/VsaShadowReport/ShadowReportAnalyzer.cs:35` — eine fehlerhafte JSONL-Zeile bricht den Build bzw. das Cutover-Gate mit unbehandelter Exception ab (Gate ist fail-closed).

### Architektur & Prinzipien
- **MEDIUM** 3 aktive Qwen-Pfade verletzen das strict-JSON-Schema-Prinzip (`LiveDetectionService.cs:66`, `QuickScanService.cs:108-110`, `OllamaVisionFindingsService.cs:43-44`) — Freitext + Regex-Extraktion; der Kommentar "qwen2.5vl does not support structured format" ist im selben Repo widerlegt.
- **MEDIUM** `sidecar/gpu_manager.py:32-89` — kein VRAM-Budget-Gate, keine Eviction; YOLO+DINO+SAM bleiben co-resident, Qwen via Ollama keep_alive=24h. Widerspruch zum 29-GB-Prinzip (auf 32-GB-5090 tragfaehig, aber nicht durch Code abgesichert).
- **MEDIUM** `sidecar/routes/*.py` — schwere GPU-Endpunkte als `async def` mit blockierender Inline-Inferenz blockieren den Event-Loop und serialisieren alle Requests inkl. /health.
- **MEDIUM** `Domain/VsaCatalog/VsaObservationMap.cs:23-27` — Substring-Match `Contains("bogen")` mappt "verbogen"/"abgebogen" faelschlich auf BCC -> potenziell falsche Trainingslabels (gegen "Mehrdeutiges bleibt null").
- **LOW** Doppelte Code-/Geld-Pfade: zwei Kosten-Modellfamilien (`Domain.Models` vs `Domain.Models.Costs`), zwei VSA-Code-Validierungen, zwei Geldrechner — Drift-/Fehlimport-Risiko.

### Performance
- **MEDIUM** `UI/Controls/PipeGraphTimeline.xaml.cs:123-146` — CollectionChanged-Abo wird beim Unload nicht geloest -> Memory-Leak bei wiederholtem Oeffnen/Schliessen.
- **MEDIUM** `UI/Services/SystemMonitorService.cs:192-201,390-435` — LHM-Update + P/Invoke laufen synchron auf dem UI-Dispatcher-Thread -> Mikro-Stottern.
- **MEDIUM** `Infrastructure/Ai/Pipeline/SidecarTelemetryWriter.cs:44-54` — `sidecar.jsonl` waechst unbegrenzt (kein Rotation/Limit).
- **LOW** `sidecar/models/yolo_wrapper.py:286-308` — `nvidia-smi`-Subprozess pro Detect-Request im Hot-Path.

### Dead-Code
- **MEDIUM** `src/AuswertungPro.Wpf/AuswertungPro.Wpf.csproj` — verwaistes leeres WPF-Projekt (nur csproj, in keiner sln referenziert).
- **MEDIUM** `tools/` — ~40 Tool-Projekte ausserhalb der Solution (22 getrackt, ~20 untracked Scratch) -> Build-Blindspot.
- **LOW** Toter VSA-ILI-Pfad (`Domain/Models/VsaIliEvaluator.cs`, `VsaRuleProvider.cs`) — nirgends aufgerufen, zugleich einzige Domain-Klasse mit Datei-IO (Schichtbruch).
- **LOW** `UI/ViewModels/Protocol/CodeCatalogProviderTest.cs` — Debug-Probe im Produktiv-Namespace; ungenutzte Helper in KinsImportService/M150MdbImportHelper.

### Tests
- **MEDIUM** `tests/AuswertungPro.Next.UI.Tests/MultiModelDecisionTests.cs:12-17` — testet eine im Testfile nachgebaute Kopie der Sidecar-Entscheidungslogik statt der echten Methode (false sense of safety beim GPU-Kill-Switch).
- **MEDIUM** `tests/SewerStudioMcpServer.Tests` — leeres Testprojekt, obwohl der MCP-Server den laufenden App-Zustand mutiert (live_set_*, retry_holding).
- **LOW** QualityGate-Schwellen-Grenzfaelle (genau 0.75/0.45) und totalWeight<=0-Fallback ungetestet; CostConsistencyCheckService (KK01-KK14) ohne Tests; mehrere reine Entscheidungs-/Validierungsfunktionen (judge/passes, VSA-Validierung, Uhrlage/BBox-Mapping) ungetestet.

## 5. Prinzip-Adhaerenz

- **Thin-AI** — EINGEHALTEN. Quantifizierung, Severity, Dedup, Clock-Normalisierung, Code-Mapping, KB-Ranking und QualityGate liegen durchgaengig in C#; das LLM liefert nur validierte Vorschlaege gegen Whitelist. Beleg: `MultiModelAnalysisService`, `EnhancedVisionAnalysisService`, Application-Schicht ohne LLM-Geschaeftslogik.
- **VRAM-Budget (max ~29GB, nie alle Modelle gleichzeitig)** — VERLETZT/nicht erzwungen. `gpu_manager.py:32-89` haelt YOLO+DINO+SAM bewusst co-resident ohne Eviction/Budget-Gate; Qwen via Ollama keep_alive=24h. Auf 32-GB-Hardware tragfaehig, aber nur durch Hardware-Groesszuegigkeit, nicht durch Code. Empfehlung: Prinzip dokumentarisch an Realitaet angleichen ODER Budget-Gate einbauen.
- **QualityGate laeuft IMMER durch** — TEILWEISE VERLETZT. Korrekt im Happy-Path (`QualityGateService` sauber, Red-Fallback bei 0 Signalen, getestet), aber `FullProtocolGenerationService` LLM-Fallback erzeugt Eintraege ohne QualityGateResult (R3).
- **Dedup C#-framebasiert / kein Tracking** — EINGEHALTEN. `TemporalFindingDeduplicator` rein ueber MissedFrames-Zaehler gegen DedupWindowFrames, kein IOU/Kalman/Track-ID/ByteTrack; beide Video-Pfade nutzen exakt denselben Deduplicator.
- **Qwen-JSON-Schema (strict, kein Freitext)** — UEBERWIEGEND EINGEHALTEN. Hauptpfad (EnhancedVision, FullProtocolGeneration, Code-Entscheider) nutzt `ChatStructuredWithOptionsAsync` + Schema + Katalog-Validierung + temp=0/seed=42. Ausnahme: 3 aktive Pfade (LiveDetection, QuickScan, OllamaVisionFindings) nutzen Freitext — liefern aber nur Hilfssignale (Meter wird in C# plausibilisiert), keine finalen VSA-Codes.
- **Eval-Kontamination** — UEBERWIEGEND EINGEHALTEN. Doppelschutz (SHA-256-Inhaltshash + Haltungs-CaseId) in StageAExporter, KnowledgeBaseManager, RouterDatasetBuilder, ClassifierDatasetBuilder (real: 120 by-name + 35 by-hash ausgeschlossen, per `dataset_report.json` belegt). Luecken: Few-Shot-Builder und YoloDatasetExportService ohne Guard (latent, da im HEAD toter/nicht verdrahteter Code — Few-Shot von HIGH auf LOW korrigiert); UI-YOLO-Export ohne Guard (R4); Guard degradiert still auf "aus" bei fehlendem Eval-Set (nur Debug.WriteLine).
- **Bild-Forensik (treu, keine Halluzination)** — EINGEHALTEN. Kein KI-Upscaling/Super-Resolution in den Pfaden; `TechniqueFrameAnalyzer` rein deterministisch; Frames 1:1 kopiert; enhance_*-Skripte nutzen nur kantenerhaltende Filter und werden als separater A/B-Test gefuehrt, nicht heimlich ins Training gemischt.

## 6. Staerken des Programms

- **Lebenszyklus & Threading in der UI:** robuste Close-/Dispose-Choreografie (volatile `_closing`-Guard vor MediaPlayer-Dispose, alle Timer gestoppt, idempotentes Cleanup), diszipliniertes Cross-Thread-Marshalling (CheckAccess/HasShutdownStarted), `SafeFireAndForget` statt unbeobachteter Crashes.
- **Sicherheits-Engineering:** loopback-only Server mit Pflicht-Token und Konstante-Zeit-Vergleich (Sidecar `hmac.compare_digest`, LiveControl `FixedTimeEquals`), Zip-Slip-Schutz, Path-Traversal zentral, SAM-RLE gegen OOM gehaertet, sichere Bild-Dekodierung mit Pixel-Limit, keine Injection/unsichere Deserialisierung im ganzen Repo.
- **Ehrliche Degraded-Signale ueber die Schichtgrenze:** Sidecar markiert Inferenzfehler als `degraded`, C# verbucht das als Review-pflichtig statt als sauberen Negativbefund — "verstummtes Modell != sauberes Rohr" ist real umgesetzt.
- **Crash-sichere Trainings-Persistenz:** `TrainingSamplesStore`/`SelfTrainingHistoryStore` schreiben temp->validieren->`File.Move` mit rotierenden Backups und Recovery-Kette (loest frueheres 3.5-GB-Backup-Wachstum).
- **Geldlogik korrekt und getestet:** Combined-Offer rechnet Rabatt/Skonto/MwSt genau einmal (per Test belegt 250->213.75->231.06), keine Doppelrabattierung, durchgaengig `MidpointRounding.AwayFromZero`.
- **Test-Disziplin:** 705 Tests, 0 Skip/Ignore, keine trivialen Assertions, keine Deadlock-Muster; beide CLAUDE.md-Pflichtbereiche (QualityGate, MeasureRecommendation) abgedeckt; Architektur-Invariante (Pipeline-Tests duerfen UI nicht referenzieren) per Test erzwungen; ehrlicher Charakterisierungs-Test fuer eine bekannte QualityGate-Schwaeche.
- **Reproduzierbarkeit:** `packages.lock.json` + `RestorePackagesWithLockFile` + gepinnte `global.json`; Sidecar-Lockfiles als Mitigation gegen Nightly-Drift.

## 7. Empfohlene Reihenfolge

### Sofort (Funktionsausfall / Datenverlust)
1. **R1** — `VisionPipelineClient.cs:32` BaseAddress-Set entfernen (One-Liner, stellt den Multi-Model-Hauptpfad wieder her), Test anpassen.
2. **R2** — `TeacherAnnotationStore.DeleteAsync` unter `_fileLock` ergaenzen, `TeacherDelete_Click` umstellen (stoppt stillen Verlust von Gold-Standard-Daten).

### Kurzfristig (Prinzip-Bruch / stille Datenfehler)
3. **R3** — QualityGate auch im LLM-Fallback aufrufen (kein Pseudo-Gelb).
4. **R4** — Eval-Guard in beide UI-YOLO-Export-Pfade einziehen + Sidecar-Dummy-BBox durch HasBbox-Gate ersetzen.
5. **R5** — Video-Zuordnung um Namen/Teilhash haerten.
6. **R6** — Atomares Schreiben (temp+Replace+.bak) fuer Cost-Kataloge und TeacherAnnotationStore; gemeinsamer Helper fuer die uebrigen Stores.
7. **Meter-Plausibilisierung** in EnhancedVision/MultiModel (0-500-Clamp), OSD-Meter aus Schlechtbildern nicht uebernehmen.

### Mittelfristig (Robustheit / Konsistenz / Tech-Debt)
8. HttpClient-Leaks im Coding-Pfad disposen; PipeGraphTimeline-Abo loesen; SystemMonitor-Polling auslagern.
9. 3 Qwen-Freitext-Pfade auf strict Schema umstellen (mind. den falschen Kommentar korrigieren); `XtfHelper`/Malformed-JSONL try/catch.
10. VRAM-Prinzip entscheiden (dokumentieren oder Budget-Gate); Sidecar-Endpunkte als `def` oder `to_thread`.
11. Aufraeumen: verwaistes WPF-Projekt loeschen, Orphan-Tools in `tools.sln` oder Scratch-Ordner sortieren, net6.0-Tool heben; `VsaObservationMap` auf Wort-Boundary; Few-Shot/YoloDatasetExportService entweder verdrahten+Guard oder als Dead-Code markieren.
12. Test-Luecken schliessen: GPU-Kill-Switch als echte Policy testen, MCP-Server-Tests, QualityGate-Grenzfaelle, KK-Regeln.

## 8. Methodik & Coverage

**Tief geprueft (vollstaendig gelesen, Belege bis Datei:Zeile):** Infra-Ai-Pipeline (alle 12), Application-Kernlogik, UI-Views-Windows (PlayerWindow-Partials weitgehend), Sidecar-Python (alle 11 Quellen + 4 Tests), Domain (alle 24 produktiven), Training-Scripts (alle 14), Costs/Map/Offers, Import-Hauptleser, Querschnitt-Security per gezielten Grep-Sweeps mit Treffer-Verifikation. Pipeline-Tests wurden **real ausgefuehrt** (`dotnet test`: 424 gruen, 0 Skip).

**Verifiziert (eigene Gegenpruefung am Code):** 5 zentrale Funde wurden mit Belegketten gegengeprueft. Ergebnis: 2x CONFIRMED (R1 inkl. net10-Mini-Repro, R2), 2x auf LOW heruntergestuft (Few-Shot-Kontamination und Hidden-Eval-Check — beide durch upstream-Mechanismen/toten Code entschaerft), 2x REFUTED (SAM-Race: async-Single-Loop ohne Threadpool macht ihn nicht ausnutzbar; PdfPig-Restore: Version `1.7.0-custom-5` ist auf nuget.org oeffentlich verfuegbar — gegen den Live-Feed geprueft).

**Ueberflogen / nicht tief (ehrlich):** XAML-Bindings nur strukturell, nicht Binding-fuer-Binding; grosse God-Files (`PlayerWindow.Coding.cs` 4.799 Z., `ProtocolPdfExporter` ~2.9k Z., `CodingModeWindow` 2.9k Z.) nur in den Risiko-/Aufrufstellen, nicht zeilenweise; ~30 Orphan-Tools nur Build-Status; Infrastructure.Tests/UI.Tests nicht real durchgelaufen (nur Quellanalyse); Laufzeitverhalten von ultralytics/groundingdino/segment-anything und der end-to-end Video-Lauf nicht reproduziert; node_modules der Konferenz-Extension nicht inhaltlich auditiert.

**Bewusste Grenzen:** Kein CVE-/Dependency-Scan, kein Lasttest (Race-/Event-Loop-Funde aus dem Code abgeleitet), keine semantische Vollpruefung jedes Tool-Programms. Severity-Einstufungen folgen der Verifikation, nicht der urspruenglichen Pruefer-Meldung — REFUTED-Funde sind bewusst NICHT als Risiko aufgefuehrt.