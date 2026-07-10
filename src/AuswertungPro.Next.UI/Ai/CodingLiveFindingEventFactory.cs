using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.QualityGate;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.Infrastructure.Ai;

namespace AuswertungPro.Next.UI.Ai;

public sealed record CodingLiveFindingEventDraft(
    ProtocolEntry Entry,
    CodingEventAiContext AiContext,
    OverlayGeometry? Overlay);

public static class CodingLiveFindingEventFactory
{
    public static CodingLiveFindingEventDraft Create(
        string code,
        string? officialLabel,
        LiveFrameFinding finding,
        double meter,
        TimeSpan videoTime,
        QualityGateResult gateResult)
    {
        var entry = new ProtocolEntry
        {
            Source = ProtocolEntrySource.Ai,
            Code = code,
            Beschreibung = officialLabel ?? finding.Label,
            MeterStart = meter,
            IsStreckenschaden = VsaCodeResolver.IsStreckenschadenCode(code),
            Zeit = videoTime
        };

        CodingLiveFindingCodeMetaWriter.ApplyToEntry(entry, code, finding);

        var aiContext = new CodingEventAiContext
        {
            SuggestedCode = code,
            Confidence = gateResult.CompositeConfidence,
            Reason = finding.Label,
            // Audit Fix 3: Ampel schon beim Anlegen sichern, damit die Live-Anzeige
            // die zentrale Freigabe-Regel (zweiter Beleg) anwenden kann.
            QualityGateLevel = gateResult.TrafficLight.ToString(),
            Evidence = CodingEventEvidenceMapper.ToSnapshot(
                CodingLiveFindingQualityGatePolicy.BuildEvidence(finding) with
                {
                    DamageCategory = code
                }),
            Decision = CodingUserDecision.Ignored
        };

        return new CodingLiveFindingEventDraft(
            entry,
            aiContext,
            CodingLiveFindingOverlayBuilder.BuildRectangle(finding));
    }
}
