using System.Reflection;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerManualMarkPlaybackTests
{
    [Fact]
    public void PauseForManualMarking_sets_pause_true()
    {
        var method = typeof(PlayerKeyboardActionController).Assembly
            .GetType("AuswertungPro.Next.UI.Player.PlayerManualMarkPlayback")
            ?.GetMethod(
                "PauseForManualMarking",
                BindingFlags.Public | BindingFlags.Static,
                binder: null,
                types: [typeof(Action<bool>)],
                modifiers: null);
        Assert.NotNull(method);
        bool? pauseValue = null;

        method.Invoke(null, [new Action<bool>(pause => pauseValue = pause)]);

        Assert.True(pauseValue);
    }
}
