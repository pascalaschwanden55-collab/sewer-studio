namespace AuswertungPro.Next.Infrastructure.Tests;

using static TestRepoPaths;

public sealed class GpuModelSelectorProcessSafetyTests
{
    [Fact]
    public void GpuModelSelectorUsesSharedTimeoutProcessRunner()
    {
        var source = File.ReadAllText(RepoFile("src", "AuswertungPro.Next.Infrastructure", "Ai", "Ollama", "GpuModelSelector.cs"));

        Assert.Contains("ExternalProcessRunner.RunAsync", source);
        AssertNoForbiddenTokens(source, ".ReadToEnd()", "WaitForExit(");
    }

    private static void AssertNoForbiddenTokens(string source, params string[] forbiddenTokens)
    {
        var hits = forbiddenTokens
            .Where(token => source.Contains(token, StringComparison.Ordinal))
            .ToArray();

        Assert.True(hits.Length == 0, "Verbotene blockierende Prozess-APIs gefunden: " + string.Join(", ", hits));
    }
}
