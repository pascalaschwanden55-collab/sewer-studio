using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingMultiModelEventAppenderTests
{
    [Fact]
    public void Apply_adds_multimodel_event_and_sets_context_and_overlay()
    {
        var service = new RecordingCodingSessionService();
        var entry = new ProtocolEntry { Code = "BAA", Beschreibung = "Riss" };
        var context = new CodingEventAiContext
        {
            SuggestedCode = "BAA",
            Confidence = 0.81,
            Reason = "DINO",
            Decision = CodingUserDecision.Ignored
        };
        var overlay = new OverlayGeometry
        {
            ToolType = OverlayToolType.Rectangle,
            Points = [new NormalizedPoint(0.1, 0.2), new NormalizedPoint(0.3, 0.4)]
        };
        var draft = new CodingMultiModelEventDraft(entry, context, overlay);

        var ev = CodingMultiModelEventAppender.Apply(draft, service);

        Assert.Same(ev, Assert.Single(service.AddedEvents));
        Assert.Same(entry, ev.Entry);
        Assert.Same(context, ev.AiContext);
        Assert.Same(overlay, ev.Overlay);
    }

    [Fact]
    public void Apply_erhaelt_Bearbeitung_aus_synchronem_EventAdded_Callback()
    {
        var changedContext = new CodingEventAiContext { Decision = CodingUserDecision.AcceptedWithEdit };
        var changedOverlay = new OverlayGeometry { ToolType = OverlayToolType.Point };
        var service = new RecordingCodingSessionService { OnAdd = ev =>
        {
            ev.AiContext = changedContext;
            ev.Overlay = changedOverlay;
        } };
        var ev = CodingMultiModelEventAppender.Apply(new(new(), new(), null), service);
        Assert.Same(changedContext, ev.AiContext);
        Assert.Same(changedOverlay, ev.Overlay);
        Assert.NotNull(ev.AiContext!.HumanTouchedAtUtc);
    }

    private sealed class RecordingCodingSessionService : ICodingSessionService
    {
        public List<CodingEvent> AddedEvents { get; } = new();
        public Action<CodingEvent>? OnAdd { get; init; }

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
            OnAdd?.Invoke(ev);
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
