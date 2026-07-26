using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingManualEventAppenderTests
{
    [Fact]
    public void Apply_adds_manual_draft_without_ai_context()
    {
        var service = new RecordingCodingSessionService();
        var overlay = new OverlayGeometry
        {
            ToolType = OverlayToolType.Rectangle,
            Points = [new NormalizedPoint(0.1, 0.2), new NormalizedPoint(0.3, 0.4)]
        };
        var draft = CodingManualEventFactory.CreateUnconfirmed(
            "BCA",
            "Anschluss",
            meter: 3.2,
            videoTime: TimeSpan.FromSeconds(9),
            overlay);

        var ev = CodingManualEventAppender.Apply(draft, overlay, service);

        Assert.Same(ev, Assert.Single(service.AddedEvents));
        Assert.Same(draft.Entry, ev.Entry);
        Assert.Null(ev.AiContext);
        Assert.Same(draft.ReviewContext, ev.ReviewContext);
        Assert.Same(overlay, ev.Overlay);
    }

    [Fact]
    public void Apply_adds_code_explorer_entry_with_unconfirmed_context()
    {
        var service = new RecordingCodingSessionService();
        var overlay = new OverlayGeometry { ToolType = OverlayToolType.Point };
        var entry = new ProtocolEntry
        {
            Source = ProtocolEntrySource.Manual,
            Code = "BDD",
            Beschreibung = "Wasserstand"
        };

        var ev = CodingManualEventAppender.Apply(entry, overlay, service);

        Assert.Same(entry, ev.Entry);
        Assert.Same(overlay, ev.Overlay);
        Assert.Null(ev.AiContext);
        Assert.Equal(CodingUserDecision.Ignored, ev.ReviewContext!.Decision);
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
