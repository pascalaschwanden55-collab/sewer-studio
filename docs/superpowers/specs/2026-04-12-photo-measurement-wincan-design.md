# PhotoMeasurement — WinCan PhotoAssistant 1:1 Redesign

## Kontext

Das bestehende `PhotoMeasurementWindow` hat 11 Werkzeuge, ist aber in Bedienung und Optik
nicht auf WinCan-Niveau. Dieses Redesign bringt es auf den Stand des CDLAB.PhotoAssistant.

**Quelle:** WinCan PhotoAssistant Sprachdatei `GERMANY_WinCanPhotoAssistant.lng`
aus `C:\Program Files (x86)\CDLAB\Assemblies\Languages\`.

## Ist-Zustand (SewerStudio)

| Werkzeug | Status | Problem |
|----------|--------|---------|
| Kalibrierung | vorhanden | Referenzlinie bleibt sichtbar nach Abschluss |
| Lineal (Distanz) | vorhanden | Masszahl zu klein, keine Endpunkt-Handles |
| Wasserstand | vorhanden | Slider-Bedienung ok, optisch duenn |
| Ablagerung | vorhanden | wie Wasserstand |
| Hindernis | vorhanden | wie Wasserstand |
| Deformation | vorhanden | 4-Punkt, aber kein mm/%-Ergebnis am Overlay |
| Querschnitt (Polygon) | vorhanden | optisch schwach, keine Flaechen-% am Overlay |
| Anschluss-DN | vorhanden | Slider, kein visuelles DN-Label am Kreis |
| Biegewinkel | vorhanden | funktioniert gut, nur Optik-Upgrade noetig |
| Rissbreite | **FEHLT** | WinCan hat `btnMeasureCrack` |
| Rohroberflaechen-Distanz | **FEHLT** | WinCan hat `btnMeasurePipe` (korrigiert fuer Rohrkruemmung) |
| Foto-Export mit Grafiken | teilweise | kein "Speichern mit Grafiken" Button |
| Bild Pan/Rotate/Zoom | **FEHLT** | WinCan hat 3 separate Modi |
| Entzerren (Undistort) | **FEHLT** | WinCan hat Kamera-Kalibrierungsdatei |
| 3D-Einblendung Rohr | **FEHLT** | WinCan blendet perspektivischen Zylinder ein |
| 3D-Einblendung Abzweiger | **FEHLT** | WinCan blendet 3D-Seitenrohr ein |
| 3D-Einblendung Bogen | **FEHLT** | WinCan blendet 3D-Rohrbogen ein |
| 3D-Einblendung Muffenversatz | **FEHLT** | WinCan blendet 3D-Versatz ein |
| Stundenlinien-Overlay | **FEHLT** | WinCan blendet Uhr-Raster ein |
| Konfigurierbare Farben | **FEHLT** | WinCan hat Farbwahl fuer jedes Element |
| Einblendgrad (Nichts/Minimal/Maximal) | **FEHLT** | WinCan hat 3 Stufen |
| Muffenversatz messen | **FEHLT** | WinCan hat `strJointOffset` |

## Soll-Zustand: 3 Phasen

### Phase 1 — Optik + Bedienung (Prioritaet HOCH)

Bestehende Werkzeuge auf WinCan-Qualitaet bringen. Kein neuer Code-Pfad, nur Rendering.

#### 1.1 Overlay-Rendering (alle Werkzeuge)

**Linien:**
- Dicke: 3px (statt 2px/2.5px)
- Endpunkte: Quadratische Handles 8x8px, weiss gefuellt
- Anti-Aliasing: `RenderOptions.SetEdgeMode(line, EdgeMode.Unspecified)` (Standard WPF AA)

**Masszahlen direkt am Overlay:**
- Schrift: 14pt Bold, `Segoe UI`
- Farbe: Weiss auf halbtransparentem Schwarz (`#B4000000`)
- Padding: 6px horizontal, 3px vertikal
- CornerRadius: 4px
- Position: Mittelpunkt der Linie, 12px versetzt nach oben
- Inhalt je Werkzeug:
  - Lineal: `"23.5 mm"`
  - Wasserstand: `"35.2 %"`
  - Deformation: `"12.4 % Reduktion"`
  - Querschnitt: `"28.7 % Verringerung"`
  - Anschluss: `"DN 150 (50.0 %)"`
  - Bogen: `"42.5 Grad"`
  - Rissbreite: `"2.3 mm"`

**Werkzeug-Farben (WinCan-Stil):**

| Werkzeug | Farbe | Hex |
|----------|-------|-----|
| Kalibrierung | Magenta | #FF00FF |
| Distanz/Lineal | Lime | #00FF00 |
| Rissbreite | Rot | #FF3333 |
| Wasserstand | Koenigsblau | #4169E1 |
| Ablagerung | Schokolade | #D2691E |
| Hindernis | Crimson | #DC143C |
| Deformation | Orange | #FFA500 |
| Querschnitt | MediumPurple | #9370DB |
| Anschluss-DN | Cyan | #00FFFF |
| Bogen | Gelb | #FFFF00 |
| Muffenversatz | HotPink | #FF69B4 |
| Rohrkreis | Weiss (50% alpha) | #80FFFFFF |

#### 1.2 Kalibrierungslinie ausblenden

Nach `ApplyCalibration()` alle Canvas-Elemente mit `Tag == TagPreview` entfernen.
Zusaetzlich: Kalibrierungs-Status in Statusbar anzeigen: `"Kalibriert: DN 300, 0.724 norm"`

#### 1.3 Stundenlinien-Overlay

Optional einblendbar (ToggleButton in Toolbar):
- 12 Linien vom Rohrmittelpunkt zum Rand (1px, weiss, 30% alpha)
- Beschriftung: "12", "3", "6", "9" an den Hauptpositionen (10pt, weiss)
- Nutzt `_calibration.PipeCenter` und `_calibration.NormalizedDiameter`

#### 1.4 Einblendgrad (3 Stufen)

| Stufe | Was sichtbar |
|-------|-------------|
| Nichts | Nur Bild, keine Hilfslinien |
| Minimal | Rohrkreis + aktive Messung |
| Maximal | Rohrkreis + Stundenlinien + alle Messungen + Kamerahoehe-Linie |

3 RadioButtons in der Toolbar: `[Nichts] [Minimal] [Maximal]`

#### 1.5 Foto-Export mit Grafiken

Button `"Screenshot"` in Toolbar:
1. `RenderTargetBitmap` von `OverlayCanvas` (Bild + alle Overlays)
2. Speichern als JPG (95%) in `{Projektordner}/Fotos/{Haltung}_{Meter}m_{Timestamp}.jpg`
3. Pfad im `PhotoMeasurementResult` zurueckgeben
4. Optional: Direkt ins Protokoll als `Foto`-Eintrag verlinken

### Phase 2 — Fehlende Werkzeuge

#### 2.1 Rissbreite (btnMeasureCrack)

Wie Lineal, aber:
- Automatisch senkrecht zur Rissrichtung (wenn moeglich)
- Farbe: Rot (#FF3333)
- Label: `"Rissbreite: 2.3 mm"` statt `"23.5 mm"`
- Bedienung: Identisch zu Lineal (2-Punkt Drag)

#### 2.2 Rohroberflaechen-Distanz (btnMeasurePipe)

Wie Lineal, aber mit **Rohrkruemmungs-Korrektur:**
- Die gemessene Pixeldistanz wird entlang der Rohroberflaeche korrigiert
- Formel: `arcLength = 2 * R * arcsin(chordLength / (2 * R))`
  wobei `R = Rohrdurchmesser/2` und `chordLength` die Sehne ist
- Nur relevant bei Messungen die nicht in der Querschnittsebene liegen
- Farbe: Lime (#00FF00)
- Label: `"Distanz: 45.2 mm (Oberflaeche)"`

#### 2.3 Muffenversatz (btnMeasureJointOffset)

- 2-Punkt: Oberkante alt → Oberkante neu
- Berechnung: Versatz in mm + Versatz in % des DN
- Farbe: HotPink
- Label: `"Versatz: 8.5 mm (2.8 %)"`

#### 2.4 Bild Pan/Zoom/Rotate

3 Modi als ToggleButtons:
- **Pan:** Linke Maus = Bild verschieben (TranslateTransform)
- **Zoom:** Mausrad = Vergroessern/Verkleinern (ScaleTransform)
- **Rotate:** Linke Maus = Bild drehen (RotateTransform)

Implementierung via `TransformGroup` auf dem `PhotoImage`:
```csharp
var transforms = new TransformGroup();
transforms.Children.Add(new ScaleTransform());
transforms.Children.Add(new RotateTransform());
transforms.Children.Add(new TranslateTransform());
PhotoImage.RenderTransform = transforms;
```

Zoom auch ohne Modus aktiv via Mausrad (Ctrl+Scroll).
Doppelklick = Reset auf Originalansicht.

### Phase 3 — 3D-Einblendungen (Spaeter)

Die 3D-Einblendungen (Rohr, Abzweiger, Bogen, Muffenversatz) sind komplex und erfordern
perspektivische Projektion basierend auf FOV. Diese werden in einem separaten Spec behandelt.

**Nicht in diesem Scope:**
- 3D-Rohr-Zylinder
- 3D-Abzweiger
- 3D-Bogen
- 3D-Muffenversatz
- Kamera-Kalibrierungsdatei (Undistort)
- FOV-basierte perspektivische Korrektur

## Integration

### Aufruf aus CodingModeWindow

```csharp
// Video pausieren → Foto aufnehmen → PhotoMeasurementWindow oeffnen
var pngBytes = await CaptureCurrentFrameAsync();
var tmpPath = SaveTempPhoto(pngBytes);
var window = new PhotoMeasurementWindow(tmpPath, _overlayService.Calibration);
window.Owner = this;
if (window.ShowDialog() == true)
{
    // Messwerte in Protokolleintrag uebernehmen
    ApplyMeasurementResult(window.Result);
    // Foto-Pfad dem Eintrag zuordnen
    if (window.Result.ExportedPhotoPath != null)
        AttachPhotoToEntry(window.Result.ExportedPhotoPath);
}
```

### Aufruf standalone

```csharp
// Aus Projekt-Kontext: Foto direkt oeffnen
var window = new PhotoMeasurementWindow(photoFilePath, calibrationFromHaltung);
window.Show(); // non-modal im standalone-Modus
```

### PhotoMeasurementResult (erweitert)

```csharp
public sealed class PhotoMeasurementResult
{
    public string? ExportedPhotoPath { get; set; }     // JPG mit Overlays
    public double? Q1Mm { get; set; }                   // Primaermass
    public double? Q2Mm { get; set; }                   // Sekundaermass
    public double? FillPercent { get; set; }             // Fuellstand %
    public double? DeformPercent { get; set; }           // Deformation %
    public double? CrossSectionPercent { get; set; }     // Querschnitt %
    public double? DnRatioPercent { get; set; }          // Anschluss-DN %
    public double? BendAngleDeg { get; set; }            // Bogenwinkel
    public double? ClockFrom { get; set; }               // Uhrlage von
    public double? ClockTo { get; set; }                 // Uhrlage bis
    public double? JointOffsetMm { get; set; }           // Muffenversatz mm
    public double? JointOffsetPercent { get; set; }      // Muffenversatz %
    public double? CrackWidthMm { get; set; }            // Rissbreite mm
}
```

## Zusammenfassung

| Phase | Aufwand | Was |
|-------|---------|-----|
| 1 | 2-3 Tage | Optik-Upgrade (Linien, Labels, Farben) + Kalibrierungs-Bug + Stundenlinien + Einblendgrad + Foto-Export |
| 2 | 2-3 Tage | Rissbreite + Rohrdistanz + Muffenversatz + Pan/Zoom/Rotate |
| 3 | Spaeter | 3D-Einblendungen (separater Spec) |
