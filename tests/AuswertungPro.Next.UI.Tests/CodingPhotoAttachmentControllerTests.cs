using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingPhotoAttachmentControllerTests
{
    [Fact]
    public async Task AttachAnalyzedFramePhotoAsync_uses_preferred_frame_and_refreshes_once()
    {
        var calls = new List<string>();
        var preferred = new byte[] { 1, 2, 3 };
        var entry = new ProtocolEntry();
        var controller = CreateController(
            calls,
            getPreferredFrameBytesAsync: () =>
            {
                calls.Add("preferred");
                return Task.FromResult<byte[]?>(preferred);
            },
            getBufferedFrameBytes: () => throw new InvalidOperationException("Buffered frame must not be read."),
            attachAnalyzedFramePhoto: (actualEntry, frameBytes) =>
            {
                Assert.Same(entry, actualEntry);
                Assert.Same(preferred, frameBytes);
                calls.Add("attach");
                return "ai.png";
            },
            captureSnapshot: _ => throw new InvalidOperationException("Snapshot fallback must not run."));

        var result = await controller.AttachAnalyzedFramePhotoAsync(entry);

        Assert.Equal("ai.png", result);
        Assert.Equal(["preferred", "attach", "refresh"], calls);
    }

    [Fact]
    public async Task AttachAnalyzedFramePhotoAsync_uses_buffered_frame_then_snapshot_fallback()
    {
        var calls = new List<string>();
        var buffered = new byte[] { 4, 5, 6 };
        var entry = new ProtocolEntry();
        var controller = CreateController(
            calls,
            getPreferredFrameBytesAsync: () =>
            {
                calls.Add("preferred");
                return Task.FromResult<byte[]?>(null);
            },
            getBufferedFrameBytes: () =>
            {
                calls.Add("buffered");
                return buffered;
            },
            attachAnalyzedFramePhoto: (_, frameBytes) =>
            {
                Assert.Same(buffered, frameBytes);
                calls.Add("attach");
                return null;
            },
            captureSnapshot: actualEntry =>
            {
                Assert.Same(entry, actualEntry);
                calls.Add("snapshot");
                return "snapshot.png";
            });

        var result = await controller.AttachAnalyzedFramePhotoAsync(entry);

        Assert.Equal("snapshot.png", result);
        Assert.Equal(["snapshot.png"], entry.FotoPaths);
        Assert.Equal(["preferred", "buffered", "attach", "snapshot", "refresh"], calls);
    }

    [Fact]
    public async Task AttachAnalyzedFramePhotoAsync_does_not_refresh_when_no_photo_was_created()
    {
        var calls = new List<string>();
        var entry = new ProtocolEntry();
        var controller = CreateController(
            calls,
            getPreferredFrameBytesAsync: () =>
            {
                calls.Add("preferred");
                return Task.FromResult<byte[]?>(null);
            },
            getBufferedFrameBytes: () =>
            {
                calls.Add("buffered");
                return null;
            },
            attachAnalyzedFramePhoto: (_, _) =>
            {
                calls.Add("attach");
                return null;
            },
            captureSnapshot: _ =>
            {
                calls.Add("snapshot");
                return null;
            });

        var result = await controller.AttachAnalyzedFramePhotoAsync(entry);

        Assert.Null(result);
        Assert.Empty(entry.FotoPaths);
        Assert.Equal(["preferred", "buffered", "attach", "snapshot"], calls);
    }

    [Fact]
    public async Task AttachAnalyzedFramePhoto_starts_fire_and_forget_path_and_refreshes_after_completion()
    {
        var calls = new List<string>();
        var preferredCompletion = new TaskCompletionSource<byte[]?>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var refreshCompletion = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var frame = new byte[] { 10, 11, 12 };
        var controller = CreateController(
            calls,
            getPreferredFrameBytesAsync: () =>
            {
                calls.Add("preferred-started");
                return preferredCompletion.Task;
            },
            attachAnalyzedFramePhoto: (_, frameBytes) =>
            {
                Assert.Same(frame, frameBytes);
                calls.Add("attach");
                return "background.png";
            },
            refreshEvents: () =>
            {
                calls.Add("refresh");
                refreshCompletion.TrySetResult();
            });

        controller.AttachAnalyzedFramePhoto(new ProtocolEntry());

        Assert.Equal(["preferred-started"], calls);
        Assert.False(refreshCompletion.Task.IsCompleted);

        preferredCompletion.SetResult(frame);
        await refreshCompletion.Task.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Equal(["preferred-started", "attach", "refresh"], calls);
    }

    [Fact]
    public void AttachBoundaryAnalyzedFramePhoto_attaches_directly_without_refresh_or_capture()
    {
        var calls = new List<string>();
        var entry = new ProtocolEntry();
        var frame = new byte[] { 7, 8, 9 };
        var controller = CreateController(
            calls,
            getPreferredFrameBytesAsync: () => throw new InvalidOperationException("Extraction must not run."),
            attachAnalyzedFramePhoto: (actualEntry, frameBytes) =>
            {
                Assert.Same(entry, actualEntry);
                Assert.Same(frame, frameBytes);
                calls.Add("attach-boundary");
                return "boundary.png";
            },
            captureSnapshot: _ => throw new InvalidOperationException("Snapshot must not run."));

        var result = controller.AttachBoundaryAnalyzedFramePhoto(entry, frame);

        Assert.Equal("boundary.png", result);
        Assert.Equal(["attach-boundary"], calls);
    }

    [Fact]
    public void TakePhotoForSelectedEvent_restores_original_time_when_capture_fails()
    {
        var calls = new List<string>();
        var codingEvent = Event(entrySeconds: 2, videoSeconds: 3);
        var controller = CreateController(
            calls,
            captureSnapshot: entry =>
            {
                Assert.Equal(TimeSpan.FromSeconds(12), entry.Zeit);
                calls.Add("snapshot");
                return null;
            });

        var result = controller.TakePhotoForSelectedEvent(codingEvent);

        Assert.Equal(CodingTakePhotoCommandOutcome.CaptureFailed, result.Outcome);
        Assert.Equal(TimeSpan.FromSeconds(2), codingEvent.Entry.Zeit);
        Assert.Equal(TimeSpan.FromSeconds(3), codingEvent.VideoTimestamp);
        Assert.Equal(
            ["time", "snapshot", "overlay:Foto konnte nicht aufgenommen werden:3"],
            calls);
    }

    [Fact]
    public void TakePhotoForSelectedEvent_keeps_photo_time_applies_photo_and_refreshes()
    {
        var calls = new List<string>();
        var service = new RecordingCodingSessionService();
        var overlay = new OverlayGeometry { ToolType = OverlayToolType.Point };
        var codingEvent = Event(entrySeconds: 2, videoSeconds: 3);
        codingEvent.EventId = Guid.NewGuid();
        codingEvent.Overlay = overlay;
        var controller = CreateController(
            calls,
            captureSnapshot: entry =>
            {
                Assert.Equal(TimeSpan.FromSeconds(12), entry.Zeit);
                calls.Add("snapshot");
                return "photo.jpg";
            },
            resolveCodingSessionService: () =>
            {
                calls.Add("session");
                return service;
            });

        var result = controller.TakePhotoForSelectedEvent(codingEvent);

        Assert.Equal(CodingTakePhotoCommandOutcome.PhotoSaved, result.Outcome);
        Assert.Equal(TimeSpan.FromSeconds(12), codingEvent.Entry.Zeit);
        Assert.Equal(TimeSpan.FromSeconds(12), codingEvent.VideoTimestamp);
        Assert.Equal(["photo.jpg"], codingEvent.Entry.FotoPaths);
        Assert.Equal(
            ["time", "snapshot", "session", "overlay:Foto 1: photo.jpg:3", "refresh"],
            calls);
        var update = Assert.Single(service.Updates);
        Assert.Equal(codingEvent.EventId, update.EventId);
        Assert.Same(codingEvent.Entry, update.Entry);
        Assert.Same(overlay, update.Overlay);
    }

    private static CodingPhotoAttachmentController CreateController(
        List<string> calls,
        Func<Task<byte[]?>>? getPreferredFrameBytesAsync = null,
        Func<byte[]?>? getBufferedFrameBytes = null,
        Func<ProtocolEntry, byte[]?, string?>? attachAnalyzedFramePhoto = null,
        Func<ProtocolEntry, string?>? captureSnapshot = null,
        Func<ICodingSessionService?>? resolveCodingSessionService = null,
        Action? refreshEvents = null)
        => new(
            new CodingPhotoAttachmentControllerBindings(
                GetPreferredFrameBytesAsync: getPreferredFrameBytesAsync ?? (() => Task.FromResult<byte[]?>(null)),
                GetBufferedFrameBytes: getBufferedFrameBytes ?? (() => null),
                AttachAnalyzedFramePhoto: attachAnalyzedFramePhoto ?? ((_, _) => "ai.png"),
                CaptureSnapshot: captureSnapshot ?? (_ => "snapshot.png"),
                GetCurrentPlayerTimestamp: () =>
                {
                    calls.Add("time");
                    return TimeSpan.FromSeconds(12);
                },
                ResolveCodingSessionService: resolveCodingSessionService ?? (() => null),
                ShowOverlay: (text, duration) => calls.Add($"overlay:{text}:{duration.TotalSeconds}"),
                RefreshEvents: refreshEvents ?? (() => calls.Add("refresh"))));

    private static CodingEvent Event(int entrySeconds, int videoSeconds)
        => new()
        {
            Entry = new ProtocolEntry
            {
                Code = "BAB",
                Zeit = TimeSpan.FromSeconds(entrySeconds)
            },
            VideoTimestamp = TimeSpan.FromSeconds(videoSeconds)
        };

    private sealed class RecordingCodingSessionService : ICodingSessionService
    {
        public List<UpdateCall> Updates { get; } = new();

        public double CurrentMeter => 0;
        public double EndMeter => 0;
        public double ProgressPercent => 0;
        public CodingSession? ActiveSession => null;
        public IReadOnlyList<CodingEvent> Events => Array.Empty<CodingEvent>();

        public event EventHandler<CodingSessionState>? StateChanged { add { } remove { } }
        public event EventHandler<double>? MeterChanged { add { } remove { } }
        public event EventHandler<CodingEvent>? EventAdded { add { } remove { } }

        public CodingSession StartSession(HaltungRecord haltung, string? videoPath) => new();
        public void PauseSession() { }
        public void ResumeSession() { }
        public void SetWaitingForInput() { }
        public void AbortSession(string reason) { }
        public ProtocolDocument CompleteSession() => new();
        public void MoveNext(double stepSizeM = 0.5) { }
        public void MovePrevious(double stepSizeM = 0.5) { }
        public void MoveToMeter(double meter) { }
        public CodingEvent AddEvent(ProtocolEntry entry, OverlayGeometry? overlay = null)
            => new() { Entry = entry, Overlay = overlay };
        public void UpdateEvent(Guid eventId, ProtocolEntry entry, OverlayGeometry? overlay = null)
            => Updates.Add(new UpdateCall(eventId, entry, overlay));
        public void RemoveEvent(Guid eventId) { }

        public Task IndexConfirmedSampleAsync(
            AuswertungPro.Next.Application.Ai.Training.TrainingSample sample,
            CancellationToken ct = default)
            => Task.CompletedTask;
    }

    private sealed record UpdateCall(Guid EventId, ProtocolEntry Entry, OverlayGeometry? Overlay);
}
