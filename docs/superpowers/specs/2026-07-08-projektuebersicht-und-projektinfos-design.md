# Projektübersicht-Vorschau & Projekt-Infos-Straffung — Design

**Datum:** 2026-07-08
**Status:** Freigegeben (Design), bereit für Umsetzungsplan

## Ziel
Zwei UI-Verbesserungen im SewerStudio-Launcher:
1. **Projektübersicht (OverviewPage):** Einfachklick auf ein Projekt links zeigt rechts eine
   **Projekt-Vorschau** (professionelle Projektinfoseite inkl. Gesamtmeterzahl); Doppelklick öffnet.
   Schadensgruppen entfallen.
2. **Projekt-Infos-Formular (ProjectPage):** Block „Firma & Kontakt" entfernen; Auftraggeber bei neuen
   Projekten mit „Abwasser Uri" vorbelegen.

## Global Constraints (verbindlich, für jede Task)
- **Thin-AI / Schichten:** Geschäftslogik in C#, UI ruft ViewModel/Service. Vorschau-Aufbau als eigener
  reiner Helfer, nicht in Code-behind.
- **Additiv:** Kein großes Refactoring am Bestand. Neue Logik in neuen fokussierten Dateien.
- **Positional Records** additiv erweitern und mit **benannten Argumenten** konstruieren.
- **Deutsche Kommentare.**
- **Fokussierter Test** für Kernlogik (ProjectPreviewFactory, Auftraggeber-Default).
- **Commits:** ~68 unzusammenhängende uncommittete Dateien im Working-Tree — jede Task staged NUR ihre
  eigenen Änderungen (kein `git add -A`). Bei bereits „dirty" Dateien nur die eigenen Hunks stagen.

## Bestätigte Entscheidungen
- Einfachklick = Vorschau, Doppelklick = öffnen (Doppelklick funktioniert bereits).
- Rechte Projektseite zeigt: Kopf, Stammdaten, Kennzahl-Kacheln (inkl. Gesamtmeter) und **beide** Balken
  (Zustandsklassen + DN/Kosten). **Schadensgruppen raus.**
- Formular: kompletter Block „Firma & Kontakt" (Adresse/Telefon/E-Mail) raus. Feld „Firma" unter
  Projektdaten **bleibt** (= ausführende Inspektionsfirma). Rest wie im vom Nutzer bestätigten Foto.
- Auftraggeber-Default „Abwasser Uri" nur bei **neuen** Projekten (Draft), frei änderbar; bestehende
  Projekte unberührt.

## Ist-Zustand (relevante Fundstellen)
- `src/AuswertungPro.Next.UI/Views/Pages/OverviewPage.xaml` — rechtes Panel bindet an `Project`
  (= `_shell.Project`, das GEÖFFNETE Projekt) und `Dashboard` (aus `Project.Data`). Deshalb schaltet ein
  Listenklick rechts nichts um. Liste: `ListBox` mit `SelectedItem={Binding SelectedProjectEntry}`,
  `MouseDoubleClick=ProjectListBox_MouseDoubleClick`. Schadensgruppen-Spalte: Z. 336–369.
- `src/AuswertungPro.Next.UI/ViewModels/Pages/OverviewPageViewModel.cs` — `SelectedProjectEntry`
  (`ProjectOverviewEntry`: Name, Description, Path, ModifiedAtUtc, RecordCount). `Project`/`Dashboard` sind
  Getter aufs Shell-Projekt. `OnSelectedProjectEntryChanged` aktualisiert nur Command-CanExecute.
- `src/AuswertungPro.Next.UI/Views/Pages/ProjectPage.xaml` — Formular; Metadaten via
  `Project.Metadata[Key]`. Block „Firma & Kontakt": Z. 94–100 (`FirmaAdresse/FirmaTelefon/FirmaEmail`).
  Feld „Firma" (`FirmaName`): Z. 88–89 (bleibt).
- `src/AuswertungPro.Next.UI/ViewModels/ShellViewModel.cs` — `StartNewProjectDraft()` (Z. 337) erzeugt
  `new Project { Name = string.Empty }`.
- `src/AuswertungPro.Next.Application/Dashboard/DashboardStatisticsBuilder.cs` —
  `DashboardStatistics(int TotalHoldings, double TotalLengthMeters, decimal TotalCost,
  IReadOnlyList<DashboardBucket> ConditionClasses, IReadOnlyList<DashboardBucket> DamageGroups,
  IReadOnlyList<DashboardCostBucket> DnCostGroups)`; `Build(IEnumerable<HaltungRecord>?)`.
  `DashboardBucket(string Label, int Count, double Percent)`;
  `DashboardCostBucket(string Label, int Count, decimal Cost, double Percent)`.
- Projekt laden: `_sp.Projects.Load(path)` → `Result<Project>` mit `.Ok`, `.Value`, `.ErrorMessage`.

## Architektur (dünn, additiv)

### ProjectPreview (neues, reines Modell)
`src/AuswertungPro.Next.Application/Dashboard/ProjectPreview.cs` — trägt genau die Anzeige-Daten:
```csharp
public sealed record ProjectPreview(
    string Name,
    string Description,
    string Path,
    DateTime? ModifiedAtUtc,
    string? AppVersion,
    int HoldingCount,
    double TotalLengthMeters,
    decimal TotalCost,
    string Auftraggeber,
    string Gemeinde,
    string Zone,
    string Strasse,
    string Bearbeiter,
    string Inspektionsdatum,
    string AuftragNr,
    string Firma,
    IReadOnlyList<DashboardBucket> ConditionClasses,
    IReadOnlyList<DashboardCostBucket> DnCostGroups)
{
    public bool HasHoldings => HoldingCount > 0;
    public string ModifiedAtDisplay =>
        ModifiedAtUtc?.ToLocalTime().ToString("dd.MM.yyyy", CultureInfo.CurrentCulture) ?? "—";
}
```

### ProjectPreviewFactory (neuer, reiner, testbarer Helfer)
`src/AuswertungPro.Next.Application/Dashboard/ProjectPreviewFactory.cs`
- `static ProjectPreview FromProject(Project project, string path)` — liest `project.Metadata` (fehlende
  Keys → ""), baut `DashboardStatistics` via `DashboardStatisticsBuilder.Build(project.Data)`, mappt
  `TotalHoldings/TotalLengthMeters/TotalCost/ConditionClasses/DnCostGroups`. Schadensgruppen werden NICHT
  übernommen.
- Metadaten-Keys: `Auftraggeber, Gemeinde, Zone, Strasse, Bearbeiter, InspektionsDatum, AuftragNr,
  FirmaName`. Kleiner privater Helfer `Meta(project, key)` → Wert oder "".

### OverviewPageViewModel
- Neue Eigenschaft `[ObservableProperty] private ProjectPreview? _selectedPreview;`
- In `OnSelectedProjectEntryChanged(value)`: `BuildPreview(value)` aufrufen.
- `BuildPreview(ProjectOverviewEntry? entry)`:
  - entry null → `SelectedPreview = null`.
  - sonst `_sp.Projects.Load(entry.Path)`; bei `Ok && Value != null` →
    `SelectedPreview = ProjectPreviewFactory.FromProject(res.Value, entry.Path)`.
  - bei Fehler → **Fallback** aus den Listen-Metadaten:
    `ProjectPreview` mit Name/Path/ModifiedAt/HoldingCount aus `entry` (RecordCount), Rest leer/0, leere
    Bucket-Listen — kein Absturz, Panel bleibt nutzbar.
  - in `try/catch` (Load kann werfen).
- Nach `LoadAllProjects()`/`ApplyFilter()`: wenn `SelectedProjectEntry == null`, das **erste** Element in
  `ProjectEntries` vorselektieren (zeigt beim Start das zuletzt verwendete Projekt als Vorschau).

### OverviewPage.xaml (rechtes Panel neu)
- Bindungen von `Project`/`Dashboard` auf `SelectedPreview.*` umstellen.
- Kopf: `SelectedPreview.Name`, `SelectedPreview.Path`, `SelectedPreview.Description`; Status-Badge fix
  „Projekt gespeichert" (Datei-Stand).
- Kennzahl-Kacheln (4): `HoldingCount` „Haltungen", `TotalLengthMeters` (`{0:N0} m`) „Meter",
  `TotalCost` (`{0:N0} CHF`) „Kosten", `ModifiedAtDisplay` „gespeichert".
- Neuer **Stammdaten-Block** (2-spaltiges Raster, `TextSecondaryBrush`-Labels): Auftraggeber, Gemeinde,
  Zone, Straße, Bearbeiter, Inspektionsdatum, Auftrag-Nr., Firma.
- Balken-Bereich „Auswertung": 2 Spalten — Zustandsklassen (`ConditionClasses`) + DN/Kosten
  (`DnCostGroups`). **Schadensgruppen-Spalte (Z. 336–369) entfernen.**
- Drag&Drop-Hinweis + Öffnen bleiben. `null`-Preview: Panel zeigt neutralen Leerzustand (Bindings auf
  null sind unkritisch; Kacheln zeigen 0/—).

### ProjectPage.xaml
- Entfernen: `TextBlock "Firma & Kontakt"` + die drei Label/TextBox-Paare Adresse/Telefon/E-Mail
  (`FirmaAdresse/FirmaTelefon/FirmaEmail`), Z. 94–100. Records-Zeile bleibt.

### ShellViewModel.StartNewProjectDraft
- Vor `ReplaceProject`: Draft-Projekt anlegen und
  `draft.Metadata["Auftraggeber"] = "Abwasser Uri"` setzen (nur Neuanlage). Bestehende Projekte werden nie
  überschrieben.

## Tests
- `ProjectPreviewFactoryTests`:
  - `FromProject` mappt Haltungen/Gesamtmeter/Kosten und die Metadaten-Felder; fehlende Metadaten → "".
  - Schadensgruppen sind nicht Teil des Previews (nur ConditionClasses + DnCostGroups vorhanden).
- `ShellViewModel`/Draft-Test: nach `StartNewProjectDraft()` ist `Project.Metadata["Auftraggeber"]` ==
  „Abwasser Uri". (Falls ShellViewModel im Test schwer instanziierbar: Default in kleinen Helfer
  `NewProjectDraftFactory` auslagern und diesen testen.)

## Nicht im Scope
- „Import starten"-Button (falsch verdrahtet auf ContinueCommand) bleibt unverändert.
- Kein Async-Laden: Projekte sind klein (≤ ~100 Haltungen), synchrones Laden bei Auswahl genügt.
- Live-Dirty-Status in der Vorschau (Vorschau = Datei-Stand).
