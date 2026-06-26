# Projekt-Eröffnung (Start-Bildschirm + Auto-Projektordner) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Die Projekt-Eröffnung in einen Start-Bildschirm-Gateway umbauen und „Neues Projekt" automatisch einen Projektordner unter einem in den Einstellungen hinterlegten Verzeichnis anlegen lassen.

**Architecture:** Die bestehende Single-Window-Shell (`ShellViewModel` + `MainWindow.xaml`) bekommt einen `ShellMode` (Launcher / Draft / Workspace). Menü + Nav + Shortcuts sind nur im Workspace aktiv; in Launcher/Draft füllt die `OverviewPage` bzw. `ProjectPage` das Fenster. Eine neue pure Logikklasse berechnet den Zielordner aus dem Projektnamen.

**Tech Stack:** WPF / .NET 10, MVVM (CommunityToolkit.Mvvm), xUnit. Tests für pure Logik direkt; UI-/Shell-Verdrahtung über Datei-Inhalt-Guard-Tests (etablierte Muster: `ShellNavigationPolicyTests`, `UiArchitectureGuardTests`).

## Global Constraints

- Kommentare auf Deutsch (CLAUDE.md).
- Keine neuen NuGet-Pakete.
- Bestehende Öffnen/Speichern/Lade-Logik (`TryOpenProject`, `TrySaveProject`, `_sp.Projects`) nicht verändern — nur aufrufen.
- Medienverteilung (`MediaDistributionService` / `PostImportFolderAsync`) **nicht** anfassen (Spec D6, out of scope).
- Sanitizer ist `ProjectPathResolver.SanitizePathSegment` (public, Application/Common) — NICHT das private `ShellViewModel.MakeSafeFileName`.
- Codex zerlegt parallel das `PlayerWindow` auf demselben Branch. KEINE PlayerWindow-Dateien anfassen. Vor `dotnet build`/`dotnet test` prüfen, dass Codex nicht gerade baut (sonst obj/bin-Kollision). Pro Task **einzeln** committen (`git add <pfade>`), nie `git add -A`.
- Build: `dotnet build AuswertungPro.sln`. Test (gezielt): `dotnet test <projekt.csproj> --filter "FullyQualifiedName~<Name>"`.

---

### Task 1: `NewProjectFolderPlanner` (pure Logik)

Berechnet aus Basisverzeichnis + Projektname den Zielordner und den `projekt.json`-Pfad, entschärft ungültige Zeichen und löst Namenskollisionen per Suffix `-2`, `-3`, … auf.

**Files:**
- Create: `src/AuswertungPro.Next.Application/Common/NewProjectFolderPlanner.cs`
- Test: `tests/AuswertungPro.Next.Infrastructure.Tests/Common/NewProjectFolderPlannerTests.cs`

**Interfaces:**
- Consumes: `ProjectPathResolver.SanitizePathSegment(string?)` (bereits vorhanden).
- Produces:
  - `record NewProjectFolderPlan(string FolderPath, string ProjectFilePath)`
  - `static NewProjectFolderPlan NewProjectFolderPlanner.Plan(string baseDirectory, string projectName, Func<string,bool> directoryExists)`

- [ ] **Step 1: Write the failing test**

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using AuswertungPro.Next.Application.Common;

namespace AuswertungPro.Next.Infrastructure.Tests.Common;

public sealed class NewProjectFolderPlannerTests
{
    private static Func<string, bool> Existing(params string[] dirs)
    {
        var set = new HashSet<string>(dirs, StringComparer.OrdinalIgnoreCase);
        return set.Contains;
    }

    [Fact]
    public void Plan_builds_folder_and_projectfile_from_name()
    {
        var plan = NewProjectFolderPlanner.Plan(@"D:\Projekt", "Meiental_Husen", Existing());

        Assert.Equal(Path.Combine(@"D:\Projekt", "Meiental_Husen"), plan.FolderPath);
        Assert.Equal(Path.Combine(@"D:\Projekt", "Meiental_Husen", "projekt.json"), plan.ProjectFilePath);
    }

    [Fact]
    public void Plan_sanitizes_invalid_characters()
    {
        var plan = NewProjectFolderPlanner.Plan(@"D:\Projekt", "A/B:C", Existing());

        Assert.Equal(Path.Combine(@"D:\Projekt", "A_B_C"), plan.FolderPath);
    }

    [Fact]
    public void Plan_appends_suffix_on_collision()
    {
        var taken = Path.Combine(@"D:\Projekt", "Meiental_Husen");
        var plan = NewProjectFolderPlanner.Plan(@"D:\Projekt", "Meiental_Husen", Existing(taken));

        Assert.Equal(Path.Combine(@"D:\Projekt", "Meiental_Husen-2"), plan.FolderPath);
    }

    [Fact]
    public void Plan_increments_suffix_until_free()
    {
        var taken1 = Path.Combine(@"D:\Projekt", "Husen");
        var taken2 = Path.Combine(@"D:\Projekt", "Husen-2");
        var plan = NewProjectFolderPlanner.Plan(@"D:\Projekt", "Husen", Existing(taken1, taken2));

        Assert.Equal(Path.Combine(@"D:\Projekt", "Husen-3"), plan.FolderPath);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/AuswertungPro.Next.Infrastructure.Tests/AuswertungPro.Next.Infrastructure.Tests.csproj --filter "FullyQualifiedName~NewProjectFolderPlanner"`
Expected: FAIL — `NewProjectFolderPlanner` existiert nicht (Compile-Fehler).

- [ ] **Step 3: Write minimal implementation**

```csharp
using System;
using System.IO;

namespace AuswertungPro.Next.Application.Common;

/// <summary>Ergebnis der Zielordner-Planung fuer ein neues Projekt.</summary>
public sealed record NewProjectFolderPlan(string FolderPath, string ProjectFilePath);

/// <summary>
/// Berechnet aus Basisverzeichnis + Projektname den Projektordner und den
/// projekt.json-Pfad. Pure (kein Dateisystem-Zugriff): die Existenzpruefung
/// kommt als Delegate, damit die Logik unit-testbar bleibt.
/// </summary>
public static class NewProjectFolderPlanner
{
    public const string ProjectFileName = "projekt.json";

    public static NewProjectFolderPlan Plan(
        string baseDirectory,
        string projectName,
        Func<string, bool> directoryExists)
    {
        ArgumentNullException.ThrowIfNull(directoryExists);

        var safeName = ProjectPathResolver.SanitizePathSegment(projectName);
        var candidate = Path.Combine(baseDirectory, safeName);

        // Kollision: -2, -3, ... bis ein freier Ordnername gefunden ist.
        var counter = 2;
        while (directoryExists(candidate))
        {
            candidate = Path.Combine(baseDirectory, $"{safeName}-{counter}");
            counter++;
        }

        return new NewProjectFolderPlan(candidate, Path.Combine(candidate, ProjectFileName));
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/AuswertungPro.Next.Infrastructure.Tests/AuswertungPro.Next.Infrastructure.Tests.csproj --filter "FullyQualifiedName~NewProjectFolderPlanner"`
Expected: PASS (4 Tests grün).

- [ ] **Step 5: Commit**

```bash
git add src/AuswertungPro.Next.Application/Common/NewProjectFolderPlanner.cs tests/AuswertungPro.Next.Infrastructure.Tests/Common/NewProjectFolderPlannerTests.cs
git commit -m "feat: NewProjectFolderPlanner (Zielordner aus Projektname, Kollisions-Suffix)"
```

---

### Task 2: `AppSettings.ProjectsRootDirectory` (neue Einstellung)

Speichert das Basisverzeichnis für neue Projekte.

**Files:**
- Modify: `src/AuswertungPro.Next.UI/AppSettings.cs:26` (nach `LastProjectPath`)
- Test: `tests/AuswertungPro.Next.UI.Tests/AppSettingsProjectsRootTests.cs`

**Interfaces:**
- Produces: `string? AppSettings.ProjectsRootDirectory { get; set; }`

- [ ] **Step 1: Write the failing test**

```csharp
using System.Text.Json;
using AuswertungPro.Next.UI;

namespace AuswertungPro.Next.UI.Tests;

public sealed class AppSettingsProjectsRootTests
{
    [Fact]
    public void ProjectsRootDirectory_survives_json_roundtrip()
    {
        var settings = new AppSettings { ProjectsRootDirectory = @"D:\Projekt" };

        var json = JsonSerializer.Serialize(settings);
        var restored = JsonSerializer.Deserialize<AppSettings>(json);

        Assert.NotNull(restored);
        Assert.Equal(@"D:\Projekt", restored!.ProjectsRootDirectory);
    }

    [Fact]
    public void ProjectsRootDirectory_defaults_to_null()
        => Assert.Null(new AppSettings().ProjectsRootDirectory);
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/AuswertungPro.Next.UI.Tests/AuswertungPro.Next.UI.Tests.csproj --filter "FullyQualifiedName~AppSettingsProjectsRoot"`
Expected: FAIL — `ProjectsRootDirectory` existiert nicht (Compile-Fehler).

- [ ] **Step 3: Write minimal implementation**

In `src/AuswertungPro.Next.UI/AppSettings.cs`, direkt nach Zeile 26 (`public string? LastProjectPath { get; set; }`) einfügen:

```csharp
    // Basisverzeichnis fuer neu angelegte Projekte. Leer = beim ersten Anlegen
    // wird einmalig danach gefragt (Vorschlag D:\Projekt) und hier gespeichert.
    public string? ProjectsRootDirectory { get; set; }
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/AuswertungPro.Next.UI.Tests/AuswertungPro.Next.UI.Tests.csproj --filter "FullyQualifiedName~AppSettingsProjectsRoot"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/AuswertungPro.Next.UI/AppSettings.cs tests/AuswertungPro.Next.UI.Tests/AppSettingsProjectsRootTests.cs
git commit -m "feat: AppSettings.ProjectsRootDirectory (Basisverzeichnis neue Projekte)"
```

---

### Task 3: `ShellMode` + Shell-Zustandsmaschine (`ShellViewModel`)

Führt die drei Modi ein, baut „Uebersicht" aus dem Menü, repointet „Neues Projekt" auf den Draft-Flow und ergänzt „Projekt wechseln" sowie das Anlegen aus dem Draft.

**Files:**
- Modify: `src/AuswertungPro.Next.UI/ViewModels/ShellViewModel.cs`
- Modify: `tests/AuswertungPro.Next.UI.Tests/ShellNavigationPolicyTests.cs:55` (InlineData „Uebersicht" entfernen)
- Test: `tests/AuswertungPro.Next.UI.Tests/ProjektEroeffnungShellGuardTests.cs`

**Interfaces:**
- Consumes: `NewProjectFolderPlanner.Plan(...)` (Task 1), `AppSettings.ProjectsRootDirectory` (Task 2).
- Produces (auf `ShellViewModel`):
  - `enum ShellMode { Launcher, Draft, Workspace }`
  - `ShellMode CurrentMode { get; }` (ObservableProperty)
  - `bool IsMenuVisible { get; }` (== `CurrentMode == ShellMode.Workspace`)
  - `IRelayCommand SwitchProjectCommand { get; }`
  - `void EnterLauncher()`, `void StartNewProjectDraft()`, `void EnterWorkspaceOn(string navTitle)`
  - `bool CreateProjectFromDraft()`

- [ ] **Step 1: Write the failing guard test**

```csharp
using System;
using System.IO;
using System.Linq;

namespace AuswertungPro.Next.UI.Tests;

public sealed class ProjektEroeffnungShellGuardTests
{
    private static string ShellSource()
        => File.ReadAllText(RepoFile("src", "AuswertungPro.Next.UI", "ViewModels", "ShellViewModel.cs"));

    [Fact]
    public void Shell_defines_three_modes_and_menu_visibility()
    {
        var src = ShellSource();
        Assert.Contains("enum ShellMode", src);
        Assert.Contains("Launcher", src);
        Assert.Contains("Draft", src);
        Assert.Contains("Workspace", src);
        Assert.Contains("public bool IsMenuVisible", src);
    }

    [Fact]
    public void Shell_has_switch_and_draft_flow()
    {
        var src = ShellSource();
        Assert.Contains("SwitchProjectCommand", src);
        Assert.Contains("public void StartNewProjectDraft", src);
        Assert.Contains("public bool CreateProjectFromDraft", src);
        Assert.Contains("public void EnterWorkspaceOn", src);
        Assert.Contains("NewProjectFolderPlanner.Plan", src);
    }

    [Fact]
    public void Shell_no_longer_registers_uebersicht_navitem()
    {
        var src = ShellSource();
        Assert.DoesNotContain("\"Uebersicht\", () => new Pages.OverviewPageViewModel", src);
    }

    internal static string RepoFile(params string[] parts)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(new[] { dir.FullName }.Concat(parts).ToArray());
            if (File.Exists(candidate)) return candidate;
            dir = dir.Parent;
        }
        throw new FileNotFoundException("Repo-Datei nicht gefunden.", Path.Combine(parts));
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/AuswertungPro.Next.UI.Tests/AuswertungPro.Next.UI.Tests.csproj --filter "FullyQualifiedName~ProjektEroeffnungShellGuard"`
Expected: FAIL — die gesuchten Strings existieren noch nicht.

- [ ] **Step 3a: ShellMode-Enum + Property + Befehl-Felder**

In `ShellViewModel.cs`, oberhalb der `ShellNavigationPolicy`-Klasse (vor Zeile 13) einfügen:

```csharp
public enum ShellMode
{
    Launcher,
    Draft,
    Workspace
}
```

Nach `[ObservableProperty] private object? _currentPage;` (Zeile 42) einfügen:

```csharp
    [ObservableProperty] private ShellMode _currentMode = ShellMode.Launcher;

    /// <summary>Menue/Nav/Shortcuts nur im Workspace sichtbar.</summary>
    public bool IsMenuVisible => CurrentMode == ShellMode.Workspace;

    partial void OnCurrentModeChanged(ShellMode value) => OnPropertyChanged(nameof(IsMenuVisible));
```

Bei den Command-Properties (nach Zeile 50, `OpenPriceCatalogCommand` etc.) ergänzen:

```csharp
    public IRelayCommand SwitchProjectCommand { get; }
```

- [ ] **Step 3b: Befehle verdrahten + Startup auf Launcher**

In `ShellViewModel`-Konstruktor: `NewProjectCommand` und `SaveCommand` ersetzen und `SwitchProjectCommand` ergänzen. Ersetze Zeile 99–100:

```csharp
        SaveCommand = new RelayCommand(SaveProject);
        NewProjectCommand = new RelayCommand(NewProject);
```

durch:

```csharp
        SaveCommand = new RelayCommand(SaveProject, () => CurrentMode == ShellMode.Workspace);
        NewProjectCommand = new RelayCommand(StartNewProjectDraft);
        SwitchProjectCommand = new RelayCommand(SwitchProject);
```

Ersetze den Startup-Block Zeile 107–108:

```csharp
        SelectedNavItem = NavItems[0];
        SetCurrentPage(SelectedNavItem.CreatePage());
```

durch:

```csharp
        EnterLauncher();
```

In der `NavItems`-Liste den „Uebersicht"-Eintrag (Zeile 83) **entfernen** (die OverviewPage ist jetzt der Launcher, kein Menüpunkt):

```csharp
            new("", "Uebersicht", () => new Pages.OverviewPageViewModel(this, _sp), canOpenWithoutProject: true),
```

- [ ] **Step 3c: Modus-Methoden + Draft-Anlegen**

Ersetze die bestehende `NewProject()`-Methode (Zeile 292–320) komplett durch die neuen Methoden:

```csharp
    /// <summary>Zurueck zum Start-Bildschirm (Projektauswahl).</summary>
    public void EnterLauncher()
    {
        _suppressLeaveGuard = true;
        SelectedNavItem = null;
        _suppressLeaveGuard = false;
        _navItemBeforeChange = null;
        CurrentMode = ShellMode.Launcher;
        ResetProjectReady();
        SetCurrentPage(new Pages.OverviewPageViewModel(this, _sp));
    }

    /// <summary>„Neues Projekt": leeres Projekt + Infoblatt im Draft-Modus.</summary>
    public void StartNewProjectDraft()
    {
        if (!ConfirmDiscardUnsavedChanges())
            return;

        ReplaceProject(new Project());
        ResetProjectReady();
        _suppressLeaveGuard = true;
        SelectedNavItem = null;
        _suppressLeaveGuard = false;
        CurrentMode = ShellMode.Draft;
        SetCurrentPage(new Pages.ProjectPageViewModel(this));
    }

    /// <summary>Wechselt in den Arbeitsbereich und navigiert auf die Landeseite.</summary>
    public void EnterWorkspaceOn(string navTitle)
    {
        CurrentMode = ShellMode.Workspace;
        NavigateTo(navTitle);
    }

    private void SwitchProject()
    {
        if (!ConfirmDiscardUnsavedChanges())
            return;
        EnterLauncher();
    }

    /// <summary>Legt aus dem Draft-Infoblatt Projektordner + projekt.json an.</summary>
    public bool CreateProjectFromDraft()
    {
        var name = Project.Name?.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            SetStatus("Bitte einen Projektnamen eingeben.");
            return false;
        }

        var baseDir = _sp.Settings.ProjectsRootDirectory;
        if (string.IsNullOrWhiteSpace(baseDir))
        {
            baseDir = _sp.Dialogs.SelectFolder("Projekte-Verzeichnis waehlen", @"D:\Projekt");
            if (string.IsNullOrWhiteSpace(baseDir))
                return false;
            _sp.Settings.ProjectsRootDirectory = baseDir;
            _sp.Settings.Save();
        }

        var plan = NewProjectFolderPlanner.Plan(baseDir, name, Directory.Exists);

        try
        {
            Directory.CreateDirectory(plan.FolderPath);
        }
        catch (Exception ex)
        {
            SetStatus($"Projektordner konnte nicht angelegt werden: {ex.Message}");
            return false;
        }

        var res = _sp.Projects.Save(Project, plan.ProjectFilePath);
        if (!res.Ok)
        {
            SetStatus($"Fehler: {res.ErrorMessage}");
            return false;
        }

        _sp.Settings.AddRecentProject(plan.ProjectFilePath);
        _sp.Settings.Save();
        MarkProjectReady();
        SetStatus($"Neues Projekt: {name}");
        EnterWorkspaceOn("Import");
        return true;
    }
```

Benötigte `using`-Direktiven sicherstellen (oben in der Datei vorhanden bzw. ergänzen): `System`, `System.IO`, `AuswertungPro.Next.Application.Common` (für `NewProjectFolderPlanner`).

`OpenProjectWithDialog()` (Zeile 451) so anpassen, dass nach erfolgreichem Öffnen der Workspace betreten wird:

```csharp
    private void OpenProjectWithDialog()
    {
        if (TryOpenProjectWithDialog())
            EnterWorkspaceOn("Haltungen");
    }
```

- [ ] **Step 3d: ShellNavigationPolicy + bestehenden Policy-Test anpassen**

In `ShellNavigationPolicy.CanOpenWithoutProject` (Zeile 19) „Uebersicht" entfernen:

```csharp
    public static bool CanOpenWithoutProject(string? title)
        => title is "Projekt" or "Export" or "Einstellungen";
```

In `tests/AuswertungPro.Next.UI.Tests/ShellNavigationPolicyTests.cs` die `[InlineData("Uebersicht")]`-Zeile (Zeile 55) aus `CorePagesStayAvailableWithoutProject` **entfernen**.

- [ ] **Step 4: Build + Tests**

Run: `dotnet build AuswertungPro.sln`
Expected: 0 Errors.

Run: `dotnet test tests/AuswertungPro.Next.UI.Tests/AuswertungPro.Next.UI.Tests.csproj --filter "FullyQualifiedName~ProjektEroeffnungShellGuard|FullyQualifiedName~ShellNavigationPolicy"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/AuswertungPro.Next.UI/ViewModels/ShellViewModel.cs tests/AuswertungPro.Next.UI.Tests/ShellNavigationPolicyTests.cs tests/AuswertungPro.Next.UI.Tests/ProjektEroeffnungShellGuardTests.cs
git commit -m "feat: ShellMode (Launcher/Draft/Workspace) + Projekt-wechseln + Draft-Anlegen"
```

---

### Task 4: `MainWindow.xaml` — Menü/Nav/Shortcuts modusabhängig + „Projekt wechseln"

Menü, Sidebar und Save-Shortcut nur im Workspace; Kopf-Knopf „Projekt wechseln".

**Files:**
- Modify: `src/AuswertungPro.Next.UI/MainWindow.xaml`
- Test: `tests/AuswertungPro.Next.UI.Tests/ProjektEroeffnungMainWindowGuardTests.cs`

**Interfaces:**
- Consumes: `IsMenuVisible`, `SwitchProjectCommand`, `CurrentMode` (Task 3).

- [ ] **Step 1: Write the failing guard test**

```csharp
using System.IO;

namespace AuswertungPro.Next.UI.Tests;

public sealed class ProjektEroeffnungMainWindowGuardTests
{
    private static string Xaml()
        => File.ReadAllText(ProjektEroeffnungShellGuardTests.RepoFile(
            "src", "AuswertungPro.Next.UI", "MainWindow.xaml"));

    [Fact]
    public void Menu_collapses_outside_workspace()
    {
        var xaml = Xaml();
        // Menue + Sidebar binden an IsMenuVisible (zusaetzlich zur IsFocusMode-Logik).
        Assert.Contains("IsMenuVisible", xaml);
    }

    [Fact]
    public void Header_has_switch_project_button()
    {
        var xaml = Xaml();
        Assert.Contains("SwitchProjectCommand", xaml);
        Assert.Contains("Projekt wechseln", xaml);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/AuswertungPro.Next.UI.Tests/AuswertungPro.Next.UI.Tests.csproj --filter "FullyQualifiedName~ProjektEroeffnungMainWindowGuard"`
Expected: FAIL.

- [ ] **Step 3a: Top-Bereich = DockPanel mit „Projekt wechseln" + Menü**

Ersetze in `MainWindow.xaml` den öffnenden `<Menu DockPanel.Dock="Top">` (Zeile 16) durch einen DockPanel-Wrapper. Aus:

```xml
        <Menu DockPanel.Dock="Top">
            <Menu.Style>
                <Style TargetType="Menu" BasedOn="{StaticResource {x:Type Menu}}">
                    <Setter Property="Visibility" Value="Visible"/>
                    <Style.Triggers>
                        <DataTrigger Binding="{Binding IsFocusMode}" Value="True">
                            <Setter Property="Visibility" Value="Collapsed"/>
                        </DataTrigger>
                    </Style.Triggers>
                </Style>
            </Menu.Style>
```

wird:

```xml
        <DockPanel DockPanel.Dock="Top">
            <DockPanel.Style>
                <Style TargetType="DockPanel">
                    <Setter Property="Visibility" Value="Visible"/>
                    <Style.Triggers>
                        <DataTrigger Binding="{Binding IsMenuVisible}" Value="False">
                            <Setter Property="Visibility" Value="Collapsed"/>
                        </DataTrigger>
                        <DataTrigger Binding="{Binding IsFocusMode}" Value="True">
                            <Setter Property="Visibility" Value="Collapsed"/>
                        </DataTrigger>
                    </Style.Triggers>
                </Style>
            </DockPanel.Style>
            <Button DockPanel.Dock="Right"
                    Content="Projekt wechseln"
                    Command="{Binding SwitchProjectCommand}"
                    Margin="8,2,8,2" Padding="12,2"
                    Style="{StaticResource SecondaryButton}"/>
            <Menu>
                <Menu.Style>
                    <Style TargetType="Menu" BasedOn="{StaticResource {x:Type Menu}}"/>
                </Menu.Style>
```

Und den zugehörigen `</Menu>`-Schluss (Zeile 46) zu:

```xml
            </Menu>
        </DockPanel>
```

> Hinweis für den Umsetzer: Die `<MenuItem ...>`-Kinder zwischen Zeile 27 und 45 bleiben unverändert; nur Wrapper-Öffnung/-Schluss ändern sich.

- [ ] **Step 3b: Sidebar nur im Workspace**

In der Sidebar-`ColumnDefinition.Style` (Zeile 63–70) einen Trigger ergänzen, sodass die Spalte außerhalb des Workspace 0 breit ist. Nach dem bestehenden `IsFocusMode`-Trigger einfügen:

```xml
                            <DataTrigger Binding="{Binding IsMenuVisible}" Value="False">
                                <Setter Property="Width" Value="0"/>
                            </DataTrigger>
```

In der Sidebar-`Border.Style` (Zeile 80–87) analog ergänzen:

```xml
                        <DataTrigger Binding="{Binding IsMenuVisible}" Value="False">
                            <Setter Property="Visibility" Value="Collapsed"/>
                        </DataTrigger>
```

- [ ] **Step 4: Build + Test**

Run: `dotnet build AuswertungPro.sln`
Expected: 0 Errors.

Run: `dotnet test tests/AuswertungPro.Next.UI.Tests/AuswertungPro.Next.UI.Tests.csproj --filter "FullyQualifiedName~ProjektEroeffnungMainWindowGuard"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/AuswertungPro.Next.UI/MainWindow.xaml tests/AuswertungPro.Next.UI.Tests/ProjektEroeffnungMainWindowGuardTests.cs
git commit -m "feat: MainWindow Menue/Sidebar nur im Workspace + Knopf 'Projekt wechseln'"
```

---

### Task 5: `OverviewPage` — Projektliste scannt `ProjectsRootDirectory` + Shell-Flows

Scan-Wurzeln um das konfigurierte Verzeichnis erweitern (pure Helper), `NewProject`/Öffnen auf die Shell-Modus-Methoden umstellen.

**Files:**
- Create: `src/AuswertungPro.Next.UI/ViewModels/Pages/ProjectScanRoots.cs`
- Modify: `src/AuswertungPro.Next.UI/ViewModels/Pages/OverviewPageViewModel.cs`
- Test: `tests/AuswertungPro.Next.UI.Tests/ProjectScanRootsTests.cs`

**Interfaces:**
- Produces: `static IReadOnlyList<string> ProjectScanRoots.Resolve(string currentDirectory, string? projectsRootDirectory)`
- Consumes: `ShellViewModel.StartNewProjectDraft()`, `EnterWorkspaceOn(string)`, `EnterLauncher()` (Task 3).

- [ ] **Step 1: Write the failing test**

```csharp
using System.IO;
using System.Linq;
using AuswertungPro.Next.UI.ViewModels.Pages;

namespace AuswertungPro.Next.UI.Tests;

public sealed class ProjectScanRootsTests
{
    [Fact]
    public void Resolve_includes_configured_root_and_its_subfolders_marker()
    {
        var roots = ProjectScanRoots.Resolve(@"C:\App", @"E:\MeineProjekte");
        Assert.Contains(@"E:\MeineProjekte", roots);
    }

    [Fact]
    public void Resolve_includes_current_rohdaten_folder()
    {
        var roots = ProjectScanRoots.Resolve(@"C:\App", null);
        Assert.Contains(Path.Combine(@"C:\App", "Rohdaten"), roots);
    }

    [Fact]
    public void Resolve_ignores_blank_configured_root()
    {
        var roots = ProjectScanRoots.Resolve(@"C:\App", "   ");
        Assert.DoesNotContain("   ", roots);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/AuswertungPro.Next.UI.Tests/AuswertungPro.Next.UI.Tests.csproj --filter "FullyQualifiedName~ProjectScanRoots"`
Expected: FAIL — `ProjectScanRoots` existiert nicht.

- [ ] **Step 3a: Pure Helper anlegen**

```csharp
using System.Collections.Generic;
using System.IO;

namespace AuswertungPro.Next.UI.ViewModels.Pages;

/// <summary>
/// Liefert die Verzeichnisse, in denen die Projektliste nach *.json sucht.
/// Pur (kein Dateisystem-Zugriff) — die tatsaechliche Existenzpruefung/Enumeration
/// macht der Aufrufer.
/// </summary>
public static class ProjectScanRoots
{
    public static IReadOnlyList<string> Resolve(string currentDirectory, string? projectsRootDirectory)
    {
        var roots = new List<string>
        {
            Path.Combine(currentDirectory, "Rohdaten"),
            Path.Combine(currentDirectory, "Rohdaten", "Section_PDF")
        };

        // Konfiguriertes Projekte-Verzeichnis (falls gesetzt) bevorzugt aufnehmen.
        if (!string.IsNullOrWhiteSpace(projectsRootDirectory))
            roots.Add(projectsRootDirectory);

        return roots;
    }
}
```

- [ ] **Step 3b: `LoadAllProjects` und Flows umstellen**

In `OverviewPageViewModel.LoadAllProjects()` den hartkodierten Block, der `rootDirs` aufbaut (Zeile 137–158), ersetzen, sodass die konfigurierten Wurzeln plus deren Unterordner gescannt werden. Ersetze Zeile 137–158:

```csharp
        // 3. Standard-Scan-Ordner
        var rootDirs = new List<string>
        {
            Path.Combine(Directory.GetCurrentDirectory(), "Rohdaten"),
            Path.Combine(Directory.GetCurrentDirectory(), "Rohdaten", "Section_PDF")
        };

        // 4. D:\Projekt\ und D:\Haltungen\ (typische Speicherorte)
        foreach (var drive in new[] { "D:\\", "C:\\" })
        {
            var projektDir = Path.Combine(drive, "Projekt");
            if (Directory.Exists(projektDir))
            {
                rootDirs.Add(projektDir);
                // Auch Unterordner scannen (z.B. D:\Projekt\Zone 1.15\)
                try
                {
                    foreach (var subDir in Directory.GetDirectories(projektDir))
                        rootDirs.Add(subDir);
                }
                catch { /* Zugriff verweigert */ }
            }
        }
```

durch:

```csharp
        // 3. Konfiguriertes Projekte-Verzeichnis + Standard-Scan-Ordner
        var rootDirs = ProjectScanRoots
            .Resolve(Directory.GetCurrentDirectory(), _sp.Settings.ProjectsRootDirectory)
            .ToList();

        // 4. Fallback-Speicherorte + jeweils direkte Unterordner scannen
        var projektBases = new List<string> { @"D:\Projekt", @"C:\Projekt" };
        if (!string.IsNullOrWhiteSpace(_sp.Settings.ProjectsRootDirectory))
            projektBases.Insert(0, _sp.Settings.ProjectsRootDirectory!);

        foreach (var projektDir in projektBases)
        {
            if (!Directory.Exists(projektDir))
                continue;
            try
            {
                foreach (var subDir in Directory.GetDirectories(projektDir))
                    rootDirs.Add(subDir);
            }
            catch { /* Zugriff verweigert */ }
        }
```

`NewProject()` (Zeile 185–192) ersetzen — die Shell steuert jetzt Modus + Navigation:

```csharp
    private void NewProject()
    {
        _shell.StartNewProjectDraft();
    }
```

In `OpenProject()` (Zeile 201) und `OpenSelectedProject()` (Zeile 216) und `OpenLastProject()` (Zeile 266) jeweils `_shell.NavigateTo("Projekt");` ersetzen durch:

```csharp
        _shell.EnterWorkspaceOn("Haltungen");
```

In `DeleteSelectedProject()` (Zeile 246) `_shell.NewProject();` ersetzen durch:

```csharp
                _shell.EnterLauncher();
```

- [ ] **Step 4: Build + Test**

Run: `dotnet build AuswertungPro.sln`
Expected: 0 Errors.

Run: `dotnet test tests/AuswertungPro.Next.UI.Tests/AuswertungPro.Next.UI.Tests.csproj --filter "FullyQualifiedName~ProjectScanRoots"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/AuswertungPro.Next.UI/ViewModels/Pages/ProjectScanRoots.cs src/AuswertungPro.Next.UI/ViewModels/Pages/OverviewPageViewModel.cs tests/AuswertungPro.Next.UI.Tests/ProjectScanRootsTests.cs
git commit -m "feat: Startliste scannt ProjectsRootDirectory; Overview nutzt Shell-Modus-Flows"
```

---

### Task 6: `ProjectPage` — Draft-Name, „Projekt anlegen", Knöpfe entfernen

`DraftName` (INPC) für den Anlegen-Button, „Neues Projekt"/„Öffnen" raus, „Projekt anlegen" im Draft.

**Files:**
- Modify: `src/AuswertungPro.Next.UI/ViewModels/Pages/ProjectPageViewModel.cs`
- Modify: `src/AuswertungPro.Next.UI/Views/Pages/ProjectPage.xaml`
- Test: `tests/AuswertungPro.Next.UI.Tests/ProjektEroeffnungProjectPageGuardTests.cs`

**Interfaces:**
- Consumes: `ShellViewModel.CurrentMode`, `ShellViewModel.CreateProjectFromDraft()` (Task 3).
- Produces: `string DraftName`, `bool IsDraft`, `IRelayCommand AnlegenCommand` auf `ProjectPageViewModel`.

- [ ] **Step 1: Write the failing guard test**

```csharp
using System.IO;

namespace AuswertungPro.Next.UI.Tests;

public sealed class ProjektEroeffnungProjectPageGuardTests
{
    private static string Vm()
        => File.ReadAllText(ProjektEroeffnungShellGuardTests.RepoFile(
            "src", "AuswertungPro.Next.UI", "ViewModels", "Pages", "ProjectPageViewModel.cs"));

    private static string Xaml()
        => File.ReadAllText(ProjektEroeffnungShellGuardTests.RepoFile(
            "src", "AuswertungPro.Next.UI", "Views", "Pages", "ProjectPage.xaml"));

    [Fact]
    public void Vm_has_draftname_and_anlegen_command()
    {
        var vm = Vm();
        Assert.Contains("DraftName", vm);
        Assert.Contains("AnlegenCommand", vm);
        Assert.Contains("CreateProjectFromDraft", vm);
        Assert.Contains("public bool IsDraft", vm);
    }

    [Fact]
    public void Xaml_drops_new_and_open_buttons_and_adds_anlegen()
    {
        var xaml = Xaml();
        Assert.DoesNotContain("Content=\"Neues Projekt\"", xaml);
        Assert.DoesNotContain("Content=\"Öffnen\"", xaml);
        Assert.Contains("Content=\"Projekt anlegen\"", xaml);
        Assert.Contains("AnlegenCommand", xaml);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/AuswertungPro.Next.UI.Tests/AuswertungPro.Next.UI.Tests.csproj --filter "FullyQualifiedName~ProjektEroeffnungProjectPageGuard"`
Expected: FAIL.

- [ ] **Step 3a: ViewModel — DraftName, IsDraft, AnlegenCommand**

In `ProjectPageViewModel.cs`: `NewCommand`/`OpenCommand` werden nicht mehr gebraucht. Ersetze die Property-Deklarationen (Zeile 19–21):

```csharp
    public IRelayCommand NewCommand { get; }
    public IRelayCommand OpenCommand { get; }
    public IRelayCommand SaveAsCommand { get; }
```

durch:

```csharp
    public IRelayCommand SaveAsCommand { get; }
    public IRelayCommand AnlegenCommand { get; }

    [ObservableProperty] private string _draftName = string.Empty;

    /// <summary>True im Draft-Modus (neues, noch nicht angelegtes Projekt).</summary>
    public bool IsDraft => _shell.CurrentMode == ShellMode.Draft;
```

Ersetze die Konstruktor-Zuweisungen (Zeile 71–73):

```csharp
        NewCommand = _shell.NewProjectCommand;
        OpenCommand = _shell.OpenProjectCommand;
        SaveAsCommand = _shell.SaveAsProjectCommand;
```

durch:

```csharp
        SaveAsCommand = _shell.SaveAsProjectCommand;
        AnlegenCommand = new RelayCommand(
            () => _shell.CreateProjectFromDraft(),
            () => !string.IsNullOrWhiteSpace(DraftName));

        DraftName = Project.Name ?? string.Empty;
```

Ersetze den `_shell.PropertyChanged`-Handler (Zeile 75–82) durch eine Variante, die auch `CurrentMode`/`Project` für `IsDraft` und `DraftName` berücksichtigt:

```csharp
        _shell.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ShellViewModel.Project))
            {
                OnPropertyChanged(nameof(Project));
                DraftName = Project.Name ?? string.Empty;
                SyncDropdownsFromProject();
            }
            else if (e.PropertyName == nameof(ShellViewModel.CurrentMode))
            {
                OnPropertyChanged(nameof(IsDraft));
            }
        };
```

Neue partielle Methode für `DraftName` (z.B. nach dem Konstruktor einfügen) — schreibt den Namen ins Projekt und aktualisiert den Button:

```csharp
    partial void OnDraftNameChanged(string value)
    {
        Project.Name = value;
        (AnlegenCommand as RelayCommand)?.NotifyCanExecuteChanged();
    }
```

- [ ] **Step 3b: XAML — Knöpfe + Name-Feld + Anlegen**

In `ProjectPage.xaml`: Die beiden Buttons „Neues Projekt" und „Öffnen" (Zeile 16–17) **entfernen**. Aus:

```xml
            <StackPanel Orientation="Horizontal" Grid.Column="0">
                <Button Content="Neues Projekt" Command="{Binding NewCommand}" />
                <Button Content="Öffnen" Command="{Binding OpenCommand}" Margin="8,0,0,0" Style="{StaticResource SecondaryButton}"/>
                <Button Content="Speichern unter" Command="{Binding SaveAsCommand}" Margin="8,0,0,0" Style="{StaticResource SecondaryButton}"/>
            </StackPanel>
```

wird:

```xml
            <StackPanel Orientation="Horizontal" Grid.Column="0">
                <Button Content="Projekt anlegen"
                        Command="{Binding AnlegenCommand}"
                        Visibility="{Binding IsDraft, Converter={StaticResource BoolToVis}}"/>
                <Button Content="Speichern unter" Command="{Binding SaveAsCommand}" Margin="8,0,0,0" Style="{StaticResource SecondaryButton}"
                        Visibility="{Binding IsDraft, Converter={StaticResource InvertBoolToVis}}"/>
            </StackPanel>
```

Das Name-Feld (Zeile 31) an `DraftName` binden (INPC) statt direkt an `Project.Name`. Aus:

```xml
                    <TextBox Text="{Binding Project.Name, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}"/>
```

wird:

```xml
                    <TextBox Text="{Binding DraftName, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}"/>
```

Sicherstellen, dass die Converter `BoolToVis` und `InvertBoolToVis` der `ProjectPage` zur Verfügung stehen. `ProjectPage.xaml` hat aktuell keine `UserControl.Resources`. Direkt nach dem öffnenden `<UserControl ...>` (vor `<Grid>`, Zeile 4) einfügen:

```xml
    <UserControl.Resources>
        <BooleanToVisibilityConverter x:Key="BoolToVis"/>
        <local:InvertBoolToVisibilityConverter x:Key="InvertBoolToVis"/>
    </UserControl.Resources>
```

und im `<UserControl ...>`-Tag den Namespace ergänzen:

```xml
             xmlns:local="clr-namespace:AuswertungPro.Next.UI.Views.Pages"
```

> Hinweis: `InvertBoolToVisibilityConverter` existiert bereits im Projekt (Converter-Liste, Key `InvertBoolToVis`). Falls er in einem anderen Namespace liegt, den `xmlns:local` entsprechend setzen — der Umsetzer prüft den tatsächlichen Namespace per Grep `class InvertBoolToVisibilityConverter`.

- [ ] **Step 4: Build + Test**

Run: `dotnet build AuswertungPro.sln`
Expected: 0 Errors.

Run: `dotnet test tests/AuswertungPro.Next.UI.Tests/AuswertungPro.Next.UI.Tests.csproj --filter "FullyQualifiedName~ProjektEroeffnungProjectPageGuard"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/AuswertungPro.Next.UI/ViewModels/Pages/ProjectPageViewModel.cs src/AuswertungPro.Next.UI/Views/Pages/ProjectPage.xaml tests/AuswertungPro.Next.UI.Tests/ProjektEroeffnungProjectPageGuardTests.cs
git commit -m "feat: ProjectPage Draft-Name + 'Projekt anlegen', Neu/Oeffnen-Knoepfe raus"
```

---

### Task 7: `SettingsPage` — Feld „Projekte-Verzeichnis"

Einstellbares Basisverzeichnis mit Ordner-Auswahl, gespeichert in `ProjectsRootDirectory`.

**Files:**
- Modify: `src/AuswertungPro.Next.UI/ViewModels/Pages/SettingsPageViewModel.cs`
- Modify: `src/AuswertungPro.Next.UI/Views/Pages/SettingsPage.xaml`
- Test: `tests/AuswertungPro.Next.UI.Tests/ProjektEroeffnungSettingsGuardTests.cs`

**Interfaces:**
- Consumes: `AppSettings.ProjectsRootDirectory` (Task 2), `_sp.Dialogs.SelectFolder` (vorhanden).

- [ ] **Step 1: Write the failing guard test**

```csharp
using System.IO;

namespace AuswertungPro.Next.UI.Tests;

public sealed class ProjektEroeffnungSettingsGuardTests
{
    private static string Vm()
        => File.ReadAllText(ProjektEroeffnungShellGuardTests.RepoFile(
            "src", "AuswertungPro.Next.UI", "ViewModels", "Pages", "SettingsPageViewModel.cs"));

    private static string Xaml()
        => File.ReadAllText(ProjektEroeffnungShellGuardTests.RepoFile(
            "src", "AuswertungPro.Next.UI", "Views", "Pages", "SettingsPage.xaml"));

    [Fact]
    public void Vm_exposes_and_persists_projects_root()
    {
        var vm = Vm();
        Assert.Contains("ProjectsRootDirectory", vm);
        Assert.Contains("BrowseProjectsRootCommand", vm);
    }

    [Fact]
    public void Xaml_has_projects_root_field()
    {
        var xaml = Xaml();
        Assert.Contains("Projekte-Verzeichnis", xaml);
        Assert.Contains("BrowseProjectsRootCommand", xaml);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/AuswertungPro.Next.UI.Tests/AuswertungPro.Next.UI.Tests.csproj --filter "FullyQualifiedName~ProjektEroeffnungSettingsGuard"`
Expected: FAIL.

- [ ] **Step 3a: ViewModel — Property, Command, Laden, Speichern**

In `SettingsPageViewModel.cs` nach `[ObservableProperty] private string? _projectPath;` (Zeile 17) einfügen:

```csharp
    [ObservableProperty] private string? _projectsRootDirectory;
```

Command-Property nach `BrowseProjectPathCommand` (Zeile 75) ergänzen:

```csharp
    public IRelayCommand BrowseProjectsRootCommand { get; }
```

Im Konstruktor laden (nach Zeile 91, `ProjectPath = _sp.Settings.LastProjectPath;`):

```csharp
        ProjectsRootDirectory = _sp.Settings.ProjectsRootDirectory;
```

Command erzeugen (nach Zeile 111, `BrowseProjectPathCommand = ...`):

```csharp
        BrowseProjectsRootCommand = new RelayCommand(BrowseProjectsRoot);
```

Browse-Methode (nach `BrowseProjectPath()`, Zeile 207) einfügen:

```csharp
    private void BrowseProjectsRoot()
    {
        var p = _sp.Dialogs.SelectFolder("Projekte-Verzeichnis waehlen", ProjectsRootDirectory);
        if (p is null) return;
        ProjectsRootDirectory = p;
    }
```

In `Save()` persistieren (nach Zeile 220, `_sp.Settings.LastProjectPath = ...`):

```csharp
        _sp.Settings.ProjectsRootDirectory = string.IsNullOrWhiteSpace(ProjectsRootDirectory)
            ? null
            : ProjectsRootDirectory.Trim();
```

- [ ] **Step 3b: XAML — Feld einfügen**

In `SettingsPage.xaml` direkt nach dem „Projektpfad (*.json)"-Block (nach Zeile 73, `</DockPanel>`) einfügen:

```xml
        <TextBlock Text="Projekte-Verzeichnis (neue Projekte)" Margin="0,14,0,6" Foreground="{DynamicResource MutedBrush}"/>
        <DockPanel>
            <TextBox Text="{Binding ProjectsRootDirectory, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}" MinWidth="400" />
            <Button Content="..." Command="{Binding BrowseProjectsRootCommand}" Margin="8,0,0,0" Width="44"/>
        </DockPanel>
```

- [ ] **Step 4: Build + Test**

Run: `dotnet build AuswertungPro.sln`
Expected: 0 Errors.

Run: `dotnet test tests/AuswertungPro.Next.UI.Tests/AuswertungPro.Next.UI.Tests.csproj --filter "FullyQualifiedName~ProjektEroeffnungSettingsGuard"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/AuswertungPro.Next.UI/ViewModels/Pages/SettingsPageViewModel.cs src/AuswertungPro.Next.UI/Views/Pages/SettingsPage.xaml tests/AuswertungPro.Next.UI.Tests/ProjektEroeffnungSettingsGuardTests.cs
git commit -m "feat: Einstellung 'Projekte-Verzeichnis' (ProjectsRootDirectory)"
```

---

### Task 8: Integration — voller Build + Testlauf + manueller Smoke

**Files:** keine (Verifikation).

- [ ] **Step 1: Voller Build**

Run: `dotnet build AuswertungPro.sln`
Expected: 0 Errors, 0 neue Warnings.

- [ ] **Step 2: Volle Testsuite**

Run: `dotnet test AuswertungPro.sln`
Expected: alle grün (inkl. der neuen Guard-/Logik-Tests; `ShellNavigationPolicyTests` ohne „Uebersicht").

- [ ] **Step 3: Manueller Smoke (vom User, da WPF)**

Checkliste:
- App startet → **Start-Bildschirm** ohne linkes Menü, ohne oberes Datei-Menü.
- „Neues Projekt" → Infoblatt; Name leer → „Projekt anlegen" deaktiviert; Name gesetzt → aktiv.
- „Projekt anlegen" → Ordner unter Projekte-Verzeichnis entsteht, `projekt.json` darin, Wechsel zu Import, Menü erscheint.
- Projekt aus Liste öffnen → Workspace, Landeseite Haltungen.
- „Projekt wechseln" (oben) → zurück zum Start-Bildschirm (Rückfrage bei ungespeicherten Änderungen).
- Einstellungen → „Projekte-Verzeichnis" sichtbar, Ordner wählbar, bleibt nach Speichern/Neustart erhalten.
- Import in geöffnetem Projekt → Medien landen weiterhin im Projektordner (unverändert).

- [ ] **Step 4: Commit (nur falls Smoke-Fixes nötig)** — sonst entfällt.

---

## Self-Review

**Spec-Abdeckung:**
- A Start-Bildschirm → Task 3 (`EnterLauncher`, Startup-Launcher) + Task 4 (Menü/Sidebar aus).
- B Menü aufgeräumt: „Uebersicht" raus → Task 3; „Projekt wechseln" → Task 3 (Command) + Task 4 (Knopf); „Projekt"-Infoblatt ohne Neu/Öffnen → Task 6.
- C „Neues Projekt"-Ablauf (Draft → anlegen → Import) → Task 3 (`StartNewProjectDraft`, `CreateProjectFromDraft`) + Task 6 (Anlegen-Button).
- D Projekte-Verzeichnis → Task 2 (Setting) + Task 7 (UI) + Task 3 (Erstabfrage falls leer).
- D5 Sanitizer/Kollision → Task 1.
- E Medien unverändert → bewusst nicht angefasst (Global Constraints, Task 8 Smoke prüft).
- F2 Top-Menü/Shortcuts → Task 4 (Menü) + Task 3 (`SaveCommand` CanExecute, `OpenProjectWithDialog` → Workspace).
- F3 ProjectsRootDirectory-Scan → Task 5.
- F4 Landeseiten → Task 3 (`EnterWorkspaceOn`) + Task 5 (Aufrufe).
- F6 DraftName INPC → Task 6.

**Platzhalter-Scan:** keine TBD/TODO; jeder Code-Schritt enthält vollständigen Code bzw. exakte Vorher/Nachher-Snippets.

**Typkonsistenz:** `ShellMode`, `CurrentMode`, `IsMenuVisible`, `SwitchProjectCommand`, `StartNewProjectDraft`, `CreateProjectFromDraft`, `EnterWorkspaceOn`, `EnterLauncher` (Task 3) werden in Task 4/5/6 exakt so verwendet. `NewProjectFolderPlan`/`Plan` (Task 1) in Task 3. `ProjectScanRoots.Resolve` (Task 5) testfest. `DraftName`/`IsDraft`/`AnlegenCommand` (Task 6) konsistent. `ProjectsRootDirectory` (Task 2) in Task 3/5/7.

**Offene Annahme für den Umsetzer:** Namespace von `InvertBoolToVisibilityConverter` in Task 6 per Grep verifizieren (Converter existiert laut Converter-Liste; `xmlns:local` ggf. anpassen).
