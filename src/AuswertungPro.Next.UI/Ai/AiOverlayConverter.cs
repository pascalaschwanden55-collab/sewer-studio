using System;
using System.Collections.Generic;
using System.Linq;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Ai.Pipeline;

namespace AuswertungPro.Next.UI.Ai;

/// <summary>
/// Quelle eines KI-generierten Overlays.
/// </summary>
public enum AiOverlaySource
{
    Yolo,       // YOLO Bounding Box
    Sam,        // SAM Segmentierungs-Maske
    Finding     // Qwen/LiveDetection Finding
}

/// <summary>
/// KI-generiertes Overlay: OverlayGeometry + KI-Metadaten.
/// Wird auf dem Video angezeigt und kann vom User bestaetigt/verworfen werden.
/// </summary>
public sealed class AiOverlay
{
    public OverlayGeometry Geometry { get; set; } = new();
    public string Label { get; set; } = "";
    public double Confidence { get; set; }
    public int Severity { get; set; }
    public string? VsaCodeHint { get; set; }
    public bool IsAccepted { get; set; }
    public bool IsRejected { get; set; }
    public AiOverlaySource Source { get; set; }
}

/// <summary>
/// Konvertiert KI-Pipeline-Ergebnisse in AiOverlay-Listen fuer die Visualisierung.
/// </summary>
public static class AiOverlayConverter
{
    /// <summary>
    /// YOLO Bounding Boxes → Rectangle-Overlays (normiert).
    /// </summary>
    public static List<AiOverlay> FromYoloDetections(
        IReadOnlyList<YoloDetectionDto> detections,
        int imageWidth, int imageHeight)
    {
        if (detections == null || imageWidth <= 0 || imageHeight <= 0)
            return new List<AiOverlay>();

        return detections.Select(d =>
        {
            // Pixel → normierte Koordinaten (0.0–1.0)
            double x1 = d.X1 / imageWidth;
            double y1 = d.Y1 / imageHeight;
            double x2 = d.X2 / imageWidth;
            double y2 = d.Y2 / imageHeight;

            var geo = new OverlayGeometry
            {
                ToolType = OverlayToolType.Rectangle,
                Points = new List<NormalizedPoint>
                {
                    new(x1, y1), new(x2, y1),
                    new(x2, y2), new(x1, y2)
                }
            };

            return new AiOverlay
            {
                Geometry = geo,
                Label = d.ClassName,
                Confidence = d.Confidence,
                Severity = ConfidenceToSeverity(d.Confidence),
                Source = AiOverlaySource.Yolo
            };
        }).ToList();
    }

    /// <summary>
    /// SAM Masken → BBox-basierte Overlays (Kontur als Rechteck, da RLE nicht direkt gerendert wird).
    /// Enthaelt Quantifizierungs-Daten (CrossSection%, Height/Width mm).
    /// </summary>
    public static List<AiOverlay> FromSamMasks(
        SamResponse samResponse,
        PipeCalibration? calibration,
        int pipeDiameterMm = 300)
    {
        if (samResponse?.Masks == null) return new List<AiOverlay>();

        var results = new List<AiOverlay>();
        foreach (var mask in samResponse.Masks)
        {
            // Quantifizieren
            var quantified = MaskQuantificationService.Quantify(
                mask, samResponse.ImageWidth, samResponse.ImageHeight,
                pipeDiameterMm, calibration);

            // BBox normieren
            double x1 = 0, y1 = 0, x2 = 1, y2 = 1;
            if (mask.Bbox.Count >= 4 && samResponse.ImageWidth > 0)
            {
                x1 = mask.Bbox[0] / samResponse.ImageWidth;
                y1 = mask.Bbox[1] / samResponse.ImageHeight;
                x2 = mask.Bbox[2] / samResponse.ImageWidth;
                y2 = mask.Bbox[3] / samResponse.ImageHeight;
            }

            var geo = new OverlayGeometry
            {
                ToolType = OverlayToolType.Rectangle,
                Points = new List<NormalizedPoint>
                {
                    new(x1, y1), new(x2, y1),
                    new(x2, y2), new(x1, y2)
                },
                Q1Mm = quantified.HeightMm,
                Q2Mm = quantified.WidthMm,
            };

            // Ablagerung/Wasser → Level-Overlay
            if (quantified.CrossSectionReductionPercent is > 0)
            {
                geo.FillPercent = quantified.CrossSectionReductionPercent;
                geo.ToolType = OverlayToolType.Level;
                geo.LevelSubMode = IsWaterLabel(mask.Label)
                    ? LevelMode.Water
                    : LevelMode.Deposit;
            }

            results.Add(new AiOverlay
            {
                Geometry = geo,
                Label = quantified.Label,
                Confidence = quantified.Confidence,
                Severity = EstimateSeverity(quantified),
                VsaCodeHint = null,
                Source = AiOverlaySource.Sam
            });
        }

        return results;
    }

    /// <summary>
    /// LiveFrameFindings → Mess-Overlays basierend auf Finding-Typ.
    /// </summary>
    public static List<AiOverlay> FromFindings(
        IReadOnlyList<LiveFrameFinding> findings,
        PipeCalibration? calibration)
    {
        if (findings == null) return new List<AiOverlay>();

        var results = new List<AiOverlay>();
        foreach (var f in findings)
        {
            var overlay = FindingToOverlay(f, calibration);
            if (overlay != null)
                results.Add(overlay);
        }
        return results;
    }

    private static AiOverlay? FindingToOverlay(LiveFrameFinding f, PipeCalibration? cal)
    {
        // Uhrposition → normierte Position im Bild
        double pipeCenterX = cal?.PipeCenter.X ?? 0.5;
        double pipeCenterY = cal?.PipeCenter.Y ?? 0.5;
        double pipeRadius = (cal?.NormalizedDiameter ?? 0.7) / 2.0;

        var clockHour = ParseClockHour(f.PositionClock);
        var (pointX, pointY) = ClockToNormalized(clockHour, pipeCenterX, pipeCenterY, pipeRadius);

        var geo = new OverlayGeometry();
        var label = f.Label;

        // Typ anhand des Findings bestimmen
        if (f.CrossSectionReductionPercent is > 0)
        {
            // Ablagerung/Wasser → Level-Overlay
            geo.ToolType = OverlayToolType.Level;
            geo.FillPercent = f.CrossSectionReductionPercent;
            geo.LevelSubMode = IsWaterLabel(label) ? LevelMode.Water : LevelMode.Deposit;

            // Level-Linie auf geschaetzter Hoehe
            double fillRatio = InverseCircleSegmentPercent(f.CrossSectionReductionPercent.Value);
            double sohle = pipeCenterY + pipeRadius;
            double levelY = sohle - fillRatio * pipeRadius * 2.0;
            geo.Points = new List<NormalizedPoint>
            {
                new(pipeCenterX - pipeRadius, levelY),
                new(pipeCenterX + pipeRadius, levelY)
            };
        }
        else if (IsRissLabel(label))
        {
            // Riss → Line-Overlay auf Uhrposition
            geo.ToolType = OverlayToolType.Line;
            double lineLen = pipeRadius * 0.3;
            geo.Points = new List<NormalizedPoint>
            {
                new(pointX - lineLen / 2, pointY),
                new(pointX + lineLen / 2, pointY)
            };
            geo.Q1Mm = f.HeightMm;
            geo.Q2Mm = f.WidthMm;
        }
        else
        {
            // Standard → Rectangle-Overlay (Markierung um Fundstelle)
            geo.ToolType = OverlayToolType.Rectangle;
            double boxSize = pipeRadius * 0.25;
            geo.Points = new List<NormalizedPoint>
            {
                new(pointX - boxSize, pointY - boxSize),
                new(pointX + boxSize, pointY - boxSize),
                new(pointX + boxSize, pointY + boxSize),
                new(pointX - boxSize, pointY + boxSize)
            };
            geo.Q1Mm = f.HeightMm;
            geo.Q2Mm = f.WidthMm;
        }

        geo.ClockFrom = clockHour;
        if (f.ExtentPercent is > 0)
        {
            double extentHours = f.ExtentPercent.Value / 100.0 * 12.0;
            geo.ClockTo = (clockHour + extentHours) % 12.0;
        }

        return new AiOverlay
        {
            Geometry = geo,
            Label = label,
            Confidence = 0.0,
            Severity = f.Severity,
            VsaCodeHint = f.VsaCodeHint,
            Source = AiOverlaySource.Finding
        };
    }

    // --- Hilfsmethoden ---

    private static bool IsWaterLabel(string? label)
    {
        if (string.IsNullOrEmpty(label)) return false;
        var lower = label.ToLowerInvariant();
        return lower.Contains("wasser") || lower.Contains("water")
            || lower.Contains("bwa") || lower.Contains("bwb");
    }

    private static bool IsRissLabel(string? label)
    {
        if (string.IsNullOrEmpty(label)) return false;
        var lower = label.ToLowerInvariant();
        return lower.Contains("riss") || lower.Contains("crack")
            || lower.Contains("baa") || lower.Contains("bab");
    }

    private static int ConfidenceToSeverity(double confidence) =>
        confidence switch
        {
            >= 0.9 => 5,
            >= 0.7 => 4,
            >= 0.5 => 3,
            >= 0.3 => 2,
            _ => 1
        };

    private static int EstimateSeverity(MaskQuantificationService.QuantifiedMask q)
    {
        if (q.CrossSectionReductionPercent is >= 50) return 5;
        if (q.CrossSectionReductionPercent is >= 30) return 4;
        if (q.CrossSectionReductionPercent is >= 15) return 3;
        if (q.IntrusionPercent is >= 30) return 4;
        return 2;
    }

    private static double ParseClockHour(string? clockStr)
    {
        if (string.IsNullOrWhiteSpace(clockStr)) return 12.0;
        var match = System.Text.RegularExpressions.Regex.Match(clockStr, @"(\d{1,2})");
        if (match.Success && int.TryParse(match.Groups[1].Value, out var h))
            return h == 0 ? 12.0 : Math.Clamp(h, 1, 12);
        return 12.0;
    }

    /// <summary>Uhrposition → normierte XY-Koordinate im Rohrbild.</summary>
    private static (double x, double y) ClockToNormalized(
        double clockHour, double cx, double cy, double radius)
    {
        double angleDeg = (clockHour % 12) * 30.0;
        double angleRad = angleDeg * Math.PI / 180.0;
        // 12 Uhr = oben (-Y), Uhrzeigersinn
        double x = cx + Math.Sin(angleRad) * radius * 0.8;
        double y = cy - Math.Cos(angleRad) * radius * 0.8;
        return (x, y);
    }

    /// <summary>
    /// Inverse Kreissegment-Formel: Prozent → hRatio (Naeherung via Newton).
    /// </summary>
    private static double InverseCircleSegmentPercent(double percent)
    {
        percent = Math.Clamp(percent, 0, 100);
        if (percent <= 0) return 0;
        if (percent >= 100) return 1;

        // Newton-Iteration
        double h = percent / 100.0; // Startwert
        for (int i = 0; i < 20; i++)
        {
            double current = OverlayToolService.CircleSegmentPercent(h);
            double error = current - percent;
            if (Math.Abs(error) < 0.01) break;

            // Numerische Ableitung
            double dh = 0.001;
            double derivative = (OverlayToolService.CircleSegmentPercent(h + dh)
                                - OverlayToolService.CircleSegmentPercent(h - dh)) / (2 * dh);
            if (Math.Abs(derivative) < 1e-8) break;
            h -= error / derivative;
            h = Math.Clamp(h, 0, 1);
        }
        return h;
    }
}

// LiveFrameFinding ist in VideoFullAnalysisService.cs definiert (gleicher Namespace).
