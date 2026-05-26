using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace AuswertungPro.Next.Infrastructure.Ai.Pipeline;

/// <summary>
/// HTTP client for the Python FastAPI Vision Sidecar.
/// Pattern mirrors OllamaClient – simple, typed HTTP calls.
/// </summary>
public sealed class VisionPipelineClient
{
    private readonly HttpClient _http;
    private readonly Uri _baseUri;
    private readonly string? _sidecarToken;
    private readonly bool _sendSidecarToken;
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
    };

    public VisionPipelineClient(Uri baseUri, HttpClient? httpClient = null, string? sidecarToken = null)
    {
        _baseUri = baseUri;
        _http = httpClient ?? new HttpClient { Timeout = TimeSpan.FromMinutes(15) };
        _http.BaseAddress = baseUri;
        _sendSidecarToken = IsLoopbackUri(baseUri);
        _sidecarToken = _sendSidecarToken
            ? NormalizeToken(sidecarToken) ?? TryLoadSidecarToken()
            : null;
    }

    /// <summary>
    /// Health check. Returns null if sidecar is not reachable.
    /// </summary>
    public async Task<SidecarHealthResponse?> HealthCheckAsync(CancellationToken ct = default)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, BuildUri("/health"));
            AddSidecarTokenHeader(req);

            using var resp = await _http.SendAsync(req, ct).ConfigureAwait(false);
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

    // ── Internal ──────────────────────────────────────────────────────────

    private async Task<TResponse> PostAsync<TRequest, TResponse>(
        string endpoint, TRequest request, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(request, JsonOpts);
        using var req = new HttpRequestMessage(HttpMethod.Post, BuildUri(endpoint))
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        AddSidecarTokenHeader(req);

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

    private void AddSidecarTokenHeader(HttpRequestMessage request)
    {
        if (_sendSidecarToken && !string.IsNullOrWhiteSpace(_sidecarToken))
            request.Headers.TryAddWithoutValidation("X-Sidecar-Token", _sidecarToken);
    }

    private static bool IsLoopbackUri(Uri uri)
    {
        if (uri.IsLoopback)
            return true;

        var host = uri.Host.Trim();
        return string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(host, "127.0.0.1", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(host, "::1", StringComparison.OrdinalIgnoreCase);
    }

    private static string? TryLoadSidecarToken()
    {
        var authEnv = NormalizeToken(Environment.GetEnvironmentVariable("SEWER_SIDECAR_AUTH_TOKEN"));
        if (authEnv is not null)
            return authEnv;

        var env = NormalizeToken(Environment.GetEnvironmentVariable("SEWER_SIDECAR_TOKEN"));
        if (env is not null)
            return env;

        try
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (string.IsNullOrWhiteSpace(localAppData))
                return null;

            var path = Path.Combine(localAppData, "SewerStudio", ".sidecar_token");
            return File.Exists(path)
                ? NormalizeToken(File.ReadAllText(path))
                : null;
        }
        catch
        {
            return null;
        }
    }

    private static string? NormalizeToken(string? token)
        => string.IsNullOrWhiteSpace(token) ? null : token.Trim();
}
