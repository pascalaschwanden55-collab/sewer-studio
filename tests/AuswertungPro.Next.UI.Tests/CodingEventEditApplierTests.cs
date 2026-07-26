using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingEventEditApplierTests
{
    [Fact]
    public void Apply_updates_capture_metadata_from_edited_entry_and_persists_event()
    {
        var service = new RecordingCodingSessionService();
        var overlay = new OverlayGeometry { ToolType = OverlayToolType.Point };
        var ev = new CodingEvent
        {
            EventId = Guid.NewGuid(),
            MeterAtCapture = 1.2,
            VideoTimestamp = TimeSpan.FromSeconds(8),
            Overlay = overlay,
            Entry = new ProtocolEntry
            {
                Code = "BBA",
                MeterStart = 4.5,
                Zeit = TimeSpan.FromSeconds(22)
            }
        };

        CodingEventEditApplier.Apply(ev, service);

        Assert.Equal(4.5, ev.MeterAtCapture);
        Assert.Equal(TimeSpan.FromSeconds(22), ev.VideoTimestamp);
        var update = Assert.Single(service.Updates);
        Assert.Equal(ev.EventId, update.EventId);
        Assert.Same(ev.Entry, update.Entry);
        Assert.Same(overlay, update.Overlay);
    }

    [Fact]
    public void Apply_uses_meter_end_when_start_is_missing_and_keeps_timestamp_without_entry_time()
    {
        var service = new RecordingCodingSessionService();
        var ev = new CodingEvent
        {
            MeterAtCapture = 1.2,
            VideoTimestamp = TimeSpan.FromSeconds(8),
            Entry = new ProtocolEntry
            {
                Code = "BBA",
                MeterStart = null,
                MeterEnd = 6.7,
                Zeit = null
            }
        };

        CodingEventEditApplier.Apply(ev, service);

        Assert.Equal(6.7, ev.MeterAtCapture);
        Assert.Equal(TimeSpan.FromSeconds(8), ev.VideoTimestamp);
    }

    [Fact]
    public void Apply_is_safe_without_session_service()
    {
        var ev = new CodingEvent
        {
            MeterAtCapture = 1.2,
            Entry = new ProtocolEntry { MeterStart = 2.4 }
        };

        CodingEventEditApplier.Apply(ev, codingSessionService: null);

        Assert.Equal(2.4, ev.MeterAtCapture);
    }

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
