using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class VisionModelSelectionPolicyTests
{
    [Fact]
    public void Select_keeps_configured_model_when_exact_model_exists()
    {
        var selected = VisionModelSelectionPolicy.Select(
            "qwen3-vl:8b",
            new[] { "llama3", "qwen3-vl:8b" });

        Assert.Equal("qwen3-vl:8b", selected);
    }

    [Fact]
    public void Select_keeps_configured_model_when_available_model_starts_with_configured_name()
    {
        var selected = VisionModelSelectionPolicy.Select(
            "qwen3-vl:8b",
            new[] { "qwen3-vl:8b-q8" });

        Assert.Equal("qwen3-vl:8b", selected);
    }

    [Fact]
    public void Select_falls_back_to_first_vl_model_when_configured_model_is_missing()
    {
        var selected = VisionModelSelectionPolicy.Select(
            "missing-model",
            new[] { "llama3", "minicpm-vl:latest", "qwen3-vl:8b" });

        Assert.Equal("minicpm-vl:latest", selected);
    }

    [Fact]
    public void Select_keeps_configured_model_when_no_vl_fallback_exists()
    {
        var selected = VisionModelSelectionPolicy.Select(
            "missing-model",
            new[] { "llama3", "nomic-embed-text" });

        Assert.Equal("missing-model", selected);
    }
}
