using System.Collections.Generic;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Player;

public interface ICodingInlineDefectController
{
    CodingInlineDefectAcceptCommandWorkflowResult Accept();
    CodingInlineDefectEditCommandWorkflowResult Edit();
    CodingInlineDefectRejectCommandWorkflowResult Reject();
}

public sealed record CodingInlineDefectControllerBindings(
    Func<bool> HasCodingViewModel,
    Func<CodingEvent?> ResolveSelectedDefect,
    Func<CodingEvent?> ResolveSelectedListEvent,
    Action ExecuteAcceptDefect,
    Action<CodingEvent> SelectDefect,
    Action PausePlayback,
    Func<CodingEvent, bool> TryEdit,
    Func<ICodingSessionService?> ResolveCodingSessionService,
    Action ExecuteEditDefect,
    Func<ICollection<CodingEvent>?> ResolveEventCollection,
    Action ClearSelectedDefect,
    Action<CodingEvent> PersistAcceptedTrainingSample,
    Action<CodingEvent> PersistEditedTrainingSample,
    Action<CodingEvent> UpdateInlineDefectDetail,
    Action HideInlineDefectDetail,
    Action RefreshEvents,
    Action FadeOutAiOverlayAfterAction);

public sealed class CodingInlineDefectController : ICodingInlineDefectController
{
    private readonly CodingInlineDefectControllerBindings _bindings;

    public CodingInlineDefectController(CodingInlineDefectControllerBindings bindings)
    {
        ArgumentNullException.ThrowIfNull(bindings);
        ArgumentNullException.ThrowIfNull(bindings.HasCodingViewModel);
        ArgumentNullException.ThrowIfNull(bindings.ResolveSelectedDefect);
        ArgumentNullException.ThrowIfNull(bindings.ResolveSelectedListEvent);
        ArgumentNullException.ThrowIfNull(bindings.ExecuteAcceptDefect);
        ArgumentNullException.ThrowIfNull(bindings.SelectDefect);
        ArgumentNullException.ThrowIfNull(bindings.PausePlayback);
        ArgumentNullException.ThrowIfNull(bindings.TryEdit);
        ArgumentNullException.ThrowIfNull(bindings.ResolveCodingSessionService);
        ArgumentNullException.ThrowIfNull(bindings.ExecuteEditDefect);
        ArgumentNullException.ThrowIfNull(bindings.ResolveEventCollection);
        ArgumentNullException.ThrowIfNull(bindings.ClearSelectedDefect);
        ArgumentNullException.ThrowIfNull(bindings.PersistAcceptedTrainingSample);
        ArgumentNullException.ThrowIfNull(bindings.PersistEditedTrainingSample);
        ArgumentNullException.ThrowIfNull(bindings.UpdateInlineDefectDetail);
        ArgumentNullException.ThrowIfNull(bindings.HideInlineDefectDetail);
        ArgumentNullException.ThrowIfNull(bindings.RefreshEvents);
        ArgumentNullException.ThrowIfNull(bindings.FadeOutAiOverlayAfterAction);

        _bindings = bindings;
    }

    public CodingInlineDefectAcceptCommandWorkflowResult Accept()
        => CodingInlineDefectAcceptCommandWorkflow.Execute(
            new CodingInlineDefectAcceptCommandActions(
                AcceptDefect: () => CodingInlineDefectDecisionWorkflow.Accept(
                    _bindings.ResolveSelectedDefect,
                    _bindings.ExecuteAcceptDefect,
                    _bindings.PersistAcceptedTrainingSample),
                UpdateInlineDefectDetail: _bindings.UpdateInlineDefectDetail,
                RefreshEvents: _bindings.RefreshEvents,
                FadeOutAiOverlayAfterAction: _bindings.FadeOutAiOverlayAfterAction));

    public CodingInlineDefectEditCommandWorkflowResult Edit()
        => CodingInlineDefectEditCommandWorkflow.Execute(
            new CodingInlineDefectEditCommandRequest(
                _bindings.HasCodingViewModel(),
                _bindings.ResolveSelectedDefect(),
                _bindings.ResolveSelectedListEvent()),
            new CodingInlineDefectEditCommandActions(
                SelectDefect: _bindings.SelectDefect,
                PausePlayback: _bindings.PausePlayback,
                TryEdit: _bindings.TryEdit,
                CompleteEdit: CompleteEdit,
                RefreshEvents: _bindings.RefreshEvents,
                UpdateInlineDefectDetail: _bindings.UpdateInlineDefectDetail));

    public CodingInlineDefectRejectCommandWorkflowResult Reject()
        => CodingInlineDefectRejectCommandWorkflow.Execute(
            new CodingInlineDefectRejectCommandActions(
                RejectDefect: () => CodingInlineDefectDecisionWorkflow.Reject(
                    _bindings.ResolveSelectedDefect(),
                    _bindings.ResolveSelectedListEvent(),
                    _bindings.ResolveCodingSessionService(),
                    _bindings.ResolveEventCollection()),
                ClearSelectedDefect: _bindings.ClearSelectedDefect,
                HideInlineDefectDetail: _bindings.HideInlineDefectDetail,
                RefreshEvents: _bindings.RefreshEvents,
                FadeOutAiOverlayAfterAction: _bindings.FadeOutAiOverlayAfterAction));

    private bool CompleteEdit(CodingEvent codingEvent)
        => CodingInlineDefectDecisionWorkflow.CompleteEdit(
            codingEvent,
            _bindings.ResolveCodingSessionService(),
            _bindings.ExecuteEditDefect,
            _bindings.PersistEditedTrainingSample);
}
