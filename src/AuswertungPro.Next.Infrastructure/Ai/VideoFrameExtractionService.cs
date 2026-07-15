using System.Diagnostics;
using System.Globalization;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.Media;

namespace AuswertungPro.Next.Infrastructure.Ai;

/// <summary>
/// Startet ffmpeg fuer ein einzelnes Videobild und entfernt die temporaere PNG-Datei.
/// </summary>
public sealed class VideoFrameExtractionService : IVideoFrameExtractor
{
    private readonly IProcessOutputReader _processOutputs;

    public VideoFrameExtractionService(IProcessOutputReader processOutputs)
    {
        _processOutputs = processOutputs ?? throw new ArgumentNullException(nameof(processOutputs));
    }

    public async Task<byte[]?> TryExtractFramePngAsync(
        string ffmpegPath,
        string videoPath,
        TimeSpan at,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(videoPath))
            return null;

        var outPng = Path.Combine(
            Path.GetTempPath(),
            $"auswertungpro_frame_{Guid.NewGuid():N}.png");
        var args =
            $"-hide_banner -loglevel error -ss {at.TotalSeconds.ToString(CultureInfo.InvariantCulture)} " +
            $"-i \"{videoPath}\" -frames:v 1 -vf scale='min(1280,iw)':-2 -y \"{outPng}\"";
        var startInfo = new ProcessStartInfo
        {
            FileName = ffmpegPath,
            Arguments = args,
            UseShellExecute = false,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            CreateNoWindow = true
        };

        try
        {
            var output = await _processOutputs
                .ReadToExitAsync(startInfo, cancellationToken)
                .ConfigureAwait(false);
            if (output is null || output.ExitCode != 0 || !File.Exists(outPng))
                return null;

            return await File.ReadAllBytesAsync(outPng, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            try
            {
                if (File.Exists(outPng))
                    File.Delete(outPng);
            }
            catch
            {
                // Temp-Aufraeumen bleibt wie bisher Best Effort.
            }
        }
    }
}
