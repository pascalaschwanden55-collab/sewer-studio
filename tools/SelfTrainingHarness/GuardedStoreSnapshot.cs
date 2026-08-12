using System.Security.Cryptography;

namespace SelfTrainingHarness;

/// <summary>
/// Stellt einen Harness-Snapshot nur wieder her, solange die Anwendung nicht laeuft
/// und der Harness-Stand seit der letzten eigenen Aenderung bytegleich geblieben ist.
/// </summary>
public sealed class GuardedStoreSnapshot
{
    private readonly FileState _originalState;
    private FileState? _harnessState;
    private bool _restored;

    private GuardedStoreSnapshot(
        string targetPath,
        FileState originalState,
        string? backupPath)
    {
        TargetPath = targetPath;
        _originalState = originalState;
        BackupPath = backupPath;
    }

    public string TargetPath { get; }

    public string? BackupPath { get; }

    public static GuardedStoreSnapshot Create(string targetPath)
    {
        if (string.IsNullOrWhiteSpace(targetPath))
            throw new ArgumentException("Der Store-Pfad darf nicht leer sein.", nameof(targetPath));

        var fullPath = Path.GetFullPath(targetPath);
        var originalState = FileState.Read(fullPath);
        if (!originalState.Exists)
            return new GuardedStoreSnapshot(fullPath, originalState, backupPath: null);

        var backupPath = fullPath + $".harness-bak-{DateTime.UtcNow:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}";
        try
        {
            File.Copy(fullPath, backupPath, overwrite: false);
            var stateAfterCopy = FileState.Read(fullPath);
            var backupState = FileState.Read(backupPath);
            if (!originalState.Equals(stateAfterCopy) || !originalState.Equals(backupState))
                throw new IOException("Der Trainings-Store wurde waehrend der Sicherung veraendert.");

            return new GuardedStoreSnapshot(fullPath, originalState, backupPath);
        }
        catch
        {
            TryDelete(backupPath);
            throw;
        }
    }

    public void MarkHarnessWritesComplete()
        => _harnessState = FileState.Read(TargetPath);

    public bool TryRestore(Func<bool> isSewerStudioRunning, out string reason)
    {
        ArgumentNullException.ThrowIfNull(isSewerStudioRunning);

        if (_restored)
        {
            reason = "Der Snapshot wurde bereits wiederhergestellt.";
            return true;
        }

        if (_harnessState is null)
        {
            reason = "Der letzte Harness-Stand wurde nicht festgehalten.";
            return false;
        }

        try
        {
            if (isSewerStudioRunning())
            {
                reason = "SewerStudio laeuft; der Harness ueberschreibt den Store nicht.";
                return false;
            }

            if (!_harnessState.Equals(FileState.Read(TargetPath)))
            {
                reason = "Der Trainings-Store wurde nach dem Harness parallel veraendert.";
                return false;
            }

            // Zweite Pruefung unmittelbar vor dem schreibenden Schritt verkleinert das
            // Zeitfenster zwischen Vergleich und Wiederherstellung.
            if (isSewerStudioRunning())
            {
                reason = "SewerStudio laeuft; der Harness ueberschreibt den Store nicht.";
                return false;
            }

            if (!_harnessState.Equals(FileState.Read(TargetPath)))
            {
                reason = "Der Trainings-Store wurde nach dem Harness parallel veraendert.";
                return false;
            }

            RestoreOriginal();
            if (!_originalState.Equals(FileState.Read(TargetPath)))
                throw new IOException("Der wiederhergestellte Store stimmt nicht mit dem Snapshot ueberein.");

            if (BackupPath is not null)
                File.Delete(BackupPath);

            _restored = true;
            reason = "";
            return true;
        }
        catch (Exception ex)
        {
            reason = $"Wiederherstellung fehlgeschlagen: {ex.GetType().Name}: {ex.Message}";
            return false;
        }
    }

    private void RestoreOriginal()
    {
        if (!_originalState.Exists)
        {
            if (File.Exists(TargetPath))
                File.Delete(TargetPath);
            return;
        }

        if (BackupPath is null || !File.Exists(BackupPath))
            throw new FileNotFoundException("Die Harness-Sicherung fehlt.", BackupPath);

        var temporaryPath = TargetPath + $".harness-restore-{Guid.NewGuid():N}.tmp";
        try
        {
            File.Copy(BackupPath, temporaryPath, overwrite: false);
            if (File.Exists(TargetPath))
                File.Replace(temporaryPath, TargetPath, destinationBackupFileName: null, ignoreMetadataErrors: true);
            else
                File.Move(temporaryPath, TargetPath);
        }
        finally
        {
            TryDelete(temporaryPath);
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // Die eigentliche Sicherungs-/Wiederherstellungsursache bleibt massgebend.
        }
    }

    private sealed record FileState(bool Exists, long Length, string Sha256)
    {
        public static FileState Read(string path)
        {
            if (!File.Exists(path))
                return new FileState(false, 0, "");

            using var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read);
            var hash = Convert.ToHexString(SHA256.HashData(stream));
            return new FileState(true, stream.Length, hash);
        }
    }
}
