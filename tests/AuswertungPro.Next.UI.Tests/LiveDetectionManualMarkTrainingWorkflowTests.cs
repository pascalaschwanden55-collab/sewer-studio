using System.Reflection;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.Teacher;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Live;

namespace AuswertungPro.Next.UI.Tests;

public sealed class LiveDetectionManualMarkTrainingWorkflowTests
{
    [Fact]
    public async Task SaveAsync_adds_session_event_saves_annotation_appends_full_frame_path_and_refreshes_twice()
    {
        var service = new RecordingCodingSessionService();
        var overlay = Overlay();
        var selectedEntry = SelectedEntry("BCA");
        var frameBytes = new byte[] { 1, 2, 3 };
        var calls = new List<string>();
        var method = FindSaveMethod();
        Assert.NotNull(method);

        var result = await InvokeSaveAsync(method, [
            selectedEntry,
            overlay,
            18d,
            "3",
            "7.50m",
            service,
            frameBytes,
            new Func<Task<byte[]?>>(() =>
            {
                calls.Add("capture");
                return Task.FromResult<byte[]?>([9]);
            }),
            new Func<byte[], ProtocolEntry, OverlayGeometry, string?, double, TimeSpan, Task<TeacherAnnotation?>>(
                (bytes, entry, savedOverlay, clock, meter, time) =>
                {
                    calls.Add("save");
                    Assert.Same(frameBytes, bytes);
                    Assert.Same(selectedEntry, entry);
                    Assert.Same(overlay, savedOverlay);
                    Assert.Equal("3", clock);
                    Assert.Equal(7.5, meter);
                    Assert.Equal(TimeSpan.FromSeconds(18), time);
                    return Task.FromResult<TeacherAnnotation?>(new TeacherAnnotation { FullFramePath = "full.png" });
                }),
            new Action(() => calls.Add("refresh"))
        ]);

        AssertResult(result, saved: true, code: "BCA", sessionEventAdded: true, photoPathAdded: true);
        var ev = Assert.Single(service.AddedEvents);
        Assert.NotSame(selectedEntry, ev.Entry);
        Assert.Equal("BCA", ev.Entry.Code);
        Assert.Equal(7.5, ev.Entry.MeterStart);
        Assert.Equal(TimeSpan.FromSeconds(18), ev.Entry.Zeit);
        Assert.Equal(["full.png"], ev.Entry.FotoPaths);
        Assert.Equal(["refresh", "save", "refresh"], calls);
    }

    [Fact]
    public async Task SaveAsync_captures_current_frame_when_pre_capture_is_missing_and_allows_missing_session()
    {
        var overlay = Overlay();
        var selectedEntry = SelectedEntry("BDD");
        var captured = new byte[] { 4, 5, 6 };
        var captureCalls = 0;
        var method = FindSaveMethod();
        Assert.NotNull(method);

        var result = await InvokeSaveAsync(method, [
            selectedEntry,
            overlay,
            3d,
            null,
            "1.25m",
            null,
            null,
            new Func<Task<byte[]?>>(() =>
            {
                captureCalls++;
                return Task.FromResult<byte[]?>(captured);
            }),
            new Func<byte[], ProtocolEntry, OverlayGeometry, string?, double, TimeSpan, Task<TeacherAnnotation?>>(
                (bytes, _, _, _, meter, _) =>
                {
                    Assert.Same(captured, bytes);
                    Assert.Equal(1.25, meter);
                    return Task.FromResult<TeacherAnnotation?>(new TeacherAnnotation { FullFramePath = "full.png" });
                }),
            new Action(() => throw new InvalidOperationException("Refresh darf ohne Session-Event nicht laufen."))
        ]);

        AssertResult(result, saved: true, code: "BDD", sessionEventAdded: false, photoPathAdded: false);
        Assert.Equal(1, captureCalls);
    }

    [Fact]
    public async Task SaveAsync_returns_false_without_frame_and_does_not_call_writer()
    {
        var writerCalls = 0;
        var method = FindSaveMethod();
        Assert.NotNull(method);

        var result = await InvokeSaveAsync(method, [
            SelectedEntry("BCA"),
            Overlay(),
            3d,
            null,
            "1.25m",
            null,
            null,
            new Func<Task<byte[]?>>(() => Task.FromResult<byte[]?>(null)),
            new Func<byte[], ProtocolEntry, OverlayGeometry, string?, double, TimeSpan, Task<TeacherAnnotation?>>(
                (_, _, _, _, _, _) =>
                {
                    writerCalls++;
                    return Task.FromResult<TeacherAnnotation?>(new TeacherAnnotation());
                }),
            new Action(() => { })
        ]);

        AssertResult(result, saved: false, code: null, sessionEventAdded: false, photoPathAdded: false);
        Assert.Equal(0, writerCalls);
    }

    [Fact]
    public async Task SaveAsync_returns_false_when_writer_rejects_annotation()
    {
        var service = new RecordingCodingSessionService();
        var refreshCalls = 0;
        var method = FindSaveMethod();
        Assert.NotNull(method);

        var result = await InvokeSaveAsync(method, [
            SelectedEntry("BCA"),
            Overlay(),
            3d,
            null,
            "1.25m",
            service,
            new byte[] { 1 },
            new Func<Task<byte[]?>>(() => throw new InvalidOperationException("PreCaptured frame should be used.")),
            new Func<byte[], ProtocolEntry, OverlayGeometry, string?, double, TimeSpan, Task<TeacherAnnotation?>>(
                (_, _, _, _, _, _) => Task.FromResult<TeacherAnnotation?>(null)),
            new Action(() => refreshCalls++)
        ]);

        AssertResult(result, saved: false, code: null, sessionEventAdded: true, photoPathAdded: false);
        Assert.Single(service.AddedEvents);
        Assert.Equal(1, refreshCalls);
    }

    private static MethodInfo? FindSaveMethod()
        => typeof(LiveDetectionManualMarkEventAppender).Assembly
            .GetType("AuswertungPro.Next.UI.Ai.Live.LiveDetectionManualMarkTrainingWorkflow")
            ?.GetMethod(
                "SaveAsync",
                BindingFlags.Public | BindingFlags.Static,
                binder: null,
                types:
                [
                    typeof(ProtocolEntry),
                    typeof(OverlayGeometry),
                    typeof(double),
                    typeof(string),
                    typeof(string),
                    typeof(ICodingSessionService),
                    typeof(byte[]),
                    typeof(Func<Task<byte[]>>),
                    typeof(Func<byte[], ProtocolEntry, OverlayGeometry, string, double, TimeSpan, Task<TeacherAnnotation>>),
                    typeof(Action)
                ],
                modifiers: null);

    private static async Task<object?> InvokeSaveAsync(MethodInfo method, object?[] args)
    {
        var task = Assert.IsAssignableFrom<Task>(method.Invoke(null, args));
        await task;
        return task.GetType().GetProperty("Result")!.GetValue(task);
    }

    private static ProtocolEntry SelectedEntry(string code)
        => new()
        {
            Code = code,
            Beschreibung = "Beschreibung",
            Source = ProtocolEntrySource.Manual
        };

    private static OverlayGeometry Overlay()
        => new()
        {
            ToolType = OverlayToolType.Rectangle,
            Points = [new NormalizedPoint(0.1, 0.2), new NormalizedPoint(0.3, 0.4)]
        };

    private static void AssertResult(
        object? result,
        bool saved,
        string? code,
        bool sessionEventAdded,
        bool photoPathAdded)
    {
        Assert.NotNull(result);
        var type = result.GetType();
        Assert.Equal(saved, type.GetProperty("Saved")?.GetValue(result));
        Assert.Equal(code, type.GetProperty("Code")?.GetValue(result));
        Assert.Equal(sessionEventAdded, type.GetProperty("SessionEventAdded")?.GetValue(result));
        Assert.Equal(photoPathAdded, type.GetProperty("PhotoPathAdded")?.GetValue(result));
    }

    private sealed class RecordingCodingSessionService : ICodingSessionService
    {
        public List<CodingEvent> AddedEvents { get; } = new();

        public double CurrentMeter => 0;
        public double EndMeter => 0;
        public double ProgressPercent => 0;
        public CodingSession? ActiveSession => null;
        public IReadOnlyList<CodingEvent> Events => AddedEvents;

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
        {
            var ev = new CodingEvent { Entry = entry, Overlay = overlay };
            AddedEvents.Add(ev);
            return ev;
        }

        public void UpdateEvent(Guid eventId, ProtocolEntry entry, OverlayGeometry? overlay = null) { }
        public void RemoveEvent(Guid eventId) { }

        public Task IndexConfirmedSampleAsync(
            AuswertungPro.Next.Application.Ai.Training.TrainingSample sample,
            CancellationToken ct = default)
            => Task.CompletedTask;
    }
}
