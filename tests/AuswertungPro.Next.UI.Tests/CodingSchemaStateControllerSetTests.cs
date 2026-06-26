using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingSchemaStateControllerSetTests
{
    [Fact]
    public void Set_exposes_stable_schema_state_controllers()
    {
        var setType = typeof(CodingSchemaTypeStateController).Assembly
            .GetType("AuswertungPro.Next.UI.Player.CodingSchemaStateControllerSet");
        Assert.NotNull(setType);

        var set = Activator.CreateInstance(setType);
        Assert.NotNull(set);

        var overlayManagerOwner = Get<CodingSchemaOverlayManagerOwner>(set, "OverlayManagerOwner");
        var typeState = Get<CodingSchemaTypeStateController>(set, "TypeState");

        Assert.Same(overlayManagerOwner, Get<CodingSchemaOverlayManagerOwner>(set, "OverlayManagerOwner"));
        Assert.Same(typeState, Get<CodingSchemaTypeStateController>(set, "TypeState"));
    }

    private static T Get<T>(object target, string propertyName)
        => (T)target.GetType()
            .GetProperty(propertyName)!
            .GetValue(target)!;
}
