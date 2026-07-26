using System.IO;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.Import;

namespace AuswertungPro.Next.Infrastructure.Import;

/// <summary>
/// Stellt beim Projekt-Laden den Alles-oder-nichts-Zustand einer unterbrochenen
/// Import-Transaktion her. Existiert der Marker (<see cref="FileImportTransactionJournal"/>)
/// noch, starb der Prozess mitten drin: bei passender Commit-TxId nur aufraeumen, sonst die
/// veroeffentlichten Dateien SHA-verifiziert zurueckrollen. Idempotent (der Marker wird am
/// Ende nur geloescht, wenn keine Ruecknahme- oder Aufraeumreste bleiben).
/// </summary>
public sealed class ImportTransactionRecoveryService : IImportTransactionRecoveryService
{
    private readonly IImportTransactionJournal _journal;
    private readonly Func<string, string, string?> _cleanupStaging;

    public ImportTransactionRecoveryService(IImportTransactionJournal journal)
        : this(journal, CleanupStaging)
    {
    }

    internal ImportTransactionRecoveryService(
        IImportTransactionJournal journal,
        Func<string, string, string?> cleanupStaging)
    {
        _journal = journal ?? throw new ArgumentNullException(nameof(journal));
        _cleanupStaging = cleanupStaging ?? throw new ArgumentNullException(nameof(cleanupStaging));
    }

    public ImportRecoveryResult RecoverIfNeeded(string projectRoot, string? committedImportTxId)
    {
        if (string.IsNullOrWhiteSpace(projectRoot))
            return new ImportRecoveryResult(ImportRecoveryOutcome.None, null);

        var readResult = _journal.Read(projectRoot);
        if (readResult.Outcome == ImportTransactionJournalReadOutcome.Failed)
        {
            return new ImportRecoveryResult(
                ImportRecoveryOutcome.Blocked,
                readResult.ErrorMessage
                ?? "Der Import-Wiederherstellungsmarker konnte nicht sicher gelesen werden.");
        }

        var marker = readResult.Marker;
        if (readResult.Outcome == ImportTransactionJournalReadOutcome.Missing || marker is null)
            return new ImportRecoveryResult(ImportRecoveryOutcome.None, null);

        if (string.Equals(marker.TxId, committedImportTxId, StringComparison.Ordinal))
        {
            // Der atomare projekt.json-Save ist durchgelaufen (Absturz erst danach) —
            // der neue Zustand ist konsistent, nur Arbeitsordner + Marker aufraeumen.
            var committedCleanupWarning = _cleanupStaging(marker.StagingRoot, projectRoot);
            if (!string.IsNullOrWhiteSpace(committedCleanupWarning))
            {
                return new ImportRecoveryResult(
                    ImportRecoveryOutcome.Blocked,
                    "Der Import ist gespeichert, aber die Wiederherstellung konnte den " +
                    $"Arbeitsordner nicht vollstaendig aufraeumen. {committedCleanupWarning} " +
                    "Der Marker bleibt fuer einen erneuten Lauf erhalten.");
            }

            var clearWarning = ClearJournalAndVerify(projectRoot);
            if (!string.IsNullOrWhiteSpace(clearWarning))
            {
                return new ImportRecoveryResult(
                    ImportRecoveryOutcome.Blocked,
                    "Der Import ist gespeichert und der Arbeitsordner ist aufgeraeumt, " +
                    $"aber der Wiederherstellungs-Marker konnte nicht entfernt werden. {clearWarning}");
            }

            return new ImportRecoveryResult(
                ImportRecoveryOutcome.CompletedCleanup,
                $"Ein abgeschlossener Import vom {marker.StartedUtc.ToLocalTime():g} wurde aufgeraeumt.");
        }

        // Commit ist NICHT durchgelaufen: die veroeffentlichten Dateien zuruecknehmen,
        // aber nur, wenn ihr Inhalt seit der Veroeffentlichung unveraendert ist.
        var rolledBack = 0;
        var skippedUnsafeTargets = 0;
        var rollbackWarnings = new List<string>();
        foreach (var target in marker.PublishedTargets)
        {
            var path = Path.Combine(projectRoot, target.RelativePath);
            // Der Marker ist eine Datei im Projekt und damit manipulierbar: ohne
            // Grenzpruefung koennte ein Eintrag wie "..\..\..." oder ein absoluter
            // Pfad bei passendem Hash eine Datei ausserhalb des Projekts loeschen.
            if (!IsSafeRollbackTarget(projectRoot, path))
            {
                skippedUnsafeTargets++;
                continue;
            }

            if (TryRollbackFile(path, target.Sha256, out var deleted, out var warning))
            {
                if (deleted)
                    rolledBack++;
            }
            else if (!string.IsNullOrWhiteSpace(warning))
            {
                rollbackWarnings.Add(warning);
            }
        }

        if (skippedUnsafeTargets > 0)
        {
            rollbackWarnings.Add(
                $"{skippedUnsafeTargets} Markereintraege zeigen ausserhalb des Projekts " +
                "oder ueber Verknuepfungen hinaus und wurden nicht angefasst.");
        }

        var cleanupWarning = _cleanupStaging(marker.StagingRoot, projectRoot);
        if (!string.IsNullOrWhiteSpace(cleanupWarning))
            rollbackWarnings.Add(cleanupWarning);

        if (rollbackWarnings.Count > 0)
        {
            return new ImportRecoveryResult(
                ImportRecoveryOutcome.Blocked,
                $"Die Wiederherstellung des unvollstaendigen Imports vom " +
                $"{marker.StartedUtc.ToLocalTime():g} ist unvollstaendig " +
                $"({rolledBack} Datei(en) zurueckgenommen). " +
                string.Join(" ", rollbackWarnings) +
                " Der Marker bleibt fuer eine sichere Pruefung erhalten.");
        }

        var rollbackClearWarning = ClearJournalAndVerify(projectRoot);
        if (!string.IsNullOrWhiteSpace(rollbackClearWarning))
        {
            return new ImportRecoveryResult(
                ImportRecoveryOutcome.Blocked,
                $"Der unvollstaendige Import vom {marker.StartedUtc.ToLocalTime():g} wurde " +
                $"zurueckgenommen ({rolledBack} Datei(en)), aber der Wiederherstellungs-Marker " +
                $"konnte nicht entfernt werden. {rollbackClearWarning}");
        }

        return new ImportRecoveryResult(
            ImportRecoveryOutcome.RolledBack,
            $"Ein unvollstaendiger Import vom {marker.StartedUtc.ToLocalTime():g} wurde zurueckgenommen " +
            $"({rolledBack} Datei(en)).");
    }

    private string? ClearJournalAndVerify(string projectRoot)
    {
        try
        {
            _journal.Clear(projectRoot);
            var remaining = _journal.Read(projectRoot);
            return remaining.Outcome == ImportTransactionJournalReadOutcome.Missing
                ? null
                : remaining.ErrorMessage
                  ?? "Der Marker ist nach dem Loeschversuch weiterhin vorhanden.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException
            or ArgumentException or NotSupportedException)
        {
            return ex.Message;
        }
    }

    /// <summary>
    /// Prueft ein Rollback-Ziel aus dem (manipulierbaren) Marker: kanonisiert muss es
    /// strikt unterhalb des Projekt-Roots liegen und der Weg dorthin darf keine
    /// Junction/keinen Symlink enthalten. Jeder Zweifel sperrt fail-closed (kein Delete).
    /// </summary>
    private static bool IsSafeRollbackTarget(string projectRoot, string candidatePath)
    {
        try
        {
            var guard = new ImportFileStagingPathGuard(projectRoot);
            var fullCandidate = Path.GetFullPath(candidatePath);
            var root = guard.ProjectRoot.TrimEnd(
                Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            // Strikt UNTERHALB des Roots (der Root selbst ist nie ein loeschbares Dateiziel).
            if (!fullCandidate.StartsWith(
                    root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                return false;

            guard.EnsureNoNestedReparsePoint(fullCandidate);
            return true;
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException
            or PathTooLongException or IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static bool TryRollbackFile(
        string path,
        string expectedSha,
        out bool deleted,
        out string? warning)
    {
        deleted = false;
        warning = null;
        try
        {
            if (!TryGetPathAttributes(path, out var attributes, out var missing, out var accessError))
            {
                warning =
                    $"Die Importdatei \"{Path.GetFileName(path)}\" konnte nicht sicher geprueft " +
                    $"werden ({accessError}).";
                return false;
            }

            if (missing)
                return true;
            if ((attributes & FileAttributes.Directory) != 0)
            {
                warning =
                    $"Am erwarteten Importdateipfad \"{Path.GetFileName(path)}\" liegt ein Ordner; " +
                    "er wurde nicht angefasst.";
                return false;
            }

            var currentSha = VerifiedImportFileCopy.ComputeSha256(path);
            if (!currentSha.Equals(expectedSha, StringComparison.OrdinalIgnoreCase))
            {
                warning =
                    $"Die Importdatei \"{Path.GetFileName(path)}\" wurde inzwischen veraendert " +
                    "und deshalb nicht angefasst.";
                return false;
            }

            File.Delete(path);
            if (!TryGetPathAttributes(path, out _, out missing, out accessError))
            {
                warning =
                    $"Die Entfernung der Importdatei \"{Path.GetFileName(path)}\" konnte nicht " +
                    $"sicher bestaetigt werden ({accessError}).";
                return false;
            }

            if (!missing)
            {
                warning =
                    $"Die Importdatei \"{Path.GetFileName(path)}\" konnte nicht entfernt werden.";
                return false;
            }

            deleted = true;
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            warning =
                $"Die Importdatei \"{Path.GetFileName(path)}\" konnte nicht entfernt werden " +
                $"({ex.Message}).";
            return false;
        }
    }

    /// <summary>
    /// Loescht den Arbeitsordner einer beendeten Transaktion. Der Marker ist eine
    /// Datei im Projekt und damit manipulierbar: geloescht wird nur, wenn der Pfad
    /// einem erwarteten Staging-Ordner neben der Projektdatei entspricht
    /// (die Session arbeitet in GUID-Unterordnern davon) und keine Junction ist.
    /// </summary>
    /// <returns><c>null</c> bei Erfolg/nichts zu tun, sonst einen Warnhinweis.</returns>
    private static string? CleanupStaging(string stagingRoot, string projectRoot)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(stagingRoot))
                return null;
            if (!TryGetPathAttributes(
                    stagingRoot,
                    out var stagingAttributes,
                    out var stagingMissing,
                    out var stagingAccessError))
            {
                return $"Arbeitsordner konnte nicht sicher geprueft werden ({stagingAccessError}).";
            }

            if (stagingMissing)
                return null;
            if ((stagingAttributes & FileAttributes.Directory) == 0)
                return $"Am erwarteten Arbeitsordnerpfad liegt eine Datei: {stagingRoot}";

            // Alte Projekte speichern projekt.json direkt im Root. Neue Projekte
            // speichern sie unter Projektdateien; beide festen Orte sind sicher.
            var expectedRoots = new[]
            {
                Path.Combine(projectRoot, ".import-staging"),
                Path.Combine(
                    projectRoot,
                    ProjectFileLocator.ProjektdateienDir,
                    ".import-staging")
            }
            .Select(path => Path.GetFullPath(path)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
            .ToArray();
            var fullStaging = Path.GetFullPath(stagingRoot)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            var isExpected = expectedRoots.Any(expectedRoot =>
                string.Equals(fullStaging, expectedRoot, StringComparison.OrdinalIgnoreCase)
                || IsDirectGuidSessionDirectory(expectedRoot, fullStaging));
            if (!isExpected)
            {
                return $"Der Arbeitsordner \"{fullStaging}\" liegt nicht an einem erlaubten " +
                       "Projektort und wurde nicht geloescht.";
            }

            // Eine Junction/ein Symlink im Arbeitsordner oder seiner Elternkette darf nie rekursiv
            // geloescht werden — der Inhalt laege ausserhalb des Projekts.
            try
            {
                var pathGuard = new ImportFileStagingPathGuard(projectRoot);
                pathGuard.EnsureNoNestedReparsePoint(fullStaging);
            }
            catch (IOException ex)
            {
                return $"Arbeitsordner nicht geloescht ({ex.Message}).";
            }

            Directory.Delete(fullStaging, recursive: true);
            if (!TryGetPathAttributes(
                    fullStaging,
                    out _,
                    out stagingMissing,
                    out stagingAccessError))
            {
                return $"Die Entfernung des Arbeitsordners konnte nicht sicher bestaetigt " +
                       $"werden ({stagingAccessError}).";
            }

            if (!stagingMissing)
                return "Arbeitsordner konnte nicht vollstaendig entfernt werden.";

            return null;
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException
            or PathTooLongException or IOException or UnauthorizedAccessException)
        {
            return $"Arbeitsordner nicht geloescht ({ex.Message}).";
        }
    }

    private static bool IsDirectGuidSessionDirectory(string expectedRoot, string candidate)
    {
        var parent = Path.GetDirectoryName(candidate);
        if (!string.Equals(parent, expectedRoot, StringComparison.OrdinalIgnoreCase))
            return false;

        var leaf = Path.GetFileName(candidate);
        return leaf.Length == 32
               && Guid.TryParseExact(leaf, "N", out _);
    }

    private static bool TryGetPathAttributes(
        string path,
        out FileAttributes attributes,
        out bool missing,
        out string? error)
    {
        attributes = default;
        missing = false;
        error = null;
        try
        {
            attributes = File.GetAttributes(path);
            return true;
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            missing = true;
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException
            or ArgumentException or NotSupportedException or PathTooLongException)
        {
            error = ex.Message;
            return false;
        }
    }
}
