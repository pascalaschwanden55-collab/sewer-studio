using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Ai;

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
                return Task.CompletedTask;
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

    private static CodingTrainingPersistenceContext Context(
        Func<CodingEvent, CodingTrainingSamplePersistenceRequest, Task>? persistSingleAsync = null,
        Func<IReadOnlyList<CodingEvent>, CodingTrainingSamplePersistenceRequest, Task>? persistEventsAsync = null,
        Func<bool>? hasCodingContext = null,
        Func<CodingTrainingSamplePersistenceRequest>? request = null)
        => new(
            persistSingleAsync ?? ((_, _) => Task.CompletedTask),
            persistEventsAsync ?? ((_, _) => Task.CompletedTask),
            hasCodingContext ?? (() => true),
            request ?? (() => Request("default")));

    private static CodingTrainingSamplePersistenceRequest Request(string caseId)
        => new(
            caseId,
            InspectionDate: null,
            ConfirmedByUser: null,
            ConfirmedAtUtc: null,
            PreferredFrameBytes: null,
            CaptureFrameAsync: () => Task.FromResult<byte[]?>(null));
}
