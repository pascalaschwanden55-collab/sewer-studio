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
using AuswertungPro.Next.UI.Services;
using Microsoft.Data.Sqlite;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Verdrahtungstest fuer den Pruefplatz-KB-Indexer (Befund: Deindex war ein No-op-Lambda).
/// Der von der Factory gebaute Indexer muss ein Sample WIRKLICH aus der KnowledgeBase.db
/// entfernen (Samples + Embeddings) — gegen eine temp-KB via SEWERSTUDIO_KNOWLEDGE_ROOT.
/// </summary>
[Collection("EnvironmentVars")]
public sealed class TrainingStudioKbDeindexWiringTests : IDisposable
{
    private readonly ICodeCatalogProvider? _previousCatalog;

    public TrainingStudioKbDeindexWiringTests()
    {
        _previousCatalog = VsaCodeResolver.CurrentCatalog;
        VsaCodeResolver.ConfigureCatalog(new MinimalCatalog());
    }

    public void Dispose() => VsaCodeResolver.ConfigureCatalog(_previousCatalog);

    [Fact]
    public async Task Factory_Indexer_Deindex_entfernt_Sample_wirklich_aus_der_KB()
    {
        var root = Path.Combine(Path.GetTempPath(), "wb-deindex-wiring", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var previousRoot = Environment.GetEnvironmentVariable(KnowledgeBasePaths.EnvironmentVariableName);
        Environment.SetEnvironmentVariable(KnowledgeBasePaths.EnvironmentVariableName, root);
        KnowledgeBasePaths.InvalidateCache();
        try
        {
            var framePath = Path.Combine(root, "wb_deindex.jpg");
            await File.WriteAllBytesAsync(framePath, [1, 2, 3, 4]);
            var sample = new TrainingSample
            {
                SampleId = "wb_deindex",
                CaseId = "H-01",
                Code = "BAB",
                Beschreibung = "Bestaetigter Laengsriss im Scheitel sichtbar",
                FramePath = framePath,
                Status = TrainingSampleStatus.Approved,
                SourceType = SourceTypeNames.ManualCoding,
                MatchLevel = MatchLevelNames.ReviewApproved,
                HumanConfirmed = true,
                Corrected = false,
                ConfirmedByUser = Environment.UserName,
                ConfirmedAtUtc = new DateTime(2026, 7, 25, 8, 0, 0, DateTimeKind.Utc),
                BboxXCenter = 0.5, BboxYCenter = 0.5, BboxWidth = 0.2, BboxHeight = 0.2,
                SamMaskRle = "0,44,1,55", SamMaskImageWidth = 10, SamMaskImageHeight = 10
            };

            // Vorbereitung: Sample ueber den echten Manager in der temp-KB indexieren.
            using (var db = new KnowledgeBaseContext())
            {
                var mgr = new KnowledgeBaseManager(db, FakeEmbedder());
                Assert.True(await mgr.IndexSampleAsync(sample, CancellationToken.None));
                Assert.True(mgr.IsIndexed(sample.SampleId));
            }

            // Der von der Factory gebaute Indexer darf Deindex NICHT still ignorieren.
            var indexer = TrainingStudioWindowDependencyFactory.CreateKbIndexer(services: null);
            indexer.Deindex(sample.SampleId);

            using (var db = new KnowledgeBaseContext())
            {
                var mgr = new KnowledgeBaseManager(db, FakeEmbedder());
                Assert.False(mgr.IsIndexed(sample.SampleId));
            }
        }
        finally
        {
            Environment.SetEnvironmentVariable(KnowledgeBasePaths.EnvironmentVariableName, previousRoot);
            KnowledgeBasePaths.InvalidateCache();
            SqliteConnection.ClearAllPools();
            try { Directory.Delete(root, recursive: true); } catch { /* Aufraeumen best effort */ }
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
