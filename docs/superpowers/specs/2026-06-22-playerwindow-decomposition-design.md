# Design: PlayerWindow schrittweise entflechten — Pilot DamageMarkerController

- **Datum:** 2026-06-22
- **Fortschreibung 2026-06-24:** Die Spec bleibt massgeblich. Aktueller Stand: `DamageMarkerController`, `QuickScanController`, Schritt 3 `CodingOverlayRenderController`, `DetectionConfirmationBuffer`, der erste `LiveDetectionController`-Schnitt, `CodingOsdMeterController`, der erste `CodingAiController`-Schnitt inklusive Health-Monitor-State, `CodingFrameReadinessController`, `CodingLiveFindingEventWorkflow`, `CodingMultiModelFindingEventWorkflow`, `CodingAiResultWorkflow`, `CodingMultiModelAnalysisResultWorkflow`, `CodingStructuralClassifierResultWorkflow`, `CodingBoundaryClassifierResultWorkflow`, `CodingBoundaryEventWorkflow`, `LiveDetectionResultWorkflow`, `LiveDetectionErrorWorkflow`, `LiveDetectionSnapshotWorkflow`, `LiveDetectionTickStartWorkflow`, `LiveDetectionInferenceWorkflow`, `LiveDetectionStopUiWorkflow`, `PlayerWindowClosingWorkflow`, `PlayerWindowCleanupWorkflow`, `PlayerWindowClosedWorkflow`, `CodingModePreparePlaybackWorkflow`, `CodingModeExitFinalizationWorkflow`, `CodingModeDefaultToolWorkflow`, `CodingModeExitTeardownWorkflow`, `CodingModeShowUiWorkflow` und `CodingModeBackgroundServicesWorkflow` sind umgesetzt; dazu kamen `CodingUiUpdateWorkflow`, `LiveDetectionRuntimeStartWorkflow` und mehrere UI-Adapter. Aktuelle Vermessung: `PlayerWindow*.cs` = 95 Dateien / 4885 Zeilen, `PlayerWindow.xaml` = 822 Zeilen, zusammen 5707 Zeilen. Die alte Angabe "33 Dateien / ~7.500 Zeilen" ist historisch.
- **Aktualisierte Reihenfolge 2026-06-24:** 1) Overlay-Abstraktion / `CodingOverlayRenderController`, 2) `ConfirmationBuffer` fuer `_detectionPending*`, 3) `LiveDetectionController`, 4) `CodingAiController` in zwei Stufen. Damit wird die alte Reihenfolge "CodingAi vor LiveDetection" ueberschrieben, weil der geteilte Puffer vor den grossen Controllern als eigenes Objekt herausgezogen wurde.
- **Status:** Design freigegeben (Pilot-Zuschnitt: schlank & direkt)
- **Scope-Entscheidung:** Pilot zuerst — diese Spec beschreibt EINEN Pilot-Schnitt im Detail, der Rest ist nur skizziert.
- **Grundlage:** Kopplungsanalyse 2026-06-22 (6 Subsysteme kartiert, synthetisiert) + Architektur-Audit 2026-06-21 (Gesamtnote B-, `PlayerWindow` als einziger God-Class-Befund).

## 1. Kontext & Problem

`PlayerWindow` ist aktuell eine `partial class` über **95 Dateien / 4885 Zeilen** plus **822 Zeilen XAML**. Das Aufteilen in viele kleine Partial-Dateien hat die Lesbarkeit verbessert, aber die eigentliche Kopplung entsteht weiterhin dort, wo veränderlicher Zustand im Fenster geteilt bleibt. Genau diese Felder werden Schritt für Schritt in fokussierte Controller verschoben.

Das ist kein Stabilitäts-, sondern ein Wartbarkeitsrisiko: eine Änderung kann Playback, Codierung, Overlay, Live-Erkennung und Speichern gleichzeitig betreffen, weil sie sich denselben Zustand teilen.

## 2. Ziel & Nicht-Ziel

**Ziel:** Das Muster beweisen, mit dem `PlayerWindow` schrittweise und **verhaltensneutral** in mehrere kleine, fokussierte Controller-Klassen zerlegt werden kann — beginnend mit dem am besten isolierten Block als Pilot.

**Nicht-Ziel:**
- Kein „Big-Bang"-Umbau. Nach dem Pilot wird neu entschieden, ob überhaupt weiter zerlegt wird.
- Keine Verhaltensänderung. Kein Feature, kein Bugfix, keine geänderte Reihenfolge von `await`/Dispatcher-Hops.
- Kein großer „CodingSessionController" (siehe Grundprinzip 1).

## 3. Grundprinzipien (gelten für alle späteren Schritte)

1. **Mehrere fokussierte Controller, KEIN Mono-Controller.** Die Feld-Cluster haben verschiedene Lebenszyklen (DamageMarkers leben so lang wie das Fenster; Coding-State nur zwischen `EnterCodingMode`/`ExitCodingMode`; Playback bis `OnClosing`). Ein einziger Controller würde diese Lebenszyklen vermischen und den God-Object-Knoten nur umbenennen.
2. **Das Fenster bleibt View-Owner und delegiert.** Es behält die benannten XAML-Controls, die Dispatcher-Threadaffinität und die Event-Verdrahtung. Ein Controller bekommt schmale Abhängigkeiten hereingereicht — **nie das ganze Fenster**. Rückmeldung Controller→Window nur über Events/Callbacks, damit der UI-Update-Pfad und die Thread-Affinität exakt erhalten bleiben.
3. **Der `OnClosing`-Teardown bleibt im Fenster** als Koordinator (sicherheitskritische Reihenfolge: `_closing=true` VOR `_player.Dispose`, dann Stop-Aufrufe). Jeder Controller liefert nur eine eigene `Stop()`/`Dispose()`-Methode, die der Koordinator in unveränderter Reihenfolge aufruft.
4. **Geteilter Zustand wird in drei Stufen behandelt** (siehe Abschnitt 6).

## 4. Pilot-Design: `DamageMarkerController`

### 4.1 Warum dieser Block (belegt)
Die orangen Schadensmarker auf der Zeitleiste ([PlayerWindow.Playback.DamageMarkers.cs](../../../src/AuswertungPro.Next.UI/Views/Windows/PlayerWindow.Playback.DamageMarkers.cs)) sind der einzige Block mit beweisbar **geschlossener Grenze** (Isolations-Rating 5/5):

- `_damageMarkers` wird **ausschließlich** in dieser Datei gelesen/geschrieben (per Grep über das gesamte Windows-Verzeichnis bestätigt).
- `_damageOverlay` ist **readonly, ctor-injiziert** (xaml.cs:105/161); der einzige Fremdzugriff außerhalb ist ein value-only-Read von `PipeLengthMeters` in `Coding.Persistence.cs:153` — keine Marker-Abhängigkeit.
- Exakt **2 externe Aufrufpunkte**: `BuildDamageMarkers()` im `Loaded`-Handler (xaml.cs:266), `RepositionDamageMarkers()` im `DamageMarkerCanvas.SizeChanged`-Handler (xaml.cs:277).
- Sämtliche Geometrie liegt bereits im statischen `PlayerTimelineLayoutCalculator`; der Rest ist reines Canvas-Zeichnen.
- **Kein einziges `_coding*`-Feld, keine Timer, kein `async`, keine `_closing`/`_playbackDisposed`-Guards** → vom größten Risiko (Threading/Reentrancy) gar nicht betroffen.

### 4.2 Verantwortung & Zustand
Der Controller besitzt genau eine Aufgabe: **Schadensmarker auf der Zeitleiste bauen, positionieren und auf Klick anspringen.**

Exklusiver Zustand, der hineinwandert:
- `_damageMarkers` (Liste der `(DamageMarkerInfo, Container, TickOrRange, Label)`-Tupel) → privates Feld des Controllers.

### 4.3 Abhängigkeiten (Konstruktor, schlank & direkt — kein Interface)
Konkrete Abhängigkeiten, direkt durchgereicht (Abstraktionen wie `IOverlaySurface` kommen erst in Schritt 3, wo sie wirklich gebraucht werden — YAGNI):

- `Canvas markerCanvas` — die `DamageMarkerCanvas` (Zeichenfläche; löst über Ressourcen-Kaskade selbst `AccentBrush`/`ColorAccent` per `FindResource` auf, kein Window nötig).
- `Slider positionSlider` — die `PositionSlider` (für `GetSliderTrackBounds`/`PART_Track` und das Seek-Setzen von `Value`/`Maximum`).
- `DamageOverlay damageOverlay` — readonly, liefert `PipeLengthMeters` und `Markers`.
- Schmaler Seek-/UI-Rückruf für `SeekToMeter`: Zugriff auf den `MediaPlayer` (für `SetPause`/`Length`/`Time`/`Position`) plus die beiden Window-Hilfen `EnsurePlaying()` und `UpdateUi()` — als Delegates (`Action`/`Func`) übergeben, damit Verhalten und Thread-Pfad identisch bleiben.

> Die exakte Signatur (Delegates vs. kleines `record` aus Callbacks; ob `MediaPlayer` direkt oder als Accessor) wird im Implementierungsplan festgelegt; am Design ändert das nichts.

### 4.4 Öffentliche API
- `Build()` — entspricht heutigem `BuildDamageMarkers()` (inkl. abschließendem `Reposition()`).
- `Reposition()` — entspricht heutigem `RepositionDamageMarkers()`.

`CreatePointMarker`, `CreateRangeMarker`, `GetSliderTrackBounds`, `SeekToMeter` werden **private** Methoden des Controllers (verbatim verschoben).

### 4.5 Verdrahtung im Window (die einzigen Berührungspunkte)
- Das Window konstruiert den `DamageMarkerController` einmal, sobald `_player` und `_damageOverlay` verfügbar sind (um `Loaded`).
- `Loaded`-Handler (xaml.cs:266): statt `BuildDamageMarkers()` → `_damageMarkerController.Build()`.
- `DamageMarkerCanvas.SizeChanged`-Handler (xaml.cs:277): statt `RepositionDamageMarkers()` → `_damageMarkerController.Reposition()`.
- Das Window behält das Feld `_damageOverlay` (wegen des einen value-only-Reads in `Coding.Persistence.cs:153`) — unverändert.

### 4.6 Verhaltensneutralität (Akzeptanzkriterium)
Es darf sich **nichts** am sichtbaren Verhalten ändern: Marker an identischer Position, identische Tooltips/Labels, Klick-auf-Marker springt exakt gleich (gleiches `EnsurePlaying`→`SetPause`→Slider→`Time/Position`→`UpdateUi`). Kein neuer/entfernter `await`- oder Dispatcher-Hop. Die verschobenen Methoden bleiben Zeile für Zeile gleich, nur Ort und Sichtbarkeit ändern sich.

## 5. Verifikation
- `dotnet build AuswertungPro.sln` → 0 Fehler, 0 Warnungen.
- `dotnet test AuswertungPro.sln` → aktueller Stand: **2.986** Tests grün, **1** Test übersprungen (kein Test darf brechen).
- Optional: ein kleiner Unit-Test, der den Controller mit einem Fake-Overlay baut und prüft, dass `Build()` die erwartete Marker-Anzahl auf das Canvas legt (soweit ohne echtes WPF-Rendering testbar).
- **Manueller Gegencheck im laufenden Player** (Pflicht, da WPF schwer unit-testbar): Video mit Befunden öffnen → Marker erscheinen an richtiger Stelle, Labels/Tooltips korrekt, Klick auf Marker springt auf den richtigen Meterstand, Fenster-Größenänderung positioniert korrekt nach.

## 6. Strategie für geteilten Zustand (für spätere Schritte, nicht Teil des Pilots)
1. **Subsystem-exklusive Felder** (z.B. `_damageMarkers`, `_heatmapRects`, `_quickScanCts`): wandern als private Felder in den jeweiligen Controller. Reines Verschieben.
2. **Readonly ctor-injizierte Eingaben** (z.B. `_damageOverlay`, `_videoPath`): per Konstruktor durchreichen; das Window behält seine Referenz für die wenigen value-only-Fremd-Reads.
3. **Echt geteilte Felder** (die heikelsten): (a) Playback-Kern `_player`/`_libVlc` bleibt bis zu einem eigenen Playback-Schnitt im Window und wird nur über schmale Delegates gelesen. (b) Coding-Kern `_codingVm`/`_codingSessionService` wird später als gemeinsames `CodingSessionState`-Objekt gebündelt. (c) Der frühere Brückenpuffer `_detectionPending*` ist umgesetzt als `DetectionConfirmationBuffer`; LiveDetection und Coding-Multi-Model teilen damit kein loses Feldbündel mehr im Window.

## 7. Extraktions-Reihenfolge (Skizze — erst nach dem Pilot entscheiden)

**Fortschreibung 2026-06-24:** Die folgende urspruengliche Skizze ist historisch. Massgeblich ist die aktualisierte Reihenfolge oben: Overlay-Abstraktion, ConfirmationBuffer, LiveDetectionController, danach CodingAiController.

1. **DamageMarkerController (Pilot)** — beweist das Muster, null Coding-Kopplung.
2. **QuickScanController** — nächst-isoliert; dabei `Cancel()` für `OnClosing` exponieren und das Teardown-Muster etablieren.
3. **CodingOverlayRenderController** — rein lesend; erzwingt die drei wiederverwendbaren Bausteine `IOverlaySurface` (für `CodingOverlayCanvas`), injizierter Coordinate-Mapper und gemeinsame `OverlayTags`-Konstanten.
4. **PlaybackController** — spät, weil `_player` von ~10 Partials gelesen wird; erst schmale Lese-API exponieren. `OnClosing` bleibt als Koordinator im Window.
5. **CodingAiAnalysisController** — 2-stufig (erst Services/Health/CTS/OSD/FrameReadiness hinter Host-Interface, dann die UI-nahe Result-Anzeige).
6. **LiveDetectionController** — historisch hier geplant; durch `DetectionConfirmationBuffer` inzwischen vor `CodingAiController` vorgezogen.
7. **CodingSessionController + OverlayInteractionController + Eingabemarker** — zuletzt; erfordert Migration des `Enter/ExitCodingMode`-Lebenszyklus und von `_codingVm`/`_codingSessionService`.

## 8. Risiken
- **Threading/Reentrancy** (bei späteren Schritten): `async void`-Handler, `SafeFireAndForget` und DispatcherTimer-Callbacks müssen die `_closing`/`_playbackDisposed`-Guards in identischer Reihenfolge behalten. **Der Pilot ist davon bewusst nicht betroffen.**
- **Tag-String-Konvention** beim Overlay-Rendering (`ai_`/`overlay_`/`ref_dn`/`tool_badge`): vor Schritt 3 zentralisieren, sonst stille Render-Leichen.
- **Bestätigungs-Puffer**: `DetectionConfirmationBuffer` ist der gemeinsame Puffer. Neue LiveDetection- oder CodingAi-Schnitte dürfen keine `_detectionPending*`-Felder im Window wieder einführen.
- **`_player` als Querschnitts-Lesezustand**: erst Lese-API, dann verschieben.
- **`OnClosing`-Reihenfolge** ist sicherheitskritisch: bleibt als Koordinator im Window.
- **Verstecktes Singleton**: `Coding.Apply.cs:129` greift via `App.Current.MainWindow.DataContext` aufs ShellViewModel — beim späteren Coding-Schnitt explizit als Abhängigkeit sichtbar machen.

## 9. Offene Punkte (für den Implementierungsplan)
- Exakte Konstruktor-Signatur des Controllers (Delegates vs. Callback-`record`).
- Genauer Konstruktionszeitpunkt (im Window-Ctor vs. `Loaded`), abhängig von der Verfügbarkeit von `_player`.
- Ob ein schlanker Unit-Test für `Build()` mit Fake-Overlay sinnvoll machbar ist oder die manuelle Prüfung genügt.
