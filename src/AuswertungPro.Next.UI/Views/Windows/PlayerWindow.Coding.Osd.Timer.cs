using System;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private void StartCodingOsdTimer()
    {
        _codingOsdMeterController.StartTimer(
            PlayerWindowTimerFactory.CreateCodingOsdTimer,
            () => new CodingOsdTimerContext(
                IsClosing: _closing,
                HasPlayer: _player is not null,
                IsCodingMode: _isCodingMode,
                IsCodingAnalyzing: _codingIsAnalyzing,
                HasLiveDetection: _codingLiveDetection is not null),
            CodingReadOsdMeterAsync);
    }

    private void StopCodingOsdTimer()
    {
        _codingOsdMeterController.StopTimer();
    }
}
