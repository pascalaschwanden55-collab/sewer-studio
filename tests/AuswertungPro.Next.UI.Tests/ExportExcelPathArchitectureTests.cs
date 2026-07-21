using System.IO;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

public sealed class ExportExcelPathArchitectureTests
{
    [Fact]
    public void Excel_path_policy_is_pure_and_viewmodel_keeps_filesystem_boundary()
    {
        var viewModel = File.ReadAllText(RepoFile(
            "src",
            "AuswertungPro.Next.UI",
            "ViewModels",
            "Pages",
            "ExportPageViewModel.cs"));
        var policy = File.ReadAllText(RepoFile(
            "src",
            "AuswertungPro.Next.UI",
            "ViewModels",
            "Pages",
            "ExportExcelPathPolicy.cs"));

        Assert.Contains("ExportExcelPathPolicy.BuildConfiguredPath(", viewModel);
        Assert.Contains("ExportExcelPathPolicy.BuildFixedPath(", viewModel);
        Assert.Contains("ExportExcelPathPolicy.BuildCollisionSafePath(", viewModel);
        Assert.Contains("Directory.CreateDirectory(ordner);", viewModel);

        Assert.DoesNotContain("Directory.CreateDirectory", policy);
        Assert.DoesNotContain("IDialogService", policy);
        Assert.DoesNotContain("SaveFile", policy);
    }
}
