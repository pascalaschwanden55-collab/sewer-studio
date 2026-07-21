namespace AuswertungPro.Next.Application.Ai.Evaluation;

internal static class EvalSetClassifierClassMapper
{
    private static readonly IReadOnlyDictionary<string, string> ClassToVsaCode =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["oberflaeche"] = "BAF",
            ["versatz"] = "BAJ",
            ["riss_bruch"] = "BAB",
            ["rissbruch"] = "BAB",
            ["bruch"] = "BAC",
            ["ablagerung"] = "BBC",
            ["anschluss"] = "BCA",
            ["infiltration"] = "BBF",
            ["deformation"] = "BAA",
            ["dichtung"] = "BAI",
        };

    private static readonly HashSet<string> NegativeClasses = new(StringComparer.OrdinalIgnoreCase)
    {
        "leer",
        "empty",
        "negative",
        "no_damage",
        "no_schaden",
        "meta",
        "start",
        "ende",
    };

    public static bool IsNegativeClass(string? className)
        => !string.IsNullOrWhiteSpace(className) &&
           NegativeClasses.Contains(NormalizeClassKey(className));

    public static string? TryMapToVsaCode(string? className)
    {
        if (string.IsNullOrWhiteSpace(className))
            return null;

        var raw = className.Trim();
        var directCode = NormalizeDirectVsaCode(raw);
        if (directCode is not null)
            return directCode;

        var key = NormalizeClassKey(raw);
        if (NegativeClasses.Contains(key))
            return null;

        return ClassToVsaCode.TryGetValue(key, out var code)
            ? code
            : null;
    }

    public static string? TryMapToCoverageCode(string? className)
        => IsNegativeClass(className)
            ? "LEER"
            : TryMapToVsaCode(className);

    private static string NormalizeClassKey(string className)
        => className
            .Replace('-', '_')
            .Replace(' ', '_')
            .Trim()
            .ToLowerInvariant();

    private static string? NormalizeDirectVsaCode(string value)
    {
        var compact = new string(value
            .Where(char.IsLetterOrDigit)
            .Select(char.ToUpperInvariant)
            .ToArray());

        if (compact.Length < 3 || compact.Length > 6 || compact[0] != 'B')
            return null;

        return compact.All(char.IsLetterOrDigit)
            ? compact
            : null;
    }
}
