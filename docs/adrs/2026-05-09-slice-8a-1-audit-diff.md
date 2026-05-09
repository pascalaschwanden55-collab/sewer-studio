# Slice 8a.1 — Audit-Diff PlayerWindow.CodingMode vs CodingModeWindow

Datum: 2026-05-09
Status: Audit-Lauf (kein Code-Change)

## Zahlen

| Klasse | Methoden gesamt |
|---|---|
| PlayerWindow.Coding* (alle Coding-Partials zusammen) | **118** |
| CodingModeWindow.xaml.cs | **91** |
| Gleicher Name in beiden | 2 (`RenderAiOverlays`, `RenderOverlayGeometry`) |
| Nur PlayerWindow | **116** |
| Nur CodingModeWindow | **89** |

Methoden-Sets ueberlappen praktisch nicht — beide Implementierungen sind
unabhaengig gewachsen. Das macht die Migration aufwendiger als gedacht.

## Funktionale Aequivalenz (gleiche Aufgabe, andere Namen)

| PlayerWindow | CodingModeWindow | Aufgabe |
|---|---|---|
| `UpdateCodingDefectDetailPanel` | `UpdateDefectDetailPanel` | Befund-Detail-Panel rechts |
| `UpdateCodingStatistics` | `UpdateStatistics` | Fortschritts-Anzeige |
| `UpdateCodingUi` | `UpdateUi` | UI-Refresh nach VM-Aenderung |
| `UpdateCodingOverlayCursor` | (Tool-Button-Logik in `ToolButton_Checked`) | Cursor je nach Werkzeug |
| `StartCodingAiPulse` | `StartAiStatusPulse` | KI-Status-LED-Pulsen |
| `ShowCodingAiResults` | `ShowAiResults` | KI-Ergebnis ins Panel |
| `CodingApply_Click` | `BtnAcceptDefect_Click` | Befund uebernehmen |
| `CodingRejectDefect_Click` | `BtnRejectDefect_Click` | Befund verwerfen |
| `CodingEditDefect_Click` | `BtnEditDefect_Click` | Befund editieren |
| `CodingCalibrate_Click` | `BtnCalibrate_Checked/Unchecked` | Kalibrieren-Modus |
| `CodingTakePhoto_Click` | `BtnFoto1_Click/Foto2_Click` | Foto erfassen |
| `RenderAiOverlays` | `RenderAiOverlays` | KI-Overlays zeichnen |

## Features nur in PlayerWindow.CodingMode (kritisch fuer Migration)

Diese muessen ins CodingModeWindow uebernommen werden, sonst geht
Funktionalitaet verloren:

### Schema-Rendering (Slice 26 schon als Partial extrahiert)
- `RenderActiveCodingSchema` — Schema-Layer fuer Anschluss/Bogen/Knick/etc.
- `RenderSchemaPipeReference`, `AddSchemaLabel`
- `RenderLateralCircleOverlay`, `RenderLevelOverlay`, `RenderPipeBendOverlay`,
  `RenderPipeDirectionOverlay`, `RenderRulerOverlay`, `RenderReferenceDn`

### Auto-BCD/BCE/Streckenschaden (fachlich wichtig)
- `EnsureRohranfangExists` — automatischer BCD-Code bei Meter 0
- `EnsureRohrendeExists` — automatischer BCE-Code am Haltungsende
- `CloseOpenStreckenschaeden` — offene Streckenschaeden bei Session-Ende schliessen
- `EnsureHaltungslaenge` — Haltungslaenge aus Stammdaten auflesen

### Auto-Kalibrierung
- `TryAutoCalibrationFromCurrentFrame` — Auto-Kalibrierung aus Frame ohne
  User-Klick. **Wichtiges UX-Feature.**

### Coding-AI-Pause-Confirm-Workflow
- `PauseAndAskConfirmation` — Bei niedrigem QualityGate-Score Video pausieren
- `ConfirmAccept_Click`, `ConfirmEdit_Click`, `ConfirmReject_Click`
- `CloseConfirmationAndResume`, `ResumeAfterPause`, `ResumeAfterConfirmation`

### Frame-Readiness (fachlich wichtig)
- `IsFrameReady`, `UpdateFrameReadiness`, `ResetFrameReadiness`,
  `CodingReadOsdMeterAsync` — verhindert dass KI auf Bewegungsunschaerfe
  oder Schwenks Codes setzt

### Live-AI-Coding-Loop
- `RunCodingAnalysisAsync` — pro Frame KI fragen
- `InitCodingAi` — Pipeline aufsetzen
- `AnalyzeWithOverlayHintAsync` — User-Hinweis (Markierung) → KI-Refinement
- `SetCodingAiState`, `FadeOutAiOverlayAfterAction`

### Dedup + Hinzufuege-Logik
- `IsAlreadyCovered`, `IsSamePosition`, `CodesMatchForDedup`,
  `AddAiFindingsAsEvents`, `AddMultiModelFindingsAsEvents`
- `RefineGenericCodeFromImport`, `TryResolveImportFallbackCode`,
  `LoadExistingProtocolEventsAsImport` — Protokoll-Import-Pfad

### Material-Heuristik
- `IsKunststoffRohr`, `HasNearbyStructuralDamage` — verhindert FP

### OSD-Timer + AI-Pulse
- `StartCodingOsdTimer`, `StopCodingOsdTimer`
- `StartCodingAiPulse`, `StopCodingAiPulse`

### Werkzeug-Auswahl
- `CodingToolBend_Click`, `CodingToolIntrusion_Click`, `CodingToolLevel_Click`,
  `CodingToolRect_Click`, `SetCodingTool`
- `BuildCodingSchemaGeometry`, `UpdateCodingSchemaOverlay`,
  `ClearCodingSchemaOverlay`, `CreateCodingSchemaOverlay`

### Mode-Lifecycle
- `EnterCodingMode`, `ExitCodingMode`, `CodingMode_Click`
- `SuspendCodingOverlayInput`, `ResumeCodingOverlayInput`

## Features nur in CodingModeWindow

Diese sind im CodingModeWindow bereits etabliert und bleiben erhalten:

### Lehrer-Annotation (Trainings-Save)
- `BtnSaveAsTraining_Click` — wichtiger User-Workflow seit Slice "feat(coding)"
- `LoadFewShotAsync`

### Foto-Workflow
- `OfferPhotoCapture`, `CapturePhotoForSelectedEvent`,
  `BtnFoto1_Click`, `BtnFoto2_Click`

### Mess-Werkzeuge
- `ToolButton_Checked/Unchecked`, `ToolButtons`
- `ApplyCalibration`, `RenderPreview`
- `ClassifyBboxWithQwenAsync`, `SegmentBboxWithSamAsync` — moderne BBox+SAM-Pipeline

### Listen-Verhalten
- `LstEvents_MouseDoubleClick`, `LstEvents_SelectionChanged`
- `ContextMenuEdit_Click`, `ContextMenuDelete_Click`
- `ResortEventsByMeter`, `SelectEventByCode`

### Meter-Aufloesung ohne OSD
- `EstimateMeterFromVideoPosition` — Schaetzung aus Video-Position +
  Haltungslaenge wenn OSD nicht lesbar

## Risiko-Bewertung Slice 8a.2 (Migration)

| Block | Aufwand | Risiko | Begruendung |
|---|---|---|---|
| Schema-Rendering | mittel | niedrig | Schon als Partial extrahiert (Slice 26), kann als ganze Datei verschoben werden |
| Auto-BCD/BCE | hoch | mittel | Fachlogik mit Side-Effects auf Session, braucht sorgfaeltigen Test |
| Auto-Kalibrierung | mittel | mittel | Frame-Capture + Pipe-Detection, Sidecar-Abhaengigkeit |
| Pause-Confirm-Workflow | hoch | hoch | Lebt vom Player-State (Pause/Resume), muss umgebaut werden auf Window-eigenen Player |
| Frame-Readiness | mittel | mittel | Tickt mit Video-Timer, muss aufs CodingModeWindow-Player umgehaengt |
| Live-AI-Coding-Loop | hoch | hoch | KI-Pipeline mit eigenem Lifecycle, viele Async-Pfade |
| Dedup-Logik | niedrig | niedrig | Reine Helper-Methoden ohne UI-Bindung |
| Werkzeug-Auswahl | mittel | mittel | Tool-State + Cursor-Wechsel + Schema-Geometry-Builder |
| Mode-Lifecycle | (entfaellt) | — | EnterCodingMode/ExitCodingMode entfaellt — CodingModeWindow IST der Modus |

## Aufrufer von PlayerWindow (extern)

| Datei | Aufruf | Migration |
|---|---|---|
| `DataPage.xaml.cs` | `PlayerWindow.TrySeekTo`, `TryShowOverlayOnLast` | → CodingModeWindow.TrySeekTo (statisch) |
| `BeobachtungenWindow.xaml.cs` | `PlayerWindow.TrySeekTo` | → CodingModeWindow.TrySeekTo |
| `ProtocolEntryEditorDialog.xaml.cs` | `PlayerWindow.TryGetCurrentTime`, `TrySeekTo` | → CodingModeWindow.* |
| `ProtocolObservationsWindow.xaml.cs` | `new PlayerWindow(videoPath, options, overlayText)` + `TryShowOverlayOnLast` | → `new CodingModeWindow(...)` |
| Tests | `PlayerWindow.*` | umbauen oder loeschen |

Die statischen Bridge-Methoden (`TrySeekTo`, `TryShowOverlayOnLast`,
`TryGetCurrentTime`) tracken einen Singleton-PlayerWindow-Slot. Diese
Pattern muss ans CodingModeWindow uebernommen werden, sonst brechen
alle Aufrufer.

## Naechste Schritte (Slice 8a.2 Reihenfolge)

1. **Schema-Rendering verschieben** (niedrigstes Risiko, bereits als Partial extrahiert).
2. **Dedup-Helper verschieben** (reine Helper, leicht).
3. **Material-Heuristik + Frame-Readiness verschieben**.
4. **Werkzeug-Auswahl** (Schema-Geometry-Builder folgt mit Schema-Rendering).
5. **Auto-Kalibrierung**.
6. **Auto-BCD/BCE/Streckenschaden** (fachlich wichtig, sorgfaeltiger Test).
7. **Live-AI-Coding-Loop** (groesster Brocken, eigene Sprint-Iteration).
8. **Pause-Confirm-Workflow** (haengt am Player-State, Migration zuletzt).
9. **Static-Bridge-Methoden umbauen** (TrySeekTo etc.).
10. **Aufrufer umleiten** (DataPage etc.).
11. **PlayerWindow + alle Coding-Partials loeschen**.

Schritt 1-4 sind risikoarm und koennen direkt im Anschluss an dieses
Audit umgesetzt werden. Schritt 5-8 brauchen jeweils einen eigenen
Smoke-Test.
