using AuswertungPro.Next.Application.UseCases.GoldQualityReview;
using AuswertungPro.Next.Application.Common;

namespace AuswertungPro.Next.UI.ViewModels;

/// <summary>
/// UI-Koordination fuer die feste persoenliche 90-Bilder-Goldpruefung. Auswahl,
/// Eval-Schutz und Fortsetzungsstand bleiben im Application-Use-Case.
/// </summary>
public sealed partial class TrainingStudioViewModel
{
    [CommunityToolkit.Mvvm.Input.RelayCommand(AllowConcurrentExecutions = false)]
    private async Task LoadGoldQualityReviewAsync(CancellationToken cancellationToken)
    {
        if (_goldQualityReview is null)
        {
            StatusText = "Die Goldprüfung ist in diesem Programmstart nicht verfügbar.";
            return;
        }

        if (SavedEventCountForCurrentImage > 0)
        {
            StatusText =
                "Auf dem aktuellen Bild ist bereits ein Ereignis gespeichert. Bitte zuerst den " +
                "zusätzlichen Befund speichern oder verwerfen und danach 'Bild fertig' wählen.";
            return;
        }

        if (IsBusy
            || _saveInProgress
            || _boxRunActive
            || _isCheckingPhoto
            || _isRunningPreviewDetection
            || _isLoadingGoldQualityReview
            || _isStartingAi)
        {
            StatusText = "Ein KI- oder Speichervorgang läuft noch. Die Goldprüfung wurde nicht geladen.";
            return;
        }

        GoldQualityReviewQueueResult result;
        _isLoadingGoldQualityReview = true;
        IsBusy = true;
        StatusText = "Goldbestand und Eval-Schutz werden geprüft. Bitte kurz warten ...";
        try
        {
            result = await _goldQualityReview.ExecuteAsync(
                new GoldQualityReviewQueueRequest(_confirmedByUser),
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            StatusText = "Goldprüfung wurde abgebrochen. Die bisherige Warteschlange bleibt erhalten.";
            return;
        }
        catch (Exception ex)
        {
            StatusText = "Goldprüfung konnte nicht geladen werden. Die bisherige Warteschlange bleibt erhalten: "
                + UserError.DescribeAndReport(ex, "Training-Studio Goldprüfung laden");
            return;
        }
        finally
        {
            _isLoadingGoldQualityReview = false;
            IsBusy = false;
        }

        var previousMode = _queueMode;
        var previousReviewSessionId = _activeGoldQualityReviewSessionId;
        _queueMode = TrainingStudioQueueMode.GoldQualityReview;
        _activeGoldQualityReviewSessionId = result.SessionId;
        if (!LoadItemsCore(result.Items))
        {
            _queueMode = previousMode;
            _activeGoldQualityReviewSessionId = previousReviewSessionId;
            return;
        }

        QueueDoneCount = result.CompletedCount;
        QueueTotalCount = result.TotalCount;
        if (Items.Count == 0)
        {
            StatusText = $"Goldprüfung abgeschlossen: {QueueDoneCount} von {QueueTotalCount} bestätigt.";
            return;
        }

        StatusText = result.Resumed
            ? $"Goldprüfung fortgesetzt: {QueueDoneCount} von {QueueTotalCount} bestätigt."
            : $"Goldprüfung angelegt: {QueueTotalCount} Bilder, je 15 für BAB, BAF, BAI, BAJ, BBC und BBF.";
        await PrepareCurrentStrictReviewItemAsync(cancellationToken);
    }
}
