# Nova-Redesign in WPF, Etappe 1: Leiste und Haltungsseite — Umsetzungsplan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Die im HTML-Prototyp `SewerStudio-Nova-Optimiert-v2.html` freigegebene Gestaltung der Navigationsleiste und der Haltungsseite in die WPF-Anwendung übernehmen, ohne eine Fachfunktion zu ändern.

**Architecture:** Alles bleibt additiv im UI-Projekt: neue reine Regelklassen unter `UI/DataPage` (testbar ohne WPF), zwei neue UserControls neben der bestehenden `HaltungsansichtView`, und die Haltungsseite erhält ein neues Standardlayout Liste | Übersicht rechts | Eingabefelder unten. Die bisherige Haltungsansicht bleibt als Alternative erhalten. Farben, Schriften, Rundungen und Bewegung laufen ausschliesslich über die vorhandenen Theme-Tokens; jede Änderung bekommt einen Wächtertest neben den bestehenden `DesignAudit*Tests`.

**Tech Stack:** WPF / .NET 10, xunit 2.7 (`tests/AuswertungPro.Next.UI.Tests`), vorhandene Bausteine `RecordDetailsView`, `DataPageRecordDetailsBuilder`, `SplitterPersistenceBehavior`, `ButtonContextMenuOpener`, `SystemMonitorPanel`, `MotionSettings`, `FluentIcon`.

**Spec:** `docs/reviews/2026-09-06-nova/optimiert/v2/SewerStudio-Nova-Optimiert-v2.html` (freigegebener Prototyp), `docs/reviews/2026-09-06-nova/optimiert/v2/AENDERUNGEN.md`, `docs/reviews/2026-09-06-nova/optimiert/FUNKTIONSLISTE.md` (keine Funktion darf verschwinden), `docs/reviews/2026-09-06-nova/nachpruefung-codex/NACHPRUEFUNG.md` (Reihenfolge, Punkt 4).

## Global Constraints

- Keine NuGet-Pakete (CLAUDE.md „Coding-Regeln").
- Keine grosse Klasse erweitern: `MaintainabilityFitnessTests` (Deckel 1000 Zeilen je Datei, `HoldingFolderDistributor` bleibt unberührt). `DataPage.xaml.cs` hat 859 Zeilen: neue Logik kommt in eigene Dateien, nicht dorthin.
- Schriftskala nur über Tokens `TextXS 11, TextS 12, TextM 13, TextL 15, TextXL 18, TextTitle 22, TextDisplay 28` (`DesignAuditSchriftskalaTests`); 11 px ist die Untergrenze.
- Rundungen nur `RadiusS 4, RadiusM 6, RadiusL 8, RadiusXL 10, RadiusXXL 14, RadiusPill 999` (`DesignAuditFensterUndRundungenTests`).
- Feste Farbwerte `#RRGGBB` nur in den sechs Video-Dateien; sonst `{DynamicResource …Brush}` (`DesignAuditFeinschliffTests`).
- Sichtbare Texte mit echten Umlauten, Schweizer `ss`; Quellcode und Kommentare mit `ae/oe/ue`.
- Jeder Icon-Knopf trägt `AutomationProperties.Name` und `ToolTip` (`DesignAuditAccessibilityTests`); Menüpunkte mit literalem `Header` tragen ein `MenuItem.Icon`.
- Dauerbewegung unterliegt `MotionSettings.ReduceMotion`.
- Gespeicherte Datenformate (`projekt.json`, `settings.json`) werden nur additiv erweitert; neue Felder müssen ohne Eintrag den bisherigen Zustand ergeben.
- Kundenoriginale werden nie berührt. Diese Etappe schreibt nur Programmcode und Tests.
- Build und Test: `dotnet build AuswertungPro.sln` und `dotnet test AuswertungPro.sln`. Läuft `SewerStudio.exe`, sind die DLLs gesperrt: dann mit `-o .tmp/testout-nova` bauen und testen (siehe Memory „Laufendes Programm sperrt Build").
- Nicht Teil dieser Etappe: Übersicht, Schächte, Player, Training Studio, „Nächste Aufgabe"-Chip (braucht einen fachlichen Prüfstatus je Haltung, den es im Domänenmodell noch nicht gibt), Hell-Glas/Dunkel-Cockpit-Palettenwechsel (die Akzentfarbe bleibt `#2563EB`, weil `DesignAuditContrastTests` weisse Knopftexte voraussetzt).

---

## Dateiübersicht

Neu:
- `src/AuswertungPro.Next.UI/DataPage/ZustandsklasseInkPolicy.cs` — reine Regel: Textfarbe auf Z0 bis Z4 mit mindestens 4,5:1.
- `src/AuswertungPro.Next.UI/DataPage/ZustandsklasseInkConverter.cs` — WPF-Konverter darauf.
- `src/AuswertungPro.Next.UI/ViewModels/ShellNavigationGroups.cs` — reine Zuordnung Navigationstitel → Gruppe.
- `src/AuswertungPro.Next.UI/DataPage/DataPageColumnViewCatalog.cs` — reine Spaltensätze Kompakt, Stammdaten, Bewertung, Sanierung, Kosten, Alle.
- `src/AuswertungPro.Next.UI/DataPage/DataPageColumnViewController.cs` — wendet einen Spaltensatz auf das DataGrid an und speichert die Wahl.
- `src/AuswertungPro.Next.UI/DataPage/DataPageWorkspaceLayoutPolicy.cs` — reine Regel für die Standardhöhe der Eingabefelder (mindestens sieben Zeilen sichtbar).
- `src/AuswertungPro.Next.UI/Views/Pages/Haltungsansicht/HaltungFelderDrawer.xaml(.cs)` — Eingabefelder unten in vier aufklappbaren Themen mit Feldsuche.
- `src/AuswertungPro.Next.UI/Views/Pages/Haltungsansicht/HaltungUebersichtPanel.xaml(.cs)` — Übersicht rechts: Eckdaten und Primäre Schäden.
- Tests: `tests/AuswertungPro.Next.UI.Tests/ZustandsklasseInkPolicyTests.cs`, `ShellNavigationGroupsTests.cs`, `DataPageColumnViewCatalogTests.cs`, `DataPageWorkspaceLayoutPolicyTests.cs`, `DesignAuditNovaHaltungenTests.cs`.

Geändert:
- `src/AuswertungPro.Next.UI/Theme/ThemeLight.xaml`, `Theme/Theme.xaml` — KI-Farbtoken.
- `src/AuswertungPro.Next.UI/ViewModels/ShellViewModel.NavigationSupport.cs` — `NavItem.Group`.
- `src/AuswertungPro.Next.UI/MainWindow.xaml` — gruppierte Leiste, Systemmonitor als Aufklapper.
- `src/AuswertungPro.Next.UI/Controls/SystemMonitorPanel.xaml` — Schriftgrössen über Tokens.
- `src/AuswertungPro.Next.UI/Views/Pages/DataPage.xaml` — Werkzeugleiste, Ansichten-Chips, Arbeitsfläche.
- `src/AuswertungPro.Next.UI/Views/Pages/DataPage.xaml.cs` — nur Verdrahtung (wenige Zeilen).
- `src/AuswertungPro.Next.UI/AppSettings.cs` — `DataPageLayoutSettings.ActiveColumnView`, `ShowHaltungenNovaLayout`.
- `tests/AuswertungPro.Next.UI.Tests/DesignAuditContrastTests.cs`, `DesignAuditSchriftskalaTests.cs`, `DesignAuditCommandReachabilityTests.cs` — erweiterte Wächter.
- `CLAUDE.md` — Abschnitt „Nova-Etappe 1".

---

### Task 1: Textfarbe auf den Zustandsklassen-Marken (R05 aus der Nachprüfung)

**Files:**
- Create: `src/AuswertungPro.Next.UI/DataPage/ZustandsklasseInkPolicy.cs`
- Create: `src/AuswertungPro.Next.UI/DataPage/ZustandsklasseInkConverter.cs`
- Modify: `src/AuswertungPro.Next.UI/Views/Pages/Haltungsansicht/HaltungsansichtView.xaml:13` (Konverter registrieren) und `:153-156` (Chip)
- Modify: `src/AuswertungPro.Next.UI/Views/Pages/DataGridColorCellStyleFactory.cs` (Vordergrund der gefärbten Zelle)
- Test: `tests/AuswertungPro.Next.UI.Tests/ZustandsklasseInkPolicyTests.cs`

**Interfaces:**
- Produces: `public static Color ZustandsklasseInkPolicy.InkFor(Color background)` und `public static double ZustandsklasseInkPolicy.Contrast(Color a, Color b)`; `public sealed class ZustandsklasseInkConverter : IValueConverter` (Eingabe: Zustandsklasse-Text, Ausgabe: `SolidColorBrush`).

- [ ] **Step 1: Test schreiben**

```csharp
using System.Windows.Media;
using AuswertungPro.Next.UI.DataPage;

namespace AuswertungPro.Next.UI.Tests;

public sealed class ZustandsklasseInkPolicyTests
{
    [Fact]
    public void Jede_Zustandsklasse_bekommt_eine_Textfarbe_mit_mindestens_4_5_zu_1()
    {
        foreach (var klasse in ZustandsklasseColorPalette.SelectionOptions)
        {
            var brush = (SolidColorBrush)ZustandsklasseColorPalette.HaltungenPalette[klasse];
            var ink = ZustandsklasseInkPolicy.InkFor(brush.Color);
            Assert.True(ZustandsklasseInkPolicy.Contrast(ink, brush.Color) >= 4.5,
                $"Z{klasse}: Kontrast {ZustandsklasseInkPolicy.Contrast(ink, brush.Color):0.00}");
        }
    }

    [Fact]
    public void Dunkle_Tinte_wird_bevorzugt_wenn_beide_reichen()
    {
        // Gelb: dunkel ergibt deutlich mehr als 4,5, weiss deutlich weniger.
        var ink = ZustandsklasseInkPolicy.InkFor(Color.FromRgb(0xFF, 0xFF, 0x00));
        Assert.Equal(ZustandsklasseInkPolicy.DarkInk, ink);
    }

    [Fact]
    public void Unbekannte_Klasse_liefert_keine_Farbe()
    {
        var conv = new ZustandsklasseInkConverter();
        Assert.Equal(System.Windows.DependencyProperty.UnsetValue,
            conv.Convert("9", typeof(Brush), null!, System.Globalization.CultureInfo.InvariantCulture));
    }
}
```

- [ ] **Step 2: Test laufen lassen, er muss rot sein**

Run: `dotnet test tests/AuswertungPro.Next.UI.Tests --filter "FullyQualifiedName~ZustandsklasseInkPolicyTests"`
Expected: Compilerfehler „ZustandsklasseInkPolicy nicht gefunden".

- [ ] **Step 3: Regel und Konverter schreiben**

```csharp
// src/AuswertungPro.Next.UI/DataPage/ZustandsklasseInkPolicy.cs
using System;
using System.Windows.Media;

namespace AuswertungPro.Next.UI.DataPage;

/// <summary>
/// Textfarbe auf einer Zustandsklassen-Marke. Ziel aus dem Nova-Prototyp: jede Marke
/// erreicht mindestens 4,5:1 (WCAG normaler Text). Dunkle Tinte wird bevorzugt, weil sie auf
/// Gelb, Oliv, Orange und Gruen sicher reicht; nur wenn sie nicht reicht, wird Weiss verwendet.
/// </summary>
public static class ZustandsklasseInkPolicy
{
    public static readonly Color DarkInk = Color.FromRgb(0x0B, 0x12, 0x20);
    public static readonly Color LightInk = Colors.White;
    private const double Ziel = 4.5;

    public static Color InkFor(Color background)
        => Contrast(DarkInk, background) >= Ziel ? DarkInk : LightInk;

    /// <summary>WCAG-Kontrast zweier deckender Farben (1 bis 21).</summary>
    public static double Contrast(Color a, Color b)
    {
        var la = Luminanz(a); var lb = Luminanz(b);
        return (Math.Max(la, lb) + 0.05) / (Math.Min(la, lb) + 0.05);
    }

    private static double Luminanz(Color c)
    {
        static double Kanal(byte v) { var s = v / 255.0; return s <= 0.03928 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4); }
        return 0.2126 * Kanal(c.R) + 0.7152 * Kanal(c.G) + 0.0722 * Kanal(c.B);
    }
}
```

```csharp
// src/AuswertungPro.Next.UI/DataPage/ZustandsklasseInkConverter.cs
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace AuswertungPro.Next.UI.DataPage;

/// <summary>Zustandsklasse-Text → Textfarbe passend zum Marken-Hintergrund derselben Klasse.</summary>
public sealed class ZustandsklasseInkConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (ZustandsklasseColorPalette.TryGetBackground(value?.ToString()) is not SolidColorBrush bg)
            return DependencyProperty.UnsetValue;
        var brush = new SolidColorBrush(ZustandsklasseInkPolicy.InkFor(bg.Color));
        brush.Freeze();
        return brush;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}
```

- [ ] **Step 4: Chip in der Haltungsansicht und die gefärbte Zelle verwenden**

In `HaltungsansichtView.xaml` bei Zeile 13 ergänzen: `<dp:ZustandsklasseInkConverter x:Key="ZkInkConv"/>` (Namespace `xmlns:dp="clr-namespace:AuswertungPro.Next.UI.DataPage"`, falls noch nicht vorhanden). Beim Chip (Zeile 153 bis 156) den `TextBlock`-Vordergrund ersetzen:

```xml
<TextBlock Text="{Binding Fields[Zustandsklasse]}" HorizontalAlignment="Center" VerticalAlignment="Center"
           Foreground="{Binding Fields[Zustandsklasse], Converter={StaticResource ZkInkConv}}"
           FontSize="{DynamicResource TextS}" FontWeight="SemiBold"/>
```

In `DataGridColorCellStyleFactory.cs` dort, wo der Hintergrund-Setter aus der Palette gebaut wird, zusätzlich setzen:

```csharp
if (background is SolidColorBrush solid)
{
    var ink = new SolidColorBrush(ZustandsklasseInkPolicy.InkFor(solid.Color)); ink.Freeze();
    style.Setters.Add(new Setter(Control.ForegroundProperty, ink));
}
```

- [ ] **Step 5: Tests laufen lassen**

Run: `dotnet test tests/AuswertungPro.Next.UI.Tests --filter "FullyQualifiedName~ZustandsklasseInkPolicyTests|FullyQualifiedName~DesignAudit"`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add src/AuswertungPro.Next.UI/DataPage/ZustandsklasseInkPolicy.cs src/AuswertungPro.Next.UI/DataPage/ZustandsklasseInkConverter.cs src/AuswertungPro.Next.UI/Views/Pages/Haltungsansicht/HaltungsansichtView.xaml src/AuswertungPro.Next.UI/Views/Pages/DataGridColorCellStyleFactory.cs tests/AuswertungPro.Next.UI.Tests/ZustandsklasseInkPolicyTests.cs
git commit -m "Zustandsklassen-Marken: Textfarbe je Klasse mit mindestens 4,5:1 Kontrast"
```

---

### Task 2: KI-Farbtoken in beiden Themes

**Files:**
- Modify: `src/AuswertungPro.Next.UI/Theme/ThemeLight.xaml` (nach `ColorInfo`, Zeile 18) und `src/AuswertungPro.Next.UI/Theme/Theme.xaml` (nach `ColorInfo`)
- Modify: `tests/AuswertungPro.Next.UI.Tests/DesignAuditContrastTests.cs`

**Interfaces:**
- Produces: Farben `ColorKi`, `ColorKiSubtle`, `ColorKiText`; Brushes `KiBrush`, `KiSubtleBrush`, `KiTextBrush` in beiden Themes. Spätere Tasks binden `{DynamicResource KiTextBrush}` und `{DynamicResource KiSubtleBrush}`.

- [ ] **Step 1: Wächtertest erweitern**

In `DesignAuditContrastTests.cs` hinzufügen:

```csharp
[Theory]
[InlineData("Theme.xaml")]
[InlineData("ThemeLight.xaml")]
public void Ki_text_reaches_normal_text_contrast_on_card_and_ki_subtle(string themeFile)
{
    var xaml = File.ReadAllText(RepoFile("src", "AuswertungPro.Next.UI", "Theme", themeFile));
    Assert.True(Contrast(ReadColor(xaml, "ColorKiText"), ReadColor(xaml, "ColorCard")) >= 4.5);
    Assert.True(Contrast(ReadColor(xaml, "ColorKiText"), ReadColor(xaml, "ColorKiSubtle")) >= 4.5);
    Assert.Contains("x:Key=\"KiBrush\"", xaml);
    Assert.Contains("x:Key=\"KiSubtleBrush\"", xaml);
    Assert.Contains("x:Key=\"KiTextBrush\"", xaml);
}
```

- [ ] **Step 2: Test laufen lassen, rot**

Run: `dotnet test tests/AuswertungPro.Next.UI.Tests --filter "FullyQualifiedName~DesignAuditContrastTests"`
Expected: FAIL, `ColorKiText` nicht gefunden.

- [ ] **Step 3: Token eintragen**

`ThemeLight.xaml` nach Zeile 18 (`ColorInfo`):

```xml
<!-- KI: eigene Farbe getrennt vom Akzent, damit KI-Vorschlag und Bedienelement unterscheidbar sind -->
<Color x:Key="ColorKi">#FF0A7F8E</Color>
<Color x:Key="ColorKiSubtle">#FFDDEFF1</Color>
<Color x:Key="ColorKiText">#FF0A6E7C</Color>
```

`Theme.xaml` nach `ColorInfo`:

```xml
<Color x:Key="ColorKi">#FF3FD6C6</Color>
<Color x:Key="ColorKiSubtle">#FF1E3A44</Color>
<Color x:Key="ColorKiText">#FF7FE6DA</Color>
```

In beiden Dateien neben den anderen `SolidColorBrush`-Einträgen:

```xml
<SolidColorBrush x:Key="KiBrush" Color="{StaticResource ColorKi}"/>
<SolidColorBrush x:Key="KiSubtleBrush" Color="{StaticResource ColorKiSubtle}"/>
<SolidColorBrush x:Key="KiTextBrush" Color="{StaticResource ColorKiText}"/>
```

- [ ] **Step 4: Tests laufen lassen**

Run: `dotnet test tests/AuswertungPro.Next.UI.Tests --filter "FullyQualifiedName~DesignAuditContrastTests|FullyQualifiedName~ThemeResource"`
Expected: PASS (auch `ThemeResourceKeyUniquenessTests` und `ThemeRessourcenNamenTests`, die beide Themes auf gleiche Schlüssel prüfen).

- [ ] **Step 5: Commit**

```bash
git add src/AuswertungPro.Next.UI/Theme/ThemeLight.xaml src/AuswertungPro.Next.UI/Theme/Theme.xaml tests/AuswertungPro.Next.UI.Tests/DesignAuditContrastTests.cs
git commit -m "Theme: KI-Farbtoken in Hell und Dunkel mit Kontrastwaechter"
```

---

### Task 3: Navigationsleiste in vier Gruppen

**Files:**
- Create: `src/AuswertungPro.Next.UI/ViewModels/ShellNavigationGroups.cs`
- Modify: `src/AuswertungPro.Next.UI/ViewModels/ShellViewModel.NavigationSupport.cs:32-46` (`NavItem.Group`)
- Modify: `src/AuswertungPro.Next.UI/MainWindow.xaml:411-413` (ListBox mit Gruppen)
- Test: `tests/AuswertungPro.Next.UI.Tests/ShellNavigationGroupsTests.cs`

**Interfaces:**
- Produces: `public static string ShellNavigationGroups.GroupOf(string title)`; `public static IReadOnlyList<string> ShellNavigationGroups.Order` (`Projekt`, `Daten`, `Bewertung`, `System`); `NavItem.Group` (string).

- [ ] **Step 1: Test schreiben**

```csharp
using AuswertungPro.Next.UI.ViewModels;

namespace AuswertungPro.Next.UI.Tests;

public sealed class ShellNavigationGroupsTests
{
    [Theory]
    [InlineData("Uebersicht", "Projekt")]
    [InlineData("Projekt", "Projekt")]
    [InlineData("Haltungen", "Projekt")]
    [InlineData("Schaechte", "Projekt")]
    [InlineData("Import", "Daten")]
    [InlineData("Export", "Daten")]
    [InlineData("Medienkonflikte", "Daten")]
    [InlineData("Druckcenter", "Daten")]
    [InlineData("Dossiers", "Daten")]
    [InlineData("Sanierungs-Matrix", "Bewertung")]
    [InlineData("Schacht-Matrix", "Bewertung")]
    [InlineData("Schattenauswertung", "Bewertung")]
    [InlineData("VSA", "Bewertung")]
    [InlineData("Diagnose", "System")]
    [InlineData("Einstellungen", "System")]
    public void Jeder_Navigationspunkt_hat_seine_Gruppe(string title, string group)
        => Assert.Equal(group, ShellNavigationGroups.GroupOf(title));

    [Fact]
    public void Unbekannter_Titel_landet_in_System_statt_zu_werfen()
        => Assert.Equal("System", ShellNavigationGroups.GroupOf("Neu"));

    [Fact]
    public void Reihenfolge_der_Gruppen_ist_fest()
        => Assert.Equal(new[] { "Projekt", "Daten", "Bewertung", "System" }, ShellNavigationGroups.Order);
}
```

- [ ] **Step 2: Test laufen lassen, rot**

Run: `dotnet test tests/AuswertungPro.Next.UI.Tests --filter "FullyQualifiedName~ShellNavigationGroupsTests"`
Expected: Compilerfehler.

- [ ] **Step 3: Zuordnung schreiben und am NavItem anhängen**

```csharp
// src/AuswertungPro.Next.UI/ViewModels/ShellNavigationGroups.cs
using System;
using System.Collections.Generic;

namespace AuswertungPro.Next.UI.ViewModels;

/// <summary>Gruppen der linken Leiste (Nova-Prototyp): Projekt, Daten, Bewertung, System.</summary>
public static class ShellNavigationGroups
{
    public static IReadOnlyList<string> Order { get; } = ["Projekt", "Daten", "Bewertung", "System"];

    public static string GroupOf(string? title) => title switch
    {
        "Uebersicht" or "Projekt" or "Haltungen" or "Schaechte" => "Projekt",
        "Import" or "Export" or "Medienkonflikte" or "Druckcenter" or "Dossiers" => "Daten",
        "Sanierungs-Matrix" or "Schacht-Matrix" or "Schattenauswertung" or "VSA" => "Bewertung",
        _ => "System"
    };

    public static int OrderIndex(string? group) => Math.Max(0, Order.IndexOf(group ?? "System"));

    private static int IndexOf(this IReadOnlyList<string> list, string value)
    {
        for (var i = 0; i < list.Count; i++) if (string.Equals(list[i], value, StringComparison.Ordinal)) return i;
        return -1;
    }
}
```

In `ShellViewModel.NavigationSupport.cs` im Konstruktor von `NavItem` nach `Title = title;` ergänzen und die Eigenschaft anlegen:

```csharp
Group = ShellNavigationGroups.GroupOf(title);
GroupOrder = ShellNavigationGroups.OrderIndex(Group);
```

```csharp
public string Group { get; }
public int GroupOrder { get; }
```

- [ ] **Step 4: Leiste gruppieren**

In `MainWindow.xaml` die `ListBox` (Zeile 411) so ändern, dass sie über eine `CollectionViewSource` mit Gruppierung bindet. Vor der `ListBox` im `DockPanel`:

```xml
<DockPanel.Resources>
    <CollectionViewSource x:Key="GroupedNavItems" Source="{Binding NavItems}">
        <CollectionViewSource.GroupDescriptions>
            <PropertyGroupDescription PropertyName="Group"/>
        </CollectionViewSource.GroupDescriptions>
        <CollectionViewSource.SortDescriptions>
            <componentModel:SortDescription PropertyName="GroupOrder" Direction="Ascending"/>
        </CollectionViewSource.SortDescriptions>
    </CollectionViewSource>
</DockPanel.Resources>
```

Namespace am Fenster: `xmlns:componentModel="clr-namespace:System.ComponentModel;assembly=WindowsBase"`. Die `ListBox` bindet `ItemsSource="{Binding Source={StaticResource GroupedNavItems}}"` und bekommt einen Gruppenkopf:

```xml
<ListBox.GroupStyle>
    <GroupStyle>
        <GroupStyle.HeaderTemplate>
            <DataTemplate>
                <TextBlock Text="{Binding Name}" Margin="10,10,10,2"
                           FontSize="{DynamicResource TextXS}" FontWeight="Bold"
                           Foreground="{DynamicResource MutedBrush}"
                           Typography.Capitals="AllSmallCaps"/>
            </DataTemplate>
        </GroupStyle.HeaderTemplate>
    </GroupStyle>
</ListBox.GroupStyle>
```

`SelectedItem="{Binding SelectedNavItem}"` bleibt unverändert; die Sortierung nach `GroupOrder` ist stabil und erhält die bisherige Reihenfolge innerhalb einer Gruppe.

- [ ] **Step 5: Tests und Sichtprobe**

Run: `dotnet test tests/AuswertungPro.Next.UI.Tests --filter "FullyQualifiedName~ShellNavigationGroupsTests|FullyQualifiedName~ShellViewModel|FullyQualifiedName~Navigation"`
Expected: PASS. Programm starten: die Leiste zeigt vier Überschriften, Auswahl und Tastatur (Pfeile) funktionieren wie bisher.

- [ ] **Step 6: Commit**

```bash
git add src/AuswertungPro.Next.UI/ViewModels/ShellNavigationGroups.cs src/AuswertungPro.Next.UI/ViewModels/ShellViewModel.NavigationSupport.cs src/AuswertungPro.Next.UI/MainWindow.xaml tests/AuswertungPro.Next.UI.Tests/ShellNavigationGroupsTests.cs
git commit -m "Leiste: Navigation in die Gruppen Projekt, Daten, Bewertung, System"
```

---

### Task 4: Systemmonitor als Aufklapper „Analyse bereit" und Schriftskala-Loch schliessen

**Files:**
- Modify: `src/AuswertungPro.Next.UI/MainWindow.xaml:402-405` (SystemMonitorPanel)
- Modify: `src/AuswertungPro.Next.UI/Controls/SystemMonitorPanel.xaml:9-30` (Stil-Setter FontSize 11 / 9.5 / 9 / 14)
- Modify: `tests/AuswertungPro.Next.UI.Tests/DesignAuditSchriftskalaTests.cs:32-37`

**Interfaces:**
- Consumes: `SystemMonitorService.DiagnosticSummary`, `IsSensorBlocked` (vorhanden).

- [ ] **Step 1: Wächter erweitern, damit auch Stil-Setter zählen**

`Keine_Schrift_unter_11_Pixel_in_XAML` in `DesignAuditSchriftskalaTests.cs` bekommt einen zweiten Ausdruck:

```csharp
var zuKleinSetter = new Regex("Property=\"FontSize\"\\s+Value=\"(?:[0-9]|10)(?:\\.[0-9]+)?\"", RegexOptions.Compiled);
var trefferSetter = SucheInXaml(zuKleinSetter, _ => true);
Assert.True(trefferSetter.Count == 0, "Stil-Setter unter 11 px:\n" + string.Join("\n", trefferSetter));
```

Und `Oberflaechen_lesen_Schriftgroessen_nur_ueber_die_Skala` prüft zusätzlich `Property="FontSize" Value="[0-9]` ausserhalb von `Theme\`.

- [ ] **Step 2: Test laufen lassen, rot**

Run: `dotnet test tests/AuswertungPro.Next.UI.Tests --filter "FullyQualifiedName~DesignAuditSchriftskalaTests"`
Expected: FAIL mit `SystemMonitorPanel.xaml` (9, 9.5, 11, 14). Weitere gemeldete Dateien im selben Schritt auf Tokens umstellen (11 → `TextXS`, 12 → `TextS`, 13 → `TextM`, 14 bis 16 → `TextL`).

- [ ] **Step 3: SystemMonitorPanel auf die Skala heben**

In `SystemMonitorPanel.xaml` die Setter ersetzen:

```xml
<Style x:Key="MetricTitleText" TargetType="TextBlock">
    <Setter Property="FontSize" Value="{DynamicResource TextXS}"/> ...
<Style x:Key="MetricDetailText" TargetType="TextBlock">
    <Setter Property="FontSize" Value="{DynamicResource TextXS}"/> ...
<Style x:Key="MetricTinyText" TargetType="TextBlock" BasedOn="{StaticResource MetricDetailText}">
    <Setter Property="Foreground" Value="{DynamicResource MutedBrush}"/>  <!-- kein eigener FontSize mehr -->
<Style x:Key="MetricPercentText" TargetType="TextBlock">
    <Setter Property="FontSize" Value="{DynamicResource TextL}"/> ...
```

- [ ] **Step 4: Monitor in einen Aufklapper legen**

In `MainWindow.xaml` den Block `<ctrl:SystemMonitorPanel DockPanel.Dock="Bottom" .../>` ersetzen durch:

```xml
<Expander DockPanel.Dock="Bottom" Margin="12,4,12,12" IsExpanded="False"
          Background="{DynamicResource CardBrush}" BorderBrush="{DynamicResource BorderBrush}" BorderThickness="1"
          ToolTip="Rechnerauslastung und KI-Bereitschaft, aufklappbar"
          AutomationProperties.Name="Leistung und KI-Bereitschaft">
    <Expander.Header>
        <StackPanel Orientation="Horizontal" VerticalAlignment="Center">
            <ctrl:NeuralPulseDot Width="10" Height="10" Margin="0,0,8,0"/>
            <TextBlock FontSize="{DynamicResource TextS}" FontWeight="SemiBold" Foreground="{DynamicResource TextBrush}">
                <TextBlock.Style>
                    <Style TargetType="TextBlock">
                        <Setter Property="Text" Value="Analyse bereit"/>
                        <Style.Triggers>
                            <DataTrigger Binding="{Binding Monitor.IsSensorBlocked}" Value="True">
                                <Setter Property="Text" Value="Prüfung nötig"/>
                            </DataTrigger>
                        </Style.Triggers>
                    </Style>
                </TextBlock.Style>
            </TextBlock>
        </StackPanel>
    </Expander.Header>
    <ctrl:SystemMonitorPanel DataContext="{Binding Monitor}" Margin="0,6,0,0"/>
</Expander>
```

`NeuralPulseDot` unterliegt bereits `MotionSettings.ReduceMotion` (siehe `Controls/NeuralPulseDot.xaml.cs`). Der Menüpunkt „Ansicht → System-Monitor öffnen" bleibt unverändert.

- [ ] **Step 5: Tests laufen lassen**

Run: `dotnet test tests/AuswertungPro.Next.UI.Tests --filter "FullyQualifiedName~DesignAudit"`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add src/AuswertungPro.Next.UI/MainWindow.xaml src/AuswertungPro.Next.UI/Controls/SystemMonitorPanel.xaml tests/AuswertungPro.Next.UI.Tests/DesignAuditSchriftskalaTests.cs
git commit -m "Leiste: Systemmonitor als Aufklapper Analyse bereit; Schriftskala prueft auch Stil-Setter"
```

---

### Task 5: Werkzeugleiste der Haltungsseite mit einer Hauptaktion und „Weitere Aktionen"

**Files:**
- Modify: `src/AuswertungPro.Next.UI/Views/Pages/DataPage.xaml:106-445` (Werkzeugleiste)
- Modify: `tests/AuswertungPro.Next.UI.Tests/DesignAuditCommandReachabilityTests.cs`

**Interfaces:**
- Consumes: `DropdownButton_Click` (bereits in `DataPage.RecordInteractions.cs:274`, öffnet das `ContextMenu` eines Knopfs), alle vorhandenen Click-Handler und Commands.

- [ ] **Step 1: Wächter zuerst: jeder bisherige Aktionstext bleibt in der XAML**

In `DesignAuditCommandReachabilityTests.cs`:

```csharp
[Theory]
[InlineData("Speichern")] [InlineData("Neu")] [InlineData("Löschen")]
[InlineData("Leere Felder aus QGIS")] [InlineData("Katasterkennungen")]
[InlineData("Sanierungsmaßnahme bearbeiten")] [InlineData("Direkt zur KI-Optimierung")] [InlineData("Vorschlag für diese Haltung erstellen")]
[InlineData("Medien suchen")] [InlineData("Strassen")] [InlineData("Hydraulik berechnen")] [InlineData("Hydraulik PDF")]
[InlineData("Dossier")] [InlineData("Abdocken")] [InlineData("Spalten anordnen")] [InlineData("Spalte leeren")]
[InlineData("Zeilenhöhe:")] [InlineData("Zoom:")] [InlineData("Ausrichtung:")] [InlineData("Haltungsansicht")]
[InlineData("Weitere Aktionen")]
public void Haltungen_toolbar_keeps_every_action_reachable(string sichtbarerText)
{
    var xaml = File.ReadAllText(RepoFile("src", "AuswertungPro.Next.UI", "Views", "Pages", "DataPage.xaml"));
    Assert.Contains(sichtbarerText, xaml, StringComparison.Ordinal);
}

[Fact]
public void Haltungen_toolbar_has_exactly_one_primary_button()
{
    var xaml = File.ReadAllText(RepoFile("src", "AuswertungPro.Next.UI", "Views", "Pages", "DataPage.xaml"));
    var toolbar = xaml[..xaml.IndexOf("x:Name=\"GridHost\"", StringComparison.Ordinal)];
    Assert.Equal(1, Regex.Matches(toolbar, "Style=\"\\{StaticResource ToolbarButtonAccent\\}\"").Count);
}
```

- [ ] **Step 2: Test laufen lassen, rot**

Run: `dotnet test tests/AuswertungPro.Next.UI.Tests --filter "FullyQualifiedName~DesignAuditCommandReachabilityTests"`
Expected: FAIL bei „Weitere Aktionen" und bei der Zahl der Akzentknöpfe.

- [ ] **Step 3: Leiste umbauen**

Sichtbar bleiben in dieser Reihenfolge: `Speichern` (Stil `ToolbarButtonAccent`, einziger Akzentknopf), `Neu`, `Löschen`, ein neuer Knopf `Video prüfen` (`Command="{Binding PlayVideoCommand}" CommandParameter="{Binding Selected}"`, Glyph `&#xE768;`), der Knopf `Weitere Aktionen` und rechts die Suche mit dem `HaltungsansichtToggle`. Alle übrigen Knöpfe wandern unverändert (gleiche Handler, gleiche Commands, gleiche Icons) als `MenuItem` in das ContextMenu des neuen Knopfs:

```xml
<Button x:Name="WeitereAktionenDropdown" Click="DropdownButton_Click" Style="{StaticResource ToolbarButton}"
        ToolTip="Weitere Aktionen: Daten, Fachlich, Ansicht" AutomationProperties.Name="Weitere Aktionen">
    <StackPanel Orientation="Horizontal">
        <ui:FluentIcon Glyph="&#xE712;" Margin="0,0,6,0"/>
        <TextBlock Text="Weitere Aktionen" VerticalAlignment="Center"/>
    </StackPanel>
    <Button.ContextMenu>
        <ContextMenu>
            <MenuItem Header="Daten" IsEnabled="False"/>
            <MenuItem Header="Medien suchen" Click="MediaSearchMenu_Click"><MenuItem.Icon><ui:FluentIcon Glyph="&#xE721;"/></MenuItem.Icon></MenuItem>
            <MenuItem Header="Leere Felder aus QGIS" Command="{Binding QgisFelderErgaenzenCommand}"><MenuItem.Icon><ui:FluentIcon Glyph="&#xE8B7;"/></MenuItem.Icon></MenuItem>
            <MenuItem Header="Katasterkennungen" Command="{Binding KatasterKennungenErgaenzenCommand}"><MenuItem.Icon><ui:FluentIcon Glyph="&#xE8FD;"/></MenuItem.Icon></MenuItem>
            <MenuItem Header="Strassen" Click="StrassenStapel_Click"><MenuItem.Icon><ui:FluentIcon Glyph="&#xE81D;"/></MenuItem.Icon></MenuItem>
            <Separator/>
            <MenuItem Header="Fachlich" IsEnabled="False"/>
            <!-- Die drei Sanierungs-Punkte und die zwei Hydraulik-Punkte unveraendert aus den bisherigen ContextMenus uebernehmen -->
            <MenuItem Header="Dossier" Click="DossierPrint_Click"><MenuItem.Icon><ui:FluentIcon Glyph="&#xE8F1;"/></MenuItem.Icon></MenuItem>
            <Separator/>
            <MenuItem Header="Ansicht" IsEnabled="False"/>
            <!-- Den bisherigen Inhalt des Ansicht-ContextMenus (Nach oben, Nach unten, Spalten anordnen, Spalte leeren, Zeilenhoehe, Zoom, Ausrichtung) unveraendert uebernehmen -->
            <MenuItem Header="Abdocken" Click="UndockGrid_Click"><MenuItem.Icon><ui:FluentIcon Glyph="&#xE8A7;"/></MenuItem.Icon></MenuItem>
        </ContextMenu>
    </Button.ContextMenu>
</Button>
```

Die `x:Name`-Knöpfe `MassnahmenDropdown`, `MediaSearchButton`, `StrassenStapelButton`, `HydraulikDropdown`, `DossierPrintButton`, `UndockButton`, `AnsichtDropdown` werden entfernt; vorher mit `grep -n "MassnahmenDropdown\|MediaSearchButton\|StrassenStapelButton\|HydraulikDropdown\|DossierPrintButton\|UndockButton\|AnsichtDropdown" src/AuswertungPro.Next.UI/Views/Pages/*.cs src/AuswertungPro.Next.UI/DataPage/*.cs` prüfen, ob Code sie anspricht, und diese Stellen auf `WeitereAktionenDropdown` umstellen. `ClearColumnMenuItem` und die `Align*Button` behalten ihre Namen, weil `DataPage.ColumnLayout.cs` sie liest.

- [ ] **Step 4: Tests laufen lassen**

Run: `dotnet test tests/AuswertungPro.Next.UI.Tests --filter "FullyQualifiedName~DesignAudit|FullyQualifiedName~DataPage"`
Expected: PASS. Programm starten: jede Aktion aus dem Menü einmal auslösen.

- [ ] **Step 5: Commit**

```bash
git add src/AuswertungPro.Next.UI/Views/Pages/DataPage.xaml tests/AuswertungPro.Next.UI.Tests/DesignAuditCommandReachabilityTests.cs
git commit -m "Haltungen: Werkzeugleiste mit einer Hauptaktion, Video pruefen und Weitere Aktionen"
```

---

### Task 6: Spaltenansichten Kompakt, Stammdaten, Bewertung, Sanierung, Kosten, Alle

**Files:**
- Create: `src/AuswertungPro.Next.UI/DataPage/DataPageColumnViewCatalog.cs`
- Create: `src/AuswertungPro.Next.UI/DataPage/DataPageColumnViewController.cs`
- Modify: `src/AuswertungPro.Next.UI/AppSettings.cs:665` (`DataPageLayoutSettings.ActiveColumnView`)
- Modify: `src/AuswertungPro.Next.UI/Views/Pages/DataPage.xaml` (Chip-Zeile über `GridHost`) und `DataPage.xaml.cs` (Verdrahtung, 6 Zeilen)
- Test: `tests/AuswertungPro.Next.UI.Tests/DataPageColumnViewCatalogTests.cs`

**Interfaces:**
- Produces: `DataPageColumnViewCatalog.Views` (`IReadOnlyList<DataPageColumnView>`), `record DataPageColumnView(string Key, string Titel, IReadOnlyList<string>? Felder)` (`Felder == null` heisst alle), `DataPageColumnViewCatalog.Resolve(string? key)`; `DataPageColumnViewController(DataGrid grid, Func<DataGridColumn, string?> fieldNameOf, Func<string?> getStored, Action<string> store)` mit `Apply(string key)` und `ActiveKey`.

- [ ] **Step 1: Test schreiben**

```csharp
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.DataPage;

namespace AuswertungPro.Next.UI.Tests;

public sealed class DataPageColumnViewCatalogTests
{
    [Fact]
    public void Kompakt_zeigt_Name_Strasse_Material_DN_Laenge_und_Zustand()
    {
        var v = DataPageColumnViewCatalog.Resolve("kompakt");
        Assert.Equal(new[] { FieldKeys.HoldingName, FieldKeys.Street, FieldKeys.PipeMaterial, FieldKeys.NominalDiameterMm, FieldKeys.HoldingLengthMeters, FieldKeys.ConditionClass, FieldKeys.Link, FieldKeys.PdfPath }, v.Felder);
    }

    [Fact]
    public void Alle_hat_keine_Feldliste_und_ist_der_Rueckfall()
    {
        Assert.Null(DataPageColumnViewCatalog.Resolve("alle").Felder);
        Assert.Equal("alle", DataPageColumnViewCatalog.Resolve(null).Key);
        Assert.Equal("alle", DataPageColumnViewCatalog.Resolve("gibt-es-nicht").Key);
    }

    [Fact]
    public void Jedes_Feld_einer_Ansicht_existiert_im_Feldkatalog()
    {
        var bekannt = new HashSet<string>(FieldCatalog.ColumnOrder, StringComparer.Ordinal);
        foreach (var v in DataPageColumnViewCatalog.Views)
            foreach (var f in v.Felder ?? Array.Empty<string>())
                Assert.True(bekannt.Contains(f), $"{v.Key}: {f} fehlt im FieldCatalog");
    }

    [Fact]
    public void Der_Haltungsname_steht_in_jeder_Ansicht_vorn()
    {
        foreach (var v in DataPageColumnViewCatalog.Views)
            if (v.Felder is not null) Assert.Equal(FieldKeys.HoldingName, v.Felder[0]);
    }
}
```

- [ ] **Step 2: Test laufen lassen, rot**

Run: `dotnet test tests/AuswertungPro.Next.UI.Tests --filter "FullyQualifiedName~DataPageColumnViewCatalogTests"`
Expected: Compilerfehler.

- [ ] **Step 3: Katalog schreiben**

```csharp
// src/AuswertungPro.Next.UI/DataPage/DataPageColumnViewCatalog.cs
using System;
using System.Collections.Generic;
using System.Linq;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.DataPage;

/// <summary>Eine gespeicherte Spaltenansicht der Haltungsliste. Felder == null bedeutet alle Spalten.</summary>
public sealed record DataPageColumnView(string Key, string Titel, IReadOnlyList<string>? Felder);

/// <summary>
/// Feste Spaltensaetze aus dem Nova-Prototyp. Reine Daten, keine WPF-Abhaengigkeit: Der
/// Controller blendet Spalten nur ein oder aus; Werte und Export bleiben unberuehrt.
/// </summary>
public static class DataPageColumnViewCatalog
{
    public static IReadOnlyList<DataPageColumnView> Views { get; } =
    [
        new("kompakt", "Kompakt", [FieldKeys.HoldingName, FieldKeys.Street, FieldKeys.PipeMaterial, FieldKeys.NominalDiameterMm, FieldKeys.HoldingLengthMeters, FieldKeys.ConditionClass, FieldKeys.Link, FieldKeys.PdfPath]),
        new("stammdaten", "Stammdaten", [FieldKeys.HoldingName, FieldKeys.Street, FieldKeys.PipeMaterial, FieldKeys.NominalDiameterMm, FieldKeys.ClearWidthMm, FieldKeys.ProfileType, FieldKeys.UsageType, FieldKeys.HoldingLengthMeters, FieldKeys.InspectionYear, FieldKeys.ConstructionYear, FieldKeys.Owner, FieldKeys.GeonisId, FieldKeys.CadastreObjectId]),
        new("bewertung", "Bewertung", [FieldKeys.HoldingName, FieldKeys.ConditionClass, "VSA_Zustandsnote_D", "VSA_Zustandsnote_S", "VSA_Zustandsnote_B", "VSA_Geschaetzt", "Pruefungsresultat", "Referenzpruefung", "Gewaesserschutz", "Grundwasserspiegel"]),
        new("sanierung", "Sanierung", [FieldKeys.HoldingName, FieldKeys.RenovationDecision, FieldKeys.RecommendedRehabilitationMeasures, FieldKeys.LinerRenovationMeters, FieldKeys.ConnectionsToGrout, FieldKeys.RepairSleeve, FieldKeys.LinerEndSleeve, FieldKeys.ShortLinerRepair, "Erneuerung_Neubau_m", FieldKeys.RehabilitationExecutor, FieldKeys.WorkflowStatus]),
        new("kosten", "Kosten", [FieldKeys.HoldingName, FieldKeys.Street, FieldKeys.ConditionClass, FieldKeys.RecommendedRehabilitationMeasures, FieldKeys.Cost, FieldKeys.RehabilitationExecutor, FieldKeys.WorkflowStatus, FieldKeys.Owner]),
        new("alle", "Alle Spalten", null)
    ];

    public static DataPageColumnView Resolve(string? key)
        => Views.FirstOrDefault(v => string.Equals(v.Key, key, StringComparison.OrdinalIgnoreCase)) ?? Views[^1];
}
```

Die drei Zeichenketten ohne `FieldKeys`-Konstante (`VSA_Zustandsnote_D` usw.) sind die Feldnamen aus `FieldCatalog.cs:173-194`; der Test „existiert im Feldkatalog" schützt sie.

- [ ] **Step 4: Controller und Einstellung**

```csharp
// src/AuswertungPro.Next.UI/DataPage/DataPageColumnViewController.cs
using System;
using System.Windows;
using System.Windows.Controls;

namespace AuswertungPro.Next.UI.DataPage;

/// <summary>
/// Blendet Spalten nach einer gespeicherten Ansicht ein oder aus. Die persoenliche
/// Spaltenanordnung (DataPageGridLayoutController) bleibt bestehen; „Alle Spalten" zeigt
/// wieder jede Spalte, die dort nicht ausdruecklich versteckt ist.
/// </summary>
public sealed class DataPageColumnViewController
{
    private readonly DataGrid _grid;
    private readonly Func<DataGridColumn, string?> _fieldNameOf;
    private readonly Action<string> _store;

    public DataPageColumnViewController(DataGrid grid, Func<DataGridColumn, string?> fieldNameOf, Func<string?> getStored, Action<string> store)
    {
        _grid = grid ?? throw new ArgumentNullException(nameof(grid));
        _fieldNameOf = fieldNameOf ?? throw new ArgumentNullException(nameof(fieldNameOf));
        _store = store ?? throw new ArgumentNullException(nameof(store));
        ActiveKey = DataPageColumnViewCatalog.Resolve(getStored()).Key;
    }

    public string ActiveKey { get; private set; }

    public void Apply(string? key)
    {
        var view = DataPageColumnViewCatalog.Resolve(key);
        foreach (var column in _grid.Columns)
        {
            var field = _fieldNameOf(column);
            if (field is null) continue; // technische Spalten (Zeilenkopf, Marker) bleiben wie sie sind
            column.Visibility = view.Felder is null || view.Felder.Contains(field) ? Visibility.Visible : Visibility.Collapsed;
        }
        ActiveKey = view.Key;
        _store(view.Key);
    }
}
```

In `AppSettings.cs` in `DataPageLayoutSettings` ergänzen (additiv, Standard = bisheriges Verhalten):

```csharp
/// <summary>Gewaehlte Spaltenansicht der Haltungsliste (Schluessel aus DataPageColumnViewCatalog). Leer = alle Spalten.</summary>
public string ActiveColumnView { get; set; } = "alle";
```

- [ ] **Step 5: Chip-Zeile und Verdrahtung**

In `DataPage.xaml` direkt über `<Grid x:Name="GridHost" ...>` innerhalb desselben Rasters (Zeile 0 bleibt die Werkzeugleiste, deshalb `GridHost` auf `Grid.Row="2"` schieben und eine Zeile `Auto` einfügen):

```xml
<ItemsControl x:Name="ColumnViewChips" Grid.Row="1" Margin="2,0,2,6" ItemsSource="{Binding Source={x:Static dp:DataPageColumnViewCatalog.Views}}">
    <ItemsControl.ItemsPanel><ItemsPanelTemplate><WrapPanel/></ItemsPanelTemplate></ItemsControl.ItemsPanel>
    <ItemsControl.ItemTemplate>
        <DataTemplate>
            <ToggleButton Style="{StaticResource CompactToggleButton}" Margin="0,0,6,0" Tag="{Binding Key}" Click="ColumnViewChip_Click"
                          ToolTip="{Binding Titel}" AutomationProperties.Name="{Binding Titel}">
                <TextBlock Text="{Binding Titel}"/>
            </ToggleButton>
        </DataTemplate>
    </ItemsControl.ItemTemplate>
</ItemsControl>
```

Neue Datei `src/AuswertungPro.Next.UI/Views/Pages/DataPage.ColumnViews.cs` (partial, hält `DataPage.xaml.cs` klein):

```csharp
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using AuswertungPro.Next.UI.DataPage;

namespace AuswertungPro.Next.UI.Views.Pages;

public partial class DataPage
{
    private DataPageColumnViewController? _columnViews;

    private void InitColumnViews()
    {
        if (DataContext is not ViewModels.Pages.DataPageViewModel vm) return;
        _columnViews = new DataPageColumnViewController(
            Grid,
            column => column.Header as string is { } h ? DataPageColumnSetup.FieldNameForHeader(h) : null,
            () => vm.Settings.DataPageLayout.ActiveColumnView,
            key => { vm.Settings.DataPageLayout.ActiveColumnView = key; vm.Settings.Save(); });
        _columnViews.Apply(_columnViews.ActiveKey);
        SyncColumnViewChips();
    }

    private void ColumnViewChip_Click(object sender, RoutedEventArgs e)
    {
        if (sender is ToggleButton { Tag: string key } && _columnViews is not null) { _columnViews.Apply(key); SyncColumnViewChips(); }
    }

    private void SyncColumnViewChips()
    {
        foreach (var chip in FindVisualChildren<ToggleButton>(ColumnViewChips))
            chip.IsChecked = string.Equals(chip.Tag as string, _columnViews?.ActiveKey, System.StringComparison.OrdinalIgnoreCase);
    }
}
```

`DataPageColumnSetup.FieldNameForHeader` existiert möglicherweise unter anderem Namen: Vor dem Schreiben mit `grep -n "FieldName" src/AuswertungPro.Next.UI/Views/Pages/DataPageColumnSetup.cs src/AuswertungPro.Next.UI/DataPage/DataPageGridLayoutController.cs` die Stelle finden, an der der Layout-Controller Spalte und Feldname verbindet, und dieselbe Abbildung verwenden. `FindVisualChildren<T>` gibt es in `DataPage.ColumnLayout.cs`; sonst dort anlegen. `InitColumnViews()` wird in `DataPage.xaml.cs` nach dem Aufbau der Spalten (dort, wo `DataPageColumnSetup` läuft) einmal aufgerufen.

- [ ] **Step 6: Tests laufen lassen**

Run: `dotnet test tests/AuswertungPro.Next.UI.Tests --filter "FullyQualifiedName~DataPageColumnViewCatalogTests|FullyQualifiedName~DesignAudit|FullyQualifiedName~MaintainabilityFitness"`
Expected: PASS. Programm: Chips wechseln die Spalten, Neustart merkt sich die Wahl, „Alle Spalten" zeigt den alten Stand.

- [ ] **Step 7: Commit**

```bash
git add src/AuswertungPro.Next.UI/DataPage/DataPageColumnViewCatalog.cs src/AuswertungPro.Next.UI/DataPage/DataPageColumnViewController.cs src/AuswertungPro.Next.UI/Views/Pages/DataPage.ColumnViews.cs src/AuswertungPro.Next.UI/Views/Pages/DataPage.xaml src/AuswertungPro.Next.UI/Views/Pages/DataPage.xaml.cs src/AuswertungPro.Next.UI/AppSettings.cs tests/AuswertungPro.Next.UI.Tests/DataPageColumnViewCatalogTests.cs
git commit -m "Haltungen: Spaltenansichten Kompakt, Stammdaten, Bewertung, Sanierung, Kosten, Alle"
```

---

### Task 7: Arbeitsfläche Liste | Übersicht rechts | Eingabefelder unten

**Files:**
- Create: `src/AuswertungPro.Next.UI/DataPage/DataPageWorkspaceLayoutPolicy.cs`
- Create: `src/AuswertungPro.Next.UI/Views/Pages/Haltungsansicht/HaltungFelderDrawer.xaml` und `.xaml.cs`
- Create: `src/AuswertungPro.Next.UI/Views/Pages/Haltungsansicht/HaltungUebersichtPanel.xaml` und `.xaml.cs`
- Create: `src/AuswertungPro.Next.UI/Views/Pages/DataPage.NovaWorkspace.cs`
- Modify: `src/AuswertungPro.Next.UI/Views/Pages/DataPage.xaml:446-600` (GridHost), `DataPage.xaml.cs:74-82` (Standardansicht)
- Modify: `src/AuswertungPro.Next.UI/AppSettings.cs` (`ShowHaltungenNovaLayout`)
- Test: `tests/AuswertungPro.Next.UI.Tests/DataPageWorkspaceLayoutPolicyTests.cs`, `tests/AuswertungPro.Next.UI.Tests/DesignAuditNovaHaltungenTests.cs`

**Interfaces:**
- Consumes: `DataPageViewModel.Selected` (gewählter `HaltungRecord`), `DataPageViewModel.SelectedProtocolEntries` (Primäre Schäden des gewählten Datensatzes; von `HaltungsansichtView` bereits gebunden), `BuildHaltungRecordDetailsForAnsicht(HaltungRecord)` aus `DataPage.xaml.cs:537`, `RecordDetailsView.Groups`, `SplitterPersistenceBehavior` (`SplitterKey`, `TargetColumnIndex`, `TargetRowIndex`), `SchadenMeterConverter`, `SchadenKlartextConverter`, `ZustandsklasseBrushConverter`, `ZustandsklasseInkConverter` (Task 1).
- Produces: `DataPageWorkspaceLayoutPolicy.DrawerHeight(double gesamtHoehe, double zeilenHoehe, double kopfHoehe, double? gespeichert)`; `HaltungFelderDrawer.Groups` (`IReadOnlyList<RecordDetailGroup>`), `HaltungFelderDrawer.Titel` (string), `HaltungUebersichtPanel.Record` (`HaltungRecord?`), `HaltungUebersichtPanel.Entries` (`IEnumerable`).

- [ ] **Step 1: Test für die Höhenregel**

```csharp
using AuswertungPro.Next.UI.DataPage;

namespace AuswertungPro.Next.UI.Tests;

public sealed class DataPageWorkspaceLayoutPolicyTests
{
    // 1366 x 768: Arbeitsflaeche rund 556 px hoch, Zeile 32 px, Tabellenkopf 32 px.
    [Fact]
    public void Standard_laesst_mindestens_sieben_Zeilen_sichtbar()
    {
        var drawer = DataPageWorkspaceLayoutPolicy.DrawerHeight(gesamtHoehe: 556, zeilenHoehe: 32, kopfHoehe: 32, gespeichert: null);
        Assert.True(556 - drawer - DataPageWorkspaceLayoutPolicy.SplitterHoehe >= 32 + 7 * 32, $"Eingabefelder {drawer} px");
        Assert.True(drawer >= DataPageWorkspaceLayoutPolicy.MinDrawer);
    }

    [Fact]
    public void Gespeicherte_Hoehe_wird_uebernommen_aber_auf_sieben_Zeilen_begrenzt()
    {
        Assert.Equal(180, DataPageWorkspaceLayoutPolicy.DrawerHeight(900, 32, 32, gespeichert: 180));
        var begrenzt = DataPageWorkspaceLayoutPolicy.DrawerHeight(556, 32, 32, gespeichert: 480);
        Assert.True(556 - begrenzt - DataPageWorkspaceLayoutPolicy.SplitterHoehe >= 32 + 7 * 32);
    }

    [Fact]
    public void Sehr_kleine_Flaeche_gibt_die_Mindesthoehe_zurueck()
        => Assert.Equal(DataPageWorkspaceLayoutPolicy.MinDrawer, DataPageWorkspaceLayoutPolicy.DrawerHeight(200, 32, 32, null));
}
```

- [ ] **Step 2: Test laufen lassen, rot**

Run: `dotnet test tests/AuswertungPro.Next.UI.Tests --filter "FullyQualifiedName~DataPageWorkspaceLayoutPolicyTests"`
Expected: Compilerfehler.

- [ ] **Step 3: Höhenregel schreiben**

```csharp
// src/AuswertungPro.Next.UI/DataPage/DataPageWorkspaceLayoutPolicy.cs
using System;

namespace AuswertungPro.Next.UI.DataPage;

/// <summary>
/// Standardhoehe der Eingabefelder unter der Haltungsliste. Abnahmeziel aus dem Nova-Prototyp:
/// Bei 1366 x 768 bleiben mindestens sieben vollstaendige Zeilen sichtbar. Reine Rechnung.
/// </summary>
public static class DataPageWorkspaceLayoutPolicy
{
    public const double MinDrawer = 120;
    public const double SplitterHoehe = 6;
    public const int MindestZeilen = 7;
    private const double Anteil = 0.36;

    public static double DrawerHeight(double gesamtHoehe, double zeilenHoehe, double kopfHoehe, double? gespeichert)
    {
        var fuerListe = kopfHoehe + MindestZeilen * zeilenHoehe;
        var maxDrawer = gesamtHoehe - fuerListe - SplitterHoehe;
        if (maxDrawer < MinDrawer) return MinDrawer;
        var wunsch = gespeichert is > 0 ? gespeichert.Value : Math.Round(gesamtHoehe * Anteil);
        return Math.Clamp(wunsch, MinDrawer, maxDrawer);
    }
}
```

- [ ] **Step 4: Tests laufen lassen**

Run: `dotnet test tests/AuswertungPro.Next.UI.Tests --filter "FullyQualifiedName~DataPageWorkspaceLayoutPolicyTests"`
Expected: PASS.

- [ ] **Step 5: Eingabefelder-Schublade**

`HaltungFelderDrawer.xaml`:

```xml
<UserControl x:Class="AuswertungPro.Next.UI.Views.Pages.Haltungsansicht.HaltungFelderDrawer"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:ui="clr-namespace:AuswertungPro.Next.UI.Controls"
             xmlns:controls="clr-namespace:AuswertungPro.Next.UI.Views.Controls"
             x:Name="Root">
    <Border Background="{DynamicResource CardBrush}" BorderBrush="{DynamicResource BorderBrush}" BorderThickness="1" CornerRadius="{DynamicResource RadiusL}">
        <DockPanel>
            <DockPanel DockPanel.Dock="Top" Margin="10,6" LastChildFill="False">
                <ToggleButton x:Name="OpenToggle" DockPanel.Dock="Left" IsChecked="True" Style="{StaticResource CompactToggleButton}"
                              ToolTip="Eingabefelder auf- oder zuklappen" AutomationProperties.Name="Eingabefelder auf- oder zuklappen">
                    <StackPanel Orientation="Horizontal">
                        <ui:FluentIcon Glyph="&#xE70D;" Margin="0,0,6,0"/>
                        <TextBlock Text="Eingabefelder" FontWeight="SemiBold"/>
                    </StackPanel>
                </ToggleButton>
                <TextBlock DockPanel.Dock="Left" Text="{Binding Titel, ElementName=Root}" Margin="10,0,0,0" VerticalAlignment="Center"
                           FontFamily="{DynamicResource FontMono}" Foreground="{DynamicResource TextSecondaryBrush}"/>
                <Button DockPanel.Dock="Right" Click="AlleZu_Click" Style="{StaticResource ToolbarButton}" Margin="6,0,0,0" ToolTip="Alle Themen zuklappen" AutomationProperties.Name="Alle Themen zuklappen"><TextBlock Text="Alle zu"/></Button>
                <Button DockPanel.Dock="Right" Click="AlleAuf_Click" Style="{StaticResource ToolbarButton}" ToolTip="Alle Themen aufklappen" AutomationProperties.Name="Alle Themen aufklappen"><TextBlock Text="Alle auf"/></Button>
                <TextBox DockPanel.Dock="Right" x:Name="FeldSuche" Width="220" Margin="0,0,8,0" TextChanged="FeldSuche_TextChanged"
                         ToolTip="Feld suchen, zum Beispiel Baujahr" AutomationProperties.Name="Feld suchen"/>
            </DockPanel>
            <ScrollViewer VerticalScrollBarVisibility="Auto" Visibility="{Binding IsChecked, ElementName=OpenToggle, Converter={StaticResource BoolToVis}}">
                <ItemsControl x:Name="Themen" ItemsSource="{Binding SichtbareGruppen, ElementName=Root}" Margin="8">
                    <ItemsControl.ItemsPanel><ItemsPanelTemplate><UniformGrid Rows="1"/></ItemsPanelTemplate></ItemsControl.ItemsPanel>
                    <ItemsControl.ItemTemplate>
                        <DataTemplate>
                            <Expander IsExpanded="True" Margin="0,0,8,0" Header="{Binding Title}" Expanded="Thema_Toggled" Collapsed="Thema_Toggled">
                                <controls:RecordDetailsView IsCompactLayout="True" Groups="{Binding EinzelGruppe}"/>
                            </Expander>
                        </DataTemplate>
                    </ItemsControl.ItemTemplate>
                </ItemsControl>
            </ScrollViewer>
        </DockPanel>
    </Border>
</UserControl>
```

`BoolToVis` ist der in `App.xaml` registrierte `BooleanToVisibilityConverter` (mit `grep -n "BoolToVis" src/AuswertungPro.Next.UI/App.xaml` prüfen; sonst lokal als `<BooleanToVisibilityConverter x:Key="BoolToVis"/>` in `UserControl.Resources` anlegen).

`HaltungFelderDrawer.xaml.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using AuswertungPro.Next.UI.Views.Windows;

namespace AuswertungPro.Next.UI.Views.Pages.Haltungsansicht;

/// <summary>Eingabefelder unter der Liste: die vier Themen des RecordDetailsBuilders nebeneinander, jedes aufklappbar.</summary>
public partial class HaltungFelderDrawer : UserControl
{
    public HaltungFelderDrawer() => InitializeComponent();

    public static readonly DependencyProperty TitelProperty = DependencyProperty.Register(nameof(Titel), typeof(string), typeof(HaltungFelderDrawer), new PropertyMetadata(string.Empty));
    public string Titel { get => (string)GetValue(TitelProperty); set => SetValue(TitelProperty, value); }

    public static readonly DependencyProperty GroupsProperty = DependencyProperty.Register(nameof(Groups), typeof(IReadOnlyList<RecordDetailGroup>), typeof(HaltungFelderDrawer), new PropertyMetadata(null, (d, _) => ((HaltungFelderDrawer)d).Filtern()));
    public IReadOnlyList<RecordDetailGroup>? Groups { get => (IReadOnlyList<RecordDetailGroup>?)GetValue(GroupsProperty); set => SetValue(GroupsProperty, value); }

    public static readonly DependencyProperty SichtbareGruppenProperty = DependencyProperty.Register(nameof(SichtbareGruppen), typeof(IReadOnlyList<ThemaAnzeige>), typeof(HaltungFelderDrawer), new PropertyMetadata(null));
    public IReadOnlyList<ThemaAnzeige>? SichtbareGruppen { get => (IReadOnlyList<ThemaAnzeige>?)GetValue(SichtbareGruppenProperty); private set => SetValue(SichtbareGruppenProperty, value); }

    /// <summary>Ein Thema mit genau einer Gruppe, damit RecordDetailsView es unveraendert rendert.</summary>
    public sealed record ThemaAnzeige(string Title, IReadOnlyList<RecordDetailGroup> EinzelGruppe);

    private void FeldSuche_TextChanged(object sender, TextChangedEventArgs e) => Filtern();

    private void Filtern()
    {
        var q = (FeldSuche?.Text ?? string.Empty).Trim();
        var gruppen = Groups ?? Array.Empty<RecordDetailGroup>();
        SichtbareGruppen = gruppen
            .Select(g => q.Length == 0 ? g : g with { Items = g.Items.Where(i => i.Label.Contains(q, StringComparison.OrdinalIgnoreCase)).ToList() })
            .Where(g => g.Items.Count > 0)
            .Select(g => new ThemaAnzeige(g.Title, new[] { g }))
            .ToList();
    }

    private void AlleAuf_Click(object sender, RoutedEventArgs e) => SetzeAlle(true);
    private void AlleZu_Click(object sender, RoutedEventArgs e) => SetzeAlle(false);
    private void Thema_Toggled(object sender, RoutedEventArgs e) { }

    private void SetzeAlle(bool offen)
    {
        for (var i = 0; i < Themen.Items.Count; i++)
            if (Themen.ItemContainerGenerator.ContainerFromIndex(i) is ContentPresenter cp && FindExpander(cp) is { } ex) ex.IsExpanded = offen;
    }

    private static Expander? FindExpander(DependencyObject d)
    {
        for (var i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(d); i++)
        {
            var c = System.Windows.Media.VisualTreeHelper.GetChild(d, i);
            if (c is Expander ex) return ex;
            if (FindExpander(c) is { } inner) return inner;
        }
        return null;
    }
}
```

`RecordDetailItem.Label` existiert (`RecordDetailsModels.cs:37`). `RecordDetailGroup` ist ein `record` mit `Items`, deshalb funktioniert `with`.

- [ ] **Step 6: Übersicht rechts**

`HaltungUebersichtPanel.xaml`:

```xml
<UserControl x:Class="AuswertungPro.Next.UI.Views.Pages.Haltungsansicht.HaltungUebersichtPanel"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:local="clr-namespace:AuswertungPro.Next.UI.Views.Pages.Haltungsansicht"
             xmlns:dp="clr-namespace:AuswertungPro.Next.UI.DataPage"
             xmlns:ui="clr-namespace:AuswertungPro.Next.UI.Controls"
             x:Name="Root">
    <UserControl.Resources>
        <local:SchadenMeterConverter x:Key="SchadenMeterConv"/>
        <local:SchadenKlartextConverter x:Key="SchadenKlartextConv"/>
        <local:ZustandsklasseBrushConverter x:Key="ZkBrushConv"/>
        <dp:ZustandsklasseInkConverter x:Key="ZkInkConv"/>
    </UserControl.Resources>
    <Border Background="{DynamicResource CardBrush}" BorderBrush="{DynamicResource BorderBrush}" BorderThickness="1" CornerRadius="{DynamicResource RadiusL}" Padding="12">
        <DockPanel DataContext="{Binding Record, ElementName=Root}">
            <DockPanel DockPanel.Dock="Top" Margin="0,0,0,8">
                <TextBlock Text="Übersicht" FontSize="{DynamicResource TextL}" FontWeight="SemiBold" Foreground="{DynamicResource TextBrush}"/>
                <TextBlock Text="{Binding Fields[Haltungsname]}" Margin="10,0,0,0" VerticalAlignment="Center" FontFamily="{DynamicResource FontMono}" Foreground="{DynamicResource TextSecondaryBrush}"/>
                <Border DockPanel.Dock="Right" HorizontalAlignment="Right" Width="34" Height="22" CornerRadius="{DynamicResource RadiusS}"
                        Background="{Binding Fields[Zustandsklasse], Converter={StaticResource ZkBrushConv}}">
                    <TextBlock Text="{Binding Fields[Zustandsklasse], StringFormat=Z{0}}" HorizontalAlignment="Center" VerticalAlignment="Center"
                               FontSize="{DynamicResource TextS}" FontWeight="SemiBold" Foreground="{Binding Fields[Zustandsklasse], Converter={StaticResource ZkInkConv}}"/>
                </Border>
            </DockPanel>
            <UniformGrid DockPanel.Dock="Top" Columns="2" Margin="0,0,0,8">
                <StackPanel Margin="0,0,8,6"><TextBlock Text="Material" FontSize="{DynamicResource TextXS}" Foreground="{DynamicResource MutedBrush}"/><TextBlock Text="{Binding Fields[Rohrmaterial]}" FontFamily="{DynamicResource FontMono}"/></StackPanel>
                <StackPanel Margin="0,0,0,6"><TextBlock Text="DN mm" FontSize="{DynamicResource TextXS}" Foreground="{DynamicResource MutedBrush}"/><TextBlock Text="{Binding Fields[DN_mm]}" FontFamily="{DynamicResource FontMono}"/></StackPanel>
                <StackPanel Margin="0,0,8,6"><TextBlock Text="Länge m" FontSize="{DynamicResource TextXS}" Foreground="{DynamicResource MutedBrush}"/><TextBlock Text="{Binding Fields[Haltungslaenge_m]}" FontFamily="{DynamicResource FontMono}"/></StackPanel>
                <StackPanel Margin="0,0,0,6"><TextBlock Text="Inspektion" FontSize="{DynamicResource TextXS}" Foreground="{DynamicResource MutedBrush}"/><TextBlock Text="{Binding Fields[Datum_Jahr]}" FontFamily="{DynamicResource FontMono}"/></StackPanel>
            </UniformGrid>
            <TextBlock DockPanel.Dock="Top" Text="Primäre Schäden" FontSize="{DynamicResource TextM}" FontWeight="SemiBold" Foreground="{DynamicResource TextBrush}" Margin="0,4,0,6"/>
            <ListBox ItemsSource="{Binding Entries, ElementName=Root}" BorderThickness="0" Background="Transparent"
                     MouseDoubleClick="Schaden_MouseDoubleClick" AutomationProperties.Name="Primäre Schäden">
                <ListBox.ItemTemplate>
                    <DataTemplate>
                        <Grid Margin="0,2">
                            <Grid.ColumnDefinitions><ColumnDefinition Width="Auto"/><ColumnDefinition Width="*"/><ColumnDefinition Width="Auto"/></Grid.ColumnDefinitions>
                            <Border Background="{DynamicResource AccentSubtleBrush}" CornerRadius="{DynamicResource RadiusS}" Padding="6,2" Margin="0,0,8,0">
                                <TextBlock Text="{Binding Code}" FontFamily="{DynamicResource FontMono}" FontWeight="SemiBold" FontSize="{DynamicResource TextS}" Foreground="{DynamicResource TextBrush}"/>
                            </Border>
                            <TextBlock Grid.Column="1" Text="{Binding Converter={StaticResource SchadenKlartextConv}}" TextTrimming="CharacterEllipsis" VerticalAlignment="Center"/>
                            <TextBlock Grid.Column="2" Text="{Binding Converter={StaticResource SchadenMeterConv}}" FontFamily="{DynamicResource FontMono}" Foreground="{DynamicResource TextSecondaryBrush}" Margin="8,0,0,0" VerticalAlignment="Center"/>
                        </Grid>
                    </DataTemplate>
                </ListBox.ItemTemplate>
            </ListBox>
        </DockPanel>
    </Border>
</UserControl>
```

`HaltungUebersichtPanel.xaml.cs`:

```csharp
using System;
using System.Collections;
using System.Windows;
using System.Windows.Controls;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Views.Pages.Haltungsansicht;

/// <summary>Übersicht rechts neben der Liste: Eckdaten und Primaere Schaeden der gewaehlten Haltung, nur lesend.</summary>
public partial class HaltungUebersichtPanel : UserControl
{
    public HaltungUebersichtPanel() => InitializeComponent();

    public static readonly DependencyProperty RecordProperty = DependencyProperty.Register(nameof(Record), typeof(HaltungRecord), typeof(HaltungUebersichtPanel));
    public HaltungRecord? Record { get => (HaltungRecord?)GetValue(RecordProperty); set => SetValue(RecordProperty, value); }

    public static readonly DependencyProperty EntriesProperty = DependencyProperty.Register(nameof(Entries), typeof(IEnumerable), typeof(HaltungUebersichtPanel));
    public IEnumerable? Entries { get => (IEnumerable?)GetValue(EntriesProperty); set => SetValue(EntriesProperty, value); }

    /// <summary>Doppelklick auf einen Schaden: dieselbe Aktion wie in der Haltungsansicht (Beobachtungen).</summary>
    public Action<HaltungRecord>? BeobachtungenRequested { get; set; }

    private void Schaden_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (Record is { } r) BeobachtungenRequested?.Invoke(r);
    }
}
```

- [ ] **Step 7: Arbeitsfläche in DataPage einbauen**

In `DataPage.xaml` den `GridHost` (Zeile 446) um zwei Bereiche erweitern. Die vorhandenen Kinder (`FilterChips`, `UndockedPlaceholder`, `Grid`, `EmptyTablePlaceholder`, `HaltungsansichtView`) bleiben; das `DataGrid` und die Platzhalter rücken in eine innere Spalte. Neue Rasterdefinition von `GridHost`:

```xml
<Grid x:Name="GridHost" Grid.Row="2">
    <Grid.RowDefinitions>
        <RowDefinition Height="Auto"/>                                  <!-- FilterChips -->
        <RowDefinition Height="*"/>                                     <!-- Liste + Uebersicht -->
        <RowDefinition x:Name="DrawerSplitterRow" Height="6"/>
        <RowDefinition x:Name="DrawerRow" Height="220" MinHeight="120"/>
    </Grid.RowDefinitions>
    <Grid.ColumnDefinitions>
        <ColumnDefinition Width="*"/>
        <ColumnDefinition x:Name="SideSplitterCol" Width="6"/>
        <ColumnDefinition x:Name="SideCol" Width="320" MinWidth="240" MaxWidth="560"/>
    </Grid.ColumnDefinitions>
    <uc:FilterChipBar x:Name="FilterChips" Grid.Row="0" Grid.ColumnSpan="3" Margin="2,0,2,6"/>
    <!-- bisherige Kinder: Grid.Row="1" Grid.Column="0" -->
    <GridSplitter Grid.Row="1" Grid.Column="1" Width="6" HorizontalAlignment="Stretch" Background="{DynamicResource BorderBrush}"
                  behaviors:SplitterPersistenceBehavior.IsEnabled="True" behaviors:SplitterPersistenceBehavior.SplitterKey="HaltungenUebersicht"
                  behaviors:SplitterPersistenceBehavior.TargetColumnIndex="2" ToolTip="Breite der Übersicht" AutomationProperties.Name="Breite der Übersicht"/>
    <haltung:HaltungUebersichtPanel x:Name="Uebersicht" Grid.Row="1" Grid.Column="2" Margin="8,0,0,0"
                                    Record="{Binding Selected}" Entries="{Binding SelectedProtocolEntries}"/>
    <GridSplitter Grid.Row="2" Grid.ColumnSpan="3" Height="6" HorizontalAlignment="Stretch" ResizeDirection="Rows" Background="{DynamicResource BorderBrush}"
                  behaviors:SplitterPersistenceBehavior.IsEnabled="True" behaviors:SplitterPersistenceBehavior.SplitterKey="HaltungenEingabefelder"
                  behaviors:SplitterPersistenceBehavior.TargetRowIndex="3" ToolTip="Höhe der Eingabefelder" AutomationProperties.Name="Höhe der Eingabefelder"/>
    <haltung:HaltungFelderDrawer x:Name="FelderDrawer" Grid.Row="3" Grid.ColumnSpan="3" Margin="0,4,0,0"/>
</Grid>
```

`HaltungsansichtView` bleibt in `Grid.Row="1" Grid.Column="0" Grid.ColumnSpan="3"` und wird, wenn sie sichtbar ist, weiter über den Toggle mit dem `DataGrid` getauscht; zusätzlich werden `Uebersicht`, beide Splitter und `FelderDrawer` bei aktiver Haltungsansicht ausgeblendet. Die Standardansicht wechselt auf die neue Arbeitsfläche: in `DataPage.xaml.cs:80-82` `HaltungsansichtToggle.IsChecked = true;` durch `HaltungsansichtToggle.IsChecked = !vmSettings.ShowHaltungenNovaLayout;` ersetzen und die Sichtbarkeiten entsprechend setzen (die Setter liegen in `HaltungsansichtToggle_Changed`, Zeile 487, dort die vier neuen Elemente mit umschalten). In `AppSettings.cs` neben `HaltungsansichtSchadenHeight`:

```csharp
/// <summary>Nova-Etappe 1: Liste mit Uebersicht rechts und Eingabefeldern unten als Standard. false = bisherige Haltungsansicht.</summary>
public bool ShowHaltungenNovaLayout { get; set; } = true;
```

Neue Datei `DataPage.NovaWorkspace.cs` (partial):

```csharp
using System;
using System.Windows;
using AuswertungPro.Next.UI.DataPage;
using AuswertungPro.Next.UI.ViewModels.Pages;

namespace AuswertungPro.Next.UI.Views.Pages;

public partial class DataPage
{
    private void InitNovaWorkspace(DataPageViewModel vm)
    {
        FelderDrawer.Titel = vm.Selected?.GetFieldValue("Haltungsname") ?? string.Empty;
        FelderDrawer.Groups = vm.Selected is { } r ? BuildHaltungRecordDetailsForAnsicht(r) : null;
        Uebersicht.BeobachtungenRequested = record => RouteHaltungsansichtAction("beobachtungen", record);
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName != nameof(DataPageViewModel.Selected)) return;
            FelderDrawer.Titel = vm.Selected?.GetFieldValue("Haltungsname") ?? string.Empty;
            FelderDrawer.Groups = vm.Selected is { } sel ? BuildHaltungRecordDetailsForAnsicht(sel) : null;
        };
        SizeChanged += (_, _) => ApplyDrawerHeight(vm);
        ApplyDrawerHeight(vm);
    }

    private void ApplyDrawerHeight(DataPageViewModel vm)
    {
        if (GridHost.ActualHeight <= 0) return;
        var gespeichert = vm.Settings.ViewCustomization.SplitterSizes.TryGetValue("HaltungenEingabefelder", out var s) ? s : (double?)null;
        var hoehe = DataPageWorkspaceLayoutPolicy.DrawerHeight(GridHost.ActualHeight, vm.GridMinRowHeight, 32, gespeichert);
        DrawerRow.Height = new GridLength(hoehe);
    }
}
```

`vm.Settings.ViewCustomization` ist der Ort, an dem `SplitterPersistenceBehavior` speichert (siehe `SplitterPersistenceBehavior.cs:138`); mit `grep -n "ViewCustomization" src/AuswertungPro.Next.UI/AppSettings.cs` den genauen Eigenschaftsnamen prüfen. `InitNovaWorkspace(vm)` wird in `DataPage.xaml.cs` dort aufgerufen, wo `HaltungsansichtView.DetailBuilder` gesetzt wird (Zeile 74). `RouteHaltungsansichtAction` existiert in `DataPage.xaml.cs:75`.

- [ ] **Step 8: Wächter für das neue Layout**

```csharp
// tests/AuswertungPro.Next.UI.Tests/DesignAuditNovaHaltungenTests.cs
using System.IO;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

public sealed class DesignAuditNovaHaltungenTests
{
    private static string Xaml(params string[] parts) => File.ReadAllText(RepoFile(new[] { "src", "AuswertungPro.Next.UI" }.Concat(parts).ToArray()));

    [Fact]
    public void Haltungen_hat_Uebersicht_rechts_und_Eingabefelder_unten_mit_gespeicherten_Trennlinien()
    {
        var xaml = Xaml("Views", "Pages", "DataPage.xaml");
        Assert.Contains("HaltungUebersichtPanel", xaml);
        Assert.Contains("HaltungFelderDrawer", xaml);
        Assert.Contains("SplitterKey=\"HaltungenUebersicht\"", xaml);
        Assert.Contains("SplitterKey=\"HaltungenEingabefelder\"", xaml);
        Assert.Contains("x:Name=\"HaltungsansichtToggle\"", xaml); // die alte Ansicht bleibt erreichbar
    }

    [Fact]
    public void Eingabefelder_zeigen_die_vier_Themen_ueber_RecordDetailsView()
    {
        var xaml = Xaml("Views", "Pages", "Haltungsansicht", "HaltungFelderDrawer.xaml");
        Assert.Contains("controls:RecordDetailsView", xaml);
        Assert.Contains("<Expander", xaml);
        Assert.Contains("AutomationProperties.Name=\"Feld suchen\"", xaml);
    }

    [Fact]
    public void Uebersicht_verwendet_die_Zustandsklassen_Tinte()
    {
        var xaml = Xaml("Views", "Pages", "Haltungsansicht", "HaltungUebersichtPanel.xaml");
        Assert.Contains("ZustandsklasseInkConverter", xaml);
        Assert.DoesNotContain("Foreground=\"White\"", xaml);
    }
}
```

- [ ] **Step 9: Alles laufen lassen und am Programm prüfen**

Run: `dotnet build AuswertungPro.sln` und `dotnet test tests/AuswertungPro.Next.UI.Tests`
Expected: 0 Fehler, alle Tests grün (inklusive `MaintainabilityFitnessTests`: `DataPage.xaml.cs` bleibt unter 1000 Zeilen, weil die neue Logik in `DataPage.NovaWorkspace.cs` und `DataPage.ColumnViews.cs` liegt).

Am laufenden Programm bei Fenstergrösse 1366 × 768 (Windows-Skalierung 100 %, danach 125 % und 150 %): Haltungen öffnen, mindestens sieben Zeilen vollständig sichtbar, Übersicht rechts zeigt die gewählte Haltung, Eingabefelder unten in vier Themen, Feldsuche „Baujahr" lässt nur dieses Feld stehen, Trennlinien merken sich ihre Lage nach Neustart, Toggle „Haltungsansicht" zeigt weiter die alte Ansicht. Ergebnis mit Bildschirmfotos unter `docs/reviews/2026-09-06-nova/wpf-etappe-1/` ablegen.

- [ ] **Step 10: Commit**

```bash
git add src/AuswertungPro.Next.UI/DataPage/DataPageWorkspaceLayoutPolicy.cs src/AuswertungPro.Next.UI/Views/Pages/Haltungsansicht/HaltungFelderDrawer.xaml src/AuswertungPro.Next.UI/Views/Pages/Haltungsansicht/HaltungFelderDrawer.xaml.cs src/AuswertungPro.Next.UI/Views/Pages/Haltungsansicht/HaltungUebersichtPanel.xaml src/AuswertungPro.Next.UI/Views/Pages/Haltungsansicht/HaltungUebersichtPanel.xaml.cs src/AuswertungPro.Next.UI/Views/Pages/DataPage.NovaWorkspace.cs src/AuswertungPro.Next.UI/Views/Pages/DataPage.xaml src/AuswertungPro.Next.UI/Views/Pages/DataPage.xaml.cs src/AuswertungPro.Next.UI/AppSettings.cs tests/AuswertungPro.Next.UI.Tests/DataPageWorkspaceLayoutPolicyTests.cs tests/AuswertungPro.Next.UI.Tests/DesignAuditNovaHaltungenTests.cs
git commit -m "Haltungen: Liste, Uebersicht rechts und Eingabefelder unten mit gespeicherten Trennlinien"
```

---

### Task 8: Dokumentation und Abnahme der Etappe

**Files:**
- Modify: `CLAUDE.md` (neuer Unterabschnitt nach „Design-Feinschliff 2026-09-03")
- Create: `docs/reviews/2026-09-06-nova/wpf-etappe-1/ABNAHME.md`

- [ ] **Step 1: CLAUDE.md ergänzen**

```markdown
### Nova-Etappe 1 (2026-09, Leiste und Haltungsseite)

Quelle ist der freigegebene Prototyp `docs/reviews/2026-09-06-nova/optimiert/v2/`. Umgesetzt und
durch Waechter gehalten (`ZustandsklasseInkPolicyTests`, `ShellNavigationGroupsTests`,
`DataPageColumnViewCatalogTests`, `DataPageWorkspaceLayoutPolicyTests`, `DesignAuditNovaHaltungenTests`):

- Zustandsklassen-Marken tragen eine Textfarbe je Klasse mit mindestens 4,5:1 (`ZustandsklasseInkPolicy`).
- KI-Farbtoken `KiBrush`, `KiSubtleBrush`, `KiTextBrush` in beiden Themes, getrennt vom Akzent.
- Die Leiste ist in Projekt, Daten, Bewertung, System gruppiert (`ShellNavigationGroups`); der
  Systemmonitor ist ein Aufklapper „Analyse bereit", Schriftskala gilt auch fuer Stil-Setter.
- Haltungen: eine Hauptaktion (Speichern), `Video pruefen`, `Weitere Aktionen`; Spaltenansichten
  Kompakt, Stammdaten, Bewertung, Sanierung, Kosten, Alle (`DataPageColumnViewCatalog`, gespeichert
  in `DataPageLayout.ActiveColumnView`); Standardlayout Liste | Uebersicht rechts | Eingabefelder
  unten mit gespeicherten Trennlinien (`SplitterKey` HaltungenUebersicht / HaltungenEingabefelder),
  mindestens sieben sichtbare Zeilen (`DataPageWorkspaceLayoutPolicy`). Die alte Haltungsansicht
  bleibt ueber den Toggle erreichbar (`AppSettings.ShowHaltungenNovaLayout`).
- Nicht umgesetzt (Etappe 2): Uebersichtsseite, Schaechte, Player, Training Studio, Chip „Naechste
  Aufgabe" (braucht einen fachlichen Pruefstatus je Haltung), Palettenwechsel Glas/Cockpit.
```

- [ ] **Step 2: Abnahmeprotokoll**

`ABNAHME.md` mit Tabelle: Prüfpunkt, Ergebnis (bestanden / fehlgeschlagen / nicht geprüft), Beleg (Testname oder Bildschirmfoto). Pflichtzeilen: Build 0 Fehler; alle UI-Tests; sieben Zeilen bei 1366 × 768 (Foto); Skalierung 125 % und 150 % (Foto); jede Aktion aus „Weitere Aktionen" einmal ausgelöst; Spaltenansicht nach Neustart erhalten; alte Haltungsansicht erreichbar; Kundenoriginale unberührt (keine Datei ausserhalb `src`, `tests`, `docs`, `CLAUDE.md` geändert, mit `git status` belegt).

- [ ] **Step 3: Commit**

```bash
git add CLAUDE.md docs/reviews/2026-09-06-nova/wpf-etappe-1/ABNAHME.md
git commit -m "Doku: Nova-Etappe 1 in CLAUDE.md und Abnahmeprotokoll"
```

---

## Selbstprüfung

- Spec-Abdeckung Etappe 1: Leiste (Gruppen, Monitor-Aufklapper) Tasks 3 und 4; Haltungen (Werkzeugleiste, Ansichten, Arbeitsfläche, Kontrast der Marken) Tasks 1, 5, 6, 7; KI-Token Task 2; Doku Task 8. Bewusst ausgelassen und in Task 8 benannt: Übersicht, Schächte, Player, Training Studio, „Nächste Aufgabe", Palettenwechsel.
- Platzhalter: keine offenen „später"-Stellen; zwei Stellen verlangen vor dem Schreiben ein `grep` (Feldname-Abbildung der Spalten in Task 6, `ViewCustomization`-Eigenschaft in Task 7), jeweils mit genauem Befehl.
- Typen: `ZustandsklasseInkPolicy.InkFor`/`Contrast` (Task 1) werden in Task 7 über den Konverter verwendet; `DataPageColumnViewCatalog.Views`/`Resolve` (Task 6) sind in Test, Controller und XAML gleich benannt; `HaltungFelderDrawer.Groups`/`Titel` und `HaltungUebersichtPanel.Record`/`Entries` (Task 7) stimmen zwischen XAML, Code-behind und `DataPage.NovaWorkspace.cs` überein.
