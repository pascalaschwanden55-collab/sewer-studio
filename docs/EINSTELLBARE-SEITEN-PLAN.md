# Einstellbare Seiten & Fenster — Umsetzungsplan (für Codex)

**Erstellt 2026-07-04 von Fable. Methode:** Multi-Agenten-Workflow (6 Erkunder → 3 Architektur-Entwürfe → Synthese, ~1,07 Mio. Tokens), alle Fundstellen mit Datei:Zeile belegt; die drei tragendsten Fakten separat nachverifiziert. Self-contained, direkt übergabefähig.

## Was der Nutzer will

Jede Seite/jedes Fenster soll **einstellbar** sein, in vier Dimensionen (mit dem User geklärt, 2026-07-04):
1. **Spalten** — welche Spalten sichtbar, Reihenfolge, Breite; pro Tabelle dauerhaft gemerkt.
2. **Layout & Panels** — Panelgrößen per Maus ziehen, Bereiche ein-/ausklappen/abkoppeln, Fenster-Position/-Größe merken.
3. **Schrift & Dichte** — Zoom, Zeilenhöhe, kompakt/luftig pro Seite.
4. **Gespeicherte Ansichten** — Filter + Spalten + Sortierung als benannte Ansicht speichern und laden.

Umfang: **ein zentrales, wiederverwendbares System für alle Seiten** (nicht pro Seite neu). Pilot: **Druckcenter (BuilderPage)**.

## Kernbefund der Erkundung (warum das machbar ist)

**80 % existieren bereits.** DataPage und SchaechtePage haben ein produktives, wiederverwendbares Spalten-Layout-System:
- `DataGridColumnLayoutController` ([Views/Pages/DataGridColumnLayoutController.cs:10](src/AuswertungPro.Next.UI/Views/Pages/DataGridColumnLayoutController.cs)) — speichert Breite/Reihenfolge/Ausrichtung pro Feld (`Tag = FeldName`), mit `IsRestoring`-Guard und `try/catch` gegen transiente `DisplayIndex`-Exceptions. **Verifiziert: kein Visibility/Show-Hide** — genau das ist die einzige echte Feature-Lücke im ganzen Projekt.
- `DataPageGridLayoutController` — Zoom (0.5–2.0) + Zeilenhöhe (24–240), UI-entkoppelt, clampt/persistiert.
- Persistenz-Vorbild: `AppSettings.WindowStates` = `Dictionary<string, WindowBounds>` ([AppSettings.cs:87](src/AuswertungPro.Next.UI/AppSettings.cs#L87)) — erbt atomares Schreiben, Debounce, Restore-Points, Test-Isolation.
- Aktivierungs-Vorbild: Attached Behaviors wie `PhotoHoverPreviewBehavior` (idempotenter Loaded-Attach + Unloaded-Cleanup).
- `GridSplitter` ist bereits an 7 Stellen im Einsatz (u. a. HaltungsansichtView, TrainingCenterWindow) — Panel-Resize-Muster im Haus.
- Pilot-Vorteile Druckcenter: leeres Code-Behind (risikofreie Startfläche), 12 feste Spalten, und der Filterzustand ist bereits ein sauberer record `BuilderPageFilterCriteria` ([BuilderPageRowFilter.cs:7](src/AuswertungPro.Next.UI/ViewModels/Pages/BuilderPageRowFilter.cs#L7)) = fertiger serialisierbarer Kern für Dimension 4.

**Ist-Zustand Grids:** 22 DataGrids in 19 Dateien, alle `AutoGenerateColumns=False`. 20 mit fest im XAML verdrahteten Spalten ohne jede Persistenz; nur DataPage/SchaechtePage mit dem obigen System. Gemeinsame Basis = impliziter DataGrid-Style in beiden Themes.

## Empfohlener Ansatz

**Hybrid, pragmatisch.** Rückgrat = maximale Wiederverwendung + strikt inkrementelle Dimensions-Reihenfolge. Aktivierung = **vererbte `ViewKey`-Attached-Property** am Seiten-/Fenster-Root als zentraler Hook (kein ViewModel-Umbau, rein XAML).

**Bewusst verworfen:** eine verpflichtende `CustomizableViewModelBase`-Basisklasse — alle Seiten-VMs entstehen per Factory-Lambda in `ShellViewModel.cs`, es gibt keine gemeinsame Basis; eine erzwungene Basisklasse wäre genau das von CLAUDE.md untersagte große Refactoring. Filter-Capture (Dim 4) läuft stattdessen über ein schmales opt-in-Interface nur dort, wo nötig.

## Persistenz (eine Entscheidung, klar)

**Ein neues Property in AppSettings.cs**, kein eigenes File:
```csharp
public Dictionary<string, ViewCustomization> ViewCustomizations { get; set; } = new();
// null-Guard in NormalizeAfterLoad: settings.ViewCustomizations ??= new();  (Vorbild WindowStates, AppSettings.cs:249)
```
Je `ViewKey` (z. B. `"BuilderPage"`):
```csharp
sealed class ViewCustomization {
  Dictionary<string, DataPageLayoutSettings> Grids;   // Key = GridKey — trägt Spalten (inkl. neuem IsVisible) UND Zoom/Dichte
  Dictionary<string, double> SplitterSizes;           // Key = SplitterKey
  List<SavedView> SavedViews;
}
```
`DataPageLayoutSettings` ([AppSettings.cs:311](src/AuswertungPro.Next.UI/AppSettings.cs)) wird 1:1 wiederverwendet → Dim1 (Spalten) und Dim3 (Zoom/Dichte) teilen sich EIN Objekt, kein Doppelzustand.

**Warum kein eigenes File:** Die gesamte gehärtete Kette — atomares `File.Replace`+`.bak`+3× Retry, 750 ms-Debounce, `FlushPendingSave` beim Shutdown, Restore-Points, Korruptions-Quarantäne, Legacy-Migration UND die Test-Isolation über `SEWERSTUDIO_APPDATA_DIR` — hängt an genau dieser settings.json. Ein Dictionary erbt alles ohne eine Zeile Persistenz-Code; ein eigenes `view_settings.json` müsste das alles duplizieren = Wiederaufleben des früheren Datenverlust-Bugs.

### Harte Regeln für die Persistenz (Codex beachten)
1. Zugriff **ausschließlich** über `ServiceProvider.Settings` + den neuen `ViewCustomizationStore`. **NIE** `Environment.SpecialFolder` direkt, **NIE** eine eigene `AppSettings.Load()`-Instanz — das Anti-Muster in [PhotoGalleryPanel.xaml.cs:35](src/AuswertungPro.Next.UI/Controls/PhotoGalleryPanel.xaml.cs#L35) (`_settings = AppSettings.Load();`) **NICHT kopieren**, es umgeht die Test-Isolation und reproduziert genau den Datenverlust-Bug.
2. Für laufende Tweaks (Spalten-Drag, Splitter-Ziehen, Slider) **nur** `Save()` (debounced), **nie** `SaveImmediate()` — sonst erzeugt jeder Ruck einen Full-File-Restore-Point.
3. Bestehende `Settings.DataPageLayout`/`SchaechtePageLayout` **unangetastet** lassen (CLAUDE.md: DataPage/SchaechtePage nur nach Absprache). Das neue System läuft **parallel** über das Dictionary.

## Architektur (neue + berührte Bausteine)

| Komponente | Datei | Verantwortung |
|---|---|---|
| `ViewCustomizations`-Dictionary | AppSettings.cs (~Z.88 + Guard ~Z.249) | Storage-Anker, keyed by ViewKey; erbt gesamte Persistenz-Härtung |
| `ViewCustomization` + `SavedView` DTOs | AppSettings.cs (~Z.311, bei den Layout-DTOs) | Tolerante Container (nur nullable/defaultbar → unbekannte Keys werfen nie) |
| `IsVisible`-Flag | AppSettings.cs (`DataPageColumnLayout`) | `bool IsVisible = true` — schließt die Show/Hide-Lücke, Default true = verhaltensneutral |
| `ViewCustomizationStore` (static) | Services/ViewCustomizationStore.cs **(neu)** | Einziger Zugriffsweg: `Configure(settings)` beim Start, `GetOrCreate(viewKey)`, `Save()` (debounced) |
| `DataGridColumnLayoutController` (+Show/Hide) | Views/Pages/DataGridColumnLayoutController.cs | `Restore()` setzt zusätzlich `column.Visibility` aus IsVisible; `Capture()` liest sie — minimal-invasiv, Guards behalten |
| `ViewKey` (vererbte Attached Property) | Behaviors/ViewCustomization.cs **(neu)** | `FrameworkPropertyMetadataOptions.Inherits`, EINMAL am Root gesetzt — der zentrale Ausroll-Hook |
| `GridPersonalizationBehavior` | Behaviors/GridPersonalizationBehavior.cs **(neu)** | Brücke Grid↔Controller↔Store nach PhotoHoverPreview-Muster (idempotenter Attach, Unloaded-Cleanup) |
| `ColumnChooserMenu` | Controls/ColumnChooserMenu.cs **(neu)** | Einzige echt neue UI: Checkbox-Liste (IsVisible) + Reset, Baumuster `DataGridColumnAlignmentToolbar`. Reorder bleibt nativ |
| `SplitterPersistenceBehavior` | Behaviors/SplitterPersistenceBehavior.cs **(neu, Dim2)** | Generalisiert `SchadenSplitter_DragCompleted` (HaltungsansichtView.xaml.cs:63): DragCompleted→clamp→Save, Loaded→Restore |
| `WindowStateManager` (StateKey-Fix) | Services/WindowStateManager.cs | Optionaler expliziter `StateKey` statt `GetType().Name` (behebt „Window"-Sammel-Kollision der Detach-Fenster) + Clamping in sichtbaren Arbeitsbereich; Default weiter Typname (Altkeys schonen) |
| `SavedViewsBar` | Controls/SavedViewsBar.cs **(neu, Dim4)** | ComboBox + Speichern/Löschen/Umbenennen; aggregiert Spalten-Snapshot + Sort + Filter |
| `ISavedViewFilterProvider` | Behaviors/ISavedViewFilterProvider.cs **(neu, Dim4)** | `CaptureFilterState()`/`ApplyFilterState()` — der einzige Punkt, den ein reines Behavior nicht erreicht; opt-in pro VM |

## Ausrollung: Pakete (jedes einzeln baubar, mergefähig, Tests grün)

**Dimensions-Reihenfolge nach Aufwand/Nutzen:** Dim1 Spalten → Dim3 Schrift/Dichte → Dim2 Layout → Dim4 Ansichten.
**Seiten-Reihenfolge:** BuilderPage (Pilot) → SanierungsMatrixPage + TrainingCenterWindow → übrige. DataPage/SchaechtePage bewusst aussparen.

### P0 — Fundament (keine sichtbare UI-Änderung) · 1–2 Sessions
`ViewCustomizations`-Dictionary + DTOs + null-Guard in `NormalizeAfterLoad`. `IsVisible=true` zu `DataPageColumnLayout`. `DataGridColumnLayoutController.Restore/Capture` um `column.Visibility` erweitern (Default true = verhaltensneutral; **Guard: Capture erzwingt mind. 1 sichtbare Spalte, nie all-hidden persistieren**). `ViewCustomizationStore` (static) + `Configure`-Aufruf in App.xaml.cs neben `WindowStateManager.Configure`. `ViewKey`-Attached-Property (Inherits).
**Dateien:** AppSettings.cs; DataGridColumnLayoutController.cs; Services/ViewCustomizationStore.cs (neu); Behaviors/ViewCustomization.cs (neu); App.xaml.cs.
**Test:** IsVisible-Roundtrip Capture→Restore; Capture erzwingt ≥1 sichtbare Spalte; Store keyed korrekt; null-Guard. **Bestehende `DataGridColumnLayoutControllerTests` müssen unverändert grün bleiben** (Beweis Verhaltensneutralität).

### P1 — Pilot Dim1: Spalten auf BuilderPage · 0,5–1 Session
12 `DataGridTextColumn` je `Tag=FieldName` geben. BuilderPage.xaml.cs analog DataPage.xaml.cs: Controller-Feld, Loaded→Restore, LayoutChanged→debounced Save, Unloaded→Capture+Save. `GridPersonalizationBehavior` als wiederverwendbare Verdrahtung. `ColumnChooser`-Button in die Kopf-Card neben „Filter zurücksetzen". `ViewKey="BuilderPage"` am Root. BuilderPageViewModel: public `Services`/`Settings`-Property ergänzen (heute nur privat `_sp`).
**Test:** Attach idempotent (mehrfach Loaded); Capture/Restore über Temp-isolierten Store; Chooser toggelt IsVisible. Smoke: Spalte ausblenden/verbreitern/verschieben → App neu → bleibt.

### P2 — Pilot Dim3: Zoom/Dichte auf BuilderPage · 0,5 Session
`DataPageGridLayoutController` auf das Builder-Grid anwenden: 2 Slider im Grid-Kontextmenü (Zeilenhöhe, Zoom, Bindungsmuster wie DataPage.xaml:261), MinRowHeight-Binding + `LayoutTransform` `ScaleTransform`. **Scale STRIKT auf den Grid, nie auf den Seiten-Root** (sonst Blur/Hit-Test-Bruch). Nutzt `GridMinRowHeight`/`GridZoom` im selben Settings-Objekt aus P1.
**Test:** Clamp-Tests (existieren sinngemäß); Persist schreibt ins Dictionary. Smoke: Slider → bleibt nach Neustart.

### P3 — Dim2 billig: Splitter-Persistenz · 0,5 Session
`SplitterPersistenceBehavior` (generalisiert `SchadenSplitter_DragCompleted`). Auf BuilderPage die feste 420px-Statistik-Spalte ([BuilderPage.xaml:126](src/AuswertungPro.Next.UI/Views/Pages/BuilderPage.xaml)) per GridSplitter trennbar machen. Danach die flüchtigen Liste↔Detail-Splitter in Haltungsansicht/Schachtansicht.
**Test:** clamp+roundtrip über isolierten Store. Smoke: Panel ziehen → bleibt.

### P4 — Breite: Dim1+Dim3 auf weitere Grids · 0,25–0,5 Session je Grid
Je Grid nur `Tag=FieldName` + `GridPersonalizationBehavior` + `ViewKey` am Root. Reihenfolge nach Dichte: SanierungsMatrixPage, TrainingCenterWindow, MediaConflictsPage, BeobachtungenWindow, MediaSearchWindow, ProtocolObservationsWindow, Katalog-/Editor-Fenster. **DataPage/SchaechtePage auslassen.**
**Test:** je Grid eindeutige Tag-Keys (kein Duplikat/leer); Smoke stichprobenartig; UI-Suite je Stufe grün.

### P5 — Dim2 Fenster: WindowStateManager StateKey + Multi-Monitor-Clamping · 0,5 Session
Optionaler `StateKey`-Parameter zu `Track()` (Default weiter `GetType().Name` → Altkeys unverändert). Detach-Fenster (PhotoGalleryPanel/SystemMonitorPanel/HaltungSchadensband) eigene StateKeys → löst „Window"-Kollision. `IsVisibleOnAnyScreen` von Mittelpunkt-Prüfung auf Clamping in den Arbeitsbereich erweitern.
**Test:** getrennte StateKeys persistiert; Clamping schiebt Off-Screen zurück; Altverhalten ohne StateKey unverändert.

### P6 — Dim4 Capstone: gespeicherte Ansichten auf BuilderPage · 1–1,5 Sessions
`SavedView = { Name, Filter (BuilderPageFilterCriteria, 9 Felder), Spalten-Snapshot (inkl. Sichtbarkeit + Zoom/Dichte), SortFieldName+SortDirection }`. `SavedViewsBar` in die Kopf-Card. `ISavedViewFilterProvider` von BuilderPageViewModel implementieren: Capture liest die 9 ObservableProperties, Apply setzt sie unter dem bestehenden `_suspendFilterRefresh`-Guard (Muster `ResetFilters`) + `ApplyFilters`. Sortierung aus `DataGrid.Items.SortDescriptions` im Code-Behind. Spalten via `Controller.Restore`.
**Test:** Filter-Capture/Apply-Roundtrip (record-Gleichheit); Apply löst genau EIN `ApplyFilters` aus (Guard greift); Sort-Roundtrip. Smoke: Ansicht speichern/laden/löschen.

### P7 — Zurückgestellt/optional (nur nach Absprache) · 3–5 Sessions
`CollapsiblePanelBehavior` (Ein-/Ausklappen mit Persistenz); Generalisierung `FloatingGridWindow`+Docking auf die 3 Detach-Panels (Redock); globale Schriftgröße (ersetzt `App.xaml.cs ApplyTypographyDefaults`). Hoher Aufwand/Risiko; per-Grid-Zoom (P2) deckt 90 % des Schrift-Bedarfs.

## Risiken (im Plan adressiert)

- **All-hidden-Bug** (alle Spalten weg): Default `IsVisible=true` + Capture erzwingt ≥1 sichtbare Spalte — nie all-hidden persistieren.
- **Test-Isolation (wichtigstes Risiko):** jeder Zugriff über `ServiceProvider.Settings`/`ViewCustomizationStore`; nie eigene `AppSettings.Load()`-Instanz (PhotoGalleryPanel-Anti-Muster).
- **DataPage/SchaechtePage nicht brechen:** neues System läuft strikt parallel über das Dictionary; deren Code + `IsVisible`-Default halten Bestand byte-identisch; Bestandstests grün halten.
- **DataGrid-Interna:** `Visibility.Collapsed` + Reorder + `FrozenColumnCount` können transiente `DisplayIndex`-Exceptions werfen — den vorhandenen `try/catch` im Controller **nicht** entfernen; bei Frozen-Grids gesondert prüfen.
- **Zoom-Scale** strikt auf den Grid, nie den Seiten-Root (Blur/Hit-Test/Virtualisierung).
- **Behavior-Lebenszyklus:** Loaded feuert bei Tab-Wechsel mehrfach → Attach idempotent (StateProperty-Guard) + Unloaded sauber abkoppeln (kein Leak).
- **WindowStateManager-Kollision:** ohne StateKey-Fix bleibt „Fensterposition merken" für Detach-Fenster unzuverlässig; Migration muss Altkeys schonen.
- **settings.json-Wachstum:** nur debounced `Save()`; Payload klein.
- **Spalten-Identität:** die 20 festen Grids brauchen je stabile, eindeutige `Tag=FieldName` — fehlende/doppelte Keys brechen Restore/Chooser lautlos, beim Ausrollen (P4) je Grid prüfen.
- **SavedViews-Scope-Creep:** Filter-Capture braucht ein VM-Interface (nicht rein XAML) — bewusst opt-in (nur BuilderPage), sonst breiter VM-Umbau.

## Teststrategie

Alle neue Logik in der UI-/Application-Test-Schicht, **strikt Test-isoliert** (`SEWERSTUDIO_APPDATA_DIR` auf Temp; `SettingsPersistenceIsolationTests` als Guard, dass die echte settings.json nie getroffen wird). Reine UI-Verdrahtung (Slider-Wirkung, Blur, Redock) per manuellem WPF-Smoke der Pilotseite. Nach JEDEM Paket: `dotnet build AuswertungPro.sln` + `dotnet test` (~3825 UI-Tests müssen grün bleiben). **Keine neuen NuGet-Pakete** (nur WPF + CommunityToolkit.Mvvm Bestand) — falls doch etwas fehlt: Rückfrage.

## Bewusst NICHT

- `CustomizableViewModelBase`/verpflichtende VM-Basisklasse (großes Refactoring, CLAUDE.md).
- Eigenes `view_settings.json` (dupliziert Persistenz-Härtung/Isolation).
- Generisches Detach+Redock der einfachen Panels (P7, riskant, niedriger Alltagsnutzen).
- Globale Schriftgröße (P7, per-Grid-Zoom reicht meist).
- Einheitliches Ein-/Ausklappen mit Persistenz (P7, kein akuter Bedarf).
- Migration von DataPage/SchaechtePage auf die gemeinsame Engine — nur nach Absprache; bis dahin laufen beide Systeme parallel.
- Enterprise-Overhead (MS DI, Docker, adaptives Gate) — Hebel bleibt pragmatische Wiederverwendung.

---
*Roh-Erkundung (22-Grid-Inventar, 3 Architektur-Entwürfe) im Workflow-Journal `wf_e4975dfc-582/journal.jsonl`, falls Detailtiefe gebraucht wird.*
