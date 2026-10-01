using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Live;

namespace AuswertungPro.Next.UI.Tests;

public sealed class LiveDetectionManualMarkEventAppenderTests
{
    [Fact]
    public void Apply_creates_manual_event_from_selected_entry_and_adds_overlay()
    {
        var service = new RecordingCodingSessionService();
        var overlay = new OverlayGeometry
        {
            ToolType = OverlayToolType.Rectangle,
            Points = [new NormalizedPoint(0.1, 0.2), new NormalizedPoint(0.3, 0.4)],
            SamMask = new OverlaySamMask
            {
                MaskRle = "0,10,5,85",
                ImageWidth = 10,
                ImageHeight = 10,
                MaskAreaPixels = 5,
                Confidence = 0.9
            }
        };
        var selectedEntry = new ProtocolEntry
        {
            Code = "BCA",
            Beschreibung = "Anschluss",
            Source = ProtocolEntrySource.Manual
        };

        var ev = LiveDetectionManualMarkEventAppender.Apply(
            selectedEntry,
            fallbackMeter: 7.5,
            fallbackTime: TimeSpan.FromSeconds(18),
            overlay,
            service);

        Assert.Same(ev, Assert.Single(service.AddedEvents));
        Assert.NotSame(selectedEntry, ev.Entry);
        Assert.Equal("BCA", ev.Entry.Code);
        Assert.Equal("Anschluss", ev.Entry.Beschreibung);
        Assert.Equal(7.5, ev.Entry.MeterStart);
        Assert.Equal(TimeSpan.FromSeconds(18), ev.Entry.Zeit);
        Assert.Same(overlay, ev.Overlay);
        Assert.Same(overlay.SamMask, ev.Overlay!.SamMask);
        Assert.Null(ev.AiContext);
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
