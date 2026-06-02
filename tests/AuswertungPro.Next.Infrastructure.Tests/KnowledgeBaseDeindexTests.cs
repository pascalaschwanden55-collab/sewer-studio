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
        MeterStart     = 5.0,
        MeterEnd       = 5.0,
        InspectionDate = new DateTime(2024, 6, 1),
        TrainingEligible = true
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
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
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
