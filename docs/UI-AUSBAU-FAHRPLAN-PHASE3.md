# UI-Ausbau-Fahrplan — Übergabe Phase 3 (Stand 2026-07-03)

12-Pakete-Plan (WinCan-VX-Vergleich), vom User genehmigt: alle der Reihe nach.
**Phase 1+2 sind FERTIG** (Claude, Commits unten). Dieses Dokument ist die
Arbeitsgrundlage für die Fortsetzung (Codex oder Claude).

## Regeln (verbindlich)

- TDD für alle Logik: Policies/Builder als pure statische Klassen + xUnit-Tests (Muster: `HaltungSchadensbandBuilder`, `DataPageFilterPolicy`).
- Kommentare Deutsch. KEINE neuen NuGet-Pakete (Charts selbst zeichnen).
- Neue Controls/Dateien bevorzugen, bestehende Dateien minimal anfassen.
- Nach jedem Paket: `dotnet test tests/AuswertungPro.Next.UI.Tests` grün + Voll-Build + eigener Commit.

## Erledigt (NICHT erneut bauen)

| Paket | Commit | Kern |
|---|---|---|
| P1.1 Schadens-Längsband Haltungsansicht | c46da45c | `HaltungSchadensbandBuilder` + `HaltungSchadensband`-Wrapper um `PipeGraphTimeline` (neu: EndMeterAccessor, ColorKindAccessor) |
| P1.2 Statusleiste + App-Icon | 52824452 | MainWindow-Statusleiste rechts Projektinfo; `Assets\Brand\app.ico` |
| P1.3 Kostenbalken Sanierungs-Matrix | 52824452 | `KostenBalkenScale` + MultiBinding-Spalte, `MaxRowTotal` im VM |
| P2.1 Foto-Galerie Haltungs-Detail | 292d6f95 | `HaltungFotoGalerieBuilder` + `PhotoGalleryPanel`, Expander im Schäden-Panel |
| P2.2 Karten-Infopanel + Sidebar | 0c7d70e9 | `KarteHaltungInfoBuilder`, Infopanel in KartePage, NavItem „Karte" |
| P2.3 Filter-Chips Haltungen-Grid | bed618d9 | `FilterChipBar` + `DataPageFilterPolicy`; Filter nur auf ICollectionView, bei aktivem Filter `Grid.AllowDrop=false` (NR-Schutz) |

Außerdem bereits vorhanden (Alt-Audit überholt): DialogService-Zentralisierung, Ctrl+S/O/N-KeyBindings, DynamicResource in Controls.xaml/MainWindow.

## Offen: Codex-Lane (sofort startbar, mechanisch)

1. **P1.4 Dark-Mode-Rest:** verbleibende Hex-Farben auf Theme-Tokens umstellen: `VideoAnalysisPipelineWindow.xaml` (~26), `TrainingCenterWindow.xaml` (~21). Bewusst-dunkle Video-Flächen als benannte Tokens (z.B. `PlayerSurfaceBrush`) in `Theme\ThemeLight.xaml`/`Theme.xaml`.
2. **Konsistenz-Serie:** (a) Emoji-Icons → Segoe-MDL2-Glyphs, (b) `CornerRadius`-Tokens (Radius.S/M/L) in Theme.xaml einführen und die 8–15er-Streuung ersetzen, (c) Empty-State der DataPage als wiederverwendbares `Controls\EmptyStateView.xaml` extrahieren und in Haltungsansicht/Karte/Sanierungs-Matrix einsetzen.

## Offen: Phase 3 (in dieser Reihenfolge)

### P3.1 Code-Schnelleingabe (Inline-Autocomplete beim Codieren)
- Neu `Controls\VsaCodeAutoCompleteBox.xaml(.cs)`: TextBox + Vorschlags-Popup (Code + Klartext), Datenquelle = derselbe Katalogdienst wie `Views\ProtocolCodePickerDialog` (Suchlogik dort abschauen). Enter übernimmt den markierten Vorschlag, F2 öffnet weiterhin den vollen Picker (Parameter-Erfassung bleibt dort).
- Einbau: PlayerCodingSidePanel + `Views\ProtocolEntryEditorDialog`.
- ⚠️ Fokus-Guard: Player-Hotkeys (Space/Pfeile/+/-) müssen pausieren, solange das Textfeld Fokus hat — sonst spult Tippen das Video.
- Tests: Vorschlags-Filterlogik als pure Klasse (Präfix-/Contains-Ranking).

### P3.2 Frame-Peek (Thumbnail beim Hover über den Video-Slider)
- Neu `Behaviors\SliderThumbnailPreviewBehavior.cs` + kleines Popup: MouseMove über `PositionSlider` (`Views\Windows\PlayerWindow.xaml`, Slider-Bereich ~Z.687) → debounced ~150 ms → Frame klein extrahieren via `VideoFrameExtractor.TryExtractFramePngAsync` (Nutzungsmuster: `Infrastructure\Ai\Training\FrameStore.cs` Z.43) mit ffmpeg-Fast-Seek, ~160 px breit; LRU-Cache pro Video; CancellationToken bei Mausbewegung. Popup zeigt Frame + Zeit + Meter; solange kein Frame da: nur Zeit/Meter.
- **Dabei mit erledigen:** öffentliche Player-API „öffne/springe zu Meter/Zeit" — danach den Marker-Klick des Haltungsbands (`HaltungsansichtView`, `HaltungSchadensband.MarkerClicked`) zusätzlich ins Video springen lassen (aktuell selektiert er nur den Listeneintrag; `ProtocolEntry.Zeit` ist seit dem KINS-Import gut gefüllt).

### P3.3 Projekt-Dashboard (Zustandsverteilung + Kennzahlen)
- Neu, pur testbar: `Application\Statistics\ProjektStatistikBuilder.cs` (+ Tests): ZK-Verteilung 0–4 nach Anzahl UND Metern, Gesamtlänge, % sanierungsbedürftig, Video-/Foto-Abdeckung, Kostensumme (aus Sanierungs-Matrix-Daten).
- UI ohne NuGet: neues Control `Controls\SimpleBarChart.xaml` (horizontale Balken, Theme-Severity-Farben; Donut optional via ArcSegment — Balken reichen fachlich). Einbau: Statistik-Bereich der `Views\Pages\OverviewPage.xaml` erweitern.

### P3.4 Karte: Schachtpunkte, Fließrichtung, Layer-Schalter
- **`Infrastructure\Map\ManholeGeometry.cs` + `XtfManholeExtractor.cs` existieren fertig und UNGENUTZT** → MemoryLayer „Schächte" (SymbolStyle; Muster `_netzLayer` in `ViewModels\Pages\KarteViewModel.cs` ~Z.108), Klick auf Punkt → Schachtansicht.
- Fließrichtungspfeile: Dreieck am Linienmittelpunkt, Rotation aus Segmentwinkel — pure Geometrie-Helferklasse + Tests. Mapsui-Symbolrotation zuerst prototypisch prüfen.
- Layer-Panel: schwebendes Border mit CheckBoxen → `layer.Enabled` (Hintergrund/QGIS/Netz/Schächte).

## Fallstricke

- NR = Listenposition: niemals die Records-Reihenfolge durch Sortierung/Filter verändern — nur View-Filter; Verschieben bei aktivem Filter gesperrt lassen.
- `CollectionViewSource.GetDefaultView(Records)` wird von DataGrid UND Haltungsansicht-Liste geteilt (Filter wirkt bewusst auf beide).
- SchaechteData/Data-Mutationen immer unter `_shell.CollectionLock` (WPF-Sync-Vertrag, Guard-Test existiert).
- WPF-Smoke für Phase 1+2 steht noch aus (Band, Galerie, Karte, Chips, Statusleiste, Icon, Kostenbalken).

## Bewusst NICHT bauen

3D/IFC-Visualisierung, SonarScan, Berichtsdesigner (begründet verworfen — Solo-Nutzer, Aufwand/Nutzen).
