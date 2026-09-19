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
/// Auditbefund 11 (18.09.2026): Drei Stellen erzeugten ein Retrieval, aber nur EINE
/// reichte die Sperrliste der reservierten Pruefhaltungen weiter. Die Vollprotokoll-
/// Erstellung und das Selbsttraining bauten ihre eigene Suche ohne Schutz — reservierte
/// Testfaelle konnten dort als Vergleichswissen einfliessen.
///
/// Drei Umgehungen bei drei Stellen heissen: Die Regel selbst war das Problem, nicht die
/// einzelne Zeile. Deshalb gibt es genau eine geschuetzte Erzeugung und einen Waechter.
/// </summary>
public sealed class GuardedRetrievalFactoryTests : IDisposable
{
    private readonly ICodeCatalogProvider? _vorherigerKatalog;

    public GuardedRetrievalFactoryTests()
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

    private static EmbeddingService Embedder()
        => new(new HttpClient(new FesterEmbeddingHandler()),
            new OllamaConfig(new Uri("http://localhost:11434"), "v", "t", "nomic-embed-text", TimeSpan.FromSeconds(5)));

    private static TrainingSample Sample(string id, string caseId) => new()
    {
        SampleId = id,
        CaseId = caseId,
        Code = "BAB",
        Beschreibung = "Riss in Laengsrichtung sichtbar",
        FramePath = typeof(GuardedRetrievalFactoryTests).Assembly.Location,
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
    public async Task Fabrik_sperrt_reservierte_Pruefhaltungen_auch_ohne_uebergebene_Liste()
    {
        var root = Path.Combine(Path.GetTempPath(), "kb-guard", Guid.NewGuid().ToString("N"));
        var evalRoot = Path.Combine(root, "eval_set");
        Directory.CreateDirectory(evalRoot);
        var dbPath = Path.Combine(root, "kb.db");
        const string reserviert = "287425-81162";
        const string frei = "999999-888888";
        try
        {
            File.WriteAllText(
                Path.Combine(evalRoot, "_candidates.json"),
                $$"""[{"haltung_key":"{{reserviert}}"}]""");

            using (var db = new KnowledgeBaseContext(dbPath))
            {
                var mgr = new KnowledgeBaseManager(db, Embedder());
                Assert.True(await mgr.IndexSampleAsync(Sample("s-res", reserviert), CancellationToken.None));
                Assert.True(await mgr.IndexSampleAsync(Sample("s-frei", frei), CancellationToken.None));
            }
            SqliteConnection.ClearAllPools();

            using (var db = new KnowledgeBaseContext(dbPath))
            {
                var retrieval = GuardedRetrievalFactory.Create(db, Embedder(), evalRoot);
                var treffer = await retrieval.RetrieveAsync("Riss", topK: 10, CancellationToken.None);

                Assert.Equal(new[] { frei }, treffer.Select(t => t.Sample.CaseId).ToArray());
            }
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            try { Directory.Delete(root, recursive: true); } catch { }
        }
    }

    [Fact]
    public void Kein_Produktivcode_erzeugt_ein_Retrieval_am_Schutz_vorbei()
    {
        var quellen = Directory
            .EnumerateFiles(Path.Combine(TestRepoPaths.RepoRoot(), "src"), "*.cs", SearchOption.AllDirectories)
            .Where(p => !p.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Where(p => !p.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Where(p => !p.EndsWith("GuardedRetrievalFactory.cs", StringComparison.OrdinalIgnoreCase))
            .Where(p => File.ReadAllText(p).Contains("new RetrievalService(", StringComparison.Ordinal))
            .Select(p => Path.GetFileName(p))
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.True(
            quellen.Length == 0,
            "Retrieval nur ueber GuardedRetrievalFactory erzeugen, sonst fehlt die "
            + "Sperrliste der reservierten Pruefhaltungen:\n" + string.Join("\n", quellen));
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
