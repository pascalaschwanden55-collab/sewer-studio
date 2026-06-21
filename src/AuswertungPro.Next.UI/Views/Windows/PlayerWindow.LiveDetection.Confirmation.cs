using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.UI.Ai;
using InfraTeacher = AuswertungPro.Next.Infrastructure.Ai.Teacher;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private void ShowDetectionConfirmation(IReadOnlyList<LiveFrameFinding> findings)
    {
        if (findings.Count == 0) return;

        // Video pausieren und zur Fundstelle springen
        if (_player != null && _player.IsPlaying)
            _player.SetPause(true);

        // Zur Fundstelle springen (Timestamp aus dem analysierten Frame)
        if (_detectionPendingTimestampSec.HasValue && _player != null)
        {
            long targetMs = (long)(_detectionPendingTimestampSec.Value * 1000);
            _player.Time = targetMs;
        }

        TxtDetectionFinding.Text = LiveDetectionDisplayPolicy.BuildDetectionConfirmationTitle(findings);
        TxtDetectionDetail.Text = LiveDetectionDisplayPolicy.BuildDetectionConfirmationDetails(findings);

        DetectionConfirmationPanel.Visibility = Visibility.Visible;
    }

    private void ResumeDetection()
    {
        _detectionPendingFindings = null;
        _detectionPendingFrameBytes = null;
        _detectionPendingTimestampSec = null;
        DetectionConfirmationPanel.Visibility = Visibility.Collapsed;

        // Video automatisch weiterlaufen lassen nach Entscheidung
        if (_player != null && !_player.IsPlaying)
            _player.Play();
    }

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
            var exportService = Ai.Teacher.TrainingAnnotationExportServiceFactory.Create();

            foreach (var finding in _detectionPendingFindings)
            {
                var code = finding.VsaCodeHint ?? finding.Label;
                int classId = InfraTeacher.VsaYoloClassMap.GetClassId(code);
                var annotationId = Guid.NewGuid().ToString("N")[..12];
                var baseName = $"det_{annotationId}";

                // Bounding-Box aus Uhrposition ableiten (Ring-Sektor -> normalisierte Koordinaten)
                var bbox = LiveDetectionGeometryMapper.BBoxFromClockPosition(finding);

                // Frame temp speichern
                var tempFrame = System.IO.Path.Combine(
                    System.IO.Path.GetTempPath(), $"sewer_studio_det_{annotationId}.png");
                await System.IO.File.WriteAllBytesAsync(tempFrame, frameBytes);

                var exportResult = await exportService.ExportAsync(tempFrame, bbox, code, classId, baseName);
                AuswertungPro.Next.Application.Common.BestEffort.Try(
                    () => System.IO.File.Delete(tempFrame), "Mark-Training: Temp-Frame loeschen");

                var annotation = LiveDetectionTeacherAnnotationFactory.CreateDetection(
                    annotationId,
                    finding,
                    code,
                    bbox,
                    TimeSpan.FromSeconds(timestampSec),
                    exportResult);
                await InfraTeacher.TeacherAnnotationStore.AppendAsync(annotation);
            }

            ShowOsdMeterStatus($"✓ {_detectionPendingFindings.Count} Befund(e) gespeichert", resetAfterDelay: true);
        }
        catch (Exception ex)
        {
            ShowOsdMeterStatus($"✗ Fehler: {ex.Message}", resetAfterDelay: false);
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
            var bbox = LiveDetectionGeometryMapper.BBoxFromClockPosition(primary);

            int classId = InfraTeacher.VsaYoloClassMap.GetClassId(selectedEntry.Code);
            var annotationId = Guid.NewGuid().ToString("N")[..12];
            var baseName = $"det_corr_{annotationId}";

            var tempFrame = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(), $"sewer_studio_det_{annotationId}.png");
            await System.IO.File.WriteAllBytesAsync(tempFrame, frameBytes);

            var exportService = Ai.Teacher.TrainingAnnotationExportServiceFactory.Create();
            var exportResult = await exportService.ExportAsync(tempFrame, bbox, selectedEntry.Code, classId, baseName);
            AuswertungPro.Next.Application.Common.BestEffort.Try(
                () => System.IO.File.Delete(tempFrame), "Mark-Training: Temp-Frame loeschen");

            var annotation = LiveDetectionTeacherAnnotationFactory.CreateCorrectedDetection(
                annotationId,
                primary,
                selectedEntry,
                bbox,
                TimeSpan.FromSeconds(timestampSecForFrame),
                exportResult);
            await InfraTeacher.TeacherAnnotationStore.AppendAsync(annotation);

            ShowOsdMeterStatus($"✓ Training: {selectedEntry.Code} (korrigiert)", resetAfterDelay: true);
        }
        catch (Exception ex)
        {
            ShowOsdMeterStatus($"✗ Fehler: {ex.Message}", resetAfterDelay: false);
        }

        ResumeDetection();
    }

    private void DetectionSkip_Click(object sender, RoutedEventArgs e)
    {
        ResumeDetection();
    }
}
