using System;
using AuswertungPro.Next.Domain.Ai.Training;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Ai.Pipeline;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;
using AuswertungPro.Next.Infrastructure.Ai.Training;

namespace AuswertungPro.Next.Infrastructure.Ai.Training;

/// <summary>
/// Orchestrates automatic YOLO retraining, benchmark gating, versioning, and hot-reload.
/// </summary>
public sealed class YoloRetrainOrchestrator : ITrainingOrchestrator
{
    /// <inheritdoc/>
    public string Name => "YoloRetrain";

    private const int MaxArchivedVersions = 3;
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly VisionPipelineClient _sidecar;
    private readonly YoloDatasetExportService _datasetExport;
    private readonly BenchmarkRunner _benchmarkRunner;
    private readonly BenchmarkMetricsStore _benchmarkMetricsStore;
    private readonly string _sidecarRootDir;
    private readonly Action<string>? _log;

    // B3 Fix: Mutex verhindert parallele Retrains (VRAM-Konflikt + Manifest-Korruption)
    private readonly SemaphoreSlim _retrainMutex = new(1, 1);

    public YoloRetrainOrchestrator(
        VisionPipelineClient sidecar,
        YoloDatasetExportService datasetExport,
        BenchmarkRunner benchmarkRunner,
        BenchmarkMetricsStore benchmarkMetricsStore,
        string sidecarRootDir,
        Action<string>? log = null)
    {
        _sidecar = sidecar ?? throw new ArgumentNullException(nameof(sidecar));
        _datasetExport = datasetExport ?? throw new ArgumentNullException(nameof(datasetExport));
        _benchmarkRunner = benchmarkRunner ?? throw new ArgumentNullException(nameof(benchmarkRunner));
        _benchmarkMetricsStore = benchmarkMetricsStore ?? throw new ArgumentNullException(nameof(benchmarkMetricsStore));
        _sidecarRootDir = sidecarRootDir ?? throw new ArgumentNullException(nameof(sidecarRootDir));
        _log = log;
    }

    public async Task<YoloRetrainResult> RunIfEligibleAsync(
        int minNewApprovedSamples = 50,
        int epochs = 50,
        int imageSize = 640,
        int batch = -1,
        CancellationToken ct = default)
    {
        if (!await _retrainMutex.WaitAsync(TimeSpan.FromSeconds(1), ct).ConfigureAwait(false))
            return YoloRetrainResult.Skipped("Retraining laeuft bereits (Mutex belegt)");
        try
        {
            return await RunWithProvenanceAsync(minNewApprovedSamples, epochs, imageSize, batch, ct)
                .ConfigureAwait(false);
        }
        finally
        {
            _retrainMutex.Release();
        }
    }

    /// <summary>
    /// Phase 2.3: Wrapper, der jeden Retrain-Lauf in einen
    /// <see cref="TrainingRun"/> einbettet. Das Resultat steuert ob
    /// Complete/Cancel/Fail geschrieben wird:
    /// - Skipped (Attempted=false) -> Cancel mit Status-Text als Grund
    /// - Failed (Attempted=true, Deployed=false) -> Fail mit Status-Text
    /// - Rejected (RejectedByGate=true) -> Fail mit Status-Text
    /// - Deployed -> Complete mit NewApprovedSamples als samplesAffected
    /// Bei Exceptions wird ebenfalls FailRunAsync aufgerufen, dann re-thrown.
    /// </summary>
    private async Task<YoloRetrainResult> RunWithProvenanceAsync(
        int minNewApprovedSamples,
        int epochs,
        int imageSize,
        int batch,
        CancellationToken ct)
    {
        var run = await TrainingRunsStore.BeginRunAsync(
            TrainingRunTriggers.YoloRetrain,
            notes: $"min={minNewApprovedSamples} epochs={epochs} imgsz={imageSize} batch={batch}")
            .ConfigureAwait(false);

        try
        {
            var result = await RunIfEligibleCoreAsync(minNewApprovedSamples, epochs, imageSize, batch, ct)
                .ConfigureAwait(false);

            if (!result.Attempted)
                await TrainingRunsStore.CancelRunAsync(run.RunId, result.StatusText).ConfigureAwait(false);
            else if (result.Deployed)
                await TrainingRunsStore.CompleteRunAsync(run.RunId, samplesAffected: result.NewApprovedSamples).ConfigureAwait(false);
            else
                await TrainingRunsStore.FailRunAsync(run.RunId, result.StatusText, samplesAffected: result.NewApprovedSamples).ConfigureAwait(false);

            return result;
        }
        catch (OperationCanceledException)
        {
            await TrainingRunsStore.CancelRunAsync(run.RunId, "Cancelled").ConfigureAwait(false);
            throw;
        }
        catch (Exception ex)
        {
            await TrainingRunsStore.FailRunAsync(run.RunId, ex.Message).ConfigureAwait(false);
            throw;
        }
    }

    private async Task<YoloRetrainResult> RunIfEligibleCoreAsync(
        int minNewApprovedSamples,
        int epochs,
        int imageSize,
        int batch,
        CancellationToken ct)
    {
        var modelDir = Path.Combine(_sidecarRootDir, "models", "yolo26m");
        Directory.CreateDirectory(modelDir);

        var manifestPath = Path.Combine(modelDir, "manifest.json");
        var manifest = await LoadManifestAsync(manifestPath, ct).ConfigureAwait(false);

        var allSamples = await TrainingSamplesStore.LoadAsync().ConfigureAwait(false);
        var approvedWithRealBoxAllSources = allSamples
            .Where(s => s.Status == TrainingSampleStatus.Approved
                        && !string.IsNullOrWhiteSpace(s.FramePath)
                        && File.Exists(s.FramePath)
                        && s.HasBbox)
            .ToList();

        var approvedWithRealBox = approvedWithRealBoxAllSources
            .Where(s => string.Equals(
                s.SourceType,
                SourceTypeNames.VideoTimestamp,
                StringComparison.OrdinalIgnoreCase))
            .ToList();

        var excludedBySource = approvedWithRealBoxAllSources.Count - approvedWithRealBox.Count;

        if (approvedWithRealBox.Count == 0)
        {
            var reason = approvedWithRealBoxAllSources.Count == 0
                ? "Keine Approved-Samples mit echten BBoxen vorhanden."
                : $"Keine Approved-VideoTimestamp-Samples mit echten BBoxen vorhanden (ausgeschlossen wegen SourceType: {excludedBySource}).";
            return YoloRetrainResult.Skipped(reason);
        }

        var knownKeys = manifest.TrainedSampleKeys.ToHashSet(StringComparer.Ordinal);
        var newApproved = approvedWithRealBox
            .Where(s => !knownKeys.Contains(GetSampleKey(s)))
            .ToList();

        if (newApproved.Count < minNewApprovedSamples)
        {
            return YoloRetrainResult.Skipped(
                $"Auto-Retrain uebersprungen: {newApproved.Count} neue Approved-Samples (< {minNewApprovedSamples}).",
                newApproved.Count);
        }

        var distinctClasses = approvedWithRealBox
            .Select(s => NormalizeClassName(s.Code))
            .Where(c => !string.IsNullOrEmpty(c))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();

        if (distinctClasses < 2)
        {
            return YoloRetrainResult.Skipped(
                $"Auto-Retrain uebersprungen: zu wenig Klassenvielfalt ({distinctClasses} Klasse).",
                newApproved.Count);
        }

        Log($"YOLO Auto-Retrain: {newApproved.Count} neue Samples, {approvedWithRealBox.Count} total (SourceType=VideoTimestamp, ausgeschlossen={excludedBySource}).");

        var exportDir = Path.Combine(
            AuswertungPro.Next.Application.Ai.KnowledgeRootProvider.GetRoot(),
            "training_export",
            $"yolo_autotrain_{DateTime.UtcNow:yyyyMMdd_HHmmss}");

        var exportResult = await _datasetExport.ExportAsync(
            approvedWithRealBox,
            exportDir,
            trainSplit: 0.8,
            stratifiedByClass: true,
            requireRealBboxes: true,
            progress: (_, msg) => Log($"  Export: {msg}"),
            ct: ct).ConfigureAwait(false);

        if (!exportResult.IsSuccess || string.IsNullOrWhiteSpace(exportResult.YamlPath))
        {
            return YoloRetrainResult.Failed(
                $"YOLO-Export fehlgeschlagen: {exportResult.Error ?? "Unbekannter Fehler"}",
                newApproved.Count);
        }

        if (exportResult.FallbackBboxCount > 0)
        {
            return YoloRetrainResult.Failed(
                $"Datenqualitaet-Gate: {exportResult.FallbackBboxCount} Fallback-BBoxen gefunden.",
                newApproved.Count);
        }

        var previousModelPath = ResolveExistingModelPath(
            manifest.ActiveModel,
            modelDir,
            ResolveModelPathFromHealth(await _sidecar.HealthCheckAsync(ct).ConfigureAwait(false)),
            _sidecarRootDir);

        var baseModel = previousModelPath ?? "yolo11m.pt";
        var trainProject = Path.Combine(_sidecarRootDir, "runs", "train");
        var start = await _sidecar.StartYoloTrainingAsync(new YoloTrainRequestDto(
            DatasetPath: exportResult.YamlPath!,
            Epochs: epochs,
            ImageSize: imageSize,
            Batch: batch,
            BaseModel: baseModel,
            Project: trainProject,
            Amp: true,
            MaxFallbackRatio: 0.35), ct).ConfigureAwait(false);

        Log($"YOLO Auto-Retrain: Training gestartet (Job {start.JobId}).");

        var job = await WaitForTrainJobAsync(start.JobId, ct).ConfigureAwait(false);
        if (!string.Equals(job.Status, "completed", StringComparison.OrdinalIgnoreCase))
        {
            return YoloRetrainResult.Failed(
                $"Training fehlgeschlagen: {job.Error ?? job.Message}",
                newApproved.Count);
        }

        if (string.IsNullOrWhiteSpace(job.ModelPath))
        {
            return YoloRetrainResult.Failed("Training beendet ohne model_path.", newApproved.Count);
        }

        var candidatePath = ResolveCandidatePath(job.ModelPath);
        if (!File.Exists(candidatePath))
        {
            return YoloRetrainResult.Failed($"Kandidatenmodell nicht gefunden: {candidatePath}", newApproved.Count);
        }

        Log("YOLO Auto-Retrain: Kandidatenmodell wird fuer Benchmark geladen...");
        await _sidecar.ReloadYoloModelAsync(new ModelReloadRequestDto(candidatePath), ct).ConfigureAwait(false);

        var history = await _benchmarkMetricsStore.LoadHistoryAsync(ct).ConfigureAwait(false);
        var previousF1 = history.OrderByDescending(h => h.TimestampUtc).Select(h => (double?)h.F1).FirstOrDefault();

        BenchmarkRunResult benchmark;
        try
        {
            benchmark = await _benchmarkRunner.RunAsync(ct: ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await TryRollbackAsync(previousModelPath, ct).ConfigureAwait(false);
            return YoloRetrainResult.Failed(
                $"Benchmark fehlgeschlagen, Deploy abgebrochen: {ex.Message}",
                newApproved.Count);
        }

        var baselinePass = !previousF1.HasValue || benchmark.F1 >= previousF1.Value;
        var gatePass = !benchmark.HasRegression && baselinePass;
        if (!gatePass)
        {
            var reason = benchmark.HasRegression
                ? benchmark.RegressionDetail ?? "Regression erkannt"
                : $"F1-Gate nicht bestanden: alt={previousF1:P1}, neu={benchmark.F1:P1}";
            Log($"YOLO Auto-Retrain: Gate nicht bestanden ({reason}). Rollback...");
            await TryRollbackAsync(previousModelPath, ct).ConfigureAwait(false);
            return YoloRetrainResult.Rejected(reason, newApproved.Count, benchmark.F1);
        }

        var nextVersionNumber = GetNextVersionNumber(manifest);
        var versionFileName = $"yolo_v{nextVersionNumber}.pt";
        var versionPath = Path.Combine(modelDir, versionFileName);
        File.Copy(candidatePath, versionPath, overwrite: true);

        var candidateEngine = Path.ChangeExtension(candidatePath, ".engine");
        if (File.Exists(candidateEngine))
        {
            var versionEngine = Path.ChangeExtension(versionPath, ".engine");
            File.Copy(candidateEngine, versionEngine, overwrite: true);
        }

        manifest.ActiveModel = versionFileName;
        manifest.UpdatedUtc = DateTime.UtcNow;
        manifest.Versions.Add(new YoloModelVersion
        {
            Version = versionFileName,
            ModelPath = versionPath,
            CreatedUtc = DateTime.UtcNow,
            NewApprovedSamples = newApproved.Count,
            TotalDatasetSamples = approvedWithRealBox.Count,
            F1 = benchmark.F1,
            Map50 = job.Metrics?.Map50 ?? 0,
            Map50_95 = job.Metrics?.Map50_95 ?? 0
        });
        manifest.TrainedSampleKeys = approvedWithRealBox
            .Select(GetSampleKey)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        // Write-After-Success: Reload ZUERST, Manifest nur bei Erfolg persistieren
        try
        {
            await _sidecar.ReloadYoloModelAsync(new ModelReloadRequestDto(versionPath), ct)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // Manifest wurde NICHT geschrieben → altes Modell bleibt aktiv, kein Rollback noetig
            Log($"YOLO-Reload fehlgeschlagen fuer {versionFileName}: {ex.Message}");
            return YoloRetrainResult.Failed(
                $"Deploy-Reload fehlgeschlagen: {ex.Message}",
                newApproved.Count, benchmark.F1);
        }

        // Reload erfolgreich → jetzt persistieren
        await PruneOldVersionsAsync(manifest, modelDir, ct).ConfigureAwait(false);
        await SaveManifestAsync(manifestPath, manifest, ct).ConfigureAwait(false);
        await SaveActivePointerAsync(modelDir, versionFileName, ct).ConfigureAwait(false);

        Log($"YOLO Auto-Retrain: Deploy erfolgreich ({versionFileName}, F1={benchmark.F1:P1}).");
        return YoloRetrainResult.CreateDeployed(versionPath, newApproved.Count, benchmark.F1);
    }

    private async Task<YoloTrainJobStatusDto> WaitForTrainJobAsync(string jobId, CancellationToken ct)
    {
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            var status = await _sidecar.GetYoloTrainingJobAsync(jobId, ct).ConfigureAwait(false);
            switch (status.Status?.ToLowerInvariant())
            {
                case "completed":
                case "failed":
                case "cancelled":
                    return status;
            }

            if (status.DatasetQuality is not null)
            {
                Log($"  Train-Job {jobId}: {status.Status}, labels={status.DatasetQuality.TotalLabels}, fallback={status.DatasetQuality.FallbackRatio:P1}");
            }
            else
            {
                Log($"  Train-Job {jobId}: {status.Status}");
            }

            await Task.Delay(TimeSpan.FromSeconds(5), ct).ConfigureAwait(false);
        }
    }

    private async Task TryRollbackAsync(string? previousModelPath, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(previousModelPath))
            return;
        try
        {
            await _sidecar.ReloadYoloModelAsync(new ModelReloadRequestDto(previousModelPath), ct).ConfigureAwait(false);
            Log($"Rollback auf vorheriges Modell: {previousModelPath}");
        }
        catch (Exception ex)
        {
            Log($"Rollback fehlgeschlagen: {ex.Message}");
        }
    }

    private static string NormalizeClassName(string code)
    {
        if (string.IsNullOrWhiteSpace(code)) return "";
        var dotIdx = code.IndexOf('.');
        if (dotIdx > 0) code = code[..dotIdx];
        return code.Length >= 3 ? code[..3].ToUpperInvariant() : code.ToUpperInvariant();
    }

    private static string GetSampleKey(TrainingSample sample)
        => !string.IsNullOrWhiteSpace(sample.Signature) ? sample.Signature : sample.SampleId;

    private string ResolveCandidatePath(string modelPath)
    {
        if (Path.IsPathRooted(modelPath))
            return Path.GetFullPath(modelPath);
        return Path.GetFullPath(Path.Combine(_sidecarRootDir, modelPath));
    }

    private static string? ResolveExistingModelPath(
        string activeModel,
        string modelDir,
        string? healthPath,
        string sidecarRoot)
    {
        if (!string.IsNullOrWhiteSpace(activeModel))
        {
            var local = Path.GetFullPath(Path.Combine(modelDir, activeModel));
            if (File.Exists(local)) return local;
        }

        if (!string.IsNullOrWhiteSpace(healthPath))
        {
            var resolved = Path.IsPathRooted(healthPath)
                ? Path.GetFullPath(healthPath)
                : Path.GetFullPath(Path.Combine(sidecarRoot, healthPath));
            if (File.Exists(resolved)) return resolved;
        }

        return null;
    }

    private static string? ResolveModelPathFromHealth(SidecarHealthResponse? health)
    {
        var path = health?.Yolo?.ResolvedModelPath;
        if (string.IsNullOrWhiteSpace(path))
            path = health?.Yolo?.ActiveModelPath;
        return string.IsNullOrWhiteSpace(path) ? null : path;
    }

    private static int GetNextVersionNumber(YoloModelManifest manifest)
    {
        var max = 0;
        foreach (var version in manifest.Versions)
        {
            var name = version.Version ?? "";
            if (!name.StartsWith("yolo_v", StringComparison.OrdinalIgnoreCase) || !name.EndsWith(".pt", StringComparison.OrdinalIgnoreCase))
                continue;
            var middle = name["yolo_v".Length..^".pt".Length];
            if (int.TryParse(middle, out var n))
                max = Math.Max(max, n);
        }
        return max + 1;
    }

    private static async Task PruneOldVersionsAsync(YoloModelManifest manifest, string modelDir, CancellationToken ct)
    {
        // H14-Fix: Aktives Modell darf niemals geloescht werden
        var activeModel = manifest.ActiveModel;

        var toKeep = manifest.Versions
            .OrderByDescending(v => v.CreatedUtc)
            .Take(MaxArchivedVersions)
            .Select(v => v.Version)
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var toRemove = manifest.Versions
            .Where(v => !toKeep.Contains(v.Version))
            .ToList();

        foreach (var old in toRemove)
        {
            ct.ThrowIfCancellationRequested();

            // H14-Fix: Aktives Modell nicht loeschen
            if (string.Equals(old.Version, activeModel, StringComparison.OrdinalIgnoreCase))
                continue;

            if (!string.IsNullOrWhiteSpace(old.Version))
            {
                var oldPath = Path.Combine(modelDir, old.Version);
                TryDelete(oldPath);
                TryDelete(Path.ChangeExtension(oldPath, ".engine"));
            }
            manifest.Versions.Remove(old);
        }

        await Task.CompletedTask.ConfigureAwait(false);
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch
        {
            // best-effort cleanup
        }
    }

    private static async Task<YoloModelManifest> LoadManifestAsync(string path, CancellationToken ct)
    {
        if (!File.Exists(path)) return new YoloModelManifest();
        try
        {
            var json = await File.ReadAllTextAsync(path, ct).ConfigureAwait(false);
            return JsonSerializer.Deserialize<YoloModelManifest>(json, JsonOpts) ?? new YoloModelManifest();
        }
        catch (Exception ex)
        {
            // H15-Fix: Warnung loggen statt stillem Fallback
            System.Diagnostics.Debug.WriteLine(
                $"[YoloRetrainOrchestrator] Manifest korrupt ({path}): {ex.Message} – versuche Backup.");

            // Backup versuchen (.bak)
            var bakPath = path + ".bak";
            if (File.Exists(bakPath))
            {
                try
                {
                    var bakJson = await File.ReadAllTextAsync(bakPath, ct).ConfigureAwait(false);
                    var restored = JsonSerializer.Deserialize<YoloModelManifest>(bakJson, JsonOpts);
                    if (restored is not null)
                    {
                        System.Diagnostics.Debug.WriteLine(
                            $"[YoloRetrainOrchestrator] Manifest aus Backup wiederhergestellt: {bakPath}");
                        return restored;
                    }
                }
                catch
                {
                    // Backup ebenfalls korrupt – leeres Manifest zurueckgeben
                }
            }

            return new YoloModelManifest();
        }
    }

    private static async Task SaveManifestAsync(string path, YoloModelManifest manifest, CancellationToken ct)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        // H15-Fix: Vor dem Schreiben bestehende Datei als Backup sichern
        if (File.Exists(path))
        {
            try { File.Copy(path, path + ".bak", overwrite: true); }
            catch { /* Best-effort Backup */ }
        }

        var tmp = path + ".tmp";
        var json = JsonSerializer.Serialize(manifest, JsonOpts);
        await File.WriteAllTextAsync(tmp, json, ct).ConfigureAwait(false);
        File.Move(tmp, path, overwrite: true);
    }

    private static async Task SaveActivePointerAsync(string modelDir, string activeModel, CancellationToken ct)
    {
        var path = Path.Combine(modelDir, "active.json");
        var tmp = path + ".tmp";
        var json = JsonSerializer.Serialize(new { active_model = activeModel }, JsonOpts);
        await File.WriteAllTextAsync(tmp, json, ct).ConfigureAwait(false);
        File.Move(tmp, path, overwrite: true);
    }

    private void Log(string message) => _log?.Invoke(message);
}

public sealed class YoloModelManifest
{
    public string ActiveModel { get; set; } = "";
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
    public List<YoloModelVersion> Versions { get; set; } = [];
    public List<string> TrainedSampleKeys { get; set; } = [];
}

public sealed class YoloModelVersion
{
    public string Version { get; set; } = "";
    public string ModelPath { get; set; } = "";
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public int NewApprovedSamples { get; set; }
    public int TotalDatasetSamples { get; set; }
    public double F1 { get; set; }
    public double Map50 { get; set; }
    public double Map50_95 { get; set; }
}

public sealed class YoloRetrainResult
{
    public bool Attempted { get; init; }
    public bool Deployed { get; init; }
    public bool RejectedByGate { get; init; }
    public string StatusText { get; init; } = "";
    public string? ActiveModelPath { get; init; }
    public int NewApprovedSamples { get; init; }
    public double? BenchmarkF1 { get; init; }

    public static YoloRetrainResult Skipped(string status, int newSamples = 0) => new()
    {
        Attempted = false,
        Deployed = false,
        RejectedByGate = false,
        StatusText = status,
        NewApprovedSamples = newSamples
    };

    public static YoloRetrainResult Failed(string status, int newSamples = 0, double? f1 = null) => new()
    {
        Attempted = true,
        Deployed = false,
        RejectedByGate = false,
        StatusText = status,
        NewApprovedSamples = newSamples,
        BenchmarkF1 = f1
    };

    public static YoloRetrainResult Rejected(string status, int newSamples = 0, double? f1 = null) => new()
    {
        Attempted = true,
        Deployed = false,
        RejectedByGate = true,
        StatusText = status,
        NewApprovedSamples = newSamples,
        BenchmarkF1 = f1
    };

    public static YoloRetrainResult CreateDeployed(string modelPath, int newSamples = 0, double? f1 = null) => new()
    {
        Attempted = true,
        Deployed = true,
        RejectedByGate = false,
        StatusText = "YOLO-Retrain erfolgreich deployt.",
        ActiveModelPath = modelPath,
        NewApprovedSamples = newSamples,
        BenchmarkF1 = f1
    };
}
