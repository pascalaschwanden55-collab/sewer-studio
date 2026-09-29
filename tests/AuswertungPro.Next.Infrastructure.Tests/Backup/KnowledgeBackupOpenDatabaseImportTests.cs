using System.IO.Compression;
using AuswertungPro.Next.Infrastructure.Ai.Backup;
using AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase;
using AuswertungPro.Next.Infrastructure.Backup;
using Microsoft.Data.Sqlite;

namespace AuswertungPro.Next.Infrastructure.Tests.Backup;

public sealed class KnowledgeBackupOpenDatabaseImportTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "sewerstudio-knowledge-import-" + Guid.NewGuid());

    [Fact]
    public async Task OffeneWissensdatenbank_ImportScheitertOhneDatenverlust_UndGelingtNachSchliessen()
    {
        var knowledgeRoot = Path.Combine(_root, "knowledge");
        Directory.CreateDirectory(knowledgeRoot);
        var livePath = Path.Combine(knowledgeRoot, "KnowledgeBase.db");
        var importedPath = Path.Combine(_root, "imported.db");
        var snapshotPath = Path.Combine(_root, "snapshot.db");
        var zipPath = Path.Combine(_root, "backup.zip");

        using (var imported = new KnowledgeBaseContext(importedPath))
        {
            Execute(imported.Connection, "CREATE TABLE ImportProbe(Value TEXT NOT NULL);");
            Execute(imported.Connection, "INSERT INTO ImportProbe(Value) VALUES ('neu');");
            await new SqliteSnapshotCopyService().CreateVerifiedSnapshotAsync(
                importedPath, snapshotPath, null, CancellationToken.None);
            SqliteConnection.ClearPool(imported.Connection);
        }

        using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
            zip.CreateEntryFromFile(snapshotPath, "knowledge/KnowledgeBase.db");

        var locations = new KnowledgeBackupLocations(
            knowledgeRoot,
            Path.Combine(_root, "roaming-ap"),
            Path.Combine(_root, "roaming-ss"),
            Path.Combine(_root, "local-ss"),
            Path.Combine(_root, "training-center.json"),
            Path.Combine(_root, "temp"));

        using (var live = new KnowledgeBaseContext(livePath))
        {
            try
            {
                Execute(live.Connection, "PRAGMA wal_autocheckpoint=0;");
                Execute(live.Connection, "CREATE TABLE ImportProbe(Value TEXT NOT NULL);");
                Execute(live.Connection, "INSERT INTO ImportProbe(Value) VALUES ('alt');");
                Assert.True(File.Exists(livePath + "-wal"));

                var blocked = await KnowledgeBackupEngine.ImportAsync(
                    zipPath, locations, () => { });

                Assert.False(blocked.Success);
                Assert.Contains("momentan nicht verfügbar", blocked.Error);
                Assert.Equal("alt", ReadProbe(live.Connection));
                using var reopened = new KnowledgeBaseContext(livePath);
                Assert.Equal("alt", ReadProbe(reopened.Connection));
                using (var integrity = reopened.Connection.CreateCommand())
                {
                    integrity.CommandText = "PRAGMA integrity_check;";
                    Assert.Equal("ok", integrity.ExecuteScalar());
                }
                Assert.Empty(Directory.EnumerateFileSystemEntries(locations.TempRoot));
                SqliteConnection.ClearPool(reopened.Connection);
            }
            finally
            {
                SqliteConnection.ClearPool(live.Connection);
            }
        }

        var importedResult = await KnowledgeBackupEngine.ImportAsync(
            zipPath, locations, () => { });
        Assert.True(importedResult.Success, importedResult.Error);
        using (var importedLive = new KnowledgeBaseContext(livePath))
        {
            Assert.Equal("neu", ReadProbe(importedLive.Connection));
            SqliteConnection.ClearPool(importedLive.Connection);
        }
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    private static void Execute(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private static string? ReadProbe(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Value FROM ImportProbe;";
        return Convert.ToString(command.ExecuteScalar());
    }
}
