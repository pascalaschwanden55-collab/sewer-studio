using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingEventPhotoApplierTests
{
    [Fact]
    public void Apply_adds_photo_to_event_entry_and_updates_session()
    {
        var service = new RecordingCodingSessionService();
        var overlay = new OverlayGeometry { ToolType = OverlayToolType.Point };
        var ev = new CodingEvent
        {
            EventId = Guid.NewGuid(),
            Overlay = overlay,
            Entry = new ProtocolEntry { Code = "BBA" }
        };

        var result = CodingEventPhotoApplier.Apply(ev, @"C:\Fotos\a.png", service);

        Assert.Equal([@"C:\Fotos\a.png"], ev.Entry.FotoPaths);
        Assert.Equal("Foto 1: a.png", result.OverlayText);
        var update = Assert.Single(service.Updates);
        Assert.Equal(ev.EventId, update.EventId);
        Assert.Same(ev.Entry, update.Entry);
        Assert.Same(overlay, update.Overlay);
    }

    [Fact]
    public void Apply_replaces_second_photo_when_slots_are_full()
    {
        var service = new RecordingCodingSessionService();
        var ev = new CodingEvent
        {
            Entry = new ProtocolEntry
            {
                FotoPaths = ["first.png", "old-second.png"]
            }
        };

        var result = CodingEventPhotoApplier.Apply(ev, @"C:\Fotos\new-second.png", service);

        Assert.Equal(["first.png", @"C:\Fotos\new-second.png"], ev.Entry.FotoPaths);
        Assert.True(result.Replaced);
        Assert.Equal("Foto 2 ersetzt: new-second.png", result.OverlayText);
    }

    [Fact]
    public void Apply_is_safe_without_session_service()
    {
        var ev = new CodingEvent { Entry = new ProtocolEntry() };

        var result = CodingEventPhotoApplier.Apply(ev, "photo.png", codingSessionService: null);

        Assert.Equal(["photo.png"], ev.Entry.FotoPaths);
        Assert.Equal("Foto 1: photo.png", result.OverlayText);
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
