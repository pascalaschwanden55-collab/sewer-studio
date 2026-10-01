# Optik-Nachprüfung 08.09.2026 — Umsetzungsplan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Die drei Sichtfehler, die Schreibweise, die leeren Stellen und die Dunkelmodus-Befunde aus der optischen Nachprüfung beheben — jeder Punkt mit einem Wächter, der ein Zurückfallen rot macht.

**Architecture:** Reine Oberflächenarbeit in WPF: XAML-Vorlagen, Theme-Tokens, wenige Zeilen ViewModel. Keine Geschäftslogik, keine neuen Dienste, keine Registrierung. Jede Aufgabe folgt demselben Muster wie die bisherigen `DesignAudit*Tests`: erst ein Quelltext-Wächter, der die heutige Schwäche rot macht, dann die Änderung, dann grün.

**Tech Stack:** WPF / .NET 10, xUnit (`tests/AuswertungPro.Next.UI.Tests`), Theme-Tokens in `src/AuswertungPro.Next.UI/Theme/Theme.xaml` (dunkel) und `ThemeLight.xaml` (hell).

**Spec:** `docs/reviews/2026-09-08-redesign-gesamtaudit/OPTIK-NACHPRUEFUNG.md` (Befunde O1–O19 mit Bildverweisen). Dieser Plan setzt **O1, O2, O3, O4, O5, O6, O7, O10, O11, O12, O16, O17, O18, O19** um.

**Nicht in diesem Plan — und warum:**

| Befund | Grund |
|---|---|
| B1–B7 aus `XTF-FELDAUDIT.md` | Eine **zweite Sitzung setzt sie gerade um** (Stand 08.09. 19:30: Fettabscheider korrigiert, `Nutzungsart`/`Bauwerksart`/`Versickerungsart` in `GridDropdownFieldPolicy`, neue `SchachtNormoptionen`, `SiaBegriffAnzeige`, `SchachtDetailGruppen`). Nicht anfassen. |
| O8, O9, O13, O15 | Brauchen eine Gestaltungsentscheidung von Pascal (Leerzustand-Baustein, Übersichtskarten, Druckcenter-Tabelle, drei Gesichter der Zustandsklasse). Eigener Folgeplan. |
| O14 | Ein Tooltip am Tabellenkopf über den gemeinsamen Kopfstil würde bei Spalten mit eigenem Kopf-Inhalt (Statusspalten) ein Element mit Elternteil in den Tooltip legen → Laufzeitfehler. Braucht den Weg über `DataPageColumnSetup`. Folgeplan. |
| Abschnitt F (Kleineres) | Folgeplan. |

## Global Constraints

- **Arbeitsplatz:** Ein eigener Worktree, damit Build-Artefakte und Commits nicht mit der parallelen Sitzung kollidieren:
  `git worktree add ../Sewer-Studio_KI_4.5-optik -b feature/optik-nachpruefung` (von `HEAD` = `0d3ea8d89` oder neuer). Alle Pfade unten sind relativ zu diesem Worktree.
- **Diese Dateien sind tabu** (die parallele Sitzung arbeitet darin; im Worktree fehlen ihre Änderungen, beim Merge gäbe es Konflikte): `GridDropdownFieldPolicy.cs`, `SchachtFunktionVokabular.cs`, `SchaechteColumnPolicy.cs`, `SchaechteColumnViewCatalog.cs`, `DataPageColumnViewCatalog.cs`, `DataPageRecordDetailsBuilder.cs`, `SchaechteRecordDetailsBuilder.cs`, `SchachtDetailGruppen.cs`, `SchachtNormoptionen.cs`, `SiaBegriffAnzeige.cs`, `SiaAbmessung.cs`, `XtfSchachtPlanBuilder.cs`, `XtfNeuExportService.cs`, `XtfNeuWriter.cs`, `IXtfNeuExportService.cs`.
- **Commits:** nur die in der Aufgabe genannten Dateien mit `git add <Pfad>` — nie `git add -A`. Commit-Text deutsch, am Ende `Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>`.
- **Sichtbare Texte** tragen echte Umlaute (`ä ö ü`) und Schweizer `ss`, **kein `ß`**. Quellcode-Kommentare bleiben `ae/oe/ue`.
- **Keine festen Farbwerte** (`#RRGGBB`) ausserhalb der sechs Video-Dateien — immer `{DynamicResource …}`. Neue Tokens in **beiden** Themes mit gleichem Schlüssel.
- **Schriftgrössen** nur als `{DynamicResource TextXS|TextS|TextM|TextL|TextXL|TextTitle}`; 11 px ist die Untergrenze.
- **Radien** nur als Tokens: `RadiusS` 4, `RadiusM` 6, `RadiusL` 8, `RadiusXL` 10, `RadiusChip` 11, `RadiusPill` 15.
- **B7-Falle (CLAUDE.md):** Der implizite `TextBlock`-Stil beider Themes setzt `Foreground` selbst und schlägt `TextElement.Foreground`. Tinte in Vorlagen über `ContentPresenter.Resources` mit einem engeren `TextBlock`-Stil durchreichen.
- **Keine NuGet-Pakete**, keine neuen Dienste, kein Refactoring am Bestand.
- **Build:** `dotnet build src/AuswertungPro.Next.UI/AuswertungPro.Next.UI.csproj --nologo -v q` (nur das UI-Projekt; die Solution enthält 44 Werkzeugprojekte).
- **Tests:** `dotnet test tests/AuswertungPro.Next.UI.Tests/AuswertungPro.Next.UI.Tests.csproj --nologo --filter "FullyQualifiedName~<Klasse>"`. Am Ende einmal das ganze UI-Testprojekt ohne Filter (Referenz vor diesem Plan: 6887 grün, 18 übersprungen).
- Läuft `SewerStudio.exe`, sperrt es die DLLs — vorher schliessen (Memory-Notiz 27.08.).

---

### Task 1: O1 — Untertitel neben den Titel (Sanierungs-Matrix, Schacht-Matrix)

**Files:**
- Modify: `src/AuswertungPro.Next.UI/Views/Pages/SanierungsMatrixPage.xaml:20-25`
- Modify: `src/AuswertungPro.Next.UI/Views/Pages/SchachtSanierungsMatrixPage.xaml:19-22`
- Test: `tests/AuswertungPro.Next.UI.Tests/DesignAuditNovaSeitenkoepfeTests.cs`

**Interfaces:**
- Consumes: `DesignAuditNovaPaletteTests.RepoFile(params string[])` (bestehender Pfadhelfer, im selben Testprojekt).
- Produces: nichts für spätere Tasks.

Hintergrund: Der Stil `PageTitle` zeichnet seine Akzentlinie als `TextDecoration` mit `PenOffset="7"` (`Theme.xaml:196-203`). Steht der Untertitel in einem **vertikalen** `StackPanel` 2 px darunter, läuft die Linie durch seinen Text. Alle anderen Seiten setzen den Untertitel im `NovaPageHeader` **rechts neben** den Titel. Die beiden Matrix-Seiten dürfen den `NovaPageHeader` nicht verwenden (Wächter `Sanierungs_Matrix_Seiten_binden_den_Untertitel_statt_NovaPageHeader` und `DesignAuditThemeResourceTests` verlangen wörtlich `Text="{Binding PageTitle}"`). Deshalb: dieselbe Anordnung, aber von Hand.

- [ ] **Step 1: Wächter schreiben (rot)**

In `DesignAuditNovaSeitenkoepfeTests.cs` hinter der Methode `Sanierungs_Matrix_Seiten_binden_den_Untertitel_statt_NovaPageHeader` einfügen:

```csharp
    /// <summary>
    /// Optik-Nachpruefung O1: Der PageTitle-Stil zeichnet seine Akzentlinie als TextDecoration
    /// 7 px unter der Grundlinie. Steht der Untertitel direkt darunter, laeuft die Linie durch
    /// seinen Text ("durchgestrichen", Bild Light-Sanierungs-Matrix.png). Auf beiden Matrix-
    /// Seiten muss der Untertitel deshalb NEBEN dem Titel stehen — wie im NovaPageHeader.
    /// </summary>
    [Theory]
    [InlineData("SanierungsMatrixPage.xaml")]
    [InlineData("SchachtSanierungsMatrixPage.xaml")]
    public void Matrix_Untertitel_steht_neben_dem_Titel_und_nicht_unter_der_Akzentlinie(string datei)
    {
        var xaml = File.ReadAllText(DesignAuditNovaPaletteTests.RepoFile("src", "AuswertungPro.Next.UI", "Views", "Pages", datei));
        var titel = xaml.IndexOf("Text=\"{Binding PageTitle}\"", StringComparison.Ordinal);
        Assert.True(titel >= 0, "Titelbindung fehlt");

        var panelStart = xaml.LastIndexOf("<StackPanel", titel, StringComparison.Ordinal);
        var panelTag = xaml[panelStart..xaml.IndexOf('>', panelStart)];
        Assert.Contains("Orientation=\"Horizontal\"", panelTag);

        var untertitelBlock = xaml[titel..xaml.IndexOf("</StackPanel>", titel, StringComparison.Ordinal)];
        Assert.Contains("Text=\"{Binding PageSubtitle}\"", untertitelBlock);
        Assert.Contains("VerticalAlignment=\"Bottom\"", untertitelBlock);
        Assert.DoesNotContain("Margin=\"0,2,0,0\"", untertitelBlock);
    }
```

Falls die Datei kein `using System;` hat: hinzufügen (für `StringComparison`).

- [ ] **Step 2: Wächter laufen lassen — muss rot sein**

Run: `dotnet test tests/AuswertungPro.Next.UI.Tests/AuswertungPro.Next.UI.Tests.csproj --nologo --filter "FullyQualifiedName~DesignAuditNovaSeitenkoepfeTests"`
Expected: 2 rot (`Matrix_Untertitel_steht_neben_dem_Titel…` für beide Dateien), Rest grün.

- [ ] **Step 3: Beide Köpfe umstellen**

`SanierungsMatrixPage.xaml` — den Block

```xml
                <StackPanel DockPanel.Dock="Left">
                    <TextBlock Text="{Binding PageTitle}"
                               Style="{StaticResource PageTitle}"/>
                    <TextBlock Text="{Binding PageSubtitle}"
                               Style="{StaticResource Caption}" Margin="0,2,0,0"/>
                </StackPanel>
```

ersetzen durch

```xml
                <!-- Untertitel NEBEN dem Titel, wie im NovaPageHeader: Die Akzentlinie des
                     PageTitle-Stils ist eine TextDecoration mit PenOffset 7 und laeuft sonst
                     durch einen darunter stehenden Untertitel (Optik-Nachpruefung O1). -->
                <StackPanel DockPanel.Dock="Left" Orientation="Horizontal" VerticalAlignment="Center">
                    <TextBlock Text="{Binding PageTitle}"
                               Style="{StaticResource PageTitle}"/>
                    <TextBlock Text="{Binding PageSubtitle}"
                               Style="{StaticResource Caption}" Margin="10,0,0,3"
                               VerticalAlignment="Bottom"/>
                </StackPanel>
```

`SchachtSanierungsMatrixPage.xaml` — den Block

```xml
                <StackPanel DockPanel.Dock="Left">
                    <TextBlock Text="{Binding PageTitle}" Style="{StaticResource PageTitle}"/>
                    <TextBlock Text="{Binding PageSubtitle}" Style="{StaticResource Caption}" Margin="0,2,0,0"/>
                </StackPanel>
```

ersetzen durch

```xml
                <!-- Untertitel NEBEN dem Titel, siehe SanierungsMatrixPage (Optik-Nachpruefung O1). -->
                <StackPanel DockPanel.Dock="Left" Orientation="Horizontal" VerticalAlignment="Center">
                    <TextBlock Text="{Binding PageTitle}" Style="{StaticResource PageTitle}"/>
                    <TextBlock Text="{Binding PageSubtitle}" Style="{StaticResource Caption}"
                               Margin="10,0,0,3" VerticalAlignment="Bottom"/>
                </StackPanel>
```

- [ ] **Step 4: Bauen und Wächter grün**

Run: `dotnet build src/AuswertungPro.Next.UI/AuswertungPro.Next.UI.csproj --nologo -v q` dann den Test-Befehl aus Step 2.
Expected: Build 0 Fehler; alle `DesignAuditNovaSeitenkoepfeTests` grün. Zusätzlich `--filter "FullyQualifiedName~DesignAuditThemeResourceTests"` grün (verlangt weiterhin die wörtlichen Bindungen).

- [ ] **Step 5: Commit**

```bash
git add src/AuswertungPro.Next.UI/Views/Pages/SanierungsMatrixPage.xaml src/AuswertungPro.Next.UI/Views/Pages/SchachtSanierungsMatrixPage.xaml tests/AuswertungPro.Next.UI.Tests/DesignAuditNovaSeitenkoepfeTests.cs
git commit -m "Optik O1: Untertitel der Matrix-Seiten neben den Titel statt unter die Akzentlinie

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 2: O2 — Feste Farben in Triggern durch Theme-Tokens ersetzen, Wächter erweitern

**Files:**
- Modify: `src/AuswertungPro.Next.UI/Theme/Theme.xaml:99-101` (dunkel, hinter `WarningSubtleBrush`)
- Modify: `src/AuswertungPro.Next.UI/Theme/ThemeLight.xaml:99-101` (hell, gleiche Stelle)
- Modify: `src/AuswertungPro.Next.UI/Views/Controls/RecordDetailsView.xaml:506-511`
- Modify: `src/AuswertungPro.Next.UI/Views/Windows/SanierungsmassnahmenWindow.xaml:320,323,342,493-494`
- Test: `tests/AuswertungPro.Next.UI.Tests/DesignAuditFeinschliffTests.cs:224`

**Interfaces:**
- Produces: acht neue Theme-Tokens (beide Themes): `SanierenSubtleBrush`, `SanierenBorderBrush`, `AusgefuehrtSubtleBrush`, `AusgefuehrtBorderBrush`, `DangerRowBrush`, `WarningRowBrush`, `UebertragenSubtleBrush`, `UebertragenTextBrush`.

Hintergrund: Der Wächter `Feste_Farben_gibt_es_nur_in_Video_Fenstern` prüft nur die Attributform `Background="#…"`. Die Setter-Form `<Setter Property="Background" Value="#…"/>` in Triggern rutscht durch — und genau so entstand die hellgrüne Karte im Dunkelmodus (Bild `tabellen/Dark-Haltungen-standard.png`).

- [ ] **Step 1: Wächter erweitern (rot)**

In `DesignAuditFeinschliffTests.cs` die Zeile

```csharp
        var festeFarbe = new Regex("\\b(Background|Foreground|BorderBrush|Fill|Stroke)=\"#[0-9A-Fa-f]{6,8}\"", RegexOptions.Compiled);
```

ersetzen durch

```csharp
        // Optik-Nachpruefung O2: auch die Setter-Form in Triggern (<Setter Property="Background"
        // Value="#..."/>) zaehlt — so kam die hellgruene Sanieren-Karte in den Dunkelmodus.
        var festeFarbe = new Regex(
            "\\b(Background|Foreground|BorderBrush|Fill|Stroke)=\"#[0-9A-Fa-f]{6,8}\""
            + "|Property=\"(Background|Foreground|BorderBrush|Fill|Stroke)\"\\s+Value=\"#[0-9A-Fa-f]{6,8}\"",
            RegexOptions.Compiled);
```

- [ ] **Step 2: Wächter laufen lassen — muss rot sein**

Run: `dotnet test tests/AuswertungPro.Next.UI.Tests/AuswertungPro.Next.UI.Tests.csproj --nologo --filter "FullyQualifiedName~DesignAuditFeinschliffTests.Feste_Farben"`
Expected: rot mit genau diesen neun Treffern: `RecordDetailsView.xaml:506,507,510,511` und `SanierungsmassnahmenWindow.xaml:320,323,342,493,494`. Andere Treffer → stoppen und melden (dann hat sich der Bestand seit dem Plan geändert).

- [ ] **Step 3: Tokens in beiden Themes anlegen**

`Theme.xaml` (dunkel) — direkt hinter der Zeile `<SolidColorBrush x:Key="WarningSubtleBrush" Color="#FF493719"/>` einfügen:

```xml
    <!-- Optik-Nachpruefung O2: Hervorhebungen im Eingabeformular (Sanieren / Ausgefuehrt durch)
         und im Sanierungsmassnahmen-Fenster. Vorher feste Hellwerte in Triggern — im Dunkeln
         eine leuchtende Karte mit unlesbarem Text. Die Zeilenfarben sind halbtransparent und
         gelten deshalb in beiden Themen gleich. -->
    <SolidColorBrush x:Key="SanierenSubtleBrush" Color="#FF1B3A24"/>
    <SolidColorBrush x:Key="SanierenBorderBrush" Color="#FF6AA84F"/>
    <SolidColorBrush x:Key="AusgefuehrtSubtleBrush" Color="#FF14303F"/>
    <SolidColorBrush x:Key="AusgefuehrtBorderBrush" Color="#FF3FB7E6"/>
    <SolidColorBrush x:Key="DangerRowBrush" Color="#33FF4444"/>
    <SolidColorBrush x:Key="WarningRowBrush" Color="#33FF8C00"/>
    <SolidColorBrush x:Key="UebertragenSubtleBrush" Color="#FF1B3A24"/>
    <SolidColorBrush x:Key="UebertragenTextBrush" Color="#FF9BE7A9"/>
```

`ThemeLight.xaml` (hell) — hinter `<SolidColorBrush x:Key="WarningSubtleBrush" Color="#FFFFF3CD"/>` einfügen:

```xml
    <!-- Optik-Nachpruefung O2: siehe Theme.xaml. Die hellen Werte sind die bisherigen. -->
    <SolidColorBrush x:Key="SanierenSubtleBrush" Color="#FFEAF6E3"/>
    <SolidColorBrush x:Key="SanierenBorderBrush" Color="#FF6AA84F"/>
    <SolidColorBrush x:Key="AusgefuehrtSubtleBrush" Color="#FFE4F3FA"/>
    <SolidColorBrush x:Key="AusgefuehrtBorderBrush" Color="#FF00A2D6"/>
    <SolidColorBrush x:Key="DangerRowBrush" Color="#33FF4444"/>
    <SolidColorBrush x:Key="WarningRowBrush" Color="#33FF8C00"/>
    <SolidColorBrush x:Key="UebertragenSubtleBrush" Color="#FFD7F5DD"/>
    <SolidColorBrush x:Key="UebertragenTextBrush" Color="#FF0F3D1F"/>
```

- [ ] **Step 4: Die neun Stellen umstellen**

`RecordDetailsView.xaml` (Zeilen 506-511):

```xml
                                                            <DataTrigger Binding="{Binding HighlightKind}" Value="{x:Static windows:RecordDetailHighlightKind.Sanieren}">
                                                                <Setter Property="Background" Value="{DynamicResource SanierenSubtleBrush}"/>
                                                                <Setter Property="BorderBrush" Value="{DynamicResource SanierenBorderBrush}"/>
                                                            </DataTrigger>
                                                            <DataTrigger Binding="{Binding HighlightKind}" Value="{x:Static windows:RecordDetailHighlightKind.AusgefuehrtDurch}">
                                                                <Setter Property="Background" Value="{DynamicResource AusgefuehrtSubtleBrush}"/>
                                                                <Setter Property="BorderBrush" Value="{DynamicResource AusgefuehrtBorderBrush}"/>
                                                            </DataTrigger>
```

`SanierungsmassnahmenWindow.xaml`:

- Zeile 320: `Value="#33FF4444"` → `Value="{DynamicResource DangerRowBrush}"`
- Zeile 323: `Value="#33FF8C00"` → `Value="{DynamicResource WarningRowBrush}"`
- Zeile 342: `<Setter Property="Foreground" Value="#FF8C00"/>` → `<Setter Property="Foreground" Value="{DynamicResource WarningTextBrush}"/>`
- Zeile 493: `Value="#D7F5DD"` → `Value="{DynamicResource UebertragenSubtleBrush}"`
- Zeile 494: `Value="#0F3D1F"` → `Value="{DynamicResource UebertragenTextBrush}"`

- [ ] **Step 5: Bauen und Wächter grün**

Run: Build-Befehl, dann `--filter "FullyQualifiedName~DesignAuditFeinschliffTests"` und `--filter "FullyQualifiedName~DesignAuditThemeResourceTests"` (prüft, dass beide Themes dieselben Schlüssel führen).
Expected: alles grün.

- [ ] **Step 6: Commit**

```bash
git add src/AuswertungPro.Next.UI/Theme/Theme.xaml src/AuswertungPro.Next.UI/Theme/ThemeLight.xaml src/AuswertungPro.Next.UI/Views/Controls/RecordDetailsView.xaml src/AuswertungPro.Next.UI/Views/Windows/SanierungsmassnahmenWindow.xaml tests/AuswertungPro.Next.UI.Tests/DesignAuditFeinschliffTests.cs
git commit -m "Optik O2: Trigger-Farben als Theme-Tokens, Farbwaechter sieht jetzt auch Setter

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 3: O16 — Zustandsklasse in der Schattenauswertung als Marke statt gelber Text

**Files:**
- Modify: `src/AuswertungPro.Next.UI/Views/Pages/SchattenauswertungPage.xaml:1-8` (Namensraum, Ressource) und `:121-137` (zwei Spalten)
- Create: `tests/AuswertungPro.Next.UI.Tests/DesignAuditOptikNachpruefungTests.cs`

**Interfaces:**
- Consumes: `AuswertungPro.Next.UI.DataPage.ZustandsklasseInkConverter` (bestehend, `IValueConverter`, liefert die Tinte mit ≥ 4,5:1 zur Klassenfarbe), `local:ZustandsklasseToBrushConverter` (bestehend, Klassenfarbe als Hintergrund), Token `RadiusChip`.
- Produces: die Testklasse `DesignAuditOptikNachpruefungTests` (Tasks 4, 6, 7, 8, 9, 10 hängen ihre Wächter dort an).

Hintergrund: Beide ZK-Spalten färben die **Textfarbe** mit der Hintergrundpalette der Marken → „2" ist Gelb auf Weiss (`Light-Schattenauswertung.png`). Die Haltungstabelle löst das mit Marke + `ZustandsklasseInkPolicy`.

- [ ] **Step 1: Testklasse anlegen, Wächter schreiben (rot)**

Neue Datei `tests/AuswertungPro.Next.UI.Tests/DesignAuditOptikNachpruefungTests.cs`:

```csharp
using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Waechter der optischen Nachpruefung vom 08.09.2026
/// (docs/reviews/2026-09-08-redesign-gesamtaudit/OPTIK-NACHPRUEFUNG.md). Jeder Test haelt
/// einen dort belegten Befund fest, damit er nicht zurueckfaellt. Reine Quelltext-Pruefungen
/// nach dem Muster der uebrigen DesignAudit*-Klassen.
/// </summary>
public sealed class DesignAuditOptikNachpruefungTests
{
    private static string Ui(params string[] teile)
        => File.ReadAllText(RepoFile(new[] { "src", "AuswertungPro.Next.UI" }.Concat(teile).ToArray()));

    /// <summary>O16: Die Zustandsklasse ist eine Marke mit Tintenregel, kein gelber Text auf Weiss.</summary>
    [Fact]
    public void Schattenauswertung_zeigt_die_Zustandsklasse_als_Marke_mit_Tintenregel()
    {
        var xaml = Ui("Views", "Pages", "SchattenauswertungPage.xaml");
        Assert.Contains("ZustandsklasseInkConverter", xaml);
        Assert.DoesNotContain("Foreground=\"{Binding MenschKlasse, Converter={StaticResource ZustandsklasseBrush}}\"", xaml);
        Assert.DoesNotContain("Foreground=\"{Binding SchattenKlasse, Converter={StaticResource ZustandsklasseBrush}}\"", xaml);
        Assert.Equal(2, Regex.Matches(xaml, "CornerRadius=\"\\{DynamicResource RadiusChip\\}\"").Count);
    }
}
```

- [ ] **Step 2: Wächter laufen lassen — muss rot sein**

Run: `dotnet test tests/AuswertungPro.Next.UI.Tests/AuswertungPro.Next.UI.Tests.csproj --nologo --filter "FullyQualifiedName~DesignAuditOptikNachpruefungTests"`
Expected: 1 rot.

- [ ] **Step 3: Namensraum und Konverter registrieren**

`SchattenauswertungPage.xaml`, Kopf: hinter `xmlns:ctrl="clr-namespace:AuswertungPro.Next.UI.Controls"` einfügen

```xml
             xmlns:dp="clr-namespace:AuswertungPro.Next.UI.DataPage"
```

und in `<UserControl.Resources>` hinter `<local:ZustandsklasseToBrushConverter x:Key="ZustandsklasseBrush"/>`:

```xml
        <!-- Optik-Nachpruefung O16: Tinte mit mindestens 4,5:1 auf jeder Klassenfarbe —
             dieselbe Regel wie die Marken der Haltungstabelle. -->
        <dp:ZustandsklasseInkConverter x:Key="ZkInk"/>
```

- [ ] **Step 4: Beide Spalten als Marke**

Den Block der Spalte „ZK (Ich)"

```xml
                        <DataGridTemplateColumn Header="ZK (Ich)" Width="70">
                            <DataGridTemplateColumn.CellTemplate>
                                <DataTemplate>
                                    <TextBlock Text="{Binding MenschKlasse}" HorizontalAlignment="Center"
                                               FontWeight="SemiBold"
                                               Foreground="{Binding MenschKlasse, Converter={StaticResource ZustandsklasseBrush}}"/>
                                </DataTemplate>
                            </DataGridTemplateColumn.CellTemplate>
                        </DataGridTemplateColumn>
```

ersetzen durch

```xml
                        <DataGridTemplateColumn Header="ZK (Ich)" Width="70">
                            <DataGridTemplateColumn.CellTemplate>
                                <DataTemplate>
                                    <!-- Marke wie in der Haltungstabelle: Klassenfarbe als Grund, Tinte
                                         aus der Ink-Regel. Ohne Wert keine leere Marke (O16). -->
                                    <Border HorizontalAlignment="Center" Padding="8,1" MinWidth="30"
                                            CornerRadius="{DynamicResource RadiusChip}"
                                            Background="{Binding MenschKlasse, Converter={StaticResource ZustandsklasseBrush}}">
                                        <Border.Style>
                                            <Style TargetType="Border">
                                                <Style.Triggers>
                                                    <DataTrigger Binding="{Binding MenschKlasse}" Value="">
                                                        <Setter Property="Visibility" Value="Collapsed"/>
                                                    </DataTrigger>
                                                    <DataTrigger Binding="{Binding MenschKlasse}" Value="{x:Null}">
                                                        <Setter Property="Visibility" Value="Collapsed"/>
                                                    </DataTrigger>
                                                </Style.Triggers>
                                            </Style>
                                        </Border.Style>
                                        <TextBlock Text="{Binding MenschKlasse, StringFormat='Z{0}'}" HorizontalAlignment="Center"
                                                   FontWeight="SemiBold" FontSize="{DynamicResource TextXS}"
                                                   Foreground="{Binding MenschKlasse, Converter={StaticResource ZkInk}}"/>
                                    </Border>
                                </DataTemplate>
                            </DataGridTemplateColumn.CellTemplate>
                        </DataGridTemplateColumn>
```

Den Block der Spalte „ZK (Schatten)" genauso ersetzen — mit `SchattenKlasse` statt `MenschKlasse` an allen vier Stellen und dem bestehenden `ToolTip="{Binding SchattenNotenTooltip}"` am `Border` (nicht am TextBlock):

```xml
                        <DataGridTemplateColumn Header="ZK (Schatten)" Width="95">
                            <DataGridTemplateColumn.CellTemplate>
                                <DataTemplate>
                                    <Border HorizontalAlignment="Center" Padding="8,1" MinWidth="30"
                                            CornerRadius="{DynamicResource RadiusChip}"
                                            ToolTip="{Binding SchattenNotenTooltip}"
                                            Background="{Binding SchattenKlasse, Converter={StaticResource ZustandsklasseBrush}}">
                                        <Border.Style>
                                            <Style TargetType="Border">
                                                <Style.Triggers>
                                                    <DataTrigger Binding="{Binding SchattenKlasse}" Value="">
                                                        <Setter Property="Visibility" Value="Collapsed"/>
                                                    </DataTrigger>
                                                    <DataTrigger Binding="{Binding SchattenKlasse}" Value="{x:Null}">
                                                        <Setter Property="Visibility" Value="Collapsed"/>
                                                    </DataTrigger>
                                                </Style.Triggers>
                                            </Style>
                                        </Border.Style>
                                        <TextBlock Text="{Binding SchattenKlasse, StringFormat='Z{0}'}" HorizontalAlignment="Center"
                                                   FontWeight="SemiBold" FontSize="{DynamicResource TextXS}"
                                                   Foreground="{Binding SchattenKlasse, Converter={StaticResource ZkInk}}"/>
                                    </Border>
                                </DataTemplate>
                            </DataGridTemplateColumn.CellTemplate>
                        </DataGridTemplateColumn>
```

- [ ] **Step 5: Bauen und Wächter grün**

Run: Build, dann `--filter "FullyQualifiedName~DesignAuditOptikNachpruefungTests"` und `--filter "FullyQualifiedName~DesignAuditFeinschliffTests"` (keine festen Farben, Umlaute).
Expected: grün.

- [ ] **Step 6: Commit**

```bash
git add src/AuswertungPro.Next.UI/Views/Pages/SchattenauswertungPage.xaml tests/AuswertungPro.Next.UI.Tests/DesignAuditOptikNachpruefungTests.cs
git commit -m "Optik O16: Zustandsklasse in der Schattenauswertung als Marke mit Tintenregel

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 4: O17/O18 — Abzeichen und Filterchips im Dunkelmodus lesbar

**Files:**
- Modify: `src/AuswertungPro.Next.UI/Views/Pages/HaltungStatusColumnFactory.cs:133`
- Modify: `src/AuswertungPro.Next.UI/Controls/FilterChipBar.xaml.cs:43`
- Modify: `src/AuswertungPro.Next.UI/Controls/FilterChipBar.xaml:93-96` (Vorlage `ZustandsklasseChipToggleButton`)
- Test: `tests/AuswertungPro.Next.UI.Tests/DesignAuditContrastTests.cs` (neue Theorie + Helfer)
- Test: `tests/AuswertungPro.Next.UI.Tests/DesignAuditOptikNachpruefungTests.cs` (zwei Wächter)

**Interfaces:**
- Consumes: `ZustandsklasseInkPolicy.InkFor(Color) : Color` (bestehend; die Klasse liegt im selben Namensraum wie `ZustandsklasseInkConverter` — vor Step 4 mit `grep -rn "class ZustandsklasseInkPolicy" src/` den Namensraum lesen und das `using` übernehmen), Token `SuccessTextBrush` (dunkel `#FF56D364`, hell `#FF15803D`, beide vorhanden).

Hintergrund (Vergrösserungen aus `tabellen/Dark-Haltungen-standard.png`): „fachlich geprüft" steht im Dunkeln mit `SuccessBrush` (#15803D) auf `SuccessSubtleBrush` (#14432A) — 2,3:1. Die ZK-Filterchips zeigen **weisse** Ziffern auf Gelb und Grün, obwohl der Code `Brushes.Black` setzt: Die Vorlage reicht die Tinte über `TextElement.Foreground` weiter, und der implizite `TextBlock`-Stil des Themes überschreibt sie (B7-Falle).

- [ ] **Step 1: Kontrast-Wächter schreiben (grün — er belegt die Zielwerte) und Quelltext-Wächter (rot)**

In `DesignAuditContrastTests.cs` hinter `Ki_text_reaches_normal_text_contrast_on_card_and_ki_subtle` einfügen:

```csharp
    /// <summary>
    /// Optik-Nachpruefung O17: Das Abzeichen „fachlich geprueft" stand im Dunkeln mit
    /// SuccessBrush (#15803D) auf SuccessSubtleBrush (#14432A) — 2,3:1. Die Abzeichen der
    /// Pruefung-Spalte (HaltungStatusColumnFactory.Kapsel) muessen auf ihrer eigenen Flaeche
    /// Normaltext-Kontrast erreichen: Erfolg mit SuccessTextBrush, Offen mit ColorTextMuted.
    /// </summary>
    [Theory]
    [InlineData("Theme.xaml")]
    [InlineData("ThemeLight.xaml")]
    public void Pruefung_Abzeichen_erreichen_Normaltext_Kontrast_auf_ihrer_Flaeche(string themeFile)
    {
        var xaml = File.ReadAllText(RepoFile("src", "AuswertungPro.Next.UI", "Theme", themeFile));
        Assert.True(Contrast(ReadBrushColor(xaml, "SuccessTextBrush"), ReadBrushColor(xaml, "SuccessSubtleBrush")) >= 4.5,
            "SuccessTextBrush auf SuccessSubtleBrush unter 4,5:1");
        Assert.True(Contrast(ReadColor(xaml, "ColorTextMuted"), ReadBrushColor(xaml, "SurfaceSubtleBrush")) >= 4.5,
            "MutedBrush auf SurfaceSubtleBrush unter 4,5:1");
    }

    /// <summary>Pinsel mit festem Farbwert (kein StaticResource auf eine Color).</summary>
    private static string ReadBrushColor(string xaml, string key)
    {
        var match = Regex.Match(
            xaml,
            $"<SolidColorBrush\\s+x:Key=\"{Regex.Escape(key)}\"\\s+Color=\"(?<value>#[0-9A-Fa-f]{{8}})\"");
        Assert.True(match.Success, $"Theme-Pinsel {key} mit festem Farbwert fehlt.");
        return match.Groups["value"].Value;
    }
```

In `DesignAuditOptikNachpruefungTests.cs` hinter dem O16-Test einfügen:

```csharp
    /// <summary>O17: Das Erfolgs-Abzeichen nimmt die Text-Tinte, nicht die Flaechenfarbe.</summary>
    [Fact]
    public void Pruefung_Abzeichen_verwenden_die_Text_Tinte_auf_der_Erfolgsflaeche()
    {
        var code = Ui("Views", "Pages", "HaltungStatusColumnFactory.cs");
        Assert.Contains("Kapsel(HaltungPruefstand.Abgeschlossen, \"SuccessSubtleBrush\", \"SuccessTextBrush\")", code);
        Assert.DoesNotContain("\"SuccessSubtleBrush\", \"SuccessBrush\"", code);
    }

    /// <summary>
    /// O18: Die ZK-Filterchips nehmen ihre Tinte aus der Ink-Regel und reichen sie an der
    /// B7-Falle vorbei (impliziter TextBlock-Stil) bis zur Ziffer durch.
    /// </summary>
    [Fact]
    public void Zustandsklassen_Filterchips_nehmen_die_Tinte_aus_der_Ink_Regel()
    {
        var code = Ui("Controls", "FilterChipBar.xaml.cs");
        Assert.DoesNotContain("Brushes.Black", code);
        Assert.Contains("ZustandsklasseInkPolicy.InkFor", code);

        var xaml = Ui("Controls", "FilterChipBar.xaml");
        Assert.Contains("RelativeSource={RelativeSource AncestorType=ToggleButton}", xaml);
    }
```

- [ ] **Step 2: Laufen lassen**

Run: `--filter "FullyQualifiedName~DesignAuditContrastTests"` und `--filter "FullyQualifiedName~DesignAuditOptikNachpruefungTests"`
Expected: Kontrast-Theorie grün (beide Themen; rechnerisch 5,8:1 dunkel, 4,6:1 hell für Erfolg; 4,8:1 / 4,9:1 für Offen). Die zwei neuen Quelltext-Wächter rot.

- [ ] **Step 3: Abzeichen-Tinte umstellen**

`HaltungStatusColumnFactory.cs`, Zeile 133:

```csharp
        huelle.AppendChild(Kapsel(HaltungPruefstand.Abgeschlossen, "SuccessSubtleBrush", "SuccessBrush"));
```

→

```csharp
        // Optik-Nachpruefung O17: SuccessBrush auf SuccessSubtleBrush war im Dunkeln 2,3:1.
        // SuccessTextBrush ist die fuer Text auf der Erfolgsflaeche gedachte Tinte (beide Themen).
        huelle.AppendChild(Kapsel(HaltungPruefstand.Abgeschlossen, "SuccessSubtleBrush", "SuccessTextBrush"));
```

- [ ] **Step 4: Chip-Tinte aus der Ink-Regel**

`FilterChipBar.xaml.cs`: `using`-Zeile für den Namensraum von `ZustandsklasseInkPolicy` ergänzen (siehe Interfaces). Dann in `ApplyZustandsklasseColors()`:

```csharp
            chip.Background = background;
            chip.Foreground = Brushes.Black;
```

→

```csharp
            chip.Background = background;
            // Optik-Nachpruefung O18: dieselbe Tintenregel wie die Marken der Tabelle
            // (mindestens 4,5:1 auf jeder Klassenfarbe) statt festem Schwarz.
            var tinte = new SolidColorBrush(ZustandsklasseInkPolicy.InkFor(((SolidColorBrush)background).Color));
            tinte.Freeze();
            chip.Foreground = tinte;
```

- [ ] **Step 5: Tinte an der B7-Falle vorbei durchreichen**

`FilterChipBar.xaml`, in der Vorlage des Stils `ZustandsklasseChipToggleButton` (zweite Vorlage der Datei, mit `x:Name="activeRing"`) den `ContentPresenter`

```xml
                            <ContentPresenter HorizontalAlignment="Center"
                                              VerticalAlignment="Center"
                                              RecognizesAccessKey="True"
                                              TextElement.Foreground="{TemplateBinding Foreground}"/>
```

ersetzen durch

```xml
                            <ContentPresenter HorizontalAlignment="Center"
                                              VerticalAlignment="Center"
                                              RecognizesAccessKey="True">
                                <!-- B7-Falle: Der implizite TextBlock-Stil des Themes setzt Foreground
                                     selbst und schlaegt TextElement.Foreground — im Dunkeln stand so
                                     eine weisse Ziffer auf Gelb. Tinte deshalb wie in den Knopfvorlagen
                                     ueber einen engeren Stil durchreichen (Optik-Nachpruefung O18). -->
                                <ContentPresenter.Resources>
                                    <Style TargetType="TextBlock" BasedOn="{StaticResource {x:Type TextBlock}}">
                                        <Setter Property="Foreground" Value="{Binding Foreground, RelativeSource={RelativeSource AncestorType=ToggleButton}}"/>
                                    </Style>
                                </ContentPresenter.Resources>
                            </ContentPresenter>
```

Nur diese eine Vorlage anfassen; der erste Stil `FilterChipToggleButton` (mit `TextSecondaryBrush`) bleibt.

- [ ] **Step 6: Bauen und alle Wächter grün**

Run: Build; dann `--filter "FullyQualifiedName~DesignAuditOptikNachpruefungTests"`, `--filter "FullyQualifiedName~DesignAuditContrastTests"`, `--filter "FullyQualifiedName~HaltungStatusColumnFactoryTests"`, `--filter "FullyQualifiedName~FilterChipBar"`.
Expected: alles grün.

- [ ] **Step 7: Commit**

```bash
git add src/AuswertungPro.Next.UI/Views/Pages/HaltungStatusColumnFactory.cs src/AuswertungPro.Next.UI/Controls/FilterChipBar.xaml src/AuswertungPro.Next.UI/Controls/FilterChipBar.xaml.cs tests/AuswertungPro.Next.UI.Tests/DesignAuditContrastTests.cs tests/AuswertungPro.Next.UI.Tests/DesignAuditOptikNachpruefungTests.cs
git commit -m "Optik O17/O18: Pruefung-Abzeichen und ZK-Filterchips im Dunkelmodus lesbar

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 5: O4/O5/O6 — Schweizer ss, echte Umlaute, keine Entwicklertexte

**Files:**
- Modify (ß → ss, XAML): `Views/Pages/ProjectPage.xaml:76`, `Views/Pages/SanierungsMatrixPage.xaml:33,37,86,326`, `Views/Pages/SchachtSanierungsMatrixPage.xaml:27,52`, `Views/Pages/BuilderPage.xaml:171,470,478`, `Views/Pages/DataPage.xaml:77,266`, `Views/Pages/Haltungsansicht/HaltungsansichtView.xaml:106`, `Views/Pages/SettingsPage.xaml:1206`, `Dialogs/CostCatalogEditorDialog.xaml:129`, `Dialogs/PositionTemplateEditorDialog.xaml:21,39`, `Views/ProtocolHistoryWindow.xaml:47`, `Views/ProtocolObservationsWindow.xaml:150` (alle unter `src/AuswertungPro.Next.UI/`)
- Modify (ß → ss, C#): `DataPage/DataPageCostRestoreController.cs:66`, `DataPage/DataPageMeasureSuggestionController.cs:62-63`, `ViewModels/Pages/DataPageViewModel.cs:733`, `Views/ProtocolEntryEditorDialog.xaml.cs:618`, `Views/Windows/PhotoMeasurementToolPresentationPolicy.cs:63-68`, `Views/Windows/PhotoMeasurementWindow.Rendering.cs:242`
- Modify (Umlaute): `ViewModels/Pages/SanierungsMatrixPageViewModel.cs:102,331,433`, `ViewModels/Pages/SchachtSanierungsMatrixPageViewModel.cs:198-199`
- Modify (Entwicklertexte): `ViewModels/Pages/SchaechtePageViewModel.cs:486`, `Views/Pages/ProjectPage.xaml:88`, `ViewModels/Pages/BuilderPageFilterSummaryBuilder.cs:40`, `Views/Pages/DataPage.xaml:144`
- Test: `tests/AuswertungPro.Next.UI.Tests/DesignAuditFeinschliffTests.cs:36-61` (ß-Prüfung), `DesignAuditLaufzeittexteTests.cs` (Quellen, Wörter, ß), `DesignAuditThemeResourceTests.cs:76`, `DataPageMeasureSuggestionControllerTests.cs`, `DataPageCostRestoreControllerTests.cs`, `BuilderPageRowBuilderTests.cs`

**Interfaces:** keine.

Hinweis: `ProjectPage.xaml:34` („Programm schließen") wird in Task 8 **entfernt**, nicht umgeschrieben. Die Treffer „Records:" in `DataPageMediaSearchControllerTests`/`DataPageVideoAnalysisControllerTests` betreffen andere Texte und bleiben.

- [ ] **Step 1: Wächter erweitern (rot)**

`DesignAuditFeinschliffTests.cs`, in `Sichtbare_Texte_verwenden_echte_Umlaute` die Zeile

```csharp
                    if (UmlautErsatz.IsMatch(wert))
                        treffer.Add($"{Relativ(datei)}:{i + 1}: {m.Groups[1].Value}=\"{wert}\"");
```

ersetzen durch

```csharp
                    // Optik-Nachpruefung O4: Schweizer Schreibweise ist ss, kein scharfes ß.
                    if (UmlautErsatz.IsMatch(wert) || wert.Contains('ß'))
                        treffer.Add($"{Relativ(datei)}:{i + 1}: {m.Groups[1].Value}=\"{wert}\"");
```

und den Fehlertext

```csharp
            "Sichtbare Texte schreiben Umlaute als ae/oe/ue. Die Konvention gilt nur fuer den Quellcode, nicht fuer das, was der Nutzer liest:\n"
```

→

```csharp
            "Sichtbare Texte schreiben Umlaute als ae/oe/ue oder ein scharfes ß (Schweizer Schreibweise ist ss). Die Konvention ae/oe/ue gilt nur fuer den Quellcode, nicht fuer das, was der Nutzer liest:\n"
```

`DesignAuditLaufzeittexteTests.cs`: `Quellen` erweitern

```csharp
    private static readonly string[][] Quellen =
    [
        ["src", "AuswertungPro.Next.Application", "DataPage", "LearningReadinessPresenter.cs"],
        ["src", "AuswertungPro.Next.UI", "DataPage", "SchaechteRecordDetailsBuilder.cs"],
        ["src", "AuswertungPro.Next.UI", "DataPage", "DataPageRecordDetailsBuilder.cs"],
        // Optik-Nachpruefung O5: Untertitel und Statuszeilen der beiden Matrix-Seiten sowie
        // die Meldungen der Kosten-/Massnahmen-Controller sind sichtbare Texte.
        ["src", "AuswertungPro.Next.UI", "ViewModels", "Pages", "SanierungsMatrixPageViewModel.cs"],
        ["src", "AuswertungPro.Next.UI", "ViewModels", "Pages", "SchachtSanierungsMatrixPageViewModel.cs"],
        ["src", "AuswertungPro.Next.UI", "DataPage", "DataPageCostRestoreController.cs"],
        ["src", "AuswertungPro.Next.UI", "DataPage", "DataPageMeasureSuggestionController.cs"],
        ["src", "AuswertungPro.Next.UI", "Views", "Windows", "PhotoMeasurementToolPresentationPolicy.cs"]
    ];
```

`Ersatzschreibweisen` erweitern:

```csharp
    private static readonly string[] Ersatzschreibweisen =
    [
        "Faell", "Schaetz", "aehnlich", "Gruen", "Schaed", "Pruef", "Verknuepf",
        "Loesch", "Oeffn", "Groess", "Naechst", "Ueber", "Zustaend", "Maengel", "Bemuehung",
        "waehl", "oeffn", "Anschluess", "Schaecht"
    ];
```

und die Abfrage in `Sichtbare_Laufzeittexte_tragen_echte_Umlaute` so, dass auch ß gemeldet wird:

```csharp
        var funde = (from teile in Quellen
                     let pfad = RepoFile(teile)
                     from text in Zeichenketten(File.ReadAllText(pfad))
                     let wort = Ersatzschreibweisen.FirstOrDefault(w => text.Contains(w, System.StringComparison.Ordinal))
                                ?? (text.Contains('ß') ? "ß statt ss" : null)
                     where wort is not null
                     select $"{Path.GetFileName(pfad)}: \"{text}\" ({wort})").ToList();
```

- [ ] **Step 2: Beide Wächter laufen lassen — rot, mit der Trefferliste**

Run: `--filter "FullyQualifiedName~DesignAuditFeinschliffTests.Sichtbare_Texte"` und `--filter "FullyQualifiedName~DesignAuditLaufzeittexteTests"`
Expected: rot. Die Trefferliste ist die Arbeitsliste für Step 3 — sie muss die oben genannten Dateien enthalten. Meldet der Laufzeittext-Wächter in den Matrix-ViewModels weitere Texte (z. B. mit „Ueber…"), gehören auch die in Step 3.

- [ ] **Step 3: Texte korrigieren**

Ersetzungen (nur sichtbare Texte, keine Feldschlüssel):

| Datei | alt | neu |
|---|---|---|
| ProjectPage.xaml:76 | `Text="Straße"` | `Text="Strasse"` |
| SanierungsMatrixPage.xaml:33 | `Haltungen mit Maßnahme` | `Haltungen mit Massnahme` |
| SanierungsMatrixPage.xaml:37 | `nach dem Schließen` | `nach dem Schliessen` |
| SanierungsMatrixPage.xaml:86 | `Header="Maßnahmen"` | `Header="Massnahmen"` |
| SanierungsMatrixPage.xaml:326 | `dieser Maßnahme` | `dieser Massnahme` |
| SchachtSanierungsMatrixPage.xaml:27 | `Schächte mit Maßnahme` | `Schächte mit Massnahme` |
| SchachtSanierungsMatrixPage.xaml:52 | `Header="Maßnahme"` | `Header="Massnahme"` |
| BuilderPage.xaml:171 | `Nur mit Maßnahmen` | `Nur mit Massnahmen` |
| BuilderPage.xaml:470 | `Header="Straße"` | `Header="Strasse"` |
| BuilderPage.xaml:478 | `Maßnahmen (Vorschau)` | `Massnahmen (Vorschau)` |
| DataPage.xaml:77, 266 | `Sanierungsmaßnahme…` | `Sanierungsmassnahme…` |
| HaltungsansichtView.xaml:106 | `Sanierungsmaßnahmen...` | `Sanierungsmassnahmen...` |
| SettingsPage.xaml:1206 | `Sanierungsmaßnahmen` | `Sanierungsmassnahmen` |
| CostCatalogEditorDialog.xaml:129, ProtocolHistoryWindow.xaml:47, ProtocolObservationsWindow.xaml:150 | `Schließen` | `Schliessen` |
| PositionTemplateEditorDialog.xaml:21, 39 | `Maßnahmen` | `Massnahmen` |
| DataPageCostRestoreController.cs:66 | `Kosten/Maßnahmen wiederhergestellt` | `Kosten/Massnahmen wiederhergestellt` |
| DataPageMeasureSuggestionController.cs:62-63 | `Maßnahmenvorschlag` | `Massnahmenvorschlag` |
| DataPageViewModel.cs:733 | `Sanierungsmaßnahme geöffnet` | `Sanierungsmassnahme geöffnet` |
| ProtocolEntryEditorDialog.xaml.cs:618 | `größer/gleich` | `grösser/gleich` |
| PhotoMeasurementToolPresentationPolicy.cs:63-65, 68 | `Kreis-Größe`, `schließen` | `Kreis-Grösse`, `schliessen` |
| PhotoMeasurementWindow.Rendering.cs:242 | `schließen` | `schliessen` |
| SanierungsMatrixPageViewModel.cs:102, 433 | `Hauptarbeit waehlen - Meter, DN und Anschluesse` | `Hauptarbeit wählen – Meter, DN und Anschlüsse` |
| SanierungsMatrixPageViewModel.cs:331 | `Haltungen oeffnen` | `Haltungen öffnen` |
| SchachtSanierungsMatrixPageViewModel.cs:198 | `Keine Schaechte geladen (… oeffnen)` | `Keine Schächte geladen (… öffnen)` |
| SchachtSanierungsMatrixPageViewModel.cs:199 | `Schaechte geladen.` | `Schächte geladen.` |

Entwicklertexte:

- `SchaechtePageViewModel.cs:486`: `LastResult = $"Spalten geladen: {Columns.Count}";` → `LastResult = "";` (die Zeile davor ist eine Diagnosemeldung ohne Nutzen für den Nutzer; Fehlerfälle setzen `LastResult` weiterhin).
- `ProjectPage.xaml:88`: `StringFormat=Records: {0}` → `StringFormat=Haltungen: {0}`.
- `BuilderPageFilterSummaryBuilder.cs:40`: `parts.Add($"Treffer={filteredRowsCount}/{totalRows}");` → `parts.Add($"Treffer: {filteredRowsCount} von {totalRows}");`
- `DataPage.xaml:144`: `Text="{Binding LearningTrafficLightText}"` → `Text="{Binding LearningTrafficLightText, StringFormat='KI-Lernampel {0}'}"` (aus „● Rot" wird „● KI-Lernampel Rot").

- [ ] **Step 4: Tests nachziehen, die die alten Texte festhalten**

- `DesignAuditThemeResourceTests.cs:76`: `Assert.Contains("Header=\"Maßnahmen\"", xaml);` → `Assert.Contains("Header=\"Massnahmen\"", xaml);`
- `DataPageMeasureSuggestionControllerTests.cs`: jedes `Maßnahmenvorschlag` → `Massnahmenvorschlag`.
- `DataPageCostRestoreControllerTests.cs`: `Kosten/Maßnahmen wiederhergestellt` → `Kosten/Massnahmen wiederhergestellt`.
- `BuilderPageRowBuilderTests.cs`: jede Erwartung der Form `Treffer=<a>/<b>` → `Treffer: <a> von <b>` (mit `grep -n "Treffer=" tests/AuswertungPro.Next.UI.Tests/BuilderPageRowBuilderTests.cs` alle Stellen finden).

- [ ] **Step 5: Bauen und Wächter grün**

Run: Build; dann `--filter "FullyQualifiedName~DesignAudit"` (alle Gestaltungswächter) und `--filter "FullyQualifiedName~BuilderPageRowBuilderTests|FullyQualifiedName~DataPageMeasureSuggestionControllerTests|FullyQualifiedName~DataPageCostRestoreControllerTests"`.
Expected: alles grün, insbesondere `Sichtbare_Texte_verwenden_echte_Umlaute` und `Sichtbare_Laufzeittexte_tragen_echte_Umlaute` ohne Treffer.

- [ ] **Step 6: Commit**

```bash
git add -u src/AuswertungPro.Next.UI/ tests/AuswertungPro.Next.UI.Tests/DesignAuditFeinschliffTests.cs tests/AuswertungPro.Next.UI.Tests/DesignAuditLaufzeittexteTests.cs tests/AuswertungPro.Next.UI.Tests/DesignAuditThemeResourceTests.cs tests/AuswertungPro.Next.UI.Tests/DataPageMeasureSuggestionControllerTests.cs tests/AuswertungPro.Next.UI.Tests/DataPageCostRestoreControllerTests.cs tests/AuswertungPro.Next.UI.Tests/BuilderPageRowBuilderTests.cs
git status --short   # muss NUR die in dieser Aufgabe genannten Dateien zeigen
git commit -m "Optik O4/O5/O6: Schweizer ss, echte Umlaute in Laufzeittexten, keine Entwicklertexte

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

(`git add -u` nimmt nur geänderte, bereits versionierte Dateien unter `src/AuswertungPro.Next.UI/` — im eigenen Worktree gibt es dort keine fremden Änderungen. `git status` vor dem Commit trotzdem lesen.)

---

### Task 6: O7 — Platzhalter in Suchfeldern, leere Statuszeilen ausblenden

**Files:**
- Modify: `src/AuswertungPro.Next.UI/Views/Pages/Haltungsansicht/HaltungFelderDrawer.xaml:65-67`
- Modify: `src/AuswertungPro.Next.UI/Views/Pages/SettingsPage.xaml:279-283`
- Modify: `src/AuswertungPro.Next.UI/Views/Pages/ExportPage.xaml:481-483`
- Modify: `src/AuswertungPro.Next.UI/Views/Pages/DossiersPage.xaml` (Fusszeile, `<!-- Fusszeile -->`)
- Test: `tests/AuswertungPro.Next.UI.Tests/DesignAuditOptikNachpruefungTests.cs`

**Interfaces:**
- Consumes: das Platzhalter-Muster aus `DataPage.xaml:175-195` (`TextBlock` über dem `TextBox`, sichtbar nur bei leerem `Text` über `DataTrigger ElementName`). Der Schacht-Drawer verwendet dasselbe Control `HaltungFelderDrawer` — eine Änderung reicht.

- [ ] **Step 1: Wächter schreiben (rot)**

In `DesignAuditOptikNachpruefungTests.cs` einfügen:

```csharp
    /// <summary>O7: Leere Suchfelder tragen einen Platzhalter; leere Statuszeilen verschwinden.</summary>
    [Fact]
    public void Suchfelder_tragen_Platzhalter_und_leere_Statuszeilen_verschwinden()
    {
        var drawer = Ui("Views", "Pages", "Haltungsansicht", "HaltungFelderDrawer.xaml");
        Assert.Contains("Text=\"Feld suchen\"", drawer);
        Assert.Contains("Binding=\"{Binding Text, ElementName=FeldSuche}\" Value=\"\"", drawer);

        var settings = Ui("Views", "Pages", "SettingsPage.xaml");
        Assert.Contains("Text=\"Einstellung suchen\"", settings);
        Assert.Contains("Binding=\"{Binding Text, ElementName=SucheBox}\" Value=\"\"", settings);

        var export = Ui("Views", "Pages", "ExportPage.xaml");
        Assert.Contains("<DataTrigger Binding=\"{Binding LastResult}\" Value=\"\">", export);

        var dossiers = Ui("Views", "Pages", "DossiersPage.xaml");
        Assert.Contains("<Condition Binding=\"{Binding StatusMessage}\" Value=\"\"/>", dossiers);
        Assert.Contains("<Condition Binding=\"{Binding IsBusy}\" Value=\"False\"/>", dossiers);
    }
```

- [ ] **Step 2: Laufen lassen — rot**

Run: `--filter "FullyQualifiedName~DesignAuditOptikNachpruefungTests.Suchfelder"`
Expected: rot.

- [ ] **Step 3: Feldsuche im Drawer**

`HaltungFelderDrawer.xaml`, den `TextBox`

```xml
                <TextBox DockPanel.Dock="Right" x:Name="FeldSuche" Width="220" Margin="0,0,8,0"
                         TextChanged="FeldSuche_TextChanged"
                         ToolTip="Feld suchen, zum Beispiel Baujahr" AutomationProperties.Name="Feld suchen"/>
```

ersetzen durch

```xml
                <!-- Optik-Nachpruefung O7: Platzhalter wie in der Suchpille der Haltungsseite —
                     ein leerer Kasten ohne Hinweis war nicht als Suche erkennbar. -->
                <Grid DockPanel.Dock="Right" Width="220" Margin="0,0,8,0">
                    <TextBox x:Name="FeldSuche"
                             TextChanged="FeldSuche_TextChanged"
                             ToolTip="Feld suchen, zum Beispiel Baujahr" AutomationProperties.Name="Feld suchen"/>
                    <TextBlock Text="Feld suchen" IsHitTestVisible="False"
                               VerticalAlignment="Center" Margin="8,0,0,0"
                               FontSize="{DynamicResource TextS}" Foreground="{DynamicResource MutedBrush}">
                        <TextBlock.Style>
                            <Style TargetType="TextBlock" BasedOn="{StaticResource {x:Type TextBlock}}">
                                <Setter Property="Visibility" Value="Collapsed"/>
                                <Style.Triggers>
                                    <DataTrigger Binding="{Binding Text, ElementName=FeldSuche}" Value="">
                                        <Setter Property="Visibility" Value="Visible"/>
                                    </DataTrigger>
                                </Style.Triggers>
                            </Style>
                        </TextBlock.Style>
                    </TextBlock>
                </Grid>
```

- [ ] **Step 4: Suche in den Einstellungen**

`SettingsPage.xaml`, den `TextBox x:Name="SucheBox"` (Zeilen 279-283)

```xml
                        <TextBox x:Name="SucheBox"
                                 Width="260"
                                 TextChanged="SucheBox_TextChanged"
                                 AutomationProperties.Name="Einstellung suchen"
                                 ToolTip="Einstellung suchen — zeigt nur passende Gruppen und springt zum ersten Reiter mit Treffer."/>
```

ersetzen durch

```xml
                        <Grid Width="260">
                            <TextBox x:Name="SucheBox"
                                     TextChanged="SucheBox_TextChanged"
                                     AutomationProperties.Name="Einstellung suchen"
                                     ToolTip="Einstellung suchen — zeigt nur passende Gruppen und springt zum ersten Reiter mit Treffer."/>
                            <!-- Optik-Nachpruefung O7: Platzhalter, sichtbar nur bei leerem Feld. -->
                            <TextBlock Text="Einstellung suchen" IsHitTestVisible="False"
                                       VerticalAlignment="Center" Margin="8,0,0,0"
                                       FontSize="{DynamicResource TextS}" Foreground="{DynamicResource MutedBrush}">
                                <TextBlock.Style>
                                    <Style TargetType="TextBlock" BasedOn="{StaticResource {x:Type TextBlock}}">
                                        <Setter Property="Visibility" Value="Collapsed"/>
                                        <Style.Triggers>
                                            <DataTrigger Binding="{Binding Text, ElementName=SucheBox}" Value="">
                                                <Setter Property="Visibility" Value="Visible"/>
                                            </DataTrigger>
                                        </Style.Triggers>
                                    </Style>
                                </TextBlock.Style>
                            </TextBlock>
                        </Grid>
```

- [ ] **Step 5: Leere Statuszeile auf Export**

`ExportPage.xaml`, den `TextBox` unter `<!-- Ergebnis der letzten Verteilung -->`

```xml
            <TextBox Grid.Row="1" Text="{Binding LastResult}" AcceptsReturn="True" TextWrapping="Wrap"
                     IsReadOnly="True" VerticalScrollBarVisibility="Auto" MaxHeight="140" Margin="0,8,0,0"
                     Foreground="{DynamicResource MutedBrush}"/>
```

ersetzen durch

```xml
            <TextBox Grid.Row="1" Text="{Binding LastResult}" AcceptsReturn="True" TextWrapping="Wrap"
                     IsReadOnly="True" VerticalScrollBarVisibility="Auto" MaxHeight="140" Margin="0,8,0,0"
                     Foreground="{DynamicResource MutedBrush}">
                <!-- Optik-Nachpruefung O7: ohne Ergebnis kein leerer umrandeter Kasten. -->
                <TextBox.Style>
                    <Style TargetType="TextBox" BasedOn="{StaticResource {x:Type TextBox}}">
                        <Style.Triggers>
                            <DataTrigger Binding="{Binding LastResult}" Value="">
                                <Setter Property="Visibility" Value="Collapsed"/>
                            </DataTrigger>
                            <DataTrigger Binding="{Binding LastResult}" Value="{x:Null}">
                                <Setter Property="Visibility" Value="Collapsed"/>
                            </DataTrigger>
                        </Style.Triggers>
                    </Style>
                </TextBox.Style>
            </TextBox>
```

- [ ] **Step 6: Leere Fusszeile auf Dossiers**

`DossiersPage.xaml`, die Fusszeile

```xml
        <Border Grid.Row="2" Margin="0,8,0,0" Padding="12,8"
                Background="{DynamicResource HeaderBrush}"
                BorderBrush="{DynamicResource BorderBrush}"
                BorderThickness="1" CornerRadius="{DynamicResource RadiusXL}">
            <Grid>
```

ersetzen durch

```xml
        <Border Grid.Row="2" Margin="0,8,0,0" Padding="12,8"
                Background="{DynamicResource HeaderBrush}"
                BorderBrush="{DynamicResource BorderBrush}"
                BorderThickness="1" CornerRadius="{DynamicResource RadiusXL}">
            <!-- Optik-Nachpruefung O7: Fusszeile nur mit Meldung oder waehrend der Arbeit. -->
            <Border.Style>
                <Style TargetType="Border">
                    <Style.Triggers>
                        <MultiDataTrigger>
                            <MultiDataTrigger.Conditions>
                                <Condition Binding="{Binding StatusMessage}" Value=""/>
                                <Condition Binding="{Binding IsBusy}" Value="False"/>
                            </MultiDataTrigger.Conditions>
                            <Setter Property="Visibility" Value="Collapsed"/>
                        </MultiDataTrigger>
                        <MultiDataTrigger>
                            <MultiDataTrigger.Conditions>
                                <Condition Binding="{Binding StatusMessage}" Value="{x:Null}"/>
                                <Condition Binding="{Binding IsBusy}" Value="False"/>
                            </MultiDataTrigger.Conditions>
                            <Setter Property="Visibility" Value="Collapsed"/>
                        </MultiDataTrigger>
                    </Style.Triggers>
                </Style>
            </Border.Style>
            <Grid>
```

- [ ] **Step 7: Bauen und Wächter grün**

Run: Build; `--filter "FullyQualifiedName~DesignAuditOptikNachpruefungTests"`, `--filter "FullyQualifiedName~DesignAuditFeinschliffTests"`, `--filter "FullyQualifiedName~DesignAuditAccessibilityTests"` (jedes Feld behält `AutomationProperties.Name` + `ToolTip`), `--filter "FullyQualifiedName~HaltungFelderDrawer"`, `--filter "FullyQualifiedName~SettingsSearch"`.
Expected: grün.

- [ ] **Step 8: Commit**

```bash
git add src/AuswertungPro.Next.UI/Views/Pages/Haltungsansicht/HaltungFelderDrawer.xaml src/AuswertungPro.Next.UI/Views/Pages/SettingsPage.xaml src/AuswertungPro.Next.UI/Views/Pages/ExportPage.xaml src/AuswertungPro.Next.UI/Views/Pages/DossiersPage.xaml tests/AuswertungPro.Next.UI.Tests/DesignAuditOptikNachpruefungTests.cs
git commit -m "Optik O7: Platzhalter in Feld- und Einstellungssuche, leere Statuszeilen ausgeblendet

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 7: O10 — Das Ergebnis-Symbol der VSA-Seite folgt dem Zustand

**Files:**
- Modify: `src/AuswertungPro.Next.UI/ViewModels/Pages/VsaPageViewModel.cs:27-30` (Eigenschaft), `:83` (Start), `:211-216` (Erfolg)
- Modify: `src/AuswertungPro.Next.UI/Views/Pages/VsaPage.xaml:70-71`
- Test: `tests/AuswertungPro.Next.UI.Tests/DesignAuditOptikNachpruefungTests.cs`

**Interfaces:**
- Produces: `VsaPageViewModel.HatErgebnis : bool` (ObservableProperty; `false` beim Start eines Laufs, `true` nur nach dem Erfolgsende; Fehlerpfade lassen es `false`).

- [ ] **Step 1: Wächter schreiben (rot)**

```csharp
    /// <summary>O10: Kein gruener Haken vor „Noch keine Berechnung" — das Symbol folgt HatErgebnis.</summary>
    [Fact]
    public void VSA_Ergebnis_Symbol_folgt_dem_Zustand()
    {
        var xaml = Ui("Views", "Pages", "VsaPage.xaml");
        Assert.Contains("<DataTrigger Binding=\"{Binding HatErgebnis}\" Value=\"True\">", xaml);
        Assert.DoesNotContain("Text=\"&#xE73E;\" FontFamily=\"{DynamicResource FontIcon}\"", xaml);

        var vm = Ui("ViewModels", "Pages", "VsaPageViewModel.cs");
        Assert.Contains("private bool _hatErgebnis;", vm);
        Assert.Contains("HatErgebnis = true;", vm);
        Assert.Contains("HatErgebnis = false;", vm);
    }
```

- [ ] **Step 2: Laufen lassen — rot**

Run: `--filter "FullyQualifiedName~DesignAuditOptikNachpruefungTests.VSA"` → rot.

- [ ] **Step 3: ViewModel**

`VsaPageViewModel.cs`: hinter `[ObservableProperty] private bool _isBusy;` einfügen

```csharp
    /// <summary>Wahr nur nach einem erfolgreich beendeten Lauf; steuert das Symbol der Ergebniskarte (O10).</summary>
    [ObservableProperty] private bool _hatErgebnis;
```

In `RunAsync()` direkt hinter `IsBusy = true;`:

```csharp
        HatErgebnis = false;
```

Am Erfolgsende, direkt vor `_setStatus("VSA berechnet");`:

```csharp
        HatErgebnis = true;
```

- [ ] **Step 4: XAML**

`VsaPage.xaml`, die Zeilen

```xml
                        <TextBlock Text="&#xE73E;" FontFamily="{DynamicResource FontIcon}" FontSize="{DynamicResource TextL}"
                                   Foreground="{DynamicResource SuccessBrush}" VerticalAlignment="Center" Margin="0,0,8,0"/>
```

ersetzen durch

```xml
                        <!-- Optik-Nachpruefung O10: Info-Symbol ohne Ergebnis, Haken erst nach dem Lauf. -->
                        <TextBlock FontFamily="{DynamicResource FontIcon}" FontSize="{DynamicResource TextL}"
                                   VerticalAlignment="Center" Margin="0,0,8,0">
                            <TextBlock.Style>
                                <Style TargetType="TextBlock" BasedOn="{StaticResource {x:Type TextBlock}}">
                                    <Setter Property="Text" Value="&#xE946;"/>
                                    <Setter Property="Foreground" Value="{DynamicResource MutedBrush}"/>
                                    <Style.Triggers>
                                        <DataTrigger Binding="{Binding HatErgebnis}" Value="True">
                                            <Setter Property="Text" Value="&#xE73E;"/>
                                            <Setter Property="Foreground" Value="{DynamicResource SuccessBrush}"/>
                                        </DataTrigger>
                                    </Style.Triggers>
                                </Style>
                            </TextBlock.Style>
                        </TextBlock>
```

- [ ] **Step 5: Bauen und grün**

Run: Build; `--filter "FullyQualifiedName~DesignAuditOptikNachpruefungTests"`, `--filter "FullyQualifiedName~VsaPage"`.
Expected: grün.

- [ ] **Step 6: Commit**

```bash
git add src/AuswertungPro.Next.UI/ViewModels/Pages/VsaPageViewModel.cs src/AuswertungPro.Next.UI/Views/Pages/VsaPage.xaml tests/AuswertungPro.Next.UI.Tests/DesignAuditOptikNachpruefungTests.cs
git commit -m "Optik O10: VSA-Ergebnissymbol zeigt erst nach dem Lauf einen Haken

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 8: O11/O12 — Ein Hauptknopf je Seite, Projektseite und Import aufräumen

**Files:**
- Modify: `src/AuswertungPro.Next.UI/Views/Pages/SanierungsMatrixPage.xaml:40,289`
- Modify: `src/AuswertungPro.Next.UI/Views/Pages/SchachtSanierungsMatrixPage.xaml:32`
- Modify: `src/AuswertungPro.Next.UI/Views/Pages/SettingsPage.xaml:292`
- Modify: `src/AuswertungPro.Next.UI/Views/Pages/BuilderPage.xaml:608`
- Modify: `src/AuswertungPro.Next.UI/Views/Pages/ProjectPage.xaml:31-35`, `ProjectPage.xaml.cs:24-28`
- Modify: `src/AuswertungPro.Next.UI/Views/Pages/ImportPage.xaml:43,45,172-175`
- Test: `tests/AuswertungPro.Next.UI.Tests/DesignAuditOptikNachpruefungTests.cs`

**Interfaces:**
- Consumes: Stil `ToolbarButtonAccent` (Theme, die blaue Pille der Nova-Werkzeugleisten; ein reiner String als `Content` ist erlaubt, die Vorlage reicht die weisse Tinte selbst durch), Menüpunkt `Beenden` in `MainWindow.xaml:155` (bleibt der einzige Weg zum Beenden).

- [ ] **Step 1: Wächter schreiben (rot)**

```csharp
    /// <summary>
    /// O11/O12: Der Hauptknopf einer Seite ist die blaue Pille (ToolbarButtonAccent), nicht das
    /// blaue Rechteck (PrimaryButton). Import hat genau einen Hauptknopf; die Projektseite
    /// beendet das Programm nicht neben „Speichern" (dafuer gibt es Datei → Beenden).
    /// </summary>
    [Fact]
    public void Seiten_haben_eine_Hauptaktion_als_Pille_und_keinen_PrimaryButton_mehr()
    {
        var seiten = Directory.EnumerateFiles(
                RepoFile("src", "AuswertungPro.Next.UI", "Views", "Pages"), "*.xaml", SearchOption.TopDirectoryOnly)
            .Where(p => Path.GetFileName(p) != "OverviewPage.xaml"); // klassische Uebersicht bleibt unveraendert
        foreach (var seite in seiten)
            Assert.False(File.ReadAllText(seite).Contains("PrimaryButton", StringComparison.Ordinal),
                $"{Path.GetFileName(seite)} verwendet noch PrimaryButton");

        var import = Ui("Views", "Pages", "ImportPage.xaml");
        Assert.Equal(1, Regex.Matches(import, "ToolbarButtonAccent").Count);
        Assert.DoesNotContain("Text=\"Manuell:\"", import);
        Assert.Contains("Text=\"{Binding CatalogStatus}\"", import);
        Assert.Contains("ToolTip=\"{Binding CatalogStatus}\"", import);

        var projekt = Ui("Views", "Pages", "ProjectPage.xaml");
        Assert.DoesNotContain("Programm schliessen", projekt);
        Assert.DoesNotContain("Programm schließen", projekt);
        Assert.Contains("Content=\"Projekt speichern\" Command=\"{Binding SaveCommand}\" Style=\"{StaticResource ToolbarButtonAccent}\"", projekt);
        Assert.DoesNotContain("CloseButton_Click", Ui("Views", "Pages", "ProjectPage.xaml.cs"));
    }
```

- [ ] **Step 2: Laufen lassen — rot**

Run: `--filter "FullyQualifiedName~DesignAuditOptikNachpruefungTests.Seiten_haben"` → rot.

- [ ] **Step 3: PrimaryButton → ToolbarButtonAccent**

- `SanierungsMatrixPage.xaml:40` und `:289`, `SchachtSanierungsMatrixPage.xaml:32`, `SettingsPage.xaml:292`: `Style="{StaticResource PrimaryButton}"` → `Style="{StaticResource ToolbarButtonAccent}"`.
- `BuilderPage.xaml:608`: `<Style TargetType="Button" BasedOn="{StaticResource PrimaryButton}">` → `<Style TargetType="Button" BasedOn="{StaticResource ToolbarButtonAccent}">`.

- [ ] **Step 4: Projektseite**

`ProjectPage.xaml`, die drei Knöpfe

```xml
                <Button Content="Projekt speichern" Command="{Binding SaveCommand}" Style="{StaticResource SecondaryButton}" Width="200" MinWidth="180" Margin="8,0,0,0"
                        Visibility="{Binding IsNotDraft, Converter={StaticResource BoolToVis}}"
                        ToolTip="Alle Projektdaten speichern." />
                <Button Content="Programm schließen" Click="CloseButton_Click" Style="{StaticResource SecondaryButton}" Width="200" MinWidth="180" Margin="8,0,0,0"
                        ToolTip="SewerStudio beenden. Ungespeicherte Änderungen werden vorher abgefragt." />
```

ersetzen durch

```xml
                <!-- Optik-Nachpruefung O11: eine Hauptaktion. Beenden gehoert ins Menue Datei,
                     nicht neben Speichern. -->
                <Button Content="Projekt speichern" Command="{Binding SaveCommand}" Style="{StaticResource ToolbarButtonAccent}" Margin="8,0,0,0"
                        Visibility="{Binding IsNotDraft, Converter={StaticResource BoolToVis}}"
                        ToolTip="Alle Projektdaten speichern." />
```

`ProjectPage.xaml.cs`: die Methode `CloseButton_Click` (Zeilen 24-28) ersatzlos entfernen. Wird `using System.Windows;` danach nur noch für `RoutedEventArgs` gebraucht und ist sonst unbenutzt, ebenfalls entfernen (der Build meldet es nicht; `dotnet build` mit `-warnaserror` ist nicht aktiv — Entfernen ist optional).

- [ ] **Step 5: Import**

`ImportPage.xaml`:
- Zeile 43: `<TextBlock Text="Manuell:" VerticalAlignment="Center" Margin="0,0,6,0" Opacity="0.7" FontSize="{DynamicResource TextXS}"/>` entfernen.
- Zeile 45 (Knopf „Import PDF"): `Style="{StaticResource ToolbarButtonAccent}"` → `Style="{StaticResource ToolbarButton}"`.
- Zeilen 172-175 (Katalogzeile):

```xml
                <TextBlock Text="{Binding CatalogStatus}"
                           VerticalAlignment="Center"
                           Foreground="{DynamicResource TextSecondaryBrush}"
                           TextWrapping="Wrap"/>
```

→

```xml
                <!-- Optik-Nachpruefung O12: zwei volle Windows-Pfade in einer Zeile sind Laerm —
                     gekuerzt anzeigen, vollstaendig im Hinweis. -->
                <TextBlock Text="{Binding CatalogStatus}"
                           ToolTip="{Binding CatalogStatus}"
                           VerticalAlignment="Center"
                           Foreground="{DynamicResource TextSecondaryBrush}"
                           TextTrimming="CharacterEllipsis"/>
```

- [ ] **Step 6: Bauen und grün**

Run: Build; `--filter "FullyQualifiedName~DesignAuditOptikNachpruefungTests"`, `--filter "FullyQualifiedName~DesignAuditCommandReachabilityTests"`, `--filter "FullyQualifiedName~DesignAuditNovaSeitenkoepfeTests"`, `--filter "FullyQualifiedName~DesignAuditThemeResourceTests"`, `--filter "FullyQualifiedName~ImportPage|FullyQualifiedName~ProjectPage"`.
Expected: grün. Meldet `DesignAuditCommandReachabilityTests` den entfernten `CloseButton_Click`, dessen Erwartung auf `Beenden` in `MainWindow.xaml` umstellen — nicht den Knopf zurückholen.

- [ ] **Step 7: Commit**

```bash
git add src/AuswertungPro.Next.UI/Views/Pages/SanierungsMatrixPage.xaml src/AuswertungPro.Next.UI/Views/Pages/SchachtSanierungsMatrixPage.xaml src/AuswertungPro.Next.UI/Views/Pages/SettingsPage.xaml src/AuswertungPro.Next.UI/Views/Pages/BuilderPage.xaml src/AuswertungPro.Next.UI/Views/Pages/ProjectPage.xaml src/AuswertungPro.Next.UI/Views/Pages/ProjectPage.xaml.cs src/AuswertungPro.Next.UI/Views/Pages/ImportPage.xaml tests/AuswertungPro.Next.UI.Tests/DesignAuditOptikNachpruefungTests.cs
git commit -m "Optik O11/O12: eine Hauptaktion je Seite, Projektseite und Import-Leiste aufgeraeumt

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 9: O19 — Einstellungsgruppen als Karten statt GroupBox-Doppelrahmen

**Files:**
- Modify: `src/AuswertungPro.Next.UI/Views/Pages/SettingsPage.xaml:1-15` (Namensraum `sys`, Stil `SettingsSectionGroupBox`)
- Test: `tests/AuswertungPro.Next.UI.Tests/DesignAuditOptikNachpruefungTests.cs`

**Interfaces:**
- Consumes: Tokens `CardBrush`, `BorderBrush`, `RadiusL`, `TextL`, `TextBrush`.

Hintergrund: Die Gruppen laufen über die Theme-`GroupBox`-Vorlage (Kopfleiste mit Verlauf, zweiteiliger Rahmen). Im Dunkeln ein breiter heller Doppelrahmen (`Dark-Einstellungen.png`). Der seitenlokale Stil `SettingsSectionGroupBox` bekommt eine eigene Kartenvorlage; die Theme-Vorlage bleibt für andere Fenster unverändert.

- [ ] **Step 1: Wächter (rot)**

```csharp
    /// <summary>O19: Einstellungsgruppen sind Karten, nicht die zweiteilige GroupBox-Vorlage.</summary>
    [Fact]
    public void Einstellungsgruppen_verwenden_eine_Kartenvorlage()
    {
        var xaml = Ui("Views", "Pages", "SettingsPage.xaml");
        var stilStart = xaml.IndexOf("x:Key=\"SettingsSectionGroupBox\"", StringComparison.Ordinal);
        var stil = xaml[stilStart..xaml.IndexOf("</Style>", stilStart, StringComparison.Ordinal)];
        Assert.Contains("<ControlTemplate TargetType=\"GroupBox\">", stil);
        Assert.Contains("CornerRadius=\"{DynamicResource RadiusL}\"", stil);
        Assert.Contains("DataType=\"{x:Type sys:String}\"", stil);
    }
```

- [ ] **Step 2: Laufen lassen — rot**

Run: `--filter "FullyQualifiedName~DesignAuditOptikNachpruefungTests.Einstellungsgruppen"` → rot.

- [ ] **Step 3: Namensraum und Vorlage**

`SettingsPage.xaml`, im Wurzelelement hinter den bestehenden `xmlns:`-Zeilen ergänzen (die Datei führt bisher kein `sys`):

```xml
             xmlns:sys="clr-namespace:System;assembly=mscorlib"
```

Den Stil

```xml
        <Style x:Key="SettingsSectionGroupBox" TargetType="{x:Type GroupBox}">
            <Setter Property="Margin" Value="0,0,0,12"/>
            <Setter Property="Padding" Value="12,10"/>
            <Setter Property="Foreground" Value="{DynamicResource TextBrush}"/>
            <Setter Property="BorderBrush" Value="{DynamicResource BorderBrush}"/>
            <Setter Property="Background" Value="{DynamicResource CardBrush}"/>
        </Style>
```

ersetzen durch

```xml
        <!-- Optik-Nachpruefung O19: Karte mit Titelzeile statt der zweiteiligen Theme-GroupBox
             (breiter Doppelrahmen im Dunkelmodus). Der Kopf ist ein String; ein eigener
             TextBlock setzt Groesse und Tinte selbst (B7: der implizite TextBlock-Stil
             schlaegt TextElement.*). -->
        <Style x:Key="SettingsSectionGroupBox" TargetType="{x:Type GroupBox}">
            <Setter Property="Margin" Value="0,0,0,12"/>
            <Setter Property="Padding" Value="14,12"/>
            <Setter Property="Foreground" Value="{DynamicResource TextBrush}"/>
            <Setter Property="BorderBrush" Value="{DynamicResource BorderBrush}"/>
            <Setter Property="Background" Value="{DynamicResource CardBrush}"/>
            <Setter Property="Template">
                <Setter.Value>
                    <ControlTemplate TargetType="GroupBox">
                        <Border Background="{TemplateBinding Background}"
                                BorderBrush="{TemplateBinding BorderBrush}"
                                BorderThickness="1"
                                CornerRadius="{DynamicResource RadiusL}"
                                Padding="{TemplateBinding Padding}">
                            <DockPanel>
                                <ContentPresenter DockPanel.Dock="Top" ContentSource="Header" Margin="0,0,0,10">
                                    <ContentPresenter.Resources>
                                        <DataTemplate DataType="{x:Type sys:String}">
                                            <TextBlock Text="{Binding}" FontWeight="SemiBold"
                                                       FontSize="{DynamicResource TextL}"
                                                       Foreground="{DynamicResource TextBrush}"/>
                                        </DataTemplate>
                                    </ContentPresenter.Resources>
                                </ContentPresenter>
                                <ContentPresenter/>
                            </DockPanel>
                        </Border>
                    </ControlTemplate>
                </Setter.Value>
            </Setter>
        </Style>
```

- [ ] **Step 4: Bauen und grün**

Run: Build; `--filter "FullyQualifiedName~DesignAuditOptikNachpruefungTests"`, `--filter "FullyQualifiedName~SettingsPage"`, `--filter "FullyQualifiedName~SettingsSearch"` (der Suchcontroller liest Gruppenüberschriften — er muss die Gruppen weiterhin finden).
Expected: grün.

- [ ] **Step 5: Commit**

```bash
git add src/AuswertungPro.Next.UI/Views/Pages/SettingsPage.xaml tests/AuswertungPro.Next.UI.Tests/DesignAuditOptikNachpruefungTests.cs
git commit -m "Optik O19: Einstellungsgruppen als Karten statt GroupBox-Doppelrahmen

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 10: O3 — Alle fünf Schadensstufen im Training Studio sichtbar

**Files:**
- Modify: `src/AuswertungPro.Next.UI/Views/Windows/TrainingStudioWindow.xaml:506-521`
- Test: `tests/AuswertungPro.Next.UI.Tests/DesignAuditOptikNachpruefungTests.cs`

**Interfaces:** keine.

Hintergrund: Die fünf Knöpfe stehen in einem horizontalen `StackPanel` mit `Width="40"`, werden aber breiter gezeichnet und laufen rechts aus der 330-px-Spalte — sichtbar sind 1, 2, 3 (`Light-TrainingStudio-1920x1080.png`; R3 im Morgenbericht). Ein `UniformGrid` teilt die Breite, egal wie breit die Spalte ist.

- [ ] **Step 1: Wächter (rot)**

```csharp
    /// <summary>O3 / R3: Die fuenf Stufenknoepfe teilen sich die Breite, statt rechts auszulaufen.</summary>
    [Fact]
    public void Schadensstufen_1_bis_5_teilen_sich_die_Breite_in_einem_UniformGrid()
    {
        var xaml = Ui("Views", "Windows", "TrainingStudioWindow.xaml");
        var start = xaml.IndexOf("Text=\"Schadensstufe (optional)\"", StringComparison.Ordinal);
        Assert.True(start >= 0);
        var ausschnitt = xaml[start..xaml.IndexOf("Beschreibung (mind. 10 Zeichen)", start, StringComparison.Ordinal)];
        Assert.Contains("<UniformGrid Columns=\"5\"", ausschnitt);
        Assert.DoesNotContain("Width=\"40\"", ausschnitt);
        Assert.Equal(5, Regex.Matches(ausschnitt, "CommandParameter=\"[1-5]\"").Count);
    }
```

- [ ] **Step 2: Laufen lassen — rot**

Run: `--filter "FullyQualifiedName~DesignAuditOptikNachpruefungTests.Schadensstufen"` → rot.

- [ ] **Step 3: StackPanel → UniformGrid**

`TrainingStudioWindow.xaml`, den Block

```xml
                                <StackPanel Orientation="Horizontal" Margin="0,2,0,8">
                                    <Button Content="1" Width="40" MinWidth="40" Margin="0,0,4,0" Background="{DynamicResource Severity1Brush}"
                                            Foreground="White" FontWeight="Bold"
                                            Command="{Binding SetSeverityCommand}" CommandParameter="1"/>
                                    <Button Content="2" Width="40" MinWidth="40" Margin="0,0,4,0" Background="{DynamicResource Severity2Brush}"
                                            Foreground="White" FontWeight="Bold"
                                            Command="{Binding SetSeverityCommand}" CommandParameter="2"/>
                                    <Button Content="3" Width="40" MinWidth="40" Margin="0,0,4,0" Background="{DynamicResource Severity3Brush}"
                                            Foreground="White" FontWeight="Bold"
                                            Command="{Binding SetSeverityCommand}" CommandParameter="3"/>
                                    <Button Content="4" Width="40" MinWidth="40" Margin="0,0,4,0" Background="{DynamicResource Severity4Brush}"
                                            Foreground="White" FontWeight="Bold"
                                            Command="{Binding SetSeverityCommand}" CommandParameter="4"/>
                                    <Button Content="5" Width="40" MinWidth="40" Background="{DynamicResource Severity5Brush}"
                                            Foreground="White" FontWeight="Bold"
                                            Command="{Binding SetSeverityCommand}" CommandParameter="5"/>
                                </StackPanel>
```

ersetzen durch

```xml
                                <!-- Optik-Nachpruefung O3 (R3): fuenf gleich breite Knoepfe ueber die ganze
                                     Spaltenbreite — als StackPanel mit fester Breite liefen 4 und 5 rechts
                                     aus der 330-px-Spalte. -->
                                <UniformGrid Columns="5" Margin="0,2,0,8">
                                    <Button Content="1" Margin="0,0,4,0" Background="{DynamicResource Severity1Brush}"
                                            Foreground="White" FontWeight="Bold"
                                            Command="{Binding SetSeverityCommand}" CommandParameter="1"/>
                                    <Button Content="2" Margin="0,0,4,0" Background="{DynamicResource Severity2Brush}"
                                            Foreground="White" FontWeight="Bold"
                                            Command="{Binding SetSeverityCommand}" CommandParameter="2"/>
                                    <Button Content="3" Margin="0,0,4,0" Background="{DynamicResource Severity3Brush}"
                                            Foreground="White" FontWeight="Bold"
                                            Command="{Binding SetSeverityCommand}" CommandParameter="3"/>
                                    <Button Content="4" Margin="0,0,4,0" Background="{DynamicResource Severity4Brush}"
                                            Foreground="White" FontWeight="Bold"
                                            Command="{Binding SetSeverityCommand}" CommandParameter="4"/>
                                    <Button Content="5" Background="{DynamicResource Severity5Brush}"
                                            Foreground="White" FontWeight="Bold"
                                            Command="{Binding SetSeverityCommand}" CommandParameter="5"/>
                                </UniformGrid>
```

(`Foreground="White"` ist ein benannter Wert, kein `#RRGGBB` — der Farbwächter lässt ihn durch; die Knöpfe sind farbige Flächen mit weisser Ziffer in beiden Themen.)

- [ ] **Step 4: Bauen und grün**

Run: Build; `--filter "FullyQualifiedName~DesignAuditOptikNachpruefungTests"`, `--filter "FullyQualifiedName~DesignAuditNovaTrainingStudioTests"`, `--filter "FullyQualifiedName~TrainingStudio"`.
Expected: grün.

- [ ] **Step 5: Commit**

```bash
git add src/AuswertungPro.Next.UI/Views/Windows/TrainingStudioWindow.xaml tests/AuswertungPro.Next.UI.Tests/DesignAuditOptikNachpruefungTests.cs
git commit -m "Optik O3: Schadensstufen 1 bis 5 im Training Studio als UniformGrid sichtbar

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 11: Gesamtlauf, Doku und Abnahme

**Files:**
- Modify: `CLAUDE.md` (neuer Abschnitt nach „Nova: Aufklapp-Listen und Grafiken (2026-09-08)")
- Modify: `docs/reviews/2026-09-08-redesign-gesamtaudit/OPTIK-NACHPRUEFUNG.md` (Stand-Block oben)

**Interfaces:** keine.

- [ ] **Step 1: Ganzes UI-Testprojekt**

Run: `dotnet test tests/AuswertungPro.Next.UI.Tests/AuswertungPro.Next.UI.Tests.csproj --nologo`
Expected: 0 Fehler; grün mindestens 6887 + 12 neue (1 Seitenköpfe-Theorie × 2, 1 Kontrast-Theorie × 2, 8 in `DesignAuditOptikNachpruefungTests`); übersprungen weiterhin 18. Weicht die Zahl der Übersprungenen ab, hat `UebersprungeneTestsWaechterTests` etwas zu sagen — lesen, nicht überspringen.

- [ ] **Step 2: Infrastruktur- und Pipeline-Tests (die Laufzeittexte liegen teils in Application)**

Run: `dotnet test tests/AuswertungPro.Next.Infrastructure.Tests/AuswertungPro.Next.Infrastructure.Tests.csproj --nologo` und `dotnet test tests/AuswertungPro.Next.Pipeline.Tests/AuswertungPro.Next.Pipeline.Tests.csproj --nologo`
Expected: 0 Fehler (Referenz 6316 / 2648 grün).

- [ ] **Step 3: CLAUDE.md**

Hinter dem Abschnitt „### Projektwechsel-Fixwelle (08.09.2026, R1/R2/R4 aus dem Gesamtaudit)" einfügen:

```markdown
### Optik-Nachpruefung (08.09.2026, O1-O19 aus dem Gesamtaudit)

Befunde mit Bildbelegen: `docs/reviews/2026-09-08-redesign-gesamtaudit/OPTIK-NACHPRUEFUNG.md`.
Umgesetzt sind O1-O7, O10-O12 und O16-O19; Waechter `DesignAuditOptikNachpruefungTests` (8)
sowie Erweiterungen in `DesignAuditNovaSeitenkoepfeTests` (jetzt 5), `DesignAuditContrastTests`,
`DesignAuditFeinschliffTests` und `DesignAuditLaufzeittexteTests`. Nicht zurueckdrehen:

- **Der `PageTitle`-Stil traegt seine Akzentlinie als `TextDecoration` 7 px unter der
  Grundlinie.** Ein Untertitel gehoert deshalb NEBEN den Titel (wie im `NovaPageHeader`),
  nie in eine Zeile darunter — sonst wirkt er durchgestrichen (Matrix-Seiten, O1).
- **Der Farbwaechter sieht auch Setter in Triggern** (`Property="Background" Value="#…"`).
  Hervorhebungen im Formular und im Sanierungsmassnahmen-Fenster laufen ueber die Tokens
  `Sanieren*`, `Ausgefuehrt*`, `DangerRowBrush`, `WarningRowBrush`, `Uebertragen*` (O2).
- **Ein Abzeichen nimmt die Text-Tinte seiner Flaeche**: `SuccessTextBrush` auf
  `SuccessSubtleBrush` (5,8:1 dunkel, 4,6:1 hell), nie `SuccessBrush` (2,3:1 dunkel). Der
  Kontrastwaechter prueft die Paare der Pruefung-Abzeichen in beiden Themen (O17).
- **Tinte in Vorlagen ueber `ContentPresenter.Resources` durchreichen** (B7-Falle) — auch
  bei `ToggleButton`-Chips. Die ZK-Filterchips nehmen ihre Tinte aus
  `ZustandsklasseInkPolicy`, nicht aus `Brushes.Black` (O18).
- **Sichtbare Texte: kein `ß`.** Der Umlaut-Waechter meldet jetzt auch das scharfe ß; der
  Laufzeittext-Waechter kennt die beiden Matrix-ViewModels und die Kosten-/Massnahmen-
  Controller (O4/O5).
- **Ein Hauptknopf je Seite als Pille** (`ToolbarButtonAccent`); `PrimaryButton` gibt es
  in `Views/Pages` nur noch in der klassischen `OverviewPage`. Beenden liegt allein im
  Menue `Datei` (O11).
- Offen fuer einen Folgeplan: O8, O9, O13, O14, O15 und Abschnitt F der Nachpruefung.
```

- [ ] **Step 4: Stand in der Nachprüfung**

`OPTIK-NACHPRUEFUNG.md`, direkt nach dem ersten Absatz („Ergebnis in einem Satz …") einfügen:

```markdown
**Stand nach Umsetzung (Plan `docs/superpowers/plans/2026-09-08-optik-nachpruefung-umsetzung.md`):**
O1, O2, O3, O4, O5, O6, O7, O10, O11, O12, O16, O17, O18, O19 sind umgesetzt und durch Wächter
gehalten. Offen: O8, O9, O13, O14, O15 und Abschnitt F — sie brauchen eine Gestaltungsentscheidung
und einen eigenen Plan.
```

- [ ] **Step 5: Commit und Übergabe**

```bash
git add CLAUDE.md docs/reviews/2026-09-08-redesign-gesamtaudit/OPTIK-NACHPRUEFUNG.md
git commit -m "Optik-Nachpruefung: Stand und Waechter in CLAUDE.md und Bericht festgehalten

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
git log --oneline -12
```

Danach **nicht selbst mergen**: Die parallele Sitzung arbeitet auf demselben Zweig im Hauptbaum. Übergabe an Pascal mit dem Hinweis, dass `feature/optik-nachpruefung` keine der tabu-Dateien berührt und sich deshalb ohne Konflikt auf den dann aktuellen Stand rebasen lässt (`git rebase feature/eval-pruefsatz-review` im Worktree, dann Sichtprüfung hell + dunkel auf Haltungen, Schächte, Sanierungs-Matrix, Einstellungen, Schattenauswertung).

---

## Selbstprüfung des Plans

**Spec-Abdeckung:** O1 → T1 · O2 → T2 · O3 → T10 · O4/O5/O6 → T5 · O7 → T6 · O10 → T7 · O11/O12 → T8 · O16 → T3 · O17/O18 → T4 · O19 → T9. Ausgenommen mit Begründung: O8, O9, O13, O14, O15, Abschnitt F, B1–B7.

**Typen und Namen über Tasks hinweg:** `DesignAuditOptikNachpruefungTests` mit Helfer `Ui(params string[])` wird in T3 angelegt und in T4, T6, T7, T8, T9, T10 erweitert. `ReadBrushColor` nur in T4 (`DesignAuditContrastTests`). Token-Namen aus T2 werden nur in T2 verwendet. `HatErgebnis` nur in T7. Stilname `ToolbarButtonAccent` in T8 (bestehend).

**Reihenfolge:** T1–T4 sind unabhängig. T5 muss vor T8 laufen (T8 entfernt den „Programm schließen"-Knopf, den T5 bewusst nicht umschreibt; läuft T8 zuerst, meldet der ß-Wächter aus T5 diese Zeile nicht mehr — harmlos). T11 zuletzt.
