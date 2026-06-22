using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Windows.Input;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Player;
using AuswertungPro.Next.UI.Services;
using InfraSelfImproving = AuswertungPro.Next.Infrastructure.Ai.SelfImproving;
using InfraTeacher = AuswertungPro.Next.Infrastructure.Ai.Teacher;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private OverlayToolType _markToolType = OverlayToolType.None;

    private void ManualMark_Click(object sender, RoutedEventArgs e)
    {
        if (_isCodingMode)
            ToolsDropdownPopup.IsOpen = !ToolsDropdownPopup.IsOpen;
        else
            MarkToolPopup.IsOpen = !MarkToolPopup.IsOpen;
    }

    private void ToolsDropdown_Click(object sender, RoutedEventArgs e)
    {
        ToolsDropdownPopup.IsOpen = !ToolsDropdownPopup.IsOpen;
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
        MarkToolPopup.IsOpen = false;
        CodingMarkToolPopup.IsOpen = false;
        ToolsDropdownPopup.IsOpen = false;
        _markToolType = tool;
        TxtMarkToolName.Text = label;
        TxtActiveToolLabel.Text = label;
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
    private ICodingSessionService CreateCodingSessionService()
        => CodingSessionServiceFactory.Create(_serviceProvider?.Settings);

    private void EnsureMarkOverlayReady()
    {
        if (_codingOverlayService != null && _codingVm != null) return;

        // Lazy-Init: minimales Setup fuer Overlay-Zeichnung
        _codingOverlayService ??= new OverlayToolService();
        if (_codingVm == null)
        {
            _codingSessionService ??= CreateCodingSessionService();
            _codingVm = new ViewModels.Windows.CodingSessionViewModel(
                _codingSessionService,
                _codingOverlayService,
                new InfraSelfImproving.CodingFeedbackRecorder());
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

        if (!_isCodingMode)
        {
            _codingSchemaManager.Cancel();
            _codingOverlayService?.CancelDraw();
            if (_codingOverlayService != null)
                _codingOverlayService.ActiveTool = OverlayToolType.None;
            CodingOverlayPopup.IsOpen = false;
            CodingOverlayCanvas.IsHitTestVisible = false;
        }
    }

    /// <summary>
    /// Nach abgeschlossener Markierung (Ellipse/Freihand/Rechteck): Code-Katalog oeffnen + Training speichern.
    /// </summary>
    private async void HandleMarkDrawingComplete()
    {
        try
        {
            var overlay = _codingVm?.CurrentOverlay;
            if (overlay == null) return;

            var timestampSec = _player.Time / 1000.0;

            // Frame einmal erfassen — wird fuer SAM-Segmentierung UND den YOLO-Export wiederverwendet.
            var frameBytes = await CaptureCurrentFrameAsync();

            // Uhrlage zunaechst geometrisch (Overlay-Zentrum -> Uhr) als Fallback.
            string? clockPos = LiveDetectionGeometryMapper.EstimateClockFromOverlayCenter(overlay);

            // SAM segmentiert die gezogene Box und schreibt echte Messwerte (Uhrlage/Hoehe/
            // Breite/Querschnitt) ins Overlay. Bei fehlendem Sidecar/Maske bleibt die
            // geometrische Schaetzung erhalten — der Codier-Ablauf wird nie blockiert.
            var samResult = await TrySegmentMarkBoxAsync(overlay, frameBytes);
            if (!string.IsNullOrEmpty(samResult?.Quant.ClockPosition))
                clockPos = samResult!.Quant.ClockPosition;

            // SAM-Maske SICHTBAR machen, BEVOR das Codierfenster aufgeht: der Nutzer sieht,
            // dass die KI die Markierung (z.B. den Bogen) erfasst hat. Kurze Pause, damit das
            // Overlay tatsaechlich gezeichnet wird, dann erst das VSA-Codierfenster.
            if (samResult != null)
            {
                ShowMarkSamMask(samResult, overlay);
                await Task.Delay(3000);   // 3 s: SAM-Maske sichtbar lassen, dann erst das Codefenster
            }

            // Training speichern + Codierfenster (VsaCodeExplorer) mit vorausgefuellten Messwerten.
            bool saved = await SaveMarkAsTrainingAsync(overlay, timestampSec, clockPos, frameBytes);

            // Overlay + SAM-Maske + Bogen-Marker entfernen und Canvas neu zeichnen
            Ai.Pipeline.SamMaskRenderer.ClearMasks(CodingOverlayCanvas);
            BendMarkerRenderer.Clear(CodingOverlayCanvas);
            if (_codingVm != null) _codingVm.CurrentOverlay = null;
            RedrawCodingCanvas(includeManualOverlay: false);

            // Codiermodus: Werkzeug NICHT abschalten, sonst loest die naechste Box keine
            // Segmentierung / kein Codefenster mehr aus (Bug "funktioniert nur einmal").
            // Nur in der Live-Markierung (ausserhalb Codiermodus) nach dem Speichern abschalten.
            if (saved && !_isCodingMode)
            {
                // Erfolgreich gespeichert â†’ Tool deaktivieren
                DeactivateMarkTool();
            }
            else
            {
                // Abgebrochen â†’ Tool bleibt aktiv, naechste Markierung kann sofort gezeichnet werden
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
    /// Laesst SAM die gezogene Box segmentieren und schreibt die Messwerte ins Overlay
    /// (Hoehe/Breite mm, Querschnitt-%, Uhrlage). Gibt die SAM-Uhrlage zurueck oder null,
    /// wenn keine Segmentierung moeglich war (Aufrufer behaelt dann die geometrische
    /// Schaetzung). Reine Verdrahtung — die Logik liegt im MarkBoxSegmentationService.
    /// </summary>
    // Gibt das Segmentierungs-Ergebnis (inkl. Rohmaske) zurueck, damit der Aufrufer die
    // SAM-Maske sichtbar rendern kann. null, wenn keine Segmentierung moeglich war.
    private async Task<Infrastructure.Ai.Pipeline.BoxSegmentationResult?> TrySegmentMarkBoxAsync(
        OverlayGeometry overlay, byte[]? frameBytes)
    {
        if (_codingBoxSegmentation == null || frameBytes == null || frameBytes.Length == 0
            || overlay.Points.Count < 2)
            return null;
        try
        {
            var box = Application.Ai.NormalizedBoundingBox.FromPoints(
                overlay.Points.Select(p => new Domain.Models.NormalizedPoint(p.X, p.Y)).ToList());
            var calibration = _codingOverlayService?.Calibration;
            int dn = calibration?.NominalDiameterMm ?? 0;

            var result = await _codingBoxSegmentation.SegmentBoxAsync(
                frameBytes, box, dn, calibration, System.Threading.CancellationToken.None);
            if (result == null) return null;

            if (result.Quant.HeightMm.HasValue) overlay.Q1Mm = result.Quant.HeightMm.Value;
            if (result.Quant.WidthMm.HasValue) overlay.Q2Mm = result.Quant.WidthMm.Value;
            var cross = result.Quant.CrossSectionReductionPercent ?? result.Quant.ExtentPercent;
            if (cross.HasValue) overlay.FillPercent = cross.Value;
            if (!string.IsNullOrEmpty(result.Quant.ClockPosition)
                && double.TryParse(result.Quant.ClockPosition,
                    System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var clk))
                overlay.ClockFrom = clk;

            return result;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Mark-SAM] Segmentierung uebersprungen: {ex.Message}");
            return null;
        }
    }

    // Zeigt die Erkennung der Mark-Box sichtbar auf dem Codier-Canvas, BEVOR das
    // VSA-Codierfenster aufgeht. Bei einem BOGEN waere die SAM-Maske irrefuehrend
    // (sie deckt das ganze runde Rohr-Loch ab, nicht den Bogen-Rand - SAM/SAM3/Hough koennen
    // die Bogen-Kontur nicht treffen, empirisch belegt). Daher fuer Boegen einen GEOMETRIE-
    // MARKER am Fluchtpunkt zeichnen (wo das Rohr abknickt) statt der Maske. Fuer echte
    // Punktschaeden (Riss/Anschluss) die SAM-Maske wie bisher.
    private void ShowMarkSamMask(Infrastructure.Ai.Pipeline.BoxSegmentationResult result, OverlayGeometry? overlay)
    {
        try
        {
            var rect = GetCodingContentRect();
            if (rect.Width <= 0 || rect.Height <= 0)
                return;

            // BOX-SPEZIFISCH: is_bend ist frame-weit. Der Bogen-Marker darf NUR erscheinen,
            // wenn die GEZOGENE Box wirklich den Bogen meint - d.h. den Fluchtpunkt umschliesst
            // (die abknickende Rohroeffnung liegt am Fluchtpunkt). Ein Punktschaden an der Wand
            // liegt NICHT am Fluchtpunkt und behaelt seine SAM-Maske, auch wenn der Frame
            // zusaetzlich als Bogen gilt.
            if (result.IsBend && LiveDetectionGeometryMapper.BoxContainsVanishingPoint(overlay, result.VanishX, result.VanishY))
            {
                BendMarkerRenderer.Show(CodingOverlayCanvas, result.VanishX, result.VanishY, rect);
                return;
            }

            // WICHTIG: in das tatsaechliche Video-Rechteck rendern (Letterbox/Pillarbox-Raender),
            // NICHT in die volle Canvas-Flaeche - sonst Maske verzerrt/verschoben.
            var samResp = new Infrastructure.Ai.Pipeline.SamResponse(
                new[] { result.Mask }, result.ImageWidth, result.ImageHeight, 0);
            Ai.Pipeline.SamMaskRenderer.RenderMasks(
                CodingOverlayCanvas,
                samResp,
                new[] { result.Quant },
                rect.Width,
                rect.Height,
                logger: null,
                options: null,
                offsetX: rect.X,
                offsetY: rect.Y);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Mark-SAM] Masken-Render uebersprungen: {ex.Message}");
        }
    }

    /// <summary>
    /// Speichert eine Markierung als Teacher-Annotation (YOLO-Export + TeacherAnnotationStore).
    /// Eigenstaendige Implementierung im PlayerWindow-Codiermodus.
    /// </summary>
    /// <summary>Rueckgabe: true wenn gespeichert, false wenn abgebrochen.</summary>
    private async Task<bool> SaveMarkAsTrainingAsync(OverlayGeometry overlay, double timestampSec, string? clockPosition, byte[]? preCapturedFrame = null)
    {
        try
        {
            // 1. VSA-Code waehlen â€” VsaCodeExplorer oeffnet sich sofort
            // Meter automatisch aus OSD oder Videoposition berechnen
            var autoMeter = _codingLastOsdMeter ?? GetMeterFromVideoPosition();
            var entry = CodingExplorerEntryFactory.CreateSeed(overlay);
            var explorerVm = CreateVsaCodeExplorerViewModel(entry, autoMeter, TimeSpan.FromSeconds(timestampSec));
            var explorer = new Views.Windows.VsaCodeExplorerWindow(explorerVm, _videoPath, TimeSpan.FromSeconds(timestampSec))
            {
                Owner = this
            };
            if (explorer.ShowDialog() != true || explorer.SelectedEntry == null)
                return false;

            var selectedEntry = explorer.SelectedEntry;

            // Den selbst gesetzten Code SOFORT als KI-BEFUND eintragen — unabhaengig davon, ob
            // der nachfolgende Training-/YOLO-Export klappt. Sonst fehlt der Code in KI-BEFUNDE,
            // wenn der Export scheitert (User-Wunsch: jeder eigene Code MUSS erscheinen).
            CodingEvent? manualEvent = null;
            if (_codingSessionService != null && _codingVm != null)
            {
                var manualMeter = 0.0;
                if (double.TryParse(TxtCodingMeter?.Text?.Replace("m", "").Trim(),
                        System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out var pm0))
                    manualMeter = pm0;
                var manualEntry = CodingExplorerEntryFactory.CreateManualFromSelected(
                    selectedEntry,
                    manualMeter,
                    TimeSpan.FromSeconds(timestampSec));
                manualEvent = _codingSessionService.AddEvent(manualEntry, overlay);
                RefreshCodingEventsList();
            }

            // 2. Frame-Capture (bereits vor der SAM-Segmentierung erfasst -> wiederverwenden).
            var frameBytes = preCapturedFrame ?? await CaptureCurrentFrameAsync();
            if (frameBytes == null) return false;

            // 3. BoundingBox aus Overlay-Punkten
            var bbox = Application.Ai.NormalizedBoundingBox.FromPoints(
                overlay.Points.Select(p => new Domain.Models.NormalizedPoint(p.X, p.Y)).ToList());

            // Mindestgroesse pruefen (1% des Frames)
            if (bbox.Width < 0.01 || bbox.Height < 0.01) return false;

            // 4. YOLO-Export
            int classId = InfraTeacher.VsaYoloClassMap.GetClassId(selectedEntry.Code);
            var annotationId = Guid.NewGuid().ToString("N")[..12];
            var baseName = $"mark_{annotationId}";

            // Frame in Temp speichern
            var tempFrame = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(), $"sewer_studio_mark_{annotationId}.png");
            await System.IO.File.WriteAllBytesAsync(tempFrame, frameBytes);

            var exportService = Ai.Teacher.TrainingAnnotationExportServiceFactory.Create();
            var exportResult = await exportService.ExportAsync(tempFrame, bbox, selectedEntry.Code, classId, baseName);

            // Temp aufrÃ¤umen
            AuswertungPro.Next.Application.Common.BestEffort.Try(
                () => System.IO.File.Delete(tempFrame), "Mark-Training: Temp-Frame loeschen");

            // 5. TeacherAnnotation erstellen + persistieren
            var captureMeter = 0.0;
            if (double.TryParse(TxtCodingMeter?.Text?.Replace("m", "").Trim(), System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var parsedMeter))
                captureMeter = parsedMeter;

            var annotation = LiveDetectionTeacherAnnotationFactory.CreateManualMark(
                annotationId,
                selectedEntry,
                overlay,
                bbox,
                clockPosition,
                captureMeter,
                TimeSpan.FromSeconds(timestampSec),
                exportResult);

            await InfraTeacher.TeacherAnnotationStore.AppendAsync(annotation);

            // Foto nachtraeglich an den bereits eingetragenen Befund haengen (der Code wurde oben
            // schon SOFORT eingetragen, damit er auch bei Export-Fehlern in KI-BEFUNDE steht).
            if (manualEvent != null && exportResult.FullFramePath != null)
            {
                manualEvent.Entry.FotoPaths.Add(exportResult.FullFramePath);
                RefreshCodingEventsList();
            }

            ShowOsdMeterStatus($"✓ {selectedEntry.Code} gespeichert", resetAfterDelay: true);
            return true;
        }
        catch (Exception ex)
        {
            ShowOsdMeterStatus($"\u2717 Fehler: {ex.Message}", resetAfterDelay: false);
            return false;
        }
    }

    private void ShowOsdMeterStatus(string message, bool resetAfterDelay)
    {
        OsdMeterBadge.Visibility = Visibility.Visible;
        TxtOsdMeter.Text = message;

        if (!resetAfterDelay)
            return;

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

        var clockPosition = LiveDetectionGeometryMapper.ClickToClockPosition(clickPoint, canvasSize);
        var timestampSec = _player.Time / 1000.0;

        OpenCodeCatalogForMark(clockPosition, timestampSec, null);
        e.Handled = true;
    }

    private void OnFindingClicked(LiveFrameFinding finding, double timestampSec)
    {
        _player.SetPause(true);
        OpenCodeCatalogForMark(
            finding.PositionClock,
            timestampSec,
            finding.VsaCodeHint);
    }

    private void OpenCodeCatalogForMark(string? clockPosition, double timestampSec, string? suggestedCode)
    {
        var sp = _serviceProvider;

        if (sp?.CodeCatalog is null)
        {
            DialogHost.Current.Info(
                "Schadenscode-Katalog nicht verfügbar.\n" +
                "Bitte die App neu starten oder KI-Einstellungen prüfen.",
                "Markieren");
            return;
        }

        var entry = CodingExplorerEntryFactory.CreateSeed(
            videoTime: TimeSpan.FromSeconds(timestampSec),
            suggestedCode: suggestedCode,
            clockPosition: clockPosition);

        var explorerVm = CreateVsaCodeExplorerViewModel(
            entry,
            _codingLastOsdMeter ?? GetMeterFromVideoPosition(),
            TimeSpan.FromSeconds(timestampSec));

        var dlg = new VsaCodeExplorerWindow(explorerVm, _videoPath, TimeSpan.FromSeconds(timestampSec))
        {
            Owner = this
        };

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

            _onEntryCreated?.Invoke(entry);
            ShowOverlay($"Beobachtung erfasst: {entry.Code}", TimeSpan.FromSeconds(4));
        }
    }

    // ÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚ÂÃƒÂ¢Ã¢â‚¬Â¢Ã‚Â
}
