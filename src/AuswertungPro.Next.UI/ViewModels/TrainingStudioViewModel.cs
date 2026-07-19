using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Application.Ai.Workbench;
using AuswertungPro.Next.Infrastructure.Ai;   // VsaCodeResolver (Default-Label-Lookup)

namespace AuswertungPro.Next.UI.ViewModels;

/// <summary>
/// Pruefplatz-ViewModel (Etappe 1): duenn ueber <see cref="IAnnotationWorkbenchService"/>.
/// Tastatur-Arbeitsfluss: Box ziehen → Maske + Vorschlag → Akzeptieren/Korrigieren → naechstes.
/// Jeder Box-Lauf hat sein EIGENES CancellationTokenSource (kein geteilter Abbruch).
/// </summary>
public sealed partial class TrainingStudioViewModel : ObservableObject
{
    private readonly IAnnotationWorkbenchService _workbench;
    private readonly Func<IReadOnlyList<WorkbenchItem>> _loadQueue;
    private readonly string _confirmedByUser;
    private readonly Func<string, string?> _codeLabelLookup;

    private CancellationTokenSource? _boxCts;

    public TrainingStudioViewModel(
        IAnnotationWorkbenchService workbench,
        Func<IReadOnlyList<WorkbenchItem>> loadQueue,
        string confirmedByUser,
        Func<string, string?>? codeLabelLookup = null)
    {
        _workbench = workbench;
        _loadQueue = loadQueue;
        _confirmedByUser = confirmedByUser;
        _codeLabelLookup = codeLabelLookup ?? VsaCodeResolver.LookupLabel;
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
    private WorkbenchSuggestion? _suggestion;

    [ObservableProperty] private string? _selectedCode;
    [ObservableProperty] private string _beschreibung = string.Empty;
    [ObservableProperty] private double? _clockPosition;
    [ObservableProperty] private int? _severity;
    [ObservableProperty] private string _statusText = string.Empty;
    [ObservableProperty] private bool _isBusy;
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

        // Eigenes CTS je Aufruf; einen laufenden Vorgaenger abbrechen (KEIN geteilter Abbruch).
        _boxCts?.Cancel();
        var cts = new CancellationTokenSource();
        _boxCts = cts;
        var ct = cts.Token;

        CurrentBox = box;
        IsBusy = true;
        try
        {
            var codeHint = Suggestion?.Candidates.FirstOrDefault()?.VsaCode ?? "damage";
            var segTask = _workbench.SegmentAsync(item, box, codeHint, ct);
            var sugTask = _workbench.SuggestAsync(item, box, ct);
            await Task.WhenAll(segTask, sugTask);

            if (ct.IsCancellationRequested)
                return;   // ein neuerer Lauf hat uebernommen

            Segmentation = segTask.Result;
            Suggestion = sugTask.Result;

            var top = Suggestion.Candidates.FirstOrDefault()?.VsaCode;
            SelectedCode = top;
            Beschreibung = BuildBeschreibungVorlage(top, ClockPosition);
            StatusText = Suggestion.FrameUsable
                ? Segmentation.StatusText
                : $"Frame nicht verwertbar: {Suggestion.QualityReason}";
        }
        catch (OperationCanceledException)
        {
            // Abgebrochen durch einen neuen Box-Lauf: Zustand nicht uebernehmen.
        }
        catch (Exception ex)
        {
            // Sidecar/Netzwerk/Modellfehler: sichtbare Meldung statt App-Absturz.
            if (_boxCts == cts)
                StatusText = $"KI-Vorschlag/Maske nicht moeglich: {ex.Message}";
        }
        finally
        {
            if (_boxCts == cts)
                IsBusy = false;
        }
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
            SelectedCode = code;
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
        if (item is null || CurrentBox is null || string.IsNullOrWhiteSpace(SelectedCode))
        {
            StatusText = "Zum Speichern fehlen Box oder Code.";
            return;
        }

        var topCode = Suggestion?.Candidates.FirstOrDefault()?.VsaCode;
        var wasCorrected = asCorrection && !string.Equals(SelectedCode, topCode, StringComparison.OrdinalIgnoreCase);
        var decision = new WorkbenchDecision(SelectedCode!, wasCorrected, Beschreibung, ClockPosition, Severity, _confirmedByUser);

        IsBusy = true;
        try
        {
            var result = await _workbench.SaveAsync(item, CurrentBox.Value, Segmentation, decision);
            if (result.Saved)
            {
                StatusText = result.RefusalReason is null
                    ? $"Gespeichert + {result.KbIndexState}."
                    : $"Gespeichert ({result.KbIndexState}) — Hinweis: {result.RefusalReason}";
                QueueDoneCount++;
                NextItem();
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
            StatusText = $"Nicht gespeichert: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
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
        var label = _codeLabelLookup(code) ?? code;
        return clock.HasValue
            ? $"{label} bei {clock.Value:0.#} Uhr — Ausmass ergaenzen"
            : $"{label} — Lage und Ausmass ergaenzen";
    }
}
