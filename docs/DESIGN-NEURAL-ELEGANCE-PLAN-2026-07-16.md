# SewerStudio — Design-Optimierung „Neural Elegance": Umsetzungsplan (fuer Codex)

**Erstellt:** 2026-07-16 von Fable (Claude)
**Branch/Stand:** `feature/gis-karte`. **ACHTUNG:** Im Working Tree liegt uncommittete Arbeit (Injizierbar-Refactoring vom 16.07.). Vor Beginn dieses Plans MUSS der aktuelle Stand committet sein (`git status` sauber), sonst nicht anfangen.
**Scope:** Komplette optische Design-Optimierung: Effekte und Animationen im ganzen Programm. Motto: **Zukunftstechnologie / neuronales Netz — professionell und elegant.** KEINE Logik-Aenderungen, KEINE KI-Pipeline-Aenderungen, KEINE neuen NuGet-Pakete.
**Ziel:** Das Programm fuehlt sich an wie ein lebendiges, praezises KI-Instrument: dezentes Leuchten an KI-Orten, ruhiger Puls wo die KI wirklich arbeitet, fliessende Uebergaenge, spuerbare Tiefe — nie Spielzeug, nie Neon-Kitsch. Der Startup-Splash („3D Neural Inspection Core", dunkel + Gold) ist der Markenauftritt; das Programm selbst spricht dieselbe Sprache in Blau/Teal weiter.

> **Wichtig:** Die Ordner `.claude/worktrees/` und `.worktrees/` enthalten Kopien der Codebase — NIEMALS dort aendern. Nur unter `src/AuswertungPro.Next.UI/` arbeiten.

---

## Leitbild „Neural Elegance" (verbindlich fuer alle Pakete)

1. **Licht statt Laerm:** Akzente entstehen durch dezentes Leuchten (Glow mit `ShadowDepth=0`) und Verlaeufe (Blau→Teal, existiert als `AccentBarBrush`) — niemals durch grelle Farben oder Blinken.
2. **Puls = Leben:** Dauerhafte Bewegung gibt es NUR dort, wo die KI oder ein Live-System wirklich arbeitet (Analyse laeuft, Live-Monitor, Wartezustand). Alles andere bewegt sich nur auf Nutzer-Ereignis (Hover, Fokus, Oeffnen, Seitenwechsel).
3. **Fluss:** Bewegung hat Richtung und Bedeutung — Fortschritt fliesst nach rechts, Neues kommt von unten ein, Ebenen heben sich zum Betrachter.
4. **Tiefe:** Hierarchie durch ein einheitliches Schatten-/Elevation-System statt durch dicke Rahmen.
5. **Ruhe als Feature:** Ein Schalter „Animationen reduzieren" stoppt alle Dauer-Loops. Loops pausieren automatisch, wenn ihr Element unsichtbar ist.

**Verbote (Abbruchkriterium bei der Sichtpruefung):**
- Kein Element blinkt oder pulsiert ohne echten Aktiv-Zustand dahinter.
- Pro sichtbarem Bildschirm maximal 1–2 dauerhafte Bewegungen.
- Keine Animation von Layout-Eigenschaften (`Width`, `Height`, `Margin`, `Padding`) — nur `Opacity`, `RenderTransform` und Brush-/Effect-Eigenschaften.
- Kein `BlurEffect` auf Flaechen, keine animierten `BlurRadius`-Werte auf grossen Elementen.
- Datenlesbarkeit schlaegt Effekt: Grids, Zahlen und Formulare bleiben statisch ruhig.

---

## Ist-Bausteine (am 16.07. gelesen und verifiziert — darauf aufbauen, nichts doppelt bauen)

| Baustein | Fundort | Status |
|---|---|---|
| Animations-Tokens `AnimDurationFast/Normal/Slow` (0.12/0.18/0.30 s), `AnimEaseOut/In`, `RadiusS–XL` | `Theme/Controls.xaml:9–22` | vorhanden |
| Code-Pendant `AnimationTokens.Fast/Normal/Slow` | `Controls/AnimationTokens.cs` | vorhanden |
| Brush-System inkl. `SecondaryAccentBrush` (Teal), `AccentBarBrush` (Blau→Teal-Verlauf, je Theme), `CardGlassBrush`, `GlassBrush`, `NavPanelBrush` | `Theme/ThemeLight.xaml` + `Theme/Theme.xaml` (Zeilen 10–108) | vorhanden |
| Seitenwechsel-Animation (Fade+Slide+Zoom, 300 ms) | `Controls/AnimatedContentControl.cs` | vorhanden |
| Menue-/ContextMenu-/ComboBox-/ToolTip-Einblendungen, Button-Hover/Press | `Theme/Controls.xaml` (Symbol-Plan 14.07., umgesetzt) | vorhanden |
| Icon-Sprache `FluentIcon`/`FontIcon`, 0 Emoji | ueberall | vorhanden |
| **`NeuralSphereControl` (50-Knoten-3D-Kugel, Pulse, 30 fps, stoppt bei Unloaded)** | `Controls/NeuralSphereControl.xaml(.cs)` | **VERWAIST — nirgends eingebaut!** |
| Startup-Splash „3D Neural Inspection Core" (dunkel, Gold-Impulse, eigenes NeuralCanvas) | `Views/Windows/StartupSplashWindow.xaml` | vorhanden, NICHT anfassen |
| Mica-Backdrop (attached property `ui:Fluent.Backdrop="Mica"`) | `Fluent.cs`; genutzt nur von `MainWindow.xaml:8` | teilweise |
| BusyOverlay (3/4-Bogen-Spinner, Code-Behind-Rotation) | `Controls/BusyOverlay.xaml(.cs)` | vorhanden |
| ToastHost (Einblendung via Code-Behind, Akzentbalken je Schwere) | `Controls/ToastHost.xaml(.cs)` | vorhanden |
| EmptyState (Fade-in 0.22 s, Icon-Kreis) | `Controls/EmptyStateControl.xaml` | vorhanden |
| SystemMonitorPanel (CPU/RAM/GPU/VRAM-Karten, statisches „LIVE"-Badge) | `Controls/SystemMonitorPanel.xaml:78–87` | vorhanden |
| Pipeline-Stepper Video→Mapping→Protokoll (statische 10-px-Ellipsen + DataTrigger) | `Views/Windows/VideoAnalysisPipelineWindow.xaml:421–489` | vorhanden |
| ProgressBar-Style (flache Accent-Fuellung, **kein Indeterminate-Template**) | `Theme/Controls.xaml:617–637` | ausbaubar |
| TextBox-Style (hell+dunkel getrennt) | `ThemeLight.xaml:515` + `Theme.xaml:517` | ausbaubar |
| ScrollBar-Styles | `Controls.xaml:1366` (gewinnt) + Theme-Dateien | ausbaubar |
| Sidebar-Nav mit `AccentStrip`, eigene Pipe/Schacht/VSA-Icons, Gradient-Linie unterm Logo | `MainWindow.xaml:330–500` | vorhanden |

**Merkregel aus dem Symbol-Plan:** `Controls.xaml` wird zuletzt gemerged und gewinnt in BEIDEN Themes. Styles, die NUR in `Theme.xaml`/`ThemeLight.xaml` liegen (TextBox, GroupBox), muessen dort synchron in beiden Dateien geaendert werden.

---

## Arbeitsregeln fuer Codex

1. Vor Beginn: Working Tree sauber (siehe Kopf), dann `dotnet build AuswertungPro.sln` + `dotnet test AuswertungPro.sln` — Baseline muss gruen sein (~9400 Tests).
2. **Pro Paket ein Commit** (Commit-Messages auf Deutsch), nach jedem Paket Build + Tests.
3. **Niemals hardcodierte Farben** — immer `{DynamicResource ...}`. Einzige erlaubte Ausnahme: Farb-Stops in Verlaeufen/Effekten, die technisch keine DynamicResource koennen — dann als Kommentar im XAML dokumentieren (Praezedenz: Separator-Verlauf aus Symbol-Plan B5).
4. Keine `{Binding ...}`-Pfade aendern, keine Commands/Click-Handler aendern. Nach jeder XAML-Aenderung Bindings gegen das ViewModel pruefen (xaml-binding-Regel).
5. UI-Texte und Kommentare auf Deutsch.
6. **Nur `Opacity`, `RenderTransform`, Brush-/Effect-Properties animieren.** `From`-basierte Storyboards in EnterActions, damit der Endwert dem Basiswert entspricht und es bei jedem Ausloesen neu spielt.
7. **Jede Endlos-Animation** (`RepeatBehavior="Forever"`) braucht: (a) einen echten Aktiv-Zustand als Ausloeser, (b) Stopp bei `IsVisible=False` (MultiTrigger oder IsVisibleChanged im Code-Behind), (c) Respekt vor `MotionSettings.ReduceMotion` (Paket B).
8. Timer-basiertes Zeichnen (NeuralSphere) bleibt bei 30 fps (`Interval=33ms`) und `DispatcherPriority.Render`.
9. Wo ein XAML den Praefix `ui:` oder `ctrl:` nicht kennt: Namespace ergaenzen (`clr-namespace:AuswertungPro.Next.UI` bzw. `...UI.Controls`).
10. Zeilenangaben in diesem Plan wurden am 16.07. verifiziert, koennen aber durch das Injizierbar-Refactoring leicht verschoben sein — vor jedem Edit die Datei lesen.
11. **Keine sichtbaren Symbolzeichen** (`→ ✓ ✕ ✎ ⚠`) in `Theme/Controls.xaml`, `TrainingCenterWindow.xaml`, `VideoAnalysisPipelineWindow.xaml`, `MeasureTemplateEditorWindow.xaml` und den beiden Editor-Dialogen — auch nicht in Kommentaren. `FluentIconTests` prueft die ganze Datei per Regex und wird sonst rot (bei der Umsetzung von Paket A passiert). Im Text „nach" schreiben, im UI ein Glyph verwenden.
12. Der Alpha-Anteil in `DropShadowEffect.Color` wird ignoriert — siehe Alpha-Regel in Paket A2. Gilt fuer jeden Schatten und jeden Glow in allen Paketen.
13. **Achtstellige Farbwerte immer pruefen:** WPF liest `#AARRGGBB`, nicht den CSS-Stil `#RRGGBBAA`. Wer `#2563EB15` als „Blau, schwach deckend" meint, bekommt ein transparentes Gruen (siehe H7).
14. **Animationen in Templates wirklich ausloesen, nicht nur den Quelltext pruefen.** Ein Verlaufspinsel kann eingefroren werden — das faellt erst zur Laufzeit auf. Muster: `ProgressBarIndeterminateTemplateTests` (Style-Block aus der Datei schneiden, isoliert parsen, Fenster rendern, `IsFrozen` und echte Bewegung pruefen). `XamlReader` kann `Controls.xaml` wegen `x:Shared` nicht am Stueck laden, `pack://` braucht eine laufende Anwendung.

---

## PAKET A — Effekt-Fundament: Elevation-, Glow- und Bewegungs-Tokens — Aufwand: S

> **STATUS 16.07.: UMGESETZT** (Commit `dadd31b24`, Build 0/0, 9434 Tests gruen). Abweichungen gegenueber der urspruenglichen Fassung sind unten eingearbeitet: Alpha-Regel in A2, Bestandsschatten bleiben unangetastet (A1). Guards: `DesignAuditThemeResourceTests.Effect_foundation_defines_elevation_glow_and_neural_underline` und `.Glow_accent_color_is_defined_in_light_and_dark_theme`.

Alle Ergaenzungen in `Theme/Controls.xaml` direkt nach den bestehenden Animations-Tokens (Zeile 22), plus je eine Farbe in beiden Theme-Dateien.

### A1 — Elevation-Schatten (drei Stufen, zentrale Ressourcen)
```xml
<!-- Elevation-System: EbeneS=Karten/Chips, EbeneM=Popups/Toasts, EbeneL=Dialoge/Overlays.
     x:Shared="False": jede Verwendung bekommt ihre eigene Instanz (Effects sind pro Visual;
     nur so bleiben sie einzeln animierbar, z. B. Hover-Lift in Paket F). -->
<DropShadowEffect x:Key="ShadowS" x:Shared="False" Color="#000000" Opacity="0.10" BlurRadius="8"  ShadowDepth="1" Direction="270"/>
<DropShadowEffect x:Key="ShadowM" x:Shared="False" Color="#000000" Opacity="0.18" BlurRadius="16" ShadowDepth="3" Direction="270"/>
<DropShadowEffect x:Key="ShadowL" x:Shared="False" Color="#000000" Opacity="0.26" BlurRadius="24" ShadowDepth="5" Direction="270"/>
```
**Bestandsschatten NICHT umstellen** (Entscheid bei der Umsetzung am 16.07.): Die Inline-Schatten (Menue-Popup, ComboBox-Dropdown, BusyOverlay, ToastHost) nutzen `Color="#30000000"` ohne `Opacity` — wegen der Alpha-Regel unten sind das effektiv volldeckende Schatten, also weit von den neuen Stufen entfernt. Eine Umstellung waere eine sichtbare Aenderung am Bestand und gehoert nicht in ein Fundament-Paket. Wer sie spaeter angeht, macht daraus ein eigenes Paket mit eigener Sichtpruefung.

### A2 — Akzent-Glow (das „Neural-Licht")

> **Alpha-Regel (bei der Umsetzung verifiziert):** `DropShadowEffect` wertet nur den **RGB-Anteil** von `Color` aus; die Deckkraft steuert ausschliesslich `Opacity`. Ein Alpha im Farbwert (`#802563EB`) wird ignoriert und taeuscht eine Wirkung vor, die es nicht gibt. Farben darum **immer volldeckend** (`#FF…`) schreiben und die Staerke ueber `Opacity` steuern — gilt fuer alle Effekt-Pakete.

In **beide** Theme-Dateien (nach `ColorSecondaryAccentHover`):
```xml
<!-- ThemeLight.xaml: -->
<Color x:Key="GlowAccentColor">#FF2563EB</Color>
<!-- Theme.xaml (dunkel, hellerer Accent): -->
<Color x:Key="GlowAccentColor">#FF539BF5</Color>
```
In `Controls.xaml`:
```xml
<!-- KI-/Fokus-Leuchten: ShadowDepth=0 ergibt einen gleichmaessigen Schein statt Schatten.
     Basis-Opacity 0 — sichtbar wird der Glow nur ueber Trigger-Animationen. -->
<DropShadowEffect x:Key="AccentGlow" x:Shared="False" Color="{DynamicResource GlowAccentColor}"
                  Opacity="0" BlurRadius="14" ShadowDepth="0"/>
```
**Fallback:** Macht `DynamicResource` auf `Effect.Color` Probleme (leerer Glow nach Theme-Wechsel), stattdessen feste Farbe `#FF2563EB` + Kommentar — Blau funktioniert auf hell wie dunkel.

### A3 — Bewegungs-Token ergaenzen
```xml
<Duration x:Key="AnimDurationXSlow">0:0:0.45</Duration>
<CubicEase x:Key="AnimEaseInOut" EasingMode="EaseInOut"/>
```
Und in `Controls/AnimationTokens.cs` das Pendant `public static readonly TimeSpan XSlow = TimeSpan.FromMilliseconds(450);` mit gleichem Doku-Kommentar („Hero-/Entrance-Effekte").

### A4 — Neural-Verlaufslinie als wiederverwendbare Ressource
`AccentBarBrush` (Blau→Teal) existiert je Theme. Fuer Titellinien (Paket G) zusaetzlich in `Controls.xaml` einen auslaufenden Verlauf:
```xml
<!-- Titel-Unterstreichung: Verlauf laeuft nach rechts ins Transparente aus (wie Sidebar-Logo-Linie).
     Farb-Stops fest (GradientStops koennen keine DynamicResource) — auf hell wie dunkel lesbar. -->
<LinearGradientBrush x:Key="NeuralUnderlineBrush" StartPoint="0,0" EndPoint="1,0">
    <GradientStop Color="#FF2563EB" Offset="0"/>
    <GradientStop Color="#FF0891B2" Offset="0.45"/>
    <GradientStop Color="#000891B2" Offset="1"/>
</LinearGradientBrush>
```

**Test:** bestehende Theme-Guard-Tests laufen; ein fokussierter Test fuer `AnimationTokens.XSlow` (Wert 450 ms) im bestehenden Testmuster.
**Commit:** `feat(ui): Effekt-Fundament — Elevation-Schatten, Akzent-Glow und Bewegungs-Tokens`

---

## PAKET B — Ruhe-Schalter: MotionSettings („Animationen reduzieren") — Aufwand: S

> **STATUS 16.07.: UMGESETZT** (Commit `778695e9c`, Build 0/0, 9439 Tests gruen). So gebaut — fuer die Pakete C–H ist B3 die verbindliche Regel:
>
> - `Controls/MotionSettings.cs`: `ReduceMotion` (get: ausdrueckliche Einstellung, sonst `!SystemParameters.ClientAreaAnimation`), `Configure(bool)`, `ResetForTests()`.
> - **Wichtige Semantik:** `Configure(true)` reduziert immer, `Configure(false)` setzt auf „folge Windows" zurueck (`_override = null`) — der Schalter kann nur zusaetzlich beruhigen. Sonst haette der Standardwert `false` den Systemwunsch uebersteuert.
> - `AppSettings.ReduceMotion` (Default false) + Checkbox im Abschnitt „Darstellung und Diagnose" (Zeile Bewegung, ueber Diagnose) + `OnReduceMotionChanged` (SaveImmediate + Configure) + `App.xaml.cs` neben `WindowStateManager.Configure`.
> - Guards: `MotionSettingsTests` (4 Faelle) und `DesignAuditThemeResourceTests.Reduce_motion_setting_is_wired_from_settings_page_to_startup`.

Muss VOR den Loop-Paketen (C/D/E) existieren, damit jeder Loop das Flag von Anfang an respektiert.

### B1 — Statische Einstellung (Muster: `AnimationTokens`)
Umgesetzt in `src/AuswertungPro.Next.UI/Controls/MotionSettings.cs` — siehe Status-Kasten.

### B2 — Einstellung in der SettingsPage
Umgesetzt: Checkbox **„Dauer-Animationen reduzieren (Puls- und Leuchteffekte aus)"** in „Darstellung und Diagnose". Sie wirkt sofort (speichern + `MotionSettings.Configure`), greift optisch aber erst beim naechsten Fensteraufbau — so im ToolTip gesagt.

### B3 — Verwendungsregel (gilt fuer alle folgenden Pakete)
- **Code-Behind-Loops** (NeuralSphere, NeuralPulseDot, BusyOverlay): vor `Storyboard.Begin()`/`_timer.Start()` `if (MotionSettings.ReduceMotion) return;` — statischer Endzustand bleibt sichtbar (z. B. Punkt gefuellt, aber ohne Ring).
- **Reine XAML-Loops** vermeiden; wo noetig, den Start in den Code-Behind ziehen, damit das Flag greift.

**Test:** `MotionSettingsTests` — Override gewinnt ueber Systemwert; Reset stellt Systemverhalten wieder her; `Configure(false)` erzwingt keine Animationen.
**Commit:** `feat(ui): MotionSettings — Schalter fuer reduzierte Dauer-Animationen` — erledigt (`778695e9c`).

---

## PAKET C — KI-Puls: `NeuralPulseDot` als Signature-Element — Aufwand: M

> **STATUS 16.07.: UMGESETZT** (Commits `4f5c1387d`, `abb65c3e1`; Build 0/0, 9441 Tests gruen).
>
> - `Controls/NeuralPulseDot.xaml(.cs)`: DPs `IsActive`, `DotBrush`; Ring 0.5→1.5 + Opacity 0.9→0 ueber 1.6 s, danach 0.8 s Pause (Storyboard 2.4 s, Forever). Laeuft nur bei `IsActive && IsVisible && !MotionSettings.ReduceMotion`.
> - Farben via `SetResourceReference` statt fester Zuweisung — sonst ueberlebt die Farbe keinen Theme-Wechsel. Viewbox innen: `Width/Height` skalieren sauber.
> - Eingebaut: LIVE-Abzeichen (`IsActive="True"`, `DotBrush="White"`), drei Stepper-Punkte, Meter-Abzeichen (`VideoPhaseActive`), Training-Center-Statusleiste (`IsBusy`).
> - **C4 entfaellt:** In der MainWindow-Statusleiste gibt es kein KI-/Sidecar-Status-Binding — es wurde bewusst keines erfunden.
> - Aktiv-Ableitung im Stepper ohne neuen Converter: `VideoPhaseActive` direkt, Mapping/Protokoll ueber `MultiDataTrigger` (Vorphase fertig + eigene Phase offen).
> - Guard: `DesignAuditThemeResourceTests.Ai_pulse_only_runs_where_work_actually_happens`.

Ein kleines, ueberall einsetzbares „die KI lebt"-Element: gefuellter Punkt mit ruhig auslaufendem Ring.

### C1 — Neues Control `Controls/NeuralPulseDot.xaml(.cs)`
- Aufbau: `Grid` 14×14; innen `Ellipse` 7×7 (`Fill={DynamicResource AccentBrush}`); darum `Ellipse x:Name="PulseRing"` 14×14 (`Stroke={DynamicResource AccentBrush}`, `StrokeThickness=1.5`, `Opacity=0`, `RenderTransformOrigin=0.5,0.5`, ScaleTransform 0.5).
- DPs (Muster: `NeuralSphereControl`): `IsActive` (bool, default false), `DotBrush` optional fuer Sonderfarben (Default Accent).
- Verhalten (Code-Behind):
  - `IsActive=true` UND sichtbar UND `!MotionSettings.ReduceMotion` → Endlos-Storyboard: Ring Scale 0.5→1.5 + Opacity 0.9→0 ueber 1.6 s, dann 0.8 s Pause (Storyboard-Gesamtdauer 2.4 s, `RepeatBehavior="Forever"`); Punkt bleibt statisch gefuellt.
  - `IsActive=false` → Storyboard stoppen, Ring Opacity 0, Punkt-Fill `MutedBrush`.
  - `IsVisibleChanged`/`Unloaded` → Storyboard stoppen bzw. bei sichtbar+aktiv neu starten (Muster: NeuralSphere `Loaded/Unloaded`, `NeuralSphereControl.xaml.cs:80–91`).
- Groesse skalierbar ueber `Width/Height` (Viewbox innen), Standard 14 px.

### C2 — Einsatz 1: SystemMonitor „LIVE"
`Controls/SystemMonitorPanel.xaml:78–87`: Das statische gruene „LIVE"-Pill-Badge behaelt Text und Farbe; links im Pill ein `NeuralPulseDot` 8×8 mit `DotBrush=White`, `IsActive=True` fest (der Monitor IST live). Padding des Pills auf `6,1` anpassen.

### C3 — Einsatz 2: Pipeline-Stepper (Analyse-Fenster)
`Views/Windows/VideoAnalysisPipelineWindow.xaml:421–489`: Die drei 10-px-Stepper-Ellipsen (Video/Mapping/Protokoll) durch `NeuralPulseDot` (12×12) ersetzen. Aktiv-Logik NUR aus vorhandenen Bindings (`VideoPhaseDone`, `MappingPhaseDone`, `IsDone` — und dem Binding, das anzeigt, dass die Analyse laeuft; vor Ort im ViewModel nachsehen, z. B. IsRunning/IsBusy; existiert keines, Phase „aktiv" allein aus den Done-Flags ableiten):
- Video aktiv = Analyse laeuft ∧ ¬VideoPhaseDone → pulsiert; VideoPhaseDone → statisch Accent + vorhandener Success-Haken bleibt.
- Mapping aktiv = VideoPhaseDone ∧ ¬MappingPhaseDone; Protokoll aktiv = MappingPhaseDone ∧ ¬IsDone.
- Die `&#xE72A;`-Verbindungspfeile: DataTrigger — Pfeil VOR der aktiven Phase `AccentBrush` statt `MutedBrush` (der „Datenfluss" zeigt zur Arbeit).
- **Dazu Farbfix (verifiziert):** Start-Button `VideoAnalysisPipelineWindow.xaml:412` hat hardcodiertes `#2563EB` im Template → `{DynamicResource AccentBrush}`.

### C4 — Einsatz 3: Statuszeile Hauptfenster (nur wenn vorhandenes Binding)
`MainWindow.xaml` Statusleiste: NUR wenn dort bereits ein KI-/Sidecar-Status im ViewModel gebunden ist (vor Ort pruefen; ShellViewModel). Existiert keiner: diesen Einsatz WEGLASSEN — kein neues Backend fuer Optik bauen.

**Test:** STA-freier Logik-Test, falls machbar (Aktiv-Ableitung als statische Helper-Methode `NeuralPulseDot.ComputeActive(...)` ist NICHT noetig — Logik liegt in XAML-Triggern; stattdessen Sichtpruefung). MotionSettings-Wirkung manuell pruefen.
**Commit:** `feat(ui): NeuralPulseDot — ruhiger KI-Puls fuer Live-Monitor und Pipeline-Stepper`

---

## PAKET D — Die Neural-Sphere bekommt ihren Auftritt — Aufwand: M

> **STATUS 16.07.: UMGESETZT** (Commit `abb65c3e1`).
>
> - D1 erledigt: Farben ueber `TryFindResource("ColorAccent"/"ColorAccentLight")` mit den alten Werten als Rueckfall; `UpdateTimerState()` startet nur bei `IsActive && IsVisible && !MotionSettings.ReduceMotion`; `IsVisibleChanged` abonniert. Eine **Viewbox** in `NeuralSphereControl.xaml` skaliert die fest auf 140×140 gerechnete Zeichnung — ohne sie wird die Kugel bei kleinerer Groesse abgeschnitten statt verkleinert.
> - D2 erledigt: Kugel 34×34 im Kopf des Analyse-Fensters, links vom Akzentbalken. Aktiv ueber zwei `MultiDataTrigger` (Videophase laeuft **oder** Video fertig, Ergebnis offen — jeweils mit `IsDone=False`). `SetError` setzt `IsDone` mit, darum stoppt die Kugel auch im Fehlerfall.
> - **D3 abgewandelt:** Im Training Center ist fuer die Kugel kein Platz — die Kopfzeile ist eine volle Werkzeugleiste (zehn Spalten), eine Titelzeile gibt es nicht. Statt eines Layout-Umbaus steht dort ein `NeuralPulseDot` in der Statusleiste (`IsBusy`), und "Busy..." heisst jetzt "Arbeitet...".
> - **D4 offen:** Die Knoten am BusyOverlay-Spinner sind nicht gebaut — der Ring reicht, und der Nutzen stand in keinem Verhaeltnis zum Risiko am Bestandscontrol. Bei Bedarf spaeter mit eigener Sichtpruefung.
> - Guard: `DesignAuditThemeResourceTests.Neural_sphere_is_in_use_and_follows_theme_and_motion_settings`.

Das aufwendigste vorhandene Asset (`NeuralSphereControl`: 50 Knoten, Fibonacci-Verteilung, Puls-Impulse) ist nirgends eingebaut — das ist die groesste verschenkte Wirkung im Programm.

### D1 — Theme-Faehigkeit nachruesten (verifiziert: Farben hardcodiert)
`Controls/NeuralSphereControl.xaml.cs:39–40`: `AccentColor`/`AccentLight` sind fest `#2563EB`/`#3B82F6`. Umstellen auf Lookup beim Laden: `TryFindResource("ColorAccent")`/`("ColorAccentLight")` mit den bisherigen Werten als Fallback. (Sphere liest die Farbe einmal bei `EnsureVisuals` — Theme-Wechsel zur Laufzeit darf sie ignorieren, Kommentar dazu.)
Zusaetzlich: `MotionSettings.ReduceMotion` → Timer nicht starten; Sphere zeigt dann das statische Netz (einmal `RenderFrame()`).

### D2 — Einsatz 1: Analyse-Fenster-Kopf
`VideoAnalysisPipelineWindow.xaml`: im Kopfbereich (DockPanel um Zeile 396, links neben/ueber dem Start-Button-Cluster — beim Lesen die beste freie Ecke waehlen) eine `NeuralSphereControl` 56×56, `IsActive` an das Laufen-Binding aus C3 gebunden. Laeuft nichts: Sphere dreht nicht (IsActive=false zeigt das ruhende Netz — Verhalten der vorhandenen `OnIsActiveChanged` vor Ort pruefen und ggf. so ergaenzen, dass Inaktiv = Timer aus + einmaliges statisches Rendering).

### D3 — Einsatz 2: Training Center
`Views/Windows/TrainingCenterWindow.xaml`: im Fenster-Header (Datei lesen, Titelzeile suchen) eine Sphere 48×48, `IsActive` an das vorhandene Busy-/Batch-laeuft-Binding des TrainingCenter-ViewModels (vor Ort pruefen; nur binden, was existiert). `StatusText`-DP der Sphere ungenutzt lassen (Text steht schon im Fenster).

### D4 — Einsatz 3: BusyOverlay-Verwandtschaft (Sichtentscheid)
Das `BusyOverlay` (`Controls/BusyOverlay.xaml:18–26`) behaelt seinen Ring-Spinner als Standard. ZUSAETZLICH den Spinner-Pfad dezent veredeln: unter dem 3/4-Bogen drei kleine Knoten-Ellipsen (3 px, Accent, Opacity 0.5) auf dem Bogenkreis mitrotieren lassen (gleiche RotateTransform-Storyboard-Quelle im Code-Behind, `BusyOverlay.xaml.cs` lesen). Wirkt es bei der Sichtpruefung unruhig → Knoten wieder raus, Bogen bleibt.

**Sichtpruefung:** Sphere-CPU-Last im Leerlauf-Fenster ≈ 0 (Timer aus, wenn `IsActive=false` oder unsichtbar); bei aktiver Analyse fluessig, kein Ruckeln im restlichen UI.
**Commit:** `feat(ui): NeuralSphere im Analyse-Fenster und Training Center — KI-Arbeit sichtbar machen`

---

## PAKET E — Neural Flow: Fortschritt & Warten — Aufwand: M

> **STATUS 16.07.: UMGESETZT** (Commit `bf5308341`; Build 0/0, 9444 Tests gruen).
>
> - **Der Indeterminate-Zweig war kein Schmuck, sondern ein Fehler:** `IsIndeterminate` wird an **neun** Stellen benutzt (VsaPage, SettingsPage 2×, OverviewPage, ExportPage, BuilderPage, MediaSearchWindow) — ohne Template-Zweig blieb der Balken dort schlicht leer.
> - Umgesetzt wie geplant: `PART_Indicator` auf `AccentBarBrush`; Sweep-Border mit drei benannten `GradientStop`s, deren `Offset` per `MultiTrigger` (`IsIndeterminate` + `IsVisible`) endlos von links nach rechts wandert; `StopStoryboard` in den ExitActions.
> - **Verifiziert statt gehofft:** `ProgressBarIndeterminateTemplateTests` rendert ein echtes Fenster mit dem echten Style, prueft `IsFrozen == false` und misst, dass der Streif sich nach 400 ms bewegt hat. Der Zweifel war berechtigt — ein eingefrorener Verlaufspinsel im Template haette zur Laufzeit geworfen, an neun Stellen.
> - **Test-Fallen (fuer kuenftige Template-Tests):** `XamlReader` kann `Theme/Controls.xaml` **nicht** als Ganzes laden (`x:Shared` ist nur in kompilierten Woerterbuechern erlaubt), und `pack://`-URIs brauchen eine laufende WPF-Anwendung. Loesung: den betroffenen Style-Block aus der Datei schneiden und isoliert per `XamlReader.Parse` laden — testet die gepflegte Definition ohne Kopie im Test.
> - **E2 entfiel:** Das BusyOverlay blendet bereits ueber `AnimationTokens.Normal/Fast` mit `CubicEase` ein — nichts zu vereinheitlichen. Der Ring-Spinner laeuft weiter auch bei ReduceMotion (gleiche Begruendung wie der Streif: er ist die Warteanzeige selbst).
> - E3 erledigt sich mit E1 (der Ausfallschutz-Balken nutzt den globalen Style) — nur Sichtpruefung.

### E1 — ProgressBar: Verlauf + Indeterminate-Shimmer (verifiziert: Template `Controls.xaml:617–637`)
1. `PART_Indicator`-Background von flachem `AccentBrush` auf `{DynamicResource AccentBarBrush}` (Blau→Teal-Verlauf, existiert je Theme) — Fortschritt „fliesst" farblich nach rechts.
2. **Indeterminate-Zustand ergaenzen** (fehlt heute komplett): Trigger `IsIndeterminate=True` →
   - `PART_Indicator` collapsed;
   - stattdessen Border `x:Name="IndeterminateSweep"` (volle Breite, gleiche CornerRadius) sichtbar, Background = LinearGradientBrush `transparent → Accent (#FF2563EB) → transparent` (Stops fest, dokumentierte Ausnahme), dessen drei `GradientStop.Offset`-Werte per Storyboard endlos von links (-0.5/-0.25/0) nach rechts (1/1.25/1.5) wandern, Dauer 1.4 s, `RepeatBehavior="Forever"`.
   - Offsets sind relativ (0–1) → responsiv ohne Layout-Animation.
3. Stopp-Regel 7: Das Storyboard in einen `MultiTrigger` (`IsIndeterminate=True` + `IsVisible=True`) haengen, ExitActions stoppen es.
4. ReduceMotion: Indeterminate-Shimmer ist funktionales Feedback („es arbeitet") — er darf trotz ReduceMotion laufen. Kommentar im XAML.

### E2 — BusyOverlay-Nachricht mit Puenktchen-Takt
`Controls/BusyOverlay.xaml.cs` (lesen): Wenn dort ein Fade fuer `ScrimRoot` existiert, auf `AnimationTokens.Normal` + EaseOut vereinheitlichen. Keine weiteren Effekte — der Scrim soll beruhigen, nicht unterhalten.

### E3 — Backup-/Ausfallschutz-Balken
`MainWindow.xaml:286–291` (PC-Ausfallschutz-ProgressBar) profitiert automatisch von E1 — nur Sichtpruefung, keine Extra-Arbeit.

**Commit:** `feat(ui): ProgressBar mit Neural-Verlauf und Indeterminate-Shimmer`

---

## PAKET F — Tiefe: Hover-Lift, Fenster-Entrance, Mica-Rollout — Aufwand: M

### F1 — Opt-in-Style `HoverLiftCard`
In `Controls.xaml` (bei den Card-nahen Styles):
```xml
<!-- Instanz je Verwendung (x:Shared="False"), sonst teilen/frieren alle Karten EIN Transform
     und die Hover-Animation wirft "immutable object"-Fehler bzw. bewegt alle Karten gemeinsam. -->
<TranslateTransform x:Key="LiftTransform" x:Shared="False"/>

<!-- Interaktive Karten: heben sich bei Hover 2 px zum Betrachter (nur Transform+Schatten, kein Layout). -->
<Style x:Key="HoverLiftCard" TargetType="Border">
    <Setter Property="Effect" Value="{StaticResource ShadowS}"/>
    <Setter Property="RenderTransform" Value="{StaticResource LiftTransform}"/>
    <Style.Triggers>
        <Trigger Property="IsMouseOver" Value="True">
            <Trigger.EnterActions>
                <BeginStoryboard>
                    <Storyboard>
                        <DoubleAnimation Storyboard.TargetProperty="(Border.RenderTransform).(TranslateTransform.Y)"
                                         To="-2" Duration="{StaticResource AnimDurationFast}"
                                         EasingFunction="{StaticResource AnimEaseOut}"/>
                        <DoubleAnimation Storyboard.TargetProperty="(Border.Effect).(DropShadowEffect.Opacity)"
                                         To="0.22" Duration="{StaticResource AnimDurationFast}"/>
                    </Storyboard>
                </BeginStoryboard>
            </Trigger.EnterActions>
            <Trigger.ExitActions>
                <BeginStoryboard>
                    <Storyboard>
                        <DoubleAnimation Storyboard.TargetProperty="(Border.RenderTransform).(TranslateTransform.Y)"
                                         To="0" Duration="{StaticResource AnimDurationNormal}"/>
                        <DoubleAnimation Storyboard.TargetProperty="(Border.Effect).(DropShadowEffect.Opacity)"
                                         To="0.10" Duration="{StaticResource AnimDurationNormal}"/>
                    </Storyboard>
                </BeginStoryboard>
            </Trigger.ExitActions>
        </Trigger>
    </Style.Triggers>
</Style>
```
**Anwenden NUR auf klickbare Karten** (Karte = fuehrt zu Aktion/Navigation): OverviewPage-Kacheln, ExportPage-Verteil-Karten, ProjectPage-Projektkarten (je Datei lesen; pro Seite max. die Karten der obersten Ebene, keine Karten in Scroll-Listen mit >20 Eintraegen).

### F2 — Fenster-Entrance als attached behavior
**Neue Datei:** `src/AuswertungPro.Next.UI/WindowFx.cs` (Root-Namespace, Muster `Fluent.cs`): attached property `ui:WindowFx.Entrance="True"` fuer `Window`.
- OnLoaded: Wenn `window.Content` ein `FrameworkElement` OHNE eigenen RenderTransform ist → ScaleTransform 0.985→1 + Opacity 0→1, `AnimationTokens.Slow`, EaseOut, `RenderTransformOrigin=0.5,0.5`. Hat der Content schon einen Transform: nur Opacity faden.
- Laeuft immer (kurzes Ereignis-Feedback, kein Loop) — ReduceMotion egal.
- Anwenden auf modale Dialoge und Werkzeug-Fenster: `CorrectionDialog`, `ImportPreviewWindow`, `RecordDetailsWindow`, `DossierPrintDialog`, `HydraulikPrintDialog`, `MeasureSelectionWindow`, `CatalogSelectorWindow`, `TextPreviewWindow`, `BeobachtungenWindow`, `ObservationCatalogWindow`, Editor-Fenster (`CodeCatalogEditorWindow`, `PriceCatalogEditorWindow`, `MeasureTemplateEditorWindow`, `SchachtMassnahmenKatalogEditorWindow`). **NICHT** auf: `PlayerWindow`, `LiveFrameWindow` (Video), `StartupSplashWindow` (eigene Choreografie), `MainWindow`.

### F3 — Mica-Rollout
`ui:Fluent.Backdrop="Mica"` (existiert, `Fluent.cs`) zusaetzlich auf: `TrainingCenterWindow`, `VideoAnalysisPipelineWindow`, `KarteWindow`, `VsaCodeExplorerWindow`, `SanierungsmassnahmenWindow`, `SchachtMassnahmenWindow`. Je Fenster pruefen, dass der Fenster-Background `{DynamicResource BgBrush}` (o. ae. themebasiert) ist — Referenz ist das MainWindow. **NICHT** auf Player-/Video-Fenster (Render-Performance).

**Test:** Ein fokussierter Test fuer `WindowFx`-Attached-Property-Registrierung, falls STA-Testinfra vorhanden (Muster im Testprojekt suchen; sonst weglassen und Sichtpruefung).
**Commit:** `feat(ui): Tiefe — Hover-Lift fuer Aktionskarten, Fenster-Entrance und Mica-Rollout`

---

## PAKET G — Seiten-Charakter: Entrance-Stagger, Nav-Politur, Titellinien — Aufwand: M

### G1 — Gestaffelte Karten-Einblendung `EntranceFx`
**Neue Datei:** `src/AuswertungPro.Next.UI/EntranceFx.cs` (Root-Namespace): attached property `ui:EntranceFx.Stagger="True"` fuer `Panel`.
- OnLoaded: die ersten max. 10 direkten Kinder (`FrameworkElement`) nacheinander einblenden — Opacity 0→1 + TranslateY 12→0, Dauer `AnimationTokens.Slow`, EaseOut, `BeginTime = Index × 45 ms`. Kinder mit eigenem RenderTransform: nur Opacity.
- Delay-Berechnung als testbare statische Methode: `internal static TimeSpan DelayFor(int index) => TimeSpan.FromMilliseconds(Math.Min(index, 9) * 45);`
- **NUR auf statische Panels** (StackPanel/Grid/WrapPanel mit fixen Karten). NIEMALS auf ItemsControls mit Virtualisierung oder Listen mit Datenbindung variabler Laenge.
- Anwenden: OverviewPage (Cockpit-Kacheln — Datei lesen, oberste Kachel-Ebene), ImportPage (Quell-Karten), ExportPage (Verteil-Karten), SettingsPage (Abschnitts-Karten). Laeuft bei jedem Seitenwechsel erneut (Loaded feuert beim Einhaengen) — zusammen mit dem bestehenden `AnimatedContentControl` ergibt das den „Aufbau"-Effekt; wirkt es doppelt/traege, Stagger-Delay auf 30 ms senken statt Effekt streichen.

### G2 — Sidebar-Navigation: aktiver Eintrag lebt (einmalig, kein Loop)
`MainWindow.xaml:473–500` (`ListBoxItem`-Template, `AccentStrip`):
1. `IsSelected=True` EnterActions: AccentStrip Opacity 0→1 UND ScaleY 0.4→1 (`RenderTransformOrigin=0.5,0.5`, ScaleTransform vorbereiten), `AnimDurationNormal`, EaseOut — der Balken „waechst" ein. ExitActions: Opacity→0 (`AnimDurationFast`).
2. Icon-Pop beim Aktivieren: Im selben Trigger das Icon-Grid (Zeile 389, `Grid Width=24 Height=18` — `x:Name` vergeben) Scale 1→1.08→1 via keyframe-Storyboard 180 ms. Dezent, einmalig.
3. Hover: bestehenden Hover-Setter (Template lesen, `bd`-Border) um sanften Uebergang ergaenzen — Background-Wechsel via `Trigger.EnterActions` Opacity-Animation eines Overlay-Borders statt hartem Setter, NUR wenn ohne Template-Umbau machbar; sonst Hover so lassen.

### G3 — Neural-Titellinien auf den Hauptseiten
Muster (Referenz: Sidebar-Logo-Linie `MainWindow.xaml:358–366`):
```xml
<Border Height="2" Width="120" HorizontalAlignment="Left" CornerRadius="1" Margin="0,4,0,0"
        Background="{StaticResource NeuralUnderlineBrush}"/>
```
Unter die Seitentitel von: OverviewPage, DataPage, ImportPage, ExportPage, BuilderPage, SchaechtePage, VsaPage, KartePage, SettingsPage, SchattenauswertungPage, MediaConflictsPage (je Datei lesen; hat eine Seite keinen Titelblock, Seite auslassen — KEINEN neuen Titel erfinden). Breite an Titellaenge anpassen (80–140 px), immer linksbuendig unterm Titel.

**Test:** `EntranceFxTests.DelayFor` (0→0 ms, 3→135 ms, 15→405 ms gekappt).
**Commit:** `feat(ui): Seiten-Entrance mit Stagger, lebendige Sidebar-Auswahl und Neural-Titellinien`

---

## PAKET H — Mikrointeraktionen: Fokus-Glow, Haken-Pop, Feinschliff — Aufwand: M

### H1 — TextBox-Fokus-Glow (Dateien: `ThemeLight.xaml:515` UND `Theme.xaml:517` — synchron!)
Im TextBox-Template (vor Ort lesen): dem Rahmen-Border `Effect={StaticResource AccentGlow}` geben (Basis-Opacity 0 → unsichtbar, kostet nichts). Trigger `IsKeyboardFocusWithin=True`:
- EnterActions: Glow-Opacity 0→0.5 (`AnimDurationFast`), BorderBrush-Wechsel auf Accent behalten/ergaenzen (falls heute vorhanden — pruefen).
- ExitActions: Opacity→0 (`AnimDurationNormal`).
Gleiches Muster fuer die ComboBox (`Controls.xaml:36`, `BorderElement`).

### H2 — CheckBox-Haken-Pop (`Controls.xaml:183`)
Template lesen; beim Wechsel `IsChecked=True` den Haken/Fuellungs-Visual mit Scale 0.6→1 (120 ms, EaseOut) einblenden statt hart. Kein Loop, laeuft immer.

### H3 — ScrollBar-Hover-Akzent (`Controls.xaml:1366`)
Thumb-Farbe bei Hover sanft von Muted/Border auf `AccentBrush` blenden (Opacity-Overlay- oder Brush-Animation je nach Template — Breite NICHT animieren, Layout-Verbot). Dezent: Der Nutzer merkt erst beim Greifen, dass die App reagiert.

### H4 — Toast-Lebenslinie (`Controls/ToastHost.xaml` + `.xaml.cs`)
Im Toast-Border unten eine 2-px-Linie (`AccentBarBrush`, CornerRadius 0,0,6,6, `RenderTransformOrigin=0,0.5`, ScaleTransform X=1). Im Code-Behind dort, wo die Anzeigedauer laeuft (`Toast_Loaded` lesen): ScaleX 1→0 linear ueber die Restlebensdauer — der Nutzer sieht, wie lange der Toast noch bleibt. Severity-Farben der Linie: wie der vorhandene AccentBar (DataTrigger nachziehen).

### H5 — EmptyState-Float (`Controls/EmptyStateControl.xaml:28–42`)
Der Icon-Kreis schwebt: TranslateY 0→-3→0, Dauer 4 s, `AutoReverse`, EaseInOut, Forever — Start im Code-Behind (`EmptyStateControl.xaml.cs`) NUR wenn `!MotionSettings.ReduceMotion` (Regel B3), Stopp bei Unloaded. EmptyStates sind per Definition allein auf der Flaeche → erfuellt die 1-Loop-Regel.

### H6 — Aufraeumen (verifizierte Reste)
- Sechsstellige `#2563EB`-Werte im Analyse-Fenster: **erledigt** in `abb65c3e1` (Start-Button, Primaer-Button-Style, ThinProgress-Indikator, obere Akzentlinie, „Laeuft"-Text) — alle auf `AccentBrush`.
- Beim Anfassen der Dateien aus diesem Plan: angetroffene hardcodierte Hex-Farben in umgebenden Elementen auf Theme-Brushes umstellen, wenn eindeutig zuordenbar (sonst liegen lassen und im Commit-Text notieren).

### H7 — Achtstellige Abzeichen-Farben: vermutlich seit je die falsche Farbe — **braucht Pascals Entscheidung**

**Befund vom 16.07.** (`VideoAnalysisPipelineWindow.xaml`, Zeilen ~209, ~687, ~727, ~820): Sechs Werte sind im CSS-Stil `#RRGGBBAA` geschrieben, WPF liest aber `#AARRGGBB`. Gemeint war „Akzentfarbe, schwach deckend" — angezeigt wird etwas voellig anderes:

| Wert | Gemeint | Was WPF daraus macht |
|---|---|---|
| `#2563EB15` | Blau, ~8 % deckend | Alpha 14 %, Farbe **gruen** (R63 G EB B15) |
| `#16A34A15` / `#16A34A40` | Gruen, schwach deckend | Alpha 9 %, Farbe **braun-rot** |
| `#DC262610` / `#DC262630` | Rot, ~6 % deckend | **Alpha 86 %**, Farbe fast **schwarz** — der auffaelligste Fall |

Nicht mitgefixt, weil es eine sichtbare Aenderung am Bestand ist und eine Entscheidung braucht: Sollen die Abzeichen die vorhandenen `AccentSubtleBrush`/`SecondaryAccentSubtleBrush` nutzen, oder eigene, je Theme definierte Subtle-Brushes bekommen (Success/Danger fehlen als Subtle-Variante)? **Empfehlung:** je Theme `SuccessSubtleBrush` und `DangerSubtleBrush` ergaenzen und alle sechs Stellen darauf umstellen — dann verschwindet der letzte hart kodierte Farbrest aus dem Fenster. Danach Sichtpruefung hell und dunkel.

**Commit:** `feat(ui): Mikrointeraktionen — Fokus-Glow, Haken-Pop, Scroll-Akzent, Toast-Lebenslinie`

---

## Reihenfolge / Prioritaet fuer Codex

1. ~~**Paket A + B** (Fundament + Ruhe-Schalter)~~ — **erledigt am 16.07.** (`dadd31b24`, `778695e9c`). Alles Weitere baut darauf: Schatten/Glow ueber `ShadowS/M/L` + `AccentGlow`, Dauer-Loops immer hinter `MotionSettings.ReduceMotion`.
2. ~~**Paket C + D** (KI-Puls + NeuralSphere)~~ — **erledigt am 16.07.** (`4f5c1387d`, `abb65c3e1`). Das Motto ist sichtbar: Kugel und Puls laufen nur bei echter Arbeit.
3. ~~**Paket E** (Neural Flow)~~ — **erledigt am 16.07.** (`bf5308341`). War mehr als Optik: der unbestimmte Wartebalken zeigte an neun Stellen gar nichts.
4. **Paket F** (Tiefe) — **hier weitermachen.** Dialoge und Karten, wirkt im Alltag ueberall.
5. **Paket G** (Seiten-Charakter) — Entrance + Navigation + Titellinien.
6. **Paket H** (Mikrointeraktionen) — Feinschliff zuletzt.

Nach Paket D einen Zwischenstand bauen und die Sichtpruefungs-Punkte unten fuer die bis dahin fertigen Teile durchgehen — nicht alles blind bis zum Ende durchziehen.

---

## Abnahme-Checkliste (nach allen Paketen)

1. `dotnet build AuswertungPro.sln` — 0 Fehler, 0 neue Warnungen.
2. `dotnet test AuswertungPro.sln` — alle Tests gruen (Baseline ~9400).
3. `grep -rn "#2563EB" src/AuswertungPro.Next.UI/Views --include=*.xaml` → Fundstelle Zeile 412 im Pipeline-Fenster ist weg; neue Treffer nur mit Ausnahme-Kommentar (Gradients/Effekte).
4. **CPU-Leerlauf-Check:** App gestartet, Hauptfenster sichtbar, keine Analyse → Task-Manager CPU der App ≈ 0–1 %. Analyse-Fenster offen ohne Lauf → ebenso (Sphere-Timer aus).
5. **ReduceMotion-Check:** Schalter an → PulseDot statisch, Sphere statisch, EmptyState schwebt nicht; Hover/Fokus/Einblendungen funktionieren weiter; ProgressBar-Indeterminate darf weiter wandern.
6. **Sichtpruefung durch Pascal (hell UND dunkel):**
   - Analyse-Fenster: Sphere + Stepper-Puls waehrend eines Laufs; ruhig nach Abschluss.
   - System-Monitor: LIVE-Puls dezent, lenkt nicht ab.
   - Dialog oeffnen (z. B. Import-Vorschau): Entrance fluessig, kein Flackern.
   - OverviewPage: Kachel-Stagger einmalig und schnell; Hover-Lift fuehlbar aber dezent.
   - TextBox anklicken: Fokus-Glow sichtbar, nicht grell; Theme-Wechsel hell⇄dunkel → Glow-Farbe passt.
   - Toast ausloesen: Lebenslinie laeuft synchron zur Anzeigedauer.
   - Sidebar: Auswahlwechsel — Balken waechst ein, Icon-Pop einmalig.
7. Kein Eintrag dieses Plans aendert Commands, Bindings oder Geschaeftslogik — reine Optik plus der eine Settings-Schalter (Paket B).
8. Gesamteindruck-Frage an Pascal: „Wirkt es wie ein praezises KI-Instrument — oder wie ein Spielzeug?" Bei Spielzeug-Verdacht: zuerst Dauer-Loops halbieren (Opacity-Spitzen senken), dann erst Effekte entfernen.
