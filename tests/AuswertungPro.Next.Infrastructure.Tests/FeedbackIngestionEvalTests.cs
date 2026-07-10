using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.KnowledgeBase;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase;
using AuswertungPro.Next.Infrastructure.Ai.QualityGate;
using AuswertungPro.Next.Infrastructure.Ai.SelfImproving;
using Microsoft.Data.Sqlite;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>
/// Audit Fix #6b: Der Feedback-Accept-Pfad darf KEINE eval-blinden, degenerierten Samples
/// (ohne FramePath, CaseId = Befund-Label) in die KB indexieren — sonst umgeht er den
/// Eval-Kontaminationsschutz und verwaessert das Retrieval mit inhaltsleeren Embeddings.
/// </summary>
public sealed class FeedbackIngestionEvalTests
{
    private sealed class RecordingIndexer : ITrainingSampleIndexer
    {
        public TrainingSample? Indexed;
        public Task<bool> IndexSampleAsync(TrainingSample sample, CancellationToken ct = default)
        {
            Indexed = sample;
            return Task.FromResult(true);
        }
    }

    private static MappedProtocolEntry Entry(string findingLabel) =>
        new(new RawVideoDetection(findingLabel, 12.0, 12.0, "mid"),
            SuggestedCode: "BAB", Confidence: 0.7, Reason: null, Warnings: Array.Empty<string>());

    [Fact]
    public async Task Accept_DegenerateSample_NoFrameNoHaltung_IsNotIndexed()
    {
        var root = Path.Combine(Path.GetTempPath(), "fb-ingest", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            using var db = new KnowledgeBaseContext(Path.Combine(root, "kb.db"));
            var indexer = new RecordingIndexer();
            var svc = new FeedbackIngestionService(
                new ValidationLogger(db.Connection),
                new WeightLearningService(db.Connection),
                indexer);

            // FindingLabel "Riss" -> CaseId ist ein Label, kein Schacht-Paar; kein FramePath.
            await svc.ProcessFeedbackAsync(Entry("Riss"), finalCode: "BAB", accepted: true);

            Assert.Null(indexer.Indexed); // degeneriert -> NICHT indexiert
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Accept_SampleWithHaltungIdentity_IsIndexed()
    {
        var root = Path.Combine(Path.GetTempPath(), "fb-ingest", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            using var db = new KnowledgeBaseContext(Path.Combine(root, "kb.db"));
            var indexer = new RecordingIndexer();
            var svc = new FeedbackIngestionService(
                new ValidationLogger(db.Connection),
                new WeightLearningService(db.Connection),
                indexer);

            // CaseId in Schacht-Paar-Form ("865-864") -> hasHaltungId greift, Indexierung erlaubt.
            await svc.ProcessFeedbackAsync(Entry("865-864"), finalCode: "BAB", accepted: true);

            Assert.NotNull(indexer.Indexed);
            Assert.Equal("BAB", indexer.Indexed!.Code);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Reject_DoesNotIndex()
    {
        var root = Path.Combine(Path.GetTempPath(), "fb-ingest", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            using var db = new KnowledgeBaseContext(Path.Combine(root, "kb.db"));
            var indexer = new RecordingIndexer();
            var svc = new FeedbackIngestionService(
                new ValidationLogger(db.Connection),
                new WeightLearningService(db.Connection),
                indexer);

            await svc.ProcessFeedbackAsync(Entry("865-864"), finalCode: "BAB", accepted: false);

            Assert.Null(indexer.Indexed); // Reject indexiert nie
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task CorrectedFeedback_IsGroupedUnderSuggestedCode()
    {
        var root = Path.Combine(Path.GetTempPath(), "fb-ingest", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            using var db = new KnowledgeBaseContext(Path.Combine(root, "kb.db"));
            var svc = new FeedbackIngestionService(
                new ValidationLogger(db.Connection),
                new WeightLearningService(db.Connection));

            await svc.ProcessFeedbackAsync(Entry("865-864"), finalCode: "BAA", accepted: true);

            using var cmd = db.Connection.CreateCommand();
            cmd.CommandText = "SELECT VsaCode, SuggestedCode, FinalCode, WasCorrect FROM ValidationLog";
            using var reader = cmd.ExecuteReader();
            Assert.True(reader.Read());
            Assert.Equal("BAB", reader.GetString(0));
            Assert.Equal("BAB", reader.GetString(1));
            Assert.Equal("BAA", reader.GetString(2));
            Assert.Equal(0, reader.GetInt32(3));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }
}
