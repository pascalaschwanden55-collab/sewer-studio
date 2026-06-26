using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.UI.Ai;

public sealed record CodingEingabemarkerDirectEventWorkflowRequest(
    string CodeHint,
    string Keyword,
    OverlayGeometry? CurrentOverlay,
    ICodingSessionService CodingSessionService);

public sealed record CodingEingabemarkerDirectEventWorkflowActions(
    Func<double> ResolveMeter,
    Func<TimeSpan> ResolveVideoTime,
    Func<string, string?> LookupLabel,
    Func<ProtocolEntry, string?> CapturePhoto,
    Action RefreshEvents,
    Action UpdateToolBadge,
    Action<CodingEvent> PersistTraining,
    Action<string, string, double> ShowSuccessStatus);

public sealed record CodingEingabemarkerDirectEventWorkflowResult(
    CodingEvent Event,
    string Label,
    double Meter,
    TimeSpan VideoTime);

public static class CodingEingabemarkerDirectEventWorkflow
{
    public static CodingEingabemarkerDirectEventWorkflowResult Execute(
        CodingEingabemarkerDirectEventWorkflowRequest request,
        CodingEingabemarkerDirectEventWorkflowActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actions);
        ArgumentNullException.ThrowIfNull(request.CodingSessionService);

        var meter = actions.ResolveMeter();
        var videoTime = actions.ResolveVideoTime();
        var label = actions.LookupLabel(request.CodeHint) ?? request.Keyword;

        var draft = CodingEingabemarkerEventFactory.CreateAccepted(
            request.CodeHint,
            label,
            request.Keyword,
            meter,
            videoTime);

        var fotoPath = actions.CapturePhoto(draft.Entry);
        CodingProtocolEntryPhotoPathAppender.AddIfPresent(draft.Entry, fotoPath);

        var ev = CodingEingabemarkerEventAppender.Apply(
            draft,
            request.CurrentOverlay,
            request.CodingSessionService);

        actions.RefreshEvents();
        actions.UpdateToolBadge();
        actions.PersistTraining(ev);
        actions.ShowSuccessStatus(request.CodeHint, label, meter);

        return new CodingEingabemarkerDirectEventWorkflowResult(
            ev,
            label,
            meter,
            videoTime);
    }
}
