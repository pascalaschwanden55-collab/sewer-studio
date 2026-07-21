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
        if (_trainingYoloExport is null)
        {
            const string message = "YOLO-Export ist fuer dieses Fenster nicht eingerichtet.";
            Log(message);
            StatusText = message;
            return;
        }

        await TrainingYoloExportWorkflow.RunAsync(
            TrainingYoloExportRequestFactory.Create(
                Samples,
                _trainingYoloExport,
                () => IsBusy,
                ResetGenerationCancellation,
                value => IsBusy = value,
                Log,
                value => ProgressMax = value,
                value => ProgressValue = value,
                value => StatusText = value)).ConfigureAwait(true);
    }
}
