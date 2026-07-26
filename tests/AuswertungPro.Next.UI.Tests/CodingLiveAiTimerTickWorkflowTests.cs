using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingLiveAiTimerTickWorkflowTests
{
    [Fact]
    public async Task ExecuteAsync_skips_when_tick_policy_blocks_analysis()
    {
        var calls = new List<string>();

        var result = await CodingLiveAiTimerTickWorkflow.ExecuteAsync(
            new CodingLiveAiTimerTickWorkflowRequest(
                IsClosing: false,
                HasPlayer: true,
                HasLiveDetection: true,
                SessionState: CodingSessionState.WaitingForUserInput,
                IsPlayerPlaying: true),
            Actions(calls));

        Assert.Equal(CodingLiveAiTimerTickWorkflowOutcome.Skipped, result);
        Assert.Empty(calls);
    }

    [Fact]
    public async Task ExecuteAsync_runs_analysis_when_tick_policy_allows_it()
    {
        var calls = new List<string>();

        var result = await CodingLiveAiTimerTickWorkflow.ExecuteAsync(
            new CodingLiveAiTimerTickWorkflowRequest(
                IsClosing: false,
                HasPlayer: true,
                HasLiveDetection: true,
                SessionState: CodingSessionState.Running,
                IsPlayerPlaying: true),
            Actions(calls));

        Assert.Equal(CodingLiveAiTimerTickWorkflowOutcome.Analyzed, result);
        Assert.Equal(["analyze"], calls);
    }

    [Fact]
    public async Task ExecuteAsync_logs_error_without_throwing_when_analysis_fails()
    {
        var calls = new List<string>();

        var result = await CodingLiveAiTimerTickWorkflow.ExecuteAsync(
            new CodingLiveAiTimerTickWorkflowRequest(
                IsClosing: false,
                HasPlayer: true,
                HasLiveDetection: true,
                SessionState: CodingSessionState.Running,
                IsPlayerPlaying: true),
            Actions(
                calls,
                runAnalysisAsync: () =>
                {
                    calls.Add("analyze");
                    throw new InvalidOperationException("Pipeline down");
                }));

        Assert.Equal(CodingLiveAiTimerTickWorkflowOutcome.ErrorLogged, result);
        Assert.Equal(["analyze", "trace:Pipeline down"], calls);
    }

    private static CodingLiveAiTimerTickWorkflowActions Actions(
        List<string> calls,
        Func<Task>? runAnalysisAsync = null)
        => new(
            RunAnalysisAsync: runAnalysisAsync ?? (() =>
            {
                calls.Add("analyze");
                return Task.CompletedTask;
            }),
            TraceError: message => calls.Add($"trace:{message}"));
}
