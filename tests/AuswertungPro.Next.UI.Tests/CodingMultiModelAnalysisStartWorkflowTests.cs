using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingMultiModelAnalysisStartWorkflowTests
{
    [Fact]
    public async Task ExecuteAsync_stops_with_error_when_snapshot_is_missing()
    {
        var calls = new List<string>();

        var result = await CodingMultiModelAnalysisStartWorkflow.ExecuteAsync(
            Request(),
            Actions(
                calls,
                captureSnapshotAsync: _ =>
                {
                    calls.Add("capture");
                    return Task.FromResult<byte[]?>([]);
                }));

        Assert.Equal(CodingMultiModelAnalysisStartWorkflowOutcome.NoSnapshot, result.Outcome);
        Assert.Null(result.FrameBytes);
        Assert.Equal(
            [
                "state:Multi analysieren|Schritt 1 von 4: Snapshot|pulse:True",
                "capture",
                "state:Frame nicht extrahierbar|Multi-Model|pulse:False"
            ],
            calls);
    }

    [Fact]
    public async Task ExecuteAsync_skips_when_frame_readiness_is_not_clean()
    {
        var calls = new List<string>();

        var result = await CodingMultiModelAnalysisStartWorkflow.ExecuteAsync(
            Request(),
            Actions(calls, isFrameReady: () =>
            {
                calls.Add("is-ready");
                return false;
            }));

        Assert.Equal(CodingMultiModelAnalysisStartWorkflowOutcome.FrameNotReady, result.Outcome);
        Assert.Equal(
            [
                "state:Multi analysieren|Schritt 1 von 4: Snapshot|pulse:True",
                "capture",
                "store:12.3:3",
                "read-osd:12.3:3",
                "readiness:12.3:7.8",
                "is-ready",
                "state:Dateneinblendung erkannt - uebersprungen|Warte auf sauberes Videobild...|pulse:False"
            ],
            calls);
    }

    [Fact]
    public async Task ExecuteAsync_returns_frame_and_meter_when_ready_for_inference()
    {
        var calls = new List<string>();

        var result = await CodingMultiModelAnalysisStartWorkflow.ExecuteAsync(
            Request(),
            Actions(calls));

        Assert.Equal(CodingMultiModelAnalysisStartWorkflowOutcome.Ready, result.Outcome);
        Assert.Equal([1, 2, 3], result.FrameBytes);
        Assert.Equal(7.8, result.FrameOsdMeter);
        Assert.Equal(
            [
                "state:Multi analysieren|Schritt 1 von 4: Snapshot|pulse:True",
                "capture",
                "store:12.3:3",
                "read-osd:12.3:3",
                "readiness:12.3:7.8",
                "is-ready",
                "state:Multi analysieren|Schritt 2 von 4: YOLO und DINO|pulse:True"
            ],
            calls);
    }

    private static CodingMultiModelAnalysisStartWorkflowRequest Request()
        => new(
            ActivityText: "Multi analysieren",
            CaptureTimestampSeconds: 12.3,
            CancellationToken: CancellationToken.None);

    private static CodingMultiModelAnalysisStartWorkflowActions Actions(
        List<string> calls,
        Func<CancellationToken, Task<byte[]?>>? captureSnapshotAsync = null,
        Func<bool>? isFrameReady = null)
        => new(
            SetCodingAiState: (status, _, detail, pulse) => calls.Add($"state:{status}|{detail}|pulse:{pulse}"),
            CaptureSnapshotAsync: captureSnapshotAsync ?? (_ =>
            {
                calls.Add("capture");
                return Task.FromResult<byte[]?>([1, 2, 3]);
            }),
            StoreAnalyzedFrame: (frameBytes, timestamp) => calls.Add($"store:{timestamp:F1}:{frameBytes.Length}"),
            TryReadAnalyzedFrameOsdMeterAsync: (frameBytes, timestamp, _) =>
            {
                calls.Add($"read-osd:{timestamp:F1}:{frameBytes.Length}");
                return Task.FromResult<double?>(7.8);
            },
            UpdateFrameReadiness: result => calls.Add($"readiness:{result.TimestampSeconds:F1}:{result.MeterReading:F1}"),
            IsFrameReady: isFrameReady ?? (() =>
            {
                calls.Add("is-ready");
                return true;
            }));
}
