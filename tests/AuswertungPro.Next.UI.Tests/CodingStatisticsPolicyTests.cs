using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.ViewModels.Windows;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingStatisticsPolicyTests
{
    [Fact]
    public void Build_returns_zero_summary_without_events()
    {
        var summary = CodingStatisticsPolicy.Build(
            Array.Empty<CodingEvent>(),
            _ => throw new InvalidOperationException("Status resolver must not be called."));

        Assert.Equal(0, summary.Total);
        Assert.Equal(0, summary.Open);
        Assert.Equal(0, summary.AutoAccepted);
        Assert.Equal(0, summary.Pending);
        Assert.Equal(0, summary.ReviewRequired);
        Assert.Equal("\u2013", summary.AverageConfidenceText);
    }

    [Fact]
    public void Build_counts_only_ai_statuses_and_averages_ai_confidence()
    {
        var events = new[]
        {
            EventWithoutAi(),
            AiEvent(0.95, CodingUserDecision.Ignored, gate: "Green"),
            AiEvent(0.70, CodingUserDecision.Ignored),
            AiEvent(0.30, CodingUserDecision.Ignored),
            AiEvent(0.80, CodingUserDecision.AcceptedWithEdit),
            AiEvent(0.80, CodingUserDecision.Rejected)
        };

        var summary = CodingStatisticsPolicy.Build(
            events,
            CodingSessionViewModel.GetDefectStatus);

        Assert.Equal(6, summary.Total);
        Assert.Equal(2, summary.Open);
        Assert.Equal(2, summary.AutoAccepted);
        Assert.Equal(1, summary.Pending);
        Assert.Equal(1, summary.ReviewRequired);
        Assert.Equal("71%", summary.AverageConfidenceText);
    }

    private static CodingEvent EventWithoutAi() => new();

    private static CodingEvent AiEvent(double confidence, CodingUserDecision decision, string? gate = null)
        => new()
        {
            AiContext = new CodingEventAiContext
            {
                Confidence = confidence,
                QualityGateLevel = gate,
                Decision = decision
            }
        };
}
