// AuswertungPro – KI Videoanalyse Modul
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace AuswertungPro.Next.UI.Ai.Ollama;

/// <summary>
/// Prüft ob Ollama erreichbar ist und welche Modelle verfügbar sind.
/// </summary>
public sealed class OllamaHealthCheck(HttpClient http, OllamaConfig config)
{
    /// <summary>Prüft Verbindung und gibt verfügbare Modelle zurück.</summary>
    public async Task<HealthResult> CheckAsync(CancellationToken ct = default)
    {
        try
        {
            var url = new Uri(config.BaseUri, "/api/tags");
            using var resp = await http.GetAsync(url, ct).ConfigureAwait(false);

            if (!resp.IsSuccessStatusCode)
                return HealthResult.Fail($"HTTP {(int)resp.StatusCode}: {resp.ReasonPhrase}");

            var json = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            var models = ParseModels(json);
            return HealthResult.Ok(models);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            return HealthResult.Fail(
                $"Ollama nicht erreichbar ({config.BaseUri}): {ex.Message}");
        }
    }

    /// <summary>True wenn das gewünschte Modell verfügbar ist.</summary>
    public async Task<bool> IsModelAvailableAsync(string modelName, CancellationToken ct = default)
    {
        var result = await CheckAsync(ct).ConfigureAwait(false);
        if (!result.IsOnline) return false;

        foreach (var m in result.AvailableModels)
        {
            if (m.StartsWith(modelName, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private static IReadOnlyList<string> ParseModels(string json)
    {
        var list = new List<string>();
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("models", out var models))
            {
                foreach (var m in models.EnumerateArray())
                {
                    if (m.TryGetProperty("name", out var name))
                        list.Add(name.GetString() ?? "");
                }
            }
        }
        catch { /* Parsing-Fehler ignorieren */ }
        return list;
    }
}

/// <summary>Ergebnis eines Health-Checks.</summary>
public sealed record HealthResult
{
    public bool IsOnline { get; private init; }
    public string Error  { get; private init; } = "";
    public IReadOnlyList<string> AvailableModels { get; private init; } = [];

    public static HealthResult Ok(IReadOnlyList<string> models)
        => new() { IsOnline = true, AvailableModels = models };

    public static HealthResult Fail(string error)
        => new() { IsOnline = false, Error = error };
}
