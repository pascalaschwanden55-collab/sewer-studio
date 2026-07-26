using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingEventDeleteApplierTests
{
    [Fact]
    public void Apply_removes_event_from_session_and_event_list()
    {
        var service = new RecordingCodingSessionService();
        var ev = Event("BBA");
        var other = Event("BBC");
        var events = new List<CodingEvent> { ev, other };

        var result = CodingEventDeleteApplier.Apply(ev, service, events, selectedDefect: other);

        Assert.True(result.RemovedFromList);
        Assert.False(result.ShouldClearSelectedDefect);
        Assert.Equal(ev.EventId, Assert.Single(service.RemovedEventIds));
        Assert.Same(other, Assert.Single(events));
    }

    [Fact]
    public void Apply_requests_selected_defect_clear_when_deleted_event_is_selected()
    {
        var service = new RecordingCodingSessionService();
        var ev = Event("BBA");
        var events = new List<CodingEvent> { ev };

        var result = CodingEventDeleteApplier.Apply(ev, service, events, selectedDefect: ev);

        Assert.True(result.ShouldClearSelectedDefect);
    }

    [Fact]
    public void Apply_is_safe_without_session_or_event_list()
    {
        var ev = Event("BBA");

        var result = CodingEventDeleteApplier.Apply(
            ev,
            codingSessionService: null,
            codingEvents: null,
            selectedDefect: ev);

        Assert.False(result.RemovedFromList);
        Assert.True(result.ShouldClearSelectedDefect);
    }

    private static CodingEvent Event(string code)
    {
        return new CodingEvent
        {
            EventId = Guid.NewGuid(),
            Entry = new ProtocolEntry { Code = code }
        };
    }

    private sealed class RecordingCodingSessionService : ICodingSessionService
    {
        public List<Guid> RemovedEventIds { get; } = new();

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
        public CodingEvent AddEvent(ProtocolEntry entry, OverlayGeometry? overlay = null) => new() { Entry = entry, Overlay = overlay };
        public void UpdateEvent(Guid eventId, ProtocolEntry entry, OverlayGeometry? overlay = null) { }
        public void RemoveEvent(Guid eventId) => RemovedEventIds.Add(eventId);

        public Task IndexConfirmedSampleAsync(
            AuswertungPro.Next.Application.Ai.Training.TrainingSample sample,
            CancellationToken ct = default)
            => Task.CompletedTask;
    }
}
