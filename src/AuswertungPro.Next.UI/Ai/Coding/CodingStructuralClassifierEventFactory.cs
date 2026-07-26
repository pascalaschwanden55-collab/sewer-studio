using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.UI.Ai.Coding;

public sealed record CodingStructuralClassifierEventDraft(
    ProtocolEntry Entry,
    CodingEventAiContext AiContext);

public static class CodingStructuralClassifierEventFactory
{
    public static CodingStructuralClassifierEventDraft Create(
        string code,
        string description,
        string classifierLabel,
        double? classifierConfidence,
        double meter,
        TimeSpan videoTime,
        bool meterFromOsd)
    {
        var entry = new ProtocolEntry
        {
            Source = ProtocolEntrySource.Ai,
            Code = code,
            Beschreibung = description,
            MeterStart = meter,
            Zeit = videoTime
        };

        if (!meterFromOsd)
        {
            entry.CodeMeta ??= new ProtocolEntryCodeMeta { Code = code };
            entry.CodeMeta.Parameters["vsa.meter.quelle"] = "geschaetzt";
        }

        var aiContext = new CodingEventAiContext
        {
            SuggestedCode = code,
            Confidence = classifierConfidence ?? 0.0,
            Reason = $"{classifierLabel} (Klassifikator, ohne DINO/SAM-Box)",
            Decision = CodingUserDecision.Ignored
        };

        return new CodingStructuralClassifierEventDraft(entry, aiContext);
    }
}
