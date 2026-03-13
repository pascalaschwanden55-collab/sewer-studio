using System;
using System.Collections.Generic;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Ai;

/// <summary>
/// Overlay-Zeichenwerkzeuge: Wandelt Pixel-Interaktion in VSA-Quantifizierung um.
/// Arbeitet mit normierten Koordinaten (0.0–1.0).
/// </summary>
public sealed class OverlayToolService : IOverlayToolService
{
    private OverlayToolType _activeTool;
    private PipeCalibration? _calibration;
    private NormalizedPoint? _drawStart;
    private NormalizedPoint? _drawCurrent;
    private bool _isDrawing;

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

    // --- Zeichenoperationen ---

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
        if (!_isDrawing || _drawStart == null || _drawCurrent == null)
        {
            CancelDraw();
            return null;
        }

        var geometry = _activeTool switch
        {
            OverlayToolType.Line => BuildLineGeometry(_drawStart, _drawCurrent),
            OverlayToolType.Arc => BuildArcGeometry(_drawStart, _drawCurrent),
            OverlayToolType.Rectangle => BuildRectangleGeometry(_drawStart, _drawCurrent),
            OverlayToolType.Point => BuildPointGeometry(_drawStart),
            OverlayToolType.Stretch => BuildStretchGeometry(_drawStart, _drawCurrent),
            _ => null
        };

        _isDrawing = false;
        _drawStart = null;
        _drawCurrent = null;
        return geometry;
    }

    public void CancelDraw()
    {
        _isDrawing = false;
        _drawStart = null;
        _drawCurrent = null;
    }

    public bool IsDrawing => _isDrawing;
    public NormalizedPoint? DrawStartPoint => _drawStart;
    public NormalizedPoint? DrawCurrentPoint => _drawCurrent;

    public OverlayGeometry? PreviewGeometry
    {
        get
        {
            if (!_isDrawing || _drawStart == null || _drawCurrent == null) return null;
            return _activeTool switch
            {
                OverlayToolType.Line => BuildLineGeometry(_drawStart, _drawCurrent),
                OverlayToolType.Arc => BuildArcGeometry(_drawStart, _drawCurrent),
                OverlayToolType.Rectangle => BuildRectangleGeometry(_drawStart, _drawCurrent),
                OverlayToolType.Point => BuildPointGeometry(_drawStart),
                _ => null
            };
        }
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
}
