using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class VideoAnalysisPipelineWindow : Window
{
    private readonly VideoAnalysisPipelineService _pipeline;
    private readonly PipelineRequest _request;
    private readonly CancellationTokenSource _cts = new();

    private PipelineResult? _result;
    public PipelineResult? Result => _result;

    public VideoAnalysisPipelineViewModel Vm { get; }

    public VideoAnalysisPipelineWindow(PipelineRequest request, VideoAnalysisPipelineService pipeline)
    {
        InitializeComponent();

        _request = request;
        _pipeline = pipeline;

        Vm = new VideoAnalysisPipelineViewModel();
        DataContext = Vm;

        Loaded += async (_, __) => await RunPipelineAsync();
        Closed += (_, __) => _cts.Cancel();
    }

    private async Task RunPipelineAsync()
    {
        Vm.Reset();
        Vm.SetPhase("Videoanalyse", "Starte Analyse ...");

        try
        {
            var progress = new Progress<PipelineProgress>(p =>
            {
                // einfache UI-Abbildung – feinere Progress-Mappings können später ergänzt werden
                Vm.StatusText = p.Status;
                Vm.PhaseLabel = p.Phase.ToString();

                Vm.VideoPhaseActive = p.Phase == PipelinePhase.VideoAnalysis;
                Vm.VideoPhaseDone = p.Phase != PipelinePhase.VideoAnalysis;
                Vm.MappingPhaseDone = p.Phase == PipelinePhase.Done;

                if (p.Phase == PipelinePhase.VideoAnalysis)
                {
                    Vm.VideoProgressPct = p.PercentInPhase * 100.0;
                    Vm.MappingProgressPct = 0;
                }
                else if (p.Phase == PipelinePhase.CodeMapping)
                {
                    Vm.VideoProgressPct = 100.0;
                    Vm.MappingProgressPct = p.PercentInPhase * 100.0;
                }
                else if (p.Phase == PipelinePhase.Done)
                {
                    Vm.VideoProgressPct = 100.0;
                    Vm.MappingProgressPct = 100.0;
                }
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

            // Stats
            Vm.FramesAnalyzed = result.Stats?.FramesAnalyzed ?? 0;
            Vm.DetectionCount = result.Detections?.Count ?? 0;
            Vm.HighConfidenceCount = result.Stats?.EntriesWithHighConfidence ?? 0;
            Vm.StatsText = result.Stats is null
                ? ""
                : $"Frames: {result.Stats.FramesAnalyzed}, Detections: {result.Stats.DetectionsRaw}, Entries: {result.Stats.EntriesGenerated}, HighConf: {result.Stats.EntriesWithHighConfidence}";

            // einfache Liste für UI
            Vm.Detections.Clear();
            foreach (var d in (result.Detections ?? Array.Empty<RawVideoDetection>()).Take(250))
            {
                Vm.Detections.Add(DetectionItem.From(d));
            }

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
            MessageBox.Show("Kein gültiges Ergebnis zum Übertragen vorhanden.", "Videoanalyse KI",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        DialogResult = true;
        Close();
    }
}