using System.IO;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

public sealed class DesignAuditThemeResourceTests
{
    [Fact]
    public void VideoAnalysisPipelineWindow_uses_theme_resources_for_surface_and_text_colors()
    {
        var xaml = ReadUiFile("Views", "Windows", "VideoAnalysisPipelineWindow.xaml");

        Assert.DoesNotContain("Background=\"#0C1019\"", xaml);
        AssertDoesNotContainAny(xaml,
            "#0C1019",
            "#131825",
            "#1A2030",
            "#243049",
            "#F0F4FA",
            "#7B8DA6",
            "#94A3B8",
            "#60A5FA");
    }

    [Fact]
    public void TrainingCenterWindow_uses_theme_resources_for_slate_surfaces_and_text()
    {
        var xaml = ReadUiFile("Views", "Windows", "TrainingCenterWindow.xaml");

        AssertDoesNotContainAny(xaml,
            "#1E293B",
            "#0F172A",
            "#94A3B8",
            "#64748B");
    }

    [Fact]
    public void CorrectionDialog_uses_theme_resources_and_does_not_shadow_button_styles()
    {
        var xaml = ReadUiFile("Views", "Windows", "CorrectionDialog.xaml");
        var themeLight = ReadUiFile("Theme", "ThemeLight.xaml");
        var themeDark = ReadUiFile("Theme", "Theme.xaml");

        Assert.DoesNotContain("x:Key=\"PrimaryButton\"", xaml);
        Assert.DoesNotContain("x:Key=\"SecondaryButton\"", xaml);
        Assert.Contains("x:Key=\"SuccessButton\"", themeLight);
        Assert.Contains("x:Key=\"SuccessButton\"", themeDark);
        Assert.Contains("Style=\"{StaticResource SuccessButton}\"", xaml);
        AssertDoesNotContainAny(xaml,
            "#0D1117",
            "#161B22",
            "#21262D",
            "#30363D",
            "#E6EDF3",
            "#8B949E",
            "#484F58",
            "#58A6FF",
            "#238636",
            "#2EA043");
    }

    [Fact]
    public void DossierPrintDialog_uses_theme_resources_for_surface_and_text_colors()
    {
        var xaml = ReadUiFile("Views", "Windows", "DossierPrintDialog.xaml");

        Assert.Contains("Background=\"{DynamicResource BgBrush}\"", xaml);
        Assert.Contains("Style=\"{StaticResource SecondaryButton}\"", xaml);
        Assert.Contains("Style=\"{StaticResource SuccessButton}\"", xaml);
        AssertDoesNotContainAny(xaml,
            "#FF0D1117",
            "#E6EDF3",
            "#21262D",
            "#30363D",
            "#58A6FF",
            "#1A3A5C",
            "#8B949E",
            "#C9D1D9",
            "#238636",
            "#2EA043");
    }

    [Fact]
    public void HydraulikPrintDialog_uses_theme_resources_for_surface_and_text_colors()
    {
        var xaml = ReadUiFile("Views", "Windows", "HydraulikPrintDialog.xaml");

        Assert.Contains("Background=\"{DynamicResource BgBrush}\"", xaml);
        Assert.Contains("Style=\"{StaticResource SecondaryButton}\"", xaml);
        Assert.Contains("Style=\"{StaticResource SuccessButton}\"", xaml);
        AssertDoesNotContainAny(xaml,
            "#FF0D1117",
            "#E6EDF3",
            "#21262D",
            "#30363D",
            "#58A6FF",
            "#1A3A5C",
            "#C9D1D9",
            "#238636",
            "#2EA043");
    }

    private static void AssertDoesNotContainAny(string text, params string[] forbidden)
    {
        foreach (var value in forbidden)
            Assert.DoesNotContain(value, text);
    }

    private static string ReadUiFile(params string[] relativeParts)
    {
        var root = FindRepoRoot();
        var path = Path.Combine(new[] { root, "src", "AuswertungPro.Next.UI" }.Concat(relativeParts).ToArray());
        return File.ReadAllText(path);
    }

    private static string FindRepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "AuswertungPro.sln")))
                return current.FullName;

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Repo root with AuswertungPro.sln was not found.");
    }
}
