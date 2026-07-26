using AuswertungPro.Next.Application.Ai.Training;

namespace AuswertungPro.Next.Infrastructure.Ai.Training;

/// <summary>
/// Fuehrt die eigentlichen Recovery-Ausgaben aus und prueft danach die
/// gemeinsame Konsistenz von Frames, JSON und SQLite.
/// </summary>
internal sealed class PersonalGoldArchiveRecoveryOutput(
    ITrainingFrameStore frameStore,
    PersonalGoldBrainDatabaseBuilder database)
{
    public async Task<IReadOnlyDictionary<string, string>> CopyCandidateFramesAsync(
        ArchiveRecoveryPaths paths,
        IReadOnlyList<LegacyPersonalGoldCandidate> candidates,
        IReadOnlyList<ArchiveRecoveryFramePlan> framePlans,
        CancellationToken cancellationToken)
    {
        var plansById = framePlans.ToDictionary(
            plan => plan.SampleId,
            StringComparer.OrdinalIgnoreCase);
        var targets = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var candidate in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var plan = plansById[candidate.Sample.SampleId];
            PersonalGoldBrainFileService.EnsureMutationPathIsSafe(
                paths.ActiveRoot,
                Path.GetDirectoryName(plan.TargetPath)!);
            PersonalGoldBrainFileService.EnsureMutationPathIsSafe(
                paths.ActiveRoot,
                plan.TargetPath);
            var target = await frameStore
                .StoreExistingAsync(
                    candidate.Sample.FramePath,
                    Path.GetDirectoryName(plan.TargetPath),
                    cancellationToken)
                .ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(target)
                || !string.Equals(
                    Path.GetFullPath(target),
                    plan.TargetPath,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new IOException(
                    $"Goldbild fuer Archiv-Sample '{candidate.Sample.SampleId}' " +
                    "konnte nicht sicher kopiert werden.");
            }

            PersonalGoldBrainFileService.EnsureMutationPathIsSafe(
                paths.ActiveRoot,
                target);
            var hash = await PersonalGoldBrainFileService
                .HashAsync(target, cancellationToken)
                .ConfigureAwait(false);
            if (!hash.Equals(plan.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                throw new IOException(
                    $"Goldbild fuer Archiv-Sample '{candidate.Sample.SampleId}' " +
                    "besitzt eine falsche Pruefsumme.");
            }
            targets.Add(candidate.Sample.SampleId, target);
        }
        return targets;
    }

    public void VerifyRecoveredState(
        ArchiveRecoveryPaths paths,
        IReadOnlyList<TrainingSample> samples,
        IReadOnlyDictionary<string, string> targetPaths)
    {
        var saved = System.Text.Json.JsonSerializer.Deserialize<List<TrainingSample>>(
                        File.ReadAllBytes(paths.ActiveSamplesPath))
                    ?? throw new InvalidDataException(
                        "Nachgeholte training_samples.json ist nicht lesbar.");
        if (saved.Count != samples.Count
            || saved.Any(sample =>
                !ManualGoldTrainingPolicy.IsManuallyConfirmed(
                    sample,
                    samples[0].ConfirmedByUser)))
        {
            throw new InvalidDataException(
                "Nach dem Archiv-Nachholen ist die aktive Trainingsliste nicht Gold-only.");
        }

        database.Inspect(paths.ActiveDatabasePath, saved);
        var databasePaths = PersonalGoldMigrationDatabaseStore.ReadPaths(
            paths.ActiveDatabasePath,
            saved);
        foreach (var sample in saved)
        {
            if (!databasePaths.TryGetValue(sample.SampleId, out var databasePath))
            {
                throw new InvalidDataException(
                    $"Nachhol-Pruefung: Datenbankpfad fehlt fuer '{sample.SampleId}'.");
            }
            if (!targetPaths.TryGetValue(sample.SampleId, out var expectedPath))
            {
                throw new InvalidDataException(
                    $"Nachhol-Pruefung: Zielpfad fehlt fuer '{sample.SampleId}'.");
            }
            if (!string.Equals(
                    databasePath,
                    expectedPath,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    $"Nachhol-Pruefung: Pfade weichen ab fuer '{sample.SampleId}': " +
                    $"DB='{databasePath}', Gold='{expectedPath}'.");
            }
            if (!File.Exists(expectedPath))
            {
                throw new FileNotFoundException(
                    $"Nachhol-Pruefung: Goldbild fehlt fuer '{sample.SampleId}'.",
                    expectedPath);
            }
        }
    }

    public static async Task WriteReceiptAsync(
        PersonalGoldArchiveRecoveryRequest request,
        ArchiveRecoveryPaths paths,
        string receiptPath,
        int existingPersonal,
        int activePersonal,
        IReadOnlyList<LegacyPersonalGoldCandidate> candidates,
        CancellationToken cancellationToken)
        => await PersonalGoldBrainFileService
            .WriteJsonAsync(
                receiptPath,
                new
                {
                    schema_version = 1,
                    recovered_utc = request.StartedUtc,
                    confirmed_by_user = request.ConfirmedByUser,
                    selection_policy = "database-only-personal-manual-confirmed",
                    legacy_archive = paths.LegacyRoot,
                    previous_personal_gold_samples = existingPersonal,
                    recovered_samples = candidates.Count,
                    active_personal_gold_samples = activePersonal,
                    sample_ids = candidates
                        .Select(candidate => candidate.Sample.SampleId)
                        .OrderBy(id => id, StringComparer.OrdinalIgnoreCase)
                        .ToArray(),
                    frame_paths = candidates
                        .OrderBy(
                            candidate => candidate.Sample.SampleId,
                            StringComparer.OrdinalIgnoreCase)
                        .ToDictionary(
                            candidate => candidate.Sample.SampleId,
                            candidate => candidate.Sample.FramePath,
                            StringComparer.OrdinalIgnoreCase)
                },
                cancellationToken)
            .ConfigureAwait(false);
}
