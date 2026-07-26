using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingSingleModelAnalysisWorkflowTests
{
    [Fact]
    public async Task ExecuteAsync_stops_with_error_when_snapshot_is_missing()
    {
        var calls = new List<string>();

        var result = await CodingSingleModelAnalysisWorkflow.ExecuteAsync(
            Request(hasEnhancedVision: false),
            Actions(
                calls,
                captureSnapshotAsync: _ =>
                {
                    calls.Add("capture");
                    return Task.FromResult<byte[]?>(Array.Empty<byte>());
                }));

        Assert.Equal(CodingSingleModelAnalysisWorkflowOutcome.NoSnapshot, result);
        Assert.Equal(
            [
                "state:Aktuellen Frame analysieren...|Schritt 1 von 3: Snapshot|pulse:True",
                "capture",
                "state:Frame nicht extrahierbar|Modell: TestModel|pulse:False"
            ],
            calls);
    }

    [Fact]
    public async Task ExecuteAsync_runs_live_detection_after_snapshot_and_osd_read()
    {
        var calls = new List<string>();

        var result = await CodingSingleModelAnalysisWorkflow.ExecuteAsync(
            Request(hasEnhancedVision: false),
            Actions(calls));

        Assert.Equal(CodingSingleModelAnalysisWorkflowOutcome.ResultShown, result);
        Assert.Equal(
            [
                "state:Aktuellen Frame analysieren...|Schritt 1 von 3: Snapshot|pulse:True",
                "capture",
                "store:12.3:3",
                "read-osd:12.3:3",
                "state:Aktuellen Frame analysieren...|Schritt 2 von 3: Inferenz (TestModel)|pulse:True",
                "live:12.3:3",
                "show:live:7.8"
            ],
            calls);
    }

    [Fact]
    public async Task ExecuteAsync_uses_enhanced_vision_when_available()
    {
        var calls = new List<string>();

        var result = await CodingSingleModelAnalysisWorkflow.ExecuteAsync(
            Request(hasEnhancedVision: true),
            Actions(calls));

        Assert.Equal(CodingSingleModelAnalysisWorkflowOutcome.ResultShown, result);
        Assert.Equal(
            [
                "state:Aktuellen Frame analysieren...|Schritt 1 von 3: Snapshot|pulse:True",
                "capture",
                "store:12.3:3",
                "read-osd:12.3:3",
                "state:Aktuellen Frame analysieren...|Schritt 2 von 3: Inferenz (TestModel)|pulse:True",
                "enhanced:12.3:3",
                "show:enhanced:7.8"
            ],
            calls);
    }

    private static CodingSingleModelAnalysisWorkflowRequest Request(bool hasEnhancedVision)
        => new(
            ActivityText: "Aktuellen Frame analysieren...",
            ModelName: "TestModel",
            CaptureTimestampSeconds: 12.3,
            HasEnhancedVision: hasEnhancedVision,
            CancellationToken: CancellationToken.None);

    private static CodingSingleModelAnalysisWorkflowActions Actions(
        List<string> calls,
        Func<CancellationToken, Task<byte[]?>>? captureSnapshotAsync = null)
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
            AnalyzeEnhancedVisionAsync: (frameBytes, timestamp, _) =>
            {
                calls.Add($"enhanced:{timestamp:F1}:{frameBytes.Length}");
                return Task.FromResult(new LiveDetection(timestamp, [], null, "enhanced"));
            },
            AnalyzeLiveDetectionAsync: (frameBytes, timestamp, _) =>
            {
                calls.Add($"live:{timestamp:F1}:{frameBytes.Length}");
                return Task.FromResult(new LiveDetection(timestamp, [], null, "live"));
            },
            ShowCodingAiResults: result => calls.Add($"show:{result.Error}:{result.MeterReading:F1}"));
}
