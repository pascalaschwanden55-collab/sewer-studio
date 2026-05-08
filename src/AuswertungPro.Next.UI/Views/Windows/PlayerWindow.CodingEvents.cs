using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.Pipeline;
using AuswertungPro.Next.Application.Ai.Teacher;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.Reports;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Services;
using AuswertungPro.Next.UI.ViewModels.Protocol;
using AuswertungPro.Next.UI.ViewModels.Windows;
using AppProtocol = AuswertungPro.Next.Application.Protocol;

namespace AuswertungPro.Next.UI.Views.Windows;

// Phase 6.1.G: CodingEvents-Liste-Aktionen extrahiert aus PlayerWindow.xaml.cs.
//
// Enthaelt die Liste-Aktionen rund um die Codier-Befunde:
// - CodingSelectCode_Click: Schadenscode-Picker
// - CodingCreateEvent_Click: neuen Befund anlegen
// - CodingOfferPdfExport: PDF-Vorschlag nach Coding-Abschluss
// - CodingEvents_DoubleClick / Take/Show/Seek/CloseStretch/Delete-Handler
// - CodingEvents_SelectionChanged + Right-Click + InlineDefect
// - SyncMaskToBefundListe (Maske <-> Liste-Sync)
public partial class PlayerWindow
{
    private async void CodingSelectCode_Click(object sender, RoutedEventArgs e)
    {
        if (_codingVm == null) return;

        // Video pausieren
        _player.SetPause(true);
        SuspendCodingOverlayInput();

        try
        {
            var videoZeit = TimeSpan.FromMilliseconds(Math.Max(0, _player.Time));

            var timelineMeter = _codingVm.CurrentMeter;
            if (_player.Length > 0 && _codingVm.EndMeter > 0)
            {
                timelineMeter = Math.Round((_player.Time / (double)_player.Length) * _codingVm.EndMeter, 2);
            }

            var osdMeter = await CodingReadOsdMeterAsync();
            var meterValue = Math.Round(Math.Max(0, osdMeter ?? _codingLastOsdMeter ?? timelineMeter), 2);

            var entry = new ProtocolEntry
            {
                Source = ProtocolEntrySource.Manual,
                MeterStart = meterValue,
                MeterEnd = meterValue,
                Zeit = videoZeit
            };

            if (_codingVm.CurrentOverlay != null)
            {
                entry.CodeMeta ??= new ProtocolEntryCodeMeta();
                if (_codingVm.CurrentOverlay.ClockFrom.HasValue)
                    entry.CodeMeta.Parameters["vsa.uhr.von"] = _codingVm.CurrentOverlay.ClockFrom.Value.ToString("F1");
                if (_codingVm.CurrentOverlay.ClockTo.HasValue)
                    entry.CodeMeta.Parameters["vsa.uhr.bis"] = _codingVm.CurrentOverlay.ClockTo.Value.ToString("F1");
                if (_codingVm.CurrentOverlay.Q1Mm.HasValue)
                    entry.CodeMeta.Parameters["vsa.q1"] = _codingVm.CurrentOverlay.Q1Mm.Value.ToString("F1");
                if (_codingVm.CurrentOverlay.Q2Mm.HasValue)
                    entry.CodeMeta.Parameters["vsa.q2"] = _codingVm.CurrentOverlay.Q2Mm.Value.ToString("F1");
                if (_codingVm.CurrentOverlay.ArcDegrees.HasValue
                    && (_codingVm.CurrentOverlay.ToolType == OverlayToolType.PipeBend
                        || _codingVm.CurrentOverlay.ToolType == OverlayToolType.PipeDirection))
                    entry.CodeMeta.Parameters["vsa.winkel"] = _codingVm.CurrentOverlay.ArcDegrees.Value.ToString("F1");
                if (_codingVm.CurrentOverlay.FillPercent.HasValue)
                {
                    var key = _codingVm.CurrentOverlay.ToolType == OverlayToolType.Level
                              && _codingVm.CurrentOverlay.Points.Count >= 3
                        ? "vsa.querschnitt.prozent"
                        : "vsa.fuellgrad.prozent";
                    entry.CodeMeta.Parameters[key] = _codingVm.CurrentOverlay.FillPercent.Value.ToString("F1");
                }
            }

            var explorerVm = new ViewModels.Windows.VsaCodeExplorerViewModel(
                entry, meterValue, videoZeit);

            var dlg = new VsaCodeExplorerWindow(explorerVm, _videoPath, videoZeit)
            {
                Owner = this,
                // Live-Snapshot: Aktuelles VLC-Bild statt ffmpeg-Extraktion
                LiveSnapshotProvider = () =>
                {
                    var snapPath = Path.Combine(Path.GetTempPath(),
                        $"coding_live_{Guid.NewGuid():N}.png");
                    return TakeSnapshotSafe(snapPath) ? snapPath : null;
                }
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

                // Kein automatischer Snapshot hier — Foto wird manuell per "Foto"-Button
                // oder automatisch durch die KI-Analyse eingefuegt, wenn ein sinnvoller
                // Frame vorliegt (nicht die Dateneinblendung am Videoanfang).

                var createdEvent = _codingSessionService!.AddEvent(entry, _codingVm.CurrentOverlay);

                // Manuell codiert = direkt akzeptiert (User hat selbst entschieden)
                createdEvent.AiContext = new CodingEventAiContext
                {
                    SuggestedCode = entry.Code,
                    Confidence = 1.0,
                    Reason = "Manuell codiert",
                    Decision = CodingUserDecision.Accepted
                };

                // Sperrliste: KI soll diesen Befund nicht erneut melden
                _rejectedFindings.Add(MakeRejectionKey(entry.Code, entry.MeterStart ?? 0));

                // KI-Overlays raeumen — manuell codiert heisst erledigt
                Ai.Pipeline.SamMaskRenderer.ClearMasks(CodingOverlayCanvas);
                DetectionCanvas.Children.Clear();

                RefreshCodingEventsList();
                LstCodingEvents.SelectedItem = createdEvent;

                _codingSchemaManager.Cancel();
                _codingVm.CurrentOverlay = null;
                RedrawCodingCanvas(includeManualOverlay: false);
                TxtCodingSelectedCode.Text = "";
                BtnCodingCreateEvent.IsEnabled = false;
                UpdateCodingOverlayInfo(null);
            }
        }
        finally
        {
            ResumeCodingOverlayInput();
        }
    }
    private void CodingCreateEvent_Click(object sender, RoutedEventArgs e)
    {
        // Nur verwenden wenn Code manuell gesetzt (nicht ueber CodingSelectCode_Click,
        // denn dort wird AddEvent bereits direkt aufgerufen)
        if (_codingVm == null || string.IsNullOrWhiteSpace(_codingVm.SelectedCode)) return;

        // Videozeit vom Player uebernehmen
        _codingVm.CurrentVideoTime = TimeSpan.FromMilliseconds(_player.Time);

        // Foto vom Video-Frame
        var entry = new ProtocolEntry
        {
            Code = _codingVm.SelectedCode,
            Beschreibung = _codingVm.SelectedCodeDescription,
            MeterStart = _codingLastOsdMeter ?? _codingVm.CurrentMeter,
            Zeit = TimeSpan.FromMilliseconds(_player.Time),
            Source = ProtocolEntrySource.Manual
        };

        if (_codingVm.CurrentOverlay != null)
        {
            entry.CodeMeta ??= new ProtocolEntryCodeMeta();
            if (_codingVm.CurrentOverlay.ClockFrom.HasValue)
                entry.CodeMeta.Parameters["vsa.uhr.von"] = _codingVm.CurrentOverlay.ClockFrom.Value.ToString("F1");
            if (_codingVm.CurrentOverlay.ClockTo.HasValue)
                entry.CodeMeta.Parameters["vsa.uhr.bis"] = _codingVm.CurrentOverlay.ClockTo.Value.ToString("F1");
            if (_codingVm.CurrentOverlay.Q1Mm.HasValue)
                entry.CodeMeta.Parameters["vsa.q1"] = _codingVm.CurrentOverlay.Q1Mm.Value.ToString("F1");
            if (_codingVm.CurrentOverlay.Q2Mm.HasValue)
                entry.CodeMeta.Parameters["vsa.q2"] = _codingVm.CurrentOverlay.Q2Mm.Value.ToString("F1");
            if (_codingVm.CurrentOverlay.ArcDegrees.HasValue
                && (_codingVm.CurrentOverlay.ToolType == OverlayToolType.PipeBend
                    || _codingVm.CurrentOverlay.ToolType == OverlayToolType.PipeDirection))
                entry.CodeMeta.Parameters["vsa.winkel"] = _codingVm.CurrentOverlay.ArcDegrees.Value.ToString("F1");
            if (_codingVm.CurrentOverlay.FillPercent.HasValue)
            {
                var key = _codingVm.CurrentOverlay.ToolType == OverlayToolType.Level
                          && _codingVm.CurrentOverlay.Points.Count >= 3
                    ? "vsa.querschnitt.prozent"
                    : "vsa.fuellgrad.prozent";
                entry.CodeMeta.Parameters[key] = _codingVm.CurrentOverlay.FillPercent.Value.ToString("F1");
            }
        }

        var fotoPath = CodingCaptureSnapshot(entry);
        if (fotoPath != null)
            entry.FotoPaths.Add(fotoPath);

        var manualEvent = _codingSessionService!.AddEvent(entry, _codingVm.CurrentOverlay);

        // Manuell codiert = direkt akzeptiert (User hat selbst entschieden)
        manualEvent.AiContext = new CodingEventAiContext
        {
            SuggestedCode = entry.Code,
            Confidence = 1.0,
            Reason = "Manuell codiert",
            Decision = CodingUserDecision.Accepted
        };

        // Sperrliste: KI soll diesen Befund nicht erneut melden
        _rejectedFindings.Add(MakeRejectionKey(entry.Code, entry.MeterStart ?? 0));

        // KI-Overlays raeumen — manuell codiert heisst erledigt
        Ai.Pipeline.SamMaskRenderer.ClearMasks(CodingOverlayCanvas);
        DetectionCanvas.Children.Clear();

        // Nach Meter sortiert anzeigen
        RefreshCodingEventsList();

        // Reset
        _codingSchemaManager.Cancel();
        _codingVm.CurrentOverlay = null;
        _codingVm.SelectedCode = "";
        _codingVm.SelectedCodeDescription = "";
        RedrawCodingCanvas(includeManualOverlay: false);
        TxtCodingSelectedCode.Text = "";
        BtnCodingCreateEvent.IsEnabled = false;
        UpdateCodingOverlayInfo(null);
    }

    // --- Coding Foto-Aufnahme vom Video ---

    // Phase 6.1.A: CodingCaptureSnapshot nach PlayerWindow.Snapshot.cs migriert.

    // --- Coding PDF-Export ---

    private void CodingOfferPdfExport(ProtocolDocument doc)
    {
        if (_haltungRecord == null) return;

        var result = _dialogs.ShowMessage(
            $"Codier-Session abgeschlossen ({doc.Current.Entries.Count} Ereignisse).\n\n" +
            "MÃƒÂ¶chten Sie jetzt ein PDF-Protokoll mit Grafik und Fotos erstellen?",
            "PDF-Protokoll erstellen",
            MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes) return;

        try
        {
            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Title = "PDF-Protokoll speichern",
                Filter = "PDF-Dateien (*.pdf)|*.pdf",
                DefaultExt = ".pdf",
                FileName = $"Protokoll_{_haltungRecord.GetFieldValue("Haltungsname") ?? "Haltung"}_{DateTime.Now:yyyyMMdd}.pdf"
            };

            if (dlg.ShowDialog() != true) return;

            // Projektordner ermitteln (fuer Logo-Suche und relative Pfade)
            var projectRoot = "";
            if (!string.IsNullOrWhiteSpace(App.Resolve<AppSettings>().LastProjectPath))
                projectRoot = Path.GetDirectoryName(App.Resolve<AppSettings>().LastProjectPath) ?? "";

            // Logo suchen
            var logoPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Brand", "abwasser-uri-logo.png");
            var options = new HaltungsprotokollPdfOptions
            {
                IncludePhotos = true,
                IncludeHaltungsgrafik = true,
                LogoPathAbs = File.Exists(logoPath) ? logoPath : null
            };

            var project = ((ViewModels.ShellViewModel?)App.Current.MainWindow?.DataContext)?.Project;
            var pdf = App.Resolve<AuswertungPro.Next.Application.Reports.ProtocolPdfExporter>().BuildHaltungsprotokollPdf(
                project!, _haltungRecord, doc, projectRoot, options);
            File.WriteAllBytes(dlg.FileName, pdf);

            // PDF oeffnen via ProcessRunner-Foundation (Phase 4.4)
            AuswertungPro.Next.Application.Common.ProcessRunner.TryOpenWithDefaultProgram(dlg.FileName, out _);

            ShowOverlay("PDF-Protokoll erstellt", TimeSpan.FromSeconds(4));
        }
        catch (Exception ex)
        {
            _dialogs.ShowMessage($"PDF konnte nicht erstellt werden:\n{ex.Message}", "Fehler",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // --- Coding: Doppelklick zum Bearbeiten ---

    private void CodingEvents_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (LstCodingEvents.SelectedItem is not CodingEvent codingEvent) return;

        // Video pausieren waehrend Bearbeitung
        _player.SetPause(true);
        SuspendCodingOverlayInput();

        var entry = codingEvent.Entry;
        var explorerVm = new ViewModels.Windows.VsaCodeExplorerViewModel(
            entry, entry.MeterStart, entry.Zeit);

        var dlg = new VsaCodeExplorerWindow(explorerVm, _videoPath,
            TimeSpan.FromMilliseconds(_player.Time))
        {
            Owner = this,
            LiveSnapshotProvider = () =>
            {
                var snapPath = Path.Combine(Path.GetTempPath(),
                    $"coding_live_{Guid.NewGuid():N}.png");
                return TakeSnapshotSafe(snapPath) ? snapPath : null;
            }
        };

        bool? dialogResult;
        try
        {
            dialogResult = dlg.ShowDialog();
        }
        finally
        {
            ResumeCodingOverlayInput();
        }

        if (dialogResult == true && dlg.SelectedEntry is not null)
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

            // Meter aktualisieren falls geaendert
            codingEvent.MeterAtCapture = entry.MeterStart ?? entry.MeterEnd ?? codingEvent.MeterAtCapture;
            codingEvent.VideoTimestamp = entry.Zeit ?? codingEvent.VideoTimestamp;
            _codingSessionService?.UpdateEvent(codingEvent.EventId, entry, codingEvent.Overlay);

            // Events-Liste neu binden um Anzeige zu aktualisieren
            RefreshCodingEventsList();
        }
    }

    /// <summary>
    /// Erstellt Foto vom aktuellen Video-Frame fuer das ausgewaehlte Event (max 2 Fotos).
    /// </summary>
    private void CodingTakePhotoForSelectedEvent()
    {
        if (LstCodingEvents.SelectedItem is not CodingEvent codingEvent) return;

        var entry = codingEvent.Entry;
        var fotoPath = CodingCaptureSnapshot(entry);
        if (fotoPath == null)
        {
            ShowOverlay("Foto konnte nicht aufgenommen werden", TimeSpan.FromSeconds(3));
            return;
        }

        if (entry.FotoPaths.Count >= 2)
        {
            entry.FotoPaths[1] = fotoPath;
            ShowOverlay($"Foto 2 ersetzt: {Path.GetFileName(fotoPath)}", TimeSpan.FromSeconds(3));
        }
        else
        {
            entry.FotoPaths.Add(fotoPath);
            ShowOverlay($"Foto {entry.FotoPaths.Count}: {Path.GetFileName(fotoPath)}", TimeSpan.FromSeconds(3));
        }

        RefreshCodingEventsList();
    }

    private void CodingTakePhoto_Click(object sender, RoutedEventArgs e) => CodingTakePhotoForSelectedEvent();

    private void CodingEventEdit_Click(object sender, RoutedEventArgs e)
    {
        if (LstCodingEvents.SelectedItem is CodingEvent ce)
            CodingEvents_DoubleClick(sender, null!); // Gleiche Logik wie Doppelklick
    }

    private void CodingEventShowPhotos_Click(object sender, RoutedEventArgs e)
    {
        if (LstCodingEvents.SelectedItem is not CodingEvent codingEvent) return;
        var entry = codingEvent.Entry;
        if (entry.FotoPaths.Count == 0)
        {
            ShowOverlay("Keine Fotos vorhanden. Doppelklick zum Bearbeiten.", TimeSpan.FromSeconds(3));
            return;
        }

        // Einfaches Foto-Vorschau-Fenster
        var win = new Window
        {
            Title = $"Fotos - {entry.Code} @ {codingEvent.MeterAtCapture:F2}m",
            Width = 640, Height = 400,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = this,
            ResizeMode = ResizeMode.CanResizeWithGrip
        };

        var panel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(8) };
        var projectFolder = !string.IsNullOrEmpty(App.Resolve<AppSettings>().LastProjectPath)
            ? Path.GetDirectoryName(App.Resolve<AppSettings>().LastProjectPath) ?? ""
            : "";

        foreach (var fotoPath in entry.FotoPaths)
        {
            var resolved = Path.IsPathRooted(fotoPath) && File.Exists(fotoPath)
                ? fotoPath
                : (File.Exists(Path.Combine(projectFolder, fotoPath)) ? Path.Combine(projectFolder, fotoPath) : null);

            if (resolved == null) continue;

            try
            {
                var bi = new System.Windows.Media.Imaging.BitmapImage();
                bi.BeginInit();
                bi.UriSource = new Uri(resolved, UriKind.Absolute);
                bi.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                bi.DecodePixelHeight = 360;
                bi.EndInit();
                bi.Freeze();

                var img = new System.Windows.Controls.Image
                {
                    Source = bi,
                    Stretch = System.Windows.Media.Stretch.Uniform,
                    Margin = new Thickness(4),
                    MaxHeight = 360
                };
                panel.Children.Add(img);
            }
            catch { /* Bild nicht ladbar */ }
        }

        win.Content = new ScrollViewer { Content = panel, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto };
        WindowStateManager.Track(win);
        win.Show();
    }

    private void CodingEventSeek_Click(object sender, RoutedEventArgs e)
    {
        if (LstCodingEvents.SelectedItem is not CodingEvent codingEvent) return;
        if (_player != null && codingEvent.VideoTimestamp.TotalMilliseconds > 0)
            TrySeekRobust((long)codingEvent.VideoTimestamp.TotalMilliseconds);
    }

    /// <summary>
    /// Streckenschaden schliessen: Erstellt einen identischen Eintrag mit aktuellem Meterstand
    /// als Ende-Markierung. VSA-Konvention: gleicher Code, MeterEnd = aktuelle Position.
    /// </summary>
    private void CodingEventCloseStretch_Click(object sender, RoutedEventArgs e)
    {
        if (LstCodingEvents.SelectedItem is not CodingEvent startEvent) return;
        if (_codingSessionService == null || _codingVm == null) return;

        // Aktuellen Meterstand als Endpunkt
        double currentMeter = _codingVm.CurrentMeter;
        if (currentMeter <= (startEvent.MeterAtCapture + 0.01))
        {
            _dialogs.ShowMessage(
                "Der aktuelle Meterstand muss groesser sein als der Anfang des Streckenschadens.",
                "Streckenschaden", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        // Start-Event als Streckenschaden markieren
        startEvent.Entry.IsStreckenschaden = true;
        startEvent.Entry.MeterEnd = currentMeter;

        // Ende-Event erstellen (identischer Code)
        var endEntry = new AuswertungPro.Next.Domain.Protocol.ProtocolEntry
        {
            Code = startEvent.Entry.Code,
            Beschreibung = startEvent.Entry.Beschreibung + " (Ende)",
            MeterStart = currentMeter,
            IsStreckenschaden = true,
            Source = startEvent.Entry.Source,
            CodeMeta = startEvent.Entry.CodeMeta
        };

        var endEvent = _codingSessionService.AddEvent(endEntry, null);
        endEvent.VideoTimestamp = _player != null
            ? TimeSpan.FromMilliseconds(_player.Time) : TimeSpan.Zero;

        // Event-Hook (OnSessionEventAdded) fuegt automatisch in _codingVm.Events ein.
        // KEIN explizites Events.Add() — sonst doppelt!
        RefreshCodingEventsList();

        // Status
        SetCodingAiState(
            $"Streckenschaden geschlossen: {startEvent.Entry.Code} {startEvent.MeterAtCapture:F2}m – {currentMeter:F2}m",
            Color.FromRgb(0x22, 0xC5, 0x5E), "");
    }

    private void CodingEventDelete_Click(object sender, RoutedEventArgs e)
    {
        if (LstCodingEvents.SelectedItem is not CodingEvent codingEvent) return;
        SuspendCodingOverlayInput();
        MessageBoxResult confirm;
        try
        {
            confirm = _dialogs.ShowMessage($"Ereignis '{codingEvent.Entry.Code}' loeschen?", "Loeschen",
                MessageBoxButton.YesNo, MessageBoxImage.Question);
        }
        finally
        {
            ResumeCodingOverlayInput();
        }
        if (confirm != MessageBoxResult.Yes) return;

        _codingSessionService?.RemoveEvent(codingEvent.EventId);
        _codingVm?.Events.Remove(codingEvent);
        if (_codingVm != null && ReferenceEquals(_codingVm.SelectedDefect, codingEvent))
            _codingVm.SelectedDefect = null;
        CodingDefectDetailPanel.Visibility = Visibility.Collapsed;
        HideInlineDefectDetail();
        RefreshCodingEventsList();
    }

    // Phase 6.1.F Sub-D: RefreshCodingEventsList nach PlayerWindow.CodingMode.cs migriert.

    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    // Defekt-Detail-Panel, Aktionsbuttons, Statistik
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

    private void CodingEvents_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // Vorheriges vergroessertes Item zuruecksetzen (nicht wenn Maske die Selektion steuert)
        if (!_enlargeSuppressShrink)
            ShrinkEnlargedListItem();

        if (LstCodingEvents.SelectedItem is CodingEvent ev)
        {
            if (_codingVm != null) _codingVm.SelectedDefect = ev;
            UpdateCodingDefectDetailPanel(ev);
            UpdateInlineDefectDetail(ev);

            // Maske im Bild hervorheben die zum selektierten Befund gehoert
            SyncMaskToBefundListe(ev);
        }
        else
        {
            if (_codingVm != null) _codingVm.SelectedDefect = null;
            CodingDefectDetailPanel.Visibility = Visibility.Collapsed;
            HideInlineDefectDetail();
        }
    }

    /// <summary>Hebt die Maske hervor die zum selektierten CodingEvent gehoert.</summary>
    private void SyncMaskToBefundListe(CodingEvent ev)
    {
        if (_currentMmResult?.QuantifiedMasks is not { } masks) return;
        var evCode = ev.Entry.Code?.ToUpperInvariant() ?? "";
        if (string.IsNullOrEmpty(evCode)) return;

        // Maske finden die zum Code passt
        for (int i = 0; i < masks.Count; i++)
        {
            var tag = $"{Ai.Pipeline.SamMaskRenderer.MaskTag}_{i}";
            // Nur noch sichtbare Masken beruecksichtigen
            if (!CodingOverlayCanvas.Children.OfType<FrameworkElement>()
                .Any(el => tag.Equals(el.Tag as string)))
                continue;

            var maskCode = AuswertungPro.Next.Application.Ai.VsaCodeResolver.InferCodeFromLabel(masks[i].Label)?.ToUpperInvariant() ?? "";
            if (evCode == maskCode || evCode.StartsWith(maskCode) || maskCode.StartsWith(evCode))
            {
                _selectedMaskIndex = i;
                HighlightSelectedMask(i);
                return;
            }
        }
    }

    /// <summary>Mittlere Spalte: kompakte Defekt-Details inline anzeigen.</summary>
    private void UpdateInlineDefectDetail(CodingEvent ev)
    {
        TxtInlineDetailCode.Text = ev.Entry.Code;
        TxtInlineDetailDesc.Text = ev.Entry.Beschreibung;
        TxtInlineDetailDistance.Text = $"{ev.MeterAtCapture:F2}m";

        if (ev.AiContext != null)
        {
            double conf = ev.AiContext.Confidence;
            TxtInlineDetailConfidence.Text = $"{conf * 100:F0}%";
            TxtInlineDetailConfidence.Foreground =
                ViewModels.Windows.CodingSessionViewModel.GetConfidenceBrush(conf);
        }
        else
        {
            TxtInlineDetailConfidence.Text = "\u2013";
            TxtInlineDetailConfidence.Foreground =
                new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(0x94, 0xA3, 0xB8));
        }

        // Accept/Reject immer verfuegbar — auch fuer manuell erstellte Events
        BtnInlineAccept.Visibility = Visibility.Visible;
        BtnInlineReject.Visibility = Visibility.Visible;

        var status = ViewModels.Windows.CodingSessionViewModel.GetDefectStatus(ev);
        TxtInlineDetailStatus.Text = CodingStatusToDisplayText(status);

        // Mittlere Spalte einblenden
        CodingDefectDetailInline.Visibility = Visibility.Visible;
        ColDefectDetail.Width = new GridLength(180);
    }

    private void HideInlineDefectDetail()
    {
        CodingDefectDetailInline.Visibility = Visibility.Collapsed;
        ColDefectDetail.Width = new GridLength(0);
    }

    private void CodingEvents_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        var dep = e.OriginalSource as DependencyObject;
        while (dep != null && dep is not ListBoxItem)
        {
            // Run/Inline-Elemente sind kein Visual — LogicalTreeHelper als Fallback
            dep = dep is System.Windows.Media.Visual or System.Windows.Media.Media3D.Visual3D
                ? VisualTreeHelper.GetParent(dep)
                : LogicalTreeHelper.GetParent(dep);
        }

        if (dep is ListBoxItem item)
        {
            item.IsSelected = true;
            item.Focus();
        }
    }
}
