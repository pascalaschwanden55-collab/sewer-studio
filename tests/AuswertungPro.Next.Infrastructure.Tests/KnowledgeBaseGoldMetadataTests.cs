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
                FramePath      = typeof(KnowledgeBaseGoldMetadataTests).Assembly.Location,
                MeterStart     = 10.0,
                MeterEnd       = 10.0,
                QualityGateLevel = "Green",
                Status         = TrainingSampleStatus.Approved,
                HumanConfirmed = true,
                Corrected      = true,
                ConfirmedByUser = Environment.UserName,
                ConfirmedAtUtc = confirmedAt,
                SourceType = SourceTypeNames.ManualCoding,
                MatchLevel = MatchLevelNames.ReviewCorrected,
                // IsIndexWorthy verlangt seit der Gold-Wahrheits-Haertung Box + SAM-Maske.
                BboxXCenter = 0.5, BboxYCenter = 0.5, BboxWidth = 0.2, BboxHeight = 0.2,
                SamMaskRle = "0,4050,1,3949", SamMaskImageWidth = 100, SamMaskImageHeight = 80
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
            Assert.Equal(Environment.UserName, reader.GetString(2));

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
    public async Task UnbestaetigtesSample_WirdNichtIndexiert()
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

            Assert.False(KnowledgeBaseManager.IsIndexWorthy(sample));
            Assert.False(await mgr.IndexSampleAsync(sample, CancellationToken.None));

            using var cmd = db.Connection.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM Samples WHERE SampleId = 'gold2'";
            Assert.Equal(0L, (long)cmd.ExecuteScalar()!);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Rebuild_IndexiertNurMenschlichBestaetigtesGold()
    {
        var root = Path.Combine(Path.GetTempPath(), "kb-gold-rebuild", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            using var db = new KnowledgeBaseContext(Path.Combine(root, "kb.db"));
            var mgr = new KnowledgeBaseManager(db, FakeEmbedder());
            var gold = new TrainingSample
            {
                SampleId = "gold",
                CaseId = "H-01",
                Code = "BAB",
                Beschreibung = "Bestaetigter Laengsriss im Scheitel sichtbar",
                FramePath = typeof(KnowledgeBaseGoldMetadataTests).Assembly.Location,
                Status = TrainingSampleStatus.Approved,
                HumanConfirmed = true,
                Corrected = false,
                ConfirmedByUser = Environment.UserName,
                ConfirmedAtUtc = new DateTime(2026, 7, 25, 8, 0, 0, DateTimeKind.Utc),
                SourceType = SourceTypeNames.ManualCoding,
                MatchLevel = MatchLevelNames.ReviewApproved,
                // IsIndexWorthy verlangt seit der Gold-Wahrheits-Haertung Box + SAM-Maske.
                BboxXCenter = 0.5, BboxYCenter = 0.5, BboxWidth = 0.2, BboxHeight = 0.2,
                SamMaskRle = "0,4050,1,3949", SamMaskImageWidth = 100, SamMaskImageHeight = 80
            };
            var unconfirmed = new TrainingSample
            {
                SampleId = "new",
                CaseId = "H-02",
                Code = "BAB",
                Beschreibung = "Unbestaetigter Laengsriss im Scheitel sichtbar",
                Status = TrainingSampleStatus.New,
                HumanConfirmed = null
            };

            var indexed = await mgr.RebuildAsync([gold, unconfirmed]);

            Assert.Equal(1, indexed);
            Assert.True(mgr.IsIndexed("gold"));
            Assert.False(mgr.IsIndexed("new"));
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
