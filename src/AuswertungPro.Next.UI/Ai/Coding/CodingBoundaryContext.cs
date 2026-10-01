using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Ai.Coding;

public sealed record CodingBoundaryContextSources(
    Func<bool> HasCodingViewModel,
    Func<IReadOnlyList<CodingEvent>?> ViewEvents,
    Func<IReadOnlyList<CodingEvent>> SessionEvents,
    Func<IReadOnlyList<CodingEvent>> ImportEvents,
    Func<ICodingSessionService?> CodingSessionService,
    Func<double?> FirstCleanFrameSeconds,
    Func<double?> OsdMeter,
    Func<double> ViewModelEndMeter,
    Func<TimeSpan> FallbackVideoTime);

internal sealed record CodingBoundaryCommandExecutor(
    Func<
        CodingBoundaryStartCommandRequest,
        CodingBoundaryStartCommandActions,
        Task<CodingBoundaryEventCommandResult>> EnsureStartAsync,
    Func<
        CodingBoundaryEndCommandRequest,
        CodingBoundaryEndCommandActions,
        CodingBoundaryEventCommandResult> EnsureEnd)
{
    public static CodingBoundaryCommandExecutor Default { get; } = new(
        CodingBoundaryEventCommandWorkflow.EnsureStartAsync,
        CodingBoundaryEventCommandWorkflow.EnsureEnd);
}

public sealed class CodingBoundaryContext
{
    private readonly CodingBoundaryContextSources _sources;
    private readonly CodingBoundaryEventWorkflowActions _workflowActions;
    private readonly CodingBoundaryCommandExecutor _executor;

    public CodingBoundaryContext(
        CodingBoundaryContextSources sources,
        CodingBoundaryEventWorkflowActions workflowActions)
        : this(sources, workflowActions, CodingBoundaryCommandExecutor.Default)
    {
    }

    internal CodingBoundaryContext(
        CodingBoundaryContextSources sources,
        CodingBoundaryEventWorkflowActions workflowActions,
        CodingBoundaryCommandExecutor executor)
    {
        ArgumentNullException.ThrowIfNull(sources);
        ArgumentNullException.ThrowIfNull(workflowActions);
        ArgumentNullException.ThrowIfNull(executor);
        Validate(sources, executor);

        _sources = sources;
        _workflowActions = workflowActions;
        _executor = executor;
    }

    public Task<bool> EnsureStartAsync(CodingAnalyzedFrameEvidence frame)
        => EnsureStartCoreAsync(frame.Meter, frame.ImageBytes, frame);

    public Task<bool> EnsureStartAsync(double currentMeter, byte[]? analyzedFrameBytes)
        => EnsureStartCoreAsync(currentMeter, analyzedFrameBytes, null);

    private async Task<bool> EnsureStartCoreAsync(
        double currentMeter,
        byte[]? analyzedFrameBytes, CodingAnalyzedFrameEvidence? frame)
    {
        var result = await _executor.EnsureStartAsync(
            new CodingBoundaryStartCommandRequest(
                CurrentMeter: currentMeter,
                HasCodingViewModel: _sources.HasCodingViewModel(),
                ViewEvents: _sources.ViewEvents(),
                SessionEvents: _sources.SessionEvents(),
                ImportEvents: _sources.ImportEvents(),
                CodingSessionService: _sources.CodingSessionService(),
                FirstCleanFrameSeconds: _sources.FirstCleanFrameSeconds(),
                AnalyzedFrameBytes: analyzedFrameBytes) { AnalyzedFrame = frame },
            new CodingBoundaryStartCommandActions(
                request => CodingBoundaryEventWorkflow.EnsureStartAsync(request, _workflowActions)));

        return result.Added;
    }

    public void EnsureEnd(
        double fallbackEndMeter,
        byte[]? analyzedFrameBytes = null)
        => EnsureEndCore(fallbackEndMeter, analyzedFrameBytes, null);

    public void EnsureEnd(CodingAnalyzedFrameEvidence frame)
        => EnsureEndCore(frame.Meter, frame.ImageBytes, frame);

    private void EnsureEndCore(double fallbackEndMeter, byte[]? analyzedFrameBytes, CodingAnalyzedFrameEvidence? frame)
    {
        _executor.EnsureEnd(
            new CodingBoundaryEndCommandRequest(
                HasCodingViewModel: _sources.HasCodingViewModel(),
                ViewEvents: _sources.ViewEvents(),
                ImportEvents: _sources.ImportEvents(),
                CodingSessionService: _sources.CodingSessionService(),
                OsdMeter: frame is null ? _sources.OsdMeter() : frame.MeterFromOsd ? frame.Meter : null,
                FallbackEndMeter: fallbackEndMeter,
                ViewModelEndMeter: _sources.ViewModelEndMeter(),
                FallbackVideoTime: frame?.CaptureTime ?? _sources.FallbackVideoTime(),
                AnalyzedFrameBytes: analyzedFrameBytes) { AnalyzedFrame = frame },
            new CodingBoundaryEndCommandActions(
                request => CodingBoundaryEventWorkflow.EnsureEnd(request, _workflowActions)));
    }

    private static void Validate(
        CodingBoundaryContextSources sources,
        CodingBoundaryCommandExecutor executor)
    {
        ArgumentNullException.ThrowIfNull(sources.HasCodingViewModel);
        ArgumentNullException.ThrowIfNull(sources.ViewEvents);
        ArgumentNullException.ThrowIfNull(sources.SessionEvents);
        ArgumentNullException.ThrowIfNull(sources.ImportEvents);
        ArgumentNullException.ThrowIfNull(sources.CodingSessionService);
        ArgumentNullException.ThrowIfNull(sources.FirstCleanFrameSeconds);
        ArgumentNullException.ThrowIfNull(sources.OsdMeter);
        ArgumentNullException.ThrowIfNull(sources.ViewModelEndMeter);
        ArgumentNullException.ThrowIfNull(sources.FallbackVideoTime);
        ArgumentNullException.ThrowIfNull(executor.EnsureStartAsync);
        ArgumentNullException.ThrowIfNull(executor.EnsureEnd);
    }
}
