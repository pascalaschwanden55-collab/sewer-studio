using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Application.Backup;
using AuswertungPro.Next.Infrastructure.Backup;

namespace AuswertungPro.Next.Infrastructure.Ai.Training;

/// <summary>
/// Prueft Eingaben, Quellstand und alle Pfade vor einer Gold-Gehirn-Umschaltung.
/// </summary>
internal static class PersonalGoldBrainSeparationInput
{
    private const string CommitJournalSuffix = ".gold-brain-separation.commit.json";

    internal static string ResolveCommitJournalPath(string knowledgeRoot)
        => PersonalGoldBrainFileService.NormalizeRoot(
               knowledgeRoot,
               "Aktiver Wissensordner")
           + CommitJournalSuffix;

    internal static PersonalGoldBrainResolvedPaths ResolveDeclaredPaths(
        PersonalGoldBrainSeparationRequest request,
        string transactionId)
        => CreateResolvedPaths(
            request.KnowledgeRoot,
            request.LocalArchiveRoot,
            request.ExternalMirrorRoot,
            request.ExternalArchiveRoot,
            request.LegacyProtocolTrainingPath,
            request.StartedUtc,
            transactionId);

    internal static PersonalGoldBrainResolvedPaths CreateResolvedPaths(
        string knowledgeRootPath,
        string localArchivePath,
        string externalMirrorPath,
        string externalArchivePath,
        string legacyProtocolTrainingPath,
        DateTimeOffset startedUtc,
        string transactionId)
    {
        var knowledgeRoot = PersonalGoldBrainFileService.NormalizeRoot(
            knowledgeRootPath,
            "Aktiver Wissensordner");
        var localArchive = PersonalGoldBrainFileService.NormalizeRoot(
            localArchivePath,
            "Lokales Altarchiv");
        var externalMirror = PersonalGoldBrainFileService.NormalizeRoot(
            externalMirrorPath,
            "Elements-Spiegel");
        var externalArchive = PersonalGoldBrainFileService.NormalizeRoot(
            externalArchivePath,
            "Elements-Altarchiv");
        var protocolPath = Path.GetFullPath(legacyProtocolTrainingPath);
        var staging = knowledgeRoot
                      + $".gold-staging_{startedUtc:yyyyMMdd_HHmmss}";
        var disconnectedProtocol = protocolPath
                                   + $".disconnected_{startedUtc:yyyyMMdd_HHmmss}";
        var commitJournal = ResolveCommitJournalPath(knowledgeRoot);

        PersonalGoldBrainFileService.EnsureDifferentRoots(
            knowledgeRoot,
            localArchive,
            externalMirror,
            externalArchive,
            staging);
        EnsureLegacyProtocolPathsDoNotOverlap(
            protocolPath,
            disconnectedProtocol,
            commitJournal,
            knowledgeRoot,
            localArchive,
            externalMirror,
            externalArchive,
            staging);
        PersonalGoldBrainFileService.EnsureSameVolume(
            knowledgeRoot,
            localArchive,
            "Das lokale Altarchiv");
        PersonalGoldBrainFileService.EnsureSameVolume(
            knowledgeRoot,
            staging,
            "Der Gold-Arbeitsordner");
        PersonalGoldBrainFileService.EnsureSameVolume(
            externalMirror,
            externalArchive,
            "Das Elements-Altarchiv");
        var localSafetyRoot = PersonalGoldBrainFileService.FindCommonSafetyRoot(
            knowledgeRoot,
            localArchive);
        if (!PersonalGoldBrainFileService.IsInside(localSafetyRoot, staging)
            && !PersonalGoldBrainFileService.PathsEqual(localSafetyRoot, staging))
        {
            throw new InvalidDataException(
                "Gold-Arbeitsordner liegt ausserhalb der lokalen Schutzwurzel.");
        }
        var externalSafetyRoot = PersonalGoldBrainFileService.FindCommonSafetyRoot(
            externalMirror,
            externalArchive);
        var legacySafetyRoot = Path.GetDirectoryName(protocolPath)
                               ?? throw new InvalidDataException(
                                   "Protokoll-Lernpfad besitzt keinen Elternordner.");

        foreach (var path in new[]
                 {
                     knowledgeRoot,
                     localArchive,
                     staging,
                     commitJournal
                 })
        {
            PersonalGoldBrainFileService.EnsureMutationPathIsSafe(localSafetyRoot, path);
        }
        foreach (var path in new[] { externalMirror, externalArchive })
        {
            PersonalGoldBrainFileService.EnsureMutationPathIsSafe(externalSafetyRoot, path);
        }
        PersonalGoldBrainFileService.EnsureMutationPathIsSafe(
            legacySafetyRoot,
            protocolPath);
        PersonalGoldBrainFileService.EnsureMutationPathIsSafe(
            legacySafetyRoot,
            disconnectedProtocol);

        return new PersonalGoldBrainResolvedPaths(
            transactionId,
            knowledgeRoot,
            localArchive,
            externalMirror,
            externalArchive,
            staging,
            protocolPath,
            disconnectedProtocol,
            commitJournal,
            localSafetyRoot,
            externalSafetyRoot,
            legacySafetyRoot,
            Path.Combine(knowledgeRoot, "training_samples.json"),
            Path.Combine(knowledgeRoot, "KnowledgeBase.db"));
    }

    internal static PersonalGoldBrainResolvedPaths ValidateAndResolvePaths(
        PersonalGoldBrainResolvedPaths paths)
    {
        if (File.Exists(paths.KnowledgeRoot))
            throw new InvalidDataException(
                $"Aktiver Wissensordner ist eine Datei: {paths.KnowledgeRoot}");
        if (File.Exists(paths.ExternalMirrorRoot))
            throw new InvalidDataException(
                $"Elements-Spiegel ist eine Datei: {paths.ExternalMirrorRoot}");
        if (!Directory.Exists(paths.KnowledgeRoot))
            throw new DirectoryNotFoundException(
                $"Aktiver Wissensordner fehlt: {paths.KnowledgeRoot}");
        if (!Directory.Exists(paths.ExternalMirrorRoot))
            throw new DirectoryNotFoundException(
                $"Elements-Spiegel fehlt: {paths.ExternalMirrorRoot}");
        if (Directory.Exists(paths.LocalArchiveRoot) || File.Exists(paths.LocalArchiveRoot))
            throw new IOException(
                $"Lokales Altarchiv existiert bereits: {paths.LocalArchiveRoot}");
        if (Directory.Exists(paths.ExternalArchiveRoot) || File.Exists(paths.ExternalArchiveRoot))
            throw new IOException(
                $"Elements-Altarchiv existiert bereits: {paths.ExternalArchiveRoot}");
        if (Directory.Exists(paths.StagingRoot) || File.Exists(paths.StagingRoot))
            throw new IOException(
                $"Gold-Arbeitsordner existiert bereits: {paths.StagingRoot}");
        if (Directory.Exists(paths.CommitJournalPath) || File.Exists(paths.CommitJournalPath))
            throw new IOException(
                $"Commit-Journal existiert nach der Wiederherstellung weiter: {paths.CommitJournalPath}");

        PersonalGoldBrainFileService.EnsureMutationPathIsSafe(
            paths.LocalSafetyRoot,
            paths.KnowledgeRoot);
        PersonalGoldBrainFileService.EnsureMutationPathIsSafe(
            paths.ExternalSafetyRoot,
            paths.ExternalMirrorRoot);

        if (!File.Exists(paths.SamplesPath))
            throw new FileNotFoundException("training_samples.json fehlt.", paths.SamplesPath);
        if (!File.Exists(paths.DatabasePath))
            throw new FileNotFoundException("KnowledgeBase.db fehlt.", paths.DatabasePath);

        return paths;
    }

    internal static void EnsureSourceIsIdle(string knowledgeRoot)
    {
        foreach (var path in new[]
                 {
                     Path.Combine(knowledgeRoot, "KnowledgeBase.db"),
                     Path.Combine(knowledgeRoot, "training_samples.json")
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

    internal static TrainingSample[] SelectPersonalGold(
        IEnumerable<TrainingSample> samples,
        string confirmedByUser)
    {
        if (string.IsNullOrWhiteSpace(confirmedByUser))
            throw new ArgumentException("Bestaetiger fehlt.", nameof(confirmedByUser));

        var selected = samples
            .Where(sample => ManualGoldTrainingPolicy.IsManuallyConfirmed(sample, confirmedByUser))
            .OrderBy(sample => sample.SampleId, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (selected.Length == 0)
            throw new InvalidDataException("Keine persoenlich bestaetigten Goldsamples gefunden.");
        var uniqueIds = selected
            .Select(sample => sample.SampleId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (uniqueIds.Count != selected.Length || uniqueIds.Contains(string.Empty))
            throw new InvalidDataException("Leere oder doppelte Goldsample-ID.");
        return selected;
    }

    internal static void ValidateSelectedFrames(
        string knowledgeRoot,
        IEnumerable<TrainingSample> selected)
    {
        var goldFramesRoot = Path.Combine(knowledgeRoot, "gold_frames");
        foreach (var sample in selected)
        {
            if (string.IsNullOrWhiteSpace(sample.FramePath) || !File.Exists(sample.FramePath))
                throw new FileNotFoundException($"Goldbild fehlt fuer Sample '{sample.SampleId}'.");
            if (!PersonalGoldBrainFileService.IsInside(goldFramesRoot, sample.FramePath))
            {
                throw new InvalidDataException(
                    $"Goldbild liegt ausserhalb des kanonischen Goldordners: {sample.SampleId}");
            }

            PersonalGoldBrainFileService.EnsureNoReparsePoint(sample.FramePath, goldFramesRoot);
        }
    }

    internal static PersonalGoldBrainSourceDatabaseInspection InspectSourceDatabase(
        string databasePath,
        IReadOnlyList<TrainingSample> selected)
    {
        var paths = PersonalGoldMigrationDatabaseStore.ReadPaths(databasePath, selected);
        if (paths.Count != selected.Count)
            throw new InvalidDataException("Nicht alle persoenlichen Goldsamples liegen in KnowledgeBase.db.");

        using var connection = new Microsoft.Data.Sqlite.SqliteConnection(
            new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder
            {
                DataSource = databasePath,
                Mode = Microsoft.Data.Sqlite.SqliteOpenMode.ReadOnly,
                Pooling = false
            }.ToString());
        connection.Open();
        using var count = connection.CreateCommand();
        count.CommandText = "SELECT COUNT(*) FROM Samples;";
        var sourceSamples = Convert.ToInt32(count.ExecuteScalar());
        using var embeddings = connection.CreateCommand();
        embeddings.CommandText =
            "SELECT COUNT(*) FROM Embeddings WHERE SampleId IN (" +
            string.Join(",", selected.Select((_, index) => $"$id{index}")) +
            ");";
        for (var index = 0; index < selected.Count; index++)
            embeddings.Parameters.AddWithValue($"$id{index}", selected[index].SampleId);
        if (Convert.ToInt32(embeddings.ExecuteScalar()) != selected.Count)
            throw new InvalidDataException("Nicht alle persoenlichen Goldsamples besitzen eine Einbettung.");

        return new PersonalGoldBrainSourceDatabaseInspection(sourceSamples);
    }

    internal static async Task ValidateExternalMirrorAsync(
        PersonalGoldBrainResolvedPaths paths,
        IReadOnlyList<TrainingSample> selected,
        CancellationToken cancellationToken)
    {
        var markerPath = Path.Combine(
            paths.ExternalMirrorRoot,
            KnowledgeRealtimeMirrorService.MarkerFileName);
        if (!File.Exists(markerPath))
            throw new InvalidDataException("Der Elements-Spiegel besitzt keinen Sicherheitsmarker.");
        var marker = await File.ReadAllTextAsync(markerPath, cancellationToken).ConfigureAwait(false);
        KnowledgeMirrorMarker.Validate(
            marker,
            paths.KnowledgeRoot,
            paths.ExternalMirrorRoot);

        var criticalFiles = new List<string>
        {
            "KnowledgeBase.db",
            "training_samples.json",
            "teacher_annotations.json",
            "yolo_class_map.json"
        };
        criticalFiles.AddRange(selected.Select(sample =>
        {
            if (!PersonalGoldBrainFileService.IsInside(paths.KnowledgeRoot, sample.FramePath))
            {
                throw new InvalidDataException(
                    $"Goldbild liegt ausserhalb des aktiven KI-Brains: {sample.SampleId}");
            }

            return Path.GetRelativePath(paths.KnowledgeRoot, sample.FramePath);
        }));
        var evalManifest = Path.Combine("eval_set", "_manifest.json");
        if (File.Exists(Path.Combine(paths.KnowledgeRoot, evalManifest)))
            criticalFiles.Add(evalManifest);

        foreach (var relativePath in criticalFiles.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var source = Path.Combine(paths.KnowledgeRoot, relativePath);
            var mirror = Path.Combine(paths.ExternalMirrorRoot, relativePath);
            if (!File.Exists(source) || !File.Exists(mirror))
                throw new FileNotFoundException($"Kritische Spiegeldatei fehlt: {relativePath}");
            var sourceHash = await PersonalGoldBrainFileService.HashAsync(source, cancellationToken)
                .ConfigureAwait(false);
            var mirrorHash = await PersonalGoldBrainFileService.HashAsync(mirror, cancellationToken)
                .ConfigureAwait(false);
            if (!sourceHash.Equals(mirrorHash, StringComparison.OrdinalIgnoreCase))
                throw new IOException($"Elements-Spiegel ist nicht aktuell: {relativePath}");
        }
    }

    internal static void ValidateReservedArchiveTargets(PersonalGoldBrainResolvedPaths paths)
    {
        ValidateReservedArchiveTargets(
            paths.KnowledgeRoot,
            paths.LocalSafetyRoot,
            paths.DisconnectedLegacyProtocolTrainingPath);
        ValidateReservedArchiveTargets(
            paths.ExternalMirrorRoot,
            paths.ExternalSafetyRoot,
            disconnectedLegacyPath: null);
    }

    private static void ValidateReservedArchiveTargets(
        string sourceRoot,
        string safetyRoot,
        string? disconnectedLegacyPath)
    {
        foreach (var path in new[]
                 {
                     Path.Combine(
                         sourceRoot,
                         PersonalGoldBrainSeparationService.LegacyArchiveMarkerFileName),
                     Path.Combine(
                         sourceRoot,
                         PersonalGoldBrainSeparationService.LegacyArchiveMarkerFileName) + ".tmp",
                     Path.Combine(sourceRoot, "external_context", "protocol_training.json"),
                     Path.Combine(sourceRoot, "external_context", "protocol_training.json.tmp")
                 })
        {
            PersonalGoldBrainFileService.EnsureMutationPathIsSafe(safetyRoot, path);
            if (File.Exists(path) || Directory.Exists(path))
            {
                throw new InvalidDataException(
                    $"Reserviertes Commit-Archivziel existiert bereits: {path}");
            }
        }

        if (disconnectedLegacyPath is null)
            return;
        PersonalGoldBrainFileService.EnsureMutationPathIsSafe(
            Path.GetDirectoryName(disconnectedLegacyPath)!,
            disconnectedLegacyPath);
        if (File.Exists(disconnectedLegacyPath) || Directory.Exists(disconnectedLegacyPath))
        {
            throw new InvalidDataException(
                $"Abgekoppeltes Protokollziel existiert bereits: {disconnectedLegacyPath}");
        }
    }

    private static void EnsureLegacyProtocolPathsDoNotOverlap(
        string protocolPath,
        string disconnectedProtocolPath,
        string commitJournalPath,
        params string[] movingRoots)
    {
        foreach (var candidate in new[] { protocolPath, disconnectedProtocolPath })
        {
            foreach (var root in movingRoots)
            {
                if (PersonalGoldBrainFileService.PathsEqual(root, candidate)
                    || PersonalGoldBrainFileService.IsInside(root, candidate)
                    || PersonalGoldBrainFileService.IsInside(candidate, root))
                {
                    throw new InvalidDataException(
                        "Der alte Protokoll-Lernpfad ueberlappt einen Quell-, " +
                        $"Archiv- oder Arbeitsordner: {candidate} / {root}");
                }
            }

            if (PersonalGoldBrainFileService.PathsEqual(candidate, commitJournalPath)
                || PersonalGoldBrainFileService.PathsEqual(
                    candidate,
                    commitJournalPath + ".tmp"))
            {
                throw new InvalidDataException(
                    "Der alte Protokoll-Lernpfad ueberlappt das Commit-Journal.");
            }
        }
    }
}
