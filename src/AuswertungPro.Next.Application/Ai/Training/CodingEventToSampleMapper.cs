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
        var isPersonalAcceptance =
            (decision is CodingUserDecision.Accepted or CodingUserDecision.AcceptedWithEdit)
            && !string.IsNullOrWhiteSpace(confirmedByUser)
            && confirmedAtUtc.HasValue;

        var sourceType = isPersonalAcceptance
            ? SourceTypeNames.ManualCoding
            : ev.Entry.Source switch
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

        var sample = new TrainingSample
        {
            SampleId = ev.EventId.ToString("N")[..12],
            CaseId = caseId,
            Code = ev.Entry.Code,
            Beschreibung = BuildKnowledgeDescription(
                ev.Entry.Code,
                ev.Entry.Beschreibung,
                isPersonalAcceptance),
            MeterStart = meterStart,
            MeterEnd = meterEnd,
            IsStreckenschaden = ev.Entry.IsStreckenschaden,
            TimeSeconds = ev.VideoTimestamp.TotalSeconds,
            FramePath = framePath ?? string.Empty,
            EvidenceFramePath = evidenceFramePath,
            Status = status,
            SourceType = sourceType,
            // War beim Codieren ein Modellvorschlag sichtbar? Nur ohne Vorschlag
            // entstandene Samples duerfen spaeter ein Modell messen.
            SuggestionProvenance = BuildSuggestionProvenance(ev),
            KiCode = isAiSuggestion ? ev.AiContext!.SuggestedCode : null,
            MatchLevel = isPersonalAcceptance
                ? decision == CodingUserDecision.AcceptedWithEdit
                    ? MatchLevelNames.ReviewCorrected
                    : MatchLevelNames.ReviewApproved
                : isAiSuggestion
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
            Corrected = decision switch
            {
                CodingUserDecision.AcceptedWithEdit => true,
                CodingUserDecision.Accepted or CodingUserDecision.Rejected => false,
                _ => (bool?)null
            },
            ConfirmedByUser = confirmedByUser,
            ConfirmedAtUtc = confirmedAtUtc,
            QualityGateLevel = ev.AiContext?.QualityGateLevel,
            CentralDecision = AiDecisionAuditCloner.Clone(ev.AiContext?.CentralDecision)
        };

        // Strenge Formatpruefung VOR Uebernahme: eine formal defekte SAM-Maske (defekte
        // Tokens, falsche Laufsumme, Leermaske) wird NICHT gespeichert. Das Sample bleibt
        // sichtbar unvollstaendig und landet zur Nachruestung in 'Unvollstaendige
        // Goldframes' — gewollt, statt eine faule Maske in KB/Training zu tragen.
        var overlayMask = ev.Overlay?.SamMask;
        var hasValidOverlayMask = SamMaskFormatValidator.IsValid(
            overlayMask?.MaskRle,
            overlayMask?.ImageWidth,
            overlayMask?.ImageHeight,
            out _);
        var maskRle = hasValidOverlayMask
            ? overlayMask!.MaskRle
            : ev.AiContext?.SamMaskRle;
        var maskImageWidth = hasValidOverlayMask
            ? overlayMask!.ImageWidth
            : ev.AiContext?.SamMaskImageWidth;
        var maskImageHeight = hasValidOverlayMask
            ? overlayMask!.ImageHeight
            : ev.AiContext?.SamMaskImageHeight;

        if (SamMaskFormatValidator.IsValid(
                maskRle,
                maskImageWidth,
                maskImageHeight,
                out _))
        {
            sample.SamMaskRle = maskRle;
            sample.SamMaskImageWidth = maskImageWidth;
            sample.SamMaskImageHeight = maskImageHeight;
            sample.SamMaskAreaPixels = hasValidOverlayMask
                ? overlayMask!.MaskAreaPixels
                : null;
            sample.SamMaskConfidence = hasValidOverlayMask
                ? overlayMask!.Confidence
                : ev.AiContext?.Evidence?.SamMaskStability;
            sample.SamMaskLabel = ev.Entry.Code;
        }

        // Mehrfachobjekt: sobald alle vier BBox-Felder vorhanden sind, gehoert die Box zur
        // Objekt-Identitaet (Signatur mit b:-Teil) — zwei Befunde mit gleichem Code/Meter,
        // aber verschiedenen Boxen sind verschiedene Objekte. Ohne Box bleibt das oben
        // gesetzte 4-Teiler-Format (Legacy-kompatibel).
        if (sample.HasBbox)
        {
            sample.Signature = TrainingSample.BuildCanonicalSignature(
                caseId,
                ev.Entry.Code,
                meterStart,
                meterEnd,
                sample.BboxXCenter,
                sample.BboxYCenter,
                sample.BboxWidth,
                sample.BboxHeight);
        }

        return sample;
    }

    private static string BuildKnowledgeDescription(
        string? code,
        string? description,
        bool isPersonalAcceptance)
    {
        var text = description?.Trim() ?? string.Empty;
        if (!isPersonalAcceptance || text.Length >= 10)
            return text;

        var normalizedCode = code?.Trim() ?? string.Empty;
        return !string.IsNullOrWhiteSpace(text)
            ? $"{normalizedCode} - {text}"
            : $"{normalizedCode} - persoenlich bestaetigt";
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

    /// <summary>
    /// Der KI-Kontext ist der Beleg dafuer, dass ein Vorschlag sichtbar war;
    /// fehlt er, hat der Mensch ohne Modellhilfe entschieden.
    /// </summary>
    private static TrainingSampleSuggestionProvenance BuildSuggestionProvenance(CodingEvent ev)
    {
        var ai = ev.AiContext;
        if (ai is null)
        {
            return new TrainingSampleSuggestionProvenance
            {
                Origin = TrainingSampleSuggestionOrigin.Independent
            };
        }

        return new TrainingSampleSuggestionProvenance
        {
            Origin = TrainingSampleSuggestionOrigin.SuggestionShown,
            ModelId = ai.SuggestedByModelId,
            ModelSha256 = ai.SuggestedByModelSha256,
            SuggestedCode = ai.SuggestedCode,
            SuggestedConfidence = ai.Confidence
        };
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
