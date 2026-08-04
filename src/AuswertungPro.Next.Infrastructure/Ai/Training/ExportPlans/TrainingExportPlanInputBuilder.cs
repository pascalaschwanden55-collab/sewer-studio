using System.Security.Cryptography;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Application.Ai.Training.ClassMaps;
using AuswertungPro.Next.Application.Ai.Training.ExportPlans;
using AuswertungPro.Next.Application.Ai.Training.Inventory;
using AuswertungPro.Next.Infrastructure.Ai.Training.Inventory;

namespace AuswertungPro.Next.Infrastructure.Ai.Training.ExportPlans;

/// <summary>
/// Baut den Planner-Input ausschliesslich aus einem konsistenten AP-0.1-Live-Snapshot.
/// Als Trainingsquelle werden ausschliesslich zuvor persoenlich freigegebene
/// TrainingSamples uebernommen. Teacher-Daten bleiben Teil des Inventars,
/// werden aber nicht in den Trainingsplan aufgenommen.
/// </summary>
public sealed class TrainingExportPlanInputBuilder : ITrainingExportPlanInputBuilder
{
    public async Task<TrainingExportPlanRequest> BuildAsync(
        TrainingDataInventoryRuntimeSnapshot inventory,
        TrainingExportRegistrySnapshot registry,
        IReadOnlySet<string> approvedTrainingSampleIds,
        TrainingYoloClassMapSnapshot classMap,
        DateTimeOffset generatedUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(approvedTrainingSampleIds);
        ArgumentNullException.ThrowIfNull(classMap);

        ValidateInventoryGate(inventory);
        ValidateProtectedSets(inventory.Protection, registry.ProtectedSets);
        var sourceHashes = ReadCurrentSourceHashes(inventory.Report);
        var candidates = new List<TrainingExportPlanCandidate>(approvedTrainingSampleIds.Count);
        await AddTrainingSampleCandidatesAsync(
                inventory,
                approvedTrainingSampleIds,
                candidates,
                cancellationToken)
            .ConfigureAwait(false);
        var negativeImages = await BuildVerifiedNegativeImagesAsync(
                registry,
                inventory,
                classMap,
                cancellationToken)
            .ConfigureAwait(false);

        return new TrainingExportPlanRequest(
            candidates,
            classMap,
            registry,
            inventory.Report.RunId,
            sourceHashes,
            EvaluationProtectionComplete: inventory.Protection.Status.Complete,
            ProtectedImageHashes: inventory.Protection.ImageHashes,
            ProtectedHoldingKeys: inventory.Protection.HoldingKeys,
            GeneratedUtc: generatedUtc)
        {
            NegativeImages = negativeImages
        };
    }

    /// <summary>
    /// Verifiziert die kuratierten Negativbilder gegen den Live-Bestand: Datei muss
    /// unveraendert zum Register-Hash sein, und ein Eval-/Abnahme-Bild darf NIE ins
    /// Training — Kontamination ist ein harter Stopp (kein stiller Ausschluss).
    /// </summary>
    private static async Task<IReadOnlyList<TrainingExportNegativeImage>> BuildVerifiedNegativeImagesAsync(
        TrainingExportRegistrySnapshot registry,
        TrainingDataInventoryRuntimeSnapshot inventory,
        TrainingYoloClassMapSnapshot classMap,
        CancellationToken cancellationToken)
    {
        if (registry.NegativeImages.Count == 0)
            return [];

        var result = new List<TrainingExportNegativeImage>(registry.NegativeImages.Count);
        foreach (var negative in registry.NegativeImages)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var actualSha256 = await ComputeStableSha256Async(negative.Path, cancellationToken)
                .ConfigureAwait(false);
            if (!actualSha256.Equals(negative.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                throw new TrainingExportPlanException(
                    $"Negativbild wurde seit der Register-Freigabe veraendert: {negative.Path}");
            }
            if (inventory.Protection.ImageHashes.Contains(actualSha256))
            {
                throw new TrainingExportPlanException(
                    $"Negativbild gehoert zum eingefrorenen Eval-/Abnahme-Set: {negative.Path}");
            }
            if (negative.HoldingKey is not null
                && EvalContaminationGuard.IsEvalHaltung(
                    inventory.Protection.HoldingKeys,
                    negative.HoldingKey))
            {
                throw new TrainingExportPlanException(
                    $"Negativbild-Haltung gehoert zum eingefrorenen Eval-/Abnahme-Set: {negative.HoldingKey}");
            }
            if (negative.HoldingKey is not null)
                ValidateStrictNegativeClassMapBinding(negative, classMap);

            result.Add(negative with { Sha256 = actualSha256 });
        }

        return result;
    }

    private static void ValidateStrictNegativeClassMapBinding(
        TrainingExportNegativeImage negative,
        TrainingYoloClassMapSnapshot classMap)
    {
        var activeClassIds = classMap.Classes
            .OrderBy(item => item.Value)
            .Select(item => item.Value)
            .ToArray();
        if (negative.ClassMapVersion != 3
            || classMap.Version != 3
            || classMap.ClassMapSha256 is null
            || !string.Equals(
                negative.ClassMapSha256,
                classMap.ClassMapSha256,
                StringComparison.Ordinal)
            || !string.Equals(
                negative.VsaManifestHash,
                classMap.VsaManifestHash,
                StringComparison.Ordinal)
            || activeClassIds.Length != 15
            || !activeClassIds.SequenceEqual(Enumerable.Range(0, 15))
            || !string.Equals(
                classMap.OrderedClassNames[14],
                "BCC_bogen",
                StringComparison.Ordinal))
        {
            throw new TrainingExportPlanException(
                "Das strikte Negativ-Set passt nicht zur aktuell aktiven Detect-Klassenkarte v3.");
        }
    }

    private static void ValidateInventoryGate(TrainingDataInventoryRuntimeSnapshot inventory)
    {
        if (!TrainingInventoryExitPolicy.IsSuccessful(inventory.Report))
            throw new TrainingExportPlanException("Der aktuelle AP-0.1-Inventarlauf ist nicht fehlerfrei.");
        if (!inventory.Protection.Status.Complete
            || !inventory.Protection.Status.ImageHashCheckEnabled
            || inventory.Report.Summary.Triage.EvaluationNotChecked != 0)
        {
            throw new TrainingExportPlanException(
                "Der Live-Inventarlauf hat Eval/Abnahme nicht vollstaendig geprueft.");
        }
        if (inventory.Report.Sources.Any(source => source.Role != TrainingInventorySourceRole.Current))
        {
            throw new TrainingExportPlanException(
                "Der Export-Snapshot darf keine Backup- oder Legacy-Quellen enthalten.");
        }
    }

    private static void ValidateProtectedSets(
        TrainingInventoryProtectionSnapshot protection,
        IReadOnlyList<TrainingExportProtectedSetReference> expectedSets)
    {
        var actual = protection.Sets.ToDictionary(
            set => set.SetId,
            StringComparer.OrdinalIgnoreCase);
        if (actual.Count != expectedSets.Count)
            throw new TrainingExportPlanException("Schutz-Set-Register und Live-Inventar sind nicht deckungsgleich.");
        foreach (var expected in expectedSets)
        {
            if (!actual.TryGetValue(expected.SetId, out var set)
                || !set.ManifestSha256.Equals(expected.ManifestSha256, StringComparison.OrdinalIgnoreCase))
            {
                throw new TrainingExportPlanException(
                    $"Schutz-Set '{expected.SetId}' fehlt oder sein Manifest wurde veraendert.");
            }
        }
    }

    private static IReadOnlyDictionary<string, string> ReadCurrentSourceHashes(
        TrainingDataInventoryReport report)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var source in report.Sources.Where(source => source.Role == TrainingInventorySourceRole.Current))
        {
            if (source.ParseState != TrainingInventoryParseState.Parsed
                || source.Sha256 is not { Length: 64 })
            {
                throw new TrainingExportPlanException(
                    $"Aktuelle Inventarquelle '{source.Path}' hat keinen sicheren Snapshot-Hash.");
            }
            var key = Path.GetFileName(source.Path);
            if (string.IsNullOrWhiteSpace(key) || !result.TryAdd(key, source.Sha256.ToLowerInvariant()))
                throw new TrainingExportPlanException($"Doppelte oder ungueltige Inventarquelle '{source.Path}'.");
        }
        return result;
    }

    private static async Task AddTrainingSampleCandidatesAsync(
        TrainingDataInventoryRuntimeSnapshot inventory,
        IReadOnlySet<string> approvedIds,
        ICollection<TrainingExportPlanCandidate> candidates,
        CancellationToken cancellationToken)
    {
        var approved = new HashSet<string>(
            approvedIds.Where(id => !string.IsNullOrWhiteSpace(id)).Select(id => id.Trim()),
            StringComparer.OrdinalIgnoreCase);
        if (approved.Count != approvedIds.Count)
            throw new TrainingExportPlanException("Freigegebene TrainingSamples enthalten eine leere oder doppelte ID.");
        var samplesById = new Dictionary<string, TrainingSample>(StringComparer.OrdinalIgnoreCase);
        foreach (var sample in inventory.TrainingSamples)
        {
            if (string.IsNullOrWhiteSpace(sample.SampleId))
                continue;
            if (!samplesById.TryAdd(sample.SampleId.Trim(), sample))
                throw new TrainingExportPlanException($"TrainingSample-ID '{sample.SampleId}' steht mehrfach.");
        }
        var missingIds = approved.Where(id => !samplesById.ContainsKey(id)).ToArray();
        if (missingIds.Length > 0)
        {
            throw new TrainingExportPlanException(
                $"Freigegebene TrainingSamples fehlen im Live-Snapshot: {string.Join(", ", missingIds)}");
        }

        foreach (var id in approved.OrderBy(value => value, StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var sample = samplesById[id];
            var disposition = ClassifySampleBeforeHash(sample);
            string? imageSha256 = null;
            if (disposition == TrainingInventoryDisposition.TrainValCandidate)
            {
                imageSha256 = await ComputeStableSha256Async(sample.FramePath, cancellationToken)
                    .ConfigureAwait(false);
                var holdingKey = EvalContaminationGuard.NormalizeHaltungKey(sample.CaseId);
                if (inventory.Protection.ImageHashes.Contains(imageSha256)
                    || (!string.IsNullOrWhiteSpace(holdingKey)
                        && inventory.Protection.HoldingKeys.Contains(holdingKey)))
                {
                    disposition = TrainingInventoryDisposition.EvaluationLocked;
                }
            }

            candidates.Add(new TrainingExportPlanCandidate(
                new TrainingExportSourceRef(TrainingExportSourceType.TrainingSample, id),
                sample.FramePath?.Trim() ?? string.Empty,
                imageSha256,
                TryGetExtension(sample.FramePath),
                sample.CaseId,
                sample.Code,
                TrainingYoloClassSourceKinds.TeacherVsaCode,
                ToBox(sample),
                disposition));
        }
    }

    private static TrainingInventoryDisposition ClassifySampleBeforeHash(TrainingSample sample)
    {
        if (string.IsNullOrWhiteSpace(sample.FramePath) || !File.Exists(sample.FramePath))
            return TrainingInventoryDisposition.Archive;
        if (string.IsNullOrWhiteSpace(EvalContaminationGuard.NormalizeHaltungKey(sample.CaseId)))
            return TrainingInventoryDisposition.QuarantineOrigin;
        var box = ToBox(sample);
        if (box?.IsValid != true)
            return TrainingInventoryDisposition.QuarantineGeometry;
        return TrainingInventoryDisposition.TrainValCandidate;
    }

    private static TrainingExportBoundingBox? ToBox(TrainingSample sample)
        => sample.HasBbox
            ? new TrainingExportBoundingBox(
                sample.BboxXCenter!.Value,
                sample.BboxYCenter!.Value,
                sample.BboxWidth!.Value,
                sample.BboxHeight!.Value)
            : null;

    private static string? TryGetExtension(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;
        try
        {
            return Path.GetExtension(path);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }
    }

    private static async Task<string> ComputeStableSha256Async(
        string path,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var reparsePoint = TrainingInventoryPaths.FindReparsePoint(path);
            if (reparsePoint is not null)
                throw new TrainingExportPlanException($"Sample-Bildpfad enthaelt eine Verknuepfung: {reparsePoint}");
            var before = new FileInfo(path);
            before.Refresh();
            if (!before.Exists)
                throw new TrainingExportPlanException($"Sample-Bild fehlt: {path}");

            string hash;
            await using (var stream = new FileStream(
                             path,
                             FileMode.Open,
                             FileAccess.Read,
                             FileShare.Read,
                             bufferSize: 128 * 1024,
                             FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                hash = Convert.ToHexStringLower(
                    await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false));
            }
            var after = new FileInfo(path);
            after.Refresh();
            if (after.Exists
                && before.Length == after.Length
                && before.LastWriteTimeUtc == after.LastWriteTimeUtc)
            {
                return hash;
            }
        }

        throw new TrainingExportPlanException($"Sample-Bild wurde waehrend des Hashens veraendert: {path}");
    }
}
