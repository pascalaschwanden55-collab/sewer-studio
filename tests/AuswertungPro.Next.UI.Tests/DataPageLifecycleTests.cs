using System.IO;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

public sealed class DataPageLifecycleTests
{
    [Fact]
    public void Unloaded_stoppt_such_und_layout_timer()
    {
        var source = File.ReadAllText(RepoFile(
            "src",
            "AuswertungPro.Next.UI",
            "Views",
            "Pages",
            "DataPage.xaml.cs"));
        var unloadedStart = source.IndexOf("Unloaded += (_, __) =>", StringComparison.Ordinal);
        var unloadedEnd = source.IndexOf("        };", unloadedStart, StringComparison.Ordinal);
        var unloadedBlock = source[unloadedStart..unloadedEnd];

        Assert.Contains("_searchDebounceTimer.Stop();", unloadedBlock);
        Assert.Contains("_layoutSaveDebounceTimer.Stop();", unloadedBlock);
    }
}
