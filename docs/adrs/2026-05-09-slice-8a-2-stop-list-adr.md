# Slice 8a.2 — Stop-Liste / ADR-Vorrat

Datum: 2026-05-09
Status: Offen — vier Kandidaten warten auf Entscheidung
Vorgeschichte:
- ADR Konsolidierung: `2026-05-09-slice-8a-coding-mode-konsolidierung.md` (Option B.1)
- Audit-Diff: `2026-05-09-slice-8a-1-audit-diff.md`
- 8 mechanische Slices (8a.2.1 – 8a.2.8) gepusht

## Kontext

Die Slices 8a.2.1 – 8a.2.8 haben rund **589 Zeilen** aus
`CodingModeWindow.xaml.cs` (3893 → 3304) in 8 Partials extrahiert,
ausschliesslich nach der Regel **klein, mechanisch, kein Session-State**.

Bei vier Kandidaten ist die Regel gerissen. Statt sie still zu
extrahieren, halten wir sie hier fest, beschreiben die konkrete
Verflechtung und entscheiden den Pfad pro Block.

## Stop-Liste

### 1. Calibration-Workflow

**Methoden:** `BtnCalibrate_Checked`, `BtnCalibrate_Unchecked`,
`ApplyCalibration` (Datei `CodingModeWindow.xaml.cs`, ca. Zeile
700–816).

**Warum gestoppt:**
- `BtnCalibrate_Checked` setzt `_isCalibrating = true`, mutiert den
  `_overlayService.ActiveTool` und deaktiviert benachbarte
  `ToggleButton`-Geschwister via `BtnCalibrate.Parent` (XAML-Tree-Walk).
  Damit haengt das Tool-Toggle-Verhalten an einem konkreten Layout —
  jede Verschiebung in einen Partial koppelt das Layout an den Helper.
- `ApplyCalibration` schreibt nicht nur in `_overlayService.SetCalibration`,
  sondern auch in `_sessionService.ActiveSession.Calibration` —
  **direkter Schreibzugriff auf Session-State**.
- Beide Methoden manipulieren UI-Hint-TextBlocks (`TxtCalibrationHint`,
  `TxtCalibrationStatus`) und triggern `DispatcherTimer` zum Ausblenden.

**Was eine Migration brauchen wuerde:**
- Trennung zwischen *Tool-Toggle-Logik* (im Window) und *Calibration-
  Apply* (Service-Aufruf). Letzteres koennte in einen
  CalibrationApplyService wandern, der den Session-Schreibzugriff
  kapselt.
- ToggleButton-Geschwister-Logik ueber ein gemeinsames
  RadioButton-Behavior oder Tool-Group-ViewModel-Property statt
  Visual-Tree-Walk loesen.

**Empfehlung:** Hinter Calibration-Apply-Service-Refactor erst dann
angehen, wenn der Session-State-Besitz (Block 4) geklaert ist.

---

### 2. Preview-Rendering

**Methoden:** `RenderPreview` (`CodingModeWindow.xaml.cs`, Zeile
835–1030, ca. **196 Zeilen** — der mit Abstand groesste Block).

**Warum gestoppt:**
- Liest extensiv aus `_overlayService` (`ActiveTool`, `ActiveLevelMode`,
  `Calibration`) und aus `_vm.CurrentOverlay`.
- Mutiert dedizierte Felder `_previewLine`, `_previewRect`,
  `_previewPoint` — diese werden in `ClearPreviewShapes` und in den
  Mouse-Handlern ebenfalls beruehrt. Eine Verschiebung ohne diese
  Co-Routinen erzeugt orphan-fields im Hauptdatei.
- 8 verschiedene Tool-Typen werden durch `switch (_overlayService.ActiveTool)`
  unterschieden (Line, Stretch, Ruler, Rectangle, Arc, Point, Level,
  Ellipse, Freehand). Pro Tool ein anderer WPF-Shape-Pfad mit
  Calibration-Lookup beim Level-Tool.

**Was eine Migration brauchen wuerde:**
- Erst die Co-Routinen (`ClearPreviewShapes` und Mouse-Handler in
  `OverlayCanvas_*`) auf eine einheitliche Strategy-Pattern-Basis
  bringen, damit Preview pro Tool isoliert ist.
- Alternativ ein `IPreviewRenderer`-Interface mit einer Implementierung
  pro Tool — dann faellt der grosse `switch` in mehrere kleine Renderer.

**Empfehlung:** Preview-Rendering ist Audit-Trial-Material — Strategy-
Pattern-Refactor wuerde den Switch-Kern entzerren. Bevor wir das
machen, sollte die Tool-Auswahl-Logik (`ToolButton_Checked`/`Unchecked`)
ohnehin ueberarbeitet werden, beide haengen am gleichen Tool-State.

---

### 3. Frame-Readiness

**Methoden + State:** `IsFrameReady`, `UpdateFrameReadiness`,
`ResetFrameReadiness`, `CodingReadOsdMeterAsync` plus die Felder
`_codingFrameState` (`FrameReadiness`-Enum), `_codingOsdSkippedFrames`,
`_codingMeterConfirmCount`, `_codingLastOsdMeter`,
`_pendingWarmupResult`. Aktuell **noch in `PlayerWindow.CodingMode.cs`**
(Zeile 1564–1640, plus `CodingReadOsdMeterAsync` ab 1661).

**Warum gestoppt:**
- Die drei `*FrameReadiness`-Methoden sind eine reine State-Maschine —
  fuer sich genommen mechanisch verschiebbar. Aber:
- `CodingReadOsdMeterAsync` ist KEIN Helper: macht VLC-Snapshot, ruft
  Ollama-LLM, schreibt `OsdMeterBadge.Visibility` und `TxtOsdMeter.Text`,
  und ist die Quelle, die `_codingLastOsdMeter` auffuellt.
- Wenn die State-Felder im neuen Window leben, aber
  `CodingReadOsdMeterAsync` im alten bleibt, hat man eine Geisterspur.
  Wenn alles zusammen wandert, zieht man den Live-AI-Coding-Loop mit
  (siehe Audit-Risiko-Tabelle: "Frame-Readiness mittel/mittel").

**Was eine Migration brauchen wuerde:**
- Die OSD-Lese-Logik in einen `IOsdMeterReader`-Service auslagern, der
  die Pixel-zu-Zahl-Pipeline kapselt (Snapshot + Ollama). Dann ist nur
  noch der State-Maschinen-Teil zu verschieben.
- Alternativ: Frame-Readiness komplett bis zum Live-AI-Coding-Loop-
  Slice (siehe Audit-Reihenfolge Schritt 7) stehen lassen und dann mit
  den anderen Loop-Bestandteilen gemeinsam migrieren.

**Empfehlung:** Migration zurueckstellen bis Live-AI-Coding-Loop-Slice.

---

### 4. Session-State-Besitz

**Methoden:** `ResortEventsByMeter` (`CodingModeWindow.xaml.cs`,
Zeile 2366–2384) — direkter `_vm.Events.Clear()` + `_vm.Events.Add(ev)`-
Pfad. Symptomatisch fuer eine groessere Frage:

**Warum gestoppt:**
- `_vm.Events` ist eine `ObservableCollection<CodingEvent>` im
  `CodingSessionViewModel`. Wer darf sie manipulieren? Aktuell tut es
  jeder, der das Window-Event auf Sortierung/Re-Add triggern muss —
  Mouse-Handler, Defect-Akzept-Klicks, Trainings-Save.
- Ohne klares Ownership-Modell wandert mit jedem Slice ein bisschen
  Session-Mutation in eine Partial. Das ist genau das, was die ADR
  vermeiden will.

**Was eine Migration brauchen wuerde:**
- Eine bewusste Entscheidung, wer der **Owner** der Events-Collection
  ist (am ehesten `CodingSessionViewModel` selbst) und welches
  Public-API der Window-Code dafuer benutzen soll. Kandidaten:
  - `_vm.SortByMeter()` (statt `_vm.Events.Clear()` von aussen)
  - `_vm.AddEventInOrder(ev)` (statt `Events.Add` + Resort)
  - `_vm.RemoveEvent(id)` (analog)
- Anschliessend koennen die Window-Methoden, die heute direkt in
  `Events` schreiben, in Partials wandern, weil sie nur noch
  `_vm.Xxx()` aufrufen.

**Empfehlung:** Diesen Punkt als ersten der vier angehen — er ist
Voraussetzung fuer mehrere andere Slices und kein UI-Refactor, sondern
ein klares ViewModel-API-Design.

---

## Reihenfolge fuer die ADR-Bearbeitung

1. **Session-State-Besitz** zuerst — entkoppelt 3 weitere Slices.
2. Dann Calibration-Workflow (CalibrationApplyService).
3. Dann Preview-Rendering (Strategy-Pattern pro Tool).
4. Frame-Readiness als Teil des Live-AI-Coding-Loop-Slices (mit
   Audit-Schritt 7 zusammen).

## Was bleibt fuer die Loop-Iterationen

Mechanische Extraktionen sind ausgeschoepft, was risikoarm war. Die
verbleibenden Bloecke in `CodingModeWindow.xaml.cs` (heute 3304 Zeilen)
sind:

- BBox+SAM-Pipeline (`SegmentBboxWithSamAsync`,
  `ClassifyBboxWithQwenAsync`, `RenderManualSamMaskHighlight`) — ca.
  500 Zeilen, async + Sidecar-Coupling, **keine** mechanische
  Extraktion mehr.
- Mouse-Handler + Tool-Buttons — siehe Stop-Liste 1+2.
- Foto-Workflow + LstEvents-Handler — Auswahl + Session-Commit, siehe
  Stop-Liste 4.
- Defect-Akzept/Edit/Reject + UpdateDefectDetailPanel — Session-Mutation.

Loop-Pause empfohlen, bis mindestens Punkt 4 (Session-State-Besitz)
entschieden ist.
