namespace AuswertungPro.Next.UI.Player;

public static class PlayerImportFallbackCodePolicy
{
    private static readonly string[] AllowedPrefixes =
    [
        "BCD",
        "BCE",
        "BCA",
        "BCC",
        "BBC",
        "BDDC",
        "BAA",
        "BAB",
        "BAC",
        "BAF",
        "BAH",
        "BAI",
        "BAJ",
        "BBA",
        "BBB",
        "BBD"
    ];

    public static bool IsAllowed(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return false;
        }

        var normalized = code.Trim();
        return AllowedPrefixes.Any(prefix =>
            normalized.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }
}
