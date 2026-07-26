using System.Windows;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Live;

namespace AuswertungPro.Next.UI.Tests;

public sealed class LiveDetectionMarkCatalogOpenWorkflowTests
{
    [Fact]
    public void ExecuteCanvasClick_ignores_click_when_manual_mark_mode_is_inactive()
    {
        var calls = new List<string>();

        var result = LiveDetectionMarkCatalogOpenWorkflow.ExecuteCanvasClick(
            new LiveDetectionMarkCatalogCanvasClickRequest(
                IsManualMarkMode: false,
                ClickPoint: new Point(200, 100),
                CanvasSize: new Size(200, 200),
                TimestampSec: 12.5),
            Actions(calls));

        Assert.Equal(LiveDetectionMarkCatalogOpenWorkflowOutcome.Ignored, result.Outcome);
        Assert.False(result.Handled);
        Assert.Empty(calls);
    }

    [Fact]
    public void ExecuteCanvasClick_ignores_click_when_canvas_is_too_small()
    {
        var calls = new List<string>();

        var result = LiveDetectionMarkCatalogOpenWorkflow.ExecuteCanvasClick(
            new LiveDetectionMarkCatalogCanvasClickRequest(
                IsManualMarkMode: true,
                ClickPoint: new Point(20, 20),
                CanvasSize: new Size(50, 200),
                TimestampSec: 12.5),
            Actions(calls));

        Assert.Equal(LiveDetectionMarkCatalogOpenWorkflowOutcome.Ignored, result.Outcome);
        Assert.False(result.Handled);
        Assert.Empty(calls);
    }

    [Fact]
    public void ExecuteCanvasClick_pauses_and_opens_catalog_with_clock_position()
    {
        var calls = new List<string>();

        var result = LiveDetectionMarkCatalogOpenWorkflow.ExecuteCanvasClick(
            new LiveDetectionMarkCatalogCanvasClickRequest(
                IsManualMarkMode: true,
                ClickPoint: new Point(200, 100),
                CanvasSize: new Size(200, 200),
                TimestampSec: 12.5),
            Actions(calls));

        Assert.Equal(
            [
                "pause:True",
                "open:3:12.5:"
            ],
            calls);
        Assert.Equal(LiveDetectionMarkCatalogOpenWorkflowOutcome.Opened, result.Outcome);
        Assert.True(result.Handled);
    }

    [Fact]
    public void ExecuteFindingClick_pauses_and_opens_catalog_with_suggested_code()
    {
        var calls = new List<string>();

        var result = LiveDetectionMarkCatalogOpenWorkflow.ExecuteFindingClick(
            new LiveDetectionMarkCatalogFindingClickRequest(
                ClockPosition: "6",
                TimestampSec: 7.26,
                SuggestedCode: "BCA"),
            Actions(calls));

        Assert.Equal(
            [
                "pause:True",
                "open:6:7.3:BCA"
            ],
            calls);
        Assert.Equal(LiveDetectionMarkCatalogOpenWorkflowOutcome.Opened, result.Outcome);
        Assert.True(result.Handled);
    }

    private static LiveDetectionMarkCatalogOpenWorkflowActions Actions(List<string> calls)
        => new(
            SetPause: pause => calls.Add($"pause:{pause}"),
            OpenCodeCatalog: (clockPosition, timestampSec, suggestedCode) =>
                calls.Add($"open:{clockPosition}:{timestampSec:F1}:{suggestedCode}"));
}
