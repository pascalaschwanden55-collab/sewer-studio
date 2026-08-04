using System.Collections.ObjectModel;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.Training.Preview;
using AuswertungPro.Next.Application.Ai.Workbench;
using AuswertungPro.Next.Application.Common;
using CommunityToolkit.Mvvm.Input;

namespace AuswertungPro.Next.UI.ViewModels;

/// <summary>
/// Reine Modellvorschau im Training Studio. Vorschautreffer werden nie automatisch
/// als Box, Goldsample oder persoenliche Bestaetigung uebernommen.
/// </summary>
public sealed partial class TrainingStudioViewModel
{
    private int _previewCatalogRefreshVersion;
    private bool _standardModelMarkedUnavailable;

    private void MarkStandardModelUnavailable()
    {
        if (_standardModelMarkedUnavailable)
            return;
        _standardModelMarkedUnavailable = true;
        var activeOption = TrainingStudioPreviewModelCatalog.CreateActiveOption(
            PreviewModelOptions,
            standardModelUnavailable: true);
        PreviewModelOptions =
        [
            activeOption,
            .. PreviewModelOptions.Where(
                item => item.Kind == TrainingPreviewModelKind.BccTestCandidate),
        ];
        if (SelectedPreviewModel?.Kind == TrainingPreviewModelKind.ActiveStandard)
            SelectedPreviewModel = activeOption;
    }

    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task RefreshPreviewModelsAsync(CancellationToken cancellationToken)
    {
        if (_previewDetection is null)
            return;

        var refreshVersion = Interlocked.Increment(ref _previewCatalogRefreshVersion);
        try
        {
            var catalog = await _previewDetection.GetBccCandidatesAsync(cancellationToken);
            if (refreshVersion != Volatile.Read(ref _previewCatalogRefreshVersion))
                return;
            ApplyPreviewModelCatalogState(TrainingStudioPreviewModelCatalog.Build(
                catalog,
                PreviewModelOptions,
                SelectedPreviewModel,
                _standardModelMarkedUnavailable));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            if (refreshVersion != Volatile.Read(ref _previewCatalogRefreshVersion))
                return;
            ApplyPreviewModelCatalogState(TrainingStudioPreviewModelCatalog.Unavailable(
                PreviewModelOptions,
                _standardModelMarkedUnavailable,
                "BCC-Kandidaten konnten nicht geladen werden: "
                + UserError.DescribeAndReport(ex, "Training-Studio BCC-Kandidaten")));
        }
    }

    private void ApplyPreviewModelCatalogState(
        TrainingStudioPreviewModelCatalogState state)
    {
        PreviewModelOptions = state.Options;
        SelectedPreviewModel = state.Selected;
        if (state.ErrorSummary is null)
            return;
        PreviewDetections = new ObservableCollection<TrainingStudioPreviewDetectionItem>();
        PreviewDetectionSummary = state.ErrorSummary;
    }

    partial void OnSelectedPreviewModelChanged(TrainingStudioPreviewModelOption? value)
    {
        PreviewDetections = new ObservableCollection<TrainingStudioPreviewDetectionItem>();
        PreviewDetectionSummary = value is null
            ? "Bitte ein Modell wählen."
            : $"{value.DisplayName}: bereit für einen reinen Fototest.";
    }

    private bool IsCurrentPreviewRequest(
        WorkbenchItem item,
        TrainingStudioPreviewModelOption model)
        => ReferenceEquals(CurrentItem, item)
           && Equals(SelectedPreviewModel, model);

    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task RunPreviewDetectionAsync(CancellationToken cancellationToken)
    {
        if (IsAwaitingImageCompletion)
        {
            PreviewDetectionSummary =
                "Das Ereignis ist bereits gespeichert. Bitte zuerst 'Bild fertig' oder " +
                "'Weiteres Ereignis auf diesem Bild' waehlen.";
            StatusText = PreviewDetectionSummary;
            return;
        }

        var item = CurrentItem;
        var model = SelectedPreviewModel;
        if (_previewDetection is null)
        {
            PreviewDetectionSummary = "Der Modelltest ist in diesem Fenster nicht verfügbar.";
            return;
        }
        if (item is null || string.IsNullOrWhiteSpace(item.FramePath))
        {
            PreviewDetectionSummary = "Bitte zuerst ein Foto laden.";
            return;
        }
        if (model is null)
        {
            PreviewDetectionSummary = "Bitte ein Modell wählen.";
            return;
        }
        if (model.Kind == TrainingPreviewModelKind.BccTestCandidate
            && !TrainingStudioPreviewModelCatalog.HasExactCandidatePin(
                model.CandidateId,
                model.CandidateSha256))
        {
            PreviewDetectionSummary =
                "BCC-Testmodell gesperrt: Die exakte Kandidaten-ID oder der SHA-256 fehlt.";
            StatusText = PreviewDetectionSummary;
            return;
        }
        if (IsBusy)
        {
            PreviewDetectionSummary = "Bitte warten, bis der laufende KI-Schritt fertig ist.";
            return;
        }

        PreviewDetections = new ObservableCollection<TrainingStudioPreviewDetectionItem>();
        PreviewDetectionSummary = $"{model.DisplayName}: Foto wird geprüft …";
        StatusText = PreviewDetectionSummary;
        _isRunningPreviewDetection = true;
        IsBusy = true;
        try
        {
            if (model.Kind == TrainingPreviewModelKind.ActiveStandard)
            {
                var qualification = await _previewDetection
                    .GetDetectorQualificationAsync(cancellationToken);
                if (!IsCurrentPreviewRequest(item, model))
                    return;
                if (qualification?.Qualified != true)
                {
                    MarkStandardModelUnavailable();
                    PreviewDetectionSummary =
                        string.IsNullOrWhiteSpace(qualification?.Reason)
                            ? "Standardmodell gesperrt: Der Qualifikationsstatus konnte nicht sicher geprueft werden."
                            : $"Standardmodell gesperrt: {qualification.Reason}";
                    StatusText = PreviewDetectionSummary;
                    return;
                }
            }

            var result = model.Kind == TrainingPreviewModelKind.BccTestCandidate
                ? await _previewDetection.DetectBccCandidateAsync(
                    item.FramePath,
                    model.CandidateId!,
                    model.CandidateSha256!,
                    0.25,
                    cancellationToken)
                : await _previewDetection
                    .DetectAsync(item.FramePath, model.Kind, 0.25, cancellationToken);
            if (!IsCurrentPreviewRequest(item, model))
                return;
            var presentation = TrainingStudioPreviewPresenter.Build(
                result,
                model,
                _textPresenter.ResolveCodeLabel);
            PreviewDetections =
                new ObservableCollection<TrainingStudioPreviewDetectionItem>(
                    presentation.Detections);
            PreviewDetectionSummary = presentation.Summary;
            StatusText = PreviewDetectionSummary;
        }
        catch (OperationCanceledException)
        {
            if (!IsCurrentPreviewRequest(item, model))
                return;
            PreviewDetectionSummary = "Modelltest abgebrochen.";
            StatusText = PreviewDetectionSummary;
        }
        catch (SidecarUnavailableException ex)
        {
            if (!IsCurrentPreviewRequest(item, model))
                return;
            UserError.DescribeAndReport(ex, "Training-Studio Modelltest");
            PreviewDetectionSummary =
                "Lokaler KI-Dienst ist nicht erreichbar. Bitte oben 'KI starten' wählen.";
            StatusText = PreviewDetectionSummary;
        }
        catch (Exception ex)
        {
            if (!IsCurrentPreviewRequest(item, model))
                return;
            PreviewDetectionSummary = "Modelltest nicht möglich: "
                + UserError.DescribeAndReport(ex, "Training-Studio Modelltest");
            StatusText = PreviewDetectionSummary;
        }
        finally
        {
            _isRunningPreviewDetection = false;
            IsBusy = false;
        }
    }
}
