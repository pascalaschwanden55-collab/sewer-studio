using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Player;

public sealed record CodingConfirmationDecisionControllerActions(
    Func<ICodingSessionService?> ResolveCodingSessionService,
    Func<ICollection<CodingEvent>?> ResolveCodingEvents,
    Action<CodingEvent, string> PersistTrainingSample,
    Action RefreshCodingEvents,
    Action HideConfirmationPanel,
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
        ArgumentNullException.ThrowIfNull(actions.SelectEvent);
        ArgumentNullException.ThrowIfNull(actions.IsLiveAiEnabled);
        ArgumentNullException.ThrowIfNull(actions.ResolveModelName);
        ArgumentNullException.ThrowIfNull(actions.SetPause);
        ArgumentNullException.ThrowIfNull(actions.ApplyResumeStatus);

        _pendingState = pendingState;
        _actions = actions;
    }

    public CodingConfirmationDecisionCommandResult Accept()
        => CodingConfirmationDecisionCommandWorkflow.Execute(
            new CodingConfirmationDecisionCommandActions(
                ApplyDecision: () => CodingConfirmationDecisionWorkflow.Accept(
                    _pendingState.CodingEvent,
                    _pendingState.GateResult,
                    codingEvent => _actions.PersistTrainingSample(
                        codingEvent,
                        AcceptTrainingOperation)),
                CloseConfirmationPanel: Close,
                ResumeAfterConfirmation: Resume));

    public CodingConfirmationEditCommandWorkflowResult Edit()
        => CodingConfirmationEditCommandWorkflow.Execute(
            new CodingConfirmationEditCommandActions(
                EditConfirmation: () => CodingConfirmationDecisionWorkflow.Edit(
                    _pendingState.CodingEvent,
                    _pendingState.GateResult),
                CloseConfirmationPanel: Close,
                SelectEvent: _actions.SelectEvent,
                ResumeAfterConfirmation: Resume));

    public CodingConfirmationDecisionCommandResult Reject()
        => CodingConfirmationDecisionCommandWorkflow.Execute(
            new CodingConfirmationDecisionCommandActions(
                ApplyDecision: () => CodingConfirmationDecisionWorkflow.Reject(
                    _pendingState.CodingEvent,
                    _pendingState.GateResult,
                    _actions.ResolveCodingSessionService(),
                    _actions.ResolveCodingEvents(),
                    codingEvent => _actions.PersistTrainingSample(
                        codingEvent,
                        RejectTrainingOperation),
                    _actions.RefreshCodingEvents),
                CloseConfirmationPanel: Close,
                ResumeAfterConfirmation: Resume));

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
