using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingAnalysisCommandWorkflowTests
{
    [Fact]
    public async Task ExecuteAsync_skips_when_analysis_is_already_running()
    {
        var calls = new List<string>();

        var result = await CodingAnalysisCommandWorkflow.ExecuteAsync(
            Request(disableAnalyzeButton: true),
            Actions(calls, tryBeginAnalysis: () =>
            {
                calls.Add("begin");
                return false;
            }));

        Assert.Equal(CodingAnalysisCommandWorkflowOutcome.Skipped, result.Outcome);
        Assert.Equal(["begin"], calls);
    }

    [Fact]
    public async Task ExecuteAsync_runs_single_model_after_preflight_and_restores_button()
    {
        var calls = new List<string>();

        var result = await CodingAnalysisCommandWorkflow.ExecuteAsync(
            Request(disableAnalyzeButton: true),
            Actions(calls));

        Assert.Equal(CodingAnalysisCommandWorkflowOutcome.SingleModelCompleted, result.Outcome);
        Assert.Equal(
            [
                "begin",
                "token",
                "preflight",
                "single:12.3",
                "end",
                "button:True"
            ],
            calls);
    }

    [Fact]
    public async Task ExecuteAsync_runs_multi_model_branch_without_single_model()
    {
        var calls = new List<string>();

        var result = await CodingAnalysisCommandWorkflow.ExecuteAsync(
            Request(),
            Actions(
                calls,
                runPreflight: () =>
                {
                    calls.Add("preflight");
                    return new CodingAnalysisPreflightWorkflowResult(
                        CodingAnalysisPreflightWorkflowOutcome.RunMultiModel,
                        CaptureTimestampSeconds: 45.6);
                }));

        Assert.Equal(CodingAnalysisCommandWorkflowOutcome.MultiModelCompleted, result.Outcome);
        Assert.Equal(
            [
                "begin",
                "token",
                "preflight",
                "multi:45.6",
                "end"
            ],
            calls);
    }

    [Fact]
    public async Task ExecuteAsync_stops_after_terminal_boundary_and_cleans_up()
    {
        var calls = new List<string>();

        var result = await CodingAnalysisCommandWorkflow.ExecuteAsync(
            Request(),
            Actions(
                calls,
                runPreflight: () =>
                {
                    calls.Add("preflight");
                    return new CodingAnalysisPreflightWorkflowResult(
                        CodingAnalysisPreflightWorkflowOutcome.StopAtTerminalBoundary,
                        CaptureTimestampSeconds: 78.9);
                }));

        Assert.Equal(CodingAnalysisCommandWorkflowOutcome.StoppedAtTerminalBoundary, result.Outcome);
        Assert.Equal(
            [
                "begin",
                "token",
                "preflight",
                "end"
            ],
            calls);
    }

    [Fact]
    public async Task ExecuteAsync_swallows_cancellation_but_still_cleans_up()
    {
        var calls = new List<string>();

        var result = await CodingAnalysisCommandWorkflow.ExecuteAsync(
            Request(disableAnalyzeButton: true),
            Actions(
                calls,
                runSingleModelAnalysisAsync: (_, _) =>
                {
                    calls.Add("single-cancel");
                    throw new OperationCanceledException();
                }));

        Assert.Equal(CodingAnalysisCommandWorkflowOutcome.Canceled, result.Outcome);
        Assert.Equal(
            [
                "begin",
                "token",
                "preflight",
                "single-cancel",
                "end",
                "button:True"
            ],
            calls);
    }

    [Fact]
    public async Task ExecuteAsync_reports_failures_with_compact_model_name()
    {
        var calls = new List<string>();

        var result = await CodingAnalysisCommandWorkflow.ExecuteAsync(
            Request(modelName: "models/my-model.onnx"),
            Actions(
                calls,
                runSingleModelAnalysisAsync: (_, _) =>
                {
                    calls.Add("single-error");
                    throw new InvalidOperationException("boom");
                }));

        Assert.Equal(CodingAnalysisCommandWorkflowOutcome.Failed, result.Outcome);
        Assert.Equal(
            [
                "begin",
                "token",
                "preflight",
                "single-error",
                "state:Fehler: boom|Modell: my-model.onnx",
                "end"
            ],
            calls);
    }

    private static CodingAnalysisCommandWorkflowRequest Request(
        bool disableAnalyzeButton = false,
        string? modelName = "TestModel")
        => new(
            ActivityText: "Aktuellen Frame analysieren...",
            DisableAnalyzeButton: disableAnalyzeButton,
            ModelName: modelName);

    private static CodingAnalysisCommandWorkflowActions Actions(
        List<string> calls,
        Func<bool>? tryBeginAnalysis = null,
        Func<CodingAnalysisPreflightWorkflowResult>? runPreflight = null,
        Func<double, CancellationToken, Task>? runSingleModelAnalysisAsync = null,
        Func<double, Task>? runMultiModelAnalysisAsync = null)
        => new(
            TryBeginAnalysis: tryBeginAnalysis ?? (() =>
            {
                calls.Add("begin");
                return true;
            }),
            GetAnalysisCancellationToken: () =>
            {
                calls.Add("token");
                return CancellationToken.None;
            },
            RunPreflight: runPreflight ?? (() =>
            {
                calls.Add("preflight");
                return new CodingAnalysisPreflightWorkflowResult(
                    CodingAnalysisPreflightWorkflowOutcome.ContinueSingleModel,
                    CaptureTimestampSeconds: 12.3);
            }),
            RunSingleModelAnalysisAsync: runSingleModelAnalysisAsync ?? ((timestamp, _) =>
            {
                calls.Add($"single:{timestamp:F1}");
                return Task.CompletedTask;
            }),
            RunMultiModelAnalysisAsync: runMultiModelAnalysisAsync ?? (timestamp =>
            {
                calls.Add($"multi:{timestamp:F1}");
                return Task.CompletedTask;
            }),
            SetCodingAiState: (status, _, detail) => calls.Add($"state:{status}|{detail}"),
            EndAnalysis: () => calls.Add("end"),
            SetAnalyzeButtonEnabled: enabled => calls.Add($"button:{enabled}"));
}
