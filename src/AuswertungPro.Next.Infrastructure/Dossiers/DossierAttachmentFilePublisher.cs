using System;
using System.IO;
using System.Security.Cryptography;

using AuswertungPro.Next.Infrastructure.Import;

namespace AuswertungPro.Next.Infrastructure.Dossiers;

/// <summary>
/// Veroeffentlicht eine Protokollkopie erst, nachdem sie vollstaendig in eine
/// eindeutige Temp-Datei geschrieben wurde. Eine vorhandene automatische
/// Kopie wird zuerst atomar weggestellt und erst dort geprueft. So kann ein
/// Pfadtausch zwischen Hashpruefung und Ersetzen keine manuelle PDF treffen.
/// </summary>
internal static class DossierAttachmentFilePublisher
{
    internal static void CopyAtomically(
        string source,
        string target,
        ProjectWritePathGuard guard,
        string? expectedExistingSha256 = null,
        DossierAttachmentPublishSession? session = null,
        Action? afterExistingTargetStaged = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(target);
        ArgumentNullException.ThrowIfNull(guard);

        var safeTarget = guard.EnsureSafeFileTarget(target);
        var temporary = UniqueSidecar(safeTarget, ".tmp", guard);

        try
        {
            File.Copy(source, temporary, overwrite: false);
            PublishTemporary(
                temporary,
                safeTarget,
                guard,
                expectedExistingSha256,
                session,
                afterExistingTargetStaged);
        }
        finally
        {
            TryDeleteTemporary(temporary);
        }
    }

    internal static void WriteAllBytesAtomically(
        byte[] bytes,
        string target,
        ProjectWritePathGuard guard,
        string? expectedExistingSha256 = null,
        DossierAttachmentPublishSession? session = null,
        Action? afterExistingTargetStaged = null)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        ArgumentException.ThrowIfNullOrWhiteSpace(target);
        ArgumentNullException.ThrowIfNull(guard);

        var safeTarget = guard.EnsureSafeFileTarget(target);
        var temporary = UniqueSidecar(safeTarget, ".tmp", guard);

        try
        {
            File.WriteAllBytes(temporary, bytes);
            PublishTemporary(
                temporary,
                safeTarget,
                guard,
                expectedExistingSha256,
                session,
                afterExistingTargetStaged);
        }
        finally
        {
            TryDeleteTemporary(temporary);
        }
    }

    private static void PublishTemporary(
        string temporary,
        string target,
        ProjectWritePathGuard guard,
        string? expectedExistingSha256,
        DossierAttachmentPublishSession? session,
        Action? afterExistingTargetStaged)
    {
        var publishedHash = Hash(temporary);
        if (!File.Exists(target))
        {
            // Ohne Overwrite: Eine inzwischen gleichnamig angelegte manuelle
            // Datei laesst File.Move scheitern und bleibt damit erhalten.
            File.Move(temporary, target);
            session?.TrackCreated(target, publishedHash);
            return;
        }

        if (string.IsNullOrWhiteSpace(expectedExistingSha256))
        {
            throw new IOException(
                $"Die vorhandene Beilage '{Path.GetFileName(target)}' ist nicht "
                + "eindeutig als automatische Kopie verifiziert.");
        }

        var backup = UniqueSidecar(target, ".rollback", guard);
        File.Move(target, backup);

        string previousHash;
        try
        {
            // Entscheidend ist der Inhalt, der wirklich am Zielpfad stand und
            // atomar verschoben wurde, nicht ein zuvor geoeffneter Handle.
            previousHash = Hash(backup);
        }
        catch
        {
            TryRestoreVerifiedBackup(backup, target);
            throw;
        }

        if (!string.Equals(
                previousHash,
                expectedExistingSha256,
                StringComparison.OrdinalIgnoreCase))
        {
            var restoredAt = RestoreUnexpectedFile(backup, target, guard);
            throw new IOException(
                $"Die vorhandene Beilage '{Path.GetFileName(target)}' wurde "
                + "zwischenzeitlich veraendert und blieb unter "
                + $"'{Path.GetFileName(restoredAt)}' erhalten.");
        }

        afterExistingTargetStaged?.Invoke();
        try
        {
            // Ohne Overwrite: Eine nach dem Wegstellen neu angelegte manuelle
            // PDF wird niemals durch die automatische Kopie ersetzt.
            File.Move(temporary, target);
        }
        catch
        {
            TryRestoreVerifiedBackup(backup, target);
            throw;
        }

        if (session is not null)
        {
            session.TrackReplaced(
                target,
                backup,
                publishedHash,
                previousHash);
            return;
        }

        TryDeleteVerifiedBackup(backup, previousHash);
    }

    private static string UniqueSidecar(
        string target,
        string suffix,
        ProjectWritePathGuard guard)
        => guard.EnsureSafeFileTarget(
            target + "." + Guid.NewGuid().ToString("N") + suffix);

    private static string RestoreUnexpectedFile(
        string staged,
        string target,
        ProjectWritePathGuard guard)
    {
        try
        {
            File.Move(staged, target);
            return target;
        }
        catch (IOException) when (File.Exists(target))
        {
            var conflict = guard.EnsureSafeFileTarget(Path.Combine(
                Path.GetDirectoryName(target)!,
                Path.GetFileNameWithoutExtension(target)
                + "_manuell_gesichert_"
                + Guid.NewGuid().ToString("N")
                + Path.GetExtension(target)));
            File.Move(staged, conflict);
            return conflict;
        }
    }

    private static void TryRestoreVerifiedBackup(string backup, string target)
    {
        try
        {
            if (File.Exists(backup) && !File.Exists(target))
                File.Move(backup, target);
        }
        catch (IOException)
        {
            // Eine inzwischen angelegte Datei am Ziel wird niemals ersetzt.
            // Die verifizierte Sicherung bleibt fuer die manuelle Recovery.
        }
    }

    private static void TryDeleteVerifiedBackup(string backup, string expectedHash)
    {
        try
        {
            if (File.Exists(backup)
                && string.Equals(Hash(backup), expectedHash, StringComparison.OrdinalIgnoreCase))
            {
                File.Delete(backup);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Die Sicherung ist keine PDF-Ausgabedatei. Bei Unsicherheit bleibt
            // sie erhalten, statt fremden Inhalt zu loeschen.
        }
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

    private static void TryDeleteTemporary(string temporary)
    {
        try
        {
            if (File.Exists(temporary))
                File.Delete(temporary);
        }
        catch
        {
            // Eine Temp-Datei ist keine PDF und wird nie als Beilage
            // eingelesen. Der eigentliche Kopierfehler bleibt erhalten.
        }
    }
}
