using System;
using System.IO;
using static AuswertungPro.Next.UI.Tests.SourceTextTestHelpers;

namespace AuswertungPro.Next.UI.Tests;

public sealed class DataPageProtocolMediaLinkArchitectureTests
{
    [Fact]
    public void DataPage_delegates_protocol_media_link_logic_to_controller()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(root, "src", "AuswertungPro.Next.UI", "Views", "Pages", "DataPage.xaml.cs"));

        Assert.Contains("DataPageProtocolMediaLinkController.ResolveEntry", source, StringComparison.Ordinal);
        Assert.Contains("DataPageProtocolMediaLinkController.ResolveTargetTime", source, StringComparison.Ordinal);
        Assert.Contains("DataPageProtocolMediaLinkController.BuildOverlayText", source, StringComparison.Ordinal);
        Assert.DoesNotContain("private static TimeSpan? ParseMpegTime", source, StringComparison.Ordinal);
        Assert.DoesNotContain("private static string BuildOverlayText", source, StringComparison.Ordinal);
    }

}
