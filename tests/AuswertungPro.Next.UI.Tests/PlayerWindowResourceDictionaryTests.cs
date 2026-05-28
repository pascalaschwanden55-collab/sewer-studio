using System.IO;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerWindowResourceDictionaryTests
{
    [Fact]
    public void Player_window_keeps_theme_dependent_styles_in_window_scope()
    {
        var root = FindRepoRoot();
        var xamlPath = Path.Combine(root, "src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.xaml");
        var resourcesPath = Path.Combine(root, "src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.Resources.xaml");
        var csprojPath = Path.Combine(root, "src", "AuswertungPro.Next.UI", "AuswertungPro.Next.UI.csproj");

        var xaml = File.ReadAllText(xamlPath);
        var resources = File.ReadAllText(resourcesPath);
        var csproj = File.ReadAllText(csprojPath);

        Assert.Contains("<AssemblyName>SewerStudio</AssemblyName>", csproj);
        Assert.Contains(
            "Source=\"/SewerStudio;component/Views/Windows/PlayerWindow.Resources.xaml\"",
            xaml);
        Assert.Contains("x:Key=\"PlayerCard\"", xaml);
        Assert.Contains("BasedOn=\"{StaticResource Card}\"", xaml);
        Assert.Contains("x:Key=\"PlayerButton\"", xaml);
        Assert.Contains("BasedOn=\"{StaticResource ToolbarButton}\"", xaml);
        Assert.Contains("x:Key=\"PlayerPrimaryButton\"", xaml);
        Assert.Contains("BasedOn=\"{StaticResource ToolbarButtonAccent}\"", xaml);
        Assert.Contains("x:Key=\"MarkToolPopupButton\"", xaml);

        Assert.DoesNotContain("BasedOn=\"{StaticResource Card}\"", resources);
        Assert.DoesNotContain("BasedOn=\"{StaticResource ToolbarButton}\"", resources);
        Assert.DoesNotContain("BasedOn=\"{StaticResource ToolbarButtonAccent}\"", resources);
        Assert.DoesNotContain("x:Key=\"MarkToolPopupButton\"", resources);
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
