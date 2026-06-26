using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingTrainingSamplesOwnerTests
{
    [Fact]
    public void Coordinator_is_created_lazily_and_reused()
    {
        var createCount = 0;
        var coordinator = CreateCoordinator();
        var owner = new CodingTrainingSamplesOwner(() =>
        {
            createCount++;
            return coordinator;
        });

        var first = owner.Coordinator;
        var second = owner.Coordinator;

        Assert.Same(coordinator, first);
        Assert.Same(first, second);
        Assert.Equal(1, createCount);
    }

    [Fact]
    public void Constructor_throws_for_null_factory()
    {
        Assert.Throws<ArgumentNullException>(() => new CodingTrainingSamplesOwner(null!));
    }

    private static CodingTrainingSamplePersistenceCoordinator CreateCoordinator()
        => new(
            new CodingTrainingFrameStore(),
            new CodingTrainingSamplePersister(_ => Task.CompletedTask),
            new CodingTrainingSampleEvalProtector(settings: null));
}
