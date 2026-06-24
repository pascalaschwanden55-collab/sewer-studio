using System.Reflection;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerCodingPlaybackTests
{
    [Fact]
    public void PauseForCodingInteraction_sets_pause_true()
    {
        var method = typeof(PlayerKeyboardActionController).Assembly
            .GetType("AuswertungPro.Next.UI.Player.PlayerCodingPlayback")
            ?.GetMethod(
                "PauseForCodingInteraction",
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
