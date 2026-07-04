using System.Collections.ObjectModel;
using AuswertungPro.Next.Application.Ai.Training;

namespace AuswertungPro.Next.UI.Ai.Training;

public sealed record TrainingSampleLoadWorkflowRequest(
    ObservableCollection<TrainingSample> Samples,
    Func<Task<List<TrainingSample>>> LoadSamplesAsync,
    Action<Action> OnUi);

public static class TrainingSampleLoadWorkflow
{
    public static async Task RunAsync(TrainingSampleLoadWorkflowRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Samples);
        ArgumentNullException.ThrowIfNull(request.LoadSamplesAsync);
        ArgumentNullException.ThrowIfNull(request.OnUi);

        var items = await request.LoadSamplesAsync().ConfigureAwait(false);
        TrainingSampleCollectionController.ReplaceOnUi(
            request.Samples,
            items,
            request.OnUi);
    }
}
