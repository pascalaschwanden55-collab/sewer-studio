namespace AuswertungPro.Next.Infrastructure.Tests;

using static TestRepoPaths;

public sealed class VsaKekArchiveReaderProcessSafetyTests
{
    [Fact]
    public void ArchiveReaderUsesSharedTimeoutProcessRunner()
    {
        var source = File.ReadAllText(RepoFile("src", "AuswertungPro.Next.Application", "Protocol", "VsaKekCatalogBuilder.cs"));
        var readerStart = source.IndexOf("public static class VsaKekCatalogArchiveReader", StringComparison.Ordinal);
        Assert.True(readerStart >= 0);
        var readerSource = source[readerStart..];

        Assert.Contains("ExternalProcessRunner.RunAsync", readerSource);
        AssertNoForbiddenTokens(readerSource, ".ReadToEnd()", "WaitForExit()");
    }

    private static void AssertNoForbiddenTokens(string source, params string[] forbiddenTokens)
    {
        var hits = forbiddenTokens
            .Where(token => source.Contains(token, StringComparison.Ordinal))
            .ToArray();

        Assert.True(hits.Length == 0, "Verbotene blockierende Prozess-APIs gefunden: " + string.Join(", ", hits));
    }
}
