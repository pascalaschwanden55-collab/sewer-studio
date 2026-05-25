using System;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.Ai.Training;

/// <summary>
/// Mappt CodingEvents auf TrainingSamples.
/// Schliesst den Feedback-Loop: KI-Vorschlag, User-Entscheidung, Trainingsdaten.
/// </summary>
public static class CodingEventToSampleMapper
{
    /// <summary>Mappt CodingUserDecision auf TrainingSampleStatus.</summary>
    public static TrainingSampleStatus MapDecision(CodingUserDecision decision) => decision switch
    {
        CodingUserDecision.Accepted => TrainingSampleStatus.Approved,
        CodingUserDecision.AcceptedWithEdit => TrainingSampleStatus.Approved,
        CodingUserDecision.Rejected => TrainingSampleStatus.Rejected,
        CodingUserDecision.Ignored => TrainingSampleStatus.New,
        _ => TrainingSampleStatus.New
    };

    /// <summary>
    /// Erstellt ein TrainingSample aus einem CodingEvent.
    /// Enthaelt finalen Code, Meter-Position und KI-Kontext.
    /// </summary>
    public static TrainingSample FromCodingEvent(CodingEvent ev, string caseId, string? framePath)
    {
        // Ohne KI-Kontext landet das Sample in der Review-Queue (New), nicht direkt im Training.
        // Verhindert dass rein manuelle Codiereintraege ungesehen die Trainingsdaten erweitern.
        var status = ev.AiContext != null
            ? MapDecision(ev.AiContext.Decision)
            : TrainingSampleStatus.New;

        var sourceType = ev.Overlay != null
            ? SourceTypeNames.TeacherAnnotation
            : SourceTypeNames.VideoTimestamp;

        var meterStart = Math.Round(ev.Entry.MeterStart ?? ev.MeterAtCapture, 1);
        var meterEnd = Math.Round(ev.Entry.MeterEnd ?? ev.MeterAtCapture, 1);

        return new TrainingSample
        {
            SampleId = ev.EventId.ToString("N")[..12],
            CaseId = caseId,
            Code = ev.Entry.Code,
            Beschreibung = ev.Entry.Beschreibung,
            MeterStart = meterStart,
            MeterEnd = meterEnd,
            IsStreckenschaden = ev.Entry.IsStreckenschaden,
            TimeSeconds = ev.VideoTimestamp.TotalSeconds,
            FramePath = framePath ?? string.Empty,
            Status = status,
            SourceType = sourceType,
            KiCode = ev.AiContext?.SuggestedCode,
            MatchLevel = ev.AiContext != null
                ? DetermineMatchLevel(ev.AiContext)
                : MatchLevelNames.ExactMatch,
            Notes = ev.AiContext?.Reason ?? string.Empty,
            CodeMeta = GroundTruthProtocolEntryMapper.CloneCodeMeta(ev.Entry.CodeMeta),
            Signature = TrainingSample.BuildCanonicalSignature(caseId, ev.Entry.Code, meterStart, meterEnd),
            BboxXCenter = ExtractBboxField(ev.Overlay, bboxCenter: true, isX: true),
            BboxYCenter = ExtractBboxField(ev.Overlay, bboxCenter: true, isX: false),
            BboxWidth = ExtractBboxField(ev.Overlay, bboxCenter: false, isX: true),
            BboxHeight = ExtractBboxField(ev.Overlay, bboxCenter: false, isX: false)
        };
    }

    private static double? ExtractBboxField(OverlayGeometry? overlay, bool bboxCenter, bool isX)
    {
        if (overlay?.Points == null || overlay.Points.Count < 2) return null;
        if (overlay.ToolType != OverlayToolType.Rectangle) return null;

        double minX = double.MaxValue, minY = double.MaxValue;
        double maxX = double.MinValue, maxY = double.MinValue;
        foreach (var p in overlay.Points)
        {
            if (p.X < minX) minX = p.X;
            if (p.Y < minY) minY = p.Y;
            if (p.X > maxX) maxX = p.X;
            if (p.Y > maxY) maxY = p.Y;
        }

        var width = maxX - minX;
        var height = maxY - minY;
        if (width <= 0 || height <= 0) return null;

        return bboxCenter
            ? (isX ? minX + width / 2.0 : minY + height / 2.0)
            : (isX ? width : height);
    }

    private static string DetermineMatchLevel(CodingEventAiContext ai)
    {
        return ai.Decision switch
        {
            CodingUserDecision.Accepted => MatchLevelNames.ExactMatch,
            CodingUserDecision.AcceptedWithEdit => MatchLevelNames.PartialMatch,
            CodingUserDecision.Rejected => MatchLevelNames.Mismatch,
            CodingUserDecision.Ignored => MatchLevelNames.NoFindings,
            _ => MatchLevelNames.NoFindings
        };
    }
}
