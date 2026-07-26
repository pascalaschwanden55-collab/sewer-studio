using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingStructuralClassifierEventAppenderTests
{
    [Fact]
    public void Apply_adds_classifier_event_and_sets_runtime_metadata()
    {
        var service = new RecordingCodingSessionService();
        var draft = CodingStructuralClassifierEventFactory.Create(
            "BAA",
            "Rissbildung",
            "Riss",
            classifierConfidence: 0.78,
            meter: 4.2,
            videoTime: TimeSpan.FromSeconds(11),
            meterFromOsd: true);

        var ev = CodingStructuralClassifierEventAppender.Apply(
            draft,
            meter: 4.2,
            videoTime: TimeSpan.FromSeconds(11),
            service);

        Assert.Same(ev, Assert.Single(service.AddedEvents));
        Assert.Same(draft.Entry, ev.Entry);
        Assert.Same(draft.AiContext, ev.AiContext);
        Assert.Equal(4.2, ev.MeterAtCapture);
        Assert.Equal(TimeSpan.FromSeconds(11), ev.VideoTimestamp);
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
