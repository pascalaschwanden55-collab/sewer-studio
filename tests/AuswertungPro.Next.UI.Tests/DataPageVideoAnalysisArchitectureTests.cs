using System;
using System.IO;

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

    private static string ExtractMethod(string source, string marker)
    {
        var markerIndex = source.IndexOf(marker, StringComparison.Ordinal);
        if (markerIndex < 0)
            throw new InvalidOperationException($"Method marker not found: {marker}");

        var openBrace = source.IndexOf('{', markerIndex);
        if (openBrace < 0)
            throw new InvalidOperationException($"Method has no body: {marker}");

        var depth = 0;
        for (var i = openBrace; i < source.Length; i++)
        {
            if (source[i] == '{')
            {
                depth++;
                continue;
            }

            if (source[i] != '}')
                continue;

            depth--;
            if (depth == 0)
                return source.Substring(markerIndex, i - markerIndex + 1);
        }

        throw new InvalidOperationException($"Method body is incomplete: {marker}");
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AuswertungPro.sln")))
                return directory.FullName;

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Repository root not found.");
    }
}
