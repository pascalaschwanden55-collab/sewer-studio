using AuswertungPro.Next.Infrastructure.Ai.Pipeline;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingBoundaryClassifierCommandWorkflowTests
{
    [Fact]
    public async Task ExecuteAsync_skips_non_boundary_without_resolving_meter()
    {
        var result = await CodingBoundaryClassifierCommandWorkflow.ExecuteAsync(
            Request(result: Result("BCA")),
            NoActions());

        Assert.Equal(CodingBoundaryClassifierCommandOutcome.Skipped, result.Outcome);
        Assert.Null(result.Result);
        Assert.False(result.Handled);
    }

    [Fact]
    public async Task ExecuteAsync_skips_without_ready_coding_session()
    {
        var result = await CodingBoundaryClassifierCommandWorkflow.ExecuteAsync(
            Request(hasCodingViewModel: false, hasCodingSessionService: true),
            NoActions());

        Assert.Equal(CodingBoundaryClassifierCommandOutcome.Skipped, result.Outcome);
        Assert.Null(result.Result);
        Assert.False(result.Handled);

        result = await CodingBoundaryClassifierCommandWorkflow.ExecuteAsync(
            Request(hasCodingViewModel: true, hasCodingSessionService: false),
            NoActions());

        Assert.Equal(CodingBoundaryClassifierCommandOutcome.Skipped, result.Outcome);
        Assert.Null(result.Result);
        Assert.False(result.Handled);
    }

    [Fact]
    public async Task ExecuteAsync_resolves_meter_and_delegates_with_current_video_time()
    {
        var calls = new List<string>();
        var mmResult = Result("BCD");
        var frameBytes = new byte[] { 1, 2, 3 };
        CodingBoundaryClassifierResultWorkflowRequest? delegated = null;
        var workflowResult = new CodingBoundaryClassifierResultWorkflowResult(
            CodingBoundaryClassifierResultWorkflowOutcome.BoundaryHandled);

        var result = await CodingBoundaryClassifierCommandWorkflow.ExecuteAsync(
            new CodingBoundaryClassifierCommandRequest(
                Result: mmResult,
                HasCodingViewModel: true,
                HasCodingSessionService: true,
                CaptureTimestampSeconds: 4.5,
                FrameOsdMeter: 7.25,
                CurrentVideoTime: TimeSpan.FromSeconds(21),
                FallbackVideoTime: TimeSpan.FromSeconds(99),
                EndMeter: 120.0,
                ExistingEventCount: 5,
                AnalyzedFrameBytes: frameBytes),
            new CodingBoundaryClassifierCommandActions(
                ResolveMeterForFrame: (timestamp, osdMeter) =>
                {
                    calls.Add("resolve");
                    Assert.Equal(4.5, timestamp);
                    Assert.Equal(7.25, osdMeter);
                    return 12.3;
                },
                ExecuteResultWorkflowAsync: request =>
                {
                    calls.Add("execute");
                    delegated = request;
                    return Task.FromResult(workflowResult);
                }));

        Assert.Equal(CodingBoundaryClassifierCommandOutcome.Executed, result.Outcome);
        Assert.True(result.Handled);
        Assert.Equal(["resolve", "execute"], calls);
        Assert.Equal(12.3, result.Meter);
        Assert.Equal(TimeSpan.FromSeconds(21), result.VideoTime);
        Assert.Same(workflowResult, result.Result);
        Assert.NotNull(delegated);
        Assert.Same(mmResult, delegated.Result);
        Assert.Equal(12.3, delegated.Meter);
        Assert.Equal(120.0, delegated.EndMeter);
        Assert.Equal(TimeSpan.FromSeconds(21), delegated.VideoTime);
        Assert.Equal(5, delegated.ExistingEventCount);
        Assert.Same(frameBytes, delegated.AnalyzedFrameBytes);
    }

    [Fact]
    public async Task ExecuteAsync_uses_capture_timestamp_when_current_video_time_is_missing()
    {
        var result = await CodingBoundaryClassifierCommandWorkflow.ExecuteAsync(
            new CodingBoundaryClassifierCommandRequest(
                Result: Result("BCE"),
                HasCodingViewModel: true,
                HasCodingSessionService: true,
                CaptureTimestampSeconds: 6.75,
                FrameOsdMeter: null,
                CurrentVideoTime: null,
                FallbackVideoTime: TimeSpan.FromSeconds(6.75),
                EndMeter: 100,
                ExistingEventCount: 2,
                AnalyzedFrameBytes: null),
            new CodingBoundaryClassifierCommandActions(
                ResolveMeterForFrame: (_, _) => 3.5,
                ExecuteResultWorkflowAsync: _ => Task.FromResult(
                    new CodingBoundaryClassifierResultWorkflowResult(
                        CodingBoundaryClassifierResultWorkflowOutcome.NotHandled))));

        Assert.Equal(CodingBoundaryClassifierCommandOutcome.Executed, result.Outcome);
        Assert.False(result.Handled);
        Assert.Equal(3.5, result.Meter);
        Assert.Equal(TimeSpan.FromSeconds(6.75), result.VideoTime);
    }

    private static CodingBoundaryClassifierCommandRequest Request(
        SingleFrameResult? result = null,
        bool hasCodingViewModel = true,
        bool hasCodingSessionService = true,
        double captureTimestampSeconds = 1,
        TimeSpan? currentVideoTime = null,
        TimeSpan? fallbackVideoTime = null)
        => new(
            Result: result ?? Result("BCD"),
            HasCodingViewModel: hasCodingViewModel,
            HasCodingSessionService: hasCodingSessionService,
            CaptureTimestampSeconds: captureTimestampSeconds,
            FrameOsdMeter: null,
            CurrentVideoTime: currentVideoTime ?? TimeSpan.FromSeconds(8),
            FallbackVideoTime: fallbackVideoTime ?? TimeSpan.FromSeconds(captureTimestampSeconds),
            EndMeter: 100,
            ExistingEventCount: 0,
            AnalyzedFrameBytes: null);

    private static CodingBoundaryClassifierCommandActions NoActions()
        => new(
            ResolveMeterForFrame: (_, _) => throw new InvalidOperationException("Meter should not be resolved."),
            ExecuteResultWorkflowAsync: _ => throw new InvalidOperationException("Workflow should not run."));

    private static SingleFrameResult Result(string? code)
        => new(
            IsRelevant: true,
            DinoDetections: [],
            SamResponse: null,
            QuantifiedMasks: [],
            YoloTimeMs: 0,
            DinoTimeMs: 0,
            SamTimeMs: 0,
            Error: null,
            ClassifierCode: code,
            ClassifierConfidence: 0.8);
}
