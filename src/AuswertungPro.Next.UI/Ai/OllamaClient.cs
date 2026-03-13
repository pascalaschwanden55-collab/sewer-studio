using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace AuswertungPro.Next.UI.Ai;

/// <summary>
/// Minimaler Ollama HTTP Client.
/// - /api/generate (legacy text/vision)
/// - /api/chat (structured output mit JSON schema + images in messages)
/// </summary>
public sealed class OllamaClient : IDisposable
{
    private readonly HttpClient _http;
    private readonly bool _ownsHttp;
    private readonly Uri _baseUri;
    private readonly string _keepAlive;
    private readonly int _numCtx;

    public OllamaClient(Uri baseUri, HttpClient? http = null, TimeSpan? ownedTimeout = null, string keepAlive = "24h", int numCtx = 0)
    {
        _ownsHttp = http is null;
        _baseUri = baseUri;
        _http = http ?? new HttpClient();
        // BaseAddress nur setzen wenn wir den HttpClient selbst erstellt haben,
        // da ein bereits benutzter HttpClient keine Property-Aenderungen erlaubt.
        if (_ownsHttp)
        {
            _http.BaseAddress = baseUri;
            _http.Timeout = ownedTimeout is { } timeout && timeout > TimeSpan.Zero
                ? timeout
                : TimeSpan.FromMinutes(5);
        }
        _keepAlive = keepAlive;
        _numCtx = numCtx;
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
            using var resp = await _http.GetAsync(ResolveUri("/api/tags"), ct).ConfigureAwait(false);
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
    // /api/generate (legacy)
    // ======================
    public async Task<string> GenerateAsync(
        string model,
        string prompt,
        IReadOnlyList<string>? imagesBase64,
        CancellationToken ct)
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
        using var req = new HttpRequestMessage(HttpMethod.Post, ResolveUri("/api/generate"))
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        using var resp = await _http.SendAsync(req, ct).ConfigureAwait(false);
        var body = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

        if (!resp.IsSuccessStatusCode)
            throw new HttpRequestException(
                $"Ollama /api/generate {(int)resp.StatusCode}: {Truncate(body, 300)}");

        using var doc = JsonDocument.Parse(body);
        if (doc.RootElement.TryGetProperty("response", out var r) && r.ValueKind == JsonValueKind.String)
            return r.GetString() ?? string.Empty;

        return body;
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
        using var req = new HttpRequestMessage(HttpMethod.Post, ResolveUri("/api/chat"))
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        using var resp = await _http.SendAsync(req, ct).ConfigureAwait(false);
        var body = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

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
        using var req = new HttpRequestMessage(HttpMethod.Post, ResolveUri("/api/chat"))
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        using var resp = await _http.SendAsync(req, ct).ConfigureAwait(false);
        var body = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

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
    }

    /// <summary>Erstellt die vollstaendige URI fuer einen API-Pfad.</summary>
    private Uri ResolveUri(string path) => new(_baseUri, path);

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
