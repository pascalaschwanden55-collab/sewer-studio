using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Infrastructure.Ai.Training;

namespace AuswertungPro.Next.Pipeline.Tests;

public sealed class SelfTrainingComparisonServiceTests
{
    [Fact]
    public void Compare_TimeoutWithoutFindings_IsNotCountedAsNoFindings()
    {
        var svc = new SelfTrainingComparisonService();
        var truth = new GroundTruthEntry
        {
            MeterStart = 12.0,
            MeterEnd = 12.0,
            VsaCode = "BAB",
            Text = "Riss",
            IsStreckenschaden = false
        };
        var analysis = EnhancedFrameAnalysis.Empty("Timeout (30s)", AnalysisOutcome.Timeout);

        var result = svc.Compare(truth, analysis);

        Assert.Equal(MatchLevel.Mismatch, result.Level);
        Assert.Contains("Timeout", result.Explanation);
    }
}
