using System;

namespace AuswertungPro.Next.Application.Ai.Pipeline;

/// <summary>
/// Builds SAM ring-scan parameters from the pipe-axis result.
/// The sidecar currently returns normalized pipe geometry; older callers may
/// still pass pixel values, so this helper accepts both representations.
/// </summary>
public static class PipeAxisRingScanFactory
{
    private const double FallbackOuterRadiusRatio = 0.44;
    private const double FallbackInnerRadiusRatio = 0.22;

    /// <summary>
    /// Audit 2026-05-15: Ring-Scan IMMER liefern wenn das Bild sinnvolle Dimensionen
    /// hat. Vorher konnte die Factory null zurueckgeben (PipeAxis-Confidence
    /// zu niedrig oder Plausibilitaet schlug fehl) — dann lief SAM gar nicht.
    /// Folge: kein Befund, keine visuelle Evidenz.
    /// Jetzt: bei jedem Frame mindestens der Standard-Ring (Bildmitte + 35-44% Radius).
    /// PipeAxis-Werte werden nur verwendet wenn sie plausibel sind — sonst Fallback.
    /// </summary>
    public static RingScanParams? Create(PipeAxisResult? axis, int imageWidth, int imageHeight)
    {
        if (imageWidth <= 0 || imageHeight <= 0)
            return null;

        var minSide = Math.Min(imageWidth, imageHeight);

        double centerX = imageWidth / 2.0;
        double centerY = imageHeight / 2.0;
        double radiusX = minSide * FallbackOuterRadiusRatio;
        double radiusY = radiusX;

        if (axis is not null)
        {
            var ax = ToPixels(axis.PipeCenterX, imageWidth);
            var ay = ToPixels(axis.PipeCenterY, imageHeight);
            var arx = ToRadiusPixels(axis.PipeRadiusX, imageWidth);
            var ary = ToRadiusPixels(axis.PipeRadiusY, imageHeight);

            if (IsPlausible(ax, ay, arx, ary, imageWidth, imageHeight))
            {
                centerX = ax;
                centerY = ay;
                radiusX = arx;
                radiusY = ary;
            }
            // sonst: Fallback-Werte bleiben — Ring-Scan wird trotzdem ausgefuehrt.
        }

        var outer = Math.Clamp((radiusX + radiusY) / 2.0, minSide * 0.18, minSide * 0.48);
        var inner = Math.Clamp(outer * 0.52, minSide * 0.10, outer * 0.80);
        var minArea = Math.Max(80, (int)(imageWidth * imageHeight * 0.00035));

        return new RingScanParams(
            CenterX: centerX,
            CenterY: centerY,
            InnerRadius: inner,
            OuterRadius: outer,
            NumAngles: 32,
            NumRadii: 3,
            MinScore: 0.30,
            MinAreaPixels: minArea,
            IouThreshold: 0.35);
    }

    private static double ToPixels(double value, int size)
        => value is >= 0 and <= 1.5 ? value * size : value;

    private static double ToRadiusPixels(double value, int size)
        => value is > 0 and <= 1.5 ? value * size : value;

    private static bool IsPlausible(
        double centerX,
        double centerY,
        double radiusX,
        double radiusY,
        int imageWidth,
        int imageHeight)
    {
        if (centerX < 0 || centerX > imageWidth || centerY < 0 || centerY > imageHeight)
            return false;

        var minSide = Math.Min(imageWidth, imageHeight);
        if (radiusX < minSide * 0.08 || radiusY < minSide * 0.08)
            return false;

        if (radiusX > imageWidth || radiusY > imageHeight)
            return false;

        return true;
    }
}
