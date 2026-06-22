# PlayerWindow QuickScanController — Schritt 2 der Decomposition

**Ziel:** Den Schnell-Scan (QuickScan/Heatmap) verhaltensneutral aus `PlayerWindow` in einen `QuickScanController` ziehen — nach dem in Schritt 1 etablierten Muster (exklusiver Zustand → Controller, geteilte Helfer als Delegate, Window bleibt View-Owner und delegiert, Teardown-Reihenfolge bleibt im Window).

**Grundlage:** Spec 2026-06-22-playerwindow-decomposition-design.md (Reihenfolge-Schritt 2) + Kopplungsanalyse. Muster bewährt in Schritt 1 (DamageMarkerController, Commits 2c17ceaa/d63177a6/ab7ffbf6).

## Verifizierte Kopplung (Ist-Zustand)
- **Exklusiver Zustand** (nur in QuickScan.cs benutzt): `_isQuickScanning`, `_heatmapRects`. Deklariert in `PlayerWindow.xaml.cs:99-101`.
- **Geteilter Zustand:** `_quickScanCts` — zusätzlich an ZWEI Teardown-Stellen außerhalb gecancelt: `PlayerWindow.xaml.cs:299` (Codier-Modus beenden) und `PlayerWindow.Playback.cs:381` (`OnClosing`, sicherheitskritische Reihenfolge).
- **Methoden in QuickScan.cs:** `QuickScan_Click` (async void, XAML-gebunden via `Click="QuickScan_Click"`), `AddHeatmapSegment`, `RepositionHeatmap`.
- **Externe Wiring-Punkte:** `xaml.cs:277` `HeatmapCanvas.SizeChanged += RepositionHeatmap`.
- **Geteilter Helfer:** `GetSliderTrackBounds()` (lebt seit Schritt 1 im Window/Playback-Partial) — wird per Delegate injiziert (wie bei DamageMarkerController).
- **XAML-Controls:** `HeatmapCanvas`, `QuickScanButton` (ToggleButton), `QuickScanStatusText`.
- **Services/Statics (kein Window-Zustand):** `AppSettingsAiSettingsProvider`, `OllamaClient`, `QuickScanService`, `FfmpegLocator`, `DialogHost`, `LiveDetectionDisplayPolicy`, `QuickScanHeatmapLayoutPolicy`.

## Zielbild
`QuickScanController` (in `src/AuswertungPro.Next.UI/Player/`) besitzt `_quickScanCts`/`_isQuickScanning`/`_heatmapRects` und die Logik. Konstruktor-Abhängigkeiten: `HeatmapCanvas`, `QuickScanButton`, `QuickScanStatusText`, `MediaPlayer`, `videoPath`, `EnsurePlaying`, `UpdateUi`, `GetSliderTrackBounds` (Delegate).
Öffentliche API:
- `Task ToggleAsync()` — Rumpf des alten `QuickScan_Click`.
- `void Reposition()` — altes `RepositionHeatmap`.
- `void Cancel()` — `_quickScanCts?.Cancel();` (für beide Teardown-Stellen, null-safe wie heute).

Das Window behält den XAML-gebundenen Event-Handler als **dünne Hülle**:
`private async void QuickScan_Click(object sender, RoutedEventArgs e) => await _quickScanController.ToggleAsync();`

## Verhaltensneutralität (kritisch)
- Der einzige neue `await` ist die dünne Hülle, die `ToggleAsync()` awaitet (Tail-Await, kein Folgecode) → kein Verhaltensunterschied. Innerhalb `ToggleAsync` bleiben alle `await`/Dispatcher-Hops Zeile-für-Zeile identisch; UI-Zugriffe laufen weiter auf dem UI-Thread (SynchronizationContext).
- `Cancel()` repliziert exakt `_quickScanCts?.Cancel()` (null-safe). Die Teardown-Reihenfolge in `OnClosing` (Playback.cs) und im Codier-Beenden (xaml.cs) bleibt unverändert — nur der Aufruf wechselt von `_quickScanCts?.Cancel()` zu `_quickScanController.Cancel()` an exakt derselben Stelle.

## Tasks

### Task 1: QuickScanController anlegen + Window verdrahten
- Neu: `src/AuswertungPro.Next.UI/Player/QuickScanController.cs` (Zustand + ToggleAsync/Reposition/Cancel/AddHeatmapSegment, Usings aus QuickScan.cs + `System.Threading.Tasks`, MediaPlayer-Alias).
- `PlayerWindow.xaml.cs`: Felder `_quickScanCts/_isQuickScanning/_heatmapRects` (Z.99-101) entfernen; Feld `_quickScanController` ergänzen; Controller nach `_player`/neben `_damageMarkerController` konstruieren.
- `PlayerWindow.LiveDetection.QuickScan.cs`: auf die dünne Hülle `QuickScan_Click` reduzieren (Rest wandert in den Controller).
- Build (UI-Projekt) muss grün sein.

### Task 2: Wiring + Teardown umstellen
- `xaml.cs:277`: `RepositionHeatmap()` → `_quickScanController.Reposition()`.
- `xaml.cs:299`: `_quickScanCts?.Cancel()` → `_quickScanController.Cancel()`.
- `Playback.cs:381`: `_quickScanCts?.Cancel()` → `_quickScanController.Cancel()`.
- Build grün, volle UI-Tests grün.

### Task 3: Architektur-Guard
- In `UiArchitectureGuardTests.cs` Test `PlayerWindow_quickscan_lives_in_controller`: QuickScanController.cs existiert; Window-Partials enthalten NICHT `_heatmapRects`/`_isQuickScanning`/`AddHeatmapSegment`/`RepositionHeatmap`; Window nutzt `new QuickScanController`, `_quickScanController.Reposition()`, `_quickScanController.Cancel()`, `_quickScanController.ToggleAsync()`; Controller enthält die Heatmap-Liste + `QuickScanHeatmapLayoutPolicy`.
- Gefilterten Test + volle UI-Tests grün.

### Task 4: Endverifikation
- Voller Solution-Build 0/0, komplette Testsuite grün.
- **Manueller Check** (WPF nicht unit-testbar): Schnell-Scan starten → Heatmap-Segmente erscheinen/färben korrekt; Abbrechen (Button erneut) bricht ab; Klick auf Segment springt zur richtigen Zeit; Fenster-Resize positioniert Heatmap nach; Fenster schließen während Scan läuft → kein Crash (Teardown).

## Commits (pro Task, jeder grün)
1. `refactor: extract quick scan controller`
2. `refactor: route quick scan teardown through controller`
3. `test: guard quick scan controller split`
