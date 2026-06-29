using System.IO;

namespace AuswertungPro.Next.UI.Tests;

public sealed class ProjektEroeffnungProjectPageGuardTests
{
    private static string Vm()
        => File.ReadAllText(ProjektEroeffnungShellGuardTests.RepoFile(
            "src", "AuswertungPro.Next.UI", "ViewModels", "Pages", "ProjectPageViewModel.cs"));

    private static string Xaml()
        => File.ReadAllText(ProjektEroeffnungShellGuardTests.RepoFile(
            "src", "AuswertungPro.Next.UI", "Views", "Pages", "ProjectPage.xaml"));

    [Fact]
    public void Vm_has_draftname_and_anlegen_command()
    {
        var vm = Vm();
        Assert.Contains("DraftName", vm);
        Assert.Contains("AnlegenCommand", vm);
        Assert.Contains("AbbrechenCommand", vm);
        Assert.Contains("CreateProjectFromDraft", vm);
        Assert.Contains("public bool IsDraft", vm);
    }

    [Fact]
    public void Xaml_drops_new_and_open_buttons_and_adds_anlegen()
    {
        var xaml = Xaml();
        Assert.DoesNotContain("Content=\"Neues Projekt\"", xaml);
        Assert.DoesNotContain("Content=\"Öffnen\"", xaml);
        Assert.Contains("Content=\"Projekt anlegen\"", xaml);
        Assert.Contains("AnlegenCommand", xaml);
        Assert.Contains("Content=\"Abbrechen\"", xaml);
        Assert.Contains("AbbrechenCommand", xaml);
        Assert.Contains("Content=\"Projekt speichern\" Command=\"{Binding SaveCommand}\"", xaml);
        Assert.Contains("Visibility=\"{Binding IsNotDraft, Converter={StaticResource BoolToVis}}\"", xaml);
    }
}
