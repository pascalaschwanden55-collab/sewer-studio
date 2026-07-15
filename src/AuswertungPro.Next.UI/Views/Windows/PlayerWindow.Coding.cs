using System;
using System.Threading.Tasks;
using System.Windows;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private void CodingApply_Click(object sender, RoutedEventArgs e)
        => _codingApplyController.Apply(showOverlay: true);

    private void ConfirmAccept_Click(object sender, RoutedEventArgs e)
        => _codingConfirmationController.Accept();

    private void ConfirmEdit_Click(object sender, RoutedEventArgs e)
        => _codingConfirmationController.Edit();

    private void ConfirmReject_Click(object sender, RoutedEventArgs e)
        => _codingConfirmationController.Reject();

    /// <summary>
    /// HÃ¤lt die Overlay-ZeichenflÃ¤che exakt auf VideoView-GrÃ¶ÃŸe.
    /// Wichtig fÃ¼r Popup-Overlay Ã¼ber VLC (HwndHost/Airspace).
    /// </summary>
    private void UpdateCodingOverlayViewport()
    {
        var canvasSize = CodingOverlayInputControls.GetCanvasSize(CodingOverlayCanvas);
        var nextCanvasWidth = canvasSize.Width;
        var nextCanvasHeight = canvasSize.Height;

        CodingOverlayViewportController.Update(
            VideoView.ActualWidth,
            VideoView.ActualHeight,
            canvasSize.Width,
            canvasSize.Height,
            width =>
            {
                nextCanvasWidth = width;
                CodingOverlayInputControls.SetCanvasSize(CodingOverlayCanvas, nextCanvasWidth, nextCanvasHeight);
            },
            height =>
            {
                nextCanvasHeight = height;
                CodingOverlayInputControls.SetCanvasSize(CodingOverlayCanvas, nextCanvasWidth, nextCanvasHeight);
            });
    }

    // --- Coding Navigation ---

    /// <summary>Werkzeug-Badge oben links auf Canvas anzeigen.</summary>
    private void UpdateToolBadge()
        => CodingToolBadgeController.Update(
            CodingOverlayCanvas,
            _codingOverlayToolHost.HasOverlayService,
            _codingOverlayToolHost.ActiveTool,
            _codingSchemaTypeState.ActiveSchemaType,
            _codingOverlayToolHost.ActiveLevelMode);

    private async Task AnalyzeWithOverlayHintAsync(OverlayGeometry overlay)
    {
        await RunCodingAnalysisAsync("Analyse: markierte Stelle...");
    }

}
