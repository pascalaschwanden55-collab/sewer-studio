using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingLiveFindingSessionAppenderTests
{
    [Fact]
    public void Append_attaches_photo_before_add_event_and_applies_ai_context_and_overlay()
    {
        var calls = new List<string>();
        var entry = new ProtocolEntry { Code = "BAB" };
        var aiContext = new CodingEventAiContext
        {
            SuggestedCode = "BAB",
            Confidence = 0.87,
            Reason = "Riss"
        };
        var overlay = new OverlayGeometry { ToolType = OverlayToolType.Rectangle };
        var draft = new CodingLiveFindingEventDraft(entry, aiContext, overlay);

        var codingEvent = CodingLiveFindingSessionAppender.Append(
            draft,
            attachAnalyzedFramePhoto: attachedEntry =>
            {
                calls.Add("attach");
                Assert.Same(entry, attachedEntry);
            },
            addEvent: addedEntry =>
            {
                calls.Add("add");
                Assert.Same(entry, addedEntry);
                return new CodingEvent { Entry = addedEntry };
            });

        Assert.Equal(["attach", "add"], calls);
        Assert.Same(aiContext, codingEvent.AiContext);
        Assert.Same(overlay, codingEvent.Overlay);
        Assert.Same(entry, codingEvent.Entry);
    }

    [Fact]
    public void Append_with_session_service_attaches_photo_before_add_event_and_applies_ai_context_and_overlay()
    {
        var service = new RecordingCodingSessionService();
        var entry = new ProtocolEntry { Code = "BCA" };
        var aiContext = new CodingEventAiContext
        {
            SuggestedCode = "BCA",
            Confidence = 0.92,
            Reason = "Anschluss"
        };
        var overlay = new OverlayGeometry { ToolType = OverlayToolType.Rectangle };
        var draft = new CodingLiveFindingEventDraft(entry, aiContext, overlay);

        var codingEvent = CodingLiveFindingSessionAppender.Append(
            draft,
            attachAnalyzedFramePhoto: attachedEntry =>
            {
                service.Calls.Add("attach");
                Assert.Same(entry, attachedEntry);
            },
            service);

        Assert.Equal(["attach", "add"], service.Calls);
        Assert.Same(codingEvent, Assert.Single(service.AddedEvents));
        Assert.Same(entry, codingEvent.Entry);
        Assert.Same(aiContext, codingEvent.AiContext);
        Assert.Same(overlay, codingEvent.Overlay);
    }

    private sealed class RecordingCodingSessionService : ICodingSessionService
    {
        public List<string> Calls { get; } = new();
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
            Calls.Add("add");
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
