using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingSessionEventResetterTests
{
    [Fact]
    public void ClearActiveSessionEvents_clears_active_session_events_and_returns_removed_count()
    {
        var session = new CodingSession();
        session.Events.Add(new CodingEvent());
        session.Events.Add(new CodingEvent());
        var service = new RecordingCodingSessionService(session);

        var removed = CodingSessionEventResetter.ClearActiveSessionEvents(service);

        Assert.Equal(2, removed);
        Assert.Empty(session.Events);
    }

    [Fact]
    public void ClearActiveSessionEvents_handles_missing_active_session()
    {
        var service = new RecordingCodingSessionService(null);

        var removed = CodingSessionEventResetter.ClearActiveSessionEvents(service);

        Assert.Equal(0, removed);
    }

    [Fact]
    public void ClearActiveSessionEvents_handles_missing_service()
    {
        var removed = CodingSessionEventResetter.ClearActiveSessionEvents(null);

        Assert.Equal(0, removed);
    }

    private sealed class RecordingCodingSessionService(CodingSession? activeSession) : ICodingSessionService
    {
        public CodingSession? ActiveSession { get; } = activeSession;
        public IReadOnlyList<CodingEvent> Events => ActiveSession?.Events is { } events
            ? events
            : Array.Empty<CodingEvent>();
        public double CurrentMeter => 0;
        public double EndMeter => 0;
        public double ProgressPercent => 0;

        public event EventHandler<CodingSessionState>? StateChanged { add { } remove { } }
        public event EventHandler<double>? MeterChanged { add { } remove { } }
        public event EventHandler<CodingEvent>? EventAdded { add { } remove { } }

        public CodingSession StartSession(HaltungRecord haltung, string? videoPath) => throw new NotSupportedException();
        public void PauseSession() => throw new NotSupportedException();
        public void ResumeSession() => throw new NotSupportedException();
        public void SetWaitingForInput() => throw new NotSupportedException();
        public void AbortSession(string reason) => throw new NotSupportedException();
        public ProtocolDocument CompleteSession() => throw new NotSupportedException();
        public void MoveNext(double stepSizeM = 0.5) => throw new NotSupportedException();
        public void MovePrevious(double stepSizeM = 0.5) => throw new NotSupportedException();
        public void MoveToMeter(double meter) => throw new NotSupportedException();
        public CodingEvent AddEvent(ProtocolEntry entry, OverlayGeometry? overlay = null) => throw new NotSupportedException();
        public void UpdateEvent(Guid eventId, ProtocolEntry entry, OverlayGeometry? overlay = null) => throw new NotSupportedException();
        public void RemoveEvent(Guid eventId) => throw new NotSupportedException();
        public Task IndexConfirmedSampleAsync(TrainingSample sample, CancellationToken ct = default) => throw new NotSupportedException();
    }
}
