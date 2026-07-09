# Leuchtturm-Dashboard Etappe 1 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Die bestehende Projektuebersicht wird zum Leuchtturm-Dashboard mit Vorschau und vollem Projekt-Cockpit fuer Haltungen, Schaechte, Zustand, Kosten und Navigation zur gefilterten Haltungsliste.

**Architecture:** Statistik bleibt in `Application/Dashboard` rein und WPF-frei; die UI laedt Kosten-Stores und gibt sie als Parameter hinein. WPF-Controls zeichnen Donuts und Balken ohne neue Pakete; `OverviewPageViewModel` orchestriert Kostenladen, Debounce-Refresh, Projekt-/Vorschau-Zustand und Startfilter-Navigation.

**Tech Stack:** C#/.NET 10, WPF, CommunityToolkit.Mvvm, xUnit, vorhandene Domain-Kostenmodelle und `ProjectCostStoreRepository`.

---

## File Structure

- Modify: `src/AuswertungPro.Next.Application/Dashboard/DashboardStatisticsBuilder.cs`
  - Neue Dashboard-Records (`ZustandBucket`, `ZustandVerteilung`, erweiterte `DashboardStatistics`) und reiner Builder `Build(Project, ProjectCostStore?, ProjectCostStore?)`.
  - Kosten nur aus uebergebenen Stores, nicht aus `HaltungRecord.GetFieldValue("Kosten")`.
- Modify: `src/AuswertungPro.Next.Application/Dashboard/ProjectPreview.cs`
  - Preview traegt `SchachtCount`, `DashboardStatistics Statistics`, neue Kosten-/Zustandslisten.
- Modify: `src/AuswertungPro.Next.Application/Dashboard/ProjectPreviewFactory.cs`
  - `FromProject(Project, string, ProjectCostStore? haltungCosts = null, ProjectCostStore? schachtCosts = null)`.
- Create: `src/AuswertungPro.Next.UI/DataPage/DataPageStartFilter.cs`
  - Reiner, testbarer Startfilter (`Feld`, `Wert`, Mapping aus Dashboard-Keys, Predicate fuer `HaltungRecord`).
- Modify: `src/AuswertungPro.Next.UI/ViewModels/ShellViewModel.cs`
  - Workspace-Navigation zur `OverviewPage`.
  - Neuer Einstieg `NavigateToDataPage(DataPageStartFilter filter)`.
- Modify: `src/AuswertungPro.Next.UI/ViewModels/Pages/DataPageViewModel.cs`
  - Optionaler Konstruktorparameter `DataPageStartFilter? StartFilter`.
- Modify: `src/AuswertungPro.Next.UI/Views/Pages/DataPage.xaml.cs`
  - Startfilter beim Laden einmalig auf `CollectionView` anwenden, ohne `FilterChipBar` umzubauen.
- Create: `src/AuswertungPro.Next.UI/Services/DashboardRefreshNotifier.cs`
  - Kleines Event fuer Kosten-Saves.
- Modify: `src/AuswertungPro.Next.UI/ServiceProvider.cs`
  - Singleton `DashboardRefresh`.
- Modify: `src/AuswertungPro.Next.UI/ViewModels/Pages/SanierungsMatrixPageViewModel.cs`
  - Nach erfolgreichem `costs.json`-Save `DashboardRefresh.NotifyCostsChanged()`.
- Modify: `src/AuswertungPro.Next.UI/ViewModels/Pages/SchachtSanierungsMatrixPageViewModel.cs`
  - Nach erfolgreichem `schacht_costs.json`-Save `DashboardRefresh.NotifyCostsChanged()`.
- Modify: `src/AuswertungPro.Next.UI/ViewModels/Pages/DataPageViewModel.cs`
  - Bestehendes `CostCalculatorViewModel.Saved`-Event nutzt zusaetzlich `DashboardRefresh.NotifyCostsChanged()`.
- Create: `src/AuswertungPro.Next.UI/Controls/DonutChart.cs`
  - WPF-Control auf `Canvas`, erzeugt `Path`/`ArcSegment`, Segment-Klick optional per Command.
- Create: `src/AuswertungPro.Next.UI/Controls/CategoryBars.cs`
  - WPF-Control fuer horizontale/vertikale Balken, klickbare Buckets per Command.
- Modify: `src/AuswertungPro.Next.UI/ViewModels/Pages/OverviewPageViewModel.cs`
  - Dashboard-State, Kostenladen, Debounce-Refresh, Projektlisten-Collapse, Click-Commands.
- Modify: `src/AuswertungPro.Next.UI/Views/Pages/OverviewPage.xaml`
  - Cockpit-Layout: linke Liste einklappbar, rechte Dashboard-Kacheln, Vorschau/Projekt-offen-Zustand.
- Modify: `src/AuswertungPro.Next.UI/Views/Pages/OverviewPage.xaml.cs`
  - Kleine Converter/Helpers, falls XAML sie braucht.
- Modify: `src/AuswertungPro.Next.UI/AppSettings.cs`
  - `OverviewProjectListCollapsed`.
- Test: `tests/AuswertungPro.Next.UI.Tests/DashboardStatisticsBuilderTests.cs`
  - Migration auf neue Statistik und Zusatztests.
- Create: `tests/AuswertungPro.Next.UI.Tests/DataPageStartFilterTests.cs`
  - Mapping und Predicate.
- Modify: `tests/AuswertungPro.Next.UI.Tests/ProjectPreviewFactoryTests.cs`
  - Neue Preview-Daten und Kosten-Stores.
- Test scope: keine zusaetzlichen Architekturtests geplant; falls ein bestehender Guard beim Testlauf fehlschlaegt, wird genau dieser Guard im betroffenen Task aktualisiert.

---

### Task 1: Dashboard-Statistikmodell testgetrieben migrieren

**Files:**
- Modify: `tests/AuswertungPro.Next.UI.Tests/DashboardStatisticsBuilderTests.cs`
- Modify: `src/AuswertungPro.Next.Application/Dashboard/DashboardStatisticsBuilder.cs`

- [x] **Step 1: Failing Tests fuer Projektstatistik schreiben**

Ersetze den bestehenden Testinhalt in `DashboardStatisticsBuilderTests.cs` durch Tests, die das neue Verhalten fixieren:

```csharp
[Fact]
public void Build_zaehlt_haltungen_schaechte_zustand_kosten_und_fortschritt()
{
    var project = new Project();
    var h1 = Holding("H1", "0", "300", "12.5", "Ja");
    h1.Protocol = new ProtocolDocument
    {
        Current = new ProtocolRevision
        {
            Entries =
            [
                new ProtocolEntry { Code = "BAB01" },
                new ProtocolEntry { Code = "BCA02" }
            ]
        }
    };
    var h2 = Holding("H2", "2", "400", "7,5", "Nein");
    var h3 = Holding("H3", "", "300", "5", "");
    project.Data.Add(h1);
    project.Data.Add(h2);
    project.Data.Add(h3);
    project.SchaechteData.Add(Schacht("S1", "1"));
    project.SchaechteData.Add(Schacht("S2", ""));

    var hCosts = new ProjectCostStore
    {
        ByHolding =
        {
            ["H1"] = Cost("H1", 1200m),
            ["H2"] = Cost("H2", 300m)
        }
    };
    var sCosts = new ProjectCostStore
    {
        ByHolding =
        {
            ["S1"] = Cost("S1", 450m)
        }
    };

    var stats = DashboardStatisticsBuilder.Build(project, hCosts, sCosts);

    Assert.Equal(3, stats.HoldingCount);
    Assert.Equal(2, stats.SchachtCount);
    Assert.Equal(25d, stats.TotalLengthMeters);
    Assert.Equal(1950m, stats.TotalCost);
    Assert.Equal(1, stats.SanierenHaltungen);
    Assert.Equal(3, stats.HaltungenGesamt);
    Assert.Equal(1, stats.SchaechteMitMassnahmen);
    Assert.Equal(2, stats.DringendCount);
    Assert.Equal(2, stats.OhneZustandCount);
    Assert.Contains(stats.Haltungen.Buckets, b => b.Key == "0" && b.Count == 1);
    Assert.Contains(stats.Haltungen.Buckets, b => b.Key == "ohne" && b.Count == 1);
    Assert.Contains(stats.Schaechte.Buckets, b => b.Key == "1" && b.Count == 1);
    Assert.Contains(stats.Schaechte.Buckets, b => b.Key == "ohne" && b.Count == 1);
    Assert.Contains(stats.TopSchaeden, b => b.Key == "BAB" && b.Count == 1);
    Assert.Contains(stats.HaltungDnCosts, b => b.Key == "300" && b.Cost == 1200m);
}
```

- [x] **Step 2: Failing Tests fuer Normalisierung und leeres Projekt schreiben**

Fuege zwei weitere Tests ein:

```csharp
[Theory]
[InlineData("", "ohne")]
[InlineData(" ", "ohne")]
[InlineData("2.4", "2")]
[InlineData("2,6", "3")]
[InlineData("5", "ohne")]
[InlineData("abc", "ohne")]
public void NormalizeZustandsklasse_liefert_0_bis_4_oder_ohne(string raw, string expected)
{
    Assert.Equal(expected, DashboardStatisticsBuilder.NormalizeZustandsklasse(raw));
}

[Fact]
public void Build_leeres_projekt_liefert_geordnete_null_buckets()
{
    var stats = DashboardStatisticsBuilder.Build(new Project(), null, null);

    Assert.False(stats.HasData);
    Assert.Equal(0m, stats.TotalCost);
    Assert.Equal(["0", "1", "2", "3", "4", "ohne"], stats.Haltungen.Buckets.Select(b => b.Key));
    Assert.All(stats.Haltungen.Buckets, b => Assert.Equal(0, b.Count));
    Assert.All(stats.Schaechte.Buckets, b => Assert.Equal(0, b.Count));
}
```

- [x] **Step 3: Tests ausfuehren und Fehlschlag bestaetigen**

Run:

```powershell
dotnet test tests/AuswertungPro.Next.UI.Tests/AuswertungPro.Next.UI.Tests.csproj --filter DashboardStatisticsBuilderTests
```

Expected: FAIL, weil neue Properties/Signaturen noch fehlen.

- [x] **Step 4: Statistik-Records und Builder implementieren**

In `DashboardStatisticsBuilder.cs` die vorhandenen Records ersetzen/erweitern:

```csharp
public sealed record DashboardBucket(string Key, string Label, int Count, double Percent)
{
    public DashboardBucket(string label, int count, double percent)
        : this(label, label, count, percent) { }
}

public sealed record DashboardCostBucket(string Key, string Label, int Count, decimal Cost, double Percent)
{
    public DashboardCostBucket(string label, int count, decimal cost, double percent)
        : this(label, label, count, cost, percent) { }
}

public sealed record ZustandBucket(string Key, string Label, int Count, double Percent);

public sealed record ZustandVerteilung(IReadOnlyList<ZustandBucket> Buckets)
{
    public int Total => Buckets.Sum(b => b.Count);
}

public sealed record DashboardStatistics(
    int HoldingCount,
    int SchachtCount,
    double TotalLengthMeters,
    decimal TotalCost,
    ZustandVerteilung Haltungen,
    ZustandVerteilung Schaechte,
    IReadOnlyList<DashboardBucket> TopSchaeden,
    IReadOnlyList<DashboardCostBucket> HaltungDnCosts,
    int SanierenHaltungen,
    int HaltungenGesamt,
    int SchaechteMitMassnahmen,
    int DringendCount,
    int OhneZustandCount)
{
    public bool HasData => HoldingCount > 0 || SchachtCount > 0;
    public bool HasHoldings => HoldingCount > 0;
    public int TotalHoldings => HoldingCount;
    public IReadOnlyList<DashboardBucket> DamageGroups => TopSchaeden;
    public IReadOnlyList<DashboardCostBucket> DnCostGroups => HaltungDnCosts;
}
```

Implementiere `Build(Project? project, ProjectCostStore? haltungCosts, ProjectCostStore? schachtCosts)` und lasse `Build(IEnumerable<HaltungRecord>?)` als Kompatibilitaets-Wrapper bestehen:

```csharp
public static DashboardStatistics Build(Project? project, ProjectCostStore? haltungCosts, ProjectCostStore? schachtCosts)
{
    var holdings = project?.Data?.ToList() ?? new List<HaltungRecord>();
    var schaechte = project?.SchaechteData?.ToList() ?? new List<SchachtRecord>();
    var hCostMap = haltungCosts?.ByHolding ?? new Dictionary<string, HoldingCost>();
    var sCostMap = schachtCosts?.ByHolding ?? new Dictionary<string, HoldingCost>();

    var totalCost = hCostMap.Values.Sum(ResolveNetTotal) + sCostMap.Values.Sum(ResolveNetTotal);
    var hVerteilung = BuildZustandVerteilung(holdings.Select(r => r.GetFieldValue("Zustandsklasse")));
    var sVerteilung = BuildZustandVerteilung(schaechte.Select(r => r.GetFieldValue("Zustandsklasse")));

    return new DashboardStatistics(
        holdings.Count,
        schaechte.Count,
        holdings.Sum(r => ParseDouble(r.GetFieldValue("Haltungslaenge_m")) ?? 0d),
        totalCost,
        hVerteilung,
        sVerteilung,
        BuildDamageGroups(holdings),
        BuildDnCostGroups(holdings, hCostMap),
        holdings.Count(r => IsJa(r.GetFieldValue("Sanieren_JaNein"))),
        holdings.Count,
        sCostMap.Values.Count(c => ResolveNetTotal(c) > 0m),
        CountKeys(hVerteilung, "0", "1") + CountKeys(sVerteilung, "0", "1"),
        CountKeys(hVerteilung, "ohne") + CountKeys(sVerteilung, "ohne"));
}
```

- [x] **Step 5: Statistiktests ausfuehren**

Run:

```powershell
dotnet test tests/AuswertungPro.Next.UI.Tests/AuswertungPro.Next.UI.Tests.csproj --filter DashboardStatisticsBuilderTests
```

Expected: PASS.

- [x] **Step 6: Commit**

```powershell
git add src/AuswertungPro.Next.Application/Dashboard/DashboardStatisticsBuilder.cs tests/AuswertungPro.Next.UI.Tests/DashboardStatisticsBuilderTests.cs
git commit -m "feat: dashboard-statistik fuer haltungen und schaechte"
```

---

### Task 2: ProjectPreview auf neue Statistik und Kosten-Stores umstellen

**Files:**
- Modify: `src/AuswertungPro.Next.Application/Dashboard/ProjectPreview.cs`
- Modify: `src/AuswertungPro.Next.Application/Dashboard/ProjectPreviewFactory.cs`
- Modify: `tests/AuswertungPro.Next.UI.Tests/ProjectPreviewFactoryTests.cs`

- [x] **Step 1: Failing Preview-Test erweitern**

In `ProjectPreviewFactoryTests.cs` den Haupttest so erweitern, dass Stores uebergeben werden:

```csharp
var project = new Project { Name = "Zone 1.15", Description = "Test" };
project.Data.Add(Holding("H1", 30, "DN300"));
project.SchaechteData.Add(new SchachtRecord());
project.SchaechteData[0].SetFieldValue("Schachtnummer", "S1");
var hCosts = new ProjectCostStore { ByHolding = { ["H1"] = Cost("H1", 500m) } };
var sCosts = new ProjectCostStore { ByHolding = { ["S1"] = Cost("S1", 100m) } };

var preview = ProjectPreviewFactory.FromProject(project, @"D:\P\zone.json", hCosts, sCosts);

Assert.Equal(1, preview.HoldingCount);
Assert.Equal(1, preview.SchachtCount);
Assert.Equal(600m, preview.TotalCost);
Assert.Equal(600m, preview.Statistics.TotalCost);
```

- [x] **Step 2: Test ausfuehren und Fehlschlag bestaetigen**

Run:

```powershell
dotnet test tests/AuswertungPro.Next.UI.Tests/AuswertungPro.Next.UI.Tests.csproj --filter ProjectPreviewFactoryTests
```

Expected: FAIL wegen fehlender Signatur/Properties.

- [x] **Step 3: Preview-Records anpassen**

`ProjectPreview.cs` erhaelt neue Properties:

```csharp
public sealed record ProjectPreview(
    string Name,
    string Description,
    string Path,
    DateTime? ModifiedAtUtc,
    int HoldingCount,
    int SchachtCount,
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
    DashboardStatistics Statistics)
{
    public IReadOnlyList<ZustandBucket> HoldingConditionClasses => Statistics.Haltungen.Buckets;
    public IReadOnlyList<DashboardCostBucket> DnCostGroups => Statistics.HaltungDnCosts;
    public string ModifiedAtDisplay =>
        ModifiedAtUtc?.ToLocalTime().ToString("dd.MM.yyyy", CultureInfo.CurrentCulture) ?? "-";
}
```

- [x] **Step 4: Factory-Signatur erweitern**

`ProjectPreviewFactory.FromProject` ruft:

```csharp
var stats = DashboardStatisticsBuilder.Build(project, haltungCosts, schachtCosts);
```

und setzt `SchachtCount: stats.SchachtCount`, `TotalCost: stats.TotalCost`, `Statistics: stats`.

- [x] **Step 5: Preview-Tests ausfuehren**

Run:

```powershell
dotnet test tests/AuswertungPro.Next.UI.Tests/AuswertungPro.Next.UI.Tests.csproj --filter ProjectPreviewFactoryTests
```

Expected: PASS.

- [x] **Step 6: Commit**

```powershell
git add src/AuswertungPro.Next.Application/Dashboard/ProjectPreview.cs src/AuswertungPro.Next.Application/Dashboard/ProjectPreviewFactory.cs tests/AuswertungPro.Next.UI.Tests/ProjectPreviewFactoryTests.cs
git commit -m "feat: projektvorschau nutzt dashboard-statistik"
```

---

### Task 3: DataPageStartFilter und Navigation implementieren

**Files:**
- Create: `src/AuswertungPro.Next.UI/DataPage/DataPageStartFilter.cs`
- Modify: `src/AuswertungPro.Next.UI/ViewModels/Pages/DataPageViewModel.cs`
- Modify: `src/AuswertungPro.Next.UI/ViewModels/ShellViewModel.cs`
- Modify: `src/AuswertungPro.Next.UI/Views/Pages/DataPage.xaml.cs`
- Create: `tests/AuswertungPro.Next.UI.Tests/DataPageStartFilterTests.cs`

- [x] **Step 1: Failing Startfilter-Tests schreiben**

Neue Datei `DataPageStartFilterTests.cs`:

```csharp
public sealed class DataPageStartFilterTests
{
    [Fact]
    public void FromDashboardZustand_mappt_zustand()
    {
        var filter = DataPageStartFilter.FromDashboardZustand("0");
        Assert.Equal("Zustandsklasse", filter.FieldName);
        Assert.Equal("0", filter.Value);
    }

    [Fact]
    public void Matches_prueft_dn_und_schadenscode()
    {
        var record = new HaltungRecord();
        record.SetFieldValue("DN_mm", "300", FieldSource.Manual, false);
        record.SetFieldValue("Primaere_Schaeden", "BAB Riss\nBCA Anschluss", FieldSource.Manual, false);

        Assert.True(DataPageStartFilter.FromDashboardDn("300").Matches(record));
        Assert.True(DataPageStartFilter.FromDashboardSchaden("BAB").Matches(record));
        Assert.False(DataPageStartFilter.FromDashboardSchaden("BBB").Matches(record));
    }
}
```

- [x] **Step 2: Test ausfuehren und Fehlschlag bestaetigen**

Run:

```powershell
dotnet test tests/AuswertungPro.Next.UI.Tests/AuswertungPro.Next.UI.Tests.csproj --filter DataPageStartFilterTests
```

Expected: FAIL, Typ fehlt.

- [x] **Step 3: DataPageStartFilter implementieren**

Implementiere:

```csharp
public sealed record DataPageStartFilter(string FieldName, string Value)
{
    public static DataPageStartFilter FromDashboardZustand(string key)
        => new("Zustandsklasse", key.TrimStart('Z', 'z'));
    public static DataPageStartFilter FromDashboardSchaden(string key)
        => new("Primaere_Schaeden", key.Trim().ToUpperInvariant());
    public static DataPageStartFilter FromDashboardDn(string key)
        => new("DN_mm", new string((key ?? "").Where(char.IsDigit).ToArray()));

    public bool Matches(HaltungRecord? record)
    {
        if (record is null) return false;
        if (FieldName == "Zustandsklasse")
            return string.Equals(record.GetFieldValue("Zustandsklasse").Trim(), Value, StringComparison.OrdinalIgnoreCase);
        if (FieldName == "DN_mm")
            return NormalizeDigits(record.GetFieldValue("DN_mm")) == NormalizeDigits(Value);
        if (FieldName == "Primaere_Schaeden")
            return EnumerateDamageCodes(record).Any(c => string.Equals(c, Value, StringComparison.OrdinalIgnoreCase));
        return false;
    }
}
```

- [x] **Step 4: DataPageViewModel um optionalen Filter erweitern**

Konstruktor:

```csharp
public DataPageStartFilter? StartFilter { get; }

public DataPageViewModel(ShellViewModel shell, ServiceProvider services, DataPageStartFilter? startFilter = null)
{
    StartFilter = startFilter;
    ...
}
```

- [x] **Step 5: Shell-Navigation bauen**

In `ShellViewModel`:

```csharp
public void NavigateToDataPage(DataPageStartFilter startFilter)
{
    var target = NavItems.FirstOrDefault(x => string.Equals(x.Title, "Haltungen", StringComparison.OrdinalIgnoreCase));
    if (target is null || !ShellLeaveGuard.CanLeave(CurrentPage))
        return;
    CurrentMode = ShellMode.Workspace;
    _suppressLeaveGuard = true;
    SelectedNavItem = target;
    _suppressLeaveGuard = false;
    _navItemBeforeChange = target;
    SetCurrentPage(new Pages.DataPageViewModel(this, _sp, startFilter));
}
```

- [x] **Step 6: DataPage wendet Startfilter beim Laden an**

In `DataPage.xaml.cs` Feld und Methode:

```csharp
private bool _startFilterApplied;

private void ApplyStartFilter()
{
    if (_startFilterApplied || DataContext is not DataPageViewModel vm || vm.StartFilter is null)
        return;
    _startFilterApplied = true;
    var view = CollectionViewSource.GetDefaultView(Grid.ItemsSource);
    view.Filter = obj => vm.StartFilter.Matches(obj as HaltungRecord);
    FilterChips.SetTrefferInfo(view.Cast<object>().Count(), vm.Records.Count);
    Grid.AllowDrop = false;
}
```

Rufe `ApplyStartFilter()` im bestehenden `Loaded`-Handler nach `EnsureColumns()` auf.

- [x] **Step 7: Tests ausfuehren**

Run:

```powershell
dotnet test tests/AuswertungPro.Next.UI.Tests/AuswertungPro.Next.UI.Tests.csproj --filter DataPageStartFilterTests
```

Expected: PASS.

- [x] **Step 8: Commit**

```powershell
git add src/AuswertungPro.Next.UI/DataPage/DataPageStartFilter.cs src/AuswertungPro.Next.UI/ViewModels/Pages/DataPageViewModel.cs src/AuswertungPro.Next.UI/ViewModels/ShellViewModel.cs src/AuswertungPro.Next.UI/Views/Pages/DataPage.xaml.cs tests/AuswertungPro.Next.UI.Tests/DataPageStartFilterTests.cs
git commit -m "feat: startfilter fuer dashboard-navigation"
```

---

### Task 4: Dashboard-Refresh und Kostenladen in OverviewPageViewModel

**Files:**
- Create: `src/AuswertungPro.Next.UI/Services/DashboardRefreshNotifier.cs`
- Modify: `src/AuswertungPro.Next.UI/ServiceProvider.cs`
- Modify: `src/AuswertungPro.Next.UI/ViewModels/Pages/OverviewPageViewModel.cs`
- Modify: `src/AuswertungPro.Next.UI/ViewModels/Pages/SanierungsMatrixPageViewModel.cs`
- Modify: `src/AuswertungPro.Next.UI/ViewModels/Pages/SchachtSanierungsMatrixPageViewModel.cs`
- Modify: `src/AuswertungPro.Next.UI/ViewModels/Pages/DataPageViewModel.cs`

- [x] **Step 1: DashboardRefreshNotifier erstellen**

```csharp
namespace AuswertungPro.Next.UI.Services;

public sealed class DashboardRefreshNotifier
{
    public event EventHandler? CostsChanged;
    public void NotifyCostsChanged() => CostsChanged?.Invoke(this, EventArgs.Empty);
}
```

- [x] **Step 2: ServiceProvider erweitern**

In `ServiceProvider`:

```csharp
public DashboardRefreshNotifier DashboardRefresh { get; } = new();
```

- [x] **Step 3: Matrix-Saves melden**

Nach erfolgreichem `_costRepo.Save(...)` und nach Status-Setzen:

```csharp
_sp.DashboardRefresh.NotifyCostsChanged();
```

in beiden Matrix-ViewModels.

Im bestehenden `costCalcVm.Saved += () => { ... }`-Handler in `DataPageViewModel.ShowSanierungsmassnahmenWindow` ebenfalls nach dem Store-Sync:

```csharp
_sp.DashboardRefresh.NotifyCostsChanged();
```

- [x] **Step 4: OverviewPageViewModel Dashboard-State einbauen**

Fuege Felder/Properties ein:

```csharp
private readonly DispatcherTimer _dashboardRefreshTimer;
private readonly ProjectCostStoreRepository _haltungCostRepo = new();
private readonly ProjectCostStoreRepository _schachtCostRepo = new("schacht_costs.json");

[ObservableProperty] private DashboardStatistics? _dashboard;
[ObservableProperty] private bool _isProjectListCollapsed;
[ObservableProperty] private string _dashboardCostText = "-";

public bool ShowFullDashboard => _shell.IsProjectReady;
public DashboardStatistics? ActiveDashboard => ShowFullDashboard ? Dashboard : SelectedPreview?.Statistics;
```

Konstruktor initialisiert Timer mit 300 ms und laedt `IsProjectListCollapsed` aus Settings.

- [x] **Step 5: Kosten-Stores laden und Builder aufrufen**

Implementiere:

```csharp
private DashboardStatistics BuildStatsFor(Project project, string? projectPath, out bool costAvailable)
{
    var hCosts = LoadCostStore(_haltungCostRepo, projectPath, out var hOk);
    var sCosts = LoadCostStore(_schachtCostRepo, projectPath, out var sOk);
    costAvailable = hOk || sOk;
    return DashboardStatisticsBuilder.Build(project, hCosts, sCosts);
}
```

`LoadCostStore` ruft `repo.Load(projectPath, out var error)` und gibt bei Fehler leeren Store + `false` zurueck.

- [x] **Step 6: Debounce-Refresh verdrahten**

Abonnieren:

```csharp
_shell.Project.Data.CollectionChanged += ProjectCollectionChanged;
_shell.Project.SchaechteData.CollectionChanged += ProjectCollectionChanged;
_sp.DashboardRefresh.CostsChanged += DashboardCostsChanged;
```

Bei Events `ScheduleDashboardRefresh()`; im Timer `RefreshDashboard()`.

- [x] **Step 7: Dispose sauber erweitern**

Beim Dispose Timer stoppen und alle Events abmelden.

- [x] **Step 8: Commit**

```powershell
git add src/AuswertungPro.Next.UI/Services/DashboardRefreshNotifier.cs src/AuswertungPro.Next.UI/ServiceProvider.cs src/AuswertungPro.Next.UI/ViewModels/Pages/OverviewPageViewModel.cs src/AuswertungPro.Next.UI/ViewModels/Pages/SanierungsMatrixPageViewModel.cs src/AuswertungPro.Next.UI/ViewModels/Pages/SchachtSanierungsMatrixPageViewModel.cs src/AuswertungPro.Next.UI/ViewModels/Pages/DataPageViewModel.cs
git commit -m "feat: dashboard-refresh und kostenstores verdrahten"
```

---

### Task 5: Chart-Controls bauen

**Files:**
- Create: `src/AuswertungPro.Next.UI/Controls/DonutChart.cs`
- Create: `src/AuswertungPro.Next.UI/Controls/CategoryBars.cs`

- [x] **Step 1: DonutChart implementieren**

Erstelle ein `Canvas`-basiertes Control mit DependencyProperties:

```csharp
public sealed class DonutChart : Canvas
{
    public static readonly DependencyProperty ItemsSourceProperty =
        DependencyProperty.Register(nameof(ItemsSource), typeof(IEnumerable), typeof(DonutChart),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnChartChanged));
    public static readonly DependencyProperty SegmentCommandProperty =
        DependencyProperty.Register(nameof(SegmentCommand), typeof(ICommand), typeof(DonutChart));
    public IEnumerable? ItemsSource { get => (IEnumerable?)GetValue(ItemsSourceProperty); set => SetValue(ItemsSourceProperty, value); }
    public ICommand? SegmentCommand { get => (ICommand?)GetValue(SegmentCommandProperty); set => SetValue(SegmentCommandProperty, value); }
}
```

`Rebuild()` erzeugt fuer jeden Bucket einen `Path` mit `PathGeometry`, `PathFigure`, `ArcSegment`, `LineSegment`, setzt `Tag = bucket.Key`, `ToolTip`, `Cursor`, `MouseLeftButtonUp`.

- [x] **Step 2: CategoryBars implementieren**

DependencyProperties:

```csharp
ItemsSource, BarCommand, Orientation, ValuePath
```

`Rebuild()` erzeugt `Grid`/`Rectangle`/`TextBlock`-Zeilen fuer horizontale Balken und klickt mit `bucket.Key`.

- [x] **Step 3: Build fuer Controls pruefen**

Run:

```powershell
dotnet build AuswertungPro.sln
```

Expected: 0 Fehler. Vorher `SewerStudio.exe` pruefen; wenn Prozess laeuft, Nutzer bitten zu schliessen.

- [x] **Step 4: Commit**

```powershell
git add src/AuswertungPro.Next.UI/Controls/DonutChart.cs src/AuswertungPro.Next.UI/Controls/CategoryBars.cs
git commit -m "feat: wpf-chart-controls fuer dashboard"
```

---

### Task 6: Overview-Shell als Cockpit erreichbar machen

**Files:**
- Modify: `src/AuswertungPro.Next.UI/ViewModels/ShellViewModel.cs`
- Modify: `src/AuswertungPro.Next.UI/ViewModels/Pages/OverviewPageViewModel.cs`

- [x] **Step 1: NavItem einfuegen**

Fuege vor `Projekt` ein:

```csharp
new("\uE9D2", "Uebersicht", () => new Pages.OverviewPageViewModel(this, _sp), canOpenWithoutProject: true),
```

Ergaenze ToolTip-Text fuer `"Uebersicht"`:

```csharp
"Uebersicht" => "Projekt-Cockpit mit Zustands-, Kosten- und Fortschrittsauswertung.",
```

- [x] **Step 2: Projekt-Oeffnen landet auf Uebersicht**

In `OverviewPageViewModel.OpenProject`, `OpenSelectedProject`, `OpenLastProject`:

```csharp
_shell.EnterWorkspaceOn("Uebersicht");
```

statt `"Haltungen"`.

- [x] **Step 3: Bestehende Navigation weiter erhalten**

`ShellViewModel.NavigateToDataPage` aus Task 3 navigiert weiterhin explizit zu `"Haltungen"`.

- [x] **Step 4: Commit**

```powershell
git add src/AuswertungPro.Next.UI/ViewModels/ShellViewModel.cs src/AuswertungPro.Next.UI/ViewModels/Pages/OverviewPageViewModel.cs
git commit -m "feat: uebersicht als workspace-cockpit"
```

---

### Task 7: OverviewPage XAML zum Dashboard umbauen

**Files:**
- Modify: `src/AuswertungPro.Next.UI/AppSettings.cs`
- Modify: `src/AuswertungPro.Next.UI/ViewModels/Pages/OverviewPageViewModel.cs`
- Modify: `src/AuswertungPro.Next.UI/Views/Pages/OverviewPage.xaml`
- Modify: `src/AuswertungPro.Next.UI/Views/Pages/OverviewPage.xaml.cs`

- [ ] **Step 1: AppSettings fuer einklappbare Liste**

Fuege hinzu:

```csharp
public bool OverviewProjectListCollapsed { get; set; }
```

- [ ] **Step 2: ViewModel Commands fuer Dashboard-Klicks**

Fuege Commands hinzu:

```csharp
public IRelayCommand<object?> NavigateConditionCommand { get; }
public IRelayCommand<object?> NavigateDamageCommand { get; }
public IRelayCommand<object?> NavigateDnCommand { get; }
public IRelayCommand ToggleProjectListCommand { get; }
```

Implementierung:

```csharp
private void NavigateCondition(object? key)
{
    var text = key?.ToString();
    if (string.IsNullOrWhiteSpace(text) || text == "ohne") return;
    _shell.NavigateToDataPage(DataPageStartFilter.FromDashboardZustand(text));
}
```

Analog Schaden/DN.

- [ ] **Step 3: XAML-Namespaces setzen**

`OverviewPage.xaml` erhaelt:

```xml
xmlns:ctrl="clr-namespace:AuswertungPro.Next.UI.Controls"
```

- [ ] **Step 4: Linke Liste einklappbar machen**

Spaltenbreite an `IsProjectListCollapsed` binden und Toggle-Button einbauen:

```xml
<Button Command="{Binding ToggleProjectListCommand}" Style="{StaticResource ToolbarButton}" ToolTip="Projektliste ein- oder ausklappen">
    <TextBlock Text="&#xE76B;" FontFamily="Segoe MDL2 Assets"/>
</Button>
```

- [ ] **Step 5: Rechte Spalte durch Dashboard ersetzen**

Die rechte Spalte enthaelt:

```xml
<ItemsControl ItemsSource="{Binding ActiveDashboard.Haltungen.Buckets}"/>
<ctrl:DonutChart ItemsSource="{Binding ActiveDashboard.Haltungen.Buckets}"
                 SegmentCommand="{Binding NavigateConditionCommand}"/>
<ctrl:DonutChart ItemsSource="{Binding ActiveDashboard.Schaechte.Buckets}"
                 IsHitTestVisible="False"/>
<ctrl:CategoryBars ItemsSource="{Binding ActiveDashboard.TopSchaeden}"
                   BarCommand="{Binding NavigateDamageCommand}"/>
<ctrl:CategoryBars ItemsSource="{Binding ActiveDashboard.HaltungDnCosts}"
                   BarCommand="{Binding NavigateDnCommand}"/>
```

KPI-Text bindet an `ActiveDashboard.HoldingCount`, `SchachtCount`, `TotalLengthMeters`, `DashboardCostText`.

- [ ] **Step 6: Empty States**

Nutze DataTrigger fuer `ActiveDashboard.HasData == false` und Text `"Noch keine Daten"`; fuer `SelectedPreview == null` Text `"Projekt waehlen"`.

- [ ] **Step 7: Build pruefen**

Run:

```powershell
dotnet build AuswertungPro.sln
```

Expected: 0 Fehler. Vorher `SewerStudio.exe` pruefen.

- [ ] **Step 8: Commit**

```powershell
git add src/AuswertungPro.Next.UI/AppSettings.cs src/AuswertungPro.Next.UI/ViewModels/Pages/OverviewPageViewModel.cs src/AuswertungPro.Next.UI/Views/Pages/OverviewPage.xaml src/AuswertungPro.Next.UI/Views/Pages/OverviewPage.xaml.cs
git commit -m "feat: leuchtturm-dashboard-ui"
```

---

### Task 8: Abschlussverifikation

**Files:**
- No source edits expected.

- [ ] **Step 1: SewerStudio-Prozess pruefen**

Run:

```powershell
Get-Process SewerStudio -ErrorAction SilentlyContinue
```

Expected: keine Ausgabe. Wenn Ausgabe kommt: Nutzer bitten, SewerStudio zu schliessen; nicht killen.

- [ ] **Step 2: Build**

Run:

```powershell
dotnet build AuswertungPro.sln
```

Expected: 0 Fehler.

- [ ] **Step 3: Tests**

Run:

```powershell
dotnet test AuswertungPro.sln --no-build
```

Expected: 0 Fehler, bekannter Skip bleibt ok.

- [ ] **Step 4: Arbeitsbaum pruefen**

Run:

```powershell
git status --short
git log --oneline -5
```

Expected: Arbeitsbaum sauber oder nur bewusst ungecommitete Notizen; mehrere lokale Commits, kein Push.

---

## Self-Review

- Spec-Abdeckung:
  - Haltungen + Schaechte, Z0-Z4 + ohne: Task 1.
  - Kosten aus Stores, kein Record-Feld: Task 1/4.
  - Vorschau markiertes Projekt und Projekt offen: Task 2/4/6/7.
  - Pure WPF Charts ohne NuGet: Task 5.
  - Klick-Navigation nur Haltungen, Schacht-Segmente Anzeige: Task 3/7.
  - Live-Refresh mit Debounce und Kosten-Saves: Task 4.
  - Etappe 2 ausgeschlossen: kein Task fuer Druck/PDF.
- Placeholder-Scan: keine offenen Platzhalter oder unkonkreten Arbeitsschritte.
- Type-Konsistenz:
  - `DashboardStatisticsBuilder.Build(Project?, ProjectCostStore?, ProjectCostStore?)` wird von `ProjectPreviewFactory` und `OverviewPageViewModel` verwendet.
  - `DataPageStartFilter` wird von `ShellViewModel.NavigateToDataPage` und `DataPage.xaml.cs` verwendet.
  - `DashboardRefreshNotifier` wird ueber `ServiceProvider.DashboardRefresh` verwendet.
