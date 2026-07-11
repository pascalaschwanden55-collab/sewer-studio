using System;
using System.Collections.Generic;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Infrastructure.Ai;

namespace AuswertungPro.Next.Infrastructure.Ai.Pipeline;

/// <summary>
/// Erzeugt aus reinen YOLO-cls-Predictions einen Grundgeruest-Code (BCA/BCC/BCD/BCE),
/// wenn DINO keine Box liefert. Reine Business-Logik (kein I/O) -> unit-testbar.
/// Nutzt VsaCodeResolver.ResolveFromClassifier (erbt damit Bogen-Veto + Ortsgebunden-Gate)
/// und akzeptiert nur Grundgeruest-Codes oberhalb der Mindestkonfidenz.
/// </summary>
public static class ClassifierOnlyStructuralPolicy
{
    // Bestandsaufnahme/Grundgeruest: Anschluss, Bogen, Rohranfang, Rohrende.
    // Bewusst KEINE Schadenscodes (ohne SAM-Maske fehlt Geometrie/Quantifizierung).
    private static readonly HashSet<string> Grundgeruest =
        new(StringComparer.OrdinalIgnoreCase) { "BCA", "BCC", "BCD", "BCE" };

    public static VsaCodeResolver.ResolvedCode? TryResolve(
        IReadOnlyList<YoloClassifyPrediction>? predictions,
        double meter,
        double reachLength,
        bool isBend,
        double minConfidence)
    {
        if (predictions is null || predictions.Count == 0)
            return null;

        var resolved = VsaCodeResolver.ResolveFromClassifier(
            predictions, meter, reachLength, importContext: null, isBend: isBend);

        if (resolved is null)
            return null;

        if (!Grundgeruest.Contains(resolved.Code))
            return null;

        if (resolved.Confidence < minConfidence)
            return null;

        return resolved;
    }
}
