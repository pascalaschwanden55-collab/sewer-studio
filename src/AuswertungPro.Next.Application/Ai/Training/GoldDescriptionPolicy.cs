namespace AuswertungPro.Next.Application.Ai.Training;

/// <summary>
/// Zentrale Textregel fuer persoenliche Goldsamples.
/// Die Beschreibung ist fuer den YOLO-Geometrieexport nicht erforderlich,
/// muss fuer KB-/Qwen-Kontext aber fertig und aussagekraeftig sein.
/// </summary>
public static class GoldDescriptionPolicy
{
    public const int MinimumKnowledgeTextLength = 10;

    /// <summary>
    /// Erkennt die noch nicht ausgefuellte automatische Beschreibungsvorlage.
    /// </summary>
    public static bool IsPlaceholder(string? description)
        => !string.IsNullOrWhiteSpace(description)
           && (description.Contains("ausmass ergaenzen", StringComparison.OrdinalIgnoreCase)
               || description.Contains("ausmass ergänzen", StringComparison.OrdinalIgnoreCase)
               || description.Contains("ausmaß ergaenzen", StringComparison.OrdinalIgnoreCase)
               || description.Contains("ausmaß ergänzen", StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// True, wenn der Text als Retrieval-/Qwen-Beispiel verwendet werden darf.
    /// </summary>
    public static bool IsKnowledgeTextReady(string? description)
        => !string.IsNullOrWhiteSpace(description)
           && description.Trim().Length >= MinimumKnowledgeTextLength
           && !IsPlaceholder(description);
}
