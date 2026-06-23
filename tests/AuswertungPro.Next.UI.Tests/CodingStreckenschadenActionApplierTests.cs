using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingStreckenschadenActionApplierTests
{
    [Fact]
    public void Apply_creates_open_stretch_damage_event_and_attaches_frame_photo()
    {
        var service = new RecordingCodingSessionService();
        var action = new StreckenschadenTracker.SegmentAction(
            StreckenschadenTracker.SegmentActionType.Open,
            MainCode: "BBA",
            ClockHour: 3.0,
            StartMeter: 6.7,
            EndMeter: 6.7,
            IsConfirmedStrecke: false);

        var changed = CodingStreckenschadenActionApplier.Apply(
            [action],
            [],
            service,
            TimeSpan.FromSeconds(18),
            code => code == "BBA" ? "Laengsriss" : null,
            entry => entry.FotoPaths.Add("frames/bba.png"));

        Assert.True(changed);
        var added = Assert.Single(service.AddedEvents);
        Assert.Equal("BBA", added.Entry.Code);
        Assert.Equal("Laengsriss", added.Entry.Beschreibung);
        Assert.Equal(6.7, added.Entry.MeterStart);
        Assert.Null(added.Entry.MeterEnd);
        Assert.True(added.Entry.IsStreckenschaden);
        Assert.Equal(TimeSpan.FromSeconds(18), added.Entry.Zeit);
        Assert.Equal("frames/bba.png", Assert.Single(added.Entry.FotoPaths));
        Assert.Equal(6.7, added.MeterAtCapture);
        Assert.Equal("BBA", added.AiContext?.SuggestedCode);
        Assert.Empty(service.UpdatedEvents);
    }

    [Fact]
    public void Apply_closes_matching_open_stretch_damage_event()
    {
        var service = new RecordingCodingSessionService();
        var overlay = new OverlayGeometry { ToolType = OverlayToolType.Stretch };
        var existing = new CodingEvent
        {
            EventId = Guid.NewGuid(),
            MeterAtCapture = 2.0,
            Overlay = overlay,
            Entry = new ProtocolEntry
            {
                Code = "BBA",
                MeterStart = 2.0,
                IsStreckenschaden = true
            }
        };
        var action = new StreckenschadenTracker.SegmentAction(
            StreckenschadenTracker.SegmentActionType.Close,
            MainCode: "BBA",
            ClockHour: 4.0,
            StartMeter: 2.0,
            EndMeter: 4.8,
            IsConfirmedStrecke: true);

        var changed = CodingStreckenschadenActionApplier.Apply(
            [action],
            [existing],
            service,
            TimeSpan.FromSeconds(22),
            _ => null,
            _ => { });

        Assert.True(changed);
        Assert.Empty(service.AddedEvents);
        var update = Assert.Single(service.UpdatedEvents);
        Assert.Equal(existing.EventId, update.EventId);
        Assert.Same(existing.Entry, update.Entry);
        Assert.Same(overlay, update.Overlay);
        Assert.Equal(4.8, existing.Entry.MeterEnd);
        Assert.True(existing.Entry.IsStreckenschaden);
    }

    [Fact]
    public void Apply_returns_false_when_mapper_yields_no_instruction()
    {
        var service = new RecordingCodingSessionService();
        var action = new StreckenschadenTracker.SegmentAction(
            StreckenschadenTracker.SegmentActionType.Extend,
            MainCode: "BBA",
            ClockHour: 3.0,
            StartMeter: 1.0,
            EndMeter: 2.3,
            IsConfirmedStrecke: true);

        var changed = CodingStreckenschadenActionApplier.Apply(
            [action],
            [],
            service,
            TimeSpan.Zero,
            _ => null,
            _ => { });

        Assert.False(changed);
        Assert.Empty(service.AddedEvents);
        Assert.Empty(service.UpdatedEvents);
    }

    private sealed class RecordingCodingSessionService : ICodingSessionService
    {
        public List<CodingEvent> AddedEvents { get; } = new();
        public List<UpdateCall> UpdatedEvents { get; } = new();

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

        public void UpdateEvent(Guid eventId, ProtocolEntry entry, OverlayGeometry? overlay = null)
            => UpdatedEvents.Add(new UpdateCall(eventId, entry, overlay));

        public void RemoveEvent(Guid eventId) { }

        public Task IndexConfirmedSampleAsync(
            AuswertungPro.Next.Application.Ai.Training.TrainingSample sample,
            CancellationToken ct = default)
            => Task.CompletedTask;
    }

    private sealed record UpdateCall(Guid EventId, ProtocolEntry Entry, OverlayGeometry? Overlay);
}
