using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.QualityGate;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingLiveFindingEventWorkflowTests
{
    [Fact]
    public void Execute_adds_finding_runs_post_actions_and_requests_confirmation_last()
    {
        var calls = new List<string>();
        ProtocolEntry? addedEntry = null;
        CodingEvent? addedEvent = null;
        CodingEvent? confirmationEvent = null;
        QualityGateResult? confirmationGate = null;
        var sessionService = new RecordingCodingSessionService(entry =>
        {
            calls.Add("add");
            addedEntry = entry;
            addedEvent = new CodingEvent
            {
                Entry = entry,
                MeterAtCapture = entry.MeterStart ?? 0
            };
            return addedEvent;
        });

        var result = CodingLiveFindingEventWorkflow.Execute(
            new CodingLiveFindingEventWorkflowRequest(
                ValidFindings: [Finding("BAB", severity: 4)],
                Meter: 12.3,
                VideoTime: TimeSpan.FromSeconds(9),
                CodingSessionService: sessionService,
                ViewEvents: [],
                QualityGate: null),
            new CodingLiveFindingEventWorkflowActions(
                IsFindingTooFarAhead: _ => false,
                LookupVsaLabel: code => code == "BAB" ? "Riss" : null,
                AttachAnalyzedFramePhoto: entry => calls.Add("attach"),
                Trace: message => calls.Add("trace:" + message),
                RefreshEvents: () => calls.Add("refresh"),
                RenderAiOverlays: () => calls.Add("render-ai"),
                TryRenderCurrentOverlay: () => calls.Add("render-current-overlay"),
                UpdateToolBadge: () => calls.Add("badge"),
                PauseAndAskConfirmation: (codingEvent, gate) =>
                {
                    calls.Add("confirm");
                    confirmationEvent = codingEvent;
                    confirmationGate = gate;
                }));

        Assert.Equal(
            ["attach", "add", "refresh", "render-ai", "render-current-overlay", "badge", "confirm"],
            calls);
        Assert.Equal(1, result.AddedCount);
        Assert.Equal(0, result.SkippedCount);
        Assert.Equal(0, result.CoveredCount);
        Assert.True(result.ConfirmationRequested);
        Assert.Same(addedEvent, confirmationEvent);
        Assert.NotNull(confirmationGate);
        Assert.NotNull(addedEntry);
        Assert.Equal("BAB", addedEntry.Code);
        Assert.Equal("Riss", addedEntry.Beschreibung);
        Assert.Equal(12.3, addedEntry.MeterStart);
        Assert.Equal(TimeSpan.FromSeconds(9), addedEntry.Zeit);
    }

    [Fact]
    public void Execute_marks_covered_existing_finding_without_post_actions()
    {
        var existing = new CodingEvent
        {
            Entry = new ProtocolEntry
            {
                Code = "BAB",
                MeterStart = 1,
                MeterEnd = 5,
                IsStreckenschaden = true
            },
            MeterAtCapture = 2
        };

        var result = CodingLiveFindingEventWorkflow.Execute(
            new CodingLiveFindingEventWorkflowRequest(
                ValidFindings: [Finding("BAB", severity: 2)],
                Meter: 4.8,
                VideoTime: TimeSpan.FromSeconds(4),
                CodingSessionService: new RecordingCodingSessionService(_ => throw new InvalidOperationException("No event should be added.")),
                ViewEvents: [existing],
                QualityGate: null),
            NoPostActions());

        Assert.Equal(0, result.AddedCount);
        Assert.Equal(0, result.SkippedCount);
        Assert.Equal(1, result.CoveredCount);
        Assert.False(result.ConfirmationRequested);
        Assert.Equal(4.8, existing.MeterAtCapture);
    }

    private static CodingLiveFindingEventWorkflowActions NoPostActions()
        => new(
            IsFindingTooFarAhead: _ => false,
            LookupVsaLabel: _ => null,
            AttachAnalyzedFramePhoto: _ => throw new InvalidOperationException("No event should be added."),
            Trace: _ => throw new InvalidOperationException("No trace should be written."),
            RefreshEvents: () => throw new InvalidOperationException("No refresh should run."),
            RenderAiOverlays: () => throw new InvalidOperationException("No AI overlay render should run."),
            TryRenderCurrentOverlay: () => throw new InvalidOperationException("No current overlay render should run."),
            UpdateToolBadge: () => throw new InvalidOperationException("No badge update should run."),
            PauseAndAskConfirmation: (_, _) => throw new InvalidOperationException("No confirmation should run."));

    private static LiveFrameFinding Finding(string code, int severity)
        => new(
            Label: code,
            Severity: severity,
            PositionClock: null,
            ExtentPercent: null,
            VsaCodeHint: code);

    private sealed class RecordingCodingSessionService(Func<ProtocolEntry, CodingEvent> addEvent) : ICodingSessionService
    {
        public double CurrentMeter => 0;
        public double EndMeter => 0;
        public double ProgressPercent => 0;
        public CodingSession? ActiveSession => null;
        public IReadOnlyList<CodingEvent> Events => [];

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
        public CodingEvent AddEvent(ProtocolEntry entry, OverlayGeometry? overlay = null) => addEvent(entry);
        public void UpdateEvent(Guid eventId, ProtocolEntry entry, OverlayGeometry? overlay = null) { }
        public void RemoveEvent(Guid eventId) { }

        public Task IndexConfirmedSampleAsync(
            AuswertungPro.Next.Application.Ai.Training.TrainingSample sample,
            CancellationToken ct = default)
            => Task.CompletedTask;
    }
}
