using System.Windows.Media;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

public sealed class LiveDetectionResultWorkflowTests
{
    [Fact]
    public void Execute_ignores_result_when_window_is_closing()
    {
        var result = LiveDetectionResultWorkflow.Execute(
            new LiveDetectionResultWorkflowRequest(
                Result: Detection([Finding("Riss", 3)]),
                Snapshot: [1, 2, 3],
                IsClosing: true,
                IsPlaybackDisposed: false,
                IsDetecting: true,
                ModelName: "models/qwen2.5-vl:7b"),
            NoActions());

        Assert.Equal(LiveDetectionResultWorkflowOutcome.Ignored, result.Outcome);
        Assert.False(result.Handled);
    }

    [Fact]
    public void Execute_applies_overlay_and_status_without_confirmation_for_low_findings()
    {
        var calls = new List<string>();
        var detection = Detection([Finding("Hinweis", 1)]);

        var result = LiveDetectionResultWorkflow.Execute(
            new LiveDetectionResultWorkflowRequest(
                detection,
                Snapshot: [1, 2, 3],
                IsClosing: false,
                IsPlaybackDisposed: false,
                IsDetecting: true,
                ModelName: "models/qwen2.5-vl:7b"),
            Actions(
                applyDetectionResult: applied =>
                {
                    calls.Add("apply");
                    Assert.Same(detection, applied);
                },
                renderDetectionOverlay: (findings, timestamp) =>
                    calls.Add($"render:{findings.Count}:{timestamp:F1}"),
                updateDetectionStatus: updated =>
                {
                    calls.Add("status");
                    Assert.Same(detection, updated);
                },
                setLiveDetectionBadge: (status, color, stage) =>
                {
                    calls.Add($"badge:{status}|{stage}");
                    Assert.Equal(PlayerStatusColors.Success, color);
                },
                storeFindings: (_, _, _) => throw new InvalidOperationException("No confirmation should be buffered."),
                showDetectionConfirmation: _ => throw new InvalidOperationException("No confirmation should be shown.")));

        Assert.Equal(
            ["apply", "render:1:12.5", "status", "badge:KI aktiv|qwen2.5-vl:7b | Overlay"],
            calls);
        Assert.Equal(LiveDetectionResultWorkflowOutcome.OverlayShown, result.Outcome);
        Assert.True(result.Handled);
    }

    [Fact]
    public void Execute_buffers_significant_findings_and_requests_confirmation_after_overlay()
    {
        var calls = new List<string>();
        var low = Finding("Hinweis", 1);
        var significant = Finding("Riss", 3);
        var snapshot = new byte[] { 4, 5, 6 };

        var result = LiveDetectionResultWorkflow.Execute(
            new LiveDetectionResultWorkflowRequest(
                Detection([low, significant]),
                Snapshot: snapshot,
                IsClosing: false,
                IsPlaybackDisposed: false,
                IsDetecting: true,
                ModelName: "models/qwen2.5-vl:7b"),
            Actions(
                applyDetectionResult: _ => calls.Add("apply"),
                renderDetectionOverlay: (findings, timestamp) =>
                    calls.Add($"render:{findings.Count}:{timestamp:F1}"),
                updateDetectionStatus: _ => calls.Add("status"),
                setLiveDetectionBadge: (status, color, stage) =>
                {
                    calls.Add($"badge:{status}|{stage}");
                    if (status == "Befund erkannt")
                        Assert.Equal(PlayerStatusColors.Warning, color);
                },
                storeFindings: (findings, frameBytes, timestamp) =>
                {
                    calls.Add($"store:{findings.Count}:{timestamp:F1}");
                    Assert.Equal([significant], findings);
                    Assert.Same(snapshot, frameBytes);
                },
                showDetectionConfirmation: findings =>
                {
                    calls.Add($"confirm:{findings.Count}");
                    Assert.Equal([significant], findings);
                }));

        Assert.Equal(
            [
                "apply",
                "render:2:12.5",
                "status",
                "badge:KI aktiv|qwen2.5-vl:7b | Overlay",
                "store:1:12.5",
                "confirm:1",
                "badge:Befund erkannt|qwen2.5-vl:7b | Warte auf Bestaetigung"
            ],
            calls);
        Assert.Equal(LiveDetectionResultWorkflowOutcome.ConfirmationRequested, result.Outcome);
        Assert.True(result.Handled);
    }

    private static LiveDetectionResultWorkflowActions NoActions()
        => Actions(
            applyDetectionResult: _ => throw new InvalidOperationException("Apply should not run."),
            renderDetectionOverlay: (_, _) => throw new InvalidOperationException("Render should not run."),
            updateDetectionStatus: _ => throw new InvalidOperationException("Status should not run."),
            setLiveDetectionBadge: (_, _, _) => throw new InvalidOperationException("Badge should not run."),
            storeFindings: (_, _, _) => throw new InvalidOperationException("Store should not run."),
            showDetectionConfirmation: _ => throw new InvalidOperationException("Confirmation should not run."));

    private static LiveDetectionResultWorkflowActions Actions(
        Action<LiveDetection>? applyDetectionResult = null,
        Action<IReadOnlyList<LiveFrameFinding>, double>? renderDetectionOverlay = null,
        Action<LiveDetection>? updateDetectionStatus = null,
        Action<string, Color, string?>? setLiveDetectionBadge = null,
        Action<IReadOnlyList<LiveFrameFinding>, byte[], double>? storeFindings = null,
        Action<IReadOnlyList<LiveFrameFinding>>? showDetectionConfirmation = null)
        => new(
            ApplyDetectionResult: applyDetectionResult ?? (_ => { }),
            RenderDetectionOverlay: renderDetectionOverlay ?? ((_, _) => { }),
            UpdateDetectionStatus: updateDetectionStatus ?? (_ => { }),
            SetLiveDetectionBadge: setLiveDetectionBadge ?? ((_, _, _) => { }),
            StoreFindings: storeFindings ?? ((_, _, _) => { }),
            ShowDetectionConfirmation: showDetectionConfirmation ?? (_ => { }));

    private static LiveDetection Detection(IReadOnlyList<LiveFrameFinding> findings)
        => new(
            TimestampSeconds: 12.5,
            Findings: findings,
            MeterReading: null,
            Error: null);

    private static LiveFrameFinding Finding(string label, int severity)
        => new(label, severity, PositionClock: null, ExtentPercent: null);
}
