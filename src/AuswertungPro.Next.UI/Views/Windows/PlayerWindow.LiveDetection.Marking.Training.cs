using System;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Ai;
using InfraTeacher = AuswertungPro.Next.Infrastructure.Ai.Teacher;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    /// <summary>Rueckgabe: true wenn gespeichert, false wenn abgebrochen.</summary>
    private async Task<bool> SaveMarkAsTrainingAsync(OverlayGeometry overlay, double timestampSec, string? clockPosition, byte[]? preCapturedFrame = null)
    {
        try
        {
            var autoMeter = _codingLastOsdMeter ?? GetMeterFromVideoPosition();
            var selectedEntry = CodingCodeExplorerWorkflowServiceFactory.Create(CreateVsaCodeExplorerViewModel)
                .SelectSeed(
                    overlay,
                    autoMeter,
                    TimeSpan.FromSeconds(timestampSec),
                    _videoPath,
                    this);
            if (selectedEntry == null)
                return false;

            CodingEvent? manualEvent = null;
            if (_codingSessionService != null && _codingVm != null)
            {
                var manualMeter = CodingCurrentMeterResolver.ParseDisplayedMeterOrZero(TxtCodingMeter?.Text);
                var manualEntry = CodingExplorerEntryFactory.CreateManualFromSelected(
                    selectedEntry,
                    manualMeter,
                    TimeSpan.FromSeconds(timestampSec));
                manualEvent = _codingSessionService.AddEvent(manualEntry, overlay);
                RefreshCodingEventsList();
            }

            var frameBytes = preCapturedFrame ?? await CaptureCurrentFrameAsync();
            if (frameBytes == null)
                return false;

            var bbox = LiveDetectionGeometryMapper.BBoxFromOverlay(overlay);
            if (bbox.Width < 0.01 || bbox.Height < 0.01)
                return false;

            int classId = InfraTeacher.VsaYoloClassMap.GetClassId(selectedEntry.Code);
            var annotationId = Guid.NewGuid().ToString("N")[..12];
            var baseName = $"mark_{annotationId}";

            var frameExporter = new LiveDetectionTrainingFrameExporter(
                Ai.Teacher.TrainingAnnotationExportServiceFactory.Create());
            var exportResult = await frameExporter.ExportAsync(
                frameBytes,
                bbox,
                selectedEntry.Code,
                classId,
                baseName,
                annotationId);

            var captureMeter = CodingCurrentMeterResolver.ParseDisplayedMeterOrZero(TxtCodingMeter?.Text);

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

            if (manualEvent != null && exportResult.FullFramePath != null)
            {
                manualEvent.Entry.FotoPaths.Add(exportResult.FullFramePath);
                RefreshCodingEventsList();
            }

            ShowOsdMeterStatus($"\u2713 {selectedEntry.Code} gespeichert", resetAfterDelay: true);
            return true;
        }
        catch (Exception ex)
        {
            ShowOsdMeterStatus($"\u2717 Fehler: {ex.Message}", resetAfterDelay: false);
            return false;
        }
    }
}
