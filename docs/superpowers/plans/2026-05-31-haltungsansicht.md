# Editierbare Haltungsansicht — Implementierungsplan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Eine zweite, master-detail-artige Darstellung der Haltungsdaten neben der bestehenden Tabelle (Liste links, editierbares Detail rechts), bei der alle Felder editierbar sind, dieselben Dropdown-Begriffe haben und Änderungen in beiden Sichten redundant erscheinen.

**Architecture:** Eine einzige Datenquelle (`Project.Data` → dieselben `HaltungRecord`-Objekte). Beide Sichten binden an `DataPageViewModel.Records` + `Selected`. Die komplette editierbare Detail-Darstellung existiert bereits als `RecordDetailsWindow`; sie wird in ein wiederverwendbares `RecordDetailsView`-UserControl extrahiert und sowohl vom bestehenden Popup als auch von der neuen eingebetteten Ansicht genutzt. Der Schreibpfad (`RecordDetailItem.Value` → `CommitHaltungDetailField` → `SetFieldValue(Manual, userEdited:true)` + `EnsureOptionForField` + `ScheduleAutoSave`) bleibt unverändert — dadurch ist die bidirektionale Synchronisation bauartbedingt garantiert.

**Tech Stack:** .NET 10, WPF/MVVM, xUnit (`AuswertungPro.Next.UI.Tests`), CommunityToolkit.Mvvm.

---

## Erkenntnis aus dem Code (Abweichung von der Spec)

Die Spec (`docs/superpowers/specs/2026-05-31-haltungsansicht-design.md`) schlug die neuen Dateien `HaltungFieldEditor.cs`, `DetailFieldGroups.cs` und `HaltungDetailView` vor. Beim Grounding für diesen Plan zeigte sich: **diese Bausteine existieren bereits** und müssen nur wiederverwendet werden:

| Spec-Vorschlag | Realität im Code (wiederverwenden) |
|---|---|
| `HaltungFieldEditor.Commit(...)` (gemeinsamer Schreibpfad) | `DataPage.xaml.cs:1260` `CommitHaltungDetailField` + `RecordDetailItem.Value`-Setter (`RecordDetailsModels.cs:69-84`). Routet bereits über `SetFieldValue` + `EnsureOptionForField` + `ScheduleAutoSave`. |
| `DetailFieldGroups.cs` (Gruppen→Felder, testbar) | `DataPageRecordDetailsBuilder.Build/ResolveGroup` (`src/.../DataPage/DataPageRecordDetailsBuilder.cs`) + Tests (`DataPageRecordDetailsBuilderTests.cs`). |
| `HaltungDetailView` (gruppierte Karten, dynamisches Feld-Rendering) | `RecordDetailsWindow.xaml` (Body) + `RecordDetailEditorTemplateSelector` (`RecordDetailsModels.cs:100`). FieldType→Control fertig. |
| `HaltungListView` + `HaltungsansichtViewModel` | Liste = `ListBox` an `Records`/`Selected` (geteilte Auswahl frei). Kein neues ViewModel — die DataPage-vm reicht. |

**Konsequenz für den Plan:** Statt Detail-Editor + Gruppen + Commit-Pfad neu zu bauen, wird das bestehende `RecordDetailsWindow` in ein wiederverwendbares Control zerlegt (DRY, Popup bleibt funktionsfähig) und in eine eingebettete Master-Detail-Ansicht mit Umschalter gehängt. Echter Neu-Code: drei kleine reine Helfer (mit Tests), ein wiederverwendbares Detail-Control, eine Master-Detail-View, ein Umschalter.

---

## File-Struktur

| Datei | Verantwortung | Status |
|---|---|---|
| `tests/AuswertungPro.Next.UI.Tests/DataPageRecordDetailsBuilderTests.cs` | Vollständigkeits-Test „jedes Katalogfeld genau einmal editierbar" | erweitern |
| `src/AuswertungPro.Next.UI/DataPage/ZustandsklasseColorPalette.cs` | öffentliche, testbare Zustandsklasse-Farbquelle (eine Quelle für Tabelle + Chip) | neu |
| `tests/AuswertungPro.Next.UI.Tests/ZustandsklasseColorPaletteTests.cs` | Test der Farbzuordnung | neu |
| `src/AuswertungPro.Next.UI/Views/Pages/ZustandsklasseCellStyleFactory.cs` | nutzt jetzt die gemeinsame Palette (1 Zeile) | ändern |
| `src/AuswertungPro.Next.UI/DataPage/HaltungSummaryFormatter.cs` | reine Formatierung der Listen-Zeile „DN 300 · 45.30 m · Mischabwasser" | neu |
| `tests/AuswertungPro.Next.UI.Tests/HaltungSummaryFormatterTests.cs` | Test der Formatierung | neu |
| `src/AuswertungPro.Next.UI/Views/Controls/RecordDetailsView.xaml(.cs)` | wiederverwendbares Detail-Control (aus `RecordDetailsWindow` extrahiert) | neu (Move) |
| `src/AuswertungPro.Next.UI/Views/Windows/RecordDetailsWindow.xaml(.cs)` | hostet jetzt `RecordDetailsView`; öffentliche Ctor-Signatur unverändert | ändern |
| `src/AuswertungPro.Next.UI/Views/Pages/Haltungsansicht/HaltungsansichtView.xaml(.cs)` | Master-Detail: `ListBox` links + `RecordDetailsView` rechts | neu |
| `src/AuswertungPro.Next.UI/Views/Pages/Haltungsansicht/HaltungListItemConverters.cs` | Wert-Konverter für Listen-Zeile (Summary, Chip-Brush) | neu |
| `src/AuswertungPro.Next.UI/Views/Pages/DataPage.xaml(.cs)` | Umschalter Tabelle ↔ Haltungsansicht; setzt `DetailBuilder` | ändern |

**Abgrenzung (unverändert aus Spec):** Tabelle/Spalten/Export bleiben 1:1. Keine zweite Datenhaltung. Primäre Schäden = Anzeige (Codieren bleibt im Player). Keine neue Geschäftslogik.

---

### Task 1: Feld-Vollständigkeit absichern (beweist „alle Felder editierbar")

**Files:**
- Test: `tests/AuswertungPro.Next.UI.Tests/DataPageRecordDetailsBuilderTests.cs`

Dies ist ein **Charakterisierungs-Test** des bestehenden Builders: Er friert die Garantie ein, dass jedes Feld aus `FieldCatalog.ColumnOrder` genau einmal als editierbares Detail-Item erscheint. Er muss sofort grün sein (der Builder erfüllt das bereits, `DataPageRecordDetailsBuilder.cs:23`). Wird der Builder später versehentlich Felder verlieren/doppeln, schlägt er fehl.

- [ ] **Step 1: Test ergänzen**

In `DataPageRecordDetailsBuilderTests.cs` ans Ende der Klasse einfügen:

```csharp
    [Fact]
    public void Build_covers_every_catalog_field_exactly_once()
    {
        var record = new HaltungRecord();

        var groups = DataPageRecordDetailsBuilder.Build(
            record,
            fieldName => new RecordDetailItem(fieldName, fieldName, _ => { }));

        // Label == Feldname (durch die Test-Factory oben).
        var renderedFields = groups
            .SelectMany(g => g.Items)
            .Select(item => item.Label)
            .ToList();

        // Jedes Katalogfeld erscheint genau einmal.
        foreach (var field in FieldCatalog.ColumnOrder)
            Assert.Equal(1, renderedFields.Count(f => f == field));

        // Keine Katalogfelder fehlen.
        var missing = FieldCatalog.ColumnOrder
            .Where(f => !renderedFields.Contains(f))
            .ToList();
        Assert.Empty(missing);
    }
```

Ergänze oben in der Datei den Using, falls nicht vorhanden:

```csharp
using System.Linq;
```

(`FieldCatalog` liegt im Namespace `AuswertungPro.Next.UI.Views.Windows` — bereits über `using AuswertungPro.Next.UI.Views.Windows;` importiert; `FieldCatalog.ColumnOrder`/`HaltungRecord` sind erreichbar.)

- [ ] **Step 2: Test ausführen (muss grün sein)**

Run: `dotnet test tests/AuswertungPro.Next.UI.Tests/AuswertungPro.Next.UI.Tests.csproj --filter "FullyQualifiedName~DataPageRecordDetailsBuilderTests"`
Expected: PASS (alle Tests, inkl. `Build_covers_every_catalog_field_exactly_once`).

Falls FAIL: kein Build-Fehler beheben durch Anpassen des Tests — der Test dokumentiert die Soll-Garantie. Falls der Builder tatsächlich Felder verliert, ist das ein echter Bug → in diesem Task melden, nicht „wegtesten".

- [ ] **Step 3: Commit**

```bash
git add tests/AuswertungPro.Next.UI.Tests/DataPageRecordDetailsBuilderTests.cs
git commit -m "test(haltungsansicht): jedes Katalogfeld ist genau einmal editierbar"
```

---

### Task 2: `ZustandsklasseColorPalette` (eine testbare Farbquelle)

**Files:**
- Create: `src/AuswertungPro.Next.UI/DataPage/ZustandsklasseColorPalette.cs`
- Test: `tests/AuswertungPro.Next.UI.Tests/ZustandsklasseColorPaletteTests.cs`
- Modify: `src/AuswertungPro.Next.UI/Views/Pages/ZustandsklasseCellStyleFactory.cs`

Ziel: Der Listen-Chip braucht die gleiche Farbe wie die Tabellenspalte. Die Faktory hat die Palette + Normalisierung heute `private`; das Test-Projekt sieht keine `internal`-Typen (kein `InternalsVisibleTo`). Daher wird die Palette in eine **öffentliche, reine** Hilfsklasse gezogen und die Faktory darauf umgestellt (eine Quelle, Tabellenverhalten unverändert).

- [ ] **Step 1: Failing Test schreiben**

`tests/AuswertungPro.Next.UI.Tests/ZustandsklasseColorPaletteTests.cs`:

```csharp
using System.Windows.Media;
using AuswertungPro.Next.UI.DataPage;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

public sealed class ZustandsklasseColorPaletteTests
{
    [Theory]
    [InlineData("0", 0xFF, 0x00, 0x00)] // rot
    [InlineData("4", 0x92, 0xD0, 0x50)] // gruen
    public void TryGetBackground_maps_known_classes(string value, byte r, byte g, byte b)
    {
        var brush = Assert.IsType<SolidColorBrush>(ZustandsklasseColorPalette.TryGetBackground(value));
        Assert.Equal(Color.FromRgb(r, g, b), brush.Color);
    }

    [Theory]
    [InlineData("3.4", "3")] // rundet auf gueltige Klasse
    [InlineData("2,0", "2")] // Komma-Dezimal
    public void TryGetBackground_rounds_decimal_classes(string value, string equivalentInteger)
    {
        Assert.Equal(
            ((SolidColorBrush)ZustandsklasseColorPalette.TryGetBackground(equivalentInteger)!).Color,
            ((SolidColorBrush)ZustandsklasseColorPalette.TryGetBackground(value)!).Color);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("7")]
    [InlineData(null)]
    public void TryGetBackground_returns_null_for_unknown(string? value)
    {
        Assert.Null(ZustandsklasseColorPalette.TryGetBackground(value));
    }
}
```

- [ ] **Step 2: Test ausführen (muss fehlschlagen)**

Run: `dotnet test tests/AuswertungPro.Next.UI.Tests/AuswertungPro.Next.UI.Tests.csproj --filter "FullyQualifiedName~ZustandsklasseColorPaletteTests"`
Expected: FAIL/Compile-Fehler — `ZustandsklasseColorPalette` existiert nicht.

- [ ] **Step 3: Palette implementieren**

`src/AuswertungPro.Next.UI/DataPage/ZustandsklasseColorPalette.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows.Media;

namespace AuswertungPro.Next.UI.DataPage;

/// <summary>
/// Eine Quelle für die Zustandsklasse-Farben (Skala 0=rot … 4=grün).
/// Wird von der Tabellen-Zellfarbe (ZustandsklasseCellStyleFactory) und vom
/// Listen-Chip der Haltungsansicht genutzt. Farben aus Excel-Vorlage "Haltungen.xlsx".
/// </summary>
public static class ZustandsklasseColorPalette
{
    public static IReadOnlyDictionary<string, Brush> HaltungenPalette { get; } =
        new Dictionary<string, Brush>(StringComparer.Ordinal)
        {
            ["0"] = CreateBrush(0xFF, 0x00, 0x00),
            ["1"] = CreateBrush(0xFF, 0x66, 0x00),
            ["2"] = CreateBrush(0xFF, 0xFF, 0x00),
            ["3"] = CreateBrush(0xAE, 0xB1, 0x35),
            ["4"] = CreateBrush(0x92, 0xD0, 0x50)
        };

    /// <summary>Hintergrund-Brush für eine Zustandsklasse, oder null wenn unbekannt/leer.</summary>
    public static Brush? TryGetBackground(string? value)
    {
        var key = NormalizeClass(value);
        return HaltungenPalette.TryGetValue(key, out var brush) ? brush : null;
    }

    /// <summary>Normalisiert "0".."4", Dezimalwerte (gerundet) und Komma-Dezimale; sonst "".</summary>
    public static string NormalizeClass(object? value)
    {
        var text = (value?.ToString() ?? string.Empty).Trim();
        if (text.Length == 0)
            return string.Empty;

        if (char.IsDigit(text[0]))
        {
            var digit = text[0];
            return digit is >= '0' and <= '4' ? digit.ToString() : string.Empty;
        }

        var normalized = text.Replace(',', '.');
        if (!double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
            return string.Empty;

        var rounded = (int)Math.Round(number, MidpointRounding.AwayFromZero);
        return rounded is >= 0 and <= 4 ? rounded.ToString(CultureInfo.InvariantCulture) : string.Empty;
    }

    private static SolidColorBrush CreateBrush(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }
}
```

Hinweis: `NormalizeClass` ist 1:1 die bewährte Logik aus `ZustandsklasseCellStyleFactory.NormalizeClass` (`ZustandsklasseCellStyleFactory.cs:184-202`). Erstes-Zeichen-Ziffer-Regel beachtet: `"3.4"` beginnt mit `'3'` → liefert `"3"` (identisch zum bisherigen Tabellenverhalten).

- [ ] **Step 4: Test ausführen (muss grün sein)**

Run: `dotnet test tests/AuswertungPro.Next.UI.Tests/AuswertungPro.Next.UI.Tests.csproj --filter "FullyQualifiedName~ZustandsklasseColorPaletteTests"`
Expected: PASS.

- [ ] **Step 5: Faktory auf die gemeinsame Palette umstellen (eine Quelle)**

In `src/AuswertungPro.Next.UI/Views/Pages/ZustandsklasseCellStyleFactory.cs` das inline-Dictionary `HaltungenPalette` durch die gemeinsame Quelle ersetzen.

Ersetze:

```csharp
    private static readonly IReadOnlyDictionary<string, Brush> HaltungenPalette = new Dictionary<string, Brush>(StringComparer.Ordinal)
    {
        ["0"] = CreateBrush(0xFF, 0x00, 0x00),
        ["1"] = CreateBrush(0xFF, 0x66, 0x00),
        ["2"] = CreateBrush(0xFF, 0xFF, 0x00),
        ["3"] = CreateBrush(0xAE, 0xB1, 0x35),
        ["4"] = CreateBrush(0x92, 0xD0, 0x50)
    };
```

durch:

```csharp
    private static readonly IReadOnlyDictionary<string, Brush> HaltungenPalette =
        AuswertungPro.Next.UI.DataPage.ZustandsklasseColorPalette.HaltungenPalette;
```

`SchaechtePalette` bleibt unverändert (eigene Farben). `NormalizeClass`/`CreateBrush` in der Faktory bleiben (von `SchaechtePalette` genutzt). Die Farbwerte sind identisch → Tabellenfärbung unverändert.

- [ ] **Step 6: Build + alle UI-Tests (App geschlossen!)**

Run: `dotnet build src/AuswertungPro.Next.UI/AuswertungPro.Next.UI.csproj`
Expected: 0 Fehler, 0 Warnungen. (Bei MSB3027/MSB3021 Datei-Sperre → läuft SewerStudio.exe? App schließen, neu bauen.)

Run: `dotnet test tests/AuswertungPro.Next.UI.Tests/AuswertungPro.Next.UI.Tests.csproj`
Expected: PASS (alle).

- [ ] **Step 7: Commit**

```bash
git add src/AuswertungPro.Next.UI/DataPage/ZustandsklasseColorPalette.cs tests/AuswertungPro.Next.UI.Tests/ZustandsklasseColorPaletteTests.cs src/AuswertungPro.Next.UI/Views/Pages/ZustandsklasseCellStyleFactory.cs
git commit -m "feat(haltungsansicht): gemeinsame Zustandsklasse-Farbquelle (Tabelle + Chip)"
```

---

### Task 3: `HaltungSummaryFormatter` (reine Listen-Zeilen-Formatierung)

**Files:**
- Create: `src/AuswertungPro.Next.UI/DataPage/HaltungSummaryFormatter.cs`
- Test: `tests/AuswertungPro.Next.UI.Tests/HaltungSummaryFormatterTests.cs`

- [ ] **Step 1: Failing Test schreiben**

`tests/AuswertungPro.Next.UI.Tests/HaltungSummaryFormatterTests.cs`:

```csharp
using AuswertungPro.Next.UI.DataPage;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

public sealed class HaltungSummaryFormatterTests
{
    [Fact]
    public void FormatSummary_joins_all_parts()
    {
        Assert.Equal(
            "DN 300 · 45.30 m · Mischabwasser",
            HaltungSummaryFormatter.FormatSummary("300", "45.30", "Mischabwasser"));
    }

    [Fact]
    public void FormatSummary_skips_empty_parts()
    {
        Assert.Equal("DN 300 · Mischabwasser",
            HaltungSummaryFormatter.FormatSummary("300", "", "Mischabwasser"));
        Assert.Equal("45.30 m",
            HaltungSummaryFormatter.FormatSummary("  ", "45.30", null));
    }

    [Fact]
    public void FormatSummary_returns_empty_when_nothing_present()
    {
        Assert.Equal(string.Empty, HaltungSummaryFormatter.FormatSummary(null, null, null));
    }
}
```

- [ ] **Step 2: Test ausführen (muss fehlschlagen)**

Run: `dotnet test tests/AuswertungPro.Next.UI.Tests/AuswertungPro.Next.UI.Tests.csproj --filter "FullyQualifiedName~HaltungSummaryFormatterTests"`
Expected: FAIL — `HaltungSummaryFormatter` existiert nicht.

- [ ] **Step 3: Formatter implementieren**

`src/AuswertungPro.Next.UI/DataPage/HaltungSummaryFormatter.cs`:

```csharp
using System.Collections.Generic;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.DataPage;

/// <summary>
/// Reine Formatierung der einzeiligen Kurzbeschreibung einer Haltung
/// für die Listen-Spalte der Haltungsansicht: "DN 300 · 45.30 m · Mischabwasser".
/// Leere Teile werden ausgelassen.
/// </summary>
public static class HaltungSummaryFormatter
{
    public static string FormatSummary(string? dnMm, string? laengeM, string? nutzungsart)
    {
        var parts = new List<string>(3);

        if (!string.IsNullOrWhiteSpace(dnMm))
            parts.Add($"DN {dnMm.Trim()}");
        if (!string.IsNullOrWhiteSpace(laengeM))
            parts.Add($"{laengeM.Trim()} m");
        if (!string.IsNullOrWhiteSpace(nutzungsart))
            parts.Add(nutzungsart.Trim());

        return string.Join(" · ", parts);
    }

    public static string FormatSummary(HaltungRecord record)
        => FormatSummary(
            record.GetFieldValue("DN_mm"),
            record.GetFieldValue("Haltungslaenge_m"),
            record.GetFieldValue("Nutzungsart"));
}
```

- [ ] **Step 4: Test ausführen (muss grün sein)**

Run: `dotnet test tests/AuswertungPro.Next.UI.Tests/AuswertungPro.Next.UI.Tests.csproj --filter "FullyQualifiedName~HaltungSummaryFormatterTests"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/AuswertungPro.Next.UI/DataPage/HaltungSummaryFormatter.cs tests/AuswertungPro.Next.UI.Tests/HaltungSummaryFormatterTests.cs
git commit -m "feat(haltungsansicht): reine Listen-Zeilen-Formatierung (DN/Laenge/Nutzung)"
```

---

### Task 4: `RecordDetailsView` aus `RecordDetailsWindow` extrahieren (DRY, kein Verhaltenswechsel)

**Files:**
- Create: `src/AuswertungPro.Next.UI/Views/Controls/RecordDetailsView.xaml`
- Create: `src/AuswertungPro.Next.UI/Views/Controls/RecordDetailsView.xaml.cs`
- Modify: `src/AuswertungPro.Next.UI/Views/Windows/RecordDetailsWindow.xaml`
- Modify: `src/AuswertungPro.Next.UI/Views/Windows/RecordDetailsWindow.xaml.cs`

Ziel: Den gesamten Detail-Body (Resources/Templates/TemplateSelector + Header + ScrollViewer + Gruppen-Karten) sowie die Eingabe-Handler (`NumericTextBox_*`, `EditorComboBox_*`) in ein wiederverwendbares UserControl ziehen. `RecordDetailsWindow` hostet danach nur noch dieses Control. **Reine Verschiebung, keine Verhaltensänderung.** Aufrufer (`DataPage.xaml.cs:1172`, `SchaechtePage.xaml.cs:1452`) bleiben unangetastet (gleiche Ctor-Signatur).

- [ ] **Step 1: `RecordDetailsView.xaml` anlegen**

`src/AuswertungPro.Next.UI/Views/Controls/RecordDetailsView.xaml` — übernimmt die `Window.Resources` als `UserControl.Resources` und den Inhalt der drei Grid-Rows (Header, ScrollViewer, Footer). `x:Name="Root"` für DataContext-Bindung an die eigenen Properties:

```xml
<UserControl x:Class="AuswertungPro.Next.UI.Views.Controls.RecordDetailsView"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:windows="clr-namespace:AuswertungPro.Next.UI.Views.Windows"
             x:Name="Root">
    <UserControl.Resources>
        <ContextMenu x:Key="ManagedOptionsContextMenu"
                     x:Shared="False"
                     DataContext="{Binding PlacementTarget.DataContext, RelativeSource={RelativeSource Self}}">
            <MenuItem Header="Liste bearbeiten..." Command="{Binding EditOptionsCommand}">
                <MenuItem.Style>
                    <Style TargetType="MenuItem" BasedOn="{StaticResource {x:Type MenuItem}}">
                        <Style.Triggers>
                            <DataTrigger Binding="{Binding EditOptionsCommand}" Value="{x:Null}">
                                <Setter Property="Visibility" Value="Collapsed"/>
                            </DataTrigger>
                        </Style.Triggers>
                    </Style>
                </MenuItem.Style>
            </MenuItem>
            <MenuItem Header="Vorschau" Command="{Binding PreviewOptionsCommand}">
                <MenuItem.Style>
                    <Style TargetType="MenuItem" BasedOn="{StaticResource {x:Type MenuItem}}">
                        <Style.Triggers>
                            <DataTrigger Binding="{Binding PreviewOptionsCommand}" Value="{x:Null}">
                                <Setter Property="Visibility" Value="Collapsed"/>
                            </DataTrigger>
                        </Style.Triggers>
                    </Style>
                </MenuItem.Style>
            </MenuItem>
            <MenuItem Header="Zuruecksetzen auf Standard" Command="{Binding ResetOptionsCommand}">
                <MenuItem.Style>
                    <Style TargetType="MenuItem" BasedOn="{StaticResource {x:Type MenuItem}}">
                        <Style.Triggers>
                            <DataTrigger Binding="{Binding ResetOptionsCommand}" Value="{x:Null}">
                                <Setter Property="Visibility" Value="Collapsed"/>
                            </DataTrigger>
                        </Style.Triggers>
                    </Style>
                </MenuItem.Style>
            </MenuItem>
            <MenuItem Header="Wert hinzufuegen" Command="{Binding AddOptionCommand}"
                      CommandParameter="{Binding PlacementTarget, RelativeSource={RelativeSource AncestorType=ContextMenu}}">
                <MenuItem.Style>
                    <Style TargetType="MenuItem" BasedOn="{StaticResource {x:Type MenuItem}}">
                        <Style.Triggers>
                            <DataTrigger Binding="{Binding AddOptionCommand}" Value="{x:Null}">
                                <Setter Property="Visibility" Value="Collapsed"/>
                            </DataTrigger>
                        </Style.Triggers>
                    </Style>
                </MenuItem.Style>
            </MenuItem>
            <MenuItem Header="Wert entfernen" Command="{Binding RemoveOptionCommand}"
                      CommandParameter="{Binding PlacementTarget, RelativeSource={RelativeSource AncestorType=ContextMenu}}">
                <MenuItem.Style>
                    <Style TargetType="MenuItem" BasedOn="{StaticResource {x:Type MenuItem}}">
                        <Style.Triggers>
                            <DataTrigger Binding="{Binding RemoveOptionCommand}" Value="{x:Null}">
                                <Setter Property="Visibility" Value="Collapsed"/>
                            </DataTrigger>
                        </Style.Triggers>
                    </Style>
                </MenuItem.Style>
            </MenuItem>
        </ContextMenu>

        <Style x:Key="DetailComboStyle" TargetType="ComboBox">
            <Setter Property="ContextMenu" Value="{StaticResource ManagedOptionsContextMenu}"/>
            <Style.Triggers>
                <DataTrigger Binding="{Binding HasManagedOptions}" Value="False">
                    <Setter Property="ContextMenu" Value="{x:Null}"/>
                </DataTrigger>
            </Style.Triggers>
        </Style>

        <DataTemplate x:Key="TextEditorTemplate" DataType="{x:Type windows:RecordDetailItem}">
            <TextBox Text="{Binding Value, Mode=TwoWay, UpdateSourceTrigger=LostFocus}"
                     IsReadOnly="{Binding IsReadOnly}"
                     PreviewTextInput="NumericTextBox_PreviewTextInput"
                     DataObject.Pasting="NumericTextBox_Pasting"/>
        </DataTemplate>

        <DataTemplate x:Key="MultilineEditorTemplate" DataType="{x:Type windows:RecordDetailItem}">
            <TextBox Text="{Binding Value, Mode=TwoWay, UpdateSourceTrigger=LostFocus}"
                     IsReadOnly="{Binding IsReadOnly}"
                     AcceptsReturn="True" TextWrapping="Wrap"
                     VerticalScrollBarVisibility="Auto" MinHeight="108"/>
        </DataTemplate>

        <DataTemplate x:Key="EditableComboEditorTemplate" DataType="{x:Type windows:RecordDetailItem}">
            <ComboBox Style="{StaticResource DetailComboStyle}"
                      ItemsSource="{Binding Options}" IsEditable="True"
                      StaysOpenOnEdit="True" IsTextSearchEnabled="False"
                      Text="{Binding Value, Mode=TwoWay, UpdateSourceTrigger=LostFocus}"
                      IsEnabled="{Binding CanEdit}"
                      LostKeyboardFocus="EditorComboBox_LostKeyboardFocus"
                      SelectionChanged="EditorComboBox_SelectionChanged"/>
        </DataTemplate>

        <DataTemplate x:Key="FixedComboEditorTemplate" DataType="{x:Type windows:RecordDetailItem}">
            <ComboBox Style="{StaticResource DetailComboStyle}"
                      ItemsSource="{Binding Options}" IsEditable="False"
                      SelectedItem="{Binding SelectedOption, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}"
                      IsEnabled="{Binding CanEdit}"
                      LostKeyboardFocus="EditorComboBox_LostKeyboardFocus"
                      SelectionChanged="EditorComboBox_SelectionChanged"/>
        </DataTemplate>

        <windows:RecordDetailEditorTemplateSelector x:Key="EditorTemplateSelector"
                                                    TextTemplate="{StaticResource TextEditorTemplate}"
                                                    MultilineTemplate="{StaticResource MultilineEditorTemplate}"
                                                    EditableComboTemplate="{StaticResource EditableComboEditorTemplate}"
                                                    FixedComboTemplate="{StaticResource FixedComboEditorTemplate}"/>
    </UserControl.Resources>

    <Grid>
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="*"/>
            <RowDefinition Height="Auto"/>
        </Grid.RowDefinitions>

        <Border Grid.Row="0" Margin="0,0,0,14" Padding="18,16"
                Background="{StaticResource HeaderBrush}" BorderBrush="{StaticResource BorderBrush}"
                BorderThickness="1" CornerRadius="12">
            <StackPanel>
                <TextBlock Text="{Binding Header, ElementName=Root}"
                           FontSize="26" FontWeight="SemiBold" Foreground="{StaticResource TextBrush}"/>
                <TextBlock Text="{Binding SubHeader, ElementName=Root}"
                           Margin="0,6,0,0" Foreground="{StaticResource TextSecondaryBrush}" TextWrapping="Wrap"/>
            </StackPanel>
        </Border>

        <ScrollViewer Grid.Row="1" VerticalScrollBarVisibility="Auto"
                      HorizontalScrollBarVisibility="Disabled" CanContentScroll="False">
            <ItemsControl ItemsSource="{Binding Groups, ElementName=Root}">
                <ItemsControl.ItemsPanel>
                    <ItemsPanelTemplate><StackPanel/></ItemsPanelTemplate>
                </ItemsControl.ItemsPanel>
                <ItemsControl.ItemTemplate>
                    <DataTemplate>
                        <Border Margin="0,0,0,16" Padding="16"
                                Background="{StaticResource CardBrush}" BorderBrush="{StaticResource BorderBrush}"
                                BorderThickness="1" CornerRadius="12" VerticalAlignment="Top">
                            <StackPanel>
                                <TextBlock Text="{Binding Title}" FontSize="18" FontWeight="SemiBold"
                                           Foreground="{StaticResource TextBrush}"/>
                                <TextBlock Text="{Binding Description}" Margin="0,4,0,12"
                                           Foreground="{StaticResource TextSecondaryBrush}" TextWrapping="Wrap"/>
                                <ItemsControl ItemsSource="{Binding Items}">
                                    <ItemsControl.ItemsPanel>
                                        <ItemsPanelTemplate><WrapPanel Orientation="Horizontal"/></ItemsPanelTemplate>
                                    </ItemsControl.ItemsPanel>
                                    <ItemsControl.ItemTemplate>
                                        <DataTemplate>
                                            <Border Width="390" Margin="0,0,14,14" Padding="14"
                                                    Background="{StaticResource HeaderBrush}" BorderBrush="{StaticResource BorderBrush}"
                                                    BorderThickness="1" CornerRadius="10" VerticalAlignment="Top">
                                                <StackPanel>
                                                    <TextBlock Text="{Binding Label}" Foreground="{StaticResource TextSecondaryBrush}"
                                                               FontSize="12" FontWeight="SemiBold" TextWrapping="Wrap"/>
                                                    <ContentControl Content="{Binding}" Margin="0,8,0,0"
                                                                    ContentTemplateSelector="{StaticResource EditorTemplateSelector}"/>
                                                </StackPanel>
                                            </Border>
                                        </DataTemplate>
                                    </ItemsControl.ItemTemplate>
                                </ItemsControl>
                            </StackPanel>
                        </Border>
                    </DataTemplate>
                </ItemsControl.ItemTemplate>
            </ItemsControl>
        </ScrollViewer>

        <DockPanel Grid.Row="2" Margin="0,14,0,0" LastChildFill="False"
                   Visibility="{Binding FooterVisibility, ElementName=Root}">
            <Button DockPanel.Dock="Right" Margin="0,0,10,0" Padding="14,6"
                    Command="{Binding SuggestMeasuresCommand, ElementName=Root}"
                    Visibility="{Binding SuggestMeasuresVisibility, ElementName=Root}"
                    Style="{StaticResource PrimaryButton}">
                Empfohlene Massnahmen
            </Button>
        </DockPanel>
    </Grid>
</UserControl>
```

- [ ] **Step 2: `RecordDetailsView.xaml.cs` anlegen**

`src/AuswertungPro.Next.UI/Views/Controls/RecordDetailsView.xaml.cs` — DependencyProperties (damit `Groups`/`Header`/`SubHeader` neu gesetzt werden können, wenn die Auswahl wechselt) + die verschobenen Handler:

```csharp
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using AuswertungPro.Next.UI.Views.Windows;

namespace AuswertungPro.Next.UI.Views.Controls;

public partial class RecordDetailsView : UserControl
{
    private static readonly Regex NonNumericRegex = new("[^0-9]", RegexOptions.Compiled);

    public RecordDetailsView() => InitializeComponent();

    public static readonly DependencyProperty HeaderProperty =
        DependencyProperty.Register(nameof(Header), typeof(string), typeof(RecordDetailsView),
            new PropertyMetadata("Details"));

    public string Header
    {
        get => (string)GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
    }

    public static readonly DependencyProperty SubHeaderProperty =
        DependencyProperty.Register(nameof(SubHeader), typeof(string), typeof(RecordDetailsView),
            new PropertyMetadata(string.Empty));

    public string SubHeader
    {
        get => (string)GetValue(SubHeaderProperty);
        set => SetValue(SubHeaderProperty, value);
    }

    public static readonly DependencyProperty GroupsProperty =
        DependencyProperty.Register(nameof(Groups), typeof(IReadOnlyList<RecordDetailGroup>), typeof(RecordDetailsView),
            new PropertyMetadata(null));

    public IReadOnlyList<RecordDetailGroup>? Groups
    {
        get => (IReadOnlyList<RecordDetailGroup>?)GetValue(GroupsProperty);
        set => SetValue(GroupsProperty, value);
    }

    public static readonly DependencyProperty SuggestMeasuresCommandProperty =
        DependencyProperty.Register(nameof(SuggestMeasuresCommand), typeof(ICommand), typeof(RecordDetailsView),
            new PropertyMetadata(null, OnSuggestMeasuresCommandChanged));

    public ICommand? SuggestMeasuresCommand
    {
        get => (ICommand?)GetValue(SuggestMeasuresCommandProperty);
        set => SetValue(SuggestMeasuresCommandProperty, value);
    }

    public Visibility SuggestMeasuresVisibility =>
        SuggestMeasuresCommand is not null ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>Footer wird nur gezeigt, wenn es eine Aktion gibt.</summary>
    public Visibility FooterVisibility => SuggestMeasuresVisibility;

    private static void OnSuggestMeasuresCommandChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not RecordDetailsView view) return;
        view.SetValue(SuggestMeasuresVisibilityPropertyKey, view.SuggestMeasuresVisibility);
        view.SetValue(FooterVisibilityPropertyKey, view.FooterVisibility);
    }

    private static readonly DependencyPropertyKey SuggestMeasuresVisibilityPropertyKey =
        DependencyProperty.RegisterReadOnly(nameof(SuggestMeasuresVisibility), typeof(Visibility), typeof(RecordDetailsView),
            new PropertyMetadata(Visibility.Collapsed));

    private static readonly DependencyPropertyKey FooterVisibilityPropertyKey =
        DependencyProperty.RegisterReadOnly(nameof(FooterVisibility), typeof(Visibility), typeof(RecordDetailsView),
            new PropertyMetadata(Visibility.Collapsed));

    private void EditorComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _ = e;
        UpdateComboBindingSource(sender as ComboBox);
    }

    private void EditorComboBox_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        _ = e;
        UpdateComboBindingSource(sender as ComboBox);
    }

    private static void UpdateComboBindingSource(ComboBox? comboBox)
    {
        if (comboBox?.DataContext is not RecordDetailItem item)
            return;

        var property = item.AllowFreeText ? ComboBox.TextProperty : Selector.SelectedItemProperty;
        comboBox.GetBindingExpression(property)?.UpdateSource();
    }

    private void NumericTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        if (sender is not TextBox textBox || textBox.DataContext is not RecordDetailItem item || !item.DigitsOnly)
            return;

        e.Handled = NonNumericRegex.IsMatch(e.Text ?? string.Empty);
    }

    private void NumericTextBox_Pasting(object sender, DataObjectPastingEventArgs e)
    {
        if (sender is not TextBox textBox || textBox.DataContext is not RecordDetailItem item || !item.DigitsOnly)
            return;

        if (!e.DataObject.GetDataPresent(typeof(string)))
        {
            e.CancelCommand();
            return;
        }

        var text = e.DataObject.GetData(typeof(string)) as string ?? string.Empty;
        if (NonNumericRegex.IsMatch(text))
            e.CancelCommand();
    }
}
```

> **Hinweis zu `SuggestMeasuresVisibility`/`FooterVisibility`:** In der XAML oben werden sie per `ElementName=Root` gebunden. Da DependencyProperties für reine berechnete Read-Only-Sichtbarkeiten umständlich sind, ist die einfachste robuste Variante: die beiden `<Binding>`-Stellen in der XAML stattdessen über einen `BooleanToVisibilityConverter` an einen `bool`-DP `HasSuggestMeasures` hängen. **Vereinfachung für die Umsetzung:** Ersetze in der XAML `Visibility="{Binding FooterVisibility, ElementName=Root}"` und `Visibility="{Binding SuggestMeasuresVisibility, ElementName=Root}"` durch Bindungen auf einen einzigen DP `SuggestMeasuresCommand` mit `Converter` (siehe Step 2b). Lösche dann die Read-Only-DP-Key-Blöcke und `OnSuggestMeasuresCommandChanged` wieder. Wähle EINE der beiden Varianten; nicht beide.

- [ ] **Step 2b: Sichtbarkeit vereinfachen (empfohlen) — `NullToCollapsedConverter`**

Statt der Read-Only-DPs: lege einen kleinen Konverter an und binde die Footer-/Button-Sichtbarkeit direkt an `SuggestMeasuresCommand`.

In `RecordDetailsView.xaml.cs` die drei Read-Only-Blöcke (`SuggestMeasuresVisibility`, `FooterVisibility`, `OnSuggestMeasuresCommandChanged`, beide `DependencyPropertyKey`) entfernen und `SuggestMeasuresCommand`-DP-Metadaten auf `new PropertyMetadata(null)` setzen.

Konverter (in dieselbe Datei, eigener Typ am Dateiende):

```csharp
public sealed class NullToCollapsedConverter : System.Windows.Data.IValueConverter
{
    public object Convert(object? value, System.Type targetType, object? parameter, System.Globalization.CultureInfo culture)
        => value is null ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object? value, System.Type targetType, object? parameter, System.Globalization.CultureInfo culture)
        => System.Windows.Data.Binding.DoNothing;
}
```

In `RecordDetailsView.xaml` in den `UserControl.Resources` ergänzen:

```xml
        <local:NullToCollapsedConverter x:Key="NullToCollapsed"/>
```

und am `UserControl` den Namespace ergänzen: `xmlns:local="clr-namespace:AuswertungPro.Next.UI.Views.Controls"`.

Die beiden Sichtbarkeits-Bindungen ändern zu:

```xml
                   Visibility="{Binding SuggestMeasuresCommand, ElementName=Root, Converter={StaticResource NullToCollapsed}}"
```

(DockPanel und Button bekommen beide dieselbe Bindung.)

- [ ] **Step 3: `RecordDetailsWindow.xaml` auf das Control reduzieren**

Ersetze den kompletten Inhalt von `RecordDetailsWindow.xaml` (Window.Resources + Grid) durch ein schlankes Hosting des Controls. Header/SubHeader/Groups/SuggestMeasures + Schliessen-Button bleiben Window-seitig:

```xml
<Window x:Class="AuswertungPro.Next.UI.Views.Windows.RecordDetailsWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:controls="clr-namespace:AuswertungPro.Next.UI.Views.Controls"
        x:Name="Root"
        Title="Details" Width="1360" Height="860" MinWidth="980" MinHeight="680"
        WindowStartupLocation="CenterOwner" WindowStyle="SingleBorderWindow"
        AllowsTransparency="False" ResizeMode="CanResize">
    <Grid Margin="18">
        <Grid.RowDefinitions>
            <RowDefinition Height="*"/>
            <RowDefinition Height="Auto"/>
        </Grid.RowDefinitions>

        <controls:RecordDetailsView Grid.Row="0"
                                    Header="{Binding Header, ElementName=Root}"
                                    SubHeader="{Binding SubHeader, ElementName=Root}"
                                    Groups="{Binding Groups, ElementName=Root}"
                                    SuggestMeasuresCommand="{Binding SuggestMeasuresCommand, ElementName=Root}"/>

        <DockPanel Grid.Row="1" Margin="0,14,0,0" LastChildFill="False">
            <TextBlock DockPanel.Dock="Left" VerticalAlignment="Center"
                       Foreground="{StaticResource TextSecondaryBrush}"
                       Text="Doppelklick oder Kontextmenue auf der Zeile oeffnet diese Detailansicht."/>
            <Button DockPanel.Dock="Right" Width="120" Command="{Binding CloseCommand, ElementName=Root}"
                    Style="{StaticResource SecondaryButton}">Schliessen</Button>
        </DockPanel>
    </Grid>
</Window>
```

- [ ] **Step 4: `RecordDetailsWindow.xaml.cs` entschlacken**

Entferne die verschobenen Handler (`EditorComboBox_*`, `NumericTextBox_*`, `NonNumericRegex`, `UpdateComboBindingSource`) und die `SuggestMeasuresVisibility`-Property (jetzt im Control). Behalte Ctor, Properties (`Groups`, `Header`, `SubHeader`, `CloseCommand`, `SuggestMeasuresCommand`), `EnsureVisibleOnScreen`, `CloseWindowCommand`. Da die XAML jetzt per `ElementName=Root` bindet (nicht mehr `DataContext=this`), die Zeile `DataContext = this;` **entfernen**. Ergebnis:

```csharp
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class RecordDetailsWindow : Window
{
    public IReadOnlyList<RecordDetailGroup> Groups { get; }
    public string Header { get; }
    public string SubHeader { get; }
    public ICommand CloseCommand { get; }
    public ICommand? SuggestMeasuresCommand { get; }

    public RecordDetailsWindow(
        string title,
        string header,
        string subHeader,
        IReadOnlyList<RecordDetailGroup> groups,
        ICommand? suggestMeasuresCommand = null)
    {
        InitializeComponent();
        WindowStateManager.Track(this);

        Title = string.IsNullOrWhiteSpace(title) ? "Details" : title;
        Header = string.IsNullOrWhiteSpace(header) ? "Details" : header;
        SubHeader = subHeader ?? string.Empty;
        Groups = groups ?? [];
        CloseCommand = new CloseWindowCommand(this);
        SuggestMeasuresCommand = suggestMeasuresCommand;
        Loaded += (_, _) => EnsureVisibleOnScreen();
    }

    private void EnsureVisibleOnScreen()
    {
        var area = SystemParameters.WorkArea;
        if (Width > area.Width) Width = area.Width - 20;
        if (Height > area.Height) Height = area.Height - 20;
        if (Left < area.Left) Left = area.Left;
        if (Top < area.Top) Top = area.Top;
        if (Left + Width > area.Right) Left = area.Right - Width;
        if (Top + Height > area.Bottom) Top = area.Bottom - Height;
    }

    private sealed class CloseWindowCommand : ICommand
    {
        private readonly Window _window;
        public CloseWindowCommand(Window window) => _window = window;
        public event EventHandler? CanExecuteChanged { add { } remove { } }
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => _window.Close();
    }
}
```

- [ ] **Step 5: Build (App geschlossen!)**

Run: `dotnet build src/AuswertungPro.Next.UI/AuswertungPro.Next.UI.csproj`
Expected: 0 Fehler, 0 Warnungen.

- [ ] **Step 6: Alle UI-Tests**

Run: `dotnet test tests/AuswertungPro.Next.UI.Tests/AuswertungPro.Next.UI.Tests.csproj`
Expected: PASS (alle; insbesondere die Detail-Builder-Tests bleiben grün).

- [ ] **Step 7: Manuelle Prüfung (Popup unverändert)**

App starten, eine Haltung doppelklicken (bzw. Kontextmenü → Details). Erwartung: Das Detail-Popup sieht aus und verhält sich exakt wie vorher (Gruppen, Combos, Ziffern-Felder, „Empfohlene Massnahmen", Schliessen). Stichprobe: ein Feld ändern → erscheint in der Tabelle. Gleiches kurz auf der Schächte-Seite (`SchaechtePage`) prüfen, da sie dasselbe Fenster nutzt.

- [ ] **Step 8: Commit**

```bash
git add src/AuswertungPro.Next.UI/Views/Controls/RecordDetailsView.xaml src/AuswertungPro.Next.UI/Views/Controls/RecordDetailsView.xaml.cs src/AuswertungPro.Next.UI/Views/Windows/RecordDetailsWindow.xaml src/AuswertungPro.Next.UI/Views/Windows/RecordDetailsWindow.xaml.cs
git commit -m "refactor(details): RecordDetailsView als wiederverwendbares Control extrahiert (Popup unveraendert)"
```

---

### Task 5: `HaltungsansichtView` (Master-Detail: Liste links + Detail rechts)

**Files:**
- Create: `src/AuswertungPro.Next.UI/Views/Pages/Haltungsansicht/HaltungListItemConverters.cs`
- Create: `src/AuswertungPro.Next.UI/Views/Pages/Haltungsansicht/HaltungsansichtView.xaml`
- Create: `src/AuswertungPro.Next.UI/Views/Pages/Haltungsansicht/HaltungsansichtView.xaml.cs`

Die View bindet an `Records` + `Selected` der DataPage-vm (DataContext fließt von der DataPage). Sie kennt keine Geschäftslogik: Beim Auswahlwechsel ruft sie eine von außen gesetzte `DetailBuilder`-Delegate (= `DataPage.BuildHaltungRecordDetails`) und füllt das `RecordDetailsView`.

- [ ] **Step 1: Konverter für die Listen-Zeile**

`src/AuswertungPro.Next.UI/Views/Pages/Haltungsansicht/HaltungListItemConverters.cs`:

```csharp
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.DataPage;

namespace AuswertungPro.Next.UI.Views.Pages.Haltungsansicht;

/// <summary>Bindet das ganze HaltungRecord-Item auf die einzeilige Kurzbeschreibung.</summary>
public sealed class HaltungSummaryConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is HaltungRecord r ? HaltungSummaryFormatter.FormatSummary(r) : string.Empty;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}

/// <summary>Bindet den Zustandsklasse-Text auf den Chip-Hintergrund (gleiche Quelle wie die Tabelle).</summary>
public sealed class ZustandsklasseBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => (object?)ZustandsklasseColorPalette.TryGetBackground(value?.ToString()) ?? DependencyProperty.UnsetValue;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}
```

> Live-Aktualisierung: Die Summary bindet auf das Record-Objekt selbst (ändert sich beim Editieren von DN/Länge in der Liste nicht automatisch — akzeptabel, die Liste ist der Navigator). Der Zustandsklasse-Chip bindet auf `Fields[Zustandsklasse]` und aktualisiert live über `HaltungRecord`'s `PropertyChanged` (`HaltungRecord` meldet `Fields[...]`).

- [ ] **Step 2: `HaltungsansichtView.xaml`**

`src/AuswertungPro.Next.UI/Views/Pages/Haltungsansicht/HaltungsansichtView.xaml`:

```xml
<UserControl x:Class="AuswertungPro.Next.UI.Views.Pages.Haltungsansicht.HaltungsansichtView"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:controls="clr-namespace:AuswertungPro.Next.UI.Views.Controls"
             xmlns:local="clr-namespace:AuswertungPro.Next.UI.Views.Pages.Haltungsansicht"
             x:Name="Root">
    <UserControl.Resources>
        <local:HaltungSummaryConverter x:Key="SummaryConv"/>
        <local:ZustandsklasseBrushConverter x:Key="ZkBrushConv"/>
    </UserControl.Resources>

    <Grid Margin="8">
        <Grid.ColumnDefinitions>
            <ColumnDefinition Width="320" MinWidth="240"/>
            <ColumnDefinition Width="6"/>
            <ColumnDefinition Width="*"/>
        </Grid.ColumnDefinitions>

        <!-- Liste links (virtualisiert) -->
        <ListBox Grid.Column="0"
                 x:Name="HaltungList"
                 ItemsSource="{Binding Records}"
                 SelectedItem="{Binding Selected, Mode=TwoWay}"
                 SelectionChanged="HaltungList_SelectionChanged"
                 Background="{StaticResource CardBrush}"
                 BorderBrush="{StaticResource BorderBrush}" BorderThickness="1"
                 ScrollViewer.HorizontalScrollBarVisibility="Disabled"
                 ScrollViewer.VerticalScrollBarVisibility="Auto"
                 VirtualizingStackPanel.IsVirtualizing="True"
                 VirtualizingStackPanel.VirtualizationMode="Recycling"
                 ScrollViewer.CanContentScroll="True">
            <ListBox.ItemTemplate>
                <DataTemplate>
                    <Grid Margin="2,4">
                        <Grid.ColumnDefinitions>
                            <ColumnDefinition Width="*"/>
                            <ColumnDefinition Width="Auto"/>
                        </Grid.ColumnDefinitions>
                        <StackPanel Grid.Column="0">
                            <StackPanel Orientation="Horizontal">
                                <TextBlock Text="{Binding Fields[NR]}" Foreground="{StaticResource TextSecondaryBrush}"
                                           FontSize="11" Margin="0,0,8,0"/>
                                <TextBlock Text="{Binding Fields[Haltungsname]}" Foreground="{StaticResource TextBrush}"
                                           FontWeight="SemiBold" TextTrimming="CharacterEllipsis"/>
                            </StackPanel>
                            <TextBlock Text="{Binding Converter={StaticResource SummaryConv}}"
                                       Foreground="{StaticResource TextSecondaryBrush}" FontSize="12"
                                       TextTrimming="CharacterEllipsis" Margin="0,2,0,0"/>
                        </StackPanel>
                        <Border Grid.Column="1" Width="26" Height="26" CornerRadius="13" VerticalAlignment="Center"
                                Background="{Binding Fields[Zustandsklasse], Converter={StaticResource ZkBrushConv}}">
                            <TextBlock Text="{Binding Fields[Zustandsklasse]}" HorizontalAlignment="Center"
                                       VerticalAlignment="Center" Foreground="#000000" FontSize="12" FontWeight="Bold"/>
                        </Border>
                    </Grid>
                </DataTemplate>
            </ListBox.ItemTemplate>
        </ListBox>

        <GridSplitter Grid.Column="1" Width="6" HorizontalAlignment="Stretch"
                      Background="{StaticResource BorderBrush}"/>

        <!-- Detail rechts (wiederverwendetes Control) -->
        <controls:RecordDetailsView Grid.Column="2" x:Name="Detail" Margin="8,0,0,0"/>
    </Grid>
</UserControl>
```

- [ ] **Step 3: `HaltungsansichtView.xaml.cs`**

`src/AuswertungPro.Next.UI/Views/Pages/Haltungsansicht/HaltungsansichtView.xaml.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Views.Windows;

namespace AuswertungPro.Next.UI.Views.Pages.Haltungsansicht;

public partial class HaltungsansichtView : UserControl
{
    public HaltungsansichtView()
    {
        InitializeComponent();
        IsVisibleChanged += (_, _) => RefreshDetail();
    }

    /// <summary>
    /// Wird von der DataPage gesetzt: baut die editierbaren Detail-Gruppen für eine Haltung
    /// (nutzt den bestehenden Pfad CreateHaltungDetailItem/CommitHaltungDetailField).
    /// </summary>
    public Func<HaltungRecord, IReadOnlyList<RecordDetailGroup>>? DetailBuilder { get; set; }

    private void HaltungList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _ = sender;
        _ = e;
        RefreshDetail();
    }

    private void RefreshDetail()
    {
        if (!IsVisible)
            return;

        if (HaltungList.SelectedItem is not HaltungRecord record || DetailBuilder is null)
        {
            Detail.Header = "Keine Haltung gewaehlt";
            Detail.SubHeader = "Links eine Haltung waehlen.";
            Detail.Groups = Array.Empty<RecordDetailGroup>();
            return;
        }

        var name = record.GetFieldValue("Haltungsname");
        Detail.Header = string.IsNullOrWhiteSpace(name) ? "Haltungsdetails" : $"Haltung {name}";
        Detail.SubHeader = "Alle Felder editierbar — Aenderungen erscheinen sofort in der Tabelle.";
        Detail.Groups = DetailBuilder(record);
    }
}
```

- [ ] **Step 4: Build (App geschlossen!)**

Run: `dotnet build src/AuswertungPro.Next.UI/AuswertungPro.Next.UI.csproj`
Expected: 0 Fehler, 0 Warnungen.

- [ ] **Step 5: Commit**

```bash
git add src/AuswertungPro.Next.UI/Views/Pages/Haltungsansicht/
git commit -m "feat(haltungsansicht): Master-Detail-View (Liste links + wiederverwendetes Detail rechts)"
```

---

### Task 6: Umschalter in der DataPage (Tabelle ↔ Haltungsansicht)

**Files:**
- Modify: `src/AuswertungPro.Next.UI/Views/Pages/DataPage.xaml`
- Modify: `src/AuswertungPro.Next.UI/Views/Pages/DataPage.xaml.cs`

- [ ] **Step 1: Namespace + View im GridHost ergänzen (XAML)**

In `DataPage.xaml` am `UserControl`-Tag den Namespace ergänzen:

```xml
             xmlns:haltung="clr-namespace:AuswertungPro.Next.UI.Views.Pages.Haltungsansicht"
```

Innerhalb von `<Grid x:Name="GridHost" Grid.Row="1">` (nach dem `</DataGrid>`-Block, als letztes Kind des GridHost) die neue View einfügen — anfangs ausgeblendet:

```xml
            <haltung:HaltungsansichtView x:Name="HaltungsansichtView" Visibility="Collapsed"/>
```

- [ ] **Step 2: Umschalter in die Toolbar (XAML)**

In der Toolbar (Grid.Row 0) neben dem bestehenden „Ansicht"-Bereich (`AnsichtDropdown`, `DataPage.xaml:284`) einen Umschalt-Button ergänzen. Direkt vor oder nach `AnsichtDropdown`:

```xml
                    <ToggleButton x:Name="HaltungsansichtToggle"
                                  Style="{StaticResource CompactToggleButton}"
                                  Margin="6,0,0,0"
                                  Checked="HaltungsansichtToggle_Changed"
                                  Unchecked="HaltungsansichtToggle_Changed"
                                  ToolTip="Zwischen Tabelle und Haltungsansicht umschalten">
                        <StackPanel Orientation="Horizontal">
                            <TextBlock Text="&#xE8FD;" FontFamily="Segoe MDL2 Assets" FontSize="13"
                                       VerticalAlignment="Center" Margin="0,0,6,0"/>
                            <TextBlock Text="Haltungsansicht" VerticalAlignment="Center"/>
                        </StackPanel>
                    </ToggleButton>
```

- [ ] **Step 3: Verdrahtung im Code-Behind**

In `DataPage.xaml.cs` — `DetailBuilder` setzen (Konstruktor-Ende oder im `Loaded`) und den Toggle-Handler ergänzen. Suche den Konstruktor (`public DataPage(...)` mit `InitializeComponent();`) und füge nach `InitializeComponent();` hinzu:

```csharp
        HaltungsansichtView.DetailBuilder = BuildHaltungRecordDetails;
```

Toggle-Handler (irgendwo in der Klasse, z. B. im Bereich „Haltung Record Details"):

```csharp
    private void HaltungsansichtToggle_Changed(object sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        var showAnsicht = HaltungsansichtToggle.IsChecked == true;
        HaltungsansichtView.Visibility = showAnsicht ? Visibility.Visible : Visibility.Collapsed;
        Grid.Visibility = showAnsicht ? Visibility.Collapsed : Visibility.Visible;
    }
```

> `Grid` ist der `x:Name` des DataGrid (`DataPage.xaml:487`). `Selected`/`Records` werden von beiden Sichten geteilt (gleiche Bindungen) → Auswahl bleibt beim Umschalten erhalten. Die `HaltungsansichtView` aktualisiert ihr Detail beim Sichtbarwerden (`IsVisibleChanged`) und bei Auswahlwechsel.

- [ ] **Step 4: Build (App geschlossen!)**

Run: `dotnet build src/AuswertungPro.Next.UI/AuswertungPro.Next.UI.csproj`
Expected: 0 Fehler, 0 Warnungen.

- [ ] **Step 5: Alle Tests**

Run: `dotnet test tests/AuswertungPro.Next.UI.Tests/AuswertungPro.Next.UI.Tests.csproj`
Expected: PASS (alle).

- [ ] **Step 6: Commit**

```bash
git add src/AuswertungPro.Next.UI/Views/Pages/DataPage.xaml src/AuswertungPro.Next.UI/Views/Pages/DataPage.xaml.cs
git commit -m "feat(haltungsansicht): Umschalter Tabelle <-> Haltungsansicht in der DataPage"
```

---

### Task 7: Manuelle GUI-Abnahme

**Keine Code-Änderung.** App starten und die folgende Abnahme durchführen (entspricht der Spec, Abschnitt „Manuell"):

- [ ] **Umschalten:** Button „Haltungsansicht" → Liste links + Detail rechts; zurück → Tabelle. Auswahl bleibt in beiden Richtungen erhalten.
- [ ] **Alle Felder editierbar + richtige Dropdowns:** Im Detail je Gruppe stichprobenartig prüfen: Combos (z. B. Rohrmaterial, Nutzungsart, Zustandsklasse, Sanieren, Eigentümer, Prüfungsresultat, Referenzprüfung) zeigen dieselben Begriffe wie die Tabellenspalte; Int-Felder (z. B. DN) nehmen nur Ziffern; Mehrzeiler (Primäre Schäden, Bemerkungen, Empfohlene Sanierungsmassnahmen) funktionieren.
- [ ] **Edit-Sync beide Richtungen:** Wert im Detail ändern → erscheint nach Umschalten in der Tabelle. Wert in der Tabelle ändern → erscheint nach Umschalten/Reselect im Detail. Zustandsklasse ändern → Chip-Farbe in der Liste passt sich an (gleiche Farbe wie Tabellenspalte).
- [ ] **Scrollen:** Liste links und Detail-Body rechts scrollen unabhängig; Toolbar bleibt fix; GridSplitter verschiebt die Breite.
- [ ] **Großes Netz flüssig:** Mit einem großen Projekt (viele Haltungen) durch die Liste scrollen — keine Ruckler (Virtualisierung greift).
- [ ] **Export unverändert:** Excel-Export einmal durchführen → Spalten/Inhalte unverändert (liest weiterhin `record.Fields`).
- [ ] **AutoSave:** Nach Edits im Detail kurz warten → Speicherstatus erscheint (gleiches `ScheduleAutoSave` wie Tabelle).

Falls ein Punkt fehlschlägt: systematic-debugging-Skill nutzen (Root-Cause vor Fix), nicht blind patchen.

---

## Self-Review (vom Plan-Autor ausgefüllt)

**1. Spec-Abdeckung:**
- Eine Datenquelle / bidirektional → garantiert durch Wiederverwendung von `RecordDetailItem.Value`→`CommitHaltungDetailField` (Task 4/5/6). ✓
- Alle Felder editierbar + gleiche Dropdowns → Vollständigkeits-Test (Task 1) + Wiederverwendung der `CreateHaltungDetailItem`-Factory (FieldCatalog-Combos, managed Combos). ✓
- Liste links / Detail rechts / Scrollbar / geteilte Auswahl → Task 5/6. ✓
- Zustandsklasse-Farbe aus einer Quelle → Task 2. ✓
- Tabelle/Export 1:1 → nichts an Tabelle/Spalten/Export geändert; Abnahme Task 7. ✓
- Primäre Schäden = Anzeige → werden als (mehrzeiliges) Feld gerendert wie heute im Popup; **kein Protokoll-Editor**. Hinweis: Die in der Spec erwähnte „Meter-Leiste" ist NICHT Teil dieses Plans (YAGNI; das Popup hatte sie auch nie). Bewusste Reduktion ggü. Spec-Abschnitt „Primäre Schäden – Meter-Leiste". Bei Bedarf separater Folge-Plan.

**2. Placeholder-Scan:** Keine TBD/TODO; jeder Code-Step enthält vollständigen Code. Einzige Wahlstelle: Step 2/2b in Task 4 (zwei Varianten für die Footer-Sichtbarkeit) — explizit als „EINE Variante wählen, empfohlen 2b" markiert.

**3. Typ-Konsistenz:** `DetailBuilder` Typ `Func<HaltungRecord, IReadOnlyList<RecordDetailGroup>>` passt zu `DataPage.BuildHaltungRecordDetails` (Rückgabe `List<RecordDetailGroup>` ⊂ `IReadOnlyList<...>`). `RecordDetailsView.Groups` ist `IReadOnlyList<RecordDetailGroup>?`. `ZustandsklasseColorPalette.TryGetBackground(string?)` und `HaltungSummaryFormatter.FormatSummary(...)` Signaturen stimmen mit den Tests überein. `Grid` (DataGrid x:Name) und `HaltungsansichtView`/`HaltungsansichtToggle` x:Names konsistent verwendet.

**4. Abweichungs-Notiz:** Spec-Dateien `HaltungFieldEditor.cs`/`DetailFieldGroups.cs`/`HaltungDetailView`/`HaltungsansichtViewModel` entfallen zugunsten vorhandener Bausteine (siehe Abschnitt „Erkenntnis aus dem Code"). `HaltungListView` ist in `HaltungsansichtView` integriert (eine ListBox; kein eigenes File nötig — YAGNI).
