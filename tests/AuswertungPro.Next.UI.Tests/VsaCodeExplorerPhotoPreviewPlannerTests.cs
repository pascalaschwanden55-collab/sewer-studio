using System.Reflection;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class VsaCodeExplorerPhotoPreviewPlannerTests
{
    [Fact]
    public void Plan_zeigt_nur_existierende_ersten_zwei_fotos()
    {
        var preview = Plan(
            ["foto1.png", "missing.png", "foto3.png"],
            path => path == "foto1.png");

        Assert.Equal("foto1.png", Get<string?>(preview, "Photo1Path"));
        Assert.False(Get<bool>(preview, "ShowPhoto1Placeholder"));
        Assert.Null(Get<string?>(preview, "Photo2Path"));
        Assert.True(Get<bool>(preview, "ShowPhoto2Placeholder"));
    }

    [Fact]
    public void Plan_zeigt_placeholder_wenn_foto_leer_oder_nicht_vorhanden_ist()
    {
        var preview = Plan(
            ["", "foto2.png"],
            _ => false);

        Assert.Null(Get<string?>(preview, "Photo1Path"));
        Assert.True(Get<bool>(preview, "ShowPhoto1Placeholder"));
        Assert.Null(Get<string?>(preview, "Photo2Path"));
        Assert.True(Get<bool>(preview, "ShowPhoto2Placeholder"));
    }

    private static object Plan(IReadOnlyList<string> photoPaths, Func<string, bool> fileExists)
    {
        var type = typeof(VsaCodeExplorerDispatchWorkflow).Assembly.GetType(
            "AuswertungPro.Next.UI.Ai.VsaCodeExplorerPhotoPreviewPlanner");
        Assert.NotNull(type);
        var method = type
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(m => m.Name == "Plan" && m.GetParameters().Length == 2);
        Assert.NotNull(method);
        return method.Invoke(null, [photoPaths, fileExists])!;
    }

    private static T Get<T>(object source, string propertyName)
    {
        var property = source.GetType().GetProperty(propertyName);
        Assert.NotNull(property);
        return (T)property.GetValue(source)!;
    }
}
