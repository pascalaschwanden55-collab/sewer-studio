using System.Collections.ObjectModel;
using System.IO;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.Export;
using AuswertungPro.Next.Application.UseCases.Verteilung;
using AuswertungPro.Next.UI.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AuswertungPro.Next.UI.ViewModels.Windows;

/// <summary>
/// Fenster «Verteilen»: links die Schritte Was, Ablage, Quelle, Filme, Ziel; rechts die
/// schreibfreie Vorschau. Jede Änderung einer Wahl rechnet die Vorschau im Hintergrund neu
/// und bricht die vorige ab. «Jetzt verteilen» gibt nur den Auftrag zurück — verteilt wird
/// von der Export-Seite über die bestehenden Verteilwege.
/// </summary>
public sealed partial class VerteilenViewModel : ObservableObject, IDisposable
{
    private readonly VerteilenVorgabe _vorgabe;
    private readonly IVerteilVorschau _vorschau;
    private readonly IDialogService _dialogs;
    private CancellationTokenSource? _lauf;
    private int _generation;
    private bool _disposed;

    [ObservableProperty] private VerteilArt _art;
    [ObservableProperty] private DistributionVariant _ablage;
    [ObservableProperty] private bool _txtModus;
    [ObservableProperty] private VerteilQuelle? _quelle;
    [ObservableProperty] private string? _filmOrdner;
    [ObservableProperty] private VerteilZiel _ziel;
    [ObservableProperty] private bool _istBerechnung;
    [ObservableProperty] private string _fortschritt = "";
    [ObservableProperty] private string? _fehler;
    [ObservableProperty] private VerteilVorschauErgebnis _vorschauErgebnis = VerteilVorschauErgebnis.Leer;

    public VerteilenViewModel(VerteilenVorgabe vorgabe, IVerteilVorschau vorschau, IDialogService dialogs)
    {
        _vorgabe = vorgabe ?? throw new ArgumentNullException(nameof(vorgabe));
        _vorschau = vorschau ?? throw new ArgumentNullException(nameof(vorschau));
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
        _art = vorgabe.Art;
        _ablage = vorgabe.Art == VerteilArt.Dichtheit ? DistributionVariant.Normal : vorgabe.Ablage;
        _filmOrdner = string.IsNullOrWhiteSpace(vorgabe.FilmOrdner) ? null : vorgabe.FilmOrdner;
        _ziel = vorgabe.Ziel(vorgabe.Art);

        QuelleOrdnerWaehlenCommand = new RelayCommand(QuelleOrdnerWaehlen);
        EinzelneDateienWaehlenCommand = new RelayCommand(EinzelneDateienWaehlen);
        TxtUmschaltenCommand = new RelayCommand(() => TxtModus = !TxtModus);
        FilmOrdnerWaehlenCommand = new RelayCommand(FilmOrdnerWaehlen);
        EinstellungenOeffnenCommand = new RelayCommand(() => Schliesse(new VerteilenErgebnis(null, EinstellungenOeffnen: true)));
        AbbrechenCommand = new RelayCommand(() => Schliesse(VerteilenErgebnis.Abgebrochen));
        VerteilenCommand = new RelayCommand(Verteilen, () => KannVerteilen);
    }

    /// <summary>Das Fenster schliesst sich, wenn dieses Ereignis kommt.</summary>
    public event EventHandler? SchliessenAngefordert;

    /// <summary>Antwort an die Export-Seite; bis zum Schliessen «abgebrochen».</summary>
    public VerteilenErgebnis Rueckgabe { get; private set; } = VerteilenErgebnis.Abgebrochen;

    /// <summary>Die zuletzt gestartete Vorschau (für Tests und sauberes Schliessen).</summary>
    public Task LaufendeVorschau { get; private set; } = Task.CompletedTask;

    public ObservableCollection<VerteilVorschauZeileAnzeige> Zeilen { get; } = [];

    public IRelayCommand QuelleOrdnerWaehlenCommand { get; }
    public IRelayCommand EinzelneDateienWaehlenCommand { get; }
    public IRelayCommand TxtUmschaltenCommand { get; }
    public IRelayCommand FilmOrdnerWaehlenCommand { get; }
    public IRelayCommand EinstellungenOeffnenCommand { get; }
    public IRelayCommand AbbrechenCommand { get; }
    public IRelayCommand VerteilenCommand { get; }

    // ─── Schritt 1: Was ───────────────────────────────────────────────────

    public bool IstHaltungen { get => Art == VerteilArt.Haltungen; set { if (value) Art = VerteilArt.Haltungen; } }
    public bool IstSchaechte { get => Art == VerteilArt.Schaechte; set { if (value) Art = VerteilArt.Schaechte; } }
    public bool IstDichtheit { get => Art == VerteilArt.Dichtheit; set { if (value) Art = VerteilArt.Dichtheit; } }

    // ─── Schritt 2: Ablage (nicht bei Dichtheit) ──────────────────────────

    public bool ZeigeAblage => Art != VerteilArt.Dichtheit;
    public bool IstNormal { get => Ablage == DistributionVariant.Normal; set { if (value) Ablage = DistributionVariant.Normal; } }
    public bool IstSanierung { get => Ablage == DistributionVariant.Sanierung; set { if (value) Ablage = DistributionVariant.Sanierung; } }
    public bool ZeigeSanierungHinweis => ZeigeAblage && Ablage == DistributionVariant.Sanierung;
    public string SanierungHinweis => "Ablage im Unterordner «…_Saniert <Jahr>» (Jahr aus dem Protokoll).";

    // ─── Schritt 3: Quelle ───────────────────────────────────────────────

    public bool ZeigeTxtUmschalter => Art == VerteilArt.Haltungen;
    public string QuelleTitel => TxtModus ? "TXT-Ordner (z. B. mit kiDVDaten.txt)" : "PDF-Ordner mit Protokollen";
    public string QuelleAnzeige => Quelle switch
    {
        null => "Noch nichts gewählt",
        { IstEinzeldateien: true } q => q.Dateien.Count == 1
            ? Path.GetFileName(q.Dateien[0])
            : $"{q.Dateien.Count} Dateien gewählt",
        { } q => q.Ordner ?? "",
    };
    public string EinzelnLinkText => TxtModus ? "Stattdessen einzelne TXT-Dateien wählen" : "Stattdessen einzelne PDFs wählen";
    public string TxtLinkText => TxtModus ? "Zurück zu den PDF-Protokollen" : "Stattdessen TXT-Import verwenden (kiDVDaten.txt)";

    // ─── Schritt 4: Filme (nur Haltungen) ────────────────────────────────

    public bool ZeigeFilme => Art == VerteilArt.Haltungen;
    public string FilmAnzeige => string.IsNullOrWhiteSpace(FilmOrdner) ? "Noch nicht gewählt" : FilmOrdner!;

    // ─── Schritt 5: Ziel ─────────────────────────────────────────────────

    public string ZielAnzeige => Ziel.Anzeige;

    // ─── Vorschau ────────────────────────────────────────────────────────

    public string Kopfzeile => Quelle is not { IstGewaehlt: true }
        ? "Quelle wählen – dann erscheint hier, was wohin abgelegt wird."
        : IstBerechnung
            ? Fortschritt
            : Fehler ?? VorschauErgebnis.Kopfzeile;

    public IReadOnlyList<string> Hinweise => VorschauErgebnis.Hinweise;
    public bool HatHinweise => VorschauErgebnis.Hinweise.Count > 0 && !IstBerechnung;

    public int WerdenAbgelegt => IstBerechnung ? 0 : VorschauErgebnis.WerdenAbgelegt;

    public string VerteilenText => WerdenAbgelegt == 1
        ? "Jetzt verteilen (1 Datei)"
        : $"Jetzt verteilen ({WerdenAbgelegt} Dateien)";

    public bool KannVerteilen
        => !_disposed
           && !IstBerechnung
           && Fehler is null
           && Quelle is { IstGewaehlt: true }
           && (Art != VerteilArt.Haltungen || !string.IsNullOrWhiteSpace(FilmOrdner))
           && VorschauErgebnis.WerdenAbgelegt > 0;

    /// <summary>Der Auftrag, den «Jetzt verteilen» zurückgibt.</summary>
    public VerteilAuftrag? Auftrag => Quelle is { IstGewaehlt: true } quelle
        ? new VerteilAuftrag(
            Art,
            Art == VerteilArt.Dichtheit ? DistributionVariant.Normal : Ablage,
            quelle,
            Art == VerteilArt.Haltungen ? FilmOrdner : null)
        : null;

    /// <summary>Startet die erste Vorschau (vom Fenster beim Öffnen gerufen).</summary>
    public void Starte() => AktualisiereVorschau();

    partial void OnArtChanged(VerteilArt value)
    {
        if (value != VerteilArt.Haltungen && TxtModus)
            TxtModus = false; // setzt die Quelle zurück und rechnet neu
        Ziel = _vorgabe.Ziel(value);
        OnPropertyChanged(nameof(IstHaltungen));
        OnPropertyChanged(nameof(IstSchaechte));
        OnPropertyChanged(nameof(IstDichtheit));
        OnPropertyChanged(nameof(ZeigeAblage));
        OnPropertyChanged(nameof(ZeigeSanierungHinweis));
        OnPropertyChanged(nameof(ZeigeFilme));
        OnPropertyChanged(nameof(ZeigeTxtUmschalter));
        AktualisiereVorschau();
    }

    partial void OnAblageChanged(DistributionVariant value)
    {
        OnPropertyChanged(nameof(IstNormal));
        OnPropertyChanged(nameof(IstSanierung));
        OnPropertyChanged(nameof(ZeigeSanierungHinweis));
        AktualisiereVorschau();
    }

    partial void OnTxtModusChanged(bool value)
    {
        // Eine PDF-Auswahl ist keine TXT-Auswahl und umgekehrt.
        Quelle = null;
        OnPropertyChanged(nameof(QuelleTitel));
        OnPropertyChanged(nameof(EinzelnLinkText));
        OnPropertyChanged(nameof(TxtLinkText));
    }

    partial void OnQuelleChanged(VerteilQuelle? value)
    {
        OnPropertyChanged(nameof(QuelleAnzeige));
        AktualisiereVorschau();
    }

    partial void OnFilmOrdnerChanged(string? value)
    {
        OnPropertyChanged(nameof(FilmAnzeige));
        AktualisiereVorschau();
    }

    partial void OnZielChanged(VerteilZiel value) => OnPropertyChanged(nameof(ZielAnzeige));

    partial void OnIstBerechnungChanged(bool value) => MeldeVorschauStand();
    partial void OnFortschrittChanged(string value) => OnPropertyChanged(nameof(Kopfzeile));
    partial void OnFehlerChanged(string? value) => MeldeVorschauStand();
    partial void OnVorschauErgebnisChanged(VerteilVorschauErgebnis value) => MeldeVorschauStand();

    private void MeldeVorschauStand()
    {
        OnPropertyChanged(nameof(Kopfzeile));
        OnPropertyChanged(nameof(Hinweise));
        OnPropertyChanged(nameof(HatHinweise));
        OnPropertyChanged(nameof(WerdenAbgelegt));
        OnPropertyChanged(nameof(VerteilenText));
        OnPropertyChanged(nameof(KannVerteilen));
        VerteilenCommand.NotifyCanExecuteChanged();
    }

    private void QuelleOrdnerWaehlen()
    {
        var ordner = _dialogs.SelectFolder(TxtModus
            ? "TXT-Ordner wählen (z. B. mit kiDVDaten.txt)"
            : Art switch
            {
                VerteilArt.Schaechte => "PDF-Ordner mit Schachtprotokollen wählen",
                VerteilArt.Dichtheit => "PDF-Ordner mit Dichtheitsprüfungsprotokollen wählen",
                _ => "PDF-Ordner mit Protokollen wählen",
            });
        if (!string.IsNullOrWhiteSpace(ordner))
            Quelle = TxtModus ? VerteilQuelle.TxtOrdner(ordner) : VerteilQuelle.PdfOrdner(ordner);
    }

    private void EinzelneDateienWaehlen()
    {
        var dateien = TxtModus
            ? _dialogs.OpenFiles("TXT-Dateien auswählen", "TXT (*.txt)|*.txt")
            : _dialogs.OpenFiles("PDF-Protokolle auswählen", "PDF (*.pdf)|*.pdf");
        if (dateien.Length > 0)
            Quelle = TxtModus ? VerteilQuelle.TxtDateien(dateien) : VerteilQuelle.PdfDateien(dateien);
    }

    private void FilmOrdnerWaehlen()
    {
        var ordner = _dialogs.SelectFolder("Ordner mit den Rohvideos wählen", FilmOrdner);
        if (!string.IsNullOrWhiteSpace(ordner))
            FilmOrdner = ordner;
    }

    private void Verteilen()
    {
        if (!KannVerteilen || Auftrag is not { } auftrag)
            return;

        Schliesse(new VerteilenErgebnis(auftrag, EinstellungenOeffnen: false));
    }

    private void Schliesse(VerteilenErgebnis ergebnis)
    {
        Rueckgabe = ergebnis;
        _lauf?.Cancel();
        SchliessenAngefordert?.Invoke(this, EventArgs.Empty);
    }

    private void AktualisiereVorschau()
    {
        if (_disposed)
            return;

        _lauf?.Cancel();
        _lauf?.Dispose();
        _lauf = null;
        var generation = ++_generation;
        Fehler = null;
        Zeilen.Clear();

        if (Quelle is not { IstGewaehlt: true })
        {
            VorschauErgebnis = VerteilVorschauErgebnis.Leer;
            IstBerechnung = false;
            LaufendeVorschau = Task.CompletedTask;
            return;
        }

        var anfrage = new VerteilVorschauAnfrage(
            Art,
            Art == VerteilArt.Dichtheit ? DistributionVariant.Normal : Ablage,
            Quelle,
            Art == VerteilArt.Haltungen ? FilmOrdner : null,
            Ziel.Ordner,
            Ziel.Baum,
            _vorgabe.Projekt);
        var abbruch = new CancellationTokenSource();
        _lauf = abbruch;
        Fortschritt = "Vorschau wird berechnet …";
        IstBerechnung = true;
        var fortschritt = new Progress<VerteilVorschauFortschritt>(p =>
        {
            if (generation != _generation)
                return;
            Fortschritt = p.Gesamt > 0
                ? $"Vorschau: {p.Erledigt} von {p.Gesamt} Dateien gelesen …"
                : "Vorschau wird berechnet …";
        });
        LaufendeVorschau = BerechneAsync(anfrage, fortschritt, abbruch.Token, generation);
    }

    private async Task BerechneAsync(
        VerteilVorschauAnfrage anfrage,
        IProgress<VerteilVorschauFortschritt> fortschritt,
        CancellationToken abbruch,
        int generation)
    {
        try
        {
            var ergebnis = await Task.Run(() => _vorschau.Plane(anfrage, fortschritt, abbruch), abbruch);
            if (generation != _generation)
                return;

            Zeilen.Clear();
            foreach (var zeile in ergebnis.Zeilen)
                Zeilen.Add(new VerteilVorschauZeileAnzeige(zeile));
            VorschauErgebnis = ergebnis;
        }
        catch (OperationCanceledException)
        {
            // Eine neuere Wahl hat diese Vorschau abgelöst.
        }
        catch (Exception ex)
        {
            if (generation == _generation)
            {
                VorschauErgebnis = VerteilVorschauErgebnis.Leer;
                Fehler = "Vorschau nicht möglich: " + UserError.DescribeAndReport(ex, "Verteilvorschau");
            }
        }
        finally
        {
            if (generation == _generation)
                IstBerechnung = false;
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _generation++;
        _lauf?.Cancel();
        _lauf?.Dispose();
        _lauf = null;
    }
}

/// <summary>Anzeigezeile mit dem Klartext des Status-Chips.</summary>
public sealed record VerteilVorschauZeileAnzeige(VerteilVorschauZeile Zeile)
{
    public string Protokoll => Zeile.Protokoll;
    public string Zielordner => Zeile.Zielordner ?? "—";
    public string Film => Zeile.Film ?? "";
    public string Hinweis => Zeile.Hinweis;

    public string StatusText => Zeile.Status switch
    {
        VerteilVorschauStatus.Bereit => "wird abgelegt",
        VerteilVorschauStatus.FilmGefunden => "gefunden",
        VerteilVorschauStatus.FilmFehlt => "fehlt",
        VerteilVorschauStatus.FilmMehrdeutig => "mehrdeutig",
        VerteilVorschauStatus.SchonVorhanden => "schon vorhanden",
        VerteilVorschauStatus.NichtZugeordnet => "nicht zugeordnet",
        VerteilVorschauStatus.WirdGeprueft => "wird geprüft",
        _ => "",
    };

    /// <summary>Farbgruppe des Chips: Gut, Warnung, Fehler oder Neutral.</summary>
    public string StatusArt => Zeile.Status switch
    {
        VerteilVorschauStatus.Bereit or VerteilVorschauStatus.FilmGefunden => "Gut",
        VerteilVorschauStatus.FilmFehlt or VerteilVorschauStatus.FilmMehrdeutig or VerteilVorschauStatus.WirdGeprueft => "Warnung",
        VerteilVorschauStatus.NichtZugeordnet => "Fehler",
        _ => "Neutral",
    };
}
