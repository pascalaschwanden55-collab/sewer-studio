using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Helpers;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Ai;

public sealed class CodingTrainingPersistenceContext
{
    private readonly Func<CodingEvent, CodingTrainingSamplePersistenceRequest, Task> _persistSingleAsync;
    private readonly Func<IReadOnlyList<CodingEvent>, CodingTrainingSamplePersistenceRequest, Task> _persistEventsAsync;
    private readonly Func<bool> _hasCodingContext;
    private readonly Func<CodingTrainingSamplePersistenceRequest> _request;

    public CodingTrainingPersistenceContext(
        Func<CodingEvent, CodingTrainingSamplePersistenceRequest, Task> persistSingleAsync,
        Func<IReadOnlyList<CodingEvent>, CodingTrainingSamplePersistenceRequest, Task> persistEventsAsync,
        Func<bool> hasCodingContext,
        Func<CodingTrainingSamplePersistenceRequest> request)
    {
        ArgumentNullException.ThrowIfNull(persistSingleAsync);
        ArgumentNullException.ThrowIfNull(persistEventsAsync);
        ArgumentNullException.ThrowIfNull(hasCodingContext);
        ArgumentNullException.ThrowIfNull(request);

        _persistSingleAsync = persistSingleAsync;
        _persistEventsAsync = persistEventsAsync;
        _hasCodingContext = hasCodingContext;
        _request = request;
    }

    public static CodingTrainingPersistenceContext CreateDefault(
        Func<ICodingSessionService?> sessionProvider,
        AppSettings? settings,
        Func<bool> hasCodingContext,
        Func<byte[]?> preferredFrameBytes,
        Func<string> caseId,
        Func<string?> inspectionDateText,
        Func<Task<byte[]?>> captureFrameAsync)
    {
        ArgumentNullException.ThrowIfNull(sessionProvider);
        ArgumentNullException.ThrowIfNull(hasCodingContext);
        ArgumentNullException.ThrowIfNull(preferredFrameBytes);
        ArgumentNullException.ThrowIfNull(caseId);
        ArgumentNullException.ThrowIfNull(inspectionDateText);
        ArgumentNullException.ThrowIfNull(captureFrameAsync);

        var owner = CodingTrainingSamplesOwner.CreateDefault(sessionProvider, settings);
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
                captureFrameAsync));
    }

    public Task PersistSingleEventAsync(CodingEvent codingEvent)
        => _persistSingleAsync(codingEvent, _request());

    public void PersistEvents(IReadOnlyList<CodingEvent>? events)
    {
        CodingTrainingBatchPersistenceWorkflow.Execute(
            new CodingTrainingBatchPersistenceWorkflowRequest(
                _hasCodingContext(),
                events),
            new CodingTrainingBatchPersistenceWorkflowActions(
                PersistEvents: currentEvents => _persistEventsAsync(currentEvents, _request())
                    .SafeFireAndForget("TrainingSave")));
    }
}
