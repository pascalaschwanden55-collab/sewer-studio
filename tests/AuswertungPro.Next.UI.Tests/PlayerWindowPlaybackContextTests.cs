using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerWindowPlaybackContextTests
{
    [Fact]
    public void From_stores_video_path_initial_overlay_and_damage_overlay()
    {
        var damageOverlay = new PlayerDamageOverlayData(12.5, []);
        var videoInfo = new PlayerVideoPathInfo("C:\\videos\\demo.mp4", "demo.mp4");

        var context = PlayerWindowPlaybackContext.From(
            videoInfo,
            initialOverlayText: "Bereit",
            damageOverlay);

        Assert.Equal("C:\\videos\\demo.mp4", context.VideoPath);
        Assert.Equal("Bereit", context.InitialOverlayText);
        Assert.Same(damageOverlay, context.DamageOverlay);
    }

    [Fact]
    public void From_throws_for_null_video_info()
    {
        Assert.Throws<ArgumentNullException>(() => PlayerWindowPlaybackContext.From(null!, null, null));
    }
}
