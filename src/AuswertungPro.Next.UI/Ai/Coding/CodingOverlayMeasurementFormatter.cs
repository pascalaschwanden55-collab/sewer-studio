using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Ai.Coding;

public sealed record CodingOverlayMeasurementPanelState(
    bool IsVisible,
    string Q1Text,
    string Q2Text,
    string ClockText,
    string ArcText,
    string MeasurementText);

public static class CodingOverlayMeasurementFormatter
{
    public static string BuildOverlayMeasurementText(OverlayGeometry overlay)
    {
        if (overlay.ToolType == OverlayToolType.PipeBend && overlay.ArcDegrees.HasValue)
            return $"Winkel: {overlay.ArcDegrees.Value:F1}\u00B0";

        if (overlay.ToolType == OverlayToolType.Level && overlay.FillPercent.HasValue)
            return $"{BuildLevelLabel(overlay)}: {overlay.FillPercent.Value:F1}%";

        if (overlay.ToolType == OverlayToolType.LateralCircle)
        {
            var dnParts = new List<string>();
            if (overlay.Q1Mm.HasValue)
                dnParts.Add($"DN {overlay.Q1Mm.Value:F0}");
            if (overlay.DnRatioPercent.HasValue)
                dnParts.Add($"({overlay.DnRatioPercent.Value:F0}% v. Haupt-DN)");
            return string.Join(" ", dnParts);
        }

        if (overlay.ToolType == OverlayToolType.Ruler && overlay.Q1Mm.HasValue)
            return $"Laenge: {overlay.Q1Mm.Value:F1} mm";

        if (overlay.ToolType is OverlayToolType.Ellipse or OverlayToolType.Freehand or OverlayToolType.CrossSection
            && overlay.FillPercent.HasValue)
        {
            return $"Querschnitt: {overlay.FillPercent.Value:F1}%";
        }

        var parts = new List<string>();

        if (overlay.Q1Mm.HasValue)
            parts.Add($"Q1:{overlay.Q1Mm.Value:F0}mm");
        if (overlay.Q2Mm.HasValue)
            parts.Add($"Q2:{overlay.Q2Mm.Value:F0}mm");
        if (overlay.ClockFrom.HasValue)
        {
            parts.Add(overlay.ClockTo.HasValue
                ? $"Uhr:{overlay.ClockFrom.Value:F1}->{overlay.ClockTo.Value:F1}"
                : $"Uhr:{overlay.ClockFrom.Value:F1}");
        }
        if (overlay.ArcDegrees.HasValue)
            parts.Add($"Bogen:{overlay.ArcDegrees.Value:F0}deg");

        return string.Join("  ", parts);
    }

    public static string BuildPanelMeasurementText(OverlayGeometry overlay)
    {
        var parts = new List<string>();

        if (overlay.ToolType == OverlayToolType.PipeBend)
        {
            if (overlay.ArcDegrees.HasValue)
                parts.Add($"Winkel:{overlay.ArcDegrees:F1}\u00B0");
            if (overlay.ClockFrom.HasValue)
                parts.Add($"Uhr:{overlay.ClockFrom:F1}");
        }
        else if (overlay.ToolType == OverlayToolType.Level)
        {
            if (overlay.FillPercent.HasValue)
                parts.Add($"{BuildLevelLabel(overlay)}:{overlay.FillPercent:F1}%");
            if (overlay.ClockFrom.HasValue && overlay.Points.Count >= 3)
                parts.Add($"Uhr:{overlay.ClockFrom:F1}");
        }
        else if (overlay.ToolType == OverlayToolType.LateralCircle)
        {
            if (overlay.Q1Mm.HasValue)
                parts.Add($"DN:{overlay.Q1Mm:F0}mm");
            if (overlay.DnRatioPercent.HasValue)
                parts.Add($"{overlay.DnRatioPercent:F0}%");
            if (overlay.ClockFrom.HasValue)
                parts.Add($"Uhr:{overlay.ClockFrom:F1}");
        }
        else if (overlay.ToolType == OverlayToolType.Ruler)
        {
            if (overlay.Q1Mm.HasValue)
                parts.Add($"Laenge:{overlay.Q1Mm:F1}mm");
        }
        else
        {
            if (overlay.Q1Mm.HasValue)
                parts.Add($"Q1:{overlay.Q1Mm:F1}mm");
            if (overlay.FillPercent.HasValue)
                parts.Add($"QV:{overlay.FillPercent:F1}%");
            if (overlay.ClockFrom.HasValue)
                parts.Add($"Uhr:{overlay.ClockFrom:F1}");
            if (overlay.ArcDegrees.HasValue)
                parts.Add($"{overlay.ArcDegrees:F0}deg");
        }

        return string.Join("  |  ", parts);
    }

    public static CodingOverlayMeasurementPanelState BuildPanelState(OverlayGeometry? overlay)
    {
        if (overlay == null)
        {
            return new CodingOverlayMeasurementPanelState(
                IsVisible: false,
                Q1Text: "Q1: -",
                Q2Text: "Q2: -",
                ClockText: "Uhr: -",
                ArcText: "Bogen: -",
                MeasurementText: "");
        }

        return new CodingOverlayMeasurementPanelState(
            IsVisible: true,
            Q1Text: overlay.Q1Mm.HasValue ? $"Q1: {overlay.Q1Mm:F1} mm" : "Q1: -",
            Q2Text: overlay.Q2Mm.HasValue ? $"Q2: {overlay.Q2Mm:F1} mm" : "Q2: -",
            ClockText: BuildPanelClockText(overlay),
            ArcText: BuildPanelArcText(overlay),
            MeasurementText: BuildPanelMeasurementText(overlay));
    }

    private static string BuildPanelClockText(OverlayGeometry overlay)
    {
        if (!overlay.ClockFrom.HasValue)
            return "Uhr: -";

        return overlay.ClockTo.HasValue
            ? $"Uhr: {overlay.ClockFrom:F1} -> {overlay.ClockTo:F1}"
            : $"Uhr: {overlay.ClockFrom:F1}";
    }

    private static string BuildPanelArcText(OverlayGeometry overlay)
    {
        if (overlay.ToolType == OverlayToolType.Level && overlay.FillPercent.HasValue)
            return $"Fuellung: {overlay.FillPercent:F1}%";

        if (!overlay.ArcDegrees.HasValue)
            return "Bogen: -";

        return overlay.ToolType == OverlayToolType.PipeBend
            ? $"Winkel: {overlay.ArcDegrees:F1}\u00B0"
            : $"Bogen: {overlay.ArcDegrees:F0} deg";
    }

    private static string BuildLevelLabel(OverlayGeometry overlay)
        => overlay.Points.Count >= 3
            ? "Einragung"
            : overlay.LevelSubMode switch
            {
                LevelMode.Water => "Wasser",
                LevelMode.Obstacle => "Hindernis",
                _ => "Sediment"
            };
}
