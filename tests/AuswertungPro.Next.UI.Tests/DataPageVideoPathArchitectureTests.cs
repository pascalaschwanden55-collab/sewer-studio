using System;
using System.IO;
using static AuswertungPro.Next.UI.Tests.SourceTextTestHelpers;

namespace AuswertungPro.Next.UI.Tests;

public sealed class DataPageVideoPathArchitectureTests
{
    [Fact]
    public void EnsureVideoPath_delegates_video_fallback_workflow_to_controller()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "AuswertungPro.Next.UI",
            "ViewModels",
            "Pages",
            "DataPageViewModel.cs"));

        var method = ExtractMethod(source, "private string? EnsureVideoPath(");

        Assert.Contains("DataPageVideoPathWorkflowController.Resolve(", method, StringComparison.Ordinal);
        Assert.DoesNotContain("new VideoSearchTool(", method, StringComparison.Ordinal);
        Assert.DoesNotContain("SelectFolder(\"Video-Ordner auswaehlen\"", method, StringComparison.Ordinal);
        Assert.DoesNotContain("OpenFile(\"Video auswaehlen\"", method, StringComparison.Ordinal);
        Assert.DoesNotContain("resManual.Message", method, StringComparison.Ordinal);
    }

}
