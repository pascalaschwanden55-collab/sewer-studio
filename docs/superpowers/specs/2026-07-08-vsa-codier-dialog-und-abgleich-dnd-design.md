# VSA-Codier-Dialog modernisieren + Abgleich-Panel: Drag&Drop und symmetrische Aktionen

## Context
Zwei UI-Wünsche im SewerStudio-Codierbereich (WPF/MVVM):

1. **Moderner Codier-Dialog im Protokoll-Editor.** Beim „+" in „Primäre Schäden" (bzw. Neu/Kopieren/Bearbeiten in der Beobachtungsliste) öffnet heute der **alte** Dialog `ObservationCatalogWindow` (gelbe Felder, Uhr-Ziffernblatt). Gewünscht ist der bereits vorhandene **moderne** Dialog `VsaCodeExplorerWindow` („VSA Schadencodierung – VSA-KEK 2020", Gruppe→Hauptcode→Char1→Char2, Position, Fotos), der im Player/Live-Codieren schon aktiv ist.

2. **Abgleich-Panel gleichmächtig machen + Kacheln bewegen.** Im Codier-Panel (`PlayerCodingSidePanel`) stehen links **KI-Befunde** und rechts **Import**-Befunde (beide `CodingEvent`). Gewünscht: (a) Kacheln per **Drag&Drop** zwischen den Spalten verschieben/kopieren; (b) die **rechte** Spalte soll dieselben Aktionen wie links können: **Fotos anzeigen, Bearbeiten inkl. BBox ziehen, Bestätigen → ins KI-Brain**.

Ziel: konsistente, moderne Codier-Bedienung und ein symmetrisches Abgleich-Panel, das Training-Samples (Foto + Code) aus beiden Spalten ins KI-Brain (KnowledgeBase) bringt.

## Bestätigte Entscheidungen (Nutzer)
- Moderner Dialog für **Neu, Kopieren UND Bearbeiten** (alle laufen über eine Stelle).
- Kacheln bewegen per **Drag & Drop**; Ziehen = **Verschieben**, **Strg+Ziehen = Kopieren**.
- **In die KI-Spalte gezogen** = echter, **noch unbestätigter** KI-Befund; erst beim Bestätigen geht Foto+Code ins Brain.
- **Rechte Spalte** bekommt: Bestätigen→Brain, Fotos anzeigen, BBox ziehen — „wie links".

## Feature 1 — Moderner Dialog im Protokoll-Editor

**Eine Stelle:** `src/AuswertungPro.Next.UI/Views/ProtocolObservationsWindow.xaml.cs` → `OpenObservationDialog(ProtocolEntry entry)` (heute ~Z. 213–241) öffnet künftig `VsaCodeExplorerWindow` statt `ObservationCatalogWindow`.

Ablauf:
```
var vm  = new VsaCodeExplorerViewModel(entry, entry.MeterStart, entry.Zeit, _sp.CodeSelectionCatalog);
var dlg = new VsaCodeExplorerWindow(vm, videoPath, entry.Zeit) { Owner = this };
if (dlg.ShowDialog() == true && dlg.SelectedEntry is not null) {
    CodingProtocolEntryCopier.CopyEditableValues(entry, dlg.SelectedEntry);
    return true;
}
return false;
```
- Katalog: `_sp.CodeSelectionCatalog` (`IVsaCodeSelectionCatalog`), verfügbar in `ServiceProvider` (Z. 98/293).
- Rückspiegeln: `CodingProtocolEntryCopier.CopyEditableValues` (Muster aus `CodingCodeExplorerWorkflowService`).
- Rückgabe bleibt `bool` → „Primäre Schäden" aktualisiert sich unverändert (`SyncPrimaryDamagesFromCurrentEntries` / Controller-Refresh).
- Startgröße auf ~1420×850 setzen (Wunsch); `WindowStateManager` merkt Resize.
- `ObservationCatalogWindow` wird danach nirgends mehr geöffnet (einziger Aufrufer war diese Methode) — bleibt liegen, **kein Löschen**.
- Wegfall: KI-Vorschlag-Knopf (Ctrl+L, `_sp.ProtocolAi`) des alten Dialogs; der moderne hat ihn nicht (dafür Foto-Vermessung/Live-Snapshot; `LiveSnapshotProvider` bleibt null im Protokoll-Editor).

## Feature 2a — Drag & Drop zwischen KI ↔ Import

- **Neu:** Attached-Behavior `src/AuswertungPro.Next.UI/Behaviors/CodingEventDragDropBehavior.cs` (analog `PhotoHoverPreviewBehavior`), aktiviert auf `LstCodingEvents` **und** `LstImportEvents` (`PlayerCodingSidePanel.xaml`).
- **Neu:** reiner Helfer `src/AuswertungPro.Next.UI/Ai/CodingEventColumnTransfer.cs` mit `Move(ev, src, target)` und `Copy(ev, target)` (Stil wie `CodingImportReferenceTransfer`, nach Meter sortiert).
- Ziel-Collections: KI = `_codingSessionHost.EventCollection` (= `CodingSessionViewModel.Events`), Import = `_codingImportReferenceEvents.Events`.
- **Move (Ziehen):** Event aus Quelle raus, in Ziel rein.
  - **Aus KI heraus:** zusätzlich aus der Session entfernen (bestehender Delete-/Removal-Pfad), damit es nicht ins gespeicherte Protokoll zurückblutet.
  - **In KI hinein:** als KI-Spalten-Event ergänzen, **ohne** Auto-Accept → `AiContext` bleibt „offen/ungeprüft" → Nutzer bestätigt später (dann Brain).
- **Copy (Strg+Ziehen):** Deep-Clone des `CodingEvent` mit **neuer EventId und neuer EntryId** (sonst kollidieren IDs → Abgleich/Highlighting kaputt); Original bleibt. Clone kopiert `Entry` (inkl. `FotoPaths`, `CodeMeta`), `Overlay`, `AiContext`.
- **Nach Transfer:** `RefreshCodingEventsList()` + Import-Zähler (`SetCount`) aktualisieren, `RunCodingProtocolMatch()` erneut → Badges/Konfidenz frisch.

## Feature 2b — Rechte Spalte: gleiche Aktionen wie links

Alle Aktionen nutzen **bestehende** Workflows, nur auf `LstImportEvents.SelectedItem` umgebogen. Je Aktion 4 gleichartige Andockpunkte:
1. MenuItem in `PlayerCodingSidePanel.xaml` (rechtes Kontextmenü ~Z. 343–348),
2. Relay-Event in `PlayerCodingSidePanel.xaml.cs`,
3. Handler-Record + Bind in `PlayerCodingSidePanelEventBinder.cs`,
4. Host-Wiring in `PlayerWindow.CodingSidePanelAccessors.cs` → Methode im passenden `PlayerWindow.Coding.*.cs`-Partial.

- **Fotos anzeigen (rechts):** Host-Handler ruft `CodingPhotoViewerCommandWorkflow.Execute(new CodingPhotoViewerCommandRequest(LstImportEvents.SelectedItem), …)` — unverändert. (Hover-Vorschau ist rechts schon aktiv.)
- **Bearbeiten / BBox ziehen (rechts):** Host-Handler ruft `TryEditCodingEvent(LstImportEvents.SelectedItem)` bzw. `CodingCodeExplorerEditWorkflow.Execute(...)` → öffnet `VsaCodeExplorerWindow` → „PhotoAssistant" (`PhotoMeasurementWindow`, Werkzeug `MarkRect`) → gezogene Box wird als Overlay-PNG + `OverlayGeometry` in den `Entry`/`FotoPaths` geschrieben (`VsaCodeExplorerPhotoResultWorkflow.Apply`).
- **Bestätigen → ins KI-Brain (rechts):** Host-Handler:
  ```
  CodingEventDecisionPolicy.ApplyManualReviewDecision(importEvent, CodingUserDecision.Accepted, "Import bestätigt");
  await PersistSingleEventAsTrainingSample(importEvent);   // bestehende Kette
  ```
  → `CodingTrainingSamplePersistenceCoordinator` → `TrainingSamplesStore.MergeAndSaveAsync` (`training_samples.json`) **und** (Status==Approved) `ICodingSessionService.IndexConfirmedSampleAsync` → `KnowledgeBaseManager.IndexSampleAsync` (`knowledge_base.db`: Embedding aus `Beschreibung` + Upsert). Indexierung nur bei gültigem VSA-Code + Beschreibung ≥10 Zeichen + fachlich plausibel; sonst `KbIndexState.Pending` (Nachhol-Lauf). Ollama muss erreichbar sein.
  - Ersetzt/ergänzt die heutige rechte „Bestätigen (als Training übernehmen)"-Aktion (die nur `TeacherAnnotation` + Trainingsbild schreibt, **nicht** ins Brain).

## Wiederverwendete Bausteine (kein neues Rad)
- Dialog: `VsaCodeExplorerWindow` / `VsaCodeExplorerViewModel` / `CodingProtocolEntryCopier`; Katalog `_sp.CodeSelectionCatalog`.
- Foto: `CodingPhotoViewerCommandWorkflow` / `CodingPhotoViewerDisplayWorkflow`.
- BBox: `CodingCodeExplorerEditWorkflow` → `PhotoMeasurementWindow` (Tool `MarkRect`) → `OverlayGeometry`.
- Brain: `PersistSingleEventAsTrainingSample` → `CodingTrainingSamplePersistenceCoordinator` → `CodingTrainingSamplePersister` → `IndexConfirmedSampleAsync` → `KnowledgeBaseManager.IndexSampleAsync`.
- Transfer-Vorbild: `CodingImportReferenceTransfer`.

## Tests
- Neuer Unit-Test `CodingEventColumnTransferTests`: Move (Event aus Quelle entfernt, in Ziel eingefügt, Sortierung); Copy (Original bleibt, Duplikat mit **neuer** EventId/EntryId, Fotos/CodeMeta übernommen).
- Manuelle Verifikation (Player, echtes Projekt): F1-Dialog aus Beobachtungsliste; DnD in beide Richtungen inkl. Strg-Copy; rechte Aktionen Foto/BBox/Bestätigen; Nachweis, dass ein bestätigter Import-Befund im `knowledge_base.db`/`training_samples.json` erscheint.

## Nicht im Scope (YAGNI)
- Kein Löschen des alten `ObservationCatalogWindow`.
- Kein Umbau des linken Accept-Pfads (bleibt wie er ist).
- Keine Änderung an der Brain-Indexierlogik selbst (nur Wiederverwendung).
