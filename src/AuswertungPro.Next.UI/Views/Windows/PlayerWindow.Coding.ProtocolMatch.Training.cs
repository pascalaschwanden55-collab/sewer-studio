using System;
using System.Threading.Tasks;
using System.Windows;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Player;
using AuswertungPro.Next.UI.Services;
using InfraTeacher = AuswertungPro.Next.Infrastructure.Ai.Teacher;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
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
        var resetTimer = PlayerWindowTimerFactory.CreateOneShotTimer(
            badge.AutoHideDelay,
            () => OsdMeterBadge.Visibility = Visibility.Collapsed);
        resetTimer.Start();
        return true;
    }
}
