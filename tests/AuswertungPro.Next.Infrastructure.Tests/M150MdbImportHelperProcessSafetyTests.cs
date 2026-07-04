namespace AuswertungPro.Next.Infrastructure.Tests;

using static TestRepoPaths;

public sealed class M150MdbImportHelperProcessSafetyTests
{
    [Fact]
    public void M150MdbImportHelperUsesSharedTimeoutProcessRunner()
    {
        var source = File.ReadAllText(RepoFile("src", "AuswertungPro.Next.Infrastructure", "Import", "Xtf", "M150MdbImportHelper.cs"));

        Assert.Contains("ExternalProcessRunner.RunAsync", source);
        AssertNoForbiddenTokens(source, "WaitForExit(");
    }

    private static void AssertNoForbiddenTokens(string source, params string[] forbiddenTokens)
    {
        var hits = forbiddenTokens
            .Where(token => source.Contains(token, StringComparison.Ordinal))
            .ToArray();

        Assert.True(hits.Length == 0, "Verbotene blockierende Prozess-APIs gefunden: " + string.Join(", ", hits));
    }
}
