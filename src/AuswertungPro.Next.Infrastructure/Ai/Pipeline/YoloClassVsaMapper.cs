using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace AuswertungPro.Next.Infrastructure.Ai.Pipeline;

/// <summary>
/// Mappt YOLO-Detect-Klassennamen (yolo26m.names.json) auf VSA-Hauptcodes,
/// damit die klassenspezifischen Schwellen (YoloClassConfidence) greifen.
/// Hintergrund: Die produktiven Gewichte liefern englische Klassennamen
/// ("crack", "root", ...). Der fruehere Ansatz ClassName.Split('_')[0]
/// erwartete Namen wie "BAB_crack" und traf deshalb nie einen Schwellen-Key —
/// alle Klassen liefen auf der Default-Schwelle.
/// </summary>
public static class YoloClassVsaMapper
{
    // Zuordnung gemaess EN 13508-2 / VSA-KEK; konsistent zu
    // VsaCodeResolver.InferCodeFromLabel und EvalSetClassifierClassMapper.
    private static readonly IReadOnlyDictionary<string, string> ClassToVsaCode =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["crack"] = "BAB",          // Riss
            ["fracture"] = "BAC",       // Bruch
            ["deformation"] = "BAA",    // Verformung
            ["displacement"] = "BAJ",   // Verschobene Rohrverbindung
            ["intrusion"] = "BAI",      // Einragendes Dichtungsmaterial
            ["root"] = "BBA",           // Wurzeln
            ["roots"] = "BBA",          // Wurzeln (Plural-Variante aelterer Gewichte)
            ["deposit"] = "BBC",        // Ablagerung
            ["infiltration"] = "BBF",   // Infiltration (eindringendes Wasser)
            ["connection"] = "BCA",     // Seitlicher Anschluss
            // "structural_other" bewusst NICHT gemappt → Default-Schwelle
        };

    // Legacy-Namensschema "BAB_crack": fuehrender Token ist bereits ein VSA-Hauptcode
    private static readonly Regex VsaMainCodePattern =
        new(@"^B[A-Z]{2}$", RegexOptions.Compiled);

    /// <summary>
    /// Liefert den VSA-Hauptcode zu einem YOLO-Klassennamen oder null,
    /// wenn keine Zuordnung existiert (dann gilt die Default-Schwelle).
    /// </summary>
    public static string? ToVsaMainCode(string? className)
    {
        if (string.IsNullOrWhiteSpace(className))
            return null;

        var trimmed = className.Trim();

        if (ClassToVsaCode.TryGetValue(trimmed, out var mapped))
            return mapped;

        // Legacy: "BAB_crack" → "BAB"
        var firstToken = trimmed.Split('_')[0].ToUpperInvariant();
        if (VsaMainCodePattern.IsMatch(firstToken))
            return firstToken;

        return null;
    }
}
