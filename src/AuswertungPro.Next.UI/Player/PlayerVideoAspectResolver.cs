namespace AuswertungPro.Next.UI.Player;

internal delegate bool PlayerVideoSizeReader(
    uint videoNumber,
    ref uint width,
    ref uint height);

internal readonly record struct PlayerVideoAspectMetadata(
    uint SampleAspectNumerator,
    uint SampleAspectDenominator,
    bool SwapAxes);

internal static class PlayerVideoAspectResolver
{
    internal static bool TryResolve(
        PlayerVideoSizeReader readSize,
        out double aspect)
        => TryResolve(readSize, metadata: null, out aspect);

    internal static bool TryResolve(
        PlayerVideoSizeReader readSize,
        PlayerVideoAspectMetadata? metadata,
        out double aspect)
    {
        ArgumentNullException.ThrowIfNull(readSize);

        aspect = 0;
        uint width = 0;
        uint height = 0;

        try
        {
            if (!readSize(0, ref width, ref height) || width == 0 || height == 0)
                return false;
        }
        catch
        {
            // Die Videogroesse ist optionale Darstellungsinformation. Wenn LibVLC sie
            // noch nicht kennt (oder der Player gerade schliesst), bleibt der bisherige
            // Overlay-Zustand erhalten, statt den Codierablauf abzubrechen.
            return false;
        }

        aspect = (double)width / height;
        if (metadata is { SampleAspectNumerator: > 0, SampleAspectDenominator: > 0 } video)
            aspect *= (double)video.SampleAspectNumerator / video.SampleAspectDenominator;

        if (metadata?.SwapAxes == true)
            aspect = 1 / aspect;

        return double.IsFinite(aspect) && aspect > 0;
    }
}
