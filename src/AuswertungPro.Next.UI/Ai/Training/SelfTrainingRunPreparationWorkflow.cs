using AuswertungPro.Next.Application.Ai.Training;

namespace AuswertungPro.Next.UI.Ai.Training;

public sealed record SelfTrainingRunPreparationWorkflowRequest(
    bool IsBusy,
    bool IsSelfTrainingRunning,
    IList<TrainingCase> Cases,
    IReadOnlyList<string> RootFolders,
    Func<string, bool> DirectoryExists,
    Func<string, Task<IReadOnlyList<TrainingCase>>> ScanFolderAsync,
    TrainingCase? SelectedCase,
    Func<Task<List<TrainingSample>>> LoadSamplesAsync,
    Action<TrainingCase> SetSelectedCase,
    Func<CancellationToken> ResetCancellation,
    Action<string> SetStatusText);

public sealed record SelfTrainingRunPreparationWorkflowResult(
    bool ShouldStop,
    TrainingCase? SelectedCase,
    CancellationToken CancellationToken);

public static class SelfTrainingRunPreparationWorkflow
{
    public static async Task<SelfTrainingRunPreparationWorkflowResult> RunAsync(
        SelfTrainingRunPreparationWorkflowRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.IsBusy || request.IsSelfTrainingRunning)
            return Stop();

        await SelfTrainingAutoScanController.RunAsync(
            request.Cases.Count,
            request.RootFolders.Count,
            request.RootFolders,
            request.DirectoryExists,
            request.ScanFolderAsync,
            request.SetStatusText,
            request.Cases.Add).ConfigureAwait(false);

        IEnumerable<TrainingSample> existingSamplesForSelection = Enumerable.Empty<TrainingSample>();
        if (request.SelectedCase is null)
            existingSamplesForSelection = await request.LoadSamplesAsync().ConfigureAwait(false);

        var selection = SelfTrainingCaseSelectionController.Select(
            request.SelectedCase,
            request.Cases,
            existingSamplesForSelection);
        if (selection.ShouldStop)
        {
            request.SetStatusText(selection.StatusText ?? "");
            return Stop();
        }

        var selectedCase = selection.Case;
        if (selectedCase is null)
        {
            request.SetStatusText("Keine Faelle mit Protokoll vorhanden. Bitte zuerst Ordner waehlen und scannen.");
            return Stop();
        }

        request.SetSelectedCase(selectedCase);
        return new SelfTrainingRunPreparationWorkflowResult(
            ShouldStop: false,
            selectedCase,
            request.ResetCancellation());
    }

    private static SelfTrainingRunPreparationWorkflowResult Stop()
        => new(
            ShouldStop: true,
            SelectedCase: null,
            CancellationToken.None);
}
