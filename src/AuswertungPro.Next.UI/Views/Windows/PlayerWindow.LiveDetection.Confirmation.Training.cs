using System;
using System.Windows;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
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
            var annotationWriter = LiveDetectionTrainingAnnotationWriter.CreateDefault();

            foreach (var finding in _detectionPendingFindings)
            {
                await annotationWriter.SaveAcceptedAsync(
                    frameBytes,
                    finding,
                    TimeSpan.FromSeconds(timestampSec));
            }

            ShowOsdMeterStatus($"âœ“ {_detectionPendingFindings.Count} Befund(e) gespeichert", resetAfterDelay: true);
        }
        catch (Exception ex)
        {
            ShowOsdMeterStatus($"âœ— Fehler: {ex.Message}", resetAfterDelay: false);
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

            // VsaCodeExplorer oeffnen fuer Korrektur - Meter aus OSD/Video
            var autoMeter2 = _codingLastOsdMeter ?? GetMeterFromVideoPosition();
            var entry = CodingExplorerEntryFactory.CreateSeed();
            var explorerVm = CreateVsaCodeExplorerViewModel(entry, autoMeter2, TimeSpan.FromSeconds(timestampSec));
            var explorer = VsaCodeExplorerDialogServiceFactory.Create().Show(
                explorerVm,
                _videoPath,
                TimeSpan.FromSeconds(timestampSec),
                this);

            if (!explorer.Accepted || explorer.SelectedEntry == null)
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
            var annotationWriter = LiveDetectionTrainingAnnotationWriter.CreateDefault();
            await annotationWriter.SaveCorrectedAsync(
                frameBytes,
                primary,
                selectedEntry,
                TimeSpan.FromSeconds(timestampSecForFrame));

            ShowOsdMeterStatus($"âœ“ Training: {selectedEntry.Code} (korrigiert)", resetAfterDelay: true);
        }
        catch (Exception ex)
        {
            ShowOsdMeterStatus($"âœ— Fehler: {ex.Message}", resetAfterDelay: false);
        }

        ResumeDetection();
    }
}
