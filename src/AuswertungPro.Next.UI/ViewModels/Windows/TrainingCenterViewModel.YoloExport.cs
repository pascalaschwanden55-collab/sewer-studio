using System.Threading.Tasks;

using CommunityToolkit.Mvvm.Input;

using AuswertungPro.Next.UI.Ai.Training;

namespace AuswertungPro.Next.UI.ViewModels.Windows;

public partial class TrainingCenterViewModel
{
    /// <summary>
    /// Exportiert Approved-Samples im YOLO-Format.
    /// Ablauf, Sidecar-Fallback und Eval-Schutz liegen im Training-Workflow.
    /// </summary>
    [RelayCommand]
    private async Task ExportYoloAsync()
    {
        await TrainingYoloExportWorkflow.RunAsync(
            TrainingYoloExportRequestFactory.CreateWithDefaults(
                Samples,
                _settings,
                _codeCatalog,
                () => IsBusy,
                () => PersistSamplesAsync(),
                ResetGenerationCancellation,
                value => IsBusy = value,
                Log,
                value => ProgressMax = value,
                value => ProgressValue = value,
                value => StatusText = value)).ConfigureAwait(true);
    }
}
