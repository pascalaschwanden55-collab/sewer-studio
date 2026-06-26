namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private void StartCodingOsdTimer()
    {
        _codingOsdMeterController.StartTimer(
            () => _closing,
            () => !_playbackDisposed,
            () => _codingModeState.IsCodingMode,
            () => _codingAiRuntimeOwner.Controller.IsAnalyzing,
            () => _codingAiRuntimeOwner.Controller.LiveDetection is not null,
            CodingReadOsdMeterAsync);
    }

    private void StopCodingOsdTimer()
    {
        _codingOsdMeterController.StopTimer();
    }
}
