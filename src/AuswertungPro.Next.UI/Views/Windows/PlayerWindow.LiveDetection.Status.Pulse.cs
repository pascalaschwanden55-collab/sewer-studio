using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private void StartCodingAiPulse()
    {
        LiveDetectionPulseWorkflow.Start(
            new LiveDetectionPulseStartRequest(_codingAiPulseStateController.IsRunning),
            _codingAiPulseStateController.CreateStartActions(
                () => LiveDetectionPulseControls.Start(CodingAiPulseRing)));
    }

    private void StopCodingAiPulse()
    {
        LiveDetectionPulseWorkflow.Stop(
            _codingAiPulseStateController.CreateStopActions(
                () => LiveDetectionPulseControls.Stop(CodingAiPulseRing)));
    }
}
