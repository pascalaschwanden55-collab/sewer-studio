namespace AuswertungPro.Next.Application.Backup;

/// <summary>
/// Erkennt SQLite-Dateien und erzeugt konsistente, gepruefte Sicherungsstaende.
/// </summary>
public interface ISqliteSnapshotCopier
{
    bool IsSqliteDatabase(string path);

    bool IsCompanionOfSqliteDatabase(string path);

    long GetConservativeSnapshotBytes(string databasePath);

    Task CreateVerifiedSnapshotAsync(
        string sourcePath,
        string targetPath,
        Action<string>? afterSnapshotWritten,
        CancellationToken ct);
}
