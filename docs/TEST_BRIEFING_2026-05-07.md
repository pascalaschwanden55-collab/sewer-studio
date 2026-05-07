# SewerStudio — Test-Briefing fuer externe Tester

**Stand:** 2026-05-07
**Branch:** `feature/pdf-import-beobachtungen`
**Build:** ✅ green · **Tests:** ✅ 743 gruen (140 Infrastructure + 603 Pipeline)

---

## Was ist SewerStudio?

WPF-Desktop-App fuer automatisierte Kanal-Inspektion. Verarbeitet ~3000
WinCan/Ikas-IBSK-Videos lokal, generiert EN-13508-2/VSA-KEK-Schadenscodes
und exportiert druckfertige Inspektionsprotokolle.

**Stack:** .NET 10 WPF · LibVLCSharp · YOLO26l-seg + Qwen3-VL + SAM 2.1 +
Grounding DINO 1.5 (alles lokal auf RTX 5090 32 GB) · FastAPI-Sidecar (Python)
auf :8100 · SQLite-Knowledge-Base.

---

## Was wir in den letzten Tagen gebaut haben

In ~255 Commits (seit Audit-Start 2026-04-19) wurde die Codebase nach einem
kompletten Audit (siehe `docs/AUDIT_SCHLUSSANALYSE_2026-05-06.md`) durchgehaert.

### 1. Architektur-Migration (Thin-AI-Prinzip durchsetzen)

**Vorher:** 89 % der KI-Logik lebte im UI-Projekt. Nicht testbar, nicht
headless-faehig, kein klares Schicht-Modell.

**Heute:** 71 % der KI-Logik in Application/Infrastructure. UI/Ai von
107 → 22 Files. **76 Files migriert** in saubere Schichten.

| Schicht | KI-Files (vorher → heute) |
|---------|---------------------------|
| Domain | 4 → 4 |
| Application | 12 → 93 |
| Infrastructure | 3 → 48 |
| UI | 107 → 22 |

**Ermoeglicht durch 10 Provider/Bridge-Patterns** (in `App.xaml.cs`
registriert, von Application-Services aufgerufen):
KnowledgeRoot · AppDataPath · KnowledgeMirrorNotifier · SidecarAuthToken ·
OllamaConfig · AiRuntimeConfig · PipelineConfig · KnowledgeBasePath ·
ImagePixelDecoder (WPF-Imaging-Adapter) · OcrPdfFallback (Windows.Media.Ocr-Adapter).

### 2. PlayerWindow zerschlagen (CRITICAL ARCH-C3)

**Vorher:** `PlayerWindow.xaml.cs` = **5370 LOC** God-Class.
**Heute:** **842 LOC** (-84 %), 16 thematisch getrennte Partials:

| Partial | LOC | Zweck |
|---------|-----|-------|
| Helpers | 220 | Pure Helper |
| VideoPlayback | 272 | VLC-Steuerung |
| Hotkeys | 198 | Tastatur-Shortcuts |
| Snapshot | 123 | Frame-Capture |
| LiveDetection | 884 | Live-AI-Detection |
| Feedback | 193 | Self-Improving-Loop |
| TrainingMode | 696 | Trainings-Modus |
| CodingMode | 2998 | Codier-Modus |
| DamageMarkers | 184 | Schadens-Marker auf Slider |
| Heatmap | 100 | QuickScan-Heatmap |
| MarkTool | 563 | Manuelles Markieren |
| CodingTool | 590 | Coding-Tools (Bend/Level/Intrusion) |
| CodingOverlayRender | 1181 | Render-Routinen |
| CodingApply | 385 | Code-Anwendung |
| CodingEvents | 645 | Befund-Liste-Aktionen |
| ImportProtocol | 244 | PDF-Import |
| Eingabemarker | 321 | Manuelle BBox |
| MaskTriage | 408 | Mask-Triage im Pausenmodus |

### 3. Sicherheit (alle HIGH-Befunde abgehakt)

**SEC-H1..H3:** Command-Injection in 8 `Process.Start`-Stellen behoben
(zentraler `ProcessRunner` mit `ArgumentList.Add` + asynchronem Pipe-Drain
+ Tree-Kill bei Timeout).

**SEC-H4:** Path-Traversal in `SanitizePathSegment` (`.`/`..`/Trailing-Dots).

**SEC-H5:** Sidecar-Bearer-Token-Auth fuer alle administrativen Endpoints.

**SEC-C1:** UI-Pfade ohne Containment-Check — `TryResolveStoredPath` +
`ResolveExistingPath` + `ResolveDossierPhotoPath` + `AddResolvedPdf` alle
gegen Projekt-Root gepinnt.

**L4 XML-XXE-Schutz:** zentraler `SafeXmlLoader` mit `DtdProcessing.Prohibit`
+ `XmlResolver=null`. 8 `XDocument.Load`-Aufrufer migriert.

### 4. Stabilitaet

**STAB-H1:** HttpClient-Leak in Feedback-Pipeline (static
`_feedbackHttpClient`).

**STAB-H2/H3:** SemaphoreSlim auf allen JSONL-Append-Stellen
(positive/negative Feedback, Batch-History, Qwen-Raw-Log).

**STAB-H4:** **Sidecar Graceful-Shutdown** via neuem POST `/shutdown`-Endpoint
(uvicorn faengt SIGINT, durchlaeuft Lifespan-Cleanup, GPU-Modelle entladen
sauber). C# wartet 3 s, fallback auf Hard-Kill.

**STAB-H5:** async-void-Timer in PlayerWindow → `_isWindowClosed`-Guard
zu Beginn jedes Tick-Handlers.

**STAB-H6:** Exception-Swallowing in `KbQualityService.FindStaleCandidates()`
durch Debug-Log ersetzt — SQLite-Lock/Schema-Fehler nicht mehr stumm.

### 5. KI-Pipeline-Verbesserungen

**Active Learning aktiviert (Audit Top-10 Punkt 6):**
`ReviewQueueService.GetTopForActiveLearning` mischt 60 % Uncertainty +
40 % Diversity (rarste VSA-Codes zuerst). Code-Frequenzen werden aus der KB
gezogen. **Bug entdeckt + gefixt:** Selector matched Frequency-Map jetzt
mit dem rohen VSA-Code statt mit dem dekorierten "BAC — Risse".

**CategoryWeights aktiviert:** PlayerWindow.CodingMode laedt
`WeightLearningService.LoadAllWeights()` aus der KB beim QualityGate-Init.
Damit werden alle ueber FeedbackIngestion gelernten Per-Code-Gewichte
tatsaechlich angewendet.

**Pipeline-Telemetry-Persistierung:** `PipelineTelemetry.PersistSummaryAsync`
schreibt Phase-Stats (YOLO/DINO/SAM/Qwen) als JSONL ins Log-Verzeichnis,
SemaphoreSlim-geschuetzt fuer parallele Pipeline-Laeufe.

### 6. Wartungs-Tools (NEU — Audit-Tab in Diagnose-Seite)

In der App unter **Diagnose** sind drei neue Self-Service-Tools sichtbar:

**Brain-Mirror Health:** Button "Pruefen" zeigt Green/Yellow/Red mit
Bytes + Alter. Erkennt:
- Brain-Root fehlt (Red)
- Lokale DB ohne Mirror (Red)
- Mirror hinkt > 10 % oder > 1 MB hinter (Yellow)
- Mirror seit > 7 Tagen nicht aktualisiert trotz lokaler Aenderungen (Yellow)
- Alles aktuell (Green)

**Frame-Cleanup:** Identifiziert verwaiste PNGs unter `C:\KI_BRAIN\frames`
die zu keinem TrainingSample mehr gehoeren (Audit hatte 67 GB im
frames-Ordner moniert). Schont Frames juenger als 7 Tage.
- "Pruefen (DryRun)" zeigt Anzahl + MB ohne zu loeschen
- "Loeschen" mit Bestaetigungs-Dialog

**KB-Versionen aufraeumen:** Loescht alte Versions-Snapshots in der
KnowledgeBase. Behaelt letzte 20 + alle juenger als 30 Tage. Aktuelle
Version bleibt immer.

### 7. Test-Coverage

**743 Tests** insgesamt (140 Infrastructure + 603 Pipeline) — **+89 Tests
in dieser Session (+13.6 %)**. Neu:
- 25 ProjectPathResolver (Path-Traversal-Schutz)
- 6 ActiveLearningSelector (Bug entdeckt + gefixt)
- 5 FrameStoreCleanupService
- 6 KnowledgeMirrorHealth
- 6 HaltungRecordViewModel
- 2 TrainingCaseJsonRoundtrip (Legacy-JSON-Kompatibilitaet)
- 5 SafeXmlLoader (XXE-Schutz)
- 10 ProcessRunner (Drain + Timeout + Tree-Kill)
- 19 PdfProtocolHelpers (Caesar-Decode + Marker-Erkennung)
- 5 TaskExtensions (SafeFireAndForget)

---

## Wo bei externem Test besonders hinschauen?

### Funktionale Hot-Spots
1. **Codier-Modus** im PlayerWindow — Mark-Tool (Punkt/Ellipse/Freihand/
   Rechteck) + SAM-Vorschau + VSA-Code-Picker. Workflow: Video oeffnen →
   Codier-Modus → Markieren → Code waehlen → Save als Training.
2. **Batch-Selbsttraining** im TrainingCenter — Ordner mit
   WinCan-/IBAK-Exports nehmen, "Batch-Import & KB-Index" laufen lassen.
   Erwartetes Verhalten: jede Haltung mit Protokoll-PDF wird automatisch
   verarbeitet, Ergebnisse landen in der KB.
3. **PDF-Protokoll-Import** — verschiedene PDF-Formate: WinCan, IBAK
   IKAS-Caesar (verschluesselter Font), Scan-PDFs (OCR-Fallback). PDF-Drop
   auf TrainingCenter → Eintraege erscheinen.
4. **Live-Detection** waehrend Wiedergabe — Button "Live KI-Detection" im
   PlayerWindow. Erkannte Schaeden werden als Ring-Sektoren ueber dem
   Video angezeigt.
5. **Quick-Scan** — Button "QuickScan" zeigt eine Heatmap auf dem
   Position-Slider mit Severity-Color pro 5-Sekunden-Segment.

### Diagnose-Tab (NEU)
1. **Brain-Mirror Health** pruefen — sollte normalerweise Green sein.
2. **Frame-Cleanup DryRun** — sollte zeigen wie viele orphane PNGs es
   gibt. Wenn dein KI_BRAIN-Ordner ueber Wochen gewachsen ist, hier viele
   verwaiste sehen. Erst DryRun, dann erst loeschen.
3. **KB-Versionen aufraeumen** — bei alten Installationen koennten
   einige Versionen aelter als 30 Tage sein und herausgefiltert werden.

### Sicherheit zum Probieren (sollten alle scheitern)
1. **Manipulierte Projektdatei** mit Pfad `..\..\..\windows\system32\...`
   in einem Foto/Video/PDF-Feld → wird abgewiesen, keine Datei eingebunden.
2. **XML mit DTD-Block** importieren — wird mit `XmlException` abgelehnt.
3. **Dateiname mit Anfuehrungszeichen** (z.B. `Haltung "evil".mp4`) →
   ffmpeg/Python werden korrekt mit ArgumentList aufgerufen, keine
   Shell-Interpretation.

### Stabilitaet
1. **App schliessen waehrend laufender Live-Detection** — sollte sauber
   beenden, kein Crash, kein hangender Sidecar-Prozess.
2. **Lange Codier-Sessions** (> 100 Approve/Reject-Klicks) — Speicher
   sollte nicht stetig wachsen (HttpClient-Leak war der Audit-Befund).
3. **Sidecar-Beendigung** — Diagnose: sollte beim App-Close die
   Sidecar-Logs zeigen "Graceful shutdown initiated" → "GPU-Modelle entladen".

---

## Bekannte Limitationen / Tech-Debt

1. **Domain-Models (HaltungRecord/SchachtRecord/Project) haben noch
   `INotifyPropertyChanged` + `ObservableCollection`** — blockiert
   Headless-Use (Sidecar/CLI). Phase 1 (`HaltungRecordViewModel`-Wrapper)
   ist gebaut. Phase 2 (Konsumenten-VMs umstellen) + Phase 3 (Domain-POCO)
   stehen offen — entkoppelt erst wenn ein Headless-Use-Case kommt.

2. **MultiModelAnalysisService.cs** noch 1547 LOC (war 2185). Helpers +
   Filter sind extrahiert. Hauptklasse enthaelt noch den GPU-State-Automaten
   (AnalyzeAsync + AnalyzeWithNvdecAsync ~ 1000 LOC).

3. **Keine Tests** fuer: PlayerWindow (zu UI-bound), Sidecar-Routes
   (Python-Side, braucht pytest-Setup), VLC-Steuerung, SAM-Renderer.

4. **CategoryWeights-Tabelle** ist neu aktiviert — bei einer frischen
   KB sind noch keine gelernten Gewichte da. QualityGate fallt auf
   Default-Gewichte zurueck. Erst nach 25+ Validierungs-Eintraegen
   (FeedbackIngestionService) lernt das System per-Code-Gewichte.

---

## Schnelltest-Checkliste (15 min)

- [ ] App startet ohne Fehler. Diagnose-Tab oeffnen, Log-Tail leer aber kein Crash.
- [ ] `Brain-Mirror Health` → "Pruefen" klicken. Status muss Green/Yellow/Red zeigen.
- [ ] Ein Projekt mit ein paar Haltungen oeffnen. Builder-Page funktioniert.
- [ ] Video oeffnen, Play/Pause, Slider seeken. VLC laeuft.
- [ ] Codier-Modus aktivieren. Mark-Tool "Punkt" → auf Video klicken → VSA-Picker geht auf.
- [ ] In TrainingCenter: einen PDF-Ordner waehlen, Scan starten. Cases erscheinen.
- [ ] App schliessen. Sidecar-Prozess sollte nach < 5s aus dem Task-Manager verschwinden.

## Vollstaendiger Audit-Bericht
Detaillierte Befund-Tabelle: `docs/AUDIT_SCHLUSSANALYSE_2026-05-06.md`
