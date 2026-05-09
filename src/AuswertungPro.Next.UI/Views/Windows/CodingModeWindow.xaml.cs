using System;
using AuswertungPro.Next.Application.Ai.Vision;
using AuswertungPro.Next.Domain.Ai.Vision;
using AuswertungPro.Next.Infrastructure.Ai.Ollama;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
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
using AuswertungPro.Next.Application.Ai.Pipeline;
using AuswertungPro.Next.Application.Ai.Teacher;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;
using AuswertungPro.Next.Infrastructure.Ai;

namespace AuswertungPro.Next.UI.Views.Windows;

/// <summary>
/// Codier-Modus: Durchlauf einer Haltung von 0.00m bis Ende mit Overlay-Werkzeugen.
/// </summary>
public partial class CodingModeWindow : Window
{
    private readonly CodingSessionViewModel _vm;
    private readonly ICodingSessionService _sessionService;
    private readonly IOverlayToolService _overlayService;
    private readonly IDialogService _dialogs = App.Resolve<IDialogService>();

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
    private const string OverlayTagPreview = "overlay_preview";
    private const string OverlayTagManual = "overlay_manual";
    private const string OverlayTagCalibration = "overlay_calibration";

    // Kalibrierungsmodus
    private bool _isCalibrating;
    private NormalizedPoint? _calibStart;

    // KI Live-Analyse
    private LiveDetectionService? _liveDetection;
    private EnhancedVisionAnalysisService? _enhancedVision;
    private AiRuntimeConfig? _aiConfig;
    private OllamaClient? _ollamaClient;
    private CancellationTokenSource? _analysisCts;
    private bool _isAnalyzing;
    private List<AiOverlay>? _currentAiOverlays;
    private double? _lastAiMeterReading;
    private double _lastAiMeterTimestampSec = double.NaN;
    private string _aiModelName = string.Empty;
    private bool _aiStatusPulseRunning;

    // SAM-Segmentierung nach BBox (Audit-Fix 2026-04: bisher nur PlayerWindow)
    // Wird beim Loaded() initialisiert und beim BBox-MouseUp aufgerufen.
    private AuswertungPro.Next.Infrastructure.Ai.Pipeline.VisionPipelineClient? _sidecarClient;
    private AuswertungPro.Next.Application.Ai.Pipeline.SamResponse? _lastSamResult;
    /// <summary>Tight-BBox aus letzter SAM-Maske (in normierten Koordinaten 0..1).
    /// Wird beim Trainings-Export verwendet statt der grossen User-BBox.</summary>
    private Application.Ai.NormalizedBoundingBox? _lastSamTightBbox;

    public CodingModeWindow(HaltungRecord haltung, string? videoPath)
    {
        InitializeComponent();

        _haltung = haltung;
        _sessionService = new CodingSessionService();
        _overlayService = new OverlayToolService();
        _vm = new CodingSessionViewModel(_sessionService, _overlayService);
        _vm.VideoPath = videoPath;

        DataContext = _vm;

        // UI-Updates bei ViewModel-Aenderungen (benannte Handler fuer Cleanup)
        _vm.PropertyChanged += Vm_PropertyChanged;
        _vm.SessionCompleted += OnSessionCompleted;

        // Events-Liste binden
        LstEvents.ItemsSource = _vm.Events;

        // Event-Items nach Laden einfaerben (Zone-Dot, Konfidenz, Status) + Zaehlung aktualisieren
        _vm.Events.CollectionChanged += VmEvents_CollectionChanged;

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

        // Audit-Fix 2026-04: SAM-Segmentierung nach BBox-Zeichnung. Sidecar-Client lazy
        // initialisieren - bei Sidecar-Down faellt der Codiermodus auf Rechteck-only zurueck.
        try
        {
            var sidecarUrl = Environment.GetEnvironmentVariable("SEWERSTUDIO_SIDECAR_URL")
                ?? "http://localhost:8100";
            _sidecarClient = new AuswertungPro.Next.Infrastructure.Ai.Pipeline.VisionPipelineClient(new Uri(sidecarUrl));
            // Pre-Flight: sofort einen Health-Check senden, damit der User erfaehrt
            // ob der Sidecar erreichbar ist - vorher: stille Faulheit bis zum ersten BBox.
            _ = Task.Run(async () =>
            {
                try
                {
                    using var healthCts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                    var hc = await _sidecarClient.HealthCheckAsync(healthCts.Token);
                    SetStatusSafe(hc != null
                        ? $"Sidecar OK (status={hc.Status}, YOLO/DINO/SAM erreichbar)"
                        : $"Sidecar nicht bereit: {_sidecarClient.LastHealthError ?? "keine Antwort von /health"}");
                }
                catch (OperationCanceledException)
                {
                    SetStatusSafe("Sidecar-Health-Timeout: /health antwortet nicht innerhalb von 3s");
                }
                catch (Exception ex)
                {
                    SetStatusSafe($"Sidecar-Health-Fehler: {ex.Message}");
                }
            });
        }
        catch (Exception ex)
        {
            SetStatusSafe($"Sidecar-Init fehlgeschlagen: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"[CodingMode SAM] Sidecar-Client-Init fehlgeschlagen: {ex.Message}");
        }

        // Tastenkuerzel: O = Akzeptieren, Delete = Verwerfen (auf selektierten Befund)
        PreviewKeyDown += CodingMode_PreviewKeyDown;

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
        // Audit R-C2 2026-04-25: Jeder Cleanup-Schritt einzeln in try/catch.
        // Vorher konnte z.B. eine LibVLC-AccessViolation oder ein bereits
        // disposed _analysisCts den Handler abbrechen — die Exception wurde
        // nach aussen propagiert und konnte die App killen.
        void Safe(string step, Action a)
        {
            try { a(); }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CodingModeWindow.OnClosing] {step}: {ex.GetType().Name}: {ex.Message}");
            }
        }

        Safe("VM-Unsubscribe", () =>
        {
            _vm.PropertyChanged -= Vm_PropertyChanged;
            _vm.Events.CollectionChanged -= VmEvents_CollectionChanged;
            _vm.SessionCompleted -= OnSessionCompleted;
        });
        Safe("AnalysisCts-Cancel", () => _analysisCts?.Cancel());
        Safe("AnalysisCts-Dispose", () => _analysisCts?.Dispose());
        Safe("AiStatusPulse-Stop", () => StopAiStatusPulse());
        Safe("PipelineFailure-Unsubscribe", () =>
            AuswertungPro.Next.Infrastructure.Ai.EnhancedVisionAnalysisService.PipelineFailure -= OnPipelineFailure);
        Safe("OllamaClient-Dispose", () => _ollamaClient?.Dispose());
        Safe("Player-Stop", () => _player?.Stop());
        Safe("Player-Dispose", () => _player?.Dispose());
        Safe("LibVlc-Dispose", () => _libVlc?.Dispose());
        Safe("Vm-Dispose", () => _vm.Dispose());
    }

    // Benannte Event-Handler (fuer sauberes Cleanup via -=)
    private void Vm_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        => Dispatcher.InvokeAsync(() => UpdateUi(e.PropertyName));

    private void VmEvents_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        => Dispatcher.InvokeAsync(() => { ColorizeEventListItems(); UpdateStatistics(); });

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

        // Interaktive Zwischenzustaende (Multi-Punkt) klar anzeigen
        if (overlay.ToolType == OverlayToolType.PipeBend && !overlay.ArcDegrees.HasValue)
        {
            TxtOverlayQ1.Text = "Q1: –";
            TxtOverlayQ2.Text = "Q2: –";
            TxtOverlayClock.Text = "Uhr: –";
            TxtOverlayArc.Text = "Winkel: –";
            MeasurementPanel.Visibility = Visibility.Visible;
            TxtMeasurement.Text = overlay.Points.Count switch
            {
                <= 2 => "PipeBend: Achse 1 setzen (P1 -> P2)",
                3 => "PipeBend: Achse 2 vervollstaendigen (P4 setzen)",
                _ => "PipeBend: Punkte setzen"
            };
            return;
        }

        if (overlay.ToolType == OverlayToolType.LateralCircle && !overlay.Q1Mm.HasValue)
        {
            TxtOverlayQ1.Text = "Q1: –";
            TxtOverlayQ2.Text = "Q2: –";
            TxtOverlayClock.Text = "Uhr: –";
            TxtOverlayArc.Text = "Bogen: –";
            MeasurementPanel.Visibility = Visibility.Visible;
            TxtMeasurement.Text = overlay.Points.Count switch
            {
                <= 2 => "Anschluss: 3 Randpunkte setzen",
                _ => "Anschluss: Punkte setzen"
            };
            return;
        }

        TxtOverlayQ1.Text = overlay.Q1Mm.HasValue ? $"Q1: {overlay.Q1Mm:F1} mm" : "Q1: –";
        TxtOverlayQ2.Text = overlay.Q2Mm.HasValue ? $"Q2: {overlay.Q2Mm:F1} mm" : "Q2: –";
        TxtOverlayClock.Text = overlay.ClockFrom.HasValue
            ? $"Uhr: {overlay.ClockFrom:F1}" + (overlay.ClockTo.HasValue ? $" → {overlay.ClockTo:F1}" : "")
            : "Uhr: –";
        TxtOverlayArc.Text = overlay.ArcDegrees.HasValue
            ? (overlay.ToolType == OverlayToolType.PipeBend
                ? $"Winkel: {overlay.ArcDegrees:F1}°" + (_overlayService.PipeBendSnapEnabled ? " (Snap)" : "")
                : $"Bogen: {overlay.ArcDegrees:F0}°")
            : "Bogen: –";

        // Kompakte Anzeige im Video
        MeasurementPanel.Visibility = Visibility.Visible;
        var parts = new System.Collections.Generic.List<string>();
        if (overlay.ToolType == OverlayToolType.Level)
        {
            if (overlay.FillPercent.HasValue)
                parts.Add($"{overlay.FillPercent:F1}%");
            if (overlay.LevelSubMode.HasValue)
                parts.Add(overlay.LevelSubMode.Value switch
                {
                    LevelMode.Water => "Wasser",
                    LevelMode.Obstacle => "Hindernis",
                    _ => "Ablagerung"
                });
        }
        else
        {
            if (overlay.Q1Mm.HasValue) parts.Add($"Q1:{overlay.Q1Mm:F1}mm");
            if (overlay.ClockFrom.HasValue) parts.Add($"Uhr:{overlay.ClockFrom:F1}");
            if (overlay.ArcDegrees.HasValue) parts.Add($"{overlay.ArcDegrees:F0}°");
            if (overlay.DnRatioPercent.HasValue) parts.Add($"{overlay.DnRatioPercent:F0}%");
            if (overlay.ToolType == OverlayToolType.PipeBend && _overlayService.PipeBendSnapEnabled)
                parts.Add("Snap");
        }
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

    /// <summary>Aktuellen Meter aus der Videoposition schaetzen (Zeitanteil * Haltungslaenge).</summary>
    private double EstimateMeterFromVideoPosition()
    {
        if (_player != null && _videoDurationMs > 0 && _vm.EndMeter > 0)
        {
            double fraction = Math.Clamp(_player.Time / (double)_videoDurationMs, 0, 1);
            return Math.Round(fraction * _vm.EndMeter, 2);
        }
        return Math.Round(_vm.CurrentMeter, 2);
    }

    /// <summary>
    /// Meterquelle fuer neue Events:
    /// 1) frische OSD-KI-Lesung (falls vorhanden), sonst
    /// 2) Zeit-basierte Schaetzung aus aktueller Videoposition.
    /// </summary>
    private double ResolveCaptureMeter()
    {
        var estimated = EstimateMeterFromVideoPosition();
        if (_lastAiMeterReading.HasValue && _player != null)
        {
            double nowSec = _player.Time / 1000.0;
            bool aiFresh = !double.IsNaN(_lastAiMeterTimestampSec)
                           && Math.Abs(nowSec - _lastAiMeterTimestampSec) <= 2.0;
            bool aiInRange = _lastAiMeterReading.Value >= 0
                             && (_vm.EndMeter <= 0 || _lastAiMeterReading.Value <= _vm.EndMeter + 0.5);
            if (aiFresh && aiInRange)
                return Math.Round(_lastAiMeterReading.Value, 2);
        }

        return estimated;
    }

    /// <summary>
    /// Session-Meter vor Aktionen auf die aktuelle Video-Position synchronisieren,
    /// damit gespeicherte Event-Distanzen mit dem sichtbaren Frame uebereinstimmen.
    /// </summary>
    private double SyncSessionMeterFromVideo()
    {
        double meter = ResolveCaptureMeter();
        _sessionService.MoveToMeter(meter);
        return meter;
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

        // Multi-Punkt-Werkzeuge (PipeBend/LateralCircle): Punktweise klicken, kein Drag.
        if (_overlayService.IsMultiPointTool)
        {
            if (_overlayService.DrawPointCount == 0)
            {
                _vm.CurrentOverlay = null;
                BtnCreateEvent.IsEnabled = false;
                BtnSaveAsTraining.IsEnabled = false;
                UpdateOverlayInfo(null);
            }

            bool complete = _vm.OnCanvasMultiPointClick(normalized);
            ClearAllDrawingShapes();

            if (_vm.CurrentOverlay != null)
            {
                RenderOverlayGeometry(_vm.CurrentOverlay);
                UpdateOverlayInfo(_vm.CurrentOverlay);
                if (complete)
                {
                    BtnCreateEvent.IsEnabled = true;
                BtnSaveAsTraining.IsEnabled = true;
                }
                else
                {
                    BtnCreateEvent.IsEnabled = false;
                BtnSaveAsTraining.IsEnabled = false;
                }
            }

            return;
        }

        _vm.OnCanvasMouseDown(normalized);
        OverlayCanvas.CaptureMouse();
        ClearAllDrawingShapes();
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
                StrokeDashArray = new DoubleCollection { 6, 3 },
                Tag = OverlayTagPreview
            };
            OverlayCanvas.Children.Add(_previewLine);

            // Pixel-Laenge anzeigen
            double pxLen = Math.Sqrt(Math.Pow(p2.X - p1.X, 2) + Math.Pow(p2.Y - p1.Y, 2));
            TxtCalibrationHint.Text = $"Referenzlinie: {pxLen:F0} px";
            return;
        }

        // Multi-Punkt-Vorschau zwischen den Klicks
        if (_overlayService.IsMultiPointTool && _overlayService.DrawPointCount > 0)
        {
            _vm.OnCanvasMultiPointMove(normalized);
            ClearAllDrawingShapes();
            if (_vm.CurrentOverlay != null)
            {
                RenderOverlayGeometry(_vm.CurrentOverlay);
                UpdateOverlayInfo(_vm.CurrentOverlay);
            }
            return;
        }

        if (!_overlayService.IsDrawing) return;

        _vm.OnCanvasMouseMove(normalized);

        // Vorschau-Shape zeichnen
        RenderPreview(_overlayService.DrawStartPoint, normalized);
        UpdateOverlayInfo(_vm.CurrentOverlay);
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

        if (_overlayService.IsMultiPointTool) return;
        if (!_overlayService.IsDrawing) return;

        _vm.OnCanvasMouseUp(normalized);
        OverlayCanvas.ReleaseMouseCapture();

        // Finale Geometrie rendern (verschwindet nach 3s automatisch)
        if (_vm.CurrentOverlay != null)
        {
            ClearPreviewShapes();
            RenderOverlayGeometry(_vm.CurrentOverlay);
            UpdateOverlayInfo(_vm.CurrentOverlay);
            BtnCreateEvent.IsEnabled = true;
                BtnSaveAsTraining.IsEnabled = true;

            // Audit-Fix 2026-04: SAM-Segmentierung im Hintergrund starten (nur fuer Rechtecke).
            // Maske wird ueber das Rechteck gerendert und liefert tight-BBox fuer Trainings-Export.
            if (_vm.CurrentOverlay.ToolType == OverlayToolType.Rectangle)
            {
                // KRITISCH: Video pausieren - sonst ueberschreibt das laufende VLC-HwndHost
                // das WPF-Overlay sofort (Airspace-Problem). Im Bildtraining funktioniert die
                // Segmentierung weil dort statische Bilder ueber <Image> gezeigt werden.
                try
                {
                    if (_player != null && _player.IsPlaying)
                    {
                        _player.SetPause(true);
                        SetStatusSafe("Video pausiert fuer SAM-Segmentierung...");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[CodingMode] Pause-Fehler: {ex.Message}");
                }

                SetStatusSafe("BBox erkannt - SAM wird gestartet...");
                _ = SegmentBboxWithSamAsync(_vm.CurrentOverlay);
            }
            else
            {
                SetStatusSafe($"BBox-Tool: {_vm.CurrentOverlay.ToolType} (SAM nur fuer Rechteck)");
            }

            // Overlay nach 8 Sekunden automatisch ausblenden (vorher 3s - zu kurz fuer SAM,
            // der Async-Lauf braucht ~300ms und der User muss die Maske auch sehen koennen).
            // SAM-Maske (Tag "sam_manual_mask") bleibt durch ClearAllDrawingShapes unberuehrt.
            var fadeTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(8)
            };
            fadeTimer.Tick += (_, _) =>
            {
                fadeTimer.Stop();
                ClearAllDrawingShapes();
            };
            fadeTimer.Start();
        }
    }

    // --- Kalibrierung ---

    private void BtnCalibrate_Checked(object sender, RoutedEventArgs e)
    {
        _isCalibrating = true;
        _calibStart = null;
        CalibrationHint.Visibility = Visibility.Visible;
        TxtCalibrationHint.Text = "Linie ueber den sichtbaren Rohrdurchmesser zeichnen";
        ClearAllDrawingShapes(includeCalibration: true);

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
            PipeCenter = center,
            WasManuallyCalibrated = true
        };
        _overlayService.SetCalibration(cal);

        // Session-Kalibrierung auch setzen
        if (_sessionService.ActiveSession != null)
            _sessionService.ActiveSession.Calibration = cal;

        // UI aktualisieren
        double mmPerNorm = cal.MmPerNormUnit;
        TxtCalibrationStatus.Text = $"Kalibriert: {mmPerNorm:F1} mm/norm";
        TxtCalibrationHint.Text = $"Kalibriert! DN {dn}mm = {pixelDiameter:F0}px";

        // Referenzlinie kurz anzeigen (2s), dann automatisch ausblenden
        ClearPreviewShapes();
        ClearCalibrationReferenceShapes();
        var refLine = new Line
        {
            X1 = p1.X, Y1 = p1.Y,
            X2 = p2.X, Y2 = p2.Y,
            Stroke = Brushes.Magenta,
            StrokeThickness = 1.5,
            Opacity = 0.6,
            Tag = OverlayTagCalibration
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
            Padding = new Thickness(4, 1, 4, 1),
            Tag = OverlayTagCalibration
        };
        Canvas.SetLeft(label, midPx.X + 6);
        Canvas.SetTop(label, midPx.Y - 8);
        OverlayCanvas.Children.Add(label);

        // Overlay nach 2 Sekunden automatisch ausblenden
        var fadeTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(2)
        };
        fadeTimer.Tick += (_, _) =>
        {
            fadeTimer.Stop();
            ClearCalibrationReferenceShapes();
        };
        fadeTimer.Start();

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
            case OverlayToolType.Ruler:
                _previewLine = new Line
                {
                    X1 = p1.X, Y1 = p1.Y,
                    X2 = p2.X, Y2 = p2.Y,
                    Stroke = Brushes.Lime,
                    StrokeThickness = 2,
                    StrokeDashArray = new DoubleCollection { 4, 2 },
                    Tag = OverlayTagPreview
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
                    Fill = new SolidColorBrush(Color.FromArgb(40, 0, 255, 255)),
                    Tag = OverlayTagPreview
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
                    StrokeDashArray = new DoubleCollection { 3, 2 },
                    Tag = OverlayTagPreview
                };
                var arcLine2 = new Line
                {
                    X1 = center.X, Y1 = center.Y,
                    X2 = p2.X, Y2 = p2.Y,
                    Stroke = Brushes.Yellow,
                    StrokeThickness = 1.5,
                    StrokeDashArray = new DoubleCollection { 3, 2 },
                    Tag = OverlayTagPreview
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
                    StrokeThickness = 2,
                    Tag = OverlayTagPreview
                };
                Canvas.SetLeft(_previewPoint, p1.X - 6);
                Canvas.SetTop(_previewPoint, p1.Y - 6);
                OverlayCanvas.Children.Add(_previewPoint);
                break;

            case OverlayToolType.Level:
                var levelStroke = _overlayService.ActiveLevelMode switch
                {
                    LevelMode.Water => Brushes.RoyalBlue,
                    LevelMode.Obstacle => Brushes.Crimson,
                    _ => Brushes.Chocolate
                };
                double levelY = p2.Y;

                // Fuellflaeche mit Rohr-Ellipsen-Clip (nicht ueber den Kreis hinaus)
                var prevCalib = _overlayService.Calibration;
                var prevCenter = prevCalib?.PipeCenter ?? new NormalizedPoint(0.5, 0.5);
                double prevR = (prevCalib?.NormalizedDiameter ?? 0.7) / 2.0;
                var prevCenterPx = NormalizedToPixel(prevCenter);
                double prevRxPx = prevR * OverlayCanvas.ActualWidth;
                double prevRyPx = prevR * OverlayCanvas.ActualHeight;

                double previewTop = _overlayService.ActiveLevelMode == LevelMode.Obstacle
                    ? (prevCenterPx.Y - prevRyPx) : levelY;
                double previewBottom = _overlayService.ActiveLevelMode == LevelMode.Obstacle
                    ? levelY : (prevCenterPx.Y + prevRyPx);
                var previewFill = new Rectangle
                {
                    Width = prevRxPx * 2,
                    Height = Math.Abs(previewBottom - previewTop),
                    Fill = new SolidColorBrush(Color.FromArgb(38,
                        ((SolidColorBrush)levelStroke).Color.R,
                        ((SolidColorBrush)levelStroke).Color.G,
                        ((SolidColorBrush)levelStroke).Color.B)),
                    Tag = OverlayTagPreview,
                    Clip = new EllipseGeometry(
                        new Point(prevRxPx, prevCenterPx.Y - Math.Min(previewTop, previewBottom)),
                        prevRxPx, prevRyPx)
                };
                Canvas.SetLeft(previewFill, prevCenterPx.X - prevRxPx);
                Canvas.SetTop(previewFill, Math.Min(previewTop, previewBottom));
                OverlayCanvas.Children.Add(previewFill);

                _previewLine = new Line
                {
                    X1 = p1.X,
                    Y1 = levelY,
                    X2 = p2.X,
                    Y2 = levelY,
                    Stroke = levelStroke,
                    StrokeThickness = 2.5,
                    StrokeDashArray = new DoubleCollection { 6, 3 },
                    Tag = OverlayTagPreview
                };
                OverlayCanvas.Children.Add(_previewLine);

                if (_vm.CurrentOverlay?.ToolType == OverlayToolType.Level)
                {
                    var mode = _overlayService.ActiveLevelMode switch
                    {
                        LevelMode.Water => "Wasser",
                        LevelMode.Obstacle => "Hindernis",
                        _ => "Ablagerung"
                    };
                    var text = _vm.CurrentOverlay.FillPercent.HasValue
                        ? $"{mode}: {_vm.CurrentOverlay.FillPercent:F1}%"
                        : $"{mode}: ...";
                    var label = new TextBlock
                    {
                        Text = text,
                        Foreground = Brushes.White,
                        FontSize = 11,
                        FontWeight = FontWeights.SemiBold,
                        Background = new SolidColorBrush(Color.FromArgb(180, 0, 0, 0)),
                        Padding = new Thickness(4, 2, 4, 2),
                        Tag = OverlayTagPreview
                    };
                    Canvas.SetLeft(label, (p1.X + p2.X) / 2 + 6);
                    Canvas.SetTop(label, levelY - 16);
                    OverlayCanvas.Children.Add(label);
                }
                break;

            case OverlayToolType.Ellipse:
                var ellipsePreview = new System.Windows.Shapes.Ellipse
                {
                    Width = Math.Abs(p2.X - p1.X),
                    Height = Math.Abs(p2.Y - p1.Y),
                    Stroke = Brushes.MediumPurple,
                    StrokeThickness = 2,
                    StrokeDashArray = new DoubleCollection { 4, 2 },
                    Fill = new SolidColorBrush(Color.FromArgb(30, 147, 112, 219)),
                    Tag = OverlayTagPreview
                };
                Canvas.SetLeft(ellipsePreview, Math.Min(p1.X, p2.X));
                Canvas.SetTop(ellipsePreview, Math.Min(p1.Y, p2.Y));
                OverlayCanvas.Children.Add(ellipsePreview);
                break;

            case OverlayToolType.Freehand:
                // Freihand-Vorschau: Polyline aus gesammelten Punkten
                var preview = _vm.CurrentOverlay;
                if (preview?.Points.Count >= 2)
                {
                    var polyline = new System.Windows.Shapes.Polyline
                    {
                        Stroke = Brushes.HotPink,
                        StrokeThickness = 2,
                        StrokeDashArray = new DoubleCollection { 3, 2 },
                        Tag = OverlayTagPreview
                    };
                    foreach (var pt in preview.Points)
                    {
                        var px = NormalizedToPixel(pt);
                        polyline.Points.Add(new Point(px.X, px.Y));
                    }
                    OverlayCanvas.Children.Add(polyline);
                }
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
            case OverlayToolType.Ruler:
            {
                if (geometry.Points.Count < 2) return;
                var p1 = NormalizedToPixel(geometry.Points[0]);
                var p2 = NormalizedToPixel(geometry.Points[1]);
                var line = new Line
                {
                    X1 = p1.X, Y1 = p1.Y,
                    X2 = p2.X, Y2 = p2.Y,
                    Stroke = geometry.ToolType == OverlayToolType.Stretch ? Brushes.Orange : Brushes.Lime,
                    StrokeThickness = 2.5,
                    Tag = OverlayTagManual
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
                        Padding = new Thickness(4, 2, 4, 2),
                        Tag = OverlayTagManual
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
                    Stroke = Brushes.Yellow, StrokeThickness = 2,
                    Tag = OverlayTagManual
                };
                var line2 = new Line
                {
                    X1 = center.X, Y1 = center.Y,
                    X2 = p2.X, Y2 = p2.Y,
                    Stroke = Brushes.Yellow, StrokeThickness = 2,
                    Tag = OverlayTagManual
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
                        Padding = new Thickness(4, 2, 4, 2),
                        Tag = OverlayTagManual
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
                    StrokeThickness = 2,
                    Tag = OverlayTagManual
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
                        Padding = new Thickness(4, 2, 4, 2),
                        Tag = OverlayTagManual
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
                    Fill = new SolidColorBrush(Color.FromArgb(30, 0, 255, 255)),
                    Tag = OverlayTagManual
                };
                Canvas.SetLeft(rect, Math.Min(p1.X, p3.X));
                Canvas.SetTop(rect, Math.Min(p1.Y, p3.Y));
                OverlayCanvas.Children.Add(rect);
                break;
            }

            case OverlayToolType.Level:
            {
                if (geometry.Points.Count < 2) return;
                var p1 = NormalizedToPixel(geometry.Points[0]);
                var p2 = NormalizedToPixel(geometry.Points[1]);
                var y = p1.Y;
                var stroke = geometry.LevelSubMode switch
                {
                    LevelMode.Water => Brushes.RoyalBlue,
                    LevelMode.Obstacle => Brushes.Crimson,
                    _ => Brushes.Chocolate
                };

                // Fuellstand-Linie (Sehne im Kreis)
                var line = new Line
                {
                    X1 = p1.X, Y1 = y,
                    X2 = p2.X, Y2 = y,
                    Stroke = stroke,
                    StrokeThickness = 2.5,
                    StrokeDashArray = new DoubleCollection { 6, 3 },
                    Tag = OverlayTagManual
                };
                OverlayCanvas.Children.Add(line);

                // Fuellflaeche als Rechteck mit Rohr-Ellipsen-Clip
                var calib = _overlayService.Calibration;
                var pipeCenter = calib?.PipeCenter ?? new NormalizedPoint(0.5, 0.5);
                double pipeR = (calib?.NormalizedDiameter ?? 0.7) / 2.0;
                var centerPx = NormalizedToPixel(pipeCenter);
                double rxPx = pipeR * OverlayCanvas.ActualWidth;
                double ryPx = pipeR * OverlayCanvas.ActualHeight;

                double top = geometry.LevelSubMode == LevelMode.Obstacle ? (centerPx.Y - ryPx) : y;
                double bottom = geometry.LevelSubMode == LevelMode.Obstacle ? y : (centerPx.Y + ryPx);
                var fill = new Rectangle
                {
                    Width = rxPx * 2,
                    Height = Math.Abs(bottom - top),
                    Fill = new SolidColorBrush(Color.FromArgb(45,
                        ((SolidColorBrush)stroke).Color.R,
                        ((SolidColorBrush)stroke).Color.G,
                        ((SolidColorBrush)stroke).Color.B)),
                    Tag = OverlayTagManual,
                    Clip = new EllipseGeometry(
                        new Point(rxPx, centerPx.Y - Math.Min(top, bottom)),
                        rxPx, ryPx)
                };
                Canvas.SetLeft(fill, centerPx.X - rxPx);
                Canvas.SetTop(fill, Math.Min(top, bottom));
                OverlayCanvas.Children.Add(fill);

                if (geometry.FillPercent.HasValue)
                {
                    var label = new TextBlock
                    {
                        Text = $"{geometry.FillPercent:F1}%",
                        Foreground = Brushes.White,
                        FontSize = 12,
                        FontWeight = FontWeights.Bold,
                        Background = new SolidColorBrush(Color.FromArgb(180, 0, 0, 0)),
                        Padding = new Thickness(4, 2, 4, 2),
                        Tag = OverlayTagManual
                    };
                    Canvas.SetLeft(label, (p1.X + p2.X) / 2 + 6);
                    Canvas.SetTop(label, y - 14);
                    OverlayCanvas.Children.Add(label);
                }
                break;
            }

            case OverlayToolType.PipeBend:
            {
                Point? a2Point = null;
                if (geometry.Points.Count >= 2)
                {
                    var a1 = NormalizedToPixel(geometry.Points[0]);
                    var a2 = NormalizedToPixel(geometry.Points[1]);
                    a2Point = a2;
                    var axis1 = new Line
                    {
                        X1 = a1.X, Y1 = a1.Y,
                        X2 = a2.X, Y2 = a2.Y,
                        Stroke = Brushes.Gold,
                        StrokeThickness = 2.5,
                        Tag = OverlayTagManual
                    };
                    OverlayCanvas.Children.Add(axis1);
                }

                if (geometry.Points.Count >= 4)
                {
                    var b1 = NormalizedToPixel(geometry.Points[2]);
                    var b2 = NormalizedToPixel(geometry.Points[3]);
                    var axis2 = new Line
                    {
                        X1 = b1.X, Y1 = b1.Y,
                        X2 = b2.X, Y2 = b2.Y,
                        Stroke = Brushes.Gold,
                        StrokeThickness = 2.5,
                        Tag = OverlayTagManual
                    };
                    OverlayCanvas.Children.Add(axis2);

                    if (geometry.ArcDegrees.HasValue && a2Point.HasValue)
                    {
                        var snapSuffix = _overlayService.PipeBendSnapEnabled ? " (Snap)" : "";
                        var label = new TextBlock
                        {
                            Text = $"{geometry.ArcDegrees:F1}°{snapSuffix}",
                            Foreground = Brushes.Gold,
                            FontSize = 12,
                            FontWeight = FontWeights.Bold,
                            Background = new SolidColorBrush(Color.FromArgb(180, 0, 0, 0)),
                            Padding = new Thickness(4, 2, 4, 2),
                            Tag = OverlayTagManual
                        };
                        Canvas.SetLeft(label, (a2Point.Value.X + b1.X) / 2 + 6);
                        Canvas.SetTop(label, (a2Point.Value.Y + b1.Y) / 2 - 12);
                        OverlayCanvas.Children.Add(label);
                    }
                }
                else if (geometry.Points.Count == 3)
                {
                    var p3 = NormalizedToPixel(geometry.Points[2]);
                    var marker = new Ellipse
                    {
                        Width = 8,
                        Height = 8,
                        Fill = Brushes.Gold,
                        Stroke = Brushes.Black,
                        StrokeThickness = 1,
                        Tag = OverlayTagManual
                    };
                    Canvas.SetLeft(marker, p3.X - 4);
                    Canvas.SetTop(marker, p3.Y - 4);
                    OverlayCanvas.Children.Add(marker);
                }
                break;
            }

            case OverlayToolType.LateralCircle:
            {
                if (geometry.Points.Count == 2)
                {
                    var p1 = NormalizedToPixel(geometry.Points[0]);
                    var p2 = NormalizedToPixel(geometry.Points[1]);
                    var line = new Line
                    {
                        X1 = p1.X, Y1 = p1.Y,
                        X2 = p2.X, Y2 = p2.Y,
                        Stroke = Brushes.Orange,
                        StrokeThickness = 2,
                        StrokeDashArray = new DoubleCollection { 4, 2 },
                        Tag = OverlayTagManual
                    };
                    OverlayCanvas.Children.Add(line);
                    break;
                }

                if (geometry.Points.Count >= 3 &&
                    TryComputeCircumcircle(
                        NormalizedToPixel(geometry.Points[0]),
                        NormalizedToPixel(geometry.Points[1]),
                        NormalizedToPixel(geometry.Points[2]),
                        out var center,
                        out var radius))
                {
                    var circle = new Ellipse
                    {
                        Width = radius * 2,
                        Height = radius * 2,
                        Stroke = Brushes.Orange,
                        StrokeThickness = 2.5,
                        Fill = new SolidColorBrush(Color.FromArgb(30, 255, 165, 0)),
                        Tag = OverlayTagManual
                    };
                    Canvas.SetLeft(circle, center.X - radius);
                    Canvas.SetTop(circle, center.Y - radius);
                    OverlayCanvas.Children.Add(circle);

                    if (geometry.Q1Mm.HasValue || geometry.DnRatioPercent.HasValue)
                    {
                        var parts = new List<string>();
                        if (geometry.Q1Mm.HasValue) parts.Add($"DN {geometry.Q1Mm:F0}mm");
                        if (geometry.DnRatioPercent.HasValue) parts.Add($"{geometry.DnRatioPercent:F0}%");

                        var label = new TextBlock
                        {
                            Text = string.Join(" | ", parts),
                            Foreground = Brushes.Orange,
                            FontSize = 12,
                            FontWeight = FontWeights.Bold,
                            Background = new SolidColorBrush(Color.FromArgb(180, 0, 0, 0)),
                            Padding = new Thickness(4, 2, 4, 2),
                            Tag = OverlayTagManual
                        };
                        Canvas.SetLeft(label, center.X + radius + 6);
                        Canvas.SetTop(label, center.Y - 8);
                        OverlayCanvas.Children.Add(label);
                    }
                }
                break;
            }

            case OverlayToolType.Ellipse:
            {
                if (geometry.Points.Count < 2) return;
                var p1 = NormalizedToPixel(geometry.Points[0]);
                var p3 = NormalizedToPixel(geometry.Points[1]);
                var ellipse = new System.Windows.Shapes.Ellipse
                {
                    Width = Math.Abs(p3.X - p1.X),
                    Height = Math.Abs(p3.Y - p1.Y),
                    Stroke = Brushes.MediumPurple,
                    StrokeThickness = 2.5,
                    Fill = new SolidColorBrush(Color.FromArgb(30, 147, 112, 219)),
                    Tag = OverlayTagManual
                };
                Canvas.SetLeft(ellipse, Math.Min(p1.X, p3.X));
                Canvas.SetTop(ellipse, Math.Min(p1.Y, p3.Y));
                OverlayCanvas.Children.Add(ellipse);

                // Dimensionen-Label
                if (geometry.Q1Mm.HasValue && geometry.Q2Mm.HasValue)
                {
                    var label = new TextBlock
                    {
                        Text = $"{geometry.Q2Mm:F0}×{geometry.Q1Mm:F0}mm",
                        Foreground = Brushes.MediumPurple,
                        FontSize = 12,
                        FontWeight = FontWeights.Bold,
                        Background = new SolidColorBrush(Color.FromArgb(180, 0, 0, 0)),
                        Padding = new Thickness(4, 2, 4, 2),
                        Tag = OverlayTagManual
                    };
                    Canvas.SetLeft(label, Math.Max(p1.X, p3.X) + 6);
                    Canvas.SetTop(label, (p1.Y + p3.Y) / 2 - 8);
                    OverlayCanvas.Children.Add(label);
                }
                break;
            }

            case OverlayToolType.Freehand:
            {
                if (geometry.Points.Count < 3) return;
                // Geschlossenes Polygon — umschliesst den Schadensbereich
                var polyline = new System.Windows.Shapes.Polygon
                {
                    Stroke = Brushes.HotPink,
                    StrokeThickness = 2.5,
                    StrokeLineJoin = PenLineJoin.Round,
                    Fill = new SolidColorBrush(Color.FromArgb(25, 255, 105, 180)),
                    Tag = OverlayTagManual
                };
                foreach (var pt in geometry.Points)
                {
                    var px = NormalizedToPixel(pt);
                    polyline.Points.Add(new Point(px.X, px.Y));
                }
                OverlayCanvas.Children.Add(polyline);

                // BoundingBox-Label
                if (geometry.Q1Mm.HasValue && geometry.Q2Mm.HasValue)
                {
                    double minX = double.MaxValue, minY = double.MaxValue;
                    double maxX = double.MinValue, maxY = double.MinValue;
                    foreach (var pt in geometry.Points)
                    {
                        var px = NormalizedToPixel(pt);
                        if (px.X < minX) minX = px.X;
                        if (px.Y < minY) minY = px.Y;
                        if (px.X > maxX) maxX = px.X;
                        if (px.Y > maxY) maxY = px.Y;
                    }
                    var label = new TextBlock
                    {
                        Text = $"{geometry.Q2Mm:F0}×{geometry.Q1Mm:F0}mm",
                        Foreground = Brushes.HotPink,
                        FontSize = 12,
                        FontWeight = FontWeights.Bold,
                        Background = new SolidColorBrush(Color.FromArgb(180, 0, 0, 0)),
                        Padding = new Thickness(4, 2, 4, 2),
                        Tag = OverlayTagManual
                    };
                    Canvas.SetLeft(label, maxX + 6);
                    Canvas.SetTop(label, (minY + maxY) / 2 - 8);
                    OverlayCanvas.Children.Add(label);
                }
                break;
            }
        }
    }

    private static bool TryComputeCircumcircle(Point p1, Point p2, Point p3, out Point center, out double radius)
    {
        center = default;
        radius = 0;

        double d = 2 * (p1.X * (p2.Y - p3.Y) + p2.X * (p3.Y - p1.Y) + p3.X * (p1.Y - p2.Y));
        if (Math.Abs(d) < 1e-9) return false;

        double p1Sq = p1.X * p1.X + p1.Y * p1.Y;
        double p2Sq = p2.X * p2.X + p2.Y * p2.Y;
        double p3Sq = p3.X * p3.X + p3.Y * p3.Y;

        double ux = (p1Sq * (p2.Y - p3.Y) + p2Sq * (p3.Y - p1.Y) + p3Sq * (p1.Y - p2.Y)) / d;
        double uy = (p1Sq * (p3.X - p2.X) + p2Sq * (p1.X - p3.X) + p3Sq * (p2.X - p1.X)) / d;

        center = new Point(ux, uy);
        radius = Math.Sqrt((p1.X - ux) * (p1.X - ux) + (p1.Y - uy) * (p1.Y - uy));
        return radius > 0;
    }

    private void ClearPreviewShapes()
    {
        RemoveOverlayElementsByTag(OverlayTagPreview);
        _previewLine = null;
        _previewPoint = null;
        _previewRect = null;
    }

    private void ClearManualShapes()
        => RemoveOverlayElementsByTag(OverlayTagManual);

    private void ClearCalibrationReferenceShapes()
        => RemoveOverlayElementsByTag(OverlayTagCalibration);

    private void ClearAllDrawingShapes(bool includeCalibration = false)
    {
        ClearPreviewShapes();
        ClearManualShapes();
        if (includeCalibration)
            ClearCalibrationReferenceShapes();
    }

    private void RemoveOverlayElementsByTag(params string[] tags)
    {
        if (tags.Length == 0) return;

        var tagSet = new HashSet<string>(tags, StringComparer.Ordinal);
        var toRemove = OverlayCanvas.Children
            .OfType<FrameworkElement>()
            .Where(el => el.Tag is string tag && tagSet.Contains(tag))
            .ToList();

        foreach (var element in toRemove)
            OverlayCanvas.Children.Remove(element);
    }

    // --- Werkzeug-Buttons ---

    private IEnumerable<ToggleButton> ToolButtons()
    {
        yield return BtnToolRect;
        yield return BtnCalibrate;
    }

    private void ToolButton_Checked(object sender, RoutedEventArgs e)
    {
        // Falls Kalibrierung aktiv war: beim Werkzeugwechsel sauber beenden.
        // Ausnahme: wenn der Sender BtnCalibrate selbst ist (gerade aktiviert).
        if (!ReferenceEquals(sender, BtnCalibrate)
            && (_isCalibrating || BtnCalibrate.IsChecked == true))
        {
            _isCalibrating = false;
            _calibStart = null;
            BtnCalibrate.IsChecked = false;
            CalibrationHint.Visibility = Visibility.Collapsed;
        }

        // Alle anderen Buttons unchecken (mutual exclusion ueber alle Tools)
        foreach (var tb in ToolButtons())
        {
            if (!ReferenceEquals(tb, sender))
                tb.IsChecked = false;
        }

        // Beim Tool-Wechsel ALLE Overlay-Reste (manuell, preview, SAM-Maske, AI-Overlay)
        // wegputzen - sonst kombinieren sich Geometrien aus vorigen Werkzeugen.
        ClearAllDrawingShapes(includeCalibration: false);
        try
        {
            var samElements = OverlayCanvas.Children.OfType<FrameworkElement>()
                .Where(el => (el.Tag as string) == "sam_manual_mask").ToList();
            foreach (var el in samElements) OverlayCanvas.Children.Remove(el);
        }
        catch { }

        OverlayToolType tool;
        if (sender == BtnToolRect) tool = OverlayToolType.Rectangle;
        else tool = OverlayToolType.None;

        _overlayService.ActiveTool = tool;
        ClearAllDrawingShapes();
    }

    private void ToolButton_Unchecked(object sender, RoutedEventArgs e)
    {
        // Wenn kein Button mehr gecheckt → Tool auf None
        bool anyChecked = ToolButtons().Any(tb => tb.IsChecked == true);
        if (!anyChecked)
            _overlayService.ActiveTool = OverlayToolType.None;
    }

    // Foto-Assistent-Tools (Stundenlinien, Drehen, Entzerren, Screenshot, DisplayStyle,
    // Fliessrichtungs-Bogen) wurden aus dem Codier-Modus entfernt - sie leben jetzt nur noch
    // im PhotoMeasurementWindow. Der Codier-Modus bleibt so schlank wie moeglich.

    private void BtnPipeBendSnap_Checked(object sender, RoutedEventArgs e)
        => _overlayService.PipeBendSnapEnabled = true;

    private void BtnPipeBendSnap_Unchecked(object sender, RoutedEventArgs e)
        => _overlayService.PipeBendSnapEnabled = false;

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
        // Aktuelle Videozeit direkt vom Player holen (falls nicht via Slider-Sync gesetzt)
        if (_player is not null)
            _vm.CurrentVideoTime = TimeSpan.FromMilliseconds(_player.Time);
        var captureMeter = SyncSessionMeterFromVideo();

        // ProtocolEntry fuer den Katalog vorbereiten
        var entry = new ProtocolEntry
        {
            Source = ProtocolEntrySource.Manual,
            MeterStart = captureMeter,
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
            captureMeter,
            _vm.CurrentVideoTime);

        var dlg = new VsaCodeExplorerWindow(explorerVm, _vm.VideoPath, _vm.CurrentVideoTime)
        { Owner = this, PipeCalibration = _overlayService.Calibration };
        if (dlg.ShowDialog() == true && dlg.SelectedEntry is not null)
        {
            if (dlg.PipeCalibration != null)
                _overlayService.SetCalibration(dlg.PipeCalibration);
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
                BtnSaveAsTraining.IsEnabled = true;

            // Entry direkt als Event uebernehmen (Code + Parameter sind gesetzt)
            // WICHTIG: Nur ueber den Service adden, damit kein Duplikat in der Liste entsteht.
            var newEvent = _sessionService.AddEvent(entry, _vm.CurrentOverlay);

            // Nach Meter sortiert anzeigen
            ResortEventsByMeter();

            TxtEventCount.Text = $"{_vm.EventCount} Ereignisse";

            // Reset
            _vm.CurrentOverlay = null;
            ClearAllDrawingShapes();
            TxtSelectedCode.Text = "";
            BtnCreateEvent.IsEnabled = false;
                BtnSaveAsTraining.IsEnabled = false;
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
        var result1 = _dialogs.ShowMessage(
            $"Foto 1 fuer {codingEvent.Entry.Code} ({codingEvent.MeterAtCapture:F2}m) erstellen?",
            "Foto erstellen",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result1 == MessageBoxResult.Yes)
        {
            LstEvents.SelectedItem = codingEvent;
            await CapturePhotoForSelectedEvent(0);

            // Foto 2 anbieten nur wenn Foto 1 erstellt wurde
            var result2 = _dialogs.ShowMessage(
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
        // Distanz vor Speichern auf aktuelle Videoposition ziehen.
        SyncSessionMeterFromVideo();
        _vm.CreateEventCommand.Execute(null);
        ResortEventsByMeter();
        ClearAllDrawingShapes();
        TxtSelectedCode.Text = "";
        BtnCreateEvent.IsEnabled = false;
                BtnSaveAsTraining.IsEnabled = false;
        UpdateFotoButtons();
    }

    // --- Lehrer-Annotation (als Training speichern) ---

    private async void BtnSaveAsTraining_Click(object sender, RoutedEventArgs e)
    {
        if (_vm.CurrentOverlay == null)
        {
            _dialogs.ShowMessage("Bitte zuerst eine Markierung zeichnen.", "Keine Markierung",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        // Nur flaechenhafte Tools fuer YOLO-Training zulassen
        var allowedTools = new[]
        {
            AuswertungPro.Next.Domain.Models.OverlayToolType.Rectangle,
            AuswertungPro.Next.Domain.Models.OverlayToolType.Ellipse,
            AuswertungPro.Next.Domain.Models.OverlayToolType.Freehand
        };
        if (!allowedTools.Contains(_vm.CurrentOverlay.ToolType))
        {
            _dialogs.ShowMessage(
                $"Das Werkzeug \"{_vm.CurrentOverlay.ToolType}\" erzeugt keine Flaechenmarkierung.\n" +
                "Fuer Lehrer-Annotationen bitte Rechteck, Ellipse oder Freihand verwenden.",
                "Werkzeug nicht geeignet",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // 1. VSA-Code waehlen via bestehendem Explorer-Dialog
        var captureMeter = SyncSessionMeterFromVideo();
        if (_player is not null)
            _vm.CurrentVideoTime = TimeSpan.FromMilliseconds(_player.Time);

        var entry = new AuswertungPro.Next.Domain.Protocol.ProtocolEntry
        {
            Source = AuswertungPro.Next.Domain.Protocol.ProtocolEntrySource.Manual,
            MeterStart = captureMeter,
            Zeit = _vm.CurrentVideoTime
        };

        var explorerVm = new ViewModels.Windows.VsaCodeExplorerViewModel(
            entry, captureMeter, _vm.CurrentVideoTime);

        var dlg = new VsaCodeExplorerWindow(explorerVm, _vm.VideoPath, _vm.CurrentVideoTime)
        { Owner = this, PipeCalibration = _overlayService.Calibration };
        // Kalibrierung immer zuruecklesen (auch bei Abbruch)
        var dlgResult = dlg.ShowDialog();
        if (dlg.PipeCalibration != null)
            _overlayService.SetCalibration(dlg.PipeCalibration);
        if (dlgResult != true || dlg.SelectedEntry is null) return;

        var selectedEntry = dlg.SelectedEntry;

        try
        {
            BtnSaveAsTraining.IsEnabled = false;
            BtnSaveAsTraining.Content = "\u23F3 Speichere...";

            // 2. Frame-Snapshot erstellen
            var pngBytes = await CaptureCurrentFrameAsync();
            if (pngBytes == null || pngBytes.Length == 0)
            {
                _dialogs.ShowMessage("Frame konnte nicht extrahiert werden.", "Fehler",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // AnnotationId frueh generieren (wird fuer Temp-File + Dateinamen + JSON verwendet)
            var annotationId = Guid.NewGuid().ToString("N")[..12];

            // Frame temporaer speichern (annotationId = kollisionsfrei)
            var tempFrame = System.IO.Path.Combine(
                AuswertungPro.Next.Application.Ai.Teacher.TeacherAnnotationStore.GetImagesDir(),
                $"frame_temp_{annotationId}.png");
            await System.IO.File.WriteAllBytesAsync(tempFrame, pngBytes);

            // 3. BoundingBox berechnen + Validierung
            //    Audit-Fix 2026-04: SAM-tight-BBox bevorzugen wenn vorhanden -> bessere YOLO-Labels.
            //    User zeichnet ein grosses Rechteck, SAM liefert pixelgenaue Maske, daraus wird
            //    eine engere BBox abgeleitet. So lernt YOLO "wo genau" statt "wo ungefaehr".
            var bbox = _lastSamTightBbox is { } tight && tight.Width > 0.005 && tight.Height > 0.005
                ? tight
                : Application.Ai.NormalizedBoundingBox.FromPoints(_vm.CurrentOverlay.Points);
            const double MinBboxDimension = 0.01; // Mindestgroesse 1% des Frames
            if (bbox.Width < MinBboxDimension || bbox.Height < MinBboxDimension)
            {
                try { System.IO.File.Delete(tempFrame); } catch { }
                _dialogs.ShowMessage(
                    "Die Markierung ist zu klein oder hat keine Flaeche.\n" +
                    "Fuer Lehrer-Annotationen bitte Rechteck, Ellipse oder Freihand verwenden.",
                    "Markierung ungueltig",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var baseName = $"{selectedEntry.Code}_{captureMeter:F1}m_{annotationId}";

            // Fix 1: VSA-Code → YOLO classId ueber persistiertes Mapping
            int classId = AuswertungPro.Next.Application.Ai.Teacher.VsaYoloClassMap.GetClassId(selectedEntry.Code);

            // 4. YOLO-Export (Crop + Annotation)
            var exportService = new Ai.Teacher.TrainingAnnotationExportService();
            var exportResult = await exportService.ExportAsync(
                tempFrame, bbox, selectedEntry.Code, classId, baseName);

            // Fix 2: Nur bei erfolgreichem Export persistieren
            if (!exportResult.Success)
            {
                _dialogs.ShowMessage(
                    $"Export fehlgeschlagen:\n{exportResult.Error}\n\nAnnotation wird NICHT gespeichert.",
                    "Export-Fehler",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // 5. TeacherAnnotation erstellen und speichern (nur bei Erfolg)
            var annotation = new AuswertungPro.Next.Application.Ai.Teacher.TeacherAnnotation
            {
                AnnotationId = annotationId,
                VsaCode = selectedEntry.Code,
                Beschreibung = selectedEntry.Beschreibung ?? "",
                MeterPosition = captureMeter,
                VideoTimestamp = _vm.CurrentVideoTime ?? TimeSpan.Zero,
                HaltungName = _vm.HaltungName,
                VideoPath = _vm.VideoPath,
                ToolType = _vm.CurrentOverlay.ToolType,
                Points = new System.Collections.Generic.List<AuswertungPro.Next.Domain.Models.NormalizedPoint>(
                    _vm.CurrentOverlay.Points),
                BoundingBox = bbox,
                ClockPosition = _vm.CurrentOverlay.ClockFrom,
                FullFramePath = exportResult.FullFramePath,
                CroppedRegionPath = exportResult.CroppedRegionPath,
                YoloAnnotationPath = exportResult.YoloAnnotationPath,
                WidthMm = _vm.CurrentOverlay.Q2Mm,
                HeightMm = _vm.CurrentOverlay.Q1Mm
            };

            await AuswertungPro.Next.Application.Ai.Teacher.TeacherAnnotationStore.AppendAsync(annotation);

            // Temporaeres Frame aufraeumen (wurde nach teacher_images kopiert)
            try { System.IO.File.Delete(tempFrame); } catch { }

            // 6. Reset
            _vm.CurrentOverlay = null;
            ClearAllDrawingShapes();
            TxtSelectedCode.Text = "";
            BtnCreateEvent.IsEnabled = false;
                BtnSaveAsTraining.IsEnabled = false;
            UpdateOverlayInfo(null);

            var count = await AuswertungPro.Next.Application.Ai.Teacher.TeacherAnnotationStore.CountAsync();
            _dialogs.ShowMessage(
                $"Lehrer-Annotation gespeichert:\n" +
                $"Code: {selectedEntry.Code}\n" +
                $"Meter: {captureMeter:F2}m\n" +
                $"Tool: {annotation.ToolType}\n\n" +
                $"Gesamt: {count} Annotationen\n" +
                (exportResult.Success ? "YOLO-Export erfolgreich" : $"Export-Fehler: {exportResult.Error}"),
                "Training gespeichert",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            _dialogs.ShowMessage($"Fehler beim Speichern:\n{ex.Message}", "Fehler",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            BtnSaveAsTraining.Content = "\U0001F4DA Als Training speichern";
            BtnSaveAsTraining.IsEnabled = _vm.CurrentOverlay != null;
        }
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

    private void ContextMenuEdit_Click(object sender, RoutedEventArgs e)
    {
        if (LstEvents.SelectedItem is CodingEvent)
            LstEvents_MouseDoubleClick(sender, null!);
    }

    private void ContextMenuDelete_Click(object sender, RoutedEventArgs e)
        => BtnDeleteDefect_Click(sender, e);

    private void LstEvents_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (LstEvents.SelectedItem is not CodingEvent ev) return;

        // ProtocolEntry zum Bearbeiten oeffnen (bestehende Werte vorbelegen)
        var entry = ev.Entry;

        var explorerVm = new ViewModels.Windows.VsaCodeExplorerViewModel(
            entry,
            entry.MeterStart,
            entry.Zeit);

        var dlg = new VsaCodeExplorerWindow(explorerVm, _vm.VideoPath, _vm.CurrentVideoTime)
        { Owner = this, PipeCalibration = _overlayService.Calibration };
        if (dlg.ShowDialog() == true && dlg.SelectedEntry is not null)
        {
            if (dlg.PipeCalibration != null)
                _overlayService.SetCalibration(dlg.PipeCalibration);
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
            // Beim Auswaehlen eines Defekts direkt zur Distanz springen,
            // damit Bild und Distanzanzeige nicht auseinanderlaufen.
            if ((_vm.IsRunning || _vm.IsPaused) && Math.Abs(_vm.CurrentMeter - ev.MeterAtCapture) > 0.02)
            {
                _sessionService.MoveToMeter(ev.MeterAtCapture);
                _lastSyncedMeter = -1;
                SyncVideoToMeter();
            }
            UpdateDefectDetailPanel(ev);
            RefreshAiOverlaySelectionView();
        }
        else
        {
            _vm.SelectedDefect = null;
            DefectDetailPanel.Visibility = Visibility.Collapsed;
            RefreshAiOverlaySelectionView();
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

    /// <summary>Selektiert das erste Event mit passendem VSA-Code (fuer BBox-Klick).</summary>
    private void SelectEventByCode(string code)
    {
        if (_vm?.Events == null) return;
        var match = _vm.Events.FirstOrDefault(ev =>
            string.Equals(ev.Entry.Code, code, StringComparison.OrdinalIgnoreCase));
        if (match != null)
        {
            _vm.SelectedDefect = match;
            LstEvents.SelectedItem = match;
            LstEvents.ScrollIntoView(match);
            UpdateDefectDetailPanel(match);
        }
    }

    private void CodingMode_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        // Nicht abfangen wenn ein TextBox fokussiert ist
        if (e.OriginalSource is System.Windows.Controls.TextBox) return;

        switch (e.Key)
        {
            case System.Windows.Input.Key.Escape:
                // Audit R-H6 2026-04-25: ESC als universeller Notausstieg.
                // Kalibrierungs-Modus wird verlassen wenn aktiv. Verhindert
                // den UI-Trap bei Layout-Glitch (Toggle-Button unklickbar).
                if (_isCalibrating)
                {
                    try
                    {
                        if (BtnCalibrate != null)
                            BtnCalibrate.IsChecked = false;
                        _isCalibrating = false;
                        ClearAllDrawingShapes();
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[CodingMode ESC] Calibration-Exit: {ex.Message}");
                    }
                    e.Handled = true;
                }
                break;
            case System.Windows.Input.Key.O:
                BtnAcceptDefect_Click(sender, e);
                e.Handled = true;
                break;
            case System.Windows.Input.Key.Delete:
                BtnRejectDefect_Click(sender, e);
                e.Handled = true;
                break;
        }
    }

    private void BtnAcceptDefect_Click(object sender, RoutedEventArgs e)
    {
        var selected = _vm.SelectedDefect ?? LstEvents.SelectedItem as CodingEvent;
        if (selected == null) return;
        _vm.SelectedDefect = selected;

        _vm.AcceptDefectCommand.Execute(null);
        ClearAllDrawingShapes(); // Overlay entfernen nach Aktion
        RefreshAiOverlaySelectionView();
        if (_vm.SelectedDefect != null)
        {
            UpdateDefectDetailPanel(_vm.SelectedDefect);
            LstEvents.Items.Refresh();
            UpdateEventMarkers();
        }
    }

    private void BtnEditDefect_Click(object sender, RoutedEventArgs e)
    {
        var selected = _vm.SelectedDefect ?? LstEvents.SelectedItem as CodingEvent;
        if (selected == null) return;
        _vm.SelectedDefect = selected;

        // Editor oeffnen fuer Code-Korrektur (fuer alle Beobachtungen)
        var ev = selected;

        var entry = ev.Entry;
        var explorerVm = new ViewModels.Windows.VsaCodeExplorerViewModel(
            entry, entry.MeterStart, entry.Zeit);

        var dlg = new VsaCodeExplorerWindow(explorerVm, _vm.VideoPath, _vm.CurrentVideoTime)
        { Owner = this, PipeCalibration = _overlayService.Calibration };
        if (dlg.ShowDialog() == true && dlg.SelectedEntry is not null)
        {
            if (dlg.PipeCalibration != null)
                _overlayService.SetCalibration(dlg.PipeCalibration);
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

            // KI-Decision als bearbeitet markieren (nur bei KI-Events)
            if (ev.AiContext != null)
                _vm.EditDefectCommand.Execute(null);

            ClearAllDrawingShapes(); // Overlay entfernen nach Bearbeitung
            RefreshAiOverlaySelectionView();
            LstEvents.Items.Refresh();
            UpdateDefectDetailPanel(ev);
            UpdateEventMarkers();

            // Foto-Markierung fuer KI-Training anbieten
            if (entry.FotoPaths.Count > 0 && !string.IsNullOrEmpty(entry.FotoPaths[0])
                && System.IO.File.Exists(entry.FotoPaths[0]))
            {
                var answer = _dialogs.ShowMessage(
                    "Stelle auf dem Foto markieren fuer KI-Training?\n\n" +
                    "Das hilft der KI, den korrigierten Code beim naechsten Mal\n" +
                    "an der richtigen Stelle zu erkennen.",
                    "KI-Training: Foto markieren",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);
                if (answer == MessageBoxResult.Yes)
                {
                    var photoWin = new PhotoMeasurementWindow(
                        entry.FotoPaths[0], _overlayService.Calibration);
                    photoWin.Owner = this;
                    if (photoWin.ShowDialog() == true && photoWin.Result.Confirmed)
                    {
                        if (!string.IsNullOrEmpty(photoWin.Result.OverlayPhotoPath))
                            entry.FotoPaths[0] = photoWin.Result.OverlayPhotoPath;
                        if (photoWin.Result.UpdatedCalibration != null)
                            _overlayService.SetCalibration(photoWin.Result.UpdatedCalibration);

                        // V4.3: Mess-Metadaten (Value + Einheit + Tool + Subject) in CodeMeta.Parameters
                        // schreiben, damit Protokoll/Report/Export sie lesen koennen.
                        CopyMeasurementMetadata(entry, photoWin.Result);

                        _sessionService.UpdateEvent(ev.EventId, entry, ev.Overlay);
                    }
                }
            }
        }
    }

    /// <summary>
    /// V4.3 — kopiert Werkzeug-Metadaten aus PhotoMeasurementResult in die ProtocolEntry.CodeMeta.Parameters.
    /// Keys: Q1/Q1_Unit/Q2/Q2_Unit/MeasurementTool/MeasurementSubject.
    /// </summary>
    private static void CopyMeasurementMetadata(
        AuswertungPro.Next.Domain.Protocol.ProtocolEntry entry,
        AuswertungPro.Next.Domain.Models.PhotoMeasurementResult r)
    {
        if (r is null) return;
        entry.CodeMeta ??= new AuswertungPro.Next.Domain.Protocol.ProtocolEntryCodeMeta { Code = entry.Code };
        var p = entry.CodeMeta.Parameters;
        if (!string.IsNullOrWhiteSpace(r.Value1)) p["Q1"] = r.Value1!;
        if (!string.IsNullOrWhiteSpace(r.Unit1))  p["Q1_Unit"] = r.Unit1!;
        if (!string.IsNullOrWhiteSpace(r.Value2)) p["Q2"] = r.Value2!;
        if (!string.IsNullOrWhiteSpace(r.Unit2))  p["Q2_Unit"] = r.Unit2!;
        if (!string.IsNullOrWhiteSpace(r.MeasurementTool))    p["MeasurementTool"] = r.MeasurementTool!;
        if (!string.IsNullOrWhiteSpace(r.MeasurementSubject)) p["MeasurementSubject"] = r.MeasurementSubject!;
        entry.CodeMeta.UpdatedAt = DateTimeOffset.UtcNow;
    }

    private void BtnDeleteDefect_Click(object sender, RoutedEventArgs e)
    {
        if (_vm.SelectedDefect == null) return;

        var ev = _vm.SelectedDefect;
        var result = _dialogs.ShowMessage(
            $"Beobachtung \"{ev.Entry.Code}\" bei {ev.MeterAtCapture:F2}m wirklich löschen?",
            "Beobachtung löschen",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes) return;

        // Event aus Session und Liste entfernen
        _sessionService.RemoveEvent(ev.EventId);
        _vm.Events.Remove(ev);
        _vm.SelectedDefect = null;
        DefectDetailPanel.Visibility = Visibility.Collapsed;
        ClearAllDrawingShapes(); // Overlay entfernen nach Loeschung

        LstEvents.Items.Refresh();
        UpdateEventMarkers();
        TxtEventCount.Text = $"{_vm.EventCount} Ereignisse";
    }

    private void BtnRejectDefect_Click(object sender, RoutedEventArgs e)
    {
        var selected = _vm.SelectedDefect ?? LstEvents.SelectedItem as CodingEvent;
        if (selected == null) return;
        _vm.SelectedDefect = selected;

        _vm.RejectDefectCommand.Execute(null);
        ClearAllDrawingShapes(); // Overlay entfernen nach Aktion
        RefreshAiOverlaySelectionView();
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

        // Actions immer verfuegbar: auch manuelle/auto-accept Events sollen erneut bestaetigt
        // oder verworfen werden koennen.
        BtnAcceptDefect.Visibility = Visibility.Visible;
        BtnRejectDefect.Visibility = Visibility.Visible;
    }

    private void RefreshAiOverlaySelectionView()
    {
        if (_currentAiOverlays == null || _currentAiOverlays.Count == 0)
        {
            AiOverlayCanvas.Children.Clear();
            return;
        }

        RenderAiOverlays(_currentAiOverlays);
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
            _aiConfig = AuswertungPro.Next.Application.Ai.AiRuntimeConfigProvider.Load();
            _aiModelName = _aiConfig.VisionModel;
            if (!_aiConfig.Enabled)
            {
                SetAiStatus("KI deaktiviert", "#94A3B8", "Modell: aus");
                BtnAnalyzeFrame.IsEnabled = false;
                return;
            }

            _ollamaClient = _aiConfig.CreateOllamaClient();
            _liveDetection = new LiveDetectionService(_ollamaClient, _aiConfig.VisionModel);
            _enhancedVision = new EnhancedVisionAnalysisService(_ollamaClient, _aiConfig.VisionModel, _aiConfig.ReferenceVisionModel);

            // Pipeline-Failure-Event abfangen - alle stillen Failures landen jetzt im Result-Panel
            EnhancedVisionAnalysisService.PipelineFailure += OnPipelineFailure;

            SetAiStatus("Bereit", "#22C55E",
                $"Qwen aktiv ({CompactModelName(_aiModelName)})");

            // Few-Shot-Beispiele laden — ohne diese findet die KI drastisch weniger (Audit-Fix)
            _ = LoadFewShotAsync();
        }
        catch (Exception ex)
        {
            SetAiStatus($"Fehler: {ex.Message}", "#EF4444",
                $"Modell: {CompactModelName(_aiModelName)}", error: true);
            BtnAnalyzeFrame.IsEnabled = false;
        }
    }

    private async Task LoadFewShotAsync()
    {
        try
        {
            if (_enhancedVision == null) return;
            var store = new AuswertungPro.Next.Application.Ai.Training.FewShotExampleStore();
            await _enhancedVision.EnableFewShotAsync(store);
            Dispatcher.Invoke(() =>
                SetAiStatus("Bereit (Few-Shot geladen)", "#22C55E",
                    $"Qwen aktiv ({CompactModelName(_aiModelName)})"));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[CodingMode] Few-Shot laden fehlgeschlagen: {ex.Message}");
        }
    }

    // KI-Status-Anzeige (CompactModelName/SetPipelineDots/Start+StopAiStatusPulse/SetAiStatus)
    // nach CodingModeWindow.AiStatus.cs migriert (Slice 8a.2.4).

    private async void BtnAnalyzeFrame_Click(object sender, RoutedEventArgs e)
    {
        // Guards mit sichtbarem Feedback - vorher: stille Returns, deshalb dachte der User
        // "es passiert nichts" wenn z.B. _isAnalyzing haengen geblieben ist.
        if (_liveDetection == null)
        {
            SetAiStatus("KI nicht initialisiert (Ollama down?)", "#EF4444",
                $"Modell: {CompactModelName(_aiModelName)}", error: true);
            return;
        }
        if (_player == null)
        {
            SetAiStatus("Player nicht bereit", "#EF4444", "VLC fehlt", error: true);
            return;
        }
        if (_isAnalyzing)
        {
            // Reset-Notbremse: wenn Flag mehr als 30s gesetzt ist → freischalten,
            // damit der Button nicht ewig blockiert bleibt nach einem Crash/Hang.
            SetAiStatus("Analyse laeuft schon - Stop in 1s", "#F59E0B", "Reset...", busy: true);
            await Task.Delay(1000);
            if (_isAnalyzing)
            {
                _isAnalyzing = false;
                _analysisCts?.Cancel();
                SetAiStatus("Analyse-Lock gebrochen, neuer Versuch...", "#F59E0B", "Reset");
            }
        }
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
            SetAiStatus("Frame wird analysiert...", "#F59E0B", "1/3 Snapshot", busy: true);

            // Zeitstempel VOR dem Capture festhalten (Capture wartet bis zu 1s)
            var timestampSec = _player.Time / 1000.0;

            // Frame aus VLC als PNG extrahieren
            var pngBytes = await CaptureCurrentFrameAsync();
            if (pngBytes == null || pngBytes.Length == 0)
            {
                SetAiStatus("Frame konnte nicht extrahiert werden", "#EF4444",
                    $"Modell: {CompactModelName(_aiModelName)}", error: true);
                return;
            }

            SetAiStatus("Frame wird analysiert...", "#F59E0B",
                $"2/3 Inferenz ({CompactModelName(_aiModelName)})", busy: true);

            LiveDetection result;

            // Bevorzugt EnhancedVisionAnalysisService (besserer Prompt + Schema + ImageQuality-Gate)
            if (_enhancedVision != null)
            {
                var b64 = Convert.ToBase64String(pngBytes);
                var (enhanced, _) = await _enhancedVision.AnalyzeWithEscalationAsync(
                    b64, context: null, ct: _analysisCts.Token);
                result = AuswertungPro.Next.Application.Ai.LiveDetectionMapper.FromEnhancedAnalysis(enhanced, timestampSec);

                // Sichtbares Panel fuer User - vorher: Result war oft nur in der Liste
                await Dispatcher.InvokeAsync(() =>
                {
                    var sb = new System.Text.StringBuilder();
                    sb.AppendLine($"Bildqualitaet: {enhanced.ImageQuality}");
                    sb.AppendLine($"Material: {enhanced.PipeMaterial}");
                    if (enhanced.PipeDiameterMm.HasValue)
                        sb.AppendLine($"DN geschaetzt: {enhanced.PipeDiameterMm} mm");
                    sb.AppendLine($"Findings: {enhanced.Findings.Count}");
                    foreach (var f in enhanced.Findings.Take(3))
                    {
                        sb.AppendLine($"• {f.Label}");
                        if (!string.IsNullOrEmpty(f.VsaCodeHint))
                            sb.AppendLine($"  Code: {f.VsaCodeHint}  | Sev: {f.Severity}");
                    }
                    if (!string.IsNullOrWhiteSpace(_enhancedVision.LastPipelineWarning))
                        sb.AppendLine($"Warnung: {_enhancedVision.LastPipelineWarning}");
                    if (string.IsNullOrEmpty(enhanced.Error))
                        ShowBboxResultPanel("Frame-Analyse", sb.ToString(), isError: false);
                    else
                        ShowBboxResultPanel("Analyse-Fehler", enhanced.Error, isError: true);
                });
            }
            else
            {
                result = await _liveDetection!.AnalyzeFrameAsync(
                    pngBytes, timestampSec, _analysisCts.Token);
            }

            await Dispatcher.InvokeAsync(() => ShowAiResults(result));
        }
        catch (OperationCanceledException)
        {
            SetAiStatus("Analyse abgebrochen", "#94A3B8",
                $"Modell: {CompactModelName(_aiModelName)}");
        }
        catch (Exception ex)
        {
            SetAiStatus($"Fehler: {ex.Message}", "#EF4444",
                $"Modell: {CompactModelName(_aiModelName)}", error: true);
        }
        finally
        {
            _isAnalyzing = false;
            Dispatcher.Invoke(() =>
            {
                BtnAnalyzeFrame.IsEnabled = true;
                TxtAnalyzeButton.Text = "Frame analysieren";

                // Fertig-Meldung
                int befundCount = _vm?.Events?.Count ?? 0;
                SetAiStatus($"Analyse beendet — {befundCount} Beobachtungen", "#22C55E",
                    "Fertig");
            });
        }
    }

    private void ShowAiResults(LiveDetection result)
    {
        if (result.Error != null)
        {
            SetAiStatus($"Fehler: {result.Error}", "#EF4444",
                $"Modell: {CompactModelName(_aiModelName)}", error: true);
            AiFindingsList.ItemsSource = null;
            AiOverlayCanvas.Children.Clear();
            DetectionCanvas.Children.Clear();
            return;
        }

        if (result.Findings.Count == 0)
        {
            SetAiStatus("Kein Befund erkannt", "#22C55E", "3/3 Overlay aktualisiert");
            AiFindingsList.ItemsSource = null;
            AiOverlayCanvas.Children.Clear();
            DetectionCanvas.Children.Clear();
            return;
        }

        SetAiStatus($"{result.Findings.Count} Befund(e) erkannt", "#22C55E",
            "3/3 Overlay + Ring-Sektor");

        // Meterstand aus KI aktualisieren (informativ)
        if (result.MeterReading.HasValue)
        {
            _lastAiMeterReading = result.MeterReading.Value;
            _lastAiMeterTimestampSec = result.TimestampSeconds;
            TxtAiStatus.Text += $"  |  Meter: {result.MeterReading:F2}m";
        }

        // Einmalige Beobachtungen filtern: BCD/BCE nur einmal pro Haltung
        // Wenn bereits ein akzeptiertes BCD/BCE-Event existiert, neue Vorschlaege verwerfen
        var singletonCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "BCD", "BCE" };
        var existingCodes = _vm?.Events
            .Where(ev => ev.AiContext == null || ev.AiContext.Decision != CodingUserDecision.Rejected)
            .Select(ev => ev.Entry.Code?.ToUpperInvariant())
            .Where(c => c != null)
            .ToHashSet(StringComparer.OrdinalIgnoreCase) ?? new HashSet<string?>();

        var filteredFindings = result.Findings
            .Where(f =>
            {
                var code = f.VsaCodeHint?.ToUpperInvariant() ?? "";
                if (singletonCodes.Contains(code) && existingCodes.Contains(code))
                    return false; // Duplikat — schon protokolliert
                return true;
            }).ToList();

        // Findings in UI-Objekte konvertieren
        var items = filteredFindings.Select(f => new AiFindingDisplayItem(f)).ToList();
        AiFindingsList.ItemsSource = items;

        // KI-Overlays auf dem Video rendern (nur gefilterte Findings)
        var calibration = _overlayService?.Calibration;
        var overlays = AiOverlayConverter.FromFindings(filteredFindings, calibration);
        RenderAiOverlays(overlays);

        // Ring-Sektor Visualisierung
        RenderDetectionRingSector(filteredFindings, result.TimestampSeconds);
    }

    // KI-Overlay-Rendering (RenderAiOverlays/RenderSingleAiOverlay/RenderAi{Rectangle,Level,Line}/AddAiLabel/MapSeverityColor)
    // nach CodingModeWindow.AiOverlayRender.cs migriert (Slice 8a.2.2).

    // RenderDetectionRingSector nach CodingModeWindow.DetectionRing.cs
    // migriert (Slice 8a.2.5).

    // CaptureCurrentFrameAsync + RenderVideoViewToPng nach
    // CodingModeWindow.FrameCapture.cs migriert (Slice 8a.2.3).

    // ── Audit-Fix 2026-04: SAM-Segmentierung nach BBox ────────────────────
    // Vorher hatte der Codiermodus keinen SAM-Aufruf - User sah nur das Rechteck,
    // keine Umrisse. PlayerWindow ruft SAM seit c3c6ad60, dieses Window war der Nachzuegler.
    // Effekt fuer Training: tight-BBox aus Maske statt User-BBox -> bessere YOLO-Labels.

    /// <summary>
    /// Sendet die gezeichnete BBox an SAM, rendert die zurueckgegebene Maske
    /// auf das OverlayCanvas und merkt sich die enge BBox fuer den Trainings-Export.
    /// Bei Sidecar-Fehler oder leerer Antwort: kein Render, kein Throw - User behaelt das Rechteck.
    /// </summary>
    /// <summary>
    /// Empfaengt Pipeline-Failure-Events vom EnhancedVisionAnalysisService und macht sie
    /// im Result-Panel sichtbar (vorher: Debug.WriteLine only, fuer User unsichtbar).
    /// </summary>
    private void OnPipelineFailure(object? sender, AuswertungPro.Next.Infrastructure.Ai.EnhancedVisionAnalysisService.PipelineFailureEvent ev)
    {
        try
        {
            var msg = $"{ev.Stage} ({ev.Model}): {ev.ExceptionType}\n{ev.Message}";
            if (Dispatcher.CheckAccess())
                ShowBboxResultPanel($"Pipeline-Fehler {ev.At:HH:mm:ss}", msg, isError: true);
            else
                Dispatcher.BeginInvoke(() => ShowBboxResultPanel($"Pipeline-Fehler {ev.At:HH:mm:ss}", msg, isError: true));
            SetStatusSafe($"Pipeline: {ev.Stage} fehlgeschlagen");
        }
        catch { /* Event-Handler-Fehler nicht propagieren */ }
    }

    /// <summary>Setzt Statustext thread-sicher; ignoriert Fehler bei laufendem Shutdown.</summary>
    private void SetStatusSafe(string text)
    {
        try
        {
            if (Dispatcher.CheckAccess())
            {
                if (TxtStatus != null) TxtStatus.Text = text;
            }
            else
            {
                Dispatcher.BeginInvoke(() => { if (TxtStatus != null) TxtStatus.Text = text; });
            }
        }
        catch { }
    }

    /// <summary>
    /// Rendert die SAM-Maske mit MAXIMAL deutlichem Highlight - bewusst gar nicht subtil.
    /// Konturlinie: pulsierend weiss + 4px, Fuellung: 50% magenta-gelb, plus eine dicke
    /// gelbe Tight-BBox damit unmissverstaendlich erkennbar ist dass SAM gelaufen ist.
    /// </summary>
    private void RenderManualSamMaskHighlight(AuswertungPro.Next.Application.Ai.Pipeline.SamResponse samResp, AuswertungPro.Next.Application.Ai.Pipeline.SamMaskResult mask)
    {
        if (samResp is null || mask is null) return;
        var imgW = Math.Max(1, samResp.ImageWidth);
        var imgH = Math.Max(1, samResp.ImageHeight);
        var cw = OverlayCanvas.ActualWidth;
        var ch = OverlayCanvas.ActualHeight;
        if (cw <= 0 || ch <= 0) return;

        // Diagnose-Counter, damit wir wissen welche Stufe gerendert wurde.
        int polylineCount = 0;
        bool fillRendered = false;
        bool contourRectRendered = false;

        // 1. Maske dekodieren - faengt RLE-Probleme ab.
        bool[,]? decoded = null;
        try { decoded = Ai.Pipeline.SamMaskRenderer.DecodeRle(mask.MaskRle, imgW, imgH); }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[CodingMode SAM] RLE-Decode Fehler: {ex.Message}");
            SetStatusSafe($"SAM-Render: RLE-Decode fehlgeschlagen ({ex.Message})");
        }

        // 2. Gefuellte Maske (immer versuchen, semi-transparent gruen)
        if (decoded != null)
        {
            try
            {
                var fillGeom = Ai.Pipeline.SamMaskRenderer.ExtractFillGeometry(decoded, imgW, imgH, cw, ch, targetWidth: 720);
                var fillPath = new System.Windows.Shapes.Path
                {
                    Data = fillGeom,
                    Fill = new SolidColorBrush(Color.FromArgb(140, 57, 255, 20)),
                    Tag = "sam_manual_mask",
                    IsHitTestVisible = false
                };
                Panel.SetZIndex(fillPath, 100);
                OverlayCanvas.Children.Add(fillPath);
                fillRendered = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CodingMode SAM] Fill-Geom Fehler: {ex.Message}");
            }
        }

        // 3. Glatte Konturen via Moore-Boundary-Trace (kann leer sein)
        if (decoded != null)
        {
            try
            {
                var polylines = Ai.Pipeline.SamMaskRenderer.ExtractContourPolylines(decoded, imgW, imgH, cw, ch);
                foreach (var poly in polylines)
                {
                    if (poly.Count < 3) continue;
                    polylineCount++;

                    var outer = new System.Windows.Shapes.Polyline
                    {
                        Stroke = Brushes.White,
                        StrokeThickness = 5,
                        StrokeLineJoin = PenLineJoin.Round,
                        Tag = "sam_manual_mask",
                        IsHitTestVisible = false
                    };
                    foreach (var p in poly) outer.Points.Add(p);
                    outer.Points.Add(poly[0]);
                    Panel.SetZIndex(outer, 101);
                    OverlayCanvas.Children.Add(outer);

                    var inner = new System.Windows.Shapes.Polyline
                    {
                        Stroke = new SolidColorBrush(Color.FromRgb(0, 255, 0)),
                        StrokeThickness = 3,
                        StrokeLineJoin = PenLineJoin.Round,
                        Tag = "sam_manual_mask",
                        IsHitTestVisible = false,
                        Effect = new System.Windows.Media.Effects.DropShadowEffect
                        {
                            BlurRadius = 16, ShadowDepth = 0,
                            Color = Color.FromRgb(0, 255, 0), Opacity = 0.95
                        }
                    };
                    foreach (var p in poly) inner.Points.Add(p);
                    inner.Points.Add(poly[0]);
                    Panel.SetZIndex(inner, 102);
                    OverlayCanvas.Children.Add(inner);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CodingMode SAM] Polyline-Trace Fehler: {ex.Message}");
            }
        }

        // 4. Fallback wenn keine Polylinie produziert: alte ExtractContourGeometry
        // (zaun-aehnliche Treppen-Linien, dafuer sicher sichtbar).
        if (polylineCount == 0 && decoded != null)
        {
            try
            {
                var fallbackContour = Ai.Pipeline.SamMaskRenderer.ExtractContourGeometry(decoded, imgW, imgH, cw, ch);
                var fallbackPath = new System.Windows.Shapes.Path
                {
                    Data = fallbackContour,
                    Stroke = new SolidColorBrush(Color.FromRgb(0, 255, 0)),
                    StrokeThickness = 3,
                    Tag = "sam_manual_mask",
                    IsHitTestVisible = false,
                    Effect = new System.Windows.Media.Effects.DropShadowEffect
                    {
                        BlurRadius = 14, ShadowDepth = 0,
                        Color = Color.FromRgb(0, 255, 0), Opacity = 1.0
                    }
                };
                Panel.SetZIndex(fallbackPath, 102);
                OverlayCanvas.Children.Add(fallbackPath);
                contourRectRendered = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CodingMode SAM] Fallback-Kontur Fehler: {ex.Message}");
            }
        }

        SetStatusSafe($"SAM rendered: {polylineCount} Konturen, Fill={fillRendered}, FallbackKontur={contourRectRendered}");

        // 2. Tight-BBox als zusaetzlicher gruener Rahmen - garantiert sichtbar selbst wenn
        // die Polygon-Fuellung leer ist (Edge-Case bei sehr kleinen Masken).
        if (mask.Bbox is { Count: 4 } b)
        {
            double bx1 = b[0] / imgW * cw;
            double by1 = b[1] / imgH * ch;
            double bx2 = b[2] / imgW * cw;
            double by2 = b[3] / imgH * ch;

            var rect = new System.Windows.Shapes.Rectangle
            {
                Width = Math.Max(2, bx2 - bx1),
                Height = Math.Max(2, by2 - by1),
                Stroke = new SolidColorBrush(Color.FromRgb(57, 255, 20)),
                StrokeThickness = 3,
                StrokeDashArray = new DoubleCollection(new double[] { 6, 3 }),
                Fill = Brushes.Transparent,
                Tag = "sam_manual_mask",
                IsHitTestVisible = false,
                Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    BlurRadius = 10, ShadowDepth = 0,
                    Color = Color.FromRgb(57, 255, 20), Opacity = 0.9
                }
            };
            Canvas.SetLeft(rect, bx1);
            Canvas.SetTop(rect, by1);
            Panel.SetZIndex(rect, 103);
            OverlayCanvas.Children.Add(rect);

            // Funktional aussagekraefiges Label DIREKT am Maskenzentrum:
            //   Zeile 1: was SAM erkannt hat (Label oder fallback "Region")
            //   Zeile 2: Pixel-Anzahl + Bildanteil + Konfidenz
            // Wird spaeter durch Qwen-Klassifikation um den VSA-Code ergaenzt
            // (siehe AppendToBboxResultPanel im Qwen-Pfad).
            var detectedClass = string.IsNullOrWhiteSpace(mask.Label) || mask.Label == "manual"
                ? "Region"
                : mask.Label;
            var imageAreaPct = mask.MaskAreaPixels * 100.0 / Math.Max(1, mask.ImageAreaPixels);

            var labelStack = new StackPanel
            {
                Tag = "sam_manual_mask",
                IsHitTestVisible = false
            };
            labelStack.Children.Add(new TextBlock
            {
                Text = detectedClass,
                Foreground = Brushes.White,
                Background = new SolidColorBrush(Color.FromArgb(220, 0, 0, 0)),
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                Padding = new Thickness(6, 2, 6, 2)
            });
            labelStack.Children.Add(new TextBlock
            {
                Text = $"{mask.MaskAreaPixels} px ({imageAreaPct:F1}%) · Konf {mask.Confidence:P0}",
                Foreground = Brushes.Black,
                Background = new SolidColorBrush(Color.FromRgb(57, 255, 20)),
                FontSize = 10,
                FontWeight = FontWeights.SemiBold,
                Padding = new Thickness(6, 1, 6, 1)
            });

            // Position am Centroid - dort wo SAM den Schwerpunkt sieht
            double cxLabel = mask.CentroidX / imgW * cw;
            double cyLabel = mask.CentroidY / imgH * ch;
            Canvas.SetLeft(labelStack, Math.Max(0, cxLabel - 60));
            Canvas.SetTop(labelStack, Math.Max(0, cyLabel - 24));
            Panel.SetZIndex(labelStack, 104);
            OverlayCanvas.Children.Add(labelStack);
        }

        // 3. Pulsierende Aufmerksamkeits-Animation auf der Kontur (1.0 → 0.45 → 1.0, 2x)
        var pulseAnim = new System.Windows.Media.Animation.DoubleAnimation
        {
            From = 1.0, To = 0.45,
            Duration = TimeSpan.FromMilliseconds(380),
            AutoReverse = true,
            RepeatBehavior = new System.Windows.Media.Animation.RepeatBehavior(2)
        };
        foreach (var el in OverlayCanvas.Children.OfType<UIElement>()
            .Where(c => c is FrameworkElement fe && (fe.Tag as string) == "sam_manual_mask"))
        {
            el.BeginAnimation(UIElement.OpacityProperty, pulseAnim);
        }
    }

    private async Task SegmentBboxWithSamAsync(OverlayGeometry overlay)
    {
        // Vorherige manuelle SAM-Maske entfernen (Tag-basiert) bevor wir neu anfangen.
        try
        {
            var prev = OverlayCanvas.Children.OfType<FrameworkElement>()
                .Where(el => (el.Tag as string) == "sam_manual_mask").ToList();
            foreach (var el in prev) OverlayCanvas.Children.Remove(el);
        }
        catch { }

        if (_sidecarClient is null)
        {
            SetStatusSafe("SAM: Sidecar-Client nicht initialisiert");
            return;
        }
        if (overlay.Points is null || overlay.Points.Count < 2)
        {
            SetStatusSafe("SAM: BBox-Punkte fehlen");
            return;
        }

        SetStatusSafe("SAM: laeuft...");

        try
        {
            var pngBytes = await CaptureCurrentFrameAsync();
            if (pngBytes is null || pngBytes.Length == 0)
            {
                SetStatusSafe("SAM: Frame-Capture leer (Video pausiert?)");
                return;
            }

            // Bild-Aufloesung dynamisch ermitteln
            int imgW = 1920, imgH = 1080;
            try
            {
                using var ms = new System.IO.MemoryStream(pngBytes);
                var decoder = System.Windows.Media.Imaging.BitmapDecoder.Create(
                    ms,
                    System.Windows.Media.Imaging.BitmapCreateOptions.PreservePixelFormat,
                    System.Windows.Media.Imaging.BitmapCacheOption.OnLoad);
                if (decoder.Frames.Count > 0)
                {
                    imgW = decoder.Frames[0].PixelWidth;
                    imgH = decoder.Frames[0].PixelHeight;
                }
            }
            catch { /* nutze Default */ }

            // BBox-Koordinaten umrechnen (Canvas-normiert -> Image-Pixel) analog PlayerWindow.
            double cw = Math.Max(1, OverlayCanvas.ActualWidth);
            double ch = Math.Max(1, OverlayCanvas.ActualHeight);
            double sx = imgW / cw;
            double sy = imgH / ch;

            double minNormX = overlay.Points.Min(p => p.X);
            double minNormY = overlay.Points.Min(p => p.Y);
            double maxNormX = overlay.Points.Max(p => p.X);
            double maxNormY = overlay.Points.Max(p => p.Y);

            double minX = (minNormX * cw) * sx;
            double minY = (minNormY * ch) * sy;
            double maxX = (maxNormX * cw) * sx;
            double maxY = (maxNormY * ch) * sy;

            if ((maxX - minX) < 4 || (maxY - minY) < 4)
            {
                SetStatusSafe($"SAM: BBox zu klein ({maxX - minX:F0}×{maxY - minY:F0} px)");
                System.Diagnostics.Debug.WriteLine($"[CodingMode SAM] BBox zu klein: {maxX - minX:F0}x{maxY - minY:F0}");
                return;
            }

            var b64 = Convert.ToBase64String(pngBytes);
            var boxes = new[] { new AuswertungPro.Next.Application.Ai.Pipeline.SamBoundingBox(minX, minY, maxX, maxY, "manual", 1.0) };
            int dn = 300;
            var samReq = new AuswertungPro.Next.Application.Ai.Pipeline.SamRequest(b64, boxes, PipeDiameterMm: dn);

            AuswertungPro.Next.Application.Ai.Pipeline.SamResponse? samResp;
            try
            {
                samResp = await _sidecarClient.SegmentSamAsync(samReq);
            }
            catch (Exception ex)
            {
                SetStatusSafe($"SAM-Fehler: {ex.Message}");
                await Dispatcher.InvokeAsync(() =>
                    ShowBboxResultPanel("SAM-Fehler", ex.Message, isError: true));
                System.Diagnostics.Debug.WriteLine($"[CodingMode SAM] Sidecar-Fehler: {ex.Message}");

                // Trotzdem Qwen aufrufen, damit der User wenigstens eine Klassifikation erhaelt.
                _ = ClassifyBboxWithQwenAsync(pngBytes,
                    (int)Math.Round(minX), (int)Math.Round(minY),
                    (int)Math.Round(maxX), (int)Math.Round(maxY),
                    imgW, imgH);
                return;
            }

            if (samResp is null || samResp.Masks.Count == 0)
            {
                SetStatusSafe("SAM: Keine Maske gefunden (leeres Ergebnis vom Sidecar)");
                await Dispatcher.InvokeAsync(() =>
                    ShowBboxResultPanel("SAM: keine Maske", "Sidecar lieferte 0 Regionen.\nQwen wird trotzdem versucht...", isError: true));
                _ = ClassifyBboxWithQwenAsync(pngBytes,
                    (int)Math.Round(minX), (int)Math.Round(minY),
                    (int)Math.Round(maxX), (int)Math.Round(maxY),
                    imgW, imgH);
                return;
            }

            _lastSamResult = samResp;

            // Tight-BBox aus erster Maske ableiten - in normierten Koordinaten 0..1.
            // SamMaskResult.Bbox ist IReadOnlyList<double> = [x1, y1, x2, y2] in Pixeln.
            var firstMask = samResp.Masks[0];
            if (firstMask.Bbox is { Count: 4 } b)
            {
                double tx1 = b[0] / Math.Max(1, samResp.ImageWidth);
                double ty1 = b[1] / Math.Max(1, samResp.ImageHeight);
                double tx2 = b[2] / Math.Max(1, samResp.ImageWidth);
                double ty2 = b[3] / Math.Max(1, samResp.ImageHeight);
                _lastSamTightBbox = new NormalizedBoundingBox
                {
                    XCenter = (tx1 + tx2) / 2.0,
                    YCenter = (ty1 + ty2) / 2.0,
                    Width = Math.Max(0, tx2 - tx1),
                    Height = Math.Max(0, ty2 - ty1),
                };
            }

            // Maske rendern - eigene, sehr deutliche Darstellung fuer manuelle BBox
            // (nicht den Live-AI-Renderer, der zu transparent ist und schwer zu sehen).
            await Dispatcher.InvokeAsync(() =>
            {
                Ai.Pipeline.SamMaskRenderer.ClearMasks(OverlayCanvas);
                RenderManualSamMaskHighlight(samResp, firstMask);
            });

            // Status-Text setzen, damit der Nutzer weiss dass SAM gelaufen ist.
            SetStatusSafe($"SAM-Maske: {samResp.Masks.Count} Region(en) in {samResp.InferenceTimeMs:F0} ms");

            System.Diagnostics.Debug.WriteLine(
                $"[CodingMode SAM] OK: {samResp.Masks.Count} Maske(n), {samResp.InferenceTimeMs:F0}ms");

            // Sichtbares Result-Panel mit SAM-Info anzeigen (am Bbox).
            await Dispatcher.InvokeAsync(() =>
            {
                ShowBboxResultPanel(
                    title: "SAM erkannt",
                    body: $"Region: {firstMask.MaskAreaPixels} px ({firstMask.MaskAreaPixels * 100.0 / Math.Max(1, firstMask.ImageAreaPixels):F1}% Bild)\n"
                        + $"Position: {firstMask.CentroidX:F0},{firstMask.CentroidY:F0}\n"
                        + $"Confidence: {firstMask.Confidence:P0}\n"
                        + $"SAM-Latenz: {samResp.InferenceTimeMs:F0} ms\n"
                        + "Qwen-Klassifikation laeuft...",
                    isError: false);
            });

            // Sofort danach Qwen aufrufen damit der User weiss WAS in der BBox ist.
            _ = ClassifyBboxWithQwenAsync(pngBytes,
                (int)Math.Round(minX), (int)Math.Round(minY),
                (int)Math.Round(maxX), (int)Math.Round(maxY),
                imgW, imgH);
        }
        catch (Exception ex)
        {
            SetStatusSafe($"SAM unerwarteter Fehler: {ex.Message}");
            await Dispatcher.InvokeAsync(() =>
                ShowBboxResultPanel("SAM-Fehler", ex.Message, isError: true));
            System.Diagnostics.Debug.WriteLine($"[CodingMode SAM] Unerwarteter Fehler: {ex.Message}");
        }
    }

    /// <summary>
    /// Klassifiziert den BBox-Inhalt mit Qwen-VL (was ist drin?). Schreibt das Ergebnis
    /// als VSA-Code + Klartext in das BBox-Result-Panel.
    /// </summary>
    private async Task ClassifyBboxWithQwenAsync(
        byte[] framePngBytes, int x1, int y1, int x2, int y2, int imgW, int imgH)
    {
        if (_enhancedVision is null)
        {
            await Dispatcher.InvokeAsync(() =>
                AppendToBboxResultPanel("Qwen nicht initialisiert (Ollama down?)", isError: true));
            return;
        }
        try
        {
            // BBox-Region aus dem Frame croppen
            var cropped = CropPngToBbox(framePngBytes, x1, y1, x2, y2, imgW, imgH);
            if (cropped is null)
            {
                await Dispatcher.InvokeAsync(() =>
                    AppendToBboxResultPanel("BBox-Crop fehlgeschlagen", isError: true));
                return;
            }

            var b64 = Convert.ToBase64String(cropped);
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var (analysis, _) = await _enhancedVision.AnalyzeWithEscalationAsync(
                b64, context: null);
            sw.Stop();

            await Dispatcher.InvokeAsync(() =>
            {
                if (!string.IsNullOrEmpty(analysis.Error))
                {
                    AppendToBboxResultPanel($"Qwen-Fehler: {analysis.Error}", isError: true);
                    return;
                }

                var sb = new System.Text.StringBuilder();
                sb.AppendLine();
                sb.AppendLine($"── Qwen ({sw.ElapsedMilliseconds} ms) ──");
                if (analysis.Findings.Count == 0)
                {
                    sb.AppendLine("Keine Schadensmerkmale erkannt.");
                    sb.AppendLine($"Bildqualitaet: {analysis.ImageQuality}");
                    sb.AppendLine($"Material: {analysis.PipeMaterial}");
                    if (analysis.PipeDiameterMm.HasValue)
                        sb.AppendLine($"DN geschaetzt: {analysis.PipeDiameterMm} mm");
                }
                else
                {
                    foreach (var f in analysis.Findings.Take(3))
                    {
                        sb.AppendLine($"• {f.Label}");
                        if (!string.IsNullOrEmpty(f.VsaCodeHint))
                            sb.AppendLine($"  Code: {f.VsaCodeHint}  | Sev: {f.Severity}");
                        if (!string.IsNullOrEmpty(f.PositionClock))
                            sb.AppendLine($"  Uhr: {f.PositionClock}");
                        if (f.WidthMm.HasValue || f.HeightMm.HasValue)
                            sb.AppendLine($"  Maße: {f.WidthMm}×{f.HeightMm} mm");
                    }
                }
                if (!string.IsNullOrWhiteSpace(_enhancedVision.LastPipelineWarning))
                    sb.AppendLine($"Warnung: {_enhancedVision.LastPipelineWarning}");
                AppendToBboxResultPanel(sb.ToString(), isError: false);
            });
        }
        catch (Exception ex)
        {
            await Dispatcher.InvokeAsync(() =>
                AppendToBboxResultPanel($"Qwen-Klassifikation Fehler: {ex.Message}", isError: true));
        }
    }

    /// <summary>Croppt einen PNG-Frame auf die BBox-Region und liefert das neue PNG zurueck.</summary>
    private byte[]? CropPngToBbox(byte[] pngBytes, int x1, int y1, int x2, int y2, int imgW, int imgH)
    {
        try
        {
            using var ms = new System.IO.MemoryStream(pngBytes);
            var decoder = System.Windows.Media.Imaging.BitmapDecoder.Create(
                ms,
                System.Windows.Media.Imaging.BitmapCreateOptions.PreservePixelFormat,
                System.Windows.Media.Imaging.BitmapCacheOption.OnLoad);
            if (decoder.Frames.Count == 0) return null;
            var src = decoder.Frames[0];

            x1 = Math.Clamp(x1, 0, imgW - 1);
            y1 = Math.Clamp(y1, 0, imgH - 1);
            x2 = Math.Clamp(x2, x1 + 1, imgW);
            y2 = Math.Clamp(y2, y1 + 1, imgH);
            var rect = new System.Windows.Int32Rect(x1, y1, x2 - x1, y2 - y1);
            var crop = new System.Windows.Media.Imaging.CroppedBitmap(src, rect);

            var enc = new System.Windows.Media.Imaging.PngBitmapEncoder();
            enc.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(crop));
            using var outMs = new System.IO.MemoryStream();
            enc.Save(outMs);
            return outMs.ToArray();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[CodingMode] CropPngToBbox: {ex.Message}");
            return null;
        }
    }

    // ── Sichtbares Result-Panel ueber dem BBox ─────────────────────────────

    private System.Windows.Controls.Border? _bboxResultPanel;
    private TextBlock? _bboxResultText;

    private void ShowBboxResultPanel(string title, string body, bool isError)
    {
        // Bestehendes Panel entfernen
        if (_bboxResultPanel != null && OverlayCanvas.Children.Contains(_bboxResultPanel))
            OverlayCanvas.Children.Remove(_bboxResultPanel);

        _bboxResultText = new TextBlock
        {
            FontFamily = new FontFamily("Consolas"),
            FontSize = 12,
            Foreground = Brushes.White,
            Text = $"{title}\n{body}",
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 360
        };

        _bboxResultPanel = new System.Windows.Controls.Border
        {
            Background = isError
                ? new SolidColorBrush(Color.FromArgb(240, 180, 30, 30))
                : new SolidColorBrush(Color.FromArgb(240, 20, 30, 50)),
            BorderBrush = new SolidColorBrush(isError
                ? Color.FromRgb(255, 100, 100)
                : Color.FromRgb(0, 220, 200)),
            BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(10, 6, 10, 6),
            Child = _bboxResultText,
            Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                BlurRadius = 12, ShadowDepth = 0,
                Color = isError ? Colors.Red : Color.FromRgb(0, 220, 200),
                Opacity = 0.7
            },
            Tag = "sam_manual_mask",
            IsHitTestVisible = false
        };
        // Position: oben rechts auf Canvas
        Canvas.SetLeft(_bboxResultPanel, OverlayCanvas.ActualWidth - 380);
        Canvas.SetTop(_bboxResultPanel, 12);
        OverlayCanvas.Children.Add(_bboxResultPanel);
    }

    private void AppendToBboxResultPanel(string moreText, bool isError)
    {
        if (_bboxResultText == null) return;
        _bboxResultText.Text += "\n" + moreText;
        if (isError && _bboxResultPanel != null)
            _bboxResultPanel.BorderBrush = new SolidColorBrush(Color.FromRgb(255, 100, 100));
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
        // Gemeinsamer Resolver: VsaCodeHint normalisieren, bei Fehlschlag Label-Heuristik
        VsaCode = AuswertungPro.Next.Application.Ai.VsaCodeResolver.NormalizeFindingCode(f.VsaCodeHint)
                   ?? AuswertungPro.Next.Application.Ai.VsaCodeResolver.InferCodeFromLabel(f.Label)
                   ?? "";
        Severity = f.Severity;
        SeverityText = f.Severity.ToString();

        // VSA-Klartext aus Katalog (z.B. "BCAEB" → "Seitl. Anschluss, einmuendend, Bogen")
        Description = AuswertungPro.Next.Application.Ai.VsaCodeResolver.LookupLabel(VsaCode) ?? f.Label;

        // Position: Meter + Uhrzeit zusammengefasst
        var posParts = new List<string>();
        var normalizedClock = AuswertungPro.Next.Application.Ai.VsaCodeResolver.NormalizeClock(f.PositionClock);
        if (!string.IsNullOrWhiteSpace(normalizedClock))
            posParts.Add(normalizedClock);
        if (f.ExtentPercent.HasValue)
            posParts.Add($"{f.ExtentPercent}%");
        if (f.HeightMm is > 0)
            posParts.Add($"H:{f.HeightMm}mm");
        if (f.WidthMm is > 0)
            posParts.Add($"B:{f.WidthMm}mm");
        PositionText = posParts.Count > 0 ? string.Join(" · ", posParts) : "";

        // Detail-Text (fuer Tooltip und DetailPanel)
        var detailParts = new List<string>();
        if (!string.IsNullOrWhiteSpace(normalizedClock))
            detailParts.Add($"Uhr {normalizedClock}");
        if (f.ExtentPercent.HasValue)
            detailParts.Add($"Umfang {f.ExtentPercent}%");
        if (f.HeightMm is > 0)
            detailParts.Add($"H:{f.HeightMm}mm");
        if (f.WidthMm is > 0)
            detailParts.Add($"B:{f.WidthMm}mm");
        if (f.IntrusionPercent is > 0)
            detailParts.Add($"Einragung {f.IntrusionPercent}%");
        DetailText = detailParts.Count > 0 ? string.Join("  |  ", detailParts) : "Keine Details";

        // Confidence
        ConfidencePercent = f.Severity * 20; // Severity 1-5 → 20-100%
        ConfidenceText = $"{ConfidencePercent}%";

        // Tooltip: Alles zusammen
        FullTooltip = $"{VsaCode} {Description}\n{DetailText}\nSeverity: {Severity}/5";

        var severityColor = f.Severity switch
        {
            5 => Color.FromRgb(0xEF, 0x44, 0x44), // Rot (kritisch)
            4 => Color.FromRgb(0xF9, 0x73, 0x16), // Orange (schwer)
            3 => Color.FromRgb(0xF5, 0x9E, 0x0B), // Gelb (mittel)
            2 => Color.FromRgb(0x22, 0xC5, 0x5E), // Gruen (leicht)
            _ => Color.FromRgb(0x94, 0xA3, 0xB8)  // Grau (kaum)
        };
        SeverityBrush = new SolidColorBrush(severityColor);

        // Confidence-Farbe: Gruen >=85%, Gelb 60-85%, Rot <60%
        ConfidenceBrush = new SolidColorBrush(ConfidencePercent >= 85
            ? Color.FromRgb(0x22, 0xC5, 0x5E)
            : ConfidencePercent >= 60
                ? Color.FromRgb(0xF5, 0x9E, 0x0B)
                : Color.FromRgb(0xEF, 0x44, 0x44));
    }

    public string Label { get; }
    public string VsaCode { get; }
    public string Description { get; }
    public int Severity { get; }
    public string SeverityText { get; }
    public string DetailText { get; }
    public string PositionText { get; }
    public int ConfidencePercent { get; }
    public string ConfidenceText { get; }
    public string FullTooltip { get; }
    public SolidColorBrush SeverityBrush { get; }
    public SolidColorBrush ConfidenceBrush { get; }
}
