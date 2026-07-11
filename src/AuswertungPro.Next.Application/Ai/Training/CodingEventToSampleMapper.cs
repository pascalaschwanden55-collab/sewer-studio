using System;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;

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
    public static TrainingSample FromCodingEvent(
        CodingEvent ev,
        string caseId,
        string? framePath,
        DateTime? inspectionDate = null,
        string? confirmedByUser = null,
        DateTime? confirmedAtUtc = null,
        string? evidenceFramePath = null)
    {
        var decision = ev.AiContext?.Decision ?? ev.ReviewContext?.Decision;
        var status = decision.HasValue
            ? MapDecision(decision.Value)
            : TrainingSampleStatus.New;
        var isAiSuggestion = ev.AiContext is not null;

        var sourceType = ev.Entry.Source switch
        {
            ProtocolEntrySource.Manual => SourceTypeNames.ManualCoding,
            ProtocolEntrySource.Imported => SourceTypeNames.ImportedProtocol,
            _ => ev.Overlay is not null
                ? SourceTypeNames.TeacherAnnotation
                : SourceTypeNames.VideoTimestamp
        };

        var meterStart = Math.Round(ev.Entry.MeterStart ?? ev.MeterAtCapture, 1);
        var meterEnd = Math.Round(ev.Entry.MeterEnd ?? ev.MeterAtCapture, 1);
        var eligibility = TrainingSampleEligibility.Evaluate(inspectionDate);

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
            EvidenceFramePath = evidenceFramePath,
            Status = status,
            SourceType = sourceType,
            KiCode = isAiSuggestion ? ev.AiContext!.SuggestedCode : null,
            MatchLevel = isAiSuggestion
                ? DetermineMatchLevel(ev.AiContext!)
                : null,
            Notes = ev.AiContext?.Reason ?? ev.ReviewContext?.Reason ?? string.Empty,
            InspectionDate = inspectionDate,
            TrainingEligible = eligibility.IsEligible,
            TrainingEligibilityReason = eligibility.Reason,
            CodeMeta = GroundTruthProtocolEntryMapper.CloneCodeMeta(ev.Entry.CodeMeta),
            Signature = TrainingSample.BuildCanonicalSignature(caseId, ev.Entry.Code, meterStart, meterEnd),
            BboxXCenter = ExtractBboxField(ev.Overlay, bboxCenter: true, isX: true),
            BboxYCenter = ExtractBboxField(ev.Overlay, bboxCenter: true, isX: false),
            BboxWidth = ExtractBboxField(ev.Overlay, bboxCenter: false, isX: true),
            BboxHeight = ExtractBboxField(ev.Overlay, bboxCenter: false, isX: false),
            HumanConfirmed = decision switch
            {
                CodingUserDecision.Accepted or CodingUserDecision.AcceptedWithEdit => true,
                CodingUserDecision.Rejected => false,
                _ => (bool?)null
            },
            Corrected = isAiSuggestion ? ev.AiContext!.Decision switch
            {
                CodingUserDecision.AcceptedWithEdit => true,
                CodingUserDecision.Accepted or CodingUserDecision.Rejected => false,
                _ => (bool?)null
            } : null,
            ConfirmedByUser = confirmedByUser,
            ConfirmedAtUtc = confirmedAtUtc,
            QualityGateLevel = ev.AiContext?.QualityGateLevel
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
