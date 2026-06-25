using LibVLCSharp.Shared;

namespace AuswertungPro.Next.UI.Player;

public static class PlayerMediaRuntimeFactory
{
    public static PlayerMediaRuntime Create(PlayerWindowOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        Core.Initialize();

        var libVlc = PlayerLibVlcFactory.Create(options);
        var mediaPlayer = new MediaPlayer(libVlc)
        {
            EnableHardwareDecoding = options.EnableHardwareDecoding
        };
        var hosts = PlayerMediaHostFactory.Create(libVlc, mediaPlayer);

        return new PlayerMediaRuntime(libVlc, mediaPlayer, hosts);
    }
}
