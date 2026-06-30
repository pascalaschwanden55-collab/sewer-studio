using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AuswertungPro.Next.Application.Ai.Startup;

// -----------------------------------------------------------------------
// Orchestriert die komplette KI-Startsequenz:
//   1. Ollama pruefen / starten / warten / Modelle preloaden
//   2. Sidecar pruefen / starten / warten / Modelle warmen
//
// Alle Systemoperationen (HTTP, Prozessstarts) laufen ueber IAiStartupLauncher,
// damit der Orchestrator ohne echte Netzwerkaufrufe testbar bleibt.
//
// Timeout-Konstanten sind bewusst konservativ gewaehlt:
//   - Ollama:  80 × 500 ms = 40 s (GPU-Discovery + CUDA-Init)
//   - Sidecar: 240 × 500 ms = 120 s (Python/torch/TensorRT-Ladezeit)
// -----------------------------------------------------------------------

public static class AiStartupOrchestrator
{
    // Ollama braucht beim Kaltstart laenger als man denkt: allein die GPU-Discovery
    // (CUDA-Geraet finden, VRAM ermitteln) dauert auf einer dedizierten GPU ~13 s,
    // BEVOR /api/tags antwortet. 80 × 500 ms = 40 s deckt den Kaltstart sicher ab.
    private const int OllamaMaxAttempts = 80;

    // Sidecar-Kaltstart: Python laedt torch + ultralytics + TensorRT; die YOLO-
    // Engine-Initialisierung kann allein 30-60 s brauchen, BEVOR /health antwortet.
    // 240 × 500 ms = 120 s deckt den Kaltstart inkl. TensorRT sicher ab.
    private const int SidecarMaxAttempts = 240;

    private const int PollIntervalMs = 500;

    /// <summary>
    /// Fuehrt den vollstaendigen KI-Startvorgang durch und gibt ein strukturiertes
    /// Ergebnis zurueck. Der Aufrufer (UI) kann ueber <paramref name="progress"/>
    /// Fortschrittsmeldungen empfangen.
    /// </summary>
    public static async Task<AiStartupResult> StartAsync(
        AiStartupOrchestratorInput input,
        IAiStartupLauncher launcher,
        IProgress<string>? progress,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(launcher);

        // Fortschritt live melden, damit der "KI starten"-Knopf waehrend der langen
        // Kaltstart-Phasen (Sidecar bis zu 2 Min, Modell-Laden) nicht stumm wirkt.
        void Report(string step) => progress?.Report(step);

        var messages = new List<string>();
        var warnings = new List<string>();

        // Einstellungen wurden automatisch aktiviert -> Nutzer informieren.
        if (input.SettingsChanged)
            messages.Add("KI aktiviert: Ollama, Multi-Model und Sidecar-Konfiguration wurden eingeschaltet.");

        // ------------------------------------------------------------------ Ollama
        Report("Pruefe Ollama...");
        var ollamaReachable = await launcher
            .IsReachableAsync(input.OllamaBaseUri, "/api/tags", headers: null, ct)
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
            ollamaReachable = await WaitForReachableAsync(
                launcher, input.OllamaBaseUri, "/api/tags", headers: null, OllamaMaxAttempts, ct)
                .ConfigureAwait(false);
            if (!ollamaReachable)
                warnings.Add("Ollama wurde gestartet, ist aber noch nicht erreichbar. Modelle konnten nicht geladen werden.");
        }

        // ------------------------------------------------------------------ Ollama-Modelle
        var preloadedModels = new List<string>();
        if (ollamaReachable && input.PreloadRequests.Count > 0)
        {
            Report("Lade Sprachmodelle (Ollama)...");
            foreach (var model in input.PreloadRequests)
            {
                var preload = await launcher.PreloadOllamaModelAsync(input.OllamaBaseUri, model, ct)
                    .ConfigureAwait(false);
                if (!preload.Succeeded)
                {
                    warnings.Add($"Ollama-Modell konnte nicht geladen werden ({model.ModelName}): {preload.Error ?? "unbekannter Fehler"}");
                    continue;
                }

                // VERIFIZIEREN (User-Fall: Preload meldete ok, Modell war aber NICHT resident).
                // Wenn /api/ps zeigt, dass das Modell nicht im Speicher ist, EINMAL nachladen.
                var resident = await launcher.IsOllamaModelResidentAsync(input.OllamaBaseUri, model.ModelName, ct)
                    .ConfigureAwait(false);
                if (resident == false)
                {
                    Report($"Ollama-Modell {model.ModelName} nicht resident - lade nach...");
                    await launcher.PreloadOllamaModelAsync(input.OllamaBaseUri, model, ct).ConfigureAwait(false);
                    resident = await launcher.IsOllamaModelResidentAsync(input.OllamaBaseUri, model.ModelName, ct)
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

        // ------------------------------------------------------------------ Sidecar
        Report("Pruefe Vision-Sidecar...");
        var sidecarReachable = await launcher
            .IsReachableAsync(input.SidecarUrl, "/health", input.SidecarHeaders, ct)
            .ConfigureAwait(false);

        var sidecarAttempted = false;
        var sidecarStarted = false;
        if (sidecarReachable)
        {
            messages.Add("Sidecar ist erreichbar.");
        }
        else
        {
            var script = input.SidecarScriptPath;
            if (string.IsNullOrWhiteSpace(script) || !System.IO.File.Exists(script))
            {
                warnings.Add("Sidecar-Startskript nicht gefunden: sidecar\\start_sidecar.ps1");
            }
            else
            {
                sidecarAttempted = true;
                sidecarStarted = launcher.TryStart(
                    new AiStartupProcessRequest(
                        FileName: input.PowerShellExe,
                        Arguments: $"-NoProfile -ExecutionPolicy Bypass -File {Quote(script)}",
                        WorkingDirectory: System.IO.Path.GetDirectoryName(script),
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
            sidecarReachable = await WaitForReachableAsync(
                launcher, input.SidecarUrl, "/health", input.SidecarHeaders, SidecarMaxAttempts, ct)
                .ConfigureAwait(false);
            if (!sidecarReachable)
                warnings.Add("Vision-Sidecar wurde gestartet, ist aber noch nicht erreichbar. Modelle konnten nicht geladen werden.");
        }

        // ------------------------------------------------------------------ Vision-Modelle warmen
        // Vision-Modelle (YOLO/Classifier/DINO/SAM) vorab laden, damit die erste Analyse keinen
        // Lade-Verzug hat. ROBUST: bis zu 3 Versuche, bis ALLE erwarteten Modelle geladen sind.
        // Ein einzelnes Modell (z.B. YOLO-TensorRT-Engine) kann beim allerersten Versuch noch
        // klemmen; ein erneuter /warmup ist idempotent und holt das fehlende Modell nach.
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
                var warm = await launcher.WarmupSidecarModelsAsync(input.SidecarUrl, input.SidecarHeaders, ct).ConfigureAwait(false);
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

        return new AiStartupResult(
            SettingsChanged: input.SettingsChanged,
            OllamaReachable: ollamaReachable,
            OllamaStartAttempted: ollamaAttempted,
            OllamaStartSucceeded: ollamaStarted,
            SidecarReachable: sidecarReachable,
            SidecarStartAttempted: sidecarAttempted,
            SidecarStartSucceeded: sidecarStarted,
            PreloadedModels: preloadedModels,
            Messages: messages,
            Warnings: warnings);
    }

    // ------------------------------------------------------------------ Hilfsmethoden

    private static async Task<bool> WaitForReachableAsync(
        IAiStartupLauncher launcher,
        Uri baseUri,
        string relativePath,
        IReadOnlyDictionary<string, string>? headers,
        int maxAttempts,
        CancellationToken ct)
    {
        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            if (await launcher.IsReachableAsync(baseUri, relativePath, headers, ct).ConfigureAwait(false))
                return true;

            await Task.Delay(TimeSpan.FromMilliseconds(PollIntervalMs), ct).ConfigureAwait(false);
        }

        return false;
    }

    private static string Quote(string value)
        => "\"" + value.Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";
}

/// <summary>
/// Eingabe fuer den AiStartupOrchestrator. Alle Laufzeit-Werte werden vom
/// Aufrufer (AiStartupService in UI) vorberechnet und uebergeben.
/// </summary>
public sealed record AiStartupOrchestratorInput(
    Uri OllamaBaseUri,
    Uri SidecarUrl,
    IReadOnlyDictionary<string, string>? SidecarHeaders,
    IReadOnlyList<AiStartupModelPreloadRequest> PreloadRequests,
    string? SidecarScriptPath,
    string PowerShellExe,
    bool SettingsChanged);
