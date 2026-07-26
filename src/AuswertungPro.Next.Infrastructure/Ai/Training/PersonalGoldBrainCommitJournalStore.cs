using System.Text.Json;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Application.Common;

namespace AuswertungPro.Next.Infrastructure.Ai.Training;

/// <summary>
/// Schreibt, liest und prueft das dauerhafte Journal der Gold-Umschaltung.
/// </summary>
internal static class PersonalGoldBrainCommitJournalStore
{
    internal static async Task<PersonalGoldBrainCommitJournal> CreateAsync(
        PersonalGoldBrainSeparationRequest request,
        PersonalGoldBrainResolvedPaths paths,
        CancellationToken cancellationToken)
    {
        ValidateCommitOwnership(paths.StagingRoot, paths.TransactionId);
        PersonalGoldBrainSeparationInput.ValidateReservedArchiveTargets(paths);
        var legacyPresent = File.Exists(paths.LegacyProtocolTrainingPath);
        var legacyHash = legacyPresent
            ? await PersonalGoldBrainFileService
                .HashAsync(paths.LegacyProtocolTrainingPath, cancellationToken)
                .ConfigureAwait(false)
            : null;
        var journal = new PersonalGoldBrainCommitJournal(
            SchemaVersion: 1,
            paths.TransactionId,
            request.StartedUtc,
            paths.KnowledgeRoot,
            paths.LocalArchiveRoot,
            paths.ExternalMirrorRoot,
            paths.ExternalArchiveRoot,
            paths.StagingRoot,
            paths.LegacyProtocolTrainingPath,
            paths.DisconnectedLegacyProtocolTrainingPath,
            legacyPresent,
            legacyHash,
            Directory.Exists(Path.Combine(paths.KnowledgeRoot, "external_context")),
            Directory.Exists(Path.Combine(paths.ExternalMirrorRoot, "external_context")));

        if (File.Exists(paths.CommitJournalPath) || Directory.Exists(paths.CommitJournalPath))
            throw new IOException($"Commit-Journal existiert bereits: {paths.CommitJournalPath}");
        await PersonalGoldBrainFileService
            .WriteJsonAsync(
                paths.CommitJournalPath,
                journal,
                paths.LocalSafetyRoot,
                cancellationToken)
            .ConfigureAwait(false);
        var saved = await ReadAsync(paths).ConfigureAwait(false);
        if (!string.Equals(saved.TransactionId, journal.TransactionId, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Das atomar geschriebene Commit-Journal konnte nicht verifiziert werden.");
        }

        return journal;
    }

    internal static async Task<PersonalGoldBrainCommitJournal> ReadAsync(
        PersonalGoldBrainResolvedPaths paths)
    {
        PersonalGoldBrainFileService.EnsureMutationPathIsSafe(
            paths.LocalSafetyRoot,
            paths.CommitJournalPath);
        byte[] bytes;
        try
        {
            bytes = await File.ReadAllBytesAsync(
                    paths.CommitJournalPath,
                    CancellationToken.None)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new InvalidDataException(
                $"Commit-Journal konnte nicht sicher gelesen werden: {paths.CommitJournalPath}",
                ex);
        }

        PersonalGoldBrainCommitJournal journal;
        try
        {
            journal = JsonSerializer.Deserialize<PersonalGoldBrainCommitJournal>(
                          bytes,
                          JsonDefaults.CaseInsensitive)
                      ?? throw new InvalidDataException("Commit-Journal ist leer.");
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("Commit-Journal ist unlesbar.", ex);
        }

        ValidateMetadata(journal);
        return journal;
    }

    internal static PersonalGoldBrainResolvedPaths ResolveJournalPaths(
        PersonalGoldBrainSeparationRequest request,
        PersonalGoldBrainResolvedPaths declaredPaths,
        PersonalGoldBrainCommitJournal journal)
    {
        var paths = PersonalGoldBrainSeparationInput.CreateResolvedPaths(
            journal.KnowledgeRoot,
            journal.LocalArchiveRoot,
            journal.ExternalMirrorRoot,
            journal.ExternalArchiveRoot,
            journal.LegacyProtocolTrainingPath,
            journal.StartedUtc,
            journal.TransactionId);
        EnsureCanonicalJournalPath(
            journal.KnowledgeRoot,
            paths.KnowledgeRoot,
            "KnowledgeRoot");
        EnsureCanonicalJournalPath(
            journal.LocalArchiveRoot,
            paths.LocalArchiveRoot,
            "LocalArchiveRoot");
        EnsureCanonicalJournalPath(
            journal.ExternalMirrorRoot,
            paths.ExternalMirrorRoot,
            "ExternalMirrorRoot");
        EnsureCanonicalJournalPath(
            journal.ExternalArchiveRoot,
            paths.ExternalArchiveRoot,
            "ExternalArchiveRoot");
        EnsureCanonicalJournalPath(
            journal.StagingRoot,
            paths.StagingRoot,
            "StagingRoot");
        EnsureCanonicalJournalPath(
            journal.LegacyProtocolTrainingPath,
            paths.LegacyProtocolTrainingPath,
            "LegacyProtocolTrainingPath");
        EnsureCanonicalJournalPath(
            journal.DisconnectedLegacyProtocolTrainingPath,
            paths.DisconnectedLegacyProtocolTrainingPath,
            "DisconnectedLegacyProtocolTrainingPath");

        if (!PersonalGoldBrainFileService.PathsEqual(
                paths.KnowledgeRoot,
                declaredPaths.KnowledgeRoot)
            || !PersonalGoldBrainFileService.PathsEqual(
                paths.ExternalMirrorRoot,
                declaredPaths.ExternalMirrorRoot)
            || !PersonalGoldBrainFileService.PathsEqual(
                paths.LegacyProtocolTrainingPath,
                Path.GetFullPath(request.LegacyProtocolTrainingPath))
            || !PersonalGoldBrainFileService.PathsEqual(
                paths.CommitJournalPath,
                declaredPaths.CommitJournalPath))
        {
            throw new InvalidDataException(
                "Commit-Journal passt nicht zu den aktuell angeforderten aktiven Pfaden.");
        }

        return paths;
    }

    internal static void ValidateCommitOwnership(string root, string transactionId)
    {
        PersonalGoldBrainFileService.EnsureMutationTreeIsSafe(root);
        var marker = Path.Combine(
            root,
            PersonalGoldBrainSeparationService.CommitOwnerMarkerFileName);
        PersonalGoldBrainFileService.EnsureMutationPathIsSafe(root, marker);
        if (!File.Exists(marker)
            || !string.Equals(
                File.ReadAllText(marker),
                transactionId,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"Besitzmarker des erzeugten Gold-Arbeitsstands ist ungueltig: {root}");
        }
    }

    internal static void Delete(PersonalGoldBrainResolvedPaths paths)
    {
        PersonalGoldBrainFileService.EnsureMutationPathIsSafe(
            paths.LocalSafetyRoot,
            paths.CommitJournalPath);
        if (!File.Exists(paths.CommitJournalPath))
            throw new InvalidDataException("Commit-Journal fehlt vor dem Abschluss.");
        PersonalGoldBrainFileService.DeleteFileSafe(
            paths.LocalSafetyRoot,
            paths.CommitJournalPath);
        if (File.Exists(paths.CommitJournalPath))
            throw new IOException("Commit-Journal konnte nicht geloescht werden.");
    }

    private static void ValidateMetadata(PersonalGoldBrainCommitJournal journal)
    {
        if (journal.SchemaVersion != 1)
            throw new InvalidDataException("Commit-Journal besitzt eine unbekannte Version.");
        if (!Guid.TryParseExact(journal.TransactionId, "N", out _))
            throw new InvalidDataException("Commit-Journal besitzt keine gueltige Transaktions-ID.");
        if (journal.StartedUtc == default)
            throw new InvalidDataException("Commit-Journal besitzt keinen gueltigen Startzeitpunkt.");
        if (journal.LegacyProtocolTrainingWasPresent)
        {
            if (journal.LegacyProtocolTrainingSha256 is not { Length: 64 }
                || journal.LegacyProtocolTrainingSha256.Any(character => !Uri.IsHexDigit(character)))
            {
                throw new InvalidDataException(
                    "Commit-Journal besitzt keine gueltige Protokoll-Pruefsumme.");
            }
        }
        else if (journal.LegacyProtocolTrainingSha256 is not null)
        {
            throw new InvalidDataException(
                "Commit-Journal enthaelt eine unerwartete Protokoll-Pruefsumme.");
        }
    }

    private static void EnsureCanonicalJournalPath(
        string journalPath,
        string resolvedPath,
        string name)
    {
        if (string.IsNullOrWhiteSpace(journalPath)
            || !string.Equals(
                Path.TrimEndingDirectorySeparator(journalPath),
                resolvedPath,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                $"Commit-Journal enthaelt einen nicht kanonischen Pfad: {name}.");
        }
    }
}
