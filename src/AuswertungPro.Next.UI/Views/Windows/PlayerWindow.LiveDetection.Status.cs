using System.Windows.Media;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private void SetLiveDetectionBadge(string status, Color dotColor, string? stage = null)
        => RunStatusUi(() => LiveDetectionStatusControls.ShowLiveDetectionBadge(
            AiStatusBadge,
            AiStatusText,
            AiStatusDot,
            status,
            dotColor,
            stage));

    private void SetYoloStatus(string text, Color dotColor, string? model = null)
        => RunStatusUi(() => LiveDetectionStatusControls.ShowYoloStatus(
            YoloStatusBar,
            TxtYoloStatus,
            YoloDot,
            TxtYoloModel,
            text,
            dotColor,
            model));

    private void SetCodingAiState(string status, Color dotColor, string? stage = null, bool pulse = false)
        => RunStatusUi(() => LiveDetectionCodingAiStateWorkflow.Execute(
            new LiveDetectionCodingAiStateWorkflowRequest(pulse),
            new LiveDetectionCodingAiStateWorkflowActions(
                ShowCodingAiState: () => LiveDetectionStatusControls.ShowCodingAiState(
                    TxtCodingAiStatus,
                    TxtCodingAiStage,
                    CodingAiDot,
                    status,
                    dotColor,
                    stage),
                StartPulse: StartCodingAiPulse,
                StopPulse: StopCodingAiPulse)));

    private void RunStatusUi(Action apply)
        => PlayerUiDispatchWorkflow.Execute(
            new PlayerUiDispatchWorkflowRequest(
                HasDispatcherAccess: Dispatcher.CheckAccess()),
            new PlayerUiDispatchWorkflowActions(
                Apply: apply,
                DispatchToUi: action => Dispatcher.Invoke(action)));

    private void UpdateDetectionStatus(LiveDetection result)
    {
        LiveDetectionStatusControls.ShowDetectionStatus(
            LiveDetectionStatusText,
            FindingSummaryPanel,
            FindingSummaryText,
            result);
    }
}
