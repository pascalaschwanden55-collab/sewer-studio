using AuswertungPro.Next.Application.Ai.QualityGate;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.Ai;

/// <summary>Uebersetzt zwischen speicherbarem Ereignis-Schnappschuss und QualityGate-Belegen.</summary>
public static class CodingEventEvidenceMapper
{
    public static CodingEventAiEvidence? ToSnapshot(EvidenceVector? evidence)
    {
        if (evidence is null)
            return null;

        return new CodingEventAiEvidence
        {
            YoloConf = evidence.YoloConf,
            DinoConf = evidence.DinoConf,
            SamMaskStability = evidence.SamMaskStability,
            QwenVisionConf = evidence.QwenVisionConf,
            LlmCodeConf = evidence.LlmCodeConf,
            KbSimilarity = evidence.KbSimilarity,
            KbCodeAgreement = evidence.KbCodeAgreement,
            PlausibilityScore = evidence.PlausibilityScore,
            DamageCategory = evidence.DamageCategory,
            FrameCount = evidence.FrameCount
        };
    }

    public static EvidenceVector? ToEvidence(CodingEventAiEvidence? snapshot)
    {
        if (snapshot is null)
            return null;

        return new EvidenceVector(
            YoloConf: snapshot.YoloConf,
            DinoConf: snapshot.DinoConf,
            SamMaskStability: snapshot.SamMaskStability,
            QwenVisionConf: snapshot.QwenVisionConf,
            LlmCodeConf: snapshot.LlmCodeConf,
            KbSimilarity: snapshot.KbSimilarity,
            KbCodeAgreement: snapshot.KbCodeAgreement,
            PlausibilityScore: snapshot.PlausibilityScore,
            DamageCategory: snapshot.DamageCategory,
            FrameCount: snapshot.FrameCount);
    }

    public static CodingEventAiEvidence? Clone(CodingEventAiEvidence? source)
        => ToSnapshot(ToEvidence(source));
}
