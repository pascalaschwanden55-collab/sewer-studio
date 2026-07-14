namespace AuswertungPro.Next.Application.Ai;

/// <summary>Löst die ausführbaren FFmpeg- und FFprobe-Programme auf.</summary>
public interface IFfmpegExecutableLocator
{
    string ResolveFfmpeg();

    string ResolveFfprobe();

    bool IsFfmpegAvailable();
}
