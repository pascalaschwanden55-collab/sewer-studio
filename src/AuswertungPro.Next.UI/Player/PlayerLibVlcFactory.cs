using LibVLCSharp.Shared;

namespace AuswertungPro.Next.UI.Player;

public static class PlayerLibVlcFactory
{
    public static LibVLC Create(PlayerWindowOptions options)
    {
        var args = PlayerLibVlcArguments.Build(options);

        try
        {
            return new LibVLC(args);
        }
        catch
        {
            return new LibVLC();
        }
    }
}
