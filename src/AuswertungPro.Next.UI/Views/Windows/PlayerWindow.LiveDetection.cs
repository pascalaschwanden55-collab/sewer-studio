using System;
using AuswertungPro.Next.Application.Ai.Vision;
using AuswertungPro.Next.Domain.Ai.Vision;
using AuswertungPro.Next.Infrastructure.Ai.Ollama;
using AuswertungPro.Next.Application.Ai;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.Infrastructure.Ai;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.Application.Ai.Teacher;
using AuswertungPro.Next.Application.Common;

namespace AuswertungPro.Next.UI.Views.Windows;

// Phase 6.1.E Sub-A: Live-Detection-Lifecycle (Cluster B3, Teil 1).
//
// Lebenszyklus der Live-KI-Erkennung (5s-Timer + Snapshot + LiveDetectionService):
//   - LiveDetection_Click (Toggle-Button-Handler)
//   - StartLiveDetectionAsync / StopLiveDetection
//   - DetectionTimer_Tick + RunDetectionAsync (Inferenz-Loop)
//   - CaptureCurrentFrameAsync (640px-Snapshot fuer Live-Loop)
//   - UpdateDetectionStatus (Status-Text-Update)
//   - ClearDetectionOverlays (Canvas leeren)
//
// Status-Badges (SetLiveDetectionBadge, SetYoloStatus) — UI-State-Updates
// fuer das KI-Statusfeld.
//
// Felder im Hauptpartial: _liveDetectionClient, _liveDetectionService,
//   _detectionTimer, _detectionCts, _isDetecting, _isDetectionInFlight,
//   _liveDetectionModelName, _currentFindings, _detectionPendingFindings,
//   _detectionPendingFrameBytes, _detectionPendingTimestampSec,
//   _lastDetectionTimestamp, _isWindowClosed, _isManualMarkMode, _player.
public partial class PlayerWindow
{
    // ─── Status-Badges ─────────────────────────────────────────────────────────

    private void SetLiveDetectionBadge(string status, Color dotColor, string? stage = null)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.Invoke(() => SetLiveDetectionBadge(status, dotColor, stage));
            return;
        }

        var stageSuffix = string.IsNullOrWhiteSpace(stage) ? string.Empty : $" | {stage}";
        AiStatusBadge.Visibility = Visibility.Visible;
        AiStatusText.Text = $"{status}{stageSuffix}";
        AiStatusDot.Fill = new SolidColorBrush(dotColor);
    }

    private void SetYoloStatus(string text, Color dotColor, string? model = null)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.Invoke(() => SetYoloStatus(text, dotColor, model));
            return;
        }

        YoloStatusBar.Visibility = Visibility.Visible;
        TxtYoloStatus.Text = $"YOLO: {text}";
        YoloDot.Fill = new SolidColorBrush(dotColor);
        TxtYoloModel.Text = model ?? string.Empty;
    }

    // ─── Live-Detection Lifecycle ──────────────────────────────────────────────

    private async void LiveDetection_Click(object sender, RoutedEventArgs e)
    {
        if (_isDetecting)
        {
            StopLiveDetection();
            LiveDetectionButton.IsChecked = false;
            return;
        }

        await StartLiveDetectionAsync();
    }

    private async Task StartLiveDetectionAsync()
    {
        AiRuntimeConfig cfg;
        try { cfg = AuswertungPro.Next.Application.Ai.AiRuntimeConfigProvider.Load(); }
        catch
        {
            MessageBox.Show("KI-Konfiguration konnte nicht geladen werden.", "Live-KI",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            LiveDetectionButton.IsChecked = false;
            return;
        }

        if (!cfg.Enabled)
        {
            MessageBox.Show("KI ist deaktiviert. Bitte in den Einstellungen aktivieren.", "Live-KI",
                MessageBoxButton.OK, MessageBoxImage.Information);
            LiveDetectionButton.IsChecked = false;
            return;
        }

        try
        {
            var client = new OllamaClient(cfg.OllamaBaseUri,
                ownedTimeout: cfg.OllamaRequestTimeout > TimeSpan.Zero ? cfg.OllamaRequestTimeout : TimeSpan.FromMinutes(10),
                keepAlive: cfg.OllamaKeepAlive, numCtx: cfg.OllamaNumCtx);

            // Auto-detect vision model: check if configured model exists, fallback to first *vl* model
            var visionModel = cfg.VisionModel;
            try
            {
                var models = await client.ListModelNamesAsync(CancellationToken.None);
                bool configuredExists = false;
                string? fallbackVision = null;
                foreach (var m in models)
                {
                    if (m.StartsWith(visionModel, StringComparison.OrdinalIgnoreCase) ||
                        m.Equals(visionModel, StringComparison.OrdinalIgnoreCase))
                        configuredExists = true;
                    if (fallbackVision == null && m.Contains("vl", StringComparison.OrdinalIgnoreCase))
                        fallbackVision = m;
                }
                if (!configuredExists && fallbackVision != null)
                    visionModel = fallbackVision;
            }
            catch { /* use configured model */ }

            _liveDetectionClient = client;
            _liveDetectionService = new LiveDetectionService(client, visionModel);
            _liveDetectionModelName = visionModel;
            _detectionCts = new CancellationTokenSource();
            _isDetecting = true;

            // Show overlay layer
            DetectionOverlayGrid.Visibility = Visibility.Visible;
            SetLiveDetectionBadge("KI aktiv", Color.FromRgb(0x22, 0xC5, 0x5E),
                $"Modell: {CompactModelName(visionModel)}");
            SetYoloStatus("Aktiv", Color.FromRgb(0x22, 0xC5, 0x5E), CompactModelName(visionModel));

            LiveDetectionStatusText.Visibility = Visibility.Visible;
            LiveDetectionStatusText.Text = "Warte auf Frame...";

            _detectionTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            _detectionTimer.Tick += DetectionTimer_Tick;
            _detectionTimer.Start();

            // Run first detection immediately
            RunDetectionAsync().SafeFireAndForget("LiveDetection");
        }
        catch (Exception ex)
        {
            LiveDetectionButton.IsChecked = false;
            MessageBox.Show($"Live-KI konnte nicht gestartet werden: {ex.Message}", "Live-KI",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void StopLiveDetection()
    {
        _detectionTimer?.Stop();
        _detectionTimer = null;
        _detectionCts?.Cancel();
        _detectionCts?.Dispose();
        _detectionCts = null;
        _isDetecting = false;
        _isDetectionInFlight = false;
        _liveDetectionService = null;
        _liveDetectionClient?.Dispose();
        _liveDetectionClient = null;
        _liveDetectionModelName = string.Empty;

        // Hide overlay layer (unless manual mark mode is still active)
        if (!_isManualMarkMode)
            DetectionOverlayGrid.Visibility = Visibility.Collapsed;
        AiStatusBadge.Visibility = Visibility.Collapsed;
        SetYoloStatus("Gestoppt", Color.FromRgb(0x94, 0xA3, 0xB8));
        DetectionCanvas.Children.Clear();
        FindingSummaryPanel.Visibility = Visibility.Collapsed;
        _currentFindings.Clear();

        // Fertig-Meldung mit Zusammenfassung
        int totalEvents = _codingVm?.Events?.Count ?? 0;
        LiveDetectionStatusText.Text = $"KI-Analyse beendet — {totalEvents} Beobachtungen";
        LiveDetectionStatusText.Visibility = Visibility.Visible;

        // Video pausieren damit der User die Meldung sieht
        if (_player != null && _player.IsPlaying)
            _player.SetPause(true);

        var hideTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        hideTimer.Tick += (_, _) =>
        {
            hideTimer.Stop();
            if (!_isDetecting)
                LiveDetectionStatusText.Visibility = Visibility.Collapsed;
        };
        hideTimer.Start();
    }

    private async void DetectionTimer_Tick(object? sender, EventArgs e)
    {
        // Window bereits geschlossen waehrend Tick im Flug: sofort raus,
        // sonst greift RunDetectionAsync auf disposed _player/Canvas zu.
        if (_isWindowClosed) return;
        try
        {
            await RunDetectionAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PlayerWindow] DetectionTimer_Tick Fehler: {ex.Message}");
        }
    }

    private async Task RunDetectionAsync()
    {
        if (_isDetectionInFlight || _liveDetectionService is null || _detectionCts is null)
            return;
        if (!_player.IsPlaying)
            return;
        // Keine neue Analyse waehrend User-Bestaetigung
        if (_detectionPendingFindings != null)
            return;

        _isDetectionInFlight = true;
        SetLiveDetectionBadge("KI aktiv", Color.FromRgb(0xF5, 0x9E, 0x0B),
            $"{CompactModelName(_liveDetectionModelName)} | Snapshot");

        try
        {
            var snapshot = await CaptureCurrentFrameAsync();
            if (snapshot is null)
            {
                _isDetectionInFlight = false;
                SetLiveDetectionBadge("KI aktiv", Color.FromRgb(0x22, 0xC5, 0x5E),
                    $"{CompactModelName(_liveDetectionModelName)} | Bereit");
                return;
            }

            SetLiveDetectionBadge("KI aktiv", Color.FromRgb(0xF5, 0x9E, 0x0B),
                $"{CompactModelName(_liveDetectionModelName)} | Inferenz");
            var timestampSec = _player.Time / 1000.0;
            var result = await _liveDetectionService.AnalyzeFrameAsync(
                snapshot, timestampSec, _detectionCts.Token).ConfigureAwait(false);

            Dispatcher.Invoke(() =>
            {
                if (!_isDetecting) return;

                _lastDetectionTimestamp = result.TimestampSeconds;
                _currentFindings.Clear();
                _currentFindings.AddRange(result.Findings);

                RenderDetectionOverlay(result.Findings, result.TimestampSeconds);
                UpdateDetectionStatus(result);

                SetLiveDetectionBadge("KI aktiv", Color.FromRgb(0x22, 0xC5, 0x5E),
                    $"{CompactModelName(_liveDetectionModelName)} | Overlay");

                // Auto-Pause bei relevanten Befunden (Severity >= 2)
                var significantFindings = result.Findings
                    .Where(f => f.Severity >= 2).ToList();
                if (significantFindings.Count > 0)
                {
                    _detectionPendingFindings = significantFindings;
                    _detectionPendingFrameBytes = snapshot;
                    _detectionPendingTimestampSec = result.TimestampSeconds;
                    ShowDetectionConfirmation(significantFindings);
                    SetLiveDetectionBadge("Befund erkannt", Color.FromRgb(0xF5, 0x9E, 0x0B),
                        $"{CompactModelName(_liveDetectionModelName)} | Warte auf Bestaetigung");
                }
            });
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            var msg = ex.Message;
            if (msg.Length > 200) msg = msg[..200] + "...";
            Dispatcher.Invoke(() =>
            {
                LiveDetectionStatusText.Text = $"Fehler: {msg}";
                SetLiveDetectionBadge("KI Fehler", Color.FromRgb(0xEF, 0x44, 0x44),
                    CompactModelName(_liveDetectionModelName));
            });
        }
        finally
        {
            _isDetectionInFlight = false;
        }
    }

    private async Task<byte[]?> CaptureCurrentFrameAsync()
    {
        var tempPath = Path.Combine(
            Path.GetTempPath(),
            $"sewer_live_{Guid.NewGuid():N}.png");
        try
        {
            var success = TakeSnapshotSafe(tempPath, 640);
            if (!success)
                return null;

            // Wait briefly for file write
            await Task.Delay(80);

            if (!File.Exists(tempPath))
                return null;

            return await File.ReadAllBytesAsync(tempPath,
                _detectionCts?.Token ?? CancellationToken.None);
        }
        catch
        {
            return null;
        }
        finally
        {
            try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { }
        }
    }

    private void UpdateDetectionStatus(LiveDetection result)
    {
        if (result.Error is not null)
        {
            LiveDetectionStatusText.Text = $"Fehler: {result.Error}";
            return;
        }

        var count = result.Findings.Count;
        LiveDetectionStatusText.Text = count > 0
            ? $"{count} Befund(e) erkannt @ {result.TimestampSeconds:0.0}s"
            : $"Kein Befund @ {result.TimestampSeconds:0.0}s";

        if (count > 0)
        {
            var summary = string.Join(" | ",
                result.Findings.Take(3).Select(f =>
                    $"{f.VsaCodeHint ?? f.Label} (S{f.Severity})"));
            FindingSummaryPanel.Visibility = Visibility.Visible;
            FindingSummaryText.Text = summary;
        }
        else
        {
            FindingSummaryPanel.Visibility = Visibility.Collapsed;
        }
    }

    private void ClearDetectionOverlays()
    {
        DetectionCanvas.Children.Clear();
        DetectionOverlayGrid.Visibility = Visibility.Collapsed;
        CodingFindingsList.ItemsSource = null;
    }

    // ─── Detection-Overlay-Rendering (Bbox + Ring-Sektor-Fallback) ────────────

    private void RenderDetectionOverlay(IReadOnlyList<LiveFrameFinding> findings, double timestampSec)
    {
        DetectionCanvas.Children.Clear();

        var canvasWidth = DetectionCanvas.ActualWidth;
        var canvasHeight = DetectionCanvas.ActualHeight;
        if (canvasWidth < 60 || canvasHeight < 60)
            return;

        if (findings.Count == 0)
            return;

        // Tatsaechliche Videoflaeche berechnen (Aspect-Ratio-korrigiert)
        var (offX, offY, width, height) = GetVideoViewRenderRect();

        // Pruefen ob mindestens ein Finding Bbox hat
        bool hasBbox = findings.Any(f => f.BboxX1.HasValue && f.BboxY1.HasValue
                                       && f.BboxX2.HasValue && f.BboxY2.HasValue);

        // Wenn keine Bboxes: Fallback auf Ring-Sektor-Darstellung
        if (!hasBbox)
        {
            RenderRingSectorOverlay(findings, timestampSec, width, height);
            return;
        }

        // ── Bbox-basiertes Rendering: Rechtecke + Labels direkt auf dem Bild ──
        for (var i = 0; i < findings.Count && i < 8; i++)
        {
            var finding = findings[i];
            var color = MapDetectionSeverityColor(finding.Severity);

            if (finding.BboxX1.HasValue && finding.BboxY1.HasValue
                && finding.BboxX2.HasValue && finding.BboxY2.HasValue)
            {
                var px1 = offX + finding.BboxX1.Value * width;
                var py1 = offY + finding.BboxY1.Value * height;
                var px2 = offX + finding.BboxX2.Value * width;
                var py2 = offY + finding.BboxY2.Value * height;

                var rectLeft = Math.Min(px1, px2);
                var rectTop = Math.Min(py1, py2);
                var rectW = Math.Max(1, Math.Abs(px2 - px1));
                var rectH = Math.Max(1, Math.Abs(py2 - py1));

                // Farbiges Rechteck (halbtransparent gefuellt, farbiger Rand)
                var rect = new System.Windows.Shapes.Rectangle
                {
                    Width = rectW,
                    Height = rectH,
                    Stroke = new SolidColorBrush(Color.FromArgb(220, color.R, color.G, color.B)),
                    StrokeThickness = 2.5,
                    Fill = new SolidColorBrush(Color.FromArgb(35, color.R, color.G, color.B)),
                    RadiusX = 4,
                    RadiusY = 4,
                    IsHitTestVisible = false
                };
                Canvas.SetLeft(rect, rectLeft);
                Canvas.SetTop(rect, rectTop);
                DetectionCanvas.Children.Add(rect);

                // Label-Badge oben am Rechteck
                var labelText = $"{finding.VsaCodeHint ?? finding.Label} [S{finding.Severity}]";
                if (finding.ExtentPercent is > 0)
                    labelText += $" {finding.ExtentPercent}%";

                var label = new Border
                {
                    Background = new SolidColorBrush(Color.FromArgb(210, color.R, color.G, color.B)),
                    CornerRadius = new CornerRadius(3),
                    Padding = new Thickness(6, 2, 6, 2),
                    Cursor = Cursors.Hand,
                    IsHitTestVisible = true,
                    Child = new TextBlock
                    {
                        Text = labelText,
                        FontSize = 12,
                        FontWeight = FontWeights.SemiBold,
                        Foreground = Brushes.White
                    }
                };

                var capturedFinding = finding;
                var capturedTimestamp = timestampSec;
                label.MouseLeftButtonDown += (_, _) => OnFindingClicked(capturedFinding, capturedTimestamp);
                label.ToolTip = $"Klick: Schadenscode zuweisen\n{finding.Label}"
                    + (finding.VsaCodeHint != null ? $"\nVorschlag: {finding.VsaCodeHint}" : "")
                    + $"\nSchwere: {finding.Severity}/5";

                label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                var desired = label.DesiredSize;
                var lx = Math.Clamp(rectLeft, 2, width - desired.Width - 2);
                var ly = Math.Clamp(rectTop - desired.Height - 4, 2, height - desired.Height - 2);
                Canvas.SetLeft(label, lx);
                Canvas.SetTop(label, ly);
                DetectionCanvas.Children.Add(label);
            }
            else
            {
                // Einzelnes Finding ohne Bbox → Ring-Sektor-Fallback
                RenderRingSectorFinding(finding, i, findings.Count, width, height, timestampSec);
            }
        }
    }

    /// <summary>
    /// Fallback: Ring-Sektor-Darstellung wenn keine Bounding Boxes verfuegbar.
    /// </summary>
    private void RenderRingSectorOverlay(IReadOnlyList<LiveFrameFinding> findings,
        double timestampSec, double width, double height)
    {
        var size = Math.Min(width, height) * 0.78;
        var cx = width / 2.0;
        var cy = height / 2.0;
        var ringOuter = size * 0.42;
        var ringInner = size * 0.28;

        // Aeusserer Fuehrungsring
        var guide = new System.Windows.Shapes.Ellipse
        {
            Width = ringOuter * 2, Height = ringOuter * 2,
            Stroke = new SolidColorBrush(Color.FromArgb(125, 197, 209, 134)),
            StrokeDashArray = new DoubleCollection { 3, 3 },
            StrokeThickness = 1.0, Fill = Brushes.Transparent, IsHitTestVisible = false
        };
        Canvas.SetLeft(guide, cx - ringOuter);
        Canvas.SetTop(guide, cy - ringOuter);
        DetectionCanvas.Children.Add(guide);

        // Innerer Fuehrungsring
        var guideInner = new System.Windows.Shapes.Ellipse
        {
            Width = ringInner * 2, Height = ringInner * 2,
            Stroke = new SolidColorBrush(Color.FromArgb(105, 197, 209, 134)),
            StrokeDashArray = new DoubleCollection { 3, 3 },
            StrokeThickness = 0.9, Fill = Brushes.Transparent, IsHitTestVisible = false
        };
        Canvas.SetLeft(guideInner, cx - ringInner);
        Canvas.SetTop(guideInner, cy - ringInner);
        DetectionCanvas.Children.Add(guideInner);

        // Uhr-Teilstriche
        for (var hour = 1; hour <= 12; hour++)
        {
            var angleDeg = -90 + (hour % 12) * 30;
            var rad = DegToRad(angleDeg);
            DetectionCanvas.Children.Add(new System.Windows.Shapes.Line
            {
                X1 = cx + Math.Cos(rad) * (ringInner - 4),
                Y1 = cy + Math.Sin(rad) * (ringInner - 4),
                X2 = cx + Math.Cos(rad) * (ringOuter + 4),
                Y2 = cy + Math.Sin(rad) * (ringOuter + 4),
                Stroke = new SolidColorBrush(Color.FromArgb(65, 227, 227, 201)),
                StrokeThickness = 0.8, IsHitTestVisible = false
            });
        }

        for (var i = 0; i < findings.Count && i < 8; i++)
            RenderRingSectorFinding(findings[i], i, findings.Count, width, height, timestampSec);
    }

    /// <summary>
    /// Rendert ein einzelnes Finding als Ring-Sektor (Fallback ohne Bbox).
    /// </summary>
    private void RenderRingSectorFinding(LiveFrameFinding finding, int index, int total,
        double width, double height, double timestampSec)
    {
        var size = Math.Min(width, height) * 0.78;
        var cx = width / 2.0;
        var cy = height / 2.0;
        var ringOuter = size * 0.42;
        var ringInner = size * 0.28;

        var parsedClock = ParseClockHour(finding.PositionClock);
        var centerDeg = parsedClock.HasValue
            ? -90 + (parsedClock.Value % 12) * 30
            : -90 + index * (360.0 / total);

        var sweep = finding.ExtentPercent is > 0
            ? Math.Clamp(finding.ExtentPercent.Value * 3.6, 14.0, 160.0)
            : 18.0;

        var startDeg = centerDeg - sweep / 2.0;
        var color = MapDetectionSeverityColor(finding.Severity);

        var sector = new System.Windows.Shapes.Path
        {
            Data = BuildRingSectorGeometry(cx, cy, ringInner, ringOuter, startDeg, sweep),
            Fill = new SolidColorBrush(Color.FromArgb(98, color.R, color.G, color.B)),
            Stroke = new SolidColorBrush(Color.FromArgb(220, color.R, color.G, color.B)),
            StrokeThickness = 1.0, IsHitTestVisible = false
        };
        DetectionCanvas.Children.Add(sector);

        // Severity-Punkt ausserhalb Ring
        var rad2 = DegToRad(centerDeg);
        var mx = cx + Math.Cos(rad2) * (ringOuter + 2);
        var my = cy + Math.Sin(rad2) * (ringOuter + 2);

        var dot = new System.Windows.Shapes.Ellipse
        {
            Width = 8, Height = 8,
            Fill = new SolidColorBrush(color),
            Stroke = Brushes.White, StrokeThickness = 0.8, IsHitTestVisible = false
        };
        Canvas.SetLeft(dot, mx - 4);
        Canvas.SetTop(dot, my - 4);
        DetectionCanvas.Children.Add(dot);

        // Label-Badge (klickbar)
        var labelText = BuildDetectionLabel(finding);
        var label = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(228, 17, 19, 24)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(210, color.R, color.G, color.B)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(5, 2, 5, 2),
            Cursor = Cursors.Hand,
            Child = new TextBlock
            {
                Text = labelText, FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(225, 234, 245))
            }
        };

        var capturedFinding = finding;
        var capturedTimestamp = timestampSec;
        label.MouseLeftButtonDown += (_, _) => OnFindingClicked(capturedFinding, capturedTimestamp);
        label.ToolTip = $"Klick: Schadenscode zuweisen\n{finding.Label}"
            + (finding.VsaCodeHint != null ? $"\nVorschlag: {finding.VsaCodeHint}" : "")
            + $"\nSchwere: {finding.Severity}/5";

        label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var desired = label.DesiredSize;
        var lx = Math.Cos(rad2) >= 0 ? mx + 8 : mx - desired.Width - 8;
        var ly = my - desired.Height / 2.0;
        Canvas.SetLeft(label, Math.Clamp(lx, 2, width - desired.Width - 2));
        Canvas.SetTop(label, Math.Clamp(ly, 2, height - desired.Height - 2));
        DetectionCanvas.Children.Add(label);
    }
    // === Sub-J: Detection-Confirmation + OnFindingClicked ===

    // ── LiveDetection Bestaetigungs-Logik ──────────────────────────
    private void ShowDetectionConfirmation(IReadOnlyList<LiveFrameFinding> findings)
    {
        if (findings.Count == 0) return;

        // Video pausieren und zur Fundstelle springen
        if (_player != null && _player.IsPlaying)
            _player.SetPause(true);

        // Zur Fundstelle springen (Timestamp aus dem analysierten Frame)
        if (_detectionPendingTimestampSec.HasValue && _player != null)
        {
            long targetMs = (long)(_detectionPendingTimestampSec.Value * 1000);
            _player.Time = targetMs;
        }

        // Zusammenfassung der Befunde
        var primary = findings[0];
        var severityText = primary.Severity switch
        {
            5 => "S5 kritisch",
            4 => "S4 schwer",
            3 => "S3 mittel",
            2 => "S2 leicht",
            _ => $"S{primary.Severity}"
        };

        TxtDetectionFinding.Text = findings.Count == 1
            ? $"KI-Erkennung: {primary.Label} ({severityText})"
            : $"KI-Erkennung: {findings.Count} Befunde — {primary.Label} ({severityText})";

        var details = new System.Text.StringBuilder();
        foreach (var f in findings)
        {
            if (details.Length > 0) details.Append("  |  ");
            details.Append($"{f.PositionClock ?? "?"} Uhr · {f.Label}");
            if (f.ExtentPercent.HasValue) details.Append($" · {f.ExtentPercent}%");
        }
        TxtDetectionDetail.Text = details.ToString();

        DetectionConfirmationPanel.Visibility = Visibility.Visible;
    }

    private void ResumeDetection()
    {
        _detectionPendingFindings = null;
        _detectionPendingFrameBytes = null;
        _detectionPendingTimestampSec = null;
        DetectionConfirmationPanel.Visibility = Visibility.Collapsed;

        // Video automatisch weiterlaufen lassen nach Entscheidung
        if (_player != null && !_player.IsPlaying)
            _player.Play();
    }

    private async void DetectionAccept_Click(object sender, RoutedEventArgs e)
    {
        if (_detectionPendingFindings == null || _detectionPendingFindings.Count == 0)
        {
            ResumeDetection();
            return;
        }

        try
        {
            var frameBytes = _detectionPendingFrameBytes;
            if (frameBytes == null || frameBytes.Length == 0)
            {
                frameBytes = await CaptureCurrentFrameAsync();
                if (frameBytes == null) { ResumeDetection(); return; }
            }

            var timestampSec = _detectionPendingTimestampSec ?? (_player.Time / 1000.0);
            var exportService = new Ai.Teacher.TrainingAnnotationExportService();

            foreach (var finding in _detectionPendingFindings)
            {
                var code = finding.VsaCodeHint ?? finding.Label;
                int classId = AuswertungPro.Next.Application.Ai.Teacher.VsaYoloClassMap.GetClassId(code);
                var annotationId = Guid.NewGuid().ToString("N")[..12];
                var baseName = $"det_{annotationId}";

                // Bounding-Box aus Uhrposition ableiten (Ring-Sektor → normalisierte Koordinaten)
                var bbox = BBoxFromClockPosition(finding);

                // Frame temp speichern
                var tempFrame = System.IO.Path.Combine(
                    System.IO.Path.GetTempPath(), $"sewer_studio_det_{annotationId}.png");
                await System.IO.File.WriteAllBytesAsync(tempFrame, frameBytes);

                var exportResult = await exportService.ExportAsync(tempFrame, bbox, code, classId, baseName);
                try { System.IO.File.Delete(tempFrame); } catch { }

                // TeacherAnnotation erstellen
                var annotation = new AuswertungPro.Next.Application.Ai.Teacher.TeacherAnnotation
                {
                    AnnotationId = annotationId,
                    VsaCode = code,
                    Beschreibung = finding.Label,
                    MeterPosition = 0,
                    VideoTimestamp = TimeSpan.FromSeconds(timestampSec),
                    ToolType = OverlayToolType.None,
                    Points = new List<Domain.Models.NormalizedPoint>(),
                    BoundingBox = bbox,
                    ClockPosition = double.TryParse(finding.PositionClock, out var cp) ? cp : null,
                    FullFramePath = exportResult.FullFramePath,
                    CroppedRegionPath = exportResult.CroppedRegionPath,
                    YoloAnnotationPath = exportResult.YoloAnnotationPath,
                    WidthMm = finding.WidthMm,
                    HeightMm = finding.HeightMm
                };
                await AuswertungPro.Next.Application.Ai.Teacher.TeacherAnnotationStore.AppendAsync(annotation);
            }

            // Dezente Bestaetigung im OSD-Badge
            OsdMeterBadge.Visibility = Visibility.Visible;
            TxtOsdMeter.Text = $"✓ {_detectionPendingFindings.Count} Befund(e) gespeichert";

            var resetTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
            resetTimer.Tick += (_, _) =>
            {
                resetTimer.Stop();
                if (_codingLastOsdMeter.HasValue)
                    TxtOsdMeter.Text = $"{_codingLastOsdMeter.Value:F2}m (OSD)";
                else
                    OsdMeterBadge.Visibility = Visibility.Collapsed;
            };
            resetTimer.Start();
        }
        catch (Exception ex)
        {
            OsdMeterBadge.Visibility = Visibility.Visible;
            TxtOsdMeter.Text = $"✗ Fehler: {ex.Message}";
        }

        ResumeDetection();
    }

    private async void DetectionCorrect_Click(object sender, RoutedEventArgs e)
    {
        if (_detectionPendingFindings == null || _detectionPendingFindings.Count == 0)
        {
            ResumeDetection();
            return;
        }

        try
        {
            var timestampSec = _player.Time / 1000.0;

            // VsaCodeExplorer oeffnen fuer Korrektur — Meter aus OSD/Video
            var autoMeter2 = _codingLastOsdMeter ?? GetMeterFromVideoPosition();
            var entry = new ProtocolEntry();
            var explorerVm = new ViewModels.Windows.VsaCodeExplorerViewModel(entry, autoMeter2, TimeSpan.FromSeconds(timestampSec));
            var explorer = new Views.Windows.VsaCodeExplorerWindow(explorerVm, _videoPath, TimeSpan.FromSeconds(timestampSec))
            {
                Owner = this
            };

            if (explorer.ShowDialog() != true || explorer.SelectedEntry == null)
            {
                ResumeDetection();
                return;
            }

            var selectedEntry = explorer.SelectedEntry;

            var frameBytes = _detectionPendingFrameBytes;
            if (frameBytes == null || frameBytes.Length == 0)
            {
                frameBytes = await CaptureCurrentFrameAsync();
                if (frameBytes == null) { ResumeDetection(); return; }
            }

            var primary = _detectionPendingFindings[0];
            var timestampSecForFrame = _detectionPendingTimestampSec ?? timestampSec;
            var bbox = BBoxFromClockPosition(primary);

            int classId = AuswertungPro.Next.Application.Ai.Teacher.VsaYoloClassMap.GetClassId(selectedEntry.Code);
            var annotationId = Guid.NewGuid().ToString("N")[..12];
            var baseName = $"det_corr_{annotationId}";

            var tempFrame = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(), $"sewer_studio_det_{annotationId}.png");
            await System.IO.File.WriteAllBytesAsync(tempFrame, frameBytes);

            var exportService = new Ai.Teacher.TrainingAnnotationExportService();
            var exportResult = await exportService.ExportAsync(tempFrame, bbox, selectedEntry.Code, classId, baseName);
            try { System.IO.File.Delete(tempFrame); } catch { }

            var annotation = new AuswertungPro.Next.Application.Ai.Teacher.TeacherAnnotation
            {
                AnnotationId = annotationId,
                VsaCode = selectedEntry.Code,
                Beschreibung = selectedEntry.Beschreibung,
                MeterPosition = 0,
                VideoTimestamp = TimeSpan.FromSeconds(timestampSecForFrame),
                ToolType = OverlayToolType.None,
                Points = new List<Domain.Models.NormalizedPoint>(),
                BoundingBox = bbox,
                ClockPosition = double.TryParse(primary.PositionClock, out var cp) ? cp : null,
                FullFramePath = exportResult.FullFramePath,
                CroppedRegionPath = exportResult.CroppedRegionPath,
                YoloAnnotationPath = exportResult.YoloAnnotationPath,
                WidthMm = primary.WidthMm,
                HeightMm = primary.HeightMm
            };
            await AuswertungPro.Next.Application.Ai.Teacher.TeacherAnnotationStore.AppendAsync(annotation);

            OsdMeterBadge.Visibility = Visibility.Visible;
            TxtOsdMeter.Text = $"✓ Training: {selectedEntry.Code} (korrigiert)";

            var resetTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
            resetTimer.Tick += (_, _) =>
            {
                resetTimer.Stop();
                if (_codingLastOsdMeter.HasValue)
                    TxtOsdMeter.Text = $"{_codingLastOsdMeter.Value:F2}m (OSD)";
                else
                    OsdMeterBadge.Visibility = Visibility.Collapsed;
            };
            resetTimer.Start();
        }
        catch (Exception ex)
        {
            OsdMeterBadge.Visibility = Visibility.Visible;
            TxtOsdMeter.Text = $"✗ Fehler: {ex.Message}";
        }

        ResumeDetection();
    }

    private void DetectionSkip_Click(object sender, RoutedEventArgs e)
    {
        ResumeDetection();
    }

    /// <summary>
    /// Erzeugt eine grobe BoundingBox aus Uhrposition + Ausdehnung eines LiveFrameFinding.
    /// Mapping: Uhrposition → Kreissektor → normalisierte Box im Bild.
    /// </summary>
    private static Application.Ai.NormalizedBoundingBox BBoxFromClockPosition(LiveFrameFinding finding)
    {
        // Uhrzeiger → Winkel (12 Uhr = oben = -90°, dann im Uhrzeigersinn)
        double clockHour = 6; // Default: unten
        if (double.TryParse(finding.PositionClock, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var parsed))
            clockHour = parsed;

        double angleDeg = (clockHour / 12.0) * 360.0 - 90.0;
        double angleRad = angleDeg * Math.PI / 180.0;

        // Extent-basierte Groesse (% Umfang → Box-Groesse)
        double extent = (finding.ExtentPercent ?? 15) / 100.0;
        double boxSize = Math.Clamp(extent * 0.6, 0.08, 0.40);

        // Zentrum auf ~35% Radius vom Bildmittelpunkt
        double cx = 0.5 + 0.35 * Math.Cos(angleRad);
        double cy = 0.5 + 0.35 * Math.Sin(angleRad);

        return new Application.Ai.NormalizedBoundingBox
        {
            XCenter = Math.Clamp(cx, 0, 1),
            YCenter = Math.Clamp(cy, 0, 1),
            Width = Math.Clamp(boxSize, 0.08, 0.40),
            Height = Math.Clamp(boxSize, 0.08, 0.40)
        };
    }

    private void OnFindingClicked(LiveFrameFinding finding, double timestampSec)
    {
        _player.SetPause(true);
        OpenCodeCatalogForMark(
            finding.PositionClock,
            timestampSec,
            finding.VsaCodeHint);
    }
}