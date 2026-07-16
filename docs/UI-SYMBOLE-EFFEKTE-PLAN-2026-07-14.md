# SewerStudio — Symbole & dezente Effekte: Umsetzungsplan (fuer Codex)

**Erstellt:** 2026-07-14 von Fable (Claude)
**Branch/Stand:** `feature/gis-karte`, Working Tree sauber — vor Beginn `git status` pruefen
**Scope:** Optische Aufwertung der Symbolwelt (Icons/Glyphen) und dezente visuelle Effekte im ganzen Programm. Schwerpunkt: Rechtsklick-Menues und oberes Menue. KEINE Logik-Aenderungen, KEINE KI-Pipeline, KEINE neuen NuGet-Pakete.
**Ziel:** Das Programm wirkt wertiger: eine konsistente, lebendige Icon-Sprache statt bieder gemischter Text-Zeichen/Emoji; Menues mit Icons, Tastenkuerzeln und sanften Einblendungen — professionell, kein Spielzeug.

> **Wichtig:** Die Ordner `.claude/worktrees/` und `.worktrees/` enthalten Kopien der Codebase — NIEMALS dort aendern. Nur unter `src/AuswertungPro.Next.UI/` arbeiten.

---

## Leitbild: Die Icon-Sprache (verbindlich fuer alle Pakete)

1. **Eine Icon-Schrift:** `Segoe Fluent Icons` (Windows 11), Fallback `Segoe MDL2 Assets`. Zentral definiert — nie mehr `FontFamily="Segoe MDL2 Assets"` einzeln streuen.
2. **Glyphen statt Emoji:** Auf Buttons, Menues, Headern und Tabs nur Fluent-Glyphen. Emoji sind nur in lockeren Status-/Logtexten erlaubt (z. B. Trainings-Log), nirgends als Bedienelement.
3. **Semantische Farben (immer `DynamicResource`):**
   - Standard-Icon: `MutedBrush`
   - Aktions-/KI-Icons (Analyse, Start, Vorschlaege): `AccentBrush`
   - Destruktiv (Loeschen, Entfernen): `DangerBrush` — **hardcodiertes `OrangeRed` ist verboten und wird ersetzt**
   - Erfolg/Bestaetigen: `SuccessBrush`; Warnung: `WarningBrush`
4. **Gleiche Aktion = gleiches Glyph ueberall** (siehe Referenztabelle am Ende).
5. **`MenuItem.Icon`-Slot statt StackPanel-Bastelei:** Vorhandene Menue-Header mit eingebettetem `StackPanel`+Icon-TextBlock werden auf `Header="Text"` + `MenuItem.Icon` umgebaut.
6. **Groessen:** Menue-Icons 13 px, Toolbar 13–14 px, Navigations-Icons wie bisher, EmptyState gross.
7. **Checkbare Menuepunkte (`IsCheckable="True"`) bekommen KEIN Icon** — der Haken belegt den Slot.

---

## Betroffene Dateien (am 14.07. gelesen und verifiziert)

| Datei | Rolle |
|---|---|
| `src/AuswertungPro.Next.UI/Theme/Controls.xaml` | Menue-/ContextMenu-/ToolTip-Templates, Animations-Tokens (Zeile 12–22), FontMono-Muster (Zeile 25) |
| `src/AuswertungPro.Next.UI/MainWindow.xaml` | Oberes Menue (Zeile 47–84), Statusleiste |
| `src/AuswertungPro.Next.UI/RichToolTipContent.cs` | Muster fuer kleine UI-Hilfsklassen im Root-Namespace |
| `src/AuswertungPro.Next.UI/ViewModels/ShellViewModel.cs` | Navigations-Glyphen (Zeile 121–208) |
| `src/AuswertungPro.Next.UI/Views/Pages/DataPage.xaml` | 4 Kontextmenues (Zeilen 156, 184, 235, 495) |
| `src/AuswertungPro.Next.UI/Views/Pages/Haltungsansicht/HaltungsansichtView.xaml` | Kontextmenue (Zeile 76) |
| `src/AuswertungPro.Next.UI/Views/Pages/Schachtansicht/SchachtansichtView.xaml` | Kontextmenue (Zeile 36) |
| `src/AuswertungPro.Next.UI/Views/Pages/SchaechtePage.xaml` | Kontextmenue (Zeile 275) |
| `src/AuswertungPro.Next.UI/Views/Pages/ImportPage.xaml` | Kontextmenue mit Inline-Icons (Zeile 67 ff.) |
| `src/AuswertungPro.Next.UI/Views/Pages/ExportPage.xaml` | Kontextmenue mit Inline-Icons (Zeile 53 ff.) |
| `src/AuswertungPro.Next.UI/Views/Pages/BuilderPage.xaml` | Kontextmenue (Zeile 430) |
| `src/AuswertungPro.Next.UI/Views/Controls/RecordDetailsView.xaml` | ContextMenu-Ressource (Zeile 11) |
| `src/AuswertungPro.Next.UI/Views/Windows/PlayerCodingSidePanel.xaml` | 2 Kontextmenues (Zeilen 123, 346 — mit ✓-Textzeichen) |
| `src/AuswertungPro.Next.UI/Views/Windows/SanierungsmassnahmenWindow.xaml` | 1 Kontextmenue |
| `src/AuswertungPro.Next.UI/Theme/Theme.xaml` + `ThemeLight.xaml` | Dunkles/helles Theme — Menue-Duplikate darin NICHT anfassen (siehe Paket B, Hinweis) |

**Vorhandene Muster:** Animations-Tokens `AnimDurationFast/Normal/Slow`, `AnimEaseOut/In`, Radien `RadiusS/M/L/XL` (Controls.xaml Zeile 12–22). Button-Hover/Press ist bereits animiert (ThemeLight.xaml Zeile 203–250) — als Stil-Referenz fuer „dezent" verwenden.

---

## Arbeitsregeln fuer Codex

1. Vor Beginn: `dotnet build AuswertungPro.sln` + `dotnet test AuswertungPro.sln` — Baseline muss gruen sein.
2. **Pro Paket ein Commit** (Commit-Messages auf Deutsch), nach jedem Paket Build + Tests.
3. Theme-Regel: **niemals hardcodierte Farben**, immer `{DynamicResource ...}`. Das gilt auch fuer Icon-Foregrounds.
4. Nach jeder XAML-Aenderung: alle `{Binding ...}`-Pfade unveraendert lassen bzw. gegen ViewModel-Properties pruefen.
5. UI-Texte und Kommentare auf Deutsch.
6. **Nichts in `Theme/Theme.xaml`/`ThemeLight.xaml` fuer Menues aendern** — die Menue-Styles dort sind totes Fallback; `Controls.xaml` wird zuletzt gemerged und gewinnt in BEIDEN Themes (der `ThemeManager` tauscht nur das Theme-Dictionary an seinem Index, `Services/ThemeManager.cs:30-59`).
7. Wo ein XAML den Praefix `ui:` noch nicht kennt: `xmlns:ui="clr-namespace:AuswertungPro.Next.UI"` ergaenzen.
8. Glyph-Codepoints aus der Referenztabelle verwenden. Rendert ein Glyph als leeres Kaestchen (falscher Codepoint), Ersatz aus der Tabelle waehlen — nicht raten.

---

## PAKET A — Infrastruktur: FluentIcon + zentrale Icon-Schrift — Aufwand: S

### A1 — Statische Icon-Schrift-Klasse
**Neue Datei:** `src/AuswertungPro.Next.UI/IconFonts.cs` (Root-Namespace, Muster: `RichToolTipContent.cs`)
```csharp
using System.Windows.Media;

namespace AuswertungPro.Next.UI;

/// <summary>Zentrale Icon-Schrift: Windows 11 rendert "Segoe Fluent Icons" moderner,
/// aeltere Systeme fallen auf "Segoe MDL2 Assets" zurueck (Codepoints kompatibel).</summary>
public static class IconFonts
{
    public static FontFamily Default { get; } = new("Segoe Fluent Icons, Segoe MDL2 Assets");
}
```

### A2 — FluentIcon-Control
**Neue Datei:** `src/AuswertungPro.Next.UI/FluentIcon.cs`
```csharp
using System.Windows;
using System.Windows.Controls;

namespace AuswertungPro.Next.UI;

/// <summary>Kleines Icon-Element fuer Menues, Buttons und Header.
/// Verwendung: &lt;ui:FluentIcon Glyph="&amp;#xE74E;" Foreground="{DynamicResource MutedBrush}"/&gt;</summary>
public sealed class FluentIcon : TextBlock
{
    public static readonly DependencyProperty GlyphProperty = DependencyProperty.Register(
        nameof(Glyph), typeof(string), typeof(FluentIcon),
        new PropertyMetadata(string.Empty, static (d, e) => ((FluentIcon)d).Text = e.NewValue as string ?? string.Empty));

    public string Glyph
    {
        get => (string)GetValue(GlyphProperty);
        set => SetValue(GlyphProperty, value);
    }

    public FluentIcon()
    {
        FontFamily = IconFonts.Default;
        FontSize = 13;
        VerticalAlignment = VerticalAlignment.Center;
        HorizontalAlignment = HorizontalAlignment.Center;
    }
}
```

### A3 — XAML-Ressource fuer bestehende TextBlock-Icons
In `Theme/Controls.xaml` direkt nach `FontMono` (Zeile 25):
```xml
<!-- Icon-Schrift: Fluent (Win11) mit MDL2-Fallback. Fuer TextBlock-Glyphen; fuer neue Icons ui:FluentIcon verwenden. -->
<FontFamily x:Key="FontIcon">Segoe Fluent Icons, Segoe MDL2 Assets</FontFamily>
```

**Test:** Ein fokussierter Unit-Test (falls STA-Testinfra vorhanden, sonst weglassen): `FluentIcon.Glyph` setzen → `Text` uebernimmt den Wert.
**Commit:** `feat(ui): FluentIcon-Control und zentrale Icon-Schrift (Fluent mit MDL2-Fallback)`

---

## PAKET B — Menue-Templates: Icons, Tastenkuerzel, Hover-Akzent, Einblendung — Aufwand: L

Alle Aenderungen in `Theme/Controls.xaml` (Menue-Block Zeile 593–887). Die Templates ignorieren bisher `MenuItem.Icon` und `InputGestureText` komplett — das ist der Kern der Aufwertung.

### B1 — `SubmenuItemTemplate` (Zeile 762) erweitern
Neue Spaltenstruktur im inneren Grid: `24 | * | Auto`.
1. **Spalte 0:** `ContentPresenter x:Name="IconSlot" ContentSource="Icon"` (zentriert). Der vorhandene Check-Glyph-TextBlock (`&#xE73E;`, Zeile 775–783) bleibt, bekommt aber `FontFamily="{StaticResource FontIcon}"`. Trigger `IsChecked=True`: Check sichtbar, `IconSlot` collapsed (Haken hat Vorrang).
2. **Spalte 2 (neu):** Tastenkuerzel:
```xml
<TextBlock Text="{TemplateBinding InputGestureText}"
           Foreground="{DynamicResource MutedBrush}"
           FontSize="11"
           Margin="24,0,4,0"
           VerticalAlignment="Center"/>
```
3. **Hover-Akzentbalken (das „Leben"):** Im Wurzel-Element (Border in Grid wrappen) links ein Balken:
```xml
<Border x:Name="AccentBar" Width="3" Height="14" CornerRadius="1.5"
        Background="{DynamicResource AccentBrush}"
        HorizontalAlignment="Left" VerticalAlignment="Center"
        Margin="1,0,0,0" Opacity="0"/>
```
Trigger `IsHighlighted=True`: EnterActions-Storyboard `Opacity` → 1 (`{StaticResource AnimDurationFast}`, EasingFunction `{StaticResource AnimEaseOut}`), ExitActions → 0. Bestehende Hover-Setter (HoverBrush usw.) bleiben.

### B2 — `SubmenuHeaderTemplate` (Zeile 679) gleich behandeln
Gleiche Icon-Spalte + Akzentbalken wie B1. Zusaetzlich das Pfeil-Dreieck (`Path Data="M0,0 L0,8 L5,4 Z"`, Zeile 707–715) ersetzen durch:
```xml
<TextBlock Grid.Column="2" Text="&#xE76C;" FontFamily="{StaticResource FontIcon}"
           FontSize="10" Foreground="{DynamicResource MutedBrush}"
           VerticalAlignment="Center" Margin="8,0,0,0"/>
```

### B3 — Popup-Einblendung (Top-Level + Submenu)
In `TopLevelMenuItemTemplate` (Popup Zeile 608) und `SubmenuHeaderTemplate` (Popup Zeile 718):
1. `PopupAnimation="Slide"` → `PopupAnimation="None"` (sonst doppelte Bewegung).
2. Dem Popup-Border `RenderTransform` mit `TranslateTransform x:Name="PopupShift"` geben; `Opacity` bleibt 1 als Basiswert.
3. Trigger `IsSubmenuOpen=True` EnterActions (From-basiert, damit es bei jedem Oeffnen neu spielt und der Endwert dem Basiswert entspricht):
```xml
<BeginStoryboard>
    <Storyboard>
        <DoubleAnimation Storyboard.TargetName="PopupBorder" Storyboard.TargetProperty="Opacity"
                         From="0" To="1" Duration="{StaticResource AnimDurationNormal}"
                         EasingFunction="{StaticResource AnimEaseOut}"/>
        <DoubleAnimation Storyboard.TargetName="PopupShift" Storyboard.TargetProperty="Y"
                         From="-6" To="0" Duration="{StaticResource AnimDurationNormal}"
                         EasingFunction="{StaticResource AnimEaseOut}"/>
    </Storyboard>
</BeginStoryboard>
```
(Dem Popup-Border dafuer `x:Name="PopupBorder"` geben. Beim Submenu-Header, das nach rechts oeffnet, stattdessen `X` von `-6` nach `0` animieren.)

### B4 — ContextMenu-Template (Zeile 828): Einblendung
Auf dem Wurzel-Border des Templates ein `EventTrigger RoutedEvent="Loaded"` mit demselben From-basierten Storyboard (Opacity 0→1, TranslateY -4→0, `AnimDurationNormal`, `AnimEaseOut`). `Loaded` feuert bei jedem Oeffnen, weil der Popup-Host den Baum neu anhaengt.

### B5 — Separator mit Verlauf (Zeile 856)
```xml
<Border Height="1" Margin="10,5">
    <Border.Background>
        <LinearGradientBrush StartPoint="0,0" EndPoint="1,0">
            <GradientStop Color="Transparent" Offset="0"/>
            <GradientStop Color="#809EAEC4" Offset="0.5"/>
            <GradientStop Color="Transparent" Offset="1"/>
        </LinearGradientBrush>
    </Border.Background>
</Border>
```
(Die Mittenfarbe entspricht `BorderBrush` mit Alpha — Verlaufs-Stops koennen keine DynamicResource-Brushes referenzieren; dieser eine dekorative Wert ist als Ausnahme dokumentiert und funktioniert auf hell wie dunkel.)

### B6 — ComboBox-Dropdown (Zeile 89–113): gleiche Loaded-Einblendung wie B4 auf dem Dropdown-Border (dezent, gleiche Dauer).

**Sichtpruefung (durch Pascal, nach Umsetzung):** Menue oeffnen (hell + dunkel), Untermenue, Rechtsklick im Daten-Grid, ComboBox — Einblendung fluessig, kein Flackern, Haken bei checkbaren Items weiterhin sichtbar.
**Commit:** `feat(ui): Menue-Templates mit Icon-Slot, Tastenkuerzeln, Hover-Akzent und sanfter Einblendung`

---

## PAKET C — ToolTip veredeln — Aufwand: S

`Theme/Controls.xaml` Zeile 1238–1248: Der ToolTip-Style hat kein Template (eckige Standardbox).
1. ControlTemplate ergaenzen: Border mit `CornerRadius="{StaticResource RadiusM}"`, `Background="{DynamicResource CardBrush}"`, `BorderBrush="{DynamicResource BorderBrush}"`, `Padding="{TemplateBinding Padding}"`, weicher Schatten (`DropShadowEffect Color="#30000000" BlurRadius="14" ShadowDepth="3" Direction="270"`), dafuer aussen `Margin="0,0,10,10"` als Schattenraum und `HasDropShadow=False`.
2. Loaded-EventTrigger: Opacity From 0 → 1, `AnimDurationFast`.
3. **Fallback:** Zeigen die Ecken nach der Umsetzung schwarze Artefakte (Popup ohne Transparenz), Template ohne Margin/Schatten lassen und nur `CornerRadius` + Einblendung behalten — bei der Sichtpruefung klaeren.

**Commit:** `feat(ui): ToolTips mit runden Ecken, Schatten und Einblendung`

---

## PAKET D — Oberes Menue: Icons + Tastenkuerzel — Aufwand: S

`MainWindow.xaml` Zeile 51–83. Pro Eintrag `MenuItem.Icon` setzen (Muster):
```xml
<MenuItem Header="Speichern" Command="{Binding SaveCommand}" InputGestureText="Strg+S">
    <MenuItem.Icon>
        <ui:FluentIcon Glyph="&#xE74E;" Foreground="{DynamicResource MutedBrush}"/>
    </MenuItem.Icon>
</MenuItem>
```

| Eintrag | Glyph | Foreground |
|---|---|---|
| Neues Projekt | `&#xE710;` (Add) | MutedBrush |
| Projekt oeffnen... | `&#xE838;` (FolderOpen) | MutedBrush |
| Speichern | `&#xE74E;` (Save) | MutedBrush |
| Speichern unter... | `&#xE792;` (SaveAs) | MutedBrush |
| Beenden | `&#xE7E8;` (PowerButton) | MutedBrush |
| Code-Katalog... | `&#xE8F1;` (Library) | MutedBrush |
| KI starten | `&#xE945;` (LightningBolt) | **AccentBrush** |
| KI Videoanalyse – Training Center... | `&#xEA80;` (Lightbulb) | **AccentBrush** |
| Karte... | `&#xE707;` (MapPin) | MutedBrush |
| Fokusmodus | — (checkbar, kein Icon) | — |
| Karte abkoppeln | `&#xE8A7;` (OpenInNewWindow) | MutedBrush |
| System-Monitor oeffnen | `&#xE9D9;` (Diagnostic) | MutedBrush |

Zusaetzlich:
- „Beenden": `InputGestureText="Alt+F4"` ergaenzen.
- „Fokusmodus (F11)": Header auf „Fokusmodus" kuerzen, `InputGestureText="F11"` setzen (die Anzeige kommt jetzt aus Paket B).

**Commit:** `feat(ui): Icons und Tastenkuerzel im Hauptmenue`

---

## PAKET E — Rechtsklick-Menues aufwerten — Aufwand: L

**Muster fuer alle:** Icons ueber `MenuItem.Icon` + `ui:FluentIcon` (Foreground `MutedBrush`, destruktiv `DangerBrush`, KI/Vorschlaege `AccentBrush`). Vorhandene Inline-`StackPanel`-Header (ImportPage, ExportPage, DataPage 184/235) auf `Header="Text"` + Icon-Slot umbauen — gleiche Glyphen weiterverwenden, nur der Slot wechselt. **Jedes `Foreground="OrangeRed"` wird zu `Foreground="{DynamicResource DangerBrush}"`.**

### E1 — `DataPage.xaml:495` (Haupt-Datengrid) und `Haltungsansicht/HaltungsansichtView.xaml:76` (identische Aktionen, identische Icons)
| Eintrag | Glyph |
|---|---|
| Position nach oben / unten | `&#xE74A;` / `&#xE74B;` |
| Beobachtungen... | `&#xE890;` (View) |
| Play / Play (Gegeninspektion) | `&#xE768;` (Play, beide — gleiche Aktion) |
| Haltungsprotokoll AWU drucken... | `&#xE749;` (Print) |
| Haltungsprotokoll Original (PDF) / Dichtheitspruefung (PDF) | `&#xE8A5;` (OpenFile) |
| Gehe zu Ordner | `&#xE838;` (FolderOpen) |
| Sanierungsmassnahmen... | `&#xE90F;` (Repair) |
| Markierte Zeilen loeschen / Haltung loeschen | `&#xE74D;` (Delete), Foreground + Icon `DangerBrush` |

### E2 — `SchaechtePage.xaml:275` und `Schachtansicht/SchachtansichtView.xaml:36`
| Eintrag | Glyph |
|---|---|
| Details anzeigen... | `&#xE946;` (Info) |
| Sanierungsmassnahmen... | `&#xE90F;` (Repair) |
| Protokoll (PDF)... | `&#xE8A5;` (OpenFile) |
| Gehe zu Ordner | `&#xE838;` (FolderOpen) |
| Position nach oben / unten (nur Schachtansicht) | `&#xE74A;` / `&#xE74B;` |
| Schacht loeschen | `&#xE74D;`, `DangerBrush` |

### E3 — `DataPage.xaml:156` (Sanierungs-Button-Menue)
| Eintrag | Glyph |
|---|---|
| Sanierungsmassnahme bearbeiten | `&#xE70F;` (Edit) |
| Vorschlag aus Schadenscodes | `&#xEA80;` (Lightbulb), `AccentBrush` |
| Alle Massnahmen vorschlagen | `&#xEA80;` (Lightbulb), `AccentBrush` |

### E4 — `DataPage.xaml:184` (Hydraulik-Menue)
Das Wassertropfen-**Emoji** (`&#x1F4A7;`, Segoe UI Emoji) fliegt raus:
| Eintrag | Glyph |
|---|---|
| Hydraulik berechnen | `&#xE8EF;` (Calculator), `AccentBrush` |
| Hydraulik PDF | `&#xE749;` (Print) — Glyph bleibt, nur in den Icon-Slot |

### E5 — `DataPage.xaml:235` (Spalten-Menue)
`E74A`/`E74B` von Inline-StackPanel in den Icon-Slot. „Spalten anordnen" ist checkbar → kein Icon.

### E6 — `PlayerCodingSidePanel.xaml:123` (Codier-Ereignisse)
| Eintrag | Glyph |
|---|---|
| Bearbeiten (Doppelklick) | `&#xE70F;` (Edit) |
| Fotos anzeigen | `&#xE8B9;` (Picture) |
| Streckenschaden schliessen (Ende hier) | `&#xE930;` (Completed) |
| Zum Zeitpunkt springen | `&#xE823;` (Recent/Uhr) |
| Loeschen | `&#xE74D;`, `DangerBrush` |

### E7 — `PlayerCodingSidePanel.xaml:346` (Import-Referenzen) — **biederstes Menue der App**
Die `&#x2713;`-Textzeichen in den Headern entfernen; stattdessen:
| Eintrag | Glyph |
|---|---|
| Bestaetigen → ins KI-Brain | `&#xE8FB;` (Accept), `SuccessBrush` |
| Bestaetigen (als Training uebernehmen) | `&#xE8FB;` (Accept), `SuccessBrush` |
| Fotos anzeigen | `&#xE8B9;` |
| Bearbeiten (Code / BBox ziehen) | `&#xE70F;` |
| Zum Zeitpunkt springen | `&#xE823;` |

### E8 — `ImportPage.xaml:67` / `ExportPage.xaml:53`
Nur Pattern-Umbau: Inline-StackPanel-Icons in den `MenuItem.Icon`-Slot, vorhandene Glyphen (`E968`, `E8F1`, `E8B7`, ...) beibehalten, Foreground `MutedBrush` ergaenzen wo keiner gesetzt ist.

### E9 — `BuilderPage.xaml:430`
| Eintrag | Glyph |
|---|---|
| Kostenblatt (diese Haltung) | `&#xE749;` (Print) |
| Volles Dossier (diese Haltung) | `&#xE749;` (Print) |
Slider-Eintraege (Zeilenhoehe usw.): unveraendert.

### E10 — `RecordDetailsView.xaml:11` (ManagedOptionsContextMenu)
| Eintrag | Glyph |
|---|---|
| Liste bearbeiten... | `&#xE70F;` (Edit) |
| Vorschau | `&#xE890;` (View) |

### E11 — `SanierungsmassnahmenWindow.xaml` (1 Kontextmenue)
Datei lesen, Aktionen nach Referenztabelle behandeln (bearbeiten E70F, loeschen E74D+Danger, hinzufuegen E710 usw.).

**Commit:** `feat(ui): Rechtsklick-Menues mit Icons, semantischen Farben und DangerBrush statt OrangeRed`

---

## PAKET F — Biedere Symbole ersetzen (Inventur-Ergebnis)

**Grundlage:** Icon-Inventur vom 14.07. (9 Auditoren, 355 sichtbare Symbole, davon 111 als bieder eingestuft). Jeder Eintrag unten ist am Fundort verifiziert; wo eine Aktion schon in Paket B/E steckt, ist es vermerkt (nicht doppelt umsetzen).

**Regeln fuer alle F-Unterpakete:**
- Nackte Textzeichen als Button-Inhalt werden zu `ui:FluentIcon` (Glyph laut Tabelle) — als eigener `TextBlock`/`FluentIcon` VOR dem Label im `StackPanel`, nicht als Teil des Content-Strings.
- `Foreground="Red"`/`"OrangeRed"`/`#EF4444` bei Loeschen → `{DynamicResource DangerBrush}`; gruene Bestaetigung → `{DynamicResource SuccessBrush}`.
- Icon-Buttons bekommen feste Groesse (24×24 oder 28×24), `CornerRadius 4–6`, dezenten Hover (Akzent- bzw. Danger-Brush ~10–15 % Opacity).

### F1 — Navigation & globale Pfeile (hoechste Sichtbarkeit) — Aufwand: M
| Datei | Zeile | Ist | Soll |
|---|---|---|---|
| `MainWindow.xaml` | 346 | Nav-Icon bleibt im selektierten Zustand grau | Im Nav-`DataTemplate` Trigger: `ListBoxItem.IsSelected=True` → Icon-Foreground `AccentBrush` (der Blickfang der App) |
| `MainWindow.xaml` | 408 | Buchstabe „V" im VSA-Punktwolken-Icon | „V" entfernen, Punktwolke allein wirken lassen (Titel „VSA" steht daneben) |
| `Theme/Controls.xaml` | 60 | ComboBox-Dropdown: gefuelltes Dreieck | Gemeinsame **Strich-Chevron**-Ressource: `Path Data="M1,1 L5,5 L9,1"`, `Stroke="{DynamicResource MutedBrush}"`, `StrokeThickness=1.5`, runde Caps |
| `Theme/Controls.xaml` | 304 | Expander: gefuelltes Dreieck | Strich-Chevron `M1,1 L5,5 L1,9`, Rotations-Animation unveraendert lassen |
| `Theme/Controls.xaml` | 400 | TreeViewItem: gefuelltes Dreieck | Identisch — **eine** `ChevronGeometry`/Style-Ressource fuer ComboBox+Expander+TreeView |
| `Theme/Controls.xaml` | 715 | Untermenue-Dreieck | **Bereits in Paket B2** (`&#xE76C;`) — hier nichts extra |

**Commit:** `feat(ui): Nav-Akzent im aktiven Eintrag und einheitliche Strich-Chevrons`

### F2 — Browse-Buttons „…" vereinheitlichen — Aufwand: S (grosser Effekt)
Acht Buttons zeigen nacktes `...`/`…`. **Zentral loesen:** einen `SettingsBrowseButton`-Style anlegen (Icon `&#xE838;` FolderOpen bzw. `&#xE8E5;` OpenFile bei Datei-Auswahl, 14 px, `AccentBrush`, ~32×28, `CornerRadius 4`) und auf alle anwenden:
- `SettingsPage.xaml` Zeilen 392, 402, 412, 575, 615 → FolderOpen; Zeile 593 (`pdftotext.exe`) → OpenFile
- `ExportPage.xaml` Zeile 141 (Ziel-Wurzel je Verteil-Karte) → FolderOpen

**Commit:** `feat(ui): einheitliche Browse-Buttons mit Ordner-Glyph statt drei Punkten`

### F3 — Editor-Dialoge: ASCII-Buttons zu Glyphen — Aufwand: M
| Datei | Zeile | Ist | Glyph |
|---|---|---|---|
| `OptionsEditorWindow.xaml` | 15 / 22 | `+ Hinzufuegen` / `- Entfernen` | `&#xE710;` (Accent) / `&#xE738;` (Muted, Danger-Hover) |
| `OptionsEditorDialog.xaml` | 19 / 22 | MoveUp/MoveDown (MDL2, ohne Groesse) | `&#xE70E;`/`&#xE70D;`, 12 px, `TextSecondaryBrush`, Accent-Hover |
| `CostCatalogEditorDialog.xaml` | 87 / 106 | `+ DN` / `+ Position` | `&#xE710;` (Accent) vor Label |
| `PositionTemplateEditorDialog.xaml` | 39 / 74 | `+ Gruppe` / `+ Position` | `&#xE710;` (Accent) |
| `PositionTemplateEditorDialog.xaml` | 94 / 99 | MoveUp/MoveDown (36 px) | `&#xE70E;`/`&#xE70D;`, 12 px, `TextSecondaryBrush` |
| `PositionTemplateEditorDialog.xaml` | 132 | Zeilen-Loeschbutton `Foreground="Red"` 18 px Bold | `&#xE74D;`, 14 px, `DangerBrush`, **kein Bold**, 28×28, Danger-Hover |
| `PositionTemplateEditorDialog.xaml` | 151 | `← Zurueckholen` | `&#xE72B;` (Back), 12 px, Accent, vor Label |
| `MeasureTemplateEditorWindow.xaml` | 67 | `+ Position` (ASCII, neben MDL2-Loeschbutton) | `&#xE710;` (Accent) — gleiches Muster wie Nachbar |

**Commit:** `feat(ui): Editor-Dialoge auf Glyph-Buttons und Theme-Farben umgestellt`

### F4 — Sanierungs-/Schacht-Fenster: Textzeichen-Buttons — Aufwand: M
| Datei | Zeile | Ist | Soll |
|---|---|---|---|
| `SanierungsmassnahmenWindow.xaml` | 105 / 202 | `In Kalkulation >>` / `KI übernehmen >>` | Text + `&#xE72A;` (Forward) 12 px Accent rechts |
| `SanierungsmassnahmenWindow.xaml` | 433 / 498 | `X` Entfernen (rot, `Style={x:Null}`) | `&#xE74D;`, `DangerBrush`, Ghost-Button (28×28 bzw. 22×22) |
| `SanierungsmassnahmenWindow.xaml` | 439 / 490 | `^` Nach oben | `&#xE70E;`, `TextSecondaryBrush`, Accent-Hover |
| `SanierungsmassnahmenWindow.xaml` | 442 / 494 | `v` Nach unten | `&#xE70D;`, `TextSecondaryBrush` |
| `SanierungsmassnahmenWindow.xaml` | 479 | `%` als Spaltenkopf (Checkbox) | `&#xE73A;` 12 px Accent + ToolTip „In Uebertrag uebernehmen", oder Kurztext „Sel." |
| `SchachtMassnahmenWindow.xaml` | 66 / 40 | `＋`-Button + Header zitiert `＋` | Button `&#xE710;` Accent Ghost 24×24; Header umformulieren „DEINE LISTE" |
| `SchachtMassnahmenWindow.xaml` | 100 | `✕` Entfernen (Danger) | `&#xE74D;`, DangerBrush, Ghost 24×24 |
| `SchachtMassnahmenKatalogEditorWindow.xaml` | 42 / 43 | `＋ Hinzufügen` / `✕ Entfernen` | `&#xE710;` (Accent) / `&#xE74D;` (Danger) vor Label |

**Commit:** `feat(ui): Sanierungs- und Schacht-Fenster mit Glyph-Buttons statt Textzeichen`

### F5 — PhotoMeasurement & Hydraulik — Aufwand: S
| Datei | Zeile | Ist | Soll |
|---|---|---|---|
| `PhotoMeasurementWindow.xaml` | 375 | `OK ✔` | `&#xE73E;` 14 px Weiss vor „OK" (gruener Button bleibt) |
| `PhotoMeasurementWindow.xaml` | 381 | `↶ Undo` | `&#xE7A7;` (Undo) 14 px Weiss |
| `PhotoMeasurementWindow.xaml` | 384 | `✖ Löschen` | `&#xE74D;` 14 px Weiss |
| `HydraulikPanelWindow.xaml` | 140 | 💧-Emoji auf blauem Container (Kontrast) | Weisses `&#xEB42;` (Drop) 18 px im bestehenden Accent-Container |
| `PhotoMeasurementWindow.Rendering.cs` | 107 / 237 | Status mit `\|`-Trennern, engl. „Water" | Trenner ` · `; LevelMode deutsch (Wasser/Ablagerung/Hindernis) |
| `PhotoMeasurementWindow.Rendering.cs` | 344 | Messwert `45° @ 3.0h` | `45° · 3.0 Uhr` |

**Commit:** `feat(ui): Foto-Messung und Hydraulik-Panel — Glyphen statt Textzeichen und Emoji`

### F6 — DataPage & MediaConflicts: Emoji raus, Grau akzentuieren — Aufwand: M
| Datei | Zeile | Ist | Soll |
|---|---|---|---|
| `DataPage.xaml` | 179 | 💧 am Hydraulik-Button | Monochromes Tropfen-Path (Fill `AccentBrush`, ~14×16) im StackPanel-Slot |
| `DataPage.xaml` | 188 | 💧 im Kontextmenue | **In Paket E4** (`&#xE8EF;` Calculator) — dort erledigt |
| `MediaConflictsPage.xaml` | 112/120/287/295/303 | Toolbar/Detail-Glyphen bleiben grau | `AccentBrush` (Zuordnen/Video/Play/Oeffnen), **Zeile 295 „Uebernehmen" → `SuccessBrush`** |
| `MediaConflictsPage.xaml` | 131/314 | „Weitere"/„Oeffnen"-Menue grau, nackter Chrome | Menue als ToolbarButton stylen, Glyph 13 px Accent |

**Commit:** `feat(ui): DataPage ohne Emoji, MediaConflicts-Seite einheitlich akzentuiert`

### F7 — Karte & Haltungsansicht — Aufwand: S
| Datei | Zeile | Ist | Soll |
|---|---|---|---|
| `KartePage.xaml` | 84 | `●` Schaechte-Toggle | `&#xE91F;` (FullCircleMask) 12 px Accent, oder `Ellipse` 10×10 (Accent/Muted je Zustand) |
| `HaltungsansichtView.xaml` | 107 | `⇄` Gegeninspektion-Marker | `&#xE8AB;` (Switch) 12 px Accent, optional 18×18-Pill in AccentSubtle |
| `HaltungsansichtView.xaml` | 169 | `+` Neue Beobachtung | `&#xE710;`, 12 px Accent im 28×24-Button |

**Commit:** `feat(ui): Karten- und Haltungsansicht-Marker als Glyphen`

### F8 — PlayerWindow: der groesste Stilmix — Aufwand: L
Nackte Textzeichen/Emoji durchgaengig ersetzen (Foreground jeweils passend zur bestehenden Buttonfarbe: Weiss/Accent/Amber/Danger):
| Zeile | Ist | Glyph |
|---|---|---|
| 198 | `?` Hilfe | `&#xE897;` (Help) Accent |
| 313 / 339 | `✓ Übernehmen` / `✓ Akzeptieren` | `&#xE73E;` |
| 315 / 341 | `✎ Code ändern` / `✎ Korrigieren` | `&#xE70F;` |
| 318 | `✗ Verwerfen` | `&#xE711;` |
| 344 | `▶ Weiter` | `&#xE76C;` (ChevronRight) |
| 422 / 429 | `◀ ` / `▶ ` Meter-Step | `&#xE76B;` / `&#xE76C;` |
| 467 | `⊕ Rohr kalibrieren` | `&#xECC8;` (AddTo) in fester 22-px-Icon-Spalte |
| 471 / 878 | `▢ Bereich`/`Rechteck` | `&#xE739;` (Rahmen) |
| 552 | `✖ Overlays aus` | `&#xED1A;` (Hide) — semantisch korrekt |
| 561 | 📍 Eingabemarker (ToggleButton) | `&#xE707;` (MapPin), Checked = gefuellt/Accent |
| 610 | `Enter ✔`-Hinweis | Keycap-Chip: Border `CornerRadius 4`, `SurfaceSubtleBrush`, Text „Enter" 10 px Mono |
| 869 | `● Punkt` | 8×8-`Ellipse` Fill Accent in 22-px-Spalte |
| 872 | `⬭ Ellipse` (**Tofu-Risiko U+2B2D**) | Path-Ellipse-Kontur 16×10, Stroke Accent 1.5 |
| 875 | `✎ Freihand` | `&#xE70F;` |

**Commit:** `feat(ui): PlayerWindow — alle Aktions-Buttons auf einheitliche Fluent-Glyphen`

### F9 — Player-Codierpanel & Statuszeichen — Aufwand: M
| Datei | Zeile | Ist | Soll |
|---|---|---|---|
| `PlayerCodingSidePanel.xaml` | 104 | 📷 Foto | `&#xE722;` (Camera) Accent |
| `PlayerCodingSidePanel.xaml` | 220 / 427 | 📍 Meter-Angabe (10 px) | `&#xE707;` 10 px TextSecondary — oder Emoji weglassen |
| `PlayerCodingSidePanel.xaml` | 226 | 📷1/📷2 Foto-Indikator | `&#xE722;`+Zahl; Darstellung via Converter statt `CodingSession.PhotoIndicator` (Domain) |
| `PlayerCodingSidePanel.xaml` | 304 / 310 / 316 | `✓`/`⚙`/`✗` Detail-Buttons | `&#xE73E;` / `&#xE70F;` (⚙ ist semantisch falsch) / `&#xE711;`, Weiss |
| `PlayerCodingSidePanel.xaml` | 347 | `✓`-ContextMenu-Header | **In Paket E7** — dort erledigt |
| `Ai/CodingDefectStatusDisplayPolicy.cs` | 43 | `✓ ✎ ⏳ ⚠ ✗` (Mix Text+Emoji) | Einheitlich `&#xE73E;/&#xE70F;/&#xE823;/&#xE7BA;/&#xE711;`, `Segoe Fluent Icons`, 11 px, Farbe weiter aus `GetStatusBrush` |
| `Domain/Models/CodingSession.cs` | 122 | `PhotoIndicator` liefert Emoji aus Domain | Emoji aus Domain entfernen, Anzeige in UI-Converter (Schichttrennung) |

**Commit:** `feat(ui): Codierpanel und KI-Status mit einheitlichem Fluent-Icon-Set`

### F10 — TrainingCenter & Pipeline-Stepper — Aufwand: M
| Datei | Zeile | Ist | Soll |
|---|---|---|---|
| `TrainingCenterWindow.xaml` | 50 | `X` Reset | `&#xE711;` 12 px Muted, 24×24 |
| `TrainingCenterWindow.xaml` | 56 | `▶ Batch-Import` | `&#xE768;` (Play) im StackPanel, Accent-Primaerbutton |
| `TrainingCenterWindow.xaml` | 62 | `⬇ Gold nachholen` | `&#xE896;` (Download) Accent |
| `TrainingCenterWindow.xaml` | 799 | `Box ziehen -> …` (ASCII) | Echtes `→` oder drei nummerierte Mini-Chips |
| `TrainingCenterWindow.xaml` | 884 | Trend-Pfeil hart auf `SuccessBrush` | `&#xE70E;/&#xE70D;/&#xE72A;` richtungsabhaengig gefaerbt (Success ↑ / Danger ↓ / Muted →), 22×22-Badge. **Quelle:** `TrainingKnowledgeBaseQualityPresentationBuilder.cs:41-43` |
| `TrainingCenterWindow.xaml` | 943 | Doppel-Glyph `&#xE736;&#xE72A;` FewShot | Ein Glyph `&#xE710;` (Add) Accent + Text „Zu FewShot" |
| `VideoAnalysisPipelineWindow.xaml` | 426/455/484 | Stepper `●`/`○` (Text) springt zu MDL2-Haken | Durchgaengig eine Formsprache: 10-px-`Ellipse` (Fill Accent/Muted) + separates `&#xE73E;` SuccessBrush bei Fertig — wie Stage-Dots im TrainingCenter |

**Commit:** `feat(ui): TrainingCenter-Buttons, Trend-Badge und Pipeline-Stepper vereinheitlicht`

### F11 — VsaCodeExplorer & Texttrenner — Aufwand: S
| Datei | Zeile | Ist | Soll |
|---|---|---|---|
| `VsaCodeExplorerWindow.xaml` | 367 | `→` zwischen VON/BIS | `&#xE72A;` (Forward) 12 px Muted, auf Grundlinie |
| `VsaCodeExplorerWindow.xaml` | 397 | `← Code links waehlen` | EmptyState-Muster: `&#xE76B;` 30 px Accent, Titel + Subtext |
| `VsaCodeExplorerWindow.xaml` | 500 / 524 | `⌖ Vermessen` (Foto 1/2) | `&#xE721;` (Lupe) oder Ruler-Glyph Accent, einheitlich |
| `AiFindingDisplayItem.cs` | 53 | `\|`-Trenner in DetailText | ` · ` (`string.Join(" · ", detailParts)`) — konsistent mit Z. 39/62 |
| `ImportPreviewWindow.xaml.cs` | 20 | `\|` im Header-Untertitel | ` · ` in MutedBrush, oder zwei Info-Badges |
| `HydraulikPanelViewModel.cs` | 188/189 | `>=`/`<` im Ergebnistext | `≥` (U+2265); besser Status als farbiges Pill-Badge (Success „Ablagerungsfrei" / Warning „Gefahr") |
| `ReviewCardViewModel.cs` | 36 | `?` als KI-Code-Fallback | Text-Badge „Unbekannt" in WarningBrush-Subtle-Pill |

**Commit:** `feat(ui): VsaExplorer-Glyphen und einheitliche Mittelpunkt-Trenner`

### F12 — Navigations-Glyphen im ShellViewModel — Aufwand: S
| Zeile | Eintrag | Ist | Soll |
|---|---|---|---|
| 181 | Schacht-Matrix | `` (= identisch zu „Schaechte") | Eigenes Raster-Glyph `` (GridView) o. ae. |
| 192 | VSA | `` (Legacy E1xx) | Moderner Codepoint `` (CheckList) — passt zu Zustandsklassen |
| 204 | Diagnose | `` (Fragezeichen = „Hilfe") | `` (Bug) oder `` (Diagnostic) |

**Test:** `ShellViewModel`-Nav-Liste hat keine doppelten Icon-Codepoints mehr (kleiner Unit-Test moeglich: Distinct-Count der `NavItem.Icon` == Anzahl Nav-Eintraege).
**Commit:** `feat(ui): eindeutige und semantisch passende Navigations-Glyphen`

---

## Reihenfolge / Prioritaet fuer Codex

1. **Paket A** (Infrastruktur) — muss zuerst, alle anderen bauen darauf.
2. **Paket B** + **D** + **E** (Menues) — der ausdrueckliche Wunsch „Menues aufwerten"; hoechster sichtbarer Nutzen.
3. **Paket C** (ToolTip) — klein, wirkt ueberall.
4. **Paket F1, F8, F9** — hoechste Sichtbarkeit (Navigation, Player).
5. **Paket F2–F7, F10–F12** — restliche Flaechen, mechanisch abzuarbeiten.
6. **Paket G** zuletzt — globale Schrift-Vereinheitlichung, wenn alles Uebrige steht.

---

## PAKET G — Mechanische Vereinheitlichung Icon-Schrift — Aufwand: M (mechanisch)

1. Alle `FontFamily="Segoe MDL2 Assets"` in XAML unter `src/AuswertungPro.Next.UI/` durch `FontFamily="{StaticResource FontIcon}"` ersetzen (ca. 22 Dateien, ~148 Stellen). Ausnahme: Dateien in `Theme/` dort, wo bereits in Paket B behandelt.
2. In C#-Code, der Icon-TextBloecke baut (`VsaCodeExplorerWindow.xaml.cs`, `PhotoMeasurementWindow*.cs`, `TrainingCenterWindow.xaml.cs`, ...): `new FontFamily("Segoe MDL2 Assets")` → `IconFonts.Default`.
3. Danach visuell nichts kaputt: Codepoints sind zwischen MDL2 und Fluent kompatibel; Fluent rendert nur runder/moderner.

**Commit:** `refactor(ui): einheitliche Icon-Schrift ueber FontIcon/IconFonts (Fluent statt MDL2)`

---

## Glyph-Referenztabelle (Aktion → Codepoint)

| Aktion | Glyph | Name |
|---|---|---|
| Speichern | E74E | Save |
| Speichern unter | E792 | SaveAs |
| Oeffnen (Datei) | E8A5 | OpenFile |
| Ordner oeffnen | E838 | FolderOpen |
| Ordner (statisch) | E8B7 | Folder |
| Neu/Hinzufuegen | E710 | Add |
| Loeschen/Entfernen | E74D | Delete |
| Bearbeiten | E70F | Edit |
| Aktualisieren | E72C | Refresh |
| Suchen | E721 | Search |
| Abbrechen/Schliessen | E711 | Cancel |
| Bestaetigen/Uebernehmen | E8FB | Accept |
| Abgeschlossen | E930 | Completed |
| Info/Details | E946 | Info |
| Warnung | E7BA | Warning |
| Drucken | E749 | Print |
| Play / Pause / Stop | E768 / E769 / E71A | |
| Nach oben / unten | E74A / E74B | Up/Down |
| Chevron rechts / unten | E76C / E70D | |
| Ansehen/Vorschau | E890 | View |
| Bild/Foto | E8B9 | Picture |
| Video | E714 | Video |
| Kamera | E722 | Camera |
| Karte/Pin | E707 | MapPin |
| Einstellungen | E713 | Settings |
| Diagnose | E9D9 | Diagnostic |
| Reparatur/Sanierung | E90F | Repair |
| Idee/Vorschlag (KI) | EA80 | Lightbulb |
| KI/Analyse-Energie | E945 | LightningBolt |
| Rechner/Berechnung | E8EF | Calculator |
| Bibliothek/Katalog | E8F1 | Library |
| Import / Export | E896 / E898 | Download/Upload |
| Kopieren / Einfuegen | E8C8 / E77F | Copy/Paste |
| Filter / Sortieren | E71C / E8CB | |
| Zeit/Springen | E823 | Recent |
| In neuem Fenster | E8A7 | OpenInNewWindow |
| Vollbild | E740 | FullScreen |
| Beenden | E7E8 | PowerButton |
| Teilen/Verteilen | E72D | Share |

---

## Abnahme-Checkliste (nach allen Paketen)

1. `dotnet build AuswertungPro.sln` — 0 Fehler.
2. `dotnet test AuswertungPro.sln` — alle Tests gruen (Baseline: 8000+).
3. `grep -r "OrangeRed" src/AuswertungPro.Next.UI --include=*.xaml` → keine Treffer mehr.
4. `grep -r "Segoe MDL2 Assets" src/AuswertungPro.Next.UI --include=*.xaml` → nur noch ggf. bewusste Reste (Ziel: 0 nach Paket G).
5. Sichtpruefung durch Pascal (hell UND dunkel): Hauptmenue, Rechtsklick im Datengrid, Player-Seitenpanel, ToolTips, ComboBox-Dropdown. Kriterien: Einblendungen fluessig (kein Ruckeln), Icons einheitlich gross, Haken bei checkbaren Items sichtbar, keine leeren Glyph-Kaestchen.
6. Kein Eintrag dieses Plans aendert Commands, Click-Handler oder Bindings — reine Optik.
