using AuswertungPro.Next.Infrastructure.Ai.Pipeline;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingMultiModelInferenceWorkflowTests
{
    [Fact]
    public async Task ExecuteAsync_resolves_classifier_input_before_analysis_and_result_handling()
    {
        var calls = new List<string>();

        var result = await CodingMultiModelInferenceWorkflow.ExecuteAsync(
            Request(nominalDiameterMm: 600, endMeter: 20),
            Actions(calls));

        Assert.Equal(CodingMultiModelInferenceWorkflowOutcome.ResultHandled, result.Outcome);
        Assert.Equal(
            [
                "resolve:12.3:7.8",
                "analyze:600:4.5:20.0:3",
                "boundary:12.3:7.8",
                "structural:12.3:7.8",
                "result"
            ],
            calls);
    }

    [Fact]
    public async Task ExecuteAsync_shows_error_without_boundary_or_result_handling()
    {
        var calls = new List<string>();

        var result = await CodingMultiModelInferenceWorkflow.ExecuteAsync(
            Request(nominalDiameterMm: null, endMeter: null),
            Actions(
                calls,
                analyzeFrameAsync: (_, _, _) =>
                {
                    calls.Add("analyze-error");
                    return Task.FromResult(SingleFrameResult.Empty("Sidecar down"));
                }));

        Assert.Equal(CodingMultiModelInferenceWorkflowOutcome.Error, result.Outcome);
        Assert.Equal(
            [
                "resolve:12.3:7.8",
                "analyze-error",
                "state:Fehler: Sidecar down|Multi-Model|pulse:False"
            ],
            calls);
    }

    [Fact]
    public async Task ExecuteAsync_stops_after_boundary_classifier_when_handled()
    {
        var calls = new List<string>();

        var result = await CodingMultiModelInferenceWorkflow.ExecuteAsync(
            Request(nominalDiameterMm: 600, endMeter: 20),
            Actions(calls, tryHandleBoundaryAsync: (_, _, _) =>
            {
                calls.Add("boundary");
                return Task.FromResult(true);
            }));

        Assert.Equal(CodingMultiModelInferenceWorkflowOutcome.BoundaryHandled, result.Outcome);
        Assert.Equal(["resolve:12.3:7.8", "analyze:600:4.5:20.0:3", "boundary"], calls);
    }

    [Fact]
    public async Task ExecuteAsync_awaits_boundary_classifier_before_returning()
    {
        var calls = new List<string>();
        var boundaryCompletion = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        var pending = CodingMultiModelInferenceWorkflow.ExecuteAsync(
            Request(nominalDiameterMm: 600, endMeter: 20),
            Actions(calls, tryHandleBoundaryAsync: (_, _, _) =>
            {
                calls.Add("boundary-pending");
                return boundaryCompletion.Task;
            }));

        Assert.False(pending.IsCompleted);
        Assert.Equal(["resolve:12.3:7.8", "analyze:600:4.5:20.0:3", "boundary-pending"], calls);

        boundaryCompletion.SetResult(true);
        var result = await pending;

        Assert.Equal(CodingMultiModelInferenceWorkflowOutcome.BoundaryHandled, result.Outcome);
        Assert.Equal(["resolve:12.3:7.8", "analyze:600:4.5:20.0:3", "boundary-pending"], calls);
    }

    [Fact]
    public async Task ExecuteAsync_stops_after_structural_classifier_when_handled()
    {
        var calls = new List<string>();

        var result = await CodingMultiModelInferenceWorkflow.ExecuteAsync(
            Request(nominalDiameterMm: 600, endMeter: 20),
            Actions(calls, tryHandleStructural: (_, _, _) =>
            {
                calls.Add("structural");
                return true;
            }));

        Assert.Equal(CodingMultiModelInferenceWorkflowOutcome.StructuralHandled, result.Outcome);
        Assert.Equal(
            ["resolve:12.3:7.8", "analyze:600:4.5:20.0:3", "boundary:12.3:7.8", "structural"],
            calls);
    }

    private static CodingMultiModelInferenceWorkflowRequest Request(
        int? nominalDiameterMm,
        double? endMeter)
        => new(
            ActivityText: "Multi analysieren",
            FrameBytes: [1, 2, 3],
            CaptureTimestampSeconds: 12.3,
            FrameOsdMeter: 7.8,
            NominalDiameterMm: nominalDiameterMm,
            EndMeter: endMeter,
            CancellationToken: CancellationToken.None);

    private static CodingMultiModelInferenceWorkflowActions Actions(
        List<string> calls,
        Func<byte[], CodingMultiModelClassifierInput, CancellationToken, Task<SingleFrameResult>>? analyzeFrameAsync = null,
        Func<SingleFrameResult, double, double?, Task<bool>>? tryHandleBoundaryAsync = null,
        Func<SingleFrameResult, double, double?, bool>? tryHandleStructural = null)
        => new(
            ResolveCurrentMeter: (timestamp, meter) =>
            {
                calls.Add($"resolve:{timestamp:F1}:{meter:F1}");
                return 4.5;
            },
            AnalyzeFrameAsync: analyzeFrameAsync ?? ((frameBytes, classifierInput, _) =>
            {
                calls.Add(
                    $"analyze:{classifierInput.NominalDiameterMm}:{classifierInput.CurrentMeter:F1}:{classifierInput.ReachLength:F1}:{frameBytes.Length}");
                return Task.FromResult(SingleFrameResult.Empty());
            }),
            SetCodingAiState: (status, _, detail, pulse) => calls.Add($"state:{status}|{detail}|pulse:{pulse}"),
            TryHandleBoundaryClassifierResultAsync: tryHandleBoundaryAsync ?? ((_, timestamp, meter) =>
            {
                calls.Add($"boundary:{timestamp:F1}:{meter:F1}");
                return Task.FromResult(false);
            }),
            TryHandleStructuralClassifierResult: tryHandleStructural ?? ((_, timestamp, meter) =>
            {
                calls.Add($"structural:{timestamp:F1}:{meter:F1}");
                return false;
            }),
            HandleAnalysisResult: _ => calls.Add("result"));
}
