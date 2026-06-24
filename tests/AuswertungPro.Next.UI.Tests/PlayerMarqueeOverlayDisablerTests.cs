using System.Reflection;
using AuswertungPro.Next.UI.Player;
using LibVLCSharp.Shared;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerMarqueeOverlayDisablerTests
{
    [Fact]
    public void Disable_sets_marquee_enable_to_policy_disabled_value()
    {
        var method = FindDisableMethod();
        Assert.NotNull(method);
        VideoMarqueeOption? option = null;
        int? value = null;

        method.Invoke(null, [
            new Action<VideoMarqueeOption, int>((capturedOption, capturedValue) =>
            {
                option = capturedOption;
                value = capturedValue;
            })
        ]);

        Assert.Equal(VideoMarqueeOption.Enable, option);
        Assert.Equal(PlayerMarqueeOverlayPolicy.DisabledEnable, value);
    }

    [Fact]
    public void Disable_swallows_marquee_errors()
    {
        var method = FindDisableMethod();
        Assert.NotNull(method);

        method.Invoke(null, [
            new Action<VideoMarqueeOption, int>((_, _) => throw new InvalidOperationException("marquee failed"))
        ]);
    }

    private static MethodInfo? FindDisableMethod()
        => typeof(PlayerMarqueeOverlayPolicy).Assembly
            .GetType("AuswertungPro.Next.UI.Player.PlayerMarqueeOverlayDisabler")
            ?.GetMethod(
                "Disable",
                BindingFlags.Public | BindingFlags.Static,
                binder: null,
                types: [typeof(Action<VideoMarqueeOption, int>)],
                modifiers: null);
}
