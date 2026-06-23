using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingStretchDamageManualCloseApplierTests
{
    [Fact]
    public void Apply_requires_later_meter_when_current_meter_is_not_after_start()
    {
        var service = new RecordingCodingSessionService();
        var startEvent = Event("BBA", meterAtCapture: 4.0);

        var result = CodingStretchDamageManualCloseApplier.Apply(
            startEvent,
            currentMeter: 4.0,
            currentVideoTime: TimeSpan.FromSeconds(12),
            service);

        Assert.Equal(CodingStretchDamageManualCloseResultKind.RequiresLaterMeter, result.Kind);
        Assert.Null(result.EndEvent);
        Assert.Null(result.StatusText);
        Assert.Empty(service.AddedEvents);
    }

    [Fact]
    public void Apply_closes_start_event_and_creates_end_event()
    {
        var service = new RecordingCodingSessionService();
        var startEvent = Event("BAJ", meterAtCapture: 2.3);
        startEvent.Entry.Beschreibung = "Laengsriss";

        var result = CodingStretchDamageManualCloseApplier.Apply(
            startEvent,
            currentMeter: 8.5,
            currentVideoTime: TimeSpan.FromSeconds(44),
            service);

        Assert.Equal(CodingStretchDamageManualCloseResultKind.Closed, result.Kind);
        Assert.Equal("Streckenschaden geschlossen: BAJ 2.30m - 8.50m", result.StatusText);
        Assert.Equal(8.5, startEvent.Entry.MeterEnd);
        Assert.True(startEvent.Entry.IsStreckenschaden);

        var added = Assert.Single(service.AddedEvents);
        Assert.Same(added, result.EndEvent);
        Assert.Equal("BAJ", added.Entry.Code);
        Assert.Equal("Laengsriss (Ende)", added.Entry.Beschreibung);
        Assert.Equal(8.5, added.Entry.MeterStart);
        Assert.True(added.Entry.IsStreckenschaden);
        Assert.Equal(TimeSpan.FromSeconds(44), added.VideoTimestamp);
    }

    private static CodingEvent Event(string code, double meterAtCapture)
    {
        return new CodingEvent
        {
            MeterAtCapture = meterAtCapture,
            Entry = new ProtocolEntry
            {
                Code = code,
                MeterStart = meterAtCapture,
                IsStreckenschaden = true
            }
        };
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
        public event EventHandler<CodingEvent>? EventAdded;

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
            EventAdded?.Invoke(this, ev);
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
