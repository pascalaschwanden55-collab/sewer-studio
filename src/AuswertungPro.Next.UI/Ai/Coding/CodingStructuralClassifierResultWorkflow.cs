using System.Windows.Media;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Ai.Coding;

public enum CodingStructuralClassifierResultWorkflowOutcome
{
    NotHandled,
    CoveredExisting,
    Added
}

public sealed record CodingStructuralClassifierResultWorkflowRequest(
    SingleFrameResult Result,
    double Meter,
    TimeSpan VideoTime,
    IReadOnlyList<CodingEvent> ViewEvents,
    ICodingSessionService CodingSessionService,
    bool MeterFromOsd);

public sealed record CodingStructuralClassifierResultWorkflowActions(
    Func<string, string?> LookupVsaLabel,
    Func<LiveFrameFinding, double, string?> ResolveFindingCodeForCoding,
    Action ClearDetectionOverlays,
    Action ClearMasks,
    Action<LiveFrameFinding, string> ShowResolvedFinding,
    Action<ProtocolEntry> AttachAnalyzedFramePhoto,
    Action RefreshEvents,
    Action<string, Color, string?> SetAiState);

public sealed record CodingStructuralClassifierResultWorkflowResult(
    CodingStructuralClassifierResultWorkflowOutcome Outcome)
{
    public bool Handled => Outcome != CodingStructuralClassifierResultWorkflowOutcome.NotHandled;
}

public static class CodingStructuralClassifierResultWorkflow
{
    public static CodingStructuralClassifierResultWorkflowResult Execute(
        CodingStructuralClassifierResultWorkflowRequest request,
        CodingStructuralClassifierResultWorkflowActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Result);
        ArgumentNullException.ThrowIfNull(request.ViewEvents);
        ArgumentNullException.ThrowIfNull(request.CodingSessionService);
        ArgumentNullException.ThrowIfNull(actions);

        var code = request.Result.ClassifierCode;
        if (!CodingClassifierDisplayPolicy.IsStructuralClassifierCode(code))
            return NotHandled();

        if (request.Result.HasDetections)
            return NotHandled();

        var structuralCode = code!;
        var label = CodingClassifierDisplayPolicy.ResolveStructuralLabel(
            structuralCode,
            actions.LookupVsaLabel(structuralCode));
        var finding = CodingStructuralClassifierFindingFactory.Create(structuralCode, label);
        var resolvedCode = actions.ResolveFindingCodeForCoding(finding, request.Meter);
        if (resolvedCode == null || !resolvedCode.StartsWith(structuralCode, StringComparison.OrdinalIgnoreCase))
            return NotHandled();

        var coveringEvent = CodingFindingCoveragePolicy.FindCoveringEvent(
            request.ViewEvents,
            resolvedCode,
            request.Meter,
            finding);

        actions.ClearDetectionOverlays();
        actions.ClearMasks();
        actions.ShowResolvedFinding(finding, resolvedCode);

        if (coveringEvent != null)
        {
            actions.SetAiState(
                CodingClassifierDisplayPolicy.BuildDetectedStatusText(label, added: false),
                PlayerStatusColors.Success,
                CodingClassifierDisplayPolicy.BuildClassifierDetail(request.Result.ClassifierConfidence));
            return new CodingStructuralClassifierResultWorkflowResult(
                CodingStructuralClassifierResultWorkflowOutcome.CoveredExisting);
        }

        var draft = CodingStructuralClassifierEventFactory.Create(
            resolvedCode,
            actions.LookupVsaLabel(resolvedCode) ?? label,
            label,
            request.Result.ClassifierConfidence,
            request.Meter,
            request.VideoTime,
            request.MeterFromOsd);

        actions.AttachAnalyzedFramePhoto(draft.Entry);
        CodingStructuralClassifierEventAppender.Apply(
            draft,
            request.Meter,
            request.VideoTime,
            request.CodingSessionService);

        actions.RefreshEvents();
        actions.SetAiState(
            CodingClassifierDisplayPolicy.BuildDetectedStatusText(draft.Entry.Beschreibung, added: true),
            PlayerStatusColors.Success,
            CodingClassifierDisplayPolicy.BuildClassifierDetail(request.Result.ClassifierConfidence));

        return new CodingStructuralClassifierResultWorkflowResult(
            CodingStructuralClassifierResultWorkflowOutcome.Added);
    }

    private static CodingStructuralClassifierResultWorkflowResult NotHandled()
        => new(CodingStructuralClassifierResultWorkflowOutcome.NotHandled);
}
