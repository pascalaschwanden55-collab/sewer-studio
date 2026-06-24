using System.Windows.Media;
using AuswertungPro.Next.Application.Ai;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private void SetLiveDetectionBadge(string status, Color dotColor, string? stage = null)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.Invoke(() => SetLiveDetectionBadge(status, dotColor, stage));
            return;
        }

        LiveDetectionStatusControls.ShowLiveDetectionBadge(AiStatusBadge, AiStatusText, AiStatusDot, status, dotColor, stage);
    }

    private void SetYoloStatus(string text, Color dotColor, string? model = null)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.Invoke(() => SetYoloStatus(text, dotColor, model));
            return;
        }

        LiveDetectionStatusControls.ShowYoloStatus(YoloStatusBar, TxtYoloStatus, YoloDot, TxtYoloModel, text, dotColor, model);
    }

    private void SetCodingAiState(string status, Color dotColor, string? stage = null, bool pulse = false)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.Invoke(() => SetCodingAiState(status, dotColor, stage, pulse));
            return;
        }

        LiveDetectionStatusControls.ShowCodingAiState(TxtCodingAiStatus, TxtCodingAiStage, CodingAiDot, status, dotColor, stage);
        if (pulse)
            StartCodingAiPulse();
        else
            StopCodingAiPulse();
    }

    private void UpdateDetectionStatus(LiveDetection result)
    {
        LiveDetectionStatusControls.ShowDetectionStatus(
            LiveDetectionStatusText,
            FindingSummaryPanel,
            FindingSummaryText,
            result);
    }
}
