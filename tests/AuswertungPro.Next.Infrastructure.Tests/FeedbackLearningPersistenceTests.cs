using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.KnowledgeBase;
using AuswertungPro.Next.Application.Ai.QualityGate;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase;
using AuswertungPro.Next.Infrastructure.Ai.QualityGate;
using AuswertungPro.Next.Infrastructure.Ai.SelfImproving;
using Microsoft.Data.Sqlite;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class FeedbackLearningPersistenceTests
{
    [Fact]
    public async Task RelearnThreshold_SurvivesServiceRecreationAndRestart()
    {
        var root = Path.Combine(Path.GetTempPath(), "feedback-persistent", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var dbPath = Path.Combine(root, "knowledge.db");

        try
        {
            for (var i = 0; i < 12; i++)
                await AddFeedbackWithFreshServiceAsync(dbPath);

            using (var db = new KnowledgeBaseContext(dbPath))
            {
                var state = new ValidationLogger(db.Connection).GetLearningState();
                Assert.Equal(0, state.LastCompletedValidationCount);
                Assert.Equal("Idle", state.Status);
            }

            // Simulierter App-Neustart: neue Connections und neue Serviceinstanzen.
            SqliteConnection.ClearAllPools();
            for (var i = 0; i < 13; i++)
                await AddFeedbackWithFreshServiceAsync(dbPath);

            using (var db = new KnowledgeBaseContext(dbPath))
            {
                var logger = new ValidationLogger(db.Connection);
                var state = logger.GetLearningState();
                var snapshot = new WeightLearningService(db.Connection).LoadSnapshot();

                Assert.Equal(25, logger.GetTotalCount());
                Assert.Equal(25, state.LastCompletedValidationCount);
                Assert.Equal("Idle", state.Status);
                Assert.Null(state.LastError);
                Assert.True(snapshot.HasLearnedWeights);
                Assert.Equal(snapshot.Version, state.ActiveWeightVersion);
                Assert.Equal(snapshot.Version, QualityGateService.ActiveProcessWeightVersion);
            }

            // Keine Doppelverarbeitung: ein weiteres Event reicht nicht fuer den naechsten Batch.
            await AddFeedbackWithFreshServiceAsync(dbPath);
            using (var db = new KnowledgeBaseContext(dbPath))
            {
                var state = new ValidationLogger(db.Connection).GetLearningState();
                Assert.Equal(25, state.LastCompletedValidationCount);
            }
        }
        finally
        {
            QualityGateService.ConfigureProcessWeights(
                Array.Empty<CategoryWeights>(),
                QualityGateWeightSnapshot.DefaultVersion);
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task CodingFeedbackRecorder_PassesRealConfirmedSampleToIndexer()
    {
        var root = Path.Combine(Path.GetTempPath(), "feedback-indexer", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var dbPath = Path.Combine(root, "knowledge.db");
        var framePath = Path.Combine(root, "frame.png");
        await File.WriteAllBytesAsync(framePath, new byte[] { 1, 2, 3 });

        try
        {
            var indexer = new RecordingIndexer();
            var recorder = new CodingFeedbackRecorder(dbPath, indexer);
            var codingEvent = new CodingEvent
            {
                EventId = Guid.NewGuid(),
                MeterAtCapture = 4.2,
                VideoTimestamp = TimeSpan.FromSeconds(12),
                Entry = new ProtocolEntry
                {
                    Code = "BAB",
                    Beschreibung = "Riss",
                    MeterStart = 4.2,
                    FotoPaths = { framePath }
                },
                AiContext = new CodingEventAiContext
                {
                    SuggestedCode = "BAB",
                    Confidence = 0.95,
                    QualityGateLevel = "Green",
                    KbCodeAgreement = true,
                    EpistemicUncertainty = 0.10,
                    Decision = CodingUserDecision.Accepted
                }
            };

            await recorder.RecordDecisionAsync(codingEvent, "865-864");

            Assert.NotNull(indexer.Indexed);
            Assert.Equal("865-864", indexer.Indexed!.CaseId);
            Assert.Equal("BAB", indexer.Indexed.Code);
            Assert.Equal(framePath, indexer.Indexed.FramePath);
            Assert.Equal(TrainingSampleStatus.Approved, indexer.Indexed.Status);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    private static async Task AddFeedbackWithFreshServiceAsync(string dbPath)
    {
        using var db = new KnowledgeBaseContext(dbPath);
        var service = new FeedbackIngestionService(
            new ValidationLogger(db.Connection),
            new WeightLearningService(db.Connection));

        await service.ProcessFeedbackAsync(CreateEntry(), finalCode: "BAB", accepted: true);
    }

    private static MappedProtocolEntry CreateEntry()
    {
        var evidence = new EvidenceVector(
            YoloConf: 0.90,
            DinoConf: 0.85,
            PlausibilityScore: 0.90,
            DamageCategory: "BAB");
        var detection = new RawVideoDetection(
            "Riss",
            1.0,
            1.0,
            "high",
            VsaCodeHint: "BAB",
            Evidence: evidence);

        return new MappedProtocolEntry(
            detection,
            "BAB",
            0.90,
            "test",
            Array.Empty<string>());
    }

    private sealed class RecordingIndexer : ITrainingSampleIndexer
    {
        public TrainingSample? Indexed { get; private set; }

        public Task<bool> IndexSampleAsync(TrainingSample sample, CancellationToken ct = default)
        {
            Indexed = sample;
            return Task.FromResult(true);
        }
    }
}
