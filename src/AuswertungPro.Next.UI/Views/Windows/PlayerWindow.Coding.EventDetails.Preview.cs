using System;
using System.Windows;
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
        ImgInlineEvidencePreview.Source = state.Source;
        ImgInlineEvidencePreview.Visibility = state.ImageVisible ? Visibility.Visible : Visibility.Collapsed;
        TxtInlineEvidencePreviewStatus.Text = state.StatusText ?? "";
        TxtInlineEvidencePreviewStatus.Visibility = state.StatusVisible ? Visibility.Visible : Visibility.Collapsed;
    }
}
