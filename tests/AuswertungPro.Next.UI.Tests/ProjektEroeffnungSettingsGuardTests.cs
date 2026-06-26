using System.IO;

namespace AuswertungPro.Next.UI.Tests;

public sealed class ProjektEroeffnungSettingsGuardTests
{
    private static string Vm()
        => File.ReadAllText(ProjektEroeffnungShellGuardTests.RepoFile(
            "src", "AuswertungPro.Next.UI", "ViewModels", "Pages", "SettingsPageViewModel.cs"));

    private static string Xaml()
        => File.ReadAllText(ProjektEroeffnungShellGuardTests.RepoFile(
            "src", "AuswertungPro.Next.UI", "Views", "Pages", "SettingsPage.xaml"));

    [Fact]
    public void Vm_exposes_and_persists_projects_root()
    {
        var vm = Vm();
        Assert.Contains("ProjectsRootDirectory", vm);
        Assert.Contains("BrowseProjectsRootCommand", vm);
    }

    [Fact]
    public void Xaml_has_projects_root_field()
    {
        var xaml = Xaml();
        Assert.Contains("Projekte-Verzeichnis", xaml);
        Assert.Contains("BrowseProjectsRootCommand", xaml);
    }
}
