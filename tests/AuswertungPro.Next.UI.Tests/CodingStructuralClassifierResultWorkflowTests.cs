using System.Windows.Media;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingStructuralClassifierResultWorkflowTests
{
    [Fact]
    public void Execute_ignores_non_structural_classifier_result()
    {
        var result = CodingStructuralClassifierResultWorkflow.Execute(
            new CodingStructuralClassifierResultWorkflowRequest(
                Result(code: "BCE"),
                Meter: 4.2,
                VideoTime: TimeSpan.FromSeconds(8),
                ViewEvents: [],
                CodingSessionService: new RecordingCodingSessionService(_ => throw new InvalidOperationException("No event should be added.")),
                MeterFromOsd: true),
            NoActions());

        Assert.Equal(CodingStructuralClassifierResultWorkflowOutcome.NotHandled, result.Outcome);
        Assert.False(result.Handled);
    }

    [Fact]
    public void Execute_marks_covering_existing_event_without_adding_duplicate()
    {
        var calls = new List<string>();
        var existing = new CodingEvent
        {
            Entry = new ProtocolEntry { Code = "BCA", MeterStart = 4.0 },
            MeterAtCapture = 4.0
        };

        var result = CodingStructuralClassifierResultWorkflow.Execute(
            new CodingStructuralClassifierResultWorkflowRequest(
                Result(code: "BCA", confidence: 0.82),
                Meter: 4.2,
                VideoTime: TimeSpan.FromSeconds(8),
                ViewEvents: [existing],
                CodingSessionService: new RecordingCodingSessionService(_ => throw new InvalidOperationException("No event should be added.")),
                MeterFromOsd: true),
            Actions(
                lookupVsaLabel: code => code == "BCA" ? "Anschluss" : null,
                resolveFindingCodeForCoding: (finding, meter) =>
                {
                    calls.Add("resolve");
                    Assert.Equal("Anschluss", finding.Label);
                    Assert.Equal(4.2, meter);
                    return "BCA";
                },
                clearDetectionOverlays: () => calls.Add("clear-overlays"),
                clearMasks: () => calls.Add("clear-masks"),
                showResolvedFinding: (finding, code) =>
                {
                    calls.Add($"show:{code}");
                    Assert.Equal("Anschluss", finding.Label);
                },
                setAiState: (status, color, detail) =>
                {
                    calls.Add($"state:{status}|{detail}");
                    Assert.Equal(PlayerStatusColors.Success, color);
                }));

        Assert.Equal(
            ["resolve", "clear-overlays", "clear-masks", "show:BCA", "state:Anschluss bereits vorhanden|Klassifikator 82%"],
            calls);
        Assert.Equal(CodingStructuralClassifierResultWorkflowOutcome.CoveredExisting, result.Outcome);
        Assert.True(result.Handled);
    }

    [Fact]
    public void Execute_adds_new_structural_event_after_attaching_analyzed_frame()
    {
        var calls = new List<string>();
        ProtocolEntry? addedEntry = null;
        var sessionService = new RecordingCodingSessionService(entry =>
        {
            calls.Add("add");
            addedEntry = entry;
            return new CodingEvent { Entry = entry };
        });

        var result = CodingStructuralClassifierResultWorkflow.Execute(
            new CodingStructuralClassifierResultWorkflowRequest(
                Result(code: "BCC", confidence: 0.91),
                Meter: 7.5,
                VideoTime: TimeSpan.FromSeconds(12),
                ViewEvents: [],
                CodingSessionService: sessionService,
                MeterFromOsd: false),
            Actions(
                lookupVsaLabel: code => code == "BCC" ? "Bogen" : null,
                resolveFindingCodeForCoding: (finding, meter) =>
                {
                    calls.Add("resolve");
                    Assert.Equal("Bogen", finding.Label);
                    Assert.Equal(7.5, meter);
                    return "BCC";
                },
                clearDetectionOverlays: () => calls.Add("clear-overlays"),
                clearMasks: () => calls.Add("clear-masks"),
                showResolvedFinding: (_, code) => calls.Add($"show:{code}"),
                attachAnalyzedFramePhoto: entry =>
                {
                    calls.Add("attach");
                    Assert.Equal("BCC", entry.Code);
                },
                refreshEvents: () => calls.Add("refresh"),
                setAiState: (status, color, detail) =>
                {
                    calls.Add($"state:{status}|{detail}");
                    Assert.Equal(PlayerStatusColors.Success, color);
                }));

        Assert.Equal(
            ["resolve", "clear-overlays", "clear-masks", "show:BCC", "attach", "add", "refresh", "state:Bogen erkannt|Klassifikator 91%"],
            calls);
        Assert.Equal(CodingStructuralClassifierResultWorkflowOutcome.Added, result.Outcome);
        Assert.True(result.Handled);
        Assert.NotNull(addedEntry);
        Assert.Equal("BCC", addedEntry.Code);
        Assert.Equal("Bogen", addedEntry.Beschreibung);
        Assert.Equal(7.5, addedEntry.MeterStart);
        Assert.Equal(TimeSpan.FromSeconds(12), addedEntry.Zeit);
        Assert.Equal("geschaetzt", addedEntry.CodeMeta!.Parameters["vsa.meter.quelle"]);
    }

    private static CodingStructuralClassifierResultWorkflowActions NoActions()
        => Actions(
            lookupVsaLabel: _ => throw new InvalidOperationException("Lookup should not run."),
            resolveFindingCodeForCoding: (_, _) => throw new InvalidOperationException("Resolve should not run."),
            clearDetectionOverlays: () => throw new InvalidOperationException("Clear overlays should not run."),
            clearMasks: () => throw new InvalidOperationException("Clear masks should not run."),
            showResolvedFinding: (_, _) => throw new InvalidOperationException("Show finding should not run."),
            attachAnalyzedFramePhoto: _ => throw new InvalidOperationException("Attach should not run."),
            refreshEvents: () => throw new InvalidOperationException("Refresh should not run."),
            setAiState: (_, _, _) => throw new InvalidOperationException("Set state should not run."));

    private static CodingStructuralClassifierResultWorkflowActions Actions(
        Func<string, string?>? lookupVsaLabel = null,
        Func<LiveFrameFinding, double, string?>? resolveFindingCodeForCoding = null,
        Action? clearDetectionOverlays = null,
        Action? clearMasks = null,
        Action<LiveFrameFinding, string>? showResolvedFinding = null,
        Action<ProtocolEntry>? attachAnalyzedFramePhoto = null,
        Action? refreshEvents = null,
        Action<string, Color, string?>? setAiState = null)
        => new(
            LookupVsaLabel: lookupVsaLabel ?? (_ => null),
            ResolveFindingCodeForCoding: resolveFindingCodeForCoding ?? ((_, _) => null),
            ClearDetectionOverlays: clearDetectionOverlays ?? (() => { }),
            ClearMasks: clearMasks ?? (() => { }),
            ShowResolvedFinding: showResolvedFinding ?? ((_, _) => { }),
            AttachAnalyzedFramePhoto: attachAnalyzedFramePhoto ?? (_ => { }),
            RefreshEvents: refreshEvents ?? (() => { }),
            SetAiState: setAiState ?? ((_, _, _) => { }));

    private static SingleFrameResult Result(string? code, double? confidence = null, bool hasDetections = false)
        => new(
            IsRelevant: true,
            DinoDetections: hasDetections
                ? [new DinoDetectionDto(0, 0, 10, 10, "finding", 0.8, "finding")]
                : [],
            SamResponse: null,
            QuantifiedMasks: [],
            YoloTimeMs: 0,
            DinoTimeMs: 0,
            SamTimeMs: 0,
            Error: null,
            ClassifierCode: code,
            ClassifierConfidence: confidence);

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
