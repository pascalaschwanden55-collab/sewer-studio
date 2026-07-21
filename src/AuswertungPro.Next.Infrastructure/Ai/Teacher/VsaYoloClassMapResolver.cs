namespace AuswertungPro.Next.Infrastructure.Ai.Teacher;

internal static class VsaYoloClassMapResolver
{
    public static bool TryResolveClassId(
        VsaYoloClassMapDocument document,
        string value,
        IReadOnlyDictionary<string, int> legacyDefaults,
        out int id)
    {
        var exactKey = value.Trim();
        if (document.Classes.TryGetValue(exactKey, out id))
            return true;

        var category = ExtractCategory(exactKey, legacyDefaults);
        if (document.Classes.TryGetValue(category, out id))
            return true;

        if (document.Format == VsaYoloClassMapFormat.Versioned)
        {
            var semanticMatches = document.Classes
                .Where(item => item.Key.StartsWith(category + "_", StringComparison.OrdinalIgnoreCase))
                .ToArray();
            if (semanticMatches.Length == 1)
            {
                id = semanticMatches[0].Value;
                return true;
            }

            if (semanticMatches.Length > 1)
            {
                throw new InvalidDataException(
                    $"VSA-Praefix '{category}' passt zu mehreren versionierten YOLO-Klassen.");
            }
        }

        id = default;
        return false;
    }

    public static string GetKeyForAddition(
        VsaYoloClassMapDocument document,
        string value,
        IReadOnlyDictionary<string, int> legacyDefaults)
    {
        var trimmed = value.Trim();
        return document.Format == VsaYoloClassMapFormat.Versioned
               && trimmed.Contains('_', StringComparison.Ordinal)
            ? trimmed
            : ExtractCategory(trimmed, legacyDefaults);
    }

    private static string ExtractCategory(
        string vsaCode,
        IReadOnlyDictionary<string, int> legacyDefaults)
    {
        var clean = vsaCode.Replace(".", string.Empty).Trim().ToUpperInvariant();
        if (clean.Length < 2)
            return clean;

        if (clean.StartsWith("BCD", StringComparison.Ordinal)) return "BCD";
        if (clean.StartsWith("BCE", StringComparison.Ordinal)) return "BCE";
        if (clean.StartsWith("BCA", StringComparison.Ordinal)) return "BCA";
        if (clean.StartsWith("BCC", StringComparison.Ordinal)) return "BCC";

        if (clean.Length >= 3)
        {
            var prefix = clean[..3];
            if (legacyDefaults.ContainsKey(prefix))
                return prefix;
        }

        return clean.Length >= 3 ? clean[..3] : clean;
    }
}
