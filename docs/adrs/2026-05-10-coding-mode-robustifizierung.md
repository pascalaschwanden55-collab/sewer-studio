# CodingModeWindow Robustifizierung — Mini-ADR

Datum: 2026-05-10
Status: **Vorgeschlagen** (User-Freigabe pro Slice ausstehend)

Vorgeschichte:
- Tiefenanalyse 2026-05-10 hat 8 Bruchstellen im CodingModeWindow identifiziert
  (BBox/SAM-Koordinaten, Frame-Capture, Auto-/Manual-Kalibrierung, stale
  SAM-BBox, DataPage-Sync, Abspielbuttons).
- Punkt **#1 (Letterbox-Koordinaten)** und **#7 (DataPage CompletedProtocol-
  Sync)** sind heute Abend bereits implementiert und im Working Tree
  (commit-bereit). Diese ADR deckt die verbleibenden 6 Punkte ab.
- Slice-Disziplin (Memory `feedback_slice_8a_migration.md`):
  klein/mechanisch, jeder Slice mit Build+Tests+UI-Smoke vor Commit.

## Was diese ADR macht

- Dokumentiert die 6 verbleibenden Bruchstellen mit konkretem Bug + Soll-Verhalten.
- Schlaegt Reihenfolge vor — abhaengigkeitsbasiert, nicht alphabetisch.
- Gibt Aufwand-Schaetzung pro Slice (jeder = eigene Session).
- Listet Test-/Smoke-Erwartungen.

## Was diese ADR NICHT macht

- **Kein Code in dieser Session.** Pro Slice eigene User-Freigabe ausstehend.
- Kein Big-Bang ueber alle 6 Punkte. Memory-Direktive verbietet das nach den
  drei BBox-Reverts heute Abend.

---

## Slice 8a.6.A — Frame-Capture haerten (Punkt #2)

### Problem
`CaptureCurrentFrameAsync` (in `CodingModeWindow.FrameCapture.cs`):
1. Versucht zuerst `_player.TakeSnapshot` — bei laufendem Video oft still
   gescheitert oder leeres File.
2. Fallback: `RenderTargetBitmap` auf VideoView/OverlayCanvas-Parent. Bei
   VLC-HwndHost liefert das oft **schwarz** (kommentiert in
   `RenderVideoViewToPng` Z.79+).

Folge: SAM/Qwen bekommen unbrauchbares Bild → Klassifikation falsch oder
SAM-Maske leer.

### Soll
- Capture-Pfad mit klarer Reihenfolge: TakeSnapshot mit ausreichender Wartezeit
  und State-Pruefung (Player muss `Playing` oder `Paused` sein, nicht `Opening`).
- Fallback **nur** wenn TakeSnapshot eindeutig gescheitert ist (Datei leer/zu klein).
- Wenn beide scheitern: explizit Fehler zurueckgeben (null + Status-Text), nicht
  schwarzes PNG das die Pipeline kaputt macht.
- Zusaetzlich: Frame-Validierung — wenn Bild zu uniform (alle Pixel gleich/schwarz),
  als Fehler werten.

### Schritte
1. State-Pruefung vor TakeSnapshot.
2. Fallback nur bei explizitem Fehler.
3. Frame-Validierung (z.B. Histogramm-Spread mindestens X).
4. Fehlerpfad mit User-sichtbarem Status statt stillem null.
5. Tests: Validierungs-Helper als unit-testbare statische Methode.

### Aufwand: ~1-2h, +2-3 Tests

---

## Slice 8a.6.B — SAM-Pfad haerten + stale BBox (Punkt #6)

### Problem
`_lastSamTightBbox` wird nicht konsequent zurueckgesetzt:
- Bei BBox-Tool-Wechsel
- Bei SAM-Fehler
- Bei neuer User-BBox die SAM nicht erfolgreich segmentiert

Folge: alter Tight-BBox-Wert wird beim Trainings-Export verwendet, obwohl
er zur aktuellen BBox/Frame nicht mehr passt.

### Soll
- `_lastSamTightBbox = null` in jedem `OverlayCanvas_MouseLeftButtonDown` (neue Geste).
- `_lastSamTightBbox = null` im SAM-Catch-Block (HTTP-Fehler, Sidecar-down).
- `_lastSamTightBbox = null` wenn SAM-Response kommt aber Maske leer/ungueltig.
- Trainings-Export prueft Aktualitaet (z.B. timestamp ≤ N Sekunden), sonst
  fallback auf User-BBox.

### Schritte
1. 3 Reset-Punkte hinzufuegen.
2. Optional: Timestamp am `_lastSamTightBbox` fuer Aktualitaet.
3. Tests: stale-BBox-Reset im Failure-Pfad.

### Aufwand: ~30min, +1-2 Tests

---

## Slice 8a.6.C — Manuelle Kalibrierung auf Source-Frame (Punkt #5)

### Problem
`ApplyCalibration(start, end)` arbeitet auf normalized Canvas-Coords. Mit
dem heutigen Letterbox-Fix (#1) sind das Video-Frame-relative Coords —
**aber nur wenn `_videoFrameWidthCache` schon gesetzt ist**. Vor dem ersten
Capture: alte Logik (Canvas-Coords).

Folge: Wenn User vor dem ersten Capture kalibriert, ist die Referenzlinie
auf Canvas-Pixel berechnet, nicht auf Video-Pixel. Pipe-Pixel-Diameter
stimmt nicht.

### Soll
- Vor manueller Kalibrierung: einmaliger Capture-Versuch um Frame-Cache zu
  fuellen. Wenn Cache nicht fuellbar: User-Hint „Bitte erst Frame analysieren
  oder Video kurz laufen lassen".
- Kalibrierung nur akzeptieren wenn Cache nicht-leer.
- ApplyCalibration mit gecachten `_videoFrameWidthCache/Height` arbeiten,
  damit Pipe-Pixel-Diameter im Source-Frame-Koordinatensystem ist.

### Schritte
1. Pre-Calibration-Capture im `BtnCalibrate_Checked`.
2. ApplyCalibration: explizit Source-Frame-Pixel berechnen.
3. User-sichtbarer Status wenn Cache leer.

### Aufwand: ~1h, hängt direkt an #1

---

## Slice 8a.6.D — Auto-Kalibrierung not-blind one-shot (Punkt #3)

### Problem
`_calibrationAutoTried = true` wird in `TryAutoCalibrateOnceAsync` gesetzt
**bevor** klar ist, ob der Frame fuer den Algorithmus geeignet war
(z.B. wegen OSD-Text, schwarze Balken, schlechte Kantenkontrast).
Folge: erster Frame schlecht → Auto-Kal scheitert → kein Retry, manuell
muss her.

### Soll
- `_calibrationAutoTried = true` nur, wenn:
  - Bitmap erfolgreich dekodiert UND
  - Algorithmus mind. eine plausible Kante gefunden hat (auch wenn keine
    saubere Ausgabe).
- Fehlversuche ohne plausible Kante: `_calibrationAutoTried` bleibt false,
  naechste Frame-Iteration versucht erneut (mit Limit, z.B. 5 Versuche).
- Bei finalem Failure: User-Hint „Auto-Kalibrierung fehlgeschlagen,
  bitte manuell kalibrieren".

### Schritte
1. AutoCalibrationService liefert Erfolg/Failure-Reason zurueck.
2. CodingMode-Pfad: nur „erfolgreich" markiert sich.
3. Limit-Counter, kein Endlos-Retry.
4. Tests: Retry-Logik mit Mock-AutoCalibration-Antworten.

### Aufwand: ~1h, +2-3 Tests

---

## Slice 8a.6.E — Auto-Kalibrierung Letterbox-fest (Punkt #4)

### Problem
`AutoCalibrationService.TryAutoCalibrate` scannt horizontale Helligkeitskanten
ueber das ganze Bild. Im typischen Inspektions-Frame liegen OSD-Text,
schwarze Balken, helle Wand und Rohrkante uebereinander → Heuristik wird
verwirrt.

### Soll
- ROI-Begrenzung: scanne nur den **mittleren 60%** des Bildes (vertikal),
  damit OSD-Text oben/unten ausgeschlossen ist.
- Pillarbox-/Letterbox-Erkennung: scharfe schwarze Kanten am Bildrand
  ignorieren.
- Mindest-Kontrast-Schwelle: nur Kanten mit ausreichendem Helligkeits-
  Gradient als Rohrkante akzeptieren.

### Schritte
1. ROI-Maske im AutoCalibrationService.
2. Pillarbox-Erkennung (Pixel-Sampling am linken/rechten Rand).
3. Min-Kontrast-Filter.
4. Tests: synthetische Test-Bilder mit Pillarbox + OSD.

### Aufwand: ~3-4h, +5-7 Tests, Algorithmus-Tuning

---

## Slice 8a.6.F — Abspielbuttons im CodingModeWindow (Punkt #8)

### Problem
- Aktuell: Steuerung-Row hat nur Zurueck/Weiter, Pause, Fortsetzen,
  Abschliessen, Abbrechen — Session-/Meter-Steuerung, kein freier Player.
- User-Wunsch (klar 2026-05-10): „Videoplayer und Codiermodus = ein Fenster".
- Mein Versuch heute Abend (Sub-Slice 1) Player-Toolbar als **neue Outer-
  Grid-Row** hat BBox kaputt gemacht — Layout-Restruktur ist im
  CodingModeWindow nicht klein-mechanisch.

### Soll
- Play/Pause/Stop + Speed-Buttons + Position-Slider sichtbar.
- **OHNE** neue Outer-Grid-Row — stattdessen in der bestehenden
  Steuerung-Row 5 ergaenzen oder im Header (Row 0).
- Position-Slider als eigenes kleines UserControl, nicht direkt im
  CodingModeWindow-XAML.
- DispatcherTimer fuer Slider-Update — siehe heute Abend revertete Sub-Slice
  1, Code-Inhalt war OK, NUR Layout-Stelle problematisch.

### Schritte
1. Steuerung-Row inspizieren: ist Platz fuer Play/Pause/Stop/Speed?
2. Wenn nein: neues UserControl `PlayerToolbarControl.xaml` extrahieren,
   das in einem Auto-Row im **inneren** Sidebar-Grid hingelegt wird (NICHT
   ins Outer-Grid).
3. Click-Handler in Code-Behind oder UserControl-Codebehind.
4. Position-Slider mit DispatcherTimer.
5. Build + Tests.
6. **Pflicht: UI-Smoke** vor Commit. BBox + Kalibrierung muessen weiter gehen.
7. Wenn UI-Smoke fail: revert sofort, Mini-ADR-Erweiterung mit Stack-Trace.

### Aufwand: ~2-3h, UI-Smoke pflicht, Risiko mittel

---

## Reihenfolge

```
#1 Letterbox-Coords     → DONE (heute Abend, im Working Tree)
#7 CompletedProtocol    → DONE (heute Abend, im Working Tree)
                            ↓
8a.6.A Frame-Capture haerten     ← keine Abhaengigkeit, einfachster Slice
                            ↓
8a.6.B SAM-Stale-BBox            ← unabhaengig, klein
                            ↓
8a.6.C Manuelle Kal. auf Source  ← haengt direkt an #1 (Cache)
                            ↓
8a.6.D Auto-Kal. not-blind       ← haengt an .A (Capture muss zuverlaessig sein)
                            ↓
8a.6.E Auto-Kal. Letterbox-fest  ← haengt an .D
                            ↓
8a.6.F Player-Toolbar im UI      ← unabhaengig, riskanteste Stelle (Layout)
```

**.A und .B** koennen heute Abend noch (klein, kein UI-Risiko).
**.C** ist Pflicht-Folge wenn .A+#1 fertig.
**.D + .E** sind Algorithmus-Slices — eigene Session.
**.F** ist die UI-Erweiterung — nach allen Robustifizierungen, weil sonst
Layout-Touch wieder BBox kaputt macht.

## Aufwand-Schaetzung gesamt

| Slice | Aufwand | Risiko |
|---|---|---|
| .A Frame-Capture | 1-2h | gering |
| .B Stale SAM-BBox | 30min | gering |
| .C Manual-Kal Source | 1h | mittel |
| .D Auto-Kal not-blind | 1h | gering |
| .E Auto-Kal Letterbox | 3-4h | mittel (Algorithmus) |
| .F Player-Toolbar UI | 2-3h | hoch (Layout) |
| **Gesamt** | **8-12h** | mind. 6 Sessions |

## Test-Erwartung

- Pro Slice: `dotnet build` 0 Warn / 0 Err.
- Pro Slice: `dotnet test` alle gruen (1030+ neue).
- Slices .A/.B/.C/.D/.E: Unit-Tests pflicht (statische Helper, klassisch testbar).
- Slice .F: Manueller UI-Smoke pflicht (BBox + Kalibrierung weiter ok, neue
  Toolbar funktioniert).

## Stop-Liste / Wenn-dann

- Wenn .F UI-Smoke fail: sofort revert (heute Abend bereits 3x passiert).
  Mini-ADR-Erweiterung mit Stack-Trace, dann gezielter Re-Try.
- Wenn .E Algorithmus zu komplex wird: ROI-only-Variante als ausreichend
  akzeptieren, vollwertige Letterbox-Erkennung optional.
- Wenn .C Cache-Fuellung scheitert: User-Hint statt stillem Fail-Through.

## User-Freigabe

Bitte bestaetigen pro Slice:
- (Q1) Reihenfolge .A → .B → .C → .D → .E → .F akzeptiert?
- (Q2) Heute Abend noch .A (Frame-Capture haerten) anfangen, oder Schluss?
- (Q3) Welche Slices als zusammenhaengende Mini-ADR-Folgekette
  ausarbeiten (.D+.E zusammen oder getrennt)?
