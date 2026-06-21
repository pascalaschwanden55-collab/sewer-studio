using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Application.Protocol;
using AuswertungPro.Next.Infrastructure.Ai;
using AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase;
using AuswertungPro.Next.Infrastructure.Ai.Ollama;
using Microsoft.Data.Sqlite;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>
/// Prueft, dass Gold-Fund-Metadaten (HumanConfirmed/Corrected/ConfirmedByUser/ConfirmedAtUtc)
/// beim Indexieren in der SQLite-KB persistiert und beim Lesen korrekt zurueckgegeben werden.
/// Audit Fix #3.
/// </summary>
public sealed class KnowledgeBaseGoldMetadataTests : IDisposable
{
    private readonly ICodeCatalogProvider? _previousCatalog;

    public KnowledgeBaseGoldMetadataTests()
    {
        _previousCatalog = VsaCodeResolver.CurrentCatalog;
        VsaCodeResolver.ConfigureCatalog(new MinimalCatalog());
    }

    public void Dispose() => VsaCodeResolver.ConfigureCatalog(_previousCatalog);

    [Fact]
    public async Task GoldMetadata_WirdPersistiertUndZurueckgelesen()
    {
        var root = Path.Combine(Path.GetTempPath(), "kb-gold-meta", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var confirmedAt = new DateTime(2026, 6, 20, 10, 0, 0, DateTimeKind.Utc);

            using var db = new KnowledgeBaseContext(Path.Combine(root, "kb.db"));
            var mgr = new KnowledgeBaseManager(db, FakeEmbedder());

            var sample = new TrainingSample
            {
                SampleId       = "gold1",
                CaseId         = "H-01",
                Code           = "BAB",
                Beschreibung   = "Laengsriss deutlich sichtbar an Sohle",
                MeterStart     = 10.0,
                MeterEnd       = 10.0,
                QualityGateLevel = "Green",
                HumanConfirmed = true,
                Corrected      = true,
                ConfirmedByUser = "pascal",
                ConfirmedAtUtc = confirmedAt
            };

            Assert.True(KnowledgeBaseManager.IsIndexWorthy(sample));
            Assert.True(await mgr.IndexSampleAsync(sample, CancellationToken.None));

            // Direkt aus der DB lesen (wie KnowledgeBaseQualityGateTests)
            using var cmd = db.Connection.CreateCommand();
            cmd.CommandText = "SELECT HumanConfirmed, Corrected, ConfirmedByUser, ConfirmedAtUtc FROM Samples WHERE SampleId = 'gold1'";
            using var reader = cmd.ExecuteReader();
            Assert.True(reader.Read(), "Sample nicht in DB gefunden");

            // HumanConfirmed (INTEGER: 1 = true)
            Assert.False(reader.IsDBNull(0), "HumanConfirmed ist NULL");
            Assert.Equal(1L, reader.GetInt64(0));

            // Corrected (INTEGER: 1 = true)
            Assert.False(reader.IsDBNull(1), "Corrected ist NULL");
            Assert.Equal(1L, reader.GetInt64(1));

            // ConfirmedByUser (TEXT)
            Assert.False(reader.IsDBNull(2), "ConfirmedByUser ist NULL");
            Assert.Equal("pascal", reader.GetString(2));

            // ConfirmedAtUtc (TEXT, ISO 8601 "O" Format)
            Assert.False(reader.IsDBNull(3), "ConfirmedAtUtc ist NULL");
            var storedAt = DateTime.Parse(reader.GetString(3), null, System.Globalization.DateTimeStyles.RoundtripKind);
            Assert.Equal(confirmedAt, storedAt);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task GoldMetadata_Null_WirdAlsNullPersistiert()
    {
        var root = Path.Combine(Path.GetTempPath(), "kb-gold-meta", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            using var db = new KnowledgeBaseContext(Path.Combine(root, "kb.db"));
            var mgr = new KnowledgeBaseManager(db, FakeEmbedder());

            var sample = new TrainingSample
            {
                SampleId     = "gold2",
                CaseId       = "H-02",
                Code         = "BAB",
                Beschreibung = "Laengsriss ohne Bestaetigung",
                MeterStart   = 5.0,
                MeterEnd     = 5.0,
                QualityGateLevel = "Yellow",
                // Gold-Felder NICHT gesetzt (null)
                HumanConfirmed  = null,
                Corrected       = null,
                ConfirmedByUser = null,
                ConfirmedAtUtc  = null
            };

            Assert.True(await mgr.IndexSampleAsync(sample, CancellationToken.None));

            using var cmd = db.Connection.CreateCommand();
            cmd.CommandText = "SELECT HumanConfirmed, Corrected, ConfirmedByUser, ConfirmedAtUtc FROM Samples WHERE SampleId = 'gold2'";
            using var reader = cmd.ExecuteReader();
            Assert.True(reader.Read(), "Sample nicht in DB gefunden");

            Assert.True(reader.IsDBNull(0), "HumanConfirmed sollte NULL sein");
            Assert.True(reader.IsDBNull(1), "Corrected sollte NULL sein");
            Assert.True(reader.IsDBNull(2), "ConfirmedByUser sollte NULL sein");
            Assert.True(reader.IsDBNull(3), "ConfirmedAtUtc sollte NULL sein");
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    // ── Hilfsmethoden ────────────────────────────────────────────────────

    private static EmbeddingService FakeEmbedder()
        => new(new HttpClient(new FixedEmbeddingHandler()),
            new OllamaConfig(new Uri("http://localhost:11434"), "v", "t", "nomic-embed-text", TimeSpan.FromSeconds(5)));

    private sealed class FixedEmbeddingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"embeddings":[[0.1,0.2,0.3,0.4]]}""", Encoding.UTF8, "application/json")
            });
    }

    private sealed class MinimalCatalog : ICodeCatalogProvider
    {
        private static readonly CodeDefinition[] Codes = { new() { Code = "BAB", Title = "Risse", IsSelectable = true } };
        public IReadOnlyList<CodeDefinition> GetAll() => Codes;
        public bool TryGet(string code, out CodeDefinition def)
        {
            def = Codes.FirstOrDefault(c => string.Equals(c.Code, code, StringComparison.OrdinalIgnoreCase)) ?? new CodeDefinition();
            return !string.IsNullOrWhiteSpace(def.Code);
        }
        public void Save(IReadOnlyList<CodeDefinition> codes) => throw new InvalidOperationException();
        public IReadOnlyList<string> AllowedCodes() => Codes.Select(c => c.Code).ToList();
        public IReadOnlyList<string> Validate(IReadOnlyList<CodeDefinition>? codes = null) => Array.Empty<string>();
    }
}
