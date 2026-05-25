using AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class KnowledgeBaseInfrastructureTests
{
    [Fact]
    public void KnowledgeBaseContext_CreatesExpectedSchema()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), "AuswertungPro.Next.Tests", Guid.NewGuid().ToString("N"), "KnowledgeBase.db");

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
}
