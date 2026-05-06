using System;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Ai.Pipeline;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;

namespace AuswertungPro.Next.Infrastructure.Ai.ChangeDetection;

/// <summary>
/// Service fuer Pixel-Level Aenderungserkennung zwischen zwei Inspektionen
/// derselben Haltung (alt vs. neu).
/// </summary>
public sealed class ChangeDetectionService
{
    private readonly VisionPipelineClient _client;

    public ChangeDetectionService(VisionPipelineClient client)
    {
        _client = client;
    }

    /// <summary>Vergleicht zwei Inspektionsbilder und erkennt Aenderungen.</summary>
    public async Task<ChangeDetectionResult> DetectChangesAsync(
        byte[] imageOld, byte[] imageNew,
        int threshold = 30,
        CancellationToken ct = default)
    {
        var request = new ChangeDetectionRequest
        {
            ImageOldBase64 = Convert.ToBase64String(imageOld),
            ImageNewBase64 = Convert.ToBase64String(imageNew),
            Threshold = threshold,
        };

        var response = await _client.PostJsonAsync<ChangeDetectionRequest, ChangeDetectionResponse>(
            "/analyze/change-detection", request, ct).ConfigureAwait(false);

        return new ChangeDetectionResult
        {
            OverlayBase64 = response.ChangeOverlayBase64,
            ImageWidth = response.ImageWidth,
            ImageHeight = response.ImageHeight,
            ChangePercent = response.ChangePercent,
            WorsePercent = response.WorsePercent,
            BetterPercent = response.BetterPercent,
            InferenceTimeMs = response.InferenceTimeMs,
        };
    }
}

// ── DTOs ──────────────────────────────────────────────────────────────────

public sealed class ChangeDetectionRequest
{
    [JsonPropertyName("image_old_base64")]
    public string ImageOldBase64 { get; set; } = "";

    [JsonPropertyName("image_new_base64")]
    public string ImageNewBase64 { get; set; } = "";

    [JsonPropertyName("threshold")]
    public int Threshold { get; set; } = 30;
}

public sealed class ChangeDetectionResponse
{
    [JsonPropertyName("change_overlay_base64")]
    public string ChangeOverlayBase64 { get; set; } = "";

    [JsonPropertyName("image_width")]
    public int ImageWidth { get; set; }

    [JsonPropertyName("image_height")]
    public int ImageHeight { get; set; }

    [JsonPropertyName("change_percent")]
    public double ChangePercent { get; set; }

    [JsonPropertyName("worse_percent")]
    public double WorsePercent { get; set; }

    [JsonPropertyName("better_percent")]
    public double BetterPercent { get; set; }

    [JsonPropertyName("inference_time_ms")]
    public double InferenceTimeMs { get; set; }

    [JsonPropertyName("model_used")]
    public string ModelUsed { get; set; } = "";
}

/// <summary>Ergebnis der Aenderungserkennung.</summary>
public sealed class ChangeDetectionResult
{
    /// <summary>PNG-Overlay als Base64 (RGBA: Rot=Verschlechterung, Gruen=Verbesserung).</summary>
    public string OverlayBase64 { get; set; } = "";
    public int ImageWidth { get; set; }
    public int ImageHeight { get; set; }
    /// <summary>Prozentualer Anteil geaenderter Pixel.</summary>
    public double ChangePercent { get; set; }
    /// <summary>Prozentualer Anteil verschlechterter Pixel.</summary>
    public double WorsePercent { get; set; }
    /// <summary>Prozentualer Anteil verbesserter Pixel.</summary>
    public double BetterPercent { get; set; }
    public double InferenceTimeMs { get; set; }
}
