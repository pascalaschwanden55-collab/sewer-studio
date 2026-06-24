using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingOsdMeterReadWorkflowTests
{
    [Fact]
    public async Task ExecuteAsync_skips_empty_frame_without_reading()
    {
        var calls = new List<string>();

        var result = await CodingOsdMeterReadWorkflow.ExecuteAsync(
            new CodingOsdMeterReadWorkflowRequest(
                PngBytes: [],
                FrameTimestampSeconds: 8.5,
                LastMeter: 1.2,
                LastTimestampSeconds: 7.5,
                CancellationToken: CancellationToken.None),
            Actions(calls));

        Assert.Equal(CodingOsdMeterReadWorkflowOutcome.NoFrame, result.Outcome);
        Assert.Null(result.Meter);
        Assert.Empty(calls);
    }

    [Fact]
    public async Task ExecuteAsync_applies_state_for_accepted_meter()
    {
        var calls = new List<string>();

        var result = await CodingOsdMeterReadWorkflow.ExecuteAsync(
            new CodingOsdMeterReadWorkflowRequest(
                PngBytes: [1, 2, 3],
                FrameTimestampSeconds: 8.5,
                LastMeter: 1.2,
                LastTimestampSeconds: 7.5,
                CancellationToken: CancellationToken.None),
            Actions(
                calls,
                readMeterAsync: (pngBytes, frameTimestamp, lastMeter, lastTimestamp, _) =>
                {
                    calls.Add(
                        $"read:{pngBytes.Length}:{frameTimestamp:F1}:{lastMeter:F1}:{lastTimestamp:F1}");
                    return Task.FromResult(CodingOsdMeterReadResult.Accepted(
                        12.345,
                        "12.345",
                        candidate: 12.345,
                        recentMeter: 1.2));
                }));

        Assert.Equal(CodingOsdMeterReadWorkflowOutcome.Accepted, result.Outcome);
        Assert.Equal(12.345, result.Meter);
        Assert.Equal(
            [
                "read:3:8.5:1.2:7.5",
                "apply:12.345:8.5:12.35m (OSD)"
            ],
            calls);
    }

    [Fact]
    public async Task ExecuteAsync_logs_rejected_meter_candidate()
    {
        var calls = new List<string>();

        var result = await CodingOsdMeterReadWorkflow.ExecuteAsync(
            new CodingOsdMeterReadWorkflowRequest(
                PngBytes: [1],
                FrameTimestampSeconds: 8.5,
                LastMeter: 1.2,
                LastTimestampSeconds: 7.5,
                CancellationToken: CancellationToken.None),
            Actions(
                calls,
                readMeterAsync: (_, _, _, _, _) =>
                    Task.FromResult(CodingOsdMeterReadResult.Rejected(
                        "9.999",
                        candidate: 9.999,
                        recentMeter: 1.2))));

        Assert.Equal(CodingOsdMeterReadWorkflowOutcome.NoMeter, result.Outcome);
        Assert.Null(result.Meter);
        Assert.Equal(
            [
                "trace:[OSD] Meter verworfen. Raw='9.999', Candidate=10.00, Last=1.20"
            ],
            calls);
    }

    [Fact]
    public async Task ExecuteAsync_logs_failed_read_result()
    {
        var calls = new List<string>();

        var result = await CodingOsdMeterReadWorkflow.ExecuteAsync(
            new CodingOsdMeterReadWorkflowRequest(
                PngBytes: [1],
                FrameTimestampSeconds: null,
                LastMeter: null,
                LastTimestampSeconds: null,
                CancellationToken: CancellationToken.None),
            Actions(
                calls,
                readMeterAsync: (_, _, _, _, _) =>
                    Task.FromResult(CodingOsdMeterReadResult.Failed("timeout"))));

        Assert.Equal(CodingOsdMeterReadWorkflowOutcome.NoMeter, result.Outcome);
        Assert.Null(result.Meter);
        Assert.Equal(["trace:[OSD] Frame-Meter nicht lesbar: timeout"], calls);
    }

    [Fact]
    public async Task ExecuteAsync_rethrows_caller_cancellation()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            CodingOsdMeterReadWorkflow.ExecuteAsync(
                new CodingOsdMeterReadWorkflowRequest(
                    PngBytes: [1],
                    FrameTimestampSeconds: null,
                    LastMeter: null,
                    LastTimestampSeconds: null,
                    CancellationToken: cts.Token),
                Actions(
                    [],
                    readMeterAsync: (_, _, _, _, ct) => throw new OperationCanceledException(ct))));
    }

    private static CodingOsdMeterReadWorkflowActions Actions(
        List<string> calls,
        Func<byte[], double?, double?, double?, CancellationToken, Task<CodingOsdMeterReadResult>>? readMeterAsync = null)
        => new(
            ReadMeterAsync: readMeterAsync ?? ((_, _, _, _, _) =>
            {
                calls.Add("read");
                return Task.FromResult(CodingOsdMeterReadResult.Empty);
            }),
            ApplyMeterState: state => calls.Add(
                $"apply:{state.Meter:F3}:{state.TimestampSeconds:F1}:{state.BadgeText}"),
            Trace: message => calls.Add($"trace:{message}"));
}
