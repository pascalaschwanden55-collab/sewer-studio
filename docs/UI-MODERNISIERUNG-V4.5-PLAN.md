# SewerStudio v4.5 — UI-Modernisierungs-Plan „Fluent & Flow"

**Übergabefähig an Opus oder Codex. Self-contained — alle Fundstellen verifiziert am 2026-07-04 auf Branch `feature/gis-karte`.**

## Leitidee

Das Fundament ist gut (Token-System, Hell+Dunkel-Theme, MDL2-Icons, Animations-Tokens, Toast, Busy-Overlay, Hover-Foto-Vorschau). v4.5 hebt das Programm auf Windows-11-Fluent-Niveau mit Apple-Klarheit: **echtes Fenster-Material (Mica), moderne Scrollbars, Tooltips überall, Slider statt Zahlenfelder, abkoppelbare Panels, ein Dashboard mit Grafiken, und eine Startanimation, die sich an der echten Ladezeit orientiert.** Kein Framework-Wechsel, keine neuen NuGet-Pakete (Mica geht per P/Invoke).

**Referenz-Prinzipien:**
- *Windows 11 Fluent*: Mica-Material, runde Ecken (sind da), dezente Elevation, Segoe-Fluent-Iconografie, Overlay-Scrollbars, Tooltips mit Shortcut-Hinweis.
- *Apple*: Klarheit vor Dichte, direkte Manipulation (Slider/Drag statt Eingabefelder), sanfte physikalische Animationen (120–300 ms, EaseOut — Tokens existieren: `Theme/Controls.xaml:8-21`, `Controls/AnimationTokens.cs`), Zurückhaltung: Animation unterstützt, lenkt nie ab.

## Harte Regeln (Repo-Konventionen — gelten für JEDES Paket)

1. Farben NUR über `{DynamicResource ...}`-Brushes (beide Themes pflegen: `Theme/Theme.xaml` dunkel + `Theme/ThemeLight.xaml` hell — identische Schlüssel!). Niemals Hex-Werte im View-Code.
2. Animationsdauern/Easings NUR über die Tokens (`AnimDurationFast/Normal/Slow`, `AnimEaseOut`).
3. Keine neuen NuGet-Pakete ohne Rückfrage beim User (betrifft v. a. Docking: **kein AvalonDock** — das Abkoppeln nutzt das vorhandene Fenster-Muster).
4. Kommentare auf Deutsch. MVVM mit CommunityToolkit (`[ObservableProperty]`, `[RelayCommand]`).
5. Nach jedem XAML-Change: alle `{Binding}`-Pfade gegen ViewModel-Properties prüfen (stille Binding-Fehler). Bestehende ~3385 UI-Tests müssen grün bleiben; neue Logik (Converter, ViewModels, Services) bekommt Tests.
6. `dotnet build AuswertungPro.sln` nach jedem Paket — 0 Fehler.

---

## Paket V0 — Version 4.5 + Startanimation (der Auftakt, klein)

**V0.1 Versionssprung:**
- `src\AuswertungPro.Next.UI\AppIdentity.cs:9` → `Version = "4.5"` (propagiert automatisch in Splash-Status, SettingsPage:1301, OverviewPage, Backup-Manifest).
- `Views\Windows\StartupSplashWindow.xaml:56`: hartkodiertes `v4.4` entfernen → auf `AppIdentity.DisplayVersion` binden oder leer lassen (Code-Behind:191 überschreibt ohnehin). Dabei Katalog-Jahr vereinheitlichen: Code-Behind sagt „VSA-KEK 2023", XAML „2020" — aktiver Katalog ist `vsa_kek_2020_catalog_manifest.json` → einheitlich **„VSA-KEK 2020"** (oder Jahr weglassen).
- NEU in `AuswertungPro.Next.UI.csproj` (hat heute KEINE Version → EXE meldet 1.0.0.0): `<Version>4.5.0</Version>`, `<FileVersion>4.5.0.0</FileVersion>`, `<AssemblyVersion>4.5.0.0</AssemblyVersion>`.

**V0.2 Splash aufpeppen** (`StartupSplashWindow.xaml.cs` — die 3D-Fibonacci-Neural-Sphere bleibt, sie ist das Markenzeichen):
- **Dauer an echte Ladezeit koppeln statt starr 10,5 s** (heute: Fortschrittsbalken 10,5 s, Zeile ~708-718; App wartet darauf, gekappt 15 s in `App.xaml.cs:143-145`): Balken läuft in ~3,5 s auf 90 % und springt auf 100 %, sobald der ServiceProvider steht → gefühlter Start halbiert sich. Min-Anzeigezeit 3,5 s, damit die Animation wirkt.
- **Skip**: Klick/beliebige Taste auf dem Splash → sofort ausblenden (Apple-Prinzip: Nutzer nie warten lassen).
- **Finale**: beim Übergang kurzer Puls-Burst der Sphere + Titel-Glow, dann 500-ms-Crossfade zum MainWindow (existiert: `App.xaml.cs:147-149`).
- **MainWindow-Entrance**: nach dem Fade-in einmalig gestaffeltes Einblenden der Sidebar-Nav-Items (je +30 ms Versatz, Fade + 8-px-Slide, Tokens nutzen) — einmal pro App-Start, nicht bei Seitenwechseln.
- Versionstext „v4.5" im Splash prominenter (Reveal-Animation existiert, Zeile ~696-706).

---

## Paket F1 — Fluent-Fundament: Fenster-Material & Control-Styles (größter Optik-Hebel)

**F1.1 Mica-Backdrop + dunkle Titelleiste (P/Invoke, kein NuGet):**
- Neuer Helper `src\AuswertungPro.Next.UI\Services\WindowBackdropHelper.cs`: `DwmSetWindowAttribute` mit `DWMWA_SYSTEMBACKDROP_TYPE (38) = 2 (Mica)` und `DWMWA_USE_IMMERSIVE_DARK_MODE (20)` passend zum aktiven Theme (`ThemeManager`). Windows-11-Check (Build ≥ 22621), sonst still nichts tun. Bei Mica muss der Fenster-Hintergrund (teil)transparent werden: pro Fenster `Background=Transparent` auf Window-Ebene, Inhalt liegt weiter auf `CardBrush`-Flächen — als opt-in Attached Property (`ui:Fluent.Backdrop="Mica"`), zuerst nur MainWindow + SettingsPage-Host, dann schrittweise.
- Dark-Titlebar für ALLE Fenster beim Theme-Wechsel nachziehen (Hook im `ThemeManager.ApplyTheme` + `Window.Loaded`-Klassen-Handler analog Default-Icon-Handler `App.xaml.cs:313-363`).

**F1.2 Moderne Scrollbars** (größter „Standard-WPF"-Verräter): `Theme.xaml:754-763`/`ThemeLight.xaml:772-781` setzen nur Width=8, KEIN Template → klassisches graues Chrome. Neues `ControlTemplate` im Stil Win11-Overlay: schmaler runder Thumb (6 px, `CornerRadius=3`), keine Pfeil-Buttons, Thumb verbreitert sich bei Hover auf 10 px (Token-Animation), Track transparent. In `Theme/Controls.xaml` (beide Themes erben Farben via Brushes).

**F1.3 Fehlende Control-Styles ergänzen** (in `Theme/Controls.xaml`): RadioButton (15 Nutzungen in 7 Dateien — Kreis + Accent-Dot mit Scale-Animation), Expander (Chevron `` mit 90°-Rotations-Animation), ToggleButton-Basis, TreeView/TreeViewItem, ListView/GridViewColumnHeader. Optik an vorhandene CheckBox/ComboBox-Styles angleichen.

**F1.4 Konsistenz-Sweep:**
- Button-Hover-Scale (1.0→1.03, 120 ms) fehlt im Dark-Theme (`Theme.xaml:176-190`) → nachziehen; zusätzlich Pressed-Feedback Scale 0.98 in beiden Themes.
- Emoji/Unicode-Streu-Icons → MDL2-Glyphs vereinheitlichen (7 Dateien, u. a. `KartePage.xaml:31` „✕"→``, `VideoAnalysisPipelineWindow.xaml:557` „⬜ Abdocken"→``; ferner ImportPage, PlayerWindow, PositionTemplateEditorDialog, MeasureTemplateEditorWindow, OptionsEditorDialog).
- Lokale Style-Duplikate in geteilte Ressourcen überführen: `DataPage.xaml:27-40` (CompactButton/CompactToggleButton ohne Template → Default-Chrome!), `HydraulikPanelWindow.xaml:17-60` (HComboBox etc.), PlayerWindow (PlayerCard/PlayerButton). Verhalten identisch halten (verhaltensneutral).
- Row-Hover-Farben vereinheitlichen: hartkodiertes `#FF2D3440` (`Theme.xaml:587-604` + Overrides in ProtocolObservationsWindow:97, BeobachtungenWindow:90, MediaSearchWindow:128, SanierungsmassnahmenWindow:468) → neue Brush-Ressource `RowHoverBrush` in beiden Themes.

---

## Paket F2 — Tooltips überall + Rich-Tooltips

**Befund:** 151 Tooltips in nur 21 von ~66 XAML-Dateien; gestylter ToolTip-Style existiert (`Controls.xaml:706`). MainWindow: 0. SettingsPage: 0 bei 17 Buttons. OverviewPage/ProjectPage/MediaConflictsPage/VsaPage: 0. KartePage/ExportPage: je 1.

**F2.1 Rich-Tooltip-Baustein:** wiederverwendbares ContentTemplate `RichToolTip` (in `Theme/Controls.xaml` + kleines POCO oder Attached Properties): MDL2-Icon + fetter Titel + Beschreibungstext (max ~40 Wörter) + optional Shortcut-Chip (z. B. „Strg+S") + optional Vorschaubild. Vorbild Win11 (Taskleisten-Tooltips) / macOS-Hilfe-Tags. `ToolTipService.InitialShowDelay=400`, `BetweenShowDelay=100` global konsistent setzen.

**F2.2 Tooltip-Sweep** (jeder sichtbare Button/Toggle/Icon-Button bekommt mindestens Kurz-Tooltip; zentrale Aktionen Rich-Tooltip):
- `MainWindow.xaml`: 12 Nav-Einträge (über `NavItem`-Property + ItemTemplate), Menüpunkte, „Projekt wechseln", Statusleisten-Elemente.
- `SettingsPage.xaml` (17 Buttons), `OverviewPage.xaml` (7), `ProjectPage.xaml` (5), `MediaConflictsPage.xaml` (6), `KartePage.xaml` (4), `ExportPage.xaml` (4), `VsaPage.xaml`, Dialoge (`OptionsEditorDialog`, `CostCatalogEditorDialog`, `PositionTemplateEditorDialog`...).
- Lückenfüllung bei den Guten: `PhotoMeasurementWindow` (12/34), Rest von PlayerWindow/TrainingCenter.
- Texte fachlich korrekt (Kanalinspektions-Domäne), deutsch, erklären WAS + WOZU.

---

## Paket F3 — Slider & direkte Manipulation

Slider-Style ist bereits gestylt; 9 Slider existieren (DataPage-Kontextmenü, SchaechtePage-Toolbar, PhotoMeasurement, Player-Timeline `PlayerWindow.xaml:687`).

- **F3.1 Player** (`PlayerWindow.xaml`): (a) **Lautstärke-Slider + Mute-Toggle** (MDL2 ``/``) — fehlt komplett; LibVLCSharp `MediaPlayer.Volume` 0–100, Persistenz in AppSettings. (b) **Geschwindigkeit**: die 6 ToggleButtons (0.5x–8x, Zeile 845-856) durch kompakten Slider mit Snap-Punkten + Wertanzeige ersetzen (Buttons als Fallback im Overflow-Menü ok). (c) **Overlay-Transparenz-Slider** für die Coding-Overlays (Renderer in `Player\Coding*Renderer.cs` haben keine UI-Regelung) — Wert als ObservableProperty + AppSettings.
- **F3.2 Karte** (`KartePage.xaml`, Mapsui `MapControl` Zeile 12 — reines SKElement, KEIN Airspace-Problem): Zoom-Overlay rechts unten im Fluent-Stil: Buttons `+`/`−`/„Ganzes Netz" (``) + optional vertikaler Zoom-Slider; via `MapControl.Map.Navigator.ZoomIn/ZoomOut/ZoomToBox`. Halbtransparente Card, erscheint bei Maus-über-Karte (Fade, Tokens).
- **F3.3 Foto-Galerie** (`Controls\PhotoGalleryPanel.xaml`): Kachelgrößen-Slider (80–260 px statt fix `Width="124"` Zeile 28) im Panel-Header; Persistenz AppSettings.
- **F3.4 KI-Einstellungen** (`SettingsPage.xaml`): numerische Schwellwerte (Konfidenz etc.) als Slider mit Wert-Badge + Reset-auf-Default-Knopf. NUR Bedienung ändern — Default-WERTE nicht anfassen (DINO-Schwellen sind kalibriert!).

---

## Paket F4 — Hover ausweiten, Micro-Interactions, Empty-States

- **F4.1 Hover-Foto-Vorschau auf neue Listen** (Muster ist fertig: `Behaviors\PhotoHoverPreviewBehavior.cs`, 350 ms; je neuer Typ nur Selektor in `PhotoHoverPreviewSelectors.cs` + `IsEnabled="True"`):
  Kandidaten: `Controls\PhotoGalleryPanel.xaml` (statt Mini-ToolTip), **DataPage-Haltungszeilen** (Primärschaden-Foto — `DataPagePrimaryDamagePreviewBuilder.cs` existiert bereits!), `SanierungsmassnahmenWindow`/`SanierungsMatrixPage`-Zeilen, `MediaConflictsPage`.
- **F4.2 Empty-States** (P5 aus dem Politur-Plan, noch offen): wiederverwendbares `Controls\EmptyStateControl` (großes MDL2-Icon in AccentSubtle-Kreis + Titel + 1 Satz + CTA-Button-Command). Einsetzen: DataPage:538, SchaechtePage:264, VideoAnalysisPipelineWindow:696, KartePage ohne Kataster, PhotoGalleryPanel leer, MediaSearchWindow ohne Treffer. Sanfter Fade-in (Tokens).
- **F4.3 Statusleiste aufwerten** (`MainWindow.xaml:62-87`, heute reiner Text): KI-Status-Badge mit Punkt-Indikator (grün pulsierend wenn „KI AKTIV" — `ShellViewModel.AiIndicatorTitle` existiert ungenutzt!), MDL2-Icons vor Haltungs-/Schacht-Zählern, dezente Trenner, Klick auf KI-Badge → SystemMonitor-Fenster (s. F5).
- **F4.4 Icon-Dichte**: SettingsPage-Abschnitte und OverviewPage/ProjectPage-Buttons bekommen führende MDL2-Glyphs (16 px, `MutedBrush`) — Apple-Prinzip: Icon + Text, nie Icon allein bei unklarer Bedeutung.

---

## Paket F5 — Abkoppelbare Fenster (Multi-Monitor-Arbeitsplatz)

Vorbild-Muster existiert: `KarteWindow` hostet dieselbe `KartePage`/dasselbe `KarteViewModel` (`MainWindow.xaml.cs:126-129`); ebenso `FloatingGridWindow` (DataPage.xaml.cs:821) und `BeobachtungenWindow`. **Kein Docking-Framework** — bewusst leichtgewichtig.

- **F5.1 Einheitlicher „Abkoppeln"-Button** (MDL2 `` OpenInNewWindow, IconButton-Style, Tooltip „In eigenem Fenster öffnen") in Panel-Headern von: **Foto-Galerie** (`PhotoGalleryPanel`), **System-Monitor** (`SystemMonitorPanel` — Sidebar-Version bleibt, Fenster zeigt große Ansicht), **Schadensband/PipeGraphTimeline** (großes Fenster mit Zoom), Beobachtungsliste (Button dahin, wo sie eingebettet ist — Verweis auf existierendes `BeobachtungenWindow`).
- **F5.2 Fenster-Gedächtnis**: `WindowStateManager.Track` (Services\WindowStateManager.cs:23 — heute NUR MainWindow!) auf alle Zweitfenster ausweiten: PlayerWindow, KarteWindow, TrainingCenterWindow, VideoAnalysisPipelineWindow, FloatingGridWindow, BeobachtungenWindow + die neuen. Mehrmonitor-Schutz existiert schon (Zeile 96-105).
- **F5.3 „Ansicht"-Menü** (MainWindow-Menü): Einträge „Karte abkoppeln", „Foto-Galerie abkoppeln", „System-Monitor öffnen" — Discoverability.
- Hinweis Airspace: nur der VLC-Player ist HwndHost-betroffen (Overlays dort bleiben Top-Level-Popups — Muster in `PlayerWindow.Coding.cs:13` dokumentiert). Karte/Fotos/Grids sind reine WPF → unproblematisch.

---

## Paket F6 — Projekt-Dashboard & Grafiken (der Wow-Effekt)

Heute: `OverviewPage.xaml:218-264` „Statistiken" = reine Text-Kacheln; nirgends ein Chart für Zustandsnoten/Severity. Vorhandene Chart-Muster zum Wiederverwenden: Kostenbalken (`SanierungsMatrixPage.xaml:210-223` + Converter), TrainingCenter-Balken (:434, :499, :562), `PipeGraphTimeline`.

- **F6.1 Dashboard-Karten im Workspace** (neue Sektion auf der Projekt-/Übersichtsseite im Workspace-Modus, pure WPF — Path/ArcSegment, keine Chart-Lib):
  - **Zustandsklassen-Donut** (Zk0–Zk4 in den Severity-Brushes, die als `Severity1–5` im Theme existieren; Klick auf Segment → Haltungen-Seite mit passendem Filter-Chip via `FilterChipBar`),
  - **Severity-/Schadenscode-Histogramm** (Top-10 Codes, horizontale Balken),
  - **DN-Verteilung** (Balken) und **Material-Verteilung**,
  - **Kosten-Kachel** (Summe + Balken je Dringlichkeit),
  - Kacheln mit gestaffeltem Einblende-Effekt (Tokens), Zahlen zählen hoch (300 ms).
  - Alle Werte aus vorhandenen Projekt-Daten (`Project.Data`, VSA-Zustandsnoten-Felder) über ein neues testbares `DashboardStatisticsBuilder` (Application-Schicht, Unit-Tests!).
- **F6.2 Karten-Statistik-Overlay** (KartePage): kleine Legende ist da (:74-88) → um Mini-Zustandsverteilung (gestapelter Balken) ergänzen.

---

## Paket F7 — Shortcuts & Command-Palette (Stretch, zuletzt)

- **F7.1** Player-Shortcuts (Leertaste, D, M, C — heute nur in Tooltips erwähnt, Code-Behind `PlayerWindow.Keyboard.cs`) als deklarierte Referenz pflegen + **Shortcut-Cheatsheet-Overlay** (F1 bzw. `?`-IconButton: halbtransparentes Overlay mit Tastatur-Chips, gruppiert nach Bereich; Schließen mit Esc/Klick).
- **F7.2 (optional)** Command-Palette Strg+K: Fuzzy-Suche über Nav-Ziele + Haltungs-/Schachtnamen + Aktionen („Analyse starten", „Exportieren"). Eigenes Popup-Window mit Listbox, ViewModel-getrieben, testbar. Nur angehen, wenn F1–F6 fertig und Zeit bleibt.

---

## Reihenfolge & Aufwand (jedes Paket einzeln baubar + committbar)

| # | Paket | Aufwand | Sichtbarkeit |
|---|---|---|---|
| 1 | V0 Version + Splash | S | hoch (jeder Start) |
| 2 | F1 Fluent-Fundament | L | sehr hoch (überall) |
| 3 | F2 Tooltips | M | hoch |
| 4 | F3 Slider | M | hoch |
| 5 | F4 Hover/Empty/Statusleiste | M | hoch |
| 6 | F5 Abkoppeln | M | mittel-hoch |
| 7 | F6 Dashboard | L | sehr hoch (Wow) |
| 8 | F7 Shortcuts/Palette | S–M | mittel |

Empfohlene Commits: 1 Commit pro Unterpunkt (V0.1, V0.2, F1.1, ...), Präfix `feat(ui):`/`style(ui):`.

## Verifikation (pro Paket + am Ende)

1. `dotnet build AuswertungPro.sln` — 0 Fehler.
2. `dotnet test` — alle bestehenden Tests grün (UI-Tests ~3385); neue Tests für: DashboardStatisticsBuilder, neue Converter, EmptyState-Sichtbarkeitslogik, WindowBackdropHelper-Versionscheck (reine Logik mocken), Slider-Persistenz in AppSettings.
3. XAML-Binding-Check nach jedem View-Change (alle `{Binding}`-Pfade existieren als Properties).
4. **Beide Themes prüfen**: jede neue Optik in Hell UND Dunkel (Theme-Umschalter SettingsPage:298-334; Laufzeit-Wechsel teils erst nach Neustart).
5. Manueller WPF-Smoke (macht der User): Start (Splash schneller + skippbar, MainWindow-Entrance), Mica-Effekt auf Win11, Scrollbars in langen Listen, Tooltips MainWindow-Nav + Settings, Player Lautstärke/Speed/Transparenz, Karten-Zoom-Overlay, Galerie-Kachelslider, Hover-Vorschau auf DataPage-Zeile, Empty-State bei leerem Projekt, Fenster abkoppeln + Position wird gemerkt (App-Neustart), Dashboard-Zahlen stimmen mit Grid überein, Titel/Version zeigen 4.5.

## Bewusst NICHT in v4.5

- Kein Docking-Framework (AvalonDock etc.) — NuGet + Overkill; das Fenster-Muster reicht.
- Kein WPF→WinUI/MAUI-Umstieg, kein Custom-Window-Chrome (Titelleiste bleibt OS-Standard — Win11 rundet ohnehin; Mica + Dark-Titlebar holen den Look ohne Chrome-Risiken).
- Keine Änderungen an KI-Logik, Schwellwerten, Pipeline oder Geschäftslogik — reine Präsentationsschicht (plus kleine AppSettings-Properties für Persistenz).
- PlayerWindow-God-Klasse wird NICHT refactored (läuft als eigenes Vorhaben) — dort nur additive UI (Slider, Tooltips).
