using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingEingabemarkerDirectEventWorkflowTests
{
    [Fact]
    public void Execute_creates_event_adds_photo_and_runs_post_actions()
    {
        var calls = new List<string>();
        var service = new RecordingCodingSessionService();
        var overlay = new OverlayGeometry();

        var result = CodingEingabemarkerDirectEventWorkflow.Execute(
            new CodingEingabemarkerDirectEventWorkflowRequest(
                CodeHint: "BCA",
                Keyword: "anschluss",
                CurrentOverlay: overlay,
                CodingSessionService: service),
            new CodingEingabemarkerDirectEventWorkflowActions(
                ResolveMeter: () =>
                {
                    calls.Add("meter");
                    return 12.3;
                },
                ResolveVideoTime: () =>
                {
                    calls.Add("time");
                    return TimeSpan.FromSeconds(45);
                },
                LookupLabel: code =>
                {
                    calls.Add($"label:{code}");
                    return "Anschluss";
                },
                CapturePhoto: entry =>
                {
                    calls.Add($"photo:{entry.Code}");
                    return "foto.png";
                },
                RefreshEvents: () => calls.Add("refresh"),
                UpdateToolBadge: () => calls.Add("badge"),
                PersistTraining: ev =>
                {
                    Assert.Same(service.AddedEvent, ev);
                    calls.Add("persist");
                },
                ShowSuccessStatus: (code, label, meter) => calls.Add($"status:{code}:{label}:{meter:F1}")));

        Assert.Equal(["meter", "time", "label:BCA", "photo:BCA", "refresh", "badge", "persist", "status:BCA:Anschluss:12.3"], calls);
        Assert.Same(service.AddedEvent, result.Event);
        Assert.Same(overlay, service.AddedEvent!.Overlay);
        Assert.Equal("BCA", service.AddedEvent.Entry.Code);
        Assert.Equal("Anschluss", service.AddedEvent.Entry.Beschreibung);
        Assert.Equal(12.3, service.AddedEvent.Entry.MeterStart);
        Assert.Equal(TimeSpan.FromSeconds(45), service.AddedEvent.Entry.Zeit);
        Assert.Contains("foto.png", service.AddedEvent.Entry.FotoPaths);
        Assert.Equal("Anschluss", result.Label);
        Assert.Equal(12.3, result.Meter);
    }

    [Fact]
    public void Execute_uses_keyword_as_label_when_catalog_has_no_match()
    {
        var service = new RecordingCodingSessionService();

        var result = CodingEingabemarkerDirectEventWorkflow.Execute(
            new CodingEingabemarkerDirectEventWorkflowRequest(
                CodeHint: "ZZZ",
                Keyword: "freitext",
                CurrentOverlay: null,
                CodingSessionService: service),
            new CodingEingabemarkerDirectEventWorkflowActions(
                ResolveMeter: () => 1.5,
                ResolveVideoTime: () => TimeSpan.FromSeconds(2),
                LookupLabel: _ => null,
                CapturePhoto: _ => null,
                RefreshEvents: () => { },
                UpdateToolBadge: () => { },
                PersistTraining: _ => { },
                ShowSuccessStatus: (_, _, _) => { }));

        Assert.Equal("freitext", result.Label);
        Assert.Equal("freitext", service.AddedEvent!.Entry.Beschreibung);
        Assert.Empty(service.AddedEvent.Entry.FotoPaths);
    }

    private sealed class RecordingCodingSessionService : ICodingSessionService
    {
        public CodingEvent? AddedEvent { get; private set; }
        public double CurrentMeter => 0;
        public double EndMeter => 0;
        public double ProgressPercent => 0;
        public CodingSession? ActiveSession => null;
        public IReadOnlyList<CodingEvent> Events => AddedEvent is null ? [] : [AddedEvent];

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
            AddedEvent = new CodingEvent { Entry = entry, Overlay = overlay };
            return AddedEvent;
        }

        public void UpdateEvent(Guid eventId, ProtocolEntry entry, OverlayGeometry? overlay = null) { }
        public void RemoveEvent(Guid eventId) { }

        public Task IndexConfirmedSampleAsync(
            AuswertungPro.Next.Application.Ai.Training.TrainingSample sample,
            CancellationToken ct = default)
            => Task.CompletedTask;
    }
}
