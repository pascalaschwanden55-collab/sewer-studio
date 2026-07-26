using AuswertungPro.Next.Application.Ai.Training.ClassMaps;

namespace AuswertungPro.Next.Application.Ai.Training;

/// <summary>
/// Verbindliche Hauptcodes, fuer die persoenlich bestaetigte Goldframes aufgebaut werden.
/// Die Liste folgt den produktiven Klassifikations- und Erkennungsklassen.
/// </summary>
public static class PersonalGoldMainCodeCatalog
{
    private static readonly IReadOnlyDictionary<string, string> CanonicalLabels =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["AED"] = "Rohrmaterialwechsel",
            ["BAA"] = "Verformung",
            ["BAB"] = "Riss",
            ["BAC"] = "Leitungsbruch - Einsturz",
            ["BAF"] = "Oberflächenschaden",
            ["BAH"] = "Schadhafter Anschluss",
            ["BAI"] = "Einragendes Dichtungsmaterial",
            ["BAJ"] = "Verschobene Rohrverbindung",
            ["BBA"] = "Wurzeln",
            ["BBB"] = "Anhaftende Stoffe",
            ["BBC"] = "Ablagerung",
            // BBD ist im VSA-Katalog nur ein Präfix-Anker. Der allgemeine
            // Zwei-Buchstaben-Fallback "BB" wäre hier fachlich falsch.
            ["BBD"] = "Eindringender Boden",
            ["BBF"] = "Infiltration",
            ["BCA"] = "Seitlicher Anschluss",
            ["BCC"] = "Bogen",
            ["BCD"] = "Rohranfang",
            ["BCE"] = "Rohrende",
            ["BDA"] = "Allgemeinzustand, Fotobeispiel",
            ["BDD"] = "Wasserspiegel"
        };

    public static IReadOnlyList<string> RequiredCodes { get; } = ClassifierDatasetPlan.TargetClasses
        .Where(code => !code.Equals("LEER", StringComparison.OrdinalIgnoreCase))
        .Concat(YoloDetectClassMapV3.Classes.Keys
            .Where(name => !name.StartsWith("SONST_", StringComparison.OrdinalIgnoreCase))
            .Select(name => name[..3]))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .OrderBy(code => code, StringComparer.OrdinalIgnoreCase)
        .ToArray();

    /// <summary>
    /// Liefert den Klartext des Hauptcodes. Die trainierbaren Hauptcodes besitzen
    /// einen stabilen Klartext, damit Album- und Goldordner immer gleich benannt
    /// werden. Andere Codes werden weiterhin aus dem aktiven VSA-Katalog aufgelöst.
    /// </summary>
    public static string? ResolveLabel(
        string? mainCode,
        Func<string, string?> catalogLabelLookup)
    {
        ArgumentNullException.ThrowIfNull(catalogLabelLookup);
        var normalized = NormalizeMainCode(mainCode);
        if (normalized is null)
            return null;

        if (CanonicalLabels.TryGetValue(normalized, out var canonicalLabel))
            return canonicalLabel;

        var label = catalogLabelLookup(normalized)?.Trim();
        return string.IsNullOrWhiteSpace(label)
               || string.Equals(label, normalized, StringComparison.OrdinalIgnoreCase)
            ? null
            : label;
    }

    /// <summary>Formatiert einen Hauptcode gut lesbar als "BAB — Riss".</summary>
    public static string FormatDisplayName(
        string? mainCode,
        Func<string, string?> catalogLabelLookup)
    {
        var normalized = NormalizeMainCode(mainCode);
        if (normalized is null)
            return mainCode?.Trim() ?? string.Empty;

        var label = ResolveLabel(normalized, catalogLabelLookup);
        return string.IsNullOrWhiteSpace(label)
            ? normalized
            : $"{normalized} — {label}";
    }

    /// <summary>
    /// Liefert den lesbaren und dateisicheren Ordnernamen, zum Beispiel
    /// "BAB - Riss". Der endgueltig codierte Hauptcode bestimmt den Ordner.
    /// </summary>
    public static string FormatFolderName(
        string? code,
        Func<string, string?> catalogLabelLookup)
    {
        ArgumentNullException.ThrowIfNull(catalogLabelLookup);
        var mainCode = NormalizeMainCode(code)
                       ?? throw new ArgumentException(
                           "VSA-Code besitzt keinen Hauptcode.",
                           nameof(code));
        var label = ResolveLabel(mainCode, catalogLabelLookup);
        if (string.IsNullOrWhiteSpace(label))
            return mainCode;

        var safeLabel = label;
        foreach (var invalidCharacter in Path.GetInvalidFileNameChars())
            safeLabel = safeLabel.Replace(invalidCharacter, '-');
        safeLabel = safeLabel.Trim(' ', '.', '-');
        return string.IsNullOrWhiteSpace(safeLabel)
            ? mainCode
            : $"{mainCode} - {safeLabel}";
    }

    public static string? NormalizeMainCode(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return null;

        var normalized = code.Trim().Replace(".", string.Empty).ToUpperInvariant();
        return normalized.Length >= 3 ? normalized[..3] : normalized;
    }
}
