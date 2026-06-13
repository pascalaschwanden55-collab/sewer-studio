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

public sealed class KnowledgeBaseQualityGateTests : IDisposable
{
    private readonly ICodeCatalogProvider? _previousCatalog;

    public KnowledgeBaseQualityGateTests()
    {
        _previousCatalog = VsaCodeResolver.CurrentCatalog;
        VsaCodeResolver.ConfigureCatalog(new MinimalCatalog());
    }

    public void Dispose() => VsaCodeResolver.ConfigureCatalog(_previousCatalog);

    [Fact]
    public async Task UpsertSample_SchreibtQualityGateLevel()
    {
        var root = Path.Combine(Path.GetTempPath(), "kb-qg", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            using var db = new KnowledgeBaseContext(Path.Combine(root, "kb.db"));
            var mgr = new KnowledgeBaseManager(db, FakeEmbedder());

            var sample = new TrainingSample
            {
                SampleId = "qg1", CaseId = "H-01", Code = "BAB",
                Beschreibung = "Laengsriss", MeterStart = 5.0, MeterEnd = 5.0,
                InspectionDate = new DateTime(2024, 6, 1), TrainingEligible = true,
                QualityGateLevel = "Green"
            };
            Assert.True(KnowledgeBaseManager.IsIndexWorthy(sample));
            Assert.True(await mgr.IndexSampleAsync(sample, CancellationToken.None));

            using var cmd = db.Connection.CreateCommand();
            cmd.CommandText = "SELECT QualityGateLevel FROM Samples WHERE SampleId = 'qg1'";
            var stored = cmd.ExecuteScalar() as string;

            Assert.Equal("Green", stored);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

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
