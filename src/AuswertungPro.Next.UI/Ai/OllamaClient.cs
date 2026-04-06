using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;

namespace AuswertungPro.Next.UI.Ai;

/// <summary>
/// Minimaler Ollama HTTP Client mit Polly Retry + Circuit Breaker.
/// - /api/generate (legacy text/vision)
/// - /api/chat (structured output mit JSON schema + images in messages)
/// - Retry: 3x mit exponential Backoff (2s, 4s, 8s)
/// - Circuit Breaker: Nach 5 Failures im 60s-Fenster → 30s Fast-Fail
/// </summary>
public sealed class OllamaClient : IDisposable
{
    private readonly HttpClient _http;
    private readonly bool _ownsHttp;
    private readonly string _keepAlive;
    private readonly int _numCtx;

    // Polly Resilience-Pipeline: Retry + Circuit Breaker
    private readonly ResiliencePipeline _resiliencePipeline;
    private int _retryCount;
    private int _circuitBreakerTrips;

    /// <summary>Anzahl Retries seit Start (Telemetrie).</summary>
    public int RetryCount => _retryCount;
    /// <summary>Anzahl Circuit-Breaker-Ausloeser seit Start.</summary>
    public int CircuitBreakerTrips => _circuitBreakerTrips;

    public OllamaClient(Uri baseUri, HttpClient? http = null, TimeSpan? ownedTimeout = null, string keepAlive = "24h", int numCtx = 0)
    {
        _ownsHttp = http is null;
        _http = http ?? new HttpClient();
        _http.BaseAddress = baseUri;
        _keepAlive = keepAlive;
        _numCtx = numCtx;
        if (_ownsHttp)
            _http.Timeout = ownedTimeout is { } timeout && timeout > TimeSpan.Zero
                ? timeout
                : TimeSpan.FromMinutes(2); // Reduziert von 5min auf 2min — Polly uebernimmt Retries

        // Polly Pipeline: Retry (aussen) → Circuit Breaker (innen)
        _resiliencePipeline = new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                BackoffType = DelayBackoffType.Exponential,
                Delay = TimeSpan.FromSeconds(2),
                ShouldHandle = new PredicateBuilder()
                    .Handle<HttpRequestException>()
                    .Handle<TaskCanceledException>(ex => !ex.CancellationToken.IsCancellationRequested),
                OnRetry = args =>
                {
                    Interlocked.Increment(ref _retryCount);
                    System.Diagnostics.Debug.WriteLine(
                        $"[OllamaClient] Retry #{args.AttemptNumber} nach {args.RetryDelay.TotalSeconds:F1}s — {args.Outcome.Exception?.GetType().Name}: {args.Outcome.Exception?.Message}");
                    return ValueTask.CompletedTask;
                }
            })
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions
            {
                FailureRatio = 1.0,
                SamplingDuration = TimeSpan.FromSeconds(60),
                MinimumThroughput = 5,
                BreakDuration = TimeSpan.FromSeconds(30),
                ShouldHandle = new PredicateBuilder()
                    .Handle<HttpRequestException>()
                    .Handle<TaskCanceledException>(ex => !ex.CancellationToken.IsCancellationRequested),
                OnOpened = _ =>
                {
                    Interlocked.Increment(ref _circuitBreakerTrips);
                    System.Diagnostics.Debug.WriteLine("[OllamaClient] CIRCUIT BREAKER OFFEN — 30s Fast-Fail");
                    return ValueTask.CompletedTask;
                },
                OnClosed = _ =>
                {
                    System.Diagnostics.Debug.WriteLine("[OllamaClient] Circuit Breaker geschlossen — Ollama erreichbar");
                    return ValueTask.CompletedTask;
                }
            })
            .Build();
    }

    public void Dispose()
    {
        if (_ownsHttp)
            _http.Dispose();
    }

    // ======================
    // /api/tags (model list)
    // ======================
    public async Task<IReadOnlyList<string>> ListModelNamesAsync(CancellationToken ct)
    {
        try
        {
            using var resp = await _http.GetAsync("/api/tags", ct).ConfigureAwait(false);
            resp.EnsureSuccessStatusCode();
            var body = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(body);
            var names = new List<string>();
            if (doc.RootElement.TryGetProperty("models", out var arr) && arr.ValueKind == JsonValueKind.Array)
            {
                foreach (var m in arr.EnumerateArray())
                {
                    if (m.TryGetProperty("name", out var n) && n.ValueKind == JsonValueKind.String)
                        names.Add(n.GetString()!);
                }
            }
            return names;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[OllamaClient] ListModelNames fehlgeschlagen: {ex.Message}");
            return Array.Empty<string>();
        }
    }

    // ======================
    // /api/ps (geladene Modelle im VRAM)
    // ======================

    /// <summary>
    /// Gibt die Namen aller aktuell im VRAM geladenen Modelle zurueck.
    /// </summary>
    public async Task<IReadOnlyList<string>> ListLoadedModelsAsync(CancellationToken ct = default)
    {
        try
        {
            using var resp = await _http.GetAsync("/api/ps", ct).ConfigureAwait(false);
            resp.EnsureSuccessStatusCode();
            var body = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(body);
            var names = new List<string>();
            if (doc.RootElement.TryGetProperty("models", out var arr) && arr.ValueKind == JsonValueKind.Array)
            {
                foreach (var m in arr.EnumerateArray())
                {
                    if (m.TryGetProperty("name", out var n) && n.ValueKind == JsonValueKind.String)
                        names.Add(n.GetString()!);
                }
            }
            return names;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[OllamaClient] ListLoadedModels fehlgeschlagen: {ex.Message}");
            return Array.Empty<string>();
        }
    }

    // ==============================
    // Modell-Management (VRAM)
    // ==============================

    /// <summary>
    /// Entlaedt ein Modell aus dem VRAM (keep_alive=0).
    /// Fehler werden ignoriert — Modell war evtl. schon entladen.
    /// </summary>
    public async Task UnloadModelAsync(string model, CancellationToken ct = default)
    {
        var payload = new Dictionary<string, object?>
        {
            ["model"] = model,
            ["prompt"] = "",
            ["stream"] = false,
            ["keep_alive"] = "0"
        };
        try
        {
            var json = JsonSerializer.Serialize(payload);
            using var req = new HttpRequestMessage(HttpMethod.Post, "/api/generate")
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
            using var resp = await _http.SendAsync(req, ct).ConfigureAwait(false);
            System.Diagnostics.Debug.WriteLine(
                $"[OllamaClient] UnloadModel {model}: {(int)resp.StatusCode}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[OllamaClient] UnloadModel {model} fehlgeschlagen: {ex.Message}");
        }
    }

    /// <summary>
    /// Laedt ein Modell in den VRAM vor (leerer Prompt mit keep_alive).
    /// </summary>
    public async Task WarmupModelAsync(string model, int numCtx = 0, CancellationToken ct = default)
    {
        var payload = new Dictionary<string, object?>
        {
            ["model"] = model,
            ["prompt"] = "",
            ["stream"] = false,
            ["keep_alive"] = "-1"   // permanent im VRAM halten
        };
        if (numCtx > 0)
            payload["options"] = new Dictionary<string, object> { ["num_ctx"] = numCtx };
        var json = JsonSerializer.Serialize(payload);
        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/generate")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        using var resp = await _http.SendAsync(req, ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
    }

    // ======================
    // /api/generate (legacy)
    // ======================
    public async Task<string> GenerateAsync(
        string model,
        string prompt,
        IReadOnlyList<string>? imagesBase64,
        CancellationToken ct)
    {
        return await _resiliencePipeline.ExecuteAsync(async token =>
        {
            var payload = new Dictionary<string, object?>
            {
                ["model"] = model,
                ["prompt"] = prompt,
                ["stream"] = false,
                ["keep_alive"] = _keepAlive
            };

            if (imagesBase64 is { Count: > 0 })
                payload["images"] = imagesBase64;

            ApplyNumCtx(payload);

            var json = JsonSerializer.Serialize(payload);
            using var req = new HttpRequestMessage(HttpMethod.Post, "/api/generate")
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };

            using var resp = await _http.SendAsync(req, token).ConfigureAwait(false);
            var body = await resp.Content.ReadAsStringAsync(token).ConfigureAwait(false);

            if (!resp.IsSuccessStatusCode)
                throw new HttpRequestException(
                    $"Ollama /api/generate {(int)resp.StatusCode}: {Truncate(body, 300)}");

            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("response", out var r) && r.ValueKind == JsonValueKind.String)
                return r.GetString() ?? string.Empty;

            return body;
        }, ct).ConfigureAwait(false);
    }

    // ======================
    // /api/chat (plain)
    // ======================

    public sealed record ChatMessage(string Role, string Content, IReadOnlyList<string>? ImagesBase64 = null);

    /// <summary>
    /// Plain /api/chat ohne structured format — empfohlen fuer Vision-Modelle (qwen2.5vl etc.).
    /// </summary>
    public async Task<string> ChatAsync(
        string model,
        IReadOnlyList<ChatMessage> messages,
        CancellationToken ct)
    {
        return await _resiliencePipeline.ExecuteAsync(async token =>
        {
            var msgList = new List<Dictionary<string, object?>>(messages.Count);
            foreach (var m in messages)
            {
                var d = new Dictionary<string, object?>
                {
                    ["role"] = m.Role,
                    ["content"] = m.Content
                };
                if (m.ImagesBase64 is { Count: > 0 })
                    d["images"] = m.ImagesBase64;
                msgList.Add(d);
            }

            var payload = new Dictionary<string, object?>
            {
                ["model"] = model,
                ["messages"] = msgList,
                ["stream"] = false,
                ["keep_alive"] = _keepAlive
            };

            ApplyNumCtx(payload);

            var json = JsonSerializer.Serialize(payload);
            using var req = new HttpRequestMessage(HttpMethod.Post, "/api/chat")
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };

            using var resp = await _http.SendAsync(req, token).ConfigureAwait(false);
            var body = await resp.Content.ReadAsStringAsync(token).ConfigureAwait(false);

            if (!resp.IsSuccessStatusCode)
                throw new HttpRequestException(
                    $"Ollama /api/chat {(int)resp.StatusCode}: {Truncate(body, 300)}");

            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("message", out var msg) &&
                msg.TryGetProperty("content", out var contentEl) &&
                contentEl.ValueKind == JsonValueKind.String)
            {
                return contentEl.GetString() ?? string.Empty;
            }

            return body;
        }, ct).ConfigureAwait(false);
    }

    // ======================
    // /api/chat (structured)
    // ======================

    public async Task<T> ChatStructuredAsync<T>(
        string model,
        IReadOnlyList<ChatMessage> messages,
        JsonElement formatSchema,
        CancellationToken ct)
    {
        return await ChatStructuredWithOptionsAsync<T>(model, messages, formatSchema, null, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Structured chat with optional Ollama options (e.g. temperature).
    /// </summary>
    public async Task<T> ChatStructuredWithOptionsAsync<T>(
        string model,
        IReadOnlyList<ChatMessage> messages,
        JsonElement formatSchema,
        Dictionary<string, object>? options,
        CancellationToken ct)
    {
        return await _resiliencePipeline.ExecuteAsync(async token =>
        {
            var msgList = new List<Dictionary<string, object?>>(messages.Count);
            foreach (var m in messages)
            {
                var d = new Dictionary<string, object?>
                {
                    ["role"] = m.Role,
                    ["content"] = m.Content
                };
                if (m.ImagesBase64 is { Count: > 0 })
                    d["images"] = m.ImagesBase64;
                msgList.Add(d);
            }

            // Ollama expects: { model, messages:[...], stream:false, format: <schema|json> }
            var payload = new Dictionary<string, object?>
            {
                ["model"] = model,
                ["messages"] = msgList,
                ["stream"] = false,
                ["format"] = formatSchema,
                ["keep_alive"] = _keepAlive
            };

            if (options is { Count: > 0 })
                payload["options"] = options;

            ApplyNumCtx(payload);

            var json = JsonSerializer.Serialize(payload);
            using var req = new HttpRequestMessage(HttpMethod.Post, "/api/chat")
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };

            using var resp = await _http.SendAsync(req, token).ConfigureAwait(false);
            var body = await resp.Content.ReadAsStringAsync(token).ConfigureAwait(false);

            if (!resp.IsSuccessStatusCode)
                throw new HttpRequestException(
                    $"Ollama /api/chat {(int)resp.StatusCode}: {Truncate(body, 300)}");

        using var doc = JsonDocument.Parse(body);

        // expected: { message: { role:"assistant", content:"{...json...}" }, ... }
        if (!doc.RootElement.TryGetProperty("message", out var msg) ||
            !msg.TryGetProperty("content", out var contentEl) ||
            contentEl.ValueKind != JsonValueKind.String)
        {
            throw new InvalidOperationException("Ollama /api/chat Antwort ohne message.content");
        }

        var content = contentEl.GetString() ?? "";
        if (string.IsNullOrWhiteSpace(content))
            throw new InvalidOperationException("Ollama /api/chat Antwort: content leer");

        // Debug-Log: Qwen-Rohantwort speichern (temporaer)
        try
        {
            var logDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "SewerStudio", "logs");
            Directory.CreateDirectory(logDir);
            File.AppendAllText(
                Path.Combine(logDir, "qwen_raw_responses.log"),
                $"{DateTime.Now:HH:mm:ss} [{model}] {content[..Math.Min(content.Length, 500)]}\n---\n");
        }
        catch { /* Logging darf nie crashen */ }

        try
        {
            var result = JsonSerializer.Deserialize<T>(content,
                Application.Common.JsonDefaults.CaseInsensitive);

            if (result is null)
                throw new InvalidOperationException("Deserialisierung ergibt null");

            return result;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Structured JSON konnte nicht geparst werden: " + ex.Message + "\nRaw:\n" + content);
        }
        }, ct).ConfigureAwait(false);
    }

    private void ApplyNumCtx(Dictionary<string, object?> payload)
    {
        if (_numCtx <= 0) return;
        if (payload.TryGetValue("options", out var existing) && existing is Dictionary<string, object> opts)
            opts["num_ctx"] = _numCtx;
        else
            payload["options"] = new Dictionary<string, object> { ["num_ctx"] = _numCtx };
    }

    private static string Truncate(string s, int maxLen) =>
        s.Length <= maxLen ? s : s[..maxLen] + "...";
}
