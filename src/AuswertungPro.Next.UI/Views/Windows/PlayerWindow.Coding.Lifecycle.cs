using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Ai;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Helpers;
using AuswertungPro.Next.UI.Services;
using AuswertungPro.Next.UI.ViewModels.Windows;
using InfraSelfImproving = AuswertungPro.Next.Infrastructure.Ai.SelfImproving;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private void CodingMode_Click(object sender, RoutedEventArgs e)
    {
        if (_haltungRecord == null)
        {
            DialogHost.Current.Info(
                "Codier-Modus benötigt eine Haltung.\n" +
                "Bitte das Video über die Datenseite mit einer Haltung öffnen.",
                "Codier-Modus");
            return;
        }

        EnterCodingMode();
    }

    private void EnterCodingMode()
    {
        if (_isCodingMode || _haltungRecord == null) return;
        _isCodingMode = true;
        ResetFrameReadiness();

        // Video pausieren
        _player.SetPause(true);

        if (_isDetecting)
        {
            StopLiveDetection();
            LiveDetectionButton.IsChecked = false;
        }

        LiveDetectionButton.Visibility = Visibility.Collapsed;
        LiveDetectionStatusText.Visibility = Visibility.Collapsed;

        // Session-Services erstellen
        _codingSessionService = CreateCodingSessionService();
        _codingOverlayService = new OverlayToolService();
        _codingSchemaManager.Cancel();
        _codingSchemaType = null;
        _codingVm = new CodingSessionViewModel(
            _codingSessionService,
            _codingOverlayService,
            new InfraSelfImproving.CodingFeedbackRecorder());
        _codingVm.VideoPath = _videoPath;
        _codingVm.PropertyChanged += CodingVm_PropertyChanged;

        // DN laden
        var dnCalibration = CodingDnCalibrationPolicy.Build(_haltungRecord.Fields);
        if (dnCalibration.Calibration != null)
            _codingOverlayService.SetCalibration(dnCalibration.Calibration);
        TxtCodingCalibDn.Text = dnCalibration.DnText;
        TxtCodingCalibStatus.Text = dnCalibration.CalibrationStatusText;

        // Fallback: Haltungslaenge pruefen, ggf. manuell abfragen
        EnsureHaltungslaenge(_haltungRecord);

        // Session starten
        try
        {
            _codingVm.StartSessionCommand.Execute(_haltungRecord);
        }
        catch (Exception ex)
        {
            DialogHost.Current.Warn(ex.Message, "Codier-Modus");
            ExitCodingMode();
            return;
        }

        // Pruefen ob Session tatsaechlich gestartet wurde
        // (StartSessionCommand faengt Fehler intern ab, z.B. fehlende Haltungslaenge)
        if (_codingSessionService.ActiveSession == null)
        {
            ExitCodingMode();
            return;
        }

        // Session pausieren (Video steht still, Schritt-Navigation)
        _codingSessionService.PauseSession();

        TxtCodingRange.Text = $"/ {_codingVm.EndMeter:F2}m";
        TxtCodingMeter.Text = "0.00m";

        // ALLE bestehenden Beobachtungen in Import-Referenz verschieben.
        // KI-Befunde-Liste startet LEER - KI erkennt frisch, User korrigiert.
        _lastCodingMatch = null;
        _codingProtocolMatchBuckets.Clear();
        UpdateCodingProtocolMatchSummary(null);
        CodingImportReferenceTransfer.MoveExistingEventsToImportReference(
            _codingVm.Events,
            _codingImportEvents);
        LstImportEvents.ItemsSource = _codingImportEvents;
        RunImportDefectCount.Text = _codingImportEvents.Count.ToString();

        // WICHTIG: Auch session.Events leeren, damit CompleteSession() nur neue
        // KI-Events enthaelt (Import-Events sind in _codingImportEvents gesichert).
        // Sonst: Duplikate im Protokoll (Import + neue KI-Events).
        _codingSessionService.ActiveSession?.Events.Clear();

        // KI-Events-Liste binden (startet leer)
        LstCodingEvents.ItemsSource = _codingVm.Events;
        RunCodingDefectCount.Text = "0";
        _codingBaselineSignature = CodingEventsSignatureBuilder.Build(_codingVm.Events);

        // Streckenschaden-Tracker zuruecksetzen: keine offenen Strecken aus der Vorsession.
        _streckenTracker.Reset();

        // Standard-Werkzeug: Rechteck-Markieren. So fuehrt JEDE im Codiermodus gezogene bbox
        // automatisch zu SAM-Segmentierung + Codefenster (HandleMarkDrawingComplete), ohne dass
        // erst ein "Markieren"-Werkzeug gewaehlt werden muss. Andere Werkzeuge (Bogen/Level/
        // Kalibrieren) ueberschreiben das bewusst per Auswahl. Video wird NICHT zwangspausiert
        // (anders als ActivateMarkTool) - der User faehrt und zieht bei Bedarf eine Box.
        _markToolType = OverlayToolType.Rectangle;
        TxtMarkToolName.Text = "Rechteck";
        TxtActiveToolLabel.Text = "Rechteck";
        if (_codingOverlayService != null)
            _codingOverlayService.ActiveTool = OverlayToolType.Rectangle;

        // UI einblenden
        CodingOverlayPopup.IsOpen = true;
        CodingOverlayCanvas.IsHitTestVisible = true;
        UpdateCodingOverlayViewport();
        UpdateCodingOverlayCursor();
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(UpdateCodingOverlayViewport));
        CodingSidePanel.Visibility = Visibility.Visible;
        CodingSidePanelColumn.Width = new GridLength(GetCodingSidePanelWidth());
        CodingToolbar.Visibility = Visibility.Visible;

        // PipeGraphTimeline einrichten und einblenden
        PipeTimeline.TotalLength = _codingVm.EndMeter;
        PipeTimeline.MeterAccessor = CodingTimelineMarkerAccessors.Meter;
        PipeTimeline.CodeAccessor = CodingTimelineMarkerAccessors.Code;
        PipeTimeline.ConfidenceAccessor = CodingTimelineMarkerAccessors.Confidence;
        PipeTimeline.IsRejectedAccessor = CodingTimelineMarkerAccessors.IsRejected;
        PipeTimeline.Markers = _codingVm.Events;
        PipeTimeline.NavigateToMeterCommand = new CommunityToolkit.Mvvm.Input.RelayCommand<double>(meter =>
        {
            if (_codingSessionService != null && (_codingVm.IsRunning || _codingVm.IsPaused))
            {
                _codingSessionService.MoveToMeter(meter);
                _codingNavPending = true;
                SyncVideoToCodingMeter();
            }
        });
        PipeTimeline.MarkerClickedCommand = new CommunityToolkit.Mvvm.Input.RelayCommand<object>(item =>
        {
            if (item is CodingEvent ce)
            {
                _codingVm.JumpToDefectCommand.Execute(ce);
                LstCodingEvents.SelectedItem = ce;
            }
        });
        CodingTimelinePanel.Visibility = Visibility.Visible;

        // KI initialisieren + OSD-Timer starten
        InitCodingAi().SafeFireAndForget("InitCodingAi");
        StartCodingOsdTimer();

        // OSD-Badge sofort sichtbar
        OsdMeterBadge.Visibility = Visibility.Visible;
        TxtOsdMeter.Text = "OSD: --";

        // Bestehende Protokoll-Eintraege direkt in Import-Referenz laden
        // (NICHT in KI-Befunde - die startet leer)
        LoadExistingProtocolEventsAsImport();

        // Video an Anfang setzen (direkt, nicht ueber PropertyChanged)
        _codingNavPending = true;
        SyncVideoToCodingMeter();
    }

    /// <summary>
    /// Laedt bestehende ProtocolEntry-Eintraege aus HaltungRecord in die Import-Referenz-Liste.
    /// KI-Befunde-Liste bleibt leer (KI erkennt frisch).
    /// </summary>
    private void LoadExistingProtocolEventsAsImport()
    {
        if (_haltungRecord?.Protocol?.Current?.Entries == null) return;

        foreach (var codingEvent in CodingProtocolEventMapper.BuildMissingImportEvents(
                     _haltungRecord.Protocol,
                     _codingImportEvents))
            _codingImportEvents.Add(codingEvent);

        RunImportDefectCount.Text = _codingImportEvents.Count.ToString();
    }

}
