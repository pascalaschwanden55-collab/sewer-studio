using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase;
using AuswertungPro.Next.Infrastructure.Ai.Ollama;
using Microsoft.Data.Sqlite;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>
/// S1 (Eval-Kontamination): Frames aus dem eingefrorenen Eval-Set duerfen NICHT in die KB
/// indexiert werden. Deterministisch, ohne Ollama/Katalog: der Eval-Check greift VOR dem
/// Embedding und vor IsIndexWorthy, daher unabhaengig vom (statischen) VsaCodeResolver-Katalog.
/// </summary>
public sealed class KnowledgeBaseManagerEvalGuardTests
{
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

    private static TrainingSample MakeSample(string id, string framePath, string caseId = "H-01") => new()
    {
        SampleId = id,
        CaseId = caseId,
        Code = "BAB",
        Beschreibung = "Riss in Laengsrichtung sichtbar",
        MeterStart = 1.0,
        MeterEnd = 1.0,
        FramePath = framePath
    };

    [Fact]
    public async Task EvalFrame_IsHardBlocked_NotIndexed_AndReasonTraceable()
    {
        var root = Path.Combine(Path.GetTempPath(), "kb-eval-mgr", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            // Eval-Bild + Hash-Satz (wie ihn LoadEvalImageHashes liefern wuerde)
            var evalBytes = new byte[] { 7, 7, 7, 7, 1, 2, 3, 4 };
            var evalHash = Convert.ToHexString(SHA256.HashData(evalBytes)).ToLowerInvariant();
            var evalHashes = new HashSet<string>(new[] { evalHash }, StringComparer.OrdinalIgnoreCase);

            // Kandidat mit identischem Inhalt unter anderem Namen -> kontaminiert.
            var contaminatedFrame = Path.Combine(root, "candidate_eval_copy.png");
            File.WriteAllBytes(contaminatedFrame, evalBytes);
            // Sauberer Kandidat.
            var cleanFrame = Path.Combine(root, "clean_frame.png");
            File.WriteAllBytes(cleanFrame, new byte[] { 9, 9, 9, 9 });

            using var db = new KnowledgeBaseContext(Path.Combine(root, "kb.db"));
            var mgr = new KnowledgeBaseManager(db, FakeEmbedder(), evalHashes);

            var evalSample = MakeSample("s-eval", contaminatedFrame);
            var cleanSample = MakeSample("s-clean", cleanFrame);

            // (3) Blockierungsgrund nachvollziehbar (oeffentliches Predicate):
            Assert.True(mgr.IsEvalContaminated(evalSample));
            Assert.False(mgr.IsEvalContaminated(cleanSample));

            // (1) Eval-Hash vorhanden -> Sample wird nicht indexiert (Block vor Embed/Katalog).
            Assert.False(await mgr.IndexSampleAsync(evalSample, CancellationToken.None));
            Assert.False(mgr.IsIndexed("s-eval"));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task EvalHaltung_IsBlockedByCaseId_EvenWhenFrameHashClean()
    {
        var root = Path.Combine(Path.GetTempPath(), "kb-eval-mgr", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            // Sauberer Frame (KEIN Hash-Treffer), Schutz laeuft rein ueber die Haltungs-Sperrliste.
            var cleanFrame = Path.Combine(root, "clean.png");
            File.WriteAllBytes(cleanFrame, new byte[] { 5, 5, 5, 5 });
            var evalHaltungen = new HashSet<string>(new[] { "287425-81162" }, StringComparer.OrdinalIgnoreCase);

            using var db = new KnowledgeBaseContext(Path.Combine(root, "kb.db"));
            // Kein Hash-Satz, nur Haltungs-Sperrliste.
            var mgr = new KnowledgeBaseManager(db, FakeEmbedder(), evalImageHashes: null, evalHaltungKeys: evalHaltungen);

            // Sample einer fremden Haltung -> nicht blockiert.
            var cleanSample = MakeSample("s-other-haltung", cleanFrame, caseId: "999999-888888");
            Assert.False(mgr.IsEvalContaminated(cleanSample));

            // Sample einer Eval-Haltung (gleiche CaseId) -> blockiert, obwohl der Frame sauber ist.
            var evalSample = MakeSample("s-eval-haltung", cleanFrame, caseId: "287425-81162");
            Assert.True(mgr.IsEvalContaminated(evalSample));
            Assert.False(await mgr.IndexSampleAsync(evalSample, CancellationToken.None));
            Assert.False(mgr.IsIndexed("s-eval-haltung"));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void GuardInactive_WhenNoEvalHashes_NothingBlocked()
    {
        var root = Path.Combine(Path.GetTempPath(), "kb-eval-mgr", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            using var db = new KnowledgeBaseContext(Path.Combine(root, "kb.db"));
            // (2) Ohne Eval-Hash-Satz blockiert der Guard nichts -> der normale Indexpfad bleibt
            // unveraendert (volle Indexierung haengt dann nur an IsIndexWorthy/Embedder wie bisher).
            var mgr = new KnowledgeBaseManager(db, FakeEmbedder(), evalImageHashes: null);
            var frame = Path.Combine(root, "x.png");
            File.WriteAllBytes(frame, new byte[] { 1, 2, 3, 4 });
            Assert.False(mgr.IsEvalContaminated(MakeSample("s1", frame)));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }
}
