using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase;

namespace AuswertungPro.Next.Infrastructure.Ai.Training;

/// <summary>
/// Dateibasierter Trainingssample-Speicher. Ein pfad-basiertes Lock schuetzt alle
/// Lese-Aendern-Schreiben-Ablaeufe vor verlorenen Aktualisierungen — auch wenn mehrere
/// Instanzen (z. B. die ServiceProvider-Instanz und die statische Fassade) auf DIESELBE
/// Datei zeigen.
/// </summary>
public sealed class TrainingSampleFileStore : ITrainingSampleStore
{
    private const string DefaultEvalSetRoot = @"C:\KI_BRAIN\eval_set";

    // Ein Lock je Zieldatei, ueber ALLE Instanzen geteilt. Getrennte Instanz-Locks auf
    // derselben Datei koennten sonst parallele Merges verlieren (Lost Update).
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> FileLocks =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly Func<string> _storagePathProvider;
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

    // Das ueber alle Instanzen geteilte Lock fuer die aktuelle Zieldatei.
    private SemaphoreSlim FileLock => FileLocks.GetOrAdd(StoragePath, static _ => new SemaphoreSlim(1, 1));

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
        var fileLock = FileLock;
        await fileLock.WaitAsync().ConfigureAwait(false);
        try
        {
            return FilterEvalContamination(await LoadInternalAsync().ConfigureAwait(false));
        }
        finally
        {
            fileLock.Release();
        }
    }

    public async Task SaveAsync(List<TrainingSample> samples)
    {
        var fileLock = FileLock;
        await fileLock.WaitAsync().ConfigureAwait(false);
        try
        {
            await SaveInternalAsync(FilterEvalContamination(samples)).ConfigureAwait(false);
        }
        finally
        {
            fileLock.Release();
        }
    }

    public Task MergeAndSaveAsync(List<TrainingSample> samples)
        => MergeAndSaveCoreAsync(samples, reportSample: null, CancellationToken.None);

    /// <inheritdoc cref="ITrainingSampleStore.TryAddNewAsync"/>
    public Task<bool> TryAddNewAsync(TrainingSample sample, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(sample);
        return MergeAndSaveCoreAsync(new List<TrainingSample> { sample }, reportSample: sample, ct);
    }

    /// <summary>
    /// Signatur-Dedup-Anhang unter dem Datei-Lock (gemeinsame Logik fuer MergeAndSaveAsync
    /// und TryAddNewAsync). Mit <paramref name="reportSample"/> wird rueckgemeldet, ob genau
    /// dieses Sample angehaengt wurde; ohne Meldung (MergeAndSaveAsync) immer true.
    /// </summary>
    private async Task<bool> MergeAndSaveCoreAsync(
        List<TrainingSample> samples,
        TrainingSample? reportSample,
        CancellationToken ct)
    {
        var fileLock = FileLock;
        await fileLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var incoming = FilterEvalContamination(samples);
            var existing = FilterEvalContamination(await LoadInternalAsync().ConfigureAwait(false));
            var signatures = existing
                .SelectMany(GetDedupSignatures)
                .ToHashSet(StringComparer.Ordinal);

            var reportAdded = false;
            foreach (var sample in incoming)
            {
                var sampleSignatures = GetDedupSignatures(sample).ToArray();
                if (sampleSignatures.Any(signatures.Contains))
                    continue;

                existing.Add(sample);
                if (ReferenceEquals(sample, reportSample))
                    reportAdded = true;
                foreach (var signature in sampleSignatures)
                    signatures.Add(signature);
            }

            await SaveInternalAsync(existing).ConfigureAwait(false);
            return reportSample is null || reportAdded;
        }
        finally
        {
            fileLock.Release();
        }
    }

    private static IEnumerable<string> GetDedupSignatures(TrainingSample sample)
    {
        if (!string.IsNullOrEmpty(sample.Signature))
            yield return sample.Signature;

        if (!sample.HasBbox)
            yield break;

        yield return TrainingSample.BuildCanonicalSignature(
            sample.CaseId,
            sample.Code,
            sample.MeterStart,
            sample.MeterEnd,
            sample.BboxXCenter,
            sample.BboxYCenter,
            sample.BboxWidth,
            sample.BboxHeight);
    }

    public async Task MergeOrUpdateAsync(IEnumerable<TrainingSample> samples)
    {
        var fileLock = FileLock;
        await fileLock.WaitAsync().ConfigureAwait(false);
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
                // Primaer per stabiler SampleId matchen: eine Codekorrektur aendert die
                // Signatur (enthaelt den Code) — ein Update mit gleicher Id muss trotzdem
                // denselben Datensatz treffen (kein Dublett).
                var index = existing.FindIndex(
                    candidate => string.Equals(candidate.SampleId, sample.SampleId, StringComparison.Ordinal));

                // Fallback per Signatur fuer Alt-Aufrufer ohne stabile Id-Zuordnung
                // (bisheriges Verhalten, unveraendert).
                if (index < 0
                    && !string.IsNullOrEmpty(sample.Signature)
                    && signatureIndex.TryGetValue(sample.Signature, out var signatureMatch))
                {
                    index = signatureMatch;
                }

                if (index >= 0)
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
            fileLock.Release();
        }
    }

    /// <inheritdoc cref="ITrainingSampleStore.RemoveBySampleIdAsync"/>
    public async Task<bool> RemoveBySampleIdAsync(string sampleId)
    {
        var fileLock = FileLock;
        await fileLock.WaitAsync().ConfigureAwait(false);
        try
        {
            // Ungefiltert laden (LoadInternalAsync): der Eval-Filter darf hier keinen
            // fremden Bestand verwerfen — entfernt wird gezielt per SampleId.
            var existing = await LoadInternalAsync().ConfigureAwait(false);
            var removed = existing.RemoveAll(
                sample => string.Equals(sample.SampleId, sampleId, StringComparison.Ordinal));
            if (removed == 0)
                return false;

            await SaveInternalAsync(existing).ConfigureAwait(false);
            return true;
        }
        finally
        {
            fileLock.Release();
        }
    }

    /// <inheritdoc cref="ITrainingSampleStore.ReplaceBySampleIdAsync"/>
    public async Task<bool> ReplaceBySampleIdAsync(TrainingSample sample)
    {
        ArgumentNullException.ThrowIfNull(sample);
        var fileLock = FileLock;
        await fileLock.WaitAsync().ConfigureAwait(false);
        try
        {
            // Ungefiltert laden: entfernt/ersetzt wird gezielt per SampleId, der Eval-Filter
            // darf keinen fremden Bestand verwerfen. EIN Lock fuer Loeschen + Anhaengen +
            // Speichern — zwischen den Schritten kann kein anderer Schreiber dazwischenkommen.
            var existing = await LoadInternalAsync().ConfigureAwait(false);
            var index = existing.FindIndex(
                candidate => string.Equals(candidate.SampleId, sample.SampleId, StringComparison.Ordinal));
            if (index < 0)
                return false;   // unbekannte Id: NICHTS schreiben (Aufrufer behandelt)

            var signatureOwner = existing.Find(candidate =>
                !string.Equals(candidate.SampleId, sample.SampleId, StringComparison.Ordinal)
                && !string.IsNullOrWhiteSpace(sample.Signature)
                && string.Equals(candidate.Signature, sample.Signature, StringComparison.Ordinal));
            if (signatureOwner is not null)
            {
                throw new InvalidOperationException(
                    $"Die Signatur des Samples '{sample.SampleId}' gehoert bereits zu " +
                    $"Sample '{signatureOwner.SampleId}'. Der bestehende Gold-Datenbestand " +
                    "wurde nicht veraendert.");
            }

            existing.RemoveAt(index);
            existing.Add(sample);
            await SaveInternalAsync(existing).ConfigureAwait(false);
            return true;
        }
        finally
        {
            fileLock.Release();
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

        // Kein lesbarer Stand mehr: Hier darf NICHT eine leere Liste zurueckgegeben
        // werden. Die Hauptdatei existierte (sonst waere dieser Pfad nie erreicht
        // worden) — leer weiterzugeben liesse den naechsten Speichervorgang den
        // vorhandenen Bestand mit fast nichts ueberschreiben. Unlesbar ist ein
        // Fehler, nicht ein Erstlauf. Dieselbe Unterscheidung gilt fuer die
        // Kostendateien (CostStoreFileProbe): fehlend = leer, unlesbar = Fehler.
        throw new InvalidOperationException(
            $"Die Trainingsdaten sind nicht lesbar, und keine Sicherungskopie ist "
            + $"lesbar ({path}). Der vorhandene Bestand wurde NICHT veraendert — "
            + $"es wird nichts gespeichert. Letzter Lesefehler: {error.Message}",
            error);
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

            await ReplaceAtomicallyAsync(tempPath, path).ConfigureAwait(false);
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

    /// <summary>
    /// Wartezeiten in Millisekunden zwischen zwei Ersetzungsversuchen (rund 4,5 s gesamt).
    /// </summary>
    private static readonly int[] ReplaceRetryDelaysMs = [50, 100, 200, 400, 800, 1000, 1000, 1000];

    /// <summary>
    /// Ersetzt die Zieldatei atomar und wiederholt den Schritt bei einer voruebergehenden
    /// Sperre. Windows verweigert eine Ersetzung, solange ein anderer Leser die Zieldatei
    /// geoeffnet haelt — der eigene Spiegeldienst, ein Virenscanner oder eine
    /// Dateivorschau reichen dafuer. Der Freigabemodus des Lesers hilft dabei nicht.
    /// Da die neue Datei zu diesem Zeitpunkt bereits vollstaendig geschrieben und
    /// geprueft ist, ist Warten die richtige Antwort; ein Abbruch wuerde den ganzen
    /// Speichervorgang verwerfen. Bleibt die Sperre bestehen, wird der Fehler gemeldet.
    /// </summary>
    internal static async Task ReplaceAtomicallyAsync(
        string tempPath,
        string targetPath,
        Action<string, string>? move = null,
        Func<int, Task>? delay = null)
    {
        move ??= static (source, target) => File.Move(source, target, overwrite: true);
        delay ??= static milliseconds => Task.Delay(milliseconds);

        for (var attempt = 0; ; attempt++)
        {
            try
            {
                move(tempPath, targetPath);
                if (attempt > 0)
                {
                    BestEffort.ReportWarning(
                        $"Trainingsdaten: Zieldatei war kurz gesperrt, Ersetzung nach "
                        + $"{attempt + 1} Versuchen erfolgreich ({Path.GetFileName(targetPath)}).");
                }

                return;
            }
            catch (Exception ex) when (attempt < ReplaceRetryDelaysMs.Length
                                       && ex is UnauthorizedAccessException or IOException)
            {
                await delay(ReplaceRetryDelaysMs[attempt]).ConfigureAwait(false);
            }
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
