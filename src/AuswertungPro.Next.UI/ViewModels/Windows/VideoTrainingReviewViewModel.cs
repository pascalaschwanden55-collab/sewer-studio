// AuswertungPro – Video-Selbsttraining Phase 2 — Review-ViewModel
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai.Teacher;
using AuswertungPro.Next.UI.Ai.Training;
using AuswertungPro.Next.UI.Ai.Training.Models;
using AiTrack = AuswertungPro.Next.UI.Services.AiActivityTracker;
using AuswertungPro.Next.Application.Ai.Teacher;
using AuswertungPro.Next.Application.Ai.Training.Models;
using AuswertungPro.Next.UI.Ai.KnowledgeBase;

namespace AuswertungPro.Next.UI.ViewModels.Windows;

/// <summary>
/// ViewModel fuer das Video-Selbsttraining Review-Fenster.
/// Zeigt die Differenz zwischen KI-Blinddurchlauf und Protokoll.
/// </summary>
public partial class VideoTrainingReviewViewModel : ObservableObject
{
    // Factory-Funktion statt fester Orchestrator — erstellt pro Durchlauf frischen HttpClient + Pipeline
    private readonly Func<VideoSelfTrainingOrchestrator>? _orchestratorFactory;
    private readonly IWinCanDbImportService? _winCanImport;
    private readonly IIbakImportService? _ibakImport;
    private readonly KbEnrichmentService? _enrichment;
    private CancellationTokenSource? _cts;

    public ObservableCollection<DifferenceEntryViewModel> Entries { get; } = new();

    [ObservableProperty] private DifferenceEntryViewModel? _selectedEntry;
    [ObservableProperty] private BitmapImage? _currentFrame;
    [ObservableProperty] private string _statusText = "Bereit — Video und Protokoll waehlen.";
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private int _progressValue;
    [ObservableProperty] private int _progressMax = 100;

    // Eingabefelder — Ordner-basiert
    [ObservableProperty] private string _folderPath = "";
    [ObservableProperty] private string _videoPath = "";
    [ObservableProperty] private string _protocolPath = "";
    [ObservableProperty] private string _protocolSourceType = ProtocolSourceTypes.InspektionsPdf;
    [ObservableProperty] private string _rohrmaterial = "";
    [ObservableProperty] private string _nennweiteText = "";
    [ObservableProperty] private string _haltungslaengeText = "";

    // Anzeige-Properties (aus Ordner automatisch befuellt)
    [ObservableProperty] private string _haltungId = "";
    [ObservableProperty] private string _videoFileName = "";

    // Markierungsmodus
    [ObservableProperty] private bool _isAnnotating;

    // Metriken (live aktualisiert)
    [ObservableProperty] private int _tpCount;
    [ObservableProperty] private int _fnCount;
    [ObservableProperty] private int _fpCount;
    [ObservableProperty] private int _mismatchCount;
    [ObservableProperty] private string _f1Text = "—";
    [ObservableProperty] private string _precisionText = "—";
    [ObservableProperty] private string _recallText = "—";

    // Ergebnis fuer Phase 3
    public VideoTrainingResult? LastResult { get; private set; }
    public DifferenceReport? LastReport => LastResult?.Report;

    public VideoTrainingReviewViewModel()
    {
        // Design-Time
    }

    public VideoTrainingReviewViewModel(
        Func<VideoSelfTrainingOrchestrator> orchestratorFactory,
        IWinCanDbImportService? winCanImport = null,
        IIbakImportService? ibakImport = null,
        KbEnrichmentService? enrichment = null)
    {
        _orchestratorFactory = orchestratorFactory;
        _winCanImport = winCanImport;
        _ibakImport = ibakImport;
        _enrichment = enrichment;
    }

    /// <summary>
    /// Setzt den Haltungsordner und sucht automatisch Video + PDF darin.
    /// Wird vom BrowseFolder_Click im Window aufgerufen.
    /// </summary>
    public void SetFolder(string folderPath)
    {
        FolderPath = folderPath;
        HaltungId = System.IO.Path.GetFileName(folderPath) ?? "";
        StatusText = "Ordner gewaehlt — suche Video und PDF...";

        // Video finden
        string[] videoExtensions = [".mp4", ".mpg", ".mpeg", ".avi"];
        VideoPath = "";
        VideoFileName = "";
        foreach (var ext in videoExtensions)
        {
            var videos = System.IO.Directory.GetFiles(folderPath, $"*{ext}");
            if (videos.Length > 0)
            {
                VideoPath = videos[0];
                VideoFileName = System.IO.Path.GetFileName(videos[0]);
                break;
            }
        }

        // PDF finden (Inspektions-PDF, nicht Plan)
        ProtocolPath = "";
        var pdfs = System.IO.Directory.GetFiles(folderPath, "*.pdf")
            .Where(p => !System.IO.Path.GetFileName(p).Contains("Plan", StringComparison.OrdinalIgnoreCase)
                     && !System.IO.Path.GetFileName(p).Contains("compressed", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(p => new System.IO.FileInfo(p).Length)
            .ToArray();

        if (pdfs.Length > 0)
        {
            // Bevorzugt PDF die den Haltungsnamen enthaelt
            ProtocolPath = pdfs.FirstOrDefault(p =>
                System.IO.Path.GetFileNameWithoutExtension(p)
                    .Contains(HaltungId.Replace(".", ""), StringComparison.OrdinalIgnoreCase))
                ?? pdfs[0];
        }

        ProtocolSourceType = ProtocolSourceTypes.InspektionsPdf;

        // Status aktualisieren
        var hasVideo = !string.IsNullOrEmpty(VideoPath);
        var hasPdf = !string.IsNullOrEmpty(ProtocolPath);
        StatusText = (hasVideo, hasPdf) switch
        {
            (true, true) => $"Bereit: {VideoFileName} + {System.IO.Path.GetFileName(ProtocolPath)}",
            (true, false) => "Kein Inspektions-PDF im Ordner gefunden.",
            (false, true) => "Kein Video im Ordner gefunden.",
            _ => "Weder Video noch PDF im Ordner gefunden."
        };

        StartAnalysisCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedEntryChanged(DifferenceEntryViewModel? value)
    {
        LoadFrame(value);
    }

    /// <summary>Startet den Video-Blinddurchlauf und die Differenzanalyse.</summary>
    [RelayCommand(CanExecute = nameof(CanStartAnalysis))]
    private async Task StartAnalysisAsync()
    {
        if (_orchestratorFactory is null) return;

        // Frischen Orchestrator erstellen (mit neuem HttpClient + Pipeline)
        var orchestrator = _orchestratorFactory();

        IsBusy = true;
        StatusText = "Analyse laeuft...";
        Entries.Clear();
        _cts = new CancellationTokenSource();
        using var _aiToken = AiTrack.Begin("Video-Blindtest");

        try
        {
            int? nennweite = int.TryParse(NennweiteText, out var nw) ? nw : null;

            double? inspLength = double.TryParse(
                HaltungslaengeText?.Replace(',', '.'),
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var hl) ? hl : null;

            var request = new VideoTrainingRequest
            {
                VideoPath = VideoPath,
                ProtocolSource = ProtocolPath,
                ProtocolSourceType = ProtocolSourceType,
                Rohrmaterial = string.IsNullOrWhiteSpace(Rohrmaterial) ? null : Rohrmaterial,
                NennweiteMm = nennweite,
                InspektionslaengeMeter = inspLength,
                FrameStepSeconds = 1.5,
                MeterTolerance = MeterTolerances.SingleTraining
            };

            // Protokoll laden (vereinfacht — im echten Code via Import-Service)
            var protocol = await LoadProtocolAsync(request, _cts.Token).ConfigureAwait(false);
            if (protocol is null)
            {
                StatusText = "Fehler: Protokoll konnte nicht geladen werden.";
                return;
            }

            var progress = new Progress<VideoTrainingProgress>(p =>
            {
                StatusText = $"[{p.Phase}] {p.Status}";
                ProgressValue = p.Total > 0 ? (int)((double)p.Current / p.Total * 100) : 0;
            });

            var result = await orchestrator.RunAsync(request, protocol, progress, _cts.Token)
                .ConfigureAwait(false);

            LastResult = result;

            // UI aktualisieren (muss auf UI-Thread)
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                PopulateEntries(result.Report);
                UpdateMetrics(result.Report);
                StatusText = $"Fertig in {result.Duration.TotalMinutes:F1} Min — F1={result.Report.F1:P0}";
            });
        }
        catch (OperationCanceledException)
        {
            StatusText = "Analyse abgebrochen.";
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

    private bool CanStartAnalysis() =>
        !IsBusy &&
        !string.IsNullOrWhiteSpace(VideoPath) &&
        !string.IsNullOrWhiteSpace(ProtocolPath);

    /// <summary>Bricht die laufende Analyse ab.</summary>
    [RelayCommand]
    private void CancelAnalysis()
    {
        _cts?.Cancel();
        StatusText = "Abbruch angefordert...";
    }

    /// <summary>Markiert den selektierten Eintrag als "KI korrekt".</summary>
    [RelayCommand]
    private void ApproveKi()
    {
        if (SelectedEntry is null) return;
        SelectedEntry.Decision = ReviewDecision.KiCorrect;
        SelectNext();
    }

    /// <summary>Markiert den selektierten Eintrag als "Protokoll korrekt".</summary>
    [RelayCommand]
    private void ApproveProtocol()
    {
        if (SelectedEntry is null) return;
        SelectedEntry.Decision = ReviewDecision.ProtocolCorrect;
        SelectNext();
    }

    /// <summary>Ignoriert den selektierten Eintrag.</summary>
    [RelayCommand]
    private void IgnoreEntry()
    {
        if (SelectedEntry is null) return;
        SelectedEntry.Decision = ReviewDecision.Ignored;
        SelectNext();
    }

    /// <summary>Uebergibt die Review-Entscheidungen an die KB-Anreicherung (Phase 3).</summary>
    [RelayCommand]
    private async Task SubmitReviewAsync()
    {
        if (_enrichment is null)
        {
            StatusText = "KB-Anreicherung nicht verfuegbar (Service fehlt).";
            return;
        }

        var reviewed = Entries.Where(e => e.Decision != ReviewDecision.Pending).ToList();
        if (reviewed.Count == 0)
        {
            StatusText = "Keine Eintraege bewertet — bitte zuerst reviewen.";
            return;
        }

        IsBusy = true;
        StatusText = "KB-Anreicherung laeuft...";
        using var _aiToken2 = AiTrack.Begin("KB-Anreicherung");

        try
        {
            int? nennweite = int.TryParse(NennweiteText, out var nw) ? nw : null;
            var result = await _enrichment.EnrichFromReviewAsync(
                reviewed,
                string.IsNullOrWhiteSpace(Rohrmaterial) ? null : Rohrmaterial,
                nennweite,
                _cts?.Token ?? CancellationToken.None).ConfigureAwait(false);

            // Annotierte Eintraege als TeacherAnnotation speichern
            int annotationCount = 0;
            var annotated = reviewed.Where(e => e.HasAnnotation && e.FramePath is not null).ToList();
            if (annotated.Count > 0)
            {
                var exportService = new TrainingAnnotationExportService();
                foreach (var entry in annotated)
                {
                    try
                    {
                        var vsaCode = entry.ProtocolCode ?? entry.KiCode ?? "";
                        var annotation = new TeacherAnnotation
                        {
                            VsaCode = vsaCode,
                            Beschreibung = entry.Explanation ?? vsaCode,
                            MeterPosition = entry.ProtocolMeter ?? entry.KiMeter ?? 0,
                            HaltungName = HaltungId,
                            VideoPath = VideoPath,
                            ToolType = OverlayToolType.Rectangle,
                            BoundingBox = entry.AnnotationBbox!,
                            FullFramePath = entry.FramePath
                        };

                        var classId = VsaYoloClassMap.GetClassId(vsaCode);
                        var baseName = $"review_{annotation.AnnotationId}";
                        var exportResult = await exportService.ExportAsync(
                            entry.FramePath!, entry.AnnotationBbox!, classId > 0 ? vsaCode : vsaCode,
                            classId, baseName, _cts?.Token ?? CancellationToken.None)
                            .ConfigureAwait(false);

                        if (exportResult.Success)
                        {
                            annotation.FullFramePath = exportResult.FullFramePath;
                            annotation.CroppedRegionPath = exportResult.CroppedRegionPath;
                            annotation.YoloAnnotationPath = exportResult.YoloAnnotationPath;
                        }

                        await TeacherAnnotationStore.AppendAsync(annotation).ConfigureAwait(false);
                        annotationCount++;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Annotation-Export Fehler: {ex.Message}");
                    }
                }
            }

            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                var parts = new List<string>
                {
                    $"{result.Indexed} indexiert",
                    $"{result.Deduplicated} dedupliziert",
                    $"{result.Skipped} uebersprungen",
                    $"{result.Errors} Fehler"
                };
                if (annotationCount > 0)
                    parts.Add($"{annotationCount} Annotationen gespeichert");
                StatusText = $"KB-Anreicherung: {string.Join(", ", parts)}";
            });
        }
        catch (Exception ex)
        {
            StatusText = $"KB-Fehler: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Liefert die reviewten Eintraege fuer Phase 3 (KB-Anreicherung).</summary>
    public int GetReviewedCount() =>
        Entries.Count(e => e.Decision != ReviewDecision.Pending);

    private void PopulateEntries(DifferenceReport report)
    {
        Entries.Clear();
        foreach (var entry in report.Entries)
            Entries.Add(new DifferenceEntryViewModel(entry));
    }

    private void UpdateMetrics(DifferenceReport report)
    {
        TpCount = report.TruePositiveCount;
        FnCount = report.FalseNegativeCount;
        FpCount = report.FalsePositiveCount;
        MismatchCount = report.CodeMismatchCount;
        PrecisionText = report.Precision > 0 ? $"{report.Precision:P0}" : "—";
        RecallText = report.Recall > 0 ? $"{report.Recall:P0}" : "—";
        F1Text = report.F1 > 0 ? $"{report.F1:P0}" : "—";
    }

    private void LoadFrame(DifferenceEntryViewModel? entry)
    {
        if (entry?.FramePath is null || !File.Exists(entry.FramePath))
        {
            CurrentFrame = null;
            return;
        }

        try
        {
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.UriSource = new Uri(entry.FramePath, UriKind.Absolute);
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.EndInit();
            bmp.Freeze();
            CurrentFrame = bmp;
        }
        catch
        {
            CurrentFrame = null;
        }
    }

    private void SelectNext()
    {
        if (SelectedEntry is null) return;
        var idx = Entries.IndexOf(SelectedEntry);
        // Naechsten unbearbeiteten suchen
        for (int i = idx + 1; i < Entries.Count; i++)
        {
            if (Entries[i].Decision == ReviewDecision.Pending)
            {
                SelectedEntry = Entries[i];
                return;
            }
        }
    }

    /// <summary>
    /// Laedt das Protokoll via ProtocolLoaderFactory.
    /// Unterstuetzt WinCan-DB3, IBAK-Daten.txt und Inspektions-PDFs.
    /// </summary>
    private Task<ProtocolDocument?> LoadProtocolAsync(VideoTrainingRequest request, CancellationToken ct)
    {
        return Task.Run(() =>
        {
            // ProtocolLoaderFactory nutzen — unterstuetzt alle Formate inkl. PDF
            var loader = new AuswertungPro.Next.UI.Ai.Training.Services.ProtocolLoaderFactory(
                _winCanImport, _ibakImport);

            var videoHint = Path.GetFileNameWithoutExtension(request.VideoPath);
            var (protocol, record) = loader.LoadProtocolWithRecord(
                request.ProtocolSource, request.ProtocolSourceType, videoHint);

            if (protocol is null || protocol.Original.Entries.Count == 0)
                return null;

            // Stammdaten aus dem Record in die UI uebernehmen
            if (record is not null)
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    var rm = record.GetFieldValue("Rohrmaterial");
                    if (!string.IsNullOrEmpty(rm) && string.IsNullOrWhiteSpace(Rohrmaterial))
                        Rohrmaterial = rm;

                    var dn = record.GetFieldValue("DN_mm");
                    if (!string.IsNullOrEmpty(dn) && string.IsNullOrWhiteSpace(NennweiteText))
                        NennweiteText = dn;

                    var hl = record.GetFieldValue("Haltungslaenge_m");
                    if (!string.IsNullOrEmpty(hl) && string.IsNullOrWhiteSpace(HaltungslaengeText))
                        HaltungslaengeText = hl;

                    if (string.IsNullOrWhiteSpace(HaltungId))
                        HaltungId = record.GetFieldValue("Haltungsname") ?? HaltungId;
                });
            }

            return protocol;
        }, ct);
    }
}

/// <summary>
/// ViewModel-Wrapper fuer einen einzelnen Differenz-Eintrag.
/// Enthaelt die Review-Entscheidung als ObservableProperty.
/// </summary>
public partial class DifferenceEntryViewModel : ObservableObject
{
    private readonly DifferenceEntry _entry;

    public DifferenceEntryViewModel(DifferenceEntry entry)
    {
        _entry = entry;
    }

    public DifferenceCategory Category => _entry.Category;
    public string? ProtocolCode => _entry.ProtocolEntry?.VsaCode;
    public double? ProtocolMeter => _entry.ProtocolEntry?.MeterStart;
    public string? ProtocolClock => _entry.ProtocolEntry?.ClockPosition;
    public string? KiCode => _entry.KiDetection?.VsaCode ?? _entry.KiDetection?.Label;
    public double? KiMeter => _entry.KiDetection?.Meter;
    public double? KiConfidence => _entry.KiDetection?.Confidence;
    public string? Explanation => _entry.Explanation;
    public string? FramePath => _entry.FramePath ?? _entry.KiDetection?.FramePath;

    /// <summary>Thumbnail-Bild fuer die DataGrid-Zeile (lazy geladen, 80px hoch).</summary>
    private BitmapImage? _thumbnail;
    public BitmapImage? Thumbnail
    {
        get
        {
            if (_thumbnail is not null) return _thumbnail;
            if (FramePath is null || !File.Exists(FramePath)) return null;
            try
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.UriSource = new Uri(FramePath, UriKind.Absolute);
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.DecodePixelHeight = 80; // Speicherschonend: nur 80px hoch laden
                bmp.EndInit();
                bmp.Freeze();
                _thumbnail = bmp;
            }
            catch { /* Frame konnte nicht geladen werden */ }
            return _thumbnail;
        }
    }

    // KI BoundingBox (normiert 0-1) — fuer Overlay-Anzeige
    public double? KiBboxX1 => _entry.KiDetection?.BboxX1;
    public double? KiBboxY1 => _entry.KiDetection?.BboxY1;
    public double? KiBboxX2 => _entry.KiDetection?.BboxX2;
    public double? KiBboxY2 => _entry.KiDetection?.BboxY2;
    public bool HasKiBbox => KiBboxX1 is not null;
    public int KiSeverity => _entry.KiDetection?.Severity ?? 0;

    public string CategoryDisplay => _entry.Category switch
    {
        DifferenceCategory.TruePositive => "Treffer",
        DifferenceCategory.FalseNegative => "Uebersehen",
        DifferenceCategory.FalsePositive => "Falschalarm",
        DifferenceCategory.CodeMismatch => "Falscher Code",
        _ => "?"
    };

    public string CategoryColor => _entry.Category switch
    {
        DifferenceCategory.TruePositive => "#2E7D32",   // Gruen
        DifferenceCategory.FalseNegative => "#C62828",   // Rot
        DifferenceCategory.FalsePositive => "#E65100",   // Orange
        DifferenceCategory.CodeMismatch => "#F9A825",    // Gelb
        _ => "#757575"
    };

    [ObservableProperty]
    private ReviewDecision _decision = ReviewDecision.Pending;

    /// <summary>Vom Reviewer gezeichnetes Markierungs-Rechteck (normalisiert 0-1).</summary>
    [ObservableProperty]
    private NormalizedBoundingBox? _annotationBbox;

    /// <summary>Hat der Reviewer eine Markierung gezeichnet?</summary>
    public bool HasAnnotation => AnnotationBbox is not null;

    /// <summary>Kann auf diesem Eintrag annotiert werden (Frame vorhanden)?</summary>
    public bool CanAnnotate => FramePath is not null && File.Exists(FramePath);

    partial void OnAnnotationBboxChanged(NormalizedBoundingBox? value)
    {
        OnPropertyChanged(nameof(HasAnnotation));
    }

    /// <summary>Zugriff auf den originalen DifferenceEntry (fuer Phase 3).</summary>
    public DifferenceEntry Entry => _entry;
}
