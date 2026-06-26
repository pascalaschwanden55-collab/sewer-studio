namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private void StartCodingOsdTimer()
    {
        _codingOsdMeterController.StartTimer(
            () => _shutdownState.IsClosing,
            () => !_shutdownState.IsPlaybackDisposed,
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
