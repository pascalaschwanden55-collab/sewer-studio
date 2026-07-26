using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingAnalysisPreflightWorkflowTests
{
    [Fact]
    public void Execute_disables_analyze_button_before_resolving_frame_position()
    {
        var calls = new List<string>();

        var result = CodingAnalysisPreflightWorkflow.Execute(
            new CodingAnalysisPreflightWorkflowRequest(
                DisableAnalyzeButton: true,
                UseMultiModel: false,
                HasMultiModel: false),
            Actions(calls));

        Assert.Equal(CodingAnalysisPreflightWorkflowOutcome.ContinueSingleModel, result.Outcome);
        Assert.Equal(12.3, result.CaptureTimestampSeconds);
        Assert.Equal(["button:False", "resolve-frame", "terminal-check:12.3"], calls);
    }

    [Fact]
    public void Execute_stops_at_terminal_boundary_before_multimodel_branch()
    {
        var calls = new List<string>();

        var result = CodingAnalysisPreflightWorkflow.Execute(
            new CodingAnalysisPreflightWorkflowRequest(
                DisableAnalyzeButton: true,
                UseMultiModel: true,
                HasMultiModel: true),
            Actions(calls, isAfterTerminalBoundary: _ => true));

        Assert.Equal(CodingAnalysisPreflightWorkflowOutcome.StopAtTerminalBoundary, result.Outcome);
        Assert.Equal(
            [
                "button:False",
                "resolve-frame",
                "terminal-check:12.3",
                "clear-overlays",
                "clear-masks",
                "state:Rohrende erreicht - KI-Analyse gestoppt|Codierung abgeschlossen"
            ],
            calls);
    }

    [Fact]
    public void Execute_requests_multimodel_when_enabled_after_terminal_check()
    {
        var calls = new List<string>();

        var result = CodingAnalysisPreflightWorkflow.Execute(
            new CodingAnalysisPreflightWorkflowRequest(
                DisableAnalyzeButton: false,
                UseMultiModel: true,
                HasMultiModel: true),
            Actions(calls));

        Assert.Equal(CodingAnalysisPreflightWorkflowOutcome.RunMultiModel, result.Outcome);
        Assert.Equal(12.3, result.CaptureTimestampSeconds);
        Assert.Equal(["resolve-frame", "terminal-check:12.3"], calls);
    }

    [Fact]
    public void Execute_continues_single_model_when_multimodel_runtime_is_missing()
    {
        var calls = new List<string>();

        var result = CodingAnalysisPreflightWorkflow.Execute(
            new CodingAnalysisPreflightWorkflowRequest(
                DisableAnalyzeButton: false,
                UseMultiModel: true,
                HasMultiModel: false),
            Actions(calls));

        Assert.Equal(CodingAnalysisPreflightWorkflowOutcome.ContinueSingleModel, result.Outcome);
        Assert.Equal(["resolve-frame", "terminal-check:12.3"], calls);
    }

    private static CodingAnalysisPreflightWorkflowActions Actions(
        List<string> calls,
        Func<CodingAnalysisFramePosition, bool>? isAfterTerminalBoundary = null)
        => new(
            SetAnalyzeButtonEnabled: enabled => calls.Add($"button:{enabled}"),
            ResolveFramePosition: () =>
            {
                calls.Add("resolve-frame");
                return new CodingAnalysisFramePosition(
                    CaptureTimestampSeconds: 12.3,
                    CurrentMeter: 4.5,
                    VideoTime: TimeSpan.FromSeconds(12.3));
            },
            IsAfterTerminalBoundary: framePosition =>
            {
                calls.Add($"terminal-check:{framePosition.CaptureTimestampSeconds:F1}");
                return isAfterTerminalBoundary?.Invoke(framePosition) ?? false;
            },
            ClearDetectionOverlays: () => calls.Add("clear-overlays"),
            ClearSamMasks: () => calls.Add("clear-masks"),
            SetCodingAiState: (status, _, detail) => calls.Add($"state:{status}|{detail}"));
}
