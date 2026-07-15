using System.Windows.Media;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.QualityGate;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Player;

public interface ICodingConfirmationController
{
    void PauseAndAsk(CodingEvent codingEvent, QualityGateResult gateResult);
    CodingConfirmationDecisionCommandResult Accept();
    CodingConfirmationEditCommandWorkflowResult Edit();
    CodingConfirmationDecisionCommandResult Reject();
}

public sealed record CodingConfirmationControllerBindings(
    Func<string> ResolveCurrentStatusText,
    Func<ICodingSessionService?> ResolveCodingSessionService,
    Action<bool> SetPause,
    Func<CodingEvent, QualityGateResult, Color> ApplyConfirmationPanel,
    Action<string, Color, string> ShowStatus,
    Func<CodingConfirmationDecisionCommandResult> Accept,
    Func<CodingConfirmationEditCommandWorkflowResult> Edit,
    Func<CodingConfirmationDecisionCommandResult> Reject);

public sealed class CodingConfirmationController : ICodingConfirmationController
{
    private readonly CodingPendingConfirmationStateController _pendingState;
    private readonly CodingConfirmationControllerBindings _bindings;

    public CodingConfirmationController(
        CodingPendingConfirmationStateController pendingState,
        CodingConfirmationControllerBindings bindings)
    {
        ArgumentNullException.ThrowIfNull(pendingState);
        ArgumentNullException.ThrowIfNull(bindings);
        ArgumentNullException.ThrowIfNull(bindings.ResolveCurrentStatusText);
        ArgumentNullException.ThrowIfNull(bindings.ResolveCodingSessionService);
        ArgumentNullException.ThrowIfNull(bindings.SetPause);
        ArgumentNullException.ThrowIfNull(bindings.ApplyConfirmationPanel);
        ArgumentNullException.ThrowIfNull(bindings.ShowStatus);
        ArgumentNullException.ThrowIfNull(bindings.Accept);
        ArgumentNullException.ThrowIfNull(bindings.Edit);
        ArgumentNullException.ThrowIfNull(bindings.Reject);

        _pendingState = pendingState;
        _bindings = bindings;
    }

    public void PauseAndAsk(CodingEvent codingEvent, QualityGateResult gateResult)
        => CodingConfirmationPauseWorkflow.Execute(
            new CodingConfirmationPauseWorkflowRequest(
                codingEvent,
                gateResult,
                _bindings.ResolveCurrentStatusText(),
                _bindings.ResolveCodingSessionService()),
            new CodingConfirmationPauseWorkflowActions(
                SetPause: _bindings.SetPause,
                StorePendingConfirmation: _pendingState.Store,
                ApplyConfirmationPanel: _bindings.ApplyConfirmationPanel,
                ShowStatus: _bindings.ShowStatus));

    public CodingConfirmationDecisionCommandResult Accept()
        => _bindings.Accept();

    public CodingConfirmationEditCommandWorkflowResult Edit()
        => _bindings.Edit();

    public CodingConfirmationDecisionCommandResult Reject()
        => _bindings.Reject();
}
