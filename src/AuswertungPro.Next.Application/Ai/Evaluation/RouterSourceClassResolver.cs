namespace AuswertungPro.Next.Application.Ai.Evaluation;

/// <summary>
/// Reine Hilfsklasse zur Bestimmung der Router-Klasse aus Quelldatei-Metadaten.
/// Enthaelt keine IO-Abhaengigkeiten.
/// </summary>
public static class RouterSourceClassResolver
{
    private static readonly HashSet<string> KnownRouterClasses = new(StringComparer.OrdinalIgnoreCase)
    {
        "leer",
        "beginn_ende",
        "wasserstand",
        "anschluss",
        "oberflaeche",
        "riss_bruch",
        "versatz",
        "ablagerung",
        "wurzeln",
        "deformation",
        "dichtung",
        "infiltration",
        "sonstiges",
    };

    private static readonly Dictionary<string, string> KnownSourceClasses = new(StringComparer.OrdinalIgnoreCase)
    {
        ["empty"] = "leer",
        ["negative"] = "leer",
        ["no_damage"] = "leer",
        ["no_schaden"] = "leer",
        ["kein_schaden"] = "leer",
        ["meta"] = "beginn_ende",
        ["start_ende"] = "beginn_ende",
        ["rohranfang_ende"] = "beginn_ende",
        ["oberflaeche"] = "oberflaeche",
        ["versatz"] = "versatz",
        ["riss_bruch"] = "riss_bruch",
        ["rissbruch"] = "riss_bruch",
        ["ablagerung"] = "ablagerung",
        ["anschluss"] = "anschluss",
        ["infiltration"] = "infiltration",
        ["deformation"] = "deformation",
        ["dichtung"] = "dichtung",
        ["wurzeln"] = "wurzeln",
    };

    /// <summary>
    /// Normiert einen Klassennamen fuer den Router-Datensatz:
    /// Leerzeichen und Bindestriche werden zu Unterstrichen, Ergebnis kleingeschrieben.
    /// </summary>
    public static string NormalizeClassName(string value)
        => value.Trim().Replace('-', '_').Replace(' ', '_').ToLowerInvariant();

    /// <summary>
    /// Versucht, aus dem Dateinamen einen VSA-Code oder LEER-Marker zu extrahieren.
    /// Gibt null zurueck, wenn kein erkennbarer Code gefunden wird.
    /// </summary>
    public static string? ExtractClassFromFileName(string path)
    {
        var stem = Path.GetFileNameWithoutExtension(path);
        if (string.IsNullOrWhiteSpace(stem))
            return null;

        var parts = stem.Split('_', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var part in parts)
        {
            var code = EvalSetBenchmarkDataset.NormalizeCode(part);
            if (!string.IsNullOrWhiteSpace(code) &&
                (string.Equals(code, "LEER", StringComparison.OrdinalIgnoreCase) ||
                 (code.Length >= 3 && code.Length <= 6 && code.StartsWith("B", StringComparison.OrdinalIgnoreCase))))
            {
                return code;
            }
        }

        if (stem.Contains("kein_schaden", StringComparison.OrdinalIgnoreCase) ||
            stem.Contains("no_damage", StringComparison.OrdinalIgnoreCase) ||
            stem.Contains("leer", StringComparison.OrdinalIgnoreCase))
        {
            return "LEER";
        }

        return null;
    }

    /// <summary>
    /// Mappt eine Quellklasse auf die entsprechende Router-Klasse.
    /// Gibt null zurueck, wenn keine Zuordnung moeglich ist oder die Klasse "sonstiges" waere.
    /// </summary>
    public static string? MapSourceClassToRouterClass(string sourceClass)
    {
        if (string.IsNullOrWhiteSpace(sourceClass))
            return null;

        var normalized = NormalizeClassName(sourceClass);
        if (KnownRouterClasses.Contains(normalized))
            return normalized;
        if (KnownSourceClasses.TryGetValue(normalized, out var mapped))
            return mapped;

        var code = EvalSetBenchmarkDataset.NormalizeCode(sourceClass);
        if (string.IsNullOrWhiteSpace(code))
            return null;

        var routerClass = EvalSetRouterPlanner.MapExpectedCodeToRouterClass(code);
        return string.Equals(routerClass, "sonstiges", StringComparison.OrdinalIgnoreCase)
            ? null
            : routerClass;
    }
}
