using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;
using AuswertungPro.Next.UI.Helpers;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private void CodingApply_Click(object sender, RoutedEventArgs e)
        => _codingApplyController.Apply(showOverlay: true);

    private void ConfirmAccept_Click(object sender, RoutedEventArgs e)
        => _codingConfirmationController.Accept().SafeFireAndForget("CodingConfirmAccept");

    private void ConfirmEdit_Click(object sender, RoutedEventArgs e)
        => _codingConfirmationController.Edit();

    private void ConfirmReject_Click(object sender, RoutedEventArgs e)
        => _codingConfirmationController.Reject().SafeFireAndForget("CodingConfirmReject");

    private void ConfirmSaveRetry_Click(object sender, RoutedEventArgs e)
        => _codingConfirmationController.RetrySave().SafeFireAndForget("CodingConfirmSaveRetry");

    private void Eingabemarker_Click(object sender, RoutedEventArgs e)
        => _codingEingabemarkerInteractionController.Toggle(
            PlayerToggleButtonControls.IsChecked(BtnEingabemarker));

    private void CmbEingabemarker_KeyDown(object sender, KeyEventArgs e)
        => _codingEingabemarkerInputController.HandleKey(
            isEscape: e.Key == Key.Escape,
            isEnter: e.Key == Key.Enter);

    private void CmbEingabemarker_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var popupVisible = CodingEingabemarkerPopupControls.IsVisible(EingabemarkerPopup);
        var selectedText = CodingEingabemarkerPopupControls.ResolveSelectedText(CmbEingabemarker.SelectedItem);

        _codingEingabemarkerInputController.HandleSelection(popupVisible, selectedText);

        // Schreibfokus zurueck ins Textfeld, damit Enter direkt bestaetigt.
        if (CodingEingabemarkerFocusPolicy.ShouldFocusInput(popupVisible, selectedText))
            CodingEingabemarkerPopupControls.FocusInput(TxtEingabemarker);
    }

    /// <summary>
    /// HÃ¤lt die Overlay-ZeichenflÃ¤che exakt auf VideoView-GrÃ¶ÃŸe.
    /// Wichtig fÃ¼r Popup-Overlay Ã¼ber VLC (HwndHost/Airspace).
    /// </summary>
    private void UpdateCodingOverlayViewport()
    {
        if (_playerMediaRuntime.TryGetVideoAspect(out var videoAspect))
            _codingOverlayRenderState.SetVideoAspect(videoAspect);

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
