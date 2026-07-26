using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.UI.Ai.Coding;

public enum CodingBoundaryEventWorkflowOutcome
{
    Existing,
    Added
}

public sealed record CodingBoundaryStartEventWorkflowRequest(
    double CurrentMeter,
    IReadOnlyList<CodingEvent> ViewEvents,
    IReadOnlyList<CodingEvent> SessionEvents,
    IReadOnlyList<CodingEvent> ImportEvents,
    ICodingSessionService CodingSessionService,
    double? FirstCleanFrameSeconds,
    byte[]? AnalyzedFrameBytes);

public sealed record CodingBoundaryEndEventWorkflowRequest(
    IReadOnlyList<CodingEvent> ViewEvents,
    IReadOnlyList<CodingEvent> ImportEvents,
    ICodingSessionService CodingSessionService,
    double? OsdMeter,
    double FallbackEndMeter,
    double ViewModelEndMeter,
    TimeSpan FallbackVideoTime,
    byte[]? AnalyzedFrameBytes);

public sealed record CodingBoundaryEventWorkflowActions(
    Func<string, string?> LookupLabel,
    Action<string> Trace,
    Func<double?, Task<byte[]?>> TryExtractFrameAtSecondsAsync,
    Action<ProtocolEntry, byte[]?> AttachBoundaryAnalyzedFramePhoto,
    Action StartAutoCalibration,
    Action RefreshEvents);

public sealed record CodingBoundaryEventWorkflowResult(
    CodingBoundaryEventWorkflowOutcome Outcome)
{
    public bool Added => Outcome == CodingBoundaryEventWorkflowOutcome.Added;
}

public static class CodingBoundaryEventWorkflow
{
    public static async Task<CodingBoundaryEventWorkflowResult> EnsureStartAsync(
        CodingBoundaryStartEventWorkflowRequest request,
        CodingBoundaryEventWorkflowActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.ViewEvents);
        ArgumentNullException.ThrowIfNull(request.SessionEvents);
        ArgumentNullException.ThrowIfNull(request.ImportEvents);
        ArgumentNullException.ThrowIfNull(request.CodingSessionService);
        ArgumentNullException.ThrowIfNull(actions);

        var bcdPresence = CodingBoundaryPresencePolicy.CountExisting(
            request.ViewEvents,
            request.SessionEvents,
            "BCD");
        if (bcdPresence.Exists)
        {
            actions.Trace(
                $"[BCD-Dedup] EnsureRohranfang: bereits vorhanden (VM={bcdPresence.ViewCount}, Session={bcdPresence.SessionCount})");
            return Existing();
        }

        actions.Trace(
            $"[BCD-Dedup] EnsureRohranfang: NEU erzeugen bei {request.CurrentMeter:F2}m (VM={bcdPresence.ViewCount}, Session={bcdPresence.SessionCount})");

        var startReference = CodingBoundaryImportReferencePolicy.ResolveStart(request.ImportEvents);
        var label = actions.LookupLabel("BCD") ?? "Rohranfang";
        var draft = CodingBoundaryEventFactory.CreateStart(
            label,
            startReference.Meter,
            startReference.VideoTime);

        var frameBytes = await actions.TryExtractFrameAtSecondsAsync(request.FirstCleanFrameSeconds)
                         ?? request.AnalyzedFrameBytes;
        actions.AttachBoundaryAnalyzedFramePhoto(draft.Entry, frameBytes);

        CodingBoundaryEventAppender.Apply(
            draft,
            startReference.Meter,
            startReference.VideoTime,
            request.CodingSessionService);

        actions.StartAutoCalibration();
        return Added();
    }

    public static CodingBoundaryEventWorkflowResult EnsureEnd(
        CodingBoundaryEndEventWorkflowRequest request,
        CodingBoundaryEventWorkflowActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.ViewEvents);
        ArgumentNullException.ThrowIfNull(request.ImportEvents);
        ArgumentNullException.ThrowIfNull(request.CodingSessionService);
        ArgumentNullException.ThrowIfNull(actions);

        if (CodingBoundaryPresencePolicy.ExistsInView(request.ViewEvents, "BCE"))
            return Existing();

        var endReference = CodingBoundaryImportReferencePolicy.ResolveEnd(
            request.ImportEvents,
            request.OsdMeter,
            request.FallbackEndMeter,
            request.ViewModelEndMeter,
            request.FallbackVideoTime);

        var label = actions.LookupLabel("BCE") ?? "Rohrende";
        var draft = CodingBoundaryEventFactory.CreateEnd(
            label,
            endReference.Meter,
            endReference.VideoTime);
        actions.AttachBoundaryAnalyzedFramePhoto(draft.Entry, request.AnalyzedFrameBytes);

        CodingBoundaryEventAppender.Apply(
            draft,
            endReference.Meter,
            endReference.VideoTime,
            request.CodingSessionService);

        actions.RefreshEvents();
        return Added();
    }

    private static CodingBoundaryEventWorkflowResult Existing()
        => new(CodingBoundaryEventWorkflowOutcome.Existing);

    private static CodingBoundaryEventWorkflowResult Added()
        => new(CodingBoundaryEventWorkflowOutcome.Added);
}
