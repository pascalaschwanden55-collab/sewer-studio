using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Application.Ai.Training.Preview;
using AuswertungPro.Next.Application.Ai.Workbench;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.UseCases.GoldQualityReview;
using AuswertungPro.Next.Application.UseCases.TrainingStudioSegmentation;
using AuswertungPro.Next.Infrastructure.Ai;   // VsaCodeResolver (Default-Label-Lookup)
using AuswertungPro.Next.UI.Services;   // GoldBeschreibungGuard (Platzhalter-Schutz)

namespace AuswertungPro.Next.UI.ViewModels;

/// <summary>Darstellung eines KI-Kandidaten mit VSA-Code und lesbarem Katalogtext.</summary>
public sealed record TrainingStudioSuggestionItem(
    string VsaCode,
    string Klartext,
    double Confidence,
    string Quelle);

/// <summary>
/// Pruefplatz-ViewModel (Etappe 1): duenn ueber <see cref="IAnnotationWorkbenchService"/>.
/// Tastatur-Arbeitsfluss: Box ziehen → Maske + Vorschlag → Akzeptieren/Korrigieren → naechstes.
/// Jeder Box-Lauf hat sein EIGENES CancellationTokenSource (kein geteilter Abbruch).
/// </summary>
public sealed partial class TrainingStudioViewModel : ObservableObject, IDisposable
{
    private readonly IAnnotationWorkbenchService _workbench;
    private readonly Func<IReadOnlyList<WorkbenchItem>> _loadQueue;
    private readonly string _confirmedByUser;
    private readonly Func<string, string?> _codeLabelLookup;
    private readonly Func<IProgress<string>, CancellationToken, Task<(bool Ready, string StatusText)>>? _ensureAiReady;
    private readonly Func<CancellationToken, Task<IReadOnlyList<PersonalGoldMainCodeStatus>>>? _loadGoldProgress;
    private readonly ITrainingPreviewDetectionService? _previewDetection;
    private readonly TrainingStudioBoxAnalysisUseCase _boxAnalysis;
    private readonly TrainingStudioTextPresenter _textPresenter;
    private readonly IGoldQualityReviewQueueUseCase? _goldQualityReview;

    private CancellationTokenSource? _boxCts;
    private bool _boxRunActive;
    private bool _saveInProgress;
    private bool _isStartingAi;
    private bool _isCheckingPhoto;
    private bool _isRunningPreviewDetection;
    private bool _isLoadingGoldQualityReview;
    private string? _activeGoldQualityReviewSessionId;
    private TrainingStudioQueueMode _queueMode;
    private int _queueVersion;

    public TrainingStudioViewModel(
        IAnnotationWorkbenchService workbench,
        Func<IReadOnlyList<WorkbenchItem>> loadQueue,
        string confirmedByUser,
        Func<string, string?>? codeLabelLookup = null,
        Func<IProgress<string>, CancellationToken, Task<(bool Ready, string StatusText)>>? ensureAiReady = null,
        Func<CancellationToken, Task<IReadOnlyList<PersonalGoldMainCodeStatus>>>? loadGoldProgress = null,
        ITrainingPreviewDetectionService? previewDetection = null,
        IGoldQualityReviewQueueUseCase? goldQualityReview = null)
    {
        _workbench = workbench;
        _loadQueue = loadQueue;
        _confirmedByUser = confirmedByUser;
        _codeLabelLookup = codeLabelLookup ?? VsaCodeResolver.LookupLabel;
        _boxAnalysis = new TrainingStudioBoxAnalysisUseCase(workbench);
        _textPresenter = new TrainingStudioTextPresenter(_codeLabelLookup);
        _ensureAiReady = ensureAiReady;
        _loadGoldProgress = loadGoldProgress;
        _previewDetection = previewDetection;
        _goldQualityReview = goldQualityReview;
        SelectedPreviewModel = PreviewModelOptions[0];
    }

    // Gibt beim Fensterschliessen den Pruefplatz-Workbench (SAM-Service + Vision-Client mit
    // eigenem HttpClient) und die laufende Box-Cancellation frei.
    public void Dispose()
    {
        (_workbench as IDisposable)?.Dispose();
        _boxCts?.Cancel();
        _boxCts?.Dispose();
        _boxCts = null;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentItem))]
    [NotifyPropertyChangedFor(nameof(HasSourceSuggestion))]
    [NotifyPropertyChangedFor(nameof(SourceReferenceDetails))]
    private ObservableCollection<WorkbenchItem> _items = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentItem))]
    [NotifyPropertyChangedFor(nameof(HasSourceSuggestion))]
    [NotifyPropertyChangedFor(nameof(SourceReferenceDetails))]
    private int _currentIndex = -1;

    [ObservableProperty] private string? _currentImagePath;
    [ObservableProperty] private BoundingBox? _currentBox;
    [ObservableProperty] private WorkbenchSegmentation? _segmentation;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowQualityWarning))]
    [NotifyPropertyChangedFor(nameof(QualityWarning))]
    [NotifyPropertyChangedFor(nameof(SuggestionCandidates))]
    private WorkbenchSuggestion? _suggestion;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedCodeLabel))]
    private string? _selectedCode;
    [ObservableProperty] private string _beschreibung = string.Empty;
    [ObservableProperty] private double? _clockPosition;
    [ObservableProperty] private int? _severity;
    [ObservableProperty] private string _statusText = string.Empty;
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartAiCommand))]
    [NotifyCanExecuteChangedFor(nameof(AddAnotherEventCommand))]
    [NotifyCanExecuteChangedFor(nameof(FinishImageCommand))]
    private bool _isBusy;
    [ObservableProperty] private int _queueDoneCount;
    [ObservableProperty] private int _queueTotalCount;
    [ObservableProperty] private ObservableCollection<PersonalGoldMainCodeStatus> _goldProgressItems = new();
    [ObservableProperty] private string _goldProgressSummary = "Goldstand wird geladen …";
    [ObservableProperty] private TrainingStudioPreviewModelOption? _selectedPreviewModel;
    [ObservableProperty] private ObservableCollection<TrainingStudioPreviewDetectionItem> _previewDetections = new();
    [ObservableProperty] private string _previewDetectionSummary =
        "Modell wählen und das aktuelle Foto prüfen.";

    [ObservableProperty]
    private IReadOnlyList<TrainingStudioPreviewModelOption> _previewModelOptions =
    [
        new(TrainingPreviewModelKind.ActiveStandard, "Aktives Standardmodell"),
    ];

    /// <summary>Optionaler Rohrdurchmesser in mm fuer neu geladene Fotos (leer = 300-mm-Default).</summary>
    [ObservableProperty] private string _pipeDiameterInput = string.Empty;

    /// <summary>Aktuell angezeigtes Item (oder null, wenn die Warteschlange leer/abgearbeitet ist).</summary>
    public WorkbenchItem? CurrentItem =>
        _activeAdditionalObjectItem
        ?? (CurrentIndex >= 0 && CurrentIndex < Items.Count ? Items[CurrentIndex] : null);

    /// <summary>Zeigt an, dass Code und Befund aus einem PDF-Operateurprotokoll stammen.</summary>
    public bool HasSourceSuggestion => CurrentItem?.SourceSuggestion is not null;

    /// <summary>Lesbare, nicht bestaetigte PDF-Referenz ohne absoluten Kundenpfad.</summary>
    public string SourceReferenceDetails =>
        TrainingStudioPdfSourcePresentation.Format(CurrentItem);

    /// <summary>true = Sidecar meldet unbrauchbaren Frame oder Bogen-Veto (Warnhinweis anzeigen).</summary>
    public bool ShowQualityWarning =>
        Suggestion is not null
        && (!Suggestion.ModelAvailable || !Suggestion.FrameUsable || Suggestion.IsBend);

    /// <summary>Warntext zu Frame-Qualitaet/Bogen (leer, wenn keine Warnung).</summary>
    public string QualityWarning
    {
        get
        {
            if (Suggestion is null) return string.Empty;
            if (!Suggestion.ModelAvailable)
                return string.IsNullOrWhiteSpace(Suggestion.UnavailableReason)
                    ? "KI-Modell nicht verfuegbar."
                    : $"KI-Modell nicht verfuegbar: {Suggestion.UnavailableReason}";
            if (!Suggestion.FrameUsable) return $"Frame nicht verwertbar: {Suggestion.QualityReason}";
            if (Suggestion.IsBend) return "Bogen erkannt — hier kein BCE (Rohrende) codieren.";
            return string.Empty;
        }
    }

    /// <summary>KI-Kandidaten mit Klartext aus dem aktiven VSA-Katalog.</summary>
    public IReadOnlyList<TrainingStudioSuggestionItem> SuggestionCandidates =>
        Suggestion?.Candidates
            .Select(candidate => new TrainingStudioSuggestionItem(
                candidate.VsaCode,
                _textPresenter.ResolveCodeLabel(candidate.VsaCode),
                candidate.Confidence,
                candidate.Quelle))
            .ToArray()
        ?? Array.Empty<TrainingStudioSuggestionItem>();

    /// <summary>Lesbare Bedeutung des aktuell eingetragenen Codes.</summary>
    public string SelectedCodeLabel => _textPresenter.ResolveCodeLabel(SelectedCode);

    partial void OnSelectedCodeChanging(string? oldValue, string? newValue)
    {
        var previousTemplate = _textPresenter.BuildBeschreibungVorlage(oldValue, ClockPosition);
        if (string.IsNullOrWhiteSpace(Beschreibung)
            || string.Equals(Beschreibung, previousTemplate, StringComparison.Ordinal))
        {
            Beschreibung = _textPresenter.BuildBeschreibungVorlage(newValue, ClockPosition);
        }
    }

    /// <summary>Optionaler Rohrdurchmesser als Zahl (null, wenn leer/ungueltig → Service nutzt 300 mm).</summary>
    public int? PipeDiameterMm =>
        int.TryParse(PipeDiameterInput, out var dn) && dn > 0 ? dn : null;

    /// <summary>Uebernimmt eine extern erzeugte Item-Liste (z. B. Fotos aus dem Dateidialog).</summary>
    public bool LoadItems(IReadOnlyList<WorkbenchItem> items)
    {
        var previousMode = _queueMode;
        var previousReviewSessionId = _activeGoldQualityReviewSessionId;
        _queueMode = TrainingStudioQueueMode.Normal;
        _activeGoldQualityReviewSessionId = null;
        if (LoadItemsCore(items))
            return true;
        _queueMode = previousMode;
        _activeGoldQualityReviewSessionId = previousReviewSessionId;
        return false;
    }

    private bool LoadItemsCore(IReadOnlyList<WorkbenchItem> items)
    {
        if (SavedEventCountForCurrentImage > 0)
        {
            StatusText =
                "Auf dem aktuellen Bild ist bereits mindestens ein Ereignis gespeichert. " +
                "Bitte den zusaetzlichen Befund speichern oder verwerfen und danach 'Bild fertig' waehlen.";
            return false;
        }

        if (_saveInProgress
            || _isCheckingPhoto
            || _isRunningPreviewDetection
            || _isStartingAi
            || _isLoadingGoldQualityReview)
        {
            StatusText = "Ein KI- oder Speichervorgang läuft noch. Die Warteschlange wurde nicht gewechselt.";
            return false;
        }

        CancelBoxRunForImageChange();
        Interlocked.Increment(ref _queueVersion);
        ResetCompletedQueueItems();
        Items = new ObservableCollection<WorkbenchItem>(items);
        CurrentIndex = Items.Count > 0 ? 0 : -1;
        QueueDoneCount = 0;
        QueueTotalCount = Items.Count;
        ResetForCurrent();
        if (Items.Count == 0)
            StatusText = "Keine Bilder geladen.";
        return true;
    }

    /// <summary>
    /// Uebernimmt eine Auswahl aus dem VSA-Codierfenster: nicht-leere Werte gewinnen,
    /// fehlende (null) lassen den bestehenden Pruefplatz-Wert unangetastet.
    /// </summary>
    public void ApplyCodeSelection(
        string? code,
        double? clockPosition,
        int? severity,
        string? katalogBeschreibung = null)
    {
        if (IsAwaitingImageCompletion)
        {
            StatusText =
                "Dieses Ereignis ist bereits gespeichert. Bitte 'Weiteres Ereignis auf diesem Bild' " +
                "oder 'Bild fertig' waehlen.";
            return;
        }

        if (!string.IsNullOrWhiteSpace(code))
            SelectedCode = _textPresenter.NormalizeCode(code);
        if (clockPosition.HasValue)
            ClockPosition = clockPosition;
        if (severity.HasValue)
            Severity = severity;
        // Die persoenlich bestaetigte Katalogauswahl liefert bereits eine fachliche
        // Beschreibung. Sie ersetzt nur ein leeres Feld oder den automatischen
        // Platzhalter; selbst geschriebener Text bleibt unangetastet.
        if (string.IsNullOrWhiteSpace(Beschreibung) || GoldBeschreibungGuard.IsPlaceholder(Beschreibung))
        {
            Beschreibung = _textPresenter.BuildKatalogBeschreibung(
                SelectedCode,
                katalogBeschreibung,
                ClockPosition,
                Severity);
        }
    }

    private bool CanStartAi() => !IsBusy;

    [RelayCommand(CanExecute = nameof(CanStartAi), AllowConcurrentExecutions = false)]
    private async Task StartAiAsync(CancellationToken ct)
    {
        if (_ensureAiReady is null)
        {
            StatusText = "KI-Start ist nur im laufenden SewerStudio verfuegbar.";
            return;
        }

        _isStartingAi = true;
        IsBusy = true;
        var acceptsProgress = true;
        try
        {
            var progress = new Progress<string>(message =>
            {
                if (acceptsProgress)
                    StatusText = message;
            });
            var result = await _ensureAiReady(progress, ct);
            acceptsProgress = false;
            if (result.Ready)
                await RefreshPreviewModelsAsync(ct);
            StatusText = result.StatusText;
        }
        catch (OperationCanceledException)
        {
            acceptsProgress = false;
            StatusText = "KI-Start abgebrochen.";
        }
        catch (Exception ex)
        {
            acceptsProgress = false;
            StatusText = "KI konnte nicht gestartet werden: "
                + UserError.DescribeAndReport(ex, "Training-Studio KI-Start");
        }
        finally
        {
            acceptsProgress = false;
            _isStartingAi = false;
            IsBusy = false;
        }
    }

    [RelayCommand(AllowConcurrentExecutions = false)]
    private Task RefreshGoldProgressAsync(CancellationToken cancellationToken)
        => RefreshGoldProgressCoreAsync(cancellationToken);

    [RelayCommand]
    private async Task BoxDrawnAsync(BoundingBox box)
    {
        if (IsAwaitingImageCompletion)
        {
            StatusText =
                "Dieses Ereignis ist bereits gespeichert. Bitte 'Weiteres Ereignis auf diesem Bild' " +
                "oder 'Bild fertig' waehlen.";
            return;
        }

        var item = CurrentItem;
        if (item is null)
            return;
        if (_isRunningPreviewDetection)
        {
            StatusText = "Der Modelltest laeuft noch. Bitte kurz warten und die Box danach erneut ziehen.";
            return;
        }
        if (_saveInProgress)
        {
            StatusText = "Speichern laeuft noch. Bitte die Box danach erneut ziehen.";
            return;
        }
        if (_isCheckingPhoto)
        {
            StatusText = "Die allgemeine Foto-Pruefung laeuft noch. Bitte kurz warten.";
            return;
        }
        if (_isStartingAi)
        {
            StatusText = "KI startet noch. Bitte kurz warten und die Box danach erneut ziehen.";
            return;
        }

        // Eigenes CTS je Aufruf; einen laufenden Vorgaenger abbrechen (KEIN geteilter Abbruch).
        _boxCts?.Cancel();
        var cts = new CancellationTokenSource();
        _boxCts = cts;
        var ct = cts.Token;

        // Solange der Box-Lauf laeuft (Maske/Vorschlag noch offen), ist Speichern gesperrt —
        // sonst wird ein Entwurf ohne gepruefte Maske als vermeintlich fertiger Fund gespeichert.
        _boxRunActive = true;

        // Eine neue Box darf nie zusammen mit der Maske/dem Vorschlag der alten Box
        // sichtbar sein. Sonst wirken beide geometrisch gegeneinander verschoben.
        Segmentation = null;
        Suggestion = null;
        CurrentBox = box;
        IsBusy = true;
        try
        {
            // Eine bereits sichtbare Hand-/PDF-Auswahl ist der beste SAM-Hinweis.
            // Suggestion wurde fuer die neue Box oben absichtlich geloescht und darf
            // deshalb hier nicht als Quelle verwendet werden.
            var codeHint = _textPresenter.NormalizeCode(SelectedCode);
            if (string.IsNullOrWhiteSpace(codeHint))
                codeHint = "damage";
            var result = await _boxAnalysis.AnalyzeAsync(item, box, codeHint, ct);

            if (ct.IsCancellationRequested || !ReferenceEquals(CurrentItem, item))
                return;   // ein neuerer Lauf hat uebernommen

            ApplyCompletedBoxResults(result);
            if (result.Failure == TrainingStudioBoxAnalysisFailure.None)
            {
                StatusText = IsStrictReviewQueue
                    ? BuildSegmentationRepairMaskStatus(saveAttempt: false)
                    : Suggestion is { FrameUsable: false }
                        ? $"Frame nicht verwertbar: {Suggestion.QualityReason}"
                        : Segmentation?.StatusText ?? "KI-Vorschlag erstellt.";
            }
            else if (result.Failure == TrainingStudioBoxAnalysisFailure.SidecarUnavailable)
            {
                UserError.DescribeAndReport(result.Error!, "Training-Studio Segmentierung");
                StatusText = BuildPartialResultPrefix()
                    + "Lokaler KI-Dienst ist nicht erreichbar. Bitte oben 'KI starten' wählen und die Box erneut ziehen.";
            }
            else
            {
                StatusText = BuildPartialResultPrefix() + "KI-Vorschlag/Maske nicht möglich: "
                    + UserError.DescribeAndReport(result.Error!, "Training-Studio Segmentierung");
            }
        }
        catch (OperationCanceledException)
        {
            // Abgebrochen durch einen neuen Box-Lauf: Zustand nicht uebernehmen.
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

    private void ApplyCompletedBoxResults(TrainingStudioBoxAnalysisResult result)
    {
        Segmentation = result.Segmentation;

        if (result.Suggestion is null)
            return;

        Suggestion = result.Suggestion;
        var top = Suggestion.Candidates.FirstOrDefault()?.VsaCode;
        if (string.IsNullOrWhiteSpace(SelectedCode) && !string.IsNullOrWhiteSpace(top))
        {
            SelectedCode = _textPresenter.NormalizeCode(top);
            Beschreibung = _textPresenter.BuildBeschreibungVorlage(SelectedCode, ClockPosition);
        }
    }

    private string BuildPartialResultPrefix()
    {
        if (Segmentation is not null)
            return "Maske ist sichtbar. ";
        if (Suggestion is not null)
            return "KI-Vorschlag ist sichtbar. ";
        return string.Empty;
    }

    [RelayCommand]
    private Task AcceptAsync() => SaveInternalAsync(asCorrection: false);

    [RelayCommand]
    private Task CorrectAsync() => SaveInternalAsync(asCorrection: true);

    [RelayCommand]
    private void Discard()
    {
        if (IsAwaitingImageCompletion)
        {
            StatusText =
                "Das gespeicherte Ereignis kann hier nicht verworfen werden. Bitte 'Bild fertig' " +
                "oder 'Weiteres Ereignis auf diesem Bild' waehlen.";
            return;
        }

        var persistedDraft = !string.IsNullOrWhiteSpace(CurrentItem?.ExistingSampleId)
            && (_activeDraftStartedAsNewObject || _newDraftQueueItemIndices.Contains(CurrentIndex));
        CancelBoxRunForImageChange();
        CurrentBox = null;
        Segmentation = null;
        Suggestion = null;
        SelectedCode = null;
        Beschreibung = string.Empty;
        if (SavedEventCountForCurrentImage > 0)
        {
            IsAwaitingImageCompletion = true;
            StatusText = persistedDraft
                ? "Markierung geschlossen. Der bereits gespeicherte Entwurf bleibt unter " +
                  "'Unvollstaendige Goldframes' fuer eine spaetere Reparatur erhalten. " +
                  "Das bestaetigte Ereignis auf diesem Bild bleibt ebenfalls erhalten."
                : "Zusaetzliche Markierung verworfen. Das bereits gespeicherte Ereignis bleibt erhalten. " +
                  "Jetzt erneut 'Weiteres Ereignis auf diesem Bild' oder 'Bild fertig' waehlen.";
        }
        else
        {
            StatusText = persistedDraft
                ? "Markierung geschlossen. Der bereits gespeicherte Entwurf bleibt unter " +
                  "'Unvollstaendige Goldframes' fuer eine spaetere Reparatur erhalten."
                : "Markierung verworfen.";
        }
    }

    /// <summary>Uebernimmt einen Vorschlags-Code als aktuellen Code (Klick auf Kandidat).</summary>
    [RelayCommand]
    private void SelectCode(string? code)
    {
        if (IsAwaitingImageCompletion)
        {
            StatusText =
                "Dieses Ereignis ist bereits gespeichert. Bitte 'Weiteres Ereignis auf diesem Bild' " +
                "oder 'Bild fertig' waehlen.";
            return;
        }

        if (!string.IsNullOrWhiteSpace(code))
        {
            SelectedCode = _textPresenter.NormalizeCode(code);
            Beschreibung = _textPresenter.BuildBeschreibungVorlage(SelectedCode, ClockPosition);
        }
    }

    /// <summary>
    /// Prueft das vollstaendige aktuelle Foto mit dem allgemeinen Klassifikator und
    /// zeigt VSA-Kandidaten. Hand-Box, Maske, Codierung und Beschreibung bleiben unveraendert.
    /// Gespeichert wird ausschliesslich ueber Akzeptieren/Korrigieren.
    /// </summary>
    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task FotoMitKiPruefenAsync(CancellationToken cancellationToken)
    {
        if (IsAwaitingImageCompletion)
        {
            StatusText =
                "Bitte zuerst 'Bild fertig' oder 'Weiteres Ereignis auf diesem Bild' waehlen.";
            return;
        }

        var item = CurrentItem;
        if (item is null)
        {
            StatusText = "Bitte zuerst ein Foto laden.";
            return;
        }
        if (IsBusy)
        {
            StatusText = "Bitte warten, bis der laufende KI-Schritt fertig ist.";
            return;
        }

        Suggestion = null;
        _isCheckingPhoto = true;
        IsBusy = true;
        StatusText = "Das ganze Foto wird mit KI geprueft …";
        try
        {
            var suggestion = await _workbench
                .SuggestPhotoAsync(item, cancellationToken)
                .ConfigureAwait(true);

            // Der Nutzer kann waehrend des KI-Aufrufs ein anderes Bild laden.
            // Ein spaetes Ergebnis darf niemals beim neuen Foto erscheinen.
            if (!ReferenceEquals(CurrentItem, item))
                return;

            Suggestion = suggestion;

            if (!suggestion.ModelAvailable)
            {
                var reason = string.IsNullOrWhiteSpace(suggestion.UnavailableReason)
                    ? "Das Klassifikationsmodell ist nicht geladen."
                    : suggestion.UnavailableReason;
                StatusText = $"KI-Modell nicht verfuegbar: {reason} Nichts gespeichert.";
                return;
            }
            if (!suggestion.FrameUsable)
            {
                StatusText = $"Foto nicht verwertbar: {suggestion.QualityReason} Nichts gespeichert.";
                return;
            }

            var top = suggestion.Candidates.FirstOrDefault();
            if (top is null)
            {
                StatusText = suggestion.IsBend
                    ? "Bogen erkannt, aber kein sicherer VSA-Code vorgeschlagen. Nichts gespeichert."
                    : "Kein sicherer VSA-Vorschlag gefunden. Nichts gespeichert.";
                return;
            }

            StatusText =
                $"KI-Vorschlag: {_textPresenter.NormalizeCode(top.VsaCode)} — {_textPresenter.ResolveCodeLabel(top.VsaCode)} " +
                $"({top.Confidence:P0}). Zum Uebernehmen anklicken. Nichts gespeichert.";
        }
        catch (OperationCanceledException)
        {
            if (ReferenceEquals(CurrentItem, item))
                StatusText = "Foto-Pruefung abgebrochen. Nichts gespeichert.";
        }
        catch (IOException ex)
        {
            UserError.DescribeAndReport(ex, "Training-Studio Foto lesen");
            if (ReferenceEquals(CurrentItem, item))
            {
                StatusText =
                    "Foto konnte nicht gelesen werden. Bitte die Datei pruefen. Nichts gespeichert.";
            }
        }
        catch (SidecarUnavailableException ex)
        {
            UserError.DescribeAndReport(ex, "Training-Studio allgemeine Foto-Pruefung");
            if (ReferenceEquals(CurrentItem, item))
            {
                StatusText =
                    "Lokaler KI-Dienst ist nicht erreichbar. Bitte oben 'KI starten' waehlen. Nichts gespeichert.";
            }
        }
        catch (Exception ex)
        {
            var error = UserError.DescribeAndReport(ex, "Training-Studio allgemeine Foto-Pruefung");
            if (ReferenceEquals(CurrentItem, item))
                StatusText = "Foto-Pruefung fehlgeschlagen: " + error;
        }
        finally
        {
            _isCheckingPhoto = false;
            IsBusy = false;
        }
    }

    /// <summary>Setzt die Schadensstufe (1..5) aus dem Button-Parameter; sonst keine Aenderung.</summary>
    [RelayCommand]
    private void SetSeverity(string? level)
    {
        if (IsAwaitingImageCompletion)
        {
            StatusText =
                "Dieses Ereignis ist bereits gespeichert. Bitte 'Weiteres Ereignis auf diesem Bild' " +
                "oder 'Bild fertig' waehlen.";
            return;
        }

        Severity = int.TryParse(level, out var s) && s is >= 1 and <= 5 ? s : Severity;
    }

    [RelayCommand]
    private async Task NextItem()
    {
        if (IsAwaitingImageCompletion)
        {
            StatusText =
                "Das Ereignis ist gespeichert. Bitte ausdruecklich 'Bild fertig' oder " +
                "'Weiteres Ereignis auf diesem Bild' waehlen.";
            return;
        }

        if (SavedEventCountForCurrentImage > 0)
        {
            StatusText =
                "Ein weiteres Ereignis auf diesem Bild ist noch offen. Bitte speichern oder verwerfen; " +
                "danach 'Bild fertig' waehlen.";
            return;
        }

        if (IsStrictReviewQueue)
        {
            StatusText =
                "In dieser Gold-Warteschlange geht es erst nach einer gültigen Maske und persönlichem Akzeptieren weiter.";
            return;
        }
        if (_saveInProgress || (IsStrictReviewQueue && IsBusy))
        {
            StatusText = "Die KI arbeitet noch. Bitte kurz warten.";
            return;
        }

        var nextIndex = FindNextOpenQueueItemIndex(CurrentIndex + 1);
        if (nextIndex >= 0)
        {
            CurrentIndex = nextIndex;
            ResetForCurrent();
            await PrepareCurrentStrictReviewItemAsync(CancellationToken.None);
        }
        else
        {
            var firstOpenIndex = FindNextOpenQueueItemIndex(0);
            if (firstOpenIndex >= 0 && firstOpenIndex != CurrentIndex)
            {
                CurrentIndex = firstOpenIndex;
                ResetForCurrent();
                await PrepareCurrentStrictReviewItemAsync(CancellationToken.None);
            }
            else
            {
                StatusText = IsStrictReviewQueue
                    ? "Ende der Liste. Nicht bestätigte Bilder bleiben offen und erscheinen beim nächsten Laden wieder."
                    : "Ende der offenen Warteschlange.";
            }
        }
    }

    [RelayCommand]
    private async Task PreviousItem()
    {
        if (IsAwaitingImageCompletion)
        {
            StatusText =
                "Bitte zuerst 'Bild fertig' oder 'Weiteres Ereignis auf diesem Bild' waehlen.";
            return;
        }

        if (SavedEventCountForCurrentImage > 0)
        {
            StatusText =
                "Ein weiteres Ereignis auf diesem Bild ist noch offen. Bitte speichern oder verwerfen; " +
                "danach 'Bild fertig' waehlen.";
            return;
        }

        if (IsStrictReviewQueue)
        {
            StatusText =
                "Bereits abgeschlossene Bilder bleiben gesperrt. Diese Gold-Warteschlange wird der Reihe nach abgearbeitet.";
            return;
        }

        if (_saveInProgress || (IsStrictReviewQueue && IsBusy))
        {
            StatusText = "Die KI arbeitet noch. Bitte kurz warten.";
            return;
        }

        var previousIndex = FindPreviousOpenQueueItemIndex(CurrentIndex - 1);
        if (previousIndex >= 0)
        {
            CurrentIndex = previousIndex;
            ResetForCurrent();
            await PrepareCurrentStrictReviewItemAsync(CancellationToken.None);
        }
        else if (CurrentIndex > 0)
        {
            StatusText =
                "Die vorherigen Bilder sind bereits abgeschlossen. Fertige Goldfaelle werden hier nicht erneut geoeffnet.";
        }
    }

    private async Task SaveInternalAsync(bool asCorrection)
    {
        if (IsAwaitingImageCompletion)
        {
            StatusText =
                "Dieses Ereignis ist bereits gespeichert. Bitte 'Weiteres Ereignis auf diesem Bild' " +
                "oder 'Bild fertig' waehlen.";
            return;
        }

        if (CurrentItem is null)
        {
            StatusText = Items.Count == 0
                ? "Bitte zuerst ein Foto laden."
                : "Warteschlange abgearbeitet.";
            return;
        }

        if (_isRunningPreviewDetection)
        {
            StatusText = "Der Modelltest laeuft noch. Bitte vor dem Speichern kurz warten.";
            return;
        }

        // Akzeptieren/Korrigieren ist erst moeglich, wenn Maske und Vorschlag fertig sind.
        if (_boxRunActive)
        {
            StatusText = "Segmentierung laeuft noch — bitte kurz warten.";
            return;
        }

        // Doppeltes Ausloesen (zweites Akzeptieren/Korrigieren waehrend des ersten Speicherns)
        // darf denselben Fund nicht zweimal durch den Gold-Pfad schieben.
        if (_saveInProgress)
        {
            StatusText = "Speichern laeuft bereits — bitte kurz warten.";
            return;
        }

        if (IsBusy)
        {
            StatusText = "Ein anderer KI-Schritt laeuft noch. Bitte vor dem Speichern kurz warten.";
            return;
        }

        var item = CurrentItem;
        var normalizedCode = _textPresenter.NormalizeCode(SelectedCode);
        if (item is null || CurrentBox is null || string.IsNullOrWhiteSpace(normalizedCode))
        {
            StatusText = "Zum Speichern fehlen Box oder Code.";
            return;
        }

        if (!CanSaveCurrentSegmentationRepairItem())
            return;

        var topCode = Suggestion?.Candidates.FirstOrDefault()?.VsaCode;
        // Bei einem PDF-Operateurbefund ist dessen sichtbarer Code die Referenz.
        // Die unabhaengige KI bleibt nur ein Vergleich und darf eine bestaetigte
        // Operateurvorgabe weder ueberschreiben noch als "Korrektur" umdeuten.
        var referenceCode = item.ExistingCode ?? item.SourceSuggestion?.VsaCode ?? topCode;
        var wasCorrected = !string.IsNullOrWhiteSpace(referenceCode)
            && !string.Equals(normalizedCode, referenceCode, StringComparison.OrdinalIgnoreCase);
        SelectedCode = normalizedCode;
        var decision = new WorkbenchDecision(normalizedCode, wasCorrected, Beschreibung, ClockPosition, Severity, _confirmedByUser);
        var queueVersion = Volatile.Read(ref _queueVersion);

        _saveInProgress = true;
        IsBusy = true;
        var prepareNextRepairItem = false;
        try
        {
            var result = await _workbench.SaveAsync(item, CurrentBox.Value, Segmentation, decision);
            if (queueVersion != Volatile.Read(ref _queueVersion)
                || !ReferenceEquals(CurrentItem, item))
            {
                StatusText = result.Saved
                    ? "Das vorherige Bild wurde gespeichert; die neue Warteschlange bleibt unverändert."
                    : $"Das vorherige Bild wurde nicht gespeichert: {result.RefusalReason}";
                return;
            }
            if (result.Saved)
            {
                if (!result.GoldApproved)
                {
                    BindSavedDraftToCurrentItem(item, result);
                    StatusText =
                        "Noch nicht als Gold abgeschlossen: " +
                        (result.RefusalReason ?? "Die gespeicherte Maske hat das Gold-Gate nicht bestanden.") +
                        " Das Bild bleibt zur Korrektur geöffnet.";
                    await RefreshGoldProgressCoreAsync(CancellationToken.None);
                    return;
                }

                if (IsGoldQualityReviewQueue)
                {
                    try
                    {
                        if (_goldQualityReview is null
                            || string.IsNullOrWhiteSpace(_activeGoldQualityReviewSessionId)
                            || string.IsNullOrWhiteSpace(item.ExistingSampleId))
                        {
                            throw new InvalidOperationException(
                                "Die aktive Goldpruefungs-Sitzung ist nicht vollstaendig gebunden.");
                        }

                        await _goldQualityReview.MarkCompletedAsync(
                            new GoldQualityReviewCompletionRequest(
                                _activeGoldQualityReviewSessionId,
                                item.ExistingSampleId,
                                _confirmedByUser),
                            CancellationToken.None);
                    }
                    catch (Exception ex)
                    {
                        StatusText =
                            "Das Goldsample wurde gespeichert, aber der Pruefungsfortschritt nicht. " +
                            "Bitte die Goldpruefung neu laden und diesen Fall erneut bestaetigen: " +
                            UserError.DescribeAndReport(ex, "Training-Studio Goldpruefung abschliessen");
                        await RefreshGoldProgressCoreAsync(CancellationToken.None);
                        return;
                    }
                }

                var savedStatus = result.RefusalReason is null
                    ? $"Gespeichert + {result.KbIndexState}."
                    : $"Gespeichert ({result.KbIndexState}) — Hinweis: {result.RefusalReason}";
                if (wasCorrected)
                    savedStatus = $"Korrektur gespeichert und an die Lerndaten zurueckgegeben. {savedStatus}";
                else if (asCorrection)
                    savedStatus = $"Code entspricht dem KI-Vorschlag und wurde bestaetigt. {savedStatus}";

                await RefreshGoldProgressCoreAsync(CancellationToken.None);
                BindOpenPdfReferencesToStoredImage(item, result.StoredImageSha256);
                if (CanOfferMultipleObjects(item))
                {
                    OpenImageCompletionChoice(savedStatus, result.StoredImageSha256);
                }
                else
                {
                    if (!TryMarkCurrentQueueItemCompleted())
                    {
                        StatusText =
                            "Dieses Bild war bereits abgeschlossen und wurde nicht doppelt gezaehlt.";
                        return;
                    }
                    QueueDoneCount = Math.Min(QueueDoneCount + 1, QueueTotalCount);
                    prepareNextRepairItem = MoveToNextItemAfterSave(savedStatus);
                }
            }
            else
            {
                // Abweisung ist immer sichtbar, nie still.
                StatusText = $"Nicht gespeichert: {result.RefusalReason}";
            }
        }
        catch (Exception ex)
        {
            // Store-/KB-/Netzwerkfehler: sichtbare Meldung statt App-Absturz.
            StatusText = "Nicht gespeichert: "
                + UserError.DescribeAndReport(ex, "Training-Studio speichern");
        }
        finally
        {
            _saveInProgress = false;
            IsBusy = false;
        }

        if (prepareNextRepairItem)
            await PrepareCurrentStrictReviewItemAsync(CancellationToken.None);
    }

    private async Task RefreshGoldProgressCoreAsync(CancellationToken cancellationToken)
    {
        if (_loadGoldProgress is null)
        {
            GoldProgressSummary = "Goldstand ist nur im laufenden SewerStudio verfügbar.";
            return;
        }

        try
        {
            var progress = await _loadGoldProgress(cancellationToken);
            GoldProgressItems = new ObservableCollection<PersonalGoldMainCodeStatus>(progress);
            var ready = progress.Count(item => item.Status is "ready" or "above_target");
            var full = progress.Sum(item => item.FullGoldSamples);
            var personal = progress.Sum(item => item.PersonalSamples);
            var incomplete = Math.Max(0, personal - full);
            GoldProgressSummary =
                $"{ready}/{progress.Count} Hauptcodes bei mindestens 30 · " +
                $"{full} vollständige Goldframes · {incomplete} unvollständig";
        }
        catch (OperationCanceledException)
        {
            // Beim Schliessen oder erneuten Laden ist keine Meldung noetig.
        }
        catch (Exception ex)
        {
            GoldProgressSummary = "Goldstand konnte nicht gelesen werden: "
                + UserError.DescribeAndReport(ex, "Training-Studio Goldstand");
        }
    }

    private void ResetForCurrent()
    {
        ResetMultiObjectStateForCurrentItem();
        CancelBoxRunForImageChange();
        PreviewDetections = new ObservableCollection<TrainingStudioPreviewDetectionItem>();
        PreviewDetectionSummary = SelectedPreviewModel is null
            ? "Bitte ein Modell wählen."
            : $"{SelectedPreviewModel.DisplayName}: bereit für einen reinen Fototest.";
        CurrentBox = IsStrictReviewQueue
            ? CurrentItem?.ExistingBox
            : null;
        Segmentation = IsGoldQualityReviewQueue
            ? CurrentItem?.ExistingSegmentation
            : null;
        Suggestion = null;
        var sourceSuggestion = CurrentItem?.SourceSuggestion;
        SelectedCode = _textPresenter.NormalizeCode(
            CurrentItem?.ExistingCode
            ?? sourceSuggestion?.VsaCode);
        Beschreibung = !string.IsNullOrWhiteSpace(CurrentItem?.ExistingBeschreibung)
            ? CurrentItem.ExistingBeschreibung
            : !string.IsNullOrWhiteSpace(sourceSuggestion?.Beschreibung)
                ? sourceSuggestion.Beschreibung
                : _textPresenter.BuildBeschreibungVorlage(SelectedCode, clock: null);
        ClockPosition = CurrentItem?.ExistingClockPosition;
        Severity = CurrentItem?.ExistingSeverity;
        CurrentImagePath = CurrentItem?.FramePath;
        if (CurrentItem is not null)
        {
            var folderHint = string.IsNullOrWhiteSpace(CurrentItem.SuggestedMainCode)
                ? string.Empty
                : $" · Ordnerhinweis: {PersonalGoldMainCodeCatalog.FormatDisplayName(
                    CurrentItem.SuggestedMainCode,
                    _codeLabelLookup)}";
            var sourceHint = sourceSuggestion is null
                ? string.Empty
                : $" · PDF-Referenz: {sourceSuggestion.VsaCode}, Seite {sourceSuggestion.PageNumber}" +
                  (string.IsNullOrWhiteSpace(sourceSuggestion.PhotoId)
                      ? string.Empty
                      : $", Foto {sourceSuggestion.PhotoId}");
            StatusText = $"Bild {CurrentIndex + 1} von {Items.Count}{folderHint}{sourceHint}";
        }
    }

}
