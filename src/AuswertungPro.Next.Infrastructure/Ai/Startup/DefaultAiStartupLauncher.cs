using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Ai.Startup;

namespace AuswertungPro.Next.Infrastructure.Ai.Startup;

// -----------------------------------------------------------------------
// Standard-Implementierung von IAiStartupLauncher:
// Alle Systemoperationen (HTTP, Prozessstarts, JSON-Parsing) fuer den
// KI-Startvorgang.
// Liegt in Infrastructure, da reale Netzwerk- und Prozessaufrufe erfolgen.
// -----------------------------------------------------------------------

public sealed class DefaultAiStartupLauncher : IAiStartupLauncher
{
    public async Task<bool> IsReachableAsync(
        Uri baseUri,
        string relativePath,
        IReadOnlyDictionary<string, string>? headers,
        CancellationToken ct)
    {
        try
        {
            // 5 s statt 2 s: ein beschaeftigter/GC-pausierter Dienst (Ollama/Sidecar) faellt
            // sonst kurz aus dem Probe und gilt faelschlich als "tot" -> die App startet
            // einen ZWEITEN Prozess (Doppelstart), der die Modelle wegwirft/leer bleibt.
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            using var req = new HttpRequestMessage(HttpMethod.Get, new Uri(baseUri, relativePath));
            if (headers is not null)
            {
                foreach (var pair in headers)
                    req.Headers.TryAddWithoutValidation(pair.Key, pair.Value);
            }

            using var resp = await http.SendAsync(req, ct).ConfigureAwait(false);
            return resp.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public bool TryStart(AiStartupProcessRequest request, out string? error)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = request.FileName,
                Arguments = request.Arguments,
                UseShellExecute = false,
                CreateNoWindow = request.Hidden,
                WindowStyle = request.Hidden ? ProcessWindowStyle.Hidden : ProcessWindowStyle.Normal
            };

            if (!string.IsNullOrWhiteSpace(request.WorkingDirectory))
                startInfo.WorkingDirectory = request.WorkingDirectory;

            Process.Start(startInfo);
            error = null;
            return true;
        }
        catch (Exception ex) when (ex is InvalidOperationException
                                        || ex is System.ComponentModel.Win32Exception
                                        || ex is IOException)
        {
            error = ex.Message;
            return false;
        }
    }

    public async Task<AiStartupModelPreloadResult> PreloadOllamaModelAsync(
        Uri baseUri,
        AiStartupModelPreloadRequest request,
        CancellationToken ct)
    {
        try
        {
            using var http = new HttpClient { BaseAddress = baseUri, Timeout = TimeSpan.FromMinutes(10) };
            var payload = request.Kind == AiStartupModelKind.Embed
                ? JsonSerializer.Serialize(new
                {
                    model = request.ModelName,
                    input = "SewerStudio KI Warmup",
                    keep_alive = request.KeepAlive
                })
                : JsonSerializer.Serialize(new
                {
                    model = request.ModelName,
                    prompt = "",
                    stream = false,
                    keep_alive = request.KeepAlive
                });

            using var req = new HttpRequestMessage(
                HttpMethod.Post,
                request.Kind == AiStartupModelKind.Embed ? "/api/embed" : "/api/generate")
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json")
            };

            using var resp = await http.SendAsync(req, ct).ConfigureAwait(false);
            if (resp.IsSuccessStatusCode)
                return new AiStartupModelPreloadResult(true, null);

            var body = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            return new AiStartupModelPreloadResult(false, $"HTTP {(int)resp.StatusCode}: {Truncate(body, 300)}");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException
                                        || ex is TaskCanceledException
                                        || ex is IOException
                                        || ex is InvalidOperationException)
        {
            return new AiStartupModelPreloadResult(false, ex.Message);
        }
    }

    public async Task<bool?> IsOllamaModelResidentAsync(
        Uri baseUri,
        string modelName,
        CancellationToken ct)
    {
        try
        {
            using var http = new HttpClient { BaseAddress = baseUri, Timeout = TimeSpan.FromSeconds(5) };
            using var resp = await http.GetAsync("/api/ps", ct).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode)
                return null;
            var body = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(body);
            if (!doc.RootElement.TryGetProperty("models", out var arr) || arr.ValueKind != JsonValueKind.Array)
                return false;
            foreach (var m in arr.EnumerateArray())
            {
                var name = m.TryGetProperty("name", out var n) ? n.GetString() : null;
                // Ollama meldet z.B. "qwen3-vl:8b-q8" – Prefix-Vergleich deckt :latest-Varianten ab.
                if (name is not null &&
                    (name.Equals(modelName, StringComparison.OrdinalIgnoreCase)
                     || name.StartsWith(modelName, StringComparison.OrdinalIgnoreCase)
                     || modelName.StartsWith(name.Split(':')[0], StringComparison.OrdinalIgnoreCase)))
                    return true;
            }
            return false;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return null;
        }
    }

    public async Task<AiStartupWarmupResult> WarmupSidecarModelsAsync(
        Uri sidecarBaseUri,
        IReadOnlyDictionary<string, string>? headers,
        CancellationToken ct)
    {
        try
        {
            // Modell-Laden (YOLO-Engine/DINO/SAM) kann beim Kaltstart einige Zehn-Sekunden dauern.
            using var http = new HttpClient { BaseAddress = sidecarBaseUri, Timeout = TimeSpan.FromMinutes(5) };
            using var req = new HttpRequestMessage(HttpMethod.Post, "/warmup");
            if (headers is not null)
            {
                foreach (var pair in headers)
                    req.Headers.TryAddWithoutValidation(pair.Key, pair.Value);
            }

            using var resp = await http.SendAsync(req, ct).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode)
            {
                // Aelterer Sidecar ohne /warmup (404): kein Fehler, Modelle laden dann beim ersten Bild.
                var code = (int)resp.StatusCode;
                return new AiStartupWarmupResult(false, Array.Empty<string>(),
                    code == 404
                        ? "Sidecar kennt /warmup nicht (Modelle laden beim ersten Bild)."
                        : $"HTTP {code}");
            }

            var body = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            return new AiStartupWarmupResult(true, ParseLoadedModels(body), null);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException
                                        || ex is TaskCanceledException
                                        || ex is IOException
                                        || ex is InvalidOperationException)
        {
            return new AiStartupWarmupResult(false, Array.Empty<string>(), ex.Message);
        }
    }

    private static IReadOnlyList<string> ParseLoadedModels(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("loaded", out var arr) && arr.ValueKind == JsonValueKind.Array)
            {
                return arr.EnumerateArray()
                    .Where(e => e.ValueKind == JsonValueKind.String)
                    .Select(e => e.GetString()!)
                    .ToList();
            }
        }
        catch (JsonException)
        {
            // Antwort nicht parsbar – als "keine Modelle" behandeln (best-effort).
        }

        return Array.Empty<string>();
    }

    private static string Truncate(string value, int maxLength)
        => value.Length <= maxLength ? value : value[..maxLength];
}
