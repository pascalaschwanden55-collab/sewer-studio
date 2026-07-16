using AuswertungPro.Next.Application.Ai.Training;

namespace AuswertungPro.Next.UI.Ai.Training;

public sealed class TrainingCaseIdSource : ITrainingCaseIdSource
{
    private readonly TrainingCenterStore _store;

    public TrainingCaseIdSource(TrainingCenterStore store)
        => _store = store ?? throw new ArgumentNullException(nameof(store));

    public async Task<IReadOnlyList<string>> LoadCaseIdsAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var state = await _store.LoadAsync().ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        return state.Cases.Select(trainingCase => trainingCase.CaseId).ToArray();
    }
}
