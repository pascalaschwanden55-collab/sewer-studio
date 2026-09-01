using System.Reflection;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Infrastructure.Ai;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class FullProtocolGenerationServiceTextTests
{
    [Fact]
    public void Qwen_Systemprompt_enthaelt_gueltiges_Deutsch_ohne_Mojibake()
    {
        using var httpClient = new HttpClient();
        using var service = new FullProtocolGenerationService(
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
            new NoopAiSuggestionPlausibilityService(),
            httpClient);
        var method = typeof(FullProtocolGenerationService).GetMethod(
            "BuildSystemPrompt",
            BindingFlags.Instance | BindingFlags.NonPublic);

        var prompt = Assert.IsType<string>(method!.Invoke(service, null));

        Assert.Contains("gültigem JSON", prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("Ã", prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("�", prompt, StringComparison.Ordinal);
    }
}
