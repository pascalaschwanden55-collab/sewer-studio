using AuswertungPro.Next.Infrastructure.Ai.Shared;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

/// <summary>Fuehrt den Test nur aus, wenn ffmpeg auffindbar und ausfuehrbar ist.</summary>
internal sealed class FfmpegFactAttribute : FactAttribute
{
    public FfmpegFactAttribute()
    {
        if (!FfmpegProbe.Verfuegbar)
            Skip = "ffmpeg nicht auffindbar (SEWERSTUDIO_FFMPEG, PATH oder Standardorte).";
    }
}

internal static class FfmpegProbe
{
    private static readonly Lazy<bool> VerfuegbarLazy = new(Pruefe);

    public static bool Verfuegbar => VerfuegbarLazy.Value;

    private static bool Pruefe()
    {
        try
        {
            var pfad = new FfmpegFileLocator().ResolveFfmpeg();
            return !string.IsNullOrWhiteSpace(pfad)
                && (File.Exists(pfad) || !Path.IsPathRooted(pfad));
        }
        catch
        {
            return false;
        }
    }
}
