# Phase 5.4 — Produktmodus / Expertenmodus (Inventar + Empfehlung)

**Datum:** 2026-05-04
**Auftrag:** "Programm wirkt schlanker. Hauptnavigation reduzieren. KI-Labor hinter Expertenmodus." — Audit A5 (Konsens 3/3, ~3 Tage geplant).
**Resultat:** Inventar + Migrationsplan. KEIN Code-Eingriff jetzt.

---

## A. Bestand

**Bereits vorhanden (Phase 1.4):**
- `AppSettings.ShowExpertenmodusFeatures` (Bool, default `true`)
- Wirkt aktuell auf:
  - **Eigendevis-NavItem** in `ShellViewModel.NavItems` (verborgen wenn off)
  - **Hydraulik-Toolbar-Buttons** in `DataPage.xaml`
- Settings-Page hat Checkbox dafuer

**Verbleibende Expertenmodus-Kandidaten (was im Default-Modus weg sollte):**

### Hauptnavigation (`ShellViewModel.NavItems`)
- `Diagnose` → Expertenmodus (Log-Tail ist Entwickler-Werkzeug)
- `Medienkonflikte` → moeglich (User mit kleinen Projekten brauchen das selten)
- `VSA` → bleibt im Default (Pflicht-Workflow nach Codierung)

### Window-Aufrufe / Klick-Pfade
- `TrainingCenterWindow` (Werkzeuge → KI-Labor)
- `BenchmarkWindow`
- `VideoAnalysisPipelineWindow` (KI-Pipeline-Test)
- `VsaCodeExplorerWindow` (Debug)
- `SanierungRulesWindow` (KI-Regeln)
- `OptionsEditorDialog` (Editor fuer Dropdown-Optionen)
- `MeasureTemplateEditorWindow`, `PositionTemplateEditorDialog` (Template-Editoren)
- `PriceCatalogEditorWindow`, `CostCatalogEditorDialog` (Preis/Kosten-Editoren)

### MainWindow-Statusleiste / Hardware-Monitor
- `SystemMonitorService` GPU/CPU/RAM-Anzeige in `MainWindow.xaml.cs`
- VRAM-Monitor
- Modell-Status-Anzeige

### Toolbar-Items in Pages
- DataPage: Hydraulik-Buttons (✅ schon migriert in Phase 1.4)
- DataPage: KI-Sanierungs-Optimization-Button (Expertenmodus-Kandidat)
- DataPage: Video-Analyse-Pipeline-Start (Expertenmodus-Kandidat)
- TrainingCenter-Tabs (Few-Shot, Yolo-Retrain, Teacher, etc.)

---

## B. Vorgeschlagene Aufteilung

### Produktmodus (default)
- Uebersicht, Projekt, Haltungen, Schaechte
- Import, Export
- Druckcenter
- VSA
- Einstellungen
- Statusleiste minimal: nur "Bereit / Speichern / Geladen ..."

### Expertenmodus (`ShowExpertenmodusFeatures = true`)
- Alle Default-Items
- Eigendevis (bereits)
- Diagnose
- Medienkonflikte (optional)
- TrainingCenter / Benchmark / VideoAnalysis (Werkzeuge-Menu)
- Hydraulik (bereits)
- Hardware-Monitor in Statusleiste
- Modell-Status-Anzeige

---

## C. Risiken einer Big-Bang-Migration

1. **NavItems-Filter:** Bei Aenderung der Sichtbarkeit muss MainWindow die ausgewaehlte Page neu setzen, sonst bleibt eine versteckte Page als CurrentPage stehen.
2. **Window-Aufrufe in CodeBehind:** `TrainingCenterWindow` wird nicht nur via NavItem geoeffnet, sondern auch ueber `Werkzeuge`-Menu in MainWindow. Beide Pfade muessen filtern.
3. **Statusleiste-Bindings:** Hardware-Monitor laeuft im Hintergrund (CPU-Polling). Ausblenden im Default reicht nicht — Service sollte gar nicht gestartet werden, wenn Default-Modus.
4. **User-Verwirrung:** Wer von einem alten Build mit allen Items kommt, vermisst sie ploetzlich. UI-Texte muessen erklaeren wo Expertenmodus an-/aus geht.

---

## D. Empfohlener gestaffelter Pfad

### Sub-Phase 5.4.A: Hauptnavigation (~30 min)
- `ShellViewModel.NavItems`: Diagnose nur im Expertenmodus.
- Settings-Page: Checkbox-Tooltip erweitern um "Diagnose, KI-Werkzeuge, Hardware-Monitor".

### Sub-Phase 5.4.B: Werkzeuge-Menu (~1 h)
- MainWindow `Werkzeuge` (oder File-Menu): `TrainingCenter`, `Benchmark`, `VideoAnalysisPipeline` nur sichtbar bei Expertenmodus.
- `MenuItem.Visibility="{Binding ShowExpertenmodus, Converter={StaticResource BoolToVis}}"`

### Sub-Phase 5.4.C: Statusleiste / Hardware-Monitor (~2 h)
- `SystemMonitorService.Start()` nur wenn Expertenmodus oder Diagnose-Setting aktiv.
- Statusleiste: GPU/RAM/CPU-Bloecke ueber Visibility-Binding.

### Sub-Phase 5.4.D: Toolbar-Items in Pages (~2-3 h)
- DataPage: KI-Sanierungs-Optimization, VideoAnalysis-Buttons mit Visibility.
- TrainingCenterWindow: bestimmte Tabs (FewShot, Yolo-Retrain, Teacher) nur bei Expertenmodus.

### Sub-Phase 5.4.E: Live-Test + Doku (~1 h)
- Beide Modi durchklicken, screenshots.
- Dokumentation in Settings: Expertenmodus-Hinweis.

**Total:** ~6-7 h, statt 3 Tage geplant.

---

## E. Was bewusst NICHT in der Phase 5.4

- **Komplette UI-Ueberarbeitung** (Phase 6.4 — eigene Mehr-Wochen-Phase)
- **KI-Schicht aus UI ziehen** (Phase 5.3 — ServiceProvider-Aenderung)
- **Bestehende Window-Klassen umbauen** — Visibility-Filter genuegen

---

## F. Akzeptanz

- Phase 1.4 Toggle steht: `ShowExpertenmodusFeatures` mit Default `true`.
- Eigendevis und Hydraulik bereits abhaengig vom Toggle.
- Verbleibend: Diagnose, KI-Werkzeuge, Hardware-Monitor, Editoren.
- ⏸️ Migration in 5 Sub-Phasen empfohlen, jede live-testbar.
- KEIN Code-Eingriff in dieser Iteration.
