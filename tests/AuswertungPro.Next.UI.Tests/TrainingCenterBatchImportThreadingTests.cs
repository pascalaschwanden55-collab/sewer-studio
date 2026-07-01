using System.IO;
using Xunit;
using static AuswertungPro.Next.UI.Tests.SourceTextTestHelpers;

namespace AuswertungPro.Next.UI.Tests;

public sealed class TrainingCenterBatchImportThreadingTests
{
    [Fact]
    public void BatchImportAndIndexAsync_uses_central_ui_dispatcher_helper()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepoRoot(),
            "src",
            "AuswertungPro.Next.UI",
            "ViewModels",
            "Windows",
            "TrainingCenterViewModel.cs"));
        var batchImportSource = ExtractMethodBody(source, "private async Task BatchImportAndIndexAsync()");

        Assert.Contains("TrainingBatchImportCaseWorkflowController.ProcessAsync(", batchImportSource);
        Assert.Contains("OnUi,", batchImportSource);
        Assert.DoesNotContain("System.Windows.Application.Current?.Dispatcher", batchImportSource);
    }

}
