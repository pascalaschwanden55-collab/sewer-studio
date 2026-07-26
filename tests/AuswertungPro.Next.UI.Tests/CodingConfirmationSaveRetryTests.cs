using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Ai.QualityGate;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;
using AuswertungPro.Next.UI.Player;
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// F3-Regression: Ein fehlgeschlagener Goldsave darf nicht mehr still im Tageslog
/// verschwinden. Das Bestaetigungspanel bleibt offen, meldet den Fehler und bietet
/// „Erneut speichern" an; der Erfolgsstatus kommt erst nach erfolgreichem Speichern.
/// </summary>
public sealed class CodingConfirmationSaveRetryTests
{
    // ── Coordinator: Fehler kommt als Ergebnis zurueck (Logging bleibt) ──

    [Fact]
    public async Task Coordinator_meldet_fehler_als_ergebnis_und_loggt_weiter()
    {
        using var temp = new TempDir();
        var logged = new List<string>();
        var coordinator = new CodingTrainingSamplePersistenceCoordinator(
            new CodingTrainingFrameStore(() => temp.Path),
            new CodingTrainingSamplePersister(_ =>
                throw new InvalidOperationException("KB nicht erreichbar")),
            CleanProtector(),
            logged.Add);

        var result = await coordinator.PersistSingleEventAsync(
            MakeEvent(),
            Request(caseId: "H-100"));

        Assert.False(result.Success);
        Assert.Equal("KB nicht erreichbar", result.Error);
        Assert.Contains(logged, message => message.Contains("KB nicht erreichbar"));
    }

    [Fact]
    public async Task Coordinator_meldet_erfolg_bei_gespeichertem_sample()
    {
        using var temp = new TempDir();
        var coordinator = new CodingTrainingSamplePersistenceCoordinator(
            new CodingTrainingFrameStore(() => temp.Path),
            new CodingTrainingSamplePersister(_ => Task.CompletedTask),
            CleanProtector());

        var result = await coordinator.PersistSingleEventAsync(
            MakeEvent(),
            Request(caseId: "H-100"));

        Assert.True(result.Success);
        Assert.Null(result.Error);
    }

    // ── Decision-Controller: Fehlerpfad haelt Panel offen, Retry nutzt denselben Pfad ──

    [Fact]
    public async Task Accept_haelt_panel_offen_und_meldet_fehler_wenn_speichern_fehlschlaegt()
    {
        var calls = new List<string>();
        string? shownError = null;
        var pendingState = PendingState(out _);
        var controller = CreateController(
            pendingState,
            calls,
            persist: (_, operation) =>
            {
                calls.Add($"persist:{operation}");
                return Task.FromResult(
                    CodingTrainingSamplePersistenceResult.Failed("Speicher voll"));
            },
            showError: error =>
            {
                calls.Add("error");
                shownError = error;
            });

        var result = await controller.Accept();

        Assert.True(result.Applied);
        Assert.Equal("Speicher voll", shownError);
        Assert.True(pendingState.HasPendingConfirmation);
        Assert.Equal(["persist:TrainingSaveAccept", "error"], calls);
        Assert.DoesNotContain("hide", calls);
        Assert.DoesNotContain("status", calls);
        Assert.DoesNotContain("pause:False", calls);
    }

    [Fact]
    public async Task RetrySave_ruft_denselben_persistenzpfad_erneut_und_schliesst_bei_erfolg()
    {
        var calls = new List<string>();
        var persistCalls = 0;
        var pendingState = PendingState(out _);
        var controller = CreateController(
            pendingState,
            calls,
            persist: (_, operation) =>
            {
                persistCalls++;
                calls.Add($"persist:{operation}");
                return Task.FromResult(persistCalls == 1
                    ? CodingTrainingSamplePersistenceResult.Failed("Netzwerkfehler")
                    : CodingTrainingSamplePersistenceResult.Ok);
            });

        await controller.Accept();
        Assert.True(pendingState.HasPendingConfirmation);

        var retryResult = await controller.RetrySave();

        Assert.True(retryResult.Applied);
        Assert.Equal(2, persistCalls);
        Assert.False(pendingState.HasPendingConfirmation);
        Assert.Equal(
            ["persist:TrainingSaveAccept", "error", "persist:TrainingSaveAccept", "hide", "pause:False", "status"],
            calls);
    }

    [Fact]
    public async Task RetrySave_nach_reject_fehler_nutzt_weiterhin_die_reject_operation()
    {
        var calls = new List<string>();
        var persistCalls = 0;
        var pendingState = PendingState(out var codingEvent);
        var codingEvents = new List<CodingEvent> { codingEvent };
        var controller = CreateController(
            pendingState,
            calls,
            codingEvents,
            persist: (_, operation) =>
            {
                persistCalls++;
                calls.Add($"persist:{operation}");
                return Task.FromResult(persistCalls == 1
                    ? CodingTrainingSamplePersistenceResult.Failed("Fehler")
                    : CodingTrainingSamplePersistenceResult.Ok);
            });

        await controller.Reject();
        await controller.RetrySave();

        Assert.Equal(2, persistCalls);
        Assert.Equal(
            ["refresh", "persist:TrainingSaveReject", "error", "persist:TrainingSaveReject", "hide", "pause:False", "status"],
            calls);
    }

    [Fact]
    public async Task Accept_erfolg_status_kommt_erst_nach_dem_speichern()
    {
        var calls = new List<string>();
        var pendingState = PendingState(out _);
        var controller = CreateController(
            pendingState,
            calls,
            persist: (_, operation) =>
            {
                calls.Add($"persist:{operation}");
                return Task.FromResult(CodingTrainingSamplePersistenceResult.Ok);
            });

        await controller.Accept();

        Assert.Equal(
            ["persist:TrainingSaveAccept", "hide", "pause:False", "status"],
            calls);
    }

    // ── Command-Workflow: Fehlerzweig ──

    [Fact]
    public async Task Command_workflow_zeigt_fehler_ohne_zu_schliessen_oder_fortzusetzen()
    {
        var calls = new List<string>();

        var result = await CodingConfirmationDecisionCommandWorkflow.Execute(
            new CodingConfirmationDecisionCommandActions(
                ApplyDecision: () =>
                {
                    calls.Add("decision");
                    return Task.FromResult(
                        CodingConfirmationDecisionApplyOutcome.PersistenceFailed("Grund"));
                },
                CloseConfirmationPanel: () => calls.Add("close"),
                ResumeAfterConfirmation: () => calls.Add("resume"),
                ShowPersistenceError: error => calls.Add($"error:{error}")));

        Assert.True(result.Applied);
        Assert.Equal(["decision", "error:Grund"], calls);
    }

    // ── Hilfen ──

    private static CodingConfirmationDecisionController CreateController(
        CodingPendingConfirmationStateController pendingState,
        List<string> calls,
        ICollection<CodingEvent>? codingEvents = null,
        Func<CodingEvent, string, Task<CodingTrainingSamplePersistenceResult>>? persist = null,
        Action<string?>? showError = null)
        => new(
            pendingState,
            new CodingConfirmationDecisionControllerActions(
                ResolveCodingSessionService: () => null,
                ResolveCodingEvents: () => codingEvents,
                PersistTrainingSample: persist ?? ((_, operation) =>
                {
                    calls.Add($"persist:{operation}");
                    return Task.FromResult(CodingTrainingSamplePersistenceResult.Ok);
                }),
                RefreshCodingEvents: () => calls.Add("refresh"),
                HideConfirmationPanel: () => calls.Add("hide"),
                ShowPersistenceError: showError ?? (_ => calls.Add("error")),
                SelectEvent: _ => calls.Add("select"),
                IsLiveAiEnabled: () => true,
                ResolveModelName: () => "qwen",
                SetPause: paused => calls.Add($"pause:{paused}"),
                ApplyResumeStatus: _ => calls.Add("status")));

    private static CodingPendingConfirmationStateController PendingState(out CodingEvent codingEvent)
    {
        codingEvent = MakeEvent();
        var state = new CodingPendingConfirmationStateController();
        state.Store(
            codingEvent,
            new QualityGateResult(
                0.8,
                TrafficLight.Yellow,
                new Dictionary<string, double>(),
                "test"));
        return state;
    }

    private static CodingEvent MakeEvent()
        => new()
        {
            Entry = new ProtocolEntry { Code = "BBA", Beschreibung = "Riss" },
            AiContext = new CodingEventAiContext
            {
                Decision = CodingUserDecision.Accepted,
                SuggestedCode = "BBA",
                Confidence = 0.8,
                Reason = "KI-Vorschlag"
            },
            MeterAtCapture = 1.2
        };

    private static CodingTrainingSamplePersistenceRequest Request(string caseId)
        => new(
            CaseId: caseId,
            InspectionDate: null,
            ConfirmedByUser: "tester",
            ConfirmedAtUtc: new DateTime(2026, 6, 23, 10, 11, 12, DateTimeKind.Utc),
            PreferredFrameBytes: null,
            CaptureFrameAsync: () => Task.FromResult<byte[]?>(null));

    private static CodingTrainingSampleEvalProtector CleanProtector()
        => new(() => new EvalContaminationSets(new HashSet<string>(), new HashSet<string>()));

    private sealed class TempDir : IDisposable
    {
        public TempDir()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"sewer-save-retry-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
                Directory.Delete(Path, recursive: true);
        }
    }
}
