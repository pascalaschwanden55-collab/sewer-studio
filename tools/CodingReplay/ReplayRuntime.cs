using System.Text.Json;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Infrastructure.Ai.Configuration;

namespace CodingReplay;

internal static class ReplayRuntime
{
    internal static AiPlatformSettings Load(string settingsFile)
    {
        if (!File.Exists(settingsFile)) return AiSettingsFactory.Load();
        using var doc = JsonDocument.Parse(ReplayFiles.Read(settingsFile));
        var root = doc.RootElement;
        string? Text(string name) => root.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
        double? Number(string name) => root.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetDouble(out var d) ? d : null;
        bool? Flag(string name) => root.TryGetProperty(name, out var v) && v.ValueKind is JsonValueKind.True or JsonValueKind.False ? v.GetBoolean() : null;
        return AiSettingsFactory.Load(new AiSettingsSource(
            Enabled: Flag("AiEnabled"), OllamaUrl: Text("AiOllamaUrl"), VisionModel: Text("AiVisionModel"),
            TextModel: Text("AiTextModel"), EmbedModel: Text("AiEmbedModel"),
            OllamaTimeoutMin: (int?)Number("AiOllamaTimeoutMin"), OllamaKeepAlive: Text("AiOllamaKeepAlive"),
            OllamaNumCtx: (int?)Number("AiOllamaNumCtx"), MultiModelEnabled: Flag("PipelineMultiModelEnabled"),
            SidecarUrl: Text("PipelineSidecarUrl"), SidecarToken: Text("PipelineSidecarToken"),
            PipelineMode: Text("PipelineMode"), YoloConfidence: Number("PipelineYoloConfidence"),
            DinoBoxThreshold: Number("PipelineDinoBoxThreshold"), DinoTextThreshold: Number("PipelineDinoTextThreshold")));
    }
}
