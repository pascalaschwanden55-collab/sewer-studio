using System;
using AuswertungPro.Next.Infrastructure.Ai.Ollama;
using System.Collections.Generic;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Ai.Pipeline;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;

namespace AuswertungPro.Next.Infrastructure.Ai.Pipeline;

/// <summary>
/// HTTP client for the Python FastAPI Vision Sidecar.
/// Pattern mirrors OllamaClient – simple, typed HTTP calls.
/// </summary>
public sealed class VisionPipelineClient
{
    private readonly HttpClient _http;
    private readonly Uri _baseUri;
    private readonly string? _authToken;
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
    };

    public VisionPipelineClient(Uri baseUri, HttpClient? httpClient = null, string? authToken = null)
    {
        _baseUri = baseUri;
        _http = httpClient ?? new HttpClient { Timeout = TimeSpan.FromMinutes(15) };
        // Auth-Token Resolver in 3 Stufen (Audit-Fix 2026-04-30: SAM-Segmentierung war
        // ohne Token unsichtbar weil AuswertungPro.Next.Application.Ai.SidecarAuthTokenAccessor.CurrentAuthToken bei
        // manuell gestartetem Sidecar leer ist):
        //   1. Explizit per Konstruktor uebergeben
        //   2. Singleton von PythonSidecarService (App-gestartet)
        //   3. Token-Datei %LOCALAPPDATA%/SewerStudio/.sidecar_token (manuell gestartet)
        // Wenn alles leer und SEWER_SIDECAR_AUTH=disabled gesetzt -> ohne Token weiter.
        var effective = string.IsNullOrWhiteSpace(authToken) ? AuswertungPro.Next.Application.Ai.SidecarAuthTokenAccessor.CurrentAuthToken : authToken;
        if (string.IsNullOrWhiteSpace(effective))
        {
            try
            {
                var tokenFile = AuswertungPro.Next.Application.Ai.SidecarAuthTokenAccessor.TokenFilePath;
                if (System.IO.File.Exists(tokenFile))
                {
                    var fromFile = System.IO.File.ReadAllText(tokenFile).Trim();
                    if (!string.IsNullOrWhiteSpace(fromFile))
                        effective = fromFile;
                }
            }
            catch { /* best-effort: Token-Datei optional */ }
        }
        _authToken = string.IsNullOrWhiteSpace(effective) ? null : effective;
        // BaseAddress NICHT setzen — wir verwenden _baseUri + BuildUri() fuer jeden Request.
        // Verhindert InvalidOperationException wenn HttpClient wiederverwendet wird.
    }

    public string? LastHealthError { get; private set; }

    /// <summary>
    /// Fuegt Auth-Header (X-Sidecar-Token) zur Request hinzu, falls Token gesetzt.
    /// </summary>
    private void AddAuthHeader(HttpRequestMessage req)
    {
        if (_authToken is not null)
            req.Headers.TryAddWithoutValidation("X-Sidecar-Token", _authToken);
    }

    /// <summary>
    /// Health check. Returns null if sidecar is not reachable.
    /// </summary>
    public async Task<SidecarHealthResponse?> HealthCheckAsync(CancellationToken ct = default)
    {
        LastHealthError = null;
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, BuildUri("/health"));
            AddAuthHeader(req);
            using var resp = await _http.SendAsync(req, ct).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode)
            {
                LastHealthError = $"HTTP {(int)resp.StatusCode} {resp.ReasonPhrase}";
                System.Diagnostics.Debug.WriteLine($"[VisionPipelineClient] HealthCheck failed: {LastHealthError}");
                return null;
            }

            var json = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            return JsonSerializer.Deserialize<SidecarHealthResponse>(json, JsonOpts);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LastHealthError = $"{ex.GetType().Name}: {ex.Message}";
            System.Diagnostics.Debug.WriteLine($"[VisionPipelineClient] HealthCheck exception: {LastHealthError}");
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
    /// Florence-2 Open-Vocabulary Detection (Endpoint: /detect/dino, Slot: DINO).
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

    /// <summary>Aufnahmetechnik-Klassifikation: axial/schacht/uebergang (~0.1ms).</summary>
    public async Task<ViewTypeResponse> ClassifyViewTypeAsync(ViewTypeRequest request, CancellationToken ct = default)
    {
        return await PostAsync<ViewTypeRequest, ViewTypeResponse>("/classify/viewtype", request, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// V4.2 Phase 3: DINOv2 Foundation-Encoder + Linear-Heads Klassifikation.
    /// Ersetzt die tote Grounding-DINO-Kaskade im Codier-Modus.
    /// Antwortet mit leerer predictions-Liste wenn keine Heads trainiert sind → Fallback auf Qwen.
    /// </summary>
    public async Task<DinoV2Response> ClassifyDinoV2Async(DinoV2Request request, CancellationToken ct = default)
    {
        return await PostAsync<DinoV2Request, DinoV2Response>("/classify/dinov2", request, ct).ConfigureAwait(false);
    }

    /// <summary>Batch-YOLO: mehrere Bilder in einem Forward Pass.</summary>
    public async Task<YoloBatchResponseDto> DetectYoloBatchAsync(YoloBatchRequestDto request, CancellationToken ct = default)
        => await PostAsync<YoloBatchRequestDto, YoloBatchResponseDto>("/detect/yolo/batch", request, ct).ConfigureAwait(false);

    /// <summary>Batch-DINO: mehrere Bilder grounding.</summary>
    public async Task<DinoBatchResponseDto> DetectDinoBatchAsync(DinoBatchRequestDto request, CancellationToken ct = default)
        => await PostAsync<DinoBatchRequestDto, DinoBatchResponseDto>("/detect/dino/batch", request, ct).ConfigureAwait(false);

    /// <summary>Batch-SAM: mehrere Bilder mit gebatchten Boxen segmentieren.</summary>
    public async Task<SamBatchResponseDto> SegmentSamBatchAsync(SamBatchRequestDto request, CancellationToken ct = default)
        => await PostAsync<SamBatchRequestDto, SamBatchResponseDto>("/segment/sam/batch", request, ct).ConfigureAwait(false);

    /// <summary>
    /// Export training data to YOLO format.
    /// </summary>
    public async Task<TrainingExportResponseDto> ExportTrainingAsync(TrainingExportRequestDto request, CancellationToken ct = default)
    {
        return await PostAsync<TrainingExportRequestDto, TrainingExportResponseDto>("/training/export-yolo", request, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Start asynchronous YOLO training on the sidecar.
    /// </summary>
    public async Task<YoloTrainJobStartDto> StartYoloTrainingAsync(YoloTrainRequestDto request, CancellationToken ct = default)
    {
        return await PostAsync<YoloTrainRequestDto, YoloTrainJobStartDto>("/training/train-yolo", request, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Fetch status for a running/completed YOLO training job.
    /// </summary>
    public async Task<YoloTrainJobStatusDto> GetYoloTrainingJobAsync(string jobId, CancellationToken ct = default)
    {
        return await GetAsync<YoloTrainJobStatusDto>($"/training/jobs/{jobId}", ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Hot-swap active YOLO model on the sidecar without process restart.
    /// </summary>
    public async Task<ModelReloadResponseDto> ReloadYoloModelAsync(ModelReloadRequestDto request, CancellationToken ct = default)
    {
        return await PostAsync<ModelReloadRequestDto, ModelReloadResponseDto>("/model/reload", request, ct).ConfigureAwait(false);
    }

    // ── LoRA Training ──────────────────────────────────────────────────────

    /// <summary>Start asynchronous LoRA fine-tuning on the sidecar.</summary>
    public async Task<LoraTrainJobStartDto> StartLoraTrainingAsync(LoraTrainRequestDto request, CancellationToken ct = default)
    {
        return await PostAsync<LoraTrainRequestDto, LoraTrainJobStartDto>("/training/train-lora", request, ct).ConfigureAwait(false);
    }

    /// <summary>Fetch status for a running/completed LoRA training job.</summary>
    public async Task<LoraTrainJobStatusDto> GetLoraTrainingJobAsync(string jobId, CancellationToken ct = default)
    {
        return await GetAsync<LoraTrainJobStatusDto>($"/training/lora-jobs/{jobId}", ct).ConfigureAwait(false);
    }

    /// <summary>Deploy LoRA adapter via Ollama Modelfile.</summary>
    public async Task<LoraDeployResponseDto> DeployLoraAdapterAsync(LoraDeployRequestDto request, CancellationToken ct = default)
    {
        return await PostAsync<LoraDeployRequestDto, LoraDeployResponseDto>("/training/deploy-lora", request, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Rohrachsen-Analyse: Fluchtpunkt + Rohrmitte + Muffen-Erkennung.
    /// Leichtgewichtig (~5ms/Frame), fuer Knick-Detektion.
    /// </summary>
    public async Task<PipeAxisResult> AnalyzePipeAxisAsync(PipeAxisRequest request, CancellationToken ct = default)
    {
        return await PostAsync<PipeAxisRequest, PipeAxisResult>("/analyze/pipe-axis", request, ct).ConfigureAwait(false);
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
        AddAuthHeader(req);

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

    /// <summary>
    /// PDF-Tabellen-Extraktion via Nemotron-Parse.
    /// </summary>
    public async Task<ParsePdfTableResponse> ParsePdfTableAsync(ParsePdfTableRequest request, CancellationToken ct = default)
    {
        return await PostAsync<ParsePdfTableRequest, ParsePdfTableResponse>("/parse/pdf-table", request, ct).ConfigureAwait(false);
    }

    // ── Internal ──────────────────────────────────────────────────────────

    /// <summary>Generischer POST-Aufruf gegen den Sidecar (intern + fuer Services).</summary>
    public async Task<TResponse> PostJsonAsync<TRequest, TResponse>(
        string endpoint, TRequest request, CancellationToken ct)
        => await PostAsync<TRequest, TResponse>(endpoint, request, ct).ConfigureAwait(false);

    private async Task<TResponse> PostAsync<TRequest, TResponse>(
        string endpoint, TRequest request, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(request, JsonOpts);
        using var req = new HttpRequestMessage(HttpMethod.Post, BuildUri(endpoint))
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        AddAuthHeader(req);
        using var resp = await _http.SendAsync(req, ct).ConfigureAwait(false);

        var body = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

        if (!resp.IsSuccessStatusCode)
            throw new HttpRequestException(
                $"Sidecar {endpoint} returned {(int)resp.StatusCode}: {body}");

        return JsonSerializer.Deserialize<TResponse>(body, JsonOpts)
            ?? throw new InvalidOperationException($"Failed to deserialize response from {endpoint}");
    }

    private async Task<TResponse> GetAsync<TResponse>(string endpoint, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, BuildUri(endpoint));
        AddAuthHeader(req);
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
