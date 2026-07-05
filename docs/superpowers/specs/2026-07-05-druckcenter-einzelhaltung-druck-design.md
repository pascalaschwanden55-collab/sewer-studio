# Druckcenter: Einzelhaltung drucken (Kostenblatt + Dossier) — Design

**Erstellt:** 2026-07-05 · **Seite:** Druckcenter = `BuilderPage` / `BuilderPageViewModel`

## Nutzerwunsch

Im Druckcenter sollen druckbar sein:
1. **Alle Haltungen** mit Kostenzusammenstellung (existiert bereits).
2. **Eine einzelne Haltung** — wahlweise als leichtes **Kostenblatt** oder als **volles Dossier**.

Auslösung der Einzelhaltung: **Rechtsklick auf die Zeile** in der Tabelle (mit dem User geklärt, 2026-07-05).

## Ist-Zustand (Wiederverwendung, nichts neu erfinden)

- `BuilderPageViewModel.ExportPdfAsync` ([BuilderPageViewModel.cs:164](../../src/AuswertungPro.Next.UI/ViewModels/Pages/BuilderPageViewModel.cs)) baut die Kostenzusammenstellung über die **gefilterten** Zeilen: `BuilderPageSummaryEntryBuilder.Build(rows, vatRate)` → `OfferPdfModelFactory.CreateCostSummary` → `cost_summary.sbnhtml`. **= „Alle drucken" ist fertig.**
- Jede Tabellenzeile ist ein `DruckcenterRowVm` und trägt die echte Haltung: `Record` (`HaltungRecord`) ([BuilderPageViewModel.cs:1308](../../src/AuswertungPro.Next.UI/ViewModels/Pages/BuilderPageViewModel.cs#L1308)).
- Das volle Dossier existiert komplett: `DataPagePrintController.PrintDossierPdfAsync(project, record)` ([DataPagePrintController.cs:129](../../src/AuswertungPro.Next.UI/DataPage/DataPagePrintController.cs#L129)) — inkl. Auswahl-Dialog (`DossierPrintDialog`), Schacht-/Hydraulik-/Kosten-/Original-PDF-Auflösung. Der Controller ist bewusst mit injizierbaren `Func`-Providern + realen Defaults gebaut und getestet.
- Schacht-Lookup ist trivial nachbaubar: `_shell.Project.SchaechteData.FirstOrDefault(s => s.GetFieldValue("Schachtnummer") == nr)` (analog `DataPageViewModel.FindSchachtByNummer`, [DataPageViewModel.cs:932](../../src/AuswertungPro.Next.UI/ViewModels/Pages/DataPageViewModel.cs#L932)).

## Entwurf

### UX
- **„PDF exportieren"** bleibt = alle (gefilterten) Haltungen; Beschriftung/Tooltip so, dass der Umfang klar ist.
- **Rechtsklick** auf eine Tabellenzeile wählt die Zeile unter dem Cursor aus (Muster `HaltungsansichtView.HaltungList_PreviewMouseRightButtonDown`) und öffnet ein Kontextmenü mit zwei Einträgen:
  - **„Kostenblatt (diese Haltung)"**
  - **„Volles Dossier (diese Haltung)"**
- Die bestehenden Kontextmenü-Einträge des Grids (Zeilenhöhe/Zoom-Slider) bleiben; die beiden neuen Aktionen stehen oben, getrennt durch einen `Separator`.

### Technik
- `BuilderPageViewModel`:
  - Neu: `SelectedRow` (Binding an `DataGrid.SelectedItem`).
  - Neu: `PrintSingleKostenblattCommand` → ruft den bestehenden Kosten-PDF-Kern mit **einer** Zeile auf.
    → Dafür den PDF-Baukern aus `ExportPdfAsync` in eine private Methode `BuildCostSummaryPdfAsync(IReadOnlyList<DruckcenterRowVm> rows, string variantTitle)` ausklammern; „Alle" und „Einzel" rufen denselben Kern (Einzel-Variantentitel z. B. „Kostenzusammenstellung — Haltung <Name>").
  - Neu: `PrintSingleDossierCommand` → konstruiert einen `DataPagePrintController` mit denselben Providern wie DataPage (aus `_sp`/`_shell`) und ruft `PrintDossierPdfAsync(_shell.Project, SelectedRow.Record)`.
  - Guard: ohne Auswahl freundlicher Hinweis „Bitte zuerst eine Haltung in der Tabelle wählen." (der Dossier-Controller hat diesen Guard schon).
- `BuilderPage.xaml`: `SelectedItem="{Binding SelectedRow}"`, `PreviewMouseRightButtonDown`-Handler (wählt Zeile), zwei `MenuItem` im vorhandenen `DataGrid.ContextMenu`.

### Controller-Aufbau: bewusste Duplizierung statt DataPage-Umbau
Der `DataPagePrintController` wird im `BuilderPageViewModel` frisch aufgebaut (dieselben ~6 Provider wie [DataPageViewModel.cs:150](../../src/AuswertungPro.Next.UI/ViewModels/Pages/DataPageViewModel.cs#L150)). Bewusst **keine** gemeinsame Factory, weil das DataPage anfassen würde (CLAUDE.md: DataPage nur nach Absprache). Die ~10 Zeilen Wiring-Duplizierung sind der akzeptierte Preis; spätere Extraktion in eine `DossierPrintControllerFactory` bleibt möglich.

## Betroffene Dateien
- `src/.../ViewModels/Pages/BuilderPageViewModel.cs` — `SelectedRow`, 2 Commands, `BuildCostSummaryPdfAsync`-Ausklammerung, Controller-Aufbau, Schacht-Lookup.
- `src/.../Views/Pages/BuilderPage.xaml` — SelectedItem-Binding, Rechtsklick-Select, 2 Kontextmenü-Einträge.
- `tests/AuswertungPro.Next.UI.Tests/…` — neue Tests (siehe unten).

## Tests
- Kosten-Kern erzeugt für **eine** Zeile eine gültige Modellstruktur (Variantentitel enthält den Haltungsnamen; Summe = Netto der einen Haltung).
- „Alle" und „Einzel" nutzen denselben Kern (kein Verhaltensbruch der bestehenden `ExportPdf`-Logik — Bestandstests grün).
- Dossier-Command ohne Auswahl → Hinweis, kein PDF-Build (der Controller-Guard greift; Test spiegelt `DataPagePrintControllerTests` sinngemäß).
- Nach jeder Änderung: `dotnet build AuswertungPro.sln` + `dotnet test` (UI-Suite grün).

## Bewusst NICHT (YAGNI)
- Kein neues Kostenblatt-Template — der bestehende `cost_summary.sbnhtml`-Ausdruck, auf eine Haltung begrenzt, ist das Kostenblatt.
- Kein DataPage-Refactoring, keine gemeinsame Basisklasse, keine neuen NuGet-Pakete.
- Nicht Teil dieses Specs: das große „einstellbare Seiten"-System ([EINSTELLBARE-SEITEN-PLAN.md](../../EINSTELLBARE-SEITEN-PLAN.md)).

## Offener Punkt: „ich sehe nicht alle Angaben" (Layout)
Getrennte, kleine Folgeaufgabe. Wahrscheinlich wird auf kleinem Fenster die untere Druck-Leiste (`BuilderPage.xaml` Grid-Row 4) abgeschnitten, weil die Seite keinen Gesamt-Scroll hat. Vor einem Fix mit dem User klären, **was genau** abgeschnitten war (Tabelle / untere Knöpfe / Filter).
