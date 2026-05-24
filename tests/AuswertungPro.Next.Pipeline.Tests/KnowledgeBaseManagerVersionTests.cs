using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.UI.Ai.KnowledgeBase;
using AuswertungPro.Next.UI.Ai.Ollama;
using AuswertungPro.Next.UI.Ai.Training;
using Microsoft.Data.Sqlite;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

public sealed class KnowledgeBaseManagerVersionTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(
        Path.GetTempPath(),
        "sewerstudio-kb-version-" + Guid.NewGuid().ToString("N") + ".db");

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        TryDelete(_dbPath);
        TryDelete(_dbPath + "-wal");
        TryDelete(_dbPath + "-shm");
    }

    [Fact]
    public async Task IndexSampleAsync_updates_incremental_version_sample_count()
    {
        using var db = new KnowledgeBaseContext(_dbPath);
        var manager = new KnowledgeBaseManager(db, CreateEmbedder());

        var ok1 = await manager.IndexSampleAsync(CreateSample("s1", "BAB"));
        var ok2 = await manager.IndexSampleAsync(CreateSample("s2", "BBA"));

        Assert.True(ok1);
        Assert.True(ok2);
        Assert.Equal(2, manager.GetIndexedCount());
        Assert.Equal(1, ScalarInt(db.Connection, "SELECT COUNT(*) FROM Versions"));
        Assert.Equal(2, ScalarInt(db.Connection, "SELECT SampleCount FROM Versions LIMIT 1"));
    }

    private static TrainingSample CreateSample(string id, string code) => new()
    {
        SampleId = id,
        CaseId = "case-" + id,
        Code = code,
        Beschreibung = "Testbefund " + code,
        MeterStart = 1.0,
        MeterEnd = 1.0,
        ExportedUtc = DateTime.UtcNow,
    };

    private static EmbeddingService CreateEmbedder()
    {
        var http = new HttpClient(new EmbedHandler())
        {
            BaseAddress = new Uri("http://localhost:11434")
        };
        var cfg = new OllamaConfig(
            BaseUri: new Uri("http://localhost:11434"),
            VisionModel: "vision",
            TextModel: "text",
            EmbedModel: "embed",
            RequestTimeout: TimeSpan.FromSeconds(5),
            KeepAlive: "5m",
            NumCtx: 2048);
        return new EmbeddingService(http, cfg);
    }

    private static int ScalarInt(SqliteConnection connection, string sql)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        return Convert.ToInt32(cmd.ExecuteScalar());
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
            // best effort test cleanup
        }
    }

    private sealed class EmbedHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"embeddings":[[0.1,0.2,0.3]]}""")
            };
            return Task.FromResult(response);
        }
    }
}
