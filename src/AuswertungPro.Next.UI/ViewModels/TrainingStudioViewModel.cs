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
using AuswertungPro.Next.Infrastructure.Ai;   // VsaCodeResolver (Default-Label-Lookup)
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;
using AuswertungPro.Next.UI.Services;   // GoldBeschreibungGuard (Platzhalter-Schutz)

namespace AuswertungPro.Next.UI.ViewModels;

/// <summary>Darstellung eines KI-Kandidaten mit VSA-Code und lesbarem Katalogtext.</summary>
public sealed record TrainingStudioSuggestionItem(
    string VsaCode,
    string Klartext,
    double Confidence,
    string Quelle);

/// <summary>Auswahl eines Modells, das das Foto nur zur Vorschau prueft.</summary>
public sealed record TrainingStudioPreviewModelOption(
    TrainingPreviewModelKind Kind,
    string DisplayName);

/// <summary>Automatisch erkannte Vorschau-Box in echten Bildpixeln.</summary>
public sealed record TrainingStudioPreviewDetectionItem(
    double X1,
    double Y1,
    double X2,
    double Y2,
    string DisplayText,
    double Confidence);

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

    private CancellationTokenSource? _boxCts;
    private bool _boxRunActive;
    private bool _saveInProgress;
    private bool _isStartingAi;
    private bool _isCheckingPhoto;

    public TrainingStudioViewModel(
        IAnnotationWorkbenchService workbench,
        Func<IReadOnlyList<WorkbenchItem>> loadQueue,
        string confirmedByUser,
        Func<string, string?>? codeLabelLookup = null,
        Func<IProgress<string>, CancellationToken, Task<(bool Ready, string StatusText)>>? ensureAiReady = null,
        Func<CancellationToken, Task<IReadOnlyList<PersonalGoldMainCodeStatus>>>? loadGoldProgress = null,
        ITrainingPreviewDetectionService? previewDetection = null)
    {
        _workbench = workbench;
        _loadQueue = loadQueue;
        _confirmedByUser = confirmedByUser;
        _codeLabelLookup = codeLabelLookup ?? VsaCodeResolver.LookupLabel;
        _ensureAiReady = ensureAiReady;
        _loadGoldProgress = loadGoldProgress;
        _previewDetection = previewDetection;
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
    private ObservableCollection<WorkbenchItem> _items = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentItem))]
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
    private bool _isBusy;
    [ObservableProperty] private int _queueDoneCount;
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
        new(TrainingPreviewModelKind.BccTestCandidate, "BCC-Testmodell (nicht aktiv)"),
    ];

    /// <summary>Optionaler Rohrdurchmesser in mm fuer neu geladene Fotos (leer = 300-mm-Default).</summary>
    [ObservableProperty] private string _pipeDiameterInput = string.Empty;

    /// <summary>Aktuell angezeigtes Item (oder null, wenn die Warteschlange leer/abgearbeitet ist).</summary>
    public WorkbenchItem? CurrentItem =>
        CurrentIndex >= 0 && CurrentIndex < Items.Count ? Items[CurrentIndex] : null;

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
                ResolveCodeLabel(candidate.VsaCode),
                candidate.Confidence,
                candidate.Quelle))
            .ToArray()
        ?? Array.Empty<TrainingStudioSuggestionItem>();

    /// <summary>Lesbare Bedeutung des aktuell eingetragenen Codes.</summary>
    public string SelectedCodeLabel => ResolveCodeLabel(SelectedCode);

    partial void OnSelectedCodeChanging(string? oldValue, string? newValue)
    {
        var previousTemplate = BuildBeschreibungVorlage(oldValue, ClockPosition);
        if (string.IsNullOrWhiteSpace(Beschreibung)
            || string.Equals(Beschreibung, previousTemplate, StringComparison.Ordinal))
        {
            Beschreibung = BuildBeschreibungVorlage(newValue, ClockPosition);
        }
    }

    /// <summary>Optionaler Rohrdurchmesser als Zahl (null, wenn leer/ungueltig → Service nutzt 300 mm).</summary>
    public int? PipeDiameterMm =>
        int.TryParse(PipeDiameterInput, out var dn) && dn > 0 ? dn : null;

    /// <summary>Uebernimmt eine extern erzeugte Item-Liste (z. B. Fotos aus dem Dateidialog).</summary>
    public void LoadItems(IReadOnlyList<WorkbenchItem> items)
    {
        Items = new ObservableCollection<WorkbenchItem>(items);
        CurrentIndex = Items.Count > 0 ? 0 : -1;
        QueueDoneCount = 0;
        ResetForCurrent();
        if (Items.Count == 0)
            StatusText = "Keine Bilder geladen.";
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
        if (!string.IsNullOrWhiteSpace(code))
            SelectedCode = NormalizeCode(code);
        if (clockPosition.HasValue)
            ClockPosition = clockPosition;
        if (severity.HasValue)
            Severity = severity;
        // Die persoenlich bestaetigte Katalogauswahl liefert bereits eine fachliche
        // Beschreibung. Sie ersetzt nur ein leeres Feld oder den automatischen
        // Platzhalter; selbst geschriebener Text bleibt unangetastet.
        if (string.IsNullOrWhiteSpace(Beschreibung) || GoldBeschreibungGuard.IsPlaceholder(Beschreibung))
        {
            Beschreibung = BuildKatalogBeschreibung(
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

    private bool _standardModelMarkedUnavailable;

    /// <summary>
    /// Beschriftet das Standardmodell ehrlich um (nur Anzeige — die Auswahl bleibt
    /// moeglich, der Lauf zeigt dann die Sperr-Meldung statt Boxen). Einmalig.
    /// </summary>
    private void MarkStandardModelUnavailable()
    {
        if (_standardModelMarkedUnavailable)
            return;
        _standardModelMarkedUnavailable = true;
        PreviewModelOptions =
        [
            new(TrainingPreviewModelKind.ActiveStandard, "Aktives Standardmodell (nicht freigegeben)"),
            PreviewModelOptions[1],
        ];
        if (SelectedPreviewModel?.Kind == TrainingPreviewModelKind.ActiveStandard)
            SelectedPreviewModel = PreviewModelOptions[0];
    }

    partial void OnSelectedPreviewModelChanged(TrainingStudioPreviewModelOption? value)
    {
        PreviewDetections = new ObservableCollection<TrainingStudioPreviewDetectionItem>();
        PreviewDetectionSummary = value is null
            ? "Bitte ein Modell wählen."
            : $"{value.DisplayName}: bereit für einen reinen Fototest.";
    }

    /// <summary>
    /// Prüft das aktuelle Foto mit dem gewählten Modell. Die Treffer bleiben reine
    /// Vorschau und werden bewusst nie in CurrentBox oder ein Goldsample übernommen.
    /// </summary>
    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task RunPreviewDetectionAsync(CancellationToken cancellationToken)
    {
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
        if (IsBusy)
        {
            PreviewDetectionSummary = "Bitte warten, bis der laufende KI-Schritt fertig ist.";
            return;
        }

        // Das Standardmodell darf nur nach einer ausdruecklich positiven Qualifikation
        // blaue Vorschau-Boxen liefern. Fehlender/unlesbarer Status bleibt gesperrt.
        // Der getrennte BCC-Testkandidat ist davon nicht betroffen.
        if (model.Kind == TrainingPreviewModelKind.ActiveStandard)
        {
            var qualification = await _previewDetection
                .GetDetectorQualificationAsync(cancellationToken);
            if (qualification?.Qualified != true)
            {
                MarkStandardModelUnavailable();
                PreviewDetectionSummary = string.IsNullOrWhiteSpace(qualification?.Reason)
                    ? "Standardmodell gesperrt: Der Qualifikationsstatus konnte nicht sicher geprueft werden."
                    : $"Standardmodell gesperrt: {qualification.Reason}";
                StatusText = PreviewDetectionSummary;
                return;
            }
        }

        PreviewDetections = new ObservableCollection<TrainingStudioPreviewDetectionItem>();
        PreviewDetectionSummary = $"{model.DisplayName}: Foto wird geprüft …";
        StatusText = PreviewDetectionSummary;
        IsBusy = true;
        try
        {
            var result = await _previewDetection
                .DetectAsync(item.FramePath, model.Kind, 0.25, cancellationToken);
            if (!result.Available)
            {
                PreviewDetectionSummary = string.IsNullOrWhiteSpace(result.Error)
                    ? $"{model.DisplayName}: Modell ist nicht verfügbar."
                    : $"{model.DisplayName}: {result.Error}";
                StatusText = PreviewDetectionSummary;
                return;
            }

            var items = result.Detections
                .Select(detection =>
                {
                    var code = YoloClassVsaMapper.ToPersistableVsaCode(detection.ClassName);
                    var text = string.IsNullOrWhiteSpace(code)
                        ? detection.ClassName
                        : $"{code} — {ResolveCodeLabel(code)}";
                    return new TrainingStudioPreviewDetectionItem(
                        detection.X1,
                        detection.Y1,
                        detection.X2,
                        detection.Y2,
                        text,
                        detection.Confidence);
                })
                .ToArray();
            PreviewDetections = new ObservableCollection<TrainingStudioPreviewDetectionItem>(items);

            PreviewDetectionSummary = items.Length == 0
                ? $"{model.DisplayName}: kein Treffer. Nur Vorschau — nichts gespeichert."
                : $"{model.DisplayName}: {items.Length} Treffer. Blaue Boxen sind nur Vorschau und werden nicht gespeichert.";
            StatusText = PreviewDetectionSummary;
        }
        catch (OperationCanceledException)
        {
            PreviewDetectionSummary = "Modelltest abgebrochen.";
            StatusText = PreviewDetectionSummary;
        }
        catch (SidecarUnavailableException ex)
        {
            UserError.DescribeAndReport(ex, "Training-Studio Modelltest");
            PreviewDetectionSummary =
                "Lokaler KI-Dienst ist nicht erreichbar. Bitte oben 'KI starten' wählen.";
            StatusText = PreviewDetectionSummary;
        }
        catch (Exception ex)
        {
            PreviewDetectionSummary = "Modelltest nicht möglich: "
                + UserError.DescribeAndReport(ex, "Training-Studio Modelltest");
            StatusText = PreviewDetectionSummary;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void LoadQueue()
    {
        Items = new ObservableCollection<WorkbenchItem>(_loadQueue());
        CurrentIndex = Items.Count > 0 ? 0 : -1;
        QueueDoneCount = 0;
        ResetForCurrent();
        if (Items.Count == 0)
            StatusText = "Warteschlange leer.";
    }

    [RelayCommand(AllowConcurrentExecutions = false)]
    private Task RefreshGoldProgressAsync(CancellationToken cancellationToken)
        => RefreshGoldProgressCoreAsync(cancellationToken);

    [RelayCommand]
    private async Task BoxDrawnAsync(BoundingBox box)
    {
        var item = CurrentItem;
        if (item is null)
            return;
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
        Task<WorkbenchSegmentation>? segTask = null;
        Task<WorkbenchSuggestion>? sugTask = null;
        try
        {
            var codeHint = Suggestion?.Candidates.FirstOrDefault()?.VsaCode ?? "damage";
            segTask = _workbench.SegmentAsync(item, box, codeHint, ct);
            sugTask = _workbench.SuggestAsync(item, box, ct);
            await Task.WhenAll(segTask, sugTask);

            if (ct.IsCancellationRequested)
                return;   // ein neuerer Lauf hat uebernommen

            ApplyCompletedBoxResults(segTask, sugTask);

            StatusText = Suggestion is { FrameUsable: false }
                ? $"Frame nicht verwertbar: {Suggestion.QualityReason}"
                : Segmentation?.StatusText ?? "KI-Vorschlag erstellt.";
        }
        catch (OperationCanceledException)
        {
            // Abgebrochen durch einen neuen Box-Lauf: Zustand nicht uebernehmen.
        }
        catch (SidecarUnavailableException ex)
        {
            if (_boxCts == cts)
            {
                ApplyCompletedBoxResults(segTask, sugTask);
                var partial = BuildPartialResultPrefix();
                UserError.DescribeAndReport(ex, "Training-Studio Segmentierung");
                StatusText = partial
                    + "Lokaler KI-Dienst ist nicht erreichbar. Bitte oben 'KI starten' waehlen und die Box erneut ziehen.";
            }
        }
        catch (Exception ex)
        {
            // Sidecar/Netzwerk/Modellfehler: sichtbare Meldung statt App-Absturz.
            if (_boxCts == cts)
            {
                ApplyCompletedBoxResults(segTask, sugTask);
                StatusText = BuildPartialResultPrefix() + "KI-Vorschlag/Maske nicht moeglich: "
                    + UserError.DescribeAndReport(ex, "Training-Studio Segmentierung");
            }
        }
        finally
        {
            if (_boxCts == cts)
            {
                _boxRunActive = false;
                IsBusy = false;
            }
        }
    }

    private void ApplyCompletedBoxResults(
        Task<WorkbenchSegmentation>? segmentationTask,
        Task<WorkbenchSuggestion>? suggestionTask)
    {
        if (segmentationTask?.Status == TaskStatus.RanToCompletion)
            Segmentation = segmentationTask.Result;

        if (suggestionTask?.Status != TaskStatus.RanToCompletion)
            return;

        Suggestion = suggestionTask.Result;
        var top = Suggestion.Candidates.FirstOrDefault()?.VsaCode;
        if (string.IsNullOrWhiteSpace(SelectedCode) && !string.IsNullOrWhiteSpace(top))
        {
            SelectedCode = NormalizeCode(top);
            Beschreibung = BuildBeschreibungVorlage(SelectedCode, ClockPosition);
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
        CurrentBox = null;
        Segmentation = null;
        Suggestion = null;
        SelectedCode = null;
        Beschreibung = string.Empty;
        StatusText = "Markierung verworfen.";
    }

    /// <summary>Uebernimmt einen Vorschlags-Code als aktuellen Code (Klick auf Kandidat).</summary>
    [RelayCommand]
    private void SelectCode(string? code)
    {
        if (!string.IsNullOrWhiteSpace(code))
        {
            SelectedCode = NormalizeCode(code);
            Beschreibung = BuildBeschreibungVorlage(SelectedCode, ClockPosition);
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
                $"KI-Vorschlag: {NormalizeCode(top.VsaCode)} — {ResolveCodeLabel(top.VsaCode)} " +
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
        => Severity = int.TryParse(level, out var s) && s is >= 1 and <= 5 ? s : Severity;

    [RelayCommand]
    private void NextItem()
    {
        if (CurrentIndex < Items.Count - 1)
        {
            CurrentIndex++;
            ResetForCurrent();
        }
        else
        {
            StatusText = "Warteschlange abgearbeitet.";
        }
    }

    [RelayCommand]
    private void PreviousItem()
    {
        if (CurrentIndex > 0)
        {
            CurrentIndex--;
            ResetForCurrent();
        }
    }

    private async Task SaveInternalAsync(bool asCorrection)
    {
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

        var item = CurrentItem;
        var normalizedCode = NormalizeCode(SelectedCode);
        if (item is null || CurrentBox is null || string.IsNullOrWhiteSpace(normalizedCode))
        {
            StatusText = "Zum Speichern fehlen Box oder Code.";
            return;
        }

        var topCode = Suggestion?.Candidates.FirstOrDefault()?.VsaCode;
        var wasCorrected = !string.IsNullOrWhiteSpace(topCode)
            && !string.Equals(normalizedCode, topCode, StringComparison.OrdinalIgnoreCase);
        SelectedCode = normalizedCode;
        var decision = new WorkbenchDecision(normalizedCode, wasCorrected, Beschreibung, ClockPosition, Severity, _confirmedByUser);

        _saveInProgress = true;
        IsBusy = true;
        try
        {
            var result = await _workbench.SaveAsync(item, CurrentBox.Value, Segmentation, decision);
            if (result.Saved)
            {
                var savedStatus = result.RefusalReason is null
                    ? $"Gespeichert + {result.KbIndexState}."
                    : $"Gespeichert ({result.KbIndexState}) — Hinweis: {result.RefusalReason}";
                if (wasCorrected)
                    savedStatus = $"Korrektur gespeichert und an die Lerndaten zurueckgegeben. {savedStatus}";
                else if (asCorrection)
                    savedStatus = $"Code entspricht dem KI-Vorschlag und wurde bestaetigt. {savedStatus}";

                QueueDoneCount++;
                await RefreshGoldProgressCoreAsync(CancellationToken.None);
                MoveToNextItemAfterSave(savedStatus);
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

    private void MoveToNextItemAfterSave(string savedStatus)
    {
        if (CurrentIndex < Items.Count - 1)
        {
            CurrentIndex++;
            ResetForCurrent();
            StatusText = $"{savedStatus} Naechstes Bild: {CurrentIndex + 1} von {Items.Count}.";
            return;
        }

        StatusText = $"{savedStatus} Warteschlange abgearbeitet.";
    }

    private void ResetForCurrent()
    {
        PreviewDetections = new ObservableCollection<TrainingStudioPreviewDetectionItem>();
        PreviewDetectionSummary = SelectedPreviewModel is null
            ? "Bitte ein Modell wählen."
            : $"{SelectedPreviewModel.DisplayName}: bereit für einen reinen Fototest.";
        CurrentBox = null;
        Segmentation = null;
        Suggestion = null;
        SelectedCode = NormalizeCode(CurrentItem?.ExistingCode);
        Beschreibung = string.IsNullOrWhiteSpace(CurrentItem?.ExistingBeschreibung)
            ? BuildBeschreibungVorlage(SelectedCode, clock: null)
            : CurrentItem.ExistingBeschreibung;
        ClockPosition = null;
        Severity = null;
        CurrentImagePath = CurrentItem?.FramePath;
        if (CurrentItem is not null)
        {
            var folderHint = string.IsNullOrWhiteSpace(CurrentItem.SuggestedMainCode)
                ? string.Empty
                : $" · Ordnerhinweis: {PersonalGoldMainCodeCatalog.FormatDisplayName(
                    CurrentItem.SuggestedMainCode,
                    _codeLabelLookup)}";
            StatusText = $"Bild {CurrentIndex + 1} von {Items.Count}{folderHint}";
        }
    }

    private string BuildBeschreibungVorlage(string? code, double? clock)
    {
        if (string.IsNullOrWhiteSpace(code))
            return string.Empty;
        var normalizedCode = NormalizeCode(code);
        var label = _codeLabelLookup(normalizedCode) ?? normalizedCode;
        return clock.HasValue
            ? $"{label} bei {clock.Value:0.#} Uhr — Ausmass ergaenzen"
            : $"{label} — Lage und Ausmass ergaenzen";
    }

    private string BuildKatalogBeschreibung(
        string? code,
        string? katalogBeschreibung,
        double? clock,
        int? severity)
    {
        if (string.IsNullOrWhiteSpace(code))
            return string.Empty;

        var normalizedCode = NormalizeCode(code);
        var label = string.IsNullOrWhiteSpace(katalogBeschreibung)
            ? _codeLabelLookup(normalizedCode)
            : katalogBeschreibung.Trim();
        var beschreibung = string.IsNullOrWhiteSpace(label)
            ? $"VSA-Code {normalizedCode}"
            : $"{normalizedCode} — {label}";

        if (clock.HasValue)
            beschreibung += $", bei {clock.Value:0.#} Uhr";
        if (severity.HasValue)
            beschreibung += $", Schadensstufe {severity.Value}";

        return beschreibung;
    }

    private string ResolveCodeLabel(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return string.Empty;

        return _codeLabelLookup(NormalizeCode(code)) ?? "Code nicht im VSA-Katalog gefunden";
    }

    private static string NormalizeCode(string? code)
        => code?.Trim().ToUpperInvariant() ?? string.Empty;
}
