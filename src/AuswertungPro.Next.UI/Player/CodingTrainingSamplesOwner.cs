using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Player;

public sealed class CodingTrainingSamplesOwner
{
    private readonly Func<CodingTrainingSamplePersistenceCoordinator> _createCoordinator;
    private CodingTrainingSamplePersistenceCoordinator? _coordinator;

    public CodingTrainingSamplesOwner(Func<CodingTrainingSamplePersistenceCoordinator> createCoordinator)
    {
        ArgumentNullException.ThrowIfNull(createCoordinator);

        _createCoordinator = createCoordinator;
    }

    public CodingTrainingSamplePersistenceCoordinator Coordinator
        => _coordinator ??= _createCoordinator();

    public static CodingTrainingSamplesOwner CreateDefault(
        Func<ICodingSessionService?> sessionProvider,
        AppSettings? settings,
        ITrainingSampleStore? trainingSamples = null)
    {
        ArgumentNullException.ThrowIfNull(sessionProvider);

        return new CodingTrainingSamplesOwner(
            () => CodingTrainingSamplePersistenceCoordinator.CreateDefault(
                sessionProvider,
                settings,
                trainingSamples));
    }
}
