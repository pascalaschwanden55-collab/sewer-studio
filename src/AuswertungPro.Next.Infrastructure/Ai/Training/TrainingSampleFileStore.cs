using System.Diagnostics;
using System.Text.Json;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase;

namespace AuswertungPro.Next.Infrastructure.Ai.Training;

/// <summary>
/// Dateibasierter Trainingssample-Speicher. Ein Instanz-Lock schuetzt alle
/// Lese-Aendern-Schreiben-Ablaeufe vor verlorenen Aktualisierungen.
/// </summary>
public sealed class TrainingSampleFileStore : ITrainingSampleStore
{
    private const string DefaultEvalSetRoot = @"C:\KI_BRAIN\eval_set";
    private readonly Func<string> _storagePathProvider;
    private readonly SemaphoreSlim _fileLock = new(1, 1);
    private string? _configuredEvalSetRoot;

    public TrainingSampleFileStore()
        : this(() => KnowledgeBasePaths.GetTrainingSamplesPath())
    {
    }

    public TrainingSampleFileStore(string storagePath)
        : this(() => storagePath)
    {
        if (string.IsNullOrWhiteSpace(storagePath))
            throw new ArgumentException("Der Speicherpfad darf nicht leer sein.", nameof(storagePath));
    }

    internal TrainingSampleFileStore(Func<string> storagePathProvider)
    {
        _storagePathProvider = storagePathProvider
            ?? throw new ArgumentNullException(nameof(storagePathProvider));
    }

    public string StoragePath => Path.GetFullPath(_storagePathProvider());

    public string EffectiveEvalSetRoot =>
        Volatile.Read(ref _configuredEvalSetRoot)
        ?? Environment.GetEnvironmentVariable("SEWERSTUDIO_EVAL_SET_ROOT")
        ?? DefaultEvalSetRoot;

    public void ConfigureEvalProtection(string? evalSetRoot) =>
        Volatile.Write(
            ref _configuredEvalSetRoot,
            string.IsNullOrWhiteSpace(evalSetRoot) ? null : Path.GetFullPath(evalSetRoot));

    public async Task<List<TrainingSample>> LoadAsync()
    {
        await _fileLock.WaitAsync().ConfigureAwait(false);
        try
        {
            return FilterEvalContamination(await LoadInternalAsync().ConfigureAwait(false));
        }
        finally
        {
            _fileLock.Release();
        }
    }

    public async Task SaveAsync(List<TrainingSample> samples)
    {
        await _fileLock.WaitAsync().ConfigureAwait(false);
        try
        {
            await SaveInternalAsync(FilterEvalContamination(samples)).ConfigureAwait(false);
        }
        finally
        {
            _fileLock.Release();
        }
    }

    public async Task MergeAndSaveAsync(List<TrainingSample> samples)
    {
        await _fileLock.WaitAsync().ConfigureAwait(false);
        try
        {
            var incoming = FilterEvalContamination(samples);
            var existing = FilterEvalContamination(await LoadInternalAsync().ConfigureAwait(false));
            var signatures = existing
                .Where(sample => !string.IsNullOrEmpty(sample.Signature))
                .Select(sample => sample.Signature)
                .ToHashSet(StringComparer.Ordinal);

            foreach (var sample in incoming)
            {
                if (!string.IsNullOrEmpty(sample.Signature) && signatures.Contains(sample.Signature))
                    continue;

                existing.Add(sample);
                if (!string.IsNullOrEmpty(sample.Signature))
                    signatures.Add(sample.Signature);
            }

            await SaveInternalAsync(existing).ConfigureAwait(false);
        }
        finally
        {
            _fileLock.Release();
        }
    }

    public async Task MergeOrUpdateAsync(IEnumerable<TrainingSample> samples)
    {
        await _fileLock.WaitAsync().ConfigureAwait(false);
        try
        {
            var incoming = FilterEvalContamination(samples);
            var existing = FilterEvalContamination(await LoadInternalAsync().ConfigureAwait(false));
            var signatureIndex = new Dictionary<string, int>(StringComparer.Ordinal);
            for (var index = 0; index < existing.Count; index++)
            {
                if (!string.IsNullOrEmpty(existing[index].Signature))
                    signatureIndex[existing[index].Signature] = index;
            }

            foreach (var sample in incoming)
            {
                if (!string.IsNullOrEmpty(sample.Signature)
                    && signatureIndex.TryGetValue(sample.Signature, out var index))
                {
                    TrainingSampleMerge.ApplyUpdatableFields(existing[index], sample);
                    continue;
                }

                existing.Add(sample);
                if (!string.IsNullOrEmpty(sample.Signature))
                    signatureIndex[sample.Signature] = existing.Count - 1;
            }

            await SaveInternalAsync(existing).ConfigureAwait(false);
        }
        finally
        {
            _fileLock.Release();
        }
    }

    private List<TrainingSample> FilterEvalContamination(IEnumerable<TrainingSample>? samples)
    {
        var input = samples?.Where(sample => sample is not null).ToList() ?? [];
        if (input.Count == 0)
            return input;

        var hashes = EvalContaminationGuard.LoadEvalImageHashes(EffectiveEvalSetRoot);
        var holdings = EvalContaminationGuard.LoadEvalHaltungKeys(EffectiveEvalSetRoot);
        if (hashes.Count == 0 && holdings.Count == 0)
            return input;

        var clean = new List<TrainingSample>(input.Count);
        foreach (var sample in input)
        {
            var classification = EvalContaminationGuard.ClassifyForExport(
                hashes,
                holdings,
                sample.FramePath,
                sample.CaseId);
            if (classification == EvalContaminationGuard.ExportContaminationResult.Clean)
            {
                clean.Add(sample);
            }
            else
            {
                BestEffort.ReportWarning(
                    $"[TrainingSampleFileStore] Sample {sample.SampleId} blockiert: {classification}.");
            }
        }

        return clean;
    }

    private async Task<List<TrainingSample>> LoadInternalAsync()
    {
        var path = StoragePath;
        if (!File.Exists(path))
            return [];

        try
        {
            await using var stream = File.OpenRead(path);
            var samples = await JsonSerializer
                .DeserializeAsync<List<TrainingSample>>(stream)
                .ConfigureAwait(false);
            if (samples is null)
                return [];

            var migrated = false;
            foreach (var sample in samples)
            {
                if (string.IsNullOrEmpty(sample.Signature) || string.IsNullOrEmpty(sample.CaseId))
                    continue;

                if (sample.Signature.Split('|').Length == 3)
                {
                    sample.Signature = $"{sample.CaseId}|{sample.Signature}";
                    migrated = true;
                }
            }

            if (migrated)
            {
                Trace.WriteLine("[TrainingSampleFileStore] Signatur-Migration durchgefuehrt");
                await SaveInternalAsync(samples).ConfigureAwait(false);
            }

            return samples;
        }
        catch (Exception ex)
        {
            return await RecoverFromBackupAsync(path, ex).ConfigureAwait(false);
        }
    }

    private static async Task<List<TrainingSample>> RecoverFromBackupAsync(string path, Exception error)
    {
        var backup = path + $".bad_{DateTime.UtcNow:yyyyMMdd_HHmmss}";
        BestEffort.Try(
            () => File.Copy(path, backup),
            $"Trainingsdaten: korrupte Datei nach {backup} sichern");
        BestEffort.ReportWarning(
            $"[TrainingSampleFileStore] JSON korrupt, Backup unter {backup}: {error.Message}");

        var candidates = new List<string>();
        if (File.Exists(path + ".bak"))
            candidates.Add(path + ".bak");
        candidates.AddRange(Directory.GetFiles(
                Path.GetDirectoryName(path)!,
                Path.GetFileName(path) + ".bad_*")
            .OrderByDescending(candidate => candidate));

        foreach (var candidate in candidates)
        {
            try
            {
                await using var stream = File.OpenRead(candidate);
                var samples = await JsonSerializer
                    .DeserializeAsync<List<TrainingSample>>(stream)
                    .ConfigureAwait(false);
                if (samples is { Count: > 0 })
                    return samples;
            }
            catch (Exception ex)
            {
                BestEffort.ReportWarning(
                    $"Trainingsdaten-Backup nicht lesbar ({candidate}): {ex.Message}");
            }
        }

        BestEffort.ReportWarning(
            "[TrainingSampleFileStore] KRITISCH: Kein lesbares Backup gefunden, starte leer");
        return [];
    }

    private async Task SaveInternalAsync(List<TrainingSample> samples)
    {
        var path = StoragePath;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        RotateBackups(path);

        var tempPath = path + ".tmp";
        try
        {
            await using (var stream = File.Create(tempPath))
            {
                await JsonSerializer
                    .SerializeAsync(stream, samples, JsonDefaults.Indented)
                    .ConfigureAwait(false);
                await stream.FlushAsync().ConfigureAwait(false);
            }

            await using (var stream = File.OpenRead(tempPath))
            {
                var check = await JsonSerializer
                    .DeserializeAsync<List<TrainingSample>>(stream)
                    .ConfigureAwait(false);
                if (check is null || check.Count != samples.Count)
                {
                    throw new InvalidOperationException(
                        $"Validierung fehlgeschlagen: erwartet {samples.Count}, gelesen {check?.Count ?? 0}");
                }
            }

            File.Move(tempPath, path, overwrite: true);
            CleanupBadFiles(path);
        }
        catch
        {
            BestEffort.Try(
                () =>
                {
                    if (File.Exists(tempPath))
                        File.Delete(tempPath);
                },
                "Trainingsdaten: Temp-Datei nach Speicherfehler loeschen");
            throw;
        }
    }

    private static void RotateBackups(string path)
    {
        if (!File.Exists(path))
            return;

        try
        {
            if (File.Exists(path + ".bak.2"))
                BestEffort.Try(() => File.Copy(path + ".bak.2", path + ".bak.3", true), "Trainingsdaten-Backup .bak.2 nach .bak.3");
            if (File.Exists(path + ".bak"))
                BestEffort.Try(() => File.Copy(path + ".bak", path + ".bak.2", true), "Trainingsdaten-Backup .bak nach .bak.2");
            File.Copy(path, path + ".bak", overwrite: true);
        }
        catch (Exception ex)
        {
            BestEffort.ReportWarning($"Trainingsdaten-Backups konnten nicht rotiert werden: {ex.Message}");
        }
    }

    private static void CleanupBadFiles(string path)
    {
        try
        {
            var badFiles = Directory.GetFiles(
                    Path.GetDirectoryName(path)!,
                    Path.GetFileName(path) + ".bad_*")
                .OrderByDescending(file => file)
                .ToArray();
            for (var index = 3; index < badFiles.Length; index++)
            {
                var oldFile = badFiles[index];
                BestEffort.Try(
                    () => File.Delete(oldFile),
                    $"Trainingsdaten: altes Korruptionsbackup {Path.GetFileName(oldFile)} loeschen");
            }
        }
        catch (Exception ex)
        {
            BestEffort.ReportWarning(
                $"Alte Trainingsdaten-Korruptionsbackups konnten nicht aufgeraeumt werden: {ex.Message}");
        }
    }
}
