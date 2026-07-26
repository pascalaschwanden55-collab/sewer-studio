using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingEingabemarkerSubmissionControllerTests
{
    [Fact]
    public async Task SubmitAsync_adds_direct_event_in_existing_order()
    {
        var calls = new List<string>();
        var service = new RecordingCodingSessionService(calls);
        var overlay = new OverlayGeometry();
        var controller = new CodingEingabemarkerSubmissionController(
            Bindings(calls, service, overlay));

        var result = await controller.SubmitAsync("  Riss  ");

        Assert.Equal(CodingEingabemarkerSubmissionWorkflowOutcome.DirectEventAdded, result.Outcome);
        Assert.Equal(
            [
                "has-vm",
                "service",
                "hide",
                "analyzing",
                "code:Riss",
                "events",
                "meter",
                "overlay",
                "meter",
                "time",
                "label:BAB",
                "photo:BAB",
                "add:BAB",
                "refresh",
                "badge",
                "persist",
                "status:BAB:Rissbeschreibung:12.3",
                "cancel"
            ],
            calls);
        Assert.Same(overlay, service.AddedEvent!.Overlay);
        Assert.Contains("foto.png", service.AddedEvent.Entry.FotoPaths);
    }

    [Fact]
    public async Task SubmitAsync_Persistenzfehler_zeigt_keinen_Erfolgsstatus()
    {
        var calls = new List<string>();
        var service = new RecordingCodingSessionService(calls);
        var controller = new CodingEingabemarkerSubmissionController(
            Bindings(
                calls,
                service,
                overlay: null,
                persistTrainingAsync: _ => Task.FromResult(
                    CodingTrainingSamplePersistenceResult.Failed("JSON gesperrt"))));

        var result = await controller.SubmitAsync("Riss");

        Assert.Equal(
            CodingEingabemarkerSubmissionWorkflowOutcome.PersistenceFailed,
            result.Outcome);
        Assert.Contains("error:JSON gesperrt", calls);
        Assert.DoesNotContain(
            calls,
            call => call.StartsWith("status:", StringComparison.Ordinal));
        Assert.Equal("cancel", calls[^1]);
    }

    [Fact]
    public async Task SubmitAsync_runs_ai_fallback_and_always_cancels_marker()
    {
        var calls = new List<string>();
        var controller = new CodingEingabemarkerSubmissionController(
            Bindings(
                calls,
                service: null,
                overlay: null,
                resolveCodeHint: keyword =>
                {
                    calls.Add($"code:{keyword}");
                    return null;
                }));

        var result = await controller.SubmitAsync("unbekannt");

        Assert.Equal(CodingEingabemarkerSubmissionWorkflowOutcome.AiFallbackStarted, result.Outcome);
        Assert.Equal(
            [
                "has-vm",
                "service",
                "hide",
                "analyzing",
                "code:unbekannt",
                "ai-status:unbekannt",
                "ai:unbekannt",
                "cancel"
            ],
            calls);
    }

    private static CodingEingabemarkerSubmissionControllerBindings Bindings(
        List<string> calls,
        ICodingSessionService? service,
        OverlayGeometry? overlay,
        Func<string, string?>? resolveCodeHint = null,
        Func<CodingEvent, Task<CodingTrainingSamplePersistenceResult>>? persistTrainingAsync = null)
        => new(
            HasCodingViewModel: () =>
            {
                calls.Add("has-vm");
                return true;
            },
            ResolveCodingSessionService: () =>
            {
                calls.Add("service");
                return service;
            },
            HideInput: () => calls.Add("hide"),
            SetAnalyzingPhase: () => calls.Add("analyzing"),
            ResolveCodeHint: resolveCodeHint ?? (keyword =>
            {
                calls.Add($"code:{keyword}");
                return "BAB";
            }),
            ResolveEvents: () =>
            {
                calls.Add("events");
                return [];
            },
            ShowDuplicateStatus: (code, meter) => calls.Add($"duplicate-status:{code}:{meter:F1}"),
            ResolveCurrentOverlay: () =>
            {
                calls.Add("overlay");
                return overlay;
            },
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
                return "Rissbeschreibung";
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
                Assert.Same((service as RecordingCodingSessionService)?.AddedEvent, ev);
                calls.Add("persist");
            },
            ShowSuccessStatus: (code, label, meter) =>
                calls.Add($"status:{code}:{label}:{meter:F1}"),
            ShowAiFallbackStatus: keyword => calls.Add($"ai-status:{keyword}"),
            RunAiFallbackAsync: keyword =>
            {
                calls.Add($"ai:{keyword}");
                return Task.CompletedTask;
            },
            ShowErrorStatus: message => calls.Add($"error:{message}"),
            CancelMarker: () => calls.Add("cancel"),
            PersistTrainingAsync: persistTrainingAsync);

    private sealed class RecordingCodingSessionService(List<string> calls) : ICodingSessionService
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
            calls.Add($"add:{entry.Code}");
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
