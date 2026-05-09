using System;
using AuswertungPro.Next.Application.Ai;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using AuswertungPro.Next.Infrastructure.Ai.Ollama;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Pipeline;

namespace AuswertungPro.Next.UI.Modules;

/// <summary>
/// Phase 5.2.E: KI-Pipeline-Setup aus ServiceProvider extrahiert.
///
/// Verantwortlich fuer:
///   1. PythonSidecarService-Erzeugung (Pfad-Resolution + Konstruktor)
///   2. Background-Warmup-Task (Sidecar-Wait, VisionModel/EmbedModel/ReferenceModel
///      vorladen, VRAM-Verifikation)
///
/// Reihenfolge im Warmup ist wichtig: Sidecar zuerst (laedt YOLO+SAM ~3-4 GB),
/// erst dann Qwen — sonst Ollama-CPU-Fallback bei knappem VRAM.
/// </summary>
internal static class AiPipelineModule
{
    /// <summary>
    /// Sucht den Sidecar-Ordner relativ zur App und erzeugt PythonSidecarService.
    /// </summary>
    public static PythonSidecarService CreateSidecar(
        PipelineConfig pipelineCfg,
        ILoggerFactory loggerFactory,
        IHttpClientFactory? httpFactory = null)
    {
        // Sidecar-Pfad: zuerst 5 Ebenen ueber AppContext.BaseDirectory (Repo-Root in Dev),
        // dann Fallback auf BaseDirectory/sidecar (Deploy-Layout).
        var sidecarDir = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "sidecar");
        if (!Directory.Exists(sidecarDir))
            sidecarDir = Path.Combine(AppContext.BaseDirectory, "sidecar");

        return new PythonSidecarService(
            loggerFactory.CreateLogger<PythonSidecarService>(),
            Path.GetFullPath(sidecarDir),
            pipelineCfg.SidecarUrl.Host,
            pipelineCfg.SidecarUrl.Port,
            httpFactory);
    }

    /// <summary>
    /// Startet den Modell-Warmup als Hintergrund-Task. Blockiert NICHT.
    ///
    /// Schritt-Reihenfolge:
    ///   0. Auf Sidecar warten (max 60s)
    ///   1. VisionModel (Qwen-8B) auf GPU vorladen (num_gpu=-1)
    ///   2. EmbedModel (nomic) auf GPU vorladen
    ///   3. ReferenceModel (Qwen-32B) hybrid (num_gpu=10, Rest RAM)
    ///   4. VRAM-Verifikation via /api/ps
    /// </summary>
    public static void RunWarmupInBackground(
        AiRuntimeConfig cfg,
        PythonSidecarService sidecar,
        ILogger logger)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                // 0. Auf Sidecar warten — verhindert parallelen VRAM-Kampf,
                // bei dem Ollama bei knappen VRAM-Budgets auf CPU zurueckfaellt.
                for (int i = 0; i < 60 && !sidecar.IsAvailable; i++)
                    await Task.Delay(1000);
                if (!sidecar.IsAvailable)
                    logger.LogWarning(
                        "[Startup] Sidecar nach 60s nicht verfuegbar — Qwen-Warmup laeuft trotzdem");

                using var warmupClient = cfg.CreateOllamaClient();

                // 1. VisionModel permanent vorladen — num_gpu=-1 zwingt GPU
                await warmupClient.WarmupModelAsync(cfg.VisionModel, cfg.OllamaNumCtx, numGpu: -1);
                logger.LogInformation(
                    "[Startup] VisionModel {Model} vorgeladen (num_gpu=all, NUM_PARALLEL={Parallel}, ctx={Ctx})",
                    cfg.VisionModel,
                    Environment.GetEnvironmentVariable("OLLAMA_NUM_PARALLEL") ?? "?",
                    cfg.OllamaNumCtx);

                // 2. EmbedModel vorladen — klein, immer auf GPU
                if (!string.IsNullOrEmpty(cfg.EmbedModel))
                {
                    await warmupClient.WarmupModelAsync(cfg.EmbedModel, 0, numGpu: -1);
                    logger.LogInformation(
                        "[Startup] EmbedModel {Model} vorgeladen (num_gpu=all)", cfg.EmbedModel);
                }

                // 3. ReferenceModel (32B) hybrid: num_gpu=10 Layers + Rest RAM
                // (~9s statt 28s bei num_gpu=0).
                if (!string.IsNullOrEmpty(cfg.ReferenceVisionModel)
                    && !string.Equals(cfg.ReferenceVisionModel, cfg.VisionModel, StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        using var warmupHttp = new HttpClient
                        {
                            BaseAddress = cfg.OllamaBaseUri,
                            Timeout = TimeSpan.FromMinutes(10)
                        };
                        var payload = new Dictionary<string, object?>
                        {
                            ["model"] = cfg.ReferenceVisionModel,
                            ["prompt"] = "",
                            ["stream"] = false,
                            ["keep_alive"] = "8760h",
                            ["options"] = new Dictionary<string, object>
                            {
                                ["num_gpu"] = 10,
                                ["num_ctx"] = cfg.OllamaNumCtx > 0 ? Math.Min(cfg.OllamaNumCtx, 4096) : 4096
                            }
                        };
                        var json = System.Text.Json.JsonSerializer.Serialize(payload);
                        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/generate")
                        {
                            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
                        };
                        using var resp = await warmupHttp.SendAsync(req).ConfigureAwait(false);
                        logger.LogInformation(
                            "[Startup] ReferenceModel {Model} vorgeladen (num_gpu=10 hybrid, komplett RAM)",
                            cfg.ReferenceVisionModel);
                    }
                    catch (Exception exRef)
                    {
                        logger.LogWarning(exRef,
                            "[Startup] ReferenceModel {Model} Warmup fehlgeschlagen",
                            cfg.ReferenceVisionModel);
                    }
                }

                // 4. VRAM-Verifikation: liegt das VisionModel wirklich auf GPU?
                await VerifyModelInVramAsync(cfg.OllamaBaseUri, cfg.VisionModel, logger);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "[Startup] Modell-Warmup fehlgeschlagen");
            }
        });
    }

    /// <summary>
    /// Prueft via /api/ps ob das Modell tatsaechlich im VRAM liegt.
    /// Wenn size_vram==0 → Ollama hat auf CPU zurueckgefallen (VRAM zu knapp).
    /// Loggt nur — wirft nicht.
    /// </summary>
    private static async Task VerifyModelInVramAsync(Uri ollamaBaseUri, string model, ILogger logger)
    {
        try
        {
            using var http = new HttpClient
            {
                BaseAddress = ollamaBaseUri,
                Timeout = TimeSpan.FromSeconds(5)
            };
            using var resp = await http.GetAsync("/api/ps").ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode) return;

            var body = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
            using var doc = System.Text.Json.JsonDocument.Parse(body);
            if (!doc.RootElement.TryGetProperty("models", out var models)) return;

            var modelPrefix = model.Split(':')[0];
            foreach (var m in models.EnumerateArray())
            {
                var name = m.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                if (!name.Equals(model, StringComparison.OrdinalIgnoreCase)
                    && !name.StartsWith(modelPrefix, StringComparison.OrdinalIgnoreCase))
                    continue;

                long sizeVram = m.TryGetProperty("size_vram", out var v) ? v.GetInt64() : 0;
                long sizeTotal = m.TryGetProperty("size", out var s) ? s.GetInt64() : 0;
                double vramGb = sizeVram / 1_073_741_824.0;
                double totalGb = sizeTotal / 1_073_741_824.0;

                if (sizeVram == 0)
                {
                    logger.LogWarning(
                        "[Startup] Modell {Model} laeuft auf CPU (size_vram=0, total={Total:F1}GB) — "
                        + "VRAM zu knapp oder Ollama konnte nicht auf GPU laden. Prueffe nvidia-smi und andere VRAM-Nutzer.",
                        name, totalGb);
                }
                else
                {
                    logger.LogInformation(
                        "[Startup] Modell {Model} im VRAM: {Vram:F1}GB von {Total:F1}GB",
                        name, vramGb, totalGb);
                }
                return;
            }

            logger.LogWarning("[Startup] Modell {Model} nicht in /api/ps gefunden nach Warmup", model);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "[Startup] VRAM-Verifikation fehlgeschlagen (nicht kritisch)");
        }
    }
}
