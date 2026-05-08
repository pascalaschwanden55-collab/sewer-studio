# Changelog

Notable changes to this project. Format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) loosely; SewerStudio is not versioned formally yet, so entries are grouped by branch / development period.

## Unreleased — branch `feature/pdf-import-beobachtungen`

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
