using AuswertungPro.Next.Application.Ai;

namespace AuswertungPro.Next.Infrastructure.Ai.Shared;

/// <summary>Kompatibilitätsfassade für die FFmpeg- und FFprobe-Suche.</summary>
public static class FfmpegLocator
{
    /// <summary>Name der Umgebungsvariable für den FFmpeg-Pfad.</summary>
    public const string EnvKey = FfmpegFileLocator.EnvironmentVariableName;

    private static readonly IFfmpegExecutableLocator Default = new FfmpegFileLocator();

    public static IFfmpegExecutableLocator Current => Default;

    [Obsolete("Die FFmpeg-Fassade ist unveraenderbar. Abhaengigkeit direkt uebergeben.")]
    public static void Use(IFfmpegExecutableLocator locator)
    {
        ArgumentNullException.ThrowIfNull(locator);
        throw new NotSupportedException(
            "Die FFmpeg-Fassade kann nicht mehr global ersetzt werden.");
    }

    public static string ResolveFfmpeg()
        => Current.ResolveFfmpeg();

    public static string ResolveFfprobe()
        => Current.ResolveFfprobe();

    /// <summary>Leitet den FFprobe-Pfad ohne Datei- oder Ordnerzugriff ab.</summary>
    public static string DeriveFfprobeFrom(string ffmpegPath)
        => FfmpegFileLocator.DeriveFfprobeFrom(ffmpegPath);

    public static bool IsFfmpegAvailable()
        => Current.IsFfmpegAvailable();
}
