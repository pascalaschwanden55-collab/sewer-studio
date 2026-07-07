# Kostenfelder folgen „Sanieren = Ja" — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Der Haltungen-Vorlage-Export (und die abgeleiteten Kostenfelder in der Tabelle) zeigen nur die Werte von Haltungen mit `Sanieren_JaNein = Ja`; `Nein`/leer → geleert. Behebt „72 statt 52 Anschlüsse".

**Architecture:** Neue reine Rechenlogik `SanierungCostFieldMapper.SyncRecord` (pro Record, Sanieren-Regel) + Dienst `IDerivedCostFieldSynchronizer` (über alle Records). Der Dienst läuft vor dem Haltungen-Export und an den Speicher-/Edit-Stellen. Zusätzlich Template-Header-Fix. Additiv, kein Kern-Umbau.

**Tech Stack:** .NET 10, C#, WPF/MVVM, ClosedXML, xUnit.

## Global Constraints

- Kommentare deutsch. Keine neuen NuGet-Pakete. Bestehende Tests grün halten. Jede Logik-Änderung mit fokussiertem Test.
- Bestehende `ApplyCosts`/`ClearCosts` in `SanierungCostFieldMapper` NICHT ändern (Matrix/CostCalc nutzen sie weiter).
- Hauptregel: `Sanieren_JaNein == "Ja"` (getrimmt, `OrdinalIgnoreCase`) → zählt; sonst nicht.
- Abgeleitete Felder (8): `Kosten`, `Empfohlene_Sanierungsmassnahmen`, `Renovierung_Inliner_m`, `Renovierung_Inliner_Stk`, `Anschluesse_verpressen`, `Reparatur_Manschette`, `Linerendmanschette_LEM`, `Reparatur_Kurzliner`.
- Mengenfelder (6): wie oben ohne `Kosten` und `Empfohlene_Sanierungsmassnahmen`.
- Sync-Schreibvorgänge: `SetFieldValue(feld, wert, FieldSource.Manual, userEdited: true)` (bypasst die `HaltungRecord.cs:52`-Guard, wie `ApplyCosts` heute).

---

### Task 1: `SanierungCostFieldMapper.SyncRecord` — die Sanieren-Regel (reine Logik)

**Files:**
- Modify: `src/AuswertungPro.Next.Application/DataPage/SanierungCostFieldMapper.cs`
- Test: `tests/AuswertungPro.Next.Pipeline.Tests/SanierungCostFieldMapperSyncTests.cs` (neu)

**Interfaces:**
- Consumes: `HaltungRecord`, `HoldingCost` (Domain.Models); vorhandene Helfer `MaxMeasureQty`, `SumSelectedQty`, `SumMeasureLengths`, `HasSelectedLiner`, `ResolveNetTotal` (public static im selben Mapper); `MeasuresTextBuilder.FormatInt/FormatDecimal/BuildMeasuresText`.
- Produces: `static bool SyncRecord(HaltungRecord record, HoldingCost? cost)` — true wenn sich ein Feld geändert hat. `static readonly string[] QuantityFieldNames`.

- [ ] **Step 1: Failing test-Datei anlegen**

```csharp
using System.Collections.Generic;
using AuswertungPro.Next.Application.DataPage;
using AuswertungPro.Next.Domain.Models;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

public class SanierungCostFieldMapperSyncTests
{
    private static HaltungRecord Rec(string sanieren, string? anschl = null)
    {
        var r = new HaltungRecord();
        r.SetFieldValue("Sanieren_JaNein", sanieren, FieldSource.Manual, userEdited: true);
        if (anschl != null) r.SetFieldValue("Anschluesse_verpressen", anschl, FieldSource.Pdf, userEdited: false);
        return r;
    }

    private static HoldingCost CostWithAnschluss(int stk) => new()
    {
        Holding = "H1",
        Measures = { new MeasureCost { MeasureId = "M", MeasureName = "GFK", Lines = {
            new CostLine { ItemKey = "ANSCHLUSS_EINBINDEN", Unit = "Stk", Qty = stk, Selected = true, UnitPrice = 100m }
        } } }
    };

    [Fact]
    public void Ja_mit_Massnahme_setzt_Anschlusszahl()
    {
        var r = Rec("Ja");
        var changed = SanierungCostFieldMapper.SyncRecord(r, CostWithAnschluss(2));
        Assert.True(changed);
        Assert.Equal("2", r.GetFieldValue("Anschluesse_verpressen"));
    }

    [Fact]
    public void Nein_leert_alle_Kostenfelder_auch_Pdf_Import()
    {
        var r = Rec("Nein", anschl: "5");
        var changed = SanierungCostFieldMapper.SyncRecord(r, cost: null);
        Assert.True(changed);
        Assert.Equal("", r.GetFieldValue("Anschluesse_verpressen"));
    }

    [Fact]
    public void Ja_ohne_Massnahme_leert_Mengenfelder_behaelt_Kosten()
    {
        var r = Rec("Ja", anschl: "3");
        r.SetFieldValue("Kosten", "1200.00", FieldSource.Manual, userEdited: true);
        SanierungCostFieldMapper.SyncRecord(r, cost: null);
        Assert.Equal("", r.GetFieldValue("Anschluesse_verpressen"));
        Assert.Equal("1200.00", r.GetFieldValue("Kosten"));
    }

    [Fact]
    public void Bereits_synchron_meldet_keine_Aenderung()
    {
        var r = Rec("Ja");
        SanierungCostFieldMapper.SyncRecord(r, CostWithAnschluss(2));
        var changedAgain = SanierungCostFieldMapper.SyncRecord(r, CostWithAnschluss(2));
        Assert.False(changedAgain);
    }
}
```

- [ ] **Step 2: Test läuft → FAIL** (`SyncRecord` existiert nicht)

Run: `dotnet test tests/AuswertungPro.Next.Pipeline.Tests --filter SanierungCostFieldMapperSyncTests`
Expected: FAIL (Compile/Not defined).

- [ ] **Step 3: `SyncRecord` + `QuantityFieldNames` implementieren**

In `SanierungCostFieldMapper.cs` ergänzen (using `System.Globalization;` ist schon da):

```csharp
/// <summary>Die 6 Mengen-Zaehlfelder (abgeleitete Felder ohne Kosten/Empfohlene).</summary>
public static readonly string[] QuantityFieldNames =
{
    "Renovierung_Inliner_m", "Renovierung_Inliner_Stk", "Anschluesse_verpressen",
    "Reparatur_Manschette", "Linerendmanschette_LEM", "Reparatur_Kurzliner",
};

/// <summary>
/// Zieht die abgeleiteten Kostenfelder eines Records nach der Sanieren-Regel nach:
/// Sanieren=Ja + Massnahmen → aus cost berechnen; Ja ohne Massnahme → Mengenfelder leeren,
/// Kosten/Empfohlene behalten (Pauschal-Schutz); Nein/leer → alle 8 Felder leeren.
/// Schreibt nur geaenderte Felder (userEdited:true, wie ApplyCosts). Kein UI/Lernen.
/// Rueckgabe: true, wenn sich mindestens ein Feld geaendert hat.
/// </summary>
public static bool SyncRecord(HaltungRecord record, HoldingCost? cost)
{
    if (record is null) return false;

    var toRenovate = string.Equals(
        (record.GetFieldValue("Sanieren_JaNein") ?? "").Trim(), "Ja",
        StringComparison.OrdinalIgnoreCase);

    var target = new Dictionary<string, string>(StringComparer.Ordinal);

    if (!toRenovate)
    {
        foreach (var f in CostFieldNames) target[f] = "";
    }
    else
    {
        var hasMeasures = cost is not null
            && cost.Measures.Any(m => m.Lines.Any(l => l.Selected && l.Qty > 0m));
        if (!hasMeasures)
        {
            foreach (var f in QuantityFieldNames) target[f] = "";
        }
        else
        {
            var inlinerMeters = SumMeasureLengths(cost!,
                "NADELFILZ", "GFK", "SCHLAUCHLINER_NADELFILZ",
                "SCHLAUCHLINER_NADELFILZ_OPENEND", "SCHLAUCHLINER_GFK");
            var inlinerStk = HasSelectedLiner(cost!) ? 1 : 0;
            var anschluesse = Math.Max(
                MaxMeasureQty(cost!, "ANSCHLUSS_EINBINDEN", "ANSCHLUSS_DICHTEN", "ANSCHLUSS_VERSCHLIESSEN"),
                MaxMeasureQty(cost!, "ANSCHLUSS_AUFFRAESEN"));
            var manschette = SumSelectedQty(cost!, "MANSCHETTE_PER_ST", "MANSCHETTE_EDELSTAHL");
            var lem = SumSelectedQty(cost!, "LINERENDMANSCHETTE_LEM");
            var kurzliner = SumSelectedQty(cost!, "KURZLINER_PER_ST", "QUICKLOCK_PER_ST", "KURZLINER_PARTLINER");

            target["Renovierung_Inliner_m"] = MeasuresTextBuilder.FormatDecimal(inlinerMeters);
            target["Renovierung_Inliner_Stk"] = MeasuresTextBuilder.FormatInt(inlinerStk);
            target["Anschluesse_verpressen"] = MeasuresTextBuilder.FormatInt(anschluesse);
            target["Reparatur_Manschette"] = MeasuresTextBuilder.FormatInt(manschette);
            target["Linerendmanschette_LEM"] = MeasuresTextBuilder.FormatInt(lem);
            target["Reparatur_Kurzliner"] = MeasuresTextBuilder.FormatInt(kurzliner);
            target["Kosten"] = ResolveNetTotal(cost!).ToString("0.00", CultureInfo.InvariantCulture);
            target["Empfohlene_Sanierungsmassnahmen"] = MeasuresTextBuilder.BuildMeasuresText(cost!);
        }
    }

    var changed = false;
    foreach (var kv in target)
    {
        if (!string.Equals(record.GetFieldValue(kv.Key), kv.Value, StringComparison.Ordinal))
        {
            record.SetFieldValue(kv.Key, kv.Value, FieldSource.Manual, userEdited: true);
            changed = true;
        }
    }
    return changed;
}
```

- [ ] **Step 4: Test läuft → PASS**

Run: `dotnet test tests/AuswertungPro.Next.Pipeline.Tests --filter SanierungCostFieldMapperSyncTests`
Expected: PASS (4 Tests).

- [ ] **Step 5: Commit**

```bash
git add src/AuswertungPro.Next.Application/DataPage/SanierungCostFieldMapper.cs tests/AuswertungPro.Next.Pipeline.Tests/SanierungCostFieldMapperSyncTests.cs
git commit -m "feat(kosten): SyncRecord - abgeleitete Felder folgen Sanieren=Ja"
```

---

### Task 2: `IDerivedCostFieldSynchronizer` + Dienst (über alle Records)

**Files:**
- Create: `src/AuswertungPro.Next.Application/DataPage/IDerivedCostFieldSynchronizer.cs`
- Create: `src/AuswertungPro.Next.Application/DataPage/DerivedCostFieldSynchronizer.cs`
- Test: `tests/AuswertungPro.Next.Pipeline.Tests/DerivedCostFieldSynchronizerTests.cs` (neu)

**Interfaces:**
- Consumes: `Project` (mit `Data`: `List<HaltungRecord>`), `ProjectCostStore` (`ByHolding: Dictionary<string,HoldingCost>`), `SanierungCostFieldMapper.SyncRecord`.
- Produces: `interface IDerivedCostFieldSynchronizer { int Sync(Project project, ProjectCostStore store); }` und Impl `DerivedCostFieldSynchronizer`. `Sync` gibt Anzahl geänderter Records zurück. Holding-Schlüssel = Feld `Haltungsname` (getrimmt), Lookup `OrdinalIgnoreCase`.

- [ ] **Step 1: Failing test**

```csharp
using AuswertungPro.Next.Application.DataPage;
using AuswertungPro.Next.Domain.Models;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

public class DerivedCostFieldSynchronizerTests
{
    private static HaltungRecord Rec(string name, string sanieren, string? anschl = null)
    {
        var r = new HaltungRecord();
        r.SetFieldValue("Haltungsname", name, FieldSource.Manual, userEdited: true);
        r.SetFieldValue("Sanieren_JaNein", sanieren, FieldSource.Manual, userEdited: true);
        if (anschl != null) r.SetFieldValue("Anschluesse_verpressen", anschl, FieldSource.Pdf, userEdited: false);
        return r;
    }

    [Fact]
    public void Nein_Haltung_wird_geleert_Ja_bleibt_leer_ohne_Store()
    {
        var project = new Project();
        project.Data.Add(Rec("A-B", "Nein", anschl: "5"));
        project.Data.Add(Rec("C-D", "Ja"));
        var store = new ProjectCostStore();

        var changed = new DerivedCostFieldSynchronizer().Sync(project, store);

        Assert.Equal(1, changed); // nur die Nein-Haltung hatte 5 -> ""
        Assert.Equal("", project.Data[0].GetFieldValue("Anschluesse_verpressen"));
    }

    [Fact]
    public void Ja_Haltung_mit_Store_bekommt_Anschlusszahl()
    {
        var project = new Project();
        project.Data.Add(Rec("A-B", "Ja"));
        var store = new ProjectCostStore();
        store.ByHolding["A-B"] = new HoldingCost { Holding = "A-B", Measures = { new MeasureCost {
            Lines = { new CostLine { ItemKey = "ANSCHLUSS_EINBINDEN", Unit = "Stk", Qty = 3, Selected = true } } } } };

        new DerivedCostFieldSynchronizer().Sync(project, store);

        Assert.Equal("3", project.Data[0].GetFieldValue("Anschluesse_verpressen"));
    }
}
```

- [ ] **Step 2: Test → FAIL**

Run: `dotnet test tests/AuswertungPro.Next.Pipeline.Tests --filter DerivedCostFieldSynchronizerTests`
Expected: FAIL (nicht definiert).

- [ ] **Step 3: Interface + Dienst implementieren**

`IDerivedCostFieldSynchronizer.cs`:

```csharp
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.DataPage;

/// <summary>Zieht die abgeleiteten Kostenfelder aller Haltungen nach der Sanieren-Regel nach.</summary>
public interface IDerivedCostFieldSynchronizer
{
    /// <summary>Rueckgabe: Anzahl geaenderter Records.</summary>
    int Sync(Project project, ProjectCostStore store);
}
```

`DerivedCostFieldSynchronizer.cs`:

```csharp
using System;
using System.Collections.Generic;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.DataPage;

public sealed class DerivedCostFieldSynchronizer : IDerivedCostFieldSynchronizer
{
    public int Sync(Project project, ProjectCostStore store)
    {
        if (project?.Data is null) return 0;

        // Store case-insensitiv nach Haltungsname aufloesen.
        var byName = new Dictionary<string, HoldingCost>(StringComparer.OrdinalIgnoreCase);
        if (store?.ByHolding is not null)
            foreach (var kv in store.ByHolding)
                byName[kv.Key.Trim()] = kv.Value;

        var changed = 0;
        foreach (var rec in project.Data)
        {
            var key = (rec.GetFieldValue("Haltungsname") ?? "").Trim();
            byName.TryGetValue(key, out var cost);
            if (SanierungCostFieldMapper.SyncRecord(rec, cost))
                changed++;
        }
        return changed;
    }
}
```

- [ ] **Step 4: Test → PASS**

Run: `dotnet test tests/AuswertungPro.Next.Pipeline.Tests --filter DerivedCostFieldSynchronizerTests`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/AuswertungPro.Next.Application/DataPage/IDerivedCostFieldSynchronizer.cs src/AuswertungPro.Next.Application/DataPage/DerivedCostFieldSynchronizer.cs tests/AuswertungPro.Next.Pipeline.Tests/DerivedCostFieldSynchronizerTests.cs
git commit -m "feat(kosten): DerivedCostFieldSynchronizer ueber alle Haltungen"
```

---

### Task 3: DI-Registrierung im `ServiceProvider`

**Files:**
- Modify: `src/AuswertungPro.Next.UI/ServiceProvider.cs`

**Interfaces:**
- Consumes: `DerivedCostFieldSynchronizer` (Task 2).
- Produces: `public IDerivedCostFieldSynchronizer CostFieldSync { get; }` am `ServiceProvider`.

- [ ] **Step 1: Property + Konstruktor-Zeile ergänzen** (Muster wie `ExcelExport`)

Property zum bestehenden Property-Block:

```csharp
public AuswertungPro.Next.Application.DataPage.IDerivedCostFieldSynchronizer CostFieldSync { get; }
```

Im Konstruktor (bei den anderen `new`-Zuweisungen):

```csharp
CostFieldSync = new AuswertungPro.Next.Application.DataPage.DerivedCostFieldSynchronizer();
```

- [ ] **Step 2: Build**

Run: `dotnet build src/AuswertungPro.Next.UI`
Expected: 0 Fehler.

- [ ] **Step 3: Commit**

```bash
git add src/AuswertungPro.Next.UI/ServiceProvider.cs
git commit -m "chore(di): DerivedCostFieldSynchronizer registrieren"
```

---

### Task 4: Sync direkt vor dem Haltungen-Export (DER Fix für 72→52)

**Files:**
- Modify: `src/AuswertungPro.Next.UI/ViewModels/Pages/ExportPageViewModel.cs` (`ExportAsync`, ~:78-99)

**Interfaces:**
- Consumes: `_sp.CostFieldSync` (Task 3), `ProjectCostStoreRepository` (Load), `_sp.Settings.LastProjectPath`, `_shell.Project`.

- [ ] **Step 1: Vor dem `ExportToTemplate`-Aufruf synchronisieren**

In `ExportAsync`, unmittelbar VOR `var res = await Task.Run(...)` einfügen:

```csharp
// Vor dem Export die abgeleiteten Kostenfelder auf den aktuellen Stand ziehen
// (Sanieren=Nein/leer -> geleert). Fehlende/gesperrte costs.json -> NICHT syncen (kein Datenverlust).
var projectPath = _sp.Settings.LastProjectPath ?? "";
if (!string.IsNullOrWhiteSpace(projectPath))
{
    var store = new ProjectCostStoreRepository().Load(projectPath, out var syncLoadError);
    if (syncLoadError is null)
        _sp.CostFieldSync.Sync(_shell.Project, store);
}
```

Nötige `using`: `AuswertungPro.Next.Infrastructure.Costs;` (für `ProjectCostStoreRepository`) — prüfen/ergänzen.

- [ ] **Step 2: Build**

Run: `dotnet build src/AuswertungPro.Next.UI`
Expected: 0 Fehler.

- [ ] **Step 3: Manuelle Verifikation** (siehe „Abnahme" unten): Zone 1.15 exportieren → Spalte „Anschlüsse verpressen" summiert **52**, nicht 72.

- [ ] **Step 4: Commit**

```bash
git add src/AuswertungPro.Next.UI/ViewModels/Pages/ExportPageViewModel.cs
git commit -m "fix(export): Haltungen-Export synct Kostenfelder nach Sanieren=Ja (72->52)"
```

---

### Task 5: Template-Header-Fix — `Renovierung Inliner m` wird sonst nie exportiert

**Files:**
- Modify: `Export_Vorlage/Haltungen.xlsx` (Header-Zelle in Zeile 11)
- Test: `tests/AuswertungPro.Next.Infrastructure.Tests/ExcelExportTests.cs` (Coverage-Test ergänzen)

**Interfaces:**
- Consumes: `ExcelTemplateExportService.ExportToTemplate`, `FieldCatalog`.

- [ ] **Step 1: Failing Coverage-Test** — belegt, dass `Renovierung_Inliner_m` in eine Zielspalte exportiert wird. (Konkret: Projekt mit einem Record, `Renovierung_Inliner_m="12.5"`, `Sanieren_JaNein="Ja"`; nach `ExportToTemplate` steht 12.5 in der Spalte mit Header „Renovierung Inliner m".) Vorhandenes Test-Muster in `ExcelExportTests.cs` als Vorbild nutzen (dort wird bereits gegen `Export_Vorlage/Haltungen.xlsx` exportiert und gelesen).

- [ ] **Step 2: Test → FAIL** (Header ist aktuell „m" → kein Match → Wert fehlt).

- [ ] **Step 3: Header korrigieren** — in `Export_Vorlage/Haltungen.xlsx`, Blatt „Haltungen", Header-Zeile 11: die Zelle mit „m" (für Renovierung Inliner Meter) auf **`Renovierung Inliner m`** setzen. (Manuell in Excel oder per ClosedXML-Einmalskript; die Datei ist in git nachverfolgt.)

- [ ] **Step 4: Test → PASS**

Run: `dotnet test tests/AuswertungPro.Next.Infrastructure.Tests --filter ExcelExport`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add Export_Vorlage/Haltungen.xlsx tests/AuswertungPro.Next.Infrastructure.Tests/ExcelExportTests.cs
git commit -m "fix(export): Header 'Renovierung Inliner m' -> Feld wird exportiert"
```

---

### Task 6: Aktuell halten in der App — Ja→Nein mit Rückfrage + Sync auf Speicher-Pfaden

> Diese Task hält die Tabelle live konsistent (nicht nur beim Export). UI-lastig; Verifikation manuell.

**Files:**
- Modify: `src/AuswertungPro.Next.UI/ViewModels/Pages/SanierungsMatrixPageViewModel.cs` (nach Save ~:1226 → `_sp.CostFieldSync.Sync(_shell-Project?, store)` mit dem frisch gemergten Store)
- Modify: `src/AuswertungPro.Next.UI/ViewModels/Windows/CostCalculatorViewModel.cs` (nach Save ~:242 → Sync)
- Modify: `src/AuswertungPro.Next.UI/ViewModels/Pages/BuilderPageViewModel.cs` (`RecomputeStoredCostsWithCurrentCatalog` ~:937 → nach Save `_sp.CostFieldSync.Sync(_shell.Project, store)`)
- Modify: Grid-Commit von `Sanieren_JaNein` (`src/AuswertungPro.Next.UI/Views/Pages/DataPage.xaml.cs`, Commit-Pfad ~:761-790)

**Interfaces:**
- Consumes: `_sp.CostFieldSync`, jeweiliges Project + geladener/gemergter Store, `_sp.Dialogs.ConfirmWarn` (für die Rückfrage).

- [ ] **Step 1: Speicher-Pfade nachziehen** — an jeder der drei Save-Stellen NACH erfolgreichem `repo.Save(...)` einfügen:

```csharp
// Abgeleitete Record-Felder auf den frisch gespeicherten Store-Stand nachziehen.
_sp.CostFieldSync.Sync(<projectObjekt>, <gemergterStore>);
```

(`<projectObjekt>` = das jeweilige Project des VM; `<gemergterStore>` = der eben gespeicherte Store.)

- [ ] **Step 2: Ja→Nein Rückfrage im Grid-Commit** — beim Commit des Feldes `Sanieren_JaNein`, wenn der neue Wert nicht „Ja" ist und der alte „Ja" war:

```csharp
// Umstellen auf Nein/leer: erst fragen, dann Kostenfelder der Haltung leeren.
if (!_sp.Dialogs.ConfirmWarn(
        "Diese Haltung auf 'nicht sanieren' setzen? Die berechneten Kostenwerte werden entfernt.",
        "Sanieren", defaultNo: true))
{
    // Abbruch: Wert zurueck auf 'Ja', nichts leeren.
    record.SetFieldValue("Sanieren_JaNein", "Ja", FieldSource.Manual, userEdited: true);
    return;
}
SanierungCostFieldMapper.SyncRecord(record, cost: null); // Nein -> alle 8 Felder leeren
```

(Die Massnahmen im Kostenspeicher werden NICHT entfernt → Nein→Ja stellt sie über den normalen Sync wieder her.)

- [ ] **Step 3: Build**

Run: `dotnet build AuswertungPro.sln`
Expected: 0 Fehler.

- [ ] **Step 4: Manuelle Verifikation** — Matrix speichern → Tabelle zeigt aktuelle Werte; Haltung Ja→Nein → Rückfrage; bestätigt → Felder leer; Nein→Ja → Werte zurück.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat(kosten): Kostenfelder live nachziehen + Ja->Nein mit Rueckfrage"
```

---

## Optional / Hygiene (niedrige Priorität, separat entscheidbar)

- **NPK-Filter auf `Sanieren=Ja`** in `BuilderPageViewModel.PrepareLvPositions`: `holdings` zusätzlich auf Records mit `Sanieren_JaNein=Ja` filtern (deckt den allgemeinen Fall „Nein-Haltung mit echten Massnahmen" ab; in Zone 1.15 heute 0-Wirkung).
- **Geister-Store-Einträge** (Haltung ohne Record) beim Speichern gezielt entfernen, wenn `project.Data` vollständig geladen ist. In Zone 1.15 harmlos (nicht im NPK), nur `costs.json`-Hygiene.

---

## Abnahme (manuell, Zone 1.15)

1. `dotnet build AuswertungPro.sln` → 0 Fehler; `dotnet test AuswertungPro.sln` → alle grün.
2. Projekt „Zone 1.15" öffnen, Haltungen exportieren → Spalte **Anschlüsse verpressen summiert 52** (vorher 72).
3. Eine Haltung Ja→Nein umstellen → Rückfrage erscheint; bestätigt → deren Kostenfelder leer; wieder auf Ja → Werte zurück.
4. Handgetippter Kosten-Pauschalbetrag auf einer Ja-Haltung ohne Massnahme bleibt nach Export erhalten.

## Self-Review-Notiz

- Spec-Abdeckung: Haltungen-Export (T4), Sanieren-Regel inkl. Pauschal-Schutz (T1), live nachziehen + Ja→Nein-Rückfrage (T6), Template-Header (T5). NPK-Filter/Geister als optionale Hygiene ausgewiesen (NPK war bereits korrekt — Korrektur ggü. früherer Annahme).
- Typkonsistenz: `SyncRecord(HaltungRecord, HoldingCost?)`, `Sync(Project, ProjectCostStore)`, `CostFieldSync` durchgängig gleich benannt.
