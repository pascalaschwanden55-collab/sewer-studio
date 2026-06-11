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
