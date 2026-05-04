# Phase 1.4 — Inventar Hydraulik / Eigendevis

**Datum:** 2026-05-04
**Auftrag:** Analyse + sanfte UI-Entkopplung, kein Radikalschnitt.
**Status:** Inventar fertig. Empfehlungen pro Block.

---

## A. Hydraulik-Block

### A1. Was gibt es

| File | Zeilen | Schicht | Funktion |
|---|---:|---|---|
| `src/AuswertungPro.Next.UI/Hydraulik/HydraulikEngine.cs` | 234 | UI | Berechnungs-Engine DWA-A 110 (Kreisquerschnitt) |
| `src/AuswertungPro.Next.UI/ViewModels/Windows/HydraulikPanelViewModel.cs` | 294 | UI | Panel-VM mit Settings-Persistenz |
| `src/AuswertungPro.Next.UI/Views/Windows/HydraulikPanelWindow.xaml` | 485 | UI | Eingabe-Fenster |
| `src/AuswertungPro.Next.UI/Views/Windows/HydraulikPanelWindow.xaml.cs` | 273 | UI | CodeBehind |
| `src/AuswertungPro.Next.UI/Views/Windows/HydraulikPrintDialog.xaml` | ? | UI | Print-Dialog (laut Kommentar in `PrintOptionsDialog.xaml.cs:13` durch generischen Dialog ersetzt — Dead Code-Kandidat) |
| `src/AuswertungPro.Next.UI/Views/Windows/HydraulikPrintDialog.xaml.cs` | ? | UI | CodeBehind dito |
| `src/AuswertungPro.Next.Application/Reports/HydraulikPdfBuilder.cs` | 320 | Application | PDF-Erzeugung |
| `src/AuswertungPro.Next.Application/Reports/HydraulikPrintOptions.cs` | ? | Application | Optionen-DTO |
| `tests/AuswertungPro.Next.Pipeline.Tests/HydraulikEngineTests.cs` | ? | Tests | Vorhanden (gruen) |

**Gesamtumfang Code:** ~1.606 Zeilen + Tests. Settings: `HydraulikPanelSettings` in [AppSettings.cs:401](../../src/AuswertungPro.Next.UI/AppSettings.cs#L401).

### A2. Wo wird es ausgeloest

**Nicht im Hauptmenue.** Aufruf nur ueber zwei Toolbar-Buttons in [DataPage.xaml:250-265](../../src/AuswertungPro.Next.UI/Views/Pages/DataPage.xaml#L250-L265):

```
"Hydraulik"    → DataPageViewModel.OpenHydraulikCommand
"Hydraulik PDF" → DataPageViewModel.PrintHydraulikCommand
```

Beide Commands sind in [DataPageViewModel.cs:98-99,226-227,1879](../../src/AuswertungPro.Next.UI/ViewModels/Pages/DataPageViewModel.cs#L98) deklariert und an `HaltungRecord` gebunden (also pro-Haltung). Dazu ein dritter interner Aufruf in `DataPageViewModel.cs:2143`.

### A3. Lebt der Code (Dead-Code-Check)

- **Tests vorhanden** → mind. die Engine wird getestet.
- **PrintOptionsDialog.xaml.cs:13** sagt: *"Generischer Druckoptionen-Dialog — ersetzt DossierPrintDialog und HydraulikPrintDialog"* → `HydraulikPrintDialog` ist **vermutlich Dead Code**, sollte aber nicht ohne Verifikation geloescht werden, weil die `PrintHydraulikCommand`-Stelle es noch instanziiert (Z.2000).
- **HydraulikEngine** wird im laufenden Workflow nicht automatisch verwendet — nur auf Klick.

### A4. Empfehlung Hydraulik

**Sanfte Entkopplung mit Setting-Toggle (default sichtbar):**

1. Neuer AppSettings-Toggle `ShowExpertenmodusFeatures` (default = `true`, also keine Default-Verhaltens-Aenderung).
2. `HydraulikButton` und `HydraulikPrintButton` in `DataPage.xaml` an die Toggle-Property binden via `Visibility="{Binding ShowExpertenmodus, Converter=...}"`.
3. **Nichts loeschen** — Engine, Tests, PDF-Builder bleiben.

**Loeschen-Kandidat (NACH Verifikation, nicht jetzt):** `HydraulikPrintDialog.xaml/.cs` (laut Kommentar bereits durch `PrintOptionsDialog` abgeloest, aber noch instanziiert in `DataPageViewModel.cs:2000`). Echte Loeschung erst wenn man `DataPageViewModel.PrintHydraulikPdf` so umbaut, dass es `PrintOptionsDialog` benutzt.

---

## B. Eigendevis-Block

### B1. Was gibt es

| File | Zeilen | Schicht | Funktion |
|---|---:|---|---|
| `src/AuswertungPro.Next.UI/Views/Pages/EigendevisPage.xaml` | 171 | UI | Hauptpage |
| `src/AuswertungPro.Next.UI/Views/Pages/EigendevisPage.xaml.cs` | ? | UI | CodeBehind |
| `src/AuswertungPro.Next.UI/ViewModels/Pages/EigendevisPageViewModel.cs` | 248 | UI | Page-VM |
| `src/AuswertungPro.Next.Domain/Models/Devis/Eigendevis.cs` | ? | Domain | DTO |
| `src/AuswertungPro.Next.Domain/Models/Devis/DevisEnums.cs` | ? | Domain | Enums |
| `src/AuswertungPro.Next.Domain/Models/Devis/DevisMappingModels.cs` | ? | Domain | Mapping-DTOs |
| `src/AuswertungPro.Next.Domain/Models/Costs/OfferTotals.cs` | ? | Domain | Offerte-Totals |
| `src/AuswertungPro.Next.Application/Devis/IDevisGenerator.cs` | ? | Application | Interface |
| `src/AuswertungPro.Next.Application/Devis/IDevisMappingService.cs` | ? | Application | Interface |
| `src/AuswertungPro.Next.Infrastructure/Devis/DevisGenerator.cs` | ? | Infra | Implementierung |
| `src/AuswertungPro.Next.Infrastructure/Devis/DevisExcelExporter.cs` | ? | Infra | Excel-Export |
| `src/AuswertungPro.Next.Infrastructure/Devis/DevisMappingService.cs` | ? | Infra | Mapping |
| `src/AuswertungPro.Next.Infrastructure/Devis/SubmissionsPositionService.cs` | ? | Infra | Submissions-Positionen |
| `src/AuswertungPro.Next.Infrastructure/Devis/HistorischeSanierungenService.cs` | ? | Infra | Historische Sanierungen |

**Gesamtumfang:** ueber alle 4 Schichten verteilt (gut!), >14 Files. **Keine Tests** in `tests/AuswertungPro.Next.Pipeline.Tests/`.

### B2. Wo wird es ausgeloest

**Direkt im Hauptmenue.** [ShellViewModel.cs:75](../../src/AuswertungPro.Next.UI/ViewModels/ShellViewModel.cs#L75) (Position 10/12):

```csharp
new("", "Eigendevis", () => new Pages.EigendevisPageViewModel(this, _sp)),
```

### B3. Lebt der Code

- Page hat 248 Zeilen VM + 171 XAML — substanziell.
- ServiceProvider injiziert `DevisGenerator`, `DevisExcelExporter`, `MeasureRecommendationService` etc. → Eigendevis ist mit Sanierung verflochten.
- Audit-Konsens 3/3: alle drei Audits empfehlen Eigendevis aus dem Hauptcode/Hauptmenue zu entfernen oder optional zu machen.

### B4. Empfehlung Eigendevis

**Sanfte Entkopplung mit Setting-Toggle (default sichtbar):**

1. Selber AppSettings-Toggle wie bei Hydraulik (`ShowExpertenmodusFeatures`).
2. NavItem-Liste in `ShellViewModel.cs` filtern — Eigendevis-Item nur einbinden wenn Toggle aktiv. **Default = aktiv** → keine Default-Verhaltens-Aenderung.
3. **Nichts loeschen** — alle 14 Files bleiben, nur die Sichtbarkeit aus der Standard-Hauptnavigation wird konfigurierbar.

**Loeschen-Kandidat:** keiner ohne Audit-Run. Domain/Application-Layer ist sauber strukturiert — eher zukuenftiges optionales Plugin-Modul als Dead Code.

---

## C. Was Phase 1.4 jetzt **nicht** macht

- Keine Loeschungen
- Keine Verschiebungen ueber Schichten
- Keine Aenderungen an Tests
- Keine NuGet-Aenderungen
- Keine `git rm`

---

## D. Vorschlag fuer den naechsten konkreten Schritt

**Variante 1 — Nur Inventar (heute fertig):** Diese Datei committen. User entscheidet wann/ob die Entkopplung kommt.

**Variante 2 — Inventar + Toggle-Infrastruktur (1-2 h Aufwand):**
- AppSettings: `bool ShowExpertenmodusFeatures` (default `true`)
- Settings-Page: Checkbox dafuer
- ShellViewModel: NavItem-Liste filtert Eigendevis bei Toggle=false
- DataPage.xaml: Hydraulik-Buttons Visibility an Toggle gebunden
- Default-Verhalten **unveraendert**, weil Toggle initial true.

**Variante 3 — Defensiv-Aufraeumen Dead Code (30 min):**
- Verifizieren dass `HydraulikPrintDialog` noch live ist (oder durch `PrintOptionsDialog` ersetzbar).
- Wenn ersetzbar: ein einziger Code-Pfad-Wechsel in `DataPageViewModel.PrintHydraulikPdf`.

Kombination 1+2 ist mein Favorit — Inventar geht in den Audit-Ordner, Toggle-Infrastruktur ist die "kleine UI-Entkopplung" (die du erwaehnst). Default = sichtbar, also reversibel und ohne UX-Risiko.

---

## E. Anhang — Konkrete Code-Stellen

### Hydraulik-Aufruf-Pfade
- [DataPage.xaml:250](../../src/AuswertungPro.Next.UI/Views/Pages/DataPage.xaml#L250) — Button "Hydraulik"
- [DataPage.xaml:259](../../src/AuswertungPro.Next.UI/Views/Pages/DataPage.xaml#L259) — Button "Hydraulik PDF"
- [DataPage.xaml.cs:2286](../../src/AuswertungPro.Next.UI/Views/Pages/DataPage.xaml.cs#L2286) — Klick-Handler "Hydraulik"
- [DataPage.xaml.cs:2294](../../src/AuswertungPro.Next.UI/Views/Pages/DataPage.xaml.cs#L2294) — Klick-Handler "Hydraulik PDF"
- [DataPageViewModel.cs:1879](../../src/AuswertungPro.Next.UI/ViewModels/Pages/DataPageViewModel.cs#L1879) — `OpenHydraulikPanel`
- [DataPageViewModel.cs:1977](../../src/AuswertungPro.Next.UI/ViewModels/Pages/DataPageViewModel.cs#L1977) — `PrintHydraulikPdf`

### Eigendevis-Aufruf-Pfade
- [ShellViewModel.cs:75](../../src/AuswertungPro.Next.UI/ViewModels/ShellViewModel.cs#L75) — NavItem
- [ServiceProvider.cs:285-300](../../src/AuswertungPro.Next.UI/ServiceProvider.cs#L285) — DI-Registrierung (DevisGenerator + Exporter)
