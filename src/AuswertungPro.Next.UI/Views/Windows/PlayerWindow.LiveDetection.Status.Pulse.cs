using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private void StartCodingAiPulse()
    {
        LiveDetectionPulseWorkflow.Start(
            new LiveDetectionPulseStartRequest(_codingAiPulseRunning),
            new LiveDetectionPulseStartActions(
                SetRunning: () => _codingAiPulseRunning = true,
                StartPulse: () => LiveDetectionPulseControls.Start(CodingAiPulseRing)));
    }

    private void StopCodingAiPulse()
    {
        LiveDetectionPulseWorkflow.Stop(
            new LiveDetectionPulseStopActions(
                ClearRunning: () => _codingAiPulseRunning = false,
                StopPulse: () => LiveDetectionPulseControls.Stop(CodingAiPulseRing)));
    }
}
