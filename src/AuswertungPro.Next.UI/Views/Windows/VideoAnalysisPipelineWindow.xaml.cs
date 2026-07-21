using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;
using AuswertungPro.Next.UI.Ai.Pipeline;
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class VideoAnalysisPipelineWindow : Window
{
    private readonly IVideoAnalysisPipelineService _pipeline;
    private PipelineRequest _request;
    private readonly CancellationTokenSource _cts = new();
    private readonly List<LiveFrameFinding> _liveFrameFindings = new();
    private PipelinePipeRadarMode _overlayMode = PipelinePipeRadarMode.Detail;
    private LiveFrameWindow? _liveFrameWindow;

    private PipelineResult? _result;
    public PipelineResult? Result => _result;

    public VideoAnalysisPipelineViewModel Vm { get; }

    public VideoAnalysisPipelineWindow(PipelineRequest request, IVideoAnalysisPipelineService pipeline)
    {
        InitializeComponent();
        WindowStateManager.Track(this);

        _request = request;
        _pipeline = pipeline;

        Vm = new VideoAnalysisPipelineViewModel();
        DataContext = Vm;

        Vm.Detections.CollectionChanged += OnDetectionsChanged;
        PipeRadarCanvas.SizeChanged += (_, _) => RenderPipeRadar();
        LiveFrameOverlayCanvas.SizeChanged += (_, _) => RenderLiveFrameOverlay();

        Closed += (_, __) =>
        {
            _cts.Cancel();
            _cts.Dispose();
            Vm.Detections.CollectionChanged -= OnDetectionsChanged;
            CloseLiveFrameWindow();
        };
    }

    private async void Start_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            StartButton.IsEnabled = false;
            await RunPipelineAsync();
        }
        catch (Exception ex)
        {
            Vm.SetError(ex.Message);
        }
    }

    private async Task RunPipelineAsync()
    {
        using var _aiToken = Services.AiActivityTracker.Begin("Videoanalyse-Pipeline");
        Vm.Reset();
        _liveFrameFindings.Clear();

        // Speed mode from ComboBox
        var frameStep = GetSelectedFrameStep();
        _request = _request with { FrameStepSeconds = frameStep };
        SpeedModeCombo.IsEnabled = false; // Lock during analysis
        StartButton.IsEnabled = false;

        Vm.SetPhase("Videoanalyse", "Starte Analyse ...");

        var progressMapper = new PipelineProgressMapper(Vm, _liveFrameFindings, ToBitmap);

        try
        {
            var progress = new Progress<PipelineProgress>(p =>
            {
                var effects = progressMapper.Apply(p);
                if (effects.RenderLiveFrameOverlay)
                    RenderLiveFrameOverlay();
                if (effects.ForwardLiveFrame)
                    ForwardLiveFrame();
            });

            var result = await _pipeline.RunAsync(_request, progress, _cts.Token);

            _result = result;

            if (!result.IsSuccess)
            {
                Vm.SetError(result.Error ?? "Unbekannter Fehler");
                return;
            }

            Vm.IsDone = true;
            Vm.HasError = false;

            var presentation = PipelineResultPresenter.ApplySuccessful(Vm, result);
            var visibleDetections = presentation.VisibleDetections;
            ReplaceVisibleDetections(visibleDetections);

            Vm.StatusText = "Fertig. Du kannst jetzt übertragen.";
            Vm.PhaseLabel = "Fertig";
        }
        catch (OperationCanceledException)
        {
            Vm.SetError("Abgebrochen.");
        }
        catch (Exception ex)
        {
            Vm.SetError(ex.Message);
        }
    }

    private static BitmapImage ToBitmap(byte[] pngBytes)
    {
        using var ms = new System.IO.MemoryStream(pngBytes);
        var bmp = new BitmapImage();
        bmp.BeginInit();
        bmp.CacheOption = BitmapCacheOption.OnLoad;
        bmp.StreamSource = ms;
        bmp.EndInit();
        bmp.Freeze();
        return bmp;
    }

    private void RenderLiveFrameOverlay()
    {
        if (LiveFrameOverlayCanvas is null)
            return;

        PipelineLiveFrameOverlayRenderer.Render(
            LiveFrameOverlayCanvas,
            Vm.LiveFrameImage is not null,
            _liveFrameFindings,
            LiveFrameOverlayCanvas.ActualWidth,
            LiveFrameOverlayCanvas.ActualHeight);
    }

    private void ForwardLiveFrame()
    {
        _liveFrameWindow?.UpdateFrame(
            Vm.LiveFrameImage,
            _liveFrameFindings,
            Vm.LiveFrameStatus,
            Vm.LiveFrameInfo,
            Vm.LiveFrameQuantSummary);
    }

    private void OnDetectionsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RenderPipeRadar();
    }

    private void ReplaceVisibleDetections(IEnumerable<DetectionItem> detections)
    {
        Vm.Detections.CollectionChanged -= OnDetectionsChanged;
        try
        {
            Vm.Detections.Clear();
            foreach (var detection in detections)
                Vm.Detections.Add(detection);
        }
        finally
        {
            Vm.Detections.CollectionChanged += OnDetectionsChanged;
        }

        RenderPipeRadar();
    }

    private double GetSelectedFrameStep()
    {
        if (SpeedModeCombo.SelectedItem is ComboBoxItem item && item.Tag is string tag
            && double.TryParse(tag, NumberStyles.Float, CultureInfo.InvariantCulture, out var step))
            return step;
        return 1.0; // Maximum quality default
    }

    private void OverlayModeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _overlayMode = OverlayModeCombo.SelectedIndex <= 0
            ? PipelinePipeRadarMode.Compact
            : PipelinePipeRadarMode.Detail;
        RenderPipeRadar();
    }

    private void RenderPipeRadar()
    {
        if (PipeRadarCanvas is null || PipeRadarEmptyText is null)
            return;

        PipelinePipeRadarRenderer.Render(
            PipeRadarCanvas,
            PipeRadarEmptyText,
            Vm.Detections,
            _overlayMode,
            PipeRadarCanvas.ActualWidth,
            PipeRadarCanvas.ActualHeight);
    }

    private void Undock_Click(object sender, RoutedEventArgs e)
    {
        if (_liveFrameWindow is not null)
        {
            _liveFrameWindow.Activate();
            return;
        }

        _liveFrameWindow = new LiveFrameWindow();
        _liveFrameWindow.Closed += (_, _) => _liveFrameWindow = null;
        _liveFrameWindow.Show();

        // Send current frame immediately
        ForwardLiveFrame();
    }

    private void CloseLiveFrameWindow()
    {
        if (_liveFrameWindow is not null)
        {
            _liveFrameWindow.Close();
            _liveFrameWindow = null;
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        if (!Vm.IsDone && !Vm.HasError)
        {
            _cts.Cancel();
            return;
        }

        DialogResult = false;
        Close();
    }

    private void Accept_Click(object sender, RoutedEventArgs e)
    {
        if (_result is null || !_result.IsSuccess || _result.Document is null)
        {
            DialogHost.Current.Info("Kein gültiges Ergebnis zum Übertragen vorhanden.", "Videoanalyse KI");
            return;
        }

        // Nur AUSGEWAEHLTE Eintraege gelangen ins Fachprotokoll (Fehlerpruefung 11.07.,
        // Kritisch 2). Vorausgewaehlt ist nur "verlaesslich"; Pruefen/Ablehnen muss der
        // Nutzer bewusst anhaken. Ohne Mapping (Alt-Fallback) bleibt das Verhalten wie bisher.
        if (_result.MappedEntries is { Count: > 0 })
        {
            var ausgewaehlt = Vm.Detections
                .Where(d => d.IsSelected && d.EntryId != Guid.Empty)
                .Select(d => d.EntryId)
                .ToHashSet();

            if (ausgewaehlt.Count == 0)
            {
                DialogHost.Current.Info(
                    "Kein Eintrag ausgewählt. Bitte die zu übernehmenden Befunde anhaken " +
                    "(nur 'verlässlich' ist vorausgewählt).",
                    "Videoanalyse KI");
                return;
            }

            var gesamt = Vm.Detections.Count;
            AiProtocolAcceptancePolicy.Apply(_result.Document, ausgewaehlt);
            Vm.StatusText = $"Übernommen: {ausgewaehlt.Count} von {gesamt} Befunden (Rest verworfen).";
        }

        DialogResult = true;
        Close();
    }
}
