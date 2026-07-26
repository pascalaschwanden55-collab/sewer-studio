using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;
using AuswertungPro.Next.UI.ViewModels.Windows;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingStatisticsRefreshPolicyTests
{
    [Theory]
    [InlineData(null)]
    [InlineData(nameof(CodingSessionViewModel.StatAiCriteriaMet))]
    [InlineData(nameof(CodingSessionViewModel.StatHumanAccepted))]
    [InlineData(nameof(CodingSessionViewModel.StatHumanCorrected))]
    [InlineData(nameof(CodingSessionViewModel.StatRejected))]
    [InlineData(nameof(CodingSessionViewModel.StatOpen))]
    [InlineData(nameof(CodingSessionViewModel.StatAverageAiConfidenceText))]
    [InlineData(nameof(CodingSessionViewModel.EventCount))]
    public void ShouldRefresh_returns_true_for_statistics_properties(string? propertyName)
    {
        Assert.True(CodingStatisticsRefreshPolicy.ShouldRefresh(propertyName));
    }

    [Theory]
    [InlineData(nameof(CodingSessionViewModel.CurrentMeter))]
    [InlineData(nameof(CodingSessionViewModel.CurrentOverlay))]
    [InlineData("Other")]
    public void ShouldRefresh_returns_false_for_unrelated_properties(string propertyName)
    {
        Assert.False(CodingStatisticsRefreshPolicy.ShouldRefresh(propertyName));
    }
}
