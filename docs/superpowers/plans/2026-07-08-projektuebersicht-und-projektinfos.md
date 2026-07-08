# Projektübersicht-Vorschau & Projekt-Infos-Straffung Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Einfachklick in der Projektübersicht zeigt rechts eine professionelle Projekt-Vorschau (inkl. Gesamtmeter, ohne Schadensgruppen); Projekt-Infos-Formular wird um „Firma & Kontakt" gestrafft und Auftraggeber bei Neuanlage mit „Abwasser Uri" vorbelegt.

**Architecture:** Neuer reiner Helfer `ProjectPreviewFactory` baut aus einem geladenen `Project` ein `ProjectPreview`-Record (nutzt `DashboardStatisticsBuilder`). `OverviewPageViewModel` lädt bei Auswahl-Wechsel das gewählte Projekt aus seiner Datei und stellt es als `SelectedPreview` bereit; das rechte Panel bindet daran. Formular-/Draft-Änderungen sind kleine, additive Eingriffe.

**Tech Stack:** WPF/.NET 10, MVVM (CommunityToolkit.Mvvm), xUnit.

## Global Constraints

- Thin-AI/Schichten: Geschäftslogik in C# (Application), UI ruft ViewModel/Helfer — keine Logik im Code-behind.
- Additiv: kein großes Refactoring; neue Logik in neuen fokussierten Dateien.
- Positional Records additiv erweitern, mit **benannten Argumenten** konstruieren.
- Kommentare auf Deutsch.
- Fokussierter Test für Kernlogik (Factory + Draft-Default).
- Metadaten-Keys exakt: `Auftraggeber, Gemeinde, Zone, Strasse, Bearbeiter, InspektionsDatum, AuftragNr, FirmaName` (Dictionary ist `StringComparer.Ordinal`).
- Auftraggeber-Default „Abwasser Uri" nur bei Neuanlage; bestehende Projekte nie überschreiben.
- **Commits:** ~68 unzusammenhängende uncommittete Dateien im Working-Tree. Jede Task staged NUR ihre eigenen Dateien/Hunks (kein `git add -A`). **`src/AuswertungPro.Next.UI/ViewModels/ShellViewModel.cs` ist bereits „dirty" (Fremdänderungen)** → in Task 5 nur den eigenen Hunk stagen (siehe Schritte).

## File Structure

- Neu `src/AuswertungPro.Next.Application/Dashboard/ProjectPreview.cs` — Anzeige-Record der Vorschau.
- Neu `src/AuswertungPro.Next.Application/Dashboard/ProjectPreviewFactory.cs` — baut `ProjectPreview` aus `Project`.
- Neu `src/AuswertungPro.Next.Application/Projects/NewProjectDraftFactory.cs` — erzeugt Draft-`Project` mit Auftraggeber-Default.
- Ändern `src/AuswertungPro.Next.UI/ViewModels/Pages/OverviewPageViewModel.cs` — `SelectedPreview` + Aufbau bei Auswahl + Default-Vorselektion.
- Ändern `src/AuswertungPro.Next.UI/Views/Pages/OverviewPage.xaml` — rechtes Panel auf `SelectedPreview`, Gesamtmeter-Kachel, Stammdaten-Raster, Schadensgruppen weg.
- Ändern `src/AuswertungPro.Next.UI/Views/Pages/ProjectPage.xaml` — Block „Firma & Kontakt" entfernen.
- Ändern `src/AuswertungPro.Next.UI/ViewModels/ShellViewModel.cs` — `StartNewProjectDraft` nutzt `NewProjectDraftFactory`.
- Tests: `tests/AuswertungPro.Next.UI.Tests/ProjectPreviewFactoryTests.cs`, `tests/AuswertungPro.Next.UI.Tests/NewProjectDraftFactoryTests.cs`.

---

### Task 1: ProjectPreview-Record + ProjectPreviewFactory (+ Tests)

**Files:**
- Create: `src/AuswertungPro.Next.Application/Dashboard/ProjectPreview.cs`
- Create: `src/AuswertungPro.Next.Application/Dashboard/ProjectPreviewFactory.cs`
- Test: `tests/AuswertungPro.Next.UI.Tests/ProjectPreviewFactoryTests.cs`

**Interfaces:**
- Consumes: `AuswertungPro.Next.Domain.Models.Project` (Properties `Name` string, `Description` string, `ModifiedAtUtc` DateTime, `AppVersion` string, `Metadata` `Dictionary<string,string>`, `Data` `ObservableCollection<HaltungRecord>`); `DashboardStatisticsBuilder.Build(IEnumerable<HaltungRecord>?)` → `DashboardStatistics(int TotalHoldings, double TotalLengthMeters, decimal TotalCost, IReadOnlyList<DashboardBucket> ConditionClasses, IReadOnlyList<DashboardBucket> DamageGroups, IReadOnlyList<DashboardCostBucket> DnCostGroups)`; `DashboardBucket(string Label, int Count, double Percent)`; `DashboardCostBucket(string Label, int Count, decimal Cost, double Percent)`.
- Produces: `ProjectPreview` (Felder siehe unten) und `ProjectPreviewFactory.FromProject(Project project, string path) → ProjectPreview`.

- [ ] **Step 1: Failing test schreiben**

`tests/AuswertungPro.Next.UI.Tests/ProjectPreviewFactoryTests.cs`:
```csharp
using System.Linq;
using AuswertungPro.Next.Application.Dashboard;
using AuswertungPro.Next.Domain.Models;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

public sealed class ProjectPreviewFactoryTests
{
    private static HaltungRecord Holding(double laenge, string dn, decimal kosten)
    {
        var r = new HaltungRecord();
        r.SetFieldValue("Haltungslaenge_m", laenge.ToString(System.Globalization.CultureInfo.InvariantCulture), FieldSource.Manual, false);
        r.SetFieldValue("DN", dn, FieldSource.Manual, false);
        r.SetFieldValue("Kosten", kosten.ToString(System.Globalization.CultureInfo.InvariantCulture), FieldSource.Manual, false);
        return r;
    }

    [Fact]
    public void FromProject_mappt_kennzahlen_und_metadaten()
    {
        var project = new Project { Name = "Zone 1.15", Description = "Test" };
        project.Metadata["Auftraggeber"] = "Abwasser Uri";
        project.Metadata["Gemeinde"] = "Altdorf";
        project.Metadata["Zone"] = "1.15";
        project.Data.Add(Holding(30, "DN300", 100m));
        project.Data.Add(Holding(20, "DN300", 50m));

        var preview = ProjectPreviewFactory.FromProject(project, @"D:\P\zone.json");

        Assert.Equal("Zone 1.15", preview.Name);
        Assert.Equal(@"D:\P\zone.json", preview.Path);
        Assert.Equal(2, preview.HoldingCount);
        Assert.Equal(50d, preview.TotalLengthMeters);
        Assert.Equal(150m, preview.TotalCost);
        Assert.Equal("Abwasser Uri", preview.Auftraggeber);
        Assert.Equal("Altdorf", preview.Gemeinde);
        Assert.Equal("1.15", preview.Zone);

        // Balken werden 1:1 aus dem Builder durchgereicht (robust gegen Builder-Interna):
        var expected = DashboardStatisticsBuilder.Build(project.Data);
        Assert.Equal(expected.ConditionClasses.Count, preview.ConditionClasses.Count);
        Assert.Equal(expected.DnCostGroups.Count, preview.DnCostGroups.Count);
    }

    [Fact]
    public void FromProject_fehlende_metadaten_werden_leer()
    {
        var project = new Project { Name = "X" };
        project.Metadata.Remove("Bearbeiter");

        var preview = ProjectPreviewFactory.FromProject(project, "p.json");

        Assert.Equal(string.Empty, preview.Bearbeiter);
    }
}
```

- [ ] **Step 2: Test läuft rot**

Run: `dotnet build tests/AuswertungPro.Next.UI.Tests/AuswertungPro.Next.UI.Tests.csproj --no-restore -v q`
Expected: FEHLER — `ProjectPreview` / `ProjectPreviewFactory` existieren nicht (CS0246).

- [ ] **Step 3: ProjectPreview-Record anlegen**

`src/AuswertungPro.Next.Application/Dashboard/ProjectPreview.cs`:
```csharp
using System;
using System.Collections.Generic;
using System.Globalization;

namespace AuswertungPro.Next.Application.Dashboard;

/// <summary>
/// Schreibgeschützte Projekt-Vorschau für die Projektübersicht (rechtes Panel). Trägt genau die
/// Anzeige-Daten eines Projekts, ohne es zu öffnen. Schadensgruppen sind bewusst NICHT enthalten.
/// </summary>
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

    /// <summary>Lokales Datum (nur Tag) oder „—".</summary>
    public string ModifiedAtDisplay =>
        ModifiedAtUtc?.ToLocalTime().ToString("dd.MM.yyyy", CultureInfo.CurrentCulture) ?? "—";
}
```

- [ ] **Step 4: ProjectPreviewFactory anlegen**

`src/AuswertungPro.Next.Application/Dashboard/ProjectPreviewFactory.cs`:
```csharp
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.Dashboard;

/// <summary>
/// Baut aus einem geladenen <see cref="Project"/> eine <see cref="ProjectPreview"/> für die
/// Projektübersicht. Reiner Helfer (keine Abhängigkeiten), damit unit-testbar. Kennzahlen kommen
/// aus <see cref="DashboardStatisticsBuilder"/>; Schadensgruppen werden bewusst weggelassen.
/// </summary>
public static class ProjectPreviewFactory
{
    public static ProjectPreview FromProject(Project project, string path)
    {
        var stats = DashboardStatisticsBuilder.Build(project.Data);
        return new ProjectPreview(
            Name: project.Name ?? string.Empty,
            Description: project.Description ?? string.Empty,
            Path: path,
            ModifiedAtUtc: project.ModifiedAtUtc,
            AppVersion: project.AppVersion,
            HoldingCount: stats.TotalHoldings,
            TotalLengthMeters: stats.TotalLengthMeters,
            TotalCost: stats.TotalCost,
            Auftraggeber: Meta(project, "Auftraggeber"),
            Gemeinde: Meta(project, "Gemeinde"),
            Zone: Meta(project, "Zone"),
            Strasse: Meta(project, "Strasse"),
            Bearbeiter: Meta(project, "Bearbeiter"),
            Inspektionsdatum: Meta(project, "InspektionsDatum"),
            AuftragNr: Meta(project, "AuftragNr"),
            Firma: Meta(project, "FirmaName"),
            ConditionClasses: stats.ConditionClasses,
            DnCostGroups: stats.DnCostGroups);
    }

    private static string Meta(Project project, string key)
        => project.Metadata.TryGetValue(key, out var v) ? v ?? string.Empty : string.Empty;
}
```

- [ ] **Step 5: Tests laufen grün**

Run: `dotnet build tests/AuswertungPro.Next.UI.Tests/AuswertungPro.Next.UI.Tests.csproj --no-restore -v q` (0 Fehler), dann
`dotnet test tests/AuswertungPro.Next.UI.Tests/AuswertungPro.Next.UI.Tests.csproj --no-build --filter "FullyQualifiedName~ProjectPreviewFactory"`
Expected: `Bestanden! … erfolgreich: 2`.

- [ ] **Step 6: Commit**

```bash
git add src/AuswertungPro.Next.Application/Dashboard/ProjectPreview.cs \
        src/AuswertungPro.Next.Application/Dashboard/ProjectPreviewFactory.cs \
        tests/AuswertungPro.Next.UI.Tests/ProjectPreviewFactoryTests.cs
git commit -m "feat(overview): ProjectPreview + ProjectPreviewFactory fuer Projekt-Vorschau"
```

---

### Task 2: OverviewPageViewModel — SelectedPreview + Aufbau bei Auswahl + Default-Vorselektion

**Files:**
- Modify: `src/AuswertungPro.Next.UI/ViewModels/Pages/OverviewPageViewModel.cs`

**Interfaces:**
- Consumes: `ProjectPreviewFactory.FromProject(Project, string)`, `ProjectPreview` (Task 1); `_sp.Projects.Load(string) → Result<Project>` mit `.Ok`, `.Value`, `.ErrorMessage`; vorhandenes `ProjectOverviewEntry` (Name, Description, Path, ModifiedAtUtc `DateTime?`, RecordCount).
- Produces: `OverviewPageViewModel.SelectedPreview` (`ProjectPreview?`) — Bindungsquelle für Task 3.

- [ ] **Step 1: Using ergänzen**

In `OverviewPageViewModel.cs` sicherstellen, dass `using AuswertungPro.Next.Application.Dashboard;` vorhanden ist (ist bereits vorhanden — sonst ergänzen). Neu benötigt: `using System.Collections.Generic;` (bereits vorhanden).

- [ ] **Step 2: SelectedPreview-Property + Aufbau**

Direkt nach dem Feld `private ProjectOverviewEntry? _selectedProjectEntry;` (ca. Z. 17–18) neue Property ergänzen:
```csharp
        [ObservableProperty]
        private ProjectPreview? _selectedPreview;
```

`OnSelectedProjectEntryChanged` (aktuell Z. 271–275) so erweitern, dass die Vorschau gebaut wird:
```csharp
    partial void OnSelectedProjectEntryChanged(ProjectOverviewEntry? value)
    {
        (OpenSelectedCommand as RelayCommand)?.NotifyCanExecuteChanged();
        (DeleteSelectedCommand as RelayCommand)?.NotifyCanExecuteChanged();
        BuildPreview(value);
    }
```

Neue private Methode (z.B. unterhalb von `OnSelectedProjectEntryChanged`):
```csharp
    /// <summary>
    /// Baut die rechte Vorschau aus dem gewählten Listeneintrag: lädt das Projekt aus seiner Datei
    /// (ohne es zu öffnen). Schlägt das Laden fehl, wird eine minimale Vorschau aus den Listen-
    /// Metadaten gezeigt — kein Absturz, Panel bleibt nutzbar.
    /// </summary>
    private void BuildPreview(ProjectOverviewEntry? entry)
    {
        if (entry is null)
        {
            SelectedPreview = null;
            return;
        }

        try
        {
            var res = _sp.Projects.Load(entry.Path);
            if (res.Ok && res.Value is not null)
            {
                SelectedPreview = ProjectPreviewFactory.FromProject(res.Value, entry.Path);
                return;
            }
        }
        catch
        {
            // Fällt unten auf die Metadaten-Vorschau zurück.
        }

        SelectedPreview = new ProjectPreview(
            Name: entry.Name,
            Description: entry.Description,
            Path: entry.Path,
            ModifiedAtUtc: entry.ModifiedAtUtc,
            AppVersion: null,
            HoldingCount: entry.RecordCount,
            TotalLengthMeters: 0,
            TotalCost: 0m,
            Auftraggeber: string.Empty,
            Gemeinde: string.Empty,
            Zone: string.Empty,
            Strasse: string.Empty,
            Bearbeiter: string.Empty,
            Inspektionsdatum: string.Empty,
            AuftragNr: string.Empty,
            Firma: string.Empty,
            ConditionClasses: System.Array.Empty<DashboardBucket>(),
            DnCostGroups: System.Array.Empty<DashboardCostBucket>());
    }
```

- [ ] **Step 3: Default-Vorselektion nach dem Filtern**

Am Ende von `ApplyFilter()` (nach der `foreach`-Schleife, Z. 101–102) ergänzen, damit beim Start/Neuladen das oberste (zuletzt verwendete) Projekt vorgewählt ist und rechts erscheint:
```csharp
        if (SelectedProjectEntry is null || !ProjectEntries.Contains(SelectedProjectEntry))
            SelectedProjectEntry = ProjectEntries.FirstOrDefault();
```
(`System.Linq` ist bereits importiert.)

- [ ] **Step 4: Build prüfen**

Run: `dotnet build tests/AuswertungPro.Next.UI.Tests/AuswertungPro.Next.UI.Tests.csproj --no-restore -v q`
Expected: `0 Fehler`. (Reine Verdrahtung; die Mapping-Logik ist in Task 1 getestet. `_sp.Projects.Load` ist DI-gebunden, daher kein VM-Unit-Test.)

- [ ] **Step 5: Commit**

```bash
git add src/AuswertungPro.Next.UI/ViewModels/Pages/OverviewPageViewModel.cs
git commit -m "feat(overview): SelectedPreview beim Anklicken laden + Default-Vorselektion"
```

---

### Task 3: OverviewPage.xaml — rechtes Panel auf SelectedPreview, Gesamtmeter + Stammdaten, Schadensgruppen weg

**Files:**
- Modify: `src/AuswertungPro.Next.UI/Views/Pages/OverviewPage.xaml` (rechtes Panel: `<Border Grid.Column="2" …>` Z. 180–435)

**Interfaces:**
- Consumes: `SelectedPreview` (Task 2) mit Feldern `Name, Description, Path, ModifiedAtDisplay, HoldingCount, TotalLengthMeters, TotalCost, Auftraggeber, Gemeinde, Zone, Strasse, Bearbeiter, Inspektionsdatum, AuftragNr, Firma, ConditionClasses (DashboardBucket: Label, Percent), DnCostGroups (DashboardCostBucket: Label, Cost)`; vorhandenes `OpenSelectedCommand`.

- [ ] **Step 1: Rechtes Panel ersetzen**

Den kompletten rechten `<Border Grid.Column="2" Style="{StaticResource Card}"> … </Border>` (Z. 180–435) durch folgenden Block ersetzen:
```xml
            <!-- ── RECHTS: Projektinfo (Vorschau des gewählten Projekts) ── -->
            <Border Grid.Column="2" Style="{StaticResource Card}">
                <ScrollViewer VerticalScrollBarVisibility="Auto">
                    <StackPanel>
                        <TextBlock Text="Projektinfo" FontSize="15" FontWeight="SemiBold"
                                   Foreground="{DynamicResource TextBrush}" Margin="0,0,0,12"/>

                        <!-- Kopf: Name, Status, Pfad, Beschreibung -->
                        <Border CornerRadius="8" BorderThickness="1"
                                BorderBrush="{DynamicResource BorderLightBrush}"
                                Background="{DynamicResource BgLightBrush}" Padding="14" Margin="0,0,0,12">
                            <StackPanel>
                                <DockPanel Margin="0,0,0,8">
                                    <TextBlock Text="{Binding SelectedPreview.Name}" FontSize="16" FontWeight="Bold"
                                               Foreground="{DynamicResource TextBrush}"/>
                                    <Border DockPanel.Dock="Right" HorizontalAlignment="Right"
                                            CornerRadius="6" Padding="8,3"
                                            Background="{DynamicResource SuccessBrush}">
                                        <TextBlock Text="Projekt gespeichert" FontSize="10" FontWeight="Bold"
                                                   Foreground="White"/>
                                    </Border>
                                </DockPanel>
                                <TextBlock Text="{Binding SelectedPreview.Description}" TextWrapping="Wrap"
                                           FontSize="12" Foreground="{DynamicResource MutedBrush}" Margin="0,0,0,8"/>
                                <TextBlock Text="{Binding SelectedPreview.Path}" FontSize="11"
                                           Foreground="{DynamicResource MutedBrush}"/>
                            </StackPanel>
                        </Border>

                        <!-- Kennzahl-Kacheln -->
                        <TextBlock Text="Kennzahlen" FontSize="13" FontWeight="SemiBold"
                                   Foreground="{DynamicResource TextBrush}" Margin="0,0,0,8"/>
                        <Grid Margin="0,0,0,12">
                            <Grid.ColumnDefinitions>
                                <ColumnDefinition Width="*"/>
                                <ColumnDefinition Width="8"/>
                                <ColumnDefinition Width="*"/>
                                <ColumnDefinition Width="8"/>
                                <ColumnDefinition Width="*"/>
                                <ColumnDefinition Width="8"/>
                                <ColumnDefinition Width="*"/>
                            </Grid.ColumnDefinitions>

                            <Border Grid.Column="0" CornerRadius="8" Padding="12,10"
                                    Background="{DynamicResource AccentSubtleBrush}">
                                <StackPanel HorizontalAlignment="Center">
                                    <TextBlock Text="{Binding SelectedPreview.HoldingCount}"
                                               FontSize="22" FontWeight="Bold" HorizontalAlignment="Center"
                                               Foreground="{DynamicResource AccentBrush}"/>
                                    <TextBlock Text="Haltungen" FontSize="10" HorizontalAlignment="Center"
                                               Foreground="{DynamicResource MutedBrush}"/>
                                </StackPanel>
                            </Border>

                            <Border Grid.Column="2" CornerRadius="8" Padding="12,10"
                                    Background="{DynamicResource BgLightBrush}">
                                <StackPanel HorizontalAlignment="Center">
                                    <TextBlock Text="{Binding SelectedPreview.TotalLengthMeters, StringFormat={}{0:N0}}"
                                               FontSize="22" FontWeight="Bold" HorizontalAlignment="Center"
                                               Foreground="{DynamicResource TextBrush}"/>
                                    <TextBlock Text="Meter" FontSize="10" HorizontalAlignment="Center"
                                               Foreground="{DynamicResource MutedBrush}"/>
                                </StackPanel>
                            </Border>

                            <Border Grid.Column="4" CornerRadius="8" Padding="12,10"
                                    Background="{DynamicResource BgLightBrush}">
                                <StackPanel HorizontalAlignment="Center">
                                    <TextBlock Text="{Binding SelectedPreview.TotalCost, StringFormat={}{0:N0}}"
                                               FontSize="16" FontWeight="SemiBold" HorizontalAlignment="Center"
                                               Foreground="{DynamicResource TextBrush}"/>
                                    <TextBlock Text="CHF" FontSize="10" HorizontalAlignment="Center"
                                               Foreground="{DynamicResource MutedBrush}"/>
                                </StackPanel>
                            </Border>

                            <Border Grid.Column="6" CornerRadius="8" Padding="12,10"
                                    Background="{DynamicResource BgLightBrush}">
                                <StackPanel HorizontalAlignment="Center">
                                    <TextBlock Text="{Binding SelectedPreview.ModifiedAtDisplay}"
                                               FontSize="14" FontWeight="SemiBold" HorizontalAlignment="Center"
                                               Foreground="{DynamicResource TextBrush}"/>
                                    <TextBlock Text="gespeichert" FontSize="10" HorizontalAlignment="Center"
                                               Foreground="{DynamicResource MutedBrush}"/>
                                </StackPanel>
                            </Border>
                        </Grid>

                        <!-- Stammdaten -->
                        <TextBlock Text="Projektdaten" FontSize="13" FontWeight="SemiBold"
                                   Foreground="{DynamicResource TextBrush}" Margin="0,0,0,8"/>
                        <Border CornerRadius="8" Padding="14" Margin="0,0,0,12"
                                Background="{DynamicResource BgLightBrush}"
                                BorderBrush="{DynamicResource BorderLightBrush}" BorderThickness="1">
                            <Grid>
                                <Grid.ColumnDefinitions>
                                    <ColumnDefinition Width="Auto"/>
                                    <ColumnDefinition Width="*"/>
                                    <ColumnDefinition Width="24"/>
                                    <ColumnDefinition Width="Auto"/>
                                    <ColumnDefinition Width="*"/>
                                </Grid.ColumnDefinitions>
                                <Grid.RowDefinitions>
                                    <RowDefinition Height="Auto"/>
                                    <RowDefinition Height="Auto"/>
                                    <RowDefinition Height="Auto"/>
                                    <RowDefinition Height="Auto"/>
                                </Grid.RowDefinitions>
                                <Grid.Resources>
                                    <Style TargetType="TextBlock" x:Key="MetaLabel">
                                        <Setter Property="FontSize" Value="11"/>
                                        <Setter Property="Foreground" Value="{DynamicResource MutedBrush}"/>
                                        <Setter Property="Margin" Value="0,3,10,3"/>
                                    </Style>
                                    <Style TargetType="TextBlock" x:Key="MetaValue">
                                        <Setter Property="FontSize" Value="11"/>
                                        <Setter Property="FontWeight" Value="SemiBold"/>
                                        <Setter Property="Foreground" Value="{DynamicResource TextBrush}"/>
                                        <Setter Property="Margin" Value="0,3,0,3"/>
                                        <Setter Property="TextTrimming" Value="CharacterEllipsis"/>
                                    </Style>
                                </Grid.Resources>

                                <TextBlock Grid.Row="0" Grid.Column="0" Text="Auftraggeber" Style="{StaticResource MetaLabel}"/>
                                <TextBlock Grid.Row="0" Grid.Column="1" Text="{Binding SelectedPreview.Auftraggeber}" Style="{StaticResource MetaValue}"/>
                                <TextBlock Grid.Row="0" Grid.Column="3" Text="Gemeinde" Style="{StaticResource MetaLabel}"/>
                                <TextBlock Grid.Row="0" Grid.Column="4" Text="{Binding SelectedPreview.Gemeinde}" Style="{StaticResource MetaValue}"/>

                                <TextBlock Grid.Row="1" Grid.Column="0" Text="Zone" Style="{StaticResource MetaLabel}"/>
                                <TextBlock Grid.Row="1" Grid.Column="1" Text="{Binding SelectedPreview.Zone}" Style="{StaticResource MetaValue}"/>
                                <TextBlock Grid.Row="1" Grid.Column="3" Text="Straße" Style="{StaticResource MetaLabel}"/>
                                <TextBlock Grid.Row="1" Grid.Column="4" Text="{Binding SelectedPreview.Strasse}" Style="{StaticResource MetaValue}"/>

                                <TextBlock Grid.Row="2" Grid.Column="0" Text="Bearbeiter" Style="{StaticResource MetaLabel}"/>
                                <TextBlock Grid.Row="2" Grid.Column="1" Text="{Binding SelectedPreview.Bearbeiter}" Style="{StaticResource MetaValue}"/>
                                <TextBlock Grid.Row="2" Grid.Column="3" Text="Inspektionsdatum" Style="{StaticResource MetaLabel}"/>
                                <TextBlock Grid.Row="2" Grid.Column="4" Text="{Binding SelectedPreview.Inspektionsdatum}" Style="{StaticResource MetaValue}"/>

                                <TextBlock Grid.Row="3" Grid.Column="0" Text="Auftrag-Nr." Style="{StaticResource MetaLabel}"/>
                                <TextBlock Grid.Row="3" Grid.Column="1" Text="{Binding SelectedPreview.AuftragNr}" Style="{StaticResource MetaValue}"/>
                                <TextBlock Grid.Row="3" Grid.Column="3" Text="Firma" Style="{StaticResource MetaLabel}"/>
                                <TextBlock Grid.Row="3" Grid.Column="4" Text="{Binding SelectedPreview.Firma}" Style="{StaticResource MetaValue}"/>
                            </Grid>
                        </Border>

                        <!-- Auswertung: Zustandsklassen + DN/Kosten (KEINE Schadensgruppen) -->
                        <Border CornerRadius="8" Padding="12" Margin="0,0,0,12"
                                Background="{DynamicResource BgLightBrush}"
                                BorderBrush="{DynamicResource BorderLightBrush}" BorderThickness="1">
                            <StackPanel>
                                <DockPanel Margin="0,0,0,10">
                                    <TextBlock Text="Auswertung" FontSize="13" FontWeight="SemiBold"
                                               Foreground="{DynamicResource TextBrush}"/>
                                    <TextBlock DockPanel.Dock="Right" FontSize="10"
                                               Foreground="{DynamicResource MutedBrush}" HorizontalAlignment="Right">
                                        <Run Text="{Binding SelectedPreview.TotalLengthMeters, StringFormat={}{0:N0} m, Mode=OneWay}"/>
                                        <Run Text=" / "/>
                                        <Run Text="{Binding SelectedPreview.TotalCost, StringFormat={}{0:N0} CHF, Mode=OneWay}"/>
                                    </TextBlock>
                                </DockPanel>
                                <Grid>
                                    <Grid.ColumnDefinitions>
                                        <ColumnDefinition Width="*"/>
                                        <ColumnDefinition Width="16"/>
                                        <ColumnDefinition Width="*"/>
                                    </Grid.ColumnDefinitions>

                                    <StackPanel Grid.Column="0">
                                        <TextBlock Text="Zustandsklassen" FontSize="11" FontWeight="SemiBold"
                                                   Foreground="{DynamicResource TextSecondaryBrush}" Margin="0,0,0,6"/>
                                        <ItemsControl ItemsSource="{Binding SelectedPreview.ConditionClasses}">
                                            <ItemsControl.ItemTemplate>
                                                <DataTemplate>
                                                    <Grid Margin="0,0,0,5">
                                                        <Grid.ColumnDefinitions>
                                                            <ColumnDefinition Width="28"/>
                                                            <ColumnDefinition Width="*"/>
                                                            <ColumnDefinition Width="44"/>
                                                        </Grid.ColumnDefinitions>
                                                        <TextBlock Text="{Binding Label}" FontSize="10" Foreground="{DynamicResource TextBrush}"/>
                                                        <ProgressBar Grid.Column="1" Height="6" Maximum="100" Value="{Binding Percent}"
                                                                     BorderThickness="0" Background="{DynamicResource BorderBrush}"
                                                                     Foreground="{DynamicResource Severity3Brush}" VerticalAlignment="Center"/>
                                                        <TextBlock Grid.Column="2" Text="{Binding Percent, StringFormat={}{0:0.#}%}"
                                                                   FontSize="10" Foreground="{DynamicResource MutedBrush}" HorizontalAlignment="Right"/>
                                                    </Grid>
                                                </DataTemplate>
                                            </ItemsControl.ItemTemplate>
                                        </ItemsControl>
                                    </StackPanel>

                                    <StackPanel Grid.Column="2">
                                        <TextBlock Text="DN / Kosten" FontSize="11" FontWeight="SemiBold"
                                                   Foreground="{DynamicResource TextSecondaryBrush}" Margin="0,0,0,6"/>
                                        <ItemsControl ItemsSource="{Binding SelectedPreview.DnCostGroups}">
                                            <ItemsControl.ItemTemplate>
                                                <DataTemplate>
                                                    <Grid Margin="0,0,0,5">
                                                        <Grid.ColumnDefinitions>
                                                            <ColumnDefinition Width="54"/>
                                                            <ColumnDefinition Width="*"/>
                                                            <ColumnDefinition Width="58"/>
                                                        </Grid.ColumnDefinitions>
                                                        <TextBlock Text="{Binding Label}" FontSize="10" Foreground="{DynamicResource TextBrush}"/>
                                                        <ProgressBar Grid.Column="1" Height="6" Maximum="100" Value="{Binding Percent}"
                                                                     BorderThickness="0" Background="{DynamicResource BorderBrush}"
                                                                     Foreground="{DynamicResource SuccessBrush}" VerticalAlignment="Center"/>
                                                        <TextBlock Grid.Column="2" Text="{Binding Cost, StringFormat={}{0:N0}}"
                                                                   FontSize="10" Foreground="{DynamicResource MutedBrush}" HorizontalAlignment="Right"/>
                                                    </Grid>
                                                </DataTemplate>
                                            </ItemsControl.ItemTemplate>
                                        </ItemsControl>
                                    </StackPanel>
                                </Grid>
                            </StackPanel>
                        </Border>

                        <!-- Öffnen -->
                        <Button Content="Öffnen" Command="{Binding OpenSelectedCommand}"
                                Style="{StaticResource PrimaryButton}" Padding="14,6"
                                HorizontalAlignment="Left" Margin="0,0,0,12"/>

                        <!-- Drag & Drop Hinweis -->
                        <Border CornerRadius="8" Padding="12" BorderThickness="2"
                                BorderBrush="{DynamicResource BorderLightBrush}" Background="Transparent">
                            <StackPanel HorizontalAlignment="Center">
                                <TextBlock Text="Projekt-Datei hierher ziehen" FontSize="12"
                                           HorizontalAlignment="Center" Foreground="{DynamicResource MutedBrush}"/>
                                <TextBlock Text=".json Datei oder Ordner" FontSize="10"
                                           HorizontalAlignment="Center" Foreground="{DynamicResource MutedBrush}" Margin="0,2,0,0"/>
                            </StackPanel>
                        </Border>
                    </StackPanel>
                </ScrollViewer>
            </Border>
```

- [ ] **Step 2: Build prüfen (XAML kompiliert)**

Run: `dotnet build src/AuswertungPro.Next.UI/AuswertungPro.Next.UI.csproj --no-restore -v q 2>&1 | grep -iE "error|Fehler" | head`
Expected: `0 Fehler`. (Falls „error MC…"/„error CS" bei Bindings → Tippfehler im Binding-Pfad korrigieren.)

- [ ] **Step 3: Commit**

```bash
git add src/AuswertungPro.Next.UI/Views/Pages/OverviewPage.xaml
git commit -m "feat(overview): rechtes Panel als Projekt-Vorschau (Gesamtmeter, Stammdaten, ohne Schadensgruppen)"
```

---

### Task 4: ProjectPage.xaml — Block „Firma & Kontakt" entfernen

**Files:**
- Modify: `src/AuswertungPro.Next.UI/Views/Pages/ProjectPage.xaml` (Z. 92–100)

**Interfaces:**
- Consumes: nichts Neues. Produces: nichts Neues.

- [ ] **Step 1: Block entfernen**

Diesen Abschnitt (Z. 92–100) …
```xml
                    <!-- Sanieren & Eigentuemer entfernt -->

                    <TextBlock Text="Firma &amp; Kontakt" Margin="0,12,0,6" FontWeight="SemiBold" Foreground="{DynamicResource TextBrush}"/>
                    <TextBlock Text="Adresse" Foreground="{DynamicResource MutedBrush}" Margin="0,4,0,4"/>
                    <TextBox Text="{Binding Project.Metadata[FirmaAdresse], Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}"/>
                    <TextBlock Text="Telefon" Foreground="{DynamicResource MutedBrush}" Margin="0,8,0,4"/>
                    <TextBox Text="{Binding Project.Metadata[FirmaTelefon], Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}"/>
                    <TextBlock Text="E-Mail" Foreground="{DynamicResource MutedBrush}" Margin="0,8,0,4"/>
                    <TextBox Text="{Binding Project.Metadata[FirmaEmail], Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}"/>
```
… vollständig löschen. Die darauffolgende Records-Zeile bleibt:
```xml
                    <TextBlock Text="{Binding Project.Data.Count, StringFormat=Records: {0}}" Margin="0,12,0,0" Foreground="{DynamicResource MutedBrush}"/>
```

- [ ] **Step 2: Build prüfen**

Run: `dotnet build src/AuswertungPro.Next.UI/AuswertungPro.Next.UI.csproj --no-restore -v q 2>&1 | grep -iE "error|Fehler" | head`
Expected: `0 Fehler`.

- [ ] **Step 3: Commit**

```bash
git add src/AuswertungPro.Next.UI/Views/Pages/ProjectPage.xaml
git commit -m "feat(projektinfos): Block 'Firma & Kontakt' entfernt"
```

---

### Task 5: Auftraggeber-Default „Abwasser Uri" bei Neuanlage (+ Test)

**Files:**
- Create: `src/AuswertungPro.Next.Application/Projects/NewProjectDraftFactory.cs`
- Modify: `src/AuswertungPro.Next.UI/ViewModels/ShellViewModel.cs` (`StartNewProjectDraft`, Z. 337–350)
- Test: `tests/AuswertungPro.Next.UI.Tests/NewProjectDraftFactoryTests.cs`

**Interfaces:**
- Consumes: `AuswertungPro.Next.Domain.Models.Project`.
- Produces: `NewProjectDraftFactory.Create() → Project` (leerer Name, `Metadata["Auftraggeber"] == "Abwasser Uri"`).

- [ ] **Step 1: Failing test schreiben**

`tests/AuswertungPro.Next.UI.Tests/NewProjectDraftFactoryTests.cs`:
```csharp
using AuswertungPro.Next.Application.Projects;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

public sealed class NewProjectDraftFactoryTests
{
    [Fact]
    public void Create_setzt_auftraggeber_default_und_leeren_namen()
    {
        var project = NewProjectDraftFactory.Create();

        Assert.Equal("Abwasser Uri", project.Metadata["Auftraggeber"]);
        Assert.Equal(string.Empty, project.Name);
    }
}
```

- [ ] **Step 2: Test läuft rot**

Run: `dotnet build tests/AuswertungPro.Next.UI.Tests/AuswertungPro.Next.UI.Tests.csproj --no-restore -v q`
Expected: FEHLER — `NewProjectDraftFactory` existiert nicht (CS0246).

- [ ] **Step 3: Factory anlegen**

`src/AuswertungPro.Next.Application/Projects/NewProjectDraftFactory.cs`:
```csharp
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.Projects;

/// <summary>
/// Erzeugt ein Draft-Projekt für die Neuanlage. Der Auftraggeber ist fast immer „Abwasser Uri" und
/// wird daher vorbelegt (frei änderbar). Reiner Helfer, damit der Default unit-testbar ist.
/// </summary>
public static class NewProjectDraftFactory
{
    public const string DefaultAuftraggeber = "Abwasser Uri";

    public static Project Create()
    {
        var project = new Project { Name = string.Empty };
        project.Metadata["Auftraggeber"] = DefaultAuftraggeber;
        return project;
    }
}
```

- [ ] **Step 4: ShellViewModel.StartNewProjectDraft umstellen**

In `src/AuswertungPro.Next.UI/ViewModels/ShellViewModel.cs`, Methode `StartNewProjectDraft` (Z. 337–350): die Zeile
```csharp
        ReplaceProject(new Project { Name = string.Empty });
```
ersetzen durch
```csharp
        ReplaceProject(AuswertungPro.Next.Application.Projects.NewProjectDraftFactory.Create());
```
(Voll qualifiziert, um Using-Verwaltung in der großen Datei zu vermeiden.)

- [ ] **Step 5: Tests grün + Build**

Run: `dotnet build tests/AuswertungPro.Next.UI.Tests/AuswertungPro.Next.UI.Tests.csproj --no-restore -v q` (0 Fehler), dann
`dotnet test tests/AuswertungPro.Next.UI.Tests/AuswertungPro.Next.UI.Tests.csproj --no-build --filter "FullyQualifiedName~NewProjectDraftFactory"`
Expected: `Bestanden! … erfolgreich: 1`.

- [ ] **Step 6: Commit (ShellViewModel.cs chirurgisch stagen — Datei ist bereits dirty!)**

`ShellViewModel.cs` enthält Fremd-Hunks. Nur den eigenen Hunk stagen:
```bash
git add src/AuswertungPro.Next.Application/Projects/NewProjectDraftFactory.cs \
        tests/AuswertungPro.Next.UI.Tests/NewProjectDraftFactoryTests.cs
# ShellViewModel.cs: prüfen, ob weitere (fremde) Hunks vorhanden sind
git diff --stat -- src/AuswertungPro.Next.UI/ViewModels/ShellViewModel.cs
```
Wenn `git diff -- …ShellViewModel.cs` NUR den eigenen `ReplaceProject(...)`-Hunk zeigt → `git add` der Datei ist ok. Andernfalls nur den eigenen Hunk per Patch stagen:
```bash
SCRATCH="$(git rev-parse --show-toplevel)/.tmp"
mkdir -p "$SCRATCH"
git diff -- src/AuswertungPro.Next.UI/ViewModels/ShellViewModel.cs > "$SCRATCH/shell.patch"
# shell.patch auf den einen ReplaceProject-Hunk reduzieren (Header + betroffener @@-Block), dann:
git apply --cached "$SCRATCH/shell.hunk.patch"
```
Danach committen:
```bash
git commit -m "feat(projekt): Auftraggeber-Default 'Abwasser Uri' bei Neuanlage"
```

---

## Self-Review

**Spec-Abdeckung:** (1) Einfachklick→Vorschau: T2+T3. Doppelklick öffnet: bereits vorhanden (unverändert). Gesamtmeter: T3-Kachel (`TotalLengthMeters`). Professionelle Seite: T3 (Kopf/Kacheln/Stammdaten/Auswertung). Schadensgruppen weg: T3 (Spalte entfällt). (2) Firma & Kontakt weg: T4. Auftraggeber-Default: T5. `ProjectPreview`/`Factory`: T1. → Alle Spec-Punkte abgedeckt.

**Placeholder-Scan:** Kein TBD/TODO; alle Code-/XAML-Blöcke vollständig.

**Typ-Konsistenz:** `ProjectPreview`-Feldnamen in T1 = Bindungspfade in T3 (Name, Description, Path, ModifiedAtDisplay, HoldingCount, TotalLengthMeters, TotalCost, Auftraggeber, Gemeinde, Zone, Strasse, Bearbeiter, Inspektionsdatum, AuftragNr, Firma, ConditionClasses, DnCostGroups). `DashboardBucket.Label/Percent`, `DashboardCostBucket.Label/Cost/Percent` stimmen mit den ItemTemplates überein. `Result.Ok/.Value/.ErrorMessage` und `_sp.Projects.Load` wie in ShellViewModel verwendet.
