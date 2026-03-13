using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Protocol;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai.Shared;

namespace AuswertungPro.Next.UI.Ai;

/// <summary>
/// Generiert automatisch Fotos (Frame-Extraktion) fuer Rohranfang (BCD) und Rohrende/Abbruch (BCE/BDC*).
/// Speichert die Fotos im Haltungsverzeichnis und verknuepft sie mit dem ProtocolEntry.
/// </summary>
public static class BoundaryPhotoService
{
    /// <summary>
    /// Extrahiert Fotos fuer BCD- und BCE/BDC*-Eintraege aus dem Video.
    /// </summary>
    public static async Task GenerateBoundaryPhotosAsync(
        ProtocolBoundaryResult boundaries,
        string videoPath,
        string holdingDir,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(videoPath) || !File.Exists(videoPath))
            return;

        var ffmpeg = FfmpegLocator.ResolveFfmpeg();
        var photoDir = Path.Combine(holdingDir, "Fotos");
        if (!Directory.Exists(photoDir))
            Directory.CreateDirectory(photoDir);

        // Rohranfang-Foto (bei 0.00m → Videozeit 00:00)
        if (boundaries.RohranfangEntry is not null)
        {
            await ExtractAndLinkAsync(
                boundaries.RohranfangEntry,
                videoPath, ffmpeg, photoDir,
                "BCD_Rohranfang", ct).ConfigureAwait(false);
        }

        // Rohrende/Abbruch-Foto
        if (boundaries.EndEntry is not null)
        {
            var label = boundaries.IsAbort
                ? $"{boundaries.EndEntry.Code}_Abbruch"
                : "BCE_Rohrende";
            await ExtractAndLinkAsync(
                boundaries.EndEntry,
                videoPath, ffmpeg, photoDir,
                label, ct).ConfigureAwait(false);
        }
    }

    private static async Task ExtractAndLinkAsync(
        ProtocolEntry entry,
        string videoPath,
        string ffmpeg,
        string photoDir,
        string label,
        CancellationToken ct)
    {
        // Bereits ein Foto vorhanden → nichts tun
        if (entry.FotoPaths.Count > 0 && File.Exists(entry.FotoPaths[0]))
            return;

        var zeit = entry.Zeit ?? TimeSpanFromMeter(entry.MeterStart);
        if (zeit is null)
            return;

        var bytes = await VideoFrameExtractor.TryExtractFramePngAsync(
            ffmpeg, videoPath, zeit.Value, ct).ConfigureAwait(false);

        if (bytes is null || bytes.Length == 0)
            return;

        // Dateiname: BCD_Rohranfang_0.00m.png
        var meterStr = (entry.MeterStart ?? 0).ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
        var fileName = $"{label}_{meterStr}m.png";
        var filePath = Path.Combine(photoDir, fileName);

        await File.WriteAllBytesAsync(filePath, bytes, ct).ConfigureAwait(false);
        entry.FotoPaths.Add(filePath);
    }

    /// <summary>
    /// Grobe Schaetzung: Videozeit aus Meterposition (fuer Faelle ohne explizite Zeit).
    /// Wird nur als Fallback verwendet.
    /// </summary>
    private static TimeSpan? TimeSpanFromMeter(double? meter)
    {
        if (meter is null or <= 0)
            return TimeSpan.Zero; // Rohranfang = Videoanfang

        // Kein sinnvoller Fallback moeglich ohne Haltungslaenge/Videodauer
        return null;
    }
}
