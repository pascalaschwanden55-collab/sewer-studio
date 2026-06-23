using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingOpenStretchDamageCloseApplierTests
{
    [Fact]
    public void Apply_closes_each_open_stretch_damage_and_updates_session()
    {
        var service = new RecordingCodingSessionService();
        var first = Event("BBA", meterStart: 1.0, meterEnd: null, meterAtCapture: 1.5);
        var second = Event("BBC", meterStart: 4.0, meterEnd: null, meterAtCapture: 3.0);

        var changed = CodingOpenStretchDamageCloseApplier.Apply(
            [first, second],
            currentMeter: 8.0,
            service);

        Assert.True(changed);
        Assert.Equal(1.5, first.Entry.MeterEnd);
        Assert.Equal(8.0, second.Entry.MeterEnd);
        Assert.Equal(2, service.Updates.Count);
        Assert.Equal(first.EventId, service.Updates[0].EventId);
        Assert.Same(first.Entry, service.Updates[0].Entry);
        Assert.Equal(second.EventId, service.Updates[1].EventId);
        Assert.Same(second.Entry, service.Updates[1].Entry);
    }

    [Fact]
    public void Apply_returns_false_without_open_events()
    {
        var service = new RecordingCodingSessionService();

        var changed = CodingOpenStretchDamageCloseApplier.Apply(
            [],
            currentMeter: 8.0,
            service);

        Assert.False(changed);
        Assert.Empty(service.Updates);
    }

    private static CodingEvent Event(string code, double? meterStart, double? meterEnd, double meterAtCapture)
        => new()
        {
            EventId = Guid.NewGuid(),
            MeterAtCapture = meterAtCapture,
            Entry = new ProtocolEntry
            {
                Code = code,
                IsStreckenschaden = true,
                MeterStart = meterStart,
                MeterEnd = meterEnd
            }
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
        public CodingEvent AddEvent(ProtocolEntry entry, OverlayGeometry? overlay = null) => new() { Entry = entry, Overlay = overlay };
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
