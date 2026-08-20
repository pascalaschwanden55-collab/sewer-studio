using System.Collections.Generic;
using System.IO;
using System.Linq;
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
    private readonly Func<string, string, string?> _inspectStaging;
    private readonly Func<string, string, string?> _cleanupStaging;

    public ImportTransactionRecoveryService(IImportTransactionJournal journal)
        : this(journal, InspectStaging, CleanupStaging)
    {
    }

    internal ImportTransactionRecoveryService(
        IImportTransactionJournal journal,
        Func<string, string, string?> cleanupStaging)
        : this(journal, InspectStaging, cleanupStaging)
    {
    }

    internal ImportTransactionRecoveryService(
        IImportTransactionJournal journal,
        Func<string, string, string?> inspectStaging,
        Func<string, string, string?> cleanupStaging)
    {
        _journal = journal ?? throw new ArgumentNullException(nameof(journal));
        _inspectStaging = inspectStaging ?? throw new ArgumentNullException(nameof(inspectStaging));
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
            // Erst pruefen, dann anfassen - auch hier.
            var committedStagingObstacle = _inspectStaging(marker.StagingRoot, projectRoot);
            if (!string.IsNullOrWhiteSpace(committedStagingObstacle))
            {
                return new ImportRecoveryResult(
                    ImportRecoveryOutcome.Blocked,
                    "Der Import ist gespeichert, aber der Arbeitsordner kann nicht sicher " +
                    $"entfernt werden. {committedStagingObstacle} Im Projektordner wurde nichts " +
                    "veraendert; der Marker bleibt fuer einen erneuten Lauf erhalten.",
                    ProjectFolderModified: false);
            }

            var committedCleanupWarning = _cleanupStaging(marker.StagingRoot, projectRoot);
            if (!string.IsNullOrWhiteSpace(committedCleanupWarning))
            {
                // Das Aufraeumen ist bereits gelaufen und teilweise gescheitert: im
                // Zweifel NICHT "unveraendert" melden.
                return new ImportRecoveryResult(
                    ImportRecoveryOutcome.Blocked,
                    "Der Import ist gespeichert, aber die Wiederherstellung konnte den " +
                    $"Arbeitsordner nicht vollstaendig aufraeumen. {committedCleanupWarning} " +
                    "Der Marker bleibt fuer einen erneuten Lauf erhalten.",
                    ProjectFolderModified: true);
            }

            var clearWarning = ClearJournalAndVerify(projectRoot);
            if (!string.IsNullOrWhiteSpace(clearWarning))
            {
                // Der Arbeitsordner IST weg - der Projektordner ist damit veraendert.
                return new ImportRecoveryResult(
                    ImportRecoveryOutcome.Blocked,
                    "Der Import ist gespeichert und der Arbeitsordner ist aufgeraeumt, " +
                    $"aber der Wiederherstellungs-Marker konnte nicht entfernt werden. {clearWarning}",
                    ProjectFolderModified: true);
            }

            return new ImportRecoveryResult(
                ImportRecoveryOutcome.CompletedCleanup,
                $"Ein abgeschlossener Import vom {marker.StartedUtc.ToLocalTime():g} wurde aufgeraeumt.",
                ProjectFolderModified: true);
        }

        // Commit ist NICHT durchgelaufen: die veroeffentlichten Dateien zuruecknehmen,
        // aber nur, wenn ihr Inhalt seit der Veroeffentlichung unveraendert ist.
        //
        // ZUERST vollstaendig pruefen, DANN loeschen. Eine Ruecknahme ist alles oder
        // nichts: Ein No-op laesst sich wiederholen, eine Loeschung nicht. Frueher lief
        // die Loeschschleife sofort und erst danach fiel auf, dass der Rollback
        // unvollstaendig bleibt - der Benutzer bekam eine Box, die zugleich
        // "3 Datei(en) zurueckgenommen" und "nicht veraendert" behauptete.
        var loeschbar = new List<string>();
        var hindernisse = new List<string>();
        var blockierendeDateien = new List<string>();

        foreach (var target in marker.PublishedTargets)
        {
            var path = Path.Combine(projectRoot, target.RelativePath);

            // Der Marker ist eine Datei im Projekt und damit manipulierbar: ohne
            // Grenzpruefung koennte ein Eintrag mit Aufwaertspfaden oder ein absoluter
            // Pfad bei passendem Hash eine Datei ausserhalb des Projekts loeschen.
            if (!IsSafeRollbackTarget(projectRoot, path))
            {
                hindernisse.Add(
                    $"Der Markereintrag \"{target.RelativePath}\" zeigt aus dem Projekt heraus " +
                    "oder ueber eine Verknuepfung hinaus und wurde nicht angefasst.");
                blockierendeDateien.Add(target.RelativePath);
                continue;
            }

            var pruefung = InspectRollbackTarget(path, target.Sha256);
            switch (pruefung.Verdict)
            {
                case RollbackVerdict.Deletable:
                    loeschbar.Add(path);
                    break;
                case RollbackVerdict.AlreadyGone:
                    break;
                default:
                    hindernisse.Add(pruefung.Warning!);
                    blockierendeDateien.Add(target.RelativePath);
                    break;
            }
        }

        // Der Arbeitsordner gehoert in denselben Preflight. Sonst waeren alle Ziele
        // geloescht, bevor auffaellt, dass ".import-staging" eine Datei oder eine
        // Junction ist - genau der Teilzustand, den der Preflight verhindern soll.
        var stagingObstacle = _inspectStaging(marker.StagingRoot, projectRoot);
        if (!string.IsNullOrWhiteSpace(stagingObstacle))
            hindernisse.Add(stagingObstacle);

        if (hindernisse.Count > 0)
        {
            return new ImportRecoveryResult(
                ImportRecoveryOutcome.Blocked,
                BuildBlockedRollbackMessage(marker, hindernisse, blockierendeDateien, projectRoot),
                ProjectFolderModified: false);
        }

        // Ab hier ist jedes Ziel geprueft; jetzt erst wird geloescht.
        var rolledBack = 0;
        var rollbackWarnings = new List<string>();
        var projektOrdnerVeraendert = false;
        foreach (var path in loeschbar)
        {
            if (TryRollbackFile(projectRoot, path, ShaFuerPfad(marker, projectRoot, path),
                    out var deleted, out var warning))
            {
                if (deleted)
                {
                    rolledBack++;
                    projektOrdnerVeraendert = true;
                }
            }
            else
            {
                // Zwischen Pruefung und Loeschen ist etwas passiert - echter Teilzustand.
                projektOrdnerVeraendert = true;
                if (!string.IsNullOrWhiteSpace(warning))
                    rollbackWarnings.Add(warning);
            }
        }

        var cleanupWarning = _cleanupStaging(marker.StagingRoot, projectRoot);
        if (!string.IsNullOrWhiteSpace(cleanupWarning))
            rollbackWarnings.Add(cleanupWarning);

        if (rollbackWarnings.Count > 0)
        {
            return new ImportRecoveryResult(
                ImportRecoveryOutcome.Blocked,
                $"Die Ruecknahme des unvollstaendigen Imports vom " +
                $"{marker.StartedUtc.ToLocalTime():g} ist unvollstaendig " +
                $"({rolledBack} Datei(en) zurueckgenommen). " +
                string.Join(" ", rollbackWarnings) +
                " Der Marker bleibt fuer eine sichere Pruefung erhalten.",
                ProjectFolderModified: projektOrdnerVeraendert);
        }

        var rollbackClearWarning = ClearJournalAndVerify(projectRoot);
        if (!string.IsNullOrWhiteSpace(rollbackClearWarning))
        {
            return new ImportRecoveryResult(
                ImportRecoveryOutcome.Blocked,
                $"Der unvollstaendige Import vom {marker.StartedUtc.ToLocalTime():g} wurde " +
                $"zurueckgenommen ({rolledBack} Datei(en)), aber der Wiederherstellungs-Marker " +
                $"konnte nicht entfernt werden. {rollbackClearWarning}",
                ProjectFolderModified: true);
        }

        return new ImportRecoveryResult(
            ImportRecoveryOutcome.RolledBack,
            $"Ein unvollstaendiger Import vom {marker.StartedUtc.ToLocalTime():g} wurde zurueckgenommen " +
            $"({rolledBack} Datei(en)).",
            ProjectFolderModified: true);
    }

    /// <summary>
    /// Sperrmeldung mit Namen und Ausweg. Ohne beides steht der Benutzer vor einem
    /// Projekt, das nicht mehr aufgeht: beide Oeffnen-Wege der Shell enden bei Blocked.
    /// </summary>
    private static string BuildBlockedRollbackMessage(
        ImportTransactionMarker marker,
        IReadOnlyList<string> hindernisse,
        IReadOnlyList<string> blockierendeDateien,
        string projectRoot)
    {
        var markerPfad = Path.Combine(projectRoot, FileImportTransactionJournal.MarkerFileName);
        return
            $"Der unvollstaendige Import vom {marker.StartedUtc.ToLocalTime():g} wurde NICHT " +
            "zurueckgenommen; im Projektordner wurde nichts veraendert. " +
            $"Im Weg: {string.Join(", ", blockierendeDateien)}. " +
            string.Join(" ", hindernisse) +
            $" Der Wiederherstellungs-Marker liegt unter {markerPfad}. " +
            "Pruefen Sie die genannten Dateien und sichern Sie sie. Wird der Marker danach " +
            "entfernt, laesst sich das Projekt wieder oeffnen; die bereits kopierten " +
            "Importdateien bleiben dann im Projekt.";
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

    private enum RollbackVerdict
    {
        /// <summary>Datei liegt unveraendert vor und darf entfernt werden.</summary>
        Deletable,
        /// <summary>Datei ist bereits weg - nichts zu tun, kein Hindernis.</summary>
        AlreadyGone,
        /// <summary>Etwas steht im Weg; die Ruecknahme darf gar nicht erst beginnen.</summary>
        Blocked
    }

    private readonly record struct RollbackInspection(RollbackVerdict Verdict, string? Warning);

    /// <summary>
    /// Reine Pruefung eines Rollback-Ziels - loescht NICHTS. Erst wenn jedes Ziel
    /// geprueft ist, darf die Ruecknahme beginnen.
    /// </summary>
    private static RollbackInspection InspectRollbackTarget(string path, string expectedSha)
    {
        try
        {
            if (!TryGetPathAttributes(path, out var attributes, out var missing, out var accessError))
            {
                return new RollbackInspection(
                    RollbackVerdict.Blocked,
                    $"Die Importdatei \"{Path.GetFileName(path)}\" konnte nicht sicher geprueft " +
                    $"werden ({accessError}).");
            }

            if (missing)
                return new RollbackInspection(RollbackVerdict.AlreadyGone, null);

            if ((attributes & FileAttributes.Directory) != 0)
            {
                return new RollbackInspection(
                    RollbackVerdict.Blocked,
                    $"Am erwarteten Importdateipfad \"{Path.GetFileName(path)}\" liegt ein Ordner; " +
                    "er wurde nicht angefasst.");
            }

            var currentSha = VerifiedImportFileCopy.ComputeSha256(path);
            if (!currentSha.Equals(expectedSha, StringComparison.OrdinalIgnoreCase))
            {
                return new RollbackInspection(
                    RollbackVerdict.Blocked,
                    $"Die Importdatei \"{Path.GetFileName(path)}\" wurde nach dem Import veraendert " +
                    "und deshalb nicht angefasst.");
            }

            return new RollbackInspection(RollbackVerdict.Deletable, null);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new RollbackInspection(
                RollbackVerdict.Blocked,
                $"Die Importdatei \"{Path.GetFileName(path)}\" konnte nicht geprueft werden " +
                $"({ex.Message}).");
        }
    }

    /// <summary>
    /// Entfernt ein bereits geprueftes Ziel und bestaetigt die Entfernung. Ein Fehler
    /// hier ist ein echter Teilzustand und wird als solcher gemeldet.
    /// </summary>
    /// <summary>Der im Marker hinterlegte Hash zu einem bereits aufgeloesten Zielpfad.</summary>
    private static string ShaFuerPfad(
        ImportTransactionMarker marker,
        string projectRoot,
        string path)
    {
        foreach (var target in marker.PublishedTargets)
        {
            if (string.Equals(
                    Path.Combine(projectRoot, target.RelativePath),
                    path,
                    StringComparison.OrdinalIgnoreCase))
            {
                return target.Sha256;
            }
        }

        return string.Empty;
    }

    /// <summary>
    /// Entfernt ein bereits geprueftes Ziel - prueft Pfadgrenze und Hash unmittelbar
    /// davor aber ERNEUT. Zwischen Preflight und Loeschung koennen Minuten liegen, und
    /// im Projektordner arbeiten weitere Prozesse (Spiegelung, zweite Programminstanz).
    /// Das schliesst das Zeitfenster nicht vollstaendig - dafuer braeuchte es ein
    /// exklusives Handle -, macht es aber wieder so klein wie vor der Aufteilung in
    /// Pruefen und Loeschen.
    /// </summary>
    private static bool TryRollbackFile(
        string projectRoot,
        string path,
        string expectedSha,
        out bool deleted,
        out string? warning)
    {
        deleted = false;
        warning = null;
        try
        {
            if (!IsSafeRollbackTarget(projectRoot, path))
            {
                warning =
                    $"Die Importdatei \"{Path.GetFileName(path)}\" ist seit der Pruefung nicht " +
                    "mehr sicher erreichbar und wurde nicht angefasst.";
                return false;
            }

            var erneut = InspectRollbackTarget(path, expectedSha);
            if (erneut.Verdict == RollbackVerdict.AlreadyGone)
                return true;
            if (erneut.Verdict != RollbackVerdict.Deletable)
            {
                warning = erneut.Warning;
                return false;
            }

            File.Delete(path);
            if (!TryGetPathAttributes(path, out _, out var missing, out var accessError))
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
    /// <summary>
    /// Prueft schreibfrei, ob der Arbeitsordner sicher entfernt werden koennte -
    /// LOESCHT NICHTS. Gehoert in denselben Preflight wie die Zieldateien: sonst
    /// waeren alle Ziele bereits geloescht, wenn hier ein Hindernis auffaellt.
    /// </summary>
    /// <returns><c>null</c>, wenn nichts im Weg steht, sonst den Hinderungsgrund.</returns>
    private static string? InspectStaging(string stagingRoot, string projectRoot)
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

            var fullStaging = ResolveStagingPath(stagingRoot);
            if (!IsExpectedStagingLocation(projectRoot, fullStaging))
            {
                return $"Der Arbeitsordner \"{fullStaging}\" liegt nicht an einem erlaubten " +
                       "Projektort und wurde nicht geloescht.";
            }

            // Eine Junction/ein Symlink im Arbeitsordner oder seiner Elternkette darf nie rekursiv
            // geloescht werden - der Inhalt laege ausserhalb des Projekts.
            try
            {
                new ImportFileStagingPathGuard(projectRoot).EnsureNoNestedReparsePoint(fullStaging);
            }
            catch (IOException ex)
            {
                return $"Arbeitsordner nicht geloescht ({ex.Message}).";
            }

            return null;
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException
            or PathTooLongException or IOException or UnauthorizedAccessException)
        {
            return $"Arbeitsordner nicht geloescht ({ex.Message}).";
        }
    }

    /// <summary>
    /// Loescht den Arbeitsordner einer beendeten Transaktion. Prueft unmittelbar davor
    /// erneut ueber <see cref="InspectStaging"/> - zwischen Preflight und Loeschung
    /// koennen andere Prozesse den Ordner ausgetauscht haben.
    /// </summary>
    /// <returns><c>null</c> bei Erfolg/nichts zu tun, sonst einen Warnhinweis.</returns>
    private static string? CleanupStaging(string stagingRoot, string projectRoot)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(stagingRoot))
                return null;

            var obstacle = InspectStaging(stagingRoot, projectRoot);
            if (!string.IsNullOrWhiteSpace(obstacle))
                return obstacle;

            var fullStaging = ResolveStagingPath(stagingRoot);
            if (!TryGetPathAttributes(fullStaging, out _, out var missingBefore, out _))
                return "Arbeitsordner konnte vor dem Entfernen nicht geprueft werden.";
            if (missingBefore)
                return null;

            Directory.Delete(fullStaging, recursive: true);
            if (!TryGetPathAttributes(
                    fullStaging,
                    out _,
                    out var stagingMissing,
                    out var stagingAccessError))
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

    private static string ResolveStagingPath(string stagingRoot)
        => Path.GetFullPath(stagingRoot)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    /// <summary>
    /// Alte Projekte speichern projekt.json direkt im Root, neue unter Projektdateien;
    /// beide festen Orte sind sicher. Die Session arbeitet in GUID-Unterordnern davon.
    /// </summary>
    private static bool IsExpectedStagingLocation(string projectRoot, string fullStaging)
    {
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

        return expectedRoots.Any(expectedRoot =>
            string.Equals(fullStaging, expectedRoot, StringComparison.OrdinalIgnoreCase)
            || IsDirectGuidSessionDirectory(expectedRoot, fullStaging));
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
