using System.Collections.Generic;

namespace AuswertungPro.Next.Infrastructure.Ai.Pipeline;

/// <summary>
/// Baut den Mess-Text fuer ein Label-Badge einer SAM-Maske.
/// Reine Logik ohne WPF-Abhaengigkeit, testbar.
/// </summary>
public static class MaskLabelTextBuilder
{
    /// <summary>
    /// Baut den Mess-Text fuer ein Label-Badge.
    /// Format: "H:45mm W:2mm | 3:00 | 15%"
    /// </summary>
    public static string BuildMeasurementText(MaskQuantificationService.QuantifiedMask q)
    {
        var parts = new List<string>();

        if (q.HeightMm.HasValue && q.WidthMm.HasValue)
            parts.Add($"H:{q.HeightMm}mm W:{q.WidthMm}mm");
        else if (q.HeightMm.HasValue)
            parts.Add($"H:{q.HeightMm}mm");

        if (!string.IsNullOrEmpty(q.ClockPosition))
            parts.Add(q.ClockPosition);

        if (q.ExtentPercent is > 0)
            parts.Add($"{q.ExtentPercent}%");
        else if (q.CrossSectionReductionPercent is > 0)
            parts.Add($"QR:{q.CrossSectionReductionPercent}%");
        else if (q.IntrusionPercent is > 0)
            parts.Add($"Einr:{q.IntrusionPercent}%");

        return string.Join(" | ", parts);
    }
}
