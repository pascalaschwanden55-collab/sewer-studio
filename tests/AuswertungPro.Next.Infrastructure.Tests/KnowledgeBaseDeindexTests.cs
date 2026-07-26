using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Application.Protocol;
using AuswertungPro.Next.Infrastructure.Ai;
using AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase;
using AuswertungPro.Next.Infrastructure.Ai.Ollama;
using Microsoft.Data.Sqlite;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>
/// Prueft, dass DeindexSample ein Sample und sein Embedding vollstaendig aus der
/// Wissensdatenbank entfernt (nicht nur ein Status-Flag setzt).
/// </summary>
public sealed class KnowledgeBaseDeindexTests : IDisposable
{
    // ── Katalog-Trick (wie KnowledgeBaseManagerEligibilityTests) ─────────────
    private readonly ICodeCatalogProvider? _previousCatalog;

    public KnowledgeBaseDeindexTests()
    {
        _previousCatalog = VsaCodeResolver.CurrentCatalog;
        VsaCodeResolver.ConfigureCatalog(new MinimalCatalog());
    }

    public void Dispose() => VsaCodeResolver.ConfigureCatalog(_previousCatalog);

    // ── Fake-Embedder (wie KnowledgeBaseManagerEvalGuardTests) ───────────────
    private sealed class FixedEmbeddingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"embeddings":[[0.1,0.2,0.3,0.4]]}""",
                    Encoding.UTF8,
                    "application/json")
            });
    }

    private static EmbeddingService FakeEmbedder()
        => new(new HttpClient(new FixedEmbeddingHandler()),
            new OllamaConfig(
                new Uri("http://localhost:11434"), "v", "t", "nomic-embed-text",
                TimeSpan.FromSeconds(5)));

    // ── Hilfsmethode: ein indexierbares Sample erzeugen ──────────────────────
    private static TrainingSample EligibleSample(string id) => new()
    {
        SampleId       = id,
        CaseId         = "H-01",
        Code           = "BAB",
        Beschreibung   = "Laengsriss im Scheitel sichtbar",
        FramePath      = typeof(KnowledgeBaseDeindexTests).Assembly.Location,
        MeterStart     = 5.0,
        MeterEnd       = 5.0,
        InspectionDate = new DateTime(2024, 6, 1),
        TrainingEligible = true,
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

    // ── Test ──────────────────────────────────────────────────────────────────
    [Fact]
    public async Task DeindexSample_EntferntSampleUndEmbedding()
    {
        var root = Path.Combine(Path.GetTempPath(), "kb-deindex", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            using var db = new KnowledgeBaseContext(Path.Combine(root, "kb.db"));
            var mgr = new KnowledgeBaseManager(db, FakeEmbedder());

            var sample = EligibleSample("s-deindex-01");

            // Vorbedingung: Sample ist indexierbar und wird tatsaechlich indexiert.
            Assert.True(KnowledgeBaseManager.IsIndexWorthy(sample),
                "Testaufbau-Fehler: Sample ist nicht indexwuerdig.");

            var indexed = await mgr.IndexSampleAsync(sample, CancellationToken.None);
            Assert.True(indexed, "Testaufbau-Fehler: IndexSampleAsync gab false zurueck.");
            Assert.True(mgr.IsIndexed(sample.SampleId), "Sample sollte nach Indexierung als IsIndexed gelten.");

            // Aktion: Deindex ausfuehren.
            mgr.DeindexSample(sample.SampleId);

            // Erwartung: Sample ist nicht mehr als indexiert bekannt.
            Assert.False(mgr.IsIndexed(sample.SampleId),
                "Nach DeindexSample darf IsIndexed nicht mehr true sein.");
            Assert.Equal(0, CountRows(db, "Samples", sample.SampleId));
            Assert.Equal(0, CountRows(db, "Embeddings", sample.SampleId));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task DeindexSample_RolltSampleDeleteZurueckWennEmbeddingDeleteFehlschlaegt()
    {
        var root = Path.Combine(Path.GetTempPath(), "kb-deindex-tx", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            using var db = new KnowledgeBaseContext(Path.Combine(root, "kb.db"));
            var mgr = new KnowledgeBaseManager(db, FakeEmbedder());

            var sample = EligibleSample("s-deindex-tx-01");
            var indexed = await mgr.IndexSampleAsync(sample, CancellationToken.None);
            Assert.True(indexed, "Testaufbau-Fehler: IndexSampleAsync gab false zurueck.");

            BlockEmbeddingDelete(db);

            Assert.Throws<SqliteException>(() => mgr.DeindexSample(sample.SampleId));

            Assert.Equal(1, CountRows(db, "Samples", sample.SampleId));
            Assert.Equal(1, CountRows(db, "Embeddings", sample.SampleId));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    private static int CountRows(KnowledgeBaseContext db, string table, string sampleId)
    {
        if (table is not ("Samples" or "Embeddings"))
            throw new ArgumentOutOfRangeException(nameof(table), table, "Unerwartete Test-Tabelle.");

        using var cmd = db.Connection.CreateCommand();
        cmd.CommandText = $"SELECT COUNT(*) FROM {table} WHERE SampleId = $id";
        cmd.Parameters.AddWithValue("$id", sampleId);
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    private static void BlockEmbeddingDelete(KnowledgeBaseContext db)
    {
        using var cmd = db.Connection.CreateCommand();
        cmd.CommandText = """
            CREATE TRIGGER block_embedding_delete
            BEFORE DELETE ON Embeddings
            BEGIN
                SELECT RAISE(ABORT, 'blocked embedding delete');
            END;
            """;
        cmd.ExecuteNonQuery();
    }

    // ── Minimaler Inline-Katalog (wie KnowledgeBaseManagerEligibilityTests) ──
    private sealed class MinimalCatalog : ICodeCatalogProvider
    {
        private static readonly CodeDefinition[] Codes =
        {
            new() { Code = "BAB", Title = "Risse", IsSelectable = true }
        };

        public IReadOnlyList<CodeDefinition> GetAll() => Codes;

        public bool TryGet(string code, out CodeDefinition def)
        {
            def = Codes.FirstOrDefault(c =>
                      string.Equals(c.Code, code, StringComparison.OrdinalIgnoreCase))
                  ?? new CodeDefinition();
            return !string.IsNullOrWhiteSpace(def.Code);
        }

        public void Save(IReadOnlyList<CodeDefinition> codes)
            => throw new InvalidOperationException("Test-Katalog ist schreibgeschuetzt.");

        public IReadOnlyList<string> AllowedCodes()
            => Codes.Select(c => c.Code).ToList();

        public IReadOnlyList<string> Validate(IReadOnlyList<CodeDefinition>? codes = null)
            => Array.Empty<string>();
    }
}
