using AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase;
using Microsoft.Data.Sqlite;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class KnowledgeBaseInfrastructureTests
{
    [Fact]
    public void KnowledgeBaseContext_CreatesExpectedSchema()
    {
        var root = Path.Combine(Path.GetTempPath(), "AuswertungPro.Next.Tests", Guid.NewGuid().ToString("N"));
        var dbPath = Path.Combine(root, "KnowledgeBase.db");

        try
        {
            using var db = new KnowledgeBaseContext(dbPath);

            using var cmd = db.Connection.CreateCommand();
            cmd.CommandText = """
                SELECT COUNT(*)
                FROM sqlite_master
                WHERE type = 'table'
                  AND name IN ('Samples', 'Embeddings', 'Versions', 'CategoryWeights', 'ValidationLog')
                """;

            var tableCount = Convert.ToInt32(cmd.ExecuteScalar());
            Assert.Equal(5, tableCount);

            // busy_timeout gesetzt (Schutz gegen "database is locked" bei Rebuild vs. Retrieval)
            using var pragma = db.Connection.CreateCommand();
            pragma.CommandText = "PRAGMA busy_timeout;";
            Assert.Equal(3000, Convert.ToInt32(pragma.ExecuteScalar()));

            // Additiver Index auf Embeddings.Model existiert
            using var idxCmd = db.Connection.CreateCommand();
            idxCmd.CommandText =
                "SELECT COUNT(*) FROM sqlite_master WHERE type='index' AND name='idx_embeddings_model'";
            Assert.Equal(1, Convert.ToInt32(idxCmd.ExecuteScalar()));

            using var versionCmd = db.Connection.CreateCommand();
            versionCmd.CommandText = "PRAGMA user_version;";
            Assert.Equal(KnowledgeBaseContext.SchemaVersion, Convert.ToInt32(versionCmd.ExecuteScalar()));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void KnowledgeBaseContext_RejectsFutureUserVersion()
    {
        var root = Path.Combine(Path.GetTempPath(), "AuswertungPro.Next.Tests", Guid.NewGuid().ToString("N"));
        var dbPath = Path.Combine(root, "KnowledgeBase.db");
        var futureVersion = KnowledgeBaseContext.SchemaVersion + 1;

        try
        {
            using (var db = new KnowledgeBaseContext(dbPath))
            {
                using var setCmd = db.Connection.CreateCommand();
                setCmd.CommandText = $"PRAGMA user_version={futureVersion};";
                setCmd.ExecuteNonQuery();
            }

            var error = Assert.Throws<InvalidDataException>(() => new KnowledgeBaseContext(dbPath));
            Assert.Contains("neueren Programmversion", error.Message);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void KnowledgeBaseContext_MigriertAlteDatenbankOhneDatenverlust()
    {
        var root = Path.Combine(Path.GetTempPath(), "AuswertungPro.Next.Tests", Guid.NewGuid().ToString("N"));
        var dbPath = Path.Combine(root, "KnowledgeBase.db");

        try
        {
            Directory.CreateDirectory(root);
            CreateLegacyDatabase(dbPath);

            using var db = new KnowledgeBaseContext(dbPath);

            using (var sample = db.Connection.CreateCommand())
            {
                sample.CommandText = """
                    SELECT SampleId, CaseId, VsaCode, Beschreibung, MeterStart, MeterEnd,
                           IsStreck, FramePath, ExportedUtc, VersionId,
                           SourceType, QualityGateLevel, HumanConfirmed, Corrected,
                           ConfirmedByUser, ConfirmedAtUtc
                    FROM Samples
                    WHERE SampleId = 'sample-alt'
                    """;
                using var reader = sample.ExecuteReader();
                Assert.True(reader.Read());
                Assert.Equal("sample-alt", reader.GetString(0));
                Assert.Equal("fall-17", reader.GetString(1));
                Assert.Equal("BAB", reader.GetString(2));
                Assert.Equal("Alter, bestaetigter Schaden", reader.GetString(3));
                Assert.Equal(1.25, reader.GetDouble(4));
                Assert.Equal(2.75, reader.GetDouble(5));
                Assert.Equal(1, reader.GetInt32(6));
                Assert.Equal("frames/alt.png", reader.GetString(7));
                Assert.Equal("2026-01-02T03:04:05Z", reader.GetString(8));
                Assert.Equal("v-alt", reader.GetString(9));
                Assert.Equal(string.Empty, reader.GetString(10));
                Assert.Equal(string.Empty, reader.GetString(11));
                Assert.True(reader.IsDBNull(12));
                Assert.True(reader.IsDBNull(13));
                Assert.True(reader.IsDBNull(14));
                Assert.True(reader.IsDBNull(15));
                Assert.False(reader.Read());
            }

            using (var embedding = db.Connection.CreateCommand())
            {
                embedding.CommandText = "SELECT Model, Vector, CreatedAt FROM Embeddings WHERE SampleId='sample-alt'";
                using var reader = embedding.ExecuteReader();
                Assert.True(reader.Read());
                Assert.Equal("modell-alt", reader.GetString(0));
                Assert.Equal(new byte[] { 1, 2, 3, 4 }, (byte[])reader[1]);
                Assert.Equal("2026-01-02T03:05:00Z", reader.GetString(2));
            }

            using (var version = db.Connection.CreateCommand())
            {
                version.CommandText = "SELECT SampleCount, Notes FROM Versions WHERE VersionId='v-alt'";
                using var reader = version.ExecuteReader();
                Assert.True(reader.Read());
                Assert.Equal(1, reader.GetInt32(0));
                Assert.Equal("Bestandsdaten", reader.GetString(1));
            }

            using var schema = db.Connection.CreateCommand();
            schema.CommandText = """
                SELECT COUNT(*)
                FROM pragma_table_info('Samples')
                WHERE name IN (
                    'SourceType', 'QualityGateLevel', 'HumanConfirmed', 'Corrected',
                    'ConfirmedByUser', 'ConfirmedAtUtc')
                """;
            Assert.Equal(6, Convert.ToInt32(schema.ExecuteScalar()));

            using var userVersion = db.Connection.CreateCommand();
            userVersion.CommandText = "PRAGMA user_version;";
            Assert.Equal(KnowledgeBaseContext.SchemaVersion, Convert.ToInt32(userVersion.ExecuteScalar()));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    private static void CreateLegacyDatabase(string dbPath)
    {
        using var connection = new SqliteConnection($"Data Source={dbPath}");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            PRAGMA user_version=0;

            CREATE TABLE Samples (
                SampleId     TEXT    PRIMARY KEY,
                CaseId       TEXT    NOT NULL,
                VsaCode      TEXT    NOT NULL,
                Beschreibung TEXT    NOT NULL DEFAULT '',
                MeterStart   REAL    NOT NULL DEFAULT 0,
                MeterEnd     REAL    NOT NULL DEFAULT 0,
                IsStreck     INTEGER NOT NULL DEFAULT 0,
                FramePath    TEXT    NOT NULL DEFAULT '',
                ExportedUtc  TEXT    NOT NULL,
                VersionId    TEXT    NOT NULL
            );

            CREATE TABLE Embeddings (
                SampleId  TEXT PRIMARY KEY,
                Model     TEXT NOT NULL,
                Vector    BLOB NOT NULL,
                CreatedAt TEXT NOT NULL
            );

            CREATE TABLE Versions (
                VersionId   TEXT PRIMARY KEY,
                CreatedAt   TEXT NOT NULL,
                SampleCount INTEGER NOT NULL DEFAULT 0,
                Notes       TEXT NOT NULL DEFAULT ''
            );

            INSERT INTO Samples (
                SampleId, CaseId, VsaCode, Beschreibung, MeterStart, MeterEnd,
                IsStreck, FramePath, ExportedUtc, VersionId)
            VALUES (
                'sample-alt', 'fall-17', 'BAB', 'Alter, bestaetigter Schaden', 1.25, 2.75,
                1, 'frames/alt.png', '2026-01-02T03:04:05Z', 'v-alt');

            INSERT INTO Embeddings (SampleId, Model, Vector, CreatedAt)
            VALUES ('sample-alt', 'modell-alt', X'01020304', '2026-01-02T03:05:00Z');

            INSERT INTO Versions (VersionId, CreatedAt, SampleCount, Notes)
            VALUES ('v-alt', '2026-01-02T03:06:00Z', 1, 'Bestandsdaten');
            """;
        command.ExecuteNonQuery();
    }
}
