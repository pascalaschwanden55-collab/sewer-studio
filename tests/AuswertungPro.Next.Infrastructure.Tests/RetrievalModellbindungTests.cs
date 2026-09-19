using System;
using System.Collections.Generic;
using System.IO;
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
/// Auditbefund 17 (18.09.2026): Nach einem Wechsel des Embedding-Modells blieben die alten
/// Suchvektoren verwendbar. Liefern altes und neues Modell gleich lange Vektoren, sehen
/// rechnerisch aehnliche Zahlen aus verschiedenen Bedeutungsraeumen wie passende
/// Vergleichsfaelle aus. Der Dienst ERKANNTE den Unterschied (`HasModelMismatch`),
/// sperrte ihn aber nicht.
///
/// Lieber kein Vergleichswissen als falsches.
/// </summary>
public sealed class RetrievalModellbindungTests : IDisposable
{
    private readonly ICodeCatalogProvider? _vorherigerKatalog;

    public RetrievalModellbindungTests()
    {
        _vorherigerKatalog = VsaCodeResolver.CurrentCatalog;
        VsaCodeResolver.ConfigureCatalog(new MinimalCatalog());
    }

    public void Dispose() => VsaCodeResolver.ConfigureCatalog(_vorherigerKatalog);

    private sealed class FesterEmbeddingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"embeddings":[[0.1,0.2,0.3,0.4]]}""", Encoding.UTF8, "application/json")
            });
    }

    private static EmbeddingService Embedder(string modell)
        => new(new HttpClient(new FesterEmbeddingHandler()),
            new OllamaConfig(new Uri("http://localhost:11434"), "v", "t", modell, TimeSpan.FromSeconds(5)));

    private static TrainingSample Sample(string id, string caseId) => new()
    {
        SampleId = id,
        CaseId = caseId,
        Code = "BAB",
        Beschreibung = "Riss in Laengsrichtung sichtbar",
        FramePath = typeof(RetrievalModellbindungTests).Assembly.Location,
        MeterStart = 1.0,
        MeterEnd = 1.0,
        InspectionDate = new DateTime(2024, 6, 1),
        TrainingEligible = true,
        QualityGateLevel = "Green",
        Status = TrainingSampleStatus.Approved,
        HumanConfirmed = true,
        Corrected = false,
        ConfirmedByUser = Environment.UserName,
        ConfirmedAtUtc = new DateTime(2026, 7, 25, 8, 0, 0, DateTimeKind.Utc),
        SourceType = SourceTypeNames.ManualCoding,
        MatchLevel = MatchLevelNames.ReviewApproved,
        BboxXCenter = 0.5, BboxYCenter = 0.5, BboxWidth = 0.2, BboxHeight = 0.2,
        SamMaskRle = "0,4050,1,3949", SamMaskImageWidth = 100, SamMaskImageHeight = 80
    };

    [Fact]
    public async Task NachModellwechsel_LiefertDasRetrievalKeinenFremdenVergleichsfall()
    {
        var root = Path.Combine(Path.GetTempPath(), "kb-modell", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var dbPath = Path.Combine(root, "kb.db");
        try
        {
            using (var db = new KnowledgeBaseContext(dbPath))
            {
                var mgr = new KnowledgeBaseManager(db, Embedder("nomic-embed-text"));
                Assert.True(await mgr.IndexSampleAsync(Sample("s-1", "111-222"), CancellationToken.None));
            }
            SqliteConnection.ClearAllPools();

            // Gegenprobe: mit demselben Modell bleibt der Fall verfuegbar.
            using (var db = new KnowledgeBaseContext(dbPath))
            {
                var gleich = new RetrievalService(db, Embedder("nomic-embed-text"));
                var treffer = await gleich.RetrieveAsync("Riss", topK: 10, CancellationToken.None);
                Assert.Single(treffer);
            }
            SqliteConnection.ClearAllPools();

            // Nach dem Wechsel darf derselbe Vektor NICHT mehr als Vergleich dienen.
            using (var db = new KnowledgeBaseContext(dbPath))
            {
                var anderes = new RetrievalService(db, Embedder("mxbai-embed-large"));
                var treffer = await anderes.RetrieveAsync("Riss", topK: 10, CancellationToken.None);
                Assert.Empty(treffer);
            }
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            try { Directory.Delete(root, recursive: true); } catch { }
        }
    }

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
