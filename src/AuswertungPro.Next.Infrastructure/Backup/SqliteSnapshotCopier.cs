using AuswertungPro.Next.Application.Backup;

namespace AuswertungPro.Next.Infrastructure.Backup;

/// <summary>Kompatible interne Fassade; Datei- und SQLite-Arbeit liegt im Instanzdienst.</summary>
internal static class SqliteSnapshotCopier
{
    private static ISqliteSnapshotCopier _current = new SqliteSnapshotCopyService();

    internal static ISqliteSnapshotCopier Current => Volatile.Read(ref _current);

    internal static void Use(ISqliteSnapshotCopier copier)
        => Volatile.Write(
            ref _current,
            copier ?? throw new ArgumentNullException(nameof(copier)));

    public static bool IsSqliteDatabase(string path)
        => Current.IsSqliteDatabase(path);

    public static bool IsCompanionOfSqliteDatabase(string path)
        => Current.IsCompanionOfSqliteDatabase(path);

    public static long GetConservativeSnapshotBytes(string databasePath)
        => Current.GetConservativeSnapshotBytes(databasePath);

    public static Task CreateVerifiedSnapshotAsync(
        string sourcePath,
        string targetPath,
        Action<string>? afterSnapshotWritten,
        CancellationToken ct)
        => Current.CreateVerifiedSnapshotAsync(sourcePath, targetPath, afterSnapshotWritten, ct);
}
