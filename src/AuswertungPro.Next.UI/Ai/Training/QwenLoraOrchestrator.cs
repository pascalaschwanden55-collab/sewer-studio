using System;
using AuswertungPro.Next.Application.Ai.Ollama;
using AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.UI.Ai.KnowledgeBase;
using AuswertungPro.Next.UI.Ai.Ollama;
using AuswertungPro.Next.UI.Ai.Pipeline;
using AuswertungPro.Next.Application.Ai.Pipeline;
using AuswertungPro.Next.Application.Ai.Training;

namespace AuswertungPro.Next.UI.Ai.Training;

/// <summary>
/// Orchestriert Qwen LoRA-Training: KB-Samples exportieren, Training starten,
/// Adapter via Ollama deployen, Benchmark-Gate pruefen.
/// Gleiche Struktur wie YoloRetrainOrchestrator.
/// </summary>
public sealed class QwenLoraOrchestrator
{
    private const int MaxAdapterVersions = 3;
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly VisionPipelineClient _sidecar;
    private readonly KnowledgeBaseContext _kb;
    private readonly OllamaConfig _ollamaConfig;
    private readonly BenchmarkRunner _benchmarkRunner;
    private readonly BenchmarkMetricsStore _benchmarkMetricsStore;
    private readonly Action<string>? _log;

    public QwenLoraOrchestrator(
        VisionPipelineClient sidecar,
        KnowledgeBaseContext kb,
        OllamaConfig ollamaConfig,
        BenchmarkRunner benchmarkRunner,
        BenchmarkMetricsStore benchmarkMetricsStore,
        Action<string>? log = null)
    {
        _sidecar = sidecar ?? throw new ArgumentNullException(nameof(sidecar));
        _kb = kb ?? throw new ArgumentNullException(nameof(kb));
        _ollamaConfig = ollamaConfig ?? throw new ArgumentNullException(nameof(ollamaConfig));
        _benchmarkRunner = benchmarkRunner ?? throw new ArgumentNullException(nameof(benchmarkRunner));
        _benchmarkMetricsStore = benchmarkMetricsStore ?? throw new ArgumentNullException(nameof(benchmarkMetricsStore));
        _log = log;
    }

    /// <summary>
    /// Fuehrt LoRA-Training durch wenn genug KB-Samples vorhanden.
    /// </summary>
    public async Task<LoraRetrainResult> RunIfEligibleAsync(
        int minSamples = 100,
        int epochs = 3,
        int loraRank = 16,
        CancellationToken ct = default)
    {
        var adapterDir = Path.Combine(KnowledgeRoot.GetRoot(), "lora_adapters");
        Directory.CreateDirectory(adapterDir);

        var manifestPath = Path.Combine(adapterDir, "manifest.json");
        var manifest = await LoadManifestAsync(manifestPath, ct).ConfigureAwait(false);

        // KB-Samples mit Bildern laden
        var samples = LoadKbSamplesWithImages();
        if (samples.Count < minSamples)
        {
            return LoraRetrainResult.Skipped(
                $"LoRA-Training uebersprungen: {samples.Count} KB-Samples (< {minSamples}).",
                samples.Count);
        }

        // Pruefen ob neue Samples seit letztem Training
        var newSamples = samples.Count - manifest.LastTrainedSampleCount;
        if (newSamples < 50)
        {
            return LoraRetrainResult.Skipped(
                $"LoRA-Training uebersprungen: nur {newSamples} neue Samples seit letztem Training.",
                samples.Count);
        }

        Log($"LoRA-Training: {samples.Count} Samples ({newSamples} neu).");

        // Training-Samples fuer Sidecar vorbereiten
        var loraSamples = new List<LoraTrainSampleDto>();
        foreach (var (vsaCode, beschreibung, framePath) in samples)
        {
            ct.ThrowIfCancellationRequested();
            if (!File.Exists(framePath)) continue;

            try
            {
                var imageBytes = await File.ReadAllBytesAsync(framePath, ct).ConfigureAwait(false);
                var b64 = Convert.ToBase64String(imageBytes);

                // Erwartete Antwort als JSON bauen
                var expectedResponse = BuildExpectedResponse(vsaCode, beschreibung);

                loraSamples.Add(new LoraTrainSampleDto(
                    ImageBase64: b64,
                    Prompt: "Analysiere dieses Kanalbild.",
                    ExpectedResponse: expectedResponse));
            }
            catch (Exception ex)
            {
                Log($"  Sample uebersprungen ({framePath}): {ex.Message}");
            }
        }

        if (loraSamples.Count < minSamples)
        {
            return LoraRetrainResult.Skipped(
                $"Zu wenig ladbare Samples: {loraSamples.Count} (< {minSamples}).",
                loraSamples.Count);
        }

        Log($"LoRA-Training: {loraSamples.Count} Samples vorbereitet, starte Training...");

        var outputDir = Path.Combine(adapterDir, $"run_{DateTime.UtcNow:yyyyMMdd_HHmmss}");
        var start = await _sidecar.StartLoraTrainingAsync(new LoraTrainRequestDto(
            Samples: loraSamples,
            BaseModel: "Qwen/Qwen2.5-VL-7B-Instruct",
            LoraRank: loraRank,
            Epochs: epochs,
            OutputDir: outputDir), ct).ConfigureAwait(false);

        Log($"LoRA-Training gestartet (Job {start.JobId}).");

        var job = await WaitForLoraJobAsync(start.JobId, ct).ConfigureAwait(false);
        if (!string.Equals(job.Status, "completed", StringComparison.OrdinalIgnoreCase))
        {
            return LoraRetrainResult.Failed(
                $"LoRA-Training fehlgeschlagen: {job.Error ?? job.Message}",
                loraSamples.Count);
        }

        if (string.IsNullOrWhiteSpace(job.AdapterPath))
        {
            return LoraRetrainResult.Failed("Training beendet ohne adapter_path.", loraSamples.Count);
        }

        // Adapter via Ollama deployen
        Log("LoRA-Adapter wird via Ollama deployt...");
        var loraModelName = $"{_ollamaConfig.VisionModel}-lora";
        try
        {
            await _sidecar.DeployLoraAdapterAsync(new LoraDeployRequestDto(
                AdapterPath: job.AdapterPath,
                BaseModel: _ollamaConfig.VisionModel,
                ModelName: loraModelName,
                OllamaBaseUrl: _ollamaConfig.BaseUri.ToString().TrimEnd('/')), ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            return LoraRetrainResult.Failed($"Ollama-Deploy fehlgeschlagen: {ex.Message}", loraSamples.Count);
        }

        // Benchmark laufen lassen (mit dem neuen LoRA-Modell)
        Log("LoRA-Benchmark laeuft...");
        var history = await _benchmarkMetricsStore.LoadHistoryAsync(ct).ConfigureAwait(false);
        var previousF1 = history.OrderByDescending(h => h.TimestampUtc).Select(h => (double?)h.F1).FirstOrDefault();

        BenchmarkRunResult benchmark;
        try
        {
            benchmark = await _benchmarkRunner.RunAsync(ct: ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log($"LoRA-Benchmark fehlgeschlagen: {ex.Message}. Adapter bleibt, aber kein Auto-Switch.");
            return LoraRetrainResult.Failed($"Benchmark fehlgeschlagen: {ex.Message}", loraSamples.Count);
        }

        var gatePass = !previousF1.HasValue || benchmark.F1 >= previousF1.Value;
        if (!gatePass)
        {
            Log($"LoRA Gate nicht bestanden: alt={previousF1:P1}, neu={benchmark.F1:P1}. Adapter gespeichert aber nicht aktiv.");
            return LoraRetrainResult.Rejected(
                $"F1-Gate: alt={previousF1:P1}, neu={benchmark.F1:P1}",
                loraSamples.Count, benchmark.F1);
        }

        // Manifest aktualisieren
        manifest.ActiveAdapter = loraModelName;
        manifest.AdapterPath = job.AdapterPath;
        manifest.LastTrainedSampleCount = samples.Count;
        manifest.UpdatedUtc = DateTime.UtcNow;
        manifest.Versions.Add(new LoraAdapterVersion
        {
            ModelName = loraModelName,
            AdapterPath = job.AdapterPath,
            CreatedUtc = DateTime.UtcNow,
            SampleCount = loraSamples.Count,
            TrainLoss = job.Metrics?.TrainLoss ?? 0,
            BenchmarkF1 = benchmark.F1
        });

        await PruneOldVersionsAsync(manifest).ConfigureAwait(false);
        await SaveManifestAsync(manifestPath, manifest, ct).ConfigureAwait(false);

        Log($"LoRA-Training erfolgreich: {loraModelName} (F1={benchmark.F1:P1}).");
        return LoraRetrainResult.CreateDeployed(loraModelName, loraSamples.Count, benchmark.F1);
    }

    private List<(string VsaCode, string Beschreibung, string FramePath)> LoadKbSamplesWithImages()
    {
        var results = new List<(string, string, string)>();
        using var cmd = _kb.Connection.CreateCommand();
        cmd.CommandText = "SELECT VsaCode, Beschreibung, FramePath FROM Samples WHERE FramePath IS NOT NULL AND FramePath <> ''";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var code = reader.GetString(0);
            var desc = reader.IsDBNull(1) ? "" : reader.GetString(1);
            var path = reader.GetString(2);
            results.Add((code, desc, path));
        }
        return results;
    }

    private static string BuildExpectedResponse(string vsaCode, string beschreibung)
    {
        var finding = new Dictionary<string, object?>
        {
            ["code"] = vsaCode,
            ["description"] = beschreibung,
            ["severity"] = 2,
            ["clock_position"] = "6:00"
        };
        var response = new Dictionary<string, object?>
        {
            ["meter"] = 0.0,
            ["findings"] = new[] { finding }
        };
        return JsonSerializer.Serialize(response, new JsonSerializerOptions { WriteIndented = false });
    }

    private async Task<LoraTrainJobStatusDto> WaitForLoraJobAsync(string jobId, CancellationToken ct)
    {
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            var status = await _sidecar.GetLoraTrainingJobAsync(jobId, ct).ConfigureAwait(false);
            switch (status.Status?.ToLowerInvariant())
            {
                case "completed":
                case "failed":
                case "cancelled":
                    return status;
            }
            Log($"  LoRA-Job {jobId}: {status.Status} — {status.Message}");
            await Task.Delay(TimeSpan.FromSeconds(10), ct).ConfigureAwait(false);
        }
    }

    private static Task PruneOldVersionsAsync(LoraAdapterManifest manifest)
    {
        if (manifest.Versions.Count <= MaxAdapterVersions)
            return Task.CompletedTask;

        var toRemove = manifest.Versions
            .OrderBy(v => v.CreatedUtc)
            .Take(manifest.Versions.Count - MaxAdapterVersions)
            .ToList();

        foreach (var old in toRemove)
        {
            if (!string.IsNullOrWhiteSpace(old.AdapterPath))
            {
                try
                {
                    var dir = Path.GetDirectoryName(old.AdapterPath);
                    if (dir != null && Directory.Exists(dir))
                        Directory.Delete(dir, recursive: true);
                }
                catch { /* best-effort cleanup */ }
            }
            manifest.Versions.Remove(old);
        }
        return Task.CompletedTask;
    }

    private static async Task<LoraAdapterManifest> LoadManifestAsync(string path, CancellationToken ct)
    {
        if (!File.Exists(path)) return new LoraAdapterManifest();
        try
        {
            var json = await File.ReadAllTextAsync(path, ct).ConfigureAwait(false);
            return JsonSerializer.Deserialize<LoraAdapterManifest>(json, JsonOpts) ?? new LoraAdapterManifest();
        }
        catch { return new LoraAdapterManifest(); }
    }

    private static async Task SaveManifestAsync(string path, LoraAdapterManifest manifest, CancellationToken ct)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var tmp = path + ".tmp";
        var json = JsonSerializer.Serialize(manifest, JsonOpts);
        await File.WriteAllTextAsync(tmp, json, ct).ConfigureAwait(false);
        File.Move(tmp, path, overwrite: true);
    }

    private void Log(string message) => _log?.Invoke(message);
}

// ── Manifest ────────────────────────────────────────────────────────────────

public sealed class LoraAdapterManifest
{
    public string ActiveAdapter { get; set; } = "";
    public string AdapterPath { get; set; } = "";
    public int LastTrainedSampleCount { get; set; }
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
    public List<LoraAdapterVersion> Versions { get; set; } = [];
}

public sealed class LoraAdapterVersion
{
    public string ModelName { get; set; } = "";
    public string AdapterPath { get; set; } = "";
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public int SampleCount { get; set; }
    public double TrainLoss { get; set; }
    public double BenchmarkF1 { get; set; }
}

// ── Result ──────────────────────────────────────────────────────────────────

public sealed class LoraRetrainResult
{
    public bool Attempted { get; init; }
    public bool Deployed { get; init; }
    public bool RejectedByGate { get; init; }
    public string StatusText { get; init; } = "";
    public string? ActiveModelName { get; init; }
    public int SampleCount { get; init; }
    public double? BenchmarkF1 { get; init; }

    public static LoraRetrainResult Skipped(string status, int samples = 0) => new()
    {
        Attempted = false, Deployed = false, RejectedByGate = false,
        StatusText = status, SampleCount = samples
    };

    public static LoraRetrainResult Failed(string status, int samples = 0, double? f1 = null) => new()
    {
        Attempted = true, Deployed = false, RejectedByGate = false,
        StatusText = status, SampleCount = samples, BenchmarkF1 = f1
    };

    public static LoraRetrainResult Rejected(string status, int samples = 0, double? f1 = null) => new()
    {
        Attempted = true, Deployed = false, RejectedByGate = true,
        StatusText = status, SampleCount = samples, BenchmarkF1 = f1
    };

    public static LoraRetrainResult CreateDeployed(string modelName, int samples = 0, double? f1 = null) => new()
    {
        Attempted = true, Deployed = true, RejectedByGate = false,
        StatusText = "LoRA-Adapter erfolgreich deployt.",
        ActiveModelName = modelName, SampleCount = samples, BenchmarkF1 = f1
    };
}
