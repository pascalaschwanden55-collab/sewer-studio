using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Helpers;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Ai.Coding;

public sealed class CodingTrainingPersistenceContext
{
    private readonly Func<CodingEvent, CodingTrainingSamplePersistenceRequest, Task<CodingTrainingSamplePersistenceResult>> _persistSingleAsync;
    private readonly Func<IReadOnlyList<CodingEvent>, CodingTrainingSamplePersistenceRequest, Task> _persistEventsAsync;
    private readonly Func<IReadOnlyList<CodingEvent>, CodingTrainingSamplePersistenceRequest, Task<CodingTrainingSamplePersistenceResult>>? _persistEventsWithResultAsync;
    private readonly Func<bool> _hasCodingContext;
    private readonly Func<CodingTrainingSamplePersistenceRequest> _request;
    private readonly Action<string>? _reportBatchFailure;

    public CodingTrainingPersistenceContext(
        Func<CodingEvent, CodingTrainingSamplePersistenceRequest, Task<CodingTrainingSamplePersistenceResult>> persistSingleAsync,
        Func<IReadOnlyList<CodingEvent>, CodingTrainingSamplePersistenceRequest, Task> persistEventsAsync,
        Func<bool> hasCodingContext,
        Func<CodingTrainingSamplePersistenceRequest> request,
        Func<IReadOnlyList<CodingEvent>, CodingTrainingSamplePersistenceRequest, Task<CodingTrainingSamplePersistenceResult>>? persistEventsWithResultAsync = null,
        Action<string>? reportBatchFailure = null)
    {
        ArgumentNullException.ThrowIfNull(persistSingleAsync);
        ArgumentNullException.ThrowIfNull(persistEventsAsync);
        ArgumentNullException.ThrowIfNull(hasCodingContext);
        ArgumentNullException.ThrowIfNull(request);

        _persistSingleAsync = persistSingleAsync;
        _persistEventsAsync = persistEventsAsync;
        _persistEventsWithResultAsync = persistEventsWithResultAsync;
        _hasCodingContext = hasCodingContext;
        _request = request;
        _reportBatchFailure = reportBatchFailure;
    }

    public static CodingTrainingPersistenceContext CreateDefault(
        Func<ICodingSessionService?> sessionProvider,
        AppSettings? settings,
        Func<bool> hasCodingContext,
        Func<byte[]?> preferredFrameBytes,
        Func<string> caseId,
        Func<string?> inspectionDateText,
        Func<Task<byte[]?>> captureFrameAsync,
        ITrainingSampleStore? trainingSamples = null,
        ITrainingFrameStore? trainingFrames = null,
        Action<string>? reportBatchFailure = null)
    {
        ArgumentNullException.ThrowIfNull(sessionProvider);
        ArgumentNullException.ThrowIfNull(hasCodingContext);
        ArgumentNullException.ThrowIfNull(preferredFrameBytes);
        ArgumentNullException.ThrowIfNull(caseId);
        ArgumentNullException.ThrowIfNull(inspectionDateText);
        ArgumentNullException.ThrowIfNull(captureFrameAsync);

        var owner = CodingTrainingSamplesOwner.CreateDefault(
            sessionProvider,
            settings,
            trainingSamples,
            trainingFrames);
        return new CodingTrainingPersistenceContext(
            (codingEvent, request) => owner.Coordinator.PersistSingleEventAsync(codingEvent, request),
            (events, request) => owner.Coordinator.PersistEventsAsync(events, request),
            hasCodingContext,
            () => CodingTrainingSamplePersistenceRequest.FromPlayerContext(
                caseId(),
                inspectionDateText(),
                PlayerUserNameProvider.Current(),
                PlayerClock.UtcNow(),
                preferredFrameBytes(),
                captureFrameAsync),
            (events, request) => owner.Coordinator.PersistEventsWithResultAsync(events, request),
            reportBatchFailure);
    }

    public Task<CodingTrainingSamplePersistenceResult> PersistSingleEventAsync(CodingEvent codingEvent)
        => _persistSingleAsync(codingEvent, _request());

    public async Task<CodingTrainingSamplePersistenceResult> PersistEventsWithResultAsync(
        IReadOnlyList<CodingEvent>? events)
    {
        if (!_hasCodingContext() || events is null || events.Count == 0)
            return CodingTrainingSamplePersistenceResult.Ok;

        if (_persistEventsWithResultAsync is not null)
            return await _persistEventsWithResultAsync(events, _request()).ConfigureAwait(false);

        try
        {
            await _persistEventsAsync(events, _request()).ConfigureAwait(false);
            return CodingTrainingSamplePersistenceResult.Ok;
        }
        catch (Exception ex)
        {
            return CodingTrainingSamplePersistenceResult.Failed(ex.Message);
        }
    }

    public void PersistEvents(IReadOnlyList<CodingEvent>? events)
    {
        CodingTrainingBatchPersistenceWorkflow.Execute(
            new CodingTrainingBatchPersistenceWorkflowRequest(
                _hasCodingContext(),
                events),
            new CodingTrainingBatchPersistenceWorkflowActions(
                PersistEvents: currentEvents => PersistEventsAndReportFailureAsync(currentEvents)
                    .SafeFireAndForget("TrainingSave")));
    }

    private async Task PersistEventsAndReportFailureAsync(IReadOnlyList<CodingEvent> events)
    {
        // UI-Kontext bewusst erhalten: der optionale Callback zeigt den Fehler direkt
        // im Player statt ihn nur im Hintergrund-Log zu hinterlassen.
        var result = await PersistEventsWithResultAsync(events);
        if (!result.Success)
        {
            var message = result.Error ?? "Training konnte nicht gespeichert werden.";
            _reportBatchFailure?.Invoke(message);
            throw new InvalidOperationException(message);
        }
    }
}
