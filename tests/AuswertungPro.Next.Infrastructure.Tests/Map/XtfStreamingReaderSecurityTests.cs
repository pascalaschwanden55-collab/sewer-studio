namespace AuswertungPro.Next.Infrastructure.Tests.Map;

public sealed class XtfStreamingReaderSecurityTests
{
    [Fact]
    public void StreamingXtfReadersExplicitlyDisableDtdAndExternalResolution()
    {
        var network = File.ReadAllText(FindRepoFile("src", "AuswertungPro.Next.Infrastructure", "Map", "XtfNetworkExtractor.cs"));
        var manhole = File.ReadAllText(FindRepoFile("src", "AuswertungPro.Next.Infrastructure", "Map", "XtfManholeExtractor.cs"));

        Assert.Contains("DtdProcessing = DtdProcessing.Prohibit", network);
        Assert.Contains("XmlResolver = null", network);
        Assert.Contains("DtdProcessing = DtdProcessing.Prohibit", manhole);
        Assert.Contains("XmlResolver = null", manhole);
    }

    private static string FindRepoFile(params string[] relativeParts)
    {
        foreach (var start in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory(), Path.GetDirectoryName(SourceFilePath())! }.Distinct())
        {
            var dir = new DirectoryInfo(start);
            while (dir is not null)
            {
                var candidate = Path.Combine(new[] { dir.FullName }.Concat(relativeParts).ToArray());
                if (File.Exists(candidate))
                    return candidate;
                dir = dir.Parent;
            }
        }

        throw new FileNotFoundException("Repo-Datei nicht gefunden.", Path.Combine(relativeParts));
    }

    private static string SourceFilePath([System.Runtime.CompilerServices.CallerFilePath] string sourceFilePath = "")
        => sourceFilePath;
}
