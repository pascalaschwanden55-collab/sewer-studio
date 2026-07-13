using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.Schatten;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AuswertungPro.Next.UI.ViewModels.Pages;

/// <summary>
/// Seite "Schattenauswertung": legt die eigenstaendige KI-/Regel-Auswertung neben die
/// menschliche Auswertung — rein lesend. Der Lauf schreibt ausschliesslich in die
/// Schatten-Datei im Projektordner, nie in Projektfelder (kein Dirty, kein Feld-Schreiben).
/// </summary>
public sealed partial class SchattenauswertungPageViewModel : ObservableObject
{
    private static readonly CultureInfo Ch = CultureInfo.GetCultureInfo("de-CH");

    private readonly Func<Project?> _getProject;
    private readonly ISchattenAuswertungStore _storeRepository;
    private readonly Func<ISchattenAuswertungService> _createService;
    private readonly Func<string?> _getProjectPath;

    private SchattenAuswertungStore _store = new();
    private string? _storeLoadError;
    private CancellationTokenSource? _cts;

    public ObservableCollection<SchattenauswertungRowVm> Rows { get; } = new();

    [ObservableProperty] private SchattenauswertungRowVm? _selectedRow;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _mitKi = true;
    [ObservableProperty] private string _statusText = "";
    [ObservableProperty] private string _fortschrittText = "";
    [ObservableProperty] private double _fortschrittWert;
    [ObservableProperty] private double _fortschrittMax = 1;
    [ObservableProperty] private string _zusammenfassung = "";
    [ObservableProperty] private string _storeWarnung = "";

    public IAsyncRelayCommand StartenCommand { get; }
    public IRelayCommand AbbrechenCommand { get; }
    public IRelayCommand NeuLadenCommand { get; }

    public SchattenauswertungPageViewModel(ShellViewModel shell, ServiceProvider sp)
        : this(
            getProject: () => (shell ?? throw new ArgumentNullException(nameof(shell))).Project,
            store: (sp ?? throw new ArgumentNullException(nameof(sp))).SchattenStore,
            createService: sp.CreateSchattenAuswertung,
            getProjectPath: () => sp.Settings.LastProjectPath)
    {
    }

    public SchattenauswertungPageViewModel(
        Func<Project?> getProject,
        ISchattenAuswertungStore store,
        Func<ISchattenAuswertungService> createService,
        Func<string?> getProjectPath)
    {
        _getProject = getProject ?? throw new ArgumentNullException(nameof(getProject));
        _storeRepository = store ?? throw new ArgumentNullException(nameof(store));
        _createService = createService ?? throw new ArgumentNullException(nameof(createService));
        _getProjectPath = getProjectPath ?? throw new ArgumentNullException(nameof(getProjectPath));

        StartenCommand = new AsyncRelayCommand(StartenAsync, () => !IsBusy);
        AbbrechenCommand = new RelayCommand(() => _cts?.Cancel(), () => IsBusy);
        NeuLadenCommand = new RelayCommand(Reload, () => !IsBusy);

        Reload();
    }

    partial void OnIsBusyChanged(bool value)
    {
        StartenCommand.NotifyCanExecuteChanged();
        AbbrechenCommand.NotifyCanExecuteChanged();
        NeuLadenCommand.NotifyCanExecuteChanged();
    }

    /// <summary>Gespeicherte Schatten-Ergebnisse laden und neben die Projektdaten legen.</summary>
    public void Reload()
    {
        _store = _storeRepository.Load(_getProjectPath(), out _storeLoadError);
        StoreWarnung = _storeLoadError is null
            ? ""
            : $"Schatten-Datei nicht lesbar — Lauf-Ergebnisse werden NICHT gespeichert: {_storeLoadError}";
        BaueRows();
        StatusText = _store.LetzterLaufUtc is { } t
            ? $"Letzter Lauf: {t.ToLocalTime():dd.MM.yyyy HH:mm}" + (_store.KiModell is { Length: > 0 } m ? $" · KI: {m}" : " · nur Regeln")
            : "Noch kein Lauf für dieses Projekt.";
    }

    private async Task StartenAsync()
    {
        var projekt = _getProject();
        if (projekt is null || projekt.Data.Count == 0)
        {
            StatusText = "Kein Projekt mit Haltungen geladen.";
            return;
        }

        _cts = new CancellationTokenSource();
        IsBusy = true;
        FortschrittWert = 0;
        FortschrittMax = projekt.Data.Count;
        StatusText = MitKi ? "Schattenauswertung läuft (Regeln + KI)…" : "Schattenauswertung läuft (nur Regeln)…";

        // KI-Indikator der Shell nur anwerfen, wenn wirklich ein LLM-Teil laeuft.
        using var kiAnzeige = MitKi ? AiActivityTracker.Begin("Schattenauswertung") : null;

        var progress = new Progress<SchattenFortschritt>(f =>
        {
            FortschrittText = $"{f.Phase} {f.Aktuell}/{f.Gesamt} — {f.Haltung}";
            FortschrittWert = f.Aktuell;
            FortschrittMax = Math.Max(1, f.Gesamt);
        });

        try
        {
            var service = _createService();
            var store = await service.BerechneAsync(
                projekt,
                MitKi,
                progress,
                zwischenspeichern: SpeichereStoreStill,
                _cts.Token);

            _store = store;
            SpeichereStoreStill(store);
            BaueRows();
            StatusText = _cts.IsCancellationRequested
                ? "Abgebrochen — bereits gerechnete Ergebnisse sind gespeichert."
                : "Lauf abgeschlossen.";
        }
        catch (OperationCanceledException)
        {
            StatusText = "Abgebrochen — bereits gerechnete Ergebnisse sind gespeichert.";
            Reload();
        }
        catch (Exception ex)
        {
            BestEffort.ReportWarning($"[Schattenauswertung] Lauf fehlgeschlagen: {ex}");
            StatusText = "Schattenauswertung fehlgeschlagen. Details stehen im Programmlog.";
        }
        finally
        {
            FortschrittText = "";
            IsBusy = false;
            _cts.Dispose();
            _cts = null;
        }
    }

    /// <summary>
    /// Persistiert den Zwischenstand. Bewusst still (kein Status-Gezappel aus dem
    /// Hintergrund-Thread); bei Lesefehler der Datei wird NICHT gespeichert, damit eine
    /// nur gesperrte/beschaedigte Datei nicht endgueltig ueberschrieben wird (K3-Regel).
    /// </summary>
    private void SpeichereStoreStill(SchattenAuswertungStore store)
    {
        if (_storeLoadError is not null)
            return;
        _storeRepository.Save(_getProjectPath(), store, out _);
    }

    private void BaueRows()
    {
        Rows.Clear();
        var projekt = _getProject();
        if (projekt is null)
        {
            Zusammenfassung = "";
            return;
        }

        var abweichend = 0;
        var veraltet = 0;
        var ohneCodierung = 0;

        foreach (var record in projekt.Data)
        {
            var name = record.GetFieldValue("Haltungsname");
            if (string.IsNullOrWhiteSpace(name))
                name = record.Id.ToString();

            _store.ByHaltung.TryGetValue(name, out var ergebnis);
            var row = SchattenauswertungRowVm.Erstelle(name, record, ergebnis);
            Rows.Add(row);

            if (row.AbweichungKey is "Stark" or "Leicht") abweichend++;
            if (row.IstVeraltet) veraltet++;
            if (ergebnis?.Status == SchattenStatus.OhneCodierung) ohneCodierung++;
        }

        Zusammenfassung = Rows.Count == 0
            ? ""
            : $"{Rows.Count} Haltungen · {abweichend} abweichend · {veraltet} veraltet · {ohneCodierung} ohne Codierung";
    }

    internal static string FormatKosten(decimal? wert)
        => wert is { } w ? w.ToString("N0", Ch) : "–";
}

/// <summary>Eine Vergleichszeile: menschliche Auswertung links, Schatten rechts.</summary>
public sealed class SchattenauswertungRowVm
{
    public string Haltung { get; private init; } = "";

    // Mensch (aus den Projektfeldern, nur gelesen)
    public string MenschKlasse { get; private init; } = "";
    public string MenschMassnahme { get; private init; } = "";
    public string MenschKosten { get; private init; } = "";

    // Schatten (aus dem Store)
    public string SchattenKlasse { get; private init; } = "";
    public string SchattenNotenTooltip { get; private init; } = "";
    public string SchattenMassnahme { get; private init; } = "";
    public string SchattenKosten { get; private init; } = "";
    public string StatusText { get; private init; } = "";
    public bool IstVeraltet { get; private init; }

    // Vergleich
    public string AbweichungKey { get; private init; } = "Kein"; // Gleich | Leicht | Stark | Kein
    public string AbweichungTooltip { get; private init; } = "";

    // Detailbereich
    public string KiBegruendung { get; private init; } = "";
    public string RisikoFlagsText { get; private init; } = "";
    public string KostenbandText { get; private init; } = "";
    public string KiConfidenceText { get; private init; } = "";
    public string KiFehler { get; private init; } = "";
    public bool HatDetail => KiBegruendung.Length > 0 || RisikoFlagsText.Length > 0 || KiFehler.Length > 0;

    public static SchattenauswertungRowVm Erstelle(
        string name,
        HaltungRecord record,
        SchattenHaltungErgebnis? ergebnis)
    {
        var menschKlasse = record.GetFieldValue("Zustandsklasse").Trim();
        var menschMassnahme = record.GetFieldValue("Empfohlene_Sanierungsmassnahmen").Trim();
        var menschKosten = record.GetFieldValue("Kosten").Trim();

        var schattenMassnahme = ergebnis?.KiMassnahme
            ?? ergebnis?.RegelMassnahmen.FirstOrDefault()
            ?? "";
        var schattenKosten = ergebnis?.KostenErwartet ?? ergebnis?.RegelKosten;

        var abweichung = ergebnis is null || ergebnis.Status == SchattenStatus.OhneCodierung
            ? SchattenAbweichung.KeinVergleich
            : SchattenVergleich.Bewerte(
                menschKlasse, menschMassnahme, menschKosten,
                ergebnis.Zustandsklasse, schattenMassnahme, schattenKosten);

        var istVeraltet = ergebnis is not null
            && ergebnis.Status != SchattenStatus.OhneCodierung
            && !string.Equals(ergebnis.CodierungsHash, SchattenCodierungsHash.Compute(record), StringComparison.Ordinal);

        return new SchattenauswertungRowVm
        {
            Haltung = name,
            MenschKlasse = menschKlasse,
            MenschMassnahme = menschMassnahme,
            MenschKosten = menschKosten,
            SchattenKlasse = ergebnis?.Zustandsklasse ?? "",
            SchattenNotenTooltip = ergebnis is null
                ? ""
                : $"D {ergebnis.NoteD ?? "–"} · S {ergebnis.NoteS ?? "–"} · B {ergebnis.NoteB ?? "–"}"
                  + (ergebnis.Geschaetzt ? " (geschätzt)" : ""),
            SchattenMassnahme = schattenMassnahme,
            SchattenKosten = SchattenauswertungPageViewModel.FormatKosten(schattenKosten),
            StatusText = StatusAlsText(ergebnis),
            IstVeraltet = istVeraltet,
            AbweichungKey = abweichung switch
            {
                SchattenAbweichung.Gleich => "Gleich",
                SchattenAbweichung.LeichtAbweichend => "Leicht",
                SchattenAbweichung.StarkAbweichend => "Stark",
                _ => "Kein"
            },
            AbweichungTooltip = abweichung switch
            {
                SchattenAbweichung.Gleich => "Deckt sich mit deiner Auswertung",
                SchattenAbweichung.LeichtAbweichend => "Massnahme oder Kosten weichen ab",
                SchattenAbweichung.StarkAbweichend => "Zustandsklasse weicht ab",
                _ => "Kein Vergleich möglich"
            },
            KiBegruendung = ergebnis?.KiBegruendung ?? "",
            RisikoFlagsText = ergebnis is { RisikoFlags.Count: > 0 }
                ? string.Join(" · ", ergebnis.RisikoFlags)
                : "",
            KostenbandText = ergebnis?.KostenMin is not null
                ? $"{SchattenauswertungPageViewModel.FormatKosten(ergebnis.KostenMin)} – " +
                  $"{SchattenauswertungPageViewModel.FormatKosten(ergebnis.KostenErwartet)} – " +
                  $"{SchattenauswertungPageViewModel.FormatKosten(ergebnis.KostenMax)} CHF"
                : "",
            KiConfidenceText = ergebnis?.KiConfidence is { } c ? c.ToString("P0", CultureInfo.InvariantCulture) : "",
            KiFehler = ergebnis?.KiFehler ?? ""
        };
    }

    private static string StatusAlsText(SchattenHaltungErgebnis? e) => e?.Status switch
    {
        null => "nicht gerechnet",
        SchattenStatus.OhneCodierung => "ohne Codierung",
        SchattenStatus.NurRegeln => "Regeln",
        SchattenStatus.MitKi => "Regeln + KI",
        SchattenStatus.KiFallback => "Regeln (KI-Rückfall)",
        _ => ""
    };
}
