using System.Windows;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private void Eingabemarker_Click(object sender, RoutedEventArgs e)
        => CodingEingabemarkerToggleWorkflow.Execute(
            new CodingEingabemarkerToggleWorkflowRequest(PlayerToggleButtonControls.IsChecked(BtnEingabemarker)),
            CreateEingabemarkerToggleActions());

    private void CancelEingabemarker()
        => CodingEingabemarkerToggleWorkflow.Execute(
            new CodingEingabemarkerToggleWorkflowRequest(IsChecked: false),
            CreateEingabemarkerToggleActions());

    private CodingEingabemarkerToggleWorkflowActions CreateEingabemarkerToggleActions()
        => new(
            PauseForCodingInteraction: () => PlayerCodingPlayback.PauseForCodingInteraction(_playerPlaybackControlHost.SetPause),
            SetDrawingPhase: _eingabemarkerState.SetDrawingPhase,
            EnsureMarkOverlayReady: EnsureMarkOverlayReady,
            OpenCodingOverlayPopup: () => CodingOverlayInputControls.OpenPopup(CodingOverlayPopup),
            UpdateCodingOverlayViewport: UpdateCodingOverlayViewport,
            EnableDrawingCanvas: () => CodingOverlayInputControls.EnableDrawingCanvas(CodingOverlayCanvas),
            ShowDrawingStatus: () => SetCodingAiState(
                "Eingabemarker: Rechteck um die Beobachtung ziehen",
                PlayerStatusColors.Info,
                "Klicken + Ziehen = Bereich markieren"),
            SetInactivePhase: _eingabemarkerState.SetInactivePhase,
            UncheckButton: () => PlayerToggleButtonControls.Uncheck(BtnEingabemarker),
            HideInputPopup: () => CodingEingabemarkerPopupControls.Hide(EingabemarkerPopup),
            ClearPreview: () =>
            {
                _eingabemarkerState.SetPreview(CodingEingabemarkerPreviewRenderer.Clear(
                    CodingOverlayCanvas,
                    _eingabemarkerState.PreviewRect));
            },
            ResetCanvasCursor: () => CodingOverlayInputControls.ResetCanvasCursor(CodingOverlayCanvas));

    private void EingabemarkerCanvas_MouseDown(Point canvasPos)
        => CodingEingabemarkerCanvasInputWorkflow.MouseDown(
            new CodingEingabemarkerCanvasMouseDownRequest(
                IsDrawing: _eingabemarkerState.IsDrawing,
                CanvasPosition: canvasPos),
            new CodingEingabemarkerCanvasMouseDownActions(
                StoreDragStart: _eingabemarkerState.StoreDragStart,
                CaptureMouse: () => CodingOverlayInputControls.CaptureCanvasMouse(CodingOverlayCanvas),
                CreatePreview: point => _eingabemarkerState.SetPreview(CodingEingabemarkerPreviewRenderer.Create(
                    CodingOverlayCanvas,
                    point))));

    private void EingabemarkerCanvas_MouseMove(Point canvasPos)
        => CodingEingabemarkerCanvasInputWorkflow.MouseMove(
            new CodingEingabemarkerCanvasMouseMoveRequest(
                IsDrawing: _eingabemarkerState.IsDrawing,
                HasPreview: _eingabemarkerState.HasPreview,
                DragStart: _eingabemarkerState.DragStart,
                CanvasPosition: canvasPos),
            new CodingEingabemarkerCanvasMouseMoveActions(
                UpdatePreview: previewRect => CodingEingabemarkerPreviewRenderer.Update(
                    _eingabemarkerState.PreviewRect!,
                    previewRect)));

    private void EingabemarkerCanvas_MouseUp(Point canvasPos)
        => CodingEingabemarkerCanvasInputWorkflow.MouseUp(
            new CodingEingabemarkerCanvasMouseUpRequest(
                IsDrawing: _eingabemarkerState.IsDrawing,
                DragStart: _eingabemarkerState.DragStart,
                CanvasPosition: canvasPos,
                CanvasSize: CodingOverlayInputControls.GetCanvasActualSize(CodingOverlayCanvas)),
            new CodingEingabemarkerCanvasMouseUpActions(
                ReleaseMouseCapture: () => CodingOverlayInputControls.ReleaseCanvasMouse(CodingOverlayCanvas),
                CancelMarker: CancelEingabemarker,
                StoreNormalizedSelection: _eingabemarkerState.StoreNormalizedSelection,
                SetInputPhase: _eingabemarkerState.SetInputPhase,
                DisableDrawingCanvas: () => CodingOverlayInputControls.DisableDrawingCanvas(CodingOverlayCanvas),
                ShowInputPopup: () => CodingEingabemarkerPopupControls.ShowInput(
                    EingabemarkerPopup,
                    TxtEingabemarker,
                    CmbEingabemarker),
                FocusInput: () => PlayerDispatcherScheduler.ScheduleInput(
                    Dispatcher,
                    () => TxtEingabemarker.Focus()),
                ShowInputStatus: () => SetCodingAiState(
                    "Beschreibung eingeben oder Stichwort wählen, dann Enter",
                    PlayerStatusColors.Info,
                    "z.B. \"Beule unten\", \"Riss bei 3 Uhr\", \"Anschluss offen\"")));

}
