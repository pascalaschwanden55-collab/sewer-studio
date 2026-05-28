using System.IO;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerWindowResourceDictionaryTests
{
    [Fact]
    public void Player_window_uses_direct_resource_dictionary_with_assembly_pack_uri()
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
        Assert.DoesNotContain("ResourceDictionary.MergedDictionaries", xaml);
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
