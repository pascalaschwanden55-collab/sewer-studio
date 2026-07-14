using System.Windows;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.Teacher;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Player;
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.Tests;

public sealed class LiveDetectionTrainingControllerSetFactoryTests
{
    [Fact]
    public void Create_wires_confirmation_and_manual_mark_to_the_same_writer()
    {
        StaTestRunner.Run(() =>
        {
            var owner = new Window();
            var detectionController = new LiveDetectionController();
            detectionController.StoreConfirmationFindings([Finding()], [1, 2], 6);
            var timelineHost = new PlayerTimelineHost(() => 4_000, () => 10_000, _ => { });
            var selectedEntry = new ProtocolEntry { Code = "BCA" };
            var writer = new RecordingAnnotationWriter();
            var selectionService = new CodingCodeExplorerWorkflowService(
                createViewModel: (_, _, _) => null!,
                showDialog: (_, _, _, _, _) => new VsaCodeExplorerDialogResult(true, selectedEntry));

            var controllers = LiveDetectionTrainingControllerSetFactory.Create(
                new LiveDetectionTrainingControllerSetDependencies(
                    DetectionController: detectionController,
                    TimelineHost: timelineHost,
                    Owner: owner,
                    VideoPath: "video.mp4",
                    ResolveAutomaticMeter: () => 2.5,
                    CreateCorrectionViewModel: (_, _, _) => null!,
                    CreateManualSelectionActions: () => new CodingCodeExplorerSeedSelectionWorkflowActions(
                        CreateService: () => selectionService),
                    ResolveDisplayedMeterText: () => "2.50m",
                    ResolveCodingSessionService: () => null,
                    CaptureCurrentFrameAsync: () => Task.FromResult<byte[]?>([9]),
                    RefreshCodingEvents: () => { },
                    ShowOsdMeterStatus: (_, _) => { },
                    ResumeDetection: () => { }),
                writer);

            var accepted = controllers.Confirmation.AcceptAsync().GetAwaiter().GetResult();
            var marked = controllers.ManualMark.SaveAsync(
                    Overlay(),
                    timestampSeconds: 8,
                    clockPosition: "3",
                    preCapturedFrame: [3])
                .GetAwaiter()
                .GetResult();

            Assert.Equal(LiveDetectionConfirmationAcceptCommandOutcome.AcceptedHandled, accepted.Outcome);
            Assert.Equal(LiveDetectionManualMarkTrainingCommandOutcome.Saved, marked.Outcome);
            Assert.Equal(1, writer.AcceptedCalls);
            Assert.Equal(1, writer.ManualMarkCalls);
            Assert.Equal([6d, 8d], writer.SavedTimestamps);
        });
    }

    private static LiveFrameFinding Finding()
        => new(
            Label: "Befund",
            Severity: 3,
            PositionClock: null,
            ExtentPercent: null,
            VsaCodeHint: "BAB");

    private static OverlayGeometry Overlay()
        => new()
        {
            ToolType = OverlayToolType.Rectangle,
            Points = [new NormalizedPoint(0.1, 0.2), new NormalizedPoint(0.4, 0.5)]
        };

    private sealed class RecordingAnnotationWriter : ILiveDetectionTrainingAnnotationWriter
    {
        public int AcceptedCalls { get; private set; }
        public int ManualMarkCalls { get; private set; }
        public List<double> SavedTimestamps { get; } = [];

        public Task<TeacherAnnotation> SaveAcceptedAsync(
            byte[] frameBytes,
            LiveFrameFinding finding,
            TimeSpan videoTimestamp,
            CancellationToken ct = default)
        {
            AcceptedCalls++;
            SavedTimestamps.Add(videoTimestamp.TotalSeconds);
            return Task.FromResult(new TeacherAnnotation());
        }

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
            return Task.FromResult<TeacherAnnotation?>(new TeacherAnnotation());
        }
    }
}
