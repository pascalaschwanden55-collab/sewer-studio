using System;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Infrastructure.Ai.Pipeline;

/// <summary>
/// Segmentiert eine vom Nutzer gezogene Bounding-Box per SAM und quantifiziert die erste
/// Maske (Uhrlage, Hoehe/Breite mm, Querschnitt-%). Haelt die SAM-Logik aus dem ohnehin
/// grossen Codiermodus-Window heraus und ist isoliert testbar (SAM-Aufruf als Delegate
/// injiziert, kein HTTP/UI-Wissen). Reine Orchestrierung, keine Seiteneffekte.
/// </summary>
public sealed class MarkBoxSegmentationService
{
    private readonly Func<SamRequest, CancellationToken, Task<SamResponse>> _segment;

    public MarkBoxSegmentationService(Func<SamRequest, CancellationToken, Task<SamResponse>> segment)
        => _segment = segment ?? throw new ArgumentNullException(nameof(segment));

    /// <summary>
    /// Schickt den Frame + die (normalisierte) Box an SAM und quantifiziert die erste Maske.
    /// Gibt null zurueck, wenn Frame/Box ungueltig sind oder SAM keine Maske liefert — der
    /// Aufrufer faellt dann auf die geometrische Schaetzung zurueck (nie ein harter Fehler).
    /// </summary>
    public async Task<BoxSegmentationResult?> SegmentBoxAsync(
        byte[]? frameBytes,
        NormalizedBoundingBox box,
        int pipeDiameterMm,
        PipeCalibration? calibration,
        CancellationToken ct = default)
    {
        if (frameBytes is null || frameBytes.Length == 0) return null;
        if (box.Width <= 0 || box.Height <= 0) return null;
        if (!TryReadPngSize(frameBytes, out var imgW, out var imgH)) return null;

        // Normalisierte Mitten-Box -> Pixel-Eckpunkte im Bildraum des gesendeten Frames.
        double x1 = Math.Clamp(box.XCenter - box.Width / 2.0, 0, 1) * imgW;
        double y1 = Math.Clamp(box.YCenter - box.Height / 2.0, 0, 1) * imgH;
        double x2 = Math.Clamp(box.XCenter + box.Width / 2.0, 0, 1) * imgW;
        double y2 = Math.Clamp(box.YCenter + box.Height / 2.0, 0, 1) * imgH;

        var request = new SamRequest(
            Convert.ToBase64String(frameBytes),
            new[] { new SamBoundingBox(x1, y1, x2, y2, "manuell", 1.0) },
            pipeDiameterMm > 0 ? pipeDiameterMm : null);

        var response = await _segment(request, ct).ConfigureAwait(false);
        if (response?.Masks is null || response.Masks.Count == 0) return null;

        var mask = response.Masks[0];
        var quant = MaskQuantificationService.Quantify(
            mask, response.ImageWidth, response.ImageHeight,
            Math.Max(0, pipeDiameterMm), calibration);

        return new BoxSegmentationResult(quant, mask, response.ImageWidth, response.ImageHeight);
    }

    /// <summary>
    /// Liest Breite/Hoehe aus dem PNG-IHDR-Header (Big-Endian ab Byte 16). Dependency-frei,
    /// damit die Infrastruktur-Schicht kein Bild-/UI-Decoder-Paket braucht.
    /// </summary>
    private static bool TryReadPngSize(byte[] png, out int width, out int height)
    {
        width = height = 0;
        if (png.Length < 24) return false;
        if (png[0] != 0x89 || png[1] != 0x50 || png[2] != 0x4E || png[3] != 0x47) return false;
        width = (png[16] << 24) | (png[17] << 16) | (png[18] << 8) | png[19];
        height = (png[20] << 24) | (png[21] << 16) | (png[22] << 8) | png[23];
        return width > 0 && height > 0;
    }
}

/// <summary>Ergebnis der Box-Segmentierung: quantifizierte Maske + Rohmaske + Bildmasse.</summary>
public sealed record BoxSegmentationResult(
    MaskQuantificationService.QuantifiedMask Quant,
    SamMaskResult Mask,
    int ImageWidth,
    int ImageHeight);
