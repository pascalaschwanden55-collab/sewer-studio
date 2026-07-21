using System.IO;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

public sealed class SchaechteFileActionArchitectureTests
{
    [Fact]
    public void SchaechtePage_delegates_file_actions_to_scoped_controller()
    {
        var page = File.ReadAllText(RepoFile(
            "src",
            "AuswertungPro.Next.UI",
            "Views",
            "Pages",
            "SchaechtePage.xaml.cs"));
        var controller = File.ReadAllText(RepoFile(
            "src",
            "AuswertungPro.Next.UI",
            "DataPage",
            "SchaechteFileActionController.cs"));
        var xaml = File.ReadAllText(RepoFile(
            "src",
            "AuswertungPro.Next.UI",
            "Views",
            "Pages",
            "SchaechtePage.xaml"));
        var compactPage = string.Concat(page.Where(character => !char.IsWhiteSpace(character)));

        Assert.Contains(
            "privatestaticSchaechteFileActionControllerCreateFileActionController(" +
            "SchaechtePageViewModelviewModel)=>new(" +
            "viewModel.SchachtFileTargets,viewModel.ShellOpen," +
            "viewModel.ExplorerReveal,viewModel.Dialogs);",
            compactPage);
        Assert.Contains(
            "CreateFileActionController(vm).OpenProtocol(" +
            "vm.Selected,vm.Settings.LastProjectPath);",
            compactPage);
        Assert.Contains(
            "CreateFileActionController(vm).RevealContainingFolder(" +
            "vm.Selected,vm.Settings.LastProjectPath);",
            compactPage);
        Assert.DoesNotContain("SchaechteFileActionController? _fileActionController", page);
        Assert.DoesNotContain("_fileActionController =", page);
        Assert.DoesNotContain("private string? ResolvePdfPath(", page);
        Assert.DoesNotContain("private string? ResolveExplorerTarget(", page);
        Assert.DoesNotContain("Vm.ShellOpen.TryOpen(", page);
        Assert.DoesNotContain("_vm.ShellOpen.TryOpen(", page);
        Assert.DoesNotContain("SafeShellOpen.TryOpen(", page);
        Assert.DoesNotContain("Vm.ExplorerReveal.TryReveal(", page);
        Assert.DoesNotContain("_vm.ExplorerReveal.TryReveal(", page);
        Assert.DoesNotContain("ExplorerRevealService.TryReveal(", page);
        Assert.DoesNotContain("_vm.SchachtFileTargets.ResolvePdfPath(", page);
        Assert.DoesNotContain("_vm.SchachtFileTargets.ResolveExplorerTarget(", page);
        Assert.Contains("Click=\"ProtokollMenu_Click\"", xaml);
        Assert.Contains("Click=\"OpenContainingFolderMenu_Click\"", xaml);
        Assert.Contains(
            "case\"openpdf\":ProtokollMenu_Click(this,e);break;",
            compactPage);
        Assert.Contains(
            "case\"openfolder\":OpenContainingFolderMenu_Click(this,e);break;",
            compactPage);

        Assert.Contains("_fileTargets.ResolvePdfPath(record, projectFilePath)", controller);
        Assert.Contains("_fileTargets.ResolveExplorerTarget(record, projectFilePath)", controller);
        Assert.Contains("_shellOpen.TryOpen(pdfPath, out var error)", controller);
        Assert.Contains("_explorerReveal.TryReveal(target, out var error)", controller);
        Assert.DoesNotContain("ServiceProvider", controller);
        Assert.DoesNotContain("System.Windows", controller);
    }
}
