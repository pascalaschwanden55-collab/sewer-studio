using AuswertungPro.Next.Application.Ai;

namespace AuswertungPro.Next.UI.Ai.Coding;

public static class CodingClassifierDisplayPolicy
{
    public const string PossibleBoundaryEndStatus = "Mögliches Rohrende voraus - noch nicht am Ende";
    public const string PossibleBoundaryEndDetail = "näher heranfahren";

    public static bool IsBoundaryClassifierCode(string? code)
        => code is "BCD" or "BCE";

    public static bool IsStructuralClassifierCode(string? code)
        => code is "BCA" or "BCC";

    public static string ResolveBoundaryLabel(string? code, string? catalogLabel)
    {
        if (!string.IsNullOrWhiteSpace(catalogLabel))
            return catalogLabel;

        return code == "BCD" ? "Rohranfang" : "Rohrende";
    }

    public static string ResolveStructuralLabel(string? code, string? catalogLabel)
    {
        if (!string.IsNullOrWhiteSpace(catalogLabel))
            return catalogLabel;

        return code == "BCC" ? "Bogen" : "Anschluss";
    }

    public static string BuildDetectedStatusText(string label, bool added)
        => added ? $"{label} erkannt" : $"{label} bereits vorhanden";

    public static string BuildClassifierDetail(double? confidence)
    {
        var suffix = confidence.HasValue ? $" {confidence.Value:P0}" : "";
        return $"Klassifikator{suffix}";
    }

    public static LiveFrameFinding BuildBoundaryFinding(string? code, string label)
        => new(
            Label: label,
            Severity: 4,
            PositionClock: null,
            ExtentPercent: null,
            VsaCodeHint: code);

    public static LiveFrameFinding BuildPossibleBoundaryFinding(string? code, string label)
        => new(
            Label: $"Mögliches {label}",
            Severity: 3,
            PositionClock: null,
            ExtentPercent: null,
            VsaCodeHint: code);
}
