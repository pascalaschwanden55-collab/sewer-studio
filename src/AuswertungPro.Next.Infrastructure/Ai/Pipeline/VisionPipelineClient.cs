using System;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Ai;

namespace AuswertungPro.Next.Infrastructure.Ai.Pipeline;

/// <summary>
/// HTTP client for the Python FastAPI Vision Sidecar.
/// Pattern mirrors OllamaClient – simple, typed HTTP calls.
/// </summary>
public sealed class VisionPipelineClient : IVisionPipelineClient
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
        // KEIN _http.BaseAddress setzen: BuildUri() erzeugt immer absolute URIs. Ein gesetztes
        // BaseAddress auf einem GETEILTEN HttpClient (VideoAnalysisPipelineService nutzt fuer
        // mehrere Clients dieselbe Instanz) wirft InvalidOperationException, sobald der Client
        // schon einen Request gesendet hat -> der Multi-Model-Hauptpfad bricht ab. (Audit R1)
        _sendSidecarToken = IsLoopbackUri(baseUri);
        _sidecarToken = _sendSidecarToken
            ? SidecarTokenResolver.Resolve(sidecarToken)
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
        catch (OperationCanceledException)
        {
            // Abbruch (Shutdown/Timeout) ist KEIN "Sidecar offline" — nicht als null kaschieren,
            // sonst faellt die Pipeline faelschlich auf den schwaecheren Ollama-Only-Modus zurueck.
            throw;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Health-Check mit Fehlerart-Unterscheidung (offline vs. 401 vs. ok),
    /// damit die UI Token-Fehler nicht als allgemeines "offline" anzeigt.
    /// </summary>
    public async Task<PipelineHealthCheckResult> CheckHealthDetailedAsync(CancellationToken ct = default)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, BuildUri("/health"));
            AddSidecarTokenHeader(req);
            using var resp = await _http.SendAsync(req, ct).ConfigureAwait(false);

            int code = (int)resp.StatusCode;
            if (resp.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                return new PipelineHealthCheckResult(true, false, code, null, "Token ungueltig/fehlt");

            if (!resp.IsSuccessStatusCode)
                return new PipelineHealthCheckResult(true, true, code, null, $"HTTP {code}");

            var json = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            var health = JsonSerializer.Deserialize<SidecarHealthResponse>(json, JsonOpts);
            return new PipelineHealthCheckResult(true, true, code, health, null);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            return new PipelineHealthCheckResult(false, false, null, null, ex.Message);
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

        // Gesamtaudit P3: genau EIN Retry bei transienten Fehlern (503 = Sidecar raeumt
        // VRAM auf / laedt Modell um; Transportfehler = Sidecar startet gerade neu).
        // Ohne Retry kippt ein einzelner Schluckauf den Frame unnoetig in den
        // Degraded-/Skip-Pfad. Bewusst KEIN Retry bei Abbruch durch den Aufrufer und
        // kein Mehrfach-Retry — echte Ausfaelle sollen schnell ehrlich scheitern.
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await PostOnceAsync<TResponse>(endpoint, json, ct).ConfigureAwait(false);
            }
            catch (Exception ex) when (attempt == 1 && !ct.IsCancellationRequested && IsTransientSidecarError(ex))
            {
                await Task.Delay(TimeSpan.FromMilliseconds(1500), ct).ConfigureAwait(false);
            }
            catch (Exception ex) when (IsSidecarUnavailableError(ex))
            {
                throw new SidecarUnavailableException(
                    $"Vision-Sidecar {endpoint} ist nicht verfuegbar: {ex.Message}",
                    ex);
            }
        }
    }

    private static bool IsTransientSidecarError(Exception ex)
        => ex is HttpRequestException hre
           && (hre.StatusCode == HttpStatusCode.ServiceUnavailable
               || hre.StatusCode is null); // null = Transportfehler (Verbindung abgelehnt/abgerissen)

    private static bool IsSidecarUnavailableError(Exception ex)
    {
        if (ex is HttpRequestException hre)
            return hre.StatusCode is null or HttpStatusCode.ServiceUnavailable;

        return ex is SocketException || ex.InnerException is SocketException;
    }

    private async Task<TResponse> PostOnceAsync<TResponse>(
        string endpoint, string json, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, BuildUri(endpoint))
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        AddSidecarTokenHeader(req);

        var roundtrip = Stopwatch.StartNew();
        using var resp = await _http.SendAsync(req, ct).ConfigureAwait(false);

        var body = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        roundtrip.Stop();

        if (!resp.IsSuccessStatusCode)
        {
            if (IsClientSidecarRequestError(resp.StatusCode))
                throw new SidecarBadRequestException(endpoint, resp.StatusCode, body);

            throw new HttpRequestException(
                $"Sidecar {endpoint} returned {(int)resp.StatusCode}: {body}",
                inner: null,
                statusCode: resp.StatusCode);
        }

        var result = JsonSerializer.Deserialize<TResponse>(body, JsonOpts)
            ?? throw new InvalidOperationException($"Failed to deserialize response from {endpoint}");

        if (result is YoloResponse yolo)
            await SidecarTelemetryWriter.WriteAsync(CreateTelemetryEvent(endpoint, yolo, roundtrip.ElapsedMilliseconds)).ConfigureAwait(false);

        return result;
    }

    private static bool IsClientSidecarRequestError(HttpStatusCode statusCode)
        => (int)statusCode is >= 400 and <= 499;

    private static SidecarTelemetryEvent CreateTelemetryEvent(
        string endpoint,
        YoloResponse yolo,
        long roundtripMs)
        => new(
            TimestampUtc: DateTimeOffset.UtcNow,
            Endpoint: endpoint,
            ModelName: yolo.ModelName,
            RoundtripMs: roundtripMs,
            InferenceTimeMs: yolo.InferenceTimeMs,
            QueueWaitMs: yolo.QueueWaitMs,
            Device: yolo.Device,
            VramAllocatedGb: yolo.VramAllocatedGb,
            VramTotalGb: yolo.VramTotalGb,
            DetectionCount: yolo.Detections.Count,
            IsRelevant: yolo.IsRelevant,
            FrameClass: yolo.FrameClass);

    private Uri BuildUri(string endpoint)
    {
        var baseStr = _baseUri.ToString().TrimEnd('/');
        return new Uri($"{baseStr}{endpoint}");
    }

    private void AddSidecarTokenHeader(HttpRequestMessage request)
    {
        if (_sendSidecarToken && !string.IsNullOrWhiteSpace(_sidecarToken))
            request.Headers.TryAddWithoutValidation(SidecarTokenResolver.HeaderName, _sidecarToken);
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

}
