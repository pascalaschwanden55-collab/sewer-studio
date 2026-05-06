// AuswertungPro – Bild-Bibliothek: Manuelles Annotieren von Kanalbildern
using System;
using AuswertungPro.Next.Domain.Ai.Training;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Ai.Pipeline;
using AuswertungPro.Next.UI.Ai.Teacher;
using AuswertungPro.Next.UI.Ai.Training;
using AuswertungPro.Next.Application.Ai.Pipeline;
using AuswertungPro.Next.Application.Ai.Teacher;
using AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase;

namespace AuswertungPro.Next.UI.ViewModels.Windows;

/// <summary>
/// ViewModel fuer das Bild-Annotations-Fenster.
/// Laedt einen Ordner mit Bildern und erlaubt schnelles Annotieren:
/// Rechteck zeichnen + VSA-Code/Severity/Uhrlage/Umfang/Notiz zuweisen.
/// Speichert als TeacherAnnotation + YOLO-Label + optional KB-Eintrag.
/// Nach Zeichnen eines Rechtecks wird automatisch SAM segmentiert.
/// </summary>
public partial class ImageAnnotationViewModel : ObservableObject
{
    private readonly KnowledgeBaseManager? _kbManager;
    private readonly KbDeduplicationService? _dedup;
    private readonly VisionPipelineClient? _sidecar;

    private List<string> _imagePaths = [];
    private int _currentIndex = -1;

    // Regex: Meterstand aus Dateinamen extrahieren
    // Patterns: "frame_12.34m", "_at_12.34_", "_12.34m_", "m12.34", "meter12.34"
    private static readonly Regex MeterFromFilename = new(
        @"(?:(?:frame|at|meter|m|_)[\s_]?)(\d{1,3}[.,]\d{1,2})(?:\s*m?\b)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Navigation
    [ObservableProperty] private string _folderPath = "";
    [ObservableProperty] private int _imageCount;
    [ObservableProperty] private string _positionText = "0 / 0";
    [ObservableProperty] private BitmapImage? _currentImage;
    [ObservableProperty] private string _currentFileName = "";

    // Annotation-Eingabe
    [ObservableProperty] private string _vsaCode = "";
    [ObservableProperty] private string _severityText = "3";
    [ObservableProperty] private string _clockText = "";
    [ObservableProperty] private string _extentText = "";
    [ObservableProperty] private string _meterText = "0.00";
    [ObservableProperty] private string _notiz = "";

    // BoundingBox (vom Canvas gesetzt)
    [ObservableProperty] private NormalizedBoundingBox? _currentBbox;

    /// <summary>V4.3 Ring-Riss: wenn true, bleibt VsaCode nach Save erhalten (fuer Klick-Klick-Klick-Workflow).</summary>
    public bool PreserveCodeAfterSave { get; set; }

    // SAM Punkt-Prompts (Linksklick=positiv, Rechtsklick=negativ)
    private readonly List<SamPointPrompt> _pointPrompts = [];

    // SAM-Segmentierung (nach BBox-Zeichnung oder Punkt-Klick)
    [ObservableProperty] private SamResponse? _currentSamResult;
    [ObservableProperty] private bool _isSegmenting;

    // Status
    [ObservableProperty] private string _statusText = "Ordner waehlen um zu starten.";
    [ObservableProperty] private int _annotatedCount;
    [ObservableProperty] private bool _isAnnotating;

    // Haltungs-Kontext (optional)
    [ObservableProperty] private string _haltungName = "";

    public ImageAnnotationViewModel() { }

    public ImageAnnotationViewModel(
        KnowledgeBaseManager? kbManager = null,
        KbDeduplicationService? dedup = null,
        VisionPipelineClient? sidecar = null)
    {
        _kbManager = kbManager;
        _dedup = dedup;
        _sidecar = sidecar;
    }

    /// <summary>Laedt alle Bilder aus dem angegebenen Ordner.</summary>
    public void LoadFolder(string folder)
    {
        FolderPath = folder;
        HaltungName = Path.GetFileName(folder) ?? "";

        var extensions = new[] { ".png", ".jpg", ".jpeg", ".bmp", ".tiff" };
        _imagePaths = Directory.GetFiles(folder)
            .Where(f => extensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .ToList();

        ImageCount = _imagePaths.Count;
        _currentIndex = -1;

        if (_imagePaths.Count > 0)
        {
            // Letzte Position laden (nach Neustart weitermachen)
            int resumeIndex = LoadLastPosition(folder);
            NavigateToIndex(resumeIndex);
            string resumeHint = resumeIndex > 0 ? $" (fortgesetzt bei Bild {resumeIndex + 1})" : "";
            StatusText = $"{_imagePaths.Count} Bilder geladen{resumeHint} — Rechteck zeichnen, Code eingeben, Enter.";
        }
        else
        {
            StatusText = "Keine Bilder im Ordner gefunden.";
            CurrentImage = null;
            CurrentFileName = "";
            PositionText = "0 / 0";
        }
    }

    /// <summary>Navigiert zum Bild an Index i.</summary>
    private void NavigateToIndex(int i)
    {
        if (i < 0 || i >= _imagePaths.Count) return;
        _currentIndex = i;
        PositionText = $"{i + 1} / {_imagePaths.Count}";
        SaveLastPosition();
        CurrentFileName = Path.GetFileName(_imagePaths[i]);

        try
        {
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.UriSource = new Uri(_imagePaths[i], UriKind.Absolute);
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.EndInit();
            bmp.Freeze();
            CurrentImage = bmp;
        }
        catch
        {
            CurrentImage = null;
        }

        // Eingabefelder zuruecksetzen
        CurrentBbox = null;
        CurrentSamResult = null;
        _pointPrompts.Clear();
        VsaCode = "";
        SeverityText = "3";
        ClockText = "";
        ExtentText = "";
        Notiz = "";
        MeterText = TryParseMeterFromFilename(CurrentFileName);
        IsAnnotating = true;
    }

    /// <summary>
    /// Versucht den Meterstand aus dem Dateinamen zu extrahieren.
    /// Gibt "0.00" zurueck wenn nichts erkannt wird.
    /// </summary>
    private static string TryParseMeterFromFilename(string filename)
    {
        if (string.IsNullOrWhiteSpace(filename)) return "0.00";

        var match = MeterFromFilename.Match(filename);
        if (match.Success)
        {
            var raw = match.Groups[1].Value.Replace(',', '.');
            if (double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var meter)
                && meter >= 0 && meter < 500)
            {
                return meter.ToString("F2", CultureInfo.InvariantCulture);
            }
        }
        return "0.00";
    }

    /// <summary>
    /// Ruft SAM-Segmentierung fuer die aktuelle BBox auf.
    /// Wird automatisch nach dem Zeichnen eines Rechtecks aufgerufen.
    /// </summary>
    public async Task SegmentWithSamAsync()
    {
        if (_sidecar is null || _currentIndex < 0 || CurrentBbox is null) return;

        IsSegmenting = true;
        CurrentSamResult = null;

        try
        {
            var framePath = _imagePaths[_currentIndex];
            var imageBytes = await File.ReadAllBytesAsync(framePath);
            var imageBase64 = Convert.ToBase64String(imageBytes);

            // BBox von normalisiert (center, w, h) zu absolut (x1, y1, x2, y2) umrechnen
            // SAM erwartet Pixel-Koordinaten — wir brauchen die Bildgroesse
            var bmp = CurrentImage;
            if (bmp is null) return;
            int imgW = bmp.PixelWidth;
            int imgH = bmp.PixelHeight;

            var bbox = CurrentBbox;
            double x1 = (bbox.XCenter - bbox.Width / 2) * imgW;
            double y1 = (bbox.YCenter - bbox.Height / 2) * imgH;
            double x2 = (bbox.XCenter + bbox.Width / 2) * imgW;
            double y2 = (bbox.YCenter + bbox.Height / 2) * imgH;

            var samBox = new SamBoundingBox(x1, y1, x2, y2, "annotation", 1.0);
            var request = new SamRequest(imageBase64, [samBox]);

            var result = await _sidecar.SegmentSamAsync(request, CancellationToken.None);
            CurrentSamResult = result;

            if (result.Masks.Count > 0)
                StatusText = $"SAM: Maske gefunden ({result.InferenceTimeMs:F0}ms)";
            else
                StatusText = "SAM: Keine Maske gefunden — Rechteck wird verwendet.";
        }
        catch (Exception ex)
        {
            StatusText = $"SAM nicht erreichbar — Rechteck wird verwendet. ({ex.Message})";
        }
        finally
        {
            IsSegmenting = false;
        }
    }

    /// <summary>
    /// Fuegt einen Punkt-Prompt hinzu (ohne sofortige Segmentierung).
    /// Verwende SegmentPointPromptsAsync() um alle Punkte auf einmal zu senden.
    /// </summary>
    public void AddPointPrompt(double normalizedX, double normalizedY, bool isPositive)
    {
        var bmp = CurrentImage;
        if (bmp is null) return;
        int imgW = bmp.PixelWidth;
        int imgH = bmp.PixelHeight;

        double pixelX = normalizedX * imgW;
        double pixelY = normalizedY * imgH;

        _pointPrompts.Add(new SamPointPrompt(pixelX, pixelY, isPositive ? 1 : 0));
    }

    /// <summary>
    /// Segmentiert alle gesammelten Punkt-Prompts auf einmal mit SAM.
    /// Ein HTTP-Request statt N.
    /// </summary>
    public async Task SegmentPointPromptsAsync()
    {
        if (_sidecar is null || _currentIndex < 0 || _pointPrompts.Count == 0) return;

        IsSegmenting = true;
        CurrentSamResult = null;

        try
        {
            var framePath = _imagePaths[_currentIndex];
            var imageBytes = await File.ReadAllBytesAsync(framePath);
            var imageBase64 = Convert.ToBase64String(imageBytes);

            var request = new SamRequest(imageBase64, [], _pointPrompts);
            var result = await _sidecar.SegmentSamAsync(request, CancellationToken.None);
            CurrentSamResult = result;

            var positiveCount = _pointPrompts.Count(p => p.Label == 1);
            var negativeCount = _pointPrompts.Count(p => p.Label == 0);
            StatusText = result.Masks.Count > 0
                ? $"SAM: Maske ({result.InferenceTimeMs:F0}ms) — {positiveCount}+ {negativeCount}-"
                : "SAM: Keine Maske im Ring gefunden.";
        }
        catch (Exception ex)
        {
            StatusText = $"SAM-Fehler: {ex.Message}";
        }
        finally
        {
            IsSegmenting = false;
        }
    }

    /// <summary>
    /// Ring-Scan mit BBox-Kacheln: Sendet alle BBoxen an SAM im Batch.
    /// Gleicher Mechanismus wie das manuelle Rechteck — funktioniert zuverlaessig.
    /// </summary>
    public async Task ScanRingWithBBoxesAsync(IReadOnlyList<SamBoundingBox> boxes)
    {
        if (_sidecar is null || _currentIndex < 0 || boxes.Count == 0) return;

        IsSegmenting = true;
        CurrentSamResult = null;

        try
        {
            var framePath = _imagePaths[_currentIndex];
            var imageBytes = await File.ReadAllBytesAsync(framePath);
            var imageBase64 = Convert.ToBase64String(imageBytes);

            var request = new SamRequest(imageBase64, boxes);
            var result = await _sidecar.SegmentSamAsync(request, CancellationToken.None);
            CurrentSamResult = result;

            StatusText = result.Masks.Count > 0
                ? $"Ring-Scan: {result.Masks.Count} Segment(e) aus {boxes.Count} Kacheln ({result.InferenceTimeMs:F0}ms)"
                : $"Ring-Scan: {boxes.Count} Kacheln gesendet, keine Segmente gefunden.";
        }
        catch (Exception ex)
        {
            StatusText = $"Ring-Scan Fehler: {ex.Message}";
            throw;
        }
        finally
        {
            IsSegmenting = false;
        }
    }

    /// <summary>Loescht alle Punkt-Prompts.</summary>
    public void ClearPointPrompts()
    {
        _pointPrompts.Clear();
        CurrentSamResult = null;
    }

    // ── Positions-Persistenz: Letzte Position pro Ordner merken ──

    private static readonly string PositionFile = ".annotation_position";

    /// <summary>Speichert den aktuellen Index in einer Datei im Bildordner.</summary>
    private void SaveLastPosition()
    {
        if (string.IsNullOrWhiteSpace(FolderPath) || _currentIndex < 0) return;
        try
        {
            var path = Path.Combine(FolderPath, PositionFile);
            File.WriteAllText(path, _currentIndex.ToString(CultureInfo.InvariantCulture));
        }
        catch { /* Schreibfehler ignorieren */ }
    }

    /// <summary>Laedt den letzten Index aus der Positionsdatei.</summary>
    private static int LoadLastPosition(string folder)
    {
        try
        {
            var path = Path.Combine(folder, PositionFile);
            if (!File.Exists(path)) return 0;
            var text = File.ReadAllText(path).Trim();
            if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var idx) && idx >= 0)
                return idx;
        }
        catch { /* Lesefehler ignorieren */ }
        return 0;
    }

    [RelayCommand]
    private void NavigateNext()
    {
        if (_currentIndex < _imagePaths.Count - 1)
            NavigateToIndex(_currentIndex + 1);
    }

    [RelayCommand]
    private void NavigatePrevious()
    {
        if (_currentIndex > 0)
            NavigateToIndex(_currentIndex - 1);
    }

    [RelayCommand]
    private void SkipImage()
    {
        NavigateNext();
    }

    /// <summary>Markiert "kein Fund" — Bild als Negativ-Beispiel speichern.</summary>
    [RelayCommand]
    private async Task MarkNoFindingAsync()
    {
        if (_currentIndex < 0) return;
        var framePath = _imagePaths[_currentIndex];

        double meterPos = 0;
        if (!string.IsNullOrWhiteSpace(MeterText))
        {
            var normalized = MeterText.Trim().Replace(',', '.');
            double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out meterPos);
        }

        var annotation = new TeacherAnnotation
        {
            VsaCode = "NONE",
            Beschreibung = "Kein Befund — Negativ-Beispiel",
            MeterPosition = meterPos,
            HaltungName = HaltungName,
            ToolType = OverlayToolType.None,
            FullFramePath = framePath
        };

        await TeacherAnnotationStore.AppendAsync(annotation);
        AnnotatedCount++;
        StatusText = $"Negativ-Beispiel gespeichert. ({AnnotatedCount} annotiert)";
        NavigateNext();
    }

    /// <summary>Speichert die aktuelle Annotation (Rechteck + Code + Details).</summary>
    [RelayCommand]
    private async Task SaveAnnotationAsync()
    {
        if (_currentIndex < 0 || string.IsNullOrWhiteSpace(VsaCode))
        {
            StatusText = "Bitte VSA-Code eingeben.";
            return;
        }

        var framePath = _imagePaths[_currentIndex];
        var code = VsaCode.Trim().ToUpperInvariant();
        int.TryParse(SeverityText, out var severity);
        severity = Math.Clamp(severity, 1, 5);
        int.TryParse(ExtentText, out var extent);

        // Meterstand parsen
        double meterPos = 0;
        if (!string.IsNullOrWhiteSpace(MeterText))
        {
            var normalized = MeterText.Trim().Replace(',', '.');
            double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out meterPos);
        }

        // BBox: aus Rechteck oder aus SAM-Masken-Extent ableiten
        var effectiveBbox = CurrentBbox;
        bool hasMask = CurrentSamResult is { Masks.Count: > 0 };

        if (effectiveBbox is null && hasMask)
        {
            // BBox aus SAM-Maske ableiten (normalisiert)
            var m = CurrentSamResult!.Masks[0];
            if (m.Bbox.Count >= 4)
            {
                int imgW = CurrentSamResult.ImageWidth;
                int imgH = CurrentSamResult.ImageHeight;
                double nx1 = m.Bbox[0] / imgW, ny1 = m.Bbox[1] / imgH;
                double nx2 = m.Bbox[2] / imgW, ny2 = m.Bbox[3] / imgH;
                effectiveBbox = new NormalizedBoundingBox
                {
                    XCenter = (nx1 + nx2) / 2,
                    YCenter = (ny1 + ny2) / 2,
                    Width = nx2 - nx1,
                    Height = ny2 - ny1
                };
            }
        }

        // TeacherAnnotation erstellen
        var annotation = new TeacherAnnotation
        {
            VsaCode = code,
            Beschreibung = string.IsNullOrWhiteSpace(Notiz) ? code : Notiz.Trim(),
            Severity = severity,
            MeterPosition = meterPos,
            HaltungName = HaltungName,
            ToolType = effectiveBbox is not null ? OverlayToolType.Rectangle : OverlayToolType.None,
            BoundingBox = effectiveBbox ?? new NormalizedBoundingBox(),
            FullFramePath = framePath
        };

        if (!string.IsNullOrWhiteSpace(ClockText))
        {
            if (double.TryParse(ClockText.Replace(":", "."), out var clock))
                annotation.ClockPosition = Math.Clamp(clock, 0, 12);
        }

        try
        {
            // YOLO-Export (Frame kopieren + Crop + Label)
            if (effectiveBbox is not null)
            {
                var exportService = new TrainingAnnotationExportService();
                var classId = VsaYoloClassMap.GetClassId(code);
                var baseName = $"manual_{annotation.AnnotationId}";
                var exportResult = await exportService.ExportAsync(
                    framePath, effectiveBbox, code, classId, baseName);

                if (exportResult.Success)
                {
                    annotation.FullFramePath = exportResult.FullFramePath;
                    annotation.CroppedRegionPath = exportResult.CroppedRegionPath;
                    annotation.YoloAnnotationPath = exportResult.YoloAnnotationPath;
                }
            }

            // SAM-Maske mitspeichern falls vorhanden
            if (CurrentSamResult is { Masks.Count: > 0 })
            {
                var bestMask = CurrentSamResult.Masks[0];
                annotation.MaskRle = bestMask.MaskRle;
                annotation.MaskWidth = CurrentSamResult.ImageWidth;
                annotation.MaskHeight = CurrentSamResult.ImageHeight;
            }

            // TeacherAnnotation speichern
            await TeacherAnnotationStore.AppendAsync(annotation);

            // Optional: KB-Eintrag erstellen
            if (_kbManager is not null && effectiveBbox is not null)
            {
                var sample = new TrainingSample
                {
                    SampleId = Guid.NewGuid().ToString("N"),
                    CaseId = $"manual-{DateTime.UtcNow:yyyyMMdd}",
                    Code = code,
                    Beschreibung = annotation.Beschreibung,
                    MeterStart = meterPos,
                    MeterEnd = meterPos,
                    FramePath = annotation.FullFramePath ?? framePath,
                    Status = TrainingSampleStatus.Approved,
                    MatchLevel = MatchLevelNames.TeacherAnnotation,
                    SourceType = SourceTypeNames.TeacherAnnotation,
                    IsKorrigiert = false,
                    BboxXCenter = effectiveBbox.XCenter,
                    BboxYCenter = effectiveBbox.YCenter,
                    BboxWidth = effectiveBbox.Width,
                    BboxHeight = effectiveBbox.Height,
                    KbIndexState = KbIndexState.Pending
                };

                if (KnowledgeBaseManager.IsIndexWorthy(sample))
                {
                    bool isDuplicate = false;
                    if (_dedup is not null)
                    {
                        var check = await _dedup.CheckAsync(sample, false);
                        isDuplicate = check.IsAlreadyCovered;
                    }

                    if (!isDuplicate)
                        await _kbManager.IndexSampleAsync(sample);
                }
            }

            AnnotatedCount++;
            StatusText = $"{code} gespeichert. ({AnnotatedCount} annotiert) — gleiches Bild, neues Ereignis oder Skip (S).";

            // Eingabefelder + BBox + Maske + Punkte zuruecksetzen, Bild + Meter bleiben
            CurrentBbox = null;
            CurrentSamResult = null;
            _pointPrompts.Clear();
            // V4.3 Ring-Riss: VsaCode erhalten fuer Klick-Klick-Klick-Workflow
            if (!PreserveCodeAfterSave)
            {
                VsaCode = "";
                SeverityText = "3";
                ClockText = "";
                ExtentText = "";
                Notiz = "";
            }
            // MeterText bleibt — gleiches Bild, gleicher Meterstand
        }
        catch (Exception ex)
        {
            StatusText = $"Fehler: {ex.Message}";
        }
    }
}
