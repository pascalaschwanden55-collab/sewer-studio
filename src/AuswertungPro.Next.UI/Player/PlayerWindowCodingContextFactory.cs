using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Ai.Coding;

namespace AuswertungPro.Next.UI.Player;

internal sealed record PlayerWindowCodingContextDependencies(
    ICodingSessionHost SessionHost,
    Func<ICodingSessionService?> ResolveSessionService,
    Func<IReadOnlyList<CodingEvent>> ImportEvents,
    Func<PipeCalibration?> Calibration,
    Func<double> VideoAspect,
    Func<string, bool> TakeSnapshot,
    Func<double?> FirstCleanFrameSeconds,
    Func<double?> OsdMeter,
    Func<TimeSpan> FallbackVideoTime);

internal sealed record PlayerWindowCodingContexts(
    CodingFindingContext Finding,
    CodingAnalysisContext Analysis,
    CodingBoundaryContext Boundary);

/// <summary>Verbindet die Codierkontexte mit denselben, erst beim Zugriff gelesenen Ereignisquellen.</summary>
internal static class PlayerWindowCodingContextFactory
{
    public static PlayerWindowCodingContexts Create(
        PlayerWindowCodingContextDependencies dependencies,
        CodingBoundaryEventWorkflowActions boundaryActions)
    {
        ArgumentNullException.ThrowIfNull(dependencies);
        ArgumentNullException.ThrowIfNull(dependencies.SessionHost);
        ArgumentNullException.ThrowIfNull(dependencies.ResolveSessionService);
        ArgumentNullException.ThrowIfNull(boundaryActions);

        // Vor InitializeComponent sind die Dienste noch nicht aufgebaut.
        // Auch nach einem Sitzungswechsel muessen die aktuellen Quellen gelesen werden.
        IReadOnlyList<CodingEvent>? SessionEvents()
            => dependencies.ResolveSessionService()?.ActiveSession?.Events;
        IEnumerable<CodingEvent> ViewEvents() => dependencies.SessionHost.Events;

        var finding = CodingFindingContext.CreateDefault(
            SessionEvents,
            ViewEvents,
            dependencies.ImportEvents,
            boundaryActions.Trace);
        var analysis = CodingAnalysisContext.CreateDefault(
            SessionEvents,
            ViewEvents,
            dependencies.ImportEvents,
            dependencies.Calibration,
            dependencies.VideoAspect,
            dependencies.TakeSnapshot);
        var boundary = new CodingBoundaryContext(
            new CodingBoundaryContextSources(
                HasCodingViewModel: () => dependencies.SessionHost.HasViewModel,
                ViewEvents: () => dependencies.SessionHost.EventCollection,
                SessionEvents: () => SessionEvents() ?? [],
                ImportEvents: dependencies.ImportEvents,
                CodingSessionService: dependencies.ResolveSessionService,
                FirstCleanFrameSeconds: dependencies.FirstCleanFrameSeconds,
                OsdMeter: dependencies.OsdMeter,
                ViewModelEndMeter: () => dependencies.SessionHost.EndMeter,
                FallbackVideoTime: dependencies.FallbackVideoTime),
            boundaryActions);

        return new PlayerWindowCodingContexts(finding, analysis, boundary);
    }
}
