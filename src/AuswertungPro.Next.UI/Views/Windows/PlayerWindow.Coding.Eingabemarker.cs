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
        => CodingEingabemarkerCanvasInputWorkflow.MouseDown(
            new CodingEingabemarkerCanvasMouseDownRequest(
                IsDrawing: _eingabemarkerPhase == EingabemarkerPhase.Drawing,
                CanvasPosition: canvasPos),
            new CodingEingabemarkerCanvasMouseDownActions(
                StoreDragStart: point => _eingabemarkerDragStart = point,
                CaptureMouse: () => CodingOverlayCanvas.CaptureMouse(),
                CreatePreview: point => _eingabemarkerPreviewRect = CodingEingabemarkerPreviewRenderer.Create(
                    CodingOverlayCanvas,
                    point)));

    private void EingabemarkerCanvas_MouseMove(Point canvasPos)
        => CodingEingabemarkerCanvasInputWorkflow.MouseMove(
            new CodingEingabemarkerCanvasMouseMoveRequest(
                IsDrawing: _eingabemarkerPhase == EingabemarkerPhase.Drawing,
                HasPreview: _eingabemarkerPreviewRect != null,
                DragStart: _eingabemarkerDragStart,
                CanvasPosition: canvasPos),
            new CodingEingabemarkerCanvasMouseMoveActions(
                UpdatePreview: previewRect => CodingEingabemarkerPreviewRenderer.Update(
                    _eingabemarkerPreviewRect!,
                    previewRect)));

    private void EingabemarkerCanvas_MouseUp(Point canvasPos)
        => CodingEingabemarkerCanvasInputWorkflow.MouseUp(
            new CodingEingabemarkerCanvasMouseUpRequest(
                IsDrawing: _eingabemarkerPhase == EingabemarkerPhase.Drawing,
                DragStart: _eingabemarkerDragStart,
                CanvasPosition: canvasPos,
                CanvasSize: new Size(CodingOverlayCanvas.ActualWidth, CodingOverlayCanvas.ActualHeight)),
            new CodingEingabemarkerCanvasMouseUpActions(
                ReleaseMouseCapture: CodingOverlayCanvas.ReleaseMouseCapture,
                CancelMarker: CancelEingabemarker,
                StoreNormalizedSelection: rect => _eingabemarkerRectNorm = rect,
                SetInputPhase: () => _eingabemarkerPhase = EingabemarkerPhase.Input,
                DisableDrawingCanvas: () => CodingOverlayInputControls.DisableDrawingCanvas(CodingOverlayCanvas),
                ShowInputPopup: () => CodingEingabemarkerPopupControls.ShowInput(
                    EingabemarkerPopup,
                    TxtEingabemarker,
                    CmbEingabemarker),
                FocusInput: () => Dispatcher.BeginInvoke(new Action(() => TxtEingabemarker.Focus()),
                    System.Windows.Threading.DispatcherPriority.Input),
                ShowInputStatus: () => SetCodingAiState(
                    "Beschreibung eingeben oder Stichwort wählen, dann Enter",
                    PlayerStatusColors.Info,
                    "z.B. \"Beule unten\", \"Riss bei 3 Uhr\", \"Anschluss offen\"")));

}
