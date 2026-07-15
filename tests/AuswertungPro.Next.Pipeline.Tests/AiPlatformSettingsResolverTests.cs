using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Infrastructure.Ai.Configuration;
using AuswertungPro.Next.Infrastructure.Ai.Ollama;

namespace AuswertungPro.Next.Pipeline.Tests;

public sealed class AiPlatformSettingsResolverTests
{
    [Fact]
    public void Load_bevorzugt_Quelle_und_liest_alte_Umgebungsnamen()
    {
        var environment = new Dictionary<string, string?>
        {
            ["SEWERSTUDIO_AI_TEXT_MODEL"] = "env-text",
            ["AUSWERTUNGPRO_AI_ENABLED"] = "1",
            ["SEWER_SIDECAR_TOKEN"] = "legacy-token"
        };
        var gpu = new GpuModelSelectorFake(null);
        var resolver = new AiPlatformSettingsResolver(
            gpu,
            name => environment.GetValueOrDefault(name),
            _ => { });

        var settings = resolver.Load(new AiSettingsSource(
            VisionModel: "festes-modell",
            TextModel: "source-text"));

        Assert.True(settings.Enabled);
        Assert.Equal("festes-modell", settings.VisionModel);
        Assert.Equal("source-text", settings.TextModel);
        Assert.Equal("legacy-token", settings.SidecarToken);
        Assert.Equal(0, gpu.Calls);
    }

    [Fact]
    public void Load_verwendet_injizierte_Gpu_Auswahl_im_Automodus()
    {
        var profile = new GpuModelSelector.GpuProfile(
            "gpu-modell",
            12288,
            32768,
            "Test GPU",
            "Testauswahl");
        var gpu = new GpuModelSelectorFake(profile);
        var traces = new List<string>();
        var resolver = new AiPlatformSettingsResolver(
            gpu,
            _ => null,
            traces.Add);

        var settings = resolver.Load(new AiSettingsSource(VisionModel: "auto"));

        Assert.Equal("gpu-modell", settings.VisionModel);
        Assert.Equal(12288, settings.OllamaNumCtx);
        Assert.Equal(1, gpu.Calls);
        Assert.Contains("Testauswahl", Assert.Single(traces), StringComparison.Ordinal);
    }

    private sealed class GpuModelSelectorFake(GpuModelSelector.GpuProfile? profile) : IGpuModelSelector
    {
        public int Calls { get; private set; }

        public GpuModelSelector.GpuProfile? DetectAndSelect()
        {
            Calls++;
            return profile;
        }
    }
}
