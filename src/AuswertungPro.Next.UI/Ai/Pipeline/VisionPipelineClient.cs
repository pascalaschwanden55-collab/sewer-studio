using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace AuswertungPro.Next.UI.Ai.Pipeline;

/// <summary>
/// HTTP client for the Python FastAPI Vision Sidecar.
/// Pattern mirrors OllamaClient – simple, typed HTTP calls.
/// </summary>
public sealed class VisionPipelineClient
{
    private readonly HttpClient _http;
    private readonly Uri _baseUri;
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
    };

    public VisionPipelineClient(Uri baseUri, HttpClient? httpClient = null)
    {
        _baseUri = baseUri;
        _http = httpClient ?? new HttpClient { Timeout = TimeSpan.FromMinutes(15) };
        _http.BaseAddress = baseUri;
    }

    /// <summary>
    /// Health check. Returns null if sidecar is not reachable.
    /// </summary>
    public async Task<SidecarHealthResponse?> HealthCheckAsync(CancellationToken ct = default)
    {
        try
        {
            using var resp = await _http.GetAsync(BuildUri("/health"), ct).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode)
                return null;

            var json = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            return JsonSerializer.Deserialize<SidecarHealthResponse>(json, JsonOpts);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// YOLO pre-screening detection.
    /// </summary>
    public async Task<YoloResponse> DetectYoloAsync(YoloRequest request, CancellationToken ct = default)
    {
        return await PostAsync<YoloRequest, YoloResponse>("/detect/yolo", request, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Grounding DINO open-vocabulary detection.
    /// </summary>
    public async Task<DinoResponse> DetectDinoAsync(DinoRequest request, CancellationToken ct = default)
    {
        return await PostAsync<DinoRequest, DinoResponse>("/detect/dino", request, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// SAM pixel-precise segmentation.
    /// </summary>
    public async Task<SamResponse> SegmentSamAsync(SamRequest request, CancellationToken ct = default)
    {
        return await PostAsync<SamRequest, SamResponse>("/segment/sam", request, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// YOLO-cls Whole-Frame-Klassifikation (BCD/BCE/BCA/BCC/...).
    /// </summary>
    public async Task<YoloClassifyResponse> ClassifyYoloAsync(YoloClassifyRequest request, CancellationToken ct = default)
    {
        return await PostAsync<YoloClassifyRequest, YoloClassifyResponse>("/classify/yolo", request, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Export training data to YOLO format.
    /// </summary>
    public async Task<TrainingExportResponseDto> ExportTrainingAsync(TrainingExportRequestDto request, CancellationToken ct = default)
    {
        return await PostAsync<TrainingExportRequestDto, TrainingExportResponseDto>("/training/export-yolo", request, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// NVDEC + YOLO Video-Pipeline: Dekodiert Video auf dem Sidecar-Server und
    /// liefert YOLO-Ergebnisse als NDJSON-Stream.
    /// Relevante Frames enthalten image_base64 fuer nachgelagerte DINO/SAM/Qwen-Analyse.
    /// </summary>
    public async IAsyncEnumerable<VideoFrameStreamResult> ProcessVideoStreamAsync(
        VideoProcessRequest request,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var json = JsonSerializer.Serialize(request, JsonOpts);
        using var req = new HttpRequestMessage(HttpMethod.Post, BuildUri("/process/video"))
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        // HttpCompletionOption.ResponseHeadersRead ermoeglicht streaming ohne vollstaendiges Puffern
        using var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct)
            .ConfigureAwait(false);

        if (!resp.IsSuccessStatusCode)
        {
            var body = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            throw new HttpRequestException(
                $"Sidecar /process/video returned {(int)resp.StatusCode}: {body}");
        }

        await using var stream = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        using var reader = new System.IO.StreamReader(stream);

        // ReadLineAsync gibt null zurueck wenn der Stream endet (CA2024: EndOfStream nicht verwenden)
        while (!ct.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(ct).ConfigureAwait(false);
            if (line is null)
                break;  // Stream-Ende
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var result = JsonSerializer.Deserialize<VideoFrameStreamResult>(line, JsonOpts);
            if (result is not null)
                yield return result;
        }
    }

    /// <summary>
    /// Video Super Resolution: Skaliert einen Frame auf target_height hoch.
    /// </summary>
    public async Task<EnhanceResponse> EnhanceAsync(EnhanceRequest request, CancellationToken ct = default)
    {
        return await PostAsync<EnhanceRequest, EnhanceResponse>("/enhance", request, ct).ConfigureAwait(false);
    }

    // ── Internal ──────────────────────────────────────────────────────────

    private async Task<TResponse> PostAsync<TRequest, TResponse>(
        string endpoint, TRequest request, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(request, JsonOpts);
        using var req = new HttpRequestMessage(HttpMethod.Post, BuildUri(endpoint))
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        using var resp = await _http.SendAsync(req, ct).ConfigureAwait(false);

        var body = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

        if (!resp.IsSuccessStatusCode)
            throw new HttpRequestException(
                $"Sidecar {endpoint} returned {(int)resp.StatusCode}: {body}");

        return JsonSerializer.Deserialize<TResponse>(body, JsonOpts)
            ?? throw new InvalidOperationException($"Failed to deserialize response from {endpoint}");
    }

    private Uri BuildUri(string endpoint)
    {
        var baseStr = _baseUri.ToString().TrimEnd('/');
        return new Uri($"{baseStr}{endpoint}");
    }
}
