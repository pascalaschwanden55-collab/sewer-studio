using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;

using AuswertungPro.Next.Application.Dossiers;
using AuswertungPro.Next.Infrastructure.Import;

namespace AuswertungPro.Next.Infrastructure.Dossiers;

/// <summary>
/// Kennzeichnet ausschliesslich die PDF-Kopien, die der Dossier-Sammler selbst
/// in den Beilagenordner geschrieben hat. Unbekannte Dateien gelten immer als
/// manuell. Eine nachtraeglich veraenderte automatische Datei verliert ihre
/// Eigentumskennzeichnung und wird dadurch ebenfalls wie eine manuelle Beilage
/// geschuetzt.
/// </summary>
internal static class DossierAttachmentOwnershipManifest
{
    internal const string FileName = ".sewerstudio-dossier-beilagen.v1.json";

    private const int SchemaVersion = 1;
    private const string Owner = "SewerStudio.DossierAttachmentCollector";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = false
    };

    internal static DossierAttachmentOwnershipSnapshot Load(
        string attachmentFolder,
        ProjectWritePathGuard guard,
        ICollection<string> warnings)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(attachmentFolder);
        ArgumentNullException.ThrowIfNull(guard);
        ArgumentNullException.ThrowIfNull(warnings);

        var safeFolder = guard.EnsureSafeDirectoryTarget(attachmentFolder);
        var manifestPath = guard.EnsureSafeFileTarget(Path.Combine(safeFolder, FileName));
        if (!File.Exists(manifestPath))
            return DossierAttachmentOwnershipSnapshot.Empty;

        DossierAttachmentOwnershipDocument document;
        try
        {
            var json = File.ReadAllText(manifestPath);
            document = JsonSerializer.Deserialize<DossierAttachmentOwnershipDocument>(
                    json,
                    JsonOptions)
                ?? throw new InvalidDataException("Das Beilagen-Eigentuemermanifest ist leer.");
        }
        catch (InvalidDataException)
        {
            throw;
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            throw new InvalidDataException(
                "Das Beilagen-Eigentuemermanifest ist unlesbar. "
                + "Zur Sicherheit wurden keine Beilagen veraendert.",
                ex);
        }

        if (document.SchemaVersion != SchemaVersion
            || !string.Equals(document.Owner, Owner, StringComparison.Ordinal)
            || document.Files is null)
        {
            throw new InvalidDataException(
                "Das Beilagen-Eigentuemermanifest hat ein unbekanntes Format. "
                + "Zur Sicherheit wurden keine Beilagen veraendert.");
        }

        var entryNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var verified = new Dictionary<string, DossierAttachmentOwnershipEntry>(
            StringComparer.OrdinalIgnoreCase);
        var unavailable = new Dictionary<string, DossierAttachmentOwnershipEntry>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var entry in document.Files)
        {
            ValidateEntry(entry, safeFolder, guard);
            if (!entryNames.Add(entry.FileName))
            {
                throw new InvalidDataException(
                    $"Das Beilagen-Eigentuemermanifest nennt '{entry.FileName}' mehrfach.");
            }

            var path = ResolveDirectChild(safeFolder, entry.FileName, guard);
            if (!File.Exists(path))
                continue;

            string currentHash;
            try
            {
                currentHash = Hash(path);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                warnings.Add(
                    $"Automatische Beilage '{entry.FileName}' konnte nicht geprueft werden "
                    + $"und bleibt deshalb unangetastet ({ex.Message}).");
                unavailable[entry.FileName] = entry;
                continue;
            }

            if (!string.Equals(currentHash, entry.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                warnings.Add(
                    $"Automatische Beilage '{entry.FileName}' wurde verändert. "
                    + "Sie gilt jetzt als manuell und wird nicht entfernt oder ueberschrieben.");
                continue;
            }

            verified[entry.FileName] = entry;
        }

        return new DossierAttachmentOwnershipSnapshot(verified, unavailable);
    }

    internal static void Commit(
        string attachmentFolder,
        ProjectWritePathGuard guard,
        DossierAttachmentOwnershipSnapshot previous,
        IReadOnlyList<DossierAttachment> current,
        bool hasUnresolvedSelections,
        DossierAttachmentPublishSession publications,
        ICollection<string> warnings,
        CancellationToken ct,
        Action<string>? beforeStaleTargetStaged = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(attachmentFolder);
        ArgumentNullException.ThrowIfNull(guard);
        ArgumentNullException.ThrowIfNull(previous);
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(publications);
        ArgumentNullException.ThrowIfNull(warnings);

        var safeFolder = guard.EnsureSafeDirectoryTarget(attachmentFolder);
        var newEntries = BuildEntries(safeFolder, guard, current, publications);
        RetainTemporarilyUnavailableSelections(
            safeFolder,
            guard,
            previous,
            current,
            hasUnresolvedSelections,
            newEntries,
            warnings);
        RetainUnreadableEntries(previous, newEntries);
        var currentNames = new HashSet<string>(
            newEntries.Select(entry => entry.FileName),
            StringComparer.OrdinalIgnoreCase);

        var stale = previous.Verified.Values
            .Where(entry => !currentNames.Contains(entry.FileName))
            .ToList();
        var moved = new List<(string Original, string Quarantine, string Sha256)>();
        string? quarantineFolder = null;
        var manifestWritten = false;

        try
        {
            foreach (var entry in stale)
            {
                ct.ThrowIfCancellationRequested();
                var path = ResolveDirectChild(safeFolder, entry.FileName, guard);
                if (!File.Exists(path))
                    continue;

                quarantineFolder ??= guard.EnsureSafeDirectoryTarget(Path.Combine(
                    safeFolder,
                    ".sewerstudio-stale-" + Guid.NewGuid().ToString("N")));
                Directory.CreateDirectory(quarantineFolder);
                var quarantine = guard.EnsureSafeFileTarget(Path.Combine(
                    quarantineFolder,
                    entry.FileName));
                beforeStaleTargetStaged?.Invoke(path);
                var moveResult = TryMoveVerified(
                    path,
                    quarantine,
                    entry,
                    guard,
                    warnings);
                if (moveResult == VerifiedMoveResult.Moved)
                {
                    moved.Add((path, quarantine, entry.Sha256));
                }
                else if (moveResult == VerifiedMoveResult.Unavailable)
                {
                    newEntries.Add(entry);
                    currentNames.Add(entry.FileName);
                }
            }

            Write(safeFolder, guard, newEntries);
            manifestWritten = true;
        }
        catch
        {
            RestoreMoved(moved, warnings);
            throw;
        }
        finally
        {
            // Nach einem Schreibfehler darf ein nicht wiederherstellbares PDF
            // keinesfalls durch das Aufraeumen des Quarantaeneordners verloren
            // gehen. Nach erfolgreichem Manifestwechsel gehoeren die alten
            // Kopien dagegen sicher nicht mehr zur Ausgabe.
            if (manifestWritten && quarantineFolder is not null)
                TryDeleteQuarantine(quarantineFolder, moved, warnings);
        }
    }

    internal static string ResolveAvailableTarget(
        string attachmentFolder,
        string preferredFileName,
        string subject,
        ProjectWritePathGuard guard,
        DossierAttachmentOwnershipSnapshot previous,
        ISet<string> reservedNames)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(preferredFileName);
        ArgumentNullException.ThrowIfNull(subject);

        var safeFolder = guard.EnsureSafeDirectoryTarget(attachmentFolder);
        for (var suffix = 1; ; suffix++)
        {
            var fileName = suffix == 1
                ? preferredFileName
                : Path.GetFileNameWithoutExtension(preferredFileName)
                  + "_" + suffix.ToString(System.Globalization.CultureInfo.InvariantCulture)
                  + Path.GetExtension(preferredFileName);
            var path = ResolveDirectChild(safeFolder, fileName, guard);
            var mayReplaceOwned = previous.Verified.TryGetValue(fileName, out var owned)
                && string.Equals(
                    owned.Subject,
                    subject.Trim(),
                    StringComparison.OrdinalIgnoreCase);

            if (!reservedNames.Contains(fileName)
                && (!File.Exists(path) || mayReplaceOwned))
            {
                reservedNames.Add(fileName);
                return path;
            }
        }
    }

    internal static bool IsStillVerified(
        string attachmentFolder,
        string path,
        ProjectWritePathGuard guard,
        DossierAttachmentOwnershipSnapshot snapshot,
        ICollection<string> warnings)
    {
        var fileName = Path.GetFileName(path);
        var expectedPath = ResolveDirectChild(attachmentFolder, fileName, guard);
        if (!string.Equals(
                Path.GetFullPath(path),
                expectedPath,
                StringComparison.OrdinalIgnoreCase)
            || !snapshot.Verified.TryGetValue(fileName, out var entry))
        {
            return false;
        }

        try
        {
            if (string.Equals(Hash(expectedPath), entry.Sha256, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            warnings.Add(
                $"Automatische Beilage '{entry.FileName}' konnte fuer die Vorschau nicht "
                + $"erneut geprueft werden und gilt deshalb als manuell ({ex.Message}).");
            return false;
        }

        warnings.Add(
            $"Automatische Beilage '{entry.FileName}' wurde vor der Vorschau verändert. "
            + "Sie gilt jetzt als manuell.");
        return false;
    }

    private static List<DossierAttachmentOwnershipEntry> BuildEntries(
        string folder,
        ProjectWritePathGuard guard,
        IReadOnlyList<DossierAttachment> attachments,
        DossierAttachmentPublishSession publications)
    {
        var result = new List<DossierAttachmentOwnershipEntry>();
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var attachment in attachments.Where(item =>
                     item.Kind is DossierAttachmentKind.OriginalProtocol
                         or DossierAttachmentKind.GeneratedProtocol))
        {
            if (string.IsNullOrWhiteSpace(attachment.SourcePath)
                || string.IsNullOrWhiteSpace(attachment.FileName))
            {
                throw new InvalidDataException(
                    "Eine automatische Beilage hat keinen gueltigen Zielpfad.");
            }

            var path = ResolveDirectChild(folder, attachment.FileName, guard);
            if (!string.Equals(
                    Path.GetFullPath(attachment.SourcePath),
                    path,
                    StringComparison.OrdinalIgnoreCase)
                || !File.Exists(path)
                || !names.Add(attachment.FileName))
            {
                throw new InvalidDataException(
                    $"Die automatische Beilage '{attachment.FileName}' ist nicht eindeutig.");
            }

            var publishedHash = publications.GetRequiredPublishedHash(path);
            var currentHash = Hash(path);
            if (!string.Equals(
                    currentHash,
                    publishedHash,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new IOException(
                    $"Die gerade publizierte Beilage '{attachment.FileName}' wurde "
                    + "vor dem Manifest-Schreiben ausgetauscht.");
            }

            result.Add(new DossierAttachmentOwnershipEntry
            {
                FileName = attachment.FileName,
                Sha256 = publishedHash,
                Kind = attachment.Kind.ToString(),
                Subject = attachment.HoldingName ?? string.Empty
            });
        }

        return result;
    }

    /// <summary>
    /// Ein weiterhin ausgewaehltes Protokoll darf bei einem voruebergehenden
    /// Such-, Lese- oder Kopierfehler nicht wie eine Abwahl behandelt werden.
    /// Die letzte eindeutig eigene Kopie bleibt im Ordner und im Manifest,
    /// waehrend das Ergebnis weiterhin ehrlich <c>Missing</c> meldet.
    /// </summary>
    private static void RetainTemporarilyUnavailableSelections(
        string folder,
        ProjectWritePathGuard guard,
        DossierAttachmentOwnershipSnapshot previous,
        IReadOnlyList<DossierAttachment> current,
        bool hasUnresolvedSelections,
        ICollection<DossierAttachmentOwnershipEntry> entries,
        ICollection<string> warnings)
    {
        var missing = current
            .Where(item => item.Kind == DossierAttachmentKind.Missing)
            .Select(item => item.HoldingName?.Trim() ?? string.Empty)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (missing.Count == 0 && !hasUnresolvedSelections)
            return;

        var unknownSubject = hasUnresolvedSelections || missing.Contains(string.Empty);
        var names = entries
            .Select(entry => entry.FileName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in previous.Verified.Values)
        {
            if (names.Contains(entry.FileName)
                || !unknownSubject && !missing.Contains(entry.Subject))
            {
                continue;
            }

            var path = ResolveDirectChild(folder, entry.FileName, guard);
            if (!File.Exists(path)
                || !IsStillVerified(folder, path, guard, previous, warnings))
            {
                continue;
            }

            entries.Add(entry);
            names.Add(entry.FileName);
            warnings.Add(
                $"Die bisherige automatische Beilage '{entry.FileName}' bleibt erhalten, "
                + "weil das weiterhin ausgewaehlte Protokoll momentan nicht neu gelesen "
                + "werden konnte.");
        }
    }

    private static void RetainUnreadableEntries(
        DossierAttachmentOwnershipSnapshot previous,
        ICollection<DossierAttachmentOwnershipEntry> entries)
    {
        var names = entries
            .Select(entry => entry.FileName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in previous.Unavailable.Values)
        {
            if (names.Add(entry.FileName))
                entries.Add(entry);
        }
    }

    private static VerifiedMoveResult TryMoveVerified(
        string path,
        string quarantine,
        DossierAttachmentOwnershipEntry entry,
        ProjectWritePathGuard guard,
        ICollection<string> warnings)
    {
        try
        {
            // Zuerst wird genau der aktuelle Pfadinhalt atomar weggestellt,
            // erst danach diese konkrete Datei geprueft.
            File.Move(path, quarantine);
            var currentHash = Hash(quarantine);
            if (!string.Equals(
                    currentHash,
                    entry.Sha256,
                    StringComparison.OrdinalIgnoreCase))
            {
                var restoredAt = RestoreUnexpectedMove(quarantine, path, guard);
                warnings.Add(
                    $"Automatische Beilage '{entry.FileName}' wurde waehrend des "
                    + "Sammelns veraendert. Der fremde Inhalt blieb unter "
                    + $"'{Path.GetFileName(restoredAt)}' erhalten.");
                return VerifiedMoveResult.Changed;
            }

            return VerifiedMoveResult.Moved;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            if (File.Exists(quarantine) && !File.Exists(path))
            {
                try
                {
                    File.Move(quarantine, path);
                }
                catch
                {
                    // Die Sicherung bleibt im Quarantaeneordner erhalten.
                }
            }

            warnings.Add(
                $"Automatische Beilage '{entry.FileName}' konnte vor dem Entfernen nicht "
                + $"geprueft werden und bleibt unangetastet ({ex.Message}).");
            return VerifiedMoveResult.Unavailable;
        }
    }

    private static string RestoreUnexpectedMove(
        string quarantine,
        string original,
        ProjectWritePathGuard guard)
    {
        try
        {
            File.Move(quarantine, original);
            return original;
        }
        catch (IOException) when (File.Exists(original))
        {
            var conflict = guard.EnsureSafeFileTarget(Path.Combine(
                Path.GetDirectoryName(original)!,
                Path.GetFileNameWithoutExtension(original)
                + "_manuell_gesichert_"
                + Guid.NewGuid().ToString("N")
                + Path.GetExtension(original)));
            File.Move(quarantine, conflict);
            return conflict;
        }
    }

    private static void ValidateEntry(
        DossierAttachmentOwnershipEntry entry,
        string folder,
        ProjectWritePathGuard guard)
    {
        if (entry is null
            || string.IsNullOrWhiteSpace(entry.FileName)
            || !string.Equals(
                Path.GetFileName(entry.FileName),
                entry.FileName,
                StringComparison.Ordinal)
            || !string.Equals(
                Path.GetExtension(entry.FileName),
                ".pdf",
                StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(entry.Sha256)
            || entry.Sha256.Length != 64
            || entry.Sha256.Any(character => !Uri.IsHexDigit(character))
            || !Enum.TryParse<DossierAttachmentKind>(entry.Kind, out var kind)
            || !Enum.IsDefined(kind)
            || kind is not (DossierAttachmentKind.OriginalProtocol
                or DossierAttachmentKind.GeneratedProtocol))
        {
            throw new InvalidDataException(
                "Das Beilagen-Eigentuemermanifest enthaelt einen ungueltigen Eintrag.");
        }

        _ = ResolveDirectChild(folder, entry.FileName, guard);
    }

    private static string ResolveDirectChild(
        string folder,
        string fileName,
        ProjectWritePathGuard guard)
    {
        if (Path.IsPathRooted(fileName)
            || !string.Equals(Path.GetFileName(fileName), fileName, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Ein Pfad im Beilagen-Eigentuemermanifest verlaesst den Beilagenordner.");
        }

        var safeFolder = guard.EnsureSafeDirectoryTarget(folder);
        var path = guard.EnsureSafeFileTarget(Path.Combine(safeFolder, fileName));
        if (!string.Equals(
                Path.GetDirectoryName(path),
                safeFolder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                "Ein Pfad im Beilagen-Eigentuemermanifest verlaesst den Beilagenordner.");
        }

        return path;
    }

    private static string Hash(string path)
    {
        using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private static void Write(
        string folder,
        ProjectWritePathGuard guard,
        IReadOnlyList<DossierAttachmentOwnershipEntry> entries)
    {
        var path = guard.EnsureSafeFileTarget(Path.Combine(folder, FileName));
        var temporary = guard.EnsureSafeFileTarget(
            path + "." + Guid.NewGuid().ToString("N") + ".tmp");
        var json = JsonSerializer.Serialize(new DossierAttachmentOwnershipDocument
        {
            SchemaVersion = SchemaVersion,
            Owner = Owner,
            Files = entries.ToList()
        }, JsonOptions);

        try
        {
            File.WriteAllText(temporary, json);
            if (File.Exists(path))
                File.Replace(temporary, path, destinationBackupFileName: null);
            else
                File.Move(temporary, path);
        }
        finally
        {
            try
            {
                if (File.Exists(temporary))
                    File.Delete(temporary);
            }
            catch
            {
                // Die gueltige Manifestdatei ist bereits veroeffentlicht.
            }
        }
    }

    private static void RestoreMoved(
        IEnumerable<(string Original, string Quarantine, string Sha256)> moved,
        ICollection<string> warnings)
    {
        foreach (var (original, quarantine, sha256) in moved.Reverse())
        {
            try
            {
                if (!File.Exists(quarantine))
                    continue;

                if (!string.Equals(Hash(quarantine), sha256, StringComparison.OrdinalIgnoreCase))
                {
                    warnings.Add(
                        $"Die Quarantaene-Sicherung '{Path.GetFileName(quarantine)}' "
                        + "wurde veraendert und deshalb nicht zurueckverschoben.");
                    continue;
                }

                if (File.Exists(original))
                {
                    warnings.Add(
                        $"Die Sicherung '{Path.GetFileName(quarantine)}' blieb erhalten, "
                        + "weil der urspruengliche Zielpfad inzwischen belegt ist.");
                    continue;
                }

                File.Move(quarantine, original);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                warnings.Add(
                    $"Die Sicherung '{Path.GetFileName(quarantine)}' konnte nicht "
                    + $"zurueckverschoben werden ({ex.Message}).");
            }
        }
    }

    private static void TryDeleteQuarantine(
        string folder,
        IEnumerable<(string Original, string Quarantine, string Sha256)> moved,
        ICollection<string> warnings)
    {
        try
        {
            // Nur die zuvor selbst verschobenen, hash-verifizierten Dateien
            // loeschen. Ein unerwarteter fremder Inhalt im Ordner darf durch
            // kein rekursives Aufraeumen verschwinden.
            foreach (var (_, quarantine, sha256) in moved)
            {
                if (!File.Exists(quarantine))
                    continue;

                if (!string.Equals(Hash(quarantine), sha256, StringComparison.OrdinalIgnoreCase))
                {
                    warnings.Add(
                        $"Die Quarantaene-Datei '{Path.GetFileName(quarantine)}' wurde "
                        + "veraendert und deshalb nicht geloescht.");
                    continue;
                }

                File.Delete(quarantine);
            }

            if (Directory.Exists(folder))
                Directory.Delete(folder, recursive: false);
        }
        catch
        {
            // Der Unterordner wird vom PDF-Sammler nicht gelesen. Eine
            // automatische Beilage kann dadurch nicht wieder im Dossier landen.
        }
    }

    private sealed class DossierAttachmentOwnershipDocument
    {
        public int SchemaVersion { get; set; }
        public string Owner { get; set; } = string.Empty;
        public List<DossierAttachmentOwnershipEntry>? Files { get; set; }
    }

    private enum VerifiedMoveResult
    {
        Moved,
        Changed,
        Unavailable
    }
}

internal sealed class DossierAttachmentOwnershipSnapshot
{
    internal static DossierAttachmentOwnershipSnapshot Empty { get; } = new(
        new Dictionary<string, DossierAttachmentOwnershipEntry>(StringComparer.OrdinalIgnoreCase),
        new Dictionary<string, DossierAttachmentOwnershipEntry>(StringComparer.OrdinalIgnoreCase));

    internal DossierAttachmentOwnershipSnapshot(
        IReadOnlyDictionary<string, DossierAttachmentOwnershipEntry> verified,
        IReadOnlyDictionary<string, DossierAttachmentOwnershipEntry> unavailable)
    {
        Verified = verified;
        Unavailable = unavailable;
    }

    internal IReadOnlyDictionary<string, DossierAttachmentOwnershipEntry> Verified { get; }
    internal IReadOnlyDictionary<string, DossierAttachmentOwnershipEntry> Unavailable { get; }
}

internal sealed class DossierAttachmentOwnershipEntry
{
    public string FileName { get; set; } = string.Empty;
    public string Sha256 { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
}
