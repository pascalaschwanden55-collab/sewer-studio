using AuswertungPro.Next.Application.Ai;

namespace AuswertungPro.Next.Infrastructure.Ai.Pipeline;

/// <summary>Ein Bild, das der YOLO-cls-Vorfilter vor YOLO/DINO/SAM/Qwen aussortiert.</summary>
internal sealed record ClsPrefilterSkip(
    string TracePath,
    string DropReason,
    string ProgressText,
    YoloClassifyPrediction? EmptyPrediction = null);

/// <summary>
/// Entscheidet anhand der YOLO-cls-Antwort, ob ein Bild uebersprungen wird. Reihenfolge:
/// unbrauchbares Bild (Quality-Gate des Sidecars), dann LEER (nur im Klassifikatorregime,
/// das 11-Klassen-Modell kennt kein NORMAL), dann OTHER/NORMAL. Nur eine Aussage ueber
/// der Schwelle ueberspringt; alles andere geht weiter zur Detektion. Gilt bewusst auch
/// fuer Sweep-/BCD-/BCE-Bilder: Grundgeruest-Elemente haetten eine eigene cls-Klasse.
/// </summary>
internal static class ClsPrefilterRule
{
    public const double SkipConfidence = 0.70;

    public static ClsPrefilterSkip? Decide(YoloClassifyResponse cls, bool classifierDecisionEnabled)
    {
        if (!cls.Usable)
            return new ClsPrefilterSkip(
                "cls_quality_skip", $"frame_{cls.QualityReason}", $"unbrauchbar ({cls.QualityReason})");

        var top = cls.Predictions.Count > 0 ? cls.Predictions[0] : null;
        if (top is null || top.Confidence <= SkipConfidence)
            return null;

        if (classifierDecisionEnabled && top.ClassName is "LEER" or "leer")
            return new ClsPrefilterSkip(
                "cls_leer_skip", "classifier_leer", $"Klassifikator: LEER ({top.Confidence:P0})", top);

        if (top.ClassName is "OTHER" or "other" or "NORMAL" or "normal")
            return new ClsPrefilterSkip(
                "yolo_cls_skip", "yolo_cls_normal", $"cls: {top.ClassName} ({top.Confidence:P0})");

        return null;
    }
}
