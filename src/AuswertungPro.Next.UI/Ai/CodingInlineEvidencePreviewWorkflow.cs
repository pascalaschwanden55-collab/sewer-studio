using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Ai;

public sealed record CodingInlineEvidencePreviewWorkflowRequest(
    CodingEvent CodingEvent,
    ICodingDefectPreviewRenderer? PreviewRenderer = null,
    Func<CodingEvent, CodingInlineEvidencePreviewState>? BuildPreview = null);

public sealed record CodingInlineEvidencePreviewWorkflowActions(
    Action<CodingInlineEvidencePreviewState> ApplyPreview,
    Action<string> TraceError);

public static class CodingInlineEvidencePreviewWorkflow
{
    public static void Execute(
        CodingInlineEvidencePreviewWorkflowRequest request,
        CodingInlineEvidencePreviewWorkflowActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actions);

        try
        {
            var buildPreview = request.BuildPreview ?? (codingEvent =>
                CodingInlineEvidencePreviewService.Build(
                    codingEvent,
                    request.PreviewRenderer is null
                        ? null
                        : item => request.PreviewRenderer.BuildPreviewImagePath(item)));
            actions.ApplyPreview(buildPreview(request.CodingEvent));
        }
        catch (Exception ex)
        {
            actions.ApplyPreview(CodingInlineEvidencePreviewService.LoadFailed);
            actions.TraceError($"[CodingPreview] {ex.Message}");
        }
    }
}
