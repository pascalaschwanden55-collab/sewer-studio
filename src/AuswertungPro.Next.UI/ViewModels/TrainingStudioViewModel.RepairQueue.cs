using AuswertungPro.Next.Application.Ai.Workbench;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.UseCases.TrainingStudioSegmentation;

namespace AuswertungPro.Next.UI.ViewModels;

internal enum TrainingStudioQueueMode
{
    Normal,
    SegmentationRepair,
    GoldQualityReview,
}

/// <summary>
/// Dünne UI-Koordination der Segmentierungs-Reparaturliste. Maskenprüfung und
/// parallele KI-Analyse bleiben in den Application-Use-Cases.
/// </summary>
public sealed partial class TrainingStudioViewModel
{
    private bool IsSegmentationRepairQueue =>
        _queueMode == TrainingStudioQueueMode.SegmentationRepair;

    private bool IsGoldQualityReviewQueue =>
        _queueMode == TrainingStudioQueueMode.GoldQualityReview;

    private bool IsStrictReviewQueue =>
        _queueMode is TrainingStudioQueueMode.SegmentationRepair
            or TrainingStudioQueueMode.GoldQualityReview;

    /// <summary>
    /// Haelt Vorschaubild-Auswahl und wirklich bearbeitetes Bild zusammen. In der
    /// Reparaturliste bleibt die Reihenfolge verbindlich, damit kein offenes Bild
    /// durch Anklicken uebersprungen wird.
    /// </summary>
    public Task<bool> SelectQueueItemAsync(int requestedIndex)
    {
        if (requestedIndex < 0 || requestedIndex >= Items.Count)
            return Task.FromResult(false);

        if (IsQueueItemCompleted(requestedIndex))
        {
            StatusText =
                "Dieses Bild ist bereits abgeschlossen. Fertige Goldfaelle werden hier nicht erneut geoeffnet.";
            return Task.FromResult(false);
        }

        if (requestedIndex == CurrentIndex)
            return Task.FromResult(true);

        if (IsAwaitingImageCompletion)
        {
            StatusText =
                "Das aktuelle Ereignis ist gespeichert. Bitte zuerst 'Bild fertig' oder " +
                "'Weiteres Ereignis auf diesem Bild' waehlen.";
            return Task.FromResult(false);
        }

        if (SavedEventCountForCurrentImage > 0)
        {
            StatusText =
                "Ein weiteres Ereignis auf diesem Bild ist noch offen. Bitte speichern oder verwerfen; " +
                "danach 'Bild fertig' waehlen.";
            return Task.FromResult(false);
        }

        if (IsStrictReviewQueue)
        {
            StatusText =
                "In dieser Gold-Warteschlange geht es erst nach einer gültigen Maske und persönlichem Akzeptieren weiter.";
            return Task.FromResult(false);
        }

        if (_saveInProgress || IsBusy)
        {
            StatusText = "Die KI arbeitet noch. Das Arbeitsbild wurde nicht gewechselt.";
            return Task.FromResult(false);
        }

        CurrentIndex = requestedIndex;
        ResetForCurrent();
        return Task.FromResult(true);
    }

    [CommunityToolkit.Mvvm.Input.RelayCommand]
    private void LoadQueue()
    {
        var previousMode = _queueMode;
        var previousReviewSessionId = _activeGoldQualityReviewSessionId;
        _queueMode = TrainingStudioQueueMode.Normal;
        _activeGoldQualityReviewSessionId = null;
        if (!LoadItemsCore(_loadQueue()))
        {
            _queueMode = previousMode;
            _activeGoldQualityReviewSessionId = previousReviewSessionId;
            return;
        }
        if (Items.Count == 0)
            StatusText = "Warteschlange leer.";
    }

    public async Task<bool> LoadSegmentationRepairItemsAsync(
        IReadOnlyList<WorkbenchItem> items,
        CancellationToken cancellationToken = default)
    {
        var previousMode = _queueMode;
        var previousReviewSessionId = _activeGoldQualityReviewSessionId;
        _queueMode = TrainingStudioQueueMode.SegmentationRepair;
        _activeGoldQualityReviewSessionId = null;
        if (!LoadItemsCore(items))
        {
            _queueMode = previousMode;
            _activeGoldQualityReviewSessionId = previousReviewSessionId;
            return false;
        }
        await PrepareCurrentStrictReviewItemAsync(cancellationToken);
        return true;
    }

    private bool MoveToNextItemAfterSave(string savedStatus)
    {
        var nextIndex = CurrentItem is { } completedItem
            ? FindOpenPdfReferenceForSameImage(completedItem)
            : -1;
        if (nextIndex < 0)
            nextIndex = FindNextOpenQueueItemIndex(CurrentIndex + 1);
        if (nextIndex < 0)
            nextIndex = FindNextOpenQueueItemIndex(0);

        if (nextIndex >= 0)
        {
            CurrentIndex = nextIndex;
            ResetForCurrent();
            StatusText = $"{savedStatus} Naechstes Bild: {CurrentIndex + 1} von {Items.Count}.";
            return IsStrictReviewQueue;
        }

        CurrentIndex = -1;
        ResetForCurrent();
        StatusText = IsGoldQualityReviewQueue
            ? $"{savedStatus} Goldpruefung abgeschlossen: {QueueDoneCount} von {QueueTotalCount} bestaetigt."
            : $"{savedStatus} Warteschlange abgearbeitet.";
        return false;
    }

    private async Task PrepareCurrentStrictReviewItemAsync(
        CancellationToken cancellationToken)
    {
        if (!IsStrictReviewQueue || CurrentItem is not { } item)
            return;

        if (item.ExistingBox is { } existingBox)
        {
            if (IsGoldQualityReviewQueue && item.ExistingSegmentation is not null)
            {
                await LoadGoldQualityComparisonAsync(item, existingBox, cancellationToken);
                return;
            }

            StatusText =
                $"Bild {CurrentIndex + 1} von {Items.Count}: Vorhandene Box wird mit SAM segmentiert …";
            await BoxDrawnAsync(existingBox);
            if (ReferenceEquals(CurrentItem, item) && Segmentation is not null)
                StatusText = BuildSegmentationRepairMaskStatus(saveAttempt: false);
            return;
        }

        await FotoMitKiPruefenAsync(cancellationToken);
        if (ReferenceEquals(CurrentItem, item)
            && Suggestion is { ModelAvailable: true, FrameUsable: true }
            && Suggestion.Candidates.Count > 0)
        {
            StatusText =
                $"Bild {CurrentIndex + 1} von {Items.Count}: Keine gültige Box vorhanden. " +
                "KI-Vorschlag prüfen und eine Box um die Situation ziehen; danach segmentiert SAM automatisch.";
        }
        else if (ReferenceEquals(CurrentItem, item))
        {
            StatusText += " Bitte eine Box um die Situation ziehen; danach segmentiert SAM automatisch.";
        }
    }

    private async Task LoadGoldQualityComparisonAsync(
        WorkbenchItem item,
        BoundingBox existingBox,
        CancellationToken cancellationToken)
    {
        _boxCts?.Cancel();
        var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _boxCts = cts;
        _boxRunActive = true;
        IsBusy = true;
        StatusText =
            $"Goldpruefung {QueueDoneCount + 1} von {QueueTotalCount}: " +
            "Gespeicherte Goldmaske ist sichtbar. KI-Vergleich wird geladen ...";
        try
        {
            var comparison = await _workbench.SuggestAsync(item, existingBox, cts.Token);
            if (cts.IsCancellationRequested || !ReferenceEquals(CurrentItem, item))
                return;

            Suggestion = comparison;
            StatusText = BuildSegmentationRepairMaskStatus(saveAttempt: false);
        }
        catch (OperationCanceledException)
        {
            // Bild- oder Warteschlangenwechsel: Der neuere Vorgang besitzt den Zustand.
        }
        catch (Exception ex)
        {
            if (ReferenceEquals(CurrentItem, item))
            {
                StatusText = BuildSegmentationRepairMaskStatus(saveAttempt: false)
                    + " Der KI-Vergleich ist derzeit nicht verfuegbar: "
                    + UserError.DescribeAndReport(ex, "Training-Studio Goldpruefung");
            }
        }
        finally
        {
            if (_boxCts == cts)
            {
                _boxRunActive = false;
                IsBusy = false;
                _boxCts = null;
            }
            cts.Dispose();
        }
    }

    private string BuildSegmentationRepairMaskStatus(bool saveAttempt)
    {
        var validation = TrainingStudioBoxAnalysisUseCase.ValidateSegmentation(
            CurrentBox,
            Segmentation);
        var prefix = saveAttempt
            ? "Noch nicht gespeichert: "
            : IsGoldQualityReviewQueue
                ? $"Goldpruefung {QueueDoneCount + 1} von {QueueTotalCount}: "
                : $"Bild {CurrentIndex + 1} von {Items.Count}: ";

        if (!validation.IsValid)
        {
            if (validation.Failure is TrainingStudioSegmentationValidationFailure.MissingMask
                or TrainingStudioSegmentationValidationFailure.InvalidMask)
            {
                return prefix
                       + "Es fehlt eine gültige sichtbare SAM-Maske: "
                       + validation.Reason
                       + " Bitte die rote Box um den ganzen sichtbaren Befund ziehen; "
                       + "SAM segmentiert danach automatisch neu.";
            }

            return prefix
                   + "Die SAM-Maske ist sichtbar, aber nicht verwendbar: "
                   + validation.Reason
                   + " Bitte die rote Box so anpassen, dass sie den ganzen sichtbaren Befund umfasst; "
                   + "SAM segmentiert danach automatisch neu.";
        }

        var suggestionText = Suggestion?.Candidates.Count > 0
            ? " und Codevorschlag"
            : string.Empty;
        var maskSource = IsGoldQualityReviewQueue
            ? "Gespeicherte Goldmaske"
            : "KI-Maske";
        return prefix
               + $"{maskSource}{suggestionText} prüfen. "
               + "Wenn Maske und Code stimmen, akzeptieren; sonst Code korrigieren oder eine neue Box ziehen.";
    }

    private bool CanSaveCurrentSegmentationRepairItem()
    {
        if (!IsStrictReviewQueue)
            return true;

        var validation = TrainingStudioBoxAnalysisUseCase.ValidateSegmentation(
            CurrentBox,
            Segmentation);
        if (validation.IsValid)
            return true;

        StatusText = BuildSegmentationRepairMaskStatus(saveAttempt: true);
        return false;
    }

    private void CancelBoxRunForImageChange()
    {
        var running = _boxCts;
        if (running is null)
            return;

        _boxCts = null;
        _boxRunActive = false;
        running.Cancel();
        if (!_isCheckingPhoto && !_isRunningPreviewDetection && !_isStartingAi && !_saveInProgress)
            IsBusy = false;
    }
}
