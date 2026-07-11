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
        Assert.Equal(0, summary.AiCriteriaMet);
        Assert.Equal(0, summary.HumanAccepted);
        Assert.Equal(0, summary.HumanCorrected);
        Assert.Equal(0, summary.Rejected);
        Assert.Equal("\u2013", summary.AverageAiConfidenceText);
    }

    [Fact]
    public void Build_separates_ai_human_and_manual_statuses()
    {
        var events = new[]
        {
            EventWithoutAi(),
            AiEvent(0.95, CodingUserDecision.Ignored, gate: "Green", kbAgreement: true),
            AiEvent(0.70, CodingUserDecision.Ignored),
            AiEvent(0.30, CodingUserDecision.Ignored),
            AiEvent(0.80, CodingUserDecision.Accepted),
            AiEvent(0.80, CodingUserDecision.AcceptedWithEdit),
            AiEvent(0.80, CodingUserDecision.Rejected),
            ManualEvent(CodingUserDecision.Accepted),
            ManualEvent(CodingUserDecision.AcceptedWithEdit),
            ManualEvent(CodingUserDecision.Rejected)
        };

        var summary = CodingStatisticsPolicy.Build(
            events,
            CodingSessionViewModel.GetDefectStatus);

        Assert.Equal(10, summary.Total);
        Assert.Equal(2, summary.Open);
        Assert.Equal(1, summary.AiCriteriaMet);
        Assert.Equal(2, summary.HumanAccepted);
        Assert.Equal(2, summary.HumanCorrected);
        Assert.Equal(2, summary.Rejected);
        Assert.Equal("72%", summary.AverageAiConfidenceText);
    }

    private static CodingEvent EventWithoutAi() => new();

    private static CodingEvent ManualEvent(CodingUserDecision decision)
        => new()
        {
            ReviewContext = new CodingEventReviewContext { Decision = decision }
        };

    private static CodingEvent AiEvent(
        double confidence,
        CodingUserDecision decision,
        string? gate = null,
        bool? kbAgreement = null)
        => new()
        {
            AiContext = new CodingEventAiContext
            {
                Confidence = confidence,
                QualityGateLevel = gate,
                Evidence = kbAgreement.HasValue
                    ? new CodingEventAiEvidence { KbCodeAgreement = kbAgreement }
                    : null,
                Decision = decision
            }
        };

    [Fact]
    public void Build_ignores_invalid_ai_confidence_in_average()
    {
        var summary = CodingStatisticsPolicy.Build(
            [AiEvent(double.NaN, CodingUserDecision.Rejected), AiEvent(0.8, CodingUserDecision.Accepted)],
            CodingSessionViewModel.GetDefectStatus);

        Assert.Equal("80%", summary.AverageAiConfidenceText);
    }
}
