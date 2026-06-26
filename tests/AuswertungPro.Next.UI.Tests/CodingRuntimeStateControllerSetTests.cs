using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingRuntimeStateControllerSetTests
{
    [Fact]
    public void Set_exposes_stable_runtime_state_controllers()
    {
        var setType = typeof(CodingModeStateController).Assembly
            .GetType("AuswertungPro.Next.UI.Player.CodingRuntimeStateControllerSet");
        Assert.NotNull(setType);

        var set = Activator.CreateInstance(setType);
        Assert.NotNull(set);

        var modeState = Get<CodingModeStateController>(set, "ModeState");
        var sessionRuntimeOwner = Get<CodingSessionServiceOwner>(set, "SessionRuntimeOwner");
        var overlayRuntimeOwner = Get<CodingOverlayServiceOwner>(set, "OverlayRuntimeOwner");

        Assert.Same(modeState, Get<CodingModeStateController>(set, "ModeState"));
        Assert.Same(sessionRuntimeOwner, Get<CodingSessionServiceOwner>(set, "SessionRuntimeOwner"));
        Assert.Same(overlayRuntimeOwner, Get<CodingOverlayServiceOwner>(set, "OverlayRuntimeOwner"));
    }

    private static T Get<T>(object target, string propertyName)
        => (T)target.GetType()
            .GetProperty(propertyName)!
            .GetValue(target)!;
}
