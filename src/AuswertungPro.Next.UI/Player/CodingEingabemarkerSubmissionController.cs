using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;

namespace AuswertungPro.Next.UI.Player;

public interface ICodingEingabemarkerSubmissionController
{
    Task<CodingEingabemarkerSubmissionWorkflowResult> SubmitAsync(string? rawKeyword);
}

public sealed record CodingEingabemarkerSubmissionControllerBindings(
    Func<bool> HasCodingViewModel,
    Func<ICodingSessionService?> ResolveCodingSessionService,
    Action HideInput,
    Action SetAnalyzingPhase,
    Func<string, string?> ResolveCodeHint,
    Func<IEnumerable<CodingEvent>> ResolveEvents,
    Action<string, double> ShowDuplicateStatus,
    Func<OverlayGeometry?> ResolveCurrentOverlay,
    Func<double> ResolveMeter,
    Func<TimeSpan> ResolveVideoTime,
    Func<string, string?> LookupLabel,
    Func<ProtocolEntry, string?> CapturePhoto,
    Action RefreshEvents,
    Action UpdateToolBadge,
    Action<CodingEvent> PersistTraining,
    Action<string, string, double> ShowSuccessStatus,
    Action<string> ShowAiFallbackStatus,
    Func<string, Task> RunAiFallbackAsync,
    Action<string> ShowErrorStatus,
    Action CancelMarker,
    Func<CodingEvent, Task<CodingTrainingSamplePersistenceResult>>? PersistTrainingAsync = null);

public sealed class CodingEingabemarkerSubmissionController : ICodingEingabemarkerSubmissionController
{
    private readonly CodingEingabemarkerSubmissionControllerBindings _bindings;

    public CodingEingabemarkerSubmissionController(
        CodingEingabemarkerSubmissionControllerBindings bindings)
    {
        ArgumentNullException.ThrowIfNull(bindings);
        ArgumentNullException.ThrowIfNull(bindings.HasCodingViewModel);
        ArgumentNullException.ThrowIfNull(bindings.ResolveCodingSessionService);
        ArgumentNullException.ThrowIfNull(bindings.HideInput);
        ArgumentNullException.ThrowIfNull(bindings.SetAnalyzingPhase);
        ArgumentNullException.ThrowIfNull(bindings.ResolveCodeHint);
        ArgumentNullException.ThrowIfNull(bindings.ResolveEvents);
        ArgumentNullException.ThrowIfNull(bindings.ShowDuplicateStatus);
        ArgumentNullException.ThrowIfNull(bindings.ResolveCurrentOverlay);
        ArgumentNullException.ThrowIfNull(bindings.ResolveMeter);
        ArgumentNullException.ThrowIfNull(bindings.ResolveVideoTime);
        ArgumentNullException.ThrowIfNull(bindings.LookupLabel);
        ArgumentNullException.ThrowIfNull(bindings.CapturePhoto);
        ArgumentNullException.ThrowIfNull(bindings.RefreshEvents);
        ArgumentNullException.ThrowIfNull(bindings.UpdateToolBadge);
        ArgumentNullException.ThrowIfNull(bindings.PersistTraining);
        ArgumentNullException.ThrowIfNull(bindings.ShowSuccessStatus);
        ArgumentNullException.ThrowIfNull(bindings.ShowAiFallbackStatus);
        ArgumentNullException.ThrowIfNull(bindings.RunAiFallbackAsync);
        ArgumentNullException.ThrowIfNull(bindings.ShowErrorStatus);
        ArgumentNullException.ThrowIfNull(bindings.CancelMarker);

        _bindings = bindings;
    }

    public async Task<CodingEingabemarkerSubmissionWorkflowResult> SubmitAsync(string? rawKeyword)
    {
        var hasCodingViewModel = _bindings.HasCodingViewModel();
        var codingSessionService = _bindings.ResolveCodingSessionService();

        return await CodingEingabemarkerSubmissionWorkflow.ExecuteAsync(
            new CodingEingabemarkerSubmissionWorkflowRequest(
                rawKeyword,
                hasCodingViewModel,
                codingSessionService is not null),
            new CodingEingabemarkerSubmissionWorkflowActions(
                HideInput: _bindings.HideInput,
                SetAnalyzingPhase: _bindings.SetAnalyzingPhase,
                ResolveCodeHint: _bindings.ResolveCodeHint,
                FindDuplicate: FindDuplicate,
                ShowDuplicateStatus: _bindings.ShowDuplicateStatus,
                AddDirectEvent: (codeHint, keyword) => AddDirectEvent(
                    codeHint,
                    keyword,
                    codingSessionService!),
                ShowAiFallbackStatus: _bindings.ShowAiFallbackStatus,
                RunAiFallbackAsync: _bindings.RunAiFallbackAsync,
                ShowErrorStatus: _bindings.ShowErrorStatus,
                CancelMarker: _bindings.CancelMarker,
                AddDirectEventAsync: (codeHint, keyword) => AddDirectEventAsync(
                    codeHint,
                    keyword,
                    codingSessionService!)));
    }

    private CodingEingabemarkerDuplicateMatch? FindDuplicate(string codeHint)
    {
        var duplicate = CodingEingabemarkerDuplicatePolicy.FindDuplicate(
            _bindings.ResolveEvents(),
            codeHint,
            _bindings.ResolveMeter());
        return duplicate is null
            ? null
            : new CodingEingabemarkerDuplicateMatch(duplicate.MeterAtCapture);
    }

    private void AddDirectEvent(
        string codeHint,
        string keyword,
        ICodingSessionService codingSessionService)
    {
        CodingEingabemarkerDirectEventWorkflow.Execute(
            new CodingEingabemarkerDirectEventWorkflowRequest(
                codeHint,
                keyword,
                _bindings.ResolveCurrentOverlay(),
                codingSessionService),
            new CodingEingabemarkerDirectEventWorkflowActions(
                ResolveMeter: _bindings.ResolveMeter,
                ResolveVideoTime: _bindings.ResolveVideoTime,
                LookupLabel: _bindings.LookupLabel,
                CapturePhoto: _bindings.CapturePhoto,
                RefreshEvents: _bindings.RefreshEvents,
                UpdateToolBadge: _bindings.UpdateToolBadge,
                PersistTraining: _bindings.PersistTraining,
                ShowSuccessStatus: _bindings.ShowSuccessStatus));
    }

    private Task<CodingTrainingSamplePersistenceResult> AddDirectEventAsync(
        string codeHint,
        string keyword,
        ICodingSessionService codingSessionService)
        => CodingEingabemarkerDirectEventWorkflow.ExecuteAsync(
            new CodingEingabemarkerDirectEventWorkflowRequest(
                codeHint,
                keyword,
                _bindings.ResolveCurrentOverlay(),
                codingSessionService),
            new CodingEingabemarkerDirectEventAsyncWorkflowActions(
                ResolveMeter: _bindings.ResolveMeter,
                ResolveVideoTime: _bindings.ResolveVideoTime,
                LookupLabel: _bindings.LookupLabel,
                CapturePhoto: _bindings.CapturePhoto,
                RefreshEvents: _bindings.RefreshEvents,
                UpdateToolBadge: _bindings.UpdateToolBadge,
                PersistTrainingAsync: PersistTrainingAsync,
                ShowSuccessStatus: _bindings.ShowSuccessStatus));

    private async Task<CodingTrainingSamplePersistenceResult> PersistTrainingAsync(
        CodingEvent codingEvent)
    {
        if (_bindings.PersistTrainingAsync is not null)
            return await _bindings.PersistTrainingAsync(codingEvent).ConfigureAwait(false);

        _bindings.PersistTraining(codingEvent);
        return CodingTrainingSamplePersistenceResult.Ok;
    }
}
