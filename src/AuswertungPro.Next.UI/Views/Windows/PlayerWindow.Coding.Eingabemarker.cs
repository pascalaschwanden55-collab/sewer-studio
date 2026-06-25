using System;
using System.Windows;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private void Eingabemarker_Click(object sender, RoutedEventArgs e)
        => CodingEingabemarkerToggleWorkflow.Execute(
            new CodingEingabemarkerToggleWorkflowRequest(BtnEingabemarker.IsChecked == true),
            CreateEingabemarkerToggleActions());

    private void CancelEingabemarker()
        => CodingEingabemarkerToggleWorkflow.Execute(
            new CodingEingabemarkerToggleWorkflowRequest(IsChecked: false),
            CreateEingabemarkerToggleActions());

    private CodingEingabemarkerToggleWorkflowActions CreateEingabemarkerToggleActions()
        => new(
            PauseForCodingInteraction: () => PlayerCodingPlayback.PauseForCodingInteraction(_playerPlaybackControlHost.SetPause),
            SetDrawingPhase: () => _eingabemarkerPhase = EingabemarkerPhase.Drawing,
            EnsureMarkOverlayReady: EnsureMarkOverlayReady,
            OpenCodingOverlayPopup: () => CodingOverlayPopup.IsOpen = true,
            UpdateCodingOverlayViewport: UpdateCodingOverlayViewport,
            EnableDrawingCanvas: () => CodingOverlayInputControls.EnableDrawingCanvas(CodingOverlayCanvas),
            ShowDrawingStatus: () => SetCodingAiState(
                "Eingabemarker: Rechteck um die Beobachtung ziehen",
                PlayerStatusColors.Info,
                "Klicken + Ziehen = Bereich markieren"),
            SetInactivePhase: () => _eingabemarkerPhase = EingabemarkerPhase.Inactive,
            UncheckButton: () => BtnEingabemarker.IsChecked = false,
            HideInputPopup: () => CodingEingabemarkerPopupControls.Hide(EingabemarkerPopup),
            ClearPreview: () => _eingabemarkerPreviewRect = CodingEingabemarkerPreviewRenderer.Clear(
                CodingOverlayCanvas,
                _eingabemarkerPreviewRect),
            ResetCanvasCursor: () => CodingOverlayInputControls.ResetCanvasCursor(CodingOverlayCanvas));

    private void EingabemarkerCanvas_MouseDown(Point canvasPos)
    {
        if (_eingabemarkerPhase != EingabemarkerPhase.Drawing) return;

        _eingabemarkerDragStart = canvasPos;
        CodingOverlayCanvas.CaptureMouse();

        _eingabemarkerPreviewRect = CodingEingabemarkerPreviewRenderer.Create(
            CodingOverlayCanvas,
            canvasPos);
    }

    private void EingabemarkerCanvas_MouseMove(Point canvasPos)
    {
        if (_eingabemarkerPhase != EingabemarkerPhase.Drawing || _eingabemarkerPreviewRect == null) return;

        var previewRect = CodingEingabemarkerGeometryPolicy.BuildPreviewRect(
            _eingabemarkerDragStart,
            canvasPos);

        CodingEingabemarkerPreviewRenderer.Update(_eingabemarkerPreviewRect, previewRect);
    }

    private void EingabemarkerCanvas_MouseUp(Point canvasPos)
    {
        if (_eingabemarkerPhase != EingabemarkerPhase.Drawing) return;
        CodingOverlayCanvas.ReleaseMouseCapture();

        var normalizedRect = CodingEingabemarkerGeometryPolicy.BuildNormalizedSelection(
            _eingabemarkerDragStart,
            canvasPos,
            new Size(CodingOverlayCanvas.ActualWidth, CodingOverlayCanvas.ActualHeight));
        if (normalizedRect is null) { CancelEingabemarker(); return; }

        _eingabemarkerRectNorm = normalizedRect.Value;
        _eingabemarkerPhase = EingabemarkerPhase.Input;
        CodingOverlayInputControls.DisableDrawingCanvas(CodingOverlayCanvas);

        CodingEingabemarkerPopupControls.ShowInput(EingabemarkerPopup, TxtEingabemarker, CmbEingabemarker);
        Dispatcher.BeginInvoke(new Action(() => TxtEingabemarker.Focus()),
            System.Windows.Threading.DispatcherPriority.Input);

        SetCodingAiState("Beschreibung eingeben oder Stichwort wählen, dann Enter",
            PlayerStatusColors.Info, "z.B. \"Beule unten\", \"Riss bei 3 Uhr\", \"Anschluss offen\"");
    }

}
