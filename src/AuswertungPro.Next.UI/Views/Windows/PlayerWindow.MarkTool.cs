using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Views.Windows;

// Phase 6.1.G: Manual-Mark-Tool extrahiert aus PlayerWindow.xaml.cs.
// Behandelt das "Markieren"-Popup (Punkt/Ellipse/Freihand/Rechteck), Aktivieren/
// Deaktivieren, SAM-Preview an der markierten Stelle und das Speichern als
// Teacher-Annotation (YOLO-Export + TeacherAnnotationStore + CodingEvent).
public partial class PlayerWindow
{
    private OverlayToolType _markToolType = OverlayToolType.None;

    private void ManualMark_Click(object sender, RoutedEventArgs e)
    {
        // Slice 8a.3 Step 5b: ToolsDropdownPopup-Pfad entfaellt — gehoerte zum
        // alten Codier-Modus, MarkTool laeuft jetzt immer ueber MarkToolPopup.
        MarkToolPopup.IsOpen = !MarkToolPopup.IsOpen;
    }

    private void MarkTool_Punkt_Click(object sender, RoutedEventArgs e)
        => ActivateMarkTool(OverlayToolType.Point, "Punkt");

    private void MarkTool_Ellipse_Click(object sender, RoutedEventArgs e)
        => ActivateMarkTool(OverlayToolType.Ellipse, "Ellipse");

    private void MarkTool_Freihand_Click(object sender, RoutedEventArgs e)
        => ActivateMarkTool(OverlayToolType.Freehand, "Freihand");

    private void MarkTool_Rechteck_Click(object sender, RoutedEventArgs e)
        => ActivateMarkTool(OverlayToolType.Rectangle, "Rechteck");

    private void ActivateMarkTool(OverlayToolType tool, string label)
    {
        // Slice 8a.3 Step 5b: CodingMarkToolPopup + ToolsDropdownPopup
        // entfallen mit der CodingToolbar — nur MarkToolPopup bleibt.
        MarkToolPopup.IsOpen = false;
        _markToolType = tool;
        TxtMarkToolName.Text = label;
        // Slice 8a.3 Step 5b: TxtActiveToolLabel entfaellt mit CodingToolbar.
        _player.SetPause(true);
        _codingSchemaManager.Cancel();
        _codingSchemaType = null;

        if (tool == OverlayToolType.Point)
        {
            // Bestehende Punkt-Logik: DetectionCanvas aktivieren
            _isManualMarkMode = true;
            DetectionOverlayGrid.Visibility = Visibility.Visible;
            DetectionOverlayGrid.IsHitTestVisible = true;
            DetectionCanvas.IsHitTestVisible = true;
            DetectionCanvas.Cursor = Cursors.Cross;
        }
        else
        {
            // Zeichen-Tools: CodingOverlayPopup aktivieren
            _isManualMarkMode = false;
            EnsureMarkOverlayReady();
            _codingOverlayService!.ActiveTool = tool;

            // Offene Zeichnung verwerfen
            if (_codingVm != null)
                _codingVm.CurrentOverlay = null;

            CodingOverlayPopup.IsOpen = true;
            UpdateCodingOverlayViewport();
            CodingOverlayCanvas.IsHitTestVisible = true;
            CodingOverlayCanvas.Cursor = Cursors.Cross;
        }
    }

    /// <summary>
    /// Stellt sicher dass OverlayService + ViewModel bereitstehen (auch ausserhalb Codier-Modus).
    /// </summary>
    private void EnsureMarkOverlayReady()
    {
        if (_codingOverlayService != null && _codingVm != null) return;

        // Lazy-Init: minimales Setup fuer Overlay-Zeichnung
        _codingOverlayService ??= new AuswertungPro.Next.Application.Ai.OverlayToolService();
        if (_codingVm == null)
        {
            _codingSessionService ??= new AuswertungPro.Next.Infrastructure.Ai.CodingSessionService();
            _codingVm = new ViewModels.Windows.CodingSessionViewModel(_codingSessionService, _codingOverlayService);
        }
    }

    private void DeactivateMarkTool()
    {
        _markToolType = OverlayToolType.None;
        _isManualMarkMode = false;
        TxtMarkToolName.Text = "Markieren";

        DetectionCanvas.Cursor = Cursors.Arrow;
        DetectionCanvas.IsHitTestVisible = false;
        if (!_isDetecting)
        {
            DetectionOverlayGrid.IsHitTestVisible = false;
            DetectionOverlayGrid.Visibility = Visibility.Collapsed;
        }

        // Slice 8a.3 Step 5b: _isCodingMode-Guard entfaellt — Overlay-Cleanup
        // laeuft jetzt immer (es gibt keinen In-Place-Codier-Modus mehr,
        // der das Popup offen halten muesste).
        _codingSchemaManager.Cancel();
        _codingOverlayService?.CancelDraw();
        if (_codingOverlayService != null)
            _codingOverlayService.ActiveTool = OverlayToolType.None;
        CodingOverlayPopup.IsOpen = false;
        CodingOverlayCanvas.IsHitTestVisible = false;
    }

    /// <summary>
    /// Nach abgeschlossener Markierung (Ellipse/Freihand/Rechteck): Code-Katalog oeffnen + Training speichern.
    /// </summary>
    private async void HandleMarkDrawingComplete()
    {
        // Window-Lifecycle-Guard fuer async-void-Methode
        if (_isWindowClosed) return;

        // Slice 1 (Operateur-Annotation): wenn der Operator-Box-Tool aktiv ist,
        // route auf den Operator-Pfad — das Rectangle-Tool teilt sich die
        // CodingOverlayPopup-Pipeline mit dem Mark-Tool, aber das Sample-
        // Schreiben laeuft anders (kein Code-Picker, kein Teacher-Annotation-
        // Store, sondern OperateurAnnotationService).
        if (_operatorBoxActive && _operatorActive is not null)
        {
            await HandleOperatorBoxCompleteAsync();
            return;
        }

        try
        {
            var overlay = _codingVm?.CurrentOverlay;
            if (overlay == null) return;

            var timestampSec = _player.Time / 1000.0;

            // Uhrzeiger-Position aus Overlay-Zentrum berechnen
            string? clockPos = null;
            double avgX = 0.5, avgY = 0.5;
            if (overlay.Points.Count > 0)
            {
                avgX = overlay.Points.Average(p => p.X);
                avgY = overlay.Points.Average(p => p.Y);
                var cx = 0.5; var cy = 0.5; // Rohrmitte (normalisiert)
                var dx = avgX - cx;
                var dy = avgY - cy;
                var angleDeg = Math.Atan2(dy, dx) * 180.0 / Math.PI;
                var clockAngle = (angleDeg + 90 + 360) % 360;
                var hour = (int)Math.Round(clockAngle / 30.0) % 12;
                if (hour == 0) hour = 12;
                clockPos = hour.ToString();
            }

            // SAM-Segmentierung an der markierten Stelle anzeigen
            await ShowSamPreviewAtMarkAsync(overlay, avgX, avgY);

            // Der VSA-Picker ist modal und liegt ueber dem CodingOverlayPopup.
            // Deshalb kurz warten, damit die erfolgreich gerenderte SAM-Maske sichtbar ist,
            // bevor der Picker automatisch aufgeht.
            if (!_isWindowClosed && overlay.ToolType == OverlayToolType.Rectangle && CodingOverlayPopup.IsOpen)
                await Task.Delay(TimeSpan.FromMilliseconds(1200));

            // Training speichern: Frame + YOLO-Export + TeacherAnnotation + CodingEvent
            bool saved = await SaveMarkAsTrainingAsync(overlay, timestampSec, clockPos);

            if (_codingVm != null) _codingVm.CurrentOverlay = null;

            if (saved)
            {
                // Erfolgreich gespeichert → BBox + SAM-Maske beide loeschen,
                // Tool deaktivieren. Frame ist jetzt clean fuer naechste Stelle.
                Ai.Pipeline.SamMaskRenderer.ClearMasks(CodingOverlayCanvas);
                RedrawCodingCanvas(includeManualOverlay: false, preserveSamMasks: false);
                DeactivateMarkTool();
            }
            else
            {
                // Abgebrochen → BBox weg, SAM-Maske bleibt sichtbar (User kann
                // gleich neu markieren ohne dass die Voransicht verschwindet).
                RedrawCodingCanvas(includeManualOverlay: false, preserveSamMasks: true);
                if (_codingOverlayService != null)
                    _codingOverlayService.ActiveTool = _markToolType;
                CodingOverlayCanvas.Cursor = Cursors.Cross;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PlayerWindow] HandleMarkDrawingComplete error: {ex.Message}");
        }
    }

    /// <summary>
    /// Zeigt eine SAM-Segmentierung als Vorschau an der markierten Stelle.
    /// Der User sieht sofort die Konturen des Objekts das die KI dort erkennt.
    /// </summary>
    private async Task ShowSamPreviewAtMarkAsync(OverlayGeometry overlay, double normX, double normY)
    {
        if (_isWindowClosed) return;
        if (_codingVisionClient == null)
        {
            // Slice 8a.3 Step 5b: SetCodingAiState (Status-Badge im alten
            // Codier-Toolbar) ist weggefallen. Status laeuft jetzt nur noch
            // ueber Debug.WriteLine — die SAM-Maske selbst zeigt sichtbar
            // an, ob etwas erkannt wurde.
            System.Diagnostics.Debug.WriteLine("[SAM] Abbruch: _codingVisionClient ist null (Sidecar nicht initialisiert)");
            return;
        }

        try
        {
            Ai.Pipeline.SamMaskRenderer.ClearMasks(CodingOverlayCanvas);

            if (overlay.Points.Count == 0)
            {
                System.Diagnostics.Debug.WriteLine("[SAM] Abbruch: BBox-Punkte fehlen");
                return;
            }

            // Snapshot fuer SAM
            var pngBytes = await CaptureSnapshotAsync();
            if (pngBytes == null || pngBytes.Length == 0)
            {
                System.Diagnostics.Debug.WriteLine("[SAM] Abbruch: Snapshot leer/null");
                return;
            }

            var b64 = Convert.ToBase64String(pngBytes);

            // Bild-Aufloesung dynamisch aus dem Snapshot lesen (vorher hartkodiert
            // 640x480 -> falsche Pixel-Koordinaten bei 1920x1080-Frames -> SAM
            // bekam BBox an falscher Stelle und lieferte 0 Masken).
            int imgW = 1920, imgH = 1080; // Sicherer Default fuer typische Inspektionsvideos
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
            catch (Exception decEx)
            {
                System.Diagnostics.Debug.WriteLine($"[SAM] Bild-Decode fehlgeschlagen, nutze Default {imgW}x{imgH}: {decEx.Message}");
            }

            // BBox-Berechnung analog zum Trainingsmodus (PlayerWindow.TrainingMode.cs:289-294):
            // overlay.Points sind normiert zum CodingOverlayCanvas. Wenn das Canvas-
            // Aspect-Ratio nicht zum Frame-Aspect passt (Letterbox-Bars wegen
            // Window-Resizing), wuerde die direkte Normalisierung overlay.Points * imgW
            // die BBox verschieben. Stattdessen ueber Canvas-Pixel + sx/sy-Skalierung,
            // das macht der Trainingsmodus auch und es funktioniert dort zuverlaessig.
            var (cw, ch) = GetCodingOverlayRenderSize();
            double sx = imgW / cw;
            double sy = imgH / ch;

            double minNormX = overlay.Points.Min(p => p.X);
            double minNormY = overlay.Points.Min(p => p.Y);
            double maxNormX = overlay.Points.Max(p => p.X);
            double maxNormY = overlay.Points.Max(p => p.Y);

            // Normiert -> Canvas-Pixel -> Image-Pixel
            double minX = (minNormX * cw) * sx;
            double minY = (minNormY * ch) * sy;
            double maxX = (maxNormX * cw) * sx;
            double maxY = (maxNormY * ch) * sy;

            // Sanity-Check: BBox muss > 0 Pixel haben
            if ((maxX - minX) < 4 || (maxY - minY) < 4)
            {
                System.Diagnostics.Debug.WriteLine($"[SAM] BBox zu klein: {maxX - minX:F0}x{maxY - minY:F0} px");
                return;
            }

            System.Diagnostics.Debug.WriteLine(
                $"[SAM] Anfrage: img={imgW}x{imgH}, canvas={cw:F0}x{ch:F0}, " +
                $"bbox=({minX:F0},{minY:F0})-({maxX:F0},{maxY:F0}) " +
                $"({maxX-minX:F0}x{maxY-minY:F0}px), b64={b64.Length} bytes");

            // Nur BBox als Prompt — kein Punkt-Prompt, damit SAM innerhalb der Box bleibt
            var boxes = new[] { new AuswertungPro.Next.Application.Ai.Pipeline.SamBoundingBox(minX, minY, maxX, maxY, "mark", 1.0) };

            int dn = _codingOverlayService?.Calibration?.NominalDiameterMm ?? 300;
            var samReq = new AuswertungPro.Next.Application.Ai.Pipeline.SamRequest(b64, boxes, PipeDiameterMm: dn);

            AuswertungPro.Next.Application.Ai.Pipeline.SamResponse? samResp;
            var sw = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                samResp = await _codingVisionClient.SegmentSamAsync(samReq);
            }
            catch (Exception apiEx)
            {
                System.Diagnostics.Debug.WriteLine($"[SAM] API-Fehler: {apiEx.Message}");
                return;
            }
            finally
            {
                sw.Stop();
            }

            if (samResp == null)
            {
                System.Diagnostics.Debug.WriteLine("[SAM] Antwort null (Sidecar nicht erreichbar oder 401/500)");
                return;
            }

            System.Diagnostics.Debug.WriteLine(
                $"[SAM] Antwort: {samResp.Masks.Count} Masken, img={samResp.ImageWidth}x{samResp.ImageHeight}, t={samResp.InferenceTimeMs}ms");

            if (samResp.Masks.Count == 0)
            {
                System.Diagnostics.Debug.WriteLine("[SAM] Keine Maske gefunden (leeres Ergebnis vom Sidecar)");
                return;
            }

            // Alte Masken entfernen, SAM-Vorschau rendern (Cyan = manuell markiert)
            Ai.Pipeline.SamMaskRenderer.ClearMasks(CodingOverlayCanvas);
            CodingOverlayPopup.IsOpen = true;
            UpdateCodingOverlayViewport();
            var (renderW, renderH) = GetCodingOverlayRenderSize();
            RenderSamPromptBox(minNormX, minNormY, maxNormX, maxNormY, renderW, renderH);

            // Quantifizierung fuer Label-Anzeige
            var quantified = new List<AuswertungPro.Next.Application.Ai.Pipeline.MaskQuantificationService.QuantifiedMask>();
            var cal = _codingOverlayService?.Calibration;
            foreach (var mask in samResp.Masks)
            {
                var q = cal != null
                    ? AuswertungPro.Next.Application.Ai.Pipeline.MaskQuantificationService.Quantify(mask, samResp.ImageWidth, samResp.ImageHeight, dn, cal)
                    : AuswertungPro.Next.Application.Ai.Pipeline.MaskQuantificationService.Quantify(mask, samResp.ImageWidth, samResp.ImageHeight, dn);
                quantified.Add(q);
            }

            Ai.Pipeline.SamMaskRenderer.RenderMasks(
                CodingOverlayCanvas,
                samResp,
                quantified,
                renderW,
                renderH);

            var visibleSamElements = CodingOverlayCanvas.Children.OfType<FrameworkElement>()
                .Count(e => (e.Tag as string)?.StartsWith(Ai.Pipeline.SamMaskRenderer.MaskTag, StringComparison.Ordinal) == true);

            System.Diagnostics.Debug.WriteLine(
                $"[SAM] Erfolg: {samResp.Masks.Count} Region(en) in {sw.ElapsedMilliseconds} ms, Overlay-Elemente: {visibleSamElements}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SAM] Unerwarteter Fehler: {ex}");
        }
    }

    private (double Width, double Height) GetCodingOverlayRenderSize()
    {
        UpdateCodingOverlayViewport();

        double w = CodingOverlayCanvas.ActualWidth;
        double h = CodingOverlayCanvas.ActualHeight;
        if (double.IsNaN(w) || w <= 1) w = CodingOverlayCanvas.Width;
        if (double.IsNaN(h) || h <= 1) h = CodingOverlayCanvas.Height;
        if (double.IsNaN(w) || w <= 1) w = VideoView.ActualWidth;
        if (double.IsNaN(h) || h <= 1) h = VideoView.ActualHeight;

        return (Math.Max(1, w), Math.Max(1, h));
    }

    private void RenderSamPromptBox(double minNormX, double minNormY, double maxNormX, double maxNormY, double canvasW, double canvasH)
    {
        var rect = new Rectangle
        {
            Width = Math.Max(1, (maxNormX - minNormX) * canvasW),
            Height = Math.Max(1, (maxNormY - minNormY) * canvasH),
            Stroke = new SolidColorBrush(Color.FromRgb(0x38, 0xBD, 0xF8)),
            StrokeThickness = 2,
            StrokeDashArray = new DoubleCollection { 6, 4 },
            Fill = new SolidColorBrush(Color.FromArgb(20, 0x38, 0xBD, 0xF8)),
            IsHitTestVisible = false,
            Tag = $"{Ai.Pipeline.SamMaskRenderer.MaskTag}_prompt"
        };

        Canvas.SetLeft(rect, minNormX * canvasW);
        Canvas.SetTop(rect, minNormY * canvasH);
        CodingOverlayCanvas.Children.Add(rect);
    }

    /// <summary>
    /// Speichert eine Markierung als Teacher-Annotation (YOLO-Export + TeacherAnnotationStore).
    /// Vereinfachte Version von CodingModeWindow.BtnSaveAsTraining_Click.
    /// </summary>
    /// <summary>Rueckgabe: true wenn gespeichert, false wenn abgebrochen.</summary>
    private async Task<bool> SaveMarkAsTrainingAsync(OverlayGeometry overlay, double timestampSec, string? clockPosition)
    {
        try
        {
            // Slice 8a.3 Step 5b: Preselected-Import-Pfad (LstImportEvents) ist
            // weggefallen — die Liste lebte im geloeschten CodingSidePanel.
            // Live-MarkTool oeffnet jetzt immer den VsaCodeExplorer.

            // 1. VSA-Code via Explorer auswaehlen.
            ProtocolEntry selectedEntry;
            {
                var autoMeter = GetMeterFromVideoPosition();
                var entry = new ProtocolEntry();
                var explorerVm = new ViewModels.Windows.VsaCodeExplorerViewModel(entry, autoMeter, TimeSpan.FromSeconds(timestampSec));
                var explorer = new Views.Windows.VsaCodeExplorerWindow(explorerVm, _videoPath, TimeSpan.FromSeconds(timestampSec))
                {
                    Owner = this
                };
                if (explorer.ShowDialog() != true || explorer.SelectedEntry == null)
                    return false;
                selectedEntry = explorer.SelectedEntry;
            }

            // 2. Frame-Capture
            var frameBytes = await CaptureCurrentFrameAsync();
            if (frameBytes == null) return false;

            // 3. BoundingBox aus Overlay-Punkten
            var bbox = AuswertungPro.Next.Application.Ai.NormalizedBoundingBox.FromPoints(
                overlay.Points.Select(p => new NormalizedPoint(p.X, p.Y)).ToList());

            // Mindestgroesse pruefen (1% des Frames)
            if (bbox.Width < 0.01 || bbox.Height < 0.01) return false;

            // 4. YOLO-Export
            int classId = AuswertungPro.Next.Application.Ai.Teacher.VsaYoloClassMap.GetClassId(selectedEntry.Code);
            var annotationId = Guid.NewGuid().ToString("N")[..12];
            var baseName = $"mark_{annotationId}";

            // Frame in Temp speichern
            var tempFrame = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(), $"sewer_studio_mark_{annotationId}.png");
            await System.IO.File.WriteAllBytesAsync(tempFrame, frameBytes);

            var exportService = new Ai.Teacher.TrainingAnnotationExportService();
            var exportResult = await exportService.ExportAsync(tempFrame, bbox, selectedEntry.Code, classId, baseName);

            // Temp aufräumen
            try { System.IO.File.Delete(tempFrame); } catch { }

            // 5. TeacherAnnotation erstellen + persistieren
            // Meter-Quelle: selectedEntry.MeterStart oder Videoposition als Fallback.
            var captureMeter = selectedEntry.MeterStart ?? GetMeterFromVideoPosition() ?? 0.0;

            // Haltungs-Kontext aus dem Record uebernehmen (Material/DN/Profil/Nutzung)
            int? dnMm = null;
            if (int.TryParse(_haltungRecord?.GetFieldValue("DN_mm"), System.Globalization.NumberStyles.Integer,
                    System.Globalization.CultureInfo.InvariantCulture, out var dnParsed))
                dnMm = dnParsed;

            var annotation = new AuswertungPro.Next.Application.Ai.Teacher.TeacherAnnotation
            {
                AnnotationId = annotationId,
                VsaCode = selectedEntry.Code,
                Beschreibung = selectedEntry.Beschreibung,
                MeterPosition = captureMeter,
                VideoTimestamp = TimeSpan.FromSeconds(timestampSec),
                HaltungName = _haltungRecord?.GetFieldValue("Haltungsname"),
                VideoPath = _videoPath,
                Rohrmaterial = _haltungRecord?.GetFieldValue("Rohrmaterial"),
                DnMm = dnMm,
                Profil = _haltungRecord?.GetFieldValue("Profil"),
                Nutzungsart = _haltungRecord?.GetFieldValue("Nutzungsart"),
                ToolType = overlay.ToolType,
                Points = new List<NormalizedPoint>(
                    overlay.Points.Select(p => new NormalizedPoint(p.X, p.Y))),
                BoundingBox = bbox,
                ClockPosition = clockPosition != null && double.TryParse(clockPosition, out var cp) ? cp : null,
                FullFramePath = exportResult.FullFramePath,
                CroppedRegionPath = exportResult.CroppedRegionPath,
                YoloAnnotationPath = exportResult.YoloAnnotationPath,
                WidthMm = overlay.Q2Mm,
                HeightMm = overlay.Q1Mm
            };

            await AuswertungPro.Next.Application.Ai.Teacher.TeacherAnnotationStore.AppendAsync(annotation);

            // Slice 8a.3 Step 5b: Coding-Session-AddEvent + RefreshCodingEventsList
            // entfaellt — die KI-Befunde-Liste (LstCodingEvents) lebte im
            // geloeschten CodingSidePanel. TeacherAnnotation oben enthaelt
            // bereits Frame + BBox + YOLO-Label fuer YOLO/SAM-Training.

            // Dezente Statusmeldung im OSD-Badge (kein MessageBox-Popup)
            OsdMeterBadge.Visibility = Visibility.Visible;
            TxtOsdMeter.Text = $"✓ {selectedEntry.Code} gespeichert";

            // Badge nach 3 Sekunden zuruecksetzen
            var resetTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
            resetTimer.Tick += (_, _) =>
            {
                resetTimer.Stop();
                OsdMeterBadge.Visibility = Visibility.Collapsed;
            };
            resetTimer.Start();
            return true;
        }
        catch (Exception ex)
        {
            OsdMeterBadge.Visibility = Visibility.Visible;
            TxtOsdMeter.Text = $"✗ Fehler: {ex.Message}";
            return false;
        }
    }

    private void DetectionCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // Eingabemarker nutzt CodingOverlayCanvas (nicht DetectionCanvas)

        if (!_isManualMarkMode)
            return;

        var clickPoint = e.GetPosition(DetectionCanvas);
        var canvasSize = new Size(DetectionCanvas.ActualWidth, DetectionCanvas.ActualHeight);

        if (canvasSize.Width < 60 || canvasSize.Height < 60)
            return;

        // Pause video
        _player.SetPause(true);

        var clockPosition = ClickToClockPosition(clickPoint, canvasSize);
        var timestampSec = _player.Time / 1000.0;

        OpenCodeCatalogForMark(clockPosition, timestampSec, null);
        e.Handled = true;
    }

    private static string ClickToClockPosition(Point click, Size canvasSize)
    {
        var cx = canvasSize.Width / 2.0;
        var cy = canvasSize.Height / 2.0;
        var dx = click.X - cx;
        var dy = click.Y - cy;

        var angleDeg = Math.Atan2(dy, dx) * 180.0 / Math.PI;
        var clockAngle = (angleDeg + 90 + 360) % 360;
        var hour = (int)Math.Round(clockAngle / 30.0) % 12;
        if (hour == 0) hour = 12;

        return hour.ToString();
    }

    // ─── CodingOverlayCanvas-Mouse-Handler ─────────────────────────────────
    //
    // Slice 8a.3 Step 5b: Die alten Handler in PlayerWindow.CodingTool.cs
    // (Eingabemarker, Schema, Kalibrieren, Multi-Punkt) sind zusammen mit
    // dem In-Place-Codier-Modus geloescht. Was hier bleibt, ist die schlanke
    // Version, die der Live-MarkTool-Flow braucht: Punkt setzen, Drag-
    // Vorschau zeichnen, beim Loslassen ueber HandleMarkDrawingComplete in
    // den SAM-/VsaCodeExplorer-Pfad gehen.
    private void CodingCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_codingOverlayService == null || _codingVm == null) return;
        if (_codingOverlayService.ActiveTool == OverlayToolType.None) return;

        var pos = e.GetPosition(CodingOverlayCanvas);
        var norm = CodingPixelToNorm(pos);

        // Multi-Punkt-Werkzeug (z.B. Winkelmesser): drei Klicks bauen den Overlay
        if (_codingOverlayService.IsMultiPointTool)
        {
            if (_codingOverlayService.DrawPointCount == 0)
                _codingVm.CurrentOverlay = null;

            bool complete = _codingVm.OnCanvasMultiPointClick(norm);
            ClearTransientCodingCanvas(clearManualOverlay: true);
            // Vorschau zeichnen: nichts zu tun ohne RenderOverlayGeometry —
            // der Live-MarkTool-Flow braucht es nicht (Rectangle ist Standard).
            if (complete)
                UpdateCodingOverlayInfo(_codingVm.CurrentOverlay);
            return;
        }

        // Standard-Werkzeug (z.B. Rechteck): Klick + Drag
        _codingVm.CurrentOverlay = null;
        UpdateCodingOverlayInfo(null);
        _codingVm.OnCanvasMouseDown(norm);
        CodingOverlayCanvas.CaptureMouse();
        ClearTransientCodingCanvas(clearManualOverlay: true);
    }

    private void CodingCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (_codingOverlayService == null || _codingVm == null) return;
        if (!_codingOverlayService.IsDrawing) return;

        var pos = e.GetPosition(CodingOverlayCanvas);
        var norm = CodingPixelToNorm(pos);
        _codingVm.OnCanvasMouseMove(norm);
        // Slice 8a.3 Step 5b: Live-Vorschau-Rendering (RenderOverlayGeometry)
        // entfaellt mit dem alten In-Place-Modus. Der User sieht das BBox-
        // Resultat erst beim MouseUp via SAM-Maske + RenderSamPromptBox.
    }

    private void CodingCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_codingOverlayService == null || _codingVm == null) return;
        if (!_codingOverlayService.IsDrawing) return;

        var pos = e.GetPosition(CodingOverlayCanvas);
        var norm = CodingPixelToNorm(pos);
        _codingVm.OnCanvasMouseUp(norm);
        CodingOverlayCanvas.ReleaseMouseCapture();
        ClearTransientCodingCanvas(clearManualOverlay: true);

        if (_codingVm.CurrentOverlay != null)
        {
            // Mark-Tool-Flow: VsaCodeExplorer + SAM + Training speichern.
            if (_markToolType != OverlayToolType.None
                || _codingVm.CurrentOverlay.ToolType == OverlayToolType.Rectangle)
            {
                HandleMarkDrawingComplete();
                return;
            }
            UpdateCodingOverlayInfo(_codingVm.CurrentOverlay);
        }
        else
        {
            UpdateCodingOverlayInfo(null);
        }
    }

    private void CodingCanvas_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        // Slice 8a.3 Step 5b: Mausrad-Handling (PipeBend-Winkel) entfaellt
        // mit dem geloeschten Schema-Werkzeug. Live-MarkTool nutzt das Rad
        // nicht.
    }
}
