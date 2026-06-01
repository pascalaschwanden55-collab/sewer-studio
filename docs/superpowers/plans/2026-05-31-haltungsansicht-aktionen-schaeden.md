# Haltungsansicht anreichern (Rechtsklick-Aktionen + Schäden-Mini-Tabelle) — Implementierungsplan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Die Haltungsansicht bekommt (a) das gleiche Rechtsklick-Menü wie das DataGrid und (b) die Primären Schäden als kompakte Tabelle (Optik wie Referenz-Bild 2), bei der Doppelklick/„+" den bestehenden Codier-Editor öffnet.

**Architecture:** Reine Wiederverwendung. Die Schäden-Tabelle ist eine dünne Sicht auf die bestehende `DataPageViewModel.SelectedProtocolEntries` (ObservableCollection) mit einem getesteten reinen Projektions-Helfer + dünnen Konvertern; Doppelklick/„+" rufen `OpenProtocolCommand` (bestehender `ProtocolObservationsWindow`). Das Rechtsklick-Menü ruft über eine Delegate-Property (`ActionRequested`, analog zum bestehenden `DetailBuilder`) die **bestehenden** DataPage-Handler.

**Tech Stack:** .NET 10, WPF/MVVM, xUnit (`AuswertungPro.Next.UI.Tests`).

---

## Verifizierte Reuse-Punkte (Grounding)
- `DataPageViewModel.SelectedProtocolEntries` : `ObservableCollection<ProtocolEntry>` — schon mit der gewählten Haltung synchronisiert; `OpenProtocol` ruft danach `RefreshSelectedProtocolEntries()` (Clear+Add) → an die Collection gebundene UI aktualisiert sich automatisch.
- `DataPageViewModel.OpenProtocolCommand` : `IRelayCommand<HaltungRecord?>` → öffnet `ProtocolObservationsWindow` (Codier-Editor).
- `ProtocolEntry` (Domain): `string Code`, `string Beschreibung`, `double? MeterStart`, `double? MeterEnd`, `bool IsStreckenschaden`, `bool IsDeleted`.
- DataPage-Handler (alle `private void X(object sender, RoutedEventArgs e)`, nutzen `ResolveActionRecord(sender, vm) => GetContextMenuRecord(sender) ?? vm.Selected`): `PlayMenu_Click`, `BeobachtungenMenu_Click`, `PrintAwuHaltungsprotokollMenu_Click`, `OpenOriginalPdfMenu_Click`, `CostsMenu_Click`, `MoveRecordUpMenu_Click`, `MoveRecordDownMenu_Click`, und `DeleteSelectedRows()` (parameterlos). Da `ResolveActionRecord` auf `vm.Selected` zurückfällt, genügt es, vor dem Aufruf `vm.Selected = record` zu setzen.
- `HaltungsansichtView` hat bereits die Property-Setter-Pattern (`DetailBuilder`), an dem sich `ActionRequested` orientiert.
- Bestehende UI-Konverter liegen in `…/Views/Pages/Haltungsansicht/HaltungListItemConverters.cs`.

## File-Struktur
| Datei | Verantwortung | Status |
|---|---|---|
| `…/DataPage/SchadenZeileFormatter.cs` | reine Projektion `ProtocolEntry → (Meter, Code, Klartext, Kategorie)` | neu |
| `tests/AuswertungPro.Next.UI.Tests/SchadenZeileFormatterTests.cs` | Test der Projektion | neu |
| `…/Views/Pages/Haltungsansicht/HaltungListItemConverters.cs` | + 3 dünne Konverter (Meter/Klartext/Kategorie) die den Formatter aufrufen | ändern |
| `…/Views/Pages/Haltungsansicht/HaltungsansichtView.xaml(.cs)` | Schäden-Karte (Bild-2-Optik) + Doppelklick/„+"; Rechtsklick-ContextMenu; `ActionRequested`-Delegate; Layout Spalte 2 | ändern |
| `…/Views/Pages/DataPage.xaml.cs` | `RouteHaltungsansichtAction` + Verdrahtung (`HaltungsansichtView.ActionRequested = …`) | ändern |

---

### Task 1: `SchadenZeileFormatter` (reine Projektion) + Test

**Files:**
- Create: `src/AuswertungPro.Next.UI/DataPage/SchadenZeileFormatter.cs`
- Test: `tests/AuswertungPro.Next.UI.Tests/SchadenZeileFormatterTests.cs`

- [ ] **Step 1: Failing Test schreiben**

`tests/AuswertungPro.Next.UI.Tests/SchadenZeileFormatterTests.cs`:

```csharp
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.DataPage;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

public sealed class SchadenZeileFormatterTests
{
    private static ProtocolEntry Entry(string code, string beschreibung, double? mStart, double? mEnd = null, bool strecke = false, bool deleted = false)
        => new() { Code = code, Beschreibung = beschreibung, MeterStart = mStart, MeterEnd = mEnd, IsStreckenschaden = strecke, IsDeleted = deleted };

    [Fact]
    public void Format_Punktschaden_ShowsSingleMeter()
    {
        var z = SchadenZeileFormatter.Format(Entry("BCD", "Rohranfang", 0.0));
        Assert.Equal("0.00 m", z.Meter);
        Assert.Equal("BCD", z.Code);
        Assert.Equal("Rohranfang", z.Klartext);
        Assert.Equal("Bestand", z.Kategorie);
    }

    [Fact]
    public void Format_Streckenschaden_ShowsMeterRange()
    {
        var z = SchadenZeileFormatter.Format(Entry("BBA", "Wurzeln", 2.50, 8.10, strecke: true));
        Assert.Equal("2.50–8.10 m", z.Meter);
        Assert.Equal("Betrieb", z.Kategorie);
    }

    [Fact]
    public void Format_KlartextFallsBackToCode_WhenBeschreibungEmpty()
    {
        var z = SchadenZeileFormatter.Format(Entry("BAB", "", 1.0));
        Assert.Equal("BAB", z.Klartext);
        Assert.Equal("Zustand", z.Kategorie);
    }

    [Theory]
    [InlineData("BAB", "Zustand")]
    [InlineData("BBA", "Betrieb")]
    [InlineData("BCD", "Bestand")]
    [InlineData("BDDC", "Betrieb")]
    [InlineData("XYZ", "")]
    public void Kategorie_DerivedFromCodeGroup(string code, string expected)
    {
        Assert.Equal(expected, SchadenZeileFormatter.Format(Entry(code, "x", 0.0)).Kategorie);
    }

    [Fact]
    public void FormatList_SkipsDeletedAndEmptyCode()
    {
        var entries = new[]
        {
            Entry("BCD", "Rohranfang", 0.0),
            Entry("BBA", "Wurzeln", 2.0, deleted: true),
            Entry("", "kein Code", 3.0),
        };
        var rows = SchadenZeileFormatter.FormatList(entries);
        Assert.Single(rows);
        Assert.Equal("BCD", rows[0].Code);
    }
}
```

- [ ] **Step 2: Test ausführen (muss fehlschlagen)**

Run: `dotnet test tests/AuswertungPro.Next.UI.Tests/AuswertungPro.Next.UI.Tests.csproj --filter "FullyQualifiedName~SchadenZeileFormatterTests"`
Expected: FAIL/Compile-Fehler — `SchadenZeileFormatter` existiert nicht.

- [ ] **Step 3: Formatter implementieren**

`src/AuswertungPro.Next.UI/DataPage/SchadenZeileFormatter.cs`:

```csharp
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.UI.DataPage;

/// <summary>Eine Zeile der Primäre-Schäden-Mini-Tabelle (reine Anzeigedaten).</summary>
public sealed record SchadenZeile(string Meter, string Code, string Klartext, string Kategorie);

/// <summary>
/// Reine Projektion ProtocolEntry → SchadenZeile für die Haltungsansicht-Mini-Tabelle.
/// Keine Abhängigkeit auf Katalog/Resolver: Klartext kommt aus ProtocolEntry.Beschreibung
/// (Fallback Code), Kategorie aus der VSA-Hauptgruppe (2 Buchstaben).
/// </summary>
public static class SchadenZeileFormatter
{
    public static SchadenZeile Format(ProtocolEntry entry)
    {
        var meter = FormatMeter(entry);
        var klartext = string.IsNullOrWhiteSpace(entry.Beschreibung) ? entry.Code : entry.Beschreibung.Trim();
        return new SchadenZeile(meter, entry.Code, klartext, Kategorie(entry.Code));
    }

    public static IReadOnlyList<SchadenZeile> FormatList(IEnumerable<ProtocolEntry> entries)
        => entries
            .Where(e => !e.IsDeleted && !string.IsNullOrWhiteSpace(e.Code))
            .Select(Format)
            .ToList();

    public static string FormatMeter(ProtocolEntry entry)
    {
        var start = entry.MeterStart ?? 0.0;
        var s = start.ToString("0.00", CultureInfo.InvariantCulture);
        if (entry.IsStreckenschaden && entry.MeterEnd is { } end && end > start)
            return $"{s}–{end.ToString("0.00", CultureInfo.InvariantCulture)} m";
        return $"{s} m";
    }

    /// <summary>VSA-Hauptgruppe → grobe Kategorie. BA=Zustand, BB/BD=Betrieb, BC=Bestand, sonst "".</summary>
    public static string Kategorie(string? code)
    {
        if (string.IsNullOrWhiteSpace(code) || code.Length < 2)
            return "";
        return code.Substring(0, 2).ToUpperInvariant() switch
        {
            "BA" => "Zustand",
            "BB" => "Betrieb",
            "BC" => "Bestand",
            "BD" => "Betrieb",
            _ => ""
        };
    }
}
```

- [ ] **Step 4: Test ausführen (muss grün sein)**

Run: `dotnet test tests/AuswertungPro.Next.UI.Tests/AuswertungPro.Next.UI.Tests.csproj --filter "FullyQualifiedName~SchadenZeileFormatterTests"`
Expected: PASS (alle).

- [ ] **Step 5: Commit**

```bash
git add src/AuswertungPro.Next.UI/DataPage/SchadenZeileFormatter.cs tests/AuswertungPro.Next.UI.Tests/SchadenZeileFormatterTests.cs
git commit -m "feat(haltungsansicht): reine Projektion ProtocolEntry -> Schaden-Zeile (Meter/Code/Klartext/Kategorie)"
```

---

### Task 2: Schäden-Mini-Tabelle in der Haltungsansicht (Doppelklick/„+" → Codier-Editor)

**Files:**
- Modify: `src/AuswertungPro.Next.UI/Views/Pages/Haltungsansicht/HaltungListItemConverters.cs`
- Modify: `src/AuswertungPro.Next.UI/Views/Pages/Haltungsansicht/HaltungsansichtView.xaml`
- Modify: `src/AuswertungPro.Next.UI/Views/Pages/Haltungsansicht/HaltungsansichtView.xaml.cs`
- Modify: `src/AuswertungPro.Next.UI/Views/Pages/DataPage.xaml.cs`

- [ ] **Step 1: Konverter ergänzen (Meter/Klartext/Kategorie aus ProtocolEntry)**

Ans Ende von `HaltungListItemConverters.cs` (vor der schließenden Datei, gleicher Namespace `AuswertungPro.Next.UI.Views.Pages.Haltungsansicht`) anfügen. Ergänze oben den Using `using AuswertungPro.Next.Domain.Protocol;` falls nicht vorhanden:

```csharp
/// <summary>ProtocolEntry → Meter-Anzeige (z. B. "2.50–8.10 m"); nutzt den getesteten Formatter.</summary>
public sealed class SchadenMeterConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is ProtocolEntry e ? SchadenZeileFormatter.FormatMeter(e) : string.Empty;
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}

/// <summary>ProtocolEntry → Klartext (Beschreibung, Fallback Code).</summary>
public sealed class SchadenKlartextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is ProtocolEntry e ? SchadenZeileFormatter.Format(e).Klartext : string.Empty;
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}

/// <summary>ProtocolEntry → Kategorie-Tag ("Bestand"/"Betrieb"/"Zustand"/"").</summary>
public sealed class SchadenKategorieConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is ProtocolEntry e ? SchadenZeileFormatter.Kategorie(e.Code) : string.Empty;
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}
```

Stelle sicher, dass die Usings `using AuswertungPro.Next.UI.DataPage;` (Formatter) und `using AuswertungPro.Next.Domain.Protocol;` (ProtocolEntry) oben in der Datei stehen.

- [ ] **Step 2: `HaltungsansichtView.xaml` — Layout Spalte 2 + Schäden-Karte**

Ersetze in `HaltungsansichtView.xaml` das aktuelle Detail-Element

```xml
        <!-- Detail rechts (wiederverwendetes Control) -->
        <controls:RecordDetailsView Grid.Column="2" x:Name="Detail" Margin="8,0,0,0"/>
```

durch einen vertikalen Stapel aus Feld-Detail (oben) + Schäden-Karte (unten):

```xml
        <!-- Detail rechts: Feld-Karten oben, Primaere-Schaeden-Tabelle unten -->
        <Grid Grid.Column="2" Margin="8,0,0,0">
            <Grid.RowDefinitions>
                <RowDefinition Height="*"/>
                <RowDefinition Height="Auto"/>
            </Grid.RowDefinitions>

            <controls:RecordDetailsView Grid.Row="0" x:Name="Detail"/>

            <Border Grid.Row="1" Margin="0,8,0,0" Padding="12"
                    Background="{StaticResource CardBrush}" BorderBrush="{StaticResource BorderBrush}"
                    BorderThickness="1" CornerRadius="10">
                <StackPanel>
                    <DockPanel LastChildFill="False" Margin="0,0,0,8">
                        <TextBlock DockPanel.Dock="Left" Text="Primäre Schäden" FontSize="14" FontWeight="SemiBold"
                                   Foreground="{StaticResource TextBrush}" VerticalAlignment="Center"/>
                        <Button DockPanel.Dock="Right" x:Name="SchadenAddButton" Content="+"
                                Width="28" Height="24" FontSize="16" Padding="0"
                                Click="SchadenAdd_Click" ToolTip="Neue Beobachtung codieren"/>
                    </DockPanel>

                    <ListBox x:Name="SchadenList"
                             ItemsSource="{Binding SelectedProtocolEntries}"
                             MaxHeight="220"
                             Background="Transparent" BorderThickness="0"
                             HorizontalContentAlignment="Stretch"
                             ScrollViewer.HorizontalScrollBarVisibility="Disabled"
                             ScrollViewer.VerticalScrollBarVisibility="Auto"
                             MouseDoubleClick="SchadenList_MouseDoubleClick">
                        <ListBox.ItemTemplate>
                            <DataTemplate>
                                <Border Margin="0,2" Padding="8,5"
                                        Background="{StaticResource HeaderBrush}" BorderBrush="{StaticResource BorderBrush}"
                                        BorderThickness="1" CornerRadius="6">
                                    <Grid>
                                        <Grid.ColumnDefinitions>
                                            <ColumnDefinition Width="64"/>
                                            <ColumnDefinition Width="Auto"/>
                                            <ColumnDefinition Width="*"/>
                                            <ColumnDefinition Width="Auto"/>
                                        </Grid.ColumnDefinitions>
                                        <TextBlock Grid.Column="0" VerticalAlignment="Center" FontSize="12"
                                                   Foreground="{StaticResource NeonGreenBrush}"
                                                   Text="{Binding Converter={StaticResource SchadenMeterConv}}"/>
                                        <Border Grid.Column="1" VerticalAlignment="Center" Margin="6,0" Padding="6,2"
                                                CornerRadius="4" Background="#1E3A8A">
                                            <TextBlock Text="{Binding Code}" FontSize="11" FontWeight="SemiBold" Foreground="White"/>
                                        </Border>
                                        <TextBlock Grid.Column="2" VerticalAlignment="Center" Margin="6,0" FontWeight="SemiBold"
                                                   TextTrimming="CharacterEllipsis" Foreground="{StaticResource TextBrush}"
                                                   Text="{Binding Converter={StaticResource SchadenKlartextConv}}"/>
                                        <Border Grid.Column="3" VerticalAlignment="Center" Padding="6,2" CornerRadius="4"
                                                Background="{StaticResource HeaderBrush}" BorderBrush="{StaticResource BorderBrush}" BorderThickness="1">
                                            <TextBlock Text="{Binding Converter={StaticResource SchadenKategorieConv}}"
                                                       FontSize="10" Foreground="{StaticResource TextSecondaryBrush}"/>
                                        </Border>
                                    </Grid>
                                </Border>
                            </DataTemplate>
                        </ListBox.ItemTemplate>
                    </ListBox>
                </StackPanel>
            </Border>
        </Grid>
```

Ergänze die drei Konverter in den `UserControl.Resources` (oben in der Datei, zu den bestehenden `SummaryConv`/`ZkBrushConv`):

```xml
        <local:SchadenMeterConverter x:Key="SchadenMeterConv"/>
        <local:SchadenKlartextConverter x:Key="SchadenKlartextConv"/>
        <local:SchadenKategorieConverter x:Key="SchadenKategorieConv"/>
```

> Live-Aktualisierung: `SchadenList` bindet direkt an `SelectedProtocolEntries`. Nach dem Codieren ruft `OpenProtocol` → `RefreshSelectedProtocolEntries()` (Clear+Add) → die Tabelle aktualisiert sich automatisch. Gelöschte Einträge: `RefreshSelectedProtocolEntries` liefert die aktuellen Einträge; falls dort gelöschte enthalten sind, zeigt der Klartext/Code sie weiterhin — das ist akzeptabel und entspricht der bestehenden Beobachtungen-Liste.

- [ ] **Step 3: `HaltungsansichtView.xaml.cs` — `ActionRequested` + Doppelklick/„+"**

In `HaltungsansichtView.xaml.cs`:
- Ergänze die Delegate-Property (neben dem bestehenden `DetailBuilder`):

```csharp
    /// <summary>
    /// Von der DataPage gesetzt: führt eine Aktion (actionKey) auf einer Haltung aus,
    /// indem sie die bestehenden DataPage-Handler/Commands aufruft.
    /// </summary>
    public Action<string, HaltungRecord>? ActionRequested { get; set; }
```

- Ergänze die Handler (in der Klasse):

```csharp
    private void SchadenList_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        _ = sender; _ = e;
        if (HaltungList.SelectedItem is HaltungRecord record)
            ActionRequested?.Invoke("codieren", record);
    }

    private void SchadenAdd_Click(object sender, RoutedEventArgs e)
    {
        _ = sender; _ = e;
        if (HaltungList.SelectedItem is HaltungRecord record)
            ActionRequested?.Invoke("codieren", record);
    }
```

(`HaltungRecord` ist über `using AuswertungPro.Next.Domain.Models;` bereits importiert.)

- [ ] **Step 4: `DataPage.xaml.cs` — Routing-Methode + Verdrahtung**

In `DataPage.xaml.cs`, im Konstruktor direkt nach der bestehenden Zeile `HaltungsansichtView.DetailBuilder = BuildHaltungRecordDetails;` ergänzen:

```csharp
        HaltungsansichtView.ActionRequested = RouteHaltungsansichtAction;
```

Und die Routing-Methode hinzufügen (z. B. im Bereich „Haltung Record Details"):

```csharp
    private void RouteHaltungsansichtAction(string actionKey, HaltungRecord record)
    {
        if (DataContext is not DataPageViewModel vm)
            return;

        vm.Selected = record; // bestehende Handler fallen via ResolveActionRecord auf Selected zurueck
        var e = new RoutedEventArgs();
        switch (actionKey)
        {
            case "codieren": vm.OpenProtocolCommand.Execute(record); break;
            case "play": PlayMenu_Click(this, e); break;
            case "beobachtungen": BeobachtungenMenu_Click(this, e); break;
            case "printawu": PrintAwuHaltungsprotokollMenu_Click(this, e); break;
            case "openpdf": OpenOriginalPdfMenu_Click(this, e); break;
            case "costs": CostsMenu_Click(this, e); break;
            case "moveup": MoveRecordUpMenu_Click(this, e); break;
            case "movedown": MoveRecordDownMenu_Click(this, e); break;
            case "delete": DeleteSelectedRows(); break;
        }
    }
```

> Warum sicher: `PlayMenu_Click(this, e)` & Co. rufen `ResolveActionRecord(this, vm)` → `GetContextMenuRecord(this)` findet keine Zeile (this = DataPage) → liefert `vm.Selected`, das wir gerade gesetzt haben. Kein duplizierter Funktions-Code.

- [ ] **Step 5: Build (App geschlossen!)**

Run: `dotnet build src/AuswertungPro.Next.UI/AuswertungPro.Next.UI.csproj`
Expected: 0 Fehler, 0 Warnungen. (Läuft SewerStudio.exe? → schließen; sonst MSB3027/MSB3021-Sperre, kein Code-Fehler.)

- [ ] **Step 6: Tests**

Run: `dotnet test tests/AuswertungPro.Next.UI.Tests/AuswertungPro.Next.UI.Tests.csproj`
Expected: PASS (alle).

- [ ] **Step 7: Commit**

```bash
git add src/AuswertungPro.Next.UI/Views/Pages/Haltungsansicht/HaltungListItemConverters.cs src/AuswertungPro.Next.UI/Views/Pages/Haltungsansicht/HaltungsansichtView.xaml src/AuswertungPro.Next.UI/Views/Pages/Haltungsansicht/HaltungsansichtView.xaml.cs src/AuswertungPro.Next.UI/Views/Pages/DataPage.xaml.cs
git commit -m "feat(haltungsansicht): Primaere-Schaeden-Mini-Tabelle (Doppelklick/+ -> Codier-Editor)"
```

---

### Task 3: Rechtsklick-Menü in der Liste (gespiegelt vom DataGrid)

**Files:**
- Modify: `src/AuswertungPro.Next.UI/Views/Pages/Haltungsansicht/HaltungsansichtView.xaml`
- Modify: `src/AuswertungPro.Next.UI/Views/Pages/Haltungsansicht/HaltungsansichtView.xaml.cs`

- [ ] **Step 1: ContextMenu an die Liste + Rechtsklick-Selektion (XAML)**

In `HaltungsansichtView.xaml` am `<ListBox x:Name="HaltungList" …>` (der LINKEN Haltungsliste, NICHT `SchadenList`) ergänzen:
- Attribut `PreviewMouseRightButtonDown="HaltungList_PreviewMouseRightButtonDown"`.
- Ein `ContextMenu` als `ListBox.ContextMenu`:

```xml
            <ListBox.ContextMenu>
                <ContextMenu>
                    <MenuItem Header="Position nach oben" Click="CtxMoveUp_Click"/>
                    <MenuItem Header="Position nach unten" Click="CtxMoveDown_Click"/>
                    <Separator/>
                    <MenuItem Header="Beobachtungen..." Click="CtxBeobachtungen_Click"/>
                    <Separator/>
                    <MenuItem Header="Play" Click="CtxPlay_Click"/>
                    <MenuItem Header="Haltungsprotokoll AWU drucken..." Click="CtxPrintAwu_Click"/>
                    <MenuItem Header="Haltungsprotokoll Original (PDF) oeffnen..." Click="CtxOpenPdf_Click"/>
                    <MenuItem Header="Sanierungsmassnahmen..." Click="CtxCosts_Click"/>
                    <Separator/>
                    <MenuItem Header="Haltung loeschen" Click="CtxDelete_Click" Foreground="OrangeRed"/>
                </ContextMenu>
            </ListBox.ContextMenu>
```

- [ ] **Step 2: Handler im Code-Behind (`HaltungsansichtView.xaml.cs`)**

```csharp
    // Rechtsklick wählt zuerst die Zeile unter dem Cursor, damit das Menü auf der
    // richtigen Haltung arbeitet (auch wenn sie nicht selektiert war).
    private void HaltungList_PreviewMouseRightButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        _ = sender;
        var dep = e.OriginalSource as System.Windows.DependencyObject;
        while (dep is not null and not System.Windows.Controls.ListBoxItem)
            dep = System.Windows.Media.VisualTreeHelper.GetParent(dep);
        if (dep is System.Windows.Controls.ListBoxItem { DataContext: HaltungRecord record })
            HaltungList.SelectedItem = record;
    }

    private void RaiseAction(string actionKey)
    {
        if (HaltungList.SelectedItem is HaltungRecord record)
            ActionRequested?.Invoke(actionKey, record);
    }

    private void CtxMoveUp_Click(object sender, RoutedEventArgs e) { _ = sender; _ = e; RaiseAction("moveup"); }
    private void CtxMoveDown_Click(object sender, RoutedEventArgs e) { _ = sender; _ = e; RaiseAction("movedown"); }
    private void CtxBeobachtungen_Click(object sender, RoutedEventArgs e) { _ = sender; _ = e; RaiseAction("beobachtungen"); }
    private void CtxPlay_Click(object sender, RoutedEventArgs e) { _ = sender; _ = e; RaiseAction("play"); }
    private void CtxPrintAwu_Click(object sender, RoutedEventArgs e) { _ = sender; _ = e; RaiseAction("printawu"); }
    private void CtxOpenPdf_Click(object sender, RoutedEventArgs e) { _ = sender; _ = e; RaiseAction("openpdf"); }
    private void CtxCosts_Click(object sender, RoutedEventArgs e) { _ = sender; _ = e; RaiseAction("costs"); }
    private void CtxDelete_Click(object sender, RoutedEventArgs e) { _ = sender; _ = e; RaiseAction("delete"); }
```

(Diese `actionKey`-Werte sind exakt die aus `RouteHaltungsansichtAction` in Task 2.)

- [ ] **Step 3: Build (App geschlossen!)**

Run: `dotnet build src/AuswertungPro.Next.UI/AuswertungPro.Next.UI.csproj`
Expected: 0 Fehler, 0 Warnungen.

- [ ] **Step 4: Tests**

Run: `dotnet test tests/AuswertungPro.Next.UI.Tests/AuswertungPro.Next.UI.Tests.csproj`
Expected: PASS (alle).

- [ ] **Step 5: Commit**

```bash
git add src/AuswertungPro.Next.UI/Views/Pages/Haltungsansicht/HaltungsansichtView.xaml src/AuswertungPro.Next.UI/Views/Pages/Haltungsansicht/HaltungsansichtView.xaml.cs
git commit -m "feat(haltungsansicht): Rechtsklick-Menue in der Liste (gespiegelt vom DataGrid)"
```

---

### Task 4: Manuelle GUI-Abnahme

**Keine Code-Änderung.** App starten (nach Build), Haltungsansicht öffnen:

- [ ] **Rechtsklick** auf eine Haltung in der Liste → Menü erscheint (Position ↑/↓, Beobachtungen, Play, AWU drucken, Original-PDF, Sanierungsmassnahmen, Löschen); jede Aktion läuft auf der rechtsgeklickten Haltung (auch wenn vorher nicht markiert).
- [ ] **Play** öffnet das Video; **Beobachtungen…** öffnet das Beobachtungsfenster; **Sanierungsmassnahmen…** öffnet die Kosten/Massnahmen; **AWU/Original-PDF** wie im DataGrid.
- [ ] **Primäre Schäden**: Tabelle zeigt je Zeile Meter (grün) · Code (blaue Chip) · Klartext · Kategorie-Tag, kompakt; eigener Scroll bei vielen Einträgen.
- [ ] **Doppelklick** auf eine Schaden-Zeile **und** der **„+"-Knopf** öffnen den Codier-Editor; nach dem Codieren/Schließen aktualisiert sich die Tabelle automatisch.
- [ ] **Layout** harmonisch: Feld-Karten oben, Schäden-Karte unten in gleicher Karten-Optik; **Popup** (Doppelklick im DataGrid) unverändert.

Falls etwas hakt: systematic-debugging (Ursache vor Fix).

---

## Self-Review
**Spec-Abdeckung:** Teil 1 (Rechtsklick) → Task 3 + Routing in Task 2. Teil 2 (Schäden-Tabelle, Doppelklick/„+") → Task 2 (+ Formatter Task 1). Teil 3 (harmonische Anordnung) → Task 2 Layout. Reine Projektion testbar → Task 1. ✓
**Placeholder-Scan:** kein TBD; alle Code-Schritte vollständig. ✓
**Typ-Konsistenz:** `actionKey`-Strings identisch zwischen Task 2 (`RouteHaltungsansichtAction`) und Task 3 (`RaiseAction`): codieren/play/beobachtungen/printawu/openpdf/costs/moveup/movedown/delete. `SchadenZeile`-Felder (Meter/Code/Klartext/Kategorie) konsistent zwischen Formatter (Task 1) und Konvertern (Task 2). `ActionRequested`-Signatur `Action<string,HaltungRecord>` in View (Task 2) == DataPage-Setter (Task 2). ✓
**Abweichungs-Notiz:** Kategorie-Mapping (BA→Zustand, BB/BD→Betrieb, BC→Bestand) ist eine sinnvolle, leicht anpassbare Default-Zuordnung gemäß Referenz-Bild 2; falls fachlich feiner gewünscht, im Formatter (eine Stelle) änderbar.
