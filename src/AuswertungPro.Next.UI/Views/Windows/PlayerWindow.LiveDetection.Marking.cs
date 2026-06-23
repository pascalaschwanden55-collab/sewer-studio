using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.Models;
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

    /// <summary>
    /// Laesst SAM die gezogene Box segmentieren und schreibt Messwerte ins Overlay.
    /// </summary>
    private async Task<Infrastructure.Ai.Pipeline.BoxSegmentationResult?> TrySegmentMarkBoxAsync(
        OverlayGeometry overlay, byte[]? frameBytes)
    {
        if (_codingBoxSegmentation == null || frameBytes == null || frameBytes.Length == 0
            || overlay.Points.Count < 2)
            return null;
        try
        {
            var box = LiveDetectionGeometryMapper.BBoxFromOverlay(overlay);
            var calibration = _codingOverlayService?.Calibration;
            int dn = calibration?.NominalDiameterMm ?? 0;

            var result = await _codingBoxSegmentation.SegmentBoxAsync(
                frameBytes, box, dn, calibration, System.Threading.CancellationToken.None);
            if (result == null)
                return null;

            CodingMarkBoxQuantificationOverlayPolicy.Apply(overlay, result.Quant);

            return result;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Mark-SAM] Segmentierung uebersprungen: {ex.Message}");
            return null;
        }
    }

    private void ShowMarkSamMask(Infrastructure.Ai.Pipeline.BoxSegmentationResult result, OverlayGeometry? overlay)
    {
        try
        {
            var rect = GetCodingContentRect();
            if (rect.Width <= 0 || rect.Height <= 0)
                return;

            // Bei Boegen keine SAM-Maske zeigen; ein Marker am Fluchtpunkt ist stabiler.
            if (result.IsBend && LiveDetectionGeometryMapper.BoxContainsVanishingPoint(overlay, result.VanishX, result.VanishY))
            {
                BendMarkerRenderer.Show(CodingOverlayCanvas, result.VanishX, result.VanishY, rect);
                return;
            }

            var samResp = new Infrastructure.Ai.Pipeline.SamResponse(
                new[] { result.Mask }, result.ImageWidth, result.ImageHeight, 0);
            // In das echte Video-Rechteck rendern, nicht in Letterbox-Raender.
            Ai.Pipeline.SamMaskRenderer.RenderMasks(
                CodingOverlayCanvas,
                samResp,
                new[] { result.Quant },
                rect.Width,
                rect.Height,
                logger: null,
                options: null,
                offsetX: rect.X,
                offsetY: rect.Y);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Mark-SAM] Masken-Render uebersprungen: {ex.Message}");
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
