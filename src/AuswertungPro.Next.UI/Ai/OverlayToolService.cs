using System;
using System.Collections.Generic;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Ai;

/// <summary>
/// Overlay-Zeichenwerkzeuge: Wandelt Pixel-Interaktion in VSA-Quantifizierung um.
/// Arbeitet mit normierten Koordinaten (0.0–1.0).
/// Unterstuetzt 2-Punkt-Werkzeuge (Klick+Drag) und Multi-Punkt-Werkzeuge (Winkelmesser: 3 Klicks).
/// </summary>
public sealed class OverlayToolService : IOverlayToolService
{
    private OverlayToolType _activeTool;
    private PipeCalibration? _calibration;
    private NormalizedPoint? _drawStart;
    private NormalizedPoint? _drawCurrent;
    private bool _isDrawing;

    // Multi-Punkt-Zustand (fuer Winkelmesser)
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
                OverlayToolType.Protractor when _multiPoints.Count >= 3
                    => BuildProtractorGeometry(_multiPoints[0], _multiPoints[1], _multiPoints[2]),
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
            OverlayToolType.DnCircle => BuildDnCircleGeometry(_drawStart, _drawCurrent),
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
                OverlayToolType.DnCircle => BuildDnCircleGeometry(_drawStart, _drawCurrent),
                OverlayToolType.Ruler => BuildRulerGeometry(_drawStart, _drawCurrent),
                _ => null
            };
        }
    }

    // --- Multi-Punkt-Werkzeuge ---

    public bool IsMultiPointTool => _activeTool == OverlayToolType.Protractor;

    public int RequiredPointCount => _activeTool switch
    {
        OverlayToolType.Protractor => 3,
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
        if (_activeTool != OverlayToolType.Protractor || _drawCurrent == null)
            return null;

        if (_multiPoints.Count == 1)
        {
            // 1 Punkt gesetzt: Linie P1 → Cursor
            return new OverlayGeometry
            {
                ToolType = OverlayToolType.Protractor,
                Points = new List<NormalizedPoint> { _multiPoints[0], _drawCurrent }
            };
        }

        if (_multiPoints.Count >= 2)
        {
            // 2 Punkte gesetzt: Zwei Linien + dynamischer Winkel
            var p1 = _multiPoints[0];
            var vertex = _multiPoints[1];
            var p3 = _drawCurrent;
            return BuildProtractorGeometry(p1, vertex, p3);
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

        // Q1 = Laenge in mm (kalibriert) oder Pixel-Anteil als Fallback
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

        // Uhrposition am Mittelpunkt
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
        // Strecke: horizontale Linie → Meter-Bereich
        var geo = new OverlayGeometry
        {
            ToolType = OverlayToolType.Stretch,
            Points = new List<NormalizedPoint> { start, end }
        };

        // Uhrposition am Startpunkt
        geo.ClockFrom = PointToClockHour(start);
        geo.ClockTo = PointToClockHour(end);

        return geo;
    }

    // --- Neue Werkzeuge ---

    /// <summary>
    /// Winkelmesser: Berechnet den Winkel am Scheitelpunkt (vertex) zwischen den Armen P1→vertex und vertex→P3.
    /// </summary>
    private OverlayGeometry BuildProtractorGeometry(NormalizedPoint p1, NormalizedPoint vertex, NormalizedPoint p3)
    {
        // Vektoren vom Scheitelpunkt zu den Endpunkten
        double dx1 = p1.X - vertex.X, dy1 = p1.Y - vertex.Y;
        double dx2 = p3.X - vertex.X, dy2 = p3.Y - vertex.Y;

        // Winkel via atan2
        double angle1 = Math.Atan2(dy1, dx1);
        double angle2 = Math.Atan2(dy2, dx2);
        double angleDiff = Math.Abs(angle2 - angle1) * 180.0 / Math.PI;
        if (angleDiff > 180) angleDiff = 360 - angleDiff;

        var geo = new OverlayGeometry
        {
            ToolType = OverlayToolType.Protractor,
            Points = new List<NormalizedPoint> { p1, vertex, p3 },
            ArcDegrees = angleDiff,
            ClockFrom = PointToClockHour(vertex)
        };

        return geo;
    }

    /// <summary>
    /// DN-Kreis: Misst den Durchmesser eines Anschlusses.
    /// center = Mitte der Anschlussoeffnung, edge = Rand der Oeffnung.
    /// </summary>
    private OverlayGeometry BuildDnCircleGeometry(NormalizedPoint center, NormalizedPoint edge)
    {
        double dx = edge.X - center.X, dy = edge.Y - center.Y;
        double radiusNorm = Math.Sqrt(dx * dx + dy * dy);
        double diameterMm = NormLengthToMm(radiusNorm * 2);

        double? dnRatio = null;
        if (_calibration?.NominalDiameterMm > 0)
            dnRatio = (diameterMm / _calibration.NominalDiameterMm) * 100.0;

        var geo = new OverlayGeometry
        {
            ToolType = OverlayToolType.DnCircle,
            Points = new List<NormalizedPoint> { center, edge },
            Q1Mm = diameterMm,
            DnRatioPercent = dnRatio,
            ClockFrom = PointToClockHour(center)
        };

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
}
