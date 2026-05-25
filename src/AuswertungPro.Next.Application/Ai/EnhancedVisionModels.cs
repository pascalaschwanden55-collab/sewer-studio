namespace AuswertungPro.Next.Application.Ai;

public sealed record EnhancedFrameAnalysis(
    double? Meter,
    string PipeMaterial,
    int? PipeDiameterMm,
    IReadOnlyList<EnhancedFinding> Findings,
    string ImageQuality,
    bool IsEmptyFrame,
    string? Error)
{
    public bool HasFindings => Findings.Count > 0;

    public static EnhancedFrameAnalysis Empty(string? error = null) =>
        new(null, "unbekannt", null,
            Array.Empty<EnhancedFinding>(), "schlecht", true, error);
}

public sealed record EnhancedFinding(
    string Label,
    string? VsaCodeHint,
    int Severity,
    string? PositionClock,
    int? ExtentPercent,
    int? HeightMm,
    int? WidthMm,
    int? IntrusionPercent,
    int? CrossSectionReductionPercent,
    int? DiameterReductionMm,
    double? BboxX1,
    double? BboxY1,
    double? BboxX2,
    double? BboxY2,
    string? Notes
);
