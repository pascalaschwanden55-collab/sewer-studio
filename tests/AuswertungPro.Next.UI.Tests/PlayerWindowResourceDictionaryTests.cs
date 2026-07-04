using System.IO;
using Xunit;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerWindowResourceDictionaryTests
{
    [Fact]
    public void Player_window_keeps_theme_dependent_styles_in_window_scope()
    {
        var root = FindRepoRoot();
        var xamlPath = Path.Combine(root, "src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.xaml");
        var csprojPath = Path.Combine(root, "src", "AuswertungPro.Next.UI", "AuswertungPro.Next.UI.csproj");

        var xaml = File.ReadAllText(xamlPath);
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
    }

}
