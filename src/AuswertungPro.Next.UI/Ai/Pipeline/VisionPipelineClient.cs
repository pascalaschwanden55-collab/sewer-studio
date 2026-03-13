using System;
using System.Net.Http;
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
        var ownClient = httpClient is null;
        _http = httpClient ?? new HttpClient { Timeout = TimeSpan.FromMinutes(15) };
        if (ownClient)
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
    /// Export training data to YOLO format.
    /// </summary>
    public async Task<TrainingExportResponseDto> ExportTrainingAsync(TrainingExportRequestDto request, CancellationToken ct = default)
    {
        return await PostAsync<TrainingExportRequestDto, TrainingExportResponseDto>("/training/export-yolo", request, ct).ConfigureAwait(false);
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
