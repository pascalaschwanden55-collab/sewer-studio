using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private void UpdateInlineEvidencePreview(CodingEvent ev)
    {
        CodingInlineEvidencePreviewWorkflow.Execute(
            new CodingInlineEvidencePreviewWorkflowRequest(ev),
            new CodingInlineEvidencePreviewWorkflowActions(
                ApplyPreview: ApplyInlineEvidencePreviewState,
                TraceError: message => PlayerTrace.WriteLine(message)));
    }

    private void ApplyInlineEvidencePreviewState(CodingInlineEvidencePreviewState state)
    {
        _codingInlineDefectDetailControls.ApplyPreview(state);
    }
}
