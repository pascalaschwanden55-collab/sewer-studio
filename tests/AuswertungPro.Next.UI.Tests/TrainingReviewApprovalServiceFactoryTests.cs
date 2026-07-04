using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Infrastructure.Ai.Training;
using AuswertungPro.Next.UI.Ai.Training;

namespace AuswertungPro.Next.UI.Tests;

public sealed class TrainingReviewApprovalServiceFactoryTests
{
    [Fact]
    public void Create_baut_review_approval_service_mit_delegiertem_indexer()
    {
        var service = TrainingReviewApprovalServiceFactory.Create(
            (_, _) => Task.FromResult(EmptyOutcome()),
            _ => { });

        Assert.IsAssignableFrom<IReviewApprovalService>(service);
        Assert.IsType<ReviewApprovalService>(service);
    }

    [Fact]
    public void Create_verlangt_index_und_deindex_delegate()
    {
        Assert.Throws<ArgumentNullException>(() =>
            TrainingReviewApprovalServiceFactory.Create(null!, _ => { }));

        Assert.Throws<ArgumentNullException>(() =>
            TrainingReviewApprovalServiceFactory.Create(
                (_, _) => Task.FromResult(EmptyOutcome()),
                null!));
    }

    private static KbIndexOutcome EmptyOutcome()
        => new(Array.Empty<string>(), Array.Empty<string>());
}
