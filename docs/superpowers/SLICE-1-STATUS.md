# Slice 1 — Operateur-Annotation: Status

**Branch:** `feature/pdf-import-beobachtungen`
**Stand:** 2026-05-08
**Status:** End-to-End-Workflow implementiert; manueller Smoke-Test mit echtem Sidecar steht aus.

## Was ist gebaut

Im Trainingsmodus kann der Operateur:

1. Einen Haltungsordner (Video + Inspektions-PDF) auswählen.
2. Aus dem PDF wird eine Liste der VSA-Codes mit Meterstand extrahiert und sortiert angezeigt.
3. Code anklicken → Active. Player pausiert (Seek auf Frame-Zeit ist Slice-2-Komfort).
4. Box über den Schaden ziehen → SAM segmentiert mit `return_polygon=true`.
5. SAM-Maske bleibt im Player-Overlay sichtbar bis Confirm/Discard.
6. **Confirm** (Strg+Enter oder Button): Sample landet Best-Effort in Store > YOLO-seg-Datensatz > KnowledgeBase.
7. Auto-Advance auf nächsten Pending-Code. Skip / Reject / Re-Draw verfügbar.

## Architektur-Entscheidungen

Implementation folgt dem gepatchten [Plan](plans/2026-05-08-operateur-annotation-plan.md) mit allen 6 Blockern (B1–B6) und 5 Korrekturen (K1–K5) aus dem Header-Patch:

- **B1**: Polygon-Approximation per cv2 lebt im `sam_wrapper.py`, nicht in der Route.
- **B2**: `SamMaskResult.PolygonPoints` additiv ergänzt — alle 10 bestehenden Felder unangetastet.
- **B3**: `IYoloDatasetWriter.AppendSampleAsync(sample, preview, ct)` führt Polygon durch alle Schichten.
- **B4**: YOLO-Pfad-Layout `{root}/images/train/` + `{root}/labels/train/`, `data.yaml` in stabiler `VsaYoloClassMap`-Reihenfolge.
- **B5**: Kein PlayerWindow-VM — direkte Felder im Partial, Mark-Tool-Pattern.
- **B6**: Box-Overlay via vorhandenes `CodingOverlayPopup` (WPF-Airspace mit LibVLC).
- **K1**: `FindCommittedAsync(meter, meterTolerance)` mit Anker-Meterstand.
- **K2**: KB-Erfolg setzt `KbIndexState.Indexed`, Fehler setzt `Pending`.
- **K3**: `OperationCanceledException` wird in jedem Block durchgereicht, niemals als Warning maskiert.
- **K4**: Frame finalisiert vor Store-Append nach `KI_BRAIN/frames/<sanitizedCaseId>/<SampleId>.png`.
- **K5**: Test-Stub-Signaturen entsprechen den realen Konstruktoren.

## Code-Hinweise

| Layer | Datei | Zweck |
|---|---|---|
| Domain | `TrainingSample.cs` | SAM-Maske-Felder + `FrameDeltaSeconds` |
| Application | `Ai/Annotation/OperateurAnnotationModels.cs` | DTOs |
| Application | `Ai/Annotation/OperateurAnnotationSession.cs` | State-Maschine + Helper |
| Application | `Ai/Annotation/I*.cs` | 4 Adapter-Interfaces |
| Application | `Ai/Annotation/BeobachtungParser.cs` | Pure-Text PDF-Parser |
| Application | `Ai/Annotation/OperateurAnnotationServiceAccessor.cs` | Singleton-Accessor |
| Application | `Ai/Teacher/VsaYoloClassMap.TryGetClassId` | Lookup ohne Auto-Create |
| Infrastructure | `Ai/Annotation/TrainingSamplesWriterAdapter.cs` | Store-Adapter |
| Infrastructure | `Ai/Annotation/KnowledgeBaseIndexerAdapter.cs` | KB-Adapter (Func-Hook für Tests) |
| Infrastructure | `Ai/Annotation/OperateurSessionBuilder.cs` | Folder → Session |
| Infrastructure | `Ai/Annotation/OperateurAnnotationService.cs` | Two-Phase-API |
| Infrastructure | `Ai/Training/YoloDatasetExportService.AppendSampleAsync` | Single-Sample YOLO-seg |
| Sidecar | `models/sam_wrapper.py` (`_mask_to_polygon`) | cv2.approxPolyDP |
| Sidecar | `schemas/segmentation.py` | `return_polygon` + `polygon_points` |
| UI | `Views/Windows/PlayerWindow.OperateurAnnotation.cs` | Submodus-Code-Behind |
| UI | `Views/Windows/PlayerWindow.xaml` (`OperatorSidePanel`) | XAML |
| UI | `Composition/ServiceCollectionConfigurator.cs` (`WireOperateurAnnotationService`) | DI |

## Test-Coverage

- 24 Slice-1-Tests in der Pipeline-Suite (Application + Service-Verträge).
- 14 Slice-1-Tests in der Infrastructure-Suite (Adapter + YOLO-Append + Session-Builder).
- Build durchgehend 0 Warnungen / 0 Fehler über alle 9 Phasen.

## Manueller Smoke-Test (offen)

Voraussetzungen: Sidecar läuft, Ollama läuft mit `nomic-embed-text`, `D:\yolo_sewer_v1\` schreibbar.

Soll-Verhalten:

1. Trainings-Modus → "Haltungsordner waehlen…" → Ordner-Picker öffnet sich.
2. Code-Liste zeigt Codes mit farbigem Status-Dot und Meterstand, sortiert.
3. Box ziehen → Status "SAM segmentiert …" → "Maske bereit — Strg+Enter zum Bestaetigen". Maske bleibt im Player-Overlay sichtbar.
4. Strg+Enter → Dot grün; Sample-Eintrag in `training_samples.json`; PNG in `KI_BRAIN/frames/<CaseId>/`; YOLO-Label in `D:\yolo_sewer_v1\labels\train\`; `data.yaml` aktualisiert.
5. Skip (Strg+Umsch+Z) → Dot orange; Reject (Strg+R) → Dot rot.
6. ESC verlässt nur den Submodus, nicht den Trainings-Modus.

## Tech-Debt aus Slice 1 (Slice 2)

- **Class-Map-Inkonsistenz** zwischen `ExportAsync` (lokale Map aus Sample-Codes) und `AppendSampleAsync` (`VsaYoloClassMap`). Marker im Service-Docstring. Risiko bei nächstem YOLO-Retrain.
- **Live-Mask-Render im Player** ist über das CodingOverlay-Pattern verdrahtet; Polygon-Anzeige als geschlossener Pfad könnte später als Komfort-Feature noch verfeinert werden.

## Schon adressiert

- Sanitizer für CaseId in Pfadbau (`ProjectPathResolver.SanitizePathSegment`).
- KB-`IsIndexWorthy`-Anforderung: `Beschreibung` wird aus VSA-Code + Kataloglabel gefüllt.
- Terminal-State-Guards in `OperateurAnnotationSession`.
- UI-Thread-Affinity (kein `ConfigureAwait(false)` auf den UI-Pfad-Continuations).

## Bewusst nicht in Slice 1

- Live-Polygon-Highlight als eigene Render-Schicht über VLC.
- Validation-Split im YOLO-Append (Slice 1 schreibt nur in Train-Split; `images/val` + `labels/val` werden für Ultralytics-Validität angelegt, bleiben leer).
- PDF-Layout-Spezialfälle jenseits Standard- und Fretz-Format.
