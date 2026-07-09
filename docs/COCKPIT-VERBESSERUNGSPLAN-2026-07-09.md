# SewerStudio — Projekt-Cockpit: Verbesserungsplan (fuer Codex)

**Erstellt:** 2026-07-09 von Fable (Claude)
**Branch/Stand:** `feature/gis-karte`, Working Tree enthaelt uncommittete Aenderungen (Reports/Schacht/QGIS) — **vor Beginn `git status` pruefen und NICHTS davon anfassen**
**Scope:** Nur die Uebersichtsseite („Projekt-Cockpit") und ihre Bausteine. Keine anderen Seiten, keine KI-Pipeline.

## Betroffene Dateien (alle am 09.07. gelesen und verifiziert)

| Datei | Rolle |
|---|---|
| `src/AuswertungPro.Next.UI/Views/Pages/OverviewPage.xaml` | Layout Cockpit + Projektliste |
| `src/AuswertungPro.Next.UI/Views/Pages/OverviewPage.xaml.cs` | Code-Behind: Drag&Drop, Converter |
| `src/AuswertungPro.Next.UI/ViewModels/Pages/OverviewPageViewModel.cs` | ViewModel: Projektliste, Vorschau, Dashboard-Refresh |
| `src/AuswertungPro.Next.Application/Dashboard/DashboardStatisticsBuilder.cs` | Statistik-Aufbereitung |
| `src/AuswertungPro.Next.UI/Controls/DonutChart.cs` | Donut-Diagramm (Canvas) |
| `src/AuswertungPro.Next.UI/Controls/CategoryBars.cs` | Balken-Diagramm |
| `src/AuswertungPro.Next.UI/DataPage/ZustandsklasseColorPalette.cs` | Zustandsfarben Z0–Z4 |

**Vorhandene Tests:** `tests/AuswertungPro.Next.UI.Tests/DashboardStatisticsBuilderTests.cs`, `OverviewProjectStatusPolicyTests.cs` — als Muster fuer neue Tests verwenden.

## Arbeitsregeln fuer Codex

1. Vor Beginn: `dotnet build AuswertungPro.sln` + `dotnet test AuswertungPro.sln` — Baseline muss gruen sein.
2. Pro Paket ein Commit (Commit-Messages auf Deutsch), nach jedem Paket Build + Tests.
3. Theme-Regel: **niemals hardcodierte Farben in XAML**, immer `{DynamicResource ...}` (BgBrush, CardBrush, HeaderBrush, BorderBrush, BorderLightBrush, TextBrush, MutedBrush, AccentBrush, AccentSubtleBrush, SuccessBrush, DangerBrush, WarningBrush).
4. Neue Logik testbar halten: reine Funktionen bzw. Services mit Interface, keine Fachlogik in Code-Behind.
5. Nach jeder XAML/ViewModel-Aenderung: alle `{Binding ...}`-Pfade gegen die ViewModel-Properties pruefen (stille Binding-Fehler vermeiden).
6. UI-Texte auf Deutsch; Kommentare auf Deutsch.

---

## STUFE 1 — FUNKTIONALE FIXES (zuerst, hoechster Nutzen)

### F1 — Vorschau laedt Projekte synchron auf dem UI-Thread — Aufwand: M
**Datei:** `OverviewPageViewModel.cs:489-537` (`BuildPreview`), Aufrufer `ApplyFilter:307` und `OnSelectedProjectEntryChanged:476-482`
**Befund (verifiziert):** `_sp.Projects.Load(entry.Path)` laedt die komplette Projektdatei **synchron**. Das passiert bei jedem Wechsel in der Projektliste und beim Tippen im Suchfeld, sobald die Filterung ein anderes erstes Element selektiert. Bei grossen Projekten (mehrere MB JSON, ~3000 Haltungen) friert die UI spuerbar ein. Der `_previewedPath`-Guard verhindert nur das Neuladen *derselben* Auswahl.
**Fix:**
1. `BuildPreview` asynchron machen: Datei-Load + `ProjectPreviewFactory.FromProject` per `Task.Run`, Ergebnis auf dem Dispatcher zuweisen.
2. Vorherigen Lauf abbrechen (`CancellationTokenSource` pro Aufruf, alten canceln) — sonst ueberholen sich schnelle Listenwechsel.
3. Waehrend des Ladens `IsPreviewLoading=true` setzen (fuer O8-Skeleton).
4. Debounce ~200 ms analog zum vorhandenen `_dashboardRefreshTimer`-Muster (Zeile 74-75).
**Test:** ViewModel-Test: schneller Doppelwechsel der Auswahl → nur die letzte Vorschau kommt an; Abbruch wirft nicht.

### F2 — Unnoetiges Vorschau-Laden bei geoeffnetem Projekt — Aufwand: S
**Datei:** `OverviewPageViewModel.cs:40, 489-537`
**Befund (verifiziert):** Ist ein Projekt offen (`ShowFullDashboard==true`), nimmt `ActiveDashboard` immer `Dashboard` — die Vorschau wird nie angezeigt. Trotzdem laedt jeder Klick in die Projektliste das angeklickte Projekt komplett von der Platte.
**Fix:** In `BuildPreview` frueh aussteigen wenn `ShowFullDashboard` — dabei `_previewedPath = null` setzen, damit nach dem Schliessen des Projekts die Vorschau wieder aufgebaut wird. (Kombinierbar mit F1.)

### F3 — Drag&Drop oeffnet unbekannte Projekte nicht — Aufwand: S
**Datei:** `OverviewPage.xaml.cs:43-77`
**Befund (verifiziert):** Der Drop-Handler sucht die Datei per `FirstOrDefault` in `vm.ProjectEntries`. Ist das Projekt nicht in der Liste (neuer Ordner, ausgeblendet, ausserhalb der Scan-Wurzeln), passiert **still gar nichts** — fuer den Nutzer sieht es wie ein Bug aus.
**Fix:** Wenn kein Listeneintrag gefunden wird, den Pfad direkt oeffnen. Dafuer im ViewModel eine Methode `OpenProjectFromPath(string path)` ergaenzen (ruft `_shell.TryOpenProject(path)` + die gleiche Nacharbeit wie `OpenSelectedProject:413-425`); der Code-Behind ruft nur noch diese Methode. Fachlogik (projekt.json im Ordner finden) ebenfalls ins ViewModel oder eine kleine statische Helferklasse ziehen, damit sie testbar ist.
**Test:** Ordner mit `projekt.json` → richtige Datei wird gewaehlt; Ordner ohne JSON → kein Crash, keine Aktion.

### F4 — „Loeschen"-Button ist irrefuehrend beschriftet — Aufwand: S
**Datei:** `OverviewPage.xaml:207-211`, `OverviewPageViewModel.cs:427-462`
**Befund (verifiziert):** Der Button heisst „Loeschen", blendet das Projekt aber nur aus der Uebersicht aus (`HideProject`, Daten bleiben erhalten — der Bestaetigungsdialog sagt das korrekt). Ein Nutzer, der wirklich loeschen will, wird getaeuscht; ein Nutzer, der nur ausblenden will, traut sich nicht zu klicken.
**Fix:** Button-Text auf „Ausblenden" aendern (ToolTip: „Projekt aus der Uebersicht ausblenden — Daten bleiben erhalten"). Dialogtitel ist bereits korrekt („Aus Übersicht entfernen").

### F5 — Projektliste zeigt „Leer" trotz vorhandener Schaechte — Aufwand: S
**Datei:** `OverviewPageViewModel.cs:346-349` (liest nur `Data`), `:572` (`StatsText`)
**Befund (verifiziert):** `RecordCount` zaehlt nur das `Data`-Array (Haltungen). Ein reines Schacht-Projekt zeigt „Leer".
**Fix:** Zusaetzlich `SchaechteData`-Array-Laenge lesen (`SchachtCount`), `StatsText` erweitert: „12 Haltungen · 5 Schaechte", nur Haltungen → wie bisher, nur Schaechte → „5 Schaechte", beides 0 → „Leer".
**Test:** Unit-Test fuer die `StatsText`-Logik (Property in kleine statische Format-Funktion ziehen oder direkt am Entry testen).

### F6 — Defekte Projektdateien verschwinden still — Aufwand: S
**Datei:** `OverviewPageViewModel.cs:361` (`catch { /* ignore invalid files */ }`)
**Befund (verifiziert):** Wirft `JsonDocument.Parse`, faellt die Datei kommentarlos aus der Liste. Der Nutzer sucht sein Projekt und findet es nicht — ohne jeden Hinweis.
**Fix:** Im catch trotzdem einen Eintrag anlegen: Name = Dateiname, `IsCorrupt=true`, `StatsText`=„Datei fehlerhaft"; in der Liste mit `WarningBrush`-Seitenstreifen darstellen (DataTrigger analog `IsLastProject`, `OverviewPage.xaml:157-168`). Oeffnen-Versuch zeigt dann die normale Fehlermeldung der Shell.
**Test:** Kaputte JSON-Datei → Eintrag erscheint mit `IsCorrupt=true`.

### F7 — Navigation inkonsistent: Schaechte-Donut tot, „ohne"-Segment blockiert — Aufwand: M
**Dateien:** `OverviewPage.xaml:368-373` (`IsHitTestVisible="False"`), `OverviewPageViewModel.cs:243-250` (`NavigateCondition` blockt „ohne")
**Befund (verifiziert):** Der Haltungen-Donut navigiert per Klick zur Datenseite, der Schaechte-Donut ist bewusst tot geschaltet. Und ausgerechnet das „ohne Zustand"-Segment (die Haltungen, die man am ehesten nacharbeiten will) navigiert nicht.
**Fix:**
1. „ohne" in `NavigateCondition` zulassen, sofern `DataPageStartFilter.FromDashboardZustand("ohne")` einen sinnvollen Filter liefert — **zuerst pruefen**, was `FromDashboardZustand` mit „ohne" macht; falls nicht unterstuetzt, dort ergaenzen (Filter auf leere Zustandsklasse).
2. Schaechte-Donut klickbar machen mit Navigation zur Schaechte-Seite: neues `NavigateSchachtConditionCommand` im ViewModel; in der Shell pruefen, ob es ein Pendant zu `NavigateToDataPage` fuer die Schaechte-Seite gibt — falls nein, dieses Teilpaket nur vorbereiten und mit Pascal ruecksprechen (kein Shell-Umbau ohne Rueckfrage).
**Test:** ViewModel-Test: `NavigateCondition("ohne")` ruft die Navigation auf (Shell mockbar? sonst Filter-Factory testen).

### F8 — Geladene Vorschau-Stammdaten werden nie angezeigt — Aufwand: M
**Dateien:** `OverviewPageViewModel.cs:519-536` (ProjectPreview mit Auftraggeber, Gemeinde, Zone, Strasse, Bearbeiter, Inspektionsdatum, AuftragNr, Firma), `OverviewPage.xaml` (kein einziges Binding darauf — verifiziert per Suche)
**Befund (verifiziert):** `ProjectPreviewFactory` laedt acht Stammdaten-Felder, das XAML zeigt keines davon. Verschenkte Information genau dort, wo man ein fremdes Projekt vor dem Oeffnen einschaetzen will.
**Fix:** Im Vorschau-Modus (`ShowFullDashboard==false` und `SelectedPreview!=null`) eine Stammdaten-Karte ueber den KPI-Kacheln zeigen: zweispaltiges Grid mit Label/Wert (Auftraggeber, Gemeinde, Zone, Strasse, Bearbeiter, Inspektionsdatum, Auftrag-Nr, Firma). Leere Felder ausblenden (Konverter vorhanden: `StringToVisibilityConverter`, `OverviewPage.xaml.cs:84`). Bindings auf `SelectedPreview.Auftraggeber` usw.

---

## STUFE 2 — OPTISCHE VERBESSERUNGEN

### O1 — Farb-Mismatch: Donut vs. Legende daneben — Aufwand: M (wichtigster Optik-Punkt)
**Dateien:** `OverviewPage.xaml:329-352` und `:374-397` (Legenden-Listen), `DonutChart.cs:207-218` (Zustandsfarben), `ZustandsklasseColorPalette.cs:17-25`
**Befund (verifiziert):** Der Donut faerbt nach Zustandsklasse (Z0 rot → Z4 gruen, „ohne" grau). Die Legende daneben zeigt aber einheitlich blaue (Haltungen) bzw. gruene (Schaechte) ProgressBars — die Farben von Donut und Legende passen nicht zusammen; man kann Segmente nicht zuordnen.
**Fix:**
1. In den Legenden-Zeilen vor dem Label einen kleinen Farbpunkt (10×10, CornerRadius 5) in der Zustandsfarbe zeigen und die ProgressBar-`Foreground` ebenfalls in der Zustandsfarbe faerben.
2. Dafuer einen wiederverwendbaren `ZustandsklasseToBrushConverter : IValueConverter` anlegen (UI/Converters oder DataPage-Namespace, delegiert an `ZustandsklasseColorPalette.TryGetBackground`, Fallback „ohne"→Grau 142,150,162 wie `DonutChart.cs:209-210`).
3. Denselben Converter in beiden Legenden (Haltungen + Schaechte) verwenden — damit entfaellt auch der willkuerliche Unterschied Blau/Gruen.
**Test:** Converter-Unit-Test: „0"→rot, „4"→gruen, „ohne"→grau, Unbekannt→grau.

### O2 — Donut: Gesamtzahl in die Mitte + Hover-Feedback — Aufwand: M
**Datei:** `DonutChart.cs`
**Befund:** Die Mitte des Donuts ist leer; die Gesamtzahl steht nirgends. Segmente geben ausser dem Cursor kein Hover-Feedback (ToolTip existiert, `:157`).
**Fix:**
1. Neues optionales DependencyProperty `CenterText` (string) + `CenterLabel` (string, z. B. „Haltungen"); wenn gesetzt, zwei zentrierte TextBlocks ins Canvas legen (Gesamtzahl gross/fett, Label klein in MutedBrush-Farbe). XAML bindet `CenterText` an `ActiveDashboard.Haltungen.Total` bzw. `.Schaechte.Total` (`ZustandVerteilung.Total` existiert bereits, `DashboardStatisticsBuilder.cs:28`).
2. Hover: `MouseEnter/MouseLeave` pro Segment-Path → `Opacity` 1.0 → 0.8 (nur wenn `SegmentCommand` gesetzt ist, sonst kein Interaktions-Signal vortaeuschen).
**Hinweis:** Beim Zeichnen der TextBlocks `Panel.SetZIndex` beachten, damit sie ueber den Segmenten liegen.

### O3 — Hardcodierte Farben in den Chart-Controls — Aufwand: S
**Dateien:** `DonutChart.cs:124` (Empty-Ring RGB 218,223,230), `:155` (Stroke `Brushes.White`), `CategoryBars.cs:149` (Track RGB 230,234,239)
**Befund (verifiziert):** Verstoss gegen die Theme-Regel. Bei einem spaeteren Theme-Wechsel brechen diese Stellen optisch aus.
**Fix:** In den Controls per `TryFindResource("BorderLightBrush")` / `TryFindResource("CardBrush")` aufloesen, mit den bisherigen RGB-Werten als Fallback (Controls sollen ohne Theme-Dictionary, z. B. in Tests, weiter funktionieren). Die Zustandsfarben-Palette selbst bleibt hardcodiert — das sind Fachfarben, keine Theme-Farben.

### O4 — KPI-Kacheln vereinheitlichen — Aufwand: S
**Datei:** `OverviewPage.xaml:265-302` (obere Reihe), `:439-477` (untere Reihe)
**Befunde (verifiziert):**
- Schriftgroessen wild gemischt: 26/26/24/20 oben, 22 unten.
- „CHF" doppelt: Wert zeigt „12'345.67 CHF" (`FormatDashboardCostText`, ViewModel:240-241) UND Untertitel „CHF Sanierung".
- UniformGrid-Spacing asymmetrisch: Karten 1–3 haben `Margin="0,0,8,0"`, Karte 4 nicht → Karten 1–3 sind 8 px schmaler.
- Label „Z0/Z1" kryptisch.
**Fix:**
1. Einheitliche Wertgroesse: oben ueberall `FontSize="24"`, unten ueberall `22`.
2. `FormatDashboardCostText` auf `stats.TotalCost.ToString("N0")` ohne „CHF" aendern (Rappen sind auf Dashboard-Ebene Rauschen); Untertitel „Sanierungskosten (CHF)". Achtung: Property heisst `DashboardCostText` — Format-Test ggf. anpassen.
3. Spacing: alle vier Karten `Margin="0,0,8,0"` und dem UniformGrid `Margin="0,0,-8,12"` geben (Standard-Trick fuer gleiche Breiten) — oder schlicht die letzte Karte auch mit rechtem Margin versehen und die Asymmetrie akzeptieren; wichtig ist Konsistenz oben und unten gleich.
4. „Z0/Z1" → „Dringend (Z0/Z1)".

### O5 — Suchfeld: Lupe + Loeschen-Kreuz — Aufwand: S
**Datei:** `OverviewPage.xaml:88-109`
**Fix:** Links im Suchrahmen ein Lupen-Glyph (`&#xE721;`, Segoe MDL2 Assets, MutedBrush); rechts ein kleines „✕" (Button, transparent, nur sichtbar wenn `FilterText` nicht leer — `StringToVisibilityConverter` ohne invert), Klick setzt `FilterText=""`. Kein neues Command noetig, geht per kleinem `ClearFilterCommand` im ViewModel (1 Zeile) sauberer als Code-Behind.

### O6 — Leere Zustaende aufwerten — Aufwand: S
**Datei:** `OverviewPage.xaml:218-242`
**Befund:** „Projekt waehlen / Keine Vorschau aktiv." und „Noch keine Daten" sind karg; der Drag&Drop-Support (F3) ist unsichtbar.
**Fix:** Beide Karten zentriert mit grossem Segoe-MDL2-Icon (`&#xE8B7;` Ordner bzw. `&#xE9D9;` Diagramm, 32 px, MutedBrush), Titel, und einer Hinweiszeile: „Projekt links waehlen — oder Projektordner hierher ziehen." bzw. „Daten importieren unter ‚Import', dann erscheint hier die Auswertung."

### O7 — Vertikale Kosten-Balken ohne Wertbeschriftung — Aufwand: S
**Datei:** `CategoryBars.cs:190-229` (`CreateVerticalColumn`)
**Befund (verifiziert):** Bei `Orientation=Vertical` (Kacheln „Haltungskosten nach DN") steht der Wert nur im ToolTip; man muss jeden Balken einzeln anfahren.
**Fix:** Ueber dem Balken (Row 0, unten ausgerichtet) einen TextBlock mit `item.ValueText` (FontSize 10, MutedBrush-Farbe via TryFindResource, Fallback Grau) rendern. Bei sehr schmalen Spalten (< 30 px) ausblenden, damit nichts ueberlappt.

### O8 — Lade-Zustand fuer die Vorschau (gehoert zu F1) — Aufwand: S
**Dateien:** `OverviewPage.xaml`, ViewModel
**Fix:** Neue Property `IsPreviewLoading` (aus F1); im XAML eine schlichte Karte „Vorschau wird geladen…" mit `ProgressBar IsIndeterminate="True"` (Hoehe 4), sichtbar nur waehrend des Ladens. Kein Spinner-Overkill.

### O9 — Umlaute in sichtbaren UI-Texten vereinheitlichen — Aufwand: S (niedrigste Prioritaet)
**Datei:** `OverviewPage.xaml` (u. a. „Oeffnen" ×3, „Loeschen", „Projekt waehlen", „Schaechte", „Gesamtlaenge", „Haeufigste Schaeden") vs. „Projektübersicht" (Zeile 24) und Dialogtexte mit echten Umlauten (ViewModel:434-436)
**Befund (verifiziert):** Mischmasch aus „ae/oe"-Schreibweise und echten Umlauten auf derselben Seite.
**Fix:** Alle **sichtbaren** UI-Texte dieser Seite auf echte Umlaute (Öffnen, Löschen→Ausblenden aus F4, wählen, Schächte, Gesamtlänge, Häufigste Schäden). Nur sichtbare Texte — Kommentare/Bezeichner nicht anfassen. Datei ist bereits UTF-8 mit Umlauten (Zeile 24 beweist es), Encoding-Risiko gering.

---

## STUFE 3 — MITTELFRISTIG (nur nach Ruecksprache mit Pascal)

### M1 — Projektliste asynchron laden
`LoadAllProjects` (ViewModel:310-392) scannt Verzeichnisse und parst jede projekt.json synchron auf dem UI-Thread. Bei vielen Projekten/Netzlaufwerken friert der Start der Seite ein. → Scan + Parse per `Task.Run`, Ergebnis am Dispatcher einsetzen; waehrenddessen „Projekte werden gesucht…" in der Liste. Vorsicht: `ApplyFilter`/Selection-Logik haengt daran — sauber sequenzieren.

### M2 — Kontextmenue fuer die Projektliste
Rechtsklick auf Eintrag: „Oeffnen", „Im Explorer anzeigen" (`explorer.exe /select,<pfad>`), „Aus Uebersicht ausblenden". Kleiner Komfort, spart den Weg ueber die Buttons.

### M3 — Cockpit-Kacheln „Haltungen sanieren" klickbar machen
Analog zur Donut-Navigation: Klick auf die Kachel „Haltungen sanieren" filtert die Datenseite auf `Sanieren_JaNein=Ja`. Erfordert einen neuen `DataPageStartFilter`-Fall — erst pruefen, ob es den schon gibt.

---

## Empfohlene Commit-Reihenfolge

1. F4 + F5 + F6 (kleine Listen-Fixes) — 1 Commit
2. F3 (Drag&Drop) — 1 Commit
3. F1 + F2 + O8 (asynchrone Vorschau) — 1 Commit
4. F7 (Navigation Donut/„ohne") — 1 Commit
5. F8 (Stammdaten-Karte) — 1 Commit
6. O1 (Farb-Konsistenz + Converter) — 1 Commit
7. O2 + O3 (Donut-Mitte, Hover, Theme-Farben) — 1 Commit
8. O4 + O5 + O6 + O7 (KPI, Suchfeld, Empty-States, Balken-Labels) — 1–2 Commits
9. O9 (Umlaute) — 1 Commit, ganz am Schluss (reiner Textdiff)

Nach jedem Commit: `dotnet build` + `dotnet test` gruen. STUFE 3 nicht ohne Freigabe beginnen.
