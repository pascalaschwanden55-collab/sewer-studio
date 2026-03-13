using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.Application.Protocol;
using AuswertungPro.Next.UI.ViewModels.Windows;
using CommunityToolkit.Mvvm.Input;
using LibVLCSharp.Shared;
using MediaPlayer = LibVLCSharp.Shared.MediaPlayer;

namespace AuswertungPro.Next.UI.Views.Windows;

/// <summary>
/// Codier-Modus: Durchlauf einer Haltung von 0.00m bis Ende mit Overlay-Werkzeugen.
/// </summary>
public partial class CodingModeWindow : Window
{
    private readonly CodingSessionViewModel _vm;
    private readonly ICodingSessionService _sessionService;
    private readonly IOverlayToolService _overlayService;

    private LibVLC? _libVlc;
    private MediaPlayer? _player;
    private HaltungRecord? _haltung;
    private long _videoDurationMs;  // Gesamtlaenge des Videos in ms
    private bool _videoReady;       // true sobald VLC die Dauer kennt
    private bool _videoPlaying;     // true wenn Video laeuft (nicht pausiert)
    private double _lastSyncedMeter = -1; // Verhindert Doppel-Sync

    // Overlay-Zeichnung (WPF Shapes auf Canvas)
    private Line? _previewLine;
    private Ellipse? _previewPoint;
    private Rectangle? _previewRect;

    // Kalibrierungsmodus
    private bool _isCalibrating;
    private NormalizedPoint? _calibStart;

    // KI Live-Analyse
    private LiveDetectionService? _liveDetection;
    private AiRuntimeConfig? _aiConfig;
    private OllamaClient? _ollamaClient;
    private CancellationTokenSource? _analysisCts;
    private bool _isAnalyzing;

    public CodingModeWindow(HaltungRecord haltung, string? videoPath)
    {
        InitializeComponent();

        _haltung = haltung;
        _sessionService = new CodingSessionService();
        _overlayService = new OverlayToolService();
        _vm = new CodingSessionViewModel(_sessionService, _overlayService);
        _vm.VideoPath = videoPath;

        DataContext = _vm;

        // UI-Updates bei ViewModel-Aenderungen
        _vm.PropertyChanged += (_, e) => Dispatcher.Invoke(() => UpdateUi(e.PropertyName));
        _vm.SessionCompleted += OnSessionCompleted;

        // Events-Liste binden
        LstEvents.ItemsSource = _vm.Events;

        // Event-Items nach Laden einfaerben (Zone-Dot, Konfidenz, Status)
        _vm.Events.CollectionChanged += (_, _) => Dispatcher.InvokeAsync(ColorizeEventListItems);

        Loaded += OnLoaded;
        Closing += OnClosing;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Video initialisieren (pausiert starten, Dauer ermitteln)
        if (!string.IsNullOrEmpty(_vm.VideoPath))
        {
            try
            {
                // Core.Initialize muss mindestens einmal aufgerufen werden
                Core.Initialize();

                _libVlc = new LibVLC("--no-audio", "--no-video-title-show");
                _player = new MediaPlayer(_libVlc);
                VideoView.MediaPlayer = _player;

                // Sobald VLC die Dauer kennt, merken wir sie und pausieren
                _player.LengthChanged += (_, args) =>
                {
                    if (args.Length > 0)
                    {
                        _videoDurationMs = args.Length;
                        _videoReady = true;
                    }
                };

                // Fehler-Event fuer Diagnose
                _player.EncounteredError += (_, _) =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        TxtStatus.Text = "Video-Fehler: Datei konnte nicht geladen werden";
                        System.Diagnostics.Debug.WriteLine($"VLC EncounteredError fuer: {_vm.VideoPath}");
                    });
                };

                // Nach dem ersten Frame pausieren (Playing-Event kommt nachdem erster Frame gerendert)
                _player.Playing += OnPlayerFirstPlaying;

                using var media = new Media(_libVlc, _vm.VideoPath, FromType.FromPath);
                _player.Play(media);
                // Pause kommt im Event-Handler, damit VLC erst die Dauer ermitteln kann
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Video-Fehler: {ex.Message}");
                TxtStatus.Text = $"Video-Fehler: {ex.Message}";
            }
        }

        // DN aus Haltungsdaten laden
        int nominalDn = 0;
        if (_haltung != null && _haltung.Fields.TryGetValue("DN_mm", out var dnStr)
            && int.TryParse(dnStr, out var dn) && dn > 0)
        {
            nominalDn = dn;
            _overlayService.SetCalibration(new PipeCalibration
            {
                NominalDiameterMm = dn,
                PipePixelDiameter = 0 // Wird per Referenzmessung gesetzt
            });
        }

        // Session starten
        _vm.StartSessionCommand.Execute(_haltung);

        TxtHaltungInfo.Text = $"Haltung: {_vm.HaltungName} (0.00m → {_vm.EndMeter:F2}m)";

        // PipeGraphTimeline: Marker-Accessoren und Commands verdrahten
        PipeTimeline.MeterAccessor = obj => obj is CodingEvent ce ? ce.MeterAtCapture : 0;
        PipeTimeline.CodeAccessor = obj => obj is CodingEvent ce ? ce.Entry.Code : "?";
        PipeTimeline.ConfidenceAccessor = obj => obj is CodingEvent ce && ce.AiContext != null
            ? ce.AiContext.Confidence : -1;
        PipeTimeline.IsRejectedAccessor = obj => obj is CodingEvent ce
            && CodingSessionViewModel.GetDefectStatus(ce) == DefectStatus.Rejected;
        PipeTimeline.Markers = _vm.Events;
        PipeTimeline.NavigateToMeterCommand = new RelayCommand<double>(meter =>
        {
            if (_vm.IsRunning || _vm.IsPaused)
            {
                _sessionService.MoveToMeter(meter);
                _lastSyncedMeter = -1;
                SyncVideoToMeter();
            }
        });
        PipeTimeline.MarkerClickedCommand = new RelayCommand<object>(item =>
        {
            if (item is CodingEvent ce)
            {
                _vm.JumpToDefectCommand.Execute(ce);
                LstEvents.SelectedItem = ce;
            }
        });

        // Kalibrierungs-Status anzeigen
        TxtCalibrationDn.Text = nominalDn > 0 ? $"DN: {nominalDn} mm" : "DN: unbekannt";
        TxtCalibrationStatus.Text = _overlayService.IsCalibrated
            ? "Kalibriert"
            : "Nicht kalibriert – Referenz zeichnen";

        // KI Live-Detection initialisieren
        InitAiOverlay();

        // ViewModel-Events fuer KI-Training
        _vm.DefectJumpRequested += (_, ev) =>
        {
            // Video zum Defekt springen
            _lastSyncedMeter = -1;
            SyncVideoToMeter();
        };

        _vm.DefectEditRequested += (_, ev) =>
        {
            // Editor wird direkt in BtnEditDefect_Click geoeffnet
        };
    }

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        _analysisCts?.Cancel();
        _analysisCts?.Dispose();
        _ollamaClient?.Dispose();
        _player?.Stop();
        _player?.Dispose();
        _libVlc?.Dispose();
        _vm.Dispose();
    }

    // --- UI-Update ---

    private void UpdateUi(string? propertyName)
    {
        TxtCurrentMeter.Text = $"{_vm.CurrentMeter:F2}m";
        TxtStatus.Text = _vm.StatusText;
        TxtEventCount.Text = $"{_vm.EventCount} Ereignisse";
        TxtProgress.Text = $"{_vm.ProgressPercent:F0}%";

        // PipeGraphTimeline wird per Binding aktualisiert (CurrentMeter, EndMeter)

        // Video zur Meter-Position synchronisieren
        SyncVideoToMeter();

        // OSD-Meter + Videozeit im Overlay aktualisieren
        TxtVideoMeter.Text = $"{_vm.CurrentMeter:F2}m";
        if (_vm.CurrentVideoTime.HasValue)
        {
            var t = _vm.CurrentVideoTime.Value;
            TxtVideoTime.Text = t.TotalHours >= 1
                ? t.ToString(@"h\:mm\:ss")
                : t.ToString(@"mm\:ss");
        }

        // Overlay-Messwerte anzeigen
        UpdateOverlayInfo(_vm.CurrentOverlay);

        // Event-Marker werden per CollectionChanged in PipeGraphTimeline aktualisiert

        // Foto-Buttons Zustand aktualisieren
        UpdateFotoButtons();

        // KI-Training: Statistiken + Event-Einfaerbung aktualisieren
        UpdateStatistics();
        ColorizeEventListItems();
    }

    private void UpdateOverlayInfo(OverlayGeometry? overlay)
    {
        if (overlay == null)
        {
            TxtOverlayQ1.Text = "Q1: –";
            TxtOverlayQ2.Text = "Q2: –";
            TxtOverlayClock.Text = "Uhr: –";
            TxtOverlayArc.Text = "Bogen: –";
            MeasurementPanel.Visibility = Visibility.Collapsed;
            return;
        }

        TxtOverlayQ1.Text = overlay.Q1Mm.HasValue ? $"Q1: {overlay.Q1Mm:F1} mm" : "Q1: –";
        TxtOverlayQ2.Text = overlay.Q2Mm.HasValue ? $"Q2: {overlay.Q2Mm:F1} mm" : "Q2: –";
        TxtOverlayClock.Text = overlay.ClockFrom.HasValue
            ? $"Uhr: {overlay.ClockFrom:F1}" + (overlay.ClockTo.HasValue ? $" → {overlay.ClockTo:F1}" : "")
            : "Uhr: –";
        TxtOverlayArc.Text = overlay.ArcDegrees.HasValue ? $"Bogen: {overlay.ArcDegrees:F0}°" : "Bogen: –";

        // Kompakte Anzeige im Video
        MeasurementPanel.Visibility = Visibility.Visible;
        var parts = new System.Collections.Generic.List<string>();
        if (overlay.Q1Mm.HasValue) parts.Add($"Q1:{overlay.Q1Mm:F1}mm");
        if (overlay.ClockFrom.HasValue) parts.Add($"Uhr:{overlay.ClockFrom:F1}");
        if (overlay.ArcDegrees.HasValue) parts.Add($"{overlay.ArcDegrees:F0}°");
        TxtMeasurement.Text = string.Join("  |  ", parts);
    }

    /// <summary>Marker-Aktualisierung wird jetzt durch PipeGraphTimeline (CollectionChanged) erledigt.</summary>
    private void UpdateEventMarkers() { /* PipeGraphTimeline uebernimmt */ }

    /// <summary>
    /// Wird einmalig aufgerufen wenn VLC den ersten Frame gerendert hat.
    /// Pausiert sofort, damit das Video am Anfang stehenbleibt.
    /// </summary>
    private void OnPlayerFirstPlaying(object? sender, EventArgs e)
    {
        if (_player == null) return;
        // Event abmelden, damit es nur einmal feuert
        _player.Playing -= OnPlayerFirstPlaying;
        // Kurz warten, damit VLC die Dauer sicher kennt, dann pausieren
        Task.Delay(150).ContinueWith(_ =>
        {
            if (_player.Length > 0)
                _videoDurationMs = _player.Length;
            _videoReady = _videoDurationMs > 0;
            _videoPlaying = false;
            _player.SetPause(true);
            Dispatcher.Invoke(() =>
            {
                // Session auch pausieren, damit BtnPause_Click korrekt "Fortsetzen" erkennt
                if (_vm.IsRunning)
                    _vm.PauseSessionCommand.Execute(null);
                BtnPause.Content = "Fortsetzen";
            });
        });
    }

    private void SyncVideoToMeter()
    {
        // Video-Sync: Meter → Video-Zeit ueber Haltungslaenge/Video-Dauer
        if (_player == null || !_videoReady) return;

        // Nicht synchronisieren wenn Video frei laeuft
        if (_videoPlaying) return;

        double totalLengthMs = _videoDurationMs;
        if (totalLengthMs <= 0 || _vm.EndMeter <= 0) return;

        // Nur seek wenn sich der Meter tatsaechlich geaendert hat
        if (Math.Abs(_vm.CurrentMeter - _lastSyncedMeter) < 0.005) return;
        _lastSyncedMeter = _vm.CurrentMeter;

        double fraction = _vm.CurrentMeter / _vm.EndMeter;
        long targetMs = (long)(fraction * totalLengthMs);
        _player.Time = Math.Clamp(targetMs, 0, (long)totalLengthMs);

        // Aktuelle Video-Zeit im ViewModel merken
        _vm.CurrentVideoTime = TimeSpan.FromMilliseconds(_player.Time);
    }

    // --- Overlay-Canvas Interaktion ---

    private void OverlayCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var pos = e.GetPosition(OverlayCanvas);
        var normalized = PixelToNormalized(pos);

        // Kalibrierungsmodus: Referenzlinie fuer Rohrdurchmesser
        if (_isCalibrating)
        {
            _calibStart = normalized;
            OverlayCanvas.CaptureMouse();
            ClearPreviewShapes();
            return;
        }

        if (_overlayService.ActiveTool == OverlayToolType.None) return;

        _vm.OnCanvasMouseDown(normalized);
        OverlayCanvas.CaptureMouse();
        ClearPreviewShapes();
    }

    private void OverlayCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        var pos = e.GetPosition(OverlayCanvas);
        var normalized = PixelToNormalized(pos);

        // Kalibrierungs-Vorschau
        if (_isCalibrating && _calibStart != null)
        {
            ClearPreviewShapes();
            var p1 = NormalizedToPixel(_calibStart);
            var p2 = NormalizedToPixel(normalized);
            _previewLine = new Line
            {
                X1 = p1.X, Y1 = p1.Y,
                X2 = p2.X, Y2 = p2.Y,
                Stroke = Brushes.Magenta,
                StrokeThickness = 2.5,
                StrokeDashArray = new DoubleCollection { 6, 3 }
            };
            OverlayCanvas.Children.Add(_previewLine);

            // Pixel-Laenge anzeigen
            double pxLen = Math.Sqrt(Math.Pow(p2.X - p1.X, 2) + Math.Pow(p2.Y - p1.Y, 2));
            TxtCalibrationHint.Text = $"Referenzlinie: {pxLen:F0} px";
            return;
        }

        if (!_overlayService.IsDrawing) return;

        _vm.OnCanvasMouseMove(normalized);

        // Vorschau-Shape zeichnen
        RenderPreview(_overlayService.DrawStartPoint, normalized);
    }

    private void OverlayCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        var pos = e.GetPosition(OverlayCanvas);
        var normalized = PixelToNormalized(pos);

        // Kalibrierung abschliessen
        if (_isCalibrating && _calibStart != null)
        {
            OverlayCanvas.ReleaseMouseCapture();
            ApplyCalibration(_calibStart, normalized);
            return;
        }

        if (!_overlayService.IsDrawing) return;

        _vm.OnCanvasMouseUp(normalized);
        OverlayCanvas.ReleaseMouseCapture();

        // Finale Geometrie rendern
        if (_vm.CurrentOverlay != null)
        {
            ClearPreviewShapes();
            RenderOverlayGeometry(_vm.CurrentOverlay);
            UpdateOverlayInfo(_vm.CurrentOverlay);
            BtnCreateEvent.IsEnabled = true;
        }
    }

    // --- Kalibrierung ---

    private void BtnCalibrate_Checked(object sender, RoutedEventArgs e)
    {
        _isCalibrating = true;
        _calibStart = null;
        CalibrationHint.Visibility = Visibility.Visible;
        TxtCalibrationHint.Text = "Linie ueber den sichtbaren Rohrdurchmesser zeichnen";

        // Andere Werkzeuge deaktivieren
        _overlayService.ActiveTool = OverlayToolType.None;
        foreach (var child in ((StackPanel)BtnCalibrate.Parent).Children)
        {
            if (child is ToggleButton tb && tb != BtnCalibrate)
                tb.IsChecked = false;
        }
    }

    private void BtnCalibrate_Unchecked(object sender, RoutedEventArgs e)
    {
        _isCalibrating = false;
        _calibStart = null;
        CalibrationHint.Visibility = Visibility.Collapsed;
        ClearPreviewShapes();
    }

    private void ApplyCalibration(NormalizedPoint start, NormalizedPoint end)
    {
        // Pixel-Abstand berechnen (in tatsaechlichen Canvas-Pixeln)
        var p1 = NormalizedToPixel(start);
        var p2 = NormalizedToPixel(end);
        double pixelDiameter = Math.Sqrt(Math.Pow(p2.X - p1.X, 2) + Math.Pow(p2.Y - p1.Y, 2));

        if (pixelDiameter < 10)
        {
            TxtCalibrationHint.Text = "Linie zu kurz – bitte nochmal zeichnen";
            _calibStart = null;
            return;
        }

        // Rohrmitte = Mittelpunkt der Referenzlinie
        var center = new NormalizedPoint((start.X + end.X) / 2, (start.Y + end.Y) / 2);

        // Normierter Durchmesser (Laenge der Referenzlinie in 0.0–1.0)
        double dx = end.X - start.X;
        double dy = end.Y - start.Y;
        double normDiameter = Math.Sqrt(dx * dx + dy * dy);

        // DN aus bestehendem Calibration oder Fallback
        int dn = _overlayService.Calibration?.NominalDiameterMm ?? 300;

        var cal = new PipeCalibration
        {
            NominalDiameterMm = dn,
            PipePixelDiameter = pixelDiameter,
            NormalizedDiameter = normDiameter,
            PipeCenter = center
        };
        _overlayService.SetCalibration(cal);

        // Session-Kalibrierung auch setzen
        if (_sessionService.ActiveSession != null)
            _sessionService.ActiveSession.Calibration = cal;

        // UI aktualisieren
        double mmPerNorm = cal.MmPerNormUnit;
        TxtCalibrationStatus.Text = $"Kalibriert: {mmPerNorm:F1} mm/norm";
        TxtCalibrationHint.Text = $"Kalibriert! DN {dn}mm = {pixelDiameter:F0}px";

        // Referenzlinie dauerhaft anzeigen (duenn, magenta)
        ClearPreviewShapes();
        var refLine = new Line
        {
            X1 = p1.X, Y1 = p1.Y,
            X2 = p2.X, Y2 = p2.Y,
            Stroke = Brushes.Magenta,
            StrokeThickness = 1.5,
            Opacity = 0.6
        };
        OverlayCanvas.Children.Add(refLine);

        // DN-Label an der Mitte
        var midPx = NormalizedToPixel(center);
        var label = new TextBlock
        {
            Text = $"DN {dn}",
            Foreground = Brushes.Magenta,
            FontSize = 11,
            FontWeight = FontWeights.SemiBold,
            Background = new SolidColorBrush(Color.FromArgb(140, 0, 0, 0)),
            Padding = new Thickness(4, 1, 4, 1)
        };
        Canvas.SetLeft(label, midPx.X + 6);
        Canvas.SetTop(label, midPx.Y - 8);
        OverlayCanvas.Children.Add(label);

        // Kalibrierungsmodus beenden
        _isCalibrating = false;
        _calibStart = null;
        BtnCalibrate.IsChecked = false;
        CalibrationHint.Visibility = Visibility.Collapsed;
    }

    private NormalizedPoint PixelToNormalized(Point pixel)
    {
        double w = OverlayCanvas.ActualWidth;
        double h = OverlayCanvas.ActualHeight;
        if (w <= 0 || h <= 0) return new NormalizedPoint(0.5, 0.5);
        return new NormalizedPoint(pixel.X / w, pixel.Y / h);
    }

    private Point NormalizedToPixel(NormalizedPoint normalized)
    {
        return new Point(
            normalized.X * OverlayCanvas.ActualWidth,
            normalized.Y * OverlayCanvas.ActualHeight);
    }

    // --- Vorschau-Rendering ---

    private void RenderPreview(NormalizedPoint? start, NormalizedPoint current)
    {
        ClearPreviewShapes();
        if (start == null) return;

        var p1 = NormalizedToPixel(start);
        var p2 = NormalizedToPixel(current);

        switch (_overlayService.ActiveTool)
        {
            case OverlayToolType.Line:
            case OverlayToolType.Stretch:
                _previewLine = new Line
                {
                    X1 = p1.X, Y1 = p1.Y,
                    X2 = p2.X, Y2 = p2.Y,
                    Stroke = Brushes.Lime,
                    StrokeThickness = 2,
                    StrokeDashArray = new DoubleCollection { 4, 2 }
                };
                OverlayCanvas.Children.Add(_previewLine);
                break;

            case OverlayToolType.Rectangle:
                _previewRect = new Rectangle
                {
                    Width = Math.Abs(p2.X - p1.X),
                    Height = Math.Abs(p2.Y - p1.Y),
                    Stroke = Brushes.Cyan,
                    StrokeThickness = 2,
                    StrokeDashArray = new DoubleCollection { 4, 2 },
                    Fill = new SolidColorBrush(Color.FromArgb(40, 0, 255, 255))
                };
                Canvas.SetLeft(_previewRect, Math.Min(p1.X, p2.X));
                Canvas.SetTop(_previewRect, Math.Min(p1.Y, p2.Y));
                OverlayCanvas.Children.Add(_previewRect);
                break;

            case OverlayToolType.Arc:
                // Bogen-Vorschau: Linie vom Zentrum zu Start und Ende
                var center = NormalizedToPixel(new NormalizedPoint(0.5, 0.5));
                var arcLine1 = new Line
                {
                    X1 = center.X, Y1 = center.Y,
                    X2 = p1.X, Y2 = p1.Y,
                    Stroke = Brushes.Yellow,
                    StrokeThickness = 1.5,
                    StrokeDashArray = new DoubleCollection { 3, 2 }
                };
                var arcLine2 = new Line
                {
                    X1 = center.X, Y1 = center.Y,
                    X2 = p2.X, Y2 = p2.Y,
                    Stroke = Brushes.Yellow,
                    StrokeThickness = 1.5,
                    StrokeDashArray = new DoubleCollection { 3, 2 }
                };
                OverlayCanvas.Children.Add(arcLine1);
                OverlayCanvas.Children.Add(arcLine2);
                break;

            case OverlayToolType.Point:
                _previewPoint = new Ellipse
                {
                    Width = 12, Height = 12,
                    Fill = Brushes.Red,
                    Stroke = Brushes.White,
                    StrokeThickness = 2
                };
                Canvas.SetLeft(_previewPoint, p1.X - 6);
                Canvas.SetTop(_previewPoint, p1.Y - 6);
                OverlayCanvas.Children.Add(_previewPoint);
                break;
        }
    }

    private void RenderOverlayGeometry(OverlayGeometry geometry)
    {
        if (geometry.Points.Count < 1) return;

        switch (geometry.ToolType)
        {
            case OverlayToolType.Line:
            case OverlayToolType.Stretch:
            {
                if (geometry.Points.Count < 2) return;
                var p1 = NormalizedToPixel(geometry.Points[0]);
                var p2 = NormalizedToPixel(geometry.Points[1]);
                var line = new Line
                {
                    X1 = p1.X, Y1 = p1.Y,
                    X2 = p2.X, Y2 = p2.Y,
                    Stroke = geometry.ToolType == OverlayToolType.Stretch ? Brushes.Orange : Brushes.Lime,
                    StrokeThickness = 2.5
                };
                OverlayCanvas.Children.Add(line);

                // Laengen-Label
                if (geometry.Q1Mm.HasValue)
                {
                    var midX = (p1.X + p2.X) / 2;
                    var midY = (p1.Y + p2.Y) / 2;
                    var label = new TextBlock
                    {
                        Text = $"{geometry.Q1Mm:F1}mm",
                        Foreground = Brushes.White,
                        FontSize = 12,
                        FontWeight = FontWeights.Bold,
                        Background = new SolidColorBrush(Color.FromArgb(180, 0, 0, 0)),
                        Padding = new Thickness(4, 2, 4, 2)
                    };
                    Canvas.SetLeft(label, midX + 4);
                    Canvas.SetTop(label, midY - 10);
                    OverlayCanvas.Children.Add(label);
                }
                break;
            }

            case OverlayToolType.Arc:
            {
                if (geometry.Points.Count < 2) return;
                var center = NormalizedToPixel(new NormalizedPoint(0.5, 0.5));
                var p1 = NormalizedToPixel(geometry.Points[0]);
                var p2 = NormalizedToPixel(geometry.Points[1]);
                var line1 = new Line
                {
                    X1 = center.X, Y1 = center.Y,
                    X2 = p1.X, Y2 = p1.Y,
                    Stroke = Brushes.Yellow, StrokeThickness = 2
                };
                var line2 = new Line
                {
                    X1 = center.X, Y1 = center.Y,
                    X2 = p2.X, Y2 = p2.Y,
                    Stroke = Brushes.Yellow, StrokeThickness = 2
                };
                OverlayCanvas.Children.Add(line1);
                OverlayCanvas.Children.Add(line2);

                // Winkel-Label
                if (geometry.ArcDegrees.HasValue)
                {
                    var label = new TextBlock
                    {
                        Text = $"{geometry.ArcDegrees:F0}° ({geometry.ClockFrom:F1}→{geometry.ClockTo:F1} Uhr)",
                        Foreground = Brushes.Yellow,
                        FontSize = 12,
                        FontWeight = FontWeights.Bold,
                        Background = new SolidColorBrush(Color.FromArgb(180, 0, 0, 0)),
                        Padding = new Thickness(4, 2, 4, 2)
                    };
                    Canvas.SetLeft(label, center.X + 10);
                    Canvas.SetTop(label, center.Y - 20);
                    OverlayCanvas.Children.Add(label);
                }
                break;
            }

            case OverlayToolType.Point:
            {
                var p = NormalizedToPixel(geometry.Points[0]);
                var dot = new Ellipse
                {
                    Width = 14, Height = 14,
                    Fill = Brushes.Red,
                    Stroke = Brushes.White,
                    StrokeThickness = 2
                };
                Canvas.SetLeft(dot, p.X - 7);
                Canvas.SetTop(dot, p.Y - 7);
                OverlayCanvas.Children.Add(dot);

                if (geometry.ClockFrom.HasValue)
                {
                    var label = new TextBlock
                    {
                        Text = $"{geometry.ClockFrom:F1} Uhr",
                        Foreground = Brushes.White,
                        FontSize = 12,
                        Background = new SolidColorBrush(Color.FromArgb(180, 0, 0, 0)),
                        Padding = new Thickness(4, 2, 4, 2)
                    };
                    Canvas.SetLeft(label, p.X + 10);
                    Canvas.SetTop(label, p.Y - 8);
                    OverlayCanvas.Children.Add(label);
                }
                break;
            }

            case OverlayToolType.Rectangle:
            {
                if (geometry.Points.Count < 4) return;
                var p1 = NormalizedToPixel(geometry.Points[0]);
                var p3 = NormalizedToPixel(geometry.Points[2]);
                var rect = new Rectangle
                {
                    Width = Math.Abs(p3.X - p1.X),
                    Height = Math.Abs(p3.Y - p1.Y),
                    Stroke = Brushes.Cyan,
                    StrokeThickness = 2,
                    Fill = new SolidColorBrush(Color.FromArgb(30, 0, 255, 255))
                };
                Canvas.SetLeft(rect, Math.Min(p1.X, p3.X));
                Canvas.SetTop(rect, Math.Min(p1.Y, p3.Y));
                OverlayCanvas.Children.Add(rect);
                break;
            }
        }
    }

    private void ClearPreviewShapes()
    {
        OverlayCanvas.Children.Clear();
        _previewLine = null;
        _previewPoint = null;
        _previewRect = null;
    }

    // --- Werkzeug-Buttons ---

    private void ToolButton_Checked(object sender, RoutedEventArgs e)
    {
        // Alle anderen Buttons unchecken
        foreach (var child in ((StackPanel)((ToggleButton)sender).Parent).Children)
        {
            if (child is ToggleButton tb && tb != sender)
                tb.IsChecked = false;
        }

        var tool = sender switch
        {
            _ when sender == BtnToolLine => OverlayToolType.Line,
            _ when sender == BtnToolArc => OverlayToolType.Arc,
            _ when sender == BtnToolRect => OverlayToolType.Rectangle,
            _ when sender == BtnToolPoint => OverlayToolType.Point,
            _ when sender == BtnToolStretch => OverlayToolType.Stretch,
            _ => OverlayToolType.None
        };
        _overlayService.ActiveTool = tool;
        ClearPreviewShapes();
    }

    private void ToolButton_Unchecked(object sender, RoutedEventArgs e)
    {
        // Wenn kein Button mehr gecheckt → Tool auf None
        var parent = (StackPanel)((ToggleButton)sender).Parent;
        bool anyChecked = parent.Children.OfType<ToggleButton>().Any(tb => tb.IsChecked == true);
        if (!anyChecked)
            _overlayService.ActiveTool = OverlayToolType.None;
    }

    // --- Navigation ---

    private void BtnNext_Click(object sender, RoutedEventArgs e)
    {
        PauseVideoForNavigation();
        _vm.MoveNextCommand.Execute(null);
        // Nach Navigation Video-Frame aktualisieren
        _lastSyncedMeter = -1;
        SyncVideoToMeter();
    }

    private void BtnPrevious_Click(object sender, RoutedEventArgs e)
    {
        PauseVideoForNavigation();
        _vm.MovePreviousCommand.Execute(null);
        // Nach Navigation Video-Frame aktualisieren
        _lastSyncedMeter = -1;
        SyncVideoToMeter();
    }

    /// <summary>Video pausieren wenn es frei laeuft, damit Meter-Sync korrekt greift.</summary>
    private void PauseVideoForNavigation()
    {
        if (_videoPlaying)
        {
            _videoPlaying = false;
            _player?.SetPause(true);
            if (_vm.IsRunning)
                _vm.PauseSessionCommand.Execute(null);
            BtnPause.Content = "Fortsetzen";
        }
    }

    // MeterSlider entfernt – Navigation erfolgt jetzt ueber PipeGraphTimeline.NavigateToMeterCommand

    // --- Session-Steuerung ---

    private void BtnPause_Click(object sender, RoutedEventArgs e)
    {
        if (_vm.IsPaused)
        {
            // Session + Video fortsetzen
            _vm.ResumeSessionCommand.Execute(null);
            _videoPlaying = true;
            _player?.SetPause(false);
            BtnPause.Content = "Pause";
        }
        else if (_vm.IsRunning)
        {
            // Session + Video pausieren
            _vm.PauseSessionCommand.Execute(null);
            _videoPlaying = false;
            _player?.SetPause(true);
            BtnPause.Content = "Fortsetzen";
        }
    }

    private void BtnComplete_Click(object sender, RoutedEventArgs e)
        => _vm.CompleteSessionCommand.Execute(null);

    private void BtnAbort_Click(object sender, RoutedEventArgs e)
        => _vm.AbortSessionCommand.Execute(null);

    // --- Code-Auswahl ---

    private async void BtnSelectCode_Click(object sender, RoutedEventArgs e)
    {
        // ServiceProvider fuer CodeCatalog und AI holen
        var sp = App.Services as AuswertungPro.Next.UI.ServiceProvider;
        if (sp == null) return;

        // ProtocolEntry fuer den Katalog vorbereiten
        var entry = new ProtocolEntry
        {
            Source = ProtocolEntrySource.Manual,
            MeterStart = _vm.CurrentMeter,
            Zeit = _vm.CurrentVideoTime
        };

        // Overlay-Werte vorab eintragen
        if (_vm.CurrentOverlay != null)
        {
            entry.CodeMeta ??= new ProtocolEntryCodeMeta();
            if (_vm.CurrentOverlay.ClockFrom.HasValue)
                entry.CodeMeta.Parameters["vsa.uhr.von"] = _vm.CurrentOverlay.ClockFrom.Value.ToString("F1");
            if (_vm.CurrentOverlay.ClockTo.HasValue)
                entry.CodeMeta.Parameters["vsa.uhr.bis"] = _vm.CurrentOverlay.ClockTo.Value.ToString("F1");
            if (_vm.CurrentOverlay.Q1Mm.HasValue)
                entry.CodeMeta.Parameters["vsa.q1"] = _vm.CurrentOverlay.Q1Mm.Value.ToString("F1");
            if (_vm.CurrentOverlay.Q2Mm.HasValue)
                entry.CodeMeta.Parameters["vsa.q2"] = _vm.CurrentOverlay.Q2Mm.Value.ToString("F1");
        }

        var explorerVm = new ViewModels.Windows.VsaCodeExplorerViewModel(
            entry,
            _vm.CurrentMeter,
            _vm.CurrentVideoTime);

        var dlg = new VsaCodeExplorerWindow(explorerVm, _vm.VideoPath, _vm.CurrentVideoTime) { Owner = this };
        if (dlg.ShowDialog() == true && dlg.SelectedEntry is not null)
        {
            var result = dlg.SelectedEntry;
            entry.Code = result.Code;
            entry.Beschreibung = result.Beschreibung;
            entry.CodeMeta = result.CodeMeta;
            entry.MeterStart = result.MeterStart;
            entry.MeterEnd = result.MeterEnd;
            entry.Zeit = result.Zeit;
            entry.IsStreckenschaden = result.IsStreckenschaden;
            entry.FotoPaths = result.FotoPaths;

            _vm.SelectedCode = entry.Code;
            _vm.SelectedCodeDescription = entry.Beschreibung;
            TxtSelectedCode.Text = $"{entry.Code} – {entry.Beschreibung}";
            BtnCreateEvent.IsEnabled = true;

            // Entry direkt als Event uebernehmen (Code + Parameter sind gesetzt)
            _sessionService.AddEvent(entry, _vm.CurrentOverlay);
            var newEvent = new CodingEvent
            {
                Entry = entry,
                Overlay = _vm.CurrentOverlay,
                MeterAtCapture = entry.MeterStart ?? _vm.CurrentMeter,
                VideoTimestamp = entry.Zeit ?? _vm.CurrentVideoTime ?? TimeSpan.Zero
            };
            _vm.Events.Add(newEvent);

            // Nach Meter sortiert anzeigen
            ResortEventsByMeter();

            TxtEventCount.Text = $"{_vm.EventCount} Ereignisse";

            // Reset
            _vm.CurrentOverlay = null;
            ClearPreviewShapes();
            TxtSelectedCode.Text = "";
            BtnCreateEvent.IsEnabled = false;
            UpdateEventMarkers();
            UpdateOverlayInfo(null);
            UpdateFotoButtons();

            // Neues Event in der Liste selektieren (sortiert: nach Meter finden)
            LstEvents.SelectedItem = newEvent;

            // Automatisch Foto 1 anbieten nach Codierung
            await OfferPhotoCapture(newEvent);
        }
    }

    /// <summary>
    /// Nach dem Codieren einer Beobachtung automatisch Foto-Erfassung anbieten.
    /// </summary>
    private async Task OfferPhotoCapture(CodingEvent codingEvent)
    {
        // Foto 1 anbieten
        var result1 = MessageBox.Show(
            $"Foto 1 fuer {codingEvent.Entry.Code} ({codingEvent.MeterAtCapture:F2}m) erstellen?",
            "Foto erstellen",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result1 == MessageBoxResult.Yes)
        {
            LstEvents.SelectedItem = codingEvent;
            await CapturePhotoForSelectedEvent(0);

            // Foto 2 anbieten nur wenn Foto 1 erstellt wurde
            var result2 = MessageBox.Show(
                $"Foto 2 fuer {codingEvent.Entry.Code} ({codingEvent.MeterAtCapture:F2}m) erstellen?",
                "Foto erstellen",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result2 == MessageBoxResult.Yes)
            {
                await CapturePhotoForSelectedEvent(1);
            }
        }
    }

    private void BtnCreateEvent_Click(object sender, RoutedEventArgs e)
    {
        _vm.CreateEventCommand.Execute(null);
        ClearPreviewShapes();
        TxtSelectedCode.Text = "";
        BtnCreateEvent.IsEnabled = false;
        UpdateFotoButtons();
    }

    // --- Foto-Erfassung (Foto 1 / Foto 2 pro Beobachtung) ---

    private async void BtnFoto1_Click(object sender, RoutedEventArgs e)
        => await CapturePhotoForSelectedEvent(0);

    private async void BtnFoto2_Click(object sender, RoutedEventArgs e)
        => await CapturePhotoForSelectedEvent(1);

    /// <summary>
    /// Foto vom aktuellen Frame erstellen und dem gewaehlten Event zuweisen.
    /// fotoIndex: 0 = Foto 1, 1 = Foto 2.
    /// </summary>
    private async Task CapturePhotoForSelectedEvent(int fotoIndex)
    {
        // Ziel-Event bestimmen: ausgewaehltes Event in der Liste, oder letztes Event
        var targetEvent = LstEvents.SelectedItem as CodingEvent
            ?? _vm.Events.LastOrDefault();

        if (targetEvent == null)
        {
            TxtFotoStatus.Text = "Kein Ereignis vorhanden – zuerst Code waehlen.";
            return;
        }

        try
        {
            BtnFoto1.IsEnabled = false;
            BtnFoto2.IsEnabled = false;
            TxtFotoStatus.Text = $"Foto {fotoIndex + 1} wird aufgenommen...";

            var pngBytes = await CaptureCurrentFrameAsync();
            if (pngBytes == null || pngBytes.Length == 0)
            {
                TxtFotoStatus.Text = "Frame konnte nicht extrahiert werden.";
                return;
            }

            // Foto speichern: Im Projekt-Ordner neben dem Video
            var basePath = !string.IsNullOrEmpty(_vm.VideoPath)
                ? System.IO.Path.GetDirectoryName(_vm.VideoPath)!
                : System.IO.Path.GetTempPath();

            var fotoDir = System.IO.Path.Combine(basePath, "Fotos");
            System.IO.Directory.CreateDirectory(fotoDir);

            var fileName = $"{_vm.HaltungName}_{targetEvent.MeterAtCapture:F2}m_Foto{fotoIndex + 1}_{DateTime.Now:HHmmss}.png";
            // Ungueltige Zeichen aus Dateiname entfernen
            foreach (var c in System.IO.Path.GetInvalidFileNameChars())
                fileName = fileName.Replace(c, '_');

            var fotoPath = System.IO.Path.Combine(fotoDir, fileName);
            await System.IO.File.WriteAllBytesAsync(fotoPath, pngBytes);

            // Foto dem Event zuweisen (Index 0 oder 1)
            while (targetEvent.Entry.FotoPaths.Count <= fotoIndex)
                targetEvent.Entry.FotoPaths.Add("");
            targetEvent.Entry.FotoPaths[fotoIndex] = fotoPath;

            TxtFotoStatus.Text = $"Foto {fotoIndex + 1} gespeichert: {fileName}";

            // Liste aktualisieren (PhotoIndicator)
            LstEvents.Items.Refresh();
            UpdateFotoButtons();
        }
        catch (Exception ex)
        {
            TxtFotoStatus.Text = $"Fehler: {ex.Message}";
        }
        finally
        {
            BtnFoto1.IsEnabled = true;
            BtnFoto2.IsEnabled = true;
        }
    }

    /// <summary>Foto-Buttons aktivieren wenn mindestens ein Event existiert.</summary>
    private void UpdateFotoButtons()
    {
        bool hasEvents = _vm.Events.Count > 0;
        BtnFoto1.IsEnabled = hasEvents;
        BtnFoto2.IsEnabled = hasEvents;

        // Status des gewaehlten Events anzeigen
        var sel = LstEvents.SelectedItem as CodingEvent ?? _vm.Events.LastOrDefault();
        if (sel != null)
        {
            int count = sel.Entry.FotoPaths.Count(p => !string.IsNullOrEmpty(p));
            TxtFotoStatus.Text = count > 0
                ? $"{sel.Entry.Code} – {count} Foto(s) vorhanden"
                : $"{sel.Entry.Code} – noch keine Fotos";
        }
        else
        {
            TxtFotoStatus.Text = "";
        }
    }

    // --- Doppelklick auf Ereignis → Code korrigieren ---

    private void LstEvents_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (LstEvents.SelectedItem is not CodingEvent ev) return;

        var sp = App.Services as AuswertungPro.Next.UI.ServiceProvider;
        if (sp == null) return;

        // ProtocolEntry zum Bearbeiten oeffnen (bestehende Werte vorbelegen)
        var entry = ev.Entry;

        var explorerVm = new ViewModels.Windows.VsaCodeExplorerViewModel(
            entry,
            entry.MeterStart,
            entry.Zeit);

        var dlg = new VsaCodeExplorerWindow(explorerVm, _vm.VideoPath, _vm.CurrentVideoTime) { Owner = this };
        if (dlg.ShowDialog() == true && dlg.SelectedEntry is not null)
        {
            var result = dlg.SelectedEntry;
            entry.Code = result.Code;
            entry.Beschreibung = result.Beschreibung;
            entry.CodeMeta = result.CodeMeta;
            entry.MeterStart = result.MeterStart;
            entry.MeterEnd = result.MeterEnd;
            entry.Zeit = result.Zeit;
            entry.IsStreckenschaden = result.IsStreckenschaden;
            entry.FotoPaths = result.FotoPaths;

            // Code wurde korrigiert – Event aktualisieren
            _sessionService.UpdateEvent(ev.EventId, entry, ev.Overlay);

            // Liste aktualisieren
            LstEvents.Items.Refresh();
            UpdateEventMarkers();
            TxtEventCount.Text = $"{_vm.EventCount} Ereignisse";
        }
    }

    // --- Event-Auswahl in Liste → Defekt-Details + Foto-Status ---

    private void LstEvents_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateFotoButtons();

        if (LstEvents.SelectedItem is CodingEvent ev)
        {
            _vm.SelectedDefect = ev;
            UpdateDefectDetailPanel(ev);
        }
        else
        {
            _vm.SelectedDefect = null;
            DefectDetailPanel.Visibility = Visibility.Collapsed;
        }
    }

    // ═══════════════════════════════════════════════════════════
    // KI-Training: Scan-Modus, Defekt-Aktionen, Detail-Panel
    // ═══════════════════════════════════════════════════════════

    private void ScanMode_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton rb && rb.Tag is string mode)
            _vm.SetScanModeCommand.Execute(mode);
    }

    private void BtnAcceptDefect_Click(object sender, RoutedEventArgs e)
    {
        _vm.AcceptDefectCommand.Execute(null);
        if (_vm.SelectedDefect != null)
        {
            UpdateDefectDetailPanel(_vm.SelectedDefect);
            LstEvents.Items.Refresh();
            UpdateEventMarkers();
        }
    }

    private void BtnEditDefect_Click(object sender, RoutedEventArgs e)
    {
        if (_vm.SelectedDefect == null) return;

        // Editor oeffnen fuer Code-Korrektur
        var ev = _vm.SelectedDefect;
        var sp = App.Services as AuswertungPro.Next.UI.ServiceProvider;
        if (sp == null) return;

        var entry = ev.Entry;
        var explorerVm = new ViewModels.Windows.VsaCodeExplorerViewModel(
            entry, entry.MeterStart, entry.Zeit);

        var dlg = new VsaCodeExplorerWindow(explorerVm, _vm.VideoPath, _vm.CurrentVideoTime) { Owner = this };
        if (dlg.ShowDialog() == true && dlg.SelectedEntry is not null)
        {
            var result = dlg.SelectedEntry;
            entry.Code = result.Code;
            entry.Beschreibung = result.Beschreibung;
            entry.CodeMeta = result.CodeMeta;
            entry.MeterStart = result.MeterStart;
            entry.MeterEnd = result.MeterEnd;
            entry.Zeit = result.Zeit;
            entry.IsStreckenschaden = result.IsStreckenschaden;
            entry.FotoPaths = result.FotoPaths;
            _sessionService.UpdateEvent(ev.EventId, entry, ev.Overlay);

            // Als bearbeitet markieren
            _vm.EditDefectCommand.Execute(null);
            LstEvents.Items.Refresh();
            UpdateDefectDetailPanel(ev);
            UpdateEventMarkers();
        }
    }

    private void BtnRejectDefect_Click(object sender, RoutedEventArgs e)
    {
        _vm.RejectDefectCommand.Execute(null);
        if (_vm.SelectedDefect != null)
        {
            UpdateDefectDetailPanel(_vm.SelectedDefect);
            LstEvents.Items.Refresh();
            UpdateEventMarkers();
        }
    }

    /// <summary>Defekt-Detail-Panel mit Werten des ausgewaehlten Events befuellen.</summary>
    private void UpdateDefectDetailPanel(CodingEvent ev)
    {
        DefectDetailPanel.Visibility = Visibility.Visible;

        TxtDetailCode.Text = ev.Entry.Code;
        TxtDetailDescription.Text = ev.Entry.Beschreibung;
        TxtDetailDistance.Text = $"{ev.MeterAtCapture:F2}m";

        // Uhrposition
        TxtDetailClock.Text = ev.Overlay?.ClockFrom != null
            ? $"{ev.Overlay.ClockFrom:F0}h"
            : "–";

        // Schweregrad
        if (ev.Entry.CodeMeta?.Parameters != null &&
            ev.Entry.CodeMeta.Parameters.TryGetValue("vsa.schweregrad", out var sev))
            TxtDetailSeverity.Text = sev;
        else
            TxtDetailSeverity.Text = "–";

        // Konfidenz + Farbe
        if (ev.AiContext != null)
        {
            double conf = ev.AiContext.Confidence;
            TxtDetailConfidence.Text = $"{conf * 100:F0}%";
            TxtDetailConfidence.Foreground = CodingSessionViewModel.GetConfidenceBrush(conf);
            DefectDetailBorderBrush.Color = ((SolidColorBrush)CodingSessionViewModel.GetZoneBrush(conf)).Color;
        }
        else
        {
            TxtDetailConfidence.Text = "–";
            TxtDetailConfidence.Foreground = new SolidColorBrush(Color.FromRgb(0x94, 0xA3, 0xB8));
            DefectDetailBorderBrush.Color = Color.FromRgb(0x3B, 0x82, 0xF6);
        }

        // Status
        var status = CodingSessionViewModel.GetDefectStatus(ev);
        TxtDetailStatus.Text = $"Status: {StatusToDisplayText(status)}";

        // Aktionsbuttons nur bei offenen KI-Events
        DefectActionGrid.Visibility = ev.AiContext != null &&
            status is DefectStatus.Pending or DefectStatus.ReviewRequired
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private static string StatusToDisplayText(DefectStatus status) => status switch
    {
        DefectStatus.AutoAccepted     => "Auto-Akzeptiert (Green Zone)",
        DefectStatus.Pending          => "Review empfohlen (Yellow Zone)",
        DefectStatus.ReviewRequired   => "Manuell erforderlich (Red Zone)",
        DefectStatus.Accepted         => "Akzeptiert",
        DefectStatus.AcceptedWithEdit => "Bearbeitet",
        DefectStatus.Rejected         => "Abgelehnt",
        _ => ""
    };

    /// <summary>Ereignisliste nach Meter aufsteigend sortieren.</summary>
    private void ResortEventsByMeter()
    {
        if (_vm == null) return;

        var sorted = _vm.Events
            .OrderBy(e => e.MeterAtCapture)
            .ThenBy(e => e.VideoTimestamp)
            .ToList();

        var selected = LstEvents.SelectedItem;
        _vm.Events.Clear();
        foreach (var ev in sorted)
            _vm.Events.Add(ev);

        LstEvents.ItemsSource = null;
        LstEvents.ItemsSource = _vm.Events;
        if (selected != null)
            LstEvents.SelectedItem = selected;
    }

    /// <summary>Zone-Dots und Konfidenz-Texte in der Event-ListBox einfaerben.</summary>
    private void ColorizeEventListItems()
    {
        for (int i = 0; i < LstEvents.Items.Count; i++)
        {
            if (LstEvents.ItemContainerGenerator.ContainerFromIndex(i) is not ListBoxItem container) continue;
            if (LstEvents.Items[i] is not CodingEvent ev) continue;

            // Zone-Dot finden und einfaerben
            var zoneDot = FindChild<Ellipse>(container, "ZoneDot");
            if (zoneDot != null)
            {
                if (ev.AiContext != null)
                    zoneDot.Fill = CodingSessionViewModel.GetConfidenceBrush(ev.AiContext.Confidence);
                else
                    zoneDot.Fill = new SolidColorBrush(Color.FromRgb(0x3B, 0x82, 0xF6)); // Manuell = blau
            }

            // Konfidenz-Text finden und einfaerben
            var confText = FindChild<TextBlock>(container, "TxtConfidence");
            if (confText != null && ev.AiContext != null)
            {
                confText.Text = $"{ev.AiContext.Confidence * 100:F0}%";
                confText.Foreground = CodingSessionViewModel.GetConfidenceBrush(ev.AiContext.Confidence);
            }
            else if (confText != null)
            {
                confText.Text = "";
            }

            // Status-Icon
            var statusIcon = FindChild<TextBlock>(container, "TxtStatusIcon");
            if (statusIcon != null)
            {
                var status = CodingSessionViewModel.GetDefectStatus(ev);
                statusIcon.Text = status switch
                {
                    DefectStatus.AutoAccepted     => "\u2713",
                    DefectStatus.Accepted         => "\u2713",
                    DefectStatus.AcceptedWithEdit  => "\u270E",
                    DefectStatus.Pending           => "\u23F3",
                    DefectStatus.ReviewRequired    => "\u26A0",
                    DefectStatus.Rejected          => "\u2717",
                    _ => ""
                };
                statusIcon.Foreground = CodingSessionViewModel.GetStatusBrush(status);
            }
        }
    }

    /// <summary>Rekursiv ein benanntes Kind-Element im VisualTree finden.</summary>
    private static T? FindChild<T>(DependencyObject parent, string childName) where T : FrameworkElement
    {
        int count = VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T t && t.Name == childName)
                return t;
            var found = FindChild<T>(child, childName);
            if (found != null) return found;
        }
        return null;
    }

    /// <summary>Statistiken + Zaehlungen im Seitenpanel aktualisieren.</summary>
    private void UpdateStatistics()
    {
        RunDefectCount.Text = _vm.EventCount.ToString();

        int openCount = _vm.Events.Count(e =>
            e.AiContext != null &&
            CodingSessionViewModel.GetDefectStatus(e) is DefectStatus.Pending or DefectStatus.ReviewRequired);
        RunOpenCount.Text = openCount.ToString();

        TxtStatAutoAccepted.Text = _vm.StatAutoAccepted.ToString();
        TxtStatPending.Text = _vm.StatPending.ToString();
        TxtStatReviewRequired.Text = _vm.StatReviewRequired.ToString();
        TxtStatAvgConfidence.Text = _vm.StatAverageConfidence > 0
            ? $"{_vm.StatAverageConfidence * 100:F0}%"
            : "–";
    }

    // --- Session abgeschlossen → Protokoll zurueckgeben ---

    private async void OnSessionCompleted(object? sender, ProtocolDocument doc)
    {
        // Fotos fuer Rohranfang/Rohrende generieren
        var holdingDir = _haltung?.GetFieldValue("Link");
        var videoPath = _vm.VideoPath;
        if (!string.IsNullOrWhiteSpace(holdingDir) && System.IO.Directory.Exists(holdingDir)
            && !string.IsNullOrWhiteSpace(videoPath))
        {
            double endMeter = _sessionService.EndMeter;
            var boundaries = ProtocolBoundaryService.EnsureBoundaries(doc.Current.Entries, endMeter);
            try
            {
                await BoundaryPhotoService.GenerateBoundaryPhotosAsync(
                    boundaries, videoPath, holdingDir).ConfigureAwait(false);
            }
            catch { /* Foto-Fehler sollen Session nicht blockieren */ }
        }

        Dispatcher.Invoke(() =>
        {
            CompletedProtocol = doc;
            DialogResult = true;
            Close();
        });
    }

    /// <summary>Fertiges Protokoll nach Abschluss (fuer aufrufendes Fenster).</summary>
    public ProtocolDocument? CompletedProtocol { get; private set; }

    // ═══════════════════════════════════════════════════════════
    // KI-Overlay: Live-Analyse des aktuellen Frames
    // ═══════════════════════════════════════════════════════════

    private void InitAiOverlay()
    {
        try
        {
            _aiConfig = AiRuntimeConfig.Load();
            if (!_aiConfig.Enabled)
            {
                SetAiStatus("KI deaktiviert", "#94A3B8");
                BtnAnalyzeFrame.IsEnabled = false;
                return;
            }

            _ollamaClient = _aiConfig.CreateOllamaClient();
            _liveDetection = new LiveDetectionService(_ollamaClient, _aiConfig.VisionModel);

            TxtAiModel.Text = _aiConfig.VisionModel;
            SetAiStatus("Bereit", "#22C55E");
        }
        catch (Exception ex)
        {
            SetAiStatus($"Fehler: {ex.Message}", "#EF4444");
            BtnAnalyzeFrame.IsEnabled = false;
        }
    }

    private void SetAiStatus(string text, string dotColorHex)
    {
        TxtAiStatus.Text = text;
        var color = (Color)ColorConverter.ConvertFromString(dotColorHex);
        AiStatusDot.Fill = new SolidColorBrush(color);
    }

    private async void BtnAnalyzeFrame_Click(object sender, RoutedEventArgs e)
    {
        if (_liveDetection == null || _player == null || _isAnalyzing) return;
        await AnalyzeCurrentFrameAsync();
    }

    private async Task AnalyzeCurrentFrameAsync()
    {
        if (_liveDetection == null || _player == null) return;
        if (_isAnalyzing) return;
        _isAnalyzing = true;

        _analysisCts?.Cancel();
        _analysisCts = new CancellationTokenSource();

        try
        {
            BtnAnalyzeFrame.IsEnabled = false;
            TxtAnalyzeButton.Text = "Analysiere...";
            SetAiStatus("Frame wird analysiert...", "#F59E0B");

            // Frame aus VLC als PNG extrahieren
            var pngBytes = await CaptureCurrentFrameAsync();
            if (pngBytes == null || pngBytes.Length == 0)
            {
                SetAiStatus("Frame konnte nicht extrahiert werden", "#EF4444");
                return;
            }

            var timestampSec = _player.Time / 1000.0;
            var result = await _liveDetection.AnalyzeFrameAsync(
                pngBytes, timestampSec, _analysisCts.Token);

            await Dispatcher.InvokeAsync(() => ShowAiResults(result));
        }
        catch (OperationCanceledException)
        {
            SetAiStatus("Analyse abgebrochen", "#94A3B8");
        }
        catch (Exception ex)
        {
            SetAiStatus($"Fehler: {ex.Message}", "#EF4444");
        }
        finally
        {
            _isAnalyzing = false;
            Dispatcher.Invoke(() =>
            {
                BtnAnalyzeFrame.IsEnabled = true;
                TxtAnalyzeButton.Text = "Frame analysieren";
            });
        }
    }

    private void ShowAiResults(LiveDetection result)
    {
        if (result.Error != null)
        {
            SetAiStatus($"Fehler: {result.Error}", "#EF4444");
            AiFindingsList.ItemsSource = null;
            return;
        }

        if (result.Findings.Count == 0)
        {
            SetAiStatus("Kein Schaden erkannt", "#22C55E");
            AiFindingsList.ItemsSource = null;
            return;
        }

        SetAiStatus($"{result.Findings.Count} Befund(e) erkannt", "#22C55E");

        // Meterstand aus KI aktualisieren (informativ)
        if (result.MeterReading.HasValue)
            TxtAiStatus.Text += $"  |  Meter: {result.MeterReading:F2}m";

        // Findings in UI-Objekte konvertieren
        var items = result.Findings.Select(f => new AiFindingDisplayItem(f)).ToList();
        AiFindingsList.ItemsSource = items;
    }

    /// <summary>
    /// Aktuellen Videoframe als PNG extrahieren (ueber VLC Snapshot).
    /// </summary>
    private async Task<byte[]?> CaptureCurrentFrameAsync()
    {
        // Snapshot geht auch bei pausiertem Video, solange Dauer bekannt
        if (_player == null || !_videoReady)
            return null;

        // VLC-Snapshot in temporaere Datei
        var tmpDir = System.IO.Path.GetTempPath();
        var snapFile = System.IO.Path.Combine(tmpDir, $"sewerstudio_snap_{Guid.NewGuid():N}.png");

        try
        {
            // VLC TakeSnapshot: (streamIndex, path, width, height) → 0 = auto
            _player.TakeSnapshot(0, snapFile, 0, 0);

            // Warten bis Datei geschrieben
            for (int i = 0; i < 20; i++)
            {
                await Task.Delay(50);
                if (System.IO.File.Exists(snapFile) &&
                    new System.IO.FileInfo(snapFile).Length > 100)
                    break;
            }

            if (!System.IO.File.Exists(snapFile))
                return null;

            return await System.IO.File.ReadAllBytesAsync(snapFile);
        }
        finally
        {
            try { if (System.IO.File.Exists(snapFile)) System.IO.File.Delete(snapFile); }
            catch { /* cleanup best-effort */ }
        }
    }

}

/// <summary>
/// Display-Objekt fuer eine einzelne KI-Erkennung im Overlay.
/// </summary>
public sealed class AiFindingDisplayItem
{
    public AiFindingDisplayItem(LiveFrameFinding f)
    {
        Label = f.Label;
        VsaCode = f.VsaCodeHint ?? "";
        Severity = f.Severity;
        SeverityText = f.Severity.ToString();

        // Detail-Text: Uhr + Umfang + Masse
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(f.PositionClock))
            parts.Add($"Uhr {f.PositionClock}");
        if (f.ExtentPercent.HasValue)
            parts.Add($"Umfang {f.ExtentPercent}%");
        if (f.HeightMm is > 0)
            parts.Add($"H:{f.HeightMm}mm");
        if (f.WidthMm is > 0)
            parts.Add($"B:{f.WidthMm}mm");
        if (f.IntrusionPercent is > 0)
            parts.Add($"Einragung {f.IntrusionPercent}%");
        DetailText = parts.Count > 0 ? string.Join("  |  ", parts) : "Keine Details";

        SeverityBrush = new SolidColorBrush(f.Severity switch
        {
            5 => Color.FromRgb(0xEF, 0x44, 0x44), // Rot (kritisch)
            4 => Color.FromRgb(0xF9, 0x73, 0x16), // Orange (schwer)
            3 => Color.FromRgb(0xF5, 0x9E, 0x0B), // Gelb (mittel)
            2 => Color.FromRgb(0x22, 0xC5, 0x5E), // Gruen (leicht)
            _ => Color.FromRgb(0x94, 0xA3, 0xB8)  // Grau (kaum)
        });
    }

    public string Label { get; }
    public string VsaCode { get; }
    public int Severity { get; }
    public string SeverityText { get; }
    public string DetailText { get; }
    public SolidColorBrush SeverityBrush { get; }
}
