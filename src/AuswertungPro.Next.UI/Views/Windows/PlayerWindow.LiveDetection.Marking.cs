using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    /// <summary>
    /// Nach abgeschlossener Markierung: Code-Katalog oeffnen und Training speichern.
    /// </summary>
    private async void HandleMarkDrawingComplete()
    {
        try
        {
            var overlay = _codingVm?.CurrentOverlay;
            if (overlay == null)
                return;

            var timestampSec = _player.Time / 1000.0;
            var frameBytes = await CaptureCurrentFrameAsync();

            string? clockPos = LiveDetectionGeometryMapper.EstimateClockFromOverlayCenter(overlay);

            var samResult = await TrySegmentMarkBoxAsync(overlay, frameBytes);
            if (!string.IsNullOrEmpty(samResult?.Quant.ClockPosition))
                clockPos = samResult!.Quant.ClockPosition;

            if (samResult != null)
            {
                ShowMarkSamMask(samResult, overlay);
                // SAM-Maske kurz sichtbar lassen, bevor der Code-Dialog oeffnet.
                await Task.Delay(3000);
            }

            bool saved = await SaveMarkAsTrainingAsync(overlay, timestampSec, clockPos, frameBytes);

            // Nach dem Dialog alle transienten Markierungsartefakte entfernen.
            Ai.Pipeline.SamMaskRenderer.ClearMasks(CodingOverlayCanvas);
            BendMarkerRenderer.Clear(CodingOverlayCanvas);
            if (_codingVm != null)
                _codingVm.CurrentOverlay = null;
            RedrawCodingCanvas(includeManualOverlay: false);

            // Im Codiermodus Werkzeug aktiv lassen, damit mehrere Markierungen nacheinander moeglich sind.
            if (saved && !_isCodingMode)
            {
                DeactivateMarkTool();
            }
            else
            {
                if (_codingOverlayService != null)
                    _codingOverlayService.ActiveTool = _markToolType;
                CodingOverlayCanvas.Cursor = Cursors.Cross;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PlayerWindow] HandleMarkDrawingComplete error: {ex.Message}");
        }
    }

    private void ShowOsdMeterStatus(string message, bool resetAfterDelay)
    {
        OsdMeterBadge.Visibility = Visibility.Visible;
        TxtOsdMeter.Text = message;

        if (!resetAfterDelay)
            return;

        var resetTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        resetTimer.Tick += (_, _) =>
        {
            resetTimer.Stop();
            if (_codingLastOsdMeter.HasValue)
                TxtOsdMeter.Text = CodingOsdBadgeDisplayPolicy.BuildMeterText(_codingLastOsdMeter.Value);
            else
                OsdMeterBadge.Visibility = Visibility.Collapsed;
        };
        resetTimer.Start();
    }
}
