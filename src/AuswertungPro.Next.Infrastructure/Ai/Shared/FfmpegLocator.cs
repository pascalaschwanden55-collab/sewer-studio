using System.Threading;
using AuswertungPro.Next.Application.Ai;

namespace AuswertungPro.Next.Infrastructure.Ai.Shared;

/// <summary>Kompatibilitätsfassade für die FFmpeg- und FFprobe-Suche.</summary>
public static class FfmpegLocator
{
    /// <summary>Name der Umgebungsvariable für den FFmpeg-Pfad.</summary>
    public const string EnvKey = FfmpegFileLocator.EnvironmentVariableName;

    private static IFfmpegExecutableLocator _current = new FfmpegFileLocator();

    public static IFfmpegExecutableLocator Current => Volatile.Read(ref _current);

    public static void Use(IFfmpegExecutableLocator locator)
        => Volatile.Write(
            ref _current,
            locator ?? throw new ArgumentNullException(nameof(locator)));

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
