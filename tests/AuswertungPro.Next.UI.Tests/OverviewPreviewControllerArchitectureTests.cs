using System.IO;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

public sealed class OverviewPreviewControllerArchitectureTests
{
    [Fact]
    public void Overview_preview_lifecycle_lives_in_wpf_free_controller()
    {
        var viewModel = File.ReadAllText(RepoFile(
            "src",
            "AuswertungPro.Next.UI",
            "ViewModels",
            "Pages",
            "OverviewPageViewModel.cs"));
        var controller = File.ReadAllText(RepoFile(
            "src",
            "AuswertungPro.Next.UI",
            "ViewModels",
            "Pages",
            "OverviewPreviewLoadController.cs"));

        Assert.Contains("private readonly OverviewPreviewLoadController _previewController;", viewModel);
        Assert.Contains("_previewController.Update(", viewModel);
        Assert.Contains("_previewController.Dispose();", viewModel);
        Assert.Contains("private ProjectPreview BuildPreviewCore(OverviewPreviewRequest request", viewModel);
        Assert.DoesNotContain("_previewRefreshTimer", viewModel);
        Assert.DoesNotContain("_previewCts", viewModel);
        Assert.DoesNotContain("_pendingPreviewEntry", viewModel);
        Assert.DoesNotContain("_previewLoadingPath", viewModel);
        Assert.DoesNotContain("_previewedPath", viewModel);

        Assert.DoesNotContain("System.Windows", controller);
        Assert.DoesNotContain("System.Windows.Threading", controller);
        Assert.DoesNotContain("DispatcherTimer", controller);
        Assert.Contains("TimeSpan.FromMilliseconds(200)", controller);
        Assert.Contains("_activeRunId", controller);
        Assert.Contains("SamePath(_latestRequest?.Path, request.Path)", controller);
    }
}
