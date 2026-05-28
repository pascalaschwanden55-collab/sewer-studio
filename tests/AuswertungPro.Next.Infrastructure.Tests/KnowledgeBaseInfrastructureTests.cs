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
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }
}
