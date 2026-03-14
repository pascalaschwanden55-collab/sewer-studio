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
using AuswertungPro.Next.UI.Ai.Ollama;
using AuswertungPro.Next.UI.Ai.Pipeline;
using AuswertungPro.Next.UI.Ai.Training;
using AuswertungPro.Next.UI.Services;
using AiTrack = AuswertungPro.Next.UI.Services.AiActivityTracker;

/// <summary>
/// ViewModel fuer das Training Center.
/// Vereinfacht: PDF-basiertes Training via Selbsttraining-Tab.
/// Kein Batch-Import, keine KB-Indexierung, kein YOLO-Export, keine Review Queue.
/// </summary>
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
    [ObservableProperty] private int _progressValue;
    [ObservableProperty] private int _progressMax = 1;

    // PipeGraphTimeline: Max-Meter der aktuellen Samples, aktuelle Sample-Position
    [ObservableProperty] private double _samplesMaxMeter;
    [ObservableProperty] private double _selectedSampleMeter;

    private readonly List<string> _rootFolders = new();
    private CancellationTokenSource? _genCts;

    /// <summary>Einfache Sample-Zaehler-Anzeige fuer die Status-Leiste.</summary>
    public string SampleCountDisplay => Samples.Count.ToString();

    private void RefreshSampleCountDisplay() => OnPropertyChanged(nameof(SampleCountDisplay));

    /// <summary>Fügt eine Zeile zum Status hinzu (Thread-safe via Dispatcher).</summary>
    private void Log(string message)
    {
        var line = $"[{DateTime.Now:HH:mm:ss}] {message}";
        void Apply() => StatusText = line;
        if (System.Windows.Application.Current?.Dispatcher is { } d && !d.CheckAccess())
            d.Invoke(Apply);
        else
            Apply();
    }

    public TrainingCenterViewModel(TrainingCenterStore store, TrainingCenterImportService import)
    {
        _store = store;
        _import = import;

        // Selbsttraining-Faelle aktualisieren wenn Cases sich aendern
        Cases.CollectionChanged += (_, _) => OnPropertyChanged(nameof(SelfTrainingCases));
        Samples.CollectionChanged += (_, _) => RefreshSampleCountDisplay();
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

    /// <summary>
    /// Scan: Sucht nach PDF-Protokollen in den gewaehlten Ordnern.
    /// Video ist nicht erforderlich — nur Protokoll zaehlt.
    /// </summary>
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
            StatusText = "Scanne Ordner nach PDF-Protokollen...";
            Cases.Clear();

            foreach (var folder in _rootFolders)
            {
                if (!Directory.Exists(folder)) continue;
                // ScanProtocolOnlyAsync findet auch Ordner ohne Video
                var found = await _import.ScanProtocolOnlyAsync(folder);
                foreach (var c in found)
                    Cases.Add(c);
            }

            var withProto = Cases.Count(c => !string.IsNullOrEmpty(c.ProtocolPath));
            StatusText = $"Gefunden: {Cases.Count} Fälle ({withProto} mit PDF-Protokoll)";
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
            StatusText = $"Protokoll-Training: {approved.Count} Samples als Few-Shot-Beispiele gespeichert ({codes.Count} Codes).";
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

    // ── Few-Shot Beispiel-Bibliothek ────────────────────────────────────────

    [ObservableProperty] private string _fewShotStatus = "";
    [ObservableProperty] private bool _isBuildingFewShot;

    /// <summary>
    /// Baut die Few-Shot Beispiel-Bibliothek aus allen geladenen Faellen auf.
    /// Extrahiert Fotos + Codes aus PDFs und speichert sie als kuratierte Beispiele.
    /// </summary>
    [RelayCommand]
    private async Task BuildFewShotLibrary()
    {
        if (IsBuildingFewShot) return;
        if (_rootFolders.Count == 0 && Cases.Count == 0)
        {
            FewShotStatus = "Bitte zuerst Ordner waehlen und scannen.";
            return;
        }

        IsBuildingFewShot = true;

        try
        {
            var store = new FewShotExampleStore();
            var builder = new FewShotExampleBuilder(store);

            var progress = new Progress<FewShotBuildProgress>(p =>
            {
                System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
                {
                    FewShotStatus = $"{p.CurrentFolder}/{p.TotalFolders} — {p.FolderName} " +
                        $"({p.ExamplesAdded} Beispiele, {p.ExamplesSkipped} uebersprungen)";
                    if (p.Message != null)
                        Log($"  [FewShot] {p.Message}");
                });
            });

            // Alle Root-Ordner scannen
            int totalAdded = 0, totalSkipped = 0;
            foreach (var folder in _rootFolders)
            {
                if (!Directory.Exists(folder)) continue;
                var result = await Task.Run(() => builder.BuildFromFolderAsync(folder, progress));
                totalAdded += result.ExamplesAdded;
                totalSkipped += result.ExamplesSkipped;
            }

            FewShotStatus = $"Fertig: {store.Examples.Count} Beispiele in Bibliothek " +
                $"({totalAdded} neu, {totalSkipped} uebersprungen) — {store.GetSummary()}";
            Log($"Few-Shot Bibliothek: {store.GetSummary()}");
        }
        catch (Exception ex)
        {
            FewShotStatus = $"Fehler: {ex.Message}";
        }
        finally
        {
            IsBuildingFewShot = false;
        }
    }

    // ── Selbsttraining (PDF-basiert) ────────────────────────────────────────

    [ObservableProperty] private bool _isSelfTrainingRunning;
    [ObservableProperty] private bool _isSelfTrainingPaused;
    [ObservableProperty] private string _selfTrainingStatus = "";
    [ObservableProperty] private string _selfTrainingFramePath = "";
    [ObservableProperty] private string _selfTrainingComparisonText = "";
    [ObservableProperty] private string _selfTrainingTechniqueText = "";
    [ObservableProperty] private string _selfTrainingStageName = "";
    [ObservableProperty] private int _selfTrainingProgress;
    [ObservableProperty] private int _selfTrainingTotal = 1;
    [ObservableProperty] private int _selfTrainingExactCount;
    [ObservableProperty] private int _selfTrainingPartialCount;
    [ObservableProperty] private int _selfTrainingMismatchCount;
    [ObservableProperty] private int _selfTrainingNoFindingsCount;
    [ObservableProperty] private string _selfTrainingCurrentCode = "";
    [ObservableProperty] private double _selfTrainingCurrentMeter;
    [ObservableProperty] private MatchLevel _selfTrainingLastMatchLevel;
    [ObservableProperty] private TrainingCase? _selfTrainingSelectedCase;

    // Multi-Case Auswahl (vom Code-Behind gesetzt)
    public List<TrainingCase> SelfTrainingSelectedCases { get; set; } = new();
    [ObservableProperty] private int _selfTrainingCaseIndex;
    [ObservableProperty] private int _selfTrainingCaseTotal;

    private ISelfTrainingOrchestrator? _selfTrainingOrch;
    private CancellationTokenSource? _selfTrainingCts;

    /// <summary>Verfuegbare Faelle fuer Selbsttraining (nur Protokoll noetig, kein Video).</summary>
    public System.Collections.Generic.IEnumerable<TrainingCase> SelfTrainingCases
        => Cases.Where(c => !string.IsNullOrEmpty(c.ProtocolPath));

    [RelayCommand]
    private async Task StartSelfTraining()
    {
        // Multi-Case: alle ausgewaehlten Faelle, Fallback auf Einzelauswahl
        var casesToRun = SelfTrainingSelectedCases.Count > 0
            ? SelfTrainingSelectedCases.ToList()
            : (SelfTrainingSelectedCase != null ? new List<TrainingCase> { SelfTrainingSelectedCase } : new());

        if (casesToRun.Count == 0) return;
        if (IsSelfTrainingRunning) return;

        IsSelfTrainingRunning = true;
        IsSelfTrainingPaused = false;
        SelfTrainingExactCount = 0;
        SelfTrainingPartialCount = 0;
        SelfTrainingMismatchCount = 0;
        SelfTrainingNoFindingsCount = 0;
        SelfTrainingCaseIndex = 0;
        SelfTrainingCaseTotal = casesToRun.Count;
        _selfTrainingLastShownEntry = -1;

        _selfTrainingCts = new CancellationTokenSource();
        var ct = _selfTrainingCts.Token;

        // Services erstellen via einheitliche Plattformkonfiguration
        var platform = AiPlatformConfig.Load();
        var cfg = platform.ToRuntimeConfig();
        var ollama = new OllamaClient(
            cfg.OllamaBaseUri,
            ownedTimeout: cfg.OllamaRequestTimeout,
            keepAlive: cfg.OllamaKeepAlive,
            numCtx: cfg.OllamaNumCtx);
        var enhancedVision = new EnhancedVisionAnalysisService(ollama, cfg.VisionModel);
        var comparison = new SelfTrainingComparisonService();
        var technique = new TechniqueAssessmentService(ollama, cfg.VisionModel);
        var pdfExtractor = new Ai.Training.Services.PdfProtocolExtractor();

        // Few-Shot Beispiele laden und aktivieren (verbessert Qwen-Erkennung)
        var fewShotStore = new FewShotExampleStore();
        try
        {
            await enhancedVision.EnableFewShotAsync(fewShotStore, ct);
            if (fewShotStore.Examples.Count > 0)
                Log($"Few-Shot: {fewShotStore.GetSummary()}");
            else
                Log("Few-Shot: Noch keine Beispiele — nach dem Training verfuegbar");
        }
        catch (Exception ex)
        {
            Log($"Few-Shot laden fehlgeschlagen: {ex.Message}");
        }

        var progress = new Progress<SelfTrainingStep>(OnSelfTrainingStep);

        int totalSamples = 0;
        int totalExact = 0, totalPartial = 0, totalMismatch = 0, totalNoFind = 0;

        try
        {
            for (int ci = 0; ci < casesToRun.Count; ci++)
            {
                ct.ThrowIfCancellationRequested();
                var tc = casesToRun[ci];
                SelfTrainingCaseIndex = ci + 1;
                SelfTrainingSelectedCase = tc;

                // Zaehler pro Fall zuruecksetzen fuer Anzeige
                SelfTrainingExactCount = 0;
                SelfTrainingPartialCount = 0;
                SelfTrainingMismatchCount = 0;
                SelfTrainingNoFindingsCount = 0;

                SelfTrainingStatus = $"Fall {ci + 1}/{casesToRun.Count}: {tc.CaseId}";
                Log($"Selbsttraining gestartet: {tc.CaseId} ({ci + 1}/{casesToRun.Count})");

                _selfTrainingOrch = new SelfTrainingOrchestrator(
                    enhancedVision, comparison, technique, pdfExtractor);

                var result = await Task.Run(() => _selfTrainingOrch.RunAsync(tc, progress, ct), ct);

                totalSamples += result.SamplesGenerated;
                totalExact += result.ExactMatches;
                totalPartial += result.PartialMatches;
                totalMismatch += result.Mismatches;
                totalNoFind += result.NoFindings;

                Log($"  → {tc.CaseId}: {result.SamplesGenerated} Samples, " +
                    $"{result.ExactMatches}T/{result.PartialMatches}P/{result.Mismatches}A/{result.NoFindings}L " +
                    $"in {result.Duration.TotalSeconds:F0}s");
            }

            // Gesamt-Zaehler anzeigen
            SelfTrainingExactCount = totalExact;
            SelfTrainingPartialCount = totalPartial;
            SelfTrainingMismatchCount = totalMismatch;
            SelfTrainingNoFindingsCount = totalNoFind;

            SelfTrainingStatus = $"Fertig: {casesToRun.Count} Fälle, {totalSamples} Samples — " +
                $"{totalExact} Treffer, {totalPartial} Teil, {totalMismatch} Abw, {totalNoFind} Leer";

            await LoadSamplesInternalAsync();
        }
        catch (OperationCanceledException)
        {
            SelfTrainingStatus = "Abgebrochen.";
        }
        catch (Exception ex)
        {
            SelfTrainingStatus = $"Fehler: {ex.Message}";
        }
        finally
        {
            IsSelfTrainingRunning = false;
            IsSelfTrainingPaused = false;
            _selfTrainingOrch = null;
        }
    }

    [RelayCommand]
    private void PauseSelfTraining()
    {
        if (_selfTrainingOrch == null) return;
        if (IsSelfTrainingPaused)
        {
            _selfTrainingOrch.Resume();
            IsSelfTrainingPaused = false;
            SelfTrainingStatus = "Fortgesetzt...";
        }
        else
        {
            _selfTrainingOrch.Pause();
            IsSelfTrainingPaused = true;
            SelfTrainingStatus = "Pausiert.";
        }
    }

    [RelayCommand]
    private void StopSelfTraining()
    {
        _selfTrainingCts?.Cancel();
    }

    private int _selfTrainingLastShownEntry = -1;

    private void OnSelfTrainingStep(SelfTrainingStep step)
    {
        void Apply()
        {
            SelfTrainingProgress = step.EntryIndex + 1;
            SelfTrainingTotal = step.TotalEntries;
            SelfTrainingCurrentCode = step.VsaCode;
            SelfTrainingCurrentMeter = step.MeterPosition;

            SelfTrainingStageName = step.Stage switch
            {
                SelfTrainingStage.BuildingTimeline => "OSD-Scan — Timeline wird aufgebaut...",
                SelfTrainingStage.ExtractingFrame => "PDF-Foto laden...",
                SelfTrainingStage.Analyzing => "Blinde KI-Analyse...",
                SelfTrainingStage.Comparing => "Vergleich mit Protokoll...",
                SelfTrainingStage.AssessingTechnique => "Technik-Bewertung...",
                SelfTrainingStage.Completed => "Abgeschlossen",
                _ => ""
            };

            // Wenn neuer Eintrag beginnt: alten Vergleichstext leeren
            if (step.EntryIndex != _selfTrainingLastShownEntry
                && step.Stage == SelfTrainingStage.ExtractingFrame)
            {
                SelfTrainingComparisonText = $"Protokoll: {step.VsaCode} @ {step.MeterPosition:F1}m — analysiere...";
                SelfTrainingTechniqueText = "";
                _selfTrainingLastShownEntry = step.EntryIndex;
            }

            // Frame, Vergleich und Technik nur bei Completed zusammen aktualisieren
            // → Bild und Text sind immer synchron
            if (step.Stage == SelfTrainingStage.Completed)
            {
                if (step.FramePath != null)
                    SelfTrainingFramePath = step.FramePath;

                if (step.Comparison != null)
                {
                    SelfTrainingComparisonText = step.Comparison.Explanation;
                    SelfTrainingLastMatchLevel = step.Comparison.Level;

                    switch (step.Comparison.Level)
                    {
                        case MatchLevel.ExactMatch: SelfTrainingExactCount++; break;
                        case MatchLevel.PartialMatch: SelfTrainingPartialCount++; break;
                        case MatchLevel.Mismatch: SelfTrainingMismatchCount++; break;
                        case MatchLevel.NoFindings: SelfTrainingNoFindingsCount++; break;
                    }
                }

                if (step.Technique != null)
                {
                    SelfTrainingTechniqueText =
                        $"OSD: {(step.Technique.OsdReadable ? "lesbar" : "nicht lesbar")}" +
                        (step.Technique.OsdDeltaMeters.HasValue ? $", Delta={step.Technique.OsdDeltaMeters:F2}m" : "") +
                        $" | Licht: {step.Technique.LightingQuality}" +
                        $" | Schärfe: {step.Technique.SharpnessQuality}" +
                        (step.Technique.CenteringQuality != null ? $" | Zentrierung: {step.Technique.CenteringQuality}" : "") +
                        $" | Note: {step.Technique.OverallGrade}";
                }
            }

            // KI-Fehler sichtbar im Log und Vergleichs-Panel anzeigen
            if (!string.IsNullOrEmpty(step.ErrorMessage))
            {
                Log($"  ⚠ {step.VsaCode}@{step.MeterPosition:F1}m: {step.ErrorMessage}");
                SelfTrainingComparisonText = step.ErrorMessage;
            }

            SelfTrainingStatus = step.Stage == SelfTrainingStage.BuildingTimeline
                ? "Phase 1: OSD-Scan — Timeline wird aufgebaut..."
                : $"{step.EntryIndex + 1}/{step.TotalEntries} — {step.VsaCode} @ {step.MeterPosition:F1}m — {SelfTrainingStageName}";
        }

        if (System.Windows.Application.Current?.Dispatcher is { } d && !d.CheckAccess())
            d.Invoke(Apply);
        else
            Apply();
    }
}
