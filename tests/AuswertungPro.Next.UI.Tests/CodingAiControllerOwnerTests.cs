using System.Reflection;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingAiControllerOwnerTests
{
    [Fact]
    public void Owner_exposes_stable_coding_ai_controller()
    {
        var owner = CreateOwner();

        var controller = Get<CodingAiController>(owner, "Controller");

        Assert.NotNull(controller);
        Assert.Same(controller, Get<CodingAiController>(owner, "Controller"));
    }

    private static object CreateOwner()
    {
        var ownerType = typeof(CodingAiController).Assembly
            .GetType("AuswertungPro.Next.UI.Player.CodingAiControllerOwner");
        Assert.NotNull(ownerType);

        var constructor = ownerType.GetConstructor(Type.EmptyTypes);
        Assert.NotNull(constructor);

        return constructor.Invoke([]);
    }

    private static T Get<T>(object target, string propertyName)
        => (T)target.GetType()
            .GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public)!
            .GetValue(target)!;
}
