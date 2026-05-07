# WinCan PhotoAssistant — Werkzeug-Mapping fuer Sewer Studio

Quelle: `C:\Program Files (x86)\CDLAB\Assemblies\Languages\GERMANY_WinCanPhotoAssistant.lng`
Stand: 2026-05-01.

Es wurden nur sichtbare Sprach-/Button-Keys ausgewertet. WinCan-Binaries, Icons
und proprietaerer Code werden NICHT uebernommen.

## Werkzeug-Status

| WinCan-Key | Sewer-Studio-Werkzeug | Status | Code-Referenz |
|---|---|---|---|
| `ToolStrip1.btnImgPan` | Bild verschieben | geplant | — |
| `ToolStrip1.btnImgRot` | Bild drehen | geplant | — |
| `ToolStrip1.btnImgScale` | Bildgroesse veraendern | geplant | — |
| `ToolStrip1.btnImgUndistort` | Bild entzerren | geplant | — |
| `ToolStrip1.btnMeasureCross` | Querschnitt / Flaeche messen | vorhanden | `OverlayToolType.CrossSection` (12), `Rectangle` (3), `Ellipse` (10) |
| `ToolStrip1.btnMeasureDel` | Messlinie loeschen | vorhanden | UI-Delete-Funktion in CodingMode |
| `ToolStrip1.btnMeasurePipe` | Rohroberflaechen-Distanz | implementiert | `OverlayToolType.Stretch` (5) — Geodaesie auf Zylinder noch ergaenzungsfaehig |
| `ToolStrip1.btnMeasureWater` | Wasserstand messen | vorhanden | `OverlayToolType.Level` (9) + `LevelMode.Water` |
| `ToolStrip1.btnScreenShot` | Foto mit Grafiken speichern | vorhanden | als Overlay-Export beim OK in CodingModeWindow |
| `ToolStrip1.btnViewButtons` | Schaltflaechen statt Schieberegler | geplant | — |
| `ToolStrip1.btnViewClockH` | Stundenlinien anzeigen | geplant | — |
| `ToolStrip1.btnViewDown` | Bogen in Fliessrichtung | geplant | erweitert `PipeDirection` (13) |
| `ToolStrip1.btnViewStyle0` | Einblendstil: Nichts | geplant | — |
| `ToolStrip1.btnViewStyle1` | Einblendstil: Minimal | geplant | — |
| `ToolStrip1.btnViewStyle2` | Einblendstil: Maximal | geplant | — |
| `ToolStrip1.btnViewType0` | 3D-Einblendung Rohr | geplant | — |
| `ToolStrip1.btnViewType1` | 3D-Einblendung Abzweiger | geplant | — |
| `ToolStrip1.btnViewType2` | 3D-Einblendung Bogen | vorhanden als 2D-Bogenmessung, 3D geplant | `OverlayToolType.PipeBend` (6) |
| `ToolStrip1.btnViewType3` | 3D-Einblendung Muffenversatz | geplant | — |
| `ToolStrip1.btnViewUp` | Bogen gegen Fliessrichtung | geplant | erweitert `PipeDirection` (13) |
| `ToolStrip1.btnSaveScreen` | Bild durch Bildschirmaufnahme ersetzen | geplant | — |
| `ToolStrip1.btnMeasureDeform` | Deformation messen | vorhanden | `OverlayToolType.LateralCircle` (7), `Ellipse` (10) |
| `ToolStrip1.btnMeasureCrack` | Rissbreite messen | implementiert | `OverlayToolType.Line` (1) — laut Kommentar in `CodingSession.cs:27` explizit fuer Risse |

## Sprach-Keys (Bedeutung in Sewer Studio)

| Sprach-Key | Bedeutung |
|---|---|
| `panelWorkArea.grpStr.strBendAngle` | Bogenwinkel |
| `panelWorkArea.grpStr.strCamHeight` | Kamerahoehe in Prozent des Durchmessers |
| `panelWorkArea.grpStr.strClock` | Uhrzeit / Uhrlage |
| `panelWorkArea.grpStr.strDistCrossSect` | Distanz der Querschnittsebene |
| `panelWorkArea.grpStr.strWaterLevel` | Wasserstand in Prozent des Rohrdurchmessers |
| `panelWorkArea.grpStr.strDistance` | Distanz |
| `panelWorkArea.grpStr.strDeformPercent` | Durchmesserreduktion in Prozent |
| `panelWorkArea.grpStr.strJointOffset` | Muffenversatz / Distanz in Prozent |
| `panelWorkArea.grpStr.strArea` | Bereich |

## Status-Verifikation gegen aktuellen Code

`OverlayToolType` ist definiert in
`src/AuswertungPro.Next.Domain/Models/CodingSession.cs` (Zeile 24).

Vorhandene Tools (Stand Verifikation):

- `Line` (1) — Risse (Laenge/Breite) → deckt **btnMeasureCrack**
- `Arc` (2) — Umfangsschaeden (UhrVon/UhrBis)
- `Rectangle` (3) — Flaechenschaeden
- `Point` (4) — Einzelschaeden
- `Stretch` (5) — Streckenschaeden (MeterStart/MeterEnd) → deckt **btnMeasurePipe**
- `PipeBend` (6) — Biegewinkel (4 Punkte) → deckt 2D-Teil von **btnViewType2**
- `LateralCircle` (7) — Anschlussdurchmesser → deckt **btnMeasureDeform** (DN-Kreis)
- `Ruler` (8) — Lineal mit Skalenteilung
- `Level` (9) — Wasser/Ablagerung/Hindernis (`LevelMode`) → deckt **btnMeasureWater**
- `Ellipse` (10) — Flaechen
- `Freehand` (11) — Polyline aus Mauspfad
- `CrossSection` (12) — Querschnittsverminderung → deckt **btnMeasureCross**
- `PipeDirection` (13) — Bogen-Richtungswechsel (BCC/BAG)
- `RingBBoxes` (14) — Ringriss-BBoxes

Die User-Statusangaben fuer `vorhanden`/`implementiert` decken sich mit der
aktuellen Enum-Struktur. Die `geplant`-Eintraege sind alle View-/Bildmanipulation-
Tools die noch nicht im Code existieren.

## Lizenz/Copyright-Hinweis

WinCan ist Eigentum der CDLAB. Es wurden NUR Beschriftungstexte zur Funktions-
zuordnung uebernommen. Keine Binaries, kein Icon-Material, kein WinCan-Code.
Implementierungen der oben genannten Werkzeuge erfolgen eigenstaendig im
Sewer-Studio-Codebase ohne Wiederverwendung von WinCan-Quellen.
