using System.Collections.Generic;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;

namespace AuswertungPro.Next.UI.Player;

public interface ICodingInlineDefectController
{
    CodingInlineDefectAcceptCommandWorkflowResult Accept();
    Task<CodingInlineDefectAcceptCommandWorkflowResult> AcceptAsync();
    CodingInlineDefectEditCommandWorkflowResult Edit();
    Task<CodingInlineDefectEditCommandWorkflowResult> EditAsync();
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
    Action FadeOutAiOverlayAfterAction,
    Func<CodingEvent, Task<CodingTrainingSamplePersistenceResult>>? PersistAcceptedTrainingSampleAsync = null,
    Func<CodingEvent, Task<CodingTrainingSamplePersistenceResult>>? PersistEditedTrainingSampleAsync = null,
    Action<string>? ShowPersistenceError = null);

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

    public async Task<CodingInlineDefectAcceptCommandWorkflowResult> AcceptAsync()
    {
        _bindings.ExecuteAcceptDefect();

        var selectedDefect = _bindings.ResolveSelectedDefect();
        if (selectedDefect is null)
        {
            return new CodingInlineDefectAcceptCommandWorkflowResult(
                CodingInlineDefectAcceptCommandWorkflowOutcome.NotAccepted);
        }

        var persistence = await PersistAcceptedAsync(selectedDefect);
        if (!persistence.Success)
        {
            var error = persistence.Error ?? "Training konnte nicht gespeichert werden.";
            _bindings.ShowPersistenceError?.Invoke(error);
            return new CodingInlineDefectAcceptCommandWorkflowResult(
                CodingInlineDefectAcceptCommandWorkflowOutcome.PersistenceFailed,
                error);
        }

        _bindings.UpdateInlineDefectDetail(selectedDefect);
        _bindings.RefreshEvents();
        _bindings.FadeOutAiOverlayAfterAction();
        return new CodingInlineDefectAcceptCommandWorkflowResult(
            CodingInlineDefectAcceptCommandWorkflowOutcome.Accepted);
    }

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

    public async Task<CodingInlineDefectEditCommandWorkflowResult> EditAsync()
    {
        if (!_bindings.HasCodingViewModel())
        {
            return new CodingInlineDefectEditCommandWorkflowResult(
                CodingInlineDefectEditCommandWorkflowOutcome.NoViewModel);
        }

        var selected = _bindings.ResolveSelectedDefect() ?? _bindings.ResolveSelectedListEvent();
        if (selected is null)
        {
            return new CodingInlineDefectEditCommandWorkflowResult(
                CodingInlineDefectEditCommandWorkflowOutcome.NoSelection);
        }

        _bindings.SelectDefect(selected);
        _bindings.PausePlayback();
        if (!_bindings.TryEdit(selected))
        {
            return new CodingInlineDefectEditCommandWorkflowResult(
                CodingInlineDefectEditCommandWorkflowOutcome.EditCancelled);
        }

        var persistence = await CodingInlineDefectDecisionWorkflow.CompleteEditAsync(
            selected,
            _bindings.ResolveCodingSessionService(),
            _bindings.ExecuteEditDefect,
            PersistEditedAsync);
        if (!persistence.Success)
        {
            var error = persistence.Error ?? "Training konnte nicht gespeichert werden.";
            _bindings.ShowPersistenceError?.Invoke(error);
            return new CodingInlineDefectEditCommandWorkflowResult(
                CodingInlineDefectEditCommandWorkflowOutcome.PersistenceFailed,
                error);
        }

        _bindings.RefreshEvents();
        _bindings.UpdateInlineDefectDetail(selected);
        return new CodingInlineDefectEditCommandWorkflowResult(
            CodingInlineDefectEditCommandWorkflowOutcome.Edited);
    }

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

    private async Task<CodingTrainingSamplePersistenceResult> PersistAcceptedAsync(
        CodingEvent codingEvent)
    {
        if (_bindings.PersistAcceptedTrainingSampleAsync is not null)
            return await _bindings.PersistAcceptedTrainingSampleAsync(codingEvent).ConfigureAwait(false);

        _bindings.PersistAcceptedTrainingSample(codingEvent);
        return CodingTrainingSamplePersistenceResult.Ok;
    }

    private async Task<CodingTrainingSamplePersistenceResult> PersistEditedAsync(
        CodingEvent codingEvent)
    {
        if (_bindings.PersistEditedTrainingSampleAsync is not null)
            return await _bindings.PersistEditedTrainingSampleAsync(codingEvent).ConfigureAwait(false);

        _bindings.PersistEditedTrainingSample(codingEvent);
        return CodingTrainingSamplePersistenceResult.Ok;
    }
}
