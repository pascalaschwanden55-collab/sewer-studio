namespace AuswertungPro.Next.UI.Player;

public sealed record PlayerWindowPlaybackContext(
    string VideoPath,
    string? InitialOverlayText,
    PlayerDamageOverlayData? DamageOverlay)
{
    public static PlayerWindowPlaybackContext From(
        PlayerVideoPathInfo videoInfo,
        string? initialOverlayText,
        PlayerDamageOverlayData? damageOverlay)
    {
        ArgumentNullException.ThrowIfNull(videoInfo);

        return new PlayerWindowPlaybackContext(
            videoInfo.VideoPath,
            initialOverlayText,
            damageOverlay);
    }
}
