using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Pipeline;
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class VideoAnalysisPipelineWindow : Window
{
    private readonly IVideoAnalysisPipelineService _pipeline;
    private PipelineRequest _request;
    private readonly CancellationTokenSource _cts = new();
    private readonly List<LiveFrameFinding> _liveFrameFindings = new();
    private OverlayMode _overlayMode = OverlayMode.Detail;
    private LiveFrameWindow? _liveFrameWindow;

    private PipelineResult? _result;
    public PipelineResult? Result => _result;

    public VideoAnalysisPipelineViewModel Vm { get; }

    private enum OverlayMode
    {
        Compact,
        Detail
    }

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

        // PipeGraphTimeline einrichten
        SetupPipeTimeline();

        Closed += (_, __) =>
        {
            _cts.Cancel();
            Vm.Detections.CollectionChanged -= OnDetectionsChanged;
            CloseLiveFrameWindow();
        };
    }

    /// <summary>PipeGraphTimeline mit Accessoren fuer DetectionItems einrichten.</summary>
    private void SetupPipeTimeline()
    {
        PipeTimeline.TotalLength = _request.HaltungslaengeM;
        PipeTimeline.MeterAccessor = obj => obj is DetectionItem d ? d.MeterStart : 0;
        PipeTimeline.CodeAccessor = obj => obj is DetectionItem d ? d.Code : "?";
        PipeTimeline.ConfidenceAccessor = obj => obj is DetectionItem d ? d.Confidence : -1;
        PipeTimeline.IsRejectedAccessor = _ => false;
        PipeTimeline.Markers = Vm.Detections;

        // CurrentMeter aus Vm.CurrentMeter-String aktualisieren
        Vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(VideoAnalysisPipelineViewModel.CurrentMeter))
            {
                // CurrentMeter ist z.B. "12.5 m" oder "12.5m" — Zahl extrahieren
                var text = Vm.CurrentMeter?.Replace("m", "").Trim() ?? "";
                if (double.TryParse(text, System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out var meter))
                {
                    PipeTimeline.CurrentMeter = meter;
                }
            }
            // TotalLength aktualisieren wenn Haltungslaenge erst spaeter bekannt
            if (e.PropertyName == nameof(VideoAnalysisPipelineViewModel.MeterRange)
                && PipeTimeline.TotalLength <= 0)
            {
                var rangeText = Vm.MeterRange?.Replace("m", "").Trim() ?? "";
                if (double.TryParse(rangeText, System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out var total) && total > 0)
                {
                    PipeTimeline.TotalLength = total;
                }
            }
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

        try
        {
            var progress = new Progress<PipelineProgress>(p =>
            {
                // einfache UI-Abbildung – feinere Progress-Mappings können später ergänzt werden
                Vm.StatusText = p.Status;
                Vm.PhaseLabel = p.Phase switch
                {
                    PipelinePhase.VideoAnalysis => "Videoanalyse",
                    PipelinePhase.MultiModelDetection => "Multi-Model Pipeline",
                    PipelinePhase.CodeMapping => "Code-Mapping",
                    PipelinePhase.Done => "Fertig",
                    _ => p.Phase.ToString()
                };

                var isVideoPhase = p.Phase is PipelinePhase.VideoAnalysis or PipelinePhase.MultiModelDetection;
                Vm.VideoPhaseActive = isVideoPhase;
                Vm.VideoPhaseDone = !isVideoPhase;
                Vm.MappingPhaseDone = p.Phase == PipelinePhase.Done;
                Vm.IsMultiModelActive = p.Phase == PipelinePhase.MultiModelDetection;

                // Extract YOLO skip count from status text (e.g. "38 gesamt" or "übersprungen")
                if (p.Phase == PipelinePhase.MultiModelDetection)
                {
                    var skipMatch = Regex.Match(p.Status, @"(\d+)\s+gesamt\b",
                        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
                    if (skipMatch.Success && int.TryParse(skipMatch.Groups[1].Value, out var skipCount))
                        Vm.YoloSkippedFrames = skipCount;
                }

                if (isVideoPhase)
                {
                    Vm.VideoProgressPct = Math.Clamp(p.PercentInPhase, 0, 100);
                    Vm.MappingProgressPct = 0;

                    if (p.FramesDone.HasValue)
                        Vm.FramesAnalyzed = Math.Max(0, p.FramesDone.Value);
                    if (p.FramesTotal.HasValue)
                        Vm.TotalFrames = Math.Max(0, p.FramesTotal.Value);

                    var meter = TryExtractMeterFromStatus(p.Status);
                    if (!string.IsNullOrWhiteSpace(meter))
                        Vm.CurrentMeter = meter;

                    var liveFindings = TryExtractFindingsFromStatus(p.Status);
                    if (liveFindings.HasValue)
                        Vm.DetectionCount = Math.Max(Vm.DetectionCount, liveFindings.Value);

                    Vm.LiveFrameStatus = p.Status;
                    Vm.LiveFrameInfo = BuildLiveFrameInfo(Vm.FramesAnalyzed, Vm.TotalFrames, Vm.CurrentMeter);
                    if (p.FramePreviewPng is { Length: > 0 })
                    {
                        Vm.LiveFrameImage = ToBitmap(p.FramePreviewPng);
                        RenderLiveFrameOverlay();
                    }
                    if (p.LiveFindings is not null)
                    {
                        _liveFrameFindings.Clear();
                        _liveFrameFindings.AddRange(p.LiveFindings.Take(8));
                        Vm.LiveFrameQuantSummary = BuildLiveQuantSummary(_liveFrameFindings);

                        Vm.PillarDetectionCount = Math.Max(Vm.PillarDetectionCount, _liveFrameFindings.Count);
                        Vm.PillarQuantCount = Math.Max(Vm.PillarQuantCount,
                            _liveFrameFindings.Count(f => f.HeightMm.HasValue || f.WidthMm.HasValue
                                || f.IntrusionPercent.HasValue || f.CrossSectionReductionPercent.HasValue
                                || f.DiameterReductionMm.HasValue || f.ExtentPercent.HasValue));
                        Vm.PillarLocalCount = Math.Max(Vm.PillarLocalCount,
                            _liveFrameFindings.Count(f => !string.IsNullOrWhiteSpace(f.PositionClock)));

                        RenderLiveFrameOverlay();
                    }

                    // Forward to undocked live frame window
                    _liveFrameWindow?.UpdateFrame(
                        Vm.LiveFrameImage, _liveFrameFindings,
                        Vm.LiveFrameStatus, Vm.LiveFrameInfo, Vm.LiveFrameQuantSummary);
                }
                else if (p.Phase == PipelinePhase.CodeMapping)
                {
                    Vm.VideoProgressPct = 100.0;
                    Vm.MappingProgressPct = Math.Clamp(p.PercentInPhase, 0, 100);

                    if (p.ItemsDone.HasValue)
                        Vm.DetectionCount = Math.Max(Vm.DetectionCount, p.ItemsDone.Value);

                    Vm.LiveFrameStatus = p.Status;
                    Vm.LiveFrameInfo = BuildLiveFrameInfo(Vm.FramesAnalyzed, Vm.TotalFrames, Vm.CurrentMeter);
                }
                else if (p.Phase == PipelinePhase.Done)
                {
                    Vm.VideoProgressPct = 100.0;
                    Vm.MappingProgressPct = 100.0;
                    Vm.LiveFrameStatus = "Analyse abgeschlossen";
                    Vm.LiveFrameInfo = BuildLiveFrameInfo(Vm.FramesAnalyzed, Vm.TotalFrames, Vm.CurrentMeter);
                    Vm.LiveFrameQuantSummary = BuildLiveQuantSummary(_liveFrameFindings);
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

            // Pillar counters (final)
            var dets = result.Detections ?? Array.Empty<RawVideoDetection>();
            Vm.PillarDetectionCount = dets.Count;
            Vm.PillarQuantCount = dets.Count(d => d.HeightMm.HasValue || d.WidthMm.HasValue
                || d.IntrusionPercent.HasValue || d.CrossSectionReductionPercent.HasValue
                || d.DiameterReductionMm.HasValue || d.ExtentPercent.HasValue);
            Vm.PillarLocalCount = dets.Count(d => !string.IsNullOrWhiteSpace(d.PositionClock));
            Vm.StatsText = result.Stats is null
                ? ""
                : $"Frames: {result.Stats.FramesAnalyzed}, Detections: {result.Stats.DetectionsRaw}, Entries: {result.Stats.EntriesGenerated}, HighConf: {result.Stats.EntriesWithHighConfidence}";

            Vm.TelemetryText = FormatTelemetry(result.Telemetry);

            // einfache Liste für UI
            Vm.Detections.Clear();
            foreach (var d in (result.Detections ?? Array.Empty<RawVideoDetection>()).Take(250))
            {
                Vm.Detections.Add(DetectionItem.From(d));
            }
            RenderPipeRadar();

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

    private static string? TryExtractMeterFromStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
            return null;

        var m = Regex.Match(status, @"@\s*(?<meter>\d+(?:[.,]\d+)?)m",
            RegexOptions.CultureInvariant);
        if (!m.Success)
            return null;

        var raw = m.Groups["meter"].Value.Replace(',', '.');
        return double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var meter)
            ? $"{meter:0.0} m"
            : null;
    }

    private static int? TryExtractFindingsFromStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
            return null;

        var m = Regex.Match(status, @"(?<count>\d+)\s+Befunde",
            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
        if (!m.Success)
            return null;

        return int.TryParse(m.Groups["count"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var count)
            ? count
            : null;
    }

    private static string BuildLiveFrameInfo(int framesDone, int totalFrames, string currentMeter)
    {
        var meterText = string.IsNullOrWhiteSpace(currentMeter) ? "—" : currentMeter;
        return $"Frame {framesDone}/{Math.Max(totalFrames, 0)}  |  Meter {meterText}";
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

    private static string BuildLiveQuantSummary(IReadOnlyList<LiveFrameFinding> findings)
    {
        if (findings.Count == 0)
            return "Quantifizierung: keine Punkte erkannt";

        var parts = findings.Take(4).Select(f =>
        {
            var clock = string.IsNullOrWhiteSpace(f.PositionClock) ? "?" : f.PositionClock;
            var quantParts = new List<string>();
            if (f.ExtentPercent is > 0) quantParts.Add($"{f.ExtentPercent}%");
            if (f.HeightMm is > 0) quantParts.Add($"H:{f.HeightMm}mm");
            if (f.WidthMm is > 0) quantParts.Add($"B:{f.WidthMm}mm");
            if (f.IntrusionPercent is > 0) quantParts.Add($"Einr:{f.IntrusionPercent}%");
            if (f.CrossSectionReductionPercent is > 0) quantParts.Add($"QV:{f.CrossSectionReductionPercent}%");
            if (f.DiameterReductionMm is > 0) quantParts.Add($"DV:{f.DiameterReductionMm}mm");
            var quantStr = quantParts.Count > 0 ? string.Join(" ", quantParts) : "n/a";
            return $"{clock} ({quantStr})";
        });

        return "Q: " + string.Join(" | ", parts);
    }

    private void RenderLiveFrameOverlay()
    {
        if (LiveFrameOverlayCanvas is null)
            return;

        var width = LiveFrameOverlayCanvas.ActualWidth;
        var height = LiveFrameOverlayCanvas.ActualHeight;
        if (width < 60 || height < 60)
            return;

        LiveFrameOverlayCanvas.Children.Clear();

        if (Vm.LiveFrameImage is null)
            return;

        var size = Math.Min(width, height) * 0.78;
        var cx = width / 2.0;
        var cy = height / 2.0;
        var ringOuter = size * 0.42;
        var ringInner = size * 0.28;

        var guide = new Ellipse
        {
            Width = ringOuter * 2,
            Height = ringOuter * 2,
            Stroke = new SolidColorBrush(Color.FromArgb(125, 197, 209, 134)),
            StrokeDashArray = new DoubleCollection { 3, 3 },
            StrokeThickness = 1.0,
            Fill = Brushes.Transparent
        };
        Canvas.SetLeft(guide, cx - ringOuter);
        Canvas.SetTop(guide, cy - ringOuter);
        LiveFrameOverlayCanvas.Children.Add(guide);

        var guideInner = new Ellipse
        {
            Width = ringInner * 2,
            Height = ringInner * 2,
            Stroke = new SolidColorBrush(Color.FromArgb(105, 197, 209, 134)),
            StrokeDashArray = new DoubleCollection { 3, 3 },
            StrokeThickness = 0.9,
            Fill = Brushes.Transparent
        };
        Canvas.SetLeft(guideInner, cx - ringInner);
        Canvas.SetTop(guideInner, cy - ringInner);
        LiveFrameOverlayCanvas.Children.Add(guideInner);

        for (var hour = 1; hour <= 12; hour++)
        {
            var angleDeg = -90 + (hour % 12) * 30;
            var rad = DegToRad(angleDeg);
            var x1 = cx + Math.Cos(rad) * (ringInner - 4);
            var y1 = cy + Math.Sin(rad) * (ringInner - 4);
            var x2 = cx + Math.Cos(rad) * (ringOuter + 4);
            var y2 = cy + Math.Sin(rad) * (ringOuter + 4);
            LiveFrameOverlayCanvas.Children.Add(new Line
            {
                X1 = x1,
                Y1 = y1,
                X2 = x2,
                Y2 = y2,
                Stroke = new SolidColorBrush(Color.FromArgb(65, 227, 227, 201)),
                StrokeThickness = 0.8
            });
        }

        var findings = _liveFrameFindings.Take(8).ToList();
        if (findings.Count == 0)
            return;

        for (var i = 0; i < findings.Count; i++)
        {
            var finding = findings[i];
            var parsedClock = ParseClockHour(finding.PositionClock);
            var centerDeg = parsedClock.HasValue
                ? -90 + (parsedClock.Value % 12) * 30
                : -90 + i * (360.0 / findings.Count);

            var sweep = finding.ExtentPercent is > 0
                ? Math.Clamp(finding.ExtentPercent.Value * 3.6, 14.0, 160.0)
                : 18.0;

            var startDeg = centerDeg - sweep / 2.0;
            var color = MapLiveSeverityColor(finding.Severity);

            var sector = new Path
            {
                Data = BuildRingSectorGeometry(cx, cy, ringInner, ringOuter, startDeg, sweep),
                Fill = new SolidColorBrush(Color.FromArgb(98, color.R, color.G, color.B)),
                Stroke = new SolidColorBrush(Color.FromArgb(220, color.R, color.G, color.B)),
                StrokeThickness = 1.0
            };
            LiveFrameOverlayCanvas.Children.Add(sector);

            var rad = DegToRad(centerDeg);
            var markerRadius = ringOuter + 2;
            var mx = cx + Math.Cos(rad) * markerRadius;
            var my = cy + Math.Sin(rad) * markerRadius;

            var dot = new Ellipse
            {
                Width = 7,
                Height = 7,
                Fill = new SolidColorBrush(color),
                Stroke = Brushes.White,
                StrokeThickness = 0.8
            };
            Canvas.SetLeft(dot, mx - 3.5);
            Canvas.SetTop(dot, my - 3.5);
            LiveFrameOverlayCanvas.Children.Add(dot);

            var labelText = BuildLiveFindingLabel(finding);
            var label = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(228, 14, 19, 28)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(210, color.R, color.G, color.B)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(4, 2, 4, 2),
                Child = new TextBlock
                {
                    Text = labelText,
                    FontSize = 10,
                    Foreground = new SolidColorBrush(Color.FromRgb(225, 234, 245))
                }
            };
            label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            var desired = label.DesiredSize;
            var lx = Math.Cos(rad) >= 0 ? mx + 6 : mx - desired.Width - 6;
            var ly = my - desired.Height / 2.0;
            Canvas.SetLeft(label, Math.Clamp(lx, 2, width - desired.Width - 2));
            Canvas.SetTop(label, Math.Clamp(ly, 2, height - desired.Height - 2));
            LiveFrameOverlayCanvas.Children.Add(label);
        }
    }

    private static string BuildLiveFindingLabel(LiveFrameFinding finding)
    {
        var baseText = string.IsNullOrWhiteSpace(finding.VsaCodeHint)
            ? finding.Label
            : $"{finding.VsaCodeHint} {finding.Label}";
        if (baseText.Length > 20)
            baseText = baseText[..20] + "...";

        var clock = string.IsNullOrWhiteSpace(finding.PositionClock) ? "?" : finding.PositionClock;
        var extent = finding.ExtentPercent is > 0 ? $"{finding.ExtentPercent}%" : "n/a";
        var quantExtra = "";
        if (finding.HeightMm is > 0) quantExtra += $" H:{finding.HeightMm}mm";
        if (finding.IntrusionPercent is > 0) quantExtra += $" Einr:{finding.IntrusionPercent}%";
        if (finding.CrossSectionReductionPercent is > 0) quantExtra += $" QV:{finding.CrossSectionReductionPercent}%";
        return $"{clock} / {extent}{quantExtra} - {baseText}";
    }

    private static Color MapLiveSeverityColor(int severity)
    {
        return Math.Clamp(severity, 1, 5) switch
        {
            >= 5 => Color.FromRgb(239, 68, 68),
            4 => Color.FromRgb(249, 115, 22),
            3 => Color.FromRgb(245, 158, 11),
            2 => Color.FromRgb(132, 204, 22),
            _ => Color.FromRgb(34, 197, 94)
        };
    }

    private void OnDetectionsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
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
        _overlayMode = OverlayModeCombo.SelectedIndex <= 0 ? OverlayMode.Compact : OverlayMode.Detail;
        RenderPipeRadar();
    }

    private void RenderPipeRadar()
    {
        if (PipeRadarCanvas is null)
            return;

        var width = PipeRadarCanvas.ActualWidth;
        var height = PipeRadarCanvas.ActualHeight;
        if (width < 80 || height < 80)
            return;

        PipeRadarCanvas.Children.Clear();

        var allItems = Vm.Detections
            .OrderByDescending(x => x.Confidence)
            .ThenBy(x => x.MeterStart)
            .ToList();
        var isCompact = _overlayMode == OverlayMode.Compact;
        var items = allItems.Take(isCompact ? 5 : 8).ToList();
        PipeRadarEmptyText.Visibility = items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        var size = Math.Min(width, height);
        var cx = width / 2.0;
        var cy = height / 2.0;

        var outerPipeRadius = size * 0.455;
        var ringOuterRadius = size * 0.385;
        var ringInnerRadius = size * 0.255;
        var centerHoleRadius = size * 0.19;
        var labelMaxWidth = Math.Max(isCompact ? 118 : 138, width * (isCompact ? 0.36 : 0.44));

        var backdrop = new Ellipse
        {
            Width = outerPipeRadius * 2.06,
            Height = outerPipeRadius * 2.06,
            Fill = new RadialGradientBrush
            {
                Center = new Point(0.5, 0.5),
                GradientOrigin = new Point(0.52, 0.46),
                RadiusX = 0.70,
                RadiusY = 0.70,
                GradientStops =
                {
                    new GradientStop(Color.FromArgb(84, 23, 59, 46), 0.0),
                    new GradientStop(Color.FromArgb(26, 12, 38, 29), 0.72),
                    new GradientStop(Color.FromArgb(0, 12, 38, 29), 1.0)
                }
            }
        };
        Canvas.SetLeft(backdrop, cx - backdrop.Width / 2.0);
        Canvas.SetTop(backdrop, cy - backdrop.Height / 2.0);
        PipeRadarCanvas.Children.Add(backdrop);

        var pipeBody = new Ellipse
        {
            Width = outerPipeRadius * 2,
            Height = outerPipeRadius * 2,
            Stroke = new SolidColorBrush(Color.FromRgb(118, 122, 113)),
            StrokeThickness = 1.1,
            Fill = new RadialGradientBrush
            {
                GradientOrigin = new Point(0.46, 0.38),
                Center = new Point(0.50, 0.49),
                RadiusX = 0.63,
                RadiusY = 0.63,
                GradientStops =
                {
                    new GradientStop(Color.FromRgb(168, 162, 145), 0.0),
                    new GradientStop(Color.FromRgb(134, 129, 116), 0.32),
                    new GradientStop(Color.FromRgb(94, 93, 87), 0.78),
                    new GradientStop(Color.FromRgb(66, 70, 69), 1.0)
                }
            }
        };
        Canvas.SetLeft(pipeBody, cx - outerPipeRadius);
        Canvas.SetTop(pipeBody, cy - outerPipeRadius);
        PipeRadarCanvas.Children.Add(pipeBody);

        var annulusBase = new Path
        {
            Data = BuildRingSectorGeometry(cx, cy, ringInnerRadius, ringOuterRadius, -90, 359.9),
            Fill = new SolidColorBrush(Color.FromArgb(70, 50, 78, 48)),
            StrokeThickness = 0
        };
        PipeRadarCanvas.Children.Add(annulusBase);

        var guideOuter = new Ellipse
        {
            Width = ringOuterRadius * 2,
            Height = ringOuterRadius * 2,
            Stroke = new SolidColorBrush(Color.FromArgb(175, 232, 219, 92)),
            StrokeDashArray = new DoubleCollection { 2, 3 },
            StrokeThickness = 1.0,
            Fill = Brushes.Transparent
        };
        Canvas.SetLeft(guideOuter, cx - ringOuterRadius);
        Canvas.SetTop(guideOuter, cy - ringOuterRadius);
        PipeRadarCanvas.Children.Add(guideOuter);

        var guideInner = new Ellipse
        {
            Width = ringInnerRadius * 2,
            Height = ringInnerRadius * 2,
            Stroke = new SolidColorBrush(Color.FromArgb(130, 232, 219, 92)),
            StrokeDashArray = new DoubleCollection { 2, 3 },
            StrokeThickness = 0.9,
            Fill = Brushes.Transparent
        };
        Canvas.SetLeft(guideInner, cx - ringInnerRadius);
        Canvas.SetTop(guideInner, cy - ringInnerRadius);
        PipeRadarCanvas.Children.Add(guideInner);

        var hole = new Ellipse
        {
            Width = centerHoleRadius * 2,
            Height = centerHoleRadius * 2,
            Stroke = new SolidColorBrush(Color.FromRgb(33, 40, 39)),
            StrokeThickness = 1.0,
            Fill = new RadialGradientBrush
            {
                Center = new Point(0.5, 0.45),
                GradientOrigin = new Point(0.45, 0.45),
                RadiusX = 0.8,
                RadiusY = 0.8,
                GradientStops =
                {
                    new GradientStop(Color.FromRgb(29, 35, 34), 0.0),
                    new GradientStop(Color.FromRgb(10, 14, 16), 1.0)
                }
            }
        };
        Canvas.SetLeft(hole, cx - centerHoleRadius);
        Canvas.SetTop(hole, cy - centerHoleRadius);
        PipeRadarCanvas.Children.Add(hole);

        for (var hour = 1; hour <= 12; hour++)
        {
            var angleDeg = -90 + (hour % 12) * 30;
            var angleRad = DegToRad(angleDeg);

            var x1 = cx + Math.Cos(angleRad) * (ringInnerRadius - 6);
            var y1 = cy + Math.Sin(angleRad) * (ringInnerRadius - 6);
            var x2 = cx + Math.Cos(angleRad) * (ringOuterRadius + 2);
            var y2 = cy + Math.Sin(angleRad) * (ringOuterRadius + 2);

            var tick = new Line
            {
                X1 = x1,
                Y1 = y1,
                X2 = x2,
                Y2 = y2,
                Stroke = new SolidColorBrush(Color.FromArgb(75, 204, 206, 184)),
                StrokeThickness = 0.9
            };
            PipeRadarCanvas.Children.Add(tick);
        }

        if (!isCompact)
        {
            foreach (var majorHour in new[] { 12, 3, 6, 9 })
            {
                var angleDeg = -90 + (majorHour % 12) * 30;
                var angleRad = DegToRad(angleDeg);
                var tx = cx + Math.Cos(angleRad) * (ringOuterRadius + 10);
                var ty = cy + Math.Sin(angleRad) * (ringOuterRadius + 10);

                var hourText = new TextBlock
                {
                    Text = majorHour.ToString(CultureInfo.InvariantCulture),
                    FontSize = 9,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(Color.FromArgb(180, 233, 227, 160))
                };
                hourText.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                var desired = hourText.DesiredSize;
                Canvas.SetLeft(hourText, tx - desired.Width / 2.0);
                Canvas.SetTop(hourText, ty - desired.Height / 2.0);
                PipeRadarCanvas.Children.Add(hourText);
            }
        }

        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            var centerDeg = ResolveCenterAngle(item, i, items.Count);
            var sweepDeg = ResolveSweep(item);
            var startDeg = centerDeg - sweepDeg / 2.0;
            var midRad = DegToRad(centerDeg);

            var sector = new Path
            {
                Data = BuildRingSectorGeometry(cx, cy, ringInnerRadius, ringOuterRadius, startDeg, sweepDeg),
                Fill = new SolidColorBrush(Color.FromArgb(
                    (byte)Math.Clamp(84 + (item.Confidence * 120), 84, 196),
                    item.SeverityColor.R,
                    item.SeverityColor.G,
                    item.SeverityColor.B)),
                Stroke = new SolidColorBrush(Color.FromArgb(232, 193, 237, 126)),
                StrokeThickness = 1.3
            };
            PipeRadarCanvas.Children.Add(sector);

            if (!isCompact && item.Confidence >= 0.85)
            {
                var halo = new Path
                {
                    Data = BuildRingSectorGeometry(cx, cy, ringInnerRadius - 2, ringOuterRadius + 2, startDeg, sweepDeg),
                    Fill = Brushes.Transparent,
                    Stroke = new SolidColorBrush(Color.FromArgb(130, 233, 245, 128)),
                    StrokeThickness = 1.2
                };
                PipeRadarCanvas.Children.Add(halo);
            }

            var anchorRadius = ringOuterRadius + 1;
            var labelRadius = ringOuterRadius + (isCompact ? 14 : 16) + (i % 2) * (isCompact ? 8 : 11);

            var x1 = cx + Math.Cos(midRad) * anchorRadius;
            var y1 = cy + Math.Sin(midRad) * anchorRadius;
            var x2 = cx + Math.Cos(midRad) * labelRadius;
            var y2 = cy + Math.Sin(midRad) * labelRadius;

            var connector = new Line
            {
                X1 = x1,
                Y1 = y1,
                X2 = x2,
                Y2 = y2,
                Stroke = new SolidColorBrush(Color.FromArgb(210, 66, 93, 51)),
                StrokeThickness = isCompact ? 1.0 : 1.1
            };
            PipeRadarCanvas.Children.Add(connector);

            var title = string.IsNullOrWhiteSpace(item.Code) ? item.Label : $"{item.Code} {item.Label}";
            var titleLimit = isCompact ? 18 : 22;
            if (title.Length > titleLimit)
                title = title[..titleLimit] + "...";

            var detail = $"{item.MeterStart:0.0}-{item.MeterEnd:0.0}m";
            if (!string.IsNullOrWhiteSpace(item.PositionClock))
                detail += $" @ {item.PositionClock}h";
            if (!isCompact && item.ExtentPercent is > 0)
                detail += $" / {item.ExtentPercent}%";
            if (!isCompact)
                detail += $" / {item.ConfidencePct}";

            var tb = new TextBlock
            {
                Text = $"{title}\n{detail}",
                Foreground = new SolidColorBrush(Color.FromRgb(39, 44, 43)),
                FontSize = isCompact ? 9.0 : 9.4,
                FontWeight = FontWeights.SemiBold,
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = labelMaxWidth
            };

            var labelBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(240, 246, 250, 241)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(215, 146, 186, 104)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(5),
                Padding = new Thickness(5, 3, 5, 3),
                Child = tb
            };
            labelBorder.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            var desired = labelBorder.DesiredSize;

            var alignRight = Math.Cos(midRad) >= 0;
            var left = alignRight ? x2 + 3 : x2 - desired.Width - 3;
            var topOffset = isCompact
                ? ((i % 2) == 0 ? -4.0 : 4.0)
                : ((i % 3) - 1) * 11.0;
            var top = y2 - desired.Height / 2.0 + topOffset;

            Canvas.SetLeft(labelBorder, Math.Clamp(left, 2, width - desired.Width - 2));
            Canvas.SetTop(labelBorder, Math.Clamp(top, 2, height - desired.Height - 2));
            PipeRadarCanvas.Children.Add(labelBorder);
        }
    }

    private static double ResolveCenterAngle(DetectionItem item, int index, int totalCount)
    {
        var parsedClock = ParseClockHour(item.PositionClock);
        if (parsedClock.HasValue)
            return -90 + (parsedClock.Value % 12) * 30;

        if (totalCount <= 1)
            return -90;

        return -90 + index * (360.0 / totalCount);
    }

    private static double ResolveSweep(DetectionItem item)
    {
        if (item.ExtentPercent is > 0)
            return Math.Clamp(item.ExtentPercent.Value * 3.6, 22.0, 150.0);

        var meterSpan = Math.Max(0, item.MeterEnd - item.MeterStart);
        return Math.Clamp(20.0 + meterSpan * 3.0 + item.Confidence * 22.0, 20.0, 62.0);
    }

    private static int? ParseClockHour(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var m = System.Text.RegularExpressions.Regex.Match(raw, @"\b(?<h>1[0-2]|0?[1-9])\b");
        if (!m.Success)
            return null;

        if (!int.TryParse(m.Groups["h"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var hour))
            return null;

        if (hour == 0)
            return 12;

        if (hour > 12)
            hour %= 12;

        return hour == 0 ? 12 : hour;
    }

    private static Geometry BuildRingSectorGeometry(
        double cx,
        double cy,
        double innerRadius,
        double outerRadius,
        double startDeg,
        double sweepDeg)
    {
        var startRad = DegToRad(startDeg);
        var endRad = DegToRad(startDeg + sweepDeg);
        var largeArc = sweepDeg > 180;

        var p1 = new Point(cx + Math.Cos(startRad) * outerRadius, cy + Math.Sin(startRad) * outerRadius);
        var p2 = new Point(cx + Math.Cos(endRad) * outerRadius, cy + Math.Sin(endRad) * outerRadius);
        var p3 = new Point(cx + Math.Cos(endRad) * innerRadius, cy + Math.Sin(endRad) * innerRadius);
        var p4 = new Point(cx + Math.Cos(startRad) * innerRadius, cy + Math.Sin(startRad) * innerRadius);

        var figure = new PathFigure
        {
            StartPoint = p1,
            IsClosed = true,
            IsFilled = true
        };
        figure.Segments.Add(new ArcSegment(p2, new Size(outerRadius, outerRadius), 0, largeArc, SweepDirection.Clockwise, true));
        figure.Segments.Add(new LineSegment(p3, true));
        figure.Segments.Add(new ArcSegment(p4, new Size(innerRadius, innerRadius), 0, largeArc, SweepDirection.Counterclockwise, true));

        return new PathGeometry(new[] { figure });
    }

    private static double DegToRad(double deg) => deg * Math.PI / 180.0;

    private static string FormatTelemetry(TelemetrySummary? t)
    {
        if (t is null)
            return "";

        static string Fmt(PhaseStat s) => s.TotalMs > 0
            ? $"Mean={s.MeanMs:F0}ms  P95={s.P95Ms:F0}ms"
            : "—";

        var parts = new List<string>
        {
            $"Wall: {t.WallClockMs / 1000.0:F1}s",
            $"Frames: {t.TotalFrames} ({t.SkippedFrames} skipped)",
            $"Extraction: {Fmt(t.Extraction)}"
        };

        if (t.Yolo.TotalMs > 0) parts.Add($"YOLO: {Fmt(t.Yolo)}");
        if (t.Dino.TotalMs > 0) parts.Add($"DINO: {Fmt(t.Dino)}");
        if (t.Sam.TotalMs > 0) parts.Add($"SAM: {Fmt(t.Sam)}");
        if (t.Qwen.TotalMs > 0) parts.Add($"Vision: {Fmt(t.Qwen)}");
        parts.Add($"Total/Frame: {Fmt(t.Total)}");

        return string.Join("  |  ", parts);
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
        _liveFrameWindow.UpdateFrame(
            Vm.LiveFrameImage, _liveFrameFindings,
            Vm.LiveFrameStatus, Vm.LiveFrameInfo, Vm.LiveFrameQuantSummary);
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
            MessageBox.Show("Kein gültiges Ergebnis zum Übertragen vorhanden.", "Videoanalyse KI",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        DialogResult = true;
        Close();
    }
}
