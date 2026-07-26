using System;
using System.Windows.Media;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.QualityGate;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Ai.Coding;

public sealed record CodingConfirmationPauseWorkflowRequest(
    CodingEvent CodingEvent,
    QualityGateResult GateResult,
    string CurrentStatusText,
    ICodingSessionService? CodingSessionService);

public sealed record CodingConfirmationPauseWorkflowActions(
    Action<bool> SetPause,
    Action<CodingEvent, QualityGateResult> StorePendingConfirmation,
    Func<CodingEvent, QualityGateResult, Color> ApplyConfirmationPanel,
    Action<string, Color, string> ShowStatus);

public readonly record struct CodingConfirmationPauseWorkflowResult(
    Color AmpelColor,
    string DetailText);

public static class CodingConfirmationPauseWorkflow
{
    public static CodingConfirmationPauseWorkflowResult Execute(
        CodingConfirmationPauseWorkflowRequest request,
        CodingConfirmationPauseWorkflowActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actions);
        ArgumentNullException.ThrowIfNull(request.CodingEvent);
        ArgumentNullException.ThrowIfNull(request.GateResult);
        ArgumentNullException.ThrowIfNull(actions.SetPause);
        ArgumentNullException.ThrowIfNull(actions.StorePendingConfirmation);
        ArgumentNullException.ThrowIfNull(actions.ApplyConfirmationPanel);
        ArgumentNullException.ThrowIfNull(actions.ShowStatus);

        PlayerConfirmationPlayback.PauseCodingConfirmation(actions.SetPause);
        request.CodingSessionService?.SetWaitingForInput();
        actions.StorePendingConfirmation(request.CodingEvent, request.GateResult);

        var ampelColor = actions.ApplyConfirmationPanel(request.CodingEvent, request.GateResult);
        var detailText = CodingConfirmationDisplayPolicy.QualityGateStatusText(request.GateResult);

        actions.ShowStatus(request.CurrentStatusText, ampelColor, detailText);

        return new CodingConfirmationPauseWorkflowResult(ampelColor, detailText);
    }
}
