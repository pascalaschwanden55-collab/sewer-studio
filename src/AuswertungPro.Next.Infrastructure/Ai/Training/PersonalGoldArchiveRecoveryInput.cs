using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Infrastructure.Ai;

namespace AuswertungPro.Next.Infrastructure.Ai.Training;

/// <summary>
/// Liest und bindet den Recovery-Auftrag, Archivmarker, Quellbilder und
/// Vorher-Zustaende. Diese Klasse veraendert keine aktiven Recovery-Daten.
/// </summary>
internal static class PersonalGoldArchiveRecoveryInput
{
    public static ArchiveRecoveryPaths ValidateAndResolve(
        PersonalGoldArchiveRecoveryRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ConfirmedByUser))
            throw new ArgumentException("Bestaetiger fehlt.", nameof(request));

        var activeRoot = PersonalGoldBrainFileService.NormalizeRoot(
            request.ActiveKnowledgeRoot,
            "Aktiver Wissensordner");
        var legacyRoot = PersonalGoldBrainFileService.NormalizeRoot(
            request.LegacyKnowledgeRoot,
            "Altarchiv");
        var rootsEqual = string.Equals(
            activeRoot,
            legacyRoot,
            StringComparison.OrdinalIgnoreCase);
        if (rootsEqual && !request.DryRun)
            throw new InvalidDataException("Altarchiv und aktiver Wissensordner sind identisch.");
        if (!Directory.Exists(activeRoot))
            throw new DirectoryNotFoundException($"Aktiver Wissensordner fehlt: {activeRoot}");
        if (!Directory.Exists(legacyRoot))
            throw new DirectoryNotFoundException($"Altarchiv fehlt: {legacyRoot}");
        if ((File.GetAttributes(activeRoot) & FileAttributes.ReparsePoint) != 0
            || (File.GetAttributes(legacyRoot) & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidDataException(
                "Aktiver Wissensordner oder Altarchiv ist eine Verknuepfung.");
        }
        var previousActiveRoot = rootsEqual
            ? activeRoot
            : ReadPreviousActiveRootFromArchiveMarker(legacyRoot);
        if (!rootsEqual
            && !PersonalGoldBrainFileService.PathsEqual(
                previousActiveRoot,
                activeRoot))
        {
            throw new InvalidDataException(
                "Der Archivmarker gehoert nicht zum aktuell aktiven Wissensordner.");
        }

        var activeSamplesPath = Path.Combine(activeRoot, "training_samples.json");
        var activeDatabasePath = Path.Combine(activeRoot, "KnowledgeBase.db");
        var legacyDatabasePath = Path.Combine(legacyRoot, "KnowledgeBase.db");
        if (!File.Exists(activeSamplesPath))
            throw new FileNotFoundException(
                "Aktive training_samples.json fehlt.",
                activeSamplesPath);
        if (!File.Exists(activeDatabasePath))
            throw new FileNotFoundException(
                "Aktive KnowledgeBase.db fehlt.",
                activeDatabasePath);
        if (!File.Exists(legacyDatabasePath))
            throw new FileNotFoundException(
                "Archiv-KnowledgeBase.db fehlt.",
                legacyDatabasePath);

        return new ArchiveRecoveryPaths(
            activeRoot,
            legacyRoot,
            previousActiveRoot,
            activeSamplesPath,
            activeDatabasePath,
            legacyDatabasePath);
    }

    public static void EnsureActiveIsIdle(string activeRoot)
    {
        foreach (var path in new[]
                 {
                     Path.Combine(activeRoot, "KnowledgeBase.db"),
                     Path.Combine(activeRoot, "training_samples.json")
                 })
        {
            try
            {
                using var stream = new FileStream(
                    path,
                    FileMode.Open,
                    FileAccess.ReadWrite,
                    FileShare.None);
            }
            catch (IOException ex)
            {
                throw new IOException(
                    "Der aktive Wissensordner wird noch verwendet. " +
                    "SewerStudio bitte schliessen und erneut starten.",
                    ex);
            }
        }
    }

    public static void EnsureUniqueIds(IEnumerable<TrainingSample> samples)
    {
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var sample in samples)
        {
            if (string.IsNullOrWhiteSpace(sample.SampleId) || !ids.Add(sample.SampleId))
                throw new InvalidDataException($"Leere oder doppelte Sample-ID '{sample.SampleId}'.");
        }
    }

    public static void ResolveAndValidateSourceFrames(
        string legacyRoot,
        string previousActiveRoot,
        IEnumerable<LegacyPersonalGoldCandidate> candidates)
    {
        foreach (var candidate in candidates)
        {
            var sourcePath = candidate.Sample.FramePath;
            var rooted = Path.IsPathRooted(sourcePath);
            var originalResolved = rooted
                ? Path.GetFullPath(sourcePath)
                : Path.GetFullPath(Path.Combine(legacyRoot, sourcePath));
            var belongsToPreviousActiveRoot = rooted
                && (PersonalGoldBrainFileService.PathsEqual(
                        previousActiveRoot,
                        originalResolved)
                    || PersonalGoldBrainFileService.IsInside(
                        previousActiveRoot,
                        originalResolved));
            var resolved = belongsToPreviousActiveRoot
                ? Path.GetFullPath(Path.Combine(
                    legacyRoot,
                    Path.GetRelativePath(previousActiveRoot, originalResolved)))
                : originalResolved;
            var mustBeInsideArchive = !rooted || belongsToPreviousActiveRoot;
            if (mustBeInsideArchive
                && !PersonalGoldBrainFileService.PathsEqual(legacyRoot, resolved)
                && !PersonalGoldBrainFileService.IsInside(legacyRoot, resolved))
            {
                throw new InvalidDataException(
                    $"Archivbildpfad verlaesst den geschuetzten Altordner: " +
                    candidate.Sample.SampleId);
            }
            if (!File.Exists(resolved))
            {
                throw new FileNotFoundException(
                    $"Persoenlich bestaetigtes Archivbild fehlt: {candidate.Sample.SampleId}",
                    resolved);
            }

            PersonalGoldBrainFileService.EnsureNoReparsePoint(
                resolved,
                mustBeInsideArchive
                    ? legacyRoot
                    : Path.GetDirectoryName(resolved)!);
            candidate.Sample.FramePath = resolved;
        }
    }

    public static async Task<IReadOnlyList<ArchiveRecoveryFramePlan>>
        BuildCandidateFramePlansAsync(
            ArchiveRecoveryPaths paths,
            IReadOnlyList<LegacyPersonalGoldCandidate> candidates,
            CancellationToken cancellationToken)
    {
        var goldFramesRoot = Path.Combine(paths.ActiveRoot, "gold_frames");
        PersonalGoldBrainFileService.EnsureMutationPathIsSafe(
            paths.ActiveRoot,
            goldFramesRoot);
        var plans = new List<ArchiveRecoveryFramePlan>(candidates.Count);
        foreach (var candidate in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var codeFolder = PersonalGoldMainCodeCatalog.FormatFolderName(
                candidate.Sample.Code,
                VsaCodeResolver.LookupLabel);
            var codeFramesRoot = Path.GetFullPath(
                Path.Combine(goldFramesRoot, codeFolder));
            var targetPath = Path.GetFullPath(
                await PersonalGoldMigrationFileService
                    .ResolveTargetPathAsync(
                        candidate.Sample.FramePath,
                        codeFramesRoot,
                        cancellationToken)
                    .ConfigureAwait(false));
            if (!PersonalGoldBrainFileService.IsInside(goldFramesRoot, targetPath))
                throw new InvalidDataException("Geplantes Goldbild verlaesst den Goldordner.");
            PersonalGoldBrainFileService.EnsureMutationPathIsSafe(
                paths.ActiveRoot,
                codeFramesRoot);
            PersonalGoldBrainFileService.EnsureMutationPathIsSafe(
                paths.ActiveRoot,
                targetPath);

            var sourceHash = await PersonalGoldBrainFileService
                .HashAsync(candidate.Sample.FramePath, cancellationToken)
                .ConfigureAwait(false);
            var targetWasPresent = File.Exists(targetPath);
            if (Directory.Exists(targetPath))
            {
                throw new InvalidDataException(
                    $"Goldbild-Ziel ist unerwartet ein Ordner: {targetPath}");
            }
            if (targetWasPresent)
            {
                var targetHash = await PersonalGoldBrainFileService
                    .HashAsync(targetPath, cancellationToken)
                    .ConfigureAwait(false);
                if (!targetHash.Equals(sourceHash, StringComparison.OrdinalIgnoreCase))
                {
                    throw new IOException(
                        $"Vorhandenes Goldbild besitzt eine abweichende Pruefsumme: {targetPath}");
                }
            }

            plans.Add(new ArchiveRecoveryFramePlan(
                candidate.Sample.SampleId,
                Path.GetFullPath(candidate.Sample.FramePath),
                targetPath,
                sourceHash,
                targetWasPresent));
        }
        return plans;
    }

    public static async Task<ArchiveRecoveryPreimages> ReadPreimagesAsync(
        ArchiveRecoveryPaths paths,
        byte[] originalSamplesBytes,
        CancellationToken cancellationToken)
    {
        var inventory = await ReadOptionalStableBytesAsync(
                PersonalGoldArchiveRecoveryTransaction.GetInventoryPath(paths.ActiveRoot),
                cancellationToken)
            .ConfigureAwait(false);
        var receipt = await ReadOptionalStableBytesAsync(
                PersonalGoldArchiveRecoveryTransaction.GetReceiptPath(paths.ActiveRoot),
                cancellationToken)
            .ConfigureAwait(false);
        var manifest = await ReadOptionalStableBytesAsync(
                PersonalGoldArchiveRecoveryTransaction.GetManifestPath(paths.ActiveRoot),
                cancellationToken)
            .ConfigureAwait(false);
        return new ArchiveRecoveryPreimages(
            originalSamplesBytes,
            inventory,
            receipt,
            manifest);
    }

    public static ArchiveRecoveryTransactionJournal CreatePreparingTransaction(
        PersonalGoldArchiveRecoveryRequest request,
        ArchiveRecoveryPaths paths,
        IReadOnlyList<ArchiveRecoveryFramePlan> framePlans,
        ArchiveRecoveryPreimages preimages)
    {
        var transactionId = Guid.NewGuid().ToString("N");
        var auditDirectory = Path.Combine(
            paths.ActiveRoot,
            "training",
            "gold_migrations",
            $"archive_recovery_{request.StartedUtc:yyyyMMdd_HHmmss}_{transactionId}");
        return new ArchiveRecoveryTransactionJournal(
            SchemaVersion: 1,
            TransactionId: transactionId,
            Stage: ArchiveRecoveryTransactionStage.Preparing,
            StartedUtc: request.StartedUtc,
            ActiveRoot: paths.ActiveRoot,
            LegacyRoot: paths.LegacyRoot,
            ConfirmedByUser: request.ConfirmedByUser,
            AuditDirectory: Path.GetFullPath(auditDirectory),
            ActiveDatabasePath: paths.ActiveDatabasePath,
            ActiveSamplesPath: paths.ActiveSamplesPath,
            InventoryPath: PersonalGoldArchiveRecoveryTransaction.GetInventoryPath(
                paths.ActiveRoot),
            ReceiptPath: PersonalGoldArchiveRecoveryTransaction.GetReceiptPath(
                paths.ActiveRoot),
            ManifestPath: PersonalGoldArchiveRecoveryTransaction.GetManifestPath(
                paths.ActiveRoot),
            InventoryWasPresent: preimages.Inventory.WasPresent,
            ReceiptWasPresent: preimages.Receipt.WasPresent,
            ManifestWasPresent: preimages.Manifest.WasPresent,
            OriginalSamplesSha256:
            PersonalGoldArchiveRecoveryTransaction.HashBytes(preimages.Samples),
            OriginalInventorySha256: preimages.Inventory.Sha256,
            OriginalReceiptSha256: preimages.Receipt.Sha256,
            OriginalManifestSha256: preimages.Manifest.Sha256,
            DatabaseSnapshotSha256: null,
            Frames: framePlans);
    }

    private static async Task<ArchiveRecoveryOptionalBytes> ReadOptionalStableBytesAsync(
        string path,
        CancellationToken cancellationToken)
    {
        if (Directory.Exists(path))
            throw new InvalidDataException($"Dateiziel ist unerwartet ein Ordner: {path}");
        if (!File.Exists(path))
            return new ArchiveRecoveryOptionalBytes(false, null, null);

        var bytes = await PersonalGoldMigrationFileService
            .ReadStableBytesAsync(path, cancellationToken)
            .ConfigureAwait(false);
        return new ArchiveRecoveryOptionalBytes(
            true,
            bytes,
            PersonalGoldArchiveRecoveryTransaction.HashBytes(bytes));
    }

    private static string ReadPreviousActiveRootFromArchiveMarker(string legacyRoot)
    {
        var markerPath = Path.Combine(
            legacyRoot,
            PersonalGoldBrainSeparationService.LegacyArchiveMarkerFileName);
        if (!File.Exists(markerPath))
        {
            throw new InvalidDataException(
                "Der angegebene Altordner besitzt keinen Archiv-Schutzmarker.");
        }
        PersonalGoldBrainFileService.EnsureNoReparsePoint(markerPath, legacyRoot);

        var lines = File.ReadAllLines(markerPath);
        if (lines.Length == 0
            || !string.Equals(
                lines[0],
                "SewerStudio altes KI-Gehirn - nur Archiv",
                StringComparison.Ordinal)
            || lines.Count(line => string.Equals(
                line,
                "DoNotUseAsActiveKnowledgeRoot=true",
                StringComparison.Ordinal)) != 1)
        {
            throw new InvalidDataException(
                "Der Archiv-Schutzmarker besitzt keinen gueltigen Inhalt.");
        }

        const string previousRootPrefix = "PreviousActiveRoot=";
        var previousRootLines = lines
            .Where(line => line.StartsWith(previousRootPrefix, StringComparison.Ordinal))
            .ToArray();
        if (previousRootLines.Length != 1
            || string.IsNullOrWhiteSpace(previousRootLines[0][previousRootPrefix.Length..]))
        {
            throw new InvalidDataException(
                "Der Archiv-Schutzmarker besitzt keinen eindeutigen frueheren aktiven Pfad.");
        }
        return PersonalGoldBrainFileService.NormalizeRoot(
            previousRootLines[0][previousRootPrefix.Length..],
            "Frueherer aktiver Wissensordner");
    }
}
