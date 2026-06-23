using System;
using System.Windows.Threading;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private void StartCodingOsdTimer()
    {
        _codingOsdTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _codingOsdTimer.Tick += async (_, _) =>
        {
            // Waehrend einer laufenden Live-Analyse liest diese bereits den OSD-Meter
            // -> separaten 3s-OSD-Timer aussetzen, um doppelte Qwen-Last zu vermeiden.
            if (!CodingOsdTimerPolicy.ShouldReadMeter(
                    _closing,
                    hasPlayer: _player is not null,
                    _isCodingMode,
                    _codingOsdReading,
                    _codingIsAnalyzing,
                    hasLiveDetection: _codingLiveDetection is not null))
                return;
            _codingOsdReading = true;
            try
            {
                await CodingReadOsdMeterAsync();
            }
            finally
            {
                _codingOsdReading = false;
            }
        };
        _codingOsdTimer.Start();
    }

    private void StopCodingOsdTimer()
    {
        _codingOsdTimer?.Stop();
        _codingOsdTimer = null;
        _codingOsdReading = false;
    }
}
