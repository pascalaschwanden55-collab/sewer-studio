// AuswertungPro – KI Videoanalyse Modul
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using AuswertungPro.Next.UI.Ai.Analysis.Models;
using AuswertungPro.Next.UI.Ai.Analysis.Services;
using AuswertungPro.Next.UI.Ai.Shared;
using AuswertungPro.Next.UI.Ai.Training;
using AuswertungPro.Next.UI.Ai.Training.Services;

namespace AuswertungPro.Next.UI.Ai.Analysis;

/// <summary>
/// ViewModel für VideoAnalysisWindow.
/// Orchestriert die 2-Stufen-Pipeline:
///   Stufe 1: SceneChangeDetector → FrameStore → VisionDetectionService
///   Stufe 2: ClassificationService (qwen2.5:14b + few-shot via RetrievalService)
///   Abschluss: ObservationMergeService.Merge
/// </summary>
public sealed partial class VideoAnalysisViewModel : ObservableObject
{
    private readonly VisionDetectionService _vision;
    private readonly ClassificationService  _classification;
    private readonly Dispatcher             _dispatcher;

    private CancellationTokenSource? _cts;

    [ObservableProperty] private string _videoPath   = "";
    [ObservableProperty] private string _statusText  = "Kein Video ausgewählt.";
    [ObservableProperty] private bool   _isRunning   = false;
    [ObservableProperty] private bool   _isDone      = false;
    [ObservableProperty] private bool   _hasError    = false;
    [ObservableProperty] private string _errorText   = "";
    [ObservableProperty] private int    _framesTotal = 0;
    [ObservableProperty] private int    _framesDone  = 0;
    [ObservableProperty] private double _progressPct = 0;
    [ObservableProperty] private double _meterStart  = 0;
    [ObservableProperty] private double _meterEnd    = 100;

    public ObservableCollection<AnalysisObservation> Observations { get; } = [];
    public IReadOnlyList<AnalysisObservation>?        FinalResult  { get; private set; }

    public VideoAnalysisViewModel(
        VisionDetectionService visionService,
        ClassificationService  classificationService)
    {
        _vision         = visionService;
        _classification = classificationService;
        _dispatcher     = System.Windows.Application.Current.Dispatcher;
    }

    // ── Commands ─────────────────────────────────────────────────────────

    [RelayCommand]
    private void PickVideo()
    {
        var dlg = new OpenFileDialog
        {
            Title  = "Kanalinspektion-Video auswählen",
            Filter = "Videos|*.mp4;*.avi;*.mkv;*.mov;*.wmv|Alle Dateien|*.*"
        };
        if (dlg.ShowDialog() != true) return;

        VideoPath   = dlg.FileName;
        StatusText  = "Video geladen. Klicke 'Analyse starten'.";
        IsDone      = false;
        HasError    = false;
        FramesDone  = 0;
        FramesTotal = 0;
        ProgressPct = 0;
        Observations.Clear();
        FinalResult = null;
    }

    [RelayCommand(CanExecute = nameof(CanRun))]
    private async Task RunAsync()
    {
        if (!CanRun()) return;

        _cts    = new CancellationTokenSource();
        var ct  = _cts.Token;

        IsRunning = true;
        IsDone    = false;
        HasError  = false;
        ErrorText = "";
        _dispatcher.Invoke(Observations.Clear);
        FinalResult = null;

        try
        {
            await RunPipelineAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            SetStatus(false, "Abgebrochen.");
        }
        catch (Exception ex)
        {
            SetStatus(false, ex.Message);
        }
        finally
        {
            IsRunning = false;
            _cts?.Dispose();
            _cts = null;
        }
    }

    [RelayCommand]
    private void Cancel() => _cts?.Cancel();

    private bool CanRun() => !string.IsNullOrWhiteSpace(VideoPath) && !IsRunning;

    // ── Pipeline ─────────────────────────────────────────────────────────

    private async Task RunPipelineAsync(CancellationToken ct)
    {
        var ffmpeg = FfmpegLocator.ResolveFfmpeg();

        // Videodauer
        StatusText = "Videodauer wird ermittelt...";
        var probe      = new VideoProbeService();
        var probeRes   = await probe.ProbeAsync(VideoPath, ct).ConfigureAwait(false);
        var duration   = probeRes.Success ? probeRes.DurationSeconds : 0;

        // Szenenänderungen
        StatusText = "Szenenänderungen werden erkannt...";
        var detector   = new SceneChangeDetector(ffmpeg);
        var timestamps = await detector.DetectAsync(VideoPath, ct).ConfigureAwait(false);

        FramesTotal = timestamps.Count;
        FramesDone  = 0;
        ProgressPct = 0;
        StatusText  = $"Analyse startet: {timestamps.Count} Frame(s)...";

        var allObservations = new List<AnalysisObservation>();

        for (var i = 0; i < timestamps.Count; i++)
        {
            ct.ThrowIfCancellationRequested();

            var ts    = timestamps[i];
            var meter = InterpolateMeter(ts, duration);
            var id    = Guid.NewGuid().ToString("N");

            // Frame extrahieren
            var framePath = await FrameStore
                .ExtractAndStoreAsync(ffmpeg, VideoPath, ts, id, ct: ct)
                .ConfigureAwait(false);

            if (framePath is null) continue;

            // Stufe 1: Vision
            var detections = await _vision
                .DetectAsync(framePath, meter, ct)
                .ConfigureAwait(false);

            // Stufe 2: Classification
            foreach (var desc in detections)
            {
                var obs = await _classification
                    .ClassifyAsync(desc, meter, ct)
                    .ConfigureAwait(false);

                if (obs is not null)
                {
                    VsaCodeValidator.Validate(obs);
                    allObservations.Add(obs);
                    _dispatcher.Invoke(() => Observations.Add(obs));
                }
            }

            FramesDone  = i + 1;
            ProgressPct = (double)(i + 1) / timestamps.Count * 100.0;
            StatusText  = $"Frame {i + 1}/{timestamps.Count} – Meter {meter:F1} m";
        }

        // Merge / Dedup
        StatusText = "Ergebnisse zusammenführen...";
        var merged = ObservationMergeService.Merge(allObservations);
        FinalResult = merged;

        _dispatcher.Invoke(() =>
        {
            Observations.Clear();
            foreach (var obs in merged)
                Observations.Add(obs);
        });

        ProgressPct = 100;
        IsDone      = true;
        StatusText  = $"Fertig – {merged.Count} Beobachtung(en) gefunden.";
    }

    private double InterpolateMeter(double timeSeconds, double duration)
    {
        if (duration <= 0) return MeterStart;
        var t = Math.Clamp(timeSeconds / duration, 0, 1);
        return MeterStart + t * (MeterEnd - MeterStart);
    }

    private void SetStatus(bool done, string message)
    {
        if (done)
        {
            IsDone     = true;
            StatusText = message;
        }
        else
        {
            HasError   = true;
            ErrorText  = message;
            StatusText = "Fehler: " + message;
        }
    }
}
