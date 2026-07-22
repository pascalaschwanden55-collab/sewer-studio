using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Application.Ai.Workbench;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Infrastructure.Ai;   // VsaCodeResolver (Default-Label-Lookup)

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

    private CancellationTokenSource? _boxCts;
    private bool _isStartingAi;

    public TrainingStudioViewModel(
        IAnnotationWorkbenchService workbench,
        Func<IReadOnlyList<WorkbenchItem>> loadQueue,
        string confirmedByUser,
        Func<string, string?>? codeLabelLookup = null,
        Func<IProgress<string>, CancellationToken, Task<(bool Ready, string StatusText)>>? ensureAiReady = null)
    {
        _workbench = workbench;
        _loadQueue = loadQueue;
        _confirmedByUser = confirmedByUser;
        _codeLabelLookup = codeLabelLookup ?? VsaCodeResolver.LookupLabel;
        _ensureAiReady = ensureAiReady;
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

    /// <summary>Optionaler Rohrdurchmesser in mm fuer neu geladene Fotos (leer = 300-mm-Default).</summary>
    [ObservableProperty] private string _pipeDiameterInput = string.Empty;

    /// <summary>Aktuell angezeigtes Item (oder null, wenn die Warteschlange leer/abgearbeitet ist).</summary>
    public WorkbenchItem? CurrentItem =>
        CurrentIndex >= 0 && CurrentIndex < Items.Count ? Items[CurrentIndex] : null;

    /// <summary>true = Sidecar meldet unbrauchbaren Frame oder Bogen-Veto (Warnhinweis anzeigen).</summary>
    public bool ShowQualityWarning => Suggestion is not null && (!Suggestion.FrameUsable || Suggestion.IsBend);

    /// <summary>Warntext zu Frame-Qualitaet/Bogen (leer, wenn keine Warnung).</summary>
    public string QualityWarning
    {
        get
        {
            if (Suggestion is null) return string.Empty;
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
    public void ApplyCodeSelection(string? code, double? clockPosition, int? severity)
    {
        if (!string.IsNullOrWhiteSpace(code))
            SelectedCode = NormalizeCode(code);
        if (clockPosition.HasValue)
            ClockPosition = clockPosition;
        if (severity.HasValue)
            Severity = severity;
        Beschreibung = BuildBeschreibungVorlage(SelectedCode, ClockPosition);
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
        try
        {
            var progress = new Progress<string>(message => StatusText = message);
            var result = await _ensureAiReady(progress, ct);
            StatusText = result.StatusText;
        }
        catch (OperationCanceledException)
        {
            StatusText = "KI-Start abgebrochen.";
        }
        catch (Exception ex)
        {
            StatusText = "KI konnte nicht gestartet werden: "
                + UserError.DescribeAndReport(ex, "Training-Studio KI-Start");
        }
        finally
        {
            _isStartingAi = false;
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

    [RelayCommand]
    private async Task BoxDrawnAsync(BoundingBox box)
    {
        var item = CurrentItem;
        if (item is null)
            return;
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
                IsBusy = false;
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
        SelectedCode = top;
        Beschreibung = BuildBeschreibungVorlage(top, ClockPosition);
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
    /// Fragt die KI nach der feinen Anschluss-Bauart und mischt die Kandidaten (Quelle "bca")
    /// in die Vorschlagsliste. Nur bei Anschluessen sinnvoll — der Nutzer loest ihn bewusst aus.
    /// </summary>
    [RelayCommand]
    private async Task BestimmeBauartAsync()
    {
        var item = CurrentItem;
        if (item is null)
        {
            StatusText = "Zuerst ein Bild laden und eine Box ziehen.";
            return;
        }
        if (!_workbench.BcaBauartVerfuegbar)
        {
            StatusText = "Bauart-Bestimmung nicht verfuegbar — KI (Qwen/Ollama) nicht gestartet oder deaktiviert.";
            return;
        }

        // Sichtbares Feedback: Busy-Anzeige + Statuszeile, damit der Knopf nie "still" wirkt.
        IsBusy = true;
        StatusText = "Anschluss-Bauart wird bestimmt — die KI wird gefragt …";
        try
        {
            using var cts = new CancellationTokenSource();
            var bauart = await _workbench.SuggestBcaBauartAsync(item, cts.Token).ConfigureAwait(true);
            if (bauart.Candidates.Count == 0)
            {
                StatusText = "Keine sichere Anschluss-Bauart erkannt (ist ein Anschluss im Bild?).";
                return;
            }

            // Bauart-Kandidaten in die bestehende Vorschlagsliste einmischen (Duplikate vermeiden).
            var vorhanden = Suggestion?.Candidates
                ?? (IReadOnlyList<WorkbenchCodeCandidate>)Array.Empty<WorkbenchCodeCandidate>();
            var merged = vorhanden
                .Concat(bauart.Candidates)
                .GroupBy(c => c.VsaCode, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.OrderByDescending(c => c.Confidence).First())
                .OrderByDescending(c => c.Confidence)
                .ToList();
            Suggestion = new WorkbenchSuggestion(
                merged,
                Suggestion?.FrameUsable ?? true,
                Suggestion?.QualityReason ?? string.Empty,
                Suggestion?.IsBend ?? false);
            StatusText = $"Anschluss-Bauart vorgeschlagen: {bauart.Candidates[0].VsaCode}.";
        }
        catch (Exception ex)
        {
            // Fehler nie verschlucken (async-Command), aber keine rohe Exception zeigen:
            // benutzerfreundlich beschreiben, volle Ursache nur protokollieren (UserError).
            StatusText = "Bauart-Bestimmung fehlgeschlagen: "
                + UserError.DescribeAndReport(ex, "Anschluss-Bauart bestimmen");
        }
        finally
        {
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
            IsBusy = false;
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
        CurrentBox = null;
        Segmentation = null;
        Suggestion = null;
        SelectedCode = null;
        Beschreibung = string.Empty;
        ClockPosition = null;
        Severity = null;
        CurrentImagePath = CurrentItem?.FramePath;
        if (CurrentItem is not null)
            StatusText = $"Bild {CurrentIndex + 1} von {Items.Count}";
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

    private string ResolveCodeLabel(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return string.Empty;

        return _codeLabelLookup(NormalizeCode(code)) ?? "Code nicht im VSA-Katalog gefunden";
    }

    private static string NormalizeCode(string? code)
        => code?.Trim().ToUpperInvariant() ?? string.Empty;
}
