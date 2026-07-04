using AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase;
using AuswertungPro.Next.UI.Ai.Training;
using System.IO;

namespace AuswertungPro.Next.UI.Tests;

public sealed class TrainingReviewFeedbackServiceFactoryTests
{
    [Fact]
    public void Create_baut_feedback_service_mit_kb_context()
    {
        var root = Path.Combine(Path.GetTempPath(), "SewerStudioTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        using var db = new KnowledgeBaseContext(Path.Combine(root, "kb.db"));

        var service = TrainingReviewFeedbackServiceFactory.Create(db, settings: null);

        Assert.NotNull(service);
    }
}
