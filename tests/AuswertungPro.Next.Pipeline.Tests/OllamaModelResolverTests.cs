using AuswertungPro.Next.UI.Ai.Ollama;

namespace AuswertungPro.Next.Pipeline.Tests;

public sealed class OllamaModelResolverTests
{
    [Fact]
    public void ResolveBestInstalledModel_ReturnsExactMatch_WhenPresent()
    {
        var installed = new[] { "qwen2.5vl:3b", "qwen2.5vl:7b" };

        var resolved = OllamaModelResolver.ResolveBestInstalledModel("qwen2.5vl:3b", installed);

        Assert.Equal("qwen2.5vl:3b", resolved);
    }

    [Fact]
    public void ResolveBestInstalledModel_FallsBackToSameFamily_WhenExactModelMissing()
    {
        var installed = new[] { "qwen2.5vl:7b", "qwen2.5:7b" };

        var resolved = OllamaModelResolver.ResolveBestInstalledModel("qwen2.5vl:3b", installed);

        Assert.Equal("qwen2.5vl:7b", resolved);
    }

    [Fact]
    public void ResolveBestInstalledModel_PrefersSmallestFamilyVariant()
    {
        var installed = new[] { "qwen2.5:14b", "qwen2.5:7b", "qwen2.5:3b" };

        var resolved = OllamaModelResolver.ResolveBestInstalledModel("qwen2.5:1.5b", installed);

        Assert.Equal("qwen2.5:3b", resolved);
    }

    [Fact]
    public void ClampNumCtxForVideoAnalysis_CapsHighValues()
    {
        Assert.Equal(2048, OllamaModelResolver.ClampNumCtxForVideoAnalysis(8192));
        Assert.Equal(2048, OllamaModelResolver.ClampNumCtxForVideoAnalysis(0));
        Assert.Equal(1024, OllamaModelResolver.ClampNumCtxForVideoAnalysis(1024));
    }
}
