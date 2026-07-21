using System.Text.Json;
using AuswertungPro.Next.Application.Ai;

namespace AuswertungPro.Next.Pipeline.Tests;

public sealed class VisionPipelineDtosTests
{
    // Dieselben Optionen wie der produktive VisionPipelineClient.
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
    };

    [Fact]
    public void YoloClassifyResponse_ohne_classifier_loaded_ist_fail_closed()
    {
        // Antwort ohne das Feld (aelterer/fremder Sidecar): Klassifikator gilt als NICHT geladen,
        // konsistent zum Sidecar-Default (schemas/detection.py: classifier_loaded=False).
        const string json = """{"predictions":[],"inference_time_ms":1}""";

        var dto = JsonSerializer.Deserialize<YoloClassifyResponse>(json, JsonOpts);

        Assert.NotNull(dto);
        Assert.False(dto!.ClassifierLoaded);
    }

    [Fact]
    public void YoloClassifyResponse_mit_classifier_loaded_true_wird_gelesen()
    {
        const string json = """{"predictions":[],"inference_time_ms":1,"classifier_loaded":true}""";

        var dto = JsonSerializer.Deserialize<YoloClassifyResponse>(json, JsonOpts);

        Assert.NotNull(dto);
        Assert.True(dto!.ClassifierLoaded);
    }
}
