using AuswertungPro.Next.Infrastructure.Backup;
using AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase;
using Microsoft.Data.Sqlite;

namespace AuswertungPro.Next.Infrastructure.Tests.Backup;

public sealed class SqliteSnapshotCopyServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "sewerstudio-sqlite-snapshot-" + Guid.NewGuid());

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public async Task Instanzdienst_ErzeugtEinenSelbststaendigenGeprueftenSchnappschuss()
    {
        Directory.CreateDirectory(_root);
        var sourcePath = Path.Combine(_root, "source.db");
        var targetPath = Path.Combine(_root, "target.db");
        using (var source = Open(sourcePath, SqliteOpenMode.ReadWriteCreate))
        {
            Execute(source, "PRAGMA journal_mode=WAL;");
            Execute(source, "CREATE TABLE Items(Id INTEGER PRIMARY KEY, Name TEXT NOT NULL);");
            Execute(source, "INSERT INTO Items(Name) VALUES ('eins');");

            var service = new SqliteSnapshotCopyService();
            await service.CreateVerifiedSnapshotAsync(sourcePath, targetPath, null, CancellationToken.None);

            Assert.True(service.IsSqliteDatabase(sourcePath));
            Assert.True(service.IsSqliteDatabase(targetPath));
            Assert.True(service.GetConservativeSnapshotBytes(sourcePath) >= new FileInfo(sourcePath).Length);
        }

        using var target = Open(targetPath, SqliteOpenMode.ReadOnly);
        using var command = target.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM Items;";
        Assert.Equal(1L, Convert.ToInt64(command.ExecuteScalar()));
        Assert.False(File.Exists(targetPath + "-wal"));
    }

    [Fact]
    public async Task GeoeffneteWissensdatenbank_LaesstSichSichernUndWiederOeffnen()
    {
        Directory.CreateDirectory(_root);
        var sourcePath = Path.Combine(_root, "knowledge.db");
        var snapshotPath = Path.Combine(_root, "snapshot.db");
        var restoredPath = Path.Combine(_root, "restored.db");

        // Ein bereits benutzter Pool darf den Schnappschuss nicht behindern.
        using (var warm = new KnowledgeBaseContext(sourcePath))
            Assert.True(new SqliteConnectionStringBuilder(warm.Connection.ConnectionString).Pooling);

        using var source = new KnowledgeBaseContext(sourcePath);
        try
        {
            Execute(source.Connection, "PRAGMA wal_autocheckpoint=0;");
            Execute(source.Connection, "CREATE TABLE SnapshotProbe(Value TEXT NOT NULL);");
            Execute(source.Connection, "INSERT INTO SnapshotProbe(Value) VALUES ('sichtbar');");
            Assert.True(File.Exists(sourcePath + "-wal"));

            var service = new SqliteSnapshotCopyService();
            await service.CreateVerifiedSnapshotAsync(sourcePath, snapshotPath, null, CancellationToken.None);
            Assert.False(File.Exists(snapshotPath + "-wal"));
            Assert.False(File.Exists(snapshotPath + "-shm"));

            Execute(source.Connection, "INSERT INTO SnapshotProbe(Value) VALUES ('spaeter');");
            using (var sourceCommand = source.Connection.CreateCommand())
            {
                sourceCommand.CommandText = "SELECT COUNT(*) FROM SnapshotProbe;";
                Assert.Equal(2L, Convert.ToInt64(sourceCommand.ExecuteScalar()));
            }

            File.Copy(snapshotPath, restoredPath);
            using var restored = new KnowledgeBaseContext(restoredPath);
            try
            {
                using var command = restored.Connection.CreateCommand();
                command.CommandText = "SELECT group_concat(Value, ',') FROM SnapshotProbe;";
                Assert.Equal("sichtbar", command.ExecuteScalar());
            }
            finally
            {
                SqliteConnection.ClearPool(restored.Connection);
            }
        }
        finally
        {
            SqliteConnection.ClearPool(source.Connection);
        }
    }

    private static SqliteConnection Open(string path, SqliteOpenMode mode)
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = mode,
            Pooling = false
        }.ToString());
        connection.Open();
        return connection;
    }

    private static void Execute(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }
}
