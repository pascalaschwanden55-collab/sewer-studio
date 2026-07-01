using System;
using System.IO;
using static AuswertungPro.Next.UI.Tests.SourceTextTestHelpers;

namespace AuswertungPro.Next.UI.Tests;

public sealed class DataPageVideoAnalysisArchitectureTests
{
    [Fact]
    public void DataPageViewModel_delegiert_videoanalyse_an_controller()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(root, "src", "AuswertungPro.Next.UI", "ViewModels", "Pages", "DataPageViewModel.cs"));

        Assert.Contains("private readonly DataPageVideoAnalysisController _videoAnalysisController;", source, StringComparison.Ordinal);
        AssertDelegates(source, "private void OpenVideoAiPipeline(HaltungRecord? record)", "_videoAnalysisController.Open(record);");
        AssertDelegates(source, "public LiveControl.LiveControlRetryResult TryStartVideoAiPipelineByName(string haltungsname)", "_videoAnalysisController.TryStartByName(haltungsname);");

        var openMethod = ExtractMethod(source, "private void OpenVideoAiPipeline(HaltungRecord? record)");
        Assert.DoesNotContain("new VideoAnalysisPipelineWindow", openMethod, StringComparison.Ordinal);
        Assert.DoesNotContain("ProtocolReplacementService.PrepareReplacement", openMethod, StringComparison.Ordinal);
        Assert.DoesNotContain("PipelineReachLengthParser.TryParse", openMethod, StringComparison.Ordinal);
        Assert.DoesNotContain("new HttpClient", openMethod, StringComparison.Ordinal);
    }

    private static void AssertDelegates(string source, string marker, string call)
    {
        var method = ExtractMethod(source, marker);
        Assert.Contains(call, method, StringComparison.Ordinal);
    }

}
