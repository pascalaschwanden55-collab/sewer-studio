using System;
using System.Collections.Generic;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.Ai;

/// <summary>
/// Overlay-Zeichenwerkzeuge: Wandelt Pixel-Interaktion in VSA-Quantifizierung um.
/// Arbeitet mit normierten Koordinaten (0.0–1.0).
/// Unterstuetzt 2-Punkt-Werkzeuge (Klick+Drag) und Multi-Punkt-Werkzeuge.
/// </summary>
public sealed partial class OverlayToolService : IOverlayToolService
{
    private OverlayToolType _activeTool;
    private PipeCalibration? _calibration;
    private NormalizedPoint? _drawStart;
    private NormalizedPoint? _drawCurrent;
    private bool _isDrawing;
    private LevelMode _activeLevelMode = LevelMode.Deposit;
    private bool _pipeBendSnapEnabled;

    // Multi-Punkt-Zustand
    private readonly List<NormalizedPoint> _multiPoints = new();

    // PipeBend Schema-Zustand (interaktives Bogen-Overlay)
    private PipeBendPhase _pipeBendPhase = PipeBendPhase.None;
    private NormalizedPoint _bendCenter = new(0.5, 0.5);
    private double _bendAngleDeg = 45;
    private double _bendRotationDeg; // 0 = nach oben
    private double _bendRadius = 0.15; // normalisiert, ~15% des Frames
    private string? _bendDragHandle; // "vertex", "arm1", "arm2", "radius"

    // Freihand-Zustand: sammelt Punkte waehrend MouseMove
    private readonly List<NormalizedPoint> _freehandPoints = new();
    private const double FreehandMinDistance = 0.005; // Mindestabstand fuer Punkt-Dezimierung

    // --- Werkzeug-Auswahl ---

    public OverlayToolType ActiveTool
    {
        get => _activeTool;
        set
        {
            if (_isDrawing) CancelDraw();
            _activeTool = value;
            ToolChanged?.Invoke(this, value);
        }
    }

    /// <summary>Sub-Modus fuer das Level-Werkzeug.</summary>
    public LevelMode ActiveLevelMode
    {
        get => _activeLevelMode;
        set => _activeLevelMode = value;
    }

    /// <summary>Snappt PipeBend auf Standardwinkel 15/30/45/90.</summary>
    public bool PipeBendSnapEnabled
    {
        get => _pipeBendSnapEnabled;
        set => _pipeBendSnapEnabled = value;
    }

    public event EventHandler<OverlayToolType>? ToolChanged;

    // --- Kalibrierung ---

    public void SetCalibration(PipeCalibration calibration)
    {
        _calibration = calibration ?? throw new ArgumentNullException(nameof(calibration));
    }

    public PipeCalibration? Calibration => _calibration;
    public bool IsCalibrated => _calibration != null && _calibration.IsCalibrated;

    // --- Zeichenoperationen (2-Punkt: Klick+Drag) ---

    public void BeginDraw(NormalizedPoint startPoint)
    {
        if (_activeTool == OverlayToolType.None) return;
        _drawStart = startPoint;
        _drawCurrent = startPoint;
        _isDrawing = true;

        // Freihand: Punktsammlung starten
        if (_activeTool == OverlayToolType.Freehand)
        {
            _freehandPoints.Clear();
            _freehandPoints.Add(startPoint);
        }
    }

    public void UpdateDraw(NormalizedPoint currentPoint)
    {
        if (!_isDrawing) return;
        _drawCurrent = currentPoint;

        // Freihand: Punkt hinzufuegen wenn Mindestabstand ueberschritten
        if (_activeTool == OverlayToolType.Freehand && _freehandPoints.Count > 0)
        {
            var last = _freehandPoints[^1];
            double dist = Math.Sqrt(DistanceSquared(last, currentPoint));
            if (dist >= FreehandMinDistance)
                _freehandPoints.Add(currentPoint);
        }
    }

    public OverlayGeometry? EndDraw()
    {
        // Multi-Punkt-Werkzeug: aus _multiPoints bauen
        if (IsMultiPointTool && _multiPoints.Count >= RequiredPointCount)
        {
            var geometry = _activeTool switch
            {
                OverlayToolType.PipeBend when _multiPoints.Count >= 4
                    => BuildPipeBendGeometry(_multiPoints[0], _multiPoints[1],
                                             _multiPoints[2], _multiPoints[3]),
                OverlayToolType.LateralCircle when _multiPoints.Count >= 3
                    => BuildLateralCircleGeometry(_multiPoints[0], _multiPoints[1], _multiPoints[2]),
                _ => null
            };

            _multiPoints.Clear();
            _isDrawing = false;
            _drawStart = null;
            _drawCurrent = null;
            return geometry;
        }

        // Standard 2-Punkt-Werkzeug
        if (!_isDrawing || _drawStart == null || _drawCurrent == null)
        {
            CancelDraw();
            return null;
        }

        var geo = _activeTool switch
        {
            OverlayToolType.Line => BuildLineGeometry(_drawStart, _drawCurrent),
            OverlayToolType.Arc => BuildArcGeometry(_drawStart, _drawCurrent),
            OverlayToolType.Rectangle => BuildRectangleGeometry(_drawStart, _drawCurrent),
            OverlayToolType.Point => BuildPointGeometry(_drawStart),
            OverlayToolType.Stretch => BuildStretchGeometry(_drawStart, _drawCurrent),
            OverlayToolType.Level => BuildLevelGeometry(_drawStart, _drawCurrent),
            OverlayToolType.Ruler => BuildRulerGeometry(_drawStart, _drawCurrent),
            OverlayToolType.Ellipse => BuildEllipseGeometry(_drawStart, _drawCurrent),
            OverlayToolType.Freehand => BuildFreehandGeometry(),
            _ => null
        };

        _isDrawing = false;
        _drawStart = null;
        _drawCurrent = null;
        return geo;
    }

    public void CancelDraw()
    {
        _isDrawing = false;
        _drawStart = null;
        _drawCurrent = null;
        _multiPoints.Clear();
        _freehandPoints.Clear();
        CancelPipeBendSchema();
    }

    public bool IsDrawing => _isDrawing;
    public NormalizedPoint? DrawStartPoint => _drawStart;
    public NormalizedPoint? DrawCurrentPoint => _drawCurrent;

    public OverlayGeometry? PreviewGeometry
    {
        get
        {
            // Multi-Punkt-Vorschau
            if (IsMultiPointTool && _multiPoints.Count > 0 && _drawCurrent != null)
            {
                return BuildMultiPointPreview();
            }

            if (!_isDrawing || _drawStart == null || _drawCurrent == null) return null;
            return _activeTool switch
            {
                OverlayToolType.Line => BuildLineGeometry(_drawStart, _drawCurrent),
                OverlayToolType.Arc => BuildArcGeometry(_drawStart, _drawCurrent),
                OverlayToolType.Rectangle => BuildRectangleGeometry(_drawStart, _drawCurrent),
                OverlayToolType.Point => BuildPointGeometry(_drawStart),
                OverlayToolType.Stretch => BuildStretchGeometry(_drawStart, _drawCurrent),
                OverlayToolType.Level => BuildLevelGeometry(_drawStart, _drawCurrent),
                OverlayToolType.Ruler => BuildRulerGeometry(_drawStart, _drawCurrent),
                OverlayToolType.Ellipse => BuildEllipseGeometry(_drawStart, _drawCurrent),
                OverlayToolType.Freehand => BuildFreehandPreview(),
                _ => null
            };
        }
    }

    // --- Multi-Punkt-Werkzeuge ---

    // PipeBend ist kein Multi-Point-Tool mehr (jetzt Schema-Overlay)
    public bool IsMultiPointTool => _activeTool is OverlayToolType.LateralCircle;

    public int RequiredPointCount => _activeTool switch
    {
        OverlayToolType.LateralCircle => 3,
        _ => 2
    };

    public int DrawPointCount => _multiPoints.Count;

    public IReadOnlyList<NormalizedPoint> DrawPoints => _multiPoints.AsReadOnly();

    public bool AddDrawPoint(NormalizedPoint point)
    {
        if (_activeTool == OverlayToolType.None) return false;
        _multiPoints.Add(point);
        _isDrawing = true;
        _drawStart = _multiPoints[0];
        _drawCurrent = point;
        return _multiPoints.Count >= RequiredPointCount;
    }

    /// <summary>
    /// Multi-Punkt-Vorschau: Zeigt Teilgeometrie waehrend der Klick-Sequenz.
    /// </summary>
    private OverlayGeometry? BuildMultiPointPreview()
    {
        if (_drawCurrent == null) return null;

        if (_activeTool == OverlayToolType.PipeBend)
            return BuildPipeBendPreview();

        if (_activeTool == OverlayToolType.LateralCircle)
            return BuildLateralCirclePreview();

        return null;
    }

    /// <summary>PipeBend: 4-Schritt Preview (Achse1 aufbauen → Achse2 aufbauen → Winkelbogen).</summary>
    private OverlayGeometry? BuildPipeBendPreview()
    {
        if (_multiPoints.Count == 1)
        {
            // Achse 1: Linie P1→Cursor
            return new OverlayGeometry
            {
                ToolType = OverlayToolType.PipeBend,
                Points = new List<NormalizedPoint> { _multiPoints[0], _drawCurrent! }
            };
        }

        if (_multiPoints.Count == 2)
        {
            // Achse 1 fertig, warte auf Achse 2 Startpunkt
            return new OverlayGeometry
            {
                ToolType = OverlayToolType.PipeBend,
                Points = new List<NormalizedPoint> { _multiPoints[0], _multiPoints[1], _drawCurrent! }
            };
        }

        if (_multiPoints.Count >= 3)
        {
            // Direkt nach Klick 3 ist _drawCurrent == Punkt 3: noch keine 2. Achse, daher nur Marker.
            if (DistanceSquared(_multiPoints[2], _drawCurrent!) < 1e-12)
            {
                return new OverlayGeometry
                {
                    ToolType = OverlayToolType.PipeBend,
                    Points = new List<NormalizedPoint> { _multiPoints[0], _multiPoints[1], _multiPoints[2] }
                };
            }

            // Achse 1 + Achse 2 Preview → Winkel berechnen
            return BuildPipeBendGeometry(
                _multiPoints[0], _multiPoints[1], _multiPoints[2], _drawCurrent!);
        }

        return null;
    }

    /// <summary>LateralCircle: 3-Schritt Preview (Punkte aufbauen → Umkreis).</summary>
    private OverlayGeometry? BuildLateralCirclePreview()
    {
        if (_multiPoints.Count == 1)
        {
            // Zwei Punkte: Linie P1→Cursor
            return new OverlayGeometry
            {
                ToolType = OverlayToolType.LateralCircle,
                Points = new List<NormalizedPoint> { _multiPoints[0], _drawCurrent! }
            };
        }

        if (_multiPoints.Count >= 2)
        {
            // Direkt nach Klick 2 ist _drawCurrent == Punkt 2: noch kein 3. Punkt.
            if (DistanceSquared(_multiPoints[1], _drawCurrent!) < 1e-12)
            {
                return new OverlayGeometry
                {
                    ToolType = OverlayToolType.LateralCircle,
                    Points = new List<NormalizedPoint> { _multiPoints[0], _multiPoints[1] }
                };
            }

            // Drei Punkte: Umkreis berechnen
            return BuildLateralCircleGeometry(_multiPoints[0], _multiPoints[1], _drawCurrent!);
        }

        return null;
    }

    // --- Berechnungen ---

    public double PixelToMm(double normalizedPixels, double frameWidthPx)
    {
        if (_calibration == null) return 0;
        return _calibration.PixelToMm(normalizedPixels, frameWidthPx);
    }

    public double PointToClockHour(NormalizedPoint point)
    {
        if (_calibration == null)
        {
            // Fallback: Bildmitte als Rohrmitte annehmen
            var fallback = new PipeCalibration { NominalDiameterMm = 300 };
            return fallback.PointToClockHour(point);
        }
        return _calibration.PointToClockHour(point);
    }

    // --- Geometrie-Builder ---


    /// <summary>
    /// Freihand-Zeichnung: Baut Geometrie aus gesammelten Mauspfad-Punkten.
    /// Berechnet BoundingBox fuer YOLO-Export.
    /// </summary>
    private OverlayGeometry BuildFreehandGeometry()
    {
        if (_freehandPoints.Count < 2)
        {
            _freehandPoints.Clear();
            return null!;
        }

        // Punkte kopieren und BoundingBox berechnen
        double minX = double.MaxValue, minY = double.MaxValue;
        double maxX = double.MinValue, maxY = double.MinValue;

        var points = new List<NormalizedPoint>(_freehandPoints.Count + 1);
        foreach (var p in _freehandPoints)
        {
            points.Add(new NormalizedPoint(p.X, p.Y));
            if (p.X < minX) minX = p.X;
            if (p.Y < minY) minY = p.Y;
            if (p.X > maxX) maxX = p.X;
            if (p.Y > maxY) maxY = p.Y;
        }

        // Pfad schliessen: letzten Punkt mit erstem verbinden
        if (points.Count >= 3)
            points.Add(new NormalizedPoint(points[0].X, points[0].Y));

        double widthNorm = maxX - minX;
        double heightNorm = maxY - minY;
        var center = new NormalizedPoint((minX + maxX) / 2, (minY + maxY) / 2);

        var geo = new OverlayGeometry
        {
            ToolType = OverlayToolType.Freehand,
            Points = points,
            Q1Mm = NormLengthToMm(heightNorm),   // BoundingBox-Hoehe
            Q2Mm = NormLengthToMm(widthNorm),     // BoundingBox-Breite
            ClockFrom = PointToClockHour(center)
        };

        _freehandPoints.Clear();
        return geo;
    }

    /// <summary>
    /// Freihand-Vorschau: Zeigt aktuelle Polyline waehrend des Zeichnens.
    /// </summary>
    private OverlayGeometry? BuildFreehandPreview()
    {
        if (_freehandPoints.Count < 2) return null;

        var points = new List<NormalizedPoint>(_freehandPoints.Count);
        foreach (var p in _freehandPoints)
            points.Add(new NormalizedPoint(p.X, p.Y));

        return new OverlayGeometry
        {
            ToolType = OverlayToolType.Freehand,
            Points = points
        };
    }

    // ═══════════════════════════════════════════════
    // PhotoAssistant: Slider-basierte Messung
    // ═══════════════════════════════════════════════

    /// <summary>
    /// Umkehrfunktion von CircleSegmentPercent: Findet hRatio fuer gewuenschten %-Wert.
    /// Bisektions-Suche (50 Iterationen, Genauigkeit ~1e-15).
    /// </summary>
    public static double InverseCircleSegmentPercent(double targetPercent)
    {
        targetPercent = Math.Clamp(targetPercent, 0, 100);
        if (targetPercent <= 0) return 0;
        if (targetPercent >= 100) return 1;

        double lo = 0, hi = 1;
        for (int i = 0; i < 50; i++)
        {
            double mid = (lo + hi) / 2.0;
            double pct = CircleSegmentPercent(mid);
            if (Math.Abs(pct - targetPercent) < 1e-6)
                return mid; // Fruehes Abbrechen bei ausreichender Genauigkeit
            if (pct < targetPercent)
                lo = mid;
            else
                hi = mid;
        }
        return (lo + hi) / 2.0;
    }

    /// <summary>
    /// Level-Geometrie aus Slider-Wert (0–100%) berechnen.
    /// Slider → hRatio → Y-Koordinate → OverlayGeometry.
    /// </summary>
    public OverlayGeometry? BuildLevelGeometryFromSlider(double fillPercent, LevelMode mode)
    {
        fillPercent = Math.Clamp(fillPercent, 0, 100);

        double hRatio = InverseCircleSegmentPercent(fillPercent);
        double pipeRadius = (_calibration?.NormalizedDiameter ?? 0.7) / 2.0;
        double pipeCenterY = _calibration?.PipeCenter.Y ?? 0.5;
        double pipeCenterX = _calibration?.PipeCenter.X ?? 0.5;
        double sohle = pipeCenterY + pipeRadius;
        double scheitel = pipeCenterY - pipeRadius;

        double levelY;
        if (mode == LevelMode.Obstacle)
        {
            // Von Scheitel (oben) nach unten
            levelY = scheitel + hRatio * (pipeRadius * 2.0);
        }
        else
        {
            // Von Sohle (unten) nach oben
            levelY = sohle - hRatio * (pipeRadius * 2.0);
        }

        // Horizontale Punkte am Rohrrand
        double leftX = pipeCenterX - pipeRadius;
        double rightX = pipeCenterX + pipeRadius;

        return new OverlayGeometry
        {
            ToolType = OverlayToolType.Level,
            Points = new List<NormalizedPoint>
            {
                new(leftX, levelY),
                new(rightX, levelY)
            },
            FillPercent = Math.Round(fillPercent, 1),
            LevelSubMode = mode,
            ClockFrom = _calibration?.PointToClockHour(new NormalizedPoint(0.5, levelY))
        };
    }

    /// <summary>Rohrkreis-Groesse aendern (Mausrad im PhotoAssistant).</summary>
    public void ResizePipeCircle(double deltaNormalized)
    {
        if (_calibration == null)
        {
            _calibration = new PipeCalibration
            {
                NominalDiameterMm = 300,
                NormalizedDiameter = 0.7,
                PipeCenter = new NormalizedPoint(0.5, 0.5)
            };
        }

        _calibration.NormalizedDiameter = Math.Clamp(
            _calibration.NormalizedDiameter + deltaNormalized, 0.1, 0.95);
    }

    /// <summary>Rohrkreis-Position verschieben (Drag im PhotoAssistant).</summary>
    public void MovePipeCircle(NormalizedPoint newCenter)
    {
        if (_calibration == null)
        {
            _calibration = new PipeCalibration
            {
                NominalDiameterMm = 300,
                NormalizedDiameter = 0.7,
                PipeCenter = new NormalizedPoint(0.5, 0.5)
            };
        }

        _calibration.PipeCenter = new NormalizedPoint(
            Math.Clamp(newCenter.X, 0.05, 0.95),
            Math.Clamp(newCenter.Y, 0.05, 0.95));
    }

    // ═══════════════════════════════════════════════
    // PipeBend Schema-Overlay (WinCan-Stil)
    // ═══════════════════════════════════════════════

    public PipeBendPhase BendPhase => _pipeBendPhase;
    public NormalizedPoint BendCenter => _bendCenter;
    public double BendAngleDeg => _bendAngleDeg;
    public double BendRotationDeg => _bendRotationDeg;
    public double BendRadius => _bendRadius;

    /// <summary>Erster Klick: Schema am Klickpunkt platzieren (45°, nach oben).</summary>
    public void PlacePipeBendSchema(NormalizedPoint center)
    {
        _bendCenter = center;
        _bendAngleDeg = 45;
        _bendRotationDeg = 0; // 0 = Scheitel nach oben
        _bendRadius = 0.15;
        _pipeBendPhase = PipeBendPhase.Adjusting;
    }

    /// <summary>Handle-Drag starten. handleId: "vertex", "arm1", "arm2", "radius".</summary>
    public void BeginBendDrag(string handleId)
    {
        if (_pipeBendPhase != PipeBendPhase.Adjusting) return;
        _bendDragHandle = handleId;
    }

    /// <summary>Handle-Drag aktualisieren. Berechnet Winkel/Position/Radius neu.</summary>
    public void UpdateBendDrag(NormalizedPoint mousePos)
    {
        if (_pipeBendPhase != PipeBendPhase.Adjusting || _bendDragHandle == null) return;

        switch (_bendDragHandle)
        {
            case "vertex":
                // Gesamtes Schema verschieben
                _bendCenter = mousePos;
                break;

            case "arm1":
            {
                // Winkel + Rotation anpassen: Arm1-Ende zeigt auf Mausposition
                double dx = mousePos.X - _bendCenter.X;
                double dy = mousePos.Y - _bendCenter.Y;
                double mouseAngle = Math.Atan2(dy, dx) * 180.0 / Math.PI;
                // arm1 = rotation - angle/2 → rotation = mouseAngle + angle/2
                _bendRotationDeg = mouseAngle + _bendAngleDeg / 2.0;
                // Radius optional anpassen
                double dist = Math.Sqrt(dx * dx + dy * dy);
                if (dist > 0.02) _bendRadius = dist;
                break;
            }

            case "arm2":
            {
                // Winkel + Rotation anpassen: Arm2-Ende zeigt auf Mausposition
                double dx = mousePos.X - _bendCenter.X;
                double dy = mousePos.Y - _bendCenter.Y;
                double mouseAngle = Math.Atan2(dy, dx) * 180.0 / Math.PI;
                // arm2 = rotation + angle/2 → rotation = mouseAngle - angle/2
                _bendRotationDeg = mouseAngle - _bendAngleDeg / 2.0;
                double dist = Math.Sqrt(dx * dx + dy * dy);
                if (dist > 0.02) _bendRadius = dist;
                break;
            }

            case "angle":
            {
                // Nur Winkel aendern: Abstand Maus↔Vertex → Winkel
                double dx = mousePos.X - _bendCenter.X;
                double dy = mousePos.Y - _bendCenter.Y;
                double mouseAngle = Math.Atan2(dy, dx) * 180.0 / Math.PI;
                double delta = mouseAngle - _bendRotationDeg;
                // Normalisiere auf -180..180
                while (delta > 180) delta -= 360;
                while (delta < -180) delta += 360;
                _bendAngleDeg = Math.Clamp(Math.Abs(delta) * 2.0, 5, 175);
                break;
            }

            case "radius":
            {
                // Nur Radius aendern
                double dx = mousePos.X - _bendCenter.X;
                double dy = mousePos.Y - _bendCenter.Y;
                _bendRadius = Math.Clamp(Math.Sqrt(dx * dx + dy * dy), 0.03, 0.45);
                break;
            }
        }
    }

    /// <summary>Handle-Drag beenden.</summary>
    public void EndBendDrag()
    {
        _bendDragHandle = null;
    }

    /// <summary>Schema bestaetigen (Doppelklick/Enter) → OverlayGeometry zurueckgeben.</summary>
    public OverlayGeometry? ConfirmPipeBendSchema()
    {
        if (_pipeBendPhase != PipeBendPhase.Adjusting) return null;
        _pipeBendPhase = PipeBendPhase.Confirmed;

        double finalAngle = _pipeBendSnapEnabled
            ? SnapPipeBendAngle(_bendAngleDeg)
            : Math.Round(_bendAngleDeg, 1);

        // Schema-Punkte: [vertex, arm1End, arm2End]
        var (arm1, arm2) = GetBendArmEndpoints();

        var geo = new OverlayGeometry
        {
            ToolType = OverlayToolType.PipeBend,
            Points = new List<NormalizedPoint> { _bendCenter, arm1, arm2 },
            ArcDegrees = finalAngle
        };

        return geo;
    }

    /// <summary>Schema abbrechen.</summary>
    public void CancelPipeBendSchema()
    {
        _pipeBendPhase = PipeBendPhase.None;
        _bendDragHandle = null;
    }

    /// <summary>Schema zuruecksetzen (R-Taste): 45°, 0° Rotation.</summary>
    public void ResetPipeBendSchema()
    {
        if (_pipeBendPhase != PipeBendPhase.Adjusting) return;
        _bendAngleDeg = 45;
        _bendRotationDeg = 0;
        _bendRadius = 0.15;
    }

    /// <summary>Winkel um delta Grad aendern (+/- Tasten).</summary>
    public void AdjustBendAngle(double deltaDeg)
    {
        if (_pipeBendPhase != PipeBendPhase.Adjusting) return;
        _bendAngleDeg = Math.Clamp(_bendAngleDeg + deltaDeg, 5, 175);
    }

    /// <summary>Auf naechsten Standardwinkel snappen.</summary>
    public void SnapBendAngle()
    {
        if (_pipeBendPhase != PipeBendPhase.Adjusting) return;
        _bendAngleDeg = SnapPipeBendAngle(_bendAngleDeg);
    }

    /// <summary>Aktuelle Schema-Geometrie fuer Live-Rendering (Vorschau).</summary>
    public OverlayGeometry? GetBendSchemaPreview()
    {
        if (_pipeBendPhase != PipeBendPhase.Adjusting) return null;

        var (arm1, arm2) = GetBendArmEndpoints();
        return new OverlayGeometry
        {
            ToolType = OverlayToolType.PipeBend,
            Points = new List<NormalizedPoint> { _bendCenter, arm1, arm2 },
            ArcDegrees = Math.Round(_bendAngleDeg, 1)
        };
    }

    /// <summary>Berechnet die Endpunkte der beiden Schenkel.</summary>
    public (NormalizedPoint arm1, NormalizedPoint arm2) GetBendArmEndpoints()
    {
        double rad1 = (_bendRotationDeg - _bendAngleDeg / 2.0) * Math.PI / 180.0;
        double rad2 = (_bendRotationDeg + _bendAngleDeg / 2.0) * Math.PI / 180.0;
        var arm1 = new NormalizedPoint(
            _bendCenter.X + Math.Cos(rad1) * _bendRadius,
            _bendCenter.Y + Math.Sin(rad1) * _bendRadius);
        var arm2 = new NormalizedPoint(
            _bendCenter.X + Math.Cos(rad2) * _bendRadius,
            _bendCenter.Y + Math.Sin(rad2) * _bendRadius);
        return (arm1, arm2);
    }

    /// <summary>Berechnet die Position des Radius-Handles (Mitte zwischen den Armen).</summary>
    public NormalizedPoint GetBendRadiusHandle()
    {
        double radMid = _bendRotationDeg * Math.PI / 180.0;
        return new NormalizedPoint(
            _bendCenter.X + Math.Cos(radMid) * _bendRadius,
            _bendCenter.Y + Math.Sin(radMid) * _bendRadius);
    }
}

/// <summary>Zustaende des PipeBend-Schema-Overlays.</summary>
public enum PipeBendPhase
{
    None,       // Kein Schema aktiv
    Adjusting,  // Schema platziert, Handles aktiv
    Confirmed   // Bestaetigt, Winkel uebernommen
}
