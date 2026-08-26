using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

namespace AuswertungPro.Next.Infrastructure.Dossiers;

/// <summary>
/// Verbindet publizierte PDFs mit ihrem beim Schreiben bekannten Hash und
/// rollt sie bei einem spaeten Manifestfehler ohne Overwrite zurueck.
/// </summary>
internal sealed class DossierAttachmentPublishSession
{
    private readonly ICollection<string> _warnings;
    private readonly List<PublishedFile> _files = [];

    public DossierAttachmentPublishSession(ICollection<string> warnings)
        => _warnings = warnings ?? throw new ArgumentNullException(nameof(warnings));

    public void TrackCreated(string target, string publishedSha256)
        => _files.Add(new PublishedFile(target, publishedSha256, null, null));

    public void TrackReplaced(
        string target,
        string backup,
        string publishedSha256,
        string previousSha256)
        => _files.Add(new PublishedFile(
            target,
            publishedSha256,
            backup,
            previousSha256));

    /// <summary>
    /// Liefert nur den Hash der tatsaechlich publizierten Temp-Datei. Der
    /// spaeter am Ziel gelesene Inhalt darf nie Eigentum erhalten.
    /// </summary>
    public string GetRequiredPublishedHash(string target)
    {
        var fullTarget = Path.GetFullPath(target);
        var match = _files.SingleOrDefault(file => string.Equals(
            Path.GetFullPath(file.TargetPath),
            fullTarget,
            StringComparison.OrdinalIgnoreCase));
        if (match is null)
        {
            throw new InvalidDataException(
                $"Die automatische Beilage '{Path.GetFileName(target)}' wurde "
                + "nicht in diesem Sammellauf publiziert.");
        }

        return match.PublishedSha256;
    }

    public void Complete()
    {
        foreach (var file in _files)
        {
            if (file.BackupPath is null || file.PreviousSha256 is null)
                continue;

            if (!TryDeleteVerified(
                    file.BackupPath,
                    file.PreviousSha256,
                    out var error))
            {
                _warnings.Add(
                    $"Die Sicherung fuer '{Path.GetFileName(file.TargetPath)}' blieb "
                    + $"zur Sicherheit erhalten ({error}).");
            }
        }

        _files.Clear();
    }

    public void Rollback()
    {
        foreach (var file in _files.AsEnumerable().Reverse())
            Rollback(file);

        _files.Clear();
    }

    private void Rollback(PublishedFile file)
    {
        string? stagedCurrent = null;
        try
        {
            if (File.Exists(file.TargetPath))
            {
                stagedCurrent = UniqueSidecar(file.TargetPath, ".failed");
                File.Move(file.TargetPath, stagedCurrent);
                var actual = Hash(stagedCurrent);
                if (!string.Equals(
                        actual,
                        file.PublishedSha256,
                        StringComparison.OrdinalIgnoreCase))
                {
                    var restoredAt = RestoreUnexpectedFile(stagedCurrent, file.TargetPath);
                    stagedCurrent = null;
                    _warnings.Add(
                        $"Die Beilage '{Path.GetFileName(file.TargetPath)}' wurde "
                        + "zwischenzeitlich veraendert und deshalb nicht zurueckgesetzt. "
                        + $"Der fremde Inhalt blieb unter '{Path.GetFileName(restoredAt)}'.");
                    if (file.BackupPath is not null && File.Exists(file.BackupPath))
                    {
                        _warnings.Add(
                            $"Die letzte gepruefte Sicherung fuer "
                            + $"'{Path.GetFileName(file.TargetPath)}' blieb ebenfalls erhalten.");
                    }
                    return;
                }
            }

            if (file.BackupPath is null)
            {
                DeletePublishedStage(stagedCurrent, file);
                return;
            }

            if (file.PreviousSha256 is null
                || !File.Exists(file.BackupPath)
                || !string.Equals(
                    Hash(file.BackupPath),
                    file.PreviousSha256,
                    StringComparison.OrdinalIgnoreCase))
            {
                if (stagedCurrent is not null && !File.Exists(file.TargetPath))
                {
                    File.Move(stagedCurrent, file.TargetPath);
                    stagedCurrent = null;
                }

                _warnings.Add(
                    $"Die vorherige Beilage '{Path.GetFileName(file.TargetPath)}' "
                    + "konnte nicht wiederhergestellt werden: Sicherung fehlt oder "
                    + "wurde veraendert.");
                return;
            }

            // Ohne Overwrite: Eine inzwischen am Ziel angelegte manuelle PDF
            // bleibt bestehen; die alte Sicherung wird dann nicht angeruehrt.
            File.Move(file.BackupPath, file.TargetPath);
            DeletePublishedStage(stagedCurrent, file);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            if (stagedCurrent is not null && File.Exists(stagedCurrent))
            {
                try
                {
                    RestoreUnexpectedFile(stagedCurrent, file.TargetPath);
                }
                catch
                {
                    // Beide Dateien bleiben erhalten; die erste Fehlermeldung
                    // beschreibt den abgebrochenen Rollback.
                }
            }

            _warnings.Add(
                $"Die Beilage '{Path.GetFileName(file.TargetPath)}' konnte nach einem "
                + $"Manifestfehler nicht zurueckgesetzt werden ({ex.Message}).");
        }
    }

    private void DeletePublishedStage(string? stagedCurrent, PublishedFile file)
    {
        if (stagedCurrent is null)
            return;

        if (!TryDeleteVerified(stagedCurrent, file.PublishedSha256, out var error))
        {
            _warnings.Add(
                $"Die zurueckgenommene neue Kopie "
                + $"'{Path.GetFileName(file.TargetPath)}' blieb erhalten ({error}).");
        }
    }

    private static string RestoreUnexpectedFile(string staged, string target)
    {
        try
        {
            File.Move(staged, target);
            return target;
        }
        catch (IOException) when (File.Exists(target))
        {
            var conflict = Path.Combine(
                Path.GetDirectoryName(target)!,
                Path.GetFileNameWithoutExtension(target)
                + "_manuell_gesichert_"
                + Guid.NewGuid().ToString("N")
                + Path.GetExtension(target));
            File.Move(staged, conflict);
            return conflict;
        }
    }

    private static bool TryDeleteVerified(
        string path,
        string expectedSha256,
        out string error)
    {
        error = string.Empty;
        try
        {
            if (!File.Exists(path))
                return true;

            var current = Hash(path);
            if (!string.Equals(current, expectedSha256, StringComparison.OrdinalIgnoreCase))
            {
                error = "Hash stimmt nicht mehr";
                return false;
            }

            File.Delete(path);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            error = ex.Message;
            return false;
        }
    }

    private static string UniqueSidecar(string target, string suffix)
        => target + "." + Guid.NewGuid().ToString("N") + suffix;

    private static string Hash(string path)
    {
        using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private sealed record PublishedFile(
        string TargetPath,
        string PublishedSha256,
        string? BackupPath,
        string? PreviousSha256);
}
