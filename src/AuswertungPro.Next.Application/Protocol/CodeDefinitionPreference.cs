namespace AuswertungPro.Next.Application.Protocol;

/// <summary>
/// Gemeinsamer Helfer fuer die Bevorzugungslogik beim Deduplizieren von Code-Definitionen.
/// Beide Provider (Json und Xml) teilen denselben <see cref="Choose"/>-Rumpf,
/// unterscheiden sich jedoch absichtlich im Score: Json zaehlt Examples, Xml nicht.
/// Diese Divergenz bleibt durch den <paramref name="scoreFunc"/>-Parameter erhalten.
/// </summary>
internal static class CodeDefinitionPreference
{
    /// <summary>
    /// Waehlt die reichhaltigere der beiden Definitionen basierend auf einer uebergebenen Score-Funktion
    /// und – bei Gleichstand – der laengeren Beschreibung; bei weiterem Gleichstand gewinnt <paramref name="first"/>.
    /// </summary>
    internal static CodeDefinition Choose(
        CodeDefinition first,
        CodeDefinition second,
        Func<CodeDefinition, int> scoreFunc)
    {
        var scoreFirst = scoreFunc(first);
        var scoreSecond = scoreFunc(second);
        if (scoreSecond > scoreFirst)
            return second;
        if (scoreFirst > scoreSecond)
            return first;

        var descFirst = first.Description?.Length ?? 0;
        var descSecond = second.Description?.Length ?? 0;
        if (descSecond > descFirst)
            return second;
        if (descFirst > descSecond)
            return first;

        return first;
    }

    /// <summary>
    /// Score-Funktion fuer den Json-Provider: zaehlt zusaetzlich Examples (max +2).
    /// </summary>
    internal static int ScoreWithExamples(CodeDefinition def) => BaseScore(def)
        + Math.Min(def.Examples?.Count ?? 0, 2);

    /// <summary>
    /// Score-Funktion fuer den Xml-Provider: zaehlt Examples NICHT
    /// (bewusste Divergenz, da Xml-Quellen keine Examples liefern).
    /// </summary>
    internal static int ScoreWithoutExamples(CodeDefinition def) => BaseScore(def);

    // Gemeinsamer Basis-Score (identisch in beiden Providern)
    private static int BaseScore(CodeDefinition def)
    {
        var score = 0;
        if (!string.IsNullOrWhiteSpace(def.Title) && !string.Equals(def.Title, def.Code, StringComparison.OrdinalIgnoreCase))
            score += 3;
        if (!string.IsNullOrWhiteSpace(def.Description))
            score += 2;
        if (!string.IsNullOrWhiteSpace(def.Group) && !string.Equals(def.Group, "Unbekannt", StringComparison.OrdinalIgnoreCase))
            score += 1;
        score += Math.Min(def.CategoryPath?.Count ?? 0, 3);
        score += Math.Min(def.Parameters?.Count ?? 0, 3);
        if (def.RequiresRange)
            score += 1;
        if (def.RangeThresholdM is not null)
            score += 1;
        if (!string.IsNullOrWhiteSpace(def.RangeThresholdText))
            score += 1;
        return score;
    }
}
