using System;
using System.Threading.Tasks;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    /// <summary>
    /// Laesst SAM die gezogene Box segmentieren und schreibt Messwerte ins Overlay.
    /// </summary>
    private async Task<Infrastructure.Ai.Pipeline.BoxSegmentationResult?> TrySegmentMarkBoxAsync(
        OverlayGeometry overlay, byte[]? frameBytes)
    {
        var boxSegmentation = _codingAiRuntimeOwner.Controller.BoxSegmentation;
        if (boxSegmentation == null || frameBytes == null || frameBytes.Length == 0
            || overlay.Points.Count < 2)
            return null;
        try
        {
            var box = LiveDetectionGeometryMapper.BBoxFromOverlay(overlay);
            var calibration = _codingOverlayToolHost.Calibration;
            int dn = calibration?.NominalDiameterMm ?? 0;

            var result = await boxSegmentation.SegmentBoxAsync(
                frameBytes, box, dn, calibration, System.Threading.CancellationToken.None);
            if (result == null)
                return null;

            CodingMarkBoxQuantificationOverlayPolicy.Apply(overlay, result.Quant);

            return result;
        }
        catch (Exception ex)
        {
            PlayerTrace.WriteLine($"[Mark-SAM] Segmentierung uebersprungen: {ex.Message}");
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
            PlayerTrace.WriteLine($"[Mark-SAM] Masken-Render uebersprungen: {ex.Message}");
        }
    }
}
