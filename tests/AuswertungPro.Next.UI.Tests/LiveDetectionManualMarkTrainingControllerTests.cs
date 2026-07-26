using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.Teacher;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Live;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

public sealed class LiveDetectionManualMarkTrainingControllerTests
{
    [Fact]
    public async Task SaveAsync_reuses_injected_writer_and_forwards_mark_context()
    {
        var calls = new List<string>();
        var overlay = Overlay();
        var writer = new RecordingAnnotationWriter(calls);
        var controller = CreateController(
            writer,
            calls,
            selectEntry: (candidate, timestampSeconds) =>
            {
                Assert.Same(overlay, candidate);
                calls.Add($"select:{timestampSeconds:F2}");
                return new ProtocolEntry { Code = "BCA" };
            });

        var first = await controller.SaveAsync(overlay, 6, "3", [1, 2]);
        var second = await controller.SaveAsync(overlay, 8, "9", [3]);

        Assert.Equal(LiveDetectionManualMarkTrainingCommandOutcome.Saved, first.Outcome);
        Assert.Equal(LiveDetectionManualMarkTrainingCommandOutcome.Saved, second.Outcome);
        Assert.Equal(2, writer.ManualMarkCalls);
        Assert.Equal([6d, 8d], writer.SavedTimestamps);
        Assert.Equal(
            [
                "select:6.00",
                "manual:BCA:2:3:2.50",
                "status:\u2713 BCA gespeichert:True",
                "select:8.00",
                "manual:BCA:1:9:2.50",
                "status:\u2713 BCA gespeichert:True"
            ],
            calls);
    }

    [Fact]
    public async Task SaveAsync_cancelled_selection_does_not_capture_or_write()
    {
        var calls = new List<string>();
        var writer = new RecordingAnnotationWriter(calls);
        var controller = CreateController(
            writer,
            calls,
            selectEntry: (_, _) => null);

        var result = await controller.SaveAsync(Overlay(), 4, null);

        Assert.Equal(LiveDetectionManualMarkTrainingCommandOutcome.SelectionCancelled, result.Outcome);
        Assert.Equal(0, writer.ManualMarkCalls);
        Assert.Empty(calls);
    }

    private static LiveDetectionManualMarkTrainingController CreateController(
        ILiveDetectionTrainingAnnotationWriter writer,
        List<string> calls,
        Func<OverlayGeometry, double, ProtocolEntry?> selectEntry)
        => new(
            writer,
            new LiveDetectionManualMarkTrainingControllerActions(
                SelectEntry: selectEntry,
                ResolveDisplayedMeterText: () => "2.50m",
                ResolveCodingSessionService: () => null,
                CaptureCurrentFrameAsync: () =>
                {
                    calls.Add("capture");
                    return Task.FromResult<byte[]?>([9]);
                },
                RefreshCodingEvents: () => calls.Add("refresh"),
                ShowOsdMeterStatus: (message, success) => calls.Add($"status:{message}:{success}")));

    private static OverlayGeometry Overlay()
        => new()
        {
            ToolType = OverlayToolType.Rectangle,
            Points = [new NormalizedPoint(0.1, 0.2), new NormalizedPoint(0.4, 0.5)]
        };

    private sealed class RecordingAnnotationWriter(List<string> calls)
        : ILiveDetectionTrainingAnnotationWriter
    {
        public int ManualMarkCalls { get; private set; }
        public List<double> SavedTimestamps { get; } = [];

        public Task<TeacherAnnotation> SaveAcceptedAsync(
            byte[] frameBytes,
            LiveFrameFinding finding,
            TimeSpan videoTimestamp,
            CancellationToken ct = default)
            => Task.FromResult(new TeacherAnnotation());

        public Task<TeacherAnnotation> SaveCorrectedAsync(
            byte[] frameBytes,
            LiveFrameFinding sourceFinding,
            ProtocolEntry selectedEntry,
            TimeSpan videoTimestamp,
            CancellationToken ct = default)
            => Task.FromResult(new TeacherAnnotation());

        public Task<TeacherAnnotation?> SaveManualMarkAsync(
            byte[] frameBytes,
            ProtocolEntry selectedEntry,
            OverlayGeometry overlay,
            string? clockPosition,
            double captureMeter,
            TimeSpan videoTimestamp,
            CancellationToken ct = default)
        {
            ManualMarkCalls++;
            SavedTimestamps.Add(videoTimestamp.TotalSeconds);
            calls.Add(
                $"manual:{selectedEntry.Code}:{frameBytes.Length}:{clockPosition}:{captureMeter:F2}");
            return Task.FromResult<TeacherAnnotation?>(new TeacherAnnotation());
        }
    }
}
