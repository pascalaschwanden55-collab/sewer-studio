using System.IO;
using static AuswertungPro.Next.UI.Tests.SourceTextTestHelpers;

namespace AuswertungPro.Next.UI.Tests;

public sealed class ProjektEroeffnungMainWindowGuardTests
{
    private static string Xaml()
        => File.ReadAllText(RepoFile(
            "src", "AuswertungPro.Next.UI", "MainWindow.xaml"));

    [Fact]
    public void Menu_collapses_outside_workspace()
    {
        var xaml = Xaml();
        // Menue + Sidebar binden an IsMenuVisible (zusaetzlich zur IsFocusMode-Logik).
        Assert.Contains("IsMenuVisible", xaml);
    }

    [Fact]
    public void Header_has_switch_project_button()
    {
        var xaml = Xaml();
        Assert.Contains("SwitchProjectCommand", xaml);
        Assert.Contains("Projekt wechseln", xaml);
    }
}
