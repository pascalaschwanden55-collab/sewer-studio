using System;
using System.Collections.Generic;
using AuswertungPro.Next.Application.Ai.Workbench;
using AuswertungPro.Next.Application.UseCases.TrainingStudioMultiObject;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AuswertungPro.Next.UI.ViewModels;

/// <summary>
/// UI-Zustand fuer mehrere eigenstaendige Goldobjekte auf demselben Bild.
/// Reparaturen und die gebundene Goldpruefung bleiben weiterhin Einzelfall-Queues.
/// </summary>
public sealed partial class TrainingStudioViewModel
{
    private WorkbenchItem? _activeAdditionalObjectItem;
    private string? _boundCurrentImageSha256;
    private readonly HashSet<int> _completedQueueItemIndices = [];
    private readonly HashSet<int> _newDraftQueueItemIndices = [];
    private bool _activeDraftStartedAsNewObject;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAnnotationEntryEnabled))]
    [NotifyPropertyChangedFor(nameof(ImageCompletionPrompt))]
    [NotifyCanExecuteChangedFor(nameof(AddAnotherEventCommand))]
    [NotifyCanExecuteChangedFor(nameof(FinishImageCommand))]
    private bool _isAwaitingImageCompletion;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ImageCompletionPrompt))]
    private int _savedEventCountForCurrentImage;

    /// <summary>Nach einem Gold-Save wird die aktuelle Eingabe bis zur Bildentscheidung gesperrt.</summary>
    public bool IsAnnotationEntryEnabled => !IsAwaitingImageCompletion;

    /// <summary>Lesbarer Hinweis fuer die beiden ausdruecklichen Bildentscheidungen.</summary>
    public string ImageCompletionPrompt => SavedEventCountForCurrentImage == 1
        ? "1 Ereignis ist als Gold gespeichert. Gibt es auf diesem Bild noch ein weiteres Ereignis?"
        : $"{SavedEventCountForCurrentImage} Ereignisse sind als Gold gespeichert. Gibt es auf diesem Bild noch ein weiteres Ereignis?";

    private bool CanChooseImageCompletion()
        => IsAwaitingImageCompletion
           && !_saveInProgress
           && !IsBusy
           && CurrentItem is { } item
           && IsNewObjectInCurrentSession(item);

    private bool CanOfferMultipleObjects(WorkbenchItem item)
        => IsNewObjectInCurrentSession(item)
           && !HasPendingPdfReferenceForSameImage(item);

    private bool IsNewObjectInCurrentSession(WorkbenchItem item)
        => _queueMode == TrainingStudioQueueMode.Normal
           && (string.IsNullOrWhiteSpace(item.ExistingSampleId)
               || _activeDraftStartedAsNewObject
               || _newDraftQueueItemIndices.Contains(CurrentIndex));

    private bool HasPendingPdfReferenceForSameImage(WorkbenchItem item)
        => FindOpenPdfReferenceForSameImage(item) >= 0;

    private int FindOpenPdfReferenceForSameImage(WorkbenchItem item)
    {
        if (item.SourceSuggestion is not { } source || CurrentIndex < 0)
            return -1;

        for (var index = 0; index < Items.Count; index++)
        {
            if (index == CurrentIndex
                || IsQueueItemCompleted(index)
                || Items[index].SourceSuggestion is not { } candidateSource)
            {
                continue;
            }

            if (string.Equals(Items[index].FramePath, item.FramePath, StringComparison.OrdinalIgnoreCase)
                && string.Equals(Items[index].CaseId, item.CaseId, StringComparison.Ordinal)
                && string.Equals(
                    candidateSource.SourceDocumentSha256,
                    source.SourceDocumentSha256,
                    StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }

        return -1;
    }

    private void OpenImageCompletionChoice(string savedStatus, string? storedImageSha256)
    {
        _boundCurrentImageSha256 ??= string.IsNullOrWhiteSpace(storedImageSha256)
            ? CurrentItem?.ExpectedImageSha256
            : storedImageSha256.Trim();
        if (SavedEventCountForCurrentImage == 0 && CurrentItem is { } current)
            SavedEventCountForCurrentImage = CountCompletedEventsForSameImage(current);
        SavedEventCountForCurrentImage++;
        IsAwaitingImageCompletion = true;
        StatusText =
            $"{savedStatus} Ereignis {SavedEventCountForCurrentImage} ist gespeichert. " +
            "Jetzt 'Weiteres Ereignis auf diesem Bild' oder 'Bild fertig' waehlen.";
    }

    [RelayCommand(CanExecute = nameof(CanChooseImageCompletion))]
    private void AddAnotherEvent()
    {
        if (!CanChooseImageCompletion() || CurrentItem is not { } current)
        {
            StatusText = "Zuerst ein Ereignis mit Box, SAM-Maske und Code als Gold speichern.";
            return;
        }

        var boundSource = string.IsNullOrWhiteSpace(_boundCurrentImageSha256)
            ? current
            : current with { ExpectedImageSha256 = _boundCurrentImageSha256 };
        SetActiveAdditionalObjectItem(
            TrainingStudioAdditionalObjectPolicy.CreateManualObject(boundSource));
        _activeDraftStartedAsNewObject = false;
        IsAwaitingImageCompletion = false;
        ResetAnnotationForAdditionalObject();
        StatusText =
            $"Weiteres Ereignis {SavedEventCountForCurrentImage + 1} auf demselben Bild: " +
            "neue Box um genau diese Situation ziehen, SAM-Maske pruefen und separat codieren.";
    }

    [RelayCommand(CanExecute = nameof(CanChooseImageCompletion))]
    private void FinishImage()
        => FinishImageCore();

    private bool FinishImageCore()
    {
        if (!CanChooseImageCompletion())
        {
            StatusText = "'Bild fertig' ist erst nach einem erfolgreich gespeicherten Goldereignis moeglich.";
            return false;
        }

        var savedCount = SavedEventCountForCurrentImage;
        if (!TryMarkCurrentQueueItemCompleted())
        {
            StatusText = "Dieses Bild ist bereits abgeschlossen und kann nicht doppelt gezaehlt werden.";
            return false;
        }

        IsAwaitingImageCompletion = false;
        QueueDoneCount = Math.Min(QueueDoneCount + 1, QueueTotalCount);
        SetActiveAdditionalObjectItem(null);
        SavedEventCountForCurrentImage = 0;

        var completedText = savedCount == 1
            ? "Bild fertig: 1 Ereignis gespeichert."
            : $"Bild fertig: {savedCount} Ereignisse gespeichert.";

        var nextIndex = FindNextOpenQueueItemIndex(CurrentIndex + 1);
        if (nextIndex < 0)
            nextIndex = FindNextOpenQueueItemIndex(0);

        if (nextIndex >= 0)
        {
            CurrentIndex = nextIndex;
            ResetForCurrent();
            StatusText = $"{completedText} Naechstes Bild: {CurrentIndex + 1} von {Items.Count}.";
        }
        else
        {
            CurrentIndex = -1;
            ResetForCurrent();
            StatusText = $"{completedText} Warteschlange abgearbeitet.";
        }

        return true;
    }

    private void ResetAnnotationForAdditionalObject()
    {
        CancelBoxRunForImageChange();
        PreviewDetections = new();
        PreviewDetectionSummary = SelectedPreviewModel is null
            ? "Bitte ein Modell waehlen."
            : $"{SelectedPreviewModel.DisplayName}: bereit fuer einen reinen Fototest.";
        CurrentBox = null;
        Segmentation = null;
        Suggestion = null;
        SelectedCode = null;
        Beschreibung = string.Empty;
        ClockPosition = null;
        Severity = null;
        CurrentImagePath = CurrentItem?.FramePath;
    }

    private void ResetMultiObjectStateForCurrentItem()
    {
        SetActiveAdditionalObjectItem(null);
        _boundCurrentImageSha256 = null;
        _activeDraftStartedAsNewObject = false;
        IsAwaitingImageCompletion = false;
        SavedEventCountForCurrentImage = 0;
    }

    private void ResetCompletedQueueItems()
    {
        _completedQueueItemIndices.Clear();
        _newDraftQueueItemIndices.Clear();
    }

    private bool IsQueueItemCompleted(int index)
        => index >= 0 && _completedQueueItemIndices.Contains(index);

    private bool TryMarkCurrentQueueItemCompleted()
    {
        if (CurrentIndex < 0 || CurrentIndex >= Items.Count)
            return false;

        var added = _completedQueueItemIndices.Add(CurrentIndex);
        if (added)
            _newDraftQueueItemIndices.Remove(CurrentIndex);
        return added;
    }

    private int FindNextOpenQueueItemIndex(int startIndex)
    {
        for (var index = Math.Max(0, startIndex); index < Items.Count; index++)
        {
            if (!IsQueueItemCompleted(index))
                return index;
        }

        return -1;
    }

    private int FindPreviousOpenQueueItemIndex(int startIndex)
    {
        for (var index = Math.Min(startIndex, Items.Count - 1); index >= 0; index--)
        {
            if (!IsQueueItemCompleted(index))
                return index;
        }

        return -1;
    }

    private int CountCompletedEventsForSameImage(WorkbenchItem item)
    {
        var count = 0;
        foreach (var index in _completedQueueItemIndices)
        {
            if (index >= 0
                && index < Items.Count
                && IsSameImageContext(Items[index], item))
            {
                count++;
            }
        }

        return count;
    }

    private static bool IsSameImageContext(WorkbenchItem left, WorkbenchItem right)
    {
        if (!string.Equals(left.FramePath, right.FramePath, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(left.CaseId, right.CaseId, StringComparison.Ordinal))
        {
            return false;
        }

        var leftPdf = left.SourceSuggestion?.SourceDocumentSha256;
        var rightPdf = right.SourceSuggestion?.SourceDocumentSha256;
        return string.IsNullOrWhiteSpace(leftPdf)
               || string.IsNullOrWhiteSpace(rightPdf)
               || string.Equals(leftPdf, rightPdf, StringComparison.OrdinalIgnoreCase);
    }

    private void BindOpenPdfReferencesToStoredImage(
        WorkbenchItem item,
        string? storedImageSha256)
    {
        if (item.SourceSuggestion is null || string.IsNullOrWhiteSpace(storedImageSha256))
            return;

        var normalizedHash = storedImageSha256.Trim();
        for (var index = 0; index < Items.Count; index++)
        {
            if (index == CurrentIndex
                || IsQueueItemCompleted(index)
                || Items[index].SourceSuggestion is null
                || !IsSameImageContext(Items[index], item))
            {
                continue;
            }

            Items[index] = Items[index] with { ExpectedImageSha256 = normalizedHash };
        }
    }

    private void BindSavedDraftToCurrentItem(
        WorkbenchItem item,
        WorkbenchSaveResult result)
    {
        if (!string.IsNullOrWhiteSpace(item.ExistingSampleId)
            || string.IsNullOrWhiteSpace(result.SampleId))
        {
            return;
        }

        var startedAsNewObject = _queueMode == TrainingStudioQueueMode.Normal;
        var replacement = item with
        {
            ExistingSampleId = result.SampleId,
            ExistingCode = SelectedCode,
            ExistingBeschreibung = Beschreibung,
            ExistingBox = CurrentBox,
            ExistingSegmentation = Segmentation,
            ExistingClockPosition = ClockPosition,
            ExistingSeverity = Severity,
            ExpectedImageSha256 = string.IsNullOrWhiteSpace(result.StoredImageSha256)
                ? item.ExpectedImageSha256
                : result.StoredImageSha256.Trim(),
            ExpectedConfirmedAtUtc = result.StoredConfirmedAtUtc,
        };

        if (!ReferenceEquals(_activeAdditionalObjectItem, item)
            && CurrentIndex >= 0
            && CurrentIndex < Items.Count)
        {
            Items[CurrentIndex] = replacement;
            if (startedAsNewObject)
                _newDraftQueueItemIndices.Add(CurrentIndex);
        }
        else
        {
            _activeDraftStartedAsNewObject = startedAsNewObject;
        }

        SetActiveAdditionalObjectItem(replacement);
    }

    private void SetActiveAdditionalObjectItem(WorkbenchItem? item)
    {
        if (ReferenceEquals(_activeAdditionalObjectItem, item))
            return;

        _activeAdditionalObjectItem = item;
        OnPropertyChanged(nameof(CurrentItem));
        OnPropertyChanged(nameof(HasSourceSuggestion));
        OnPropertyChanged(nameof(SourceReferenceDetails));
        AddAnotherEventCommand.NotifyCanExecuteChanged();
        FinishImageCommand.NotifyCanExecuteChanged();
    }
}
