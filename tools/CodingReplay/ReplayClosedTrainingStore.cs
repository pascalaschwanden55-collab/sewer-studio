using AuswertungPro.Next.Application.Ai.Training;

namespace CodingReplay;

/// <summary>Jeder versehentliche Zugriff auf Trainingsdaten scheitert im Messhost.</summary>
internal sealed class ReplayClosedTrainingStore : ITrainingSampleStore
{
    private static InvalidOperationException Blocked() => new("Training ist im Messhost gesperrt.");
    public Task<List<TrainingSample>> LoadAsync() => throw Blocked();
    public Task SaveAsync(List<TrainingSample> samples) => throw Blocked();
    public Task MergeOrUpdateAsync(IEnumerable<TrainingSample> samples) => throw Blocked();
    public Task MergeAndSaveAsync(List<TrainingSample> samples) => throw Blocked();
    public Task<bool> TryAddNewAsync(TrainingSample sample, CancellationToken ct = default) => throw Blocked();
    public Task<bool> RemoveBySampleIdAsync(string sampleId) => throw Blocked();
    public Task<bool> ReplaceBySampleIdAsync(TrainingSample sample) => throw Blocked();
}
