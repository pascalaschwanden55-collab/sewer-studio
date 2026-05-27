using System.Reflection;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Infrastructure.Ai.Sanierung;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class AiSanierungOptimizationServiceVsaMappingTests
{
    [Fact]
    public void System_prompt_uses_bac_not_bbb_for_collapse()
    {
        var service = new AiSanierungOptimizationService(
            new AiRuntimeSettings(
                Enabled: false,
                OllamaBaseUri: new Uri("http://127.0.0.1:11434"),
                VisionModel: "vision",
                TextModel: "text",
                EmbedModel: null,
                FfmpegPath: null,
                OllamaRequestTimeout: TimeSpan.FromSeconds(1),
                OllamaKeepAlive: "5m",
                OllamaNumCtx: 1024),
            new HttpClient());

        var method = typeof(AiSanierungOptimizationService).GetMethod(
            "BuildSystemPrompt",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(method);
        var prompt = Assert.IsType<string>(method!.Invoke(service, null));
        Assert.Contains("BAC-Codes", prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("BBB-Codes", prompt, StringComparison.Ordinal);
    }
}
