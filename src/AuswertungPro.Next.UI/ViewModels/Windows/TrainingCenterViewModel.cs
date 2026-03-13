using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;

namespace AuswertungPro.Next.UI.ViewModels.Windows;

using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.KnowledgeBase;
using AuswertungPro.Next.UI.Ai.Ollama;
using AuswertungPro.Next.UI.Ai.Pipeline;
using AuswertungPro.Next.UI.Ai.Training;
using AuswertungPro.Next.UI.Services;
using AiTrack = AuswertungPro.Next.UI.Services.AiActivityTracker;

public partial class TrainingCenterViewModel : ObservableObject
{
    private readonly TrainingCenterStore _store;
    private readonly TrainingCenterImportService _import;

    public ObservableCollection<TrainingCase> Cases { get; } = new();
    public ObservableCollection<TrainingSample> Samples { get; } = new();

    [ObservableProperty] private TrainingCase? _selectedCase;
    [ObservableProperty] private TrainingSample? _selectedSample;
    [ObservableProperty] private string _rootFolder = "";
    [ObservableProperty] private string _statusText = "";
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _logText = "";
    [ObservableProperty] private int _progressValue;
    [ObservableProperty] private int _progressMax = 1;

    // Live-Vorschau während Batch-Import
    [ObservableProperty] private string _liveFramePath = "";
    [ObservableProperty] private string _liveCaseInfo = "";
    [ObservableProperty] private string _liveCodeInfo = "";
    [ObservableProperty] private string _liveMeterInfo = "";

    // KB-Trainingsstand
    [ObservableProperty] private int _kbSampleCount;
    [ObservableProperty] private int _kbEmbeddingCount;
    [ObservableProperty] private int _kbCodesCovered;
    [ObservableProperty] private string _kbReadinessLabel = "Unbekannt";
    [ObservableProperty] private System.Windows.Media.Brush _kbReadinessBrush
        = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x94, 0xA3, 0xB8));
    [ObservableProperty] private string _kbLastUpdate = "\u2014";
    [ObservableProperty] private string _kbTopCodesText = "";

    // PipeGraphTimeline: Max-Meter der aktuellen Samples, aktuelle Sample-Position
    [ObservableProperty] private double _samplesMaxMeter;
    [ObservableProperty] private double _selectedSampleMeter;

    // Review Queue (Self-Improving Loop)
    public ObservableCollection<Ai.SelfImproving.ReviewQueueItem> ReviewQueue { get; } = new();
    [ObservableProperty] private Ai.SelfImproving.ReviewQueueItem? _selectedReviewItem;
    [ObservableProperty] private int _reviewQueueCount;
    [ObservableProperty] private string _reviewStatusText = "";

    private readonly List<string> _rootFolders = new();
    private CancellationTokenSource? _genCts;

    /// <summary>Fügt eine Zeile zum Log hinzu (Thread-safe via Dispatcher).</summary>
    private void Log(string message)
    {
        var line = $"[{DateTime.Now:HH:mm:ss}] {message}\n";
        if (System.Windows.Application.Current?.Dispatcher is { } d && !d.CheckAccess())
            d.Invoke(() => LogText += line);
        else
            LogText += line;
    }

    /// <summary>Aktualisiert die Live-Vorschau (Thread-safe).</summary>
    private void UpdateLivePreview(string caseInfo, string code, string meter, string? framePath)
    {
        void Apply()
        {
            LiveCaseInfo = caseInfo;
            LiveCodeInfo = code;
            LiveMeterInfo = meter;
            if (framePath is not null)
                LiveFramePath = framePath;
        }

        if (System.Windows.Application.Current?.Dispatcher is { } d && !d.CheckAccess())
            d.Invoke(Apply);
        else
            Apply();
    }

    private void ClearLivePreview()
    {
        LiveFramePath = "";
        LiveCaseInfo = "";
        LiveCodeInfo = "";
        LiveMeterInfo = "";
    }

    private async Task RefreshKbStatusAsync()
    {
        try
        {
            var summary = await Task.Run(() =>
            {
                using var db = new KnowledgeBaseContext();
                var diag = new KnowledgeBaseDiagnosticsService(db);
                return diag.ReadSummary(20);
            });

            void Apply()
            {
                KbSampleCount = summary.SampleCount;
                KbEmbeddingCount = summary.EmbeddingCount;
                KbCodesCovered = summary.TopCodes.Count;
                KbLastUpdate = summary.LatestVersionAtUtc?.ToLocalTime().ToString("dd.MM.yyyy HH:mm") ?? "\u2014";

                static System.Windows.Media.SolidColorBrush Rgb(byte r, byte g, byte b)
                    => new(System.Windows.Media.Color.FromRgb(r, g, b));

                (KbReadinessLabel, KbReadinessBrush) = summary.SampleCount switch
                {
                    >= 100 => ("KI-Modell einsatzbereit", Rgb(0x4A, 0xDE, 0x80)),
                    >= 25  => ("Lernbasis grundlegend",   Rgb(0xFA, 0xCC, 0x15)),
                    > 0    => ("Lernbasis unzureichend",  Rgb(0xF8, 0x71, 0x71)),
                    _      => ("Keine Trainingsdaten",    Rgb(0x94, 0xA3, 0xB8))
                };

                KbTopCodesText = string.Join("\n", summary.TopCodes
                    .Select(c => $"{c.VsaCode}: {c.Count} Samples"));
            }

            if (System.Windows.Application.Current?.Dispatcher is { } d && !d.CheckAccess())
                d.Invoke(Apply);
            else
                Apply();
        }
        catch
        {
            // KB might not exist yet — silently ignore
        }
    }

    public TrainingCenterViewModel(TrainingCenterStore store, TrainingCenterImportService import)
    {
        _store = store;
        _import = import;
    }

    // ── Cases ────────────────────────────────────────────────────────────────

    public async Task LoadAsync()
    {
        var state = await _store.LoadAsync();
        Cases.Clear();
        foreach (var c in state.Cases)
            Cases.Add(c);
        StatusText = $"Geladen: {Cases.Count} Fälle";

        await LoadSamplesInternalAsync();
        await RefreshKbStatusAsync();
    }

    [RelayCommand]
    private void BrowseRootFolder()
    {
        var dlg = new OpenFolderDialog
        {
            Title = "Trainings-Ordner wählen (Mehrfachauswahl möglich)",
            Multiselect = true
        };
        if (dlg.ShowDialog() != true)
            return;

        // Neue Auswahl zu bestehenden hinzufügen (Duplikate vermeiden)
        foreach (var folder in dlg.FolderNames)
        {
            if (!_rootFolders.Contains(folder, StringComparer.OrdinalIgnoreCase))
                _rootFolders.Add(folder);
        }

        UpdateRootFolderDisplay();
    }

    [RelayCommand]
    private void ClearRootFolders()
    {
        _rootFolders.Clear();
        UpdateRootFolderDisplay();
    }

    private void UpdateRootFolderDisplay()
    {
        RootFolder = _rootFolders.Count switch
        {
            0 => "",
            1 => _rootFolders[0],
            _ => $"{_rootFolders.Count} Ordner: {string.Join("; ", _rootFolders.Select(Path.GetFileName))}"
        };
    }

    [RelayCommand]
    private async Task ScanAsync()
    {
        if (IsBusy) return;
        if (_rootFolders.Count == 0)
        {
            StatusText = "Bitte zuerst einen oder mehrere Ordner wählen.";
            return;
        }

        try
        {
            IsBusy = true;
            StatusText = "Scanne Ordner...";
            Cases.Clear();

            foreach (var folder in _rootFolders)
            {
                if (!Directory.Exists(folder)) continue;
                var found = await _import.ScanAsync(folder);
                foreach (var c in found)
                    Cases.Add(c);
            }

            var withProto    = Cases.Count(c => !string.IsNullOrEmpty(c.ProtocolPath));
            var withoutProto = Cases.Count - withProto;
            StatusText = withoutProto > 0
                ? $"Gefunden: {Cases.Count} Fälle ({withoutProto} ohne Protokoll)"
                : $"Gefunden: {Cases.Count} Fälle";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (IsBusy) return;
        try
        {
            IsBusy = true;
            var state = new TrainingCenterState
            {
                Cases = Cases.ToList(),
                UpdatedUtc = DateTime.UtcNow
            };
            await _store.SaveAsync(state);
            StatusText = $"Gespeichert: {Cases.Count} Fälle";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool HasSelection() => SelectedCase is not null;

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void Approve()
    {
        if (SelectedCase is null) return;
        SelectedCase.Status = TrainingCaseStatus.Approved;
        StatusText = $"Approved: {SelectedCase.CaseId}";
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void Reject()
    {
        if (SelectedCase is null) return;
        SelectedCase.Status = TrainingCaseStatus.Rejected;
        StatusText = $"Rejected: {SelectedCase.CaseId}";
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void SetNew()
    {
        if (SelectedCase is null) return;
        SelectedCase.Status = TrainingCaseStatus.New;
        StatusText = $"Status New: {SelectedCase.CaseId}";
    }

    partial void OnSelectedCaseChanged(TrainingCase? value)
    {
        ApproveCommand.NotifyCanExecuteChanged();
        RejectCommand.NotifyCanExecuteChanged();
        SetNewCommand.NotifyCanExecuteChanged();
        GenerateSamplesCommand.NotifyCanExecuteChanged();
    }

    // ── Samples ──────────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task LoadSamplesAsync()
    {
        await LoadSamplesInternalAsync();
    }

    private async Task LoadSamplesInternalAsync()
    {
        var list = await TrainingSamplesStore.LoadAsync();
        Samples.Clear();
        foreach (var s in list)
            Samples.Add(s);
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task GenerateSamplesAsync()
    {
        if (SelectedCase is null || IsBusy) return;

        _genCts?.Cancel();
        _genCts?.Dispose();
        _genCts = new CancellationTokenSource();
        var ct = _genCts.Token;

        using var _aiToken = AiTrack.Begin("Training Center");
        try
        {
            IsBusy = true;
            StatusText = $"Generiere Samples für {SelectedCase.CaseId}...";

            var cfg = AiRuntimeConfig.Load();
            var settings = await TrainingCenterSettingsStore.LoadAsync();
            var meterSvc = CreateMeterTimelineService(cfg, settings.GpuConcurrency);
            var generator = new TrainingSampleGenerator(cfg, meterSvc, settings);

            var existing = await TrainingSamplesStore.LoadAsync();
            var existingSigs = existing.Select(s => s.Signature).ToHashSet(StringComparer.Ordinal);

            var generation = await generator.GenerateWithDiagnosticsAsync(
                SelectedCase, existingSigs, framesDir: null, ct);
            var newSamples = generation.Samples;

            if (newSamples.Count == 0)
            {
                StatusText = generation.Outcome switch
                {
                    TrainingSampleGenerationOutcome.OnlyDuplicates
                        => $"Keine neuen Samples für {SelectedCase.CaseId} (alle {generation.ParsedEntries} Einträge bereits vorhanden).",
                    TrainingSampleGenerationOutcome.NoProtocolEntries
                        => $"Keine Protokolleinträge erkannt für {SelectedCase.CaseId}.",
                    TrainingSampleGenerationOutcome.ProtocolUnreadable
                        => $"Protokoll konnte nicht gelesen werden: {SelectedCase.ProtocolPath}",
                    TrainingSampleGenerationOutcome.ProtocolFileMissing
                        => $"Protokolldatei fehlt: {SelectedCase.ProtocolPath}",
                    _ => "Keine neuen Samples generiert."
                };
                return;
            }

            existing.AddRange(newSamples);
            await TrainingSamplesStore.SaveAsync(existing);

            foreach (var s in newSamples)
                Samples.Add(s);

            StatusText = $"{newSamples.Count} neue Samples generiert für {SelectedCase.CaseId}.";
        }
        catch (OperationCanceledException)
        {
            StatusText = "Sample-Generierung abgebrochen.";
        }
        catch (Exception ex)
        {
            StatusText = $"Fehler bei Sample-Generierung: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool HasSampleSelection() => SelectedSample is not null;

    [RelayCommand(CanExecute = nameof(HasSampleSelection))]
    private async Task ApproveSampleAsync()
    {
        if (SelectedSample is null) return;
        SelectedSample.Status = TrainingSampleStatus.Approved;
        StatusText = $"Approved: {SelectedSample.SampleId}";
        await PersistSamplesAsync();
    }

    [RelayCommand(CanExecute = nameof(HasSampleSelection))]
    private async Task RejectSampleAsync()
    {
        if (SelectedSample is null) return;
        SelectedSample.Status = TrainingSampleStatus.Rejected;
        StatusText = $"Rejected: {SelectedSample.SampleId}";
        await PersistSamplesAsync();
    }

    [RelayCommand]
    private async Task ExportApprovedAsync()
    {
        if (IsBusy) return;
        try
        {
            IsBusy = true;
            var approved = Samples
                .Where(s => s.Status == TrainingSampleStatus.Approved && s.ExportedUtc is null)
                .ToList();

            if (approved.Count == 0)
            {
                StatusText = "Keine nicht-exportierten Approved-Samples vorhanden.";
                return;
            }

            foreach (var s in approved)
            {
                var entry = new AuswertungPro.Next.Domain.Protocol.ProtocolEntry
                {
                    Code = s.Code,
                    Beschreibung = s.Beschreibung,
                    MeterStart = s.MeterStart,
                    MeterEnd = s.MeterEnd,
                    IsStreckenschaden = s.IsStreckenschaden
                };
                ProtocolTrainingStore.AddSample(entry, s.CaseId);
                s.ExportedUtc = DateTime.UtcNow;
            }

            await PersistSamplesAsync();

            var codes = approved.Select(s => s.Code).Distinct().OrderBy(c => c).ToList();
            Log($"Protokoll-Training: {approved.Count} Samples als Few-Shot-Beispiele gespeichert.");
            Log($"  Codes: {string.Join(", ", codes)}");
            Log($"  Ziel: {Path.Combine(AppSettings.AppDataDir, "data", "protocol_training.json")}");
            Log("  Wirkung: Qwen nutzt diese Beispiele bei zukünftigen Protokoll-Generierungen.");
            StatusText = $"Protokoll-Training: {approved.Count} Samples als Few-Shot-Beispiele gespeichert ({codes.Count} Codes).";
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Nur Protokolle + Fotos: Liest alle PDFs in den gewaehlten Ordnern,
    /// extrahiert Schadenscodes und PDF-Fotos als Trainings-Samples.
    /// Kein Video noetig, kein Qwen noetig. Schnellster Lernpfad.
    /// </summary>
    [RelayCommand]
    private async Task LearnProtocolOnlyAsync()
    {
        if (IsBusy) return;
        if (_rootFolders.Count == 0)
        {
            StatusText = "Bitte zuerst einen oder mehrere Ordner wählen.";
            return;
        }

        _genCts?.Cancel();
        _genCts?.Dispose();
        _genCts = new CancellationTokenSource();
        var ct = _genCts.Token;

        using var _aiToken = AiTrack.Begin("Nur Protokolle + Fotos");
        try
        {
            IsBusy = true;
            LogText = "";
            ProgressValue = 0;

            // Scan: alle Ordner mit Protokoll (Video nicht erforderlich)
            Log("Scanne Ordner (nur Protokolle)...");
            var found = new List<TrainingCase>();
            foreach (var folder in _rootFolders)
            {
                if (!System.IO.Directory.Exists(folder)) continue;
                Log($"  {folder}");
                var result = await _import.ScanProtocolOnlyAsync(folder);
                found.AddRange(result);
            }
            Log($"Gefunden: {found.Count} Ordner mit Protokoll");

            Cases.Clear();
            foreach (var c in found) Cases.Add(c);

            if (found.Count == 0)
            {
                StatusText = "Keine Ordner mit Protokolldateien gefunden.";
                return;
            }

            ProgressMax = found.Count;
            var cfg = AiRuntimeConfig.Load();
            var ffmpeg = cfg.FfmpegPath ?? "ffmpeg";
            var framesDir = FrameStore.GetFramesDir(null);

            var allSamples = await TrainingSamplesStore.LoadAsync();
            var existingSigs = allSamples.Select(s => s.Signature).ToHashSet(StringComparer.Ordinal);
            var totalNew = 0;

            foreach (var tc in found)
            {
                ct.ThrowIfCancellationRequested();
                ProgressValue++;
                StatusText = $"[{ProgressValue}/{found.Count}] {tc.CaseId}...";

                var doc = await LoadProtocolDocAsync(tc.ProtocolPath);
                if (doc?.Current?.Entries is null || doc.Current.Entries.Count == 0)
                {
                    Log($"  {tc.CaseId}: Kein Protokoll oder leer");
                    continue;
                }

                var entries = doc.Current.Entries
                    .Where(e => !e.IsDeleted && !string.IsNullOrWhiteSpace(e.Code))
                    .ToList();

                if (entries.Count == 0)
                {
                    Log($"  {tc.CaseId}: Keine Einträge mit Code");
                    continue;
                }

                var hasVideo = System.IO.File.Exists(tc.VideoPath);
                var caseNew = 0;

                foreach (var entry in entries)
                {
                    ct.ThrowIfCancellationRequested();
                    var meterStart = entry.MeterStart ?? 0;
                    var meterEnd = entry.MeterEnd ?? meterStart;
                    var sig = $"{tc.CaseId}|{entry.Code}|{Math.Round(meterStart, 1):F1}|{Math.Round(meterEnd, 1):F1}";

                    if (existingSigs.Contains(sig)) continue;
                    existingSigs.Add(sig);

                    var safeCase = System.Text.RegularExpressions.Regex.Replace(tc.CaseId, @"[^\w\-]", "_");
                    var sampleId = $"{safeCase}_{entry.Code}_{meterStart:F2}_{Guid.NewGuid():N}";

                    // Frame: (1) Video bei Timecode, (2) PDF-Foto, (3) kein Frame
                    string? framePath = null;
                    if (hasVideo && entry.Zeit.HasValue)
                    {
                        framePath = await FrameStore.ExtractAndStoreAsync(
                            ffmpeg, tc.VideoPath, entry.Zeit.Value.TotalSeconds,
                            sampleId, framesDir, ct).ConfigureAwait(false);
                    }
                    if (string.IsNullOrEmpty(framePath) && entry.FotoPaths.Count > 0
                        && System.IO.File.Exists(entry.FotoPaths[0]))
                    {
                        framePath = entry.FotoPaths[0];
                    }

                    var sample = new TrainingSample
                    {
                        SampleId = sampleId,
                        CaseId = tc.CaseId,
                        Code = entry.Code,
                        Beschreibung = entry.Beschreibung,
                        MeterStart = meterStart,
                        MeterEnd = meterEnd,
                        IsStreckenschaden = entry.IsStreckenschaden,
                        TimeSeconds = entry.Zeit?.TotalSeconds ?? 0,
                        DetectedMeter = meterStart,
                        MeterSource = "protocol",
                        FramePath = framePath ?? "",
                        Status = TrainingSampleStatus.Approved,
                        TruthMeterCenter = meterStart,
                        Signature = sig
                    };

                    allSamples.Add(sample);
                    if (System.Windows.Application.Current?.Dispatcher is { } d && !d.CheckAccess())
                        d.Invoke(() => Samples.Add(sample));
                    else
                        Samples.Add(sample);

                    // Few-Shot speichern
                    var protoEntry = new AuswertungPro.Next.Domain.Protocol.ProtocolEntry
                    {
                        Code = entry.Code,
                        Beschreibung = entry.Beschreibung,
                        MeterStart = meterStart,
                        MeterEnd = meterEnd,
                        IsStreckenschaden = entry.IsStreckenschaden
                    };
                    ProtocolTrainingStore.AddSample(protoEntry, tc.CaseId);

                    caseNew++;
                    totalNew++;
                }

                if (caseNew > 0)
                    Log($"  {tc.CaseId}: {caseNew} Samples aus {entries.Count} Einträgen" +
                        (hasVideo ? " (mit Video-Frames)" : " (nur PDF-Fotos)"));
            }

            // Speichern
            await TrainingSamplesStore.SaveAsync(allSamples);

            // KB indexieren
            if (totalNew > 0)
            {
                Log($"KB-Indexierung: {totalNew} neue Samples...");
                var ollamaConfig = Ai.Ollama.OllamaConfig.Load();
                var ollamaReachable = await CheckOllamaReachableAsync(ollamaConfig, ct);
                if (ollamaReachable)
                {
                    var http = new System.Net.Http.HttpClient { Timeout = ollamaConfig.RequestTimeout };
                    using var kbCtx = new KnowledgeBaseContext();
                    var embedder = new EmbeddingService(http, ollamaConfig);
                    var kbManager = new KnowledgeBaseManager(kbCtx, embedder);
                    var approvedSamples = allSamples
                        .Where(s => s.Status == TrainingSampleStatus.Approved)
                        .ToList();
                    var indexed = await kbManager.RebuildAsync(approvedSamples, null, ct);
                    kbManager.CreateVersion($"Protokoll-Only {DateTime.Now:yyyy-MM-dd HH:mm}");
                    Log($"KB fertig: {indexed}/{approvedSamples.Count} indiziert");
                }
                else
                {
                    Log("KB-Indexierung übersprungen (Ollama nicht erreichbar).");
                }
                await RefreshKbStatusAsync();
            }

            Log($"═══ Nur Protokolle + Fotos: {totalNew} neue Samples aus {found.Count} Ordnern ═══");
            StatusText = $"Fertig: {totalNew} neue Samples aus Protokollen gelernt.";
        }
        catch (OperationCanceledException) { StatusText = "Abgebrochen."; }
        catch (Exception ex) { Log($"FEHLER: {ex.Message}"); StatusText = $"Fehler: {ex.Message}"; }
        finally { IsBusy = false; }
    }

    /// <summary>
    /// Lernen aus Protokoll: Extrahiert Frames direkt an den Protokoll-Timecodes,
    /// ohne Qwen-Analyse. Das Protokoll ist die Ground-Truth.
    /// Erzeugt Approved-Samples, speichert als Few-Shot + KB.
    /// </summary>
    [RelayCommand]
    private async Task LearnFromProtocolAsync()
    {
        if (IsBusy) return;

        // Wenn keine Cases gescannt → automatisch aus _rootFolders scannen
        if (Cases.Count == 0 && _rootFolders.Count > 0)
        {
            Log("Keine Fälle gescannt — scanne Ordner automatisch...");
            foreach (var folder in _rootFolders)
            {
                if (!System.IO.Directory.Exists(folder)) continue;
                var result = await _import.ScanAsync(folder);
                foreach (var c in result) Cases.Add(c);
            }
            Log($"Gefunden: {Cases.Count} Fälle");
        }

        if (Cases.Count == 0)
        {
            StatusText = "Bitte zuerst Ordner wählen oder scannen.";
            return;
        }

        _genCts?.Cancel();
        _genCts?.Dispose();
        _genCts = new CancellationTokenSource();
        var ct = _genCts.Token;

        using var _aiToken = AiTrack.Begin("Lernen aus Protokoll");
        try
        {
            IsBusy = true;
            LogText = "";
            var cfg = AiRuntimeConfig.Load();
            var ffmpeg = cfg.FfmpegPath ?? "ffmpeg";
            var framesDir = FrameStore.GetFramesDir(null);
            var casesWithProtocol = Cases
                .Where(c => !string.IsNullOrEmpty(c.ProtocolPath))
                .ToList();

            Log($"Lernen aus Protokoll: {casesWithProtocol.Count} Fälle mit Protokoll");
            ProgressMax = casesWithProtocol.Count;
            ProgressValue = 0;

            var allSamples = await TrainingSamplesStore.LoadAsync();
            var existingSigs = allSamples.Select(s => s.Signature).ToHashSet(StringComparer.Ordinal);
            var totalNew = 0;

            foreach (var tc in casesWithProtocol)
            {
                ct.ThrowIfCancellationRequested();
                ProgressValue++;
                StatusText = $"[{ProgressValue}/{casesWithProtocol.Count}] {tc.CaseId}...";

                var doc = await LoadProtocolDocAsync(tc.ProtocolPath);
                if (doc?.Current?.Entries is null || doc.Current.Entries.Count == 0)
                {
                    Log($"  {tc.CaseId}: Kein Protokoll oder leer → übersprungen");
                    continue;
                }

                var entries = doc.Current.Entries
                    .Where(e => !e.IsDeleted && !string.IsNullOrWhiteSpace(e.Code) && e.Zeit.HasValue)
                    .ToList();

                if (entries.Count == 0)
                {
                    Log($"  {tc.CaseId}: Keine Einträge mit Timecode → übersprungen");
                    continue;
                }

                var hasVideo = System.IO.File.Exists(tc.VideoPath);
                var caseNew = 0;

                foreach (var entry in entries)
                {
                    ct.ThrowIfCancellationRequested();
                    var meterStart = entry.MeterStart ?? 0;
                    var meterEnd = entry.MeterEnd ?? meterStart;
                    var sig = $"{entry.Code}|{Math.Round(meterStart, 1):F1}|{Math.Round(meterEnd, 1):F1}";

                    if (existingSigs.Contains(sig)) continue;
                    existingSigs.Add(sig);

                    var t = entry.Zeit!.Value.TotalSeconds;
                    var safeCase = System.Text.RegularExpressions.Regex.Replace(tc.CaseId, @"[^\w\-]", "_");
                    var sampleId = $"{safeCase}_{entry.Code}_{meterStart:F2}_{Guid.NewGuid():N}";

                    // Frame extrahieren (direkt via ffmpeg seek, kein Qwen noetig)
                    string? framePath = null;
                    if (hasVideo)
                    {
                        framePath = await FrameStore.ExtractAndStoreAsync(
                            ffmpeg, tc.VideoPath, t, sampleId, framesDir, ct).ConfigureAwait(false);
                    }
                    // Fallback: Foto aus PDF-Bildbericht
                    if (string.IsNullOrEmpty(framePath) && entry.FotoPaths.Count > 0
                        && System.IO.File.Exists(entry.FotoPaths[0]))
                    {
                        framePath = entry.FotoPaths[0];
                    }

                    var sample = new TrainingSample
                    {
                        SampleId = sampleId,
                        CaseId = tc.CaseId,
                        Code = entry.Code,
                        Beschreibung = entry.Beschreibung,
                        MeterStart = meterStart,
                        MeterEnd = meterEnd,
                        IsStreckenschaden = entry.IsStreckenschaden,
                        TimeSeconds = t,
                        DetectedMeter = meterStart,
                        MeterSource = "protocol",
                        FramePath = framePath ?? "",
                        Status = TrainingSampleStatus.Approved,
                        TruthMeterCenter = meterStart,
                        Signature = sig
                    };

                    allSamples.Add(sample);
                    if (System.Windows.Application.Current?.Dispatcher is { } d && !d.CheckAccess())
                        d.Invoke(() => Samples.Add(sample));
                    else
                        Samples.Add(sample);

                    // Direkt als Few-Shot speichern
                    var protoEntry = new AuswertungPro.Next.Domain.Protocol.ProtocolEntry
                    {
                        Code = entry.Code,
                        Beschreibung = entry.Beschreibung,
                        MeterStart = meterStart,
                        MeterEnd = meterEnd,
                        IsStreckenschaden = entry.IsStreckenschaden
                    };
                    ProtocolTrainingStore.AddSample(protoEntry, tc.CaseId);

                    caseNew++;
                    totalNew++;
                }

                if (caseNew > 0)
                    Log($"  {tc.CaseId}: {caseNew} Samples aus {entries.Count} Protokoll-Einträgen");
                else
                    Log($"  {tc.CaseId}: Alle {entries.Count} Einträge bereits vorhanden (Duplikate)");
            }

            // Samples speichern
            await TrainingSamplesStore.SaveAsync(allSamples);

            // KB indexieren
            if (totalNew > 0)
            {
                Log($"KB-Indexierung: {totalNew} neue Samples...");
                var ollamaConfig = Ai.Ollama.OllamaConfig.Load();
                var ollamaReachable = await CheckOllamaReachableAsync(ollamaConfig, ct);
                if (ollamaReachable)
                {
                    var http = new System.Net.Http.HttpClient { Timeout = ollamaConfig.RequestTimeout };
                    using var kbCtx = new KnowledgeBaseContext();
                    var embedder = new EmbeddingService(http, ollamaConfig);
                    var kbManager = new KnowledgeBaseManager(kbCtx, embedder);
                    var newSamples = allSamples
                        .Where(s => s.Status == TrainingSampleStatus.Approved)
                        .ToList();
                    var indexed = await kbManager.RebuildAsync(newSamples, null, ct);
                    kbManager.CreateVersion($"Lernen aus Protokoll {DateTime.Now:yyyy-MM-dd HH:mm}");
                    Log($"KB fertig: {indexed}/{newSamples.Count} indiziert");
                }
                else
                {
                    Log("KB-Indexierung übersprungen (Ollama nicht erreichbar).");
                }
                await RefreshKbStatusAsync();
            }

            Log($"═══ Lernen aus Protokoll: {totalNew} neue Samples, {casesWithProtocol.Count} Fälle ═══");
            StatusText = $"Fertig: {totalNew} neue Samples aus Protokoll gelernt.";
        }
        catch (OperationCanceledException)
        {
            StatusText = "Abgebrochen.";
        }
        catch (Exception ex)
        {
            Log($"FEHLER: {ex.Message}");
            StatusText = $"Fehler: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task<AuswertungPro.Next.Domain.Protocol.ProtocolDocument?> LoadProtocolDocAsync(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !System.IO.File.Exists(path)) return null;
        var ext = System.IO.Path.GetExtension(path).ToLowerInvariant();
        try
        {
            if (ext == ".json")
            {
                var json = System.IO.File.ReadAllText(path);
                return System.Text.Json.JsonSerializer.Deserialize<AuswertungPro.Next.Domain.Protocol.ProtocolDocument>(
                    json, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            if (ext == ".pdf")
            {
                var extractor = new AuswertungPro.Next.UI.Ai.Training.Services.PdfProtocolExtractor();
                var framesDir = FrameStore.GetFramesDir(null);
                var entries = await extractor.ExtractAsync(path, framesDir).ConfigureAwait(false);
                if (entries.Count == 0) return null;
                return new AuswertungPro.Next.Domain.Protocol.ProtocolDocument
                {
                    Current = new AuswertungPro.Next.Domain.Protocol.ProtocolRevision
                    {
                        Entries = entries.Select(e => new AuswertungPro.Next.Domain.Protocol.ProtocolEntry
                        {
                            Code = e.VsaCode,
                            Beschreibung = e.Text,
                            MeterStart = e.MeterStart,
                            MeterEnd = e.MeterEnd,
                            IsStreckenschaden = e.IsStreckenschaden,
                            Zeit = e.Zeit,
                            Source = AuswertungPro.Next.Domain.Protocol.ProtocolEntrySource.Imported,
                            FotoPaths = e.ExtractedFramePath is not null
                                ? new List<string> { e.ExtractedFramePath }
                                : new List<string>()
                        }).ToList()
                    }
                };
            }
        }
        catch { /* ignorieren */ }
        return null;
    }

    /// <summary>
    /// Exportiert Approved-Samples im YOLO-Format über den Sidecar.
    /// Erzeugt images/, labels/ und data.yaml für YOLO-Training.
    /// </summary>
    [RelayCommand]
    private async Task ExportYoloAsync()
    {
        if (IsBusy) return;

        var approved = Samples
            .Where(s => s.Status == TrainingSampleStatus.Approved
                        && !string.IsNullOrWhiteSpace(s.FramePath)
                        && File.Exists(s.FramePath))
            .ToList();

        if (approved.Count == 0)
        {
            StatusText = "Keine Approved-Samples mit gültigen Frames vorhanden.";
            Log("YOLO-Export: Keine exportierbaren Samples gefunden.");
            return;
        }

        // Zielordner wählen
        var dlg = new OpenFolderDialog { Title = "YOLO-Export Zielordner wählen" };
        if (dlg.ShowDialog() != true)
            return;

        var outputDir = dlg.FolderName;

        _genCts?.Cancel();
        _genCts?.Dispose();
        _genCts = new CancellationTokenSource();
        var ct = _genCts.Token;

        try
        {
            IsBusy = true;
            Log($"YOLO-Export: {approved.Count} Samples → {outputDir}");
            StatusText = $"YOLO-Export: {approved.Count} Samples werden vorbereitet...";

            // Sidecar-Verbindung prüfen
            var pipelineCfg = PipelineConfig.Load();
            var client = new VisionPipelineClient(pipelineCfg.SidecarUrl);

            var health = await client.HealthCheckAsync(ct).ConfigureAwait(false);
            if (health is null)
            {
                // Fallback: lokaler Export ohne Sidecar
                Log($"Sidecar nicht erreichbar ({pipelineCfg.SidecarUrl}). Versuche lokalen Export...");
                await ExportYoloLocalAsync(approved, outputDir, ct).ConfigureAwait(false);
                return;
            }

            Log($"Sidecar erreichbar: v{health.Version}, GPU: {health.Gpu?.CurrentModel ?? "?"}");

            // Samples zu DTOs konvertieren
            ProgressMax = approved.Count;
            ProgressValue = 0;

            var exportSamples = new List<TrainingExportSample>();
            for (var i = 0; i < approved.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                var s = approved[i];
                ProgressValue = i + 1;
                StatusText = $"YOLO-Export: Lade Frame {i + 1}/{approved.Count}...";

                var bytes = await File.ReadAllBytesAsync(s.FramePath, ct).ConfigureAwait(false);
                var base64 = Convert.ToBase64String(bytes);

                var labels = new List<TrainingExportSampleLabel>();
                if (!string.IsNullOrWhiteSpace(s.Code))
                {
                    labels.Add(new TrainingExportSampleLabel(
                        ClassName: s.Code,
                        XCenter: 0.5, YCenter: 0.5,
                        Width: 0.8, Height: 0.8));
                }

                exportSamples.Add(new TrainingExportSample(base64, labels));
            }

            StatusText = $"YOLO-Export: Sende {exportSamples.Count} Samples an Sidecar...";
            var request = new TrainingExportRequestDto(exportSamples, outputDir, 0.8);
            var response = await client.ExportTrainingAsync(request, ct).ConfigureAwait(false);

            // Samples als exportiert markieren
            foreach (var s in approved)
                s.ExportedUtc = DateTime.UtcNow;
            await PersistSamplesAsync();

            var msg = $"YOLO-Export fertig: {response.TotalSamples} Samples " +
                      $"({response.TrainCount} Train, {response.ValCount} Val), " +
                      $"{response.ClassesUsed.Count} Klassen → {outputDir}";
            Log(msg);
            Log($"  data.yaml: {response.DataYamlPath}");
            Log($"  Klassen: {string.Join(", ", response.ClassesUsed)}");
            StatusText = msg;
        }
        catch (OperationCanceledException)
        {
            Log("YOLO-Export abgebrochen.");
            StatusText = "YOLO-Export abgebrochen.";
        }
        catch (Exception ex)
        {
            Log($"YOLO-Export FEHLER: {ex.Message}");
            StatusText = $"YOLO-Export fehlgeschlagen: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Lokaler YOLO-Export ohne Sidecar — schreibt Bilder und Labels direkt.
    /// </summary>
    private async Task ExportYoloLocalAsync(
        List<TrainingSample> approved, string outputDir, CancellationToken ct)
    {
        var imgTrain = Path.Combine(outputDir, "images", "train");
        var imgVal = Path.Combine(outputDir, "images", "val");
        var lblTrain = Path.Combine(outputDir, "labels", "train");
        var lblVal = Path.Combine(outputDir, "labels", "val");
        foreach (var d in new[] { imgTrain, imgVal, lblTrain, lblVal })
            Directory.CreateDirectory(d);

        // Klassen sammeln
        var classSet = approved
            .Select(s => s.Code)
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(c => c)
            .ToList();
        var classMap = classSet.Select((c, i) => (c, i)).ToDictionary(x => x.c, x => x.i, StringComparer.OrdinalIgnoreCase);

        var splitIdx = (int)(approved.Count * 0.8);
        ProgressMax = approved.Count;

        for (var i = 0; i < approved.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var s = approved[i];
            ProgressValue = i + 1;
            StatusText = $"Lokaler YOLO-Export: {i + 1}/{approved.Count}...";

            var isTrain = i < splitIdx;
            var imgDir = isTrain ? imgTrain : imgVal;
            var lblDir = isTrain ? lblTrain : lblVal;

            // Bild kopieren
            var ext = Path.GetExtension(s.FramePath);
            var imgDst = Path.Combine(imgDir, $"sample_{i:D6}{ext}");
            File.Copy(s.FramePath, imgDst, overwrite: true);

            // Label schreiben
            var lblPath = Path.Combine(lblDir, $"sample_{i:D6}.txt");
            if (classMap.TryGetValue(s.Code, out var clsIdx))
                await File.WriteAllTextAsync(lblPath, $"{clsIdx} 0.500000 0.500000 0.800000 0.800000", ct);
            else
                await File.WriteAllTextAsync(lblPath, "", ct);

            s.ExportedUtc = DateTime.UtcNow;
        }

        // data.yaml
        var yamlPath = Path.Combine(outputDir, "data.yaml");
        var yamlLines = new[]
        {
            $"path: {Path.GetFullPath(outputDir)}",
            "train: images/train",
            "val: images/val",
            $"nc: {classSet.Count}",
            $"names: [{string.Join(", ", classSet.Select(c => $"'{c}'"))}]"
        };
        await File.WriteAllLinesAsync(yamlPath, yamlLines, ct);
        await PersistSamplesAsync();

        var trainCount = splitIdx;
        var valCount = approved.Count - splitIdx;
        var msg = $"Lokaler YOLO-Export fertig: {approved.Count} Samples " +
                  $"({trainCount} Train, {valCount} Val), " +
                  $"{classSet.Count} Klassen → {outputDir}";
        Log(msg);
        Log($"  data.yaml: {yamlPath}");
        Log($"  Klassen: {string.Join(", ", classSet)}");
        StatusText = msg;
    }

    /// <summary>
    /// Batch-Import: Scannt alle Ordner, generiert Samples, approved automatisch,
    /// indiziert in die Knowledge Base. Alles in einem Durchlauf.
    /// </summary>
    [RelayCommand]
    private async Task BatchImportAndIndexAsync()
    {
        if (IsBusy) return;
        if (_rootFolders.Count == 0)
        {
            StatusText = "Bitte zuerst einen oder mehrere Ordner wählen.";
            return;
        }

        _genCts?.Cancel();
        _genCts?.Dispose();
        _genCts = new CancellationTokenSource();
        var ct = _genCts.Token;

        using var _aiToken = AiTrack.Begin("Training Center");
        try
        {
            IsBusy = true;
            LogText = "";
            ProgressValue = 0;
            ProgressMax = 1;
            ClearLivePreview();

            // 1. Scan aller Root-Ordner
            Log($"Scanne {_rootFolders.Count} Ordner...");
            StatusText = "Scanne Ordner...";
            var found = new List<TrainingCase>();
            foreach (var folder in _rootFolders)
            {
                if (!Directory.Exists(folder))
                {
                    Log($"  WARNUNG: Ordner existiert nicht: {folder}");
                    continue;
                }
                Log($"  Scanne: {folder}");
                var result = await _import.ScanAsync(folder);
                found.AddRange(result);
            }
            var casesWithProtocol = found.Where(c => !string.IsNullOrEmpty(c.ProtocolPath)).ToList();

            Log($"Gefunden: {found.Count} Ordner, {casesWithProtocol.Count} mit Protokoll");
            foreach (var c in found)
            {
                var hasVideo = !string.IsNullOrEmpty(c.VideoPath) ? "Video" : "kein Video";
                var hasProto = !string.IsNullOrEmpty(c.ProtocolPath) ? Path.GetFileName(c.ProtocolPath) : "kein Protokoll";
                Log($"  {c.CaseId}: {hasVideo}, {hasProto}");
            }

            StatusText = $"Gefunden: {found.Count} Ordner, {casesWithProtocol.Count} mit Protokoll";

            Cases.Clear();
            foreach (var c in found)
                Cases.Add(c);

            if (casesWithProtocol.Count == 0)
            {
                Log("STOP: Keine Ordner mit Protokoll-Dateien gefunden.");
                StatusText = "Keine Ordner mit Protokoll-Dateien gefunden.";
                return;
            }

            // 2. Generate samples for all cases
            var cfg = AiRuntimeConfig.Load();
            Log($"AI Config: Enabled={cfg.Enabled}, ffmpeg={cfg.FfmpegPath}");

            var settings = await TrainingCenterSettingsStore.LoadAsync();
            var meterSvc = CreateMeterTimelineService(cfg, settings.GpuConcurrency);
            var generator = new TrainingSampleGenerator(cfg, meterSvc, settings);

            var allSamples = await TrainingSamplesStore.LoadAsync();
            var existingSigs = allSamples.Select(s => s.Signature).ToHashSet(StringComparer.Ordinal);
            Log($"Bestehende Samples: {allSamples.Count} ({existingSigs.Count} Signaturen)");

            ProgressMax = casesWithProtocol.Count;
            var totalNew = 0;
            var errors = 0;
            var lastError = "";
            var emptyProtocols = 0;
            var duplicateOnlyCases = 0;
            var missingProtocols = 0;
            var unreadableProtocols = 0;
            for (var i = 0; i < casesWithProtocol.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                var tc = casesWithProtocol[i];
                ProgressValue = i + 1;
                StatusText = $"[{i + 1}/{casesWithProtocol.Count}] {tc.CaseId}...";
                Log($"--- [{i + 1}/{casesWithProtocol.Count}] {tc.CaseId} ---");
                Log($"  Protokoll: {tc.ProtocolPath}");
                Log($"  Video: {(string.IsNullOrEmpty(tc.VideoPath) ? "keins" : tc.VideoPath)}");

                try
                {
                    // Preview-Frame extrahieren (immer, auch bei Duplikaten)
                    var previewFrame = await ExtractPreviewFrameAsync(tc, cfg, ct);
                    if (!string.IsNullOrEmpty(previewFrame))
                        UpdateLivePreview(tc.CaseId, "Verarbeite...", "—", previewFrame);
                    else
                        UpdateLivePreview(tc.CaseId, "Verarbeite...", "—", null);

                    var generation = await generator.GenerateWithDiagnosticsAsync(tc, existingSigs, framesDir: null, ct);
                    var newSamples = generation.Samples;

                    if (newSamples.Count == 0)
                    {
                        switch (generation.Outcome)
                        {
                            case TrainingSampleGenerationOutcome.OnlyDuplicates:
                                duplicateOnlyCases++;
                                Log($"  -> 0 Samples (alle {generation.ParsedEntries} Einträge bereits als Duplikat vorhanden)");
                                UpdateLivePreview(tc.CaseId, $"{generation.ParsedEntries} Duplikate", "bereits vorhanden", previewFrame);
                                break;

                            case TrainingSampleGenerationOutcome.ProtocolFileMissing:
                                missingProtocols++;
                                Log("  -> 0 Samples (Protokolldatei fehlt)");
                                UpdateLivePreview(tc.CaseId, "—", "Protokoll fehlt", previewFrame);
                                break;

                            case TrainingSampleGenerationOutcome.ProtocolUnreadable:
                                unreadableProtocols++;
                                Log("  -> 0 Samples (Protokoll nicht lesbar)");
                                UpdateLivePreview(tc.CaseId, "—", "nicht lesbar", previewFrame);
                                break;

                            default:
                                emptyProtocols++;
                                Log("  -> 0 Samples (keine Protokolleinträge erkannt)");
                                UpdateLivePreview(tc.CaseId, "—", "keine Einträge", previewFrame);
                                break;
                        }
                    }
                    else
                    {
                        foreach (var s in newSamples)
                        {
                            s.Status = TrainingSampleStatus.Approved;
                            existingSigs.Add(s.Signature);

                            // Live-Vorschau für jedes Sample aktualisieren
                            var sampleFrame = !string.IsNullOrEmpty(s.FramePath) ? s.FramePath : previewFrame;
                            UpdateLivePreview(
                                tc.CaseId,
                                s.Code,
                                $"{s.MeterStart:F2} – {s.MeterEnd:F2} m",
                                sampleFrame);
                        }
                        allSamples.AddRange(newSamples);
                        totalNew += newSamples.Count;

                        // Live in UI-Tabelle einfügen (nicht erst am Ende)
                        foreach (var s in newSamples)
                            Samples.Add(s);

                        Log($"  -> {newSamples.Count} Samples generiert:");
                        foreach (var s in newSamples)
                            Log($"     {s.Code} @ {s.MeterStart:F2}m - {s.Beschreibung}");
                    }
                }
                catch (Exception ex)
                {
                    errors++;
                    lastError = ex.Message;
                    Log($"  FEHLER: {ex.Message}");
                }
            }

            // 3. Persist samples (UI-Collection ist bereits aktuell)
            Log($"Speichere {allSamples.Count} Samples...");
            await TrainingSamplesStore.SaveAsync(allSamples);

            // Diagnosemeldung bei 0 Samples
            if (totalNew == 0 && casesWithProtocol.Count > 0)
            {
                var diag = $"0 Samples generiert aus {casesWithProtocol.Count} Fällen.";
                if (errors > 0)
                    diag += $" {errors} Fehler (letzter: {lastError}).";
                if (emptyProtocols > 0)
                    diag += $" {emptyProtocols} Protokolle ohne erkannte Einträge.";
                if (duplicateOnlyCases > 0)
                    diag += $" {duplicateOnlyCases} Fälle nur mit bereits vorhandenen Duplikaten.";
                if (missingProtocols > 0)
                    diag += $" {missingProtocols} fehlende Protokolldateien.";
                if (unreadableProtocols > 0)
                    diag += $" {unreadableProtocols} nicht lesbare Protokolle.";
                diag += " Prüfe: PDF-Format, Protokoll-Einträge oder Duplikat-Filter.";
                Log(diag);
                StatusText = diag;
                return;
            }

            Log($"Zwischenergebnis: {totalNew} neue Samples, {errors} Fehler, {emptyProtocols} ohne Einträge, {duplicateOnlyCases} nur Duplikate");
            StatusText = $"Samples generiert: {totalNew} neu. Starte KB-Indexierung...";

            // 4. Index approved samples into Knowledge Base
            var approvedSamples = allSamples
                .Where(s => s.Status == TrainingSampleStatus.Approved)
                .ToList();

            if (approvedSamples.Count > 0)
            {
                Log($"KB-Indexierung: {approvedSamples.Count} Samples...");

                var ollamaConfig = OllamaConfig.Load();
                Log($"Ollama: {ollamaConfig.BaseUri}, Embed-Modell: {ollamaConfig.EmbedModel}");

                // Ollama erreichbar prüfen
                var ollamaReachable = await CheckOllamaReachableAsync(ollamaConfig, ct);
                if (!ollamaReachable)
                {
                    Log($"⚠ Ollama NICHT erreichbar auf {ollamaConfig.BaseUri}.");
                    Log("  KB-Indexierung übersprungen. Starte Ollama und führe 'Batch-Import + KB' erneut aus.");
                    Log($"  Benötigtes Embed-Modell: {ollamaConfig.EmbedModel}");
                    Log($"  → Installieren: ollama pull {ollamaConfig.EmbedModel}");
                    StatusText = $"Fertig! {totalNew} Samples generiert. KB-Indexierung übersprungen (Ollama nicht erreichbar)." +
                                 (errors > 0 ? $" {errors} Fehler." : "");
                }
                else
                {
                    ProgressValue = 0;
                    ProgressMax = approvedSamples.Count;

                    try
                    {
                        var http = new System.Net.Http.HttpClient { Timeout = ollamaConfig.RequestTimeout };
                        using var kbCtx = new KnowledgeBaseContext();
                        var embedder = new EmbeddingService(http, ollamaConfig);
                        var kbManager = new KnowledgeBaseManager(kbCtx, embedder);

                        var indexed = await kbManager.RebuildAsync(
                            approvedSamples,
                            new Progress<int>(n =>
                            {
                                ProgressValue = n;
                                StatusText = $"KB-Indexierung: {n}/{approvedSamples.Count}...";
                            }),
                            ct,
                            concurrency: settings.GpuConcurrency);

                        kbManager.CreateVersion($"Batch-Import {DateTime.Now:yyyy-MM-dd HH:mm}");

                        Log($"KB fertig: {indexed}/{approvedSamples.Count} indiziert");
                        StatusText = $"Fertig! {totalNew} Samples, {indexed}/{approvedSamples.Count} in KB" +
                                     (errors > 0 ? $", {errors} Fehler" : "");

                        await RefreshKbStatusAsync();
                    }
                    catch (Exception kbEx)
                    {
                        Log($"KB-Indexierung FEHLER: {kbEx.Message}");
                        Log("  Samples wurden gespeichert. KB-Indexierung kann später wiederholt werden.");
                        StatusText = $"Fertig! {totalNew} Samples generiert. KB-Fehler: {kbEx.Message}";
                    }
                }
            }
            else
            {
                Log("Keine Approved-Samples zum Indizieren.");
                StatusText = $"Fertig! {totalNew} Samples, keine zum Indizieren" +
                             (errors > 0 ? $" ({errors} Fehler)" : "");
            }

            // 5. Automatischer YOLO-Export in Default-Ordner
            var yoloExportable = allSamples
                .Where(s => s.Status == TrainingSampleStatus.Approved
                            && !string.IsNullOrWhiteSpace(s.FramePath)
                            && File.Exists(s.FramePath))
                .ToList();

            if (yoloExportable.Count > 0)
            {
                var yoloDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "AuswertungPro", "yolo_dataset");
                try
                {
                    Log($"Automatischer YOLO-Export: {yoloExportable.Count} Samples → {yoloDir}");
                    StatusText = $"YOLO-Export: {yoloExportable.Count} Samples...";
                    await ExportYoloLocalAsync(yoloExportable, yoloDir, ct).ConfigureAwait(false);
                    Log($"YOLO-Export abgeschlossen → {yoloDir}");
                }
                catch (Exception yoloEx)
                {
                    Log($"YOLO-Export FEHLER: {yoloEx.Message}");
                    Log("  Manueller Export über 'YOLO Export...' Button möglich.");
                }
            }

            // 6. Save cases
            await _store.SaveAsync(new TrainingCenterState
            {
                Cases = Cases.ToList(),
                UpdatedUtc = DateTime.UtcNow
            });
            Log("Fälle gespeichert. Batch-Import abgeschlossen.");
        }
        catch (OperationCanceledException)
        {
            Log("Batch-Import abgebrochen durch Benutzer.");
            StatusText = "Batch-Import abgebrochen.";
        }
        catch (Exception ex)
        {
            Log($"FATALER FEHLER: {ex.Message}");
            StatusText = $"Fehler beim Batch-Import: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void CancelBatch()
    {
        _genCts?.Cancel();
        StatusText = "Abbruch angefordert...";
    }

    [RelayCommand]
    private async Task CheckKnowledgeBaseAsync()
    {
        if (IsBusy) return;

        try
        {
            IsBusy = true;
            StatusText = "Prüfe Knowledge Base...";

            var summary = await Task.Run(() =>
            {
                using var db = new KnowledgeBaseContext();
                var diag = new KnowledgeBaseDiagnosticsService(db);
                return diag.ReadSummary(12);
            });

            Log($"KB-Stand: Samples={summary.SampleCount}, Embeddings={summary.EmbeddingCount}, Versionen={summary.VersionCount}");
            if (summary.LatestVersionAtUtc is not null)
            {
                var latest = summary.LatestVersionAtUtc.Value.ToLocalTime();
                var notes = string.IsNullOrWhiteSpace(summary.LatestVersionNotes)
                    ? "-"
                    : summary.LatestVersionNotes;
                Log($"Letzte Version: {latest:yyyy-MM-dd HH:mm} ({summary.LatestVersionSampleCount} Samples) | Notiz: {notes}");
            }

            if (summary.TopCodes.Count > 0)
            {
                Log("Top-Codes:");
                foreach (var c in summary.TopCodes)
                    Log($"  {c.VsaCode}: {c.Count}");
            }
            else
            {
                Log("Top-Codes: keine Einträge vorhanden.");
            }

            StatusText = $"KB geprüft: {summary.SampleCount} Samples, {summary.EmbeddingCount} Embeddings, {summary.VersionCount} Versionen.";

            await RefreshKbStatusAsync();
        }
        catch (Exception ex)
        {
            StatusText = $"KB-Prüfung fehlgeschlagen: {ex.Message}";
            Log($"KB-Prüfung FEHLER: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    partial void OnSelectedSampleChanged(TrainingSample? value)
    {
        ApproveSampleCommand.NotifyCanExecuteChanged();
        RejectSampleCommand.NotifyCanExecuteChanged();
        SelectedSampleMeter = value?.MeterStart ?? 0;
    }

    /// <summary>Aktualisiert SamplesMaxMeter aus der aktuellen Sample-Liste.</summary>
    public void RefreshSamplesMaxMeter()
    {
        SamplesMaxMeter = Samples.Count > 0
            ? Samples.Max(s => Math.Max(s.MeterStart, s.MeterEnd))
            : 0;
    }

    /// <summary>
    /// Extrahiert einen einzelnen Preview-Frame aus dem Video (bei Sekunde 2).
    /// Wird für die Live-Vorschau genutzt, auch wenn keine neuen Samples generiert werden.
    /// </summary>
    private static async Task<string?> ExtractPreviewFrameAsync(TrainingCase tc, AiRuntimeConfig cfg, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(tc.VideoPath) || !File.Exists(tc.VideoPath))
            return null;

        var ffmpeg = cfg.FfmpegPath ?? "ffmpeg";
        var sampleId = $"preview_{Regex.Replace(tc.CaseId, @"[^\w\-]", "_")}";
        try
        {
            return await FrameStore.ExtractAndStoreAsync(ffmpeg, tc.VideoPath, 2.0, sampleId, null, ct);
        }
        catch
        {
            return null;
        }
    }

    private static MeterTimelineService CreateMeterTimelineService(AiRuntimeConfig cfg, int concurrency = 1)
    {
        if (!cfg.Enabled)
            return new MeterTimelineService(cfg);

        var ollamaClient = cfg.CreateOllamaClient();
        var vision = new OllamaVisionFindingsService(ollamaClient, cfg.VisionModel);
        var osd = new OsdMeterDetectionService(vision);
        return new MeterTimelineService(cfg, osd, concurrency);
    }

    private async Task PersistSamplesAsync()
    {
        await TrainingSamplesStore.SaveAsync(Samples.ToList());
    }

    /// <summary>
    /// Prüft ob Ollama erreichbar ist (GET /api/tags).
    /// </summary>
    private static async Task<bool> CheckOllamaReachableAsync(OllamaConfig config, CancellationToken ct)
    {
        try
        {
            using var http = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            var resp = await http.GetAsync(new Uri(config.BaseUri, "/api/tags"), ct).ConfigureAwait(false);
            return resp.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    // ── Review Queue (Self-Improving Loop) ──────────────────────────────

    /// <summary>Loads pending review items into the queue.</summary>
    public void LoadReviewQueue(Ai.SelfImproving.ReviewQueueService queueService)
    {
        ReviewQueue.Clear();
        foreach (var item in queueService.GetAll())
            ReviewQueue.Add(item);
        ReviewQueueCount = ReviewQueue.Count;
        ReviewStatusText = $"{ReviewQueueCount} Einträge zur Prüfung";
    }

    /// <summary>Approve a review item (accept the suggested code).</summary>
    public async Task ApproveReviewItemAsync(
        Ai.SelfImproving.ReviewQueueItem item,
        Ai.SelfImproving.FeedbackIngestionService feedback,
        Ai.SelfImproving.ReviewQueueService queueService,
        CancellationToken ct = default)
    {
        await feedback.ProcessFeedbackAsync(
            item.Entry, item.Entry.SuggestedCode ?? "", accepted: true, ct).ConfigureAwait(false);
        queueService.Remove(item.Id);
        ReviewQueue.Remove(item);
        ReviewQueueCount = ReviewQueue.Count;
        ReviewStatusText = $"Approved: {item.SuggestedCode} | {ReviewQueueCount} verbleibend";
        Log($"Review Approved: {item.Label} → {item.SuggestedCode}");
    }

    /// <summary>Reject a review item with a corrected code.</summary>
    public async Task RejectReviewItemAsync(
        Ai.SelfImproving.ReviewQueueItem item,
        string correctedCode,
        Ai.SelfImproving.FeedbackIngestionService feedback,
        Ai.SelfImproving.ReviewQueueService queueService,
        CancellationToken ct = default)
    {
        await feedback.ProcessFeedbackAsync(
            item.Entry, correctedCode, accepted: false, ct).ConfigureAwait(false);
        queueService.Remove(item.Id);
        ReviewQueue.Remove(item);
        ReviewQueueCount = ReviewQueue.Count;
        ReviewStatusText = $"Rejected: {item.SuggestedCode} → {correctedCode} | {ReviewQueueCount} verbleibend";
        Log($"Review Rejected: {item.Label} → {item.SuggestedCode} korrigiert zu {correctedCode}");
    }
}
