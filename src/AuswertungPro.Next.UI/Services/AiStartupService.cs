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
        => StartAsync(settings, new DefaultAiStartupLauncher(), sidecarScriptPath: null, ct);

    public static async Task<AiStartupResult> StartAsync(
        AppSettings settings,
        IAiStartupLauncher launcher,
        string? sidecarScriptPath = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(launcher);

        var messages = new List<string>();
        var warnings = new List<string>();
        var settingsChanged = ApplyRuntimeDefaults(settings);
        if (settingsChanged)
            messages.Add("KI aktiviert: Ollama, Multi-Model und Sidecar-Konfiguration wurden eingeschaltet.");

        var platform = AiSettingsFactory.Load(AppSettingsAiSettingsProvider.ToSource(settings));
        var preloadRequests = BuildOllamaPreloadRequests(platform.VisionModel, platform.TextModel, platform.EmbedModel, platform.OllamaKeepAlive);
        var modelLabel = BuildModelLabel(preloadRequests.Select(r => r.ModelName), platform.MultiModelEnabled);
        AiRuntimeStatusTracker.MarkStarting(modelLabel);

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
            ollamaReachable = await WaitForOllamaAsync(launcher, platform.OllamaBaseUri, ct)
                .ConfigureAwait(false);
            if (!ollamaReachable)
                warnings.Add("Ollama wurde gestartet, ist aber noch nicht erreichbar. Modelle konnten nicht geladen werden.");
        }

        var preloadedModels = new List<string>();
        if (ollamaReachable)
        {
            foreach (var model in preloadRequests)
            {
                var preload = await launcher.PreloadOllamaModelAsync(platform.OllamaBaseUri, model, ct)
                    .ConfigureAwait(false);
                if (preload.Succeeded)
                    preloadedModels.Add(model.ModelName);
                else
                    warnings.Add($"Ollama-Modell konnte nicht geladen werden ({model.ModelName}): {preload.Error ?? "unbekannter Fehler"}");
            }

            if (preloadedModels.Count > 0)
                messages.Add($"Ollama-Modelle geladen: {string.Join(", ", preloadedModels)}");
        }

        var sidecarHeaders = BuildSidecarHeaders(platform.SidecarToken);
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
                    messages.Add("Vision-Sidecar wird im Hintergrund gestartet.");
                else
                    warnings.Add($"Vision-Sidecar konnte nicht gestartet werden: {error ?? "unbekannter Fehler"}");
            }
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
        var token = NormalizeToken(configuredToken) ?? TryLoadSidecarToken();
        return token is null
            ? null
            : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["X-Sidecar-Token"] = token
            };
    }

    private static string? TryLoadSidecarToken()
    {
        var fromAuthEnv = NormalizeToken(Environment.GetEnvironmentVariable("SEWER_SIDECAR_AUTH_TOKEN"));
        if (fromAuthEnv is not null)
            return fromAuthEnv;

        var fromEnv = NormalizeToken(Environment.GetEnvironmentVariable("SEWER_SIDECAR_TOKEN"));
        if (fromEnv is not null)
            return fromEnv;

        try
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (string.IsNullOrWhiteSpace(localAppData))
                return null;

            var path = Path.Combine(localAppData, AppIdentity.ProductName, ".sidecar_token");
            return File.Exists(path) ? NormalizeToken(File.ReadAllText(path)) : null;
        }
        catch
        {
            return null;
        }
    }

    private static string? NormalizeToken(string? token)
        => string.IsNullOrWhiteSpace(token) ? null : token.Trim();

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
        for (var attempt = 0; attempt < 20; attempt++)
        {
            if (await launcher.IsReachableAsync(baseUri, "/api/tags", headers: null, ct).ConfigureAwait(false))
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
                using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
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
