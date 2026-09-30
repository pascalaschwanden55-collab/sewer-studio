using System.IO;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

public sealed class ExplorerRevealArchitectureTests
{
    [Fact]
    public void ExplorerRevealUsesCentralInstanceAndKeepsStaticFacadeThin()
    {
        var root = TestRepoPaths.FindRepositoryRoot();
        var provider = Read(root, "src", "AuswertungPro.Next.UI", "ServiceProvider.cs");
        var dataPage = Read(
            root,
            "src",
            "AuswertungPro.Next.UI",
            "ViewModels",
            "Pages",
            "DataPageViewModel.cs");
        var shaftPage = Read(
            root,
            "src",
            "AuswertungPro.Next.UI",
            "Views",
            "Pages",
            "SchaechtePage.xaml.cs");
        var shaftFileActions = Read(
            root,
            "src",
            "AuswertungPro.Next.UI",
            "DataPage",
            "SchaechteFileActionController.cs");
        var facade = Read(
            root,
            "src",
            "AuswertungPro.Next.UI",
            "Services",
            "ExplorerRevealService.cs");

        Assert.Contains("public IExplorerRevealService ExplorerReveal", provider);
        Assert.Contains("ExplorerReveal = new ExplorerRevealLauncher()", provider);
        Assert.Contains("_explorerReveal.TryReveal", dataPage);
        Assert.DoesNotContain("ExplorerRevealService.TryReveal", dataPage);
        Assert.Contains("viewModel.ExplorerReveal,", shaftPage);
        Assert.Contains("_explorerReveal.TryReveal", shaftFileActions);
        Assert.DoesNotContain("Vm.ExplorerReveal.TryReveal", shaftPage);
        Assert.DoesNotContain("ExplorerRevealService.TryReveal", shaftPage);
        Assert.DoesNotContain("ExplorerRevealService.TryReveal", shaftFileActions);
        Assert.DoesNotContain("File.Exists", facade);
        Assert.DoesNotContain("Directory.Exists", facade);
        Assert.DoesNotContain("Process.Start", facade);
    }

    private static string Read(string root, params string[] segments)
        => File.ReadAllText(Path.Combine([root, .. segments]));

}
