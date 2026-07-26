using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;

namespace AuswertungPro.Next.UI.Player;

public sealed record CodingConfirmationDecisionControllerActions(
    Func<ICodingSessionService?> ResolveCodingSessionService,
    Func<ICollection<CodingEvent>?> ResolveCodingEvents,
    Func<CodingEvent, string, Task<CodingTrainingSamplePersistenceResult>> PersistTrainingSample,
    Action RefreshCodingEvents,
    Action HideConfirmationPanel,
    Action<string?> ShowPersistenceError,
    Action<CodingEvent> SelectEvent,
    Func<bool> IsLiveAiEnabled,
    Func<string> ResolveModelName,
    Action<bool> SetPause,
    Action<CodingLiveAiStatusState> ApplyResumeStatus);

public sealed class CodingConfirmationDecisionController
{
    private const string AcceptTrainingOperation = "TrainingSaveAccept";
    private const string RejectTrainingOperation = "TrainingSaveReject";

    private readonly CodingPendingConfirmationStateController _pendingState;
    private readonly CodingConfirmationDecisionControllerActions _actions;
    private string _pendingOperation = AcceptTrainingOperation;

    public CodingConfirmationDecisionController(
        CodingPendingConfirmationStateController pendingState,
        CodingConfirmationDecisionControllerActions actions)
    {
        ArgumentNullException.ThrowIfNull(pendingState);
        ArgumentNullException.ThrowIfNull(actions);
        ArgumentNullException.ThrowIfNull(actions.ResolveCodingSessionService);
        ArgumentNullException.ThrowIfNull(actions.ResolveCodingEvents);
        ArgumentNullException.ThrowIfNull(actions.PersistTrainingSample);
        ArgumentNullException.ThrowIfNull(actions.RefreshCodingEvents);
        ArgumentNullException.ThrowIfNull(actions.HideConfirmationPanel);
        ArgumentNullException.ThrowIfNull(actions.ShowPersistenceError);
        ArgumentNullException.ThrowIfNull(actions.SelectEvent);
        ArgumentNullException.ThrowIfNull(actions.IsLiveAiEnabled);
        ArgumentNullException.ThrowIfNull(actions.ResolveModelName);
        ArgumentNullException.ThrowIfNull(actions.SetPause);
        ArgumentNullException.ThrowIfNull(actions.ApplyResumeStatus);

        _pendingState = pendingState;
        _actions = actions;
    }

    public Task<CodingConfirmationDecisionCommandResult> Accept()
        => CodingConfirmationDecisionCommandWorkflow.Execute(
            new CodingConfirmationDecisionCommandActions(
                ApplyDecision: () =>
                {
                    CodingEvent? eventToPersist = null;
                    var applied = CodingConfirmationDecisionWorkflow.Accept(
                        _pendingState.CodingEvent,
                        _pendingState.GateResult,
                        codingEvent => eventToPersist = codingEvent);
                    return PersistAsync(eventToPersist, applied, AcceptTrainingOperation);
                },
                CloseConfirmationPanel: Close,
                ResumeAfterConfirmation: Resume,
                ShowPersistenceError: _actions.ShowPersistenceError));

    public CodingConfirmationEditCommandWorkflowResult Edit()
        => CodingConfirmationEditCommandWorkflow.Execute(
            new CodingConfirmationEditCommandActions(
                EditConfirmation: () => CodingConfirmationDecisionWorkflow.Edit(
                    _pendingState.CodingEvent,
                    _pendingState.GateResult),
                CloseConfirmationPanel: Close,
                SelectEvent: _actions.SelectEvent,
                ResumeAfterConfirmation: Resume));

    public Task<CodingConfirmationDecisionCommandResult> Reject()
        => CodingConfirmationDecisionCommandWorkflow.Execute(
            new CodingConfirmationDecisionCommandActions(
                ApplyDecision: () =>
                {
                    CodingEvent? eventToPersist = null;
                    var applied = CodingConfirmationDecisionWorkflow.Reject(
                        _pendingState.CodingEvent,
                        _pendingState.GateResult,
                        _actions.ResolveCodingSessionService(),
                        _actions.ResolveCodingEvents(),
                        codingEvent => eventToPersist = codingEvent,
                        _actions.RefreshCodingEvents);
                    return PersistAsync(eventToPersist, applied, RejectTrainingOperation);
                },
                CloseConfirmationPanel: Close,
                ResumeAfterConfirmation: Resume,
                ShowPersistenceError: _actions.ShowPersistenceError));

    /// <summary>
    /// „Erneut speichern" nach einem fehlgeschlagenen Goldsave: ruft denselben
    /// Persistenzpfad nochmals auf. Entscheidung (Accept/Reject) ist bereits markiert;
    /// bei Erfolg wird wie beim ersten Versuch geschlossen und fortgesetzt.
    /// </summary>
    public Task<CodingConfirmationDecisionCommandResult> RetrySave()
        => CodingConfirmationDecisionCommandWorkflow.Execute(
            new CodingConfirmationDecisionCommandActions(
                ApplyDecision: () => PersistAsync(
                    _pendingState.CodingEvent,
                    applied: true,
                    _pendingOperation),
                CloseConfirmationPanel: Close,
                ResumeAfterConfirmation: Resume,
                ShowPersistenceError: _actions.ShowPersistenceError));

    private async Task<CodingConfirmationDecisionApplyOutcome> PersistAsync(
        CodingEvent? eventToPersist,
        bool applied,
        string operation)
    {
        if (!applied || eventToPersist is null)
            return CodingConfirmationDecisionApplyOutcome.Skipped;

        _pendingOperation = operation;
        try
        {
            // Bewusst auf dem Aufrufkontext (UI-Thread) bleiben: der Workflow blendet danach
            // Panel/Status um. PersistTrainingSample meldet fachliche Fehler als Ergebnis
            // (statt sie nur zu loggen); der catch hier deckt zusaetzlich technische Fehler
            // (z.B. bei der Request-Erstellung) ab.
            var result = await _actions.PersistTrainingSample(eventToPersist, operation);
            return result.Success
                ? CodingConfirmationDecisionApplyOutcome.Saved
                : CodingConfirmationDecisionApplyOutcome.PersistenceFailed(result.Error);
        }
        catch (Exception ex)
        {
            return CodingConfirmationDecisionApplyOutcome.PersistenceFailed(ex.Message);
        }
    }

    private void Close()
    {
        _actions.HideConfirmationPanel();
        _pendingState.Clear();
    }

    private void Resume()
    {
        var result = CodingConfirmationResumeWorkflow.Apply(
            _actions.ResolveCodingSessionService(),
            _actions.IsLiveAiEnabled(),
            _actions.ResolveModelName(),
            _actions.SetPause);
        _actions.ApplyResumeStatus(result.Status);
    }
}
