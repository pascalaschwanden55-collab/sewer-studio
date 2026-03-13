using System;
using System.Collections.Generic;

namespace AuswertungPro.Next.UI.Ai.Pipeline;

/// <summary>
/// Converts SAM pixel-level mask data into real-world measurements (mm, %).
/// Uses pipe diameter (DN) and image dimensions for pixel-to-mm conversion.
/// </summary>
public static class MaskQuantificationService
{
    /// <summary>
    /// Assumption: the pipe occupies ~70% of the image width.
    /// </summary>
    private const double PipeImageWidthRatio = 0.70;

    public sealed record QuantifiedMask(
        string Label,
        double Confidence,
        int? HeightMm,
        int? WidthMm,
        int? ExtentPercent,
        int? CrossSectionReductionPercent,
        int? IntrusionPercent,
        string? ClockPosition
    );

    /// <summary>
    /// Quantify a single SAM mask result given pipe parameters.
    /// </summary>
    public static QuantifiedMask Quantify(
        SamMaskResult mask,
        int imageWidth,
        int imageHeight,
        int pipeDiameterMm)
    {
        if (imageWidth <= 0 || imageHeight <= 0 || pipeDiameterMm <= 0)
        {
            return new QuantifiedMask(
                mask.Label, mask.Confidence,
                null, null, null, null, null,
                ComputeClockPosition(mask.CentroidX, mask.CentroidY, imageWidth, imageHeight));
        }

        // Pixel-to-mm conversion factor
        double pxToMm = pipeDiameterMm / (imageWidth * PipeImageWidthRatio);

        // Physical dimensions
        int heightMm = (int)Math.Round(mask.HeightPixels * pxToMm);
        int widthMm = (int)Math.Round(mask.WidthPixels * pxToMm);

        // Pipe circumference in pixels (assuming circular pipe filling ~70% of image width)
        double pipeRadiusPx = (imageWidth * PipeImageWidthRatio) / 2.0;
        double pipeCircumferencePx = 2.0 * Math.PI * pipeRadiusPx;

        // Extent: mask width relative to pipe circumference
        int extentPercent = (int)Math.Round(mask.WidthPixels / pipeCircumferencePx * 100.0);
        extentPercent = Math.Clamp(extentPercent, 0, 100);

        // Cross-section reduction: mask area relative to pipe cross-section area (in pixels)
        double pipeCrossSectionPx = Math.PI * pipeRadiusPx * pipeRadiusPx;
        int crossSectionReduction = (int)Math.Round(mask.MaskAreaPixels / pipeCrossSectionPx * 100.0);
        crossSectionReduction = Math.Clamp(crossSectionReduction, 0, 100);

        // Intrusion: only for intrusion-type labels
        int? intrusionPercent = null;
        var labelLower = mask.Label?.ToLowerInvariant() ?? "";
        if (labelLower.Contains("intrusion") || labelLower.Contains("einragung") || labelLower.Contains("root"))
        {
            // Intrusion percent = how far into the pipe the object extends (height relative to diameter)
            intrusionPercent = (int)Math.Round((double)heightMm / pipeDiameterMm * 100.0);
            intrusionPercent = Math.Clamp(intrusionPercent.Value, 0, 100);
        }

        string? clockPos = ComputeClockPosition(
            mask.CentroidX, mask.CentroidY, imageWidth, imageHeight);

        return new QuantifiedMask(
            Label: mask.Label ?? "unknown",
            Confidence: mask.Confidence,
            HeightMm: heightMm,
            WidthMm: widthMm,
            ExtentPercent: extentPercent,
            CrossSectionReductionPercent: crossSectionReduction,
            IntrusionPercent: intrusionPercent,
            ClockPosition: clockPos
        );
    }

    /// <summary>
    /// Quantify all masks in a SAM response.
    /// </summary>
    public static IReadOnlyList<QuantifiedMask> QuantifyAll(
        SamResponse samResponse,
        int pipeDiameterMm)
    {
        var results = new List<QuantifiedMask>(samResponse.Masks.Count);
        foreach (var mask in samResponse.Masks)
        {
            results.Add(Quantify(mask, samResponse.ImageWidth, samResponse.ImageHeight, pipeDiameterMm));
        }
        return results;
    }

    /// <summary>
    /// Compute clock position (1-12) from centroid position in image.
    /// Assumes the pipe center is at the image center.
    /// 12 o'clock = top center (crown), 6 o'clock = bottom center (invert).
    /// </summary>
    public static string? ComputeClockPosition(
        double centroidX, double centroidY,
        int imageWidth, int imageHeight)
    {
        if (imageWidth <= 0 || imageHeight <= 0)
            return null;

        // Center of image = center of pipe
        double cx = imageWidth / 2.0;
        double cy = imageHeight / 2.0;

        double dx = centroidX - cx;
        double dy = -(centroidY - cy); // flip Y (image Y goes down, clock Y goes up)

        // atan2 gives angle from positive X axis, counter-clockwise
        double angle = Math.Atan2(dx, dy); // Note: (dx, dy) so 12 o'clock = 0 radians
        double degrees = angle * (180.0 / Math.PI);

        // Normalize to 0-360
        if (degrees < 0) degrees += 360.0;

        // Convert to clock hours (each hour = 30 degrees, starting at 12 o'clock = 0 degrees)
        int hour = (int)Math.Round(degrees / 30.0) % 12;
        if (hour == 0) hour = 12;

        return $"{hour}:00";
    }
}
