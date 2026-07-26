using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;
using AuswertungPro.Next.UI.ViewModels.Windows;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingUiUpdateWorkflowTests
{
    [Fact]
    public void Apply_updates_common_ui_and_clears_pending_navigation_after_current_meter_change()
    {
        var calls = new List<string>();

        var result = CodingUiUpdateWorkflow.Apply(
            nameof(CodingSessionViewModel.CurrentMeter),
            navigationPending: true,
            Actions(calls));

        Assert.False(result.NavigationPending);
        Assert.Equal(
            ["meter", "sync", "overlay", "current-code"],
            calls);
    }

    [Fact]
    public void Apply_keeps_pending_navigation_when_other_property_changes()
    {
        var calls = new List<string>();

        var result = CodingUiUpdateWorkflow.Apply(
            nameof(CodingSessionViewModel.SelectedCode),
            navigationPending: true,
            Actions(calls));

        Assert.True(result.NavigationPending);
        Assert.Equal(
            ["meter", "overlay", "current-code"],
            calls);
    }

    [Fact]
    public void Apply_skips_statistics_for_non_statistical_property()
    {
        var calls = new List<string>();

        var result = CodingUiUpdateWorkflow.Apply(
            nameof(CodingSessionViewModel.CurrentVideoTime),
            navigationPending: false,
            Actions(calls));

        Assert.False(result.NavigationPending);
        Assert.Equal(
            ["meter", "overlay", "current-code"],
            calls);
    }

    [Fact]
    public void Apply_refreshes_statistics_for_statistical_property()
    {
        var calls = new List<string>();

        var result = CodingUiUpdateWorkflow.Apply(
            nameof(CodingSessionViewModel.EventCount),
            navigationPending: false,
            Actions(calls));

        Assert.False(result.NavigationPending);
        Assert.Equal(
            ["meter", "overlay", "current-code", "stats"],
            calls);
    }

    private static CodingUiUpdateActions Actions(List<string> calls)
        => new(
            ApplyMeterTimeline: () => calls.Add("meter"),
            SyncVideoToCodingMeter: () => calls.Add("sync"),
            UpdateOverlayInfo: () => calls.Add("overlay"),
            UpdateCurrentCode: () => calls.Add("current-code"),
            UpdateStatistics: () => calls.Add("stats"));
}
