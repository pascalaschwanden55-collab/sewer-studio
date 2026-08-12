using System;
using System.Diagnostics;
using System.Globalization;
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
public sealed class VisionPipelineClient : IVisionPipelineClient, IDisposable
{
    public const string ExpectedSidecarVersion = "1.2.0";
    private readonly HttpClient _http;
    private readonly bool _ownsHttp;
    private readonly Uri _baseUri;
    private readonly string? _sidecarToken;
    private readonly bool _sendSidecarToken;
    private readonly ISidecarTelemetryWriter _telemetry;
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
    };

    // Paket 3/C: Per-Request-Cap fuer Inferenzaufrufe (YOLO/DINO/SAM), entkoppelt vom
    // geteilten Client-Timeout (Default 5 min aus der Ollama-Konfiguration). Ein
    // haengender CUDA-Call im Sidecar blockiert sonst einen Frame bis zum Client-Timeout;
    // der kurze Cap macht ihn zum ehrlichen Transportfehler (zaehlt im Ausfallschutz).
    // Env-Override im Stil der uebrigen SEWERSTUDIO_-Optionen (mit AUSWERTUNGPRO_-Compat).
    public const string RequestTimeoutEnvVar = "SEWERSTUDIO_SIDECAR_REQUEST_TIMEOUT_SEC";
    public static readonly TimeSpan DefaultRequestTimeout = TimeSpan.FromSeconds(120);

    /// <summary>Per-Request-Cap fuer Inferenzaufrufe. Default 120 s, per Env ueberschreibbar.
    /// Instanz-Eigenschaft, damit Tests den Wert ohne Prozess-Umgebung setzen koennen.</summary>
    public TimeSpan RequestTimeout { get; set; } = ResolveRequestTimeout();

    public VisionPipelineClient(Uri baseUri, HttpClient? httpClient = null, string? sidecarToken = null)
        : this(baseUri, httpClient, sidecarToken, SidecarTelemetryWriter.Current)
    {
    }

    public VisionPipelineClient(
        Uri baseUri,
        HttpClient? httpClient,
        string? sidecarToken,
        ISidecarTelemetryWriter telemetry,
        TimeSpan? ownedTimeout = null)
    {
        _baseUri = baseUri;
        // Besitz-Regel wie OllamaClient: nur ein SELBST erzeugter HttpClient wird bei Dispose
        // freigegeben. Ein injizierter (geteilter) Client — z. B. der nach Timeout gecachte
        // Client des VideoAnalysisPipelineService — bleibt unangetastet.
        _ownsHttp = httpClient is null;
        _http = httpClient ?? new HttpClient
        {
            Timeout = ownedTimeout is { } t && t > TimeSpan.Zero ? t : TimeSpan.FromMinutes(15)
        };
        _telemetry = telemetry ?? throw new ArgumentNullException(nameof(telemetry));
        // KEIN _http.BaseAddress setzen: BuildUri() erzeugt immer absolute URIs. Ein gesetztes
        // BaseAddress auf einem GETEILTEN HttpClient (VideoAnalysisPipelineService nutzt fuer
        // mehrere Clients dieselbe Instanz) wirft InvalidOperationException, sobald der Client
        // schon einen Request gesendet hat -> der Multi-Model-Hauptpfad bricht ab. (Audit R1)
        _sendSidecarToken = SidecarEndpointPolicy.IsLoopback(baseUri);
        _sidecarToken = _sendSidecarToken
            ? SidecarTokenResolver.Resolve(sidecarToken)
            : null;
    }

    public void Dispose()
    {
        if (_ownsHttp)
            _http.Dispose();
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
            if (health is null)
                return new PipelineHealthCheckResult(true, true, code, null, "Health-Antwort war leer oder ungueltig");
            if (!string.Equals(health.Version, ExpectedSidecarVersion, StringComparison.Ordinal))
            {
                return new PipelineHealthCheckResult(
                    true,
                    true,
                    code,
                    health,
                    $"Sidecar-Version passt nicht: erwartet {ExpectedSidecarVersion}, erhalten {health.Version}");
            }
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
        return await PostInferenceAsync<YoloRequest, YoloResponse>("/detect/yolo", "YOLO", request, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Nur-lesende Vorschau mit einem manifest- und hashgeprueften BCC-Kandidaten.
    /// Der Sidecar waehlt das Modell selbst; der Client uebergibt keinen Dateipfad.
    /// </summary>
    public async Task<BccTestYoloResponse> DetectBccTestYoloAsync(
        YoloRequest request,
        CancellationToken ct = default)
    {
        return await PostInferenceAsync<YoloRequest, BccTestYoloResponse>(
                "/detect/yolo/bcc-test",
                "YOLO",
                request,
                ct)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Nur-lesende Vorschau mit einem exakt per ID und SHA-256 angehefteten Kandidaten.
    /// Ein Modellpfad ist nicht Teil dieses Vertrags.
    /// </summary>
    public async Task<BccTestYoloResponse> DetectBccTestYoloAsync(
        BccTestYoloRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!IsSafeCandidateId(request.CandidateId)
            || !IsSha256(request.CandidateSha256))
        {
            throw new ArgumentException(
                "Fuer den BCC-Modelltest sind eine exakte Kandidaten-ID und ein SHA-256 erforderlich.",
                nameof(request));
        }

        return await PostInferenceAsync<BccTestYoloRequest, BccTestYoloResponse>(
                "/detect/yolo/bcc-test",
                "YOLO-Test",
                request,
                ct)
            .ConfigureAwait(false);
    }

    public Task<BccTestCandidatesResponse> GetBccTestCandidatesAsync(
        CancellationToken ct = default)
        => GetAsync<BccTestCandidatesResponse>(
            "/detect/yolo/bcc-test/candidates",
            ct);

    /// <summary>
    /// Grounding DINO open-vocabulary detection.
    /// </summary>
    public async Task<DinoResponse> DetectDinoAsync(DinoRequest request, CancellationToken ct = default)
    {
        return await PostInferenceAsync<DinoRequest, DinoResponse>("/detect/dino", "DINO", request, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// SAM pixel-precise segmentation.
    /// </summary>
    public async Task<SamResponse> SegmentSamAsync(SamRequest request, CancellationToken ct = default)
    {
        return await PostInferenceAsync<SamRequest, SamResponse>("/segment/sam", "SAM", request, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// YOLO-cls Whole-Frame-Klassifikation (BCD/BCE/BCA/BCC/...).
    /// </summary>
    public async Task<YoloClassifyResponse> ClassifyYoloAsync(YoloClassifyRequest request, CancellationToken ct = default)
    {
        return await PostInferenceAsync<YoloClassifyRequest, YoloClassifyResponse>("/classify/yolo", "YOLO-cls", request, ct).ConfigureAwait(false);
    }

    // ── Internal ──────────────────────────────────────────────────────────

    private static bool IsSafeCandidateId(string? value)
    {
        if (string.IsNullOrEmpty(value)
            || value.Length > 128
            || !char.IsAsciiLetterOrDigit(value[0]))
        {
            return false;
        }

        foreach (var character in value)
        {
            if (!char.IsAsciiLetterOrDigit(character)
                && character is not '_' and not '-')
            {
                return false;
            }
        }
        return true;
    }

    private static bool IsSha256(string? value)
        => value is { Length: 64 } && value.All(Uri.IsHexDigit);

    /// <summary>
    /// Liest eine kleine Sidecar-Metadatenantwort mit derselben Token-Grenze.
    /// </summary>
    private async Task<TResponse> GetAsync<TResponse>(
        string endpoint,
        CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, BuildUri(endpoint));
        AddSidecarTokenHeader(req);
        using var resp = await _http.SendAsync(req, ct).ConfigureAwait(false);
        var body = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

        if (!resp.IsSuccessStatusCode)
        {
            if (IsClientSidecarRequestError(resp.StatusCode))
                throw new SidecarBadRequestException(endpoint, resp.StatusCode, body);

            throw new HttpRequestException(
                $"Sidecar {endpoint} returned {(int)resp.StatusCode}: {body}",
                inner: null,
                statusCode: resp.StatusCode);
        }

        return JsonSerializer.Deserialize<TResponse>(body, JsonOpts)
            ?? throw new InvalidOperationException(
                $"Failed to deserialize response from {endpoint}");
    }

    /// <summary>
    /// Inferenzaufrufe (YOLO/DINO/SAM/cls) mit Per-Request-Cap (Paket 3/C):
    /// linked CancellationTokenSource mit RequestTimeout. Ein Cap-Ausloeser wird als
    /// SidecarRequestTimeoutException gemeldet (= Transportfehler im Ausfallschutz);
    /// ein Abbruch durch den Aufrufer bleibt unveraendert OperationCanceledException.
    /// Health-Checks und der (lange) Trainings-Export laufen bewusst ohne dieses Cap.
    /// Paket 2/A6: <paramref name="model"/> ist das Modell-Label fuer die Timeout-Meldung.
    /// </summary>
    private async Task<TResponse> PostInferenceAsync<TRequest, TResponse>(
        string endpoint, string model, TRequest request, CancellationToken ct)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(RequestTimeout);
        try
        {
            return await PostAsync<TRequest, TResponse>(endpoint, request, timeoutCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested && timeoutCts.IsCancellationRequested)
        {
            throw new SidecarRequestTimeoutException(endpoint, RequestTimeout, model);
        }
    }

    private static TimeSpan ResolveRequestTimeout()
    {
        var raw = Environment.GetEnvironmentVariable(RequestTimeoutEnvVar)
                  ?? Environment.GetEnvironmentVariable("AUSWERTUNGPRO_SIDECAR_REQUEST_TIMEOUT_SEC");
        return double.TryParse(raw?.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var sec) && sec > 0
            ? TimeSpan.FromSeconds(sec)
            : DefaultRequestTimeout;
    }

    private async Task<TResponse> PostAsync<TRequest, TResponse>(
        string endpoint, TRequest request, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(request, JsonOpts);

        // Gesamtaudit P3: genau EIN Retry bei transienten Fehlern (503 = Sidecar raeumt
        // VRAM auf / laedt Modell um; Transportfehler = Sidecar startet gerade neu).
        // Ohne Retry kippt ein einzelner Schluckauf den Frame unnoetig in den
        // Degraded-/Skip-Pfad. Bewusst KEIN Retry bei Abbruch durch den Aufrufer und
        // kein Mehrfach-Retry — echte Ausfaelle sollen schnell ehrlich scheitern.
        // Ausnahme (Paket 2/A4): 503 mit code=insufficient_vram wird schon in PostOnceAsync
        // als SidecarInsufficientVramException geworfen (Kapazitaetsfehler, kein Retry).
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

    public async Task<TrainingExportPlanResponseDto> ExportPlannedTrainingAsync(
        TrainingExportPlanRequestDto request,
        CancellationToken ct = default)
    {
        return await PostAsync<TrainingExportPlanRequestDto, TrainingExportPlanResponseDto>(
                "/training/export-yolo",
                request,
                ct)
            .ConfigureAwait(false);
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

            // Paket 2/A4: 503-Fehlerbody EINMAL defensiv parsen. "insufficient_vram" ist ein
            // Kapazitaetsfehler (kein transienter Transportfehler): eigener Exception-Typ,
            // KEIN HTTP-Retry, kein Outage-Zaehler — der Frame-Catch behandelt ihn wie einen
            // Modellfehler. "model_unloaded" bleibt gezielt transient (Modell wird nachgeladen);
            // unbekannte/fehlende Codes und beschaedigte Bodys laufen wie bisher ueber den
            // allgemeinen 503-Weg (1 Retry, danach SidecarUnavailableException).
            if (resp.StatusCode == HttpStatusCode.ServiceUnavailable
                && TryParseSidecarErrorBody(body) is { } errorBody
                && string.Equals(errorBody.Code, "insufficient_vram", StringComparison.Ordinal))
            {
                throw new SidecarInsufficientVramException(
                    endpoint, errorBody.FreeGb, errorBody.RequiredGb, errorBody.ReservedGb);
            }

            throw new HttpRequestException(
                $"Sidecar {endpoint} returned {(int)resp.StatusCode}: {body}",
                inner: null,
                statusCode: resp.StatusCode);
        }

        var result = JsonSerializer.Deserialize<TResponse>(body, JsonOpts)
            ?? throw new InvalidOperationException($"Failed to deserialize response from {endpoint}");

        if (result is YoloResponse yolo)
            await WriteTelemetryBestEffortAsync(
                CreateTelemetryEvent(endpoint, yolo, roundtrip.ElapsedMilliseconds)).ConfigureAwait(false);

        return result;
    }

    private static bool IsClientSidecarRequestError(HttpStatusCode statusCode)
        => (int)statusCode is >= 400 and <= 499;

    /// <summary>
    /// Defensives Parsen eines Sidecar-Fehlerbodys (Paket 2/A4).
    /// Echter Vertrag des Sidecars (main.py exception_handler): code und die Zahlen
    /// stehen auf TOP-EBENE, "detail" ist ein Klartext-String —
    /// {"detail": "insufficient VRAM", "code": "insufficient_vram", "slot"?, "free_gb"?,
    /// "required_gb"?, "reserved_gb"?}.
    /// Toleranz: ein verschachteltes Format {"detail": {"code": ...}} wird ebenfalls
    /// akzeptiert; "detail" als nackter String zaehlt nur, wenn kein Top-Level-Code
    /// existiert. Beschaedigte oder anders geformte Bodys liefern null (= bisheriges
    /// Verhalten, allgemeiner 503-Weg).
    /// </summary>
    private static SidecarErrorBody? TryParseSidecarErrorBody(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return null;

            var code = ReadOptionalString(root, "code");
            var free = ReadOptionalGb(root, "free_gb");
            var required = ReadOptionalGb(root, "required_gb");
            var reserved = ReadOptionalGb(root, "reserved_gb");

            if (code is null && root.TryGetProperty("detail", out var detail))
            {
                if (detail.ValueKind == JsonValueKind.String)
                {
                    code = detail.GetString();
                }
                else if (detail.ValueKind == JsonValueKind.Object)
                {
                    code = ReadOptionalString(detail, "code");
                    free ??= ReadOptionalGb(detail, "free_gb");
                    required ??= ReadOptionalGb(detail, "required_gb");
                    reserved ??= ReadOptionalGb(detail, "reserved_gb");
                }
            }

            return code is null && free is null && required is null && reserved is null
                ? null
                : new SidecarErrorBody(code, free, required, reserved);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? ReadOptionalString(JsonElement element, string propertyName)
        => element.TryGetProperty(propertyName, out var child)
           && child.ValueKind == JsonValueKind.String
            ? child.GetString()
            : null;

    private static double? ReadOptionalGb(JsonElement detail, string propertyName)
        => detail.TryGetProperty(propertyName, out var element)
           && element.ValueKind == JsonValueKind.Number
           && element.TryGetDouble(out var value)
            ? value
            : null;

    private sealed record SidecarErrorBody(string? Code, double? FreeGb, double? RequiredGb, double? ReservedGb);

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

    private async Task WriteTelemetryBestEffortAsync(SidecarTelemetryEvent entry)
    {
        try
        {
            await _telemetry.WriteAsync(entry).ConfigureAwait(false);
        }
        catch
        {
            // Auch ein ersetzter Schreiber darf die Analyseantwort nie beeinflussen.
        }
    }

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

}
