using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Linq;
using AuswertungPro.Next.Application.Media;
using AuswertungPro.Next.Application.UseCases.BendSuggestions;
using AuswertungPro.Next.Application.UseCases.PipeEndSuggestions;

namespace AuswertungPro.Next.UI.ViewModels.BendSuggestions;

/// <summary>
/// ViewModel des Bereichs "Bogen-Vorschlaege" im Training Studio (Paket 3 des Auftrags
/// docs/briefings/bogen-vorschlaege-training-studio-auftrag.md): Videowahl, ein Durchlauf
/// auf Knopfdruck, Liste der Stellen, Klick zeigt Spitzenbild und kurzen Clip.
///
/// Die Orchestrierung (Busy, Fortschritt, Abbruch, Klartext-Fehler) liegt im
/// <see cref="BendSuggestionScanWorkflow"/>; hier ist nur die Anzeige. Der Workflow ruft
/// seine Aktionen teils von einem Threadpool-Thread (ConfigureAwait(false)) — alles, was
/// gebundene Eigenschaften schreibt, laeuft deshalb ueber <c>marshalToUi</c>.
/// </summary>
public sealed partial class BendSuggestionListViewModel : ObservableObject, IDisposable
{
    /// <summary>Der einzige Kandidat mit gemessenem Arbeitspunkt (workpoint.json).</summary>
    public const string KandidatId = "bcc_nc15_seed46_20260808";

    /// <summary>SHA-256 des Gewichts, an das der Arbeitspunkt gebunden ist (Regel 2).</summary>
    public const string GewichtSha256 = "8ad82c1b0186ec02126a18f095d551d7a083faa90855350b22a6e893ac860114";

    /// <summary>Clip-Puffer um die Stelle: 3 s vor dem Beginn bis 3 s nach dem Ende.</summary>
    private const double ClipPufferSekunden = 3.0;

    private static readonly CultureInfo Deutsch = CultureInfo.GetCultureInfo("de-DE");

    private readonly IBendSuggestionScanService _scanService;
    private readonly ICodingSuggestionExposure _exposure;
    private readonly IVideoFrameExtractor _frameExtractor;
    private readonly IVideoClipExtractor _clipExtractor;
    private readonly Func<string> _resolveFfmpegPath;
    private readonly Action<Action> _marshalToUi;
    private readonly Action<string> _log;

    /// <summary>
    /// Rohranfang/Rohrende aus den freigegebenen Lernstufen; optional, damit der
    /// Bogen-Weg ohne Sidecar-Lernstufen unveraendert laeuft.
    /// </summary>
    private readonly IPipeEndSuggestionScanService? _pipeEndScan;

    private CancellationTokenSource? _scanCts;
    private CancellationTokenSource? _previewCts;

    public BendSuggestionListViewModel(
        IBendSuggestionScanService scanService,
        ICodingSuggestionExposure exposure,
        IVideoFrameExtractor frameExtractor,
        IVideoClipExtractor clipExtractor,
        Func<string> resolveFfmpegPath,
        Action<Action>? marshalToUi = null,
        Action<string>? log = null,
        IPipeEndSuggestionScanService? pipeEndScan = null)
    {
        _scanService = scanService ?? throw new ArgumentNullException(nameof(scanService));
        _exposure = exposure ?? throw new ArgumentNullException(nameof(exposure));
        _frameExtractor = frameExtractor ?? throw new ArgumentNullException(nameof(frameExtractor));
        _clipExtractor = clipExtractor ?? throw new ArgumentNullException(nameof(clipExtractor));
        _resolveFfmpegPath = resolveFfmpegPath ?? throw new ArgumentNullException(nameof(resolveFfmpegPath));
        _marshalToUi = marshalToUi ?? (aktion => aktion());
        _log = log ?? (_ => { });
        _pipeEndScan = pipeEndScan;
    }

    [ObservableProperty] private string? _videoPath;
    [ObservableProperty] private string? _haltung;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartScanCommand))]
    private bool _isBusy;

    [ObservableProperty] private string _statusText = "Video wählen, dann den Durchgang starten.";
    [ObservableProperty] private string _headerText = string.Empty;
    [ObservableProperty] private string _resultInfoText = string.Empty;
    [ObservableProperty] private ObservableCollection<BendSuggestionRowViewModel> _suggestions = new();
    [ObservableProperty] private BendSuggestionRowViewModel? _selectedSuggestion;
    [ObservableProperty] private ImageSource? _peakImage;
    [ObservableProperty] private string? _clipPath;

    /// <summary>
    /// Laufende Vorschau-Ladung als Task, damit Tests sie abwarten koennen. Fehler werden
    /// innen behandelt (Statustext) — der Task faulted nie absichtlich in die UI.
    /// </summary>
    internal Task PreviewLoadTask { get; private set; } = Task.CompletedTask;

    /// <summary>Uebernimmt die Videowahl des Fensters; die Haltung kommt aus dem Dateinamen.</summary>
    public void SetVideo(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        VideoPath = path;
        Haltung = DeriveHaltung(path);
        StatusText = $"Video: {Path.GetFileName(path)} — Haltung {Haltung}.";
    }

    /// <summary>"H_36053-36052.mpg" wird zu "36053-36052" — fuehrendes H_ wird abgeschnitten.</summary>
    internal static string DeriveHaltung(string path)
    {
        var name = Path.GetFileNameWithoutExtension(path);
        return name.StartsWith("H_", StringComparison.OrdinalIgnoreCase) ? name[2..] : name;
    }

    private bool CanStartScan() => !IsBusy;

    [RelayCommand(CanExecute = nameof(CanStartScan), AllowConcurrentExecutions = false)]
    private async Task StartScan()
    {
        var videoPath = VideoPath;
        var haltung = Haltung;
        if (string.IsNullOrWhiteSpace(videoPath) || string.IsNullOrWhiteSpace(haltung))
        {
            StatusText = "Bitte zuerst ein Video wählen.";
            return;
        }

        // Ein neuer Durchlauf ersetzt die alte Liste ganz — auch dann, wenn der
        // Bogen-Teil ohne Arbeitspunkt endet und nur Rohranfang/Rohrende liefern.
        Suggestions.Clear();
        HeaderText = string.Empty;
        ResultInfoText = string.Empty;

        var request = new BendSuggestionScanWorkflowRequest
        {
            Scan = new BendSuggestionScanRequest
            {
                VideoPath = videoPath,
                CandidateId = KandidatId,
                WeightSha256 = GewichtSha256
            },
            Haltung = haltung
        };
        var actions = new BendSuggestionScanWorkflowActions(
            Scan: (scan, abbruch, fortschritt) => _scanService.ScanAsync(scan, abbruch, fortschritt),
            IsBusy: () => IsBusy,
            SetBusy: wert => AufUi(() => IsBusy = wert),
            ResetCancellation: ResetScanCancellation,
            SetStatusText: text => AufUi(() => StatusText = text),
            Log: _log,
            PublishResult: (ergebnis, zugehoerigeHaltung) => AufUi(() => PublishResult(ergebnis, zugehoerigeHaltung)));

        await BendSuggestionScanWorkflow.RunAsync(request, actions).ConfigureAwait(false);

        if (_pipeEndScan is null)
            return;

        // Rohranfang und Rohrende laufen NACH dem Bogen, unabhaengig von dessen
        // Ausgang: Ein fehlender Bogen-Arbeitspunkt sagt nichts ueber die Lernstufen.
        var pipeEndRequest = new PipeEndSuggestionScanWorkflowRequest
        {
            Scan = new PipeEndScanRequest { VideoPath = videoPath },
            Haltung = haltung
        };
        var pipeEndActions = new PipeEndSuggestionScanWorkflowActions(
            Scan: (scan, abbruch, fortschritt) => _pipeEndScan.ScanAsync(scan, abbruch, fortschritt),
            // Der Doppelstart ist bereits durch den Befehl (AllowConcurrentExecutions=false)
            // und den ersten Ablauf gesperrt; der Busy-Wert des ersten Ablaufs kommt je nach
            // Marshal erst spaeter an und darf den zweiten Teil nicht still ueberspringen.
            IsBusy: () => false,
            SetBusy: wert => AufUi(() => IsBusy = wert),
            ResetCancellation: ResetScanCancellation,
            SetStatusText: text => AufUi(() => StatusText = text),
            Log: _log,
            PublishResult: (ergebnis, zugehoerigeHaltung) => AufUi(() => PublishPipeEndResult(ergebnis, zugehoerigeHaltung)));

        await PipeEndSuggestionScanWorkflow.RunAsync(pipeEndRequest, pipeEndActions).ConfigureAwait(false);
    }

    private CancellationToken ResetScanCancellation()
    {
        _scanCts?.Dispose();
        _scanCts = new CancellationTokenSource();
        return _scanCts.Token;
    }

    /// <summary>Bricht den eigenen Durchlauf ab (eigene CancellationTokenSource je Lauf).</summary>
    [RelayCommand]
    private void CancelScan() => _scanCts?.Cancel();

    /// <summary>Bricht Scan- und Vorschaularbeit ab; das Fenster ruft das beim Schliessen.</summary>
    public void CancelPendingWork()
    {
        _scanCts?.Cancel();
        _previewCts?.Cancel();
    }

    public void Dispose()
    {
        CancelPendingWork();
        _scanCts?.Dispose();
        _previewCts?.Dispose();
    }

    /// <summary>
    /// Wird erst beim ANZEIGEN der Liste aufgerufen (PublishResult des Workflows, also nur
    /// nach einem brauchbaren Ergebnis) — nie beim Scanstart. Ab hier gilt die folgende
    /// Codierung dieser Haltung als beeinflusst, auch bei einer leeren Liste.
    /// </summary>
    private void PublishResult(BendSuggestionScanResult ergebnis, string haltung)
    {
        Suggestions.Clear();
        foreach (var vorschlag in ergebnis.Suggestions)
            Suggestions.Add(new BendSuggestionRowViewModel(vorschlag));

        HeaderText =
            $"Kandidat: {ergebnis.CandidateId}\n"
            + $"Arbeitspunkt: conf ≥ {ergebnis.MinConfidence.ToString("0.00", Deutsch)}, "
            + $"stark ab {ergebnis.StrongConfidence.ToString("0.00", Deutsch)}\n"
            + $"Beleg: {ergebnis.WorkpointSource}";
        ResultInfoText =
            $"{ergebnis.FramesAnalyzed} Bilder ausgewertet, {ergebnis.FramesNotAssessed} nicht ausgewertet · "
            + $"Laufzeit {ergebnis.Duration.TotalSeconds:0} s";

        _exposure.MarkExposed(haltung);
    }

    /// <summary>
    /// Fuegt Rohranfang und Rohrende in dieselbe Liste ein und ordnet alles nach
    /// Videozeit, wie der Mensch die Haltung abfaehrt. Auch eine leere Liste gilt
    /// als angezeigt — die folgende Codierung dieser Haltung ist beeinflusst.
    /// </summary>
    private void PublishPipeEndResult(PipeEndScanResult ergebnis, string haltung)
    {
        foreach (var vorschlag in ergebnis.Suggestions)
        {
            var pin = ergebnis.Pins.FirstOrDefault(p => p.Kind == vorschlag.Kind);
            Suggestions.Add(BendSuggestionRowViewModel.FromPipeEnd(vorschlag, pin?.Precision ?? 0.0));
        }

        var geordnet = Suggestions.OrderBy(zeile => zeile.PeakTimeSeconds).ToList();
        Suggestions.Clear();
        foreach (var zeile in geordnet)
            Suggestions.Add(zeile);

        var abnahme = string.Join(
            "\n",
            ergebnis.Pins.Select(pin =>
                $"{PipeEndKinds.Label(pin.Kind)}: Abnahme Precision {Prozent(pin.Precision)}, "
                + $"Recall {Prozent(pin.Recall)} (Freigabe 2026-08-12, genau ein Vorschlag je Video)"));
        HeaderText = string.IsNullOrEmpty(HeaderText) ? abnahme : HeaderText + "\n" + abnahme;

        var laufzeit = $"Rohranfang/Rohrende: {ergebnis.FramesAnalyzed} Bilder, Laufzeit {ergebnis.Duration.TotalSeconds:0} s";
        ResultInfoText = string.IsNullOrEmpty(ResultInfoText) ? laufzeit : ResultInfoText + " · " + laufzeit;

        _exposure.MarkExposed(haltung);
    }

    /// <summary>"85 %" mit normalem Leerzeichen — bewusst nicht ueber das Kulturformat.</summary>
    private static string Prozent(double anteil)
        => Math.Round(anteil * 100.0).ToString("0", Deutsch) + " %";

    partial void OnSelectedSuggestionChanged(BendSuggestionRowViewModel? value)
    {
        // Wechsel der Auswahl bricht einen laufenden Ladevorgang ab.
        _previewCts?.Cancel();
        _previewCts?.Dispose();
        _previewCts = null;
        PeakImage = null;
        ClipPath = null;

        var videoPath = VideoPath;
        if (value is null || string.IsNullOrWhiteSpace(videoPath))
        {
            PreviewLoadTask = Task.CompletedTask;
            return;
        }

        _previewCts = new CancellationTokenSource();
        PreviewLoadTask = LoadPreviewAsync(value, videoPath, _previewCts.Token);
    }

    private async Task LoadPreviewAsync(
        BendSuggestionRowViewModel zeile,
        string videoPath,
        CancellationToken abbrechen)
    {
        var ffmpeg = _resolveFfmpegPath();
        var spitzenzeit = TimeSpan.FromSeconds(zeile.PeakTimeSeconds);
        var clipVon = TimeSpan.FromSeconds(Math.Max(0.0, zeile.TimeStartSeconds - ClipPufferSekunden));
        var clipBis = TimeSpan.FromSeconds(zeile.TimeEndSeconds + ClipPufferSekunden);

        byte[]? bildBytes = null;
        string? clip = null;
        string? vorschauFehler = null;
        try
        {
            // Bild und Clip nacheinander, aber getrennt bewertet: Ein geladenes
            // Spitzenbild darf nicht an einem fehlgeschlagenen Clip mithaengen.
            bildBytes = await _frameExtractor
                .TryExtractFramePngAsync(ffmpeg, videoPath, spitzenzeit, abbrechen)
                .ConfigureAwait(false);
            clip = await _clipExtractor
                .CutClipAsync(ffmpeg, videoPath, clipVon, clipBis, abbrechen)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Auswahl gewechselt oder Fenster zu — kein Fehler, kein Statustext.
            return;
        }
        catch (Exception ex)
        {
            // Nie eine Exception in die UI lassen; die Meldung des Dienstes
            // (z. B. die woertliche ffmpeg-Ausgabe) geht in Log und Statustext.
            vorschauFehler = ex.Message;
            _log($"Vorschau der Stelle konnte nicht geladen werden: {ex.Message}");
        }

        AufUi(() =>
        {
            if (abbrechen.IsCancellationRequested)
                return;
            PeakImage = bildBytes is { Length: > 0 } ? ToBitmap(bildBytes) : null;
            ClipPath = clip;
            if (vorschauFehler is not null)
            {
                StatusText = $"Vorschau der Stelle konnte nicht geladen werden: {vorschauFehler}";
            }
            else if (bildBytes is null && clip is null)
            {
                StatusText = "Bild und Clip der Stelle konnten nicht geladen werden.";
            }
        });
    }

    /// <summary>PNG-Bytes als BitmapImage; Freeze, damit die Anzeige threadfest bleibt.</summary>
    private static BitmapImage ToBitmap(byte[] pngBytes)
    {
        var bild = new BitmapImage();
        bild.BeginInit();
        bild.CacheOption = BitmapCacheOption.OnLoad;
        bild.StreamSource = new MemoryStream(pngBytes);
        bild.EndInit();
        bild.Freeze();
        return bild;
    }

    private void AufUi(Action aktion) => _marshalToUi(aktion);
}
