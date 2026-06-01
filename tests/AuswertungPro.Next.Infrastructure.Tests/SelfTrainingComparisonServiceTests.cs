using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Infrastructure.Ai.Training;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>
/// S2: ExactMatch (= Auto-Accept-Grundlage) verlangt jetzt Code exakt + Meter in typabhaengiger
/// Toleranz + plausible Severity + positiv bestaetigte Uhrlage. Friert die Regeln aus der
/// User-Spec als Regression ein. Deterministisch, kein Ollama/Netz.
/// </summary>
public sealed class SelfTrainingComparisonServiceTests
{
    private static readonly SelfTrainingComparisonService Svc = new();

    private static EnhancedFinding Finding(string? code, int severity, string? clock) => new(
        Label: code ?? "x", VsaCodeHint: code, Severity: severity, PositionClock: clock,
        ExtentPercent: null, HeightMm: null, WidthMm: null, IntrusionPercent: null,
        CrossSectionReductionPercent: null, DiameterReductionMm: null,
        BboxX1: null, BboxY1: null, BboxX2: null, BboxY2: null, Notes: null);

    private static EnhancedFrameAnalysis Analysis(double? meter, params EnhancedFinding[] findings)
        => new(meter, "Beton", 300, findings, "gut", false, null);

    private static GroundTruthEntry Truth(string code, double mStart, double mEnd, string? clock, bool strecke = false)
        => new() { VsaCode = code, MeterStart = mStart, MeterEnd = mEnd, Text = "t", ClockPosition = clock, IsStreckenschaden = strecke };

    [Fact]
    public void CleanCase_AllAxesMatch_IsExactMatch()
    {
        var r = Svc.Compare(Truth("BAB", 5.0, 5.0, "3"), Analysis(5.0, Finding("BAB", 3, "3")));
        Assert.Equal(MatchLevel.ExactMatch, r.Level);
    }

    [Fact]
    public void ImplausibleSeverity_GatesExactMatch_DownToPartial()
    {
        // BAB = baulich (Kategorie A) -> Severity muss >= 2 sein; Sev 1 ist implausibel.
        var r = Svc.Compare(Truth("BAB", 5.0, 5.0, "3"), Analysis(5.0, Finding("BAB", 1, "3")));
        Assert.Equal(MatchLevel.PartialMatch, r.Level);
        Assert.False(r.SeverityPlausible);
    }

    [Fact]
    public void MissingProtocolClock_BothEmpty_IsNotExactMatch()
    {
        // Fruehere Logik wertete "beide leer" als Uhr-Treffer -> faelschlich ExactMatch.
        // Jetzt: fehlende Protokoll-Uhrlage erzeugt KEINEN Volltreffer.
        var r = Svc.Compare(Truth("BAB", 5.0, 5.0, clock: null), Analysis(5.0, Finding("BAB", 3, clock: null)));
        Assert.Equal(MatchLevel.PartialMatch, r.Level);
    }

    [Fact]
    public void ProtocolHasClock_KiEmpty_IsConflict_NotExactMatch()
    {
        var r = Svc.Compare(Truth("BAB", 5.0, 5.0, "3"), Analysis(5.0, Finding("BAB", 3, clock: null)));
        Assert.Equal(MatchLevel.PartialMatch, r.Level);
    }

    [Fact]
    public void ProtocolEmpty_KiClaimsClock_IsNotExactMatch()
    {
        var r = Svc.Compare(Truth("BAB", 5.0, 5.0, clock: null), Analysis(5.0, Finding("BAB", 3, "9")));
        Assert.Equal(MatchLevel.PartialMatch, r.Level);
    }

    [Theory]
    [InlineData(5.25, MatchLevel.ExactMatch)]   // 0.25 m <= 0.30 m Anschluss-Toleranz
    [InlineData(5.40, MatchLevel.PartialMatch)] // 0.40 m > 0.30 m -> kein Voll-Treffer
    public void AnschlussCode_UsesTighterMeterTolerance(double kiMeter, MatchLevel expected)
    {
        // BCA = seitlicher Anschluss (Kategorie C -> Severity <= 2); Uhr passt.
        var r = Svc.Compare(Truth("BCA", 5.0, 5.0, "3"), Analysis(kiMeter, Finding("BCA", 2, "3")));
        Assert.Equal(expected, r.Level);
    }

    [Theory]
    [InlineData(6.0, MatchLevel.ExactMatch)]    // im Bereich 5.0-7.0
    [InlineData(8.0, MatchLevel.PartialMatch)]  // ausserhalb 5.0-7.0 (+/- 0.5 Rand)
    public void Streckenschaden_UsesOverlapNotPointDistance(double kiMeter, MatchLevel expected)
    {
        var r = Svc.Compare(
            Truth("BAF", 5.0, 7.0, "6", strecke: true),
            Analysis(kiMeter, Finding("BAF", 3, "6")));
        Assert.Equal(expected, r.Level);
    }
}
