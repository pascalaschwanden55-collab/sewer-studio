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
    public void KnowledgeBaseContext_DoesNotDowngradeFutureUserVersion()
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

            using (var db = new KnowledgeBaseContext(dbPath))
            using (var versionCmd = db.Connection.CreateCommand())
            {
                versionCmd.CommandText = "PRAGMA user_version;";
                Assert.Equal(futureVersion, Convert.ToInt32(versionCmd.ExecuteScalar()));
            }
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }
}
