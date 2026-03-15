using System;
using System.Collections.Generic;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Ai;

/// <summary>
/// Overlay-Zeichenwerkzeuge: Wandelt Pixel-Interaktion in VSA-Quantifizierung um.
/// Arbeitet mit normierten Koordinaten (0.0–1.0).
/// Unterstuetzt 2-Punkt-Werkzeuge (Klick+Drag) und Multi-Punkt-Werkzeuge.
/// </summary>
public sealed class OverlayToolService : IOverlayToolService
{
    private OverlayToolType _activeTool;
    private PipeCalibration? _calibration;
    private NormalizedPoint? _drawStart;
    private NormalizedPoint? _drawCurrent;
    private bool _isDrawing;
    private LevelMode _activeLevelMode = LevelMode.Deposit;

    // Multi-Punkt-Zustand
    private readonly List<NormalizedPoint> _multiPoints = new();

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
    }

    public void UpdateDraw(NormalizedPoint currentPoint)
    {
        if (!_isDrawing) return;
        _drawCurrent = currentPoint;
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
                _ => null
            };
        }
    }

    // --- Multi-Punkt-Werkzeuge ---

    public bool IsMultiPointTool => _activeTool is OverlayToolType.PipeBend
                                                 or OverlayToolType.LateralCircle;

    public int RequiredPointCount => _activeTool switch
    {
        OverlayToolType.PipeBend => 4,
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

    private OverlayGeometry BuildLineGeometry(NormalizedPoint start, NormalizedPoint end)
    {
        double dx = end.X - start.X;
        double dy = end.Y - start.Y;
        double lengthNorm = Math.Sqrt(dx * dx + dy * dy);

        var geo = new OverlayGeometry
        {
            ToolType = OverlayToolType.Line,
            Points = new List<NormalizedPoint> { start, end }
        };

        geo.ClockFrom = PointToClockHour(start);
        geo.ClockTo = PointToClockHour(end);
        geo.Q1Mm = NormLengthToMm(lengthNorm);

        return geo;
    }

    /// <summary>
    /// Normierte Laenge (0.0–1.0) in mm umrechnen.
    /// Kalibriert: ueber DN und Referenzlinie. Sonst: Fallback DN300.
    /// </summary>
    private double NormLengthToMm(double normLength)
    {
        if (_calibration != null)
            return _calibration.NormToMm(normLength);
        return normLength * 500; // Fallback ~DN300
    }

    private OverlayGeometry BuildArcGeometry(NormalizedPoint start, NormalizedPoint end)
    {
        var geo = new OverlayGeometry
        {
            ToolType = OverlayToolType.Arc,
            Points = new List<NormalizedPoint> { start, end }
        };

        double clockFrom = PointToClockHour(start);
        double clockTo = PointToClockHour(end);
        geo.ClockFrom = clockFrom;
        geo.ClockTo = clockTo;

        // Bogenwinkel berechnen (im Uhrzeigersinn von → bis)
        double fromDeg = clockFrom * 30.0;
        double toDeg = clockTo * 30.0;
        double arc = toDeg - fromDeg;
        if (arc < 0) arc += 360;
        geo.ArcDegrees = arc;

        return geo;
    }

    private OverlayGeometry BuildRectangleGeometry(NormalizedPoint start, NormalizedPoint end)
    {
        var geo = new OverlayGeometry
        {
            ToolType = OverlayToolType.Rectangle,
            Points = new List<NormalizedPoint>
            {
                start,
                new(end.X, start.Y),
                end,
                new(start.X, end.Y)
            }
        };

        double widthNorm = Math.Abs(end.X - start.X);
        double heightNorm = Math.Abs(end.Y - start.Y);

        geo.Q1Mm = NormLengthToMm(heightNorm);  // Hoehe
        geo.Q2Mm = NormLengthToMm(widthNorm);   // Breite

        var center = new NormalizedPoint((start.X + end.X) / 2, (start.Y + end.Y) / 2);
        geo.ClockFrom = PointToClockHour(center);

        return geo;
    }

    private OverlayGeometry BuildPointGeometry(NormalizedPoint point)
    {
        var geo = new OverlayGeometry
        {
            ToolType = OverlayToolType.Point,
            Points = new List<NormalizedPoint> { point }
        };

        geo.ClockFrom = PointToClockHour(point);
        return geo;
    }

    private OverlayGeometry BuildStretchGeometry(NormalizedPoint start, NormalizedPoint end)
    {
        var geo = new OverlayGeometry
        {
            ToolType = OverlayToolType.Stretch,
            Points = new List<NormalizedPoint> { start, end }
        };

        geo.ClockFrom = PointToClockHour(start);
        geo.ClockTo = PointToClockHour(end);

        return geo;
    }

    /// <summary>
    /// Lineal: Wie Linie aber mit ToolType=Ruler (fuer Tick-Mark-Rendering).
    /// </summary>
    private OverlayGeometry BuildRulerGeometry(NormalizedPoint start, NormalizedPoint end)
    {
        double dx = end.X - start.X, dy = end.Y - start.Y;
        double lengthNorm = Math.Sqrt(dx * dx + dy * dy);

        var geo = new OverlayGeometry
        {
            ToolType = OverlayToolType.Ruler,
            Points = new List<NormalizedPoint> { start, end },
            Q1Mm = NormLengthToMm(lengthNorm),
            ClockFrom = PointToClockHour(start),
            ClockTo = PointToClockHour(end)
        };

        return geo;
    }

    // --- Neue Werkzeuge ---

    /// <summary>
    /// Level-Werkzeug: Horizontale Linie → Kreissegment-Prozentsatz.
    /// User zieht Linie auf Hoehe der Ablagerung/Wasseroberflaeche.
    /// </summary>
    private OverlayGeometry BuildLevelGeometry(NormalizedPoint start, NormalizedPoint end)
    {
        // Horizontale Linie: Y = Mittelwert der beiden Punkte
        double levelY = (start.Y + end.Y) / 2.0;

        // Rohr-Geometrie aus Kalibrierung oder Fallback
        double pipeRadius = (_calibration?.NormalizedDiameter ?? 0.7) / 2.0;
        double pipeCenterY = _calibration?.PipeCenter.Y ?? 0.5;
        double sohle = pipeCenterY + pipeRadius;   // 6 Uhr (unten)
        double scheitel = pipeCenterY - pipeRadius; // 12 Uhr (oben)

        double hRatio;
        if (_activeLevelMode == LevelMode.Obstacle)
        {
            // Hindernis: von Scheitel (oben) nach unten messen
            double h = levelY - scheitel;
            hRatio = Math.Clamp(h / (pipeRadius * 2.0), 0, 1);
        }
        else
        {
            // Ablagerung/Wasser: von Sohle (unten) nach oben messen
            double h = sohle - levelY;
            hRatio = Math.Clamp(h / (pipeRadius * 2.0), 0, 1);
        }

        double fillPercent = CircleSegmentPercent(hRatio);

        var geo = new OverlayGeometry
        {
            ToolType = OverlayToolType.Level,
            Points = new List<NormalizedPoint>
            {
                new(Math.Min(start.X, end.X), levelY),
                new(Math.Max(start.X, end.X), levelY)
            },
            FillPercent = Math.Round(fillPercent, 1),
            LevelSubMode = _activeLevelMode,
            ClockFrom = PointToClockHour(new NormalizedPoint(0.5, levelY))
        };

        return geo;
    }

    /// <summary>
    /// Berechnet den Querschnitts-Prozentsatz eines Kreissegments.
    /// hRatio: Fuellhoehe relativ zum Durchmesser (0.0 = leer, 1.0 = voll).
    /// </summary>
    public static double CircleSegmentPercent(double hRatio)
    {
        hRatio = Math.Clamp(hRatio, 0, 1);
        if (hRatio <= 0) return 0;
        if (hRatio >= 1) return 100;

        // Kreissegment-Formel mit R=0.5, h=hRatio*2R = hRatio
        double R = 0.5;
        double h = hRatio; // 0..1 entspricht 0..2R
        double cosArg = Math.Clamp((R - h) / R, -1, 1);
        double area = R * R * Math.Acos(cosArg) - (R - h) * Math.Sqrt(Math.Max(0, 2 * R * h - h * h));
        double fullArea = Math.PI * R * R;
        return area / fullArea * 100.0;
    }

    /// <summary>
    /// PipeBend: 4 Punkte → Biegewinkel zwischen zwei Rohrachsen.
    /// a1→a2 = Achse vor dem Bogen, b1→b2 = Achse nach dem Bogen.
    /// </summary>
    private OverlayGeometry BuildPipeBendGeometry(
        NormalizedPoint a1, NormalizedPoint a2,
        NormalizedPoint b1, NormalizedPoint b2)
    {
        // Richtungsvektoren
        double vx1 = a2.X - a1.X, vy1 = a2.Y - a1.Y;
        double vx2 = b2.X - b1.X, vy2 = b2.Y - b1.Y;

        // Laengen
        double len1 = Math.Sqrt(vx1 * vx1 + vy1 * vy1);
        double len2 = Math.Sqrt(vx2 * vx2 + vy2 * vy2);

        double angleDeg = 0;
        if (len1 > 1e-8 && len2 > 1e-8)
        {
            double dot = vx1 * vx2 + vy1 * vy2;
            double cosAngle = Math.Clamp(dot / (len1 * len2), -1, 1);
            angleDeg = Math.Acos(cosAngle) * 180.0 / Math.PI;
        }

        var geo = new OverlayGeometry
        {
            ToolType = OverlayToolType.PipeBend,
            Points = new List<NormalizedPoint> { a1, a2, b1, b2 },
            ArcDegrees = Math.Round(angleDeg, 1)
        };

        return geo;
    }

    /// <summary>
    /// LateralCircle: 3 Punkte am Rand → Umkreis (circumscribed circle).
    /// Berechnet Mittelpunkt + Radius → Durchmesser in mm → DnRatioPercent.
    /// </summary>
    private OverlayGeometry BuildLateralCircleGeometry(
        NormalizedPoint p1, NormalizedPoint p2, NormalizedPoint p3)
    {
        // Umkreis aus 3 Punkten
        double ax = p1.X, ay = p1.Y;
        double bx = p2.X, by = p2.Y;
        double cx = p3.X, cy = p3.Y;

        double D = 2.0 * (ax * (by - cy) + bx * (cy - ay) + cx * (ay - by));

        NormalizedPoint center;
        double radiusNorm;

        if (Math.Abs(D) < 1e-10)
        {
            // Punkte sind kollinear — Fallback: Mittelpunkt der aeussersten Punkte
            center = new NormalizedPoint((ax + bx + cx) / 3.0, (ay + by + cy) / 3.0);
            double d1 = Math.Sqrt((bx - ax) * (bx - ax) + (by - ay) * (by - ay));
            double d2 = Math.Sqrt((cx - bx) * (cx - bx) + (cy - by) * (cy - by));
            double d3 = Math.Sqrt((ax - cx) * (ax - cx) + (ay - cy) * (ay - cy));
            radiusNorm = Math.Max(d1, Math.Max(d2, d3)) / 2.0;
        }
        else
        {
            double ux = ((ax * ax + ay * ay) * (by - cy) +
                         (bx * bx + by * by) * (cy - ay) +
                         (cx * cx + cy * cy) * (ay - by)) / D;
            double uy = ((ax * ax + ay * ay) * (cx - bx) +
                         (bx * bx + by * by) * (ax - cx) +
                         (cx * cx + cy * cy) * (bx - ax)) / D;
            center = new NormalizedPoint(ux, uy);
            radiusNorm = Math.Sqrt((ax - ux) * (ax - ux) + (ay - uy) * (ay - uy));
        }

        double diameterMm = NormLengthToMm(radiusNorm * 2);

        double? dnRatio = null;
        if (_calibration?.NominalDiameterMm > 0)
            dnRatio = Math.Round((diameterMm / _calibration.NominalDiameterMm) * 100.0, 1);

        var geo = new OverlayGeometry
        {
            ToolType = OverlayToolType.LateralCircle,
            Points = new List<NormalizedPoint> { p1, p2, p3 },
            Q1Mm = Math.Round(diameterMm, 0),
            DnRatioPercent = dnRatio,
            ClockFrom = PointToClockHour(center)
        };

        return geo;
    }
}
