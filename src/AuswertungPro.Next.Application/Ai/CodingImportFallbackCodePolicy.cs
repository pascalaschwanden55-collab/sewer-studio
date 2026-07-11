namespace AuswertungPro.Next.Application.Ai;

public static class CodingImportFallbackCodePolicy
{
    public const double DefaultMeterWindowMeters = 2.0;
    public const double BendMeterWindowMeters = 0.25;

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

    public static bool IsWithinMeterWindow(string? code, double distanceMeters)
    {
        if (!IsAllowed(code) || double.IsNaN(distanceMeters) || distanceMeters < 0)
        {
            return false;
        }

        var maxDistance = code!.Trim().StartsWith("BCC", StringComparison.OrdinalIgnoreCase)
            ? BendMeterWindowMeters
            : DefaultMeterWindowMeters;

        return distanceMeters <= maxDistance;
    }
}
