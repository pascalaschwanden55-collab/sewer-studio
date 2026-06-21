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
using AuswertungPro.Next.Infrastructure.Ai.Configuration;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;

namespace AuswertungPro.Next.UI.Services;

public static class AiStartupService
{
    private const string DefaultOllamaUrl = "http://localhost:11434";
    private const string DefaultSidecarUrl = "http://localhost:8100";

    public static bool ApplyRuntimeDefaults(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var changed = false;

        if (settings.AiEnabled != true)
        {
            settings.AiEnabled = true;
            changed = true;
        }

        if (settings.PipelineMultiModelEnabled != true)
        {
            settings.PipelineMultiModelEnabled = true;
            changed = true;
        }

        if (!string.Equals(settings.PipelineMode, "multimodel", StringComparison.OrdinalIgnoreCase))
        {
            settings.PipelineMode = "multimodel";
            changed = true;
        }

        if (string.IsNullOrWhiteSpace(settings.AiOllamaUrl))
        {
            settings.AiOllamaUrl = DefaultOllamaUrl;
            changed = true;
        }

        if (string.IsNullOrWhiteSpace(settings.PipelineSidecarUrl))
        {
            settings.PipelineSidecarUrl = DefaultSidecarUrl;
            changed = true;
        }

        if (string.IsNullOrWhiteSpace(settings.AiOllamaKeepAlive))
        {
            settings.AiOllamaKeepAlive = "24h";
            changed = true;
        }

        return changed;
    }

    public static Task<AiStartupResult> StartAsync(
        AppSettings settings,
        CancellationToken ct = default)
        => StartAsync(settings, new DefaultAiStartupLauncher(), sidecarScriptPath: null, progress: null, ct);

    public static Task<AiStartupResult> StartAsync(
        AppSettings settings,
        IProgress<string> progress,
        CancellationToken ct = default)
        => StartAsync(settings, new DefaultAiStartupLauncher(), sidecarScriptPath: null, progress, ct);

    public static Task<AiStartupResult> StartAsync(
        AppSettings settings,
        IAiStartupLauncher launcher,
        string? sidecarScriptPath = null,
        CancellationToken ct = default)
        => StartAsync(settings, launcher, sidecarScriptPath, progress: null, ct);

    public static async Task<AiStartupResult> StartAsync(
        AppSettings settings,
        IAiStartupLauncher launcher,
        string? sidecarScriptPath,
        IProgress<string>? progress,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(launcher);

        // Fortschritt live melden, damit der "KI starten"-Knopf waehrend der langen Kaltstart-
        // Phasen (Sidecar bis zu 2 Min, Modell-Laden) nicht stumm wirkt ("haengt").
        void Report(string step) => progress?.Report(step);

        var messages = new List<string>();
        var warnings = new List<string>();
        var settingsChanged = ApplyRuntimeDefaults(settings);
        if (settingsChanged)
            messages.Add("KI aktiviert: Ollama, Multi-Model und Sidecar-Konfiguration wurden eingeschaltet.");

        var platform = AiSettingsFactory.Load(AppSettingsAiSettingsProvider.ToSource(settings));
        var preloadRequests = BuildOllamaPreloadRequests(platform.VisionModel, platform.TextModel, platform.EmbedModel, platform.OllamaKeepAlive);
        var modelLabel = BuildModelLabel(preloadRequests.Select(r => r.ModelName), platform.MultiModelEnabled);
        AiRuntimeStatusTracker.MarkStarting(modelLabel);

        Report("Pruefe Ollama...");
        var ollamaReachable = await launcher
            .IsReachableAsync(platform.OllamaBaseUri, "/api/tags", headers: null, ct)
            .ConfigureAwait(false);

        var ollamaAttempted = false;
        var ollamaStarted = false;
        if (ollamaReachable)
        {
            messages.Add("Ollama ist erreichbar.");
        }
        else
        {
            ollamaAttempted = true;
            ollamaStarted = launcher.TryStart(
                new AiStartupProcessRequest(
                    FileName: "ollama",
                    Arguments: "serve",
                    WorkingDirectory: null,
                    Hidden: true),
                out var error);

            if (ollamaStarted)
                messages.Add("Ollama wird im Hintergrund gestartet.");
            else
                warnings.Add($"Ollama konnte nicht gestartet werden: {error ?? "unbekannter Fehler"}");
        }

        if (!ollamaReachable && ollamaStarted)
        {
            Report("Warte auf Ollama (Kaltstart kann ~40s dauern)...");
            ollamaReachable = await WaitForOllamaAsync(launcher, platform.OllamaBaseUri, ct)
                .ConfigureAwait(false);
            if (!ollamaReachable)
                warnings.Add("Ollama wurde gestartet, ist aber noch nicht erreichbar. Modelle konnten nicht geladen werden.");
        }

        var preloadedModels = new List<string>();
        if (ollamaReachable && preloadRequests.Count > 0)
        {
            Report("Lade Sprachmodelle (Ollama)...");
            foreach (var model in preloadRequests)
            {
                var preload = await launcher.PreloadOllamaModelAsync(platform.OllamaBaseUri, model, ct)
                    .ConfigureAwait(false);
                if (!preload.Succeeded)
                {
                    warnings.Add($"Ollama-Modell konnte nicht geladen werden ({model.ModelName}): {preload.Error ?? "unbekannter Fehler"}");
                    continue;
                }

                // VERIFIZIEREN (User-Fall: Preload meldete ok, Modell war aber NICHT resident).
                // Wenn /api/ps zeigt, dass das Modell nicht im Speicher ist, EINMAL nachladen.
                var resident = await launcher.IsOllamaModelResidentAsync(platform.OllamaBaseUri, model.ModelName, ct)
                    .ConfigureAwait(false);
                if (resident == false)
                {
                    Report($"Ollama-Modell {model.ModelName} nicht resident - lade nach...");
                    await launcher.PreloadOllamaModelAsync(platform.OllamaBaseUri, model, ct).ConfigureAwait(false);
                    resident = await launcher.IsOllamaModelResidentAsync(platform.OllamaBaseUri, model.ModelName, ct)
                        .ConfigureAwait(false);
                }

                if (resident == false)
                    warnings.Add($"Ollama-Modell {model.ModelName} ist nach dem Laden NICHT im Speicher (nicht resident).");
                else
                    preloadedModels.Add(model.ModelName); // true ODER null (Pruefung nicht moeglich) -> als geladen werten
            }

            if (preloadedModels.Count > 0)
                messages.Add($"Ollama-Modelle geladen: {string.Join(", ", preloadedModels)}");
        }

        var sidecarHeaders = BuildSidecarHeaders(platform.SidecarToken);
        Report("Pruefe Vision-Sidecar...");
        var sidecarReachable = await launcher
            .IsReachableAsync(platform.SidecarUrl, "/health", sidecarHeaders, ct)
            .ConfigureAwait(false);

        var sidecarAttempted = false;
        var sidecarStarted = false;
        if (sidecarReachable)
        {
            messages.Add("Sidecar ist erreichbar.");
        }
        else
        {
            var script = sidecarScriptPath ?? FindDefaultSidecarScript();
            if (string.IsNullOrWhiteSpace(script) || !File.Exists(script))
            {
                warnings.Add("Sidecar-Startskript nicht gefunden: sidecar\\start_sidecar.ps1");
            }
            else
            {
                sidecarAttempted = true;
                sidecarStarted = launcher.TryStart(
                    new AiStartupProcessRequest(
                        FileName: ResolvePowerShellExe(),
                        Arguments: $"-NoProfile -ExecutionPolicy Bypass -File {Quote(script)}",
                        WorkingDirectory: Path.GetDirectoryName(script),
                        Hidden: true),
                    out var error);

                if (sidecarStarted)
                {
                    Report("Vision-Sidecar wird gestartet...");
                    messages.Add("Vision-Sidecar wird im Hintergrund gestartet.");
                }
                else
                    warnings.Add($"Vision-Sidecar konnte nicht gestartet werden: {error ?? "unbekannter Fehler"}");
            }
        }

        // Nach dem Start auf den Sidecar warten (analog zu Ollama), damit "KI starten" EHRLICH
        // meldet, ob er wirklich oben ist – statt nur "wird gestartet".
        if (!sidecarReachable && sidecarStarted)
        {
            Report("Warte auf Vision-Sidecar (Kaltstart inkl. TensorRT kann 1-2 Min dauern)...");
            sidecarReachable = await WaitForReachableAsync(launcher, platform.SidecarUrl, "/health", sidecarHeaders, ct)
                .ConfigureAwait(false);
            if (!sidecarReachable)
                warnings.Add("Vision-Sidecar wurde gestartet, ist aber noch nicht erreichbar. Modelle konnten nicht geladen werden.");
        }

        // Vision-Modelle (YOLO/Classifier/DINO/SAM) vorab laden, damit die erste Analyse keinen Lade-Verzug hat.
        // ROBUST: bis zu 3 Versuche, bis ALLE erwarteten Modelle geladen sind. Ein einzelnes Modell
        // (z.B. YOLO-TensorRT-Engine) kann beim allerersten Versuch noch klemmen; ein erneuter /warmup
        // ist idempotent und holt das fehlende Modell nach. So ist nach "KI starten" wirklich alles oben.
        if (sidecarReachable)
        {
            Report("Lade Vision-Modelle (YOLO, DINO, SAM, Klassifikator)...");
            var expected = new[] { "yolo", "classifier", "dino", "sam" };
            var loadedAll = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string? lastError = null;

            for (var attempt = 1; attempt <= 3; attempt++)
            {
                if (attempt > 1)
                    Report($"Lade Vision-Modelle, Versuch {attempt}/3...");
                var warm = await launcher.WarmupSidecarModelsAsync(platform.SidecarUrl, sidecarHeaders, ct).ConfigureAwait(false);
                foreach (var m in warm.LoadedModels)
                    loadedAll.Add(m);
                lastError = warm.Succeeded ? null : warm.Error;

                // 404 = alter Sidecar ohne /warmup -> Retry zwecklos, abbrechen.
                if (!warm.Succeeded && warm.Error is not null && warm.Error.Contains("/warmup", StringComparison.OrdinalIgnoreCase))
                    break;
                // Alle erwarteten Modelle oben -> fertig.
                if (expected.All(loadedAll.Contains))
                    break;
                // Sonst kurz warten und erneut versuchen (idempotent).
                if (attempt < 3)
                    await Task.Delay(TimeSpan.FromSeconds(2), ct).ConfigureAwait(false);
            }

            if (loadedAll.Count > 0)
                messages.Add($"Vision-Modelle geladen: {string.Join(", ", loadedAll)}");

            var missing = expected.Where(m => !loadedAll.Contains(m)).ToList();
            if (missing.Count > 0)
                warnings.Add($"Vision-Modelle NICHT geladen: {string.Join(", ", missing)}"
                    + (lastError is not null ? $" ({lastError})" : ""));
        }

        var result = new AiStartupResult(
            SettingsChanged: settingsChanged,
            OllamaReachable: ollamaReachable,
            OllamaStartAttempted: ollamaAttempted,
            OllamaStartSucceeded: ollamaStarted,
            SidecarReachable: sidecarReachable,
            SidecarStartAttempted: sidecarAttempted,
            SidecarStartSucceeded: sidecarStarted,
            PreloadedModels: preloadedModels,
            Messages: messages,
            Warnings: warnings);

        Report(result.HasWarnings ? "KI gestartet (mit Warnung)" : "KI bereit");
        AiRuntimeStatusTracker.MarkReady(modelLabel, result.HasWarnings, BuildRuntimeStatusText(result));
        return result;
    }

    public static string? FindDefaultSidecarScript()
    {
        foreach (var start in new[] { AppContext.BaseDirectory, Environment.CurrentDirectory }
                     .Where(p => !string.IsNullOrWhiteSpace(p))
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var dir = new DirectoryInfo(start);
            while (dir is not null)
            {
                var candidate = Path.Combine(dir.FullName, "sidecar", "start_sidecar.ps1");
                if (File.Exists(candidate))
                    return candidate;

                dir = dir.Parent;
            }
        }

        return null;
    }

    private static IReadOnlyDictionary<string, string>? BuildSidecarHeaders(string? configuredToken)
    {
        var token = SidecarTokenResolver.Resolve(configuredToken);
        return token is null
            ? null
            : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [SidecarTokenResolver.HeaderName] = token
            };
    }

    private static string BuildModelLabel(IEnumerable<string> modelNames, bool multiModelEnabled)
    {
        var models = modelNames
            .Where(m => !string.IsNullOrWhiteSpace(m))
            .Select(m => m.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var label = models.Length == 0 ? "Qwen-VL" : string.Join(" + ", models);
        return multiModelEnabled ? $"{label} + Sidecar" : label;
    }

    private static IReadOnlyList<AiStartupModelPreloadRequest> BuildOllamaPreloadRequests(
        string? visionModel,
        string? textModel,
        string? embedModel,
        string keepAlive)
    {
        var requests = new List<AiStartupModelPreloadRequest>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        Add(visionModel, AiStartupModelKind.Generate);
        Add(textModel, AiStartupModelKind.Generate);
        Add(embedModel, AiStartupModelKind.Embed);

        return requests;

        void Add(string? modelName, AiStartupModelKind kind)
        {
            if (string.IsNullOrWhiteSpace(modelName))
                return;

            var trimmed = modelName.Trim();
            if (!seen.Add(trimmed))
                return;

            requests.Add(new AiStartupModelPreloadRequest(trimmed, kind, keepAlive));
        }
    }

    private static string BuildRuntimeStatusText(AiStartupResult result)
    {
        if (result.HasWarnings)
            return "KI gestartet mit Warnung";

        if (result.PreloadedModels.Count > 0)
            return "Modelle geladen";

        return result.OllamaStartAttempted || result.SidecarStartAttempted
            ? "KI gestartet"
            : "KI bereit";
    }

    private static async Task<bool> WaitForOllamaAsync(
        IAiStartupLauncher launcher,
        Uri baseUri,
        CancellationToken ct)
    {
        // Ollama braucht beim Kaltstart laenger, als man denkt: allein die GPU-Discovery
        // (CUDA-Geraet finden, VRAM ermitteln) dauert auf einer dedizierten GPU ~13 s, BEVOR
        // /api/tags antwortet. Mit nur 10 s (20×500ms) gab die App zu frueh auf ("Ollama
        // gestartet, aber nicht erreichbar — Modelle nicht geladen"), obwohl der Server gleich
        // danach bereit war. 80×500ms = 40 s deckt den Kaltstart sicher ab (analog Sidecar-Warten).
        for (var attempt = 0; attempt < 80; attempt++)
        {
            if (await launcher.IsReachableAsync(baseUri, "/api/tags", headers: null, ct).ConfigureAwait(false))
                return true;

            await Task.Delay(TimeSpan.FromMilliseconds(500), ct).ConfigureAwait(false);
        }

        return false;
    }

    private static async Task<bool> WaitForReachableAsync(
        IAiStartupLauncher launcher,
        Uri baseUri,
        string relativePath,
        IReadOnlyDictionary<string, string>? headers,
        CancellationToken ct)
    {
        // Sidecar-Kaltstart dauert laenger, als 30s abdecken: Python laedt torch + ultralytics +
        // TensorRT, und die YOLO-Engine-Initialisierung kann allein 30-60s brauchen, BEVOR /health
        // antwortet. Mit 60×500ms=30s gab die App zu frueh auf ("Sidecar nicht erreichbar — Modelle
        // nicht geladen") und uebersprang dadurch den /warmup, sodass YOLO/DINO nie geladen wurden.
        // 240×500ms = 120s deckt den Kaltstart inkl. TensorRT sicher ab.
        for (var attempt = 0; attempt < 240; attempt++)
        {
            if (await launcher.IsReachableAsync(baseUri, relativePath, headers, ct).ConfigureAwait(false))
                return true;

            await Task.Delay(TimeSpan.FromMilliseconds(500), ct).ConfigureAwait(false);
        }

        return false;
    }

    private static string ResolvePowerShellExe()
    {
        var windowsDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        if (!string.IsNullOrWhiteSpace(windowsDir))
        {
            var candidate = Path.Combine(windowsDir, "System32", "WindowsPowerShell", "v1.0", "powershell.exe");
            if (File.Exists(candidate))
                return candidate;
        }

        return "powershell.exe";
    }

    private static string Quote(string value)
        => "\"" + value.Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";

    private sealed class DefaultAiStartupLauncher : IAiStartupLauncher
    {
        public async Task<bool> IsReachableAsync(
            Uri baseUri,
            string relativePath,
            IReadOnlyDictionary<string, string>? headers,
            CancellationToken ct)
        {
            try
            {
                // 5s statt 2s: ein beschaeftigter/GC-pausierter Dienst (Ollama/Sidecar) faellt
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
            catch (Exception ex) when (ex is InvalidOperationException || ex is System.ComponentModel.Win32Exception || ex is IOException)
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
            catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException || ex is IOException || ex is InvalidOperationException)
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
                        code == 404 ? "Sidecar kennt /warmup nicht (Modelle laden beim ersten Bild)." : $"HTTP {code}");
                }

                var body = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                return new AiStartupWarmupResult(true, ParseLoadedModels(body), null);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException || ex is IOException || ex is InvalidOperationException)
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
    }

    private static string Truncate(string value, int maxLength)
        => value.Length <= maxLength ? value : value[..maxLength];
}

public interface IAiStartupLauncher
{
    Task<bool> IsReachableAsync(
        Uri baseUri,
        string relativePath,
        IReadOnlyDictionary<string, string>? headers,
        CancellationToken ct);

    bool TryStart(AiStartupProcessRequest request, out string? error);

    Task<AiStartupModelPreloadResult> PreloadOllamaModelAsync(
        Uri baseUri,
        AiStartupModelPreloadRequest request,
        CancellationToken ct);

    /// <summary>Prueft via /api/ps, ob das Modell wirklich im Speicher resident ist
    /// (nicht nur "Preload meldete ok"). null = Pruefung nicht moeglich/Fehler.</summary>
    Task<bool?> IsOllamaModelResidentAsync(
        Uri baseUri,
        string modelName,
        CancellationToken ct);

    Task<AiStartupWarmupResult> WarmupSidecarModelsAsync(
        Uri sidecarBaseUri,
        IReadOnlyDictionary<string, string>? headers,
        CancellationToken ct);
}

public enum AiStartupModelKind
{
    Generate,
    Embed
}

public sealed record AiStartupProcessRequest(
    string FileName,
    string Arguments,
    string? WorkingDirectory,
    bool Hidden);

public sealed record AiStartupModelPreloadRequest(
    string ModelName,
    AiStartupModelKind Kind,
    string KeepAlive);

public sealed record AiStartupModelPreloadResult(
    bool Succeeded,
    string? Error);

public sealed record AiStartupWarmupResult(
    bool Succeeded,
    IReadOnlyList<string> LoadedModels,
    string? Error);

public sealed record AiStartupResult(
    bool SettingsChanged,
    bool OllamaReachable,
    bool OllamaStartAttempted,
    bool OllamaStartSucceeded,
    bool SidecarReachable,
    bool SidecarStartAttempted,
    bool SidecarStartSucceeded,
    IReadOnlyList<string> PreloadedModels,
    IReadOnlyList<string> Messages,
    IReadOnlyList<string> Warnings)
{
    public bool HasWarnings => Warnings.Count > 0;

    public string Summary
    {
        get
        {
            var lines = Messages.Concat(Warnings.Select(w => "Warnung: " + w)).ToArray();
            return lines.Length == 0 ? "Keine Aktion notwendig." : string.Join(Environment.NewLine, lines);
        }
    }
}
