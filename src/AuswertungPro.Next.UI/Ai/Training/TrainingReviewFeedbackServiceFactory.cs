using System;
using System.Net.Http;
using AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase;
using AuswertungPro.Next.Infrastructure.Ai.Ollama;
using AuswertungPro.Next.UI.Services;
using InfraSelfImproving = AuswertungPro.Next.Infrastructure.Ai.SelfImproving;

namespace AuswertungPro.Next.UI.Ai.Training;

public static class TrainingReviewFeedbackServiceFactory
{
    public static InfraSelfImproving.FeedbackIngestionService Create(
        KnowledgeBaseContext db,
        AppSettings? settings)
    {
        ArgumentNullException.ThrowIfNull(db);

        var logger = new AuswertungPro.Next.Infrastructure.Ai.QualityGate.ValidationLogger(db.Connection);
        var weights = new AuswertungPro.Next.Infrastructure.Ai.QualityGate.WeightLearningService(db.Connection);

        KnowledgeBaseManager? kbManager = null;
        try
        {
            var cfg = new AppSettingsAiSettingsProvider()
                .Load()
                .ToOllamaConfig();
            var http = new HttpClient { Timeout = cfg.RequestTimeout };
            var embedder = new EmbeddingService(http, cfg);
            var evalSets = EvalContaminationSetProvider.Load(settings);
            kbManager = new KnowledgeBaseManager(db, embedder, evalSets.ImageHashes, evalSets.HaltungKeys);
        }
        catch
        {
            // Feedback wird weiterhin geloggt; nur das optionale KB-Update faellt aus.
        }

        return new InfraSelfImproving.FeedbackIngestionService(logger, weights, kbManager);
    }
}
