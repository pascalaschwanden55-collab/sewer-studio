namespace AuswertungPro.Next.Application.Ai;

public sealed record LiveDetection(
    double TimestampSeconds,
    IReadOnlyList<LiveFrameFinding> Findings,
    double? MeterReading,
    string? Error);

public sealed record LiveFrameFinding(
    string Label,
    int Severity,
    string? PositionClock,
    int? ExtentPercent,
    string? VsaCodeHint = null,
    int? HeightMm = null,
    int? WidthMm = null,
    int? IntrusionPercent = null,
    int? CrossSectionReductionPercent = null,
    int? DiameterReductionMm = null,
    double? BboxX1 = null,
    double? BboxY1 = null,
    double? BboxX2 = null,
    double? BboxY2 = null);
