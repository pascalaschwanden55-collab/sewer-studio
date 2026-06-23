using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.Evaluation;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Services;
using InfraTeacher = AuswertungPro.Next.Infrastructure.Ai.Teacher;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private void ImportEvents_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (LstImportEvents.SelectedItem is not CodingEvent importEvent) return;
        SeekToImportEvent(importEvent);
    }

    private void ImportSeek_Click(object sender, RoutedEventArgs e)
    {
        if (LstImportEvents.SelectedItem is not CodingEvent importEvent) return;
        SeekToImportEvent(importEvent);
    }

    private void SeekToImportEvent(CodingEvent importEvent)
    {
        if (_player != null && CodingEventSeekPolicy.TryGetSeekMilliseconds(importEvent, out var milliseconds))
            _player.Time = milliseconds;
        else if (_codingSessionService != null && importEvent.MeterAtCapture > 0)
        {
            _codingSessionService.MoveToMeter(importEvent.MeterAtCapture);
            _codingNavPending = true;
            SyncVideoToCodingMeter();
        }
    }

    private void RunCodingProtocolMatch_Click(object sender, RoutedEventArgs e)
    {
        RunCodingProtocolMatch();
    }

    private void RunCodingProtocolMatch()
    {
        if (_codingVm == null) return;

        _lastCodingMatch = CodingProtocolMatchService.Match(
            _codingImportEvents.Select(ev => ev.Entry).ToList(),
            _codingVm.Events.Select(ev => ev.Entry).ToList());

        CodingProtocolMatchBucketBuilder.Rebuild(_codingProtocolMatchBuckets, _lastCodingMatch);
        UpdateCodingProtocolMatchSummary(_lastCodingMatch);
        RefreshCodingEventsList();
        Dispatcher.InvokeAsync(ApplyCodingProtocolMatchListHighlights, DispatcherPriority.Loaded);
    }

    private void UpdateCodingProtocolMatchSummary(CodingMatchRouting? routing)
    {
        TxtCodingProtocolMatchSummary.Text = CodingProtocolMatchSummaryFormatter.Format(routing);
        BtnAcceptGreenCodingMatches.IsEnabled = CodingProtocolMatchSummaryFormatter.CanAcceptGreenMatches(routing);
    }

    private async void CodingAcceptGreenMatches_Click(object sender, RoutedEventArgs e)
    {
        if (_codingVm == null) return;
        if (_lastCodingMatch == null)
            RunCodingProtocolMatch();
        if (_lastCodingMatch == null || _lastCodingMatch.Trainingskandidaten.Count == 0)
            return;

        var accepted = 0;
        foreach (var importEvent in CodingProtocolTrainingCandidateResolver.ResolveImportEvents(
                     _lastCodingMatch.Trainingskandidaten,
                     _codingImportEvents))
        {
            if (await ConfirmImportAsTrainingAsync(importEvent))
                accepted++;
        }

        var overlay = CodingProtocolMatchDisplayPolicy.BuildAcceptedGreenMatchesOverlay(accepted);
        ShowOverlay(overlay.Text, overlay.Duration);
    }

    private async void ImportConfirm_Click(object sender, RoutedEventArgs e)
    {
        if (LstImportEvents.SelectedItem is not CodingEvent importEvent) return;
        await ConfirmImportAsTrainingAsync(importEvent);
    }

    private async Task<bool> ConfirmImportAsTrainingAsync(CodingEvent importEvent)
    {
        SeekToImportEvent(importEvent);
        await Task.Delay(200);

        if (!TryTakeSnapshot(out var snapshotPath) || !System.IO.File.Exists(snapshotPath))
        {
            DialogHost.Current.Warn("Frame konnte nicht aufgenommen werden.\nBitte pruefen Sie ob das Video laeuft.",
                "Import bestaetigen");
            return false;
        }

        var imagesDir = InfraTeacher.TeacherAnnotationStore.GetImagesDir();
        var annotationId = Guid.NewGuid().ToString("N")[..12];
        var destFrame = System.IO.Path.Combine(imagesDir, $"mark_{annotationId}.png");
        System.IO.File.Copy(snapshotPath, destFrame, overwrite: true);

        var annotation = LiveDetectionTeacherAnnotationFactory.CreateImportConfirmation(
            annotationId,
            importEvent,
            destFrame);

        await InfraTeacher.TeacherAnnotationStore.AppendAsync(annotation);

        AuswertungPro.Next.Application.Common.BestEffort.Try(
            () => System.IO.File.Delete(snapshotPath),
            "Foto/Snapshot: Temp loeschen");
        var badge = CodingProtocolMatchDisplayPolicy.BuildImportConfirmationBadge(
            importEvent.Entry.Code,
            importEvent.MeterAtCapture);
        OsdMeterBadge.Visibility = Visibility.Visible;
        TxtOsdMeter.Text = badge.Text;
        var resetTimer = new DispatcherTimer { Interval = badge.AutoHideDelay };
        resetTimer.Tick += (_, _) => { OsdMeterBadge.Visibility = Visibility.Collapsed; resetTimer.Stop(); };
        resetTimer.Start();
        return true;
    }

}
