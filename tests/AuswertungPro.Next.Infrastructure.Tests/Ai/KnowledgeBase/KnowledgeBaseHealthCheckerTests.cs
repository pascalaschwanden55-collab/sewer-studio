using AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase;

namespace AuswertungPro.Next.Infrastructure.Tests.Ai.KnowledgeBase;

public sealed class KnowledgeBaseHealthCheckerTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "kb-health-" + Guid.NewGuid().ToString("N"));

    public KnowledgeBaseHealthCheckerTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        try { Directory.Delete(_root, recursive: true); } catch { }
    }

    [Fact]
    public void Check_ValidDatabase_IsHealthy()
    {
        var path = Path.Combine(_root, "KnowledgeBase.db");
        using (var context = new KnowledgeBaseContext(path)) { }

        var result = KnowledgeBaseHealthChecker.Check(path);

        Assert.True(result.IsHealthy, result.Error);
    }

    [Fact]
    public void Check_ByteGarbage_ReturnsClearFailure()
    {
        var path = Path.Combine(_root, "KnowledgeBase.db");
        File.WriteAllBytes(path, [0x01, 0x02, 0x03, 0x04, 0x05]);

        var result = KnowledgeBaseHealthChecker.Check(path);

        Assert.False(result.IsHealthy);
        Assert.False(string.IsNullOrWhiteSpace(result.Error));
    }
}
