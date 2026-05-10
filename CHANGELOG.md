# Changelog

Notable changes to this project. Format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) loosely; SewerStudio is not versioned formally yet, so entries are grouped by branch / development period.

## Unreleased — branch `feature/pdf-import-beobachtungen`

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
