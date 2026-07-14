using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.Teacher;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

public sealed class LiveDetectionConfirmationTrainingControllerTests
{
    [Fact]
    public async Task AcceptAsync_reuses_precaptured_frame_and_saves_every_pending_finding()
    {
        var calls = new List<string>();
        var detectionController = new LiveDetectionController();
        var findings = new[]
        {
            Finding("BAB"),
            Finding("BBA")
        };
        detectionController.StoreConfirmationFindings(findings, [1, 2, 3], 7);
        var writer = new RecordingAnnotationWriter(calls);
        var controller = CreateController(
            detectionController,
            writer,
            calls,
            currentSeconds: 3);

        var result = await controller.AcceptAsync();

        Assert.Equal(LiveDetectionConfirmationAcceptCommandOutcome.AcceptedHandled, result.Outcome);
        Assert.Equal(2, writer.AcceptedCalls);
        Assert.Equal([7d, 7d], writer.SavedTimestamps);
        Assert.Equal(
            [
                "accepted:BAB:3",
                "accepted:BBA:3",
                "status:\u2713 2 Befund(e) gespeichert:True",
                "resume"
            ],
            calls);
    }

    [Fact]
    public async Task CorrectAsync_uses_current_time_for_selection_and_pending_frame_time_for_training()
    {
        var calls = new List<string>();
        var detectionController = new LiveDetectionController();
        detectionController.StoreConfirmationFindings([Finding("BAB")], [4, 5], 7);
        var writer = new RecordingAnnotationWriter(calls);
        var selectedEntry = new ProtocolEntry { Code = "BCA" };
        var controller = CreateController(
            detectionController,
            writer,
            calls,
            currentSeconds: 3,
            resolveAutomaticMeter: () => 4.5,
            selectCorrection: (meter, timestamp) =>
            {
                calls.Add($"select:{meter:F2}/{timestamp:F2}");
                return selectedEntry;
            });

        var result = await controller.CorrectAsync();

        Assert.Equal(LiveDetectionConfirmationCorrectCommandOutcome.CorrectedHandled, result.Outcome);
        Assert.Equal(1, writer.CorrectedCalls);
        Assert.Equal([7d], writer.SavedTimestamps);
        Assert.Equal(
            [
                "select:4.50/3.00",
                "corrected:BAB->BCA:2",
                "status:\u2713 Training: BCA (korrigiert):True",
                "resume"
            ],
            calls);
    }

    private static LiveDetectionConfirmationTrainingController CreateController(
        LiveDetectionController detectionController,
        ILiveDetectionTrainingAnnotationWriter writer,
        List<string> calls,
        double currentSeconds,
        Func<double?>? resolveAutomaticMeter = null,
        Func<double?, double, ProtocolEntry?>? selectCorrection = null)
    {
        var timelineHost = new PlayerTimelineHost(
            () => (long)(currentSeconds * 1000),
            () => 10_000,
            _ => { });

        return new LiveDetectionConfirmationTrainingController(
            detectionController,
            timelineHost,
            writer,
            new LiveDetectionConfirmationTrainingControllerActions(
                ResolveAutomaticMeter: resolveAutomaticMeter ?? (() => null),
                SelectCorrection: selectCorrection ?? ((_, _) => null),
                CaptureCurrentFrameAsync: () =>
                {
                    calls.Add("capture");
                    return Task.FromResult<byte[]?>([9]);
                },
                ShowOsdMeterStatus: (message, success) => calls.Add($"status:{message}:{success}"),
                ResumeDetection: () => calls.Add("resume")));
    }

    private static LiveFrameFinding Finding(string code)
        => new(
            Label: "Befund",
            Severity: 3,
            PositionClock: null,
            ExtentPercent: null,
            VsaCodeHint: code);

    private sealed class RecordingAnnotationWriter(List<string> calls)
        : ILiveDetectionTrainingAnnotationWriter
    {
        public int AcceptedCalls { get; private set; }
        public int CorrectedCalls { get; private set; }
        public List<double> SavedTimestamps { get; } = [];

        public Task<TeacherAnnotation> SaveAcceptedAsync(
            byte[] frameBytes,
            LiveFrameFinding finding,
            TimeSpan videoTimestamp,
            CancellationToken ct = default)
        {
            AcceptedCalls++;
            SavedTimestamps.Add(videoTimestamp.TotalSeconds);
            calls.Add($"accepted:{finding.VsaCodeHint}:{frameBytes.Length}");
            return Task.FromResult(new TeacherAnnotation());
        }

        public Task<TeacherAnnotation> SaveCorrectedAsync(
            byte[] frameBytes,
            LiveFrameFinding sourceFinding,
            ProtocolEntry selectedEntry,
            TimeSpan videoTimestamp,
            CancellationToken ct = default)
        {
            CorrectedCalls++;
            SavedTimestamps.Add(videoTimestamp.TotalSeconds);
            calls.Add($"corrected:{sourceFinding.VsaCodeHint}->{selectedEntry.Code}:{frameBytes.Length}");
            return Task.FromResult(new TeacherAnnotation());
        }

        public Task<TeacherAnnotation?> SaveManualMarkAsync(
            byte[] frameBytes,
            ProtocolEntry selectedEntry,
            OverlayGeometry overlay,
            string? clockPosition,
            double captureMeter,
            TimeSpan videoTimestamp,
            CancellationToken ct = default)
            => Task.FromResult<TeacherAnnotation?>(new TeacherAnnotation());
    }
}
