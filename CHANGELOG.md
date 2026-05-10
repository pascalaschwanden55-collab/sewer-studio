# Changelog

Notable changes to this project. Format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) loosely; SewerStudio is not versioned formally yet, so entries are grouped by branch / development period.

## Unreleased — branch `feature/pdf-import-beobachtungen`

### Phase 6.3 Vorbereitung — MultiModelAnalysisService WPF-frei

ARCH-H5 / Thin-AI-Prinzip aus CLAUDE.md: KI-Logik soll aus dem
UI-Layer in Application/Infrastructure migrieren. Die letzten WPF-
Kopplungen in `MultiModelAnalysisService` (zwei `BitmapDecoder`-Sites
fuer Auto-Kalibrierung) sind mit dieser Slice durch eine
Application-Layer-Abstraktion ersetzt — der Service ist jetzt
WPF-frei und kann in einem Folge-Slice nach
`Infrastructure/Ai/Pipeline` verschoben werden (mechanisch).

**Added:**

- `AuswertungPro.Next.Application.Ai.Imaging.IPipeCalibrationFromBytes`
  Interface + `PipeCalibrationFromBytesProvider` Static-Provider
  (Pattern analog `ImagePixelDecoderProvider`).
- `AuswertungPro.Next.UI.Imaging.WpfPipeCalibrationFromBytes` als
  WPF-Impl (intern `BitmapDecoder` + `AutoCalibrationService`).
- `App.xaml.cs` registriert die Impl gemeinsam mit den anderen
  Imaging-Providern beim App-Start.

**Changed:**

- `MultiModelAnalysisService` ruft beim ersten brauchbaren Frame
  `PipeCalibrationFromBytesProvider.Instance?.TryCalibrate(bytes, dn)`
  statt direkt `BitmapDecoder.Create(...)` + `AutoCalibrationService.TryAutoCalibrate(...)`.
  Bei null-Provider (Tests / nicht-UI-Hosts) faellt der Calibration-
  Schritt aequivalent zum heutigen "TryAutoCalibrate liefert null"-Pfad zurueck.

**Documentation:**

- [`docs/adrs/2026-05-10-phase-6-3-multimodel-wpf-decouple.md`](docs/adrs/2026-05-10-phase-6-3-multimodel-wpf-decouple.md) — Mini-ADR, Status: Done.

### Slice 8a — PlayerWindow-Cleanup nach 5b

Entfernt Dead-Code-Residuen aus `PlayerWindow.CodingApply.cs`, die nach
dem Loesch-Slice 8a.3 Step 5b zurueckgeblieben waren.

**Removed:**

- `PlayerWindow.CodingApply._codingSchemaType` (nur null-gesetzt, nie
  gelesen).
- `PlayerWindow.CodingApply._codingLastOsdMeter` (nie gesetzt, immer
  null) und der dazugehoerige `#pragma warning disable CS0414, CS0649`
  Block.
- `PlayerWindow.CodingApply.EnsureHaltungslaenge` (Fallback-Kette fuer
  Haltungslaenge_m) — einziger Caller war der in 5b geloeschte
  In-Place-Coding-Mode. Falls eine Folge-Slice die Fallback-Kette
  wieder braucht: git-history vor `5b3900a`.
- `PlayerWindow.Helpers.HasValidLength` (einziger Caller war
  EnsureHaltungslaenge).

**Reframing der Audit-Diff-Plan-Steps 9-11:** Der Original-Audit-Diff
vom 2026-05-09 listete Static-Bridge-Migration + Aufrufer-Umleitung +
PlayerWindow-Loeschung als finale Schritte. Reality-Check 2026-05-10:
PlayerWindow ist 5478 LOC in 14 Partials und das Default-Video-Fenster
mit aktiven eigenen Features (LiveDetection, OperateurAnnotation,
MarkTool, TrainingMode, VideoPlayback, Hotkeys etc.). Eine Aufloesung
im Audit-Diff-Sinn entfaellt by-design — die Coding-Partials sind in
5b weg, der Rest bleibt.

**LOC-Delta:** -76 netto.

**Documentation:**

- [`docs/adrs/2026-05-10-slice-8a-playerwindow-cleanup.md`](docs/adrs/2026-05-10-slice-8a-playerwindow-cleanup.md) — Mini-ADR, Status: Done.

### Slice 8a — Auto-BCD/BCE/Streckenschaden

Korrigiert die Streckenschaden-Behandlung beim Session-Abschluss (vorher
warf eine `InvalidOperationException`, jetzt gibt es einen Yes/No/Cancel-
Dialog wie im Legacy-PlayerWindow). BCD/BCE-Auto-Erzeugung bleibt im
bestehenden `ProtocolBoundaryService.EnsureBoundaries`-Pfad — der ist
getestet und garantiert VSA-Konformitaet.

**Changed:**

- `ICodingSessionService.CompleteSession` bekommt einen optionalen
  Parameter `bool allowOpenStreckenschaden = false`. Default behaelt das
  alte Verhalten (Exception bei offenen Streckenschaeden); mit `true`
  landen offene Streckenschaeden mit `MeterEnd=null` im Protokoll und
  werden von `ProtocolBoundaryService.Validate` als Warnung geflaggt.
- `CodingSessionViewModel.CompleteSessionWithChoice(bool allowOpen)` als
  neue VM-Methode fuer den Window-Pre-Complete-Hook (existing
  `CompleteSessionCommand` ruft weiter mit Default).
- `CodingModeWindow.BtnComplete_Click` zeigt vor dem Abschluss den
  Streckenschaden-Yes/No/Cancel-Dialog (neue Partial
  `CodingModeWindow.StreckenschadenDialog.cs`).

**Added:**

- `CodingModeWindow.StreckenschadenDialog.cs` mit
  `ConfirmOpenStreckenschadenAndChooseAction(out bool allowOpen)`:
  - Yes → alle offenen Streckenschaeden via
    `ICodingSessionService.CloseStreckenschaden` bei `MeterAtCapture`
    bzw. aktuellem Meter schliessen (mit 1cm-Floor falls endMeter
    <= MeterStart).
  - No → return mit allowOpen=true, Caller ruft
    CompleteSessionWithChoice(true).
  - Cancel → return false, Window bleibt offen.

**Tests:** 5 neue Faelle in
`CodingSessionServiceCompleteSessionTests` (Default-Throw,
allowOpen=false-Throw, allowOpen=true mit/ohne offene Streckenschaeden,
geschlossene Streckenschaden Baseline). `[Collection("KnowledgeRootIsolation")]`
auf den Tests + `TrainingSamplesWriterAdapterTests` serialisiert den
statischen `KnowledgeRootProvider._resolver`. Infra 185 → 190.

**Documentation:**

- [`docs/adrs/2026-05-10-slice-8a-auto-bcd-bce-strecke.md`](docs/adrs/2026-05-10-slice-8a-auto-bcd-bce-strecke.md) — Mini-ADR, Status: Done.

### Slice 8a — Auto-Kalibrierung-Wiring

`AutoCalibrationService.TryAutoCalibrate` (172 LOC, Pixel-Scan-Algorithmus,
existiert seit langem) wird jetzt im neuen `CodingModeWindow` automatisch
beim ersten Ready-Frame ausgeloest — vorher startete jede Coding-Modus-
Session unkalibriert und der User musste die Referenz-Linie manuell
ziehen.

**Added:**

- `CodingModeWindow.AutoCalibration.cs`-Partial mit zwei Bausteinen:
  - `internal static DecodePngToBitmap(byte[]?)` — PNG → BitmapSource
    mit Null-/Korrupt-Schutz (returnt null statt zu werfen).
  - `private async Task TryAutoCalibrateOnceAsync(byte[]? pngBytes)`
    mit Frueh-Returns (schon kalibriert, schon versucht, kein
    `DN_mm` in den Stammdaten, korruptes PNG, Algo liefert null) und
    atomarem UI-Update auf dem Dispatcher-Thread.
- LiveLoop-Hook in `CodingModeWindow.LiveLoop.RunLiveAnalysisAsync`:
  einzeiliger `await TryAutoCalibrateOnceAsync(pngBytes)`-Call nach
  Warmup-Puffer und vor ShowAiResults.

**Behavior:**

- Bei Erfolg: Status-Text "Auto-kalibriert: DN xxx mm", Mess-Werkzeuge
  liefern direkt plausible mm-Werte ohne manuelle Referenz-Linie.
- Bei Fehlschlag (kein erkennbares Pipe-Profil im ersten Frame):
  Status bleibt "Nicht kalibriert", manueller `BtnCalibrate`-Pfad
  bleibt unveraendert verfuegbar.
- Genau ein Versuch pro Session (`_calibrationAutoTried`-Flag).
- `WasManuallyCalibrated=true` aus dem AutoCalibrationService wird
  unveraendert uebernommen — bestehende Eigenheit (Field-Name
  irrefuehrend, markiert "Calibration gueltig" statt "User-gesetzt").

**Tests:** 3 neue Faelle (Decoder-Roundtrip, null/empty, korrupt).
Pipeline 832 → 835.

**Documentation:**

- [`docs/adrs/2026-05-10-slice-8a-auto-kalibrierung.md`](docs/adrs/2026-05-10-slice-8a-auto-kalibrierung.md) — Mini-ADR, Status: Done.

### Slice 8a — Pause-Confirm-Workflow (re-implementiert)

Pause-Confirm-Workflow im neuen `CodingModeWindow` (alter In-Place-Pfad
im PlayerWindow wurde in Slice 8a.3 Step 5b geloescht). Loop pausiert
das Video bei Yellow/Red-Findings, oeffnet ein Confirmation-Panel mit
Akzeptieren/Bearbeiten/Verwerfen-Buttons, wartet auf User-Decision.

**Added:**

- `CodingSessionViewModel.PauseConfirm.cs`-Partial mit zwei
  Punkt-4-API-Bloecken: ConfirmationFlow (`BeginConfirmationAsync`,
  `CompleteConfirmation`, `IsAwaitingUserDecision`,
  `PendingConfirmationEvent/Confidence/IsRed`) und Sperrliste
  (`AddRejection`, `IsRejected`, `MakeRejectionKey`, alle mit
  Code+Label+Meter-Schluessel).
- `CodingModeWindow.xaml`: Inline-Confirm-Panel als ZIndex=20-Overlay,
  Visibility ans VM gebunden, Ampel-Ellipse mit DataTrigger auf
  `PendingConfirmationIsRed`, Konfidenz-Anzeige in Prozent.
- `CodingModeWindow.PauseConfirm.cs`: Click-Handler + statische Helper
  `EvaluateGate` (lokale Severity-Policy bis ein echter QualityGate-
  Service da ist) und `BuildCodingEventFromFinding`.
- `CodingModeWindow.LiveLoop.PromptConfirmIfNeededAsync`: erstes
  Yellow/Red-Finding ans VM melden, Player pausieren/resumen,
  Decision-Branches (Accepted → AddEventInOrder; AcceptedWithEdit →
  zusaetzlich LstEvents.SelectedItem + ScrollIntoView +
  UpdateDefectDetailPanel; Rejected → echte VSA-Codes in Sperrliste,
  AI-Fallback als One-Shot-Drop).
- Sperrlisten-Schluessel `CODE|LABEL@MM.MM` mit zwei Tolerance-Stufen:
  echte VSA-Codes +/-0.5m, AI-Bucket +/-0.1m.

**Tests:** 27 neue Faelle (12 ConfirmationFlow + 20 Sperrliste mit
Label-Disambiguator + AI-Tolerance). Pipeline 800 → 832.

**Documentation:**

- [`docs/adrs/2026-05-10-slice-8a-pause-confirm.md`](docs/adrs/2026-05-10-slice-8a-pause-confirm.md) — Mini-ADR, Status: Done.

### Slice 1 — Operateur-Annotation im Trainingsmodus

End-to-End-Workflow: Haltungsordner importieren → VSA-Codes aus PDF lesen → Code anklicken → Box ziehen → SAM-Maske bestätigen → Sample landet Best-Effort in TrainingSamplesStore + KnowledgeBase + YOLO-seg-Datensatz.

**Added:**

- `OperateurAnnotationService` (Two-Phase-API: `PreviewMaskAsync` ohne Persistenz, `CommitAsync` mit Best-Effort Store > YOLO > KB).
- `OperateurAnnotationSession` mit State-Maschine (`Pending → Active → PreviewReady → Committed | Skipped | Rejected | Error`) und Terminal-State-Guards.
- `OperateurSessionBuilder` für Folder-Import (Video + PDF).
- `BeobachtungParser` für PDF-Volltext → `(Code, Meter, Description)`. Standard- und Fretz-Format.
- Adapter-Interfaces in Application: `ITrainingSamplesWriter`, `IKnowledgeBaseIndexer`, `IYoloDatasetWriter`.
- `TrainingSamplesStore.AppendOneAsync` (atomar, ohne Signature-Dedup) und `UpdateIndexStateAsync`.
- `YoloDatasetExportService.AppendSampleAsync` für YOLO-seg-Single-Append mit Polygon aus `MaskPreview.PolygonJson`.
- `VsaYoloClassMap.TryGetClassId` (Lookup ohne Auto-Create für stabile Class-IDs).
- Sidecar: `SamRequest.return_polygon` + `MaskResult.polygon_points` (cv2.approxPolyDP im Wrapper).
- C# DTOs: `SamRequest.ReturnPolygon`, `SamMaskResult.PolygonPoints` (additiv, alle 10 bestehenden Felder unverändert).
- `TrainingSample`-Felder: SAM-Maske (`SamMaskRle`, `SamMaskEncoding`, `MaskWidth`, `MaskHeight`, `MaskAreaPixels`, `SamConfidence`), `FrameDeltaSeconds`, `HasMask`-Computed.
- `SourceTypeNames.OperateurAnnotation` als Konstante.
- UI: `OperatorSidePanel` mit Code-Liste (Status-Dot + Code + Meterstand), Confirm/Skip/Reject/Erneut-zeichnen, Hotkeys (ESC, Strg+Enter, Strg+Umsch+Z, Strg+R).
- DI-Wiring in `ServiceCollectionConfigurator.WireOperateurAnnotationService`, Service-Zugriff via `OperateurAnnotationServiceAccessor`.

**Documentation:**

- [`docs/superpowers/specs/2026-05-08-operateur-annotation-design.md`](docs/superpowers/specs/2026-05-08-operateur-annotation-design.md) — Spec.
- [`docs/superpowers/plans/2026-05-08-operateur-annotation-plan.md`](docs/superpowers/plans/2026-05-08-operateur-annotation-plan.md) — Plan mit 6 Blockern + 5 Korrekturen im Header.
- [`docs/superpowers/SLICE-1-STATUS.md`](docs/superpowers/SLICE-1-STATUS.md) — Statusbericht für Tester/Review.

**Tests:** ~50 neue Tests (Pipeline + Infrastructure). Build durchgehend 0 Warnungen / 0 Fehler.

### Sprint 3 — UI-Optik
- Spacing-Grid einheitlich, Brush-Konsolidierung, Tester-Banner.

### Sprint 2 — Pipeline-Telemetry
- SQLite-Telemetry produktiv aktiviert, JSONL parallel.
- Repo-Hygiene: 67 generierte Files aus dem Index entfernt, ~50 MB Index-Reduktion.

### Sprint 1 — Robustheit + Konsistenz
- Erste Architektur-Refactors (siehe `docs/adr/`).

### Test-Briefing für externe Tester
- [`docs/TEST_BRIEFING_2026-05-07.md`](docs/TEST_BRIEFING_2026-05-07.md).

## Vorher

Diese Changelog-Datei ist neu (Slice 1, 2026-05-08). Die Geschichte vor `feature/pdf-import-beobachtungen` lebt im Git-Log und in den existierenden Audit-Dokumenten:

- [`docs/AUDIT_SCHLUSSANALYSE_2026-05-06.md`](docs/AUDIT_SCHLUSSANALYSE_2026-05-06.md)
- [`docs/INTENSIV_AUDIT_STANDORTBESTIMMUNG_2026-05-07.md`](docs/INTENSIV_AUDIT_STANDORTBESTIMMUNG_2026-05-07.md)
- [`docs/ROADMAP.md`](docs/ROADMAP.md)
