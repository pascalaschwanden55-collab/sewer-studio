using System.IO;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

public sealed class VideoAnalysisPipelineWindowLifecycleTests
{
    [Fact]
    public void Closed_bricht_ab_und_gibt_cancellation_source_danach_frei()
    {
        var source = File.ReadAllText(RepoFile(
            "src",
            "AuswertungPro.Next.UI",
            "Views",
            "Windows",
            "VideoAnalysisPipelineWindow.xaml.cs"));
        var closedStart = source.IndexOf("Closed += (_, __) =>", StringComparison.Ordinal);
        var closedEnd = source.IndexOf("        };", closedStart, StringComparison.Ordinal);
        var cancel = source.IndexOf("_cts.Cancel();", closedStart, StringComparison.Ordinal);
        var dispose = source.IndexOf("_cts.Dispose();", closedStart, StringComparison.Ordinal);

        Assert.True(closedStart >= 0 && closedEnd > closedStart);
        Assert.InRange(cancel, closedStart, closedEnd);
        Assert.InRange(dispose, cancel + 1, closedEnd);
    }
}
