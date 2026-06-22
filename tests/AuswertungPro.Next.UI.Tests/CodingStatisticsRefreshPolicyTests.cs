using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.ViewModels.Windows;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingStatisticsRefreshPolicyTests
{
    [Theory]
    [InlineData(null)]
    [InlineData(nameof(CodingSessionViewModel.StatAutoAccepted))]
    [InlineData(nameof(CodingSessionViewModel.StatPending))]
    [InlineData(nameof(CodingSessionViewModel.StatReviewRequired))]
    [InlineData(nameof(CodingSessionViewModel.StatAverageConfidence))]
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
