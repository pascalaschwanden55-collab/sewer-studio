using System.Reflection;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingSelectedCodeEventWorkflowTests
{
    [Fact]
    public void Create_builds_manual_draft_captures_snapshot_appends_photo_and_adds_event()
    {
        var service = new RecordingCodingSessionService();
        var overlay = new OverlayGeometry { ToolType = OverlayToolType.Point };
        var videoTime = TimeSpan.FromSeconds(12);
        var snapshotCalled = false;
        var method = FindCreateMethod();
        Assert.NotNull(method);

        var created = method.Invoke(null, [
            "BCA",
            "Anschluss",
            4.2,
            videoTime,
            overlay,
            service,
            new Func<ProtocolEntry, string?>(entry =>
            {
                snapshotCalled = true;
                Assert.Equal("BCA", entry.Code);
                Assert.Equal(ProtocolEntrySource.Manual, entry.Source);
                Assert.Equal(4.2, entry.MeterStart);
                Assert.Equal(videoTime, entry.Zeit);
                return "foto.png";
            })
        ]);

        var ev = Assert.IsType<CodingEvent>(created);
        Assert.True(snapshotCalled);
        Assert.Same(ev, Assert.Single(service.AddedEvents));
        Assert.Same(overlay, ev.Overlay);
        Assert.Equal(["foto.png"], ev.Entry.FotoPaths);
        Assert.Equal("BCA", ev.AiContext!.SuggestedCode);
        Assert.Equal(CodingUserDecision.Ignored, ev.AiContext.Decision);
    }

    [Fact]
    public void Create_keeps_event_without_photo_when_snapshot_returns_null()
    {
        var service = new RecordingCodingSessionService();
        var method = FindCreateMethod();
        Assert.NotNull(method);

        var created = method.Invoke(null, [
            "BDD",
            "Wasserstand",
            1.5,
            TimeSpan.FromSeconds(3),
            null,
            service,
            new Func<ProtocolEntry, string?>(_ => null)
        ]);

        var ev = Assert.IsType<CodingEvent>(created);
        Assert.Empty(ev.Entry.FotoPaths);
        Assert.Equal("BDD", ev.Entry.Code);
    }

    [Fact]
    public void Create_returns_null_without_code_or_session_service_and_does_not_capture_snapshot()
    {
        var service = new RecordingCodingSessionService();
        var snapshotCalls = 0;
        var method = FindCreateMethod();
        Assert.NotNull(method);
        var capture = new Func<ProtocolEntry, string?>(_ =>
        {
            snapshotCalls++;
            return "foto.png";
        });

        var blankCode = method.Invoke(null, [
            " ",
            "leer",
            1.0,
            TimeSpan.Zero,
            null,
            service,
            capture
        ]);
        var missingService = method.Invoke(null, [
            "BCA",
            "Anschluss",
            1.0,
            TimeSpan.Zero,
            null,
            null,
            capture
        ]);

        Assert.Null(blankCode);
        Assert.Null(missingService);
        Assert.Equal(0, snapshotCalls);
        Assert.Empty(service.AddedEvents);
    }

    private static Type? WorkflowType
        => typeof(CodingManualEventAppender).Assembly
            .GetType("AuswertungPro.Next.UI.Ai.CodingSelectedCodeEventWorkflow");

    private static MethodInfo? FindCreateMethod()
        => WorkflowType?.GetMethod(
            "Create",
            BindingFlags.Public | BindingFlags.Static,
            binder: null,
            types:
            [
                typeof(string),
                typeof(string),
                typeof(double),
                typeof(TimeSpan),
                typeof(OverlayGeometry),
                typeof(ICodingSessionService),
                typeof(Func<ProtocolEntry, string>)
            ],
            modifiers: null);

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
