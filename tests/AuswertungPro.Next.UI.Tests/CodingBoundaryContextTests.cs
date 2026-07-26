using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingBoundaryContextTests
{
    [Fact]
    public async Task EnsureStartAsync_reads_current_sources_and_returns_added_state()
    {
        var firstViewEvents = new List<CodingEvent>();
        var secondViewEvents = new List<CodingEvent> { new() };
        IReadOnlyList<CodingEvent>? currentViewEvents = firstViewEvents;
        var sessionEvents = new List<CodingEvent>();
        var importEvents = new List<CodingEvent>();
        var frameBytes = new byte[] { 1, 2, 3 };
        var captured = new List<CodingBoundaryStartCommandRequest>();
        var context = new CodingBoundaryContext(
            Sources(
                viewEvents: () => currentViewEvents,
                sessionEvents: () => sessionEvents,
                importEvents: () => importEvents,
                firstCleanFrameSeconds: () => 4.5),
            WorkflowActions(),
            new CodingBoundaryCommandExecutor(
                EnsureStartAsync: (request, _) =>
                {
                    captured.Add(request);
                    return Task.FromResult(CommandResult(CodingBoundaryEventWorkflowOutcome.Added));
                },
                EnsureEnd: (_, _) => CommandResult(CodingBoundaryEventWorkflowOutcome.Existing)));

        var firstAdded = await context.EnsureStartAsync(1.25, frameBytes);
        currentViewEvents = secondViewEvents;
        var secondAdded = await context.EnsureStartAsync(2.5, frameBytes);

        Assert.True(firstAdded);
        Assert.True(secondAdded);
        Assert.Same(firstViewEvents, captured[0].ViewEvents);
        Assert.Same(secondViewEvents, captured[1].ViewEvents);
        Assert.Same(sessionEvents, captured[1].SessionEvents);
        Assert.Same(importEvents, captured[1].ImportEvents);
        Assert.Equal(2.5, captured[1].CurrentMeter);
        Assert.Equal(4.5, captured[1].FirstCleanFrameSeconds);
        Assert.Same(frameBytes, captured[1].AnalyzedFrameBytes);
    }

    [Fact]
    public void EnsureEnd_reads_current_sources_and_preserves_timeline_fallback()
    {
        var viewEvents = new List<CodingEvent>();
        var importEvents = new List<CodingEvent>();
        var frameBytes = new byte[] { 4, 5, 6 };
        CodingBoundaryEndCommandRequest? captured = null;
        var context = new CodingBoundaryContext(
            Sources(
                viewEvents: () => viewEvents,
                importEvents: () => importEvents,
                osdMeter: () => 13.4,
                viewModelEndMeter: () => 15.8,
                fallbackVideoTime: () => TimeSpan.FromSeconds(12)),
            WorkflowActions(),
            new CodingBoundaryCommandExecutor(
                EnsureStartAsync: (_, _) => Task.FromResult(CommandResult(CodingBoundaryEventWorkflowOutcome.Existing)),
                EnsureEnd: (request, _) =>
                {
                    captured = request;
                    return CommandResult(CodingBoundaryEventWorkflowOutcome.Added);
                }));

        context.EnsureEnd(14.2, frameBytes);

        Assert.NotNull(captured);
        Assert.Same(viewEvents, captured.ViewEvents);
        Assert.Same(importEvents, captured.ImportEvents);
        Assert.Equal(13.4, captured.OsdMeter);
        Assert.Equal(14.2, captured.FallbackEndMeter);
        Assert.Equal(15.8, captured.ViewModelEndMeter);
        Assert.Equal(TimeSpan.FromSeconds(12), captured.FallbackVideoTime);
        Assert.Same(frameBytes, captured.AnalyzedFrameBytes);
    }

    private static CodingBoundaryContextSources Sources(
        Func<IReadOnlyList<CodingEvent>?>? viewEvents = null,
        Func<IReadOnlyList<CodingEvent>>? sessionEvents = null,
        Func<IReadOnlyList<CodingEvent>>? importEvents = null,
        Func<double?>? firstCleanFrameSeconds = null,
        Func<double?>? osdMeter = null,
        Func<double>? viewModelEndMeter = null,
        Func<TimeSpan>? fallbackVideoTime = null)
        => new(
            HasCodingViewModel: () => true,
            ViewEvents: viewEvents ?? (() => []),
            SessionEvents: sessionEvents ?? (() => []),
            ImportEvents: importEvents ?? (() => []),
            CodingSessionService: () => null,
            FirstCleanFrameSeconds: firstCleanFrameSeconds ?? (() => null),
            OsdMeter: osdMeter ?? (() => null),
            ViewModelEndMeter: viewModelEndMeter ?? (() => 0),
            FallbackVideoTime: fallbackVideoTime ?? (() => TimeSpan.Zero));

    private static CodingBoundaryEventWorkflowActions WorkflowActions()
        => new(
            LookupLabel: _ => null,
            Trace: _ => { },
            TryExtractFrameAtSecondsAsync: _ => Task.FromResult<byte[]?>(null),
            AttachBoundaryAnalyzedFramePhoto: (_, _) => { },
            StartAutoCalibration: () => { },
            RefreshEvents: () => { });

    private static CodingBoundaryEventCommandResult CommandResult(CodingBoundaryEventWorkflowOutcome outcome)
        => new(
            CodingBoundaryEventCommandOutcome.Executed,
            new CodingBoundaryEventWorkflowResult(outcome));
}
