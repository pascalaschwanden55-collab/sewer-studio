using System.Collections.ObjectModel;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Infrastructure.Ai.Training;

namespace AuswertungPro.Next.UI.Ai.Training;

public static class TrainingSampleLoadRequestFactory
{
    public static TrainingSampleLoadWorkflowRequest CreateWithDefaults(
        ObservableCollection<TrainingSample> samples,
        Action<Action> onUi)
        => Create(samples, onUi, TrainingSamplesStore.LoadAsync);

    public static TrainingSampleLoadWorkflowRequest Create(
        ObservableCollection<TrainingSample> samples,
        Action<Action> onUi,
        Func<Task<List<TrainingSample>>> LoadSamplesAsync)
    {
        ArgumentNullException.ThrowIfNull(samples);
        ArgumentNullException.ThrowIfNull(onUi);
        ArgumentNullException.ThrowIfNull(LoadSamplesAsync);

        return new TrainingSampleLoadWorkflowRequest(
            samples,
            LoadSamplesAsync,
            onUi);
    }
}
