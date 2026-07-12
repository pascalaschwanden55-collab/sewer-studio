using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace AuswertungPro.Next.Infrastructure.Backup;

/// <summary>
/// Erstellt mit der SQLite-Online-Sicherung einen konsistenten Datenbankstand.
/// Laufende Schreibvorgaenge duerfen waehrenddessen weitergehen.
/// </summary>
internal static class SqliteSnapshotCopier
{
    private static readonly byte[] SqliteHeader = Encoding.ASCII.GetBytes("SQLite format 3\0");

    public static bool IsSqliteDatabase(string path)
    {
        try
        {
            if (!File.Exists(path) || new FileInfo(path).Length < SqliteHeader.Length)
                return false;

            Span<byte> header = stackalloc byte[16];
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            return stream.Read(header) == header.Length && header.SequenceEqual(SqliteHeader);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    public static bool IsCompanionOfSqliteDatabase(string path)
    {
        string? mainPath = null;
        if (path.EndsWith("-wal", StringComparison.OrdinalIgnoreCase))
            mainPath = path[..^4];
        else if (path.EndsWith("-shm", StringComparison.OrdinalIgnoreCase))
            mainPath = path[..^4];

        return mainPath is not null && IsSqliteDatabase(mainPath);
    }

    public static long GetConservativeSnapshotBytes(string databasePath)
    {
        var bytes = new FileInfo(databasePath).Length;
        var walPath = databasePath + "-wal";
        if (File.Exists(walPath))
            bytes = checked(bytes + new FileInfo(walPath).Length);
        return bytes;
    }

    public static Task CreateVerifiedSnapshotAsync(
        string sourcePath,
        string targetPath,
        Action<string>? afterSnapshotWritten,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        TryDelete(targetPath);

        var sourceBuilder = new SqliteConnectionStringBuilder
        {
            DataSource = sourcePath,
            Mode = SqliteOpenMode.ReadOnly,
            Cache = SqliteCacheMode.Private,
            Pooling = false,
            DefaultTimeout = 30
        };
        var targetBuilder = new SqliteConnectionStringBuilder
        {
            DataSource = targetPath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Private,
            Pooling = false,
            DefaultTimeout = 30
        };

        using (var source = new SqliteConnection(sourceBuilder.ToString()))
        using (var target = new SqliteConnection(targetBuilder.ToString()))
        {
            source.Open();
            target.Open();
            source.BackupDatabase(target);
        }

        ct.ThrowIfCancellationRequested();
        afterSnapshotWritten?.Invoke(targetPath);
        ValidateSnapshot(targetPath);
        ct.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }

    private static void ValidateSnapshot(string path)
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadWrite,
            Cache = SqliteCacheMode.Private,
            Pooling = false,
            DefaultTimeout = 30
        };

        using var connection = new SqliteConnection(builder.ToString());
        connection.Open();
        using (var journalCommand = connection.CreateCommand())
        {
            // Der Sicherungsstand soll aus genau einer selbststaendigen Datei bestehen.
            // Beim Wiederherstellen schaltet SewerStudio selbst wieder auf WAL um.
            journalCommand.CommandText = "PRAGMA journal_mode=DELETE;";
            journalCommand.ExecuteNonQuery();
        }
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA integrity_check;";
        var result = Convert.ToString(command.ExecuteScalar());
        if (!string.Equals(result, "ok", StringComparison.OrdinalIgnoreCase))
            throw new IOException($"SQLite-Inhaltspruefung fehlgeschlagen: {result ?? "keine Antwort"}");
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
            // Der anschliessende SQLite-Zugriff liefert die konkrete Fehlermeldung.
        }
    }
}
