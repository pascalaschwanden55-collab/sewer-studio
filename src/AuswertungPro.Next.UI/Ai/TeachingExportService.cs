using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai.KnowledgeBase;
using AuswertungPro.Next.UI.Ai.Pipeline;
using AuswertungPro.Next.UI.Ai.Training;
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.Ai;

/// <summary>
/// Exportiert User-Zeichnungen in die drei Lern-Pfade:
/// 1. YOLO-Annotation (Frame + BBox → Sidecar oder lokal)
/// 2. Knowledge Base (Embedding + VSA-Code)
/// 3. Qwen Few-Shot (ProtocolTrainingStore)
/// </summary>
public sealed class TeachingExportService
{
    private readonly VisionPipelineClient? _pipelineClient;
    private readonly KnowledgeBaseManager? _kbManager;

    public TeachingExportService(VisionPipelineClient? pipelineClient, KnowledgeBaseManager? kbManager)
    {
        _pipelineClient = pipelineClient;
        _kbManager = kbManager;
    }

    /// <summary>
    /// Alle drei Lern-Pfade ausfuehren (fire-and-forget geeignet).
    /// </summary>
    public async Task ExportAllAsync(
        byte[] framePng,
        CodingEvent codingEvent,
        string haltungId,
        CancellationToken ct = default)
    {
        if (codingEvent.Entry.Code is null or "") return;

        var tasks = new List<Task>();

        // 1. YOLO-Annotation
        if (codingEvent.Overlay != null && _pipelineClient != null)
            tasks.Add(ExportYoloAnnotationAsync(framePng, codingEvent.Overlay, codingEvent.Entry.Code, ct));

        // 2. Knowledge Base
        if (_kbManager != null)
            tasks.Add(IndexToKnowledgeBaseAsync(codingEvent, haltungId, ct));

        // 3. Qwen Few-Shot
        StoreAsProtocolTrainingSample(codingEvent, haltungId);

        if (tasks.Count > 0)
            await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    /// <summary>
    /// OverlayGeometry → YOLO BBox-Label + Frame als Training-Sample exportieren.
    /// </summary>
    public async Task ExportYoloAnnotationAsync(
        byte[] framePng,
        OverlayGeometry geometry,
        string vsaCode,
        CancellationToken ct)
    {
        if (_pipelineClient == null || framePng.Length == 0) return;

        var label = GeometryToYoloLabel(geometry, vsaCode);
        if (label == null) return;

        var sample = new TrainingExportSample(
            Convert.ToBase64String(framePng),
            new[] { label });

        var request = new TrainingExportRequestDto(
            new[] { sample },
            "", // Sidecar waehlt Standard-Verzeichnis
            0.8);

        try
        {
            await _pipelineClient.ExportTrainingAsync(request, ct).ConfigureAwait(false);
        }
        catch
        {
            // Training-Export ist nicht kritisch – Fehler leise ignorieren
        }
    }

    /// <summary>
    /// CodingEvent → Knowledge Base indizieren (Embedding + Code).
    /// </summary>
    public async Task IndexToKnowledgeBaseAsync(
        CodingEvent codingEvent,
        string haltungId,
        CancellationToken ct)
    {
        if (_kbManager == null) return;

        var entry = codingEvent.Entry;
        var sample = new TrainingSample
        {
            SampleId = $"teaching_{codingEvent.EventId:N}",
            CaseId = haltungId,
            Code = entry.Code ?? "",
            Beschreibung = entry.Beschreibung ?? entry.Code ?? "",
            MeterStart = entry.MeterStart ?? codingEvent.MeterAtCapture,
            MeterEnd = entry.MeterEnd ?? codingEvent.MeterAtCapture,
            IsStreckenschaden = entry.IsStreckenschaden,
            TimeSeconds = codingEvent.VideoTimestamp.TotalSeconds,
            Status = TrainingSampleStatus.Approved
        };

        try
        {
            await _kbManager.IndexSampleAsync(sample, ct).ConfigureAwait(false);
        }
        catch
        {
            // KB-Index ist nicht kritisch
        }
    }

    /// <summary>
    /// CodingEvent → ProtocolTrainingStore (Qwen Few-Shot Beispiel).
    /// </summary>
    public static void StoreAsProtocolTrainingSample(CodingEvent codingEvent, string haltungId)
    {
        try
        {
            ProtocolTrainingStore.AddSample(codingEvent.Entry, haltungId);
        }
        catch
        {
            // Training-Store ist nicht kritisch
        }
    }

    /// <summary>
    /// OverlayGeometry → YOLO-Label (normierte BBox aus beliebigem Werkzeugtyp).
    /// </summary>
    public static TrainingExportSampleLabel? GeometryToYoloLabel(OverlayGeometry geo, string className)
    {
        if (geo.Points.Count == 0 || string.IsNullOrWhiteSpace(className))
            return null;

        // Bounding Box um alle Punkte berechnen
        double minX = geo.Points.Min(p => p.X);
        double maxX = geo.Points.Max(p => p.X);
        double minY = geo.Points.Min(p => p.Y);
        double maxY = geo.Points.Max(p => p.Y);

        double w = maxX - minX;
        double h = maxY - minY;

        // Mindestgroesse fuer sinnvolle BBox
        if (w < 0.01) w = 0.05;
        if (h < 0.01) h = 0.05;

        return new TrainingExportSampleLabel(
            ClassName: className,
            XCenter: (minX + maxX) / 2.0,
            YCenter: (minY + maxY) / 2.0,
            Width: w,
            Height: h);
    }
}
