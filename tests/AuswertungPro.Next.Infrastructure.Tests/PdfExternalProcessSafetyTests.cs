namespace AuswertungPro.Next.Infrastructure.Tests;

using static TestRepoPaths;

public sealed class PdfExternalProcessSafetyTests
{
    [Fact]
    public void PdfExtractorsUseSharedAsyncTimeoutProcessRunner()
    {
        var textExtractor = File.ReadAllText(RepoFile("src", "AuswertungPro.Next.Infrastructure", "Import", "Pdf", "PdfTextExtractor.cs"));
        var ocrExtractor = File.ReadAllText(RepoFile("src", "AuswertungPro.Next.Infrastructure", "Import", "Pdf", "PdfOcrExtractor.cs"));

        Assert.Contains("ExternalProcessRunner.RunAsync", textExtractor);
        Assert.Contains("ExternalProcessRunner.RunAsync", ocrExtractor);
        AssertNoForbiddenTokens(textExtractor, ".ReadToEnd()", "WaitForExit()");
        AssertNoForbiddenTokens(ocrExtractor, ".ReadToEnd()", "WaitForExit(timeoutMs)");
    }

    private static void AssertNoForbiddenTokens(string source, params string[] forbiddenTokens)
    {
        var hits = forbiddenTokens
            .Where(token => source.Contains(token, StringComparison.Ordinal))
            .ToArray();

        Assert.True(hits.Length == 0, "Verbotene blockierende Prozess-APIs gefunden: " + string.Join(", ", hits));
    }
}
