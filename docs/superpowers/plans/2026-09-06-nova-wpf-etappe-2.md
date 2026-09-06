# Nova-Redesign in WPF, Etappe 2: das vollständige Redesign — Umsetzungsplan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Den freigegebenen Prototyp `SewerStudio-Nova-Optimiert-v2.html` vollständig in die WPF-Anwendung übernehmen: Paletten Hell·Glas / Dunkel·Cockpit, Rahmen mit Brotkrume, globaler Suche, Aufgaben-Chip und KI-Bereitschaft, Übersichtsseite im Projekt, Haltungs- und Schachtseite im Nova-Aufbau, Player und Training Studio im Prototyp-Aufbau, Bewegung, und die übrigen Seiten mit einheitlichem Seitenkopf. Keine Fachfunktion verschwindet.

**Architecture:** Additiv wie in Etappe 1. Reine Regeln (Prüfstatus, nächste Aufgabe, globale Suche, Kennzahlen, Rohrring-Geometrie, Bereitschaftstext) liegen WPF-frei unter `src/AuswertungPro.Next.Application/UseCases/...` oder `UI/DataPage` und haben je einen Test. Neue Sichten sind eigene UserControls; grosse Bestandsdateien wachsen nicht (Deckel 1000 Zeilen, `MaintainabilityFitnessTests`), neue Logik kommt in eigene Partial-Dateien und Controller. Farben, Schriften, Rundungen laufen nur über Theme-Tokens; jede sichtbare Regel bekommt einen Wächtertest neben den bestehenden `DesignAudit*Tests`.

**Tech Stack:** WPF / .NET 10, CommunityToolkit.Mvvm, xunit (`tests/AuswertungPro.Next.UI.Tests`, `tests/AuswertungPro.Next.Infrastructure.Tests`), vorhandene Bausteine `RecordDetailsView`, `DataPageRecordDetailsBuilder`, `SchaechteRecordDetailsBuilder`, `HaltungFelderDrawer`, `HaltungUebersichtPanel`, `DataPageNovaWorkspaceController`, `DataPageColumnViewCatalog`, `SplitterPersistenceBehavior`, `ButtonContextMenuOpener`, `NeuralPulseDot`, `MotionSettings`, `FluentIcon`, `DashboardStatisticsBuilder`, `AiRuntimeStatusTracker`.

**Spec:** `docs/reviews/2026-09-06-nova/optimiert/v2/SewerStudio-Nova-Optimiert-v2.html` (freigegebener Prototyp) und das daraus erstellte, zeilengenaue Inventar `docs/reviews/2026-09-06-nova/wpf-etappe-2/PROTOTYP-INVENTAR.md` (Abschnitte 1 bis 9; Verweise unten heissen „Inventar 4.3" usw.). Dazu `docs/reviews/2026-09-06-nova/optimiert/FUNKTIONSLISTE.md` (nichts darf verschwinden) und `docs/reviews/2026-09-06-nova/BEWERTUNG.md` (N01 bis N10).

## Global Constraints

- Arbeitsbaum: Worktree `C:\Sewer-Studio_KI_4.5-nova`, Branch `feature/nova-etappe-2` (abgezweigt von `feature/eval-pruefsatz-review` bei `8550cb76d`). Der Hauptbaum `C:\Sewer-Studio_KI_4.5` wird nicht angefasst; dort läuft eine andere Sitzung.
- Keine NuGet-Pakete. Keine neuen gespeicherten Datenformate; `settings.json` nur additiv (fehlender Eintrag = bisheriges Verhalten). Kundenoriginale werden nie berührt.
- Deckel 1000 Zeilen je Produktionsdatei (`MaintainabilityFitnessTests`). `DataPage.xaml.cs` 877 und `SchaechtePage.xaml.cs` 909 Zeilen: dort NICHTS ergänzen, nur in neuen Partial-Dateien.
- Schriftgrössen nur als `{DynamicResource TextXS|TextS|TextM|TextL|TextXL|TextTitle|TextDisplay}` (`DesignAuditSchriftskalaTests`, gilt auch für `Setter Property="FontSize"`); 11 px ist die Untergrenze. In `Theme/*.xaml` sind Zahlen erlaubt, nie unter 11.
- Rundungen ausserhalb des Themes nur `RadiusS 4, RadiusM 6, RadiusL 8, RadiusXL 10, RadiusXXL 14, RadiusPill 999` (`DesignAuditFensterUndRundungenTests`).
- Feste Farben `#RRGGBB` nur in den sechs Video-Dateien; sonst `{DynamicResource …Brush}` (`DesignAuditFeinschliffTests`). In `Theme/*.xaml` sind Hexwerte erlaubt.
- Sichtbare Texte mit echten Umlauten und Schweizer `ss`; Quellcode und Kommentare mit `ae/oe/ue`. Deutsch überall.
- Jeder Icon-Knopf trägt `AutomationProperties.Name` UND `ToolTip`; jeder Menüpunkt mit literalem `Header` trägt ein `MenuItem.Icon` (`DesignAuditAccessibilityTests`, `DesignAuditFeinschliffTests`). Kein Textsymbol (`▲▼✕⚠📷`) als Bedienelement; `ui:FluentIcon` verwenden.
- `DesignAuditContrastTests`: Weiss auf `ColorAccent`/`ColorAccentHover`/`ColorSuccess` ≥ 4,5:1 in BEIDEN Themes; `ColorKiText` auf `ColorCard` und `ColorKiSubtle` ≥ 4,5:1; `ColorTextMuted` (dunkel) auf `ColorCard` ≥ 4,5:1; `ColorWarning` (hell) auf `ColorCard` ≥ 4,5:1; Auswahltext ≥ 4,5:1, Auswahlkontur ≥ 3:1. Deshalb bleibt der DUNKLE Akzent `#2563EB` (der Prototyp-Wert `#7CC4FF` trägt keinen weissen Text); das Prototyp-Hellblau kommt als `AccentTextBrush` nur für Text und Symbole.
- Zustandsklassenfarben Z0 bis Z4 bleiben unverändert (`ZustandsklasseColorPalette`, `ZustandsklasseInkPolicy`; BEWERTUNG „Beibehalten").
- Jede XAML-Aktion braucht einen Handler in ihrer Partial-Klasse; jeder sichtbare Blatt-Knopf eine Aktion (`XamlActionWiringGuardTests`). Beim Umgruppieren im Player und Training Studio werden `x:Name`, `Click`, `Command` und `ToolTip` wörtlich beibehalten; es wird nur verschoben, nie umbenannt. Die Tooltips aus `DesignAuditPlayerShortcutTests` bleiben zeichengleich.
- Dauerbewegung unterliegt `MotionSettings.ReduceMotion`.
- Bewusste Abweichungen vom Prototyp (nicht umsetzen): Knopf „Verwerfen" (Autosave bei jeder Änderung macht ihn sinnlos), Avatar „PA" (kein Benutzermodell), Schein-Knopf „Für Training freigeben" im Studio (die Freigabe läuft über das Export-Register im Training Center; Abschnitt 3 zeigt das ehrlich), Demo-Toasts.
- Build und Test im Worktree: `dotnet build AuswertungPro.sln` und `dotnet test tests/AuswertungPro.Next.UI.Tests --no-build`. Läuft `SewerStudio.exe`, sind DLLs gesperrt: dann mit `-o .tmp/testout-nova2` bauen und testen (Memory „Laufendes Programm sperrt Build"). Die App nie mit dem echten Profil starten (Autosave); nur der isolierte Prüfhost aus Task 19.
- Commits: kleine, deutsche Commit-Nachrichten, am Ende `Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>`.

## Dateiübersicht

Neu (Application, WPF-frei):
- `src/AuswertungPro.Next.Application/UseCases/NaechsteAufgabe/HaltungPruefstatus.cs` — Prüfstatus je Haltung (Abgeschlossen / KiAnalysiert / Offen).
- `src/AuswertungPro.Next.Application/UseCases/NaechsteAufgabe/NaechsteAufgabeRegel.cs` — erste zu prüfende Haltung, Chip-Text.
- `src/AuswertungPro.Next.Application/UseCases/Suche/GlobaleSucheRegel.cs` — Treffer Haltung / Schacht / Strasse, höchstens 12.
- `src/AuswertungPro.Next.Application/UseCases/Uebersicht/ProjektUebersichtKennzahlen.cs` — Kennzahlen, Hero-Text, Stammdaten-Vollständigkeit.
- `src/AuswertungPro.Next.Application/UseCases/CodingSuggestions/ICodingSuggestionRegistry.cs` + `CodingSuggestionRegistry.cs` — Sitzungsregister der KI-Vorabdurchläufe.
- `src/AuswertungPro.Next.Application/UseCases/Uebersicht/RohrringGeometrie.cs` — Bögen des Rohrrings aus Befunden.

Neu (UI):
- `src/AuswertungPro.Next.UI/DataPage/DataPageColumnStyleRules.cs` — fette Namensspalte, Mono-Zahlen rechts.
- `src/AuswertungPro.Next.UI/DataPage/SchaechteColumnViewCatalog.cs` — Spaltensätze der Schachtliste.
- `src/AuswertungPro.Next.UI/DataPage/SchaechteNovaWorkspaceController.cs` — Tabelle | Schachtansicht | Eingabefelder.
- `src/AuswertungPro.Next.UI/ViewModels/ShellNavigationTitles.cs` — Anzeigename je Navigationstitel.
- `src/AuswertungPro.Next.UI/ViewModels/KiBereitschaftRegel.cs` — Leistentext aus `AiRuntimeStatus`.
- `src/AuswertungPro.Next.UI/ViewModels/GlobaleSucheViewModel.cs` — Suchfeld Strg+K.
- `src/AuswertungPro.Next.UI/ViewModels/ShellViewModel.Nova.cs` — Brotkrume, Aufgaben-Chip, Speicherstand, Bereitschaft (Partial).
- `src/AuswertungPro.Next.UI/ViewModels/Pages/ProjektUebersichtPageViewModel.cs` — Übersicht im Projekt.
- `src/AuswertungPro.Next.UI/Views/Pages/ProjektUebersichtPage.xaml(.cs)` — Hero, KI-Vorabdurchlauf, KPIs, Donut, Schäden, Projekte, Verfahren, Stammdaten.
- `src/AuswertungPro.Next.UI/Views/Pages/Haltungsansicht/RohrringControl.xaml(.cs)` — Rohrquerschnitt mit Uhrlagen.
- `src/AuswertungPro.Next.UI/Views/Pages/Schachtansicht/SchachtUebersichtPanel.xaml(.cs)` — Grundriss, Fakten, Schäden.
- `src/AuswertungPro.Next.UI/Views/Pages/SchaechtePage.ColumnViews.cs`, `SchaechtePage.NovaWorkspace.cs` — Partials.
- `src/AuswertungPro.Next.UI/Controls/NovaPageHeader.xaml(.cs)` — Titel, Untertitel, Hauptaktion.
- `src/AuswertungPro.Next.UI/Controls/NetzHintergrund.xaml(.cs)` — Hintergrund-Engine (60 Knoten).

Geändert: `Theme/Theme.xaml`, `Theme/ThemeLight.xaml`, `MainWindow.xaml(.cs)`, `ShellViewModel.NavigationSupport.cs`, `AppSettings.cs`, `DataPage.xaml`, `DataPage.ColumnViews.cs`, `DataPage.NovaWorkspace.cs`, `HaltungFelderDrawer.xaml(.cs)`, `HaltungUebersichtPanel.xaml(.cs)`, `SchaechtePage.xaml`, `PlayerWindow.xaml`, `PlayerCodingSidePanel.xaml`, `TrainingStudioWindow.xaml(.cs)`, `SettingsPage.xaml`, elf Seiten-XAML (Seitenkopf), `ServiceProviderRegistrationMap.cs`, `PlayerWindow.Coding.Suggestions.cs`, `CLAUDE.md`.

Tests (alle unter `tests/AuswertungPro.Next.UI.Tests/`, ausser Application-Regeln unter `tests/AuswertungPro.Next.Infrastructure.Tests/`): `DesignAuditNovaPaletteTests`, `DataPageColumnStyleRulesTests`, `HaltungPruefstatusTests`, `NaechsteAufgabeRegelTests`, `GlobaleSucheRegelTests`, `ShellNavigationTitlesTests`, `KiBereitschaftRegelTests`, `ShellNovaKopfzeileTests`, `ProjektUebersichtKennzahlenTests`, `CodingSuggestionRegistryTests`, `DesignAuditNovaUebersichtTests`, `RohrringGeometrieTests`, `DesignAuditNovaHaltungenTests` (erweitert), `SchaechteColumnViewCatalogTests`, `DesignAuditNovaSchaechteTests`, `DesignAuditNovaPlayerTests`, `DesignAuditNovaTrainingStudioTests`, `NetzHintergrundTests`, `DesignAuditNovaSeitenkoepfeTests`.

---
## Teil A — Stil: Hell·Glas und Dunkel·Cockpit

### Task 1: Paletten-Tokens, Karten, Leiste (beide Themes)

**Files:**
- Modify: `src/AuswertungPro.Next.UI/Theme/ThemeLight.xaml` (Farbtokens Zeilen 10–45, `Card` Zeile ~215, `BgBrush`/`NavPanelBrush` Zeilen ~140–155)
- Modify: `src/AuswertungPro.Next.UI/Theme/Theme.xaml` (dieselben Stellen)
- Test: `tests/AuswertungPro.Next.UI.Tests/DesignAuditNovaPaletteTests.cs`

**Interfaces:**
- Produces: neue Pinsel `AccentTextBrush` (hell `#154FB0`, dunkel `#9AD3FF`), `FaintBrush` (hell `#4F5F78`, dunkel `#A5B3C8`), `GlassBorderBrush` (hell `#24142850`, dunkel `#298CBEFF`), `SuccessTextBrush`/`WarningTextBrush`/`DangerTextBrush` bleiben; `NavPanelBrush` wird halbtransparent. Spätere Tasks binden `AccentTextBrush` und `FaintBrush`.

Werte aus Inventar 1.1 (hell / dunkel):

| Token | hell | dunkel |
|---|---|---|
| ColorBgLight | `#FFEEF2F7` | `#FF101B2E` |
| ColorBgMid | `#FFE4EAF2` | `#FF0C1524` |
| ColorCard | `#FFFFFFFF` | `#FF16223A` |
| ColorCardGlass | `#FFFFFFFF` | `#FF16223A` |
| ColorHeader | `#FFF6F8FB` | `#FF1B2940` |
| ColorBorder | `#FFD3DAE5` | `#FF2B3A55` |
| ColorBorderLight | `#FFE4E9F0` | `#FF22304A` |
| ColorTextPrimary | `#FF14213A` | `#FFEAF0FA` |
| ColorTextSecondary | `#FF44546E` | `#FFB4C1D6` |
| ColorTextMuted | `#FF4F5F78` | `#FFA5B3C8` |
| ColorAccent | `#FF1B5FD1` | bleibt `#FF2563EB` |
| ColorAccentHover | `#FF154FB0` | bleibt `#FF1D4ED8` |
| ColorAccentLight | `#FF3B82F6` (bleibt) | `#FF7CC4FF` |
| ColorAccentSubtle | `#FF1B5FD1` mit 11 % → `#1C1B5FD1` | `#247CC4FF` |
| ColorSelection | `#FFD6E4F7` (bleibt) | `#FF30587A` (bleibt) |
| ColorKi / KiSubtle / KiText | bleiben | bleiben |

Kontrolle vorab (Kontrastformel wie in `DesignAuditContrastTests`): Weiss auf `#1B5FD1` = 5,9:1; Weiss auf `#154FB0` = 7,4:1; `#4F5F78` auf Weiss = 6,5:1; `#A5B3C8` auf `#16223A` = 7,5:1. Alle über 4,5.

- [ ] **Step 1: Wächter schreiben (rot)**

```csharp
// tests/AuswertungPro.Next.UI.Tests/DesignAuditNovaPaletteTests.cs
using System.IO;
using System.Text.RegularExpressions;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>Nova-Etappe 2, Teil A: Die Tokenwerte des Prototyps (Inventar 1.1) bleiben stehen.</summary>
public sealed class DesignAuditNovaPaletteTests
{
    [Theory]
    [InlineData("ThemeLight.xaml", "ColorCard", "#FFFFFFFF")]
    [InlineData("ThemeLight.xaml", "ColorBgLight", "#FFEEF2F7")]
    [InlineData("ThemeLight.xaml", "ColorTextPrimary", "#FF14213A")]
    [InlineData("ThemeLight.xaml", "ColorBorder", "#FFD3DAE5")]
    [InlineData("ThemeLight.xaml", "ColorAccent", "#FF1B5FD1")]
    [InlineData("Theme.xaml", "ColorCard", "#FF16223A")]
    [InlineData("Theme.xaml", "ColorBgMid", "#FF0C1524")]
    [InlineData("Theme.xaml", "ColorTextPrimary", "#FFEAF0FA")]
    [InlineData("Theme.xaml", "ColorBorder", "#FF2B3A55")]
    [InlineData("Theme.xaml", "ColorAccent", "#FF2563EB")]
    public void Prototyp_Tokens_stehen_im_Theme(string datei, string token, string erwartet)
        => Assert.Equal(erwartet, ReadColor(Xaml(datei), token));

    [Theory]
    [InlineData("ThemeLight.xaml")]
    [InlineData("Theme.xaml")]
    public void Neue_Pinsel_sind_in_beiden_Themes_definiert(string datei)
    {
        var xaml = Xaml(datei);
        Assert.Contains("x:Key=\"AccentTextBrush\"", xaml);
        Assert.Contains("x:Key=\"FaintBrush\"", xaml);
        Assert.Contains("x:Key=\"GlassBorderBrush\"", xaml);
    }

    [Theory]
    [InlineData("ThemeLight.xaml")]
    [InlineData("Theme.xaml")]
    public void Karten_haben_Prototyp_Rundung_und_weichen_Schatten(string datei)
    {
        var card = Regex.Match(Xaml(datei), "<Style x:Key=\"Card\"[\\s\\S]*?</Style>").Value;
        Assert.Contains("<Setter Property=\"CornerRadius\" Value=\"10\"/>", card);
        Assert.Contains("BlurRadius=\"20\"", card);
        Assert.Contains("ShadowDepth=\"6\"", card);
    }

    [Theory]
    [InlineData("ThemeLight.xaml")]
    [InlineData("Theme.xaml")]
    public void Akzenttext_liest_sich_auf_der_Karte(string datei)
    {
        var xaml = Xaml(datei);
        Assert.True(Kontrast(ReadColor(xaml, "ColorAccentText"), ReadColor(xaml, "ColorCard")) >= 4.5);
        Assert.True(Kontrast(ReadColor(xaml, "ColorTextFaint"), ReadColor(xaml, "ColorCard")) >= 4.5);
    }

    internal static string Xaml(string datei)
        => File.ReadAllText(RepoFile("src", "AuswertungPro.Next.UI", "Theme", datei));

    internal static string ReadColor(string xaml, string key)
    {
        var m = Regex.Match(xaml, $"<Color x:Key=\"{key}\">(#[0-9A-Fa-f]{{8}})</Color>");
        Assert.True(m.Success, $"Token {key} fehlt");
        return m.Groups[1].Value.ToUpperInvariant();
    }

    internal static double Kontrast(string a, string b)
    {
        static double Lum(string hex)
        {
            double C(int i) { var c = System.Convert.ToInt32(hex.Substring(i, 2), 16) / 255.0; return c <= 0.03928 ? c / 12.92 : System.Math.Pow((c + 0.055) / 1.055, 2.4); }
            return 0.2126 * C(3) + 0.7152 * C(5) + 0.0722 * C(7);
        }
        var (l1, l2) = (Lum(a), Lum(b));
        return (System.Math.Max(l1, l2) + 0.05) / (System.Math.Min(l1, l2) + 0.05);
    }

    internal static string RepoFile(params string[] parts)
    {
        var dir = new DirectoryInfo(System.AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "AuswertungPro.sln")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        return Path.Combine(new[] { dir!.FullName }.Concat(parts).ToArray());
    }
}
```

- [ ] **Step 2: Test laufen lassen, rot**

Run: `dotnet build tests/AuswertungPro.Next.UI.Tests && dotnet test tests/AuswertungPro.Next.UI.Tests --no-build --filter DesignAuditNovaPaletteTests`
Expected: FAIL (Token ColorCard hell ist `#FFFCFDFF`, `AccentTextBrush` fehlt).

- [ ] **Step 3: ThemeLight.xaml umstellen**

Im Block der Farbtokens (Zeilen 10–45) die Werte aus der Tabelle setzen und direkt unter `ColorAccentSubtle` ergänzen:

```xml
    <!-- Nova-Etappe 2: Akzent nur als Text (Prototyp --accent-text), gedaempfter Text (--faint), Glasrand -->
    <Color x:Key="ColorAccentText">#FF154FB0</Color>
    <Color x:Key="ColorTextFaint">#FF4F5F78</Color>
    <Color x:Key="ColorGlassBorder">#24142850</Color>
```

Bei den soliden Pinseln (nach `BorderLightBrush`) ergänzen:

```xml
    <SolidColorBrush x:Key="AccentTextBrush" Color="{StaticResource ColorAccentText}"/>
    <SolidColorBrush x:Key="FaintBrush" Color="{StaticResource ColorTextFaint}"/>
    <SolidColorBrush x:Key="GlassBorderBrush" Color="{StaticResource ColorGlassBorder}"/>
```

`BgBrush` (Verlauf) auf die Prototyp-Fläche mit schwachen Flecken:

```xml
    <LinearGradientBrush x:Key="BgBrush" StartPoint="0,0" EndPoint="1,1">
        <GradientStop Color="#FFEEF2F7" Offset="0"/>
        <GradientStop Color="#FFEAEFF6" Offset="0.5"/>
        <GradientStop Color="#FFE4EAF2" Offset="1"/>
    </LinearGradientBrush>
```

`NavPanelBrush` wird die Glasleiste (72 % Weiss, Inventar 1.7): `<SolidColorBrush x:Key="NavPanelBrush" Color="#B8FFFFFF"/>` (den bisherigen Verlauf ersetzen).

`Card`-Stil (Zeile ~215): `CornerRadius` 6 → `10`, `Padding` bleibt, Schatten `<DropShadowEffect Color="#FF101C33" BlurRadius="20" ShadowDepth="6" Direction="270" Opacity="0.08"/>`.

DataGrid-Kopf und Linien (Zeile ~692 ff.): `HorizontalGridLinesBrush` `#FFCDD6E4` → `{DynamicResource BorderLightBrush}` (als `Setter Property="HorizontalGridLinesBrush" Value="{DynamicResource BorderLightBrush}"`), `AlternatingRowBackground` `#FFF3F5FA` → `#FFFAFBFD`, Kopf-`Background` `#FFE4EAF4` → `{DynamicResource HeaderBrush}`, Kopf-`BorderBrush` → `{DynamicResource BorderLightBrush}`.

- [ ] **Step 4: Theme.xaml (dunkel) umstellen**

Farbtokens gemäss Tabelle (Akzent bleibt). Ergänzen:

```xml
    <Color x:Key="ColorAccentText">#FF9AD3FF</Color>
    <Color x:Key="ColorTextFaint">#FFA5B3C8</Color>
    <Color x:Key="ColorGlassBorder">#298CBEFF</Color>
```
plus die drei Pinsel wie in Step 3. `BgBrush`:

```xml
    <LinearGradientBrush x:Key="BgBrush" StartPoint="0,0" EndPoint="1,1">
        <GradientStop Color="#FF0E1828" Offset="0"/>
        <GradientStop Color="#FF0C1524" Offset="0.5"/>
        <GradientStop Color="#FF0B1220" Offset="1"/>
    </LinearGradientBrush>
```
`NavPanelBrush`: `<SolidColorBrush x:Key="NavPanelBrush" Color="#B8101B30"/>`. `Card`: `CornerRadius` 10, Schatten `<DropShadowEffect Color="#FF000000" BlurRadius="30" ShadowDepth="10" Direction="270" Opacity="0.45"/>`. DataGrid: Kopf-Background `{DynamicResource HeaderBrush}`, Linien `{DynamicResource BorderLightBrush}`.

- [ ] **Step 5: Bestehende Wächter und neuen Test laufen lassen**

Run: `dotnet build AuswertungPro.sln && dotnet test tests/AuswertungPro.Next.UI.Tests --no-build --filter "DesignAudit|ZustandsklasseInk|NovaLayoutIsolated"`
Expected: alles grün. Scheitert `Muted_dark_text_and_light_warning_text_reach_normal_text_contrast`, ist ein Tabellenwert falsch abgetippt; nicht den Test ändern.

- [ ] **Step 6: Commit**

```bash
git add src/AuswertungPro.Next.UI/Theme/Theme.xaml src/AuswertungPro.Next.UI/Theme/ThemeLight.xaml tests/AuswertungPro.Next.UI.Tests/DesignAuditNovaPaletteTests.cs
git commit -m "Nova-Etappe 2: Paletten Hell·Glas und Dunkel·Cockpit als Theme-Tokens"
```

### Task 2: Pillen-Knöpfe, Chips, Tabellenkopf in Grossbuchstaben

**Files:**
- Modify: `src/AuswertungPro.Next.UI/Theme/ThemeLight.xaml` (`ToolbarButton` ~359, `ToolbarButtonAccent` ~415, `CompactToggleButton` ~568, `DataGridColumnHeader` ~724)
- Modify: `src/AuswertungPro.Next.UI/Theme/Theme.xaml` (dieselben Stile)
- Test: `tests/AuswertungPro.Next.UI.Tests/DesignAuditNovaPaletteTests.cs` (erweitern)

- [ ] **Step 1: Wächter erweitern (rot)**

```csharp
    [Theory]
    [InlineData("ThemeLight.xaml")]
    [InlineData("Theme.xaml")]
    public void Werkzeugknoepfe_und_Chips_sind_Pillen_und_der_Tabellenkopf_ist_in_Kapitaelchen(string datei)
    {
        var xaml = Xaml(datei);
        string Stil(string key) => Regex.Match(xaml, $"<Style x:Key=\"{key}\"[\\s\\S]*?\n    </Style>").Value;
        Assert.Contains("CornerRadius=\"999\"", Stil("ToolbarButton"));
        Assert.Contains("CornerRadius=\"999\"", Stil("ToolbarButtonAccent"));
        Assert.Contains("CornerRadius=\"999\"", Stil("CompactToggleButton"));
        var header = Regex.Match(xaml, "<Style TargetType=\"\\{x:Type DataGridColumnHeader\\}\">[\\s\\S]*?\n    </Style>").Value;
        Assert.Contains("Typography.Capitals=\"AllSmallCaps\"", header);
        Assert.Contains("Foreground\" Value=\"{DynamicResource MutedBrush}\"", header);
    }
```

- [ ] **Step 2: Rot bestätigen**

Run: `dotnet test tests/AuswertungPro.Next.UI.Tests --filter DesignAuditNovaPaletteTests`
Expected: FAIL an `CornerRadius="999"`.

- [ ] **Step 3: Stile in beiden Themes anpassen**

`ToolbarButton`: im Template `CornerRadius="5"` → `CornerRadius="999"`; `Padding` `10,5` → `12,5`; `Background` `Transparent` → `{DynamicResource CardBrush}`; `BorderBrush` `Transparent` → `{DynamicResource BorderBrush}` (Prototyp `.btn`: Fläche mit Rand, Inventar 1.5). Hover bleibt.
`ToolbarButtonAccent`: `CornerRadius="5"` → `999`.
`CompactToggleButton`: `CornerRadius="4"` → `999`; `Padding` `10,4` → `10,4` bleibt.
`DataGridColumnHeader`: `FontSize` 12 bleibt; `Foreground` → `{DynamicResource MutedBrush}`; auf dem inneren `Border` des Templates `Typography.Capitals="AllSmallCaps"` setzen (die Eigenschaft vererbt sich an den Kopftext); `Padding` `12,8` → `10,8`; `BorderThickness` `0,0,1,1` → `0,0,0,1`.

- [ ] **Step 4: Grün und Sicht**

Run: `dotnet build AuswertungPro.sln && dotnet test tests/AuswertungPro.Next.UI.Tests --no-build --filter "DesignAudit|DataPageNovaLayoutIsolated"`
Expected: grün. Der isolierte Smoke-Test prüft weiterhin `CardBrush` als Chip-Grundfläche; mit `CardBrush` als `ToolbarButton`-Hintergrund bleibt das gültig.

- [ ] **Step 5: Commit**

```bash
git add src/AuswertungPro.Next.UI/Theme tests/AuswertungPro.Next.UI.Tests/DesignAuditNovaPaletteTests.cs
git commit -m "Nova-Etappe 2: Pillenknoepfe, Chips und Kapitaelchen-Tabellenkopf"
```

### Task 3: Tabellen-Feinschliff (fetter Name, Zahlen in Mono rechts)

**Files:**
- Create: `src/AuswertungPro.Next.UI/DataPage/DataPageColumnStyleRules.cs`
- Modify: `src/AuswertungPro.Next.UI/Views/Pages/DataGridStandardTextColumnFactory.cs` (Create, ElementStyle)
- Modify: `src/AuswertungPro.Next.UI/Views/Pages/DataGridWrappingTextColumnFactory.cs` (CreateDisplayStyle erhält den Feldnamen)
- Test: `tests/AuswertungPro.Next.UI.Tests/DataPageColumnStyleRulesTests.cs`

**Interfaces:**
- Produces: `static class DataPageColumnStyleRules { bool IstNamensspalte(string feld); bool IstZahlenspalte(string feld); }`.

- [ ] **Step 1: Test (rot)**

```csharp
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.DataPage;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

public sealed class DataPageColumnStyleRulesTests
{
    [Fact]
    public void Nur_der_Haltungsname_ist_die_fette_Namensspalte()
    {
        Assert.True(DataPageColumnStyleRules.IstNamensspalte(FieldKeys.HoldingName));
        Assert.False(DataPageColumnStyleRules.IstNamensspalte(FieldKeys.Street));
    }

    [Theory]
    [InlineData("DN_mm", true)]
    [InlineData("Haltungslaenge_m", true)]
    [InlineData("Kosten", true)]
    [InlineData("VSA_Zustandsnote_D", true)]
    [InlineData("Gefaelle_Promille", true)]
    [InlineData("Baujahr", true)]
    [InlineData("Strasse", false)]
    [InlineData("Zustandsklasse", false)]
    public void Zahlenspalten_sind_die_Mengen_Masse_und_Kosten(string feld, bool erwartet)
        => Assert.Equal(erwartet, DataPageColumnStyleRules.IstZahlenspalte(feld));
}
```

- [ ] **Step 2: Rot** — Run: `dotnet test tests/AuswertungPro.Next.UI.Tests --filter DataPageColumnStyleRulesTests` → Kompilierfehler.

- [ ] **Step 3: Regel schreiben**

```csharp
using System;
using System.Collections.Generic;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.DataPage;

/// <summary>
/// Nova-Etappe 2 (Inventar 4.3, NUM_COLS): Namensspalte fett, Zahlenspalten rechtsbuendig in
/// der Datenschrift. Reine Regel; die Spaltenfabriken wenden sie nur an.
/// </summary>
public static class DataPageColumnStyleRules
{
    private static readonly HashSet<string> Zahlen = new(StringComparer.Ordinal)
    {
        FieldKeys.NominalDiameterMm, FieldKeys.ClearWidthMm, FieldKeys.HoldingLengthMeters,
        FieldKeys.ConstructionYear, "VSA_Zustandsnote_D", "VSA_Zustandsnote_S", "VSA_Zustandsnote_B",
        FieldKeys.LinerRenovationMeters, FieldKeys.LinerRenovationCount, FieldKeys.ConnectionsToGrout,
        FieldKeys.RepairSleeve, FieldKeys.LinerEndSleeve, FieldKeys.ShortLinerRepair,
        "Erneuerung_Neubau_m", FieldKeys.Cost, FieldKeys.GrossCost, FieldKeys.SlopePromille,
        FieldKeys.ShaftDimension1Mm, FieldKeys.ShaftDimension2Mm
    };

    public static bool IstNamensspalte(string feld) => string.Equals(feld, FieldKeys.HoldingName, StringComparison.Ordinal);

    public static bool IstZahlenspalte(string feld) => Zahlen.Contains(feld);
}
```

- [ ] **Step 4: Fabriken anwenden**

In `DataGridStandardTextColumnFactory.Create` nach dem Foreground-Setter:

```csharp
        if (DataPageColumnStyleRules.IstNamensspalte(fieldName))
            displayStyle.Setters.Add(new Setter(TextBlock.FontWeightProperty, FontWeights.SemiBold));
        if (DataPageColumnStyleRules.IstZahlenspalte(fieldName))
        {
            displayStyle.Setters.Add(new Setter(TextBlock.FontFamilyProperty, System.Windows.Application.Current?.TryFindResource("FontMono") ?? new FontFamily("Consolas")));
            displayStyle.Setters.Add(new Setter(TextBlock.TextAlignmentProperty, TextAlignment.Right));
        }
```
(`using System.Windows.Media;` ergänzen.) In `DataGridWrappingTextColumnFactory` die Signatur `CreateDisplayStyle(Style? baseStyle)` um `string fieldName` erweitern (Aufrufer in derselben Datei anpassen) und dieselben zwei Setter-Blöcke einfügen. Die gespeicherte Ausrichtung (`DataGridColumnLayoutController.SetAlignment`) läuft danach und darf weiterhin gewinnen; deshalb nichts am Controller ändern.

- [ ] **Step 5: Grün** — Run: `dotnet build AuswertungPro.sln && dotnet test tests/AuswertungPro.Next.UI.Tests --no-build --filter "DataPageColumnStyleRules|DataPageNovaLayoutIsolated|DataGrid"` → PASS (der Render-Test `NovaRenderingChecks.ColorColumnsUseTheirCellForeground` bleibt grün, weil die Zellfarbe unverändert vererbt wird).

- [ ] **Step 6: Commit**

```bash
git add src/AuswertungPro.Next.UI/DataPage/DataPageColumnStyleRules.cs src/AuswertungPro.Next.UI/Views/Pages/DataGridStandardTextColumnFactory.cs src/AuswertungPro.Next.UI/Views/Pages/DataGridWrappingTextColumnFactory.cs tests/AuswertungPro.Next.UI.Tests/DataPageColumnStyleRulesTests.cs
git commit -m "Nova-Etappe 2: Namensspalte fett, Zahlen in Datenschrift rechts"
```

---
## Teil B — Rahmen: Prüfstatus, Aufgaben-Chip, Brotkrume, Suche, Bereitschaft

### Task 4: Prüfstatus je Haltung und Regel „Nächste Aufgabe" (WPF-frei)

**Files:**
- Create: `src/AuswertungPro.Next.Application/UseCases/NaechsteAufgabe/HaltungPruefstatus.cs`
- Create: `src/AuswertungPro.Next.Application/UseCases/NaechsteAufgabe/NaechsteAufgabeRegel.cs`
- Test: `tests/AuswertungPro.Next.Infrastructure.Tests/NaechsteAufgabeRegelTests.cs`

**Interfaces:**
- Produces: `enum HaltungPruefstand { Offen, KiAnalysiert, Abgeschlossen }`; `static class HaltungPruefstatus { HaltungPruefstand Bestimme(HaltungRecord r); string Text(HaltungPruefstand s); bool HatVideo(HaltungRecord r); }`; `static class NaechsteAufgabeRegel { HaltungRecord? Naechste(IEnumerable<HaltungRecord> h); string ChipText(HaltungRecord? r); }`.
- Regel (Inventar 8.1, auf Programmdaten übersetzt): `Abgeschlossen` = Feld `Offen_abgeschlossen` gleich `abgeschlossen`; `KiAnalysiert` = mindestens ein nicht gelöschter Protokolleintrag mit `Ai != null && !Ai.Accepted`; sonst `Offen`. Nächste = erste `KiAnalysiert`, sonst erste `Offen` MIT Video (`Fields[Link]` nicht leer), sonst `null`.

- [ ] **Step 1: Test (rot)**

```csharp
using AuswertungPro.Next.Application.UseCases.NaechsteAufgabe;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class NaechsteAufgabeRegelTests
{
    private static HaltungRecord Haltung(string name, string status = "", string link = "", bool offenerKiBefund = false)
    {
        var r = new HaltungRecord();
        r.SetFieldValue(FieldKeys.HoldingName, name, FieldSource.Manual, false);
        r.SetFieldValue(FieldKeys.WorkflowStatus, status, FieldSource.Manual, false);
        r.SetFieldValue(FieldKeys.Link, link, FieldSource.Manual, false);
        if (offenerKiBefund)
        {
            r.Protocol = new ProtocolDocument();
            r.Protocol.Current ??= new ProtocolRevision();
            r.Protocol.Current.Entries.Add(new ProtocolEntry { Code = "BAB", Ai = new ProtocolEntryAiMeta { Accepted = false, Confidence = 0.9 } });
        }
        return r;
    }

    [Fact]
    public void Abgeschlossen_schlaegt_offene_KI_Befunde()
        => Assert.Equal(HaltungPruefstand.Abgeschlossen, HaltungPruefstatus.Bestimme(Haltung("a", "abgeschlossen", offenerKiBefund: true)));

    [Fact]
    public void Offener_KI_Befund_heisst_KI_analysiert()
        => Assert.Equal(HaltungPruefstand.KiAnalysiert, HaltungPruefstatus.Bestimme(Haltung("a", offenerKiBefund: true)));

    [Fact]
    public void Ohne_alles_ist_offen()
        => Assert.Equal(HaltungPruefstand.Offen, HaltungPruefstatus.Bestimme(Haltung("a")));

    [Fact]
    public void Naechste_ist_zuerst_KI_analysiert_dann_offen_mit_Video()
    {
        var ohneVideo = Haltung("1-2");
        var mitVideo = Haltung("2-3", link: "v.mp4");
        var analysiert = Haltung("3-4", offenerKiBefund: true);
        Assert.Same(analysiert, NaechsteAufgabeRegel.Naechste(new[] { ohneVideo, mitVideo, analysiert }));
        Assert.Same(mitVideo, NaechsteAufgabeRegel.Naechste(new[] { ohneVideo, mitVideo }));
        Assert.Null(NaechsteAufgabeRegel.Naechste(new[] { ohneVideo, Haltung("9-9", "abgeschlossen", "v.mp4") }));
    }

    [Fact]
    public void Chiptext_nennt_die_Haltung_oder_keine_offene_Pruefung()
    {
        Assert.Equal("Nächste Aufgabe: 2-3 prüfen", NaechsteAufgabeRegel.ChipText(Haltung("2-3", link: "v.mp4")));
        Assert.Equal("Keine offene Prüfung", NaechsteAufgabeRegel.ChipText(null));
    }

    [Theory]
    [InlineData(HaltungPruefstand.Abgeschlossen, "fachlich geprüft")]
    [InlineData(HaltungPruefstand.KiAnalysiert, "KI analysiert, Prüfung offen")]
    [InlineData(HaltungPruefstand.Offen, "nicht analysiert")]
    public void Statustexte_entsprechen_dem_Prototyp(HaltungPruefstand stand, string text)
        => Assert.Equal(text, HaltungPruefstatus.Text(stand));
}
```
Falls `ProtocolDocument.Current` einen anderen Typnamen als `ProtocolRevision` hat: den echten Namen aus `Domain/Protocol/ProtocolModels.cs` verwenden (dort nachsehen, wie `doc.Current?.Entries` gebaut wird) und den Test entsprechend anpassen; die Regel selbst liest nur `Protocol?.Current?.Entries`.

- [ ] **Step 2: Rot** — Run: `dotnet test tests/AuswertungPro.Next.Infrastructure.Tests --filter NaechsteAufgabeRegelTests` → Kompilierfehler.

- [ ] **Step 3: Regeln schreiben**

```csharp
// HaltungPruefstatus.cs
using System;
using System.Linq;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.UseCases.NaechsteAufgabe;

public enum HaltungPruefstand { Offen, KiAnalysiert, Abgeschlossen }

/// <summary>
/// Nova-Etappe 2: fachlicher Pruefstatus einer Haltung (Prototyp: geprueft / analysiert / offen).
/// Abgeschlossen = Feld offen/abgeschlossen ist "abgeschlossen". KI analysiert = mindestens ein
/// offener KI-Befund im Protokoll. Sonst offen. Reine Rechnung, keine Datenaenderung.
/// </summary>
public static class HaltungPruefstatus
{
    public static HaltungPruefstand Bestimme(HaltungRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        if (string.Equals(record.GetFieldValue(FieldKeys.WorkflowStatus)?.Trim(), "abgeschlossen", StringComparison.OrdinalIgnoreCase))
            return HaltungPruefstand.Abgeschlossen;
        var entries = record.Protocol?.Current?.Entries;
        if (entries is not null && entries.Any(e => !e.IsDeleted && e.Ai is { Accepted: false }))
            return HaltungPruefstand.KiAnalysiert;
        return HaltungPruefstand.Offen;
    }

    public static string Text(HaltungPruefstand stand) => stand switch
    {
        HaltungPruefstand.Abgeschlossen => "fachlich geprüft",
        HaltungPruefstand.KiAnalysiert => "KI analysiert, Prüfung offen",
        _ => "nicht analysiert"
    };

    public static bool HatVideo(HaltungRecord record)
        => !string.IsNullOrWhiteSpace(record.GetFieldValue(FieldKeys.Link));
}
```

```csharp
// NaechsteAufgabeRegel.cs
using System.Collections.Generic;
using System.Linq;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.UseCases.NaechsteAufgabe;

/// <summary>Inventar 8.1: erste KI-analysierte Haltung, sonst erste offene mit Video, sonst keine.</summary>
public static class NaechsteAufgabeRegel
{
    public static HaltungRecord? Naechste(IEnumerable<HaltungRecord> haltungen)
    {
        var liste = haltungen.ToList();
        return liste.FirstOrDefault(h => HaltungPruefstatus.Bestimme(h) == HaltungPruefstand.KiAnalysiert)
            ?? liste.FirstOrDefault(h => HaltungPruefstatus.Bestimme(h) == HaltungPruefstand.Offen && HaltungPruefstatus.HatVideo(h));
    }

    public static string ChipText(HaltungRecord? naechste)
        => naechste is null
            ? "Keine offene Prüfung"
            : $"Nächste Aufgabe: {naechste.GetFieldValue(FieldKeys.HoldingName)} prüfen";
}
```

- [ ] **Step 4: Grün** — Run: `dotnet test tests/AuswertungPro.Next.Infrastructure.Tests --filter NaechsteAufgabeRegelTests` → PASS.

- [ ] **Step 5: Commit** — `git add src/AuswertungPro.Next.Application/UseCases/NaechsteAufgabe tests/AuswertungPro.Next.Infrastructure.Tests/NaechsteAufgabeRegelTests.cs && git commit -m "Nova-Etappe 2: Pruefstatus je Haltung und Regel Naechste Aufgabe"`

### Task 5: Kopfzeile mit Brotkrume und Aufgaben-Chip, Anzeigenamen mit Umlauten

**Files:**
- Create: `src/AuswertungPro.Next.UI/ViewModels/ShellNavigationTitles.cs`
- Create: `src/AuswertungPro.Next.UI/ViewModels/ShellViewModel.Nova.cs`
- Modify: `src/AuswertungPro.Next.UI/ViewModels/ShellViewModel.NavigationSupport.cs` (`NavItem` erhält `DisplayTitle`)
- Modify: `src/AuswertungPro.Next.UI/ViewModels/ShellViewModel.cs` (eine Zeile `InitNova();` vor `EnterLauncher();` im Konstruktor; in `RefreshTitleAndDirty` eine Zeile `AktualisiereNovaKopfzeile();`)
- Modify: `src/AuswertungPro.Next.UI/MainWindow.xaml` (Kopfzeile Zeilen 21–139; Navigations-`TextBlock Text="{Binding Title}"` → `DisplayTitle`; Fusszeile der Leiste)
- Test: `tests/AuswertungPro.Next.UI.Tests/ShellNavigationTitlesTests.cs`, `tests/AuswertungPro.Next.UI.Tests/ShellNovaKopfzeileTests.cs`

**Interfaces:**
- Produces: `ShellNavigationTitles.Anzeige(string title)` („Uebersicht"→„Übersicht", „Schaechte"→„Schächte", sonst unverändert); `NavItem.DisplayTitle`; auf `ShellViewModel`: `string Brotkrume`, `string NaechsteAufgabeText`, `HaltungRecord? NaechsteAufgabe`, `IRelayCommand NaechsteAufgabePruefenCommand`, `string SpeicherstandText`, `void AktualisiereNovaKopfzeile()`.

- [ ] **Step 1: Tests (rot)**

```csharp
// ShellNavigationTitlesTests.cs
using AuswertungPro.Next.UI.ViewModels;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

public sealed class ShellNavigationTitlesTests
{
    [Theory]
    [InlineData("Uebersicht", "Übersicht")]
    [InlineData("Schaechte", "Schächte")]
    [InlineData("Haltungen", "Haltungen")]
    [InlineData("Sanierungs-Matrix", "Sanierungs-Matrix")]
    public void Anzeigename_traegt_echte_Umlaute(string title, string erwartet)
        => Assert.Equal(erwartet, ShellNavigationTitles.Anzeige(title));
}
```

```csharp
// ShellNovaKopfzeileTests.cs — reine Textregeln, kein Fenster
using AuswertungPro.Next.UI.ViewModels;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

public sealed class ShellNovaKopfzeileTests
{
    [Fact]
    public void Brotkrume_ist_Projekt_Schraegstrich_Seite()
        => Assert.Equal("Göschenen 2026 / Übersicht", ShellNovaKopfzeile.Brotkrume("Göschenen 2026", "Uebersicht"));

    [Fact]
    public void Brotkrume_ohne_Projekt_zeigt_nur_die_Seite()
        => Assert.Equal("Haltungen", ShellNovaKopfzeile.Brotkrume("", "Haltungen"));

    [Fact]
    public void Speicherstand_nennt_Projekt_und_Uhrzeit()
    {
        var t = new System.DateTime(2026, 9, 6, 14, 32, 0, System.DateTimeKind.Local);
        Assert.Equal("Göschenen 2026 · gespeichert 14:32", ShellNovaKopfzeile.Speicherstand("Göschenen 2026", t, false));
        Assert.Equal("Göschenen 2026 · ungespeichert", ShellNovaKopfzeile.Speicherstand("Göschenen 2026", t, true));
        Assert.Equal("Kein Projekt geöffnet", ShellNovaKopfzeile.Speicherstand("", null, false));
    }
}
```

- [ ] **Step 2: Rot** — Run: `dotnet test tests/AuswertungPro.Next.UI.Tests --filter "ShellNavigationTitles|ShellNovaKopfzeile"` → Kompilierfehler.

- [ ] **Step 3: Regeln und Partial schreiben**

```csharp
// ShellNavigationTitles.cs
namespace AuswertungPro.Next.UI.ViewModels;

/// <summary>Navigationstitel sind Schluessel (ASCII); die Leiste zeigt den Namen mit Umlauten.</summary>
public static class ShellNavigationTitles
{
    public static string Anzeige(string? title) => title switch
    {
        "Uebersicht" => "Übersicht",
        "Schaechte" => "Schächte",
        null => string.Empty,
        _ => title
    };
}

/// <summary>Reine Textregeln der Kopf- und Fusszeile (Inventar 3.2, 3.4).</summary>
public static class ShellNovaKopfzeile
{
    public static string Brotkrume(string? projekt, string? navTitle)
    {
        var seite = ShellNavigationTitles.Anzeige(navTitle);
        return string.IsNullOrWhiteSpace(projekt) ? seite : $"{projekt} / {seite}";
    }

    public static string Speicherstand(string? projekt, System.DateTime? gespeichertLokal, bool ungespeichert)
    {
        if (string.IsNullOrWhiteSpace(projekt))
            return "Kein Projekt geöffnet";
        if (ungespeichert)
            return $"{projekt} · ungespeichert";
        return gespeichertLokal is { } t ? $"{projekt} · gespeichert {t:HH:mm}" : projekt;
    }
}
```

`NavItem` (in `ShellViewModel.NavigationSupport.cs`): Eigenschaft `public string DisplayTitle => ShellNavigationTitles.Anzeige(Title);` ergänzen.

```csharp
// ShellViewModel.Nova.cs
using System;
using System.Collections.Specialized;
using AuswertungPro.Next.Application.UseCases.NaechsteAufgabe;
using AuswertungPro.Next.Domain.Models;
using CommunityToolkit.Mvvm.Input;

namespace AuswertungPro.Next.UI.ViewModels;

/// <summary>Nova-Etappe 2: Brotkrume, Aufgaben-Chip und Speicherstand der Kopf-/Fusszeile.</summary>
public partial class ShellViewModel
{
    private DateTime? _letzteSpeicherungLokal;

    public string Brotkrume => ShellNovaKopfzeile.Brotkrume(IsProjectReady ? Project.Name : null, SelectedNavItem?.Title);
    public HaltungRecord? NaechsteAufgabe { get; private set; }
    public string NaechsteAufgabeText => IsProjectReady ? NaechsteAufgabeRegel.ChipText(NaechsteAufgabe) : string.Empty;
    public string SpeicherstandText => ShellNovaKopfzeile.Speicherstand(IsProjectReady ? Project.Name : null, _letzteSpeicherungLokal, IsProjectReady && Project.Dirty);
    public IRelayCommand NaechsteAufgabePruefenCommand { get; private set; } = null!;

    private void InitNova()
    {
        NaechsteAufgabePruefenCommand = new RelayCommand(NaechsteAufgabePruefen, () => NaechsteAufgabe is not null);
        PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(SelectedNavItem) or nameof(IsProjectReady) or nameof(Project))
                AktualisiereNovaKopfzeile();
        };
    }

    /// <summary>Nach Projektwechsel, Speichern, Listenaenderung: alle drei Texte neu.</summary>
    public void AktualisiereNovaKopfzeile()
    {
        NaechsteAufgabe = IsProjectReady ? NaechsteAufgabeRegel.Naechste(Project.Data) : null;
        OnPropertyChanged(nameof(Brotkrume));
        OnPropertyChanged(nameof(NaechsteAufgabe));
        OnPropertyChanged(nameof(NaechsteAufgabeText));
        OnPropertyChanged(nameof(SpeicherstandText));
        NaechsteAufgabePruefenCommand?.NotifyCanExecuteChanged();
    }

    /// <summary>Erfolgreiches Speichern merken (Fusszeile "gespeichert HH:mm").</summary>
    private void MerkeSpeicherung()
    {
        _letzteSpeicherungLokal = DateTime.Now;
        OnPropertyChanged(nameof(SpeicherstandText));
    }

    /// <summary>Chip/Knopf: Haltung oeffnen und ihr Video pruefen (Inventar 8.1).</summary>
    private void NaechsteAufgabePruefen()
    {
        var record = NaechsteAufgabe;
        if (record is null)
            return;
        NavigateToHolding(record);
        if (CurrentPage is Pages.DataPageViewModel dataPage && dataPage.PlayVideoCommand.CanExecute(record))
            dataPage.PlayVideoCommand.Execute(record);
    }

    private void BeobachteHaltungsliste(Project p)
        => p.Data.CollectionChanged += (_, _) => AktualisiereNovaKopfzeile();
}
```
In `ShellViewModel.cs`: `InitNova();` direkt vor `EnterLauncher();`; in `RefreshTitleAndDirty()` am Ende `AktualisiereNovaKopfzeile();`; in `EnableCollectionSync(Project p)` am Ende `BeobachteHaltungsliste(p);`. In `ShellViewModel.ProjectSaving.cs` nach erfolgreichem `_sp.Projects.Save(...)` (direkt nach dem `if (!res.Ok) {...}`-Block) `MerkeSpeicherung();`.

- [ ] **Step 4: MainWindow-Kopfzeile**

In der Kopf-`DockPanel` (nach `<Menu>…</Menu>`) VOR dem Menü folgenden Block als rechten Teil einfügen (der Knopf „Projekt wechseln" bleibt `DockPanel.Dock="Right"`; danach kommt dieser Block ebenfalls rechts, damit Menü links bleibt):

```xml
            <!-- Nova-Etappe 2: Aufgaben-Chip (Inventar 3.4) -->
            <Button DockPanel.Dock="Right" Margin="0,2,8,2" Padding="12,4" MinHeight="0"
                    Command="{Binding NaechsteAufgabePruefenCommand}"
                    Style="{StaticResource ToolbarButton}"
                    Background="{DynamicResource KiSubtleBrush}" BorderBrush="{DynamicResource KiSubtleBrush}"
                    ToolTip="Nächste fachliche Aufgabe: Haltung öffnen und Video prüfen"
                    AutomationProperties.Name="Nächste fachliche Aufgabe">
                <Button.Visibility>
                    <Binding Path="IsProjectReady" Converter="{StaticResource BoolToVis}"/>
                </Button.Visibility>
                <StackPanel Orientation="Horizontal">
                    <ui:FluentIcon Glyph="&#xE768;" Foreground="{DynamicResource KiTextBrush}" Margin="0,0,6,0"/>
                    <TextBlock Text="{Binding NaechsteAufgabeText}" FontSize="{DynamicResource TextS}" FontWeight="SemiBold"
                               Foreground="{DynamicResource KiTextBrush}"/>
                </StackPanel>
            </Button>
            <!-- Brotkrume: Projekt / Seite -->
            <TextBlock DockPanel.Dock="Left" Text="{Binding Brotkrume}" Margin="12,0,8,0" VerticalAlignment="Center"
                       FontSize="{DynamicResource TextM}" FontWeight="SemiBold" Foreground="{DynamicResource TextBrush}"
                       TextTrimming="CharacterEllipsis"/>
```
Der `BoolToVis`-Konverter muss in `MainWindow.Resources` liegen (`<BooleanToVisibilityConverter x:Key="BoolToVis"/>`; ist er schon in App.xaml global registriert, nichts tun). Die Brotkrume steht rechts vom Menü: dazu die `Menu` mit `DockPanel.Dock="Left"` versehen, damit der `TextBlock` nach ihr folgt.

Navigation: `<TextBlock Text="{Binding Title}" Margin="10,0,0,0" …>` (Zeile ~560) → `Text="{Binding DisplayTitle}"`. Die `DataTrigger Binding="{Binding Title}"` für die Symbole bleiben (Schlüssel).

Fusszeile der Leiste (Inventar 3.2, unter dem Expander „Systemleistung"): nach dem `<Expander … DockPanel.Dock="Bottom">` einen zweiten Bottom-Block einfügen:

```xml
                <TextBlock DockPanel.Dock="Bottom" Margin="16,0,16,10" Text="{Binding SpeicherstandText}"
                           FontSize="{DynamicResource TextXS}" Foreground="{DynamicResource FaintBrush}"
                           TextTrimming="CharacterEllipsis" ToolTip="Projekt und Zeit der letzten Speicherung"/>
```

- [ ] **Step 5: Grün** — Run: `dotnet build AuswertungPro.sln && dotnet test tests/AuswertungPro.Next.UI.Tests --no-build --filter "ShellNavigationTitles|ShellNovaKopfzeile|ShellNavigationGroups|DesignAudit|XamlActionWiring|UiArchitecture"` → PASS. Fällt `Sichtbare_Texte_verwenden_echte_Umlaute` neu an, ist ein Text im XAML ohne Umlaut; korrigieren.

- [ ] **Step 6: Commit** — `git add -A src/AuswertungPro.Next.UI/ViewModels src/AuswertungPro.Next.UI/MainWindow.xaml tests/AuswertungPro.Next.UI.Tests/ShellNavigationTitlesTests.cs tests/AuswertungPro.Next.UI.Tests/ShellNovaKopfzeileTests.cs && git commit -m "Nova-Etappe 2: Brotkrume, Aufgaben-Chip, Speicherstand und Umlaute in der Leiste"`

### Task 6: Globale Suche Strg+K (Haltung, Schacht, Strasse)

**Files:**
- Create: `src/AuswertungPro.Next.Application/UseCases/Suche/GlobaleSucheRegel.cs`
- Create: `src/AuswertungPro.Next.UI/ViewModels/GlobaleSucheViewModel.cs`
- Modify: `src/AuswertungPro.Next.UI/ViewModels/ShellViewModel.Nova.cs` (Eigenschaft `GlobaleSuche`)
- Modify: `src/AuswertungPro.Next.UI/MainWindow.xaml` (Suchfeld mit Popup in der Kopfzeile, `KeyBinding Ctrl+K`)
- Modify: `src/AuswertungPro.Next.UI/MainWindow.xaml.cs` (Fokus-Handler `GlobaleSucheFokus_Click`, Tastenhandler)
- Test: `tests/AuswertungPro.Next.Infrastructure.Tests/GlobaleSucheRegelTests.cs`

**Interfaces:**
- Produces: `enum GlobaleSucheArt { Haltung, Schacht, Strasse }`; `record GlobaleSucheTreffer(GlobaleSucheArt Art, string Text, object? Ziel)` (Text z. B. „Haltung 78998-79002 · Seilergasse"); `static class GlobaleSucheRegel { IReadOnlyList<GlobaleSucheTreffer> Suche(string? text, IEnumerable<HaltungRecord>, IEnumerable<SchachtRecord>, Func<SchachtRecord,string> schachtNummer, int max = 12) }`.
- Regel (Inventar 8.5): Text klein/getrimmt; Haltung, wenn Name oder Strasse den Text enthält; Schacht, wenn Nummer oder Strasse; Strasse als eigener Treffer je verschiedener Strasse, die den Text enthält (Ziel = Strassenname); höchstens 12 in dieser Reihenfolge; leerer Text → leere Liste.

- [ ] **Step 1: Test (rot)**

```csharp
using AuswertungPro.Next.Application.UseCases.Suche;
using AuswertungPro.Next.Domain.Models;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class GlobaleSucheRegelTests
{
    private static HaltungRecord H(string name, string strasse)
    {
        var r = new HaltungRecord();
        r.SetFieldValue(FieldKeys.HoldingName, name, FieldSource.Manual, false);
        r.SetFieldValue(FieldKeys.Street, strasse, FieldSource.Manual, false);
        return r;
    }
    private static SchachtRecord S(string nummer, string strasse)
    {
        var r = new SchachtRecord();
        r.Fields["Schachtnummer"] = nummer; r.Fields["Strasse"] = strasse;
        return r;
    }

    [Fact]
    public void Findet_Haltung_Schacht_und_Strasse_in_dieser_Reihenfolge()
    {
        var treffer = GlobaleSucheRegel.Suche("seiler", new[] { H("78998-79002", "Seilergasse") }, new[] { S("78998", "Seilergasse") }, s => s.Fields["Schachtnummer"]);
        Assert.Collection(treffer,
            t => { Assert.Equal(GlobaleSucheArt.Haltung, t.Art); Assert.Equal("Haltung 78998-79002 · Seilergasse", t.Text); },
            t => { Assert.Equal(GlobaleSucheArt.Schacht, t.Art); Assert.Equal("Schacht 78998 · Seilergasse", t.Text); },
            t => { Assert.Equal(GlobaleSucheArt.Strasse, t.Art); Assert.Equal("Strasse Seilergasse", t.Text); Assert.Equal("Seilergasse", t.Ziel); });
    }

    [Fact]
    public void Hoechstens_zwoelf_Treffer_und_leerer_Text_liefert_nichts()
    {
        var viele = System.Linq.Enumerable.Range(0, 30).Select(i => H($"{i}-{i + 1}", "Teststrasse")).ToList();
        Assert.Equal(12, GlobaleSucheRegel.Suche("test", viele, System.Array.Empty<SchachtRecord>(), s => "").Count);
        Assert.Empty(GlobaleSucheRegel.Suche("  ", viele, System.Array.Empty<SchachtRecord>(), s => ""));
    }
}
```

- [ ] **Step 2: Rot** — `dotnet test tests/AuswertungPro.Next.Infrastructure.Tests --filter GlobaleSucheRegelTests` → Kompilierfehler.

- [ ] **Step 3: Regel und ViewModel**

```csharp
// GlobaleSucheRegel.cs
using System;
using System.Collections.Generic;
using System.Linq;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.UseCases.Suche;

public enum GlobaleSucheArt { Haltung, Schacht, Strasse }

public sealed record GlobaleSucheTreffer(GlobaleSucheArt Art, string Text, object? Ziel);

/// <summary>Inventar 8.5: Treffer Haltung, Schacht, Strasse; hoechstens 12.</summary>
public static class GlobaleSucheRegel
{
    public static IReadOnlyList<GlobaleSucheTreffer> Suche(
        string? text,
        IEnumerable<HaltungRecord> haltungen,
        IEnumerable<SchachtRecord> schaechte,
        Func<SchachtRecord, string> schachtNummer,
        int max = 12)
    {
        var q = (text ?? string.Empty).Trim();
        if (q.Length == 0)
            return Array.Empty<GlobaleSucheTreffer>();
        bool Passt(string? s) => !string.IsNullOrEmpty(s) && s.Contains(q, StringComparison.OrdinalIgnoreCase);

        var ergebnis = new List<GlobaleSucheTreffer>();
        var strassen = new List<string>();
        foreach (var h in haltungen)
        {
            var name = h.GetFieldValue(FieldKeys.HoldingName);
            var strasse = h.GetFieldValue(FieldKeys.Street);
            if (Passt(name) || Passt(strasse))
                ergebnis.Add(new(GlobaleSucheArt.Haltung, string.IsNullOrWhiteSpace(strasse) ? $"Haltung {name}" : $"Haltung {name} · {strasse}", h));
            if (Passt(strasse) && !strassen.Contains(strasse!, StringComparer.OrdinalIgnoreCase))
                strassen.Add(strasse!);
        }
        foreach (var s in schaechte)
        {
            var nummer = schachtNummer(s);
            var strasse = s.GetFieldValue("Strasse");
            if (Passt(nummer) || Passt(strasse))
                ergebnis.Add(new(GlobaleSucheArt.Schacht, string.IsNullOrWhiteSpace(strasse) ? $"Schacht {nummer}" : $"Schacht {nummer} · {strasse}", s));
            if (Passt(strasse) && !strassen.Contains(strasse!, StringComparer.OrdinalIgnoreCase))
                strassen.Add(strasse!);
        }
        ergebnis.AddRange(strassen.Select(st => new GlobaleSucheTreffer(GlobaleSucheArt.Strasse, $"Strasse {st}", st)));
        return ergebnis.Take(max).ToList();
    }
}
```
Prüfen, ob `SchachtRecord.GetFieldValue(string)` existiert (Zeile 43 der Klasse): ja.

```csharp
// GlobaleSucheViewModel.cs
using System.Collections.ObjectModel;
using AuswertungPro.Next.Application.UseCases.Suche;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.DataPage;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AuswertungPro.Next.UI.ViewModels;

/// <summary>Suchfeld der Kopfzeile (Strg+K). Sucht live; ein gewaehlter Treffer springt zur Seite.</summary>
public sealed partial class GlobaleSucheViewModel : ObservableObject
{
    private readonly ShellViewModel _shell;
    [ObservableProperty] private string _text = string.Empty;
    [ObservableProperty] private GlobaleSucheTreffer? _gewaehlt;
    [ObservableProperty] private bool _listeOffen;
    public ObservableCollection<GlobaleSucheTreffer> Treffer { get; } = new();
    public string LeerText => Text.Trim().Length == 0 ? "Name, Nummer oder Strasse eingeben." : Treffer.Count == 0 ? "Kein Treffer" : string.Empty;

    public GlobaleSucheViewModel(ShellViewModel shell) => _shell = shell;

    partial void OnTextChanged(string value)
    {
        Treffer.Clear();
        if (_shell.IsProjectReady)
            foreach (var t in GlobaleSucheRegel.Suche(value, _shell.Project.Data, _shell.Project.SchaechteData, SchaechteColumnPolicy.GetSchachtNumber))
                Treffer.Add(t);
        ListeOffen = value.Trim().Length > 0;
        OnPropertyChanged(nameof(LeerText));
    }

    partial void OnGewaehltChanged(GlobaleSucheTreffer? value)
    {
        if (value is null) return;
        switch (value.Art)
        {
            case GlobaleSucheArt.Haltung: _shell.NavigateToHolding(value.Ziel as HaltungRecord); break;
            case GlobaleSucheArt.Schacht: _shell.NavigateToShaft(value.Ziel as SchachtRecord); break;
            case GlobaleSucheArt.Strasse: _shell.NavigateToDataPage(new DataPageStartFilter(FieldKeys.Street, (string)value.Ziel!)); break;
        }
        ListeOffen = false;
        Text = string.Empty;
    }

    /// <summary>Enter im Feld: markierten oder ersten Treffer waehlen (Inventar 3.5).</summary>
    public void WaehleErstenOderMarkierten()
    {
        if (Gewaehlt is null && Treffer.Count > 0)
            Gewaehlt = Treffer[0];
    }
}
```
Prüfen, ob `DataPageStartFilter(FieldName, Value)` mit `Strasse` von der Datenseite als Filter verstanden wird (Datei `DataPage/DataPageStartFilter.cs`, `DisplayText`-Switch): fehlt der Fall, dort `"Strasse" => $"Strasse {Value}"` ergänzen. In `ShellViewModel.Nova.cs`: `public GlobaleSucheViewModel GlobaleSuche { get; private set; } = null!;` und in `InitNova()` `GlobaleSuche = new GlobaleSucheViewModel(this);`.

- [ ] **Step 4: Kopfzeile**

In `MainWindow.xaml` vor dem Aufgaben-Chip (also weiter rechts als die Brotkrume) einfügen:

```xml
            <Grid DockPanel.Dock="Right" Margin="0,2,8,2" MinWidth="300" VerticalAlignment="Center">
                <Border Background="{DynamicResource CardBrush}" BorderBrush="{DynamicResource BorderBrush}" BorderThickness="1"
                        CornerRadius="{DynamicResource RadiusPill}" Padding="10,2">
                    <DockPanel>
                        <ui:FluentIcon DockPanel.Dock="Left" Glyph="&#xE721;" Foreground="{DynamicResource MutedBrush}" Margin="0,0,6,0"/>
                        <Border DockPanel.Dock="Right" Background="{DynamicResource SurfaceSubtleBrush}" CornerRadius="{DynamicResource RadiusS}" Padding="6,1">
                            <TextBlock Text="Strg K" FontSize="{DynamicResource TextXS}" FontFamily="{DynamicResource FontMono}" Foreground="{DynamicResource MutedBrush}"/>
                        </Border>
                        <TextBox x:Name="GlobaleSucheBox" BorderThickness="0" Background="Transparent" MinWidth="200"
                                 Text="{Binding GlobaleSuche.Text, UpdateSourceTrigger=PropertyChanged}"
                                 KeyDown="GlobaleSucheBox_KeyDown"
                                 ToolTip="Haltung, Schacht oder Strasse suchen" AutomationProperties.Name="Haltung, Schacht oder Strasse suchen"/>
                    </DockPanel>
                </Border>
                <Popup IsOpen="{Binding GlobaleSuche.ListeOffen, Mode=TwoWay}" StaysOpen="False" PlacementTarget="{Binding ElementName=GlobaleSucheBox}"
                       Placement="Bottom" AllowsTransparency="True">
                    <Border Background="{DynamicResource CardBrush}" BorderBrush="{DynamicResource BorderBrush}" BorderThickness="1"
                            CornerRadius="{DynamicResource RadiusM}" MinWidth="320" MaxHeight="320" Padding="4">
                        <StackPanel>
                            <ListBox ItemsSource="{Binding GlobaleSuche.Treffer}" SelectedItem="{Binding GlobaleSuche.Gewaehlt, Mode=TwoWay}"
                                     DisplayMemberPath="Text" BorderThickness="0" AutomationProperties.Name="Suchtreffer"/>
                            <TextBlock Text="{Binding GlobaleSuche.LeerText}" Margin="8,4" Foreground="{DynamicResource MutedBrush}"
                                       FontSize="{DynamicResource TextS}"/>
                        </StackPanel>
                    </Border>
                </Popup>
            </Grid>
```
`Window.InputBindings`: `<KeyBinding Key="K" Modifiers="Control" Command="{Binding GlobaleSucheFokusCommand}"/>` — dafür in `ShellViewModel.Nova.cs` `public IRelayCommand GlobaleSucheFokusCommand { get; private set; }` (in `InitNova` mit `new RelayCommand(() => GlobaleSucheFokusAngefordert?.Invoke())`) und `public event Action? GlobaleSucheFokusAngefordert;`. In `MainWindow.xaml.cs` beim Setzen des DataContext: `vm.GlobaleSucheFokusAngefordert += () => { GlobaleSucheBox.Focus(); GlobaleSucheBox.SelectAll(); };` und

```csharp
    private void GlobaleSucheBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is not ShellViewModel vm) return;
        if (e.Key == Key.Enter) { vm.GlobaleSuche.WaehleErstenOderMarkierten(); e.Handled = true; }
        else if (e.Key == Key.Escape) { vm.GlobaleSuche.ListeOffen = false; e.Handled = true; }
    }
```
Ist ein modales Fenster offen, erreicht Strg+K das Hauptfenster nicht (WPF-Modalität); das entspricht dem Prototyp-Verhalten ohne Zusatzcode.

- [ ] **Step 5: Grün** — `dotnet build AuswertungPro.sln && dotnet test tests/AuswertungPro.Next.Infrastructure.Tests --no-build --filter GlobaleSucheRegelTests && dotnet test tests/AuswertungPro.Next.UI.Tests --no-build --filter "DesignAudit|XamlActionWiring|UiArchitecture"` → PASS.

- [ ] **Step 6: Commit** — `git add -A src/AuswertungPro.Next.Application/UseCases/Suche src/AuswertungPro.Next.UI/ViewModels src/AuswertungPro.Next.UI/MainWindow.xaml src/AuswertungPro.Next.UI/MainWindow.xaml.cs src/AuswertungPro.Next.UI/DataPage/DataPageStartFilter.cs tests/AuswertungPro.Next.Infrastructure.Tests/GlobaleSucheRegelTests.cs && git commit -m "Nova-Etappe 2: globale Suche Strg+K nach Haltung, Schacht und Strasse"`

### Task 7: KI-Bereitschaft in der Leiste („Analyse bereit")

**Files:**
- Create: `src/AuswertungPro.Next.UI/ViewModels/KiBereitschaftRegel.cs`
- Modify: `src/AuswertungPro.Next.UI/ViewModels/ShellViewModel.Nova.cs` (`KiBereitschaftText`, `IstKiBereit`)
- Modify: `src/AuswertungPro.Next.UI/MainWindow.xaml` (Expander-Kopf)
- Test: `tests/AuswertungPro.Next.UI.Tests/KiBereitschaftRegelTests.cs`

**Interfaces:**
- Produces: `enum KiBereitschaft { NichtGestartet, Startet, Bereit, PruefungNoetig }`; `static class KiBereitschaftRegel { KiBereitschaft Bestimme(AiRuntimeStatus s); string Text(KiBereitschaft b); }`. Quelle ist `AiRuntimeStatusTracker` (Titel „KI STARTET" / „KI BEREIT" / „KI WARNUNG", `IsVisible=false` = nicht gestartet). Texte nach BEWERTUNG N10: „KI nicht gestartet", „KI startet", „Analyse bereit", „Prüfung nötig".

- [ ] **Step 1: Test (rot)**

```csharp
using AuswertungPro.Next.UI.Services;
using AuswertungPro.Next.UI.ViewModels;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

public sealed class KiBereitschaftRegelTests
{
    [Theory]
    [InlineData(false, "", KiBereitschaft.NichtGestartet, "KI nicht gestartet")]
    [InlineData(true, "KI STARTET", KiBereitschaft.Startet, "KI startet")]
    [InlineData(true, "KI BEREIT", KiBereitschaft.Bereit, "Analyse bereit")]
    [InlineData(true, "KI WARNUNG", KiBereitschaft.PruefungNoetig, "Prüfung nötig")]
    public void Leistentext_folgt_dem_Laufzeitstatus(bool sichtbar, string titel, KiBereitschaft erwartet, string text)
    {
        var stand = KiBereitschaftRegel.Bestimme(new AiRuntimeStatus(sichtbar, titel, "", ""));
        Assert.Equal(erwartet, stand);
        Assert.Equal(text, KiBereitschaftRegel.Text(stand));
    }
}
```

- [ ] **Step 2: Rot** — `dotnet test tests/AuswertungPro.Next.UI.Tests --filter KiBereitschaftRegelTests` → Kompilierfehler.

- [ ] **Step 3: Regel**

```csharp
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.ViewModels;

public enum KiBereitschaft { NichtGestartet, Startet, Bereit, PruefungNoetig }

/// <summary>BEWERTUNG N10: im Alltag "Analyse bereit", "Pruefung noetig" statt Modellnamen.</summary>
public static class KiBereitschaftRegel
{
    public static KiBereitschaft Bestimme(AiRuntimeStatus status) => status switch
    {
        { IsVisible: false } => KiBereitschaft.NichtGestartet,
        { Title: "KI STARTET" } => KiBereitschaft.Startet,
        { Title: "KI WARNUNG" } => KiBereitschaft.PruefungNoetig,
        _ => KiBereitschaft.Bereit
    };

    public static string Text(KiBereitschaft b) => b switch
    {
        KiBereitschaft.NichtGestartet => "KI nicht gestartet",
        KiBereitschaft.Startet => "KI startet",
        KiBereitschaft.PruefungNoetig => "Prüfung nötig",
        _ => "Analyse bereit"
    };
}
```
In `ShellViewModel.Nova.cs`: `public string KiBereitschaftText => KiBereitschaftRegel.Text(KiBereitschaftRegel.Bestimme(AiRuntimeStatusTracker.Current));` und `public bool IstKiBereit => KiBereitschaftRegel.Bestimme(AiRuntimeStatusTracker.Current) == KiBereitschaft.Bereit;`. In `ApplyAiRuntimeStatus` (ShellViewModel.cs) am Ende zwei Zeilen: `OnPropertyChanged(nameof(KiBereitschaftText)); OnPropertyChanged(nameof(IstKiBereit));`.

- [ ] **Step 4: Expander-Kopf**

Im `Expander.Header` von MainWindow.xaml den `TextBlock`-Style so ändern, dass der Standardtext `{Binding KiBereitschaftText}` ist und der Fall `Monitor.IsSensorBlocked` weiter „Sensoren gesperrt" zeigt:

```xml
                            <TextBlock FontSize="{DynamicResource TextS}" FontWeight="SemiBold" Foreground="{DynamicResource TextBrush}"
                                       Text="{Binding KiBereitschaftText}">
                                <TextBlock.Style>
                                    <Style TargetType="TextBlock">
                                        <Style.Triggers>
                                            <DataTrigger Binding="{Binding Monitor.IsSensorBlocked}" Value="True">
                                                <Setter Property="Text" Value="Sensoren gesperrt"/>
                                            </DataTrigger>
                                        </Style.Triggers>
                                    </Style>
                                </TextBlock.Style>
                            </TextBlock>
```
Der `NeuralPulseDot` erhält `IsActive="{Binding IstKiBereit}"` (der bestehende Style mit dem Sensor-Trigger bleibt; der Setter `IsActive True` wird durch die Bindung ersetzt). `AutomationProperties.Name` des Expanders → „KI-Bereitschaft und Systemleistung"; `ToolTip` → „KI-Bereitschaft; aufgeklappt: Rechnerauslastung und Sensoren". Im Inhalt über dem `SystemMonitorPanel` eine Zeile `<TextBlock Text="{Binding AiDisplayStatusLabel}" FontSize="{DynamicResource TextXS}" Foreground="{DynamicResource MutedBrush}" TextWrapping="Wrap" Margin="0,4,0,4"/>` (Modell- und Detailtext nur aufgeklappt, N10).

- [ ] **Step 5: Grün** — `dotnet build AuswertungPro.sln && dotnet test tests/AuswertungPro.Next.UI.Tests --no-build --filter "KiBereitschaft|DesignAudit|ShellNavigation"` → PASS.

- [ ] **Step 6: Commit** — `git add -A src/AuswertungPro.Next.UI/ViewModels src/AuswertungPro.Next.UI/MainWindow.xaml tests/AuswertungPro.Next.UI.Tests/KiBereitschaftRegelTests.cs && git commit -m "Nova-Etappe 2: KI-Bereitschaft als Kopf des Leisten-Aufklappers"`

---
## Teil C — Übersichtsseite im Projekt

### Task 8: Kennzahlen der Projektübersicht (WPF-frei)

**Files:**
- Create: `src/AuswertungPro.Next.Application/UseCases/Uebersicht/ProjektUebersichtKennzahlen.cs`
- Test: `tests/AuswertungPro.Next.Infrastructure.Tests/ProjektUebersichtKennzahlenTests.cs`

**Interfaces:**
- Consumes: `HaltungPruefstatus` (Task 4), `DashboardStatistics` (vorhanden: `HoldingCount`, `SchachtCount`, `HaltungSanierungsKosten`, `SchachtSanierungsKosten`, `ConditionClasses`, `TopSchaeden`, `Sanierungsverfahren`).
- Produces: `sealed record ProjektUebersichtKennzahlen(int Haltungen, int Geprueft, int KiAnalysiert, int Offen, double GesamtlaengeM, int Schaechte, int SchaechteMitProtokoll, int DringendHaltungen, int DringendSchaechte, IReadOnlyList<StammdatenVollstaendigkeit> Stammdaten)`; `sealed record StammdatenVollstaendigkeit(string Feld, int Gefuellt, int Gesamt)` mit `Prozent` und `Stufe` (`Z4` ab 95 %, `Z3` ab 70 %, sonst `Z2`, Inventar 4.1 Punkt 4); `static class ProjektUebersichtRechner { ProjektUebersichtKennzahlen Berechne(Project p); string HeroText(ProjektUebersichtKennzahlen k); }`.
- Hero-Text (Inventar 4.1): „`<gepr>` von `<n>` Haltungen fachlich geprüft (`<x,x %>`). `<anal>` von der KI analysiert und noch nicht geprüft, `<off>` ohne Analyse. `<dring>` Haltungen dringend (Z0 oder Z1). Bestand mit `<n>` Haltungen und `<ns>` Schächten." — Prozent mit einer Nachkommastelle und Komma (de-CH: `CultureInfo.GetCultureInfo("de-CH")` formatiert `57,1`? de-CH verwendet den Punkt als Dezimaltrenner: `57.1`. Der Prototyp zeigt `57,1 %`; wir verwenden de-CH, also `57.1 %` — dokumentierte, bewusste Abweichung zugunsten der Programmkonvention „de-CH formatiert mit Punkt" aus Memory).

- [ ] **Step 1: Test (rot)**

```csharp
using AuswertungPro.Next.Application.UseCases.Uebersicht;
using AuswertungPro.Next.Domain.Models;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class ProjektUebersichtKennzahlenTests
{
    private static Project Projekt()
    {
        var p = new Project { Name = "Test" };
        p.EnsureMetadataDefaults();
        void H(string name, string status, string zk, string laenge, string material, string link)
        {
            var r = p.CreateNewRecord();
            r.SetFieldValue(FieldKeys.HoldingName, name, FieldSource.Manual, false);
            r.SetFieldValue(FieldKeys.WorkflowStatus, status, FieldSource.Manual, false);
            r.SetFieldValue(FieldKeys.ConditionClass, zk, FieldSource.Manual, false);
            r.SetFieldValue(FieldKeys.HoldingLengthMeters, laenge, FieldSource.Manual, false);
            r.SetFieldValue(FieldKeys.PipeMaterial, material, FieldSource.Manual, false);
            r.SetFieldValue(FieldKeys.NominalDiameterMm, "300", FieldSource.Manual, false);
            r.SetFieldValue(FieldKeys.Link, link, FieldSource.Manual, false);
            p.AddRecord(r);
        }
        H("1-2", "abgeschlossen", "0", "10", "Beton", "a.mp4");
        H("2-3", "", "1", "20.5", "", "b.mp4");
        H("3-4", "", "4", "", "Beton", "");
        var s = new SchachtRecord(); s.Fields["Schachtnummer"] = "1"; s.Fields["Zustandsklasse"] = "1"; s.Fields["PDF_Path"] = "x.pdf";
        p.SchaechteData.Add(s);
        var s2 = new SchachtRecord(); s2.Fields["Schachtnummer"] = "2"; s2.Fields["Zustandsklasse"] = "3";
        p.SchaechteData.Add(s2);
        return p;
    }

    [Fact]
    public void Zaehlt_Pruefstand_Laenge_Protokolle_und_Dringende()
    {
        var k = ProjektUebersichtRechner.Berechne(Projekt());
        Assert.Equal(3, k.Haltungen);
        Assert.Equal(1, k.Geprueft);
        Assert.Equal(0, k.KiAnalysiert);
        Assert.Equal(2, k.Offen);
        Assert.Equal(30.5, k.GesamtlaengeM, 3);
        Assert.Equal(2, k.Schaechte);
        Assert.Equal(1, k.SchaechteMitProtokoll);
        Assert.Equal(2, k.DringendHaltungen);
        Assert.Equal(1, k.DringendSchaechte);
    }

    [Fact]
    public void Stammdaten_Vollstaendigkeit_je_Feld_mit_Stufe()
    {
        var k = ProjektUebersichtRechner.Berechne(Projekt());
        var material = Assert.Single(k.Stammdaten, s => s.Feld == "Material");
        Assert.Equal(2, material.Gefuellt); Assert.Equal(3, material.Gesamt); Assert.Equal("Z2", material.Stufe);
        var dn = Assert.Single(k.Stammdaten, s => s.Feld == "DN");
        Assert.Equal("Z4", dn.Stufe);
    }

    [Fact]
    public void Hero_Text_nennt_alle_Zahlen()
    {
        var k = ProjektUebersichtRechner.Berechne(Projekt());
        Assert.Equal("1 von 3 Haltungen fachlich geprüft (33.3 %). 0 von der KI analysiert und noch nicht geprüft, 2 ohne Analyse. 2 Haltungen dringend (Z0 oder Z1). Bestand mit 3 Haltungen und 2 Schächten.",
            ProjektUebersichtRechner.HeroText(k));
    }
}
```
Falls `Project.CreateNewRecord()`/`AddRecord`/`EnsureMetadataDefaults` andere Namen tragen: die Namen aus dem Prüfhost `docs/reviews/2026-09-06-nova/wpf-etappe-1/abschluss/werkzeug/Program.cs` (dort verwendet) übernehmen.

- [ ] **Step 2: Rot** — `dotnet test tests/AuswertungPro.Next.Infrastructure.Tests --filter ProjektUebersichtKennzahlenTests`.

- [ ] **Step 3: Rechner**

```csharp
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using AuswertungPro.Next.Application.UseCases.NaechsteAufgabe;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.UseCases.Uebersicht;

public sealed record StammdatenVollstaendigkeit(string Feld, int Gefuellt, int Gesamt)
{
    public double Prozent => Gesamt == 0 ? 0 : 100.0 * Gefuellt / Gesamt;
    /// <summary>Inventar 4.1: Z4 ab 95 %, Z3 ab 70 %, sonst Z2 (Farbstufe des Balkens).</summary>
    public string Stufe => Prozent >= 95 ? "Z4" : Prozent >= 70 ? "Z3" : "Z2";
}

public sealed record ProjektUebersichtKennzahlen(
    int Haltungen, int Geprueft, int KiAnalysiert, int Offen, double GesamtlaengeM,
    int Schaechte, int SchaechteMitProtokoll, int DringendHaltungen, int DringendSchaechte,
    IReadOnlyList<StammdatenVollstaendigkeit> Stammdaten);

/// <summary>Nova-Etappe 2 (Inventar 4.1): alle Zahlen der Uebersicht aus demselben Bestand (BEWERTUNG N04).</summary>
public static class ProjektUebersichtRechner
{
    private static readonly CultureInfo DeCh = CultureInfo.GetCultureInfo("de-CH");

    public static ProjektUebersichtKennzahlen Berechne(Project projekt)
    {
        ArgumentNullException.ThrowIfNull(projekt);
        var h = projekt.Data.ToList();
        var s = projekt.SchaechteData.ToList();
        var stand = h.Select(HaltungPruefstatus.Bestimme).ToList();
        static bool Dringend(string? zk) => zk?.Trim() is "0" or "1";
        static bool Gefuellt(string? v) => !string.IsNullOrWhiteSpace(v);
        double Laenge(HaltungRecord r) => double.TryParse((r.GetFieldValue(FieldKeys.HoldingLengthMeters) ?? "").Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var d) && d > 0 ? d : 0;

        StammdatenVollstaendigkeit St(string feld, Func<HaltungRecord, string?> wert)
            => new(feld, h.Count(r => Gefuellt(wert(r))), h.Count);

        return new ProjektUebersichtKennzahlen(
            Haltungen: h.Count,
            Geprueft: stand.Count(x => x == HaltungPruefstand.Abgeschlossen),
            KiAnalysiert: stand.Count(x => x == HaltungPruefstand.KiAnalysiert),
            Offen: stand.Count(x => x == HaltungPruefstand.Offen),
            GesamtlaengeM: h.Sum(Laenge),
            Schaechte: s.Count,
            SchaechteMitProtokoll: s.Count(x => Gefuellt(x.GetFieldValue(FieldKeys.PdfPath))),
            DringendHaltungen: h.Count(r => Dringend(r.GetFieldValue(FieldKeys.ConditionClass))),
            DringendSchaechte: s.Count(x => Dringend(x.GetFieldValue(FieldKeys.ConditionClass))),
            Stammdaten: new[]
            {
                St("Material", r => r.GetFieldValue(FieldKeys.PipeMaterial)),
                St("DN", r => r.GetFieldValue(FieldKeys.NominalDiameterMm)),
                St("Baujahr", r => r.GetFieldValue(FieldKeys.ConstructionYear)),
                St("GEONIS", r => r.Geonis?.Haltung ?? r.GetFieldValue(FieldKeys.GeonisId))
            });
    }

    public static string HeroText(ProjektUebersichtKennzahlen k)
    {
        var prozent = k.Haltungen == 0 ? 0 : 100.0 * k.Geprueft / k.Haltungen;
        return $"{k.Geprueft} von {k.Haltungen} Haltungen fachlich geprüft ({prozent.ToString("0.0", DeCh)} %). " +
               $"{k.KiAnalysiert} von der KI analysiert und noch nicht geprüft, {k.Offen} ohne Analyse. " +
               $"{k.DringendHaltungen} Haltungen dringend (Z0 oder Z1). " +
               $"Bestand mit {k.Haltungen} Haltungen und {k.Schaechte} Schächten.";
    }
}
```
`GeonisKennungen.Haltung`: den echten Eigenschaftsnamen in `Domain/Models/GeonisKennungen.cs` nachsehen (die Hauptkennung der Haltung); passt er nicht, nur das Feld `FieldKeys.GeonisId` verwenden.

- [ ] **Step 4: Grün** — `dotnet test tests/AuswertungPro.Next.Infrastructure.Tests --filter ProjektUebersichtKennzahlenTests` → PASS.
- [ ] **Step 5: Commit** — `git add src/AuswertungPro.Next.Application/UseCases/Uebersicht tests/AuswertungPro.Next.Infrastructure.Tests/ProjektUebersichtKennzahlenTests.cs && git commit -m "Nova-Etappe 2: Kennzahlen der Projektuebersicht aus einem Bestand"`

### Task 9: Sitzungsregister der KI-Vorabdurchläufe

**Files:**
- Create: `src/AuswertungPro.Next.Application/UseCases/CodingSuggestions/ICodingSuggestionRegistry.cs`
- Create: `src/AuswertungPro.Next.Application/UseCases/CodingSuggestions/CodingSuggestionRegistry.cs`
- Modify: `src/AuswertungPro.Next.UI/ServiceProvider.cs` (Eigenschaft + `new`), `src/AuswertungPro.Next.UI/ServiceProviderRegistrationMap.cs` (Eintrag), `tests/AuswertungPro.Next.UI.Tests/ServiceProviderRegistrationTests.cs` (Zähler 143 → 144 mit Kommentar)
- Modify: `src/AuswertungPro.Next.UI/Views/Windows/PlayerWindow.Coding.Suggestions.cs` (nach `var set = await service.ScanAsync(...)`: `provider.CodingSuggestionRegistry.Merke(request.Haltung, set);`)
- Test: `tests/AuswertungPro.Next.Infrastructure.Tests/CodingSuggestionRegistryTests.cs`

**Interfaces:**
- Produces: `sealed record CodingSuggestionRun(string Haltung, DateTimeOffset Zeitpunkt, CodingSuggestionSet Set)`; `interface ICodingSuggestionRegistry { void Merke(string haltung, CodingSuggestionSet set); IReadOnlyList<CodingSuggestionRun> Heute(); event Action? Geaendert; }`. Nur Arbeitsspeicher, je Programmlauf; „Heute" = Läufe seit Programmstart, jüngster zuerst, je Haltung nur der letzte.

- [ ] **Step 1: Test (rot)**

```csharp
using AuswertungPro.Next.Application.UseCases.CodingSuggestions;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class CodingSuggestionRegistryTests
{
    [Fact]
    public void Merkt_je_Haltung_nur_den_letzten_Lauf_juengster_zuerst()
    {
        var reg = new CodingSuggestionRegistry();
        var n = 0; reg.Geaendert += () => n++;
        reg.Merke("1-2", CodingSuggestionSet.Leer("a"));
        reg.Merke("3-4", CodingSuggestionSet.Leer("b"));
        reg.Merke("1-2", CodingSuggestionSet.Leer("c"));
        var heute = reg.Heute();
        Assert.Equal(2, heute.Count);
        Assert.Equal("1-2", heute[0].Haltung);
        Assert.Equal("c", heute[0].Set.BogenTeil.Grund);
        Assert.Equal(3, n);
    }

    [Fact]
    public void Leere_Haltung_wird_nicht_gemerkt()
    {
        var reg = new CodingSuggestionRegistry();
        reg.Merke(" ", CodingSuggestionSet.Leer("a"));
        Assert.Empty(reg.Heute());
    }
}
```
`CodingSuggestionPartState.Grund`: den echten Namen der Begründung in `CodingSuggestionModels.cs` prüfen (`NichtVerfuegbar(grund)` erzeugt ihn); Test entsprechend benennen.

- [ ] **Step 2: Rot**, dann **Step 3: Implementierung**

```csharp
// ICodingSuggestionRegistry.cs
using System;
using System.Collections.Generic;

namespace AuswertungPro.Next.Application.UseCases.CodingSuggestions;

public sealed record CodingSuggestionRun(string Haltung, DateTimeOffset Zeitpunkt, CodingSuggestionSet Set);

/// <summary>
/// Sitzungsgedaechtnis der KI-Vorabdurchlaeufe fuer die Uebersicht (Inventar 4.1, Karte
/// "KI-Vorabdurchlauf"). Nur Arbeitsspeicher; nichts wird gespeichert oder als Gold gewertet.
/// </summary>
public interface ICodingSuggestionRegistry
{
    void Merke(string haltung, CodingSuggestionSet set);
    IReadOnlyList<CodingSuggestionRun> Heute();
    event Action? Geaendert;
}

// CodingSuggestionRegistry.cs
using System;
using System.Collections.Generic;
using System.Linq;

namespace AuswertungPro.Next.Application.UseCases.CodingSuggestions;

public sealed class CodingSuggestionRegistry : ICodingSuggestionRegistry
{
    private readonly object _gate = new();
    private readonly Dictionary<string, CodingSuggestionRun> _runs = new(StringComparer.OrdinalIgnoreCase);
    public event Action? Geaendert;

    public void Merke(string haltung, CodingSuggestionSet set)
    {
        ArgumentNullException.ThrowIfNull(set);
        var key = (haltung ?? string.Empty).Trim();
        if (key.Length == 0) return;
        lock (_gate)
            _runs[key] = new CodingSuggestionRun(key, DateTimeOffset.Now, set);
        Geaendert?.Invoke();
    }

    public IReadOnlyList<CodingSuggestionRun> Heute()
    {
        lock (_gate)
            return _runs.Values.OrderByDescending(r => r.Zeitpunkt).ToList();
    }
}
```
ServiceProvider: neben `CodingSuggestionExposure` (Zeile ~295/698) `public ICodingSuggestionRegistry CodingSuggestionRegistry { get; }` und `CodingSuggestionRegistry = new CodingSuggestionRegistry();`; in `ServiceProviderRegistrationMap` neben Zeile 105 `[typeof(ICodingSuggestionRegistry)] = services.CodingSuggestionRegistry,`. Im Zähltest den erwarteten Wert um eins erhöhen und die Kommentarkette fortführen („143 -> 144: ICodingSuggestionRegistry, Nova-Etappe 2").

- [ ] **Step 4: Grün** — `dotnet build AuswertungPro.sln && dotnet test tests/AuswertungPro.Next.Infrastructure.Tests --no-build --filter CodingSuggestionRegistryTests && dotnet test tests/AuswertungPro.Next.UI.Tests --no-build --filter "ServiceProviderRegistration|UiAiFreeze|UiArchitecture"` → PASS.
- [ ] **Step 5: Commit** — `git add -A src/AuswertungPro.Next.Application/UseCases/CodingSuggestions src/AuswertungPro.Next.UI/ServiceProvider.cs src/AuswertungPro.Next.UI/ServiceProviderRegistrationMap.cs src/AuswertungPro.Next.UI/Views/Windows/PlayerWindow.Coding.Suggestions.cs tests && git commit -m "Nova-Etappe 2: Sitzungsregister der KI-Vorabdurchlaeufe"`

### Task 10: Seite „Übersicht" im Projekt

**Files:**
- Create: `src/AuswertungPro.Next.UI/ViewModels/Pages/ProjektUebersichtPageViewModel.cs`
- Create: `src/AuswertungPro.Next.UI/Views/Pages/ProjektUebersichtPage.xaml`, `.xaml.cs`
- Modify: `src/AuswertungPro.Next.UI/ViewModels/ShellViewModel.cs` Zeile 134: `new("", "Uebersicht", () => CurrentMode == ShellMode.Workspace && IsProjectReady ? new Pages.ProjektUebersichtPageViewModel(this, _sp) : new Pages.OverviewPageViewModel(this, _sp), canOpenWithoutProject: true)`
- Modify: `src/AuswertungPro.Next.UI/App.xaml` (DataTemplate `ProjektUebersichtPageViewModel` → `ProjektUebersichtPage`, wie die vorhandenen Seiten-Templates)
- Test: `tests/AuswertungPro.Next.UI.Tests/ProjektUebersichtPageViewModelTests.cs`, `tests/AuswertungPro.Next.UI.Tests/DesignAuditNovaUebersichtTests.cs`

**Interfaces:**
- Consumes: `ProjektUebersichtRechner` (Task 8), `ICodingSuggestionRegistry` (Task 9), `DashboardStatisticsBuilder.Build(project, hCosts, sCosts)` (Aufruf wie in `OverviewPageViewModel` Zeile 355 — dieselben Kostenspeicher über `_sp.CostStores`; im Zweifel dort abschreiben), `ShellViewModel.NaechsteAufgabePruefenCommand`, `NavigateTo("Haltungen")`, `NavigateToDataPage(DataPageStartFilter)`.
- Produces: ViewModel-Eigenschaften `HeroTitel` (Projektname), `HeroText`, `Kennzahlen` (ProjektUebersichtKennzahlen), `Statistik` (DashboardStatistics), `KiLaeufe` (ObservableCollection<KiLaufZeile>), `ZustandLegende` (Liste `ZustandZeile(Klasse, Label, Anzahl, Farbe)`), `Schaeden` (Liste `SchadenZeile(Hauptcode, Klartext, Anzahl, Anteil)`), `LetzteProjekte` (bis 3 aus `_sp.Settings.RecentProjectPaths`, Name = Ordnername), `SanierungskostenText`, Befehle `NaechsteHaltungPruefenCommand`, `HaltungenOeffnenCommand`, `ZustandFilterCommand(string klasse)`, `KiLaufPruefenCommand(string haltung)`.

- [ ] **Step 1: Tests (rot)**

```csharp
// DesignAuditNovaUebersichtTests.cs
using System.IO;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

public sealed class DesignAuditNovaUebersichtTests
{
    private static string Xaml() => File.ReadAllText(DesignAuditNovaPaletteTests.RepoFile("src", "AuswertungPro.Next.UI", "Views", "Pages", "ProjektUebersichtPage.xaml"));

    [Fact]
    public void Uebersicht_hat_Hero_Vorabdurchlauf_vier_Kennzahlen_Ring_und_Schaeden()
    {
        var xaml = Xaml();
        foreach (var text in new[] { "Nächste Haltung prüfen", "Haltungen öffnen", "KI-Vorabdurchlauf", "Haltungen", "Schächte", "Dringend (Z0/Z1)", "Sanierungskosten", "Zustand Haltungen", "Häufigste Schäden", "Projekte", "Sanierungsverfahren", "Stammdaten" })
            Assert.Contains(text, xaml);
        Assert.Contains("ZustandsklasseInkConverter", xaml);
        Assert.DoesNotContain("#", xaml.Replace("&#x", ""));
    }

    [Fact]
    public void Im_Projekt_zeigt_Uebersicht_die_neue_Seite_und_der_Start_die_Projektliste()
    {
        var shell = File.ReadAllText(DesignAuditNovaPaletteTests.RepoFile("src", "AuswertungPro.Next.UI", "ViewModels", "ShellViewModel.cs"));
        Assert.Contains("new Pages.ProjektUebersichtPageViewModel(this, _sp)", shell);
        Assert.Contains("new Pages.OverviewPageViewModel(this, _sp)", shell);
    }
}
```

```csharp
// ProjektUebersichtPageViewModelTests.cs — reine Zeilenbildung ohne Shell
using AuswertungPro.Next.UI.ViewModels.Pages;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

public sealed class ProjektUebersichtPageViewModelTests
{
    [Fact]
    public void Zustandslegende_listet_Z4_bis_Z0_und_nicht_berechnet()
    {
        var zeilen = ProjektUebersichtPageViewModel.BaueZustandLegende(new System.Collections.Generic.Dictionary<string, int> { ["4"] = 4, ["3"] = 3, ["2"] = 3, ["1"] = 2, ["0"] = 1, [""] = 1 });
        Assert.Equal(new[] { "Z4 · kein Handlungsbedarf", "Z3 · langfristig", "Z2 · mittelfristig", "Z1 · kurzfristig", "Z0 · sofort", "nicht berechnet" }, zeilen.Select(z => z.Label).ToArray());
        Assert.Equal(new[] { 4, 3, 3, 2, 1, 1 }, zeilen.Select(z => z.Anzahl).ToArray());
    }

    [Fact]
    public void KiLaufzeile_fasst_Vorschlaege_zusammen()
    {
        var zeile = ProjektUebersichtPageViewModel.BaueKiLaufZeile("78998-79002", new[] { ("Bogen", "Meter 9,42"), ("Rohrende", "Sekunde 214") });
        Assert.Equal("Bogen · Rohrende", zeile.Badge);
        Assert.Equal("78998-79002 · Meter 9,42, Sekunde 214", zeile.Meta);
    }
}
```

- [ ] **Step 2: Rot** — `dotnet test tests/AuswertungPro.Next.UI.Tests --filter "DesignAuditNovaUebersicht|ProjektUebersichtPageViewModel"`.

- [ ] **Step 3: ViewModel**

```csharp
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using AuswertungPro.Next.Application.Dashboard;
using AuswertungPro.Next.Application.UseCases.CodingSuggestions;
using AuswertungPro.Next.Application.UseCases.Uebersicht;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.DataPage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AuswertungPro.Next.UI.ViewModels.Pages;

public sealed record ZustandZeile(string Klasse, string Label, int Anzahl);
public sealed record SchadenZeile(string Hauptcode, string Klartext, int Anzahl, double Anteil);
public sealed record KiLaufZeile(string Haltung, string Badge, string Meta);
public sealed record ProjektZeile(string Name, string Pfad, string Meta);

/// <summary>Nova-Etappe 2, Inventar 4.1: Uebersicht des offenen Projekts. Alle Zahlen aus einem Bestand.</summary>
public sealed partial class ProjektUebersichtPageViewModel : ObservableObject, IDisposable
{
    private readonly ShellViewModel _shell;
    private readonly ServiceProvider _sp;
    private readonly ICodingSuggestionRegistry _register;

    [ObservableProperty] private string _heroTitel = string.Empty;
    [ObservableProperty] private string _heroText = string.Empty;
    [ObservableProperty] private ProjektUebersichtKennzahlen? _kennzahlen;
    [ObservableProperty] private DashboardStatistics? _statistik;
    [ObservableProperty] private string _sanierungskostenText = "0";
    public ObservableCollection<KiLaufZeile> KiLaeufe { get; } = new();
    public ObservableCollection<ZustandZeile> ZustandLegende { get; } = new();
    public ObservableCollection<SchadenZeile> Schaeden { get; } = new();
    public ObservableCollection<ProjektZeile> LetzteProjekte { get; } = new();
    public IRelayCommand NaechsteHaltungPruefenCommand => _shell.NaechsteAufgabePruefenCommand;
    public string NaechsteAufgabeText => _shell.NaechsteAufgabeText;

    public ProjektUebersichtPageViewModel(ShellViewModel shell, ServiceProvider sp)
    {
        _shell = shell; _sp = sp; _register = sp.CodingSuggestionRegistry;
        _register.Geaendert += Aktualisiere;
        _shell.Project.Data.CollectionChanged += (_, _) => Aktualisiere();
        Aktualisiere();
    }

    private void Aktualisiere()
    {
        var p = _shell.Project;
        HeroTitel = p.Name;
        Kennzahlen = ProjektUebersichtRechner.Berechne(p);
        HeroText = ProjektUebersichtRechner.HeroText(Kennzahlen);
        Statistik = BaueStatistik(p);
        SanierungskostenText = (Statistik.HaltungSanierungsKosten + Statistik.SchachtSanierungsKosten).ToString("#,##0", System.Globalization.CultureInfo.GetCultureInfo("de-CH")).Replace('’', '\'');
        ZustandLegende.Clear();
        foreach (var z in BaueZustandLegende(p.Data.GroupBy(r => DashboardStatisticsBuilder.NormalizeZustandsklasse(r.GetFieldValue(FieldKeys.ConditionClass))).ToDictionary(g => g.Key, g => g.Count())))
            ZustandLegende.Add(z);
        Schaeden.Clear();
        var top = Statistik.TopSchaeden;
        var max = top.Count == 0 ? 1 : top.Max(b => b.Count);
        foreach (var b in top)
            Schaeden.Add(new SchadenZeile(b.Key, b.Label, b.Count, (double)b.Count / max));
        KiLaeufe.Clear();
        foreach (var lauf in _register.Heute())
            KiLaeufe.Add(BaueKiLaufZeile(lauf.Haltung, lauf.Set.Suggestions.Select(s => (s.Kind.ToString(), s.Meter is { } m && !s.MeterIsEstimated ? $"Meter {m.ToString("0.00", System.Globalization.CultureInfo.GetCultureInfo("de-CH"))}" : $"Sekunde {s.PeakTimeSeconds:0}")).ToList()));
        LetzteProjekte.Clear();
        foreach (var pfad in (_sp.Settings.RecentProjectPaths ?? new List<string>()).Take(3))
            LetzteProjekte.Add(new ProjektZeile(Path.GetFileName(Path.GetDirectoryName(Path.GetDirectoryName(pfad)) ?? pfad), pfad, ""));
        OnPropertyChanged(nameof(NaechsteAufgabeText));
    }

    private DashboardStatistics BaueStatistik(Project p)
    {
        // Gleicher Aufruf wie OverviewPageViewModel (Kostenspeicher des offenen Projekts).
        var pfad = _sp.Settings.LastProjectPath;
        var ordner = string.IsNullOrWhiteSpace(pfad) ? null : Path.GetDirectoryName(pfad);
        var h = ordner is null ? null : _sp.CostStores.LoadHaltungCosts(ordner);
        var s = ordner is null ? null : _sp.CostStores.LoadSchachtCosts(ordner);
        return DashboardStatisticsBuilder.Build(p, h, s);
    }

    internal static IReadOnlyList<ZustandZeile> BaueZustandLegende(IReadOnlyDictionary<string, int> anzahlJeKlasse)
    {
        (string Klasse, string Label)[] reihenfolge =
        [
            ("4", "Z4 · kein Handlungsbedarf"), ("3", "Z3 · langfristig"), ("2", "Z2 · mittelfristig"),
            ("1", "Z1 · kurzfristig"), ("0", "Z0 · sofort"), ("", "nicht berechnet")
        ];
        return reihenfolge.Select(r => new ZustandZeile(r.Klasse, r.Label, anzahlJeKlasse.TryGetValue(r.Klasse, out var n) ? n : 0)).ToList();
    }

    internal static KiLaufZeile BaueKiLaufZeile(string haltung, IReadOnlyList<(string Art, string Ort)> vorschlaege)
    {
        var arten = vorschlaege.Select(v => v.Art).Distinct().ToList();
        var orte = vorschlaege.Select(v => v.Ort).Where(o => !string.IsNullOrWhiteSpace(o)).Distinct().ToList();
        return new KiLaufZeile(haltung, string.Join(" · ", arten), orte.Count == 0 ? haltung : $"{haltung} · {string.Join(", ", orte)}");
    }

    [RelayCommand] private void HaltungenOeffnen() => _shell.NavigateTo("Haltungen");
    [RelayCommand] private void ZustandFilter(string? klasse) => _shell.NavigateToDataPage(new DataPageStartFilter(FieldKeys.ConditionClass, string.IsNullOrEmpty(klasse) ? "ohne" : klasse));
    [RelayCommand] private void KiLaufPruefen(string? haltung)
    {
        var record = _shell.Project.Data.FirstOrDefault(r => string.Equals(r.GetFieldValue(FieldKeys.HoldingName), haltung, StringComparison.OrdinalIgnoreCase));
        if (record is null) return;
        _shell.NavigateToHolding(record);
        if (_shell.CurrentPage is DataPageViewModel dp && dp.PlayVideoCommand.CanExecute(record)) dp.PlayVideoCommand.Execute(record);
    }
    [RelayCommand] private void ProjektOeffnen(string? pfad) { if (!string.IsNullOrWhiteSpace(pfad)) _shell.TryOpenProject(pfad); }

    public void Dispose() => _register.Geaendert -= Aktualisiere;
}
```
Anpassungen beim Umsetzen: `_sp.CostStores.LoadHaltungCosts/LoadSchachtCosts` heissen ggf. anders — den Aufruf 1:1 aus `OverviewPageViewModel` (Zeile ~340–356) übernehmen. `CodingSuggestion` hat die Felder `Kind` (Bogen/Rohranfang/Rohrende), `PeakTimeSeconds`, `Meter`, `MeterIsEstimated`, `Confidence`, `IsStrong`, `AcceptancePrecision` (`CodingSuggestionModels.cs` Zeile 39); ein geschätzter Meter wird nie als Meter gezeigt, sondern als Sekunde (CLAUDE.md-Regel).

- [ ] **Step 4: Seite (XAML)**

```xml
<UserControl x:Class="AuswertungPro.Next.UI.Views.Pages.ProjektUebersichtPage"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:ui="clr-namespace:AuswertungPro.Next.UI"
             xmlns:ctrl="clr-namespace:AuswertungPro.Next.UI.Controls"
             xmlns:dp="clr-namespace:AuswertungPro.Next.UI.DataPage"
             xmlns:haltung="clr-namespace:AuswertungPro.Next.UI.Views.Pages.Haltungsansicht">
    <UserControl.Resources>
        <haltung:ZustandsklasseBrushConverter x:Key="ZkBrushConv"/>
        <dp:ZustandsklasseInkConverter x:Key="ZkInkConv"/>
        <Style x:Key="KpiLabel" TargetType="TextBlock">
            <Setter Property="FontSize" Value="{DynamicResource TextXS}"/>
            <Setter Property="FontWeight" Value="Bold"/>
            <Setter Property="Foreground" Value="{DynamicResource MutedBrush}"/>
            <Setter Property="Typography.Capitals" Value="AllSmallCaps"/>
        </Style>
        <Style x:Key="KpiWert" TargetType="TextBlock">
            <Setter Property="FontSize" Value="{DynamicResource TextDisplay}"/>
            <Setter Property="FontWeight" Value="SemiBold"/>
            <Setter Property="Foreground" Value="{DynamicResource TextBrush}"/>
        </Style>
        <Style x:Key="KarteTitel" TargetType="TextBlock">
            <Setter Property="FontSize" Value="{DynamicResource TextL}"/>
            <Setter Property="FontWeight" Value="SemiBold"/>
            <Setter Property="Foreground" Value="{DynamicResource TextBrush}"/>
        </Style>
    </UserControl.Resources>
    <ScrollViewer VerticalScrollBarVisibility="Auto">
        <Grid>
            <Grid.RowDefinitions>
                <RowDefinition Height="Auto"/><RowDefinition Height="Auto"/><RowDefinition Height="Auto"/><RowDefinition Height="Auto"/>
            </Grid.RowDefinitions>
            <!-- Hero -->
            <Grid Grid.Row="0" Margin="0,0,0,12">
                <Grid.ColumnDefinitions><ColumnDefinition Width="1.4*"/><ColumnDefinition Width="12"/><ColumnDefinition Width="*"/></Grid.ColumnDefinitions>
                <Border Grid.Column="0" Style="{StaticResource Card}">
                    <StackPanel>
                        <TextBlock Text="{Binding HeroTitel}" FontSize="{DynamicResource TextTitle}" FontWeight="SemiBold" Foreground="{DynamicResource TextBrush}"/>
                        <TextBlock Text="{Binding HeroText}" TextWrapping="Wrap" Margin="0,8,0,12" Foreground="{DynamicResource TextSecondaryBrush}" MaxWidth="560" HorizontalAlignment="Left"/>
                        <StackPanel Orientation="Horizontal">
                            <Button Command="{Binding NaechsteHaltungPruefenCommand}" Style="{StaticResource ToolbarButtonAccent}" ToolTip="Erste offene Haltung öffnen und ihr Video prüfen">
                                <StackPanel Orientation="Horizontal"><ui:FluentIcon Glyph="&#xE768;" Margin="0,0,6,0"/><TextBlock Text="Nächste Haltung prüfen"/></StackPanel>
                            </Button>
                            <Button Command="{Binding HaltungenOeffnenCommand}" Style="{StaticResource ToolbarButton}" Margin="8,0,0,0" Content="Haltungen öffnen" ToolTip="Zur Haltungsliste"/>
                        </StackPanel>
                    </StackPanel>
                </Border>
                <Border Grid.Column="2" Style="{StaticResource Card}">
                    <DockPanel>
                        <DockPanel DockPanel.Dock="Top" Margin="0,0,0,8">
                            <TextBlock Text="KI-Vorabdurchlauf" Style="{StaticResource KarteTitel}"/>
                            <TextBlock Text="diese Sitzung" Margin="8,0,0,0" VerticalAlignment="Bottom" FontSize="{DynamicResource TextS}" Foreground="{DynamicResource MutedBrush}"/>
                        </DockPanel>
                        <TextBlock DockPanel.Dock="Bottom" Text="Noch kein Vorabdurchlauf in dieser Sitzung. Der Durchlauf startet beim Eintritt in den Codiermodus." TextWrapping="Wrap" Foreground="{DynamicResource MutedBrush}" FontSize="{DynamicResource TextS}">
                            <TextBlock.Style><Style TargetType="TextBlock"><Setter Property="Visibility" Value="Collapsed"/><Style.Triggers><DataTrigger Binding="{Binding KiLaeufe.Count}" Value="0"><Setter Property="Visibility" Value="Visible"/></DataTrigger></Style.Triggers></Style></TextBlock.Style>
                        </TextBlock>
                        <ItemsControl ItemsSource="{Binding KiLaeufe}">
                            <ItemsControl.ItemTemplate>
                                <DataTemplate>
                                    <Border Background="{DynamicResource HeaderBrush}" CornerRadius="{DynamicResource RadiusM}" Padding="8,6" Margin="0,0,0,6">
                                        <DockPanel>
                                            <Button DockPanel.Dock="Right" Content="Prüfen" Style="{StaticResource ToolbarButton}" Padding="8,2" MinHeight="0"
                                                    Command="{Binding DataContext.KiLaufPruefenCommand, RelativeSource={RelativeSource AncestorType=UserControl}}" CommandParameter="{Binding Haltung}"
                                                    ToolTip="Haltung öffnen und Video prüfen"/>
                                            <Border DockPanel.Dock="Left" Background="{DynamicResource KiSubtleBrush}" CornerRadius="{DynamicResource RadiusPill}" Padding="8,2" Margin="0,0,8,0">
                                                <TextBlock Text="{Binding Badge}" FontSize="{DynamicResource TextS}" FontWeight="SemiBold" Foreground="{DynamicResource KiTextBrush}"/>
                                            </Border>
                                            <TextBlock Text="{Binding Meta}" VerticalAlignment="Center" FontSize="{DynamicResource TextS}" Foreground="{DynamicResource TextSecondaryBrush}" TextTrimming="CharacterEllipsis"/>
                                        </DockPanel>
                                    </Border>
                                </DataTemplate>
                            </ItemsControl.ItemTemplate>
                        </ItemsControl>
                    </DockPanel>
                </Border>
            </Grid>
            <!-- KPIs -->
            <UniformGrid Grid.Row="1" Columns="4" Margin="-6,0,-6,6">
                <Border Style="{StaticResource Card}" Margin="6"><StackPanel>
                    <TextBlock Text="Haltungen" Style="{StaticResource KpiLabel}"/>
                    <StackPanel Orientation="Horizontal"><TextBlock Text="{Binding Kennzahlen.Haltungen}" Style="{StaticResource KpiWert}"/><TextBlock Text="{Binding Kennzahlen.Geprueft, StringFormat=geprüft {0}}" Margin="8,0,0,6" VerticalAlignment="Bottom" Foreground="{DynamicResource MutedBrush}"/></StackPanel>
                    <TextBlock Foreground="{DynamicResource MutedBrush}" FontSize="{DynamicResource TextS}"><Run Text="Gesamtlänge "/><Run Text="{Binding Kennzahlen.GesamtlaengeM, StringFormat={}{0:0} m, Mode=OneWay}"/><Run Text=" · KI analysiert "/><Run Text="{Binding Kennzahlen.KiAnalysiert, Mode=OneWay}"/><Run Text=" · offen "/><Run Text="{Binding Kennzahlen.Offen, Mode=OneWay}"/></TextBlock>
                </StackPanel></Border>
                <Border Style="{StaticResource Card}" Margin="6"><StackPanel>
                    <TextBlock Text="Schächte" Style="{StaticResource KpiLabel}"/>
                    <StackPanel Orientation="Horizontal"><TextBlock Text="{Binding Kennzahlen.Schaechte}" Style="{StaticResource KpiWert}"/><TextBlock Text="{Binding Kennzahlen.SchaechteMitProtokoll, StringFormat=mit Protokoll {0}}" Margin="8,0,0,6" VerticalAlignment="Bottom" Foreground="{DynamicResource MutedBrush}"/></StackPanel>
                    <TextBlock Text="Zustandsklasse immer von Hand" Foreground="{DynamicResource MutedBrush}" FontSize="{DynamicResource TextS}"/>
                </StackPanel></Border>
                <Border Style="{StaticResource Card}" Margin="6"><StackPanel>
                    <TextBlock Text="Dringend (Z0/Z1)" Style="{StaticResource KpiLabel}"/>
                    <StackPanel Orientation="Horizontal"><TextBlock Text="{Binding Kennzahlen.DringendHaltungen}" Style="{StaticResource KpiWert}" Foreground="{DynamicResource DangerTextBrush}"/><TextBlock Text="Haltungen" Margin="8,0,0,6" VerticalAlignment="Bottom" Foreground="{DynamicResource MutedBrush}"/></StackPanel>
                    <TextBlock Text="{Binding Kennzahlen.DringendSchaechte, StringFormat=dazu {0} Schächte}" Foreground="{DynamicResource MutedBrush}" FontSize="{DynamicResource TextS}"/>
                </StackPanel></Border>
                <Border Style="{StaticResource Card}" Margin="6"><StackPanel>
                    <TextBlock Text="Sanierungskosten" Style="{StaticResource KpiLabel}"/>
                    <StackPanel Orientation="Horizontal"><TextBlock Text="{Binding SanierungskostenText}" Style="{StaticResource KpiWert}"/><TextBlock Text="CHF" Margin="8,0,0,6" VerticalAlignment="Bottom" Foreground="{DynamicResource MutedBrush}"/></StackPanel>
                    <TextBlock Foreground="{DynamicResource MutedBrush}" FontSize="{DynamicResource TextS}"><Run Text="Haltungen "/><Run Text="{Binding Statistik.HaltungSanierungsKostenText, Mode=OneWay}"/><Run Text=" · Schächte "/><Run Text="{Binding Statistik.SchachtSanierungsKostenText, Mode=OneWay}"/><Run Text=" · ohne MWST"/></TextBlock>
                </StackPanel></Border>
            </UniformGrid>
            <!-- Zustand + Schaeden -->
            <Grid Grid.Row="2" Margin="0,0,0,12">
                <Grid.ColumnDefinitions><ColumnDefinition Width="1.2*"/><ColumnDefinition Width="12"/><ColumnDefinition Width="*"/></Grid.ColumnDefinitions>
                <Border Grid.Column="0" Style="{StaticResource Card}">
                    <DockPanel>
                        <TextBlock DockPanel.Dock="Top" Text="Zustand Haltungen" Style="{StaticResource KarteTitel}" Margin="0,0,0,8"/>
                        <ItemsControl ItemsSource="{Binding ZustandLegende}" AutomationProperties.Name="Zustandsklassen">
                            <ItemsControl.ItemTemplate>
                                <DataTemplate>
                                    <Button Command="{Binding DataContext.ZustandFilterCommand, RelativeSource={RelativeSource AncestorType=UserControl}}" CommandParameter="{Binding Klasse}"
                                            Style="{StaticResource ToolbarButton}" HorizontalContentAlignment="Stretch" Margin="0,0,0,4" Background="Transparent" BorderBrush="Transparent"
                                            ToolTip="Haltungen dieser Klasse in der Liste zeigen">
                                        <DockPanel>
                                            <Border DockPanel.Dock="Left" Width="12" Height="12" CornerRadius="{DynamicResource RadiusS}" Margin="0,0,8,0" Background="{Binding Klasse, Converter={StaticResource ZkBrushConv}}" BorderBrush="{DynamicResource BorderBrush}" BorderThickness="1"/>
                                            <TextBlock DockPanel.Dock="Right" Text="{Binding Anzahl}" FontFamily="{DynamicResource FontMono}" Foreground="{DynamicResource TextBrush}"/>
                                            <TextBlock Text="{Binding Label}" Foreground="{DynamicResource TextBrush}"/>
                                        </DockPanel>
                                    </Button>
                                </DataTemplate>
                            </ItemsControl.ItemTemplate>
                        </ItemsControl>
                    </DockPanel>
                </Border>
                <Border Grid.Column="2" Style="{StaticResource Card}">
                    <DockPanel>
                        <DockPanel DockPanel.Dock="Top" Margin="0,0,0,8">
                            <TextBlock Text="Häufigste Schäden" Style="{StaticResource KarteTitel}"/>
                            <TextBlock Text="VSA-KEK Hauptcode, aus den Befunden gezählt" Margin="8,0,0,0" VerticalAlignment="Bottom" FontSize="{DynamicResource TextS}" Foreground="{DynamicResource MutedBrush}" TextTrimming="CharacterEllipsis"/>
                        </DockPanel>
                        <TextBlock DockPanel.Dock="Bottom" Text="Keine Befunde" Foreground="{DynamicResource MutedBrush}">
                            <TextBlock.Style><Style TargetType="TextBlock"><Setter Property="Visibility" Value="Collapsed"/><Style.Triggers><DataTrigger Binding="{Binding Schaeden.Count}" Value="0"><Setter Property="Visibility" Value="Visible"/></DataTrigger></Style.Triggers></Style></TextBlock.Style>
                        </TextBlock>
                        <ItemsControl ItemsSource="{Binding Schaeden}">
                            <ItemsControl.ItemTemplate>
                                <DataTemplate>
                                    <Grid Margin="0,3">
                                        <Grid.ColumnDefinitions><ColumnDefinition Width="64"/><ColumnDefinition Width="*"/><ColumnDefinition Width="Auto"/></Grid.ColumnDefinitions>
                                        <TextBlock Text="{Binding Hauptcode}" FontFamily="{DynamicResource FontMono}" FontWeight="SemiBold" FontSize="{DynamicResource TextS}" VerticalAlignment="Center"/>
                                        <Grid Grid.Column="1" Height="10" VerticalAlignment="Center">
                                            <Border Background="{DynamicResource BorderLightBrush}" CornerRadius="{DynamicResource RadiusPill}"/>
                                            <Border Background="{DynamicResource AccentBrush}" CornerRadius="{DynamicResource RadiusPill}" HorizontalAlignment="Left">
                                                <Border.Width><MultiBinding Converter="{StaticResource AnteilBreite}"><Binding Path="Anteil"/><Binding Path="ActualWidth" RelativeSource="{RelativeSource AncestorType=Grid}"/></MultiBinding></Border.Width>
                                            </Border>
                                        </Grid>
                                        <TextBlock Grid.Column="2" Margin="10,0,0,0" FontFamily="{DynamicResource FontMono}" FontSize="{DynamicResource TextS}" VerticalAlignment="Center"><Run Text="{Binding Anzahl, Mode=OneWay}"/><Run Text=" "/><Run Text="{Binding Klartext, Mode=OneWay}"/></TextBlock>
                                    </Grid>
                                </DataTemplate>
                            </ItemsControl.ItemTemplate>
                        </ItemsControl>
                    </DockPanel>
                </Border>
            </Grid>
            <!-- Projekte / Verfahren / Stammdaten -->
            <UniformGrid Grid.Row="3" Columns="3" Margin="-6,0,-6,0">
                <Border Style="{StaticResource Card}" Margin="6"><DockPanel>
                    <TextBlock DockPanel.Dock="Top" Text="Projekte" Style="{StaticResource KarteTitel}" Margin="0,0,0,8"/>
                    <ItemsControl ItemsSource="{Binding LetzteProjekte}"><ItemsControl.ItemTemplate><DataTemplate>
                        <Button Command="{Binding DataContext.ProjektOeffnenCommand, RelativeSource={RelativeSource AncestorType=UserControl}}" CommandParameter="{Binding Pfad}" Style="{StaticResource ToolbarButton}" HorizontalContentAlignment="Left" Margin="0,0,0,4" ToolTip="{Binding Pfad}">
                            <TextBlock Text="{Binding Name}" FontWeight="SemiBold"/>
                        </Button>
                    </DataTemplate></ItemsControl.ItemTemplate></ItemsControl>
                </DockPanel></Border>
                <Border Style="{StaticResource Card}" Margin="6"><DockPanel>
                    <TextBlock DockPanel.Dock="Top" Text="Sanierungsverfahren" Style="{StaticResource KarteTitel}" Margin="0,0,0,8"/>
                    <TextBlock DockPanel.Dock="Bottom" Text="Keine Kosten erfasst" Foreground="{DynamicResource MutedBrush}">
                        <TextBlock.Style><Style TargetType="TextBlock"><Setter Property="Visibility" Value="Collapsed"/><Style.Triggers><DataTrigger Binding="{Binding Statistik.HasVerfahren}" Value="False"><Setter Property="Visibility" Value="Visible"/></DataTrigger></Style.Triggers></Style></TextBlock.Style>
                    </TextBlock>
                    <ItemsControl ItemsSource="{Binding Statistik.Sanierungsverfahren}"><ItemsControl.ItemTemplate><DataTemplate>
                        <DockPanel Margin="0,3"><TextBlock DockPanel.Dock="Right" FontFamily="{DynamicResource FontMono}" FontSize="{DynamicResource TextS}"><Run Text="{Binding Qty, Mode=OneWay, StringFormat={}{0:0.##}}"/><Run Text=" "/><Run Text="{Binding Unit, Mode=OneWay}"/></TextBlock><TextBlock Text="{Binding Label}" FontSize="{DynamicResource TextS}"/></DockPanel>
                    </DataTemplate></ItemsControl.ItemTemplate></ItemsControl>
                </DockPanel></Border>
                <Border Style="{StaticResource Card}" Margin="6"><DockPanel>
                    <DockPanel DockPanel.Dock="Top" Margin="0,0,0,8"><TextBlock Text="Stammdaten" Style="{StaticResource KarteTitel}"/><TextBlock Text="Vollständigkeit" Margin="8,0,0,0" VerticalAlignment="Bottom" FontSize="{DynamicResource TextS}" Foreground="{DynamicResource MutedBrush}"/></DockPanel>
                    <ItemsControl ItemsSource="{Binding Kennzahlen.Stammdaten}"><ItemsControl.ItemTemplate><DataTemplate>
                        <Grid Margin="0,3"><Grid.ColumnDefinitions><ColumnDefinition Width="64"/><ColumnDefinition Width="*"/><ColumnDefinition Width="Auto"/></Grid.ColumnDefinitions>
                            <TextBlock Text="{Binding Feld}" FontSize="{DynamicResource TextS}" VerticalAlignment="Center"/>
                            <Grid Grid.Column="1" Height="10" VerticalAlignment="Center">
                                <Border Background="{DynamicResource BorderLightBrush}" CornerRadius="{DynamicResource RadiusPill}"/>
                                <Border CornerRadius="{DynamicResource RadiusPill}" HorizontalAlignment="Left" Background="{Binding Stufe, Converter={StaticResource ZkBrushConv}}">
                                    <Border.Width><MultiBinding Converter="{StaticResource AnteilBreite}"><Binding Path="Prozent"/><Binding Path="ActualWidth" RelativeSource="{RelativeSource AncestorType=Grid}"/></MultiBinding></Border.Width>
                                </Border>
                            </Grid>
                            <TextBlock Grid.Column="2" Margin="10,0,0,0" FontFamily="{DynamicResource FontMono}" FontSize="{DynamicResource TextS}" VerticalAlignment="Center"><Run Text="{Binding Gefuellt, Mode=OneWay}"/><Run Text="/"/><Run Text="{Binding Gesamt, Mode=OneWay}"/></TextBlock>
                        </Grid>
                    </DataTemplate></ItemsControl.ItemTemplate></ItemsControl>
                </DockPanel></Border>
            </UniformGrid>
        </Grid>
    </ScrollViewer>
</UserControl>
```
Der Konverter `AnteilBreite` (Anteil 0..1 bzw. Prozent 0..100 × Breite) wird in `ProjektUebersichtPage.xaml.cs` als verschachtelte Klasse `AnteilBreiteConverter : IMultiValueConverter` definiert und in `UserControl.Resources` registriert (`<local:AnteilBreiteConverter x:Key="AnteilBreite"/>`; Werte > 1 werden als Prozent gelesen). `ZustandsklasseBrushConverter` liefert für `"Z4"`/`"Z3"`/`"Z2"` nichts — deshalb übergibt `Stufe` nur die Ziffer: im Rechner `Stufe` auf `"4"`/`"3"`/`"2"` umstellen UND den Test in Task 8 auf diese Werte ändern (bewusste Vereinfachung, dieselbe Palette). Der Donut selbst entfällt zugunsten der anklickbaren Legende (die Zahlen sind identisch; ein Ring ohne Klickziel bringt nichts, BEWERTUNG N04 verlangt Konsistenz, nicht Grafik).

- [ ] **Step 5: Grün** — `dotnet build AuswertungPro.sln && dotnet test tests/AuswertungPro.Next.UI.Tests --no-build --filter "DesignAuditNovaUebersicht|ProjektUebersichtPageViewModel|DesignAudit|XamlActionWiring"` → PASS.
- [ ] **Step 6: Commit** — `git add -A src tests && git commit -m "Nova-Etappe 2: Uebersichtsseite im Projekt mit Hero, Kennzahlen, Zustand und Schaeden"`

---
## Teil D — Haltungsseite: Feinschliff nach Prototyp

### Task 11: Umschalter ins Menü, Zähler an Chips und Themen, „gross anzeigen"

**Files:**
- Modify: `src/AuswertungPro.Next.UI/Views/Pages/DataPage.xaml` (Werkzeugleiste Zeilen ~100–112; Menü „Weitere Aktionen" Gruppe Ansicht ~Zeile 216; Chip-Vorlage ~Zeile 403)
- Modify: `src/AuswertungPro.Next.UI/DataPage/DataPageColumnViewCatalog.cs` (`Anzahl`)
- Modify: `src/AuswertungPro.Next.UI/Views/Pages/Haltungsansicht/HaltungFelderDrawer.xaml` (Themenkopf mit Zähler, Knopf „gross anzeigen")
- Modify: `src/AuswertungPro.Next.UI/Views/Pages/Haltungsansicht/HaltungFelderDrawer.xaml.cs` (`ThemaAnzeige.Anzahl`, `IsTall`, Ereignis)
- Modify: `src/AuswertungPro.Next.UI/DataPage/DataPageNovaWorkspaceController.cs` (`IsTall` → Höhe 60 %)
- Test: `tests/AuswertungPro.Next.UI.Tests/DesignAuditNovaHaltungenTests.cs` (erweitern), `tests/AuswertungPro.Next.UI.Tests/DataPageColumnViewCatalogTests.cs` (erweitern), `tests/AuswertungPro.Next.UI.Tests/HaltungFelderDrawerFilterTests.cs` (erweitern)

Hintergrund: Auf Pascals Bild vom 06.09. war der Knopf „Haltungsansicht" gedrückt, weil er als erster Knopf wie „die Haltungen-Ansicht" wirkt. Der Prototyp hat diesen Knopf nicht; die alte Ansicht bleibt unter „Weitere Aktionen ▸ Ansicht" erreichbar.

- [ ] **Step 1: Tests erweitern (rot)**

In `DesignAuditNovaHaltungenTests`:
```csharp
    [Fact]
    public void Der_Umschalter_zur_alten_Haltungsansicht_liegt_im_Menue_und_nicht_in_der_Werkzeugleiste()
    {
        var xaml = File.ReadAllText(Pfad("Views", "Pages", "DataPage.xaml"));
        var toggle = Regex.Match(xaml, "<MenuItem x:Name=\"HaltungsansichtToggle\"[\\s\\S]*?/>|<MenuItem x:Name=\"HaltungsansichtToggle\"[\\s\\S]*?</MenuItem>");
        Assert.True(toggle.Success, "HaltungsansichtToggle muss ein MenuItem sein");
        Assert.Contains("IsCheckable=\"True\"", toggle.Value);
        Assert.Contains("Header=\"Alte Haltungsansicht\"", toggle.Value);
        Assert.DoesNotContain("<ToggleButton x:Name=\"HaltungsansichtToggle\"", xaml);
    }

    [Fact]
    public void Eingabefelder_haben_Zaehler_je_Thema_und_einen_Knopf_gross_anzeigen()
    {
        var xaml = File.ReadAllText(Pfad("Views", "Pages", "Haltungsansicht", "HaltungFelderDrawer.xaml"));
        Assert.Contains("{Binding Anzahl}", xaml);
        Assert.Contains("AutomationProperties.Name=\"Eingabefelder gross anzeigen\"", xaml);
    }
```
(`Pfad(...)` ist der vorhandene Helfer dieser Testklasse; falls er anders heisst, den vorhandenen verwenden.)

In `DataPageColumnViewCatalogTests`:
```csharp
    [Fact]
    public void Jede_Ansicht_nennt_ihre_Spaltenzahl()
    {
        Assert.Equal(7, DataPageColumnViewCatalog.Resolve("kompakt").Anzahl(40));
        Assert.Equal(40, DataPageColumnViewCatalog.Resolve("alle").Anzahl(40));
    }
```
In `HaltungFelderDrawerFilterTests`:
```csharp
    [Fact]
    public void Thema_zaehlt_seine_Felder()
    {
        var gruppen = new[] { new RecordDetailGroup("Stammdaten", "", new[] { Item("Strasse"), Item("Baujahr") }) };
        var thema = Assert.Single(HaltungFelderDrawer.Filtere(gruppen, null));
        Assert.Equal(2, thema.Anzahl);
    }
```
(`Item(label)` wie in den bestehenden Tests dieser Datei bauen.)

- [ ] **Step 2: Rot** — `dotnet test tests/AuswertungPro.Next.UI.Tests --filter "DesignAuditNovaHaltungen|DataPageColumnViewCatalog|HaltungFelderDrawerFilter"`.

- [ ] **Step 3: Katalog und Schublade**

`DataPageColumnView`: `public int Anzahl(int alleSpalten) => Felder?.Count ?? alleSpalten;`. In `DataPage.ColumnViews.cs` beim Aufbau der Chips (`SyncColumnViewChips`) den Zähler setzen: die Chip-Vorlage in `DataPage.xaml` wird
```xml
                    <ToggleButton Style="{StaticResource PageCompactToggleButton}" Margin="0,0,6,0" Tag="{Binding Key}"
                                  Click="ColumnViewChip_Click" ToolTip="{Binding Titel}" AutomationProperties.Name="{Binding Titel}">
                        <StackPanel Orientation="Horizontal">
                            <TextBlock Text="{Binding Titel}"/>
                            <TextBlock x:Name="ChipZaehler" Margin="6,0,0,0" FontFamily="{DynamicResource FontMono}" FontSize="{DynamicResource TextXS}" Foreground="{DynamicResource MutedBrush}"/>
                        </StackPanel>
                    </ToggleButton>
```
und `SyncColumnViewChips` setzt je Chip `ChipZaehler.Text = view.Anzahl(_columnFields.Count).ToString()` (über `FindVisualChildren<TextBlock>(chip).First(t => t.Name == "ChipZaehler")`).

`HaltungFelderDrawer.xaml.cs`: `ThemaAnzeige` wird `record ThemaAnzeige(string Title, IReadOnlyList<RecordDetailGroup> EinzelGruppe) { public int Anzahl => EinzelGruppe.Sum(g => g.Items.Count); }`; neue `DependencyProperty IsTall (bool)` mit Ereignis `IsTallChanged`. XAML: Expander-Header wird
```xml
                            <Expander IsExpanded="True" Margin="0,0,8,0" VerticalAlignment="Top">
                                <Expander.Header>
                                    <StackPanel Orientation="Horizontal">
                                        <TextBlock Text="{Binding Title}" FontWeight="SemiBold"/>
                                        <TextBlock Text="{Binding Anzahl}" Margin="8,0,0,0" FontFamily="{DynamicResource FontMono}" FontSize="{DynamicResource TextXS}" Foreground="{DynamicResource MutedBrush}"/>
                                    </StackPanel>
                                </Expander.Header>
```
und in der Kopfzeile rechts neben „Alle zu":
```xml
                <ToggleButton DockPanel.Dock="Right" IsChecked="{Binding IsTall, ElementName=Root, Mode=TwoWay}" Style="{StaticResource CompactToggleButton}" Margin="6,0,0,0"
                              ToolTip="Eingabefelder gross anzeigen" AutomationProperties.Name="Eingabefelder gross anzeigen">
                    <ui:FluentIcon Glyph="&#xE740;"/>
                </ToggleButton>
```
`DataPageNovaWorkspaceController.Verdrahte()`: `_e.FelderDrawer.IsTallChanged += (_, _) => { if (_e.FelderDrawer.IsTall) { _e.FelderDrawer.IsOpen = true; _e.DrawerRow.Height = new GridLength(Math.Max(DataPageWorkspaceLayoutPolicy.MinDrawer, (_e.GridHost.ActualHeight - _e.FilterChips.ActualHeight) * 0.6)); } else ApplyDrawerHeight(); };` (Inventar 8.6: „gross" öffnet mit, nicht gespeichert).

- [ ] **Step 4: Umschalter ins Menü**

In `DataPage.xaml` den `<ToggleButton x:Name="HaltungsansichtToggle" …>…</ToggleButton>` samt folgendem Trenner-`Border` aus der Werkzeugleiste entfernen. Im Kontextmenü von „Weitere Aktionen" in der Gruppe Ansicht (vor `UndockButton`) einfügen:
```xml
                                <MenuItem x:Name="HaltungsansichtToggle" Header="Alte Haltungsansicht" IsCheckable="True"
                                          Checked="HaltungsansichtToggle_Changed" Unchecked="HaltungsansichtToggle_Changed"
                                          ToolTip="Liste links und Formularkarten statt Tabelle, Übersicht und Eingabefeldern"/>
```
Im Code (`DataPage.xaml.cs`, `DataPage.RecordInteractions.cs`, `DataPage.NovaWorkspace.cs`) bleiben alle Zugriffe `HaltungsansichtToggle.IsChecked == true` / `= false` / `IsEnabled` gültig (`MenuItem.IsChecked` ist `bool`; `IsChecked = false` und `== true` kompilieren). Prüfen: `dotnet build`. `DataPageToolbarLayoutTests.Haltungsansicht_lives_in_main_grid_row_and_uses_haltung_search_label` verlangt weiterhin `Text="Suche Haltung:"` und die `HaltungsansichtView` in `Grid.Row="1"` — beides bleibt.

- [ ] **Step 5: Grün** — `dotnet build AuswertungPro.sln && dotnet test tests/AuswertungPro.Next.UI.Tests --no-build --filter "DesignAuditNovaHaltungen|DataPageColumnViewCatalog|HaltungFelderDrawerFilter|DataPageToolbarLayout|DataPageNovaLayoutIsolated|XamlActionWiring|DesignAudit"` → PASS. `DataPageNovaLayoutIsolatedSmokeTests` schaltet die Ansicht ggf. über `HaltungsansichtToggle` um; kompiliert der Test wegen `ToggleButton`-Cast nicht, dort auf `MenuItem` umstellen.
- [ ] **Step 6: Commit** — `git add -A src tests && git commit -m "Nova-Etappe 2: Umschalter ins Menue, Zaehler an Chips und Themen, Eingabefelder gross"`

### Task 12: Übersicht rechts mit Rohrring, Fakten, Schadenliste und KI-Hinweis

**Files:**
- Create: `src/AuswertungPro.Next.Application/UseCases/Uebersicht/RohrringGeometrie.cs`
- Create: `src/AuswertungPro.Next.UI/Views/Pages/Haltungsansicht/RohrringControl.xaml`, `.xaml.cs`
- Modify: `src/AuswertungPro.Next.UI/Views/Pages/Haltungsansicht/HaltungUebersichtPanel.xaml` (ganzer Inhalt), `.xaml.cs` (`PlayerRequested`, Fakten-Eigenschaften)
- Modify: `src/AuswertungPro.Next.UI/DataPage/DataPageNovaWorkspaceController.cs` (`PlayerRequested` → `vm.PlayVideoCommand`)
- Modify: `src/AuswertungPro.Next.UI/Views/Pages/Haltungsansicht/HaltungListItemConverters.cs` (neue Konverter `SchadenStufeQuelleConverter`, `SchadenCodeConverter`)
- Test: `tests/AuswertungPro.Next.Infrastructure.Tests/RohrringGeometrieTests.cs`, `DesignAuditNovaHaltungenTests` (erweitern)

**Interfaces:**
- Produces: `sealed record RohrringBogen(double StartGrad, double SweepGrad, int Stufe, string Tooltip)`; `static class RohrringGeometrie { IReadOnlyList<RohrringBogen> Boegen(IReadOnlyList<ProtocolEntry> entries, int max = 3); int StufeVon(ProtocolEntry e); }`. Winkel: 0° = 12 Uhr, im Uhrzeigersinn. Liegt eine Uhrlage vor (`CodeMeta.Parameters` Schlüssel `Uhr_von` und optional `Uhr_bis`, Stunden 1–12, geparst über `int.TryParse` der ersten zwei Ziffern), gilt Start = (von mod 12) × 30°, Sweep = ((bis − von + 12) mod 12) × 30° (mindestens 30°). Ohne Uhrlage gilt die Prototyp-Indexregel (Inventar 5.1): Start = i × 70°, Sweep 30°. Stufe aus `CodeMeta.Severity` (1–5), fehlend = 1. Farbe im Control: `Severity{Stufe}Brush`.
- `HaltungUebersichtPanel` bekommt `PlayerRequested` (Action<HaltungRecord>) analog zu `BeobachtungenRequested`.

- [ ] **Step 1: Test (rot)**

```csharp
using AuswertungPro.Next.Application.UseCases.Uebersicht;
using AuswertungPro.Next.Domain.Protocol;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class RohrringGeometrieTests
{
    private static ProtocolEntry E(string code, string? von = null, string? bis = null, string? stufe = null)
    {
        var e = new ProtocolEntry { Code = code, Beschreibung = code, CodeMeta = new ProtocolEntryCodeMeta { Code = code, Severity = stufe } };
        if (von is not null) e.CodeMeta.Parameters["Uhr_von"] = von;
        if (bis is not null) e.CodeMeta.Parameters["Uhr_bis"] = bis;
        return e;
    }

    [Fact]
    public void Uhrlage_bestimmt_Start_und_Sweep()
    {
        var b = Assert.Single(RohrringGeometrie.Boegen(new[] { E("BAB", "12", "02", "3") }));
        Assert.Equal(0, b.StartGrad); Assert.Equal(60, b.SweepGrad); Assert.Equal(3, b.Stufe);
    }

    [Fact]
    public void Einzelne_Uhr_ergibt_dreissig_Grad_und_Sohle_liegt_unten()
    {
        var b = Assert.Single(RohrringGeometrie.Boegen(new[] { E("BBC", "06") }));
        Assert.Equal(180, b.StartGrad); Assert.Equal(30, b.SweepGrad); Assert.Equal(1, b.Stufe);
    }

    [Fact]
    public void Ohne_Uhrlage_gilt_die_Indexregel_und_hoechstens_drei()
    {
        var boegen = RohrringGeometrie.Boegen(new[] { E("A"), E("B"), E("C"), E("D") });
        Assert.Equal(3, boegen.Count);
        Assert.Equal(new[] { 0.0, 70.0, 140.0 }, boegen.Select(b => b.StartGrad).ToArray());
    }
}
```

- [ ] **Step 2: Rot**, **Step 3: Geometrie**

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.Application.UseCases.Uebersicht;

public sealed record RohrringBogen(double StartGrad, double SweepGrad, int Stufe, string Tooltip);

/// <summary>
/// Rohrquerschnitt mit Uhrlagen (Inventar 5.1). Anders als der Prototyp folgt die Lage der
/// erfassten Uhrlage (Uhr_von/Uhr_bis), die Indexregel ist nur der Rueckfall ohne Uhrlage.
/// 0 Grad = 12 Uhr, im Uhrzeigersinn.
/// </summary>
public static class RohrringGeometrie
{
    private static readonly string[] VonAliase = ["Uhr_von", "vsa.uhr.von", "ClockPos1", "SchadenlageAnfang"];
    private static readonly string[] BisAliase = ["Uhr_bis", "vsa.uhr.bis", "ClockPos2", "SchadenlageEnde"];

    public static IReadOnlyList<RohrringBogen> Boegen(IReadOnlyList<ProtocolEntry> entries, int max = 3)
    {
        var liste = new List<RohrringBogen>();
        var i = 0;
        foreach (var e in entries.Where(e => !e.IsDeleted).Take(max))
        {
            var von = Stunde(e, VonAliase);
            var bis = Stunde(e, BisAliase);
            double start, sweep;
            if (von is { } v)
            {
                start = (v % 12) * 30.0;
                sweep = bis is { } b && b != v ? (((b - v) + 12) % 12) * 30.0 : 30.0;
            }
            else
            {
                start = i * 70.0;
                sweep = 30.0;
            }
            liste.Add(new RohrringBogen(start, Math.Max(30.0, sweep), StufeVon(e), $"{e.Code} {e.Beschreibung}".Trim()));
            i++;
        }
        return liste;
    }

    public static int StufeVon(ProtocolEntry e)
        => int.TryParse(e.CodeMeta?.Severity, out var s) && s is >= 1 and <= 5 ? s : 1;

    private static int? Stunde(ProtocolEntry e, string[] aliase)
    {
        if (e.CodeMeta?.Parameters is not { } p) return null;
        foreach (var key in aliase)
            if (p.TryGetValue(key, out var raw) && raw is { Length: > 0 })
            {
                var ziffern = new string(raw.TakeWhile(char.IsDigit).ToArray());
                if (int.TryParse(ziffern, out var h) && h is >= 0 and <= 12) return h == 0 ? 12 : h;
            }
        return null;
    }
}
```

- [ ] **Step 4: RohrringControl**

```xml
<UserControl x:Class="AuswertungPro.Next.UI.Views.Pages.Haltungsansicht.RohrringControl"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" x:Name="Root" Width="200" Height="120"
             AutomationProperties.Name="Rohrquerschnitt mit Uhrlagen">
    <Canvas>
        <Ellipse Canvas.Left="50" Canvas.Top="10" Width="100" Height="100" Fill="{DynamicResource BgLightBrush}" Stroke="{DynamicResource BorderBrush}" StrokeThickness="6"/>
        <Ellipse Canvas.Left="50" Canvas.Top="10" Width="100" Height="100" Stroke="{DynamicResource AccentBrush}" StrokeThickness="1.5" Opacity="0.6"/>
        <ItemsControl x:Name="Boegen" ItemsSource="{Binding Boegen, ElementName=Root}">
            <ItemsControl.ItemsPanel><ItemsPanelTemplate><Canvas/></ItemsPanelTemplate></ItemsControl.ItemsPanel>
            <ItemsControl.ItemTemplate>
                <DataTemplate>
                    <Path Data="{Binding Pfad}" Stroke="{Binding Pinsel}" StrokeThickness="6" StrokeStartLineCap="Round" StrokeEndLineCap="Round" ToolTip="{Binding Tooltip}"/>
                </DataTemplate>
            </ItemsControl.ItemTemplate>
        </ItemsControl>
        <TextBlock Canvas.Left="94" Canvas.Top="0" Text="12" FontFamily="{DynamicResource FontMono}" FontSize="{DynamicResource TextXS}" Foreground="{DynamicResource MutedBrush}"/>
        <TextBlock Canvas.Left="156" Canvas.Top="53" Text="3" FontFamily="{DynamicResource FontMono}" FontSize="{DynamicResource TextXS}" Foreground="{DynamicResource MutedBrush}"/>
        <TextBlock Canvas.Left="97" Canvas.Top="108" Text="6" FontFamily="{DynamicResource FontMono}" FontSize="{DynamicResource TextXS}" Foreground="{DynamicResource MutedBrush}"/>
        <TextBlock Canvas.Left="38" Canvas.Top="53" Text="9" FontFamily="{DynamicResource FontMono}" FontSize="{DynamicResource TextXS}" Foreground="{DynamicResource MutedBrush}"/>
    </Canvas>
</UserControl>
```
Code-behind: `DependencyProperty Entries (IReadOnlyList<ProtocolEntry>?)`; bei Änderung `Boegen = RohrringGeometrie.Boegen(entries).Select(b => new BogenAnzeige(PfadFuer(b), PinselFuer(b.Stufe), b.Tooltip))`. `PfadFuer`: Kreis Mittelpunkt (100,60), Radius 50; Startpunkt = (100 + 50·sin(start), 60 − 50·cos(start)), Endpunkt analog mit `start+sweep`, `ArcSegment` mit `IsLargeArc = sweep > 180`, `SweepDirection.Clockwise`, als `PathGeometry`. `PinselFuer(stufe)` = `TryFindResource($"Severity{stufe}Brush") as Brush ?? Brushes.Gray`. Alle Winkel in Radiant umrechnen (`Math.PI/180`).

- [ ] **Step 5: Übersicht-Panel ausbauen**

`HaltungUebersichtPanel.xaml` (Inventar 5.2–5.4) — den `UniformGrid`-Faktenblock ersetzen durch:
```xml
            <local:RohrringControl DockPanel.Dock="Top" Entries="{Binding Entries, ElementName=Root}" HorizontalAlignment="Center" Margin="0,0,0,8"/>
            <UniformGrid DockPanel.Dock="Top" Columns="2" Margin="0,0,0,8">
                <StackPanel Margin="0,0,8,6"><TextBlock Text="Schacht oben" Style="{StaticResource FaktLabel}"/><TextBlock Text="{Binding Fields[Schacht_oben]}" Style="{StaticResource FaktWert}"/></StackPanel>
                <StackPanel Margin="0,0,0,6"><TextBlock Text="Schacht unten" Style="{StaticResource FaktLabel}"/><TextBlock Text="{Binding Fields[Schacht_unten]}" Style="{StaticResource FaktWert}"/></StackPanel>
                <StackPanel Margin="0,0,8,6"><TextBlock Text="Material" Style="{StaticResource FaktLabel}"/><TextBlock Text="{Binding Fields[Rohrmaterial]}" Style="{StaticResource FaktWert}"/></StackPanel>
                <StackPanel Margin="0,0,0,6"><TextBlock Text="DN / Profil" Style="{StaticResource FaktLabel}"/><TextBlock Style="{StaticResource FaktWert}"><Run Text="{Binding Fields[DN_mm], Mode=OneWay}"/><Run Text=" · "/><Run Text="{Binding Fields[Profiltyp], Mode=OneWay}"/></TextBlock></StackPanel>
                <StackPanel Margin="0,0,8,6"><TextBlock Text="Länge" Style="{StaticResource FaktLabel}"/><TextBlock Text="{Binding Fields[Haltungslaenge_m], StringFormat={}{0} m}" Style="{StaticResource FaktWert}"/></StackPanel>
                <StackPanel Margin="0,0,0,6"><TextBlock Text="Inspektion" Style="{StaticResource FaktLabel}"/><TextBlock Text="{Binding Fields[Datum_Jahr]}" Style="{StaticResource FaktWert}"/></StackPanel>
                <StackPanel Margin="0,0,8,6"><TextBlock Text="Prüfung" Style="{StaticResource FaktLabel}"/><TextBlock Text="{Binding PruefungText, ElementName=Root}" Style="{StaticResource FaktWert}"/></StackPanel>
                <StackPanel Margin="0,0,0,6"><TextBlock Text="Video" Style="{StaticResource FaktLabel}"/><TextBlock Text="{Binding VideoText, ElementName=Root}" Style="{StaticResource FaktWert}" TextTrimming="CharacterEllipsis"/></StackPanel>
            </UniformGrid>
```
mit den Stilen in `UserControl.Resources`: `FaktLabel` (TextXS, SemiBold, MutedBrush) und `FaktWert` (TextS, SemiBold, FontMono, TextBrush). Code-behind: `PruefungText` = `HaltungPruefstatus.Text(HaltungPruefstatus.Bestimme(record))`, `VideoText` = Dateiname aus `Fields[Link]` oder „kein Video" (beide `DependencyProperty`, in `OnRecordChanged` gesetzt).

Die Schadenliste (`ListBox.ItemTemplate`) erhält eine zweite Zeile: unter dem Klartext ein `TextBlock` (TextXS, MutedBrush) mit `Text="{Binding Converter={StaticResource SchadenStufeQuelleConv}}"`, der aus `ProtocolEntry` bildet: „Stufe n · " (wenn `CodeMeta.Severity`) + („KI-Vorschlag, Konfidenz 0.91 (Modellsicherheit)" wenn `Ai != null` sonst „fachlich erfasst") + „ · " + („offen" wenn `Ai is { Accepted: false }`, „bestätigt" wenn `Ai is { Accepted: true }`, sonst leer). Konverter in `HaltungListItemConverters.cs` als `SchadenStufeQuelleConverter`, Textbildung als `internal static string Text(ProtocolEntry e)` (testbar).

Unter der Liste (`DockPanel.Dock="Bottom"`, vor dem Leerhinweis) der KI-Hinweis:
```xml
            <Border DockPanel.Dock="Bottom" Background="{DynamicResource KiSubtleBrush}" CornerRadius="{DynamicResource RadiusM}" Padding="10,8" Margin="0,8,0,0"
                    Visibility="{Binding OffeneKiBefunde, ElementName=Root, Converter={StaticResource ZahlSichtbar}}">
                <StackPanel>
                    <TextBlock Text="KI-Vorschläge warten auf fachliche Bestätigung" FontWeight="SemiBold" Foreground="{DynamicResource KiTextBrush}"/>
                    <TextBlock Text="{Binding OffeneKiBefunde, ElementName=Root, StringFormat={}{0} offen. Die Ampel wird erst grün, wenn zwei unabhängige Belegquellen vorliegen und du bestätigt hast.}" TextWrapping="Wrap" FontSize="{DynamicResource TextS}" Foreground="{DynamicResource KiTextBrush}"/>
                    <Button Content="Im Player prüfen" Click="ImPlayer_Click" Style="{StaticResource ToolbarButtonAccent}" HorizontalAlignment="Left" Margin="0,6,0,0" ToolTip="Video dieser Haltung im Player öffnen"/>
                </StackPanel>
            </Border>
```
`ZahlSichtbar`: Konverter int > 0 → Visible (in `HaltungListItemConverters.cs`). `OffeneKiBefunde` = Anzahl `Entries` mit `Ai is { Accepted: false }`. `ImPlayer_Click` ruft `PlayerRequested?.Invoke(Record)`; der Controller setzt `_e.Uebersicht.PlayerRequested = r => { if (_vm() is { } vm && vm.PlayVideoCommand.CanExecute(r)) vm.PlayVideoCommand.Execute(r); };`.

- [ ] **Step 6: Wächter erweitern und grün**

In `DesignAuditNovaHaltungenTests`:
```csharp
    [Fact]
    public void Uebersicht_zeigt_Rohrring_Fakten_und_KI_Hinweis()
    {
        var xaml = File.ReadAllText(Pfad("Views", "Pages", "Haltungsansicht", "HaltungUebersichtPanel.xaml"));
        foreach (var t in new[] { "local:RohrringControl", "Schacht oben", "Schacht unten", "DN / Profil", "Prüfung", "Video", "Im Player prüfen", "KI-Vorschläge warten auf fachliche Bestätigung" })
            Assert.Contains(t, xaml);
    }
```
Run: `dotnet build AuswertungPro.sln && dotnet test tests/AuswertungPro.Next.Infrastructure.Tests --no-build --filter RohrringGeometrieTests && dotnet test tests/AuswertungPro.Next.UI.Tests --no-build --filter "DesignAuditNovaHaltungen|DesignAudit|XamlActionWiring|DataPageNovaLayoutIsolated"` → PASS.
- [ ] **Step 7: Commit** — `git add -A src tests && git commit -m "Nova-Etappe 2: Uebersicht mit Rohrring, Fakten, Schadenliste und KI-Hinweis"`

---
## Teil E — Schachtseite im Nova-Aufbau

### Task 13: Werkzeugleiste, „Weitere Aktionen" und Spaltenansichten der Schächte

**Files:**
- Create: `src/AuswertungPro.Next.UI/DataPage/SchaechteColumnViewCatalog.cs`
- Create: `src/AuswertungPro.Next.UI/Views/Pages/SchaechtePage.ColumnViews.cs`
- Modify: `src/AuswertungPro.Next.UI/Views/Pages/SchaechtePage.xaml` (Zeilen 13–150: Werkzeugleiste und Suchzeile)
- Modify: `src/AuswertungPro.Next.UI/AppSettings.cs` (`SchaechtePageLayout.ActiveColumnView` gibt es über `DataPageLayoutSettings` bereits; nichts Neues nötig — prüfen, sonst additiv ergänzen)
- Test: `tests/AuswertungPro.Next.UI.Tests/SchaechteColumnViewCatalogTests.cs`, `tests/AuswertungPro.Next.UI.Tests/DesignAuditNovaSchaechteTests.cs`

**Interfaces:**
- Produces: `SchaechteColumnViewCatalog.Views` (`DataPageColumnView`-Einträge, Inventar 4.4): `kompakt` „Kompakt" [`Schachtnummer`, `Strasse`, `Funktion`, `Material`, `Dimension 1 mm`, `Dimension 2 mm`, `Schachtform`, `Zustandsklasse`, `PDF_Path`]; `zustand` „Zustand und Inspektion" [`Schachtnummer`, `Zustandsklasse`, `Pruefungsresultat`, `Dichtheit`, `Referenzpruefung`, `Gewaesserschutz`, `Grundwasserspiegel`, `Belastungsklasse`, `Inspektionsdatum`]; `sanierung` „Sanierung und Kosten" [`Schachtnummer`, `Sanieren_JaNein`, `Empfohlene_Sanierungsmassnahmen`, `Ausgefuehrt_durch`, `Offen_abgeschlossen`, `Kosten`, `Eigentümer`]; `medien` „Dokumente und Medien" [`Schachtnummer`, `PDF_Path`, `PDF_Eigen`, `Link`, `Fotos`]; `alle` „Alle Spalten" (null). Die Feldnamen der Schachttabelle stammen aus der Excel-Kopfzeile (`SchaechteColumnPolicy`); Spalten, deren Name im Projekt anders lautet, bleiben in der Ansicht einfach unsichtbar — deshalb `Resolve` unverändert tolerant.
- Der `SchaechtePage.ColumnViews`-Partial spiegelt `DataPage.ColumnViews.cs` (gleiche Methoden `InitColumnViews`, `ColumnViewChip_Click`, `SyncColumnViewChips`), Ablage in `vm.Settings.SchaechtePageLayout.ActiveColumnView`, Feldname je Spalte aus dem vorhandenen Spaltenaufbau in `SchaechtePage.xaml.cs` `RebuildColumns()` (dort wird je Spalte der Feldname bekannt; die Zuordnung `Dictionary<DataGridColumn,string> _columnFields` wird im Partial deklariert und in `RebuildColumns` mit einer Zeile je Spalte gefüllt: `_columnFields[column] = fieldName;` — das ist die einzige Zeile, die in `SchaechtePage.xaml.cs` ergänzt wird).

- [ ] **Step 1: Tests (rot)**

```csharp
// SchaechteColumnViewCatalogTests.cs
using AuswertungPro.Next.UI.DataPage;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

public sealed class SchaechteColumnViewCatalogTests
{
    [Fact]
    public void Fuenf_Ansichten_mit_der_Schachtnummer_in_jeder()
    {
        Assert.Equal(new[] { "kompakt", "zustand", "sanierung", "medien", "alle" }, SchaechteColumnViewCatalog.Views.Select(v => v.Key).ToArray());
        foreach (var v in SchaechteColumnViewCatalog.Views.Where(v => v.Felder is not null))
            Assert.Contains("Schachtnummer", v.Felder!);
        Assert.Equal(9, SchaechteColumnViewCatalog.Resolve("kompakt").Felder!.Count);
    }

    [Fact]
    public void Unbekannter_Schluessel_faellt_auf_Alle_zurueck()
        => Assert.Equal("alle", SchaechteColumnViewCatalog.Resolve("gibtsnicht").Key);
}
```

```csharp
// DesignAuditNovaSchaechteTests.cs
using System.IO;
using System.Text.RegularExpressions;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

public sealed class DesignAuditNovaSchaechteTests
{
    private static string Xaml() => File.ReadAllText(DesignAuditNovaPaletteTests.RepoFile("src", "AuswertungPro.Next.UI", "Views", "Pages", "SchaechtePage.xaml"));

    [Fact]
    public void Werkzeugleiste_zeigt_nur_Hauptaktionen_und_ein_Menue_Weitere_Aktionen()
    {
        var xaml = Xaml();
        Assert.Contains("x:Name=\"WeitereAktionenDropdown\"", xaml);
        foreach (var header in new[] { "PDF-Daten", "Aktualisieren", "Leere Felder aus QGIS", "Katasterkennungen", "Feldnamen aufräumen", "Strassen", "Hoch", "Runter", "Ansicht anpassen", "Alte Schachtansicht" })
            Assert.Contains($"Header=\"{header}\"", xaml);
        Assert.DoesNotContain("<ToggleButton x:Name=\"SchachtansichtToggle\"", xaml);
        Assert.Contains("x:Name=\"ColumnViewChips\"", xaml);
        Assert.Contains("SchaechteColumnViewCatalog.Views", xaml);
    }
}
```

- [ ] **Step 2: Rot** — `dotnet test tests/AuswertungPro.Next.UI.Tests --filter "SchaechteColumnViewCatalog|DesignAuditNovaSchaechte"`.

- [ ] **Step 3: Katalog**

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.DataPage;

/// <summary>Spaltensaetze der Schachtliste (Inventar 4.4). Reine Daten wie DataPageColumnViewCatalog.</summary>
public static class SchaechteColumnViewCatalog
{
    private const string Nummer = "Schachtnummer";

    public static IReadOnlyList<DataPageColumnView> Views { get; } =
    [
        new("kompakt", "Kompakt", [Nummer, "Strasse", "Funktion", "Material", FieldKeys.ShaftDimension1Mm, FieldKeys.ShaftDimension2Mm, FieldKeys.ShaftShape, FieldKeys.ConditionClass, FieldKeys.PdfPath]),
        new("zustand", "Zustand und Inspektion", [Nummer, FieldKeys.ConditionClass, "Pruefungsresultat", "Dichtheit", "Referenzpruefung", "Gewaesserschutz", "Grundwasserspiegel", FieldKeys.LoadClass, "Inspektionsdatum"]),
        new("sanierung", "Sanierung und Kosten", [Nummer, FieldKeys.RenovationDecision, FieldKeys.RecommendedRehabilitationMeasures, FieldKeys.RehabilitationExecutor, FieldKeys.WorkflowStatus, FieldKeys.Cost, "Eigentümer"]),
        new("medien", "Dokumente und Medien", [Nummer, FieldKeys.PdfPath, FieldKeys.PdfEigen, FieldKeys.Link, "Fotos"]),
        new("alle", "Alle Spalten", null)
    ];

    public static DataPageColumnView Resolve(string? key)
        => Views.FirstOrDefault(v => string.Equals(v.Key, key, StringComparison.OrdinalIgnoreCase)) ?? Views[^1];
}
```
Der Controller `DataPageColumnViewController` löst intern über `DataPageColumnViewCatalog.Resolve`; für die Schächte braucht er einen Katalog-Parameter: Konstruktor um `Func<string?, DataPageColumnView> resolve` erweitern (Standard = `DataPageColumnViewCatalog.Resolve`, damit `DataPage.ColumnViews.cs` unverändert bleibt) und `Apply` diesen verwenden lassen.

- [ ] **Step 4: SchaechtePage.xaml umbauen**

Werkzeugleiste (erste `Border` in `Grid.Row="0"`) auf: `Speichern` (Accent), `Protokoll importieren`, `Neu`, `Löschen`, Trenner, `Weitere Aktionen` (Knopf `x:Name="WeitereAktionenDropdown" Click="DropdownButton_Click"` mit `Button.ContextMenu` — Handler `DropdownButton_Click` aus `DataPage` nach `SchaechtePage.ColumnViews.cs` kopieren: `ButtonContextMenuOpener.Open((Button)sender)` beziehungsweise exakt die Zeilen aus `DataPage.xaml.cs`), rechts die Suche (`SearchBox` mit Beschriftung „Suche Schacht:") in derselben Zeile. Menüinhalt mit Gruppenköpfen als `TextBlock` in einem nicht auswählbaren `MenuItem` wie in `DataPage.xaml` (dort abschreiben, `XamlActionWiringGuard`-konform):

- DATEN: `PDF-Daten` (`Command="{Binding ErgaenzeStammdatenAusPdfsCommand}"`), `Aktualisieren` (`RefreshProtocolCommand`), `Leere Felder aus QGIS` (`QgisFelderErgaenzenCommand`, `IsEnabled="{Binding CanMutateShaftData}"`), `Katasterkennungen` (`KatasterKennungenErgaenzenCommand`), `Feldnamen aufräumen` (`FeldnamenAufraeumenCommand`), `Strassen` (`Click="StrassenStapel_Click"`)
- REIHENFOLGE: `Hoch` (`MoveUpCommand`), `Runter` (`MoveDownCommand`)
- FACHLICH: `Sanierungsmassnahmen…` (`Click="SanierungsmassnahmenMenu_Click"`), `Protokoll (PDF)…` (`Click="ProtokollMenu_Click"`), `Gehe zu Ordner` (`Click="OpenContainingFolderMenu_Click"`)
- ANSICHT: `Ansicht anpassen` (Untermenü mit dem bisherigen Inhalt der zweiten Border: Verschieben auf Pos., Gehe zu Zeile, Zeilenhöhe, Zoom, Ausrichtung — als `MenuItem` mit `StaysOpenOnClick="True"` und dem bestehenden XAML als `MenuItem.Header`-Inhalt, genau wie `DataPage.xaml` es für „Ansicht anpassen" tut; Ereignisnamen unverändert), `Spalten anordnen` (`IsCheckable="True" IsChecked="{Binding IsColumnReorderEnabled}"`), `Spalte leeren` (`x:Name="ClearColumnModeButton" IsCheckable="True"` — im Code wird `ClearColumnModeButton.IsChecked` gelesen; `MenuItem.IsChecked` kompiliert), `Alte Schachtansicht` (`x:Name="SchachtansichtToggle" IsCheckable="True" Checked/Unchecked="SchachtansichtToggle_Changed"`).
Jeder Menüpunkt bekommt ein `MenuItem.Icon` (`ui:FluentIcon`) — Glyphen aus der alten Leiste übernehmen.

Unter der Werkzeugleiste die Chip-Zeile:
```xml
        <ItemsControl x:Name="ColumnViewChips" Grid.Row="1" Margin="2,0,2,6"
                      ItemsSource="{Binding Source={x:Static dp:SchaechteColumnViewCatalog.Views}}" AutomationProperties.Name="Spaltenansicht">
            <ItemsControl.ItemsPanel><ItemsPanelTemplate><WrapPanel/></ItemsPanelTemplate></ItemsControl.ItemsPanel>
            <ItemsControl.ItemTemplate>
                <DataTemplate>
                    <ToggleButton Style="{StaticResource CompactToggleButton}" Margin="0,0,6,0" Tag="{Binding Key}" Click="ColumnViewChip_Click" ToolTip="{Binding Titel}" AutomationProperties.Name="{Binding Titel}">
                        <StackPanel Orientation="Horizontal"><TextBlock Text="{Binding Titel}"/><TextBlock x:Name="ChipZaehler" Margin="6,0,0,0" FontFamily="{DynamicResource FontMono}" FontSize="{DynamicResource TextXS}" Foreground="{DynamicResource MutedBrush}"/></StackPanel>
                    </ToggleButton>
                </DataTemplate>
            </ItemsControl.ItemTemplate>
        </ItemsControl>
```
(`xmlns:dp="clr-namespace:AuswertungPro.Next.UI.DataPage"` ergänzen; Zeilen des Rasters: Werkzeugleiste 0, Chips 1, Tabelle 2, Statuszeile 3 — `Grid.Row` der Tabelle, der `SchachtansichtView`, des Leerzustands und der Statuszeile um 1 erhöhen.) `SchachtansichtToggle.IsChecked = true;` in `SchaechtePage.xaml.cs` Zeile 102 bleibt bis Task 14 (dann Einstellung).

- [ ] **Step 5: Grün** — `dotnet build AuswertungPro.sln && dotnet test tests/AuswertungPro.Next.UI.Tests --no-build --filter "SchaechteColumnViewCatalog|DesignAuditNovaSchaechte|DesignAudit|XamlActionWiring|DataPageColumnView|Schaecht"` → PASS.
- [ ] **Step 6: Commit** — `git add -A src tests && git commit -m "Nova-Etappe 2: Schachtseite mit Hauptaktionen, Weitere Aktionen und Spaltenansichten"`

### Task 14: Schachtansicht rechts und Eingabefelder unten

**Files:**
- Create: `src/AuswertungPro.Next.UI/Views/Pages/Schachtansicht/SchachtUebersichtPanel.xaml`, `.xaml.cs`
- Create: `src/AuswertungPro.Next.UI/DataPage/SchaechteNovaWorkspaceController.cs`
- Create: `src/AuswertungPro.Next.UI/Views/Pages/SchaechtePage.NovaWorkspace.cs`
- Modify: `src/AuswertungPro.Next.UI/Views/Pages/SchaechtePage.xaml` (Tabellenzeile wird Raster Tabelle | Splitter | Panel; darunter Splitter | Drawer)
- Modify: `src/AuswertungPro.Next.UI/AppSettings.cs` (`public bool ShowSchaechteNovaLayout { get; set; } = true;` neben `ShowHaltungenNovaLayout`)
- Modify: `src/AuswertungPro.Next.UI/Views/Pages/SchaechtePage.xaml.cs` Zeile 102: `SchachtansichtToggle.IsChecked = !vm.Settings.ShowSchaechteNovaLayout;` — falls `vm` dort noch nicht verfügbar ist, die Zeile in `OnDataContextChanged` (Zeile 125) verschieben; Aufruf `InitNovaWorkspace(vm)` ebenfalls dort (zwei Zeilen; Datei bleibt unter 1000).
- Test: `DesignAuditNovaSchaechteTests` (erweitern), `tests/AuswertungPro.Next.UI.Tests/SchaechteNovaLayoutIsolatedSmokeTests.cs`

**Interfaces:**
- `SchachtUebersichtPanel`: `DependencyProperty Record (SchachtRecord?)`, `Entries (IReadOnlyList<ProtocolEntry>?)` (aus `Record.Protocol?.Current?.Entries`, im Panel selbst gelesen, kein VM-Zugriff), `Action<SchachtRecord>? PdfRequested`. Inhalt (Inventar 5.6): Grundriss (Ellipse/Rechteck/Kreis nach `Fields[Schachtform]`: `Oval`/`Rechteckig` → Ellipse rx 62 ry 50; `Quadratisch` → Rechteck 110×100 Rundung 6; sonst Kreis r 50; Masstext `Dimension 1 mm × Dimension 2 mm`), Fakten (Funktion, Material, Tiefe, Baujahr, Belastungsklasse, Inspektion), Schäden (Code-Chip, Klartext, Stufe, Meter/Ort), Hinweis „Am Schacht wird die Zustandsklasse nie berechnet. Die Fachperson setzt sie von Hand.", Knopf „Protokoll (PDF)".
- `SchaechteNovaWorkspaceController`: Kopie von `DataPageNovaWorkspaceController` mit `SchachtRecord`, `SchaechtePageViewModel`, Schlüsseln `NovaViewKey = "SchaechtePage"`, `SchaechteEingabefelder`, `SchaechteSchachtansicht`, Detail-Builder `SchaechteRecordDetailsBuilder` (Instanz `_recordDetailsBuilder` der Seite über einen Delegaten `Func<SchachtRecord, IReadOnlyList<RecordDetailGroup>>`), Live-Abgleich: `DataPageDetailLiveSync` erwartet `HaltungRecord` — für Schächte entfällt der Live-Abgleich in dieser Etappe; stattdessen `AktualisiereFelderDrawer()` bei `SelectionChanged` und `RecordPropertyChanged` (Zeile 602 der Seite ruft es auf). Die Schachtseite kennt keinen `GridMinRowHeight`-Zeilenwert? Doch: `GridMinRowHeight` im VM (XAML `MinRowHeight="{Binding GridMinRowHeight}"`).
- Drawer: `HaltungFelderDrawer` wird wiederverwendet (Titel = Schachtnummer, Groups aus `SchaechteRecordDetailsBuilder`).

- [ ] **Step 1: Tests (rot)**

`DesignAuditNovaSchaechteTests` ergänzen:
```csharp
    [Fact]
    public void Schaechte_haben_Schachtansicht_rechts_und_Eingabefelder_unten_mit_gespeicherten_Trennlinien()
    {
        var xaml = Xaml();
        Assert.Contains("schachtansicht:SchachtUebersichtPanel", xaml);
        Assert.Contains("haltung:HaltungFelderDrawer", xaml);
        Assert.Contains("SplitterKey=\"SchaechteSchachtansicht\"", xaml);
        Assert.Contains("SplitterKey=\"SchaechteEingabefelder\"", xaml);
        Assert.Contains("ViewPersonalization.ViewKey=\"SchaechtePage\"", xaml);
        var settings = File.ReadAllText(DesignAuditNovaPaletteTests.RepoFile("src", "AuswertungPro.Next.UI", "AppSettings.cs"));
        Assert.Contains("public bool ShowSchaechteNovaLayout { get; set; } = true;", settings);
    }

    [Fact]
    public void Schachtansicht_erklaert_die_Handbewertung_und_zeigt_den_Grundriss()
    {
        var xaml = File.ReadAllText(DesignAuditNovaPaletteTests.RepoFile("src", "AuswertungPro.Next.UI", "Views", "Pages", "Schachtansicht", "SchachtUebersichtPanel.xaml"));
        Assert.Contains("Am Schacht wird die Zustandsklasse nie berechnet", xaml);
        Assert.Contains("AutomationProperties.Name=\"Schachtgrundriss\"", xaml);
        Assert.Contains("ZustandsklasseInkConverter", xaml);
    }
```
`SchaechteNovaLayoutIsolatedSmokeTests.cs`: Kopie von `DataPageNovaLayoutIsolatedSmokeTests` (Kindprozess über `WpfIsolatedTestProcess`), die eine `SchaechtePage` mit einem `SchaechtePageViewModel`-freien Aufbau lädt: Drawer `IsOpen=false` → Zeile `Auto`, `IsOpen=true` → Zeile ≥ 120, Splitterzeile 6. Den Aufbau des vorhandenen Tests 1:1 übernehmen und nur die Typen tauschen (`SchaechtePage`, Elemente `DrawerRow`, `DrawerSplitterRow`, `FelderDrawer`).

- [ ] **Step 2: Rot** — Kompilierfehler.

- [ ] **Step 3: Panel**

```xml
<UserControl x:Class="AuswertungPro.Next.UI.Views.Pages.Schachtansicht.SchachtUebersichtPanel"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:local="clr-namespace:AuswertungPro.Next.UI.Views.Pages.Schachtansicht"
             xmlns:haltung="clr-namespace:AuswertungPro.Next.UI.Views.Pages.Haltungsansicht"
             xmlns:dp="clr-namespace:AuswertungPro.Next.UI.DataPage" x:Name="Root">
    <UserControl.Resources>
        <local:SchachtZustandsklasseBrushConverter x:Key="ZkBrushConv"/>
        <dp:ZustandsklasseInkConverter x:Key="ZkInkConv"/>
        <haltung:SchadenMeterConverter x:Key="SchadenMeterConv"/>
        <haltung:SchadenKlartextConverter x:Key="SchadenKlartextConv"/>
        <haltung:SchadenStufeQuelleConverter x:Key="SchadenStufeQuelleConv"/>
        <Style x:Key="FaktLabel" TargetType="TextBlock"><Setter Property="FontSize" Value="{DynamicResource TextXS}"/><Setter Property="FontWeight" Value="SemiBold"/><Setter Property="Foreground" Value="{DynamicResource MutedBrush}"/></Style>
        <Style x:Key="FaktWert" TargetType="TextBlock"><Setter Property="FontSize" Value="{DynamicResource TextS}"/><Setter Property="FontWeight" Value="SemiBold"/><Setter Property="FontFamily" Value="{DynamicResource FontMono}"/><Setter Property="Foreground" Value="{DynamicResource TextBrush}"/></Style>
    </UserControl.Resources>
    <Border Background="{DynamicResource CardBrush}" BorderBrush="{DynamicResource BorderBrush}" BorderThickness="1" CornerRadius="{DynamicResource RadiusL}" Padding="12">
        <DockPanel DataContext="{Binding Record, ElementName=Root}">
            <DockPanel DockPanel.Dock="Top" Margin="0,0,0,8">
                <Border DockPanel.Dock="Right" MinWidth="34" Height="22" Padding="6,0" CornerRadius="{DynamicResource RadiusS}" Background="{Binding Fields[Zustandsklasse], Converter={StaticResource ZkBrushConv}}" ToolTip="Zustandsklasse (von Hand gesetzt)">
                    <TextBlock Text="{Binding Fields[Zustandsklasse], StringFormat=Z{0}}" HorizontalAlignment="Center" VerticalAlignment="Center" FontSize="{DynamicResource TextS}" FontWeight="SemiBold" Foreground="{Binding Fields[Zustandsklasse], Converter={StaticResource ZkInkConv}}"/>
                </Border>
                <TextBlock Text="Schachtansicht" FontSize="{DynamicResource TextL}" FontWeight="SemiBold" Foreground="{DynamicResource TextBrush}" VerticalAlignment="Center"/>
                <TextBlock Text="{Binding Fields[Schachtnummer]}" Margin="10,0,0,0" VerticalAlignment="Center" FontFamily="{DynamicResource FontMono}" Foreground="{DynamicResource TextSecondaryBrush}"/>
            </DockPanel>
            <Canvas DockPanel.Dock="Top" Width="200" Height="120" HorizontalAlignment="Center" Margin="0,0,0,8" AutomationProperties.Name="Schachtgrundriss">
                <Ellipse x:Name="Kreis" Canvas.Left="50" Canvas.Top="10" Width="100" Height="100" Fill="{DynamicResource BgLightBrush}" Stroke="{DynamicResource BorderBrush}" StrokeThickness="6"/>
                <Ellipse x:Name="Oval" Canvas.Left="38" Canvas.Top="10" Width="124" Height="100" Fill="{DynamicResource BgLightBrush}" Stroke="{DynamicResource BorderBrush}" StrokeThickness="6" Visibility="Collapsed"/>
                <Rectangle x:Name="Quadrat" Canvas.Left="45" Canvas.Top="10" Width="110" Height="100" RadiusX="6" RadiusY="6" Fill="{DynamicResource BgLightBrush}" Stroke="{DynamicResource BorderBrush}" StrokeThickness="6" Visibility="Collapsed"/>
                <TextBlock x:Name="MassText" Canvas.Left="0" Canvas.Top="50" Width="200" TextAlignment="Center" FontFamily="{DynamicResource FontMono}" FontSize="{DynamicResource TextXS}" Foreground="{DynamicResource MutedBrush}"/>
            </Canvas>
            <UniformGrid DockPanel.Dock="Top" Columns="2" Margin="0,0,0,8">
                <StackPanel Margin="0,0,8,6"><TextBlock Text="Funktion" Style="{StaticResource FaktLabel}"/><TextBlock Text="{Binding Fields[Funktion]}" Style="{StaticResource FaktWert}"/></StackPanel>
                <StackPanel Margin="0,0,0,6"><TextBlock Text="Material" Style="{StaticResource FaktLabel}"/><TextBlock Text="{Binding Fields[Material]}" Style="{StaticResource FaktWert}"/></StackPanel>
                <StackPanel Margin="0,0,8,6"><TextBlock Text="Tiefe" Style="{StaticResource FaktLabel}"/><TextBlock Text="{Binding Fields[Tiefe m]}" Style="{StaticResource FaktWert}"/></StackPanel>
                <StackPanel Margin="0,0,0,6"><TextBlock Text="Baujahr" Style="{StaticResource FaktLabel}"/><TextBlock Text="{Binding Fields[Baujahr]}" Style="{StaticResource FaktWert}"/></StackPanel>
                <StackPanel Margin="0,0,8,6"><TextBlock Text="Belastungsklasse" Style="{StaticResource FaktLabel}"/><TextBlock Text="{Binding Fields[Belastungsklasse]}" Style="{StaticResource FaktWert}"/></StackPanel>
                <StackPanel Margin="0,0,0,6"><TextBlock Text="Inspektion" Style="{StaticResource FaktLabel}"/><TextBlock Text="{Binding Fields[Inspektionsdatum]}" Style="{StaticResource FaktWert}"/></StackPanel>
            </UniformGrid>
            <TextBlock DockPanel.Dock="Bottom" Text="Am Schacht wird die Zustandsklasse nie berechnet. Die Fachperson setzt sie von Hand." TextWrapping="Wrap" FontSize="{DynamicResource TextXS}" Foreground="{DynamicResource MutedBrush}" Margin="0,8,0,0"/>
            <Button DockPanel.Dock="Bottom" Content="Protokoll (PDF)" Click="Pdf_Click" Style="{StaticResource ToolbarButton}" HorizontalAlignment="Left" Margin="0,8,0,0" ToolTip="Schachtprotokoll öffnen"/>
            <TextBlock DockPanel.Dock="Top" Text="Schäden" FontSize="{DynamicResource TextM}" FontWeight="SemiBold" Foreground="{DynamicResource TextBrush}" Margin="0,4,0,6"/>
            <ListBox ItemsSource="{Binding Entries, ElementName=Root}" BorderThickness="0" Background="Transparent" AutomationProperties.Name="Schäden" ScrollViewer.HorizontalScrollBarVisibility="Disabled">
                <ListBox.ItemTemplate>
                    <DataTemplate>
                        <Grid Margin="0,2">
                            <Grid.ColumnDefinitions><ColumnDefinition Width="Auto"/><ColumnDefinition Width="*"/><ColumnDefinition Width="Auto"/></Grid.ColumnDefinitions>
                            <Border Background="{DynamicResource AccentSubtleBrush}" CornerRadius="{DynamicResource RadiusS}" Padding="6,2" Margin="0,0,8,0" VerticalAlignment="Top"><TextBlock Text="{Binding Code}" FontFamily="{DynamicResource FontMono}" FontWeight="SemiBold" FontSize="{DynamicResource TextS}" Foreground="{DynamicResource TextBrush}"/></Border>
                            <StackPanel Grid.Column="1"><TextBlock Text="{Binding Converter={StaticResource SchadenKlartextConv}}" TextTrimming="CharacterEllipsis" Foreground="{DynamicResource TextBrush}"/><TextBlock Text="{Binding Converter={StaticResource SchadenStufeQuelleConv}}" FontSize="{DynamicResource TextXS}" Foreground="{DynamicResource MutedBrush}"/></StackPanel>
                            <TextBlock Grid.Column="2" Text="{Binding Converter={StaticResource SchadenMeterConv}}" FontFamily="{DynamicResource FontMono}" Foreground="{DynamicResource TextSecondaryBrush}" Margin="8,0,0,0"/>
                        </Grid>
                    </DataTemplate>
                </ListBox.ItemTemplate>
            </ListBox>
        </DockPanel>
    </Border>
</UserControl>
```
Feldnamen `Tiefe m`, `Inspektionsdatum`, `Belastungsklasse`, `Funktion`, `Material`: aus der Excel-Kopfzeile (`Export_Vorlage/Schächte.xlsx`, `SchaechteColumnPolicy`) verifizieren und ggf. auf die dort verwendeten Schreibweisen anpassen. Code-behind: `OnRecordChanged` setzt `Entries = record?.Protocol?.Current?.Entries?.Where(e => !e.IsDeleted).ToList()`, wählt den Grundriss über `Fields[Schachtform]` (Werte aus `SchachtformVokabular`: `Oval`/`Rechteckig` → Oval, `Quadratisch` → Quadrat, sonst Kreis) und setzt `MassText.Text = $"{d1} × {d2}"` (leer, wenn beide fehlen). `Pdf_Click` → `PdfRequested?.Invoke(Record)`; der Controller verdrahtet `PdfRequested = r => RouteSchachtansichtAction("pdf", r)` (die Seite reicht `RouteSchachtansichtAction` als Delegat herein; der Aktionsschlüssel für das Protokoll ist in `SchaechtePage.xaml.cs` Zeile 667 nachzusehen).

- [ ] **Step 4: Controller, Partial, XAML-Raster**

`SchaechteNovaWorkspaceController`: `DataPageNovaWorkspaceController` kopieren, Typen tauschen (`SchachtRecord`, `SchaechtePageViewModel`, `SchachtUebersichtPanel`), Konstanten wie oben, `DataPageDetailLiveSync` entfernen. `SchaechtePage.NovaWorkspace.cs`: Kopie von `DataPage.NovaWorkspace.cs` mit `VerdrahteNovaWorkspace()` (im Konstruktor der Seite nach `InitializeComponent()` aufrufen — eine Zeile), `InitNovaWorkspace(vm)` (Toggle aus `ShowSchaechteNovaLayout`, dann `ApplySchachtansichtSichtbarkeit()`), `AktualisiereFelderDrawer()` (aus `Grid_SelectedCellsChanged`/`SchachtansichtToggle_Changed` aufrufen — je eine Zeile) und `SetNovaWorkspaceVisible`.

`SchaechtePage.xaml`: `Grid.Row="2"` (Tabelle) wird ein inneres `Grid x:Name="GridHost" behaviors:ViewPersonalization.ViewKey="SchaechtePage"` mit Zeilen `*`, `DrawerSplitterRow` (6), `DrawerRow` (Auto, MinHeight 0) und Spalten `*`, `SideSplitterCol` (6), `SideCol` (320): Tabelle in (0,0), Leerzustand in (0,0), `SchachtansichtView` `Grid.Row="0" Grid.ColumnSpan="3"`, `GridSplitter x:Name="SideSplitter"` (0,1) mit `SplitterPersistenceBehavior.SplitterKey="SchaechteSchachtansicht" TargetColumnIndex="2" MinSize="240" MaxSize="560"`, `<schachtansicht:SchachtUebersichtPanel x:Name="Uebersicht" Grid.Row="0" Grid.Column="2" Margin="8,0,0,0" Record="{Binding Selected}"/>`, `GridSplitter x:Name="DrawerSplitter"` (1, ColumnSpan 3, `ResizeDirection="Rows"`, `SplitterKey="SchaechteEingabefelder" TargetRowIndex="2"`), `<haltung:HaltungFelderDrawer x:Name="FelderDrawer" Grid.Row="2" Grid.ColumnSpan="3" Margin="0,4,0,0"/>`. Namespaces `behaviors`, `haltung` ergänzen (siehe `DataPage.xaml` Kopf).

- [ ] **Step 5: Grün** — `dotnet build AuswertungPro.sln && dotnet test tests/AuswertungPro.Next.UI.Tests --no-build --filter "DesignAuditNovaSchaechte|SchaechteNovaLayoutIsolated|DesignAudit|XamlActionWiring|Maintainability|Schaecht"` → PASS.
- [ ] **Step 6: Commit** — `git add -A src tests && git commit -m "Nova-Etappe 2: Schachtansicht rechts und Eingabefelder unten auf der Schachtseite"`

---
## Teil F — Player

### Task 15: Player-Kopf kompakt, Bedienleiste nach Prototyp, „Weitere ▾"

**Files:**
- Modify: `src/AuswertungPro.Next.UI/Views/Windows/PlayerWindow.xaml` (Kopf Zeilen 143–205; Bedienleiste `Grid.Row="5"` ab Zeile ~874; Geschwindigkeit/Lautstärke/Rate ab ~1006)
- Modify: `src/AuswertungPro.Next.UI/Views/Windows/PlayerWindow.Resources.xaml` (Stil `SectionLabel`, Zeile 72)
- Test: `tests/AuswertungPro.Next.UI.Tests/DesignAuditNovaPlayerTests.cs`

Regeln (Global Constraints): `x:Name`, `Click`, `Command`, `ToolTip` bleiben wörtlich; nur verschieben. Die Tooltips aus `DesignAuditPlayerShortcutTests` unverändert. Bewegung des Kopfes: Prototyp Inventar 6.1 — eine Zeile „Video · Haltung · Datei", Chip „Codier-Modus", rechts „Tastenkürzel F1" und „Schliessen". Bedienleiste Inventar 6.5.

- [ ] **Step 1: Wächter (rot)**

```csharp
using System.IO;
using System.Text.RegularExpressions;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

public sealed class DesignAuditNovaPlayerTests
{
    private static string Xaml() => File.ReadAllText(DesignAuditNovaPaletteTests.RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.xaml"));

    [Fact]
    public void Kopf_ist_eine_Zeile_mit_Video_Haltung_Datei_und_Codiermodus_Chip()
    {
        var xaml = Xaml();
        Assert.Contains("x:Name=\"PlayerKopfzeile\"", xaml);
        Assert.Contains("x:Name=\"CodierModusChip\"", xaml);
        Assert.DoesNotContain("Text=\"Videoplayer\"", xaml);
        Assert.DoesNotContain("Text=\"Hotkeys\"", xaml);
    }

    [Fact]
    public void Bedienleiste_folgt_der_Prototyp_Reihenfolge_und_selten_Gebrauchtes_liegt_unter_Weitere()
    {
        var xaml = Xaml();
        var leiste = Regex.Match(xaml, "<Border x:Name=\"Bedienleiste\"[\\s\\S]*?<!-- Ende Bedienleiste -->").Value;
        Assert.False(string.IsNullOrEmpty(leiste), "Bedienleiste fehlt");
        int Pos(string s) { var i = leiste.IndexOf(s, System.StringComparison.Ordinal); Assert.True(i >= 0, s); return i; }
        Assert.True(Pos("Click=\"Play_Click\"") < Pos("Click=\"Stop_Click\""));
        Assert.True(Pos("Click=\"Stop_Click\"") < Pos("x:Name=\"SpeedPresetButton\""));
        Assert.True(Pos("x:Name=\"SpeedPresetButton\"") < Pos("x:Name=\"LiveDetectionButton\""));
        Assert.True(Pos("x:Name=\"LiveDetectionButton\"") < Pos("x:Name=\"QuickScanButton\""));
        Assert.True(Pos("x:Name=\"QuickScanButton\"") < Pos("x:Name=\"ManualMarkButton\""));
        Assert.True(Pos("x:Name=\"ManualMarkButton\"") < Pos("x:Name=\"WeitereDropdownButton\""));
        var weitere = Regex.Match(xaml, "<Popup x:Name=\"WeiterePopup\"[\\s\\S]*?</Popup>").Value;
        foreach (var s in new[] { "x:Name=\"VolumeSlider\"", "x:Name=\"MuteButton\"", "x:Name=\"CodingScreenshotButton\"", "x:Name=\"RateText\"" })
            Assert.Contains(s, weitere);
    }

    [Fact]
    public void Seitenpanel_verwendet_Kapitaelchen_Abschnittskoepfe()
    {
        var panel = File.ReadAllText(DesignAuditNovaPaletteTests.RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Resources.xaml"));
        var stil = Regex.Match(panel, "<Style x:Key=\"SectionLabel\"[\\s\\S]*?</Style>").Value;
        Assert.Contains("Typography.Capitals\" Value=\"AllSmallCaps\"", stil);
    }
}
```
Liegt `SectionLabel` nicht in `PlayerCodingSidePanel.xaml`, sondern in `PlayerWindow.Resources.xaml`, den Pfad im Test entsprechend setzen (der Test verlangt nur, dass der Stil Kapitälchen trägt).

- [ ] **Step 2: Rot** — `dotnet test tests/AuswertungPro.Next.UI.Tests --filter DesignAuditNovaPlayerTests`.

- [ ] **Step 3: Kopf**

Die `Border Grid.Row="0"` (PlayerCard mit „Videoplayer", Name, Pfad, Hotkey-Karte) ersetzen durch:
```xml
        <Border Grid.Row="0" x:Name="PlayerKopfzeile" Style="{StaticResource PlayerCard}" Margin="0,0,0,8" Padding="12,8">
            <DockPanel>
                <Button DockPanel.Dock="Right" Style="{StaticResource PlayerButton}" Padding="10,4" Margin="6,0,0,0"
                        Click="Close_Click" ToolTip="Player schliessen" AutomationProperties.Name="Player schliessen">
                    <TextBlock Text="Schliessen" FontSize="{DynamicResource TextS}"/>
                </Button>
                <Button DockPanel.Dock="Right" Style="{StaticResource PlayerButton}" Padding="10,4"
                        Click="ShowShortcutOverlay_Click" ToolTip="Tastenkürzel anzeigen — F1" AutomationProperties.Name="Tastenkürzel anzeigen — F1">
                    <StackPanel Orientation="Horizontal"><ui:FluentIcon Glyph="&#xE897;" Margin="0,0,6,0"/><TextBlock Text="Tastenkürzel F1" FontSize="{DynamicResource TextS}"/></StackPanel>
                </Button>
                <Border x:Name="CodierModusChip" DockPanel.Dock="Right" Background="{DynamicResource KiSubtleBrush}" CornerRadius="{DynamicResource RadiusPill}" Padding="10,2" Margin="10,0,0,0" VerticalAlignment="Center" Visibility="Collapsed">
                    <TextBlock Text="Codier-Modus" FontSize="{DynamicResource TextS}" FontWeight="SemiBold" Foreground="{DynamicResource KiTextBrush}"/>
                </Border>
                <ui:FluentIcon DockPanel.Dock="Left" Glyph="&#xE768;" Foreground="{DynamicResource AccentBrush}" Margin="0,0,8,0" VerticalAlignment="Center"/>
                <TextBlock VerticalAlignment="Center" TextTrimming="CharacterEllipsis" FontSize="{DynamicResource TextL}" FontWeight="SemiBold" Foreground="{DynamicResource TextBrush}">
                    <Run Text="Video · "/><Run x:Name="VideoNameText"/><Run Text=" · "/><Run x:Name="VideoPathText" FontWeight="Normal" Foreground="{DynamicResource TextSecondaryBrush}"/>
                </TextBlock>
            </DockPanel>
        </Border>
```
Achtung: `VideoNameText` und `VideoPathText` werden im Code als `TextBlock` verwendet (`.Text = …`). `Run` hat ebenfalls `Text`; kompiliert der Code wegen des Typs nicht (z. B. `TextBlock`-typisierte Felder in `PlayerChromeControls.cs`), beide als eigene `TextBlock`s in einem horizontalen `StackPanel` lassen (Name fett, Pfad normal, `TextTrimming`) und nur den Vortext „Video · " davor setzen. `Close_Click`: existiert ein Handler zum Schliessen (grep `Close_Click|CloseWindow_Click` in `PlayerWindow*.cs`)? Falls nein, in `PlayerWindow.xaml.cs` ergänzen: `private void Close_Click(object sender, RoutedEventArgs e) => Close();`. Den Chip sichtbar schalten, wo der Codiermodus eintritt/verlässt (`PlayerWindow.Coding.Lifecycle.Ui.cs`: dort, wo `CodingToolbar.Visibility` gesetzt wird, `CodierModusChip.Visibility` gleich setzen).

- [ ] **Step 4: Bedienleiste**

Die `Border Grid.Row="5"` erhält `x:Name="Bedienleiste"`; direkt nach ihrem schliessenden Tag ein Kommentar `<!-- Ende Bedienleiste -->`. Inhalt als ein `WrapPanel` in dieser Reihenfolge, alle Elemente 1:1 aus dem Bestand (Namen/Handler/Tooltips beibehalten):
1. `Play_Click` (PlayerPrimaryButton), `Pause_Click`, `Stop_Click`
2. Trenner, Geschwindigkeit: der bestehende Block mit `SpeedPresetButton` + `SpeedSlider` + Popup der `Speed05Button…Speed8Button` (aus `Grid.Column="1"` hierher verschieben; Beschriftung „Wiedergabegeschwindigkeit" entfernen, Tooltip trägt die Information); danach ein Zeit-Badge `CurrentTimeText`/`DurationText` bleiben in der Zeitleiste (Row 4) — keine Dopplung.
3. Trenner, `LiveDetectionButton`, `QuickScanButton` (+ `QuickScanStatusText`), `ManualMarkButton` (+ `MarkToolPopup`), `CodingModeButton`
4. `WeitereDropdownButton`: `<Button x:Name="WeitereDropdownButton" Style="{StaticResource PlayerButton}" Click="WeitereDropdown_Click" ToolTip="Weitere Player-Werkzeuge" AutomationProperties.Name="Weitere Player-Werkzeuge">` mit Text „Weitere" + Glyph `&#xE70D;`, und `<Popup x:Name="WeiterePopup" StaysOpen="False" Placement="Top" PlacementTarget="{Binding ElementName=WeitereDropdownButton}">` mit `Border` (CardBrush, BorderBrush, RadiusM, Padding 10) und `StackPanel`, in den verschoben werden: `CodingScreenshotButton` („Screen"), der Lautstärke-Block (`MuteButton`, `VolumeSlider`, `VolumeText`), `RateText` mit Beschriftung „Aktuelle Rate", `LiveDetectionStatusText`. Handler in `PlayerWindow.xaml.cs`: `private void WeitereDropdown_Click(object sender, RoutedEventArgs e) => WeiterePopup.IsOpen = !WeiterePopup.IsOpen;`.
Die Spaltendefinitionen des alten Rasters entfernen; Row 5 ist danach ein einziges `WrapPanel`.

- [ ] **Step 5: Seitenpanel**

Im Stil `SectionLabel` (dort, wo er definiert ist) `Typography.Capitals="AllSmallCaps"`, `FontSize` `{DynamicResource TextXS}`, `FontWeight Bold`, `Foreground MutedBrush` setzen. Inhalte/Reihenfolge des Panels bleiben (Inventar 6.8 entspricht dem Bestand: KI-Vorschläge, KI-Befunde, Codieren, Freigabe, Session).

- [ ] **Step 6: Grün** — `dotnet build AuswertungPro.sln && dotnet test tests/AuswertungPro.Next.UI.Tests --no-build --filter "DesignAuditNovaPlayer|DesignAuditPlayer|XamlActionWiring|DesignAudit"` → PASS.
- [ ] **Step 7: Commit** — `git add -A src tests && git commit -m "Nova-Etappe 2: Player mit kompaktem Kopf, Prototyp-Bedienleiste und Weitere-Menue"`

## Teil G — Training Studio

### Task 16: Drei Spalten, Titelchip „Analyse bereit", drei nummerierte Schritte, Beschriftungen von Hand-Box und Maske

**Files:**
- Modify: `src/AuswertungPro.Next.UI/Views/Windows/TrainingStudioWindow.xaml` (Grundraster, Zeilen 21–120; rechte Spalte 120–460; Expander 460–540)
- Modify: `src/AuswertungPro.Next.UI/Views/Windows/TrainingStudioWindow.xaml.cs` (`RedrawOverlay`: Beschriftungen)
- Modify: `src/AuswertungPro.Next.UI/ViewModels/TrainingStudioViewModel.cs` (`KiBereitschaftText`, gesetzt aus dem Ergebnis von `StartAiAsync`)
- Test: `tests/AuswertungPro.Next.UI.Tests/DesignAuditNovaTrainingStudioTests.cs`

Zielaufbau (Inventar 7.1–7.4): Fenster-Raster Zeilen `Auto` (Titel) / `*` (Arbeitsfläche) / `Auto` (Statuszeile); Arbeitsfläche Spalten `210 | * | 330`, Lücke 12, jede Spalte scrollt selbst.
- Links (`ScrollViewer` → `StackPanel`): Knöpfe untereinander `Fotos laden…`, `PDF laden…`, `PDF-Ordner laden…`, `Eingang laden`, `Segmentierung abarbeiten`, `Goldprüfung (90)`; Knopf `Weitere` (`x:Name="StudioWeitereButton"`, Popup `StudioWeiterePopup` mit `Gold-Eingang öffnen`, `Warteschlange laden`, `Alle Gold-Reparaturfälle`, `Goldalbum`, `KI starten`); „Rohr-DN (leer = 300)" + TextBox; „Geprüft: n / m" (bestehende Bindungen); die bisherige Sektion `BendSuggestionSection` (Expander „Vorschläge aus dem Video-Durchlauf") hierher verschieben, `IsExpanded="True"`, Tabelle mit `MaxHeight="220"`.
- Mitte: Bild + Overlay (Bestand), darunter `PdfThumbnailQueue`, darunter die Karte „Modelltest am Foto" (aus der rechten Spalte hierher).
- Rechts (`ScrollViewer` → `StackPanel`): Karte „1 · KI-Vorschlag" (Bestand „KI-Vorschlag"), Karte „2 · Fachliche Codierung" (Bestand „Codierung" + die Akzeptieren/Korrektur/Verwerfen/Nächstes-Karte darunter verschmolzen), Karte „3 · Freigabe für Training" mit Text „Getrennter Schritt. Nur persönlich bestätigte Goldsamples mit Box und Maske gelangen in den Export. Die Freigabe läuft über das Export-Register im Training Center." und Knopf `Training Center öffnen` (`Click="OpenTrainingCenter_Click"`; Handler ruft dieselbe Öffnungslogik wie `MainWindow.OpenTrainingCenter_Click` — nachsehen, welche Fabrik dort verwendet wird, und identisch aufrufen), zuletzt „Goldstandard je Hauptcode" (Bestand).
- Titelzeile: `<TextBlock Text="Training Studio (Prüfplatz)"…/>` + Chip mit `ctrl:NeuralPulseDot IsActive="{Binding IstKiBereit}"` und `Text="{Binding KiBereitschaftText}"` (KiSubtleBrush/KiTextBrush), rechts `Schliessen` (`Click="Close_Click"`).
- Overlay: in `RedrawOverlay` nach dem roten Rechteck ein `TextBlock` „Hand-Box" (Hintergrund `DangerBrush`, Text Weiss ist verboten → `Foreground="{DynamicResource StatusBadgeTextBrush}"`, TextXS, Padding 4,1) an der linken oberen Ecke der Box; nach der Maske ein `TextBlock` mit `vm.SegmentationStatusText` (der bestehende Text zur Maske; heisst er anders, den Text verwenden, der heute unter dem Bild steht) unten links der Box (Hintergrund `SuccessBrush`, Vordergrund `StatusBadgeTextBrush`).

- [ ] **Step 1: Wächter (rot)**

```csharp
using System.IO;
using System.Text.RegularExpressions;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

public sealed class DesignAuditNovaTrainingStudioTests
{
    private static string Xaml() => File.ReadAllText(DesignAuditNovaPaletteTests.RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "TrainingStudioWindow.xaml"));

    [Fact]
    public void Drei_Spalten_mit_Prototyp_Breiten_und_Titelchip()
    {
        var xaml = Xaml();
        Assert.Contains("<ColumnDefinition Width=\"210\"/>", xaml);
        Assert.Contains("<ColumnDefinition Width=\"330\"/>", xaml);
        Assert.Contains("Text=\"{Binding KiBereitschaftText}\"", xaml);
        Assert.Contains("Training Studio (Prüfplatz)", xaml);
    }

    [Fact]
    public void Rechte_Spalte_hat_drei_nummerierte_Schritte_und_keinen_Schein_Freigabeknopf()
    {
        var xaml = Xaml();
        Assert.Contains("1 · KI-Vorschlag", xaml);
        Assert.Contains("2 · Fachliche Codierung", xaml);
        Assert.Contains("3 · Freigabe für Training", xaml);
        Assert.Contains("Training Center öffnen", xaml);
        Assert.DoesNotContain("Für Training freigeben", xaml);
    }

    [Fact]
    public void Alle_bisherigen_Aktionen_bleiben_erreichbar()
    {
        var xaml = Xaml();
        foreach (var t in new[] { "Fotos laden…", "PDF laden…", "PDF-Ordner laden…", "Gold-Eingang öffnen", "Eingang laden", "Warteschlange laden", "Segmentierung abarbeiten", "Goldprüfung (90)", "Alle Gold-Reparaturfälle", "Goldalbum", "KI starten", "Akzeptieren (A)", "Korrektur speichern (K)", "Verwerfen (V)", "Nächstes (→)", "Codieren… (Katalog)", "Foto mit gewähltem Modell prüfen", "Foto allgemein mit KI prüfen" })
            Assert.Contains($"Content=\"{t}\"", xaml);
    }

    [Fact]
    public void Overlay_beschriftet_Hand_Box_und_Maske()
    {
        var cs = File.ReadAllText(DesignAuditNovaPaletteTests.RepoFile("src", "AuswertungPro.Next.UI", "Views", "Windows", "TrainingStudioWindow.xaml.cs"));
        Assert.Contains("\"Hand-Box\"", cs);
        Assert.Contains("StatusBadgeTextBrush", cs);
    }
}
```

- [ ] **Step 2: Rot**, **Step 3: Umbau** gemäss Zielaufbau. `TrainingStudioViewModel`: `[ObservableProperty] private string _kiBereitschaftText = "KI nicht gestartet"; [ObservableProperty] private bool _istKiBereit;` — in `StartAiAsync` nach `var result = await _ensureAiReady(progress, ct);`: `IstKiBereit = result.Ready; KiBereitschaftText = result.Ready ? "Analyse bereit" : "Prüfung nötig";` (Statustext bleibt in `StatusText`).
- [ ] **Step 4: Grün** — `dotnet build AuswertungPro.sln && dotnet test tests/AuswertungPro.Next.UI.Tests --no-build --filter "DesignAuditNovaTrainingStudio|TrainingStudio|XamlActionWiring|DesignAudit"` → PASS.
- [ ] **Step 5: Commit** — `git add -A src tests && git commit -m "Nova-Etappe 2: Training Studio in drei Spalten mit nummerierten Schritten"`

---
## Teil H — Bewegung und Hintergrund-Engine

### Task 17: Symbol-Hover, Hintergrund-Engine, Erscheinungsbild-Beschriftungen

**Files:**
- Create: `src/AuswertungPro.Next.UI/Controls/NetzHintergrund.xaml`, `.xaml.cs`
- Create: `src/AuswertungPro.Next.UI/Controls/NetzHintergrundModell.cs` (WPF-freie Knotenbewegung)
- Modify: `src/AuswertungPro.Next.UI/AppSettings.cs` (`public bool HintergrundEngine { get; set; } = true;`)
- Modify: `src/AuswertungPro.Next.UI/MainWindow.xaml` (Engine als erstes Kind des äusseren `Grid`; Navigationssymbol mit Hover-Skalierung)
- Modify: `src/AuswertungPro.Next.UI/Views/Pages/SettingsPage.xaml` (Beschriftungen „Hell · Glas" / „Dunkel · Cockpit", Schalter „Hintergrund-Engine"), `src/AuswertungPro.Next.UI/ViewModels/Pages/SettingsPageViewModel.cs` (`HintergrundEngine`)
- Test: `tests/AuswertungPro.Next.UI.Tests/NetzHintergrundTests.cs`, `DesignAuditNovaPaletteTests` (Einstellungen-Texte)

**Interfaces:**
- `NetzHintergrundModell(int breite, int hoehe, int knoten = 60, int startwert = 7)`: deterministische Knoten (LCG Startwert 7, Multiplikator 16807 mod 2147483647, Inventar 2), `Schritt()` bewegt jeden Knoten um ±0,125 px je Achse (Richtung aus dem Startwert), Reflexion am Rand; `Knoten` (Liste `(double X, double Y, double R)`), `Verbindungen(double maxAbstand = 170)` liefert Paare mit Alpha `1 − d/max`.
- `NetzHintergrund` (UserControl): zeichnet Knoten (`GlassBorderBrush`, Radius 1,2–2,8) und Linien (`AccentBrush`, Opacity 0,09 × Alpha) auf einem `Canvas`; läuft mit `DispatcherTimer` 33 ms NUR wenn `!MotionSettings.ReduceMotion && AppSettings.HintergrundEngine && IsVisible && Window.IsActive`; sonst ein Standbild (ein `Schritt()` beim Laden, dann Stopp). Öffnet sich ein Dialog (Fenster verliert `IsActive`), pausiert der Timer (Inventar 2, „kein Dialog offen").

- [ ] **Step 1: Tests (rot)**

```csharp
using AuswertungPro.Next.UI.Controls;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

public sealed class NetzHintergrundTests
{
    [Fact]
    public void Knoten_sind_deterministisch_und_bleiben_im_Rahmen()
    {
        var a = new NetzHintergrundModell(800, 600);
        var b = new NetzHintergrundModell(800, 600);
        Assert.Equal(60, a.Knoten.Count);
        Assert.Equal(a.Knoten[7].X, b.Knoten[7].X);
        for (var i = 0; i < 500; i++) { a.Schritt(); }
        Assert.All(a.Knoten, k => { Assert.InRange(k.X, 0, 800); Assert.InRange(k.Y, 0, 600); Assert.InRange(k.R, 1.2, 2.8); });
    }

    [Fact]
    public void Verbindungen_nur_unter_dem_Hoechstabstand_mit_abnehmendem_Alpha()
    {
        var m = new NetzHintergrundModell(2000, 2000, knoten: 2);
        var v = m.Verbindungen(170);
        Assert.All(v, x => Assert.InRange(x.Alpha, 0, 1));
    }
}
```
Dazu in `DesignAuditNovaPaletteTests`:
```csharp
    [Fact]
    public void Einstellungen_nennen_die_Stimmungen_des_Prototyps_und_die_Hintergrund_Engine()
    {
        var xaml = File.ReadAllText(RepoFile("src", "AuswertungPro.Next.UI", "Views", "Pages", "SettingsPage.xaml"));
        Assert.Contains("Hell · Glas", xaml);
        Assert.Contains("Dunkel · Cockpit", xaml);
        Assert.Contains("IsChecked=\"{Binding HintergrundEngine}\"", xaml);
        var main = File.ReadAllText(RepoFile("src", "AuswertungPro.Next.UI", "MainWindow.xaml"));
        Assert.Contains("<ctrl:NetzHintergrund", main);
    }
```

- [ ] **Step 2: Rot**, **Step 3: Modell**

```csharp
using System;
using System.Collections.Generic;

namespace AuswertungPro.Next.UI.Controls;

/// <summary>Inventar 2: 60 Knoten, deterministisch gesaet (LCG 7 / 16807 mod 2^31-1), +-0,125 px je Bild.</summary>
public sealed class NetzHintergrundModell
{
    public sealed record Knotenpunkt(double X, double Y, double R, double Vx, double Vy);
    public sealed record Verbindung(int A, int B, double Alpha);

    private readonly List<Knotenpunkt> _knoten = new();
    private readonly double _breite, _hoehe;
    private long _saat;

    public NetzHintergrundModell(int breite, int hoehe, int knoten = 60, int startwert = 7)
    {
        _breite = breite; _hoehe = hoehe; _saat = startwert;
        for (var i = 0; i < knoten; i++)
        {
            var x = Zufall() * breite; var y = Zufall() * hoehe;
            var r = 1.2 + Zufall() * 1.6;
            var vx = (Zufall() - 0.5) * 0.25; var vy = (Zufall() - 0.5) * 0.25;
            _knoten.Add(new Knotenpunkt(x, y, r, vx, vy));
        }
    }

    public IReadOnlyList<Knotenpunkt> Knoten => _knoten;

    private double Zufall()
    {
        _saat = (_saat * 16807L) % 2147483647L;
        return _saat / 2147483647.0;
    }

    public void Schritt()
    {
        for (var i = 0; i < _knoten.Count; i++)
        {
            var k = _knoten[i];
            var (x, y, vx, vy) = (k.X + k.Vx, k.Y + k.Vy, k.Vx, k.Vy);
            if (x < 0 || x > _breite) { vx = -vx; x = Math.Clamp(x, 0, _breite); }
            if (y < 0 || y > _hoehe) { vy = -vy; y = Math.Clamp(y, 0, _hoehe); }
            _knoten[i] = k with { X = x, Y = y, Vx = vx, Vy = vy };
        }
    }

    public IReadOnlyList<Verbindung> Verbindungen(double maxAbstand = 170)
    {
        var liste = new List<Verbindung>();
        for (var i = 0; i < _knoten.Count; i++)
            for (var j = i + 1; j < _knoten.Count; j++)
            {
                var d = Math.Sqrt(Math.Pow(_knoten[i].X - _knoten[j].X, 2) + Math.Pow(_knoten[i].Y - _knoten[j].Y, 2));
                if (d < maxAbstand) liste.Add(new Verbindung(i, j, 1 - d / maxAbstand));
            }
        return liste;
    }
}
```
`NetzHintergrund.xaml`: `<UserControl … IsHitTestVisible="False"><Canvas x:Name="Flaeche" ClipToBounds="True"/></UserControl>`. Code-behind: bei `SizeChanged` neues Modell mit den Massen; `Zeichne()` leert den Canvas und legt je Verbindung eine `Line` (`Stroke = AccentBrush`, `Opacity = 0.09 * Alpha`) und je Knoten eine `Ellipse` (`Fill = GlassBorderBrush`) an; Timer wie oben; `Loaded`/`Unloaded` starten/stoppen; `Window.Activated/Deactivated` pausieren. `AppSettings.HintergrundEngine` wird über `App.Services`-freien Weg gelesen: das Control bekommt `DependencyProperty IsEngineEnabled` und MainWindow bindet `IsEngineEnabled="{Binding HintergrundEngine}"` (neue Shell-Eigenschaft in `ShellViewModel.Nova.cs`: `public bool HintergrundEngine => _sp.Settings.HintergrundEngine;`, benachrichtigt aus den Einstellungen über `SettingsPageViewModel` → `_settings.HintergrundEngine = value; _settings.Save();` und ein statisches Ereignis `MotionSettings.EngineChanged`, das die Shell abonniert — `MotionSettings` erhält `public static event Action? EngineChanged;` und `public static void RaiseEngineChanged()`).

MainWindow: als erstes Kind des äusseren `<Grid>` (vor dem `DockPanel`): `<ctrl:NetzHintergrund IsEngineEnabled="{Binding HintergrundEngine}"/>`; damit der Hintergrund durchscheint, bekommt das äussere `DockPanel` `Background="Transparent"` (das Fenster behält `BgBrush`).
Navigationssymbol-Hover: im `ListBoxItem`-Template das `Grid Width="24"` des Symbols erhält `RenderTransformOrigin="0.5,0.5"` und `<Grid.RenderTransform><ScaleTransform x:Name="NavIconScale"/></Grid.RenderTransform>`; im `MouseEnter`-Storyboard des Templates zusätzlich `DoubleAnimation Storyboard.TargetName="NavIconScale" Storyboard.TargetProperty="ScaleX" To="1.12" Duration="0:0:0.2"` (und `ScaleY`), im `MouseLeave` zurück auf 1 — beide nur, wenn `MotionSettings.ReduceMotion` falsch ist: der bestehende Mechanismus im Template (`AnimDurationFast`) reagiert bereits auf `ReduceMotion`? Prüfen (`grep ReduceMotion MainWindow.xaml.cs`); wenn die Hover-Animationen heute immer laufen, denselben Weg nehmen (kurze Rückmeldungs-Animationen sind laut Einstellungstext erlaubt: „Rückmeldung beim Zeigen und Klicken bleibt erhalten").

Einstellungen: Beschriftungen des Design-Schalters „Hell" → „Hell · Glas", „Dunkel" → „Dunkel · Cockpit"; neue Zeile unter „Bewegung": `TextBlock Text="Hintergrund"` + `CheckBox Content="Hintergrund-Engine (Leitungsnetz im Hintergrund, nur Optik)" IsChecked="{Binding HintergrundEngine}" Style="{StaticResource SettingsFieldCheckBox}"` (Zeilenzahl des Rasters +1). `SettingsPageViewModel`: `[ObservableProperty] private bool _hintergrundEngine;` aus `_settings.HintergrundEngine` laden; `partial void OnHintergrundEngineChanged(bool value) { _settings.HintergrundEngine = value; _settings.Save(); MotionSettings.RaiseEngineChanged(); }`. `SettingsSearchController` liest neue Zeilen automatisch (CLAUDE.md).

- [ ] **Step 4: Grün** — `dotnet build AuswertungPro.sln && dotnet test tests/AuswertungPro.Next.UI.Tests --no-build --filter "NetzHintergrund|DesignAuditNovaPalette|DesignAudit|Settings"` → PASS.
- [ ] **Step 5: Commit** — `git add -A src tests && git commit -m "Nova-Etappe 2: Hintergrund-Engine, Symbol-Hover und Stimmungsnamen in den Einstellungen"`

## Teil I — Übrige Seiten

### Task 18: Einheitlicher Seitenkopf mit Untertitel auf elf Seiten

**Files:**
- Create: `src/AuswertungPro.Next.UI/Controls/NovaPageHeader.xaml`, `.xaml.cs`
- Modify: `ProjectPage.xaml`, `ImportPage.xaml`, `ExportPage.xaml`, `MediaConflictsPage.xaml`, `BuilderPage.xaml`, `DossiersPage.xaml`, `SanierungsMatrixPage.xaml`, `SchachtSanierungsMatrixPage.xaml`, `SchattenauswertungPage.xaml`, `VsaPage.xaml`, `DiagnosticsPage.xaml`, `SettingsPage.xaml` (alle unter `src/AuswertungPro.Next.UI/Views/Pages/`)
- Test: `tests/AuswertungPro.Next.UI.Tests/DesignAuditNovaSeitenkoepfeTests.cs`

**Interfaces:**
- `NovaPageHeader`: `DependencyProperty Title (string)`, `Subtitle (string)`, `ContentProperty` → `Aktionen` (object, rechts). Darstellung: Titel `PageTitle`-Stil (Wächter `Key_page_titles_use_page_title_style_without_accent_foreground` verlangt den Stil an Seitentiteln — deshalb im Control `Style="{StaticResource PageTitle}"`), Untertitel `TextS` in `MutedBrush` daneben (`VerticalAlignment=Bottom`, Margin 10,0,0,3), rechts `ContentPresenter` für die Aktionen; `Margin="0,0,0,12"`.
- Untertitel (Inventar 4.2–4.15): Projekt „Stammdaten des offenen Projekts"; Import „Kanalfernseh-Projekte, Protokolle, Medien"; Export „Excel, Verteilung, Kataster"; Medienkonflikte „Videos, die keiner Haltung sicher zugeordnet sind"; Druckcenter „Listen, Statistik und NPK-Leistungsverzeichnis"; Dossiers (Titel „Eigentümerdossiers") „eine Liegenschaft, ihre Leitungen und Schächte"; Sanierungs-Matrix „Massnahmen und Kosten je Haltung"; Schacht-Matrix „Massnahmen und Kosten je Schacht"; Schattenauswertung „dein Urteil neben dem der KI, ändert keine Projektdaten"; VSA (Titel „VSA-Bewertung") „Zustandsklasse und Noten nach VSA-KEK 2020"; Diagnose „Protokoll des laufenden Programms"; Einstellungen ohne Untertitel (Suchfeld bleibt).

- [ ] **Step 1: Wächter (rot)**

```csharp
using System.IO;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

public sealed class DesignAuditNovaSeitenkoepfeTests
{
    [Theory]
    [InlineData("ProjectPage.xaml", "Stammdaten des offenen Projekts")]
    [InlineData("ImportPage.xaml", "Kanalfernseh-Projekte, Protokolle, Medien")]
    [InlineData("ExportPage.xaml", "Excel, Verteilung, Kataster")]
    [InlineData("MediaConflictsPage.xaml", "Videos, die keiner Haltung sicher zugeordnet sind")]
    [InlineData("BuilderPage.xaml", "Listen, Statistik und NPK-Leistungsverzeichnis")]
    [InlineData("DossiersPage.xaml", "eine Liegenschaft, ihre Leitungen und Schächte")]
    [InlineData("SanierungsMatrixPage.xaml", "Massnahmen und Kosten je Haltung")]
    [InlineData("SchachtSanierungsMatrixPage.xaml", "Massnahmen und Kosten je Schacht")]
    [InlineData("SchattenauswertungPage.xaml", "dein Urteil neben dem der KI, ändert keine Projektdaten")]
    [InlineData("VsaPage.xaml", "Zustandsklasse und Noten nach VSA-KEK 2020")]
    [InlineData("DiagnosticsPage.xaml", "Protokoll des laufenden Programms")]
    public void Seite_traegt_den_Nova_Seitenkopf_mit_Prototyp_Untertitel(string datei, string untertitel)
    {
        var xaml = File.ReadAllText(DesignAuditNovaPaletteTests.RepoFile("src", "AuswertungPro.Next.UI", "Views", "Pages", datei));
        Assert.Contains("<ctrl:NovaPageHeader", xaml);
        Assert.Contains($"Subtitle=\"{untertitel}\"", xaml);
    }

    [Fact]
    public void Seitenkopf_verwendet_den_PageTitle_Stil()
    {
        var xaml = File.ReadAllText(DesignAuditNovaPaletteTests.RepoFile("src", "AuswertungPro.Next.UI", "Controls", "NovaPageHeader.xaml"));
        Assert.Contains("Style=\"{StaticResource PageTitle}\"", xaml);
    }
}
```

- [ ] **Step 2: Rot**, **Step 3: Control**

```xml
<UserControl x:Class="AuswertungPro.Next.UI.Controls.NovaPageHeader"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" x:Name="Root" Margin="0,0,0,12">
    <DockPanel>
        <ContentPresenter DockPanel.Dock="Right" Content="{Binding Aktionen, ElementName=Root}" VerticalAlignment="Center"/>
        <StackPanel Orientation="Horizontal" VerticalAlignment="Center">
            <TextBlock Text="{Binding Title, ElementName=Root}" Style="{StaticResource PageTitle}"/>
            <TextBlock Text="{Binding Subtitle, ElementName=Root}" Margin="10,0,0,3" VerticalAlignment="Bottom"
                       FontSize="{DynamicResource TextS}" Foreground="{DynamicResource MutedBrush}" TextTrimming="CharacterEllipsis"/>
        </StackPanel>
    </DockPanel>
</UserControl>
```
Code-behind: `[ContentProperty(nameof(Aktionen))]`, drei `DependencyProperty` (`Title`, `Subtitle`, `Aktionen`).

- [ ] **Step 4: Seiten umstellen**

Je Seite den bestehenden Titel-`TextBlock` (Stil `PageTitle`) und die daneben liegenden Kopf-Knöpfe in `<ctrl:NovaPageHeader Title="…" Subtitle="…">…Aktionen…</ctrl:NovaPageHeader>` überführen; die Knöpfe wandern unverändert (Command/Click/ToolTip) in den Inhalt des Headers. Bindende Titel (`Text="{Binding PageTitle}"` in `SanierungsMatrixPage`) bleiben als `Title="{Binding PageTitle}"`; der Wächter `SanierungsMatrixPage_zeigt_massnahmen_spalte_und_lesedetail` verlangt `Text="{Binding PageTitle}"` und `Text="{Binding PageSubtitle}"` wörtlich — dort deshalb den Header NICHT einsetzen, sondern nur den Untertitel-Text der Seite auf den Prototyp-Wortlaut prüfen und den Wächter oben für diese Datei auf `PageSubtitle`-Bindung anpassen (`Assert.Contains("Text=\"{Binding PageSubtitle}\"")` statt `Subtitle="…"`; der VM-Untertitel bekommt den Prototyp-Wortlaut). `xmlns:ctrl="clr-namespace:AuswertungPro.Next.UI.Controls"` je Seite ergänzen.

- [ ] **Step 5: Grün** — `dotnet build AuswertungPro.sln && dotnet test tests/AuswertungPro.Next.UI.Tests --no-build --filter "DesignAuditNovaSeitenkoepfe|DesignAuditThemeResource|DesignAudit|XamlActionWiring"` → PASS.
- [ ] **Step 6: Commit** — `git add -A src tests && git commit -m "Nova-Etappe 2: einheitlicher Seitenkopf mit Untertitel auf allen Seiten"`

## Teil J — Abnahme

### Task 19: Prüfhost, Bildschirmfotos, Gesamtlauf, Doku

**Files:**
- Create: `docs/reviews/2026-09-06-nova/wpf-etappe-2/werkzeug/Pruefhost.csproj`, `Program.cs` (Kopie aus `docs/reviews/2026-09-06-nova/wpf-etappe-1/abschluss/werkzeug/`, angepasst)
- Create: `docs/reviews/2026-09-06-nova/wpf-etappe-2/ABNAHME.md`, `bilder/*.png`
- Modify: `CLAUDE.md` (Abschnitt „Nova-Etappe 2")
- Modify: `AuswertungPro.sln`: NICHT — der Prüfhost liegt unter `docs/`, nicht `tools/`, und bleibt ausserhalb der Solution (wie in Etappe 1).

- [ ] **Step 1: Prüfhost anpassen**

`Pruefhost.csproj`: `NovaBin` → `C:/Sewer-Studio_KI_4.5-nova/src/AuswertungPro.Next.UI/bin/Debug/net10.0-windows10.0.19041`. `Program.cs`: `Root` → `C:\Sewer-Studio_KI_4.5-nova\.tmp\nova-etappe2\bedienung`, `Bin` → derselbe `NovaBin`, `App.xaml`-Pfad → Worktree. Aufruf `Pruefhost.exe <Theme> <Seite> <Ausgabe.png> [Breite Hoehe]`: nach `window.Show()` das Projekt öffnen (`((ShellViewModel)window.DataContext).TryOpenProject(projectPath)`), dann `EnterWorkspaceOn(<Seite>)`, 2 s warten, dann Bildschirmfoto:

```csharp
    static void Foto(Window w, string pfad)
    {
        var dpi = VisualTreeHelper.GetDpi(w);
        var bmp = new RenderTargetBitmap((int)(w.ActualWidth * dpi.DpiScaleX), (int)(w.ActualHeight * dpi.DpiScaleY), dpi.PixelsPerInchX, dpi.PixelsPerInchY, PixelFormats.Pbgra32);
        bmp.Render(w);
        var enc = new PngBitmapEncoder(); enc.Frames.Add(BitmapFrame.Create(bmp));
        using var fs = File.Create(pfad); enc.Save(fs);
    }
```
und danach `Shutdown`. Das Testprojekt aus `CreateProject` um zwei Protokolleinträge mit `Uhr_von`/`Uhr_bis`, Stufe und einem offenen KI-Befund erweitern, damit Rohrring, KI-Hinweis und Aufgaben-Chip sichtbar sind; ein Schacht mit `Schachtform=Oval`, `Dimension 1 mm=1100`, `Dimension 2 mm=900`.

- [ ] **Step 2: Bilder erzeugen** (Full HD 1920×1080, beide Themes) für `Uebersicht`, `Haltungen`, `Schaechte`, `Import`, `Einstellungen`; Player und Training Studio je einmal (Player über `PlayVideoCommand` der gewählten Haltung mit dem künstlichen Clip aus Etappe 1 `.tmp/nova-abschluss/bedienung/…mp4` — Pfad im Prüfhost kopieren; Training Studio über `MainWindow.OpenTrainingStudio_Click` per Reflection oder die Fabrik aus `TrainingStudioWindowDependencyFactory`). Jedes Bild ansehen (Read) und mit dem Prototyp-Bild in `optimiert/v2/nachweise/` vergleichen; Abweichungen im ABNAHME.md benennen.

Run: `dotnet build docs/reviews/2026-09-06-nova/wpf-etappe-2/werkzeug/Pruefhost.csproj && docs/reviews/2026-09-06-nova/wpf-etappe-2/werkzeug/bin/Debug/net10.0-windows10.0.19041/Pruefhost.exe Light Haltungen docs/reviews/2026-09-06-nova/wpf-etappe-2/bilder/haltungen-hell.png 1920 1080` (und die übrigen Kombinationen).

- [ ] **Step 3: Gesamtlauf**

Run: `dotnet build AuswertungPro.sln -c Release && dotnet test AuswertungPro.sln -c Release --no-build`
Expected: 0 Fehler, 0 Warnungen; alle vier Testprojekte grün (Etappe-1-Stand: 6170 / 2562 / 6414 / 62 bestanden — neue Tests kommen dazu, keiner fällt weg). Läuft `SewerStudio.exe` im Hauptbaum, stört das nicht: der Worktree hat eigene `bin`-Ordner.

- [ ] **Step 4: ABNAHME.md** mit Tabelle je Task (Prüfpunkt, Nachweis-Bild, bestanden/abweichend), den bewussten Abweichungen (Global Constraints: dunkler Akzent, Zustandsfarben, Verwerfen, Avatar, Freigabe-Knopf, Donut→Legende, de-CH-Punkt) und den Grenzen (Prüfhost ohne produktiven Start, keine Skalierungsmessung 125/150 % in dieser Etappe → beim Merge in den Hauptbaum als offene Sichtprüfung durch Pascal ausweisen).

- [ ] **Step 5: CLAUDE.md** — Abschnitt „Nova-Etappe 2 (2026-09-0x)" nach „Nova-Etappe 1": in je einer Zeile die neuen Regeln und Wächter (Paletten-Tokens, `AccentTextBrush`/`FaintBrush`/`GlassBorderBrush`, Pillen, Kapitälchen-Kopf, `HaltungPruefstatus`/`NaechsteAufgabeRegel`, globale Suche, `KiBereitschaftRegel`, `ProjektUebersichtPage` nur im Workspace, `ICodingSuggestionRegistry` (144 Vertragstypen), Umschalter „Alte Haltungsansicht" im Menü, `RohrringGeometrie` (echte Uhrlage vor Indexregel), Schächte mit `ShowSchaechteNovaLayout`, Player-`Bedienleiste`/`WeiterePopup`, Training Studio 210|*|330, `NetzHintergrund` + `AppSettings.HintergrundEngine`, `NovaPageHeader`) und die Liste der Wächtertests.

- [ ] **Step 6: Commit und Übergabe**

```bash
git add -A docs CLAUDE.md
git commit -m "Nova-Etappe 2: Abnahme mit Pruefhost-Bildern, CLAUDE.md nachgefuehrt"
```
Danach `superpowers:finishing-a-development-branch`: Branch `feature/nova-etappe-2` in `feature/eval-pruefsatz-review` mergen (im Hauptbaum, nur wenn dort `git status` sauber ist — sonst Pascal fragen), Push.

---

## Selbstprüfung des Plans (durchgeführt beim Schreiben)

- **Abdeckung gegen Inventar:** 1 Tokens → Task 1/2; 2 Bewegung → Task 17; 3.2 Leiste → Task 5/7/17; 3.4 Kopfzeile → Task 5/6; 3.5 Tastenkürzel → Strg+K (Task 6), F3 und F11 bestehen; 4.1 → Task 8/9/10; 4.3 → Task 11/12 (+ Etappe 1); 4.4 → Task 13/14; 4.5–4.15 → Task 18 (Seitenköpfe) + Task 17 (Einstellungen); 5 → Task 12/14; 6 → Task 15; 7 → Task 16; 8.1 → Task 4/5; 8.5 → Task 6; 8.6 → Task 11.
- **Bewusst nicht umgesetzt** (Global Constraints): Verwerfen, Avatar, Schein-Freigabe, Prototyp-Hellblau als Flächenakzent im Dunkeln, andere Zustandsfarben, Donut (durch anklickbare Legende ersetzt), Demo-Inhalte der Restseiten (Import-Baum, Beispiel-Log usw. sind Beispieldaten des Prototyps, keine Funktionen).
- **Typkonsistenz:** `DataPageColumnView.Anzahl(int)` (Task 11) wird von Task 13 über denselben Record verwendet; `SchadenStufeQuelleConverter` (Task 12) wird in Task 14 wiederverwendet; `DesignAuditNovaPaletteTests.RepoFile` ist `internal static` und wird von den späteren Wächtern genutzt; `ShellViewModel.NaechsteAufgabePruefenCommand` (Task 5) wird in Task 10 gebunden; `ICodingSuggestionRegistry` (Task 9) in Task 10; `HaltungPruefstatus` (Task 4) in Task 8 und 12.
