using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.Evaluation;
using AuswertungPro.Next.Application.Ai.QualityGate;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Application.Reports;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.Domain.VsaCatalog;
using AuswertungPro.Next.Infrastructure.Ai;
using AuswertungPro.Next.Infrastructure.Ai.Ollama;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;
using AuswertungPro.Next.Infrastructure.Ai.QualityGate;
using AuswertungPro.Next.Infrastructure.Ai.Shared;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Helpers;
using AuswertungPro.Next.UI.Services;
using AuswertungPro.Next.UI.ViewModels.Protocol;
using AuswertungPro.Next.UI.ViewModels.Windows;
using AppProtocol = AuswertungPro.Next.Application.Protocol;
using InfraSelfImproving = AuswertungPro.Next.Infrastructure.Ai.SelfImproving;
using InfraTeacher = AuswertungPro.Next.Infrastructure.Ai.Teacher;
using InfraTraining = AuswertungPro.Next.Infrastructure.Ai.Training;
using Rectangle = System.Windows.Shapes.Rectangle;

using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Views.Windows;

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

            var osdMeter = await CodingReadOsdMeterAsync();
            var meterValue = CodingCurrentMeterResolver.ResolveManualEntry(
                osdMeter,
                _codingLastOsdMeter,
                _player.Time,
                _player.Length,
                _codingVm.EndMeter,
                _codingVm.CurrentMeter);

            var entry = CodingExplorerEntryFactory.CreateSeed(
                _codingVm.CurrentOverlay,
                videoZeit);
            entry.MeterStart = meterValue;
            entry.MeterEnd = meterValue;

            var explorerVm = CreateVsaCodeExplorerViewModel(
                entry, meterValue, videoZeit);

            var dlg = new VsaCodeExplorerWindow(explorerVm, _videoPath, videoZeit)
            {
                Owner = this,
                // Live-Snapshot: Aktuelles VLC-Bild statt ffmpeg-Extraktion
                LiveSnapshotProvider = () =>
                {
                    var snapPath = CodingLiveSnapshotPathPolicy.CreateTempPath();
                    return TakeSnapshotSafe(snapPath) ? snapPath : null;
                }
            };

            if (dlg.ShowDialog() == true && dlg.SelectedEntry is not null)
            {
                var result = dlg.SelectedEntry;
                CodingProtocolEntryCopier.CopyEditableValues(result, entry);

                // Kein automatischer Snapshot hier — Foto wird manuell per "Foto"-Button
                // oder automatisch durch die KI-Analyse eingefuegt, wenn ein sinnvoller
                // Frame vorliegt (nicht die Dateneinblendung am Videoanfang).

                var createdEvent = _codingSessionService!.AddEvent(entry, _codingVm.CurrentOverlay);

                // Manuell codiert: Noch nicht bestaetigt — User muss "Akzeptieren" klicken.
                // Erst wenn alles gruen ist, stimmen die Daten fuer das KI-Training.
                createdEvent.AiContext = CodingManualEventFactory.CreateUnconfirmedContext(entry.Code);

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

        var draft = CodingManualEventFactory.CreateUnconfirmed(
            _codingVm.SelectedCode,
            _codingVm.SelectedCodeDescription,
            _codingLastOsdMeter ?? _codingVm.CurrentMeter,
            TimeSpan.FromMilliseconds(_player.Time),
            _codingVm.CurrentOverlay);

        var fotoPath = CodingCaptureSnapshot(draft.Entry);
        if (fotoPath != null)
            draft.Entry.FotoPaths.Add(fotoPath);

        var manualEvent = _codingSessionService!.AddEvent(draft.Entry, _codingVm.CurrentOverlay);
        manualEvent.AiContext = draft.AiContext;
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
    // --- Coding: Doppelklick zum Bearbeiten ---

    private void CodingEvents_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (LstCodingEvents.SelectedItem is not CodingEvent codingEvent) return;

        // Video pausieren waehrend Bearbeitung
        _player.SetPause(true);
        SuspendCodingOverlayInput();

        var entry = codingEvent.Entry;
        var explorerVm = CreateVsaCodeExplorerViewModel(
            entry, entry.MeterStart, entry.Zeit);

        var dlg = new VsaCodeExplorerWindow(explorerVm, _videoPath,
            TimeSpan.FromMilliseconds(_player.Time))
        {
            Owner = this,
            LiveSnapshotProvider = () =>
            {
                var snapPath = CodingLiveSnapshotPathPolicy.CreateTempPath();
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
            CodingProtocolEntryCopier.CopyEditableValues(result, entry);

            // Meter aktualisieren falls geaendert
            codingEvent.MeterAtCapture = entry.MeterStart ?? entry.MeterEnd ?? codingEvent.MeterAtCapture;
            codingEvent.VideoTimestamp = entry.Zeit ?? codingEvent.VideoTimestamp;
            _codingSessionService?.UpdateEvent(codingEvent.EventId, entry, codingEvent.Overlay);

            // Events-Liste neu binden um Anzeige zu aktualisieren
            RefreshCodingEventsList();
        }
    }


    private void CodingEventEdit_Click(object sender, RoutedEventArgs e)
    {
        if (LstCodingEvents.SelectedItem is CodingEvent ce)
            CodingEvents_DoubleClick(sender, null!); // Gleiche Logik wie Doppelklick
    }


    private void CodingEventSeek_Click(object sender, RoutedEventArgs e)
    {
        if (LstCodingEvents.SelectedItem is not CodingEvent codingEvent) return;
        if (_player != null && CodingEventSeekPolicy.TryGetSeekMilliseconds(codingEvent, out var milliseconds))
            _player.Time = milliseconds;
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
        if (!CodingStretchDamageClosePolicy.CanClose(startEvent.MeterAtCapture, currentMeter))
        {
            DialogHost.Current.Info(
                "Der aktuelle Meterstand muss gr��er sein als der Anfang des Streckenschadens.",
                "Streckenschaden");
            return;
        }

        var endEntry = CodingStreckenschadenEventFactory.CloseStart(startEvent.Entry, currentMeter);

        var endEvent = _codingSessionService.AddEvent(endEntry, null);
        endEvent.VideoTimestamp = _player != null
            ? TimeSpan.FromMilliseconds(_player.Time) : TimeSpan.Zero;

        // Event-Hook (OnSessionEventAdded) fuegt automatisch in _codingVm.Events ein.
        // KEIN explizites Events.Add() — sonst doppelt!
        RefreshCodingEventsList();

        // Status
        SetCodingAiState(
            CodingStretchDamageClosePolicy.BuildClosedStatusText(
                startEvent.Entry.Code,
                startEvent.MeterAtCapture,
                currentMeter),
            PlayerStatusColors.Success, "");
    }

    private void CodingEventDelete_Click(object sender, RoutedEventArgs e)
    {
        if (LstCodingEvents.SelectedItem is not CodingEvent codingEvent) return;
        SuspendCodingOverlayInput();
        bool confirm;
        try
        {
            confirm = DialogHost.Current.ConfirmWarn($"Ereignis '{codingEvent.Entry.Code}' l�schen?", "L�schen");
        }
        finally
        {
            ResumeCodingOverlayInput();
        }
        if (!confirm) return;

        _codingSessionService?.RemoveEvent(codingEvent.EventId);
        _codingVm?.Events.Remove(codingEvent);
        if (_codingVm != null && ReferenceEquals(_codingVm.SelectedDefect, codingEvent))
            _codingVm.SelectedDefect = null;
        HideInlineDefectDetail();
        RefreshCodingEventsList();
    }

    private void RefreshCodingEventsList()
    {
        if (_codingVm == null) return;

        var sorted = CodingEventDisplayOrderPolicy.Order(_codingVm.Events);

        var selected = LstCodingEvents.SelectedItem;
        _codingVm.Events.Clear();
        foreach (var ev in sorted)
            _codingVm.Events.Add(ev);

        LstCodingEvents.ItemsSource = null;
        LstCodingEvents.ItemsSource = _codingVm.Events;
        if (selected != null)
            LstCodingEvents.SelectedItem = selected;

        // Verzoeiert Einfaerbung nach Layout-Update
        Dispatcher.InvokeAsync(ColorizeCodingEventListItems, System.Windows.Threading.DispatcherPriority.Loaded);
        UpdateCodingStatistics();
    }
    /// <summary>Statistiken im Seitenpanel aktualisieren.</summary>
    private void UpdateCodingStatistics()
    {
        if (_codingVm == null) return;

        var summary = CodingStatisticsPolicy.Build(
            _codingVm.Events,
            CodingSessionViewModel.GetDefectStatus);

        RunCodingDefectCount.Text = summary.Total.ToString();
        RunCodingOpenCount.Text = summary.Open.ToString();
        TxtCodingStatAutoAccepted.Text = summary.AutoAccepted.ToString();
        TxtCodingStatPending.Text = summary.Pending.ToString();
        TxtCodingStatReviewRequired.Text = summary.ReviewRequired.ToString();
        TxtCodingStatAvgConfidence.Text = summary.AverageConfidenceText;
    }

}
