using System;
using System.Threading.Tasks;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    /// <summary>
    /// HÃ¤lt die Overlay-ZeichenflÃ¤che exakt auf VideoView-GrÃ¶ÃŸe.
    /// Wichtig fÃ¼r Popup-Overlay Ã¼ber VLC (HwndHost/Airspace).
    /// </summary>
    private void UpdateCodingOverlayViewport()
        => CodingOverlayViewportController.Update(
            VideoView.ActualWidth,
            VideoView.ActualHeight,
            CodingOverlayCanvas.Width,
            CodingOverlayCanvas.Height,
            width => CodingOverlayCanvas.Width = width,
            height => CodingOverlayCanvas.Height = height);

    // --- Coding Navigation ---

    /// <summary>Werkzeug-Badge oben links auf Canvas anzeigen.</summary>
    private void UpdateToolBadge()
    {
        if (!_codingOverlayToolHost.HasOverlayService) return;

        var toolText = CodingToolBadgeTextPolicy.BuildText(
            _codingOverlayToolHost.ActiveTool,
            _codingSchemaType,
            _codingOverlayToolHost.ActiveLevelMode);

        CodingToolBadgeRenderer.Update(CodingOverlayCanvas, toolText);
    }

    private async Task AnalyzeWithOverlayHintAsync(OverlayGeometry overlay)
    {
        await RunCodingAnalysisAsync("Analyse: markierte Stelle...");
    }

}
