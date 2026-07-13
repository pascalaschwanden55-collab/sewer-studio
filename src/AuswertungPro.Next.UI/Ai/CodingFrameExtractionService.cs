using System;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Infrastructure.Ai;
using AuswertungPro.Next.Infrastructure.Ai.Shared;

namespace AuswertungPro.Next.UI.Ai;

public sealed class CodingFrameExtractionService
{
    private readonly Func<string?> _ffmpegPathProvider;
    private readonly Func<string, string, TimeSpan, CancellationToken, Task<byte[]?>> _extractFrameAsync;
    private readonly Action<string> _log;

    public CodingFrameExtractionService()
        : this(
            FfmpegLocator.ResolveFfmpeg,
            VideoFrameExtractor.TryExtractFramePngAsync,
            message => BestEffort.ReportWarning(message))
    {
    }

    public CodingFrameExtractionService(
        Func<string?> ffmpegPathProvider,
        Func<string, string, TimeSpan, CancellationToken, Task<byte[]?>> extractFrameAsync,
        Action<string>? log = null)
    {
        _ffmpegPathProvider = ffmpegPathProvider ?? throw new ArgumentNullException(nameof(ffmpegPathProvider));
        _extractFrameAsync = extractFrameAsync ?? throw new ArgumentNullException(nameof(extractFrameAsync));
        _log = log ?? (_ => { });
    }

    public async Task<byte[]?> TryExtractFrameAtSecondsAsync(
        string? videoPath,
        double? seconds,
        CancellationToken cancellationToken = default)
    {
        if (seconds is null || seconds.Value < 0 || string.IsNullOrWhiteSpace(videoPath))
            return null;

        try
        {
            var ffmpeg = _ffmpegPathProvider();
            if (string.IsNullOrWhiteSpace(ffmpeg))
                return null;

            return await _extractFrameAsync(
                ffmpeg,
                videoPath,
                TimeSpan.FromSeconds(seconds.Value),
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _log($"[Foto] ffmpeg-Frame-Extraktion fehlgeschlagen: {ex.Message}");
            return null;
        }
    }
}
