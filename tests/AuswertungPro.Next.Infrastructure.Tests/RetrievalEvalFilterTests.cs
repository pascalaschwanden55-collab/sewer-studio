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
/// Audit Fix #6a: Der Eval-Kontaminationsschutz darf nicht nur am Schreib-Eingang haengen.
/// RetrievalService muss eine zweite Verteidigungslinie haben: Samples aus reservierten
/// Eval-Haltungen, die (historisch / ueber einen ungeguardeten Pfad) bereits in der KB
/// liegen, duerfen NIE als Few-Shot-Kontext zurueckgegeben werden.
/// </summary>
public sealed class RetrievalEvalFilterTests : IDisposable
{
    private readonly ICodeCatalogProvider? _previousCatalog;

    public RetrievalEvalFilterTests()
    {
        _previousCatalog = VsaCodeResolver.CurrentCatalog;
        VsaCodeResolver.ConfigureCatalog(new MinimalCatalog());
    }

    public void Dispose() => VsaCodeResolver.ConfigureCatalog(_previousCatalog);

    private sealed class FixedEmbeddingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"embeddings":[[0.1,0.2,0.3,0.4]]}""", Encoding.UTF8, "application/json")
            });
    }

    private static EmbeddingService FakeEmbedder()
        => new(new HttpClient(new FixedEmbeddingHandler()),
            new OllamaConfig(new Uri("http://localhost:11434"), "v", "t", "nomic-embed-text", TimeSpan.FromSeconds(5)));

    private static TrainingSample Sample(string id, string caseId) => new()
    {
        SampleId = id,
        CaseId = caseId,
        Code = "BAB",
        Beschreibung = "Riss in Laengsrichtung sichtbar",
        FramePath = typeof(RetrievalEvalFilterTests).Assembly.Location,
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
        // IsIndexWorthy verlangt seit der Gold-Wahrheits-Haertung Box + SAM-Maske.
        BboxXCenter = 0.5, BboxYCenter = 0.5, BboxWidth = 0.2, BboxHeight = 0.2,
        SamMaskRle = "0,4050,1,3949", SamMaskImageWidth = 100, SamMaskImageHeight = 80
    };

    [Fact]
    public async Task Retrieve_ExcludesEvalHaltung_ButKeepsCleanSample()
    {
        var root = Path.Combine(Path.GetTempPath(), "kb-retr-eval", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var dbPath = Path.Combine(root, "kb.db");
        const string evalHaltung = "287425-81162";
        const string cleanHaltung = "999999-888888";
        try
        {
            // 1) Beide Samples OHNE Eval-Sperrliste indexieren -> beide landen in der KB.
            using (var db = new KnowledgeBaseContext(dbPath))
            {
                var mgr = new KnowledgeBaseManager(db, FakeEmbedder());
                Assert.True(await mgr.IndexSampleAsync(Sample("s-eval", evalHaltung), CancellationToken.None));
                Assert.True(await mgr.IndexSampleAsync(Sample("s-clean", cleanHaltung), CancellationToken.None));
            }
            SqliteConnection.ClearAllPools();

            // 2) Kontrolle: OHNE Lesefilter liefert das Retrieval das Eval-Sample mit aus.
            using (var db = new KnowledgeBaseContext(dbPath))
            {
                var unfiltered = new RetrievalService(db, FakeEmbedder());
                var all = await unfiltered.RetrieveAsync("Riss", topK: 10, CancellationToken.None);
                Assert.Contains(all, r => r.Sample.CaseId == evalHaltung);
                Assert.Contains(all, r => r.Sample.CaseId == cleanHaltung);
            }
            SqliteConnection.ClearAllPools();

            // 3) MIT Lesefilter (Eval-Haltung gesperrt): Eval-Sample fehlt, sauberes bleibt.
            using (var db = new KnowledgeBaseContext(dbPath))
            {
                var evalKeys = new HashSet<string>(new[] { evalHaltung }, StringComparer.OrdinalIgnoreCase);
                var filtered = new RetrievalService(db, FakeEmbedder(), evalKeys);
                var results = await filtered.RetrieveAsync("Riss", topK: 10, CancellationToken.None);

                Assert.Equal(new[] { cleanHaltung }, results.Select(r => r.Sample.CaseId).ToArray());
            }
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
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
