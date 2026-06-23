using System;
using System.Windows;
using AuswertungPro.Next.UI.Ai;
using InfraTeacher = AuswertungPro.Next.Infrastructure.Ai.Teacher;

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
            var frameExporter = new LiveDetectionTrainingFrameExporter(
                AuswertungPro.Next.UI.Ai.Teacher.TrainingAnnotationExportServiceFactory.Create());

            foreach (var finding in _detectionPendingFindings)
            {
                var annotationId = LiveDetectionTrainingExportPlanner.CreateAnnotationId();
                var exportPlan = LiveDetectionTrainingExportPlanner.BuildAccepted(finding, annotationId);

                var exportResult = await frameExporter.ExportAsync(
                    frameBytes,
                    exportPlan.BoundingBox,
                    exportPlan.Code,
                    exportPlan.ClassId,
                    exportPlan.BaseName,
                    annotationId);

                var annotation = LiveDetectionTeacherAnnotationFactory.CreateDetection(
                    annotationId,
                    finding,
                    exportPlan.Code,
                    exportPlan.BoundingBox,
                    TimeSpan.FromSeconds(timestampSec),
                    exportResult);
                await InfraTeacher.TeacherAnnotationStore.AppendAsync(annotation);
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
            var annotationId = LiveDetectionTrainingExportPlanner.CreateAnnotationId();
            var exportPlan = LiveDetectionTrainingExportPlanner.BuildCorrected(primary, selectedEntry.Code, annotationId);

            var frameExporter = new LiveDetectionTrainingFrameExporter(
                AuswertungPro.Next.UI.Ai.Teacher.TrainingAnnotationExportServiceFactory.Create());
            var exportResult = await frameExporter.ExportAsync(
                frameBytes,
                exportPlan.BoundingBox,
                exportPlan.Code,
                exportPlan.ClassId,
                exportPlan.BaseName,
                annotationId);

            var annotation = LiveDetectionTeacherAnnotationFactory.CreateCorrectedDetection(
                annotationId,
                primary,
                selectedEntry,
                exportPlan.BoundingBox,
                TimeSpan.FromSeconds(timestampSecForFrame),
                exportResult);
            await InfraTeacher.TeacherAnnotationStore.AppendAsync(annotation);

            ShowOsdMeterStatus($"âœ“ Training: {selectedEntry.Code} (korrigiert)", resetAfterDelay: true);
        }
        catch (Exception ex)
        {
            ShowOsdMeterStatus($"âœ— Fehler: {ex.Message}", resetAfterDelay: false);
        }

        ResumeDetection();
    }
}
