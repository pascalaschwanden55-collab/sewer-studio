using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingTrainingPersistenceContextTests
{
    [Fact]
    public async Task PersistSingleEventAsync_reads_current_request_and_delegates_event()
    {
        var firstRequest = Request("first");
        var secondRequest = Request("second");
        var currentRequest = firstRequest;
        var codingEvent = new CodingEvent();
        var received = new List<(CodingEvent Event, CodingTrainingSamplePersistenceRequest Request)>();
        var context = Context(
            persistSingleAsync: (currentEvent, request) =>
            {
                received.Add((currentEvent, request));
                return Task.FromResult(CodingTrainingSamplePersistenceResult.Ok);
            },
            request: () => currentRequest);

        await context.PersistSingleEventAsync(codingEvent);
        currentRequest = secondRequest;
        await context.PersistSingleEventAsync(codingEvent);

        Assert.Equal(2, received.Count);
        Assert.All(received, call => Assert.Same(codingEvent, call.Event));
        Assert.Same(firstRequest, received[0].Request);
        Assert.Same(secondRequest, received[1].Request);
    }

    [Fact]
    public void PersistEvents_skips_without_coding_context_or_events()
    {
        var hasCodingContext = false;
        var requestCalls = 0;
        var persistenceCalls = 0;
        var context = Context(
            hasCodingContext: () => hasCodingContext,
            persistEventsAsync: (_, _) =>
            {
                persistenceCalls++;
                return Task.CompletedTask;
            },
            request: () =>
            {
                requestCalls++;
                return Request("unused");
            });

        context.PersistEvents([new CodingEvent()]);
        hasCodingContext = true;
        context.PersistEvents([]);

        Assert.Equal(0, requestCalls);
        Assert.Equal(0, persistenceCalls);
    }

    [Fact]
    public void PersistEvents_reads_current_request_and_delegates_same_list()
    {
        var events = new List<CodingEvent> { new() };
        var request = Request("batch");
        IReadOnlyList<CodingEvent>? receivedEvents = null;
        CodingTrainingSamplePersistenceRequest? receivedRequest = null;
        var context = Context(
            persistEventsAsync: (currentEvents, currentRequest) =>
            {
                receivedEvents = currentEvents;
                receivedRequest = currentRequest;
                return Task.CompletedTask;
            },
            request: () => request);

        context.PersistEvents(events);

        Assert.Same(events, receivedEvents);
        Assert.Same(request, receivedRequest);
    }

    [Fact]
    public async Task PersistEvents_meldet_einen_asynchronen_Speicherfehler_sichtbar()
    {
        var reported = new TaskCompletionSource<string>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var context = Context(
            persistEventsWithResultAsync: (_, _) => Task.FromResult(
                CodingTrainingSamplePersistenceResult.Failed("Datentraeger voll")),
            reportBatchFailure: message => reported.TrySetResult(message));

        context.PersistEvents([new CodingEvent()]);

        var message = await reported.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Contains("Datentraeger voll", message, StringComparison.Ordinal);
    }

    private static CodingTrainingPersistenceContext Context(
        Func<CodingEvent, CodingTrainingSamplePersistenceRequest, Task<CodingTrainingSamplePersistenceResult>>? persistSingleAsync = null,
        Func<IReadOnlyList<CodingEvent>, CodingTrainingSamplePersistenceRequest, Task>? persistEventsAsync = null,
        Func<bool>? hasCodingContext = null,
        Func<CodingTrainingSamplePersistenceRequest>? request = null,
        Func<IReadOnlyList<CodingEvent>, CodingTrainingSamplePersistenceRequest, Task<CodingTrainingSamplePersistenceResult>>? persistEventsWithResultAsync = null,
        Action<string>? reportBatchFailure = null)
        => new(
            persistSingleAsync ?? ((_, _) => Task.FromResult(CodingTrainingSamplePersistenceResult.Ok)),
            persistEventsAsync ?? ((_, _) => Task.CompletedTask),
            hasCodingContext ?? (() => true),
            request ?? (() => Request("default")),
            persistEventsWithResultAsync,
            reportBatchFailure);

    private static CodingTrainingSamplePersistenceRequest Request(string caseId)
        => new(
            caseId,
            InspectionDate: null,
            ConfirmedByUser: null,
            ConfirmedAtUtc: null,
            PreferredFrameBytes: null,
            CaptureFrameAsync: () => Task.FromResult<byte[]?>(null));
}
