// AuswertungPro – Video-Selbsttraining Phase 4 — Benchmark-ViewModel
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.UI.Ai.Training;
using AuswertungPro.Next.UI.Ai.Training.Models;
using AuswertungPro.Next.UI.Ai.Training.Services;
using AuswertungPro.Next.Application.Ai.Training.Services;

namespace AuswertungPro.Next.UI.ViewModels.Windows;

/// <summary>
/// ViewModel fuer das Benchmark-Fenster.
/// Verwaltet das Benchmark-Set, startet Durchlaeufe, zeigt Metriken und Regressions-Alarme.
/// </summary>
public partial class BenchmarkViewModel : ObservableObject
{
    private readonly BenchmarkSetStore? _setStore;
    private readonly BenchmarkRunner? _runner;
    private readonly BenchmarkMetricsStore? _metricsStore;
    private readonly ProtocolLoaderFactory? _protocolLoader;
    private CancellationTokenSource? _cts;

    public ObservableCollection<BenchmarkHaltung> Haltungen { get; } = new();
    public ObservableCollection<BenchmarkRunResult> History { get; } = new();
    public ObservableCollection<CodeClassMetrics> PerCodeMetrics { get; } = new();

    [ObservableProperty] private BenchmarkHaltung? _selectedHaltung;
    [ObservableProperty] private string _statusText = "Bereit.";
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private int _progressValue;
    [ObservableProperty] private int _progressMax = 1;

    // Aktuelle Metriken
    [ObservableProperty] private string _f1Text = "—";
    [ObservableProperty] private string _precisionText = "—";
    [ObservableProperty] private string _recallText = "—";
    [ObservableProperty] private string _regressionText = "";
    [ObservableProperty] private bool _hasRegression;

    public BenchmarkViewModel() { }

    public BenchmarkViewModel(
        BenchmarkSetStore setStore,
        BenchmarkRunner runner,
        BenchmarkMetricsStore metricsStore,
        ProtocolLoaderFactory? protocolLoader = null)
    {
        _setStore = setStore;
        _runner = runner;
        _metricsStore = metricsStore;
        _protocolLoader = protocolLoader;
    }

    /// <summary>Laedt Benchmark-Set und Metriken-Historie.</summary>
    [RelayCommand]
    private async Task LoadAsync()
    {
        if (_setStore is null || _metricsStore is null) return;

        var set = await _setStore.LoadAsync().ConfigureAwait(false);
        var history = await _metricsStore.LoadHistoryAsync().ConfigureAwait(false);

        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            Haltungen.Clear();
            foreach (var h in set.Haltungen)
                Haltungen.Add(h);

            History.Clear();
            foreach (var run in history.OrderByDescending(r => r.TimestampUtc))
                History.Add(run);

            if (history.Count > 0)
            {
                var latest = history.OrderByDescending(r => r.TimestampUtc).First();
                ShowMetrics(latest);
            }

            StatusText = $"{Haltungen.Count} Benchmark-Haltungen, {History.Count} Durchlaeufe";
            RunBenchmarkCommand.NotifyCanExecuteChanged();
        });
    }

    /// <summary>Startet einen Benchmark-Durchlauf.</summary>
    [RelayCommand(CanExecute = nameof(CanRunBenchmark))]
    private async Task RunBenchmarkAsync()
    {
        if (_runner is null) return;

        IsBusy = true;
        _cts = new CancellationTokenSource();

        try
        {
            ProgressMax = Haltungen.Count;
            ProgressValue = 0;

            var progress = new Progress<BenchmarkProgress>(p =>
            {
                ProgressValue = p.CurrentHaltung;
                StatusText = p.Status;
            });

            var result = await _runner.RunAsync(progress, _cts.Token).ConfigureAwait(false);

            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                History.Insert(0, result);
                ShowMetrics(result);
                StatusText = $"Fertig: F1={result.F1:P0} in {result.Duration.TotalMinutes:F1} Min";
            });
        }
        catch (OperationCanceledException)
        {
            StatusText = "Benchmark abgebrochen.";
        }
        catch (Exception ex)
        {
            StatusText = $"Fehler: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanRunBenchmark() => !IsBusy && Haltungen.Count > 0;

    [RelayCommand]
    private void CancelBenchmark()
    {
        _cts?.Cancel();
        StatusText = "Abbruch angefordert...";
    }

    /// <summary>Fuegt eine Haltung zum Benchmark-Set hinzu (Video + Protokoll waehlen).</summary>
    [RelayCommand]
    private async Task AddHaltungAsync()
    {
        if (_setStore is null || _protocolLoader is null) return;

        // 1. Video waehlen
        var videoDlg = new OpenFileDialog
        {
            Title = "Inspektionsvideo waehlen",
            Filter = "Video-Dateien|*.mpg;*.mpeg;*.avi;*.mp4;*.mkv;*.wmv|Alle Dateien|*.*"
        };
        if (videoDlg.ShowDialog() != true) return;

        // 2. Protokoll waehlen
        var protDlg = new OpenFileDialog
        {
            Title = "Protokoll-Datei waehlen (DB3 oder Daten.txt)",
            Filter = "WinCan DB3|*.db3|IBAK Daten.txt|Daten.txt|Alle Dateien|*.*",
            InitialDirectory = Path.GetDirectoryName(videoDlg.FileName) ?? ""
        };
        if (protDlg.ShowDialog() != true) return;

        var videoPath = videoDlg.FileName;
        var protocolPath = protDlg.FileName;
        var sourceType = ProtocolLoaderFactory.DetectSourceType(protocolPath);

        StatusText = "Protokoll wird geladen fuer Goldstandard...";

        try
        {
            var (protocol, record) = await Task.Run(() =>
                _protocolLoader.LoadProtocolWithRecord(protocolPath, sourceType,
                    Path.GetFileNameWithoutExtension(videoPath)))
                .ConfigureAwait(false);

            if (protocol is null || protocol.Original.Entries.Count == 0)
            {
                StatusText = "Fehler: Kein Protokoll gefunden oder leer.";
                return;
            }

            // GroundTruth aus Protokoll extrahieren
            var rohrmaterial = record?.GetFieldValue("Rohrmaterial");
            var dnText = record?.GetFieldValue("DN_mm");
            int? dn = int.TryParse(dnText, out var d) ? d : null;

            var groundTruth = ProtocolToGroundTruthMapper.Map(protocol, rohrmaterial, dn);

            var haltungId = record?.GetFieldValue("Haltungsname")
                ?? Path.GetFileNameWithoutExtension(videoPath);

            var benchmark = new BenchmarkHaltung
            {
                HaltungId = haltungId ?? "Unbekannt",
                VideoPath = videoPath,
                ProtocolSource = protocolPath,
                SourceType = sourceType,
                Rohrmaterial = rohrmaterial,
                NennweiteMm = dn,
                GoldStandard = groundTruth
            };

            await _setStore.AddHaltungAsync(benchmark).ConfigureAwait(false);

            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                Haltungen.Add(benchmark);
                StatusText = $"Haltung '{haltungId}' hinzugefuegt ({groundTruth.Count} Protokolleintraege). Total: {Haltungen.Count}";
            });
        }
        catch (Exception ex)
        {
            StatusText = $"Fehler beim Hinzufuegen: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task RemoveHaltungAsync()
    {
        if (SelectedHaltung is null || _setStore is null) return;
        await _setStore.RemoveHaltungAsync(SelectedHaltung.HaltungId).ConfigureAwait(false);
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            Haltungen.Remove(SelectedHaltung);
            StatusText = $"{Haltungen.Count} Benchmark-Haltungen";
        });
    }

    private void ShowMetrics(BenchmarkRunResult result)
    {
        F1Text = result.F1 > 0 ? $"{result.F1:P1}" : "—";
        PrecisionText = result.Precision > 0 ? $"{result.Precision:P1}" : "—";
        RecallText = result.Recall > 0 ? $"{result.Recall:P1}" : "—";
        HasRegression = result.HasRegression;
        RegressionText = result.RegressionDetail ?? "";

        PerCodeMetrics.Clear();
        if (result.PerCodeMetrics is not null)
        {
            foreach (var m in result.PerCodeMetrics)
                PerCodeMetrics.Add(m);
        }
    }
}
