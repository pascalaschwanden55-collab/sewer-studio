using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Helpers;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    /// <summary>
    /// Nach abgeschlossener Markierung: Code-Katalog oeffnen und Training speichern.
    /// </summary>
    private void HandleMarkDrawingComplete()
    {
        HandleMarkDrawingCompleteAsync().SafeFireAndForget("MarkDrawingComplete");
    }

    private async Task HandleMarkDrawingCompleteAsync()
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
                CodingOverlayInputControls.ApplyCanvasCursor(CodingOverlayCanvas, useCrossCursor: true);
            }
        }
        catch (Exception ex)
        {
            PlayerTrace.WriteLine($"[PlayerWindow] HandleMarkDrawingComplete error: {ex.Message}");
        }
    }

    private void ShowOsdMeterStatus(string message, bool resetAfterDelay)
    {
        CodingOsdBadgeControls.Show(OsdMeterBadge, TxtOsdMeter, message);

        if (!resetAfterDelay)
            return;

        var resetTimer = PlayerWindowTimerFactory.CreateOneShotTimer(TimeSpan.FromSeconds(3), () =>
        {
            if (_codingLastOsdMeter.HasValue)
                CodingOsdBadgeControls.ShowMeter(OsdMeterBadge, TxtOsdMeter, _codingLastOsdMeter.Value);
            else
                CodingOsdBadgeControls.Hide(OsdMeterBadge);
        });
        resetTimer.Start();
    }
}
