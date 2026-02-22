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
public sealed class OllamaClient
{
    private readonly HttpClient _http;

    public OllamaClient(Uri baseUri, HttpClient? http = null)
    {
        _http = http ?? new HttpClient();
        _http.BaseAddress = baseUri;
        _http.Timeout = TimeSpan.FromMinutes(10);
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
            ["stream"] = false
        };

        if (imagesBase64 is { Count: > 0 })
            payload["images"] = imagesBase64;

        var json = JsonSerializer.Serialize(payload);
        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/generate")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        using var resp = await _http.SendAsync(req, ct).ConfigureAwait(false);
        var body = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

        resp.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(body);
        if (doc.RootElement.TryGetProperty("response", out var r) && r.ValueKind == JsonValueKind.String)
            return r.GetString() ?? string.Empty;

        return body;
    }

    // ======================
    // /api/chat (structured)
    // ======================

    public sealed record ChatMessage(string Role, string Content, IReadOnlyList<string>? ImagesBase64 = null);

    public async Task<T> ChatStructuredAsync<T>(
        string model,
        IReadOnlyList<ChatMessage> messages,
        JsonElement formatSchema,
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
            ["format"] = formatSchema
        };

        var json = JsonSerializer.Serialize(payload);
        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/chat")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        using var resp = await _http.SendAsync(req, ct).ConfigureAwait(false);
        var body = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

        resp.EnsureSuccessStatusCode();

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
            var result = JsonSerializer.Deserialize<T>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (result is null)
                throw new InvalidOperationException("Deserialisierung ergibt null");

            return result;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Structured JSON konnte nicht geparst werden: " + ex.Message + "\nRaw:\n" + content);
        }
    }
}
