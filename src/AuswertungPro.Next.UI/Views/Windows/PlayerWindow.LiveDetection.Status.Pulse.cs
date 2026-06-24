namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private void StartCodingAiPulse()
    {
        if (_codingAiPulseRunning)
            return;

        _codingAiPulseRunning = true;
        LiveDetectionPulseControls.Start(CodingAiPulseRing);
    }

    private void StopCodingAiPulse()
    {
        _codingAiPulseRunning = false;
        LiveDetectionPulseControls.Stop(CodingAiPulseRing);
    }
}
