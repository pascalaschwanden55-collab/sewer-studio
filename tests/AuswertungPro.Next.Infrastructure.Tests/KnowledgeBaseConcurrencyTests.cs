using System.Net;
using System.Text;
using AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase;
using AuswertungPro.Next.Infrastructure.Ai.Ollama;
using Microsoft.Data.Sqlite;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class KnowledgeBaseConcurrencyTests
{
    [Fact]
    public async Task Retrieval_BleibtWaehrenOffenerRebuildTransaktionLesbar()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "AuswertungPro.Next.Tests",
            $"kb-concurrency-{Guid.NewGuid():N}");
        var dbPath = Path.Combine(root, "KnowledgeBase.db");

        try
        {
            using var writer = new KnowledgeBaseContext(dbPath);
            WriteSample(writer.Connection, transaction: null, "alt", "ALT-100", "Alter Bestand");

            using var reader = new KnowledgeBaseContext(dbPath);
            var retrieval = new RetrievalService(reader, FakeEmbedder());

            using (var rebuild = writer.Connection.BeginTransaction())
            {
                Execute(writer.Connection, rebuild, "DELETE FROM Embeddings; DELETE FROM Samples;");
                WriteSample(writer.Connection, rebuild, "neu-1", "NEU-100", "Neuer Bestand eins");
                WriteSample(writer.Connection, rebuild, "neu-2", "NEU-200", "Neuer Bestand zwei");

                // WAL muss paralleles Lesen erlauben. Ohne den Schutz endet dieser Aufruf
                // mit "database is locked" oder wartet bis zum Test-Timeout.
                var duringRebuild = await retrieval
                    .RetrieveAsync("Bestand", topK: 10)
                    .WaitAsync(TimeSpan.FromSeconds(2));

                Assert.Equal(
                    new[] { "ALT-100" },
                    duringRebuild.Select(x => x.Sample.CaseId).ToArray());
                rebuild.Commit();
            }

            var afterRebuild = await retrieval
                .RetrieveAsync("Bestand", topK: 10)
                .WaitAsync(TimeSpan.FromSeconds(2));

            Assert.Equal(
                new[] { "NEU-100", "NEU-200" },
                afterRebuild.Select(x => x.Sample.CaseId).Order().ToArray());
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    private static void WriteSample(
        SqliteConnection connection,
        SqliteTransaction? transaction,
        string sampleId,
        string caseId,
        string description)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO Samples (
                SampleId, CaseId, VsaCode, Beschreibung, MeterStart, MeterEnd,
                IsStreck, FramePath, ExportedUtc, VersionId, SourceType,
                QualityGateLevel, HumanConfirmed, Corrected, ConfirmedByUser,
                ConfirmedAtUtc)
            VALUES (
                $id, $caseId, 'BAB', $description, 0, 0,
                0, '', $now, 'test-version', 'test',
                'Green', 1, 0, 'test', $now);

            INSERT INTO Embeddings (SampleId, Model, Vector, CreatedAt)
            VALUES ($id, 'test-embedder', $vector, $now);
            """;
        command.Parameters.AddWithValue("$id", sampleId);
        command.Parameters.AddWithValue("$caseId", caseId);
        command.Parameters.AddWithValue("$description", description);
        command.Parameters.AddWithValue("$now", DateTime.UtcNow.ToString("O"));
        command.Parameters.AddWithValue(
            "$vector",
            EmbeddingService.ToBlob([0.1f, 0.2f, 0.3f, 0.4f]));
        command.ExecuteNonQuery();
    }

    private static void Execute(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string sql)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private static EmbeddingService FakeEmbedder()
        => new(
            new HttpClient(new FixedEmbeddingHandler()),
            new OllamaConfig(
                new Uri("http://localhost:11434"),
                "vision",
                "text",
                "test-embedder",
                TimeSpan.FromSeconds(5)));

    private sealed class FixedEmbeddingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"embeddings":[[0.1,0.2,0.3,0.4]]}""",
                    Encoding.UTF8,
                    "application/json")
            });
    }
}
