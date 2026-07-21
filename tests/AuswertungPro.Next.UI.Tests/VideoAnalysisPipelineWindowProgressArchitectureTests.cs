using System.IO;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

public sealed class VideoAnalysisPipelineWindowProgressArchitectureTests
{
    [Fact]
    public void Fenster_delegiert_Fortschrittsabbildung_an_Mapper()
    {
        var window = File.ReadAllText(RepoFile(
            "src",
            "AuswertungPro.Next.UI",
            "Views",
            "Windows",
            "VideoAnalysisPipelineWindow.xaml.cs"));
        var callbackStart = window.IndexOf("new Progress<PipelineProgress>", StringComparison.Ordinal);
        var callbackEnd = window.IndexOf("var result = await _pipeline.RunAsync", callbackStart, StringComparison.Ordinal);

        Assert.True(callbackStart >= 0 && callbackEnd > callbackStart);
        var callback = window[callbackStart..callbackEnd];
        Assert.Contains("progressMapper.Apply(p)", callback);
        Assert.DoesNotContain("StatusParser", callback);
        Assert.DoesNotContain("SummaryBuilder", callback);
        Assert.DoesNotContain("FramesAnalyzed =", callback);
        Assert.DoesNotContain("LiveFindings", callback);
    }
}
