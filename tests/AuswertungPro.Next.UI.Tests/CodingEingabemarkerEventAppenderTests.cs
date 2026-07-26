using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingEingabemarkerEventAppenderTests
{
    [Fact]
    public void Apply_adds_eingabemarker_event_with_overlay_and_context()
    {
        var service = new RecordingCodingSessionService();
        var overlay = new OverlayGeometry
        {
            ToolType = OverlayToolType.Rectangle,
            Points = [new NormalizedPoint(0.2, 0.3), new NormalizedPoint(0.4, 0.5)]
        };
        var draft = CodingEingabemarkerEventFactory.CreateAccepted(
            "BCA",
            "Anschluss",
            "anschluss",
            meter: 6.4,
            videoTime: TimeSpan.FromSeconds(27));

        var ev = CodingEingabemarkerEventAppender.Apply(draft, overlay, service);

        Assert.Same(ev, Assert.Single(service.AddedEvents));
        Assert.Same(draft.Entry, ev.Entry);
        Assert.Same(draft.AiContext, ev.AiContext);
        Assert.Same(overlay, ev.Overlay);
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
