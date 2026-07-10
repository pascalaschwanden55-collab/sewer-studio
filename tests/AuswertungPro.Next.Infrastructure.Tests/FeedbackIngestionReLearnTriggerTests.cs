using System;
using System.IO;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.QualityGate;
using AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase;
using AuswertungPro.Next.Infrastructure.Ai.QualityGate;
using AuswertungPro.Next.Infrastructure.Ai.SelfImproving;
using Microsoft.Data.Sqlite;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>
/// Audit P0-1: Der ReLearn-Trigger muss ueber SEPARATE FeedbackIngestionService-Instanzen
/// hinweg feuern. Der CodingFeedbackRecorder legt pro Benutzerentscheidung einen neuen
/// Service an — mit dem frueheren Instanz-Zaehler (_feedbackCount startete je Instanz bei 0)
/// wurde die Schwelle nie erreicht. Jetzt zaehlt der persistente ValidationLog.
/// </summary>
public sealed class FeedbackIngestionReLearnTriggerTests
{
    private static MappedProtocolEntry Entry(string caseId, double llmConf) =>
        new(new RawVideoDetection(caseId, 12.0, 12.0, "mid",
                Evidence: new EvidenceVector(YoloConf: 0.6, LlmCodeConf: llmConf)),
            SuggestedCode: "BAB", Confidence: llmConf, Reason: null, Warnings: Array.Empty<string>());

    [Fact]
    public async Task ReLearn_FeuertUeberSeparateInstanzen_DankPersistentemZaehler()
    {
        var root = Path.Combine(Path.GetTempPath(), "fb-trigger", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            using var db = new KnowledgeBaseContext(Path.Combine(root, "kb.db"));

            // Fuenf Feedbacks derselben Kategorie ("BAB"), jedes ueber eine NEUE Service-Instanz —
            // exakt das Muster von CodingFeedbackRecorder. ReLearnInterval=5, MinSamples=3.
            for (int i = 0; i < 5; i++)
            {
                var learner = new WeightLearningService(db.Connection) { MinSamples = 3 };
                var svc = new FeedbackIngestionService(
                    new ValidationLogger(db.Connection), learner) { ReLearnInterval = 5 };
                // Gemischte Korrektheit, damit das Lernen echte 0/1-Labels sieht.
                await svc.ProcessFeedbackAsync(Entry("865-864", 0.8), finalCode: "BAB", accepted: i % 2 == 0);
            }

            // Der Trigger hat beim 5. Feedback gefeuert und Gewichte geschrieben. Mit dem alten
            // Instanz-Zaehler (jede Instanz bei count=1) waere die Tabelle leer geblieben.
            var weights = new WeightLearningService(db.Connection).LoadAllWeights();
            Assert.NotEmpty(weights);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task ReLearn_FeuertNICHT_VorErreichenDerSchwelle()
    {
        var root = Path.Combine(Path.GetTempPath(), "fb-trigger", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            using var db = new KnowledgeBaseContext(Path.Combine(root, "kb.db"));

            // Nur vier Feedbacks bei ReLearnInterval=5 — die Schwelle wird nicht erreicht.
            for (int i = 0; i < 4; i++)
            {
                var learner = new WeightLearningService(db.Connection) { MinSamples = 3 };
                var svc = new FeedbackIngestionService(
                    new ValidationLogger(db.Connection), learner) { ReLearnInterval = 5 };
                await svc.ProcessFeedbackAsync(Entry("865-864", 0.8), finalCode: "BAB", accepted: i % 2 == 0);
            }

            var weights = new WeightLearningService(db.Connection).LoadAllWeights();
            Assert.Empty(weights);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }
}
