namespace AuswertungPro.Next.Infrastructure.Tests.Map;

using static AuswertungPro.Next.Infrastructure.Tests.TestRepoPaths;

public sealed class XtfStreamingReaderSecurityTests
{
    [Fact]
    public void StreamingXtfReadersExplicitlyDisableDtdAndExternalResolution()
    {
        var network = File.ReadAllText(RepoFile("src", "AuswertungPro.Next.Infrastructure", "Map", "XtfNetworkExtractor.cs"));
        var manhole = File.ReadAllText(RepoFile("src", "AuswertungPro.Next.Infrastructure", "Map", "XtfManholeExtractor.cs"));

        Assert.Contains("DtdProcessing = DtdProcessing.Prohibit", network);
        Assert.Contains("XmlResolver = null", network);
        Assert.Contains("DtdProcessing = DtdProcessing.Prohibit", manhole);
        Assert.Contains("XmlResolver = null", manhole);
    }
}
