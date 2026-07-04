using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Ai.Startup;
using AuswertungPro.Next.UI;
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.Tests;

public sealed class AiStartupServiceTests
{
    [Fact]
    public void ApplyRuntimeDefaults_enables_ai_and_multimodel_without_overwriting_models()
    {
        var settings = new AppSettings
        {
            AiEnabled = false,
            AiVisionModel = "custom-vision",
            AiTextModel = "custom-text",
            PipelineMultiModelEnabled = false,
            PipelineMode = "ollamaonly",
            AiOllamaKeepAlive = null
        };

        var changed = AiStartupService.ApplyRuntimeDefaults(settings);

        Assert.True(changed);
        Assert.True(settings.AiEnabled);
        Assert.True(settings.PipelineMultiModelEnabled);
        Assert.Equal("multimodel", settings.PipelineMode);
        Assert.Equal("custom-vision", settings.AiVisionModel);
        Assert.Equal("custom-text", settings.AiTextModel);
        Assert.Equal("24h", settings.AiOllamaKeepAlive);
    }

    [Fact]
    public async Task StartAsync_starts_ollama_and_sidecar_when_both_are_offline()
    {
        var temp = CreateTempSidecarScript();
        try
        {
            var launcher = new FakeAiStartupLauncher
            {
                OllamaReachable = false,
                SidecarReachable = false
            };
            var settings = new AppSettings
            {
                AiOllamaUrl = "http://localhost:11434",
                PipelineSidecarUrl = "http://localhost:8100"
            };

            var result = await AiStartupService.StartAsync(
                settings,
                launcher,
                sidecarScriptPath: temp.ScriptPath,
                ct: CancellationToken.None);
            var status = ReadRuntimeStatus();

            Assert.True(result.SettingsChanged);
            Assert.True(result.OllamaStartAttempted);
            Assert.True(result.SidecarStartAttempted);
            Assert.Contains(launcher.StartedProcesses, p =>
                string.Equals(p.FileName, "ollama", StringComparison.OrdinalIgnoreCase)
                && string.Equals(p.Arguments, "serve", StringComparison.Ordinal));
            Assert.Contains(launcher.StartedProcesses, p =>
                p.Arguments.Contains("start_sidecar.ps1", StringComparison.OrdinalIgnoreCase)
                && p.Hidden);
            Assert.Contains(result.Messages, m => m.Contains("KI aktiviert", StringComparison.OrdinalIgnoreCase));
            Assert.Contains("Modelle geladen", status.StatusText, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            ResetRuntimeStatusIfAvailable();
            Directory.Delete(temp.Root, recursive: true);
        }
    }

    [Fact]
    public async Task StartAsync_does_not_start_processes_when_endpoints_are_reachable()
    {
        var temp = CreateTempSidecarScript();
        try
        {
            var launcher = new FakeAiStartupLauncher
            {
                OllamaReachable = true,
                SidecarReachable = true
            };
            var settings = new AppSettings
            {
                AiEnabled = true,
                PipelineMultiModelEnabled = true,
                PipelineMode = "multimodel",
                AiOllamaUrl = "http://localhost:11434",
                PipelineSidecarUrl = "http://localhost:8100"
            };

            var result = await AiStartupService.StartAsync(
                settings,
                launcher,
                sidecarScriptPath: temp.ScriptPath,
                ct: CancellationToken.None);

            Assert.False(result.OllamaStartAttempted);
            Assert.False(result.SidecarStartAttempted);
            Assert.Empty(launcher.StartedProcesses);
        }
        finally
        {
            Directory.Delete(temp.Root, recursive: true);
        }
    }

    [Fact]
    public async Task StartAsync_preloads_configured_ollama_models_when_ollama_is_reachable()
    {
        var temp = CreateTempSidecarScript();
        try
        {
            ResetRuntimeStatusIfAvailable();
            var launcher = new FakeAiStartupLauncher
            {
                OllamaReachable = true,
                SidecarReachable = true
            };
            var settings = new AppSettings
            {
                AiEnabled = true,
                PipelineMultiModelEnabled = true,
                PipelineMode = "multimodel",
                AiOllamaUrl = "http://localhost:11434",
                PipelineSidecarUrl = "http://localhost:8100",
                AiVisionModel = "qwen3-vl:8b-q8",
                AiTextModel = "qwen3:8b",
                AiEmbedModel = "nomic-embed-text",
                AiOllamaKeepAlive = "24h"
            };

            var result = await AiStartupService.StartAsync(
                settings,
                launcher,
                sidecarScriptPath: temp.ScriptPath,
                ct: CancellationToken.None);
            var status = ReadRuntimeStatus();

            Assert.Equal(new[] { "qwen3-vl:8b-q8", "qwen3:8b", "nomic-embed-text" }, launcher.PreloadedModels);
            Assert.Contains(result.Messages, m => m.Contains("Ollama-Modelle geladen", StringComparison.OrdinalIgnoreCase));
            Assert.Contains("Modelle geladen", status.StatusText, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("qwen3-vl:8b-q8", status.ModelText, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("qwen3:8b", status.ModelText, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("nomic-embed-text", status.ModelText, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            ResetRuntimeStatusIfAvailable();
            Directory.Delete(temp.Root, recursive: true);
        }
    }

    [Fact]
    public async Task StartAsync_reports_warning_when_sidecar_script_is_missing()
    {
        var launcher = new FakeAiStartupLauncher
        {
            OllamaReachable = true,
            SidecarReachable = false
        };
        var settings = new AppSettings
        {
            AiOllamaUrl = "http://localhost:11434",
            PipelineSidecarUrl = "http://localhost:8100"
        };

        var result = await AiStartupService.StartAsync(
            settings,
            launcher,
            sidecarScriptPath: Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "start_sidecar.ps1"),
            ct: CancellationToken.None);

        Assert.False(result.SidecarStartAttempted);
        Assert.Contains(result.Warnings, w => w.Contains("Sidecar-Startskript nicht gefunden", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task StartAsync_updates_runtime_status_for_sidebar_indicator()
    {
        var temp = CreateTempSidecarScript();
        try
        {
            ResetRuntimeStatusIfAvailable();
            var launcher = new FakeAiStartupLauncher
            {
                OllamaReachable = true,
                SidecarReachable = true
            };
            var settings = new AppSettings
            {
                AiEnabled = true,
                PipelineMultiModelEnabled = true,
                PipelineMode = "multimodel",
                AiOllamaUrl = "http://localhost:11434",
                PipelineSidecarUrl = "http://localhost:8100",
                AiVisionModel = "qwen2.5vl:7b"
            };

            await AiStartupService.StartAsync(
                settings,
                launcher,
                sidecarScriptPath: temp.ScriptPath,
                ct: CancellationToken.None);

            var status = ReadRuntimeStatus();

            Assert.True(status.IsVisible);
            Assert.Equal("KI BEREIT", status.Title);
            Assert.Contains("Modelle geladen", status.StatusText, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("qwen2.5vl:7b", status.ModelText, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            ResetRuntimeStatusIfAvailable();
            Directory.Delete(temp.Root, recursive: true);
        }
    }

    [Fact]
    public async Task StartAsync_warmup_retries_until_all_vision_models_loaded()
    {
        var temp = CreateTempSidecarScript();
        try
        {
            ResetRuntimeStatusIfAvailable();
            var launcher = new FakeAiStartupLauncher
            {
                OllamaReachable = true,
                SidecarReachable = true,
                // 1. /warmup liefert nur SAM (YOLO/DINO/Classifier klemmen noch beim TensorRT-Kaltstart),
                // 2. /warmup liefert alle vier. Der robuste Start muss nachfassen.
                WarmupLoadedPerCall = new List<string[]>
                {
                    new[] { "sam" },
                    new[] { "yolo", "classifier", "dino", "sam" },
                }
            };
            var settings = new AppSettings
            {
                AiEnabled = true,
                PipelineMultiModelEnabled = true,
                PipelineMode = "multimodel",
                AiOllamaUrl = "http://localhost:11434",
                PipelineSidecarUrl = "http://localhost:8100",
                AiVisionModel = "qwen2.5vl:7b"
            };

            var result = await AiStartupService.StartAsync(
                settings,
                launcher,
                sidecarScriptPath: temp.ScriptPath,
                ct: CancellationToken.None);

            // Nachgefasst -> mind. 2 Aufrufe; am Ende sind ALLE Sidecar-Modelle geladen.
            Assert.True(launcher.WarmupCallCount >= 2, $"Warmup sollte nachfassen, war {launcher.WarmupCallCount}x");
            Assert.Empty(result.Warnings);
            Assert.Contains(result.Messages, m =>
                m.Contains("yolo", StringComparison.OrdinalIgnoreCase)
                && m.Contains("classifier", StringComparison.OrdinalIgnoreCase)
                && m.Contains("dino", StringComparison.OrdinalIgnoreCase)
                && m.Contains("sam", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            ResetRuntimeStatusIfAvailable();
            Directory.Delete(temp.Root, recursive: true);
        }
    }

    [Fact]
    public async Task StartAsync_warns_when_vision_model_stays_missing_after_retries()
    {
        var temp = CreateTempSidecarScript();
        try
        {
            ResetRuntimeStatusIfAvailable();
            var launcher = new FakeAiStartupLauncher
            {
                OllamaReachable = true,
                SidecarReachable = true,
                // YOLO kommt NIE hoch (Engine kaputt) -> nach Retries muss eine ehrliche Warnung stehen.
                WarmupLoadedPerCall = new List<string[]> { new[] { "dino", "sam" } }
            };
            var settings = new AppSettings
            {
                AiEnabled = true,
                PipelineMultiModelEnabled = true,
                PipelineMode = "multimodel",
                AiOllamaUrl = "http://localhost:11434",
                PipelineSidecarUrl = "http://localhost:8100",
                AiVisionModel = "qwen2.5vl:7b"
            };

            var result = await AiStartupService.StartAsync(
                settings,
                launcher,
                sidecarScriptPath: temp.ScriptPath,
                ct: CancellationToken.None);

            Assert.Equal(3, launcher.WarmupCallCount); // alle Versuche ausgeschoepft
            Assert.Contains(result.Warnings, w =>
                w.Contains("NICHT geladen", StringComparison.OrdinalIgnoreCase)
                && w.Contains("yolo", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            ResetRuntimeStatusIfAvailable();
            Directory.Delete(temp.Root, recursive: true);
        }
    }

    [Fact]
    public async Task StartAsync_warns_when_classifier_model_stays_missing_after_retries()
    {
        var temp = CreateTempSidecarScript();
        try
        {
            ResetRuntimeStatusIfAvailable();
            var launcher = new FakeAiStartupLauncher
            {
                OllamaReachable = true,
                SidecarReachable = true,
                WarmupLoadedPerCall = new List<string[]> { new[] { "yolo", "dino", "sam" } }
            };
            var settings = new AppSettings
            {
                AiEnabled = true,
                PipelineMultiModelEnabled = true,
                PipelineMode = "multimodel",
                AiOllamaUrl = "http://localhost:11434",
                PipelineSidecarUrl = "http://localhost:8100",
                AiVisionModel = "qwen2.5vl:7b"
            };

            var result = await AiStartupService.StartAsync(
                settings,
                launcher,
                sidecarScriptPath: temp.ScriptPath,
                ct: CancellationToken.None);

            Assert.Equal(3, launcher.WarmupCallCount);
            Assert.Contains(result.Warnings, w =>
                w.Contains("NICHT geladen", StringComparison.OrdinalIgnoreCase)
                && w.Contains("classifier", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            ResetRuntimeStatusIfAvailable();
            Directory.Delete(temp.Root, recursive: true);
        }
    }

    [Fact]
    public async Task StartAsync_reports_progress_steps_so_button_does_not_look_stuck()
    {
        var temp = CreateTempSidecarScript();
        try
        {
            ResetRuntimeStatusIfAvailable();
            // Sidecar offline -> Start + Warten + Warmup: hier entstehen die langen Phasen,
            // in denen der Knopf bisher stumm "Starte KI..." zeigte (User: "haengt").
            var launcher = new FakeAiStartupLauncher
            {
                OllamaReachable = true,
                SidecarReachable = false
            };
            var settings = new AppSettings
            {
                AiOllamaUrl = "http://localhost:11434",
                PipelineSidecarUrl = "http://localhost:8100"
            };
            var steps = new ConcurrentQueue<string>();
            var progress = new ImmediateProgress<string>(steps.Enqueue);

            await AiStartupService.StartAsync(
                settings,
                launcher,
                sidecarScriptPath: temp.ScriptPath,
                progress: progress,
                ct: CancellationToken.None);

            // Es muss live ueber die langen Phasen berichtet werden, nicht nur ein Endzustand.
            var reportedSteps = steps.ToArray();
            Assert.NotEmpty(reportedSteps);
            Assert.Contains(reportedSteps, s => s.Contains("Sidecar", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(reportedSteps, s => s.Contains("Modell", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            ResetRuntimeStatusIfAvailable();
            Directory.Delete(temp.Root, recursive: true);
        }
    }

    [Fact]
    public async Task StartAsync_warns_when_ollama_model_not_resident_after_preload()
    {
        // User-Fall: 'KI starten' gedrueckt, aber Ollama-Modelle blieben LEER (nicht resident).
        // Der Preload meldete Erfolg, das Modell ist aber nicht im Speicher -> ehrliche Warnung,
        // damit der Nutzer nicht denkt "alles geladen", obwohl Ollama leer ist.
        var temp = CreateTempSidecarScript();
        try
        {
            ResetRuntimeStatusIfAvailable();
            var launcher = new FakeAiStartupLauncher
            {
                OllamaReachable = true,
                SidecarReachable = true,
                OllamaResidentAfterPreload = false // Preload "ok", aber Modell NICHT resident
            };
            var settings = new AppSettings
            {
                AiEnabled = true,
                PipelineMultiModelEnabled = true,
                PipelineMode = "multimodel",
                AiOllamaUrl = "http://localhost:11434",
                PipelineSidecarUrl = "http://localhost:8100",
                AiVisionModel = "qwen3-vl:8b-q8"
            };

            var result = await AiStartupService.StartAsync(
                settings, launcher, sidecarScriptPath: temp.ScriptPath, ct: CancellationToken.None);

            Assert.Contains(result.Warnings, w =>
                w.Contains("nicht im Speicher", StringComparison.OrdinalIgnoreCase)
                || w.Contains("nicht resident", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            ResetRuntimeStatusIfAvailable();
            Directory.Delete(temp.Root, recursive: true);
        }
    }

    private static (string Root, string ScriptPath) CreateTempSidecarScript()
    {
        var root = Path.Combine(Path.GetTempPath(), "sewerstudio-ai-start-" + Guid.NewGuid().ToString("N"));
        var sidecar = Path.Combine(root, "sidecar");
        Directory.CreateDirectory(sidecar);
        var script = Path.Combine(sidecar, "start_sidecar.ps1");
        File.WriteAllText(script, "# test");
        return (root, script);
    }

    private static (bool IsVisible, string Title, string StatusText, string ModelText) ReadRuntimeStatus()
    {
        var trackerType = typeof(AiStartupService).Assembly.GetType("AuswertungPro.Next.UI.Services.AiRuntimeStatusTracker");
        Assert.NotNull(trackerType);

        var current = trackerType.GetProperty("Current")?.GetValue(null);
        Assert.NotNull(current);

        var statusType = current.GetType();
        return (
            (bool)(statusType.GetProperty("IsVisible")?.GetValue(current) ?? false),
            (string)(statusType.GetProperty("Title")?.GetValue(current) ?? ""),
            (string)(statusType.GetProperty("StatusText")?.GetValue(current) ?? ""),
            (string)(statusType.GetProperty("ModelText")?.GetValue(current) ?? ""));
    }

    private static void ResetRuntimeStatusIfAvailable()
    {
        var trackerType = typeof(AiStartupService).Assembly.GetType("AuswertungPro.Next.UI.Services.AiRuntimeStatusTracker");
        trackerType?.GetMethod("ResetForTests")?.Invoke(null, null);
    }

    private sealed class ImmediateProgress<T>(Action<T> onReport) : IProgress<T>
    {
        public void Report(T value) => onReport(value);
    }

    private sealed class FakeAiStartupLauncher : IAiStartupLauncher
    {
        public bool OllamaReachable { get; set; }
        public bool SidecarReachable { get; set; }
        public List<AiStartupProcessRequest> StartedProcesses { get; } = new();
        public List<string> PreloadedModels { get; } = new();
        public List<string> WarmedModels { get; } = new();

        public Task<bool> IsReachableAsync(
            Uri baseUri,
            string relativePath,
            IReadOnlyDictionary<string, string>? headers,
            CancellationToken ct)
        {
            var reachable = baseUri.Port == 11434 ? OllamaReachable : SidecarReachable;
            return Task.FromResult(reachable);
        }

        public bool TryStart(AiStartupProcessRequest request, out string? error)
        {
            StartedProcesses.Add(request);
            if (string.Equals(request.FileName, "ollama", StringComparison.OrdinalIgnoreCase))
                OllamaReachable = true;
            else if (request.Arguments.Contains("start_sidecar.ps1", StringComparison.OrdinalIgnoreCase))
                SidecarReachable = true; // Sidecar kommt nach dem Start hoch (Simulation)

            error = null;
            return true;
        }

        /// <summary>Simuliert, ob ein Modell nach dem Preload wirklich resident ist (/api/ps).
        /// false = Preload meldet ok, Modell aber nicht im Speicher (User-Fehlerfall).</summary>
        public bool OllamaResidentAfterPreload { get; set; } = true;

        public Task<AiStartupModelPreloadResult> PreloadOllamaModelAsync(
            Uri baseUri,
            AiStartupModelPreloadRequest request,
            CancellationToken ct)
        {
            PreloadedModels.Add(request.ModelName);
            return Task.FromResult(new AiStartupModelPreloadResult(true, null));
        }

        public Task<bool?> IsOllamaModelResidentAsync(Uri baseUri, string modelName, CancellationToken ct)
            => Task.FromResult<bool?>(OllamaResidentAfterPreload);

        /// <summary>Zaehlt, wie oft /warmup aufgerufen wurde (fuer Retry-Tests).</summary>
        public int WarmupCallCount { get; private set; }

        /// <summary>
        /// Optional: pro Aufruf-Index die "loaded"-Liste, die der Warmup zurueckgeben soll.
        /// Erlaubt Teilausfall-Simulation (z.B. 1. Aufruf nur sam, 2. Aufruf alle Modelle).
        /// Wenn null/leer, wird immer {yolo,classifier,dino,sam} geliefert (Standardverhalten).
        /// </summary>
        public List<string[]>? WarmupLoadedPerCall { get; set; }

        public Task<AiStartupWarmupResult> WarmupSidecarModelsAsync(
            Uri sidecarBaseUri,
            IReadOnlyDictionary<string, string>? headers,
            CancellationToken ct)
        {
            if (!SidecarReachable)
                return Task.FromResult(new AiStartupWarmupResult(false, Array.Empty<string>(), "nicht erreichbar"));

            var idx = WarmupCallCount;
            WarmupCallCount++;

            var models = WarmupLoadedPerCall is { Count: > 0 }
                ? (idx < WarmupLoadedPerCall.Count ? WarmupLoadedPerCall[idx] : WarmupLoadedPerCall[^1])
                : new[] { "yolo", "classifier", "dino", "sam" };

            WarmedModels.AddRange(models);
            return Task.FromResult(new AiStartupWarmupResult(true, models, null));
        }
    }
}
