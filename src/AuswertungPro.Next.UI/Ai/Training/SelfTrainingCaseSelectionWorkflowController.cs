using AuswertungPro.Next.Application.Ai.Training;

namespace AuswertungPro.Next.UI.Ai.Training;

public static class SelfTrainingCaseSelectionWorkflowController
{
    private const string NoProtocolCasesStatusText =
        "Keine Faelle mit Protokoll vorhanden. Bitte zuerst Ordner waehlen und scannen.";

    public static async Task<SelfTrainingCaseSelectionResult> RunAsync<TSamples>(
        TrainingCase? selectedCase,
        IEnumerable<TrainingCase> cases,
        Func<Task<TSamples>> loadSamplesAsync,
        Action<string> setStatus,
        Action<TrainingCase> setSelectedCase)
        where TSamples : IEnumerable<TrainingSample>
    {
        ArgumentNullException.ThrowIfNull(cases);
        ArgumentNullException.ThrowIfNull(loadSamplesAsync);
        ArgumentNullException.ThrowIfNull(setStatus);
        ArgumentNullException.ThrowIfNull(setSelectedCase);

        IEnumerable<TrainingSample> existingSamples = Enumerable.Empty<TrainingSample>();
        if (selectedCase is null)
            existingSamples = await loadSamplesAsync();

        var selection = SelfTrainingCaseSelectionController.Select(
            selectedCase,
            cases,
            existingSamples);

        if (selection.ShouldStop)
        {
            setStatus(selection.StatusText ?? "");
            return selection;
        }

        if (selection.Case is null)
        {
            setStatus(NoProtocolCasesStatusText);
            return new SelfTrainingCaseSelectionResult(true, null, NoProtocolCasesStatusText);
        }

        setSelectedCase(selection.Case);
        return selection;
    }
}
