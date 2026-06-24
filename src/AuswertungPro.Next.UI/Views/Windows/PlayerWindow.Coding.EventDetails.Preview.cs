using System;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private void UpdateInlineEvidencePreview(CodingEvent ev)
    {
        try
        {
            ApplyInlineEvidencePreviewState(CodingInlineEvidencePreviewService.Build(ev));
        }
        catch (Exception ex)
        {
            ApplyInlineEvidencePreviewState(CodingInlineEvidencePreviewService.LoadFailed);
            PlayerTrace.WriteLine($"[CodingPreview] {ex.Message}");
        }
    }

    private void ApplyInlineEvidencePreviewState(CodingInlineEvidencePreviewState state)
    {
        _codingInlineDefectDetailControls.ApplyPreview(state);
    }
}
