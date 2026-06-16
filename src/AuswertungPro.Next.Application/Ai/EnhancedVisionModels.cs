using System.Net.Http;
using System.Net.Sockets;

namespace AuswertungPro.Next.Application.Ai;

public enum AnalysisOutcome
{
    Ok = 0,
    NoFinding = 1,
    ModelUnavailable = 2,
    Timeout = 3
}

public sealed record EnhancedFrameAnalysis(
    double? Meter,
    string PipeMaterial,
    int? PipeDiameterMm,
    IReadOnlyList<EnhancedFinding> Findings,
    string ImageQuality,
    bool IsEmptyFrame,
    string? Error,
    AnalysisOutcome Outcome = AnalysisOutcome.Ok)
{
    public bool HasFindings => Findings.Count > 0;
    public bool IsTrainableNegative => Outcome == AnalysisOutcome.NoFinding;

    public static EnhancedFrameAnalysis Empty(
        string? error = null,
        AnalysisOutcome? outcome = null) =>
        new(null, "unbekannt", null,
            Array.Empty<EnhancedFinding>(), "unbekannt", true, error,
            outcome ?? InferEmptyOutcome(error));

    public static EnhancedFrameAnalysis EmptyFromException(Exception ex) =>
        Empty(ex.Message, FromException(ex));

    public static AnalysisOutcome FromException(Exception ex)
    {
        if (ex is TimeoutException or TaskCanceledException)
            return AnalysisOutcome.Timeout;
        if (ex is HttpRequestException or SocketException)
            return AnalysisOutcome.ModelUnavailable;
        return AnalysisOutcome.ModelUnavailable;
    }

    private static AnalysisOutcome InferEmptyOutcome(string? error)
    {
        if (string.IsNullOrWhiteSpace(error))
            return AnalysisOutcome.NoFinding;

        return error.Contains("timeout", StringComparison.OrdinalIgnoreCase)
            || error.Contains("zeit", StringComparison.OrdinalIgnoreCase)
            ? AnalysisOutcome.Timeout
            : AnalysisOutcome.ModelUnavailable;
    }
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
