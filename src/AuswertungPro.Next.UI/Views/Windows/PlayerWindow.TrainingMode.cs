using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using IOPath = System.IO.Path;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.KnowledgeBase;
using AuswertungPro.Next.UI.Ai.Ollama;
using AuswertungPro.Next.UI.Ai.Pipeline;
using AuswertungPro.Next.UI.Ai.Teacher;
using AuswertungPro.Next.UI.Ai.Training;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.ViewModels.Windows;

namespace AuswertungPro.Next.UI.Views.Windows;

// Trainings-Modus: Laufendes Video anschauen, Rechteck um Schaden ziehen,
// VSA-Code auswaehlen, Sample landet als Trainingsbeispiel in der KB.
// Getrennt vom Codier-Modus (der fuehrt Protokoll-Eintraege ins Datagrid).
public partial class PlayerWindow
{
    private bool _isTrainingMode;

    // Drawing state
    private bool _trainingIsDragging;
    private Point _trainingDragStart;
    private System.Windows.Shapes.Rectangle? _trainingRectShape;

    // Letztes fertiges Rechteck (pixel coords auf TrainingOverlayCanvas)
    private Rect? _trainingCurrentRect;

    // KB state (lazy init)
    private KnowledgeBaseManager? _trainingKbManager;
    private KnowledgeBaseContext? _trainingKbCtx;
    private EmbeddingService? _trainingEmbedder;
    private System.Net.Http.HttpClient? _trainingHttp;

    // SAM state (lazy init) — fuer Box-Prompt-Segmentierung analog ImageAnnotationWindow
    private VisionPipelineClient? _trainingSidecar;
    private SamResponse? _trainingLastSamResult;
    private System.Threading.CancellationTokenSource? _trainingSamCts;

    private int _trainingSessionCount;

    private void TrainingMode_Click(object sender, RoutedEventArgs e)
    {
        if (_isTrainingMode)
        {
            ExitTrainingMode();
            TrainingModeButton.IsChecked = false;
        }
        else
        {
            if (_isCodingMode)
            {
                MessageBox.Show(
                    "Bitte zuerst den Codier-Modus beenden.",
                    "Trainings-Modus",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                TrainingModeButton.IsChecked = false;
                return;
            }
            EnterTrainingMode();
            TrainingModeButton.IsChecked = true;
        }
    }

    private void EnterTrainingMode()
    {
        if (_isTrainingMode) return;
        _isTrainingMode = true;

        // Video pausieren (User kann jederzeit wieder auf Play druecken)
        try { _player.SetPause(true); } catch { }

        // UI einblenden
        TrainingSidePanel.Visibility = Visibility.Visible;
        TrainingSidePanelColumn.Width = new GridLength(280);
        TrainingOverlayPopup.IsOpen = true;
        TrainingOverlayCanvas.IsHitTestVisible = true;
        UpdateTrainingOverlayViewport();
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded,
            new Action(UpdateTrainingOverlayViewport));

        ResetTrainingDrawing();
        TxtTrainingStatus.Text = "Bereit. Rechteck ziehen.";
        _trainingSessionCount = 0;
        TxtTrainingCount.Text = "0 Samples in dieser Sitzung";

        // SizeChanged-Hook (einmalig)
        VideoView.SizeChanged -= TrainingVideoView_SizeChanged;
        VideoView.SizeChanged += TrainingVideoView_SizeChanged;

        // Kein Deactivated-Handler mehr: Popup muss auch bei Snipping-Tool/Screenshot
        // sichtbar bleiben. Der fruehere Airspace-Schutz war zu aggressiv und hat
        // das BBox-Overlay beim Wechsel zu jedem System-Fenster ausgeblendet.
    }

    private void ExitTrainingMode()
    {
        if (!_isTrainingMode) return;
        _isTrainingMode = false;

        ResetTrainingDrawing();

        TrainingOverlayPopup.IsOpen = false;
        TrainingOverlayCanvas.IsHitTestVisible = false;
        TrainingSidePanel.Visibility = Visibility.Collapsed;
        TrainingSidePanelColumn.Width = new GridLength(0);

        VideoView.SizeChanged -= TrainingVideoView_SizeChanged;
    }

    private void TrainingVideoView_SizeChanged(object? sender, SizeChangedEventArgs e)
        => UpdateTrainingOverlayViewport();

    private void UpdateTrainingOverlayViewport()
    {
        var (offX, offY, w, h) = GetVideoViewRenderRect();
        if (double.IsNaN(w) || w <= 1 || double.IsNaN(h) || h <= 1)
            return;

        if (Math.Abs(TrainingOverlayCanvas.Width - w) > 0.5)
            TrainingOverlayCanvas.Width = w;
        if (Math.Abs(TrainingOverlayCanvas.Height - h) > 0.5)
            TrainingOverlayCanvas.Height = h;

        TrainingOverlayPopup.HorizontalOffset = offX;
        TrainingOverlayPopup.VerticalOffset = offY;
    }

    // --- Drawing Handlers ---

    private void TrainingCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!_isTrainingMode) return;

        // Sicherstellen dass Canvas korrekte Dimensionen hat (Race-Condition beim ersten Klick)
        UpdateTrainingOverlayViewport();
        var cw = TrainingOverlayCanvas.ActualWidth;
        var ch = TrainingOverlayCanvas.ActualHeight;
        if (cw <= 1 || ch <= 1)
        {
            TxtTrainingStatus.Text = "Video noch nicht bereit. Kurz warten.";
            return;
        }

        // Wenn Video laeuft: erst pausieren
        try { if (_player.IsPlaying) _player.SetPause(true); } catch { }

        // Altes Rechteck entfernen
        RemoveTrainingRectShape();

        _trainingIsDragging = true;
        var p = e.GetPosition(TrainingOverlayCanvas);
        p.X = Math.Max(0, Math.Min(cw, p.X));
        p.Y = Math.Max(0, Math.Min(ch, p.Y));
        _trainingDragStart = p;

        _trainingRectShape = new System.Windows.Shapes.Rectangle
        {
            Stroke = Brushes.Cyan,
            StrokeThickness = 2,
            Fill = new SolidColorBrush(Color.FromArgb(40, 0, 255, 255)),
            IsHitTestVisible = false
        };
        Canvas.SetLeft(_trainingRectShape, _trainingDragStart.X);
        Canvas.SetTop(_trainingRectShape, _trainingDragStart.Y);
        _trainingRectShape.Width = 0;
        _trainingRectShape.Height = 0;
        TrainingOverlayCanvas.Children.Add(_trainingRectShape);

        TrainingOverlayCanvas.CaptureMouse();
        TxtTrainingStatus.Text = "Rechteck aufziehen...";
    }

    private void TrainingCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_isTrainingMode || !_trainingIsDragging || _trainingRectShape is null) return;

        var cur = e.GetPosition(TrainingOverlayCanvas);
        var cw = TrainingOverlayCanvas.ActualWidth;
        var ch = TrainingOverlayCanvas.ActualHeight;
        cur.X = Math.Max(0, Math.Min(cw, cur.X));
        cur.Y = Math.Max(0, Math.Min(ch, cur.Y));

        double x = Math.Min(_trainingDragStart.X, cur.X);
        double y = Math.Min(_trainingDragStart.Y, cur.Y);
        double w = Math.Abs(cur.X - _trainingDragStart.X);
        double h = Math.Abs(cur.Y - _trainingDragStart.Y);

        Canvas.SetLeft(_trainingRectShape, x);
        Canvas.SetTop(_trainingRectShape, y);
        _trainingRectShape.Width = w;
        _trainingRectShape.Height = h;
    }

    private void TrainingCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isTrainingMode || !_trainingIsDragging) return;
        _trainingIsDragging = false;
        TrainingOverlayCanvas.ReleaseMouseCapture();

        if (_trainingRectShape is null)
        {
            TxtTrainingStatus.Text = "Abgebrochen.";
            return;
        }

        // Mindestgroesse pruefen
        if (_trainingRectShape.Width < 10 || _trainingRectShape.Height < 10)
        {
            RemoveTrainingRectShape();
            TxtTrainingStatus.Text = "Rechteck zu klein. Erneut ziehen.";
            BtnTrainingSave.IsEnabled = false;
            BtnTrainingClear.IsEnabled = false;
            return;
        }

        _trainingCurrentRect = new Rect(
            Canvas.GetLeft(_trainingRectShape),
            Canvas.GetTop(_trainingRectShape),
            _trainingRectShape.Width,
            _trainingRectShape.Height);

        TxtTrainingStatus.Text = "Rechteck fertig. 'Speichern' klicken, Code waehlen.";
        BtnTrainingSave.IsEnabled = true;
        BtnTrainingClear.IsEnabled = true;

        // SAM asynchron anstossen — Maske als Overlay visualisiert was erkannt wurde
        _ = RunTrainingSamAsync(_trainingCurrentRect.Value);
    }

    // --- SAM-Integration (Box-Prompt analog ImageAnnotationWindow) ---

    private async Task RunTrainingSamAsync(Rect rectPx)
    {
        try
        {
            _trainingSamCts?.Cancel();
            _trainingSamCts = new System.Threading.CancellationTokenSource();
            var ct = _trainingSamCts.Token;

            if (_trainingSidecar is null)
            {
                var url = Environment.GetEnvironmentVariable("SEWERSTUDIO_SIDECAR_URL")
                          ?? "http://localhost:8100";
                _trainingSidecar = new VisionPipelineClient(new Uri(url));
                var health = await _trainingSidecar.HealthCheckAsync().ConfigureAwait(true);
                if (health is null)
                {
                    TxtTrainingStatus.Text = "SAM offline — nur Rechteck wird gespeichert.";
                    _trainingSidecar = null;
                    return;
                }
            }

            var frameBytes = await CaptureCurrentFrameAsync().ConfigureAwait(true);
            if (frameBytes is null || frameBytes.Length == 0)
            {
                TxtTrainingStatus.Text = "Frame-Capture fehlgeschlagen — SAM ueberspringen.";
                return;
            }

            // Rechteck-Pixel (Canvas-Koordinaten) → Bild-Pixel skalieren
            // Frame-Bytes kommen vom Snapshot mit voller Aufloesung,
            // Canvas hat VideoView-Darstellungs-Groesse. Wir normalisieren.
            var cw = Math.Max(1, TrainingOverlayCanvas.ActualWidth);
            var ch = Math.Max(1, TrainingOverlayCanvas.ActualHeight);
            int imgW, imgH;
            try
            {
                using var ms = new MemoryStream(frameBytes);
                var dec = System.Windows.Media.Imaging.BitmapDecoder.Create(
                    ms, System.Windows.Media.Imaging.BitmapCreateOptions.None,
                    System.Windows.Media.Imaging.BitmapCacheOption.OnLoad);
                imgW = dec.Frames[0].PixelWidth;
                imgH = dec.Frames[0].PixelHeight;
            }
            catch { imgW = 640; imgH = 480; }

            double sx = imgW / cw;
            double sy = imgH / ch;
            double x1 = rectPx.X * sx;
            double y1 = rectPx.Y * sy;
            double x2 = (rectPx.X + rectPx.Width) * sx;
            double y2 = (rectPx.Y + rectPx.Height) * sy;

            var b64 = Convert.ToBase64String(frameBytes);
            var req = new SamRequest(b64, new[] { new SamBoundingBox(x1, y1, x2, y2, "training", 1.0) });

            TxtTrainingStatus.Text = "SAM segmentiert...";
            var resp = await _trainingSidecar.SegmentSamAsync(req, ct).ConfigureAwait(true);
            if (ct.IsCancellationRequested) return;

            _trainingLastSamResult = resp;

            if (resp?.Masks is { Count: > 0 })
            {
                // Masken-Overlay auf den TrainingCanvas zeichnen
                var quantified = resp.Masks
                    .Select(m => MaskQuantificationService.Quantify(m, resp.ImageWidth, resp.ImageHeight, 300))
                    .ToList();
                SamMaskRenderer.ClearMasks(TrainingOverlayCanvas);
                SamMaskRenderer.RenderMasks(
                    TrainingOverlayCanvas,
                    resp,
                    quantified,
                    TrainingOverlayCanvas.ActualWidth,
                    TrainingOverlayCanvas.ActualHeight);
                TxtTrainingStatus.Text = $"SAM: {resp.Masks.Count} Maske(n) ({resp.InferenceTimeMs:F0}ms). Speichern druecken.";
            }
            else
            {
                TxtTrainingStatus.Text = "SAM: keine Maske — nur Rechteck wird gespeichert.";
            }
        }
        catch (OperationCanceledException) { /* neues Rechteck hat altes abgebrochen */ }
        catch (Exception ex)
        {
            TxtTrainingStatus.Text = $"SAM Fehler: {ex.Message} — nur Rechteck wird gespeichert.";
        }
    }

    private void TrainingClear_Click(object sender, RoutedEventArgs e)
    {
        ResetTrainingDrawing();
        TxtTrainingStatus.Text = "Rechteck geloescht.";
    }

    private void ResetTrainingDrawing()
    {
        RemoveTrainingRectShape();
        _trainingCurrentRect = null;
        _trainingIsDragging = false;
        _trainingLastSamResult = null;
        _trainingSamCts?.Cancel();
        SamMaskRenderer.ClearMasks(TrainingOverlayCanvas);
        BtnTrainingSave.IsEnabled = false;
        BtnTrainingClear.IsEnabled = false;
    }

    private void RemoveTrainingRectShape()
    {
        if (_trainingRectShape != null && TrainingOverlayCanvas.Children.Contains(_trainingRectShape))
            TrainingOverlayCanvas.Children.Remove(_trainingRectShape);
        _trainingRectShape = null;
    }

    // --- Save Logic ---

    private async void TrainingSave_Click(object sender, RoutedEventArgs e)
    {
        if (!_isTrainingMode || _trainingCurrentRect is null)
        {
            TxtTrainingStatus.Text = "Kein Rechteck gezeichnet.";
            return;
        }

        try
        {
            BtnTrainingSave.IsEnabled = false;
            TxtTrainingStatus.Text = "Code waehlen...";

            // 1. Code-Auswahl via VsaCodeExplorer
            // WICHTIG: Topmost setzen, damit VsaCodeExplorerWindow ueber dem
            // Transparenz-Popup liegt (sonst blockiert das BBox-Overlay die Eingabe).
            // BBox bleibt dadurch waehrend Codierung sichtbar.
            var videoTime = TimeSpan.FromMilliseconds(Math.Max(0, _player.Time));
            var autoMeter = GetTrainingMeterValue() ?? GetMeterFromVideoPosition();
            var entry = new AuswertungPro.Next.Domain.Protocol.ProtocolEntry();
            var explorerVm = new VsaCodeExplorerViewModel(entry, autoMeter, videoTime);
            var explorer = new VsaCodeExplorerWindow(explorerVm, _videoPath, videoTime)
            {
                Owner = this,
                Topmost = true
            };

            if (explorer.ShowDialog() != true || explorer.SelectedEntry == null)
            {
                TxtTrainingStatus.Text = "Code-Auswahl abgebrochen. Rechteck bleibt.";
                BtnTrainingSave.IsEnabled = true;
                return;
            }

            var selectedEntry = explorer.SelectedEntry;
            var code = (selectedEntry.Code ?? "").Trim().ToUpperInvariant();
            if (string.IsNullOrWhiteSpace(code))
            {
                TxtTrainingStatus.Text = "Kein Code gewaehlt.";
                BtnTrainingSave.IsEnabled = true;
                return;
            }

            TxtTrainingStatus.Text = $"Speichere {code}...";

            // 2. Frame capturen
            var frameBytes = await CaptureCurrentFrameAsync();
            if (frameBytes is null || frameBytes.Length == 0)
            {
                TxtTrainingStatus.Text = "Frame-Capture fehlgeschlagen.";
                BtnTrainingSave.IsEnabled = true;
                return;
            }

            // 3. BBox berechnen (normalisiert)
            var bbox = TrainingBBoxFromRect(_trainingCurrentRect.Value);
            if (bbox.Width < 0.01 || bbox.Height < 0.01)
            {
                TxtTrainingStatus.Text = "BBox zu klein.";
                BtnTrainingSave.IsEnabled = true;
                return;
            }

            // 4. Temp-Frame schreiben
            var annotationId = Guid.NewGuid().ToString("N")[..12];
            var tempFrame = IOPath.Combine(IOPath.GetTempPath(), $"sewer_training_{annotationId}.png");
            await File.WriteAllBytesAsync(tempFrame, frameBytes);

            // 5. YOLO-Export (Frame kopieren + Crop + Label)
            int classId = VsaYoloClassMap.GetClassId(code);
            var baseName = $"training_{annotationId}";
            var exportService = new TrainingAnnotationExportService();
            var exportResult = await exportService.ExportAsync(tempFrame, bbox, code, classId, baseName);

            try { File.Delete(tempFrame); } catch { }

            // 6. TeacherAnnotation persistieren
            int severity = ParseTrainingSeverity();
            double meterPos = GetTrainingMeterValue() ?? GetMeterFromVideoPosition() ?? 0.0;

            var annotation = new TeacherAnnotation
            {
                AnnotationId = annotationId,
                VsaCode = code,
                Beschreibung = string.IsNullOrWhiteSpace(selectedEntry.Beschreibung) ? code : selectedEntry.Beschreibung,
                Severity = severity,
                MeterPosition = meterPos,
                VideoTimestamp = videoTime,
                HaltungName = _haltungRecord?.GetFieldValue("Haltungsname")
                              ?? _haltungRecord?.GetFieldValue("HaltungsId")
                              ?? "",
                ToolType = OverlayToolType.Rectangle,
                Points = new System.Collections.Generic.List<NormalizedPoint>
                {
                    new NormalizedPoint(bbox.XCenter - bbox.Width / 2, bbox.YCenter - bbox.Height / 2),
                    new NormalizedPoint(bbox.XCenter + bbox.Width / 2, bbox.YCenter + bbox.Height / 2)
                },
                BoundingBox = bbox,
                FullFramePath = exportResult.FullFramePath,
                CroppedRegionPath = exportResult.CroppedRegionPath,
                YoloAnnotationPath = exportResult.YoloAnnotationPath
            };

            // SAM-Maske mitspeichern, falls vorhanden (bester Mask wird genommen)
            if (_trainingLastSamResult is { Masks.Count: > 0 })
            {
                var best = _trainingLastSamResult.Masks[0];
                annotation.MaskRle = best.MaskRle;
                annotation.MaskWidth = _trainingLastSamResult.ImageWidth;
                annotation.MaskHeight = _trainingLastSamResult.ImageHeight;
            }

            await TeacherAnnotationStore.AppendAsync(annotation);

            // 7. TrainingSample erzeugen — IMMER persistieren, KB-Indexierung ist optional
            // (Sample-Store ist die Retrain/Review-Quelle. KB-Indexierung braucht Ollama
            // und darf nicht den Sample-Write blockieren.)
            var sample = new TrainingSample
            {
                SampleId = Guid.NewGuid().ToString("N"),
                CaseId = _haltungId ?? $"video-{DateTime.UtcNow:yyyyMMdd}",
                Code = code,
                Beschreibung = annotation.Beschreibung,
                MeterStart = meterPos,
                MeterEnd = meterPos,
                FramePath = annotation.FullFramePath ?? "",
                Status = TrainingSampleStatus.Approved,
                MatchLevel = MatchLevelNames.TeacherAnnotation,
                SourceType = SourceTypeNames.TeacherAnnotation,
                IsKorrigiert = false,
                TimeSeconds = videoTime.TotalSeconds,
                BboxXCenter = bbox.XCenter,
                BboxYCenter = bbox.YCenter,
                BboxWidth = bbox.Width,
                BboxHeight = bbox.Height,
                KbIndexState = KbIndexState.Pending,
                ExportedUtc = DateTime.UtcNow
            };

            // KB-Indexierung best-effort
            bool kbIndexed = false;
            try
            {
                await EnsureTrainingKbManagerAsync();
                if (_trainingKbManager != null && KnowledgeBaseManager.IsIndexWorthy(sample))
                {
                    kbIndexed = await _trainingKbManager.IndexSampleAsync(sample);
                    if (kbIndexed)
                        sample.KbIndexState = KbIndexState.Indexed;
                }
            }
            catch (Exception kbEx)
            {
                System.Diagnostics.Debug.WriteLine($"[TrainingMode] KB-Indexierung fehlgeschlagen: {kbEx.Message}");
                kbIndexed = false;
            }

            // Sample-Store-Persist IMMER ausfuehren (auch wenn KB offline/Error)
            try
            {
                await TrainingSamplesStore.MergeOrUpdateAsync(new[] { sample });
            }
            catch (Exception storeEx)
            {
                System.Diagnostics.Debug.WriteLine($"[TrainingMode] TrainingSamplesStore-Write fehlgeschlagen: {storeEx.Message}");
            }

            _trainingSessionCount++;
            TxtTrainingCount.Text = $"{_trainingSessionCount} Sample(s) in dieser Sitzung";
            TxtTrainingStatus.Text = kbIndexed
                ? $"OK: {code} gespeichert + KB indexiert."
                : $"OK: {code} gespeichert (KB offline).";

            // Rechteck zuruecksetzen fuer naechstes Sample (Code bleibt leer, User zeichnet neu)
            ResetTrainingDrawing();
        }
        catch (Exception ex)
        {
            TxtTrainingStatus.Text = $"Fehler: {ex.Message}";
            BtnTrainingSave.IsEnabled = true;
        }
    }

    private async void TrainingNegativ_Click(object sender, RoutedEventArgs e)
    {
        if (!_isTrainingMode) return;
        try
        {
            TxtTrainingStatus.Text = "Speichere Negativ-Beispiel...";
            var frameBytes = await CaptureCurrentFrameAsync();
            if (frameBytes is null || frameBytes.Length == 0)
            {
                TxtTrainingStatus.Text = "Frame-Capture fehlgeschlagen.";
                return;
            }

            var annotationId = Guid.NewGuid().ToString("N")[..12];

            // Frame in permanentes Teacher-Images-Verzeichnis schreiben (NICHT in %TEMP% —
            // sonst wird der Pfad in der TeacherAnnotation spaeter vom Windows-Cleanup invalidiert).
            var imagesDir = TeacherAnnotationStore.GetImagesDir();
            System.IO.Directory.CreateDirectory(imagesDir);
            var framePath = IOPath.Combine(imagesDir, $"negativ_{annotationId}.png");
            await File.WriteAllBytesAsync(framePath, frameBytes);

            // Negativ-Beispiel als TeacherAnnotation ohne BBox
            var videoTime = TimeSpan.FromMilliseconds(Math.Max(0, _player.Time));
            double negMeter = GetTrainingMeterValue() ?? GetMeterFromVideoPosition() ?? 0.0;
            var annotation = new TeacherAnnotation
            {
                AnnotationId = annotationId,
                VsaCode = "NEGATIV",
                Beschreibung = "Kein Befund",
                Severity = 0,
                MeterPosition = negMeter,
                VideoTimestamp = videoTime,
                HaltungName = _haltungRecord?.GetFieldValue("Haltungsname") ?? "",
                ToolType = OverlayToolType.None,
                FullFramePath = framePath
            };
            await TeacherAnnotationStore.AppendAsync(annotation);

            // Auch Negativ-Samples im TrainingSamplesStore persistieren — sie sind
            // wichtig fuer den YOLO-Retrain (reduziert False-Positives).
            // KB-Indexierung von NEGATIV-Samples ist NICHT sinnvoll (kein Schaden zum Embedden).
            try
            {
                var negSample = new TrainingSample
                {
                    SampleId = Guid.NewGuid().ToString("N"),
                    CaseId = _haltungId ?? $"video-{DateTime.UtcNow:yyyyMMdd}",
                    Code = "NEGATIV",
                    Beschreibung = "Kein Befund",
                    MeterStart = negMeter,
                    MeterEnd = negMeter,
                    FramePath = framePath,
                    Status = TrainingSampleStatus.Approved,
                    MatchLevel = MatchLevelNames.TeacherAnnotation,
                    SourceType = SourceTypeNames.TeacherAnnotation,
                    IsKorrigiert = false,
                    TimeSeconds = videoTime.TotalSeconds,
                    // Keine BBox fuer Negativ
                    KbIndexState = KbIndexState.None,
                    ExportedUtc = DateTime.UtcNow
                };
                await TrainingSamplesStore.MergeOrUpdateAsync(new[] { negSample });
            }
            catch (Exception storeEx)
            {
                System.Diagnostics.Debug.WriteLine($"[TrainingMode] Negativ-Sample-Store fehlgeschlagen: {storeEx.Message}");
            }

            _trainingSessionCount++;
            TxtTrainingCount.Text = $"{_trainingSessionCount} Sample(s) in dieser Sitzung";
            TxtTrainingStatus.Text = "OK: Negativ-Beispiel gespeichert.";
        }
        catch (Exception ex)
        {
            TxtTrainingStatus.Text = $"Fehler: {ex.Message}";
        }
    }

    private void TrainingExit_Click(object sender, RoutedEventArgs e)
    {
        ExitTrainingMode();
        TrainingModeButton.IsChecked = false;
    }

    // --- Helpers ---

    private NormalizedBoundingBox TrainingBBoxFromRect(Rect rPx)
    {
        var cw = Math.Max(1, TrainingOverlayCanvas.ActualWidth);
        var ch = Math.Max(1, TrainingOverlayCanvas.ActualHeight);

        double nx1 = rPx.X / cw;
        double ny1 = rPx.Y / ch;
        double nx2 = (rPx.X + rPx.Width) / cw;
        double ny2 = (rPx.Y + rPx.Height) / ch;

        nx1 = Math.Clamp(nx1, 0.0, 1.0);
        ny1 = Math.Clamp(ny1, 0.0, 1.0);
        nx2 = Math.Clamp(nx2, 0.0, 1.0);
        ny2 = Math.Clamp(ny2, 0.0, 1.0);

        return new NormalizedBoundingBox
        {
            XCenter = (nx1 + nx2) / 2,
            YCenter = (ny1 + ny2) / 2,
            Width = Math.Max(0, nx2 - nx1),
            Height = Math.Max(0, ny2 - ny1)
        };
    }

    private int ParseTrainingSeverity()
    {
        if (CmbTrainingSeverity.SelectedItem is ComboBoxItem item
            && item.Content is string s && s.Length > 0
            && int.TryParse(s[..1], out var v))
            return Math.Clamp(v, 1, 5);
        return 3;
    }

    private double? GetTrainingMeterValue()
    {
        var t = TxtTrainingMeter?.Text?.Trim();
        if (string.IsNullOrEmpty(t)) return null;
        t = t.Replace("m", "", StringComparison.OrdinalIgnoreCase).Replace(",", ".");
        if (double.TryParse(t, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var m))
            return m;
        return null;
    }

    private async Task EnsureTrainingKbManagerAsync()
    {
        if (_trainingKbManager != null) return;
        try
        {
            var cfg = OllamaConfig.Load();
            _trainingHttp ??= new System.Net.Http.HttpClient { Timeout = cfg.RequestTimeout };
            _trainingEmbedder = new EmbeddingService(_trainingHttp, cfg);
            _trainingKbCtx = new KnowledgeBaseContext();
            _trainingKbManager = new KnowledgeBaseManager(_trainingKbCtx, _trainingEmbedder);
            await Task.CompletedTask;
        }
        catch
        {
            _trainingKbManager = null;
            _trainingKbCtx = null;
            _trainingEmbedder = null;
        }
    }
}
