using AuswertungPro.Next.Application.Ai.Training;

namespace DetectReleaseHoldoutPdfExtractor;

internal static class DetectClassMap
{
    private static readonly IReadOnlyDictionary<string, DetectClass> Classes =
        new Dictionary<string, DetectClass>(StringComparer.Ordinal)
        {
            ["BCA"] = new(0, "BCA", "BCA_anschluss"),
            ["BAB"] = new(1, "BAB", "BAB_riss"),
            ["BAC"] = new(2, "BAC", "BAC_bruch"),
            ["BAA"] = new(3, "BAA", "BAA_verformung"),
            ["BAF"] = new(4, "BAF", "BAF_oberflaeche"),
            ["BAH"] = new(5, "BAH", "BAH_schadanschluss"),
            ["BAI"] = new(6, "BAI", "BAI_dichtung"),
            ["BAJ"] = new(7, "BAJ", "BAJ_verbindung"),
            ["BBA"] = new(8, "BBA", "BBA_wurzeln"),
            ["BBB"] = new(9, "BBB", "BBB_anhaftung"),
            ["BBC"] = new(10, "BBC", "BBC_ablagerung"),
            ["BBD"] = new(11, "BBD", "BBD_boden"),
            ["BBF"] = new(12, "BBF", "BBF_infiltration"),
            ["SONST"] = new(13, "SONST", "SONST_schaden"),
            ["BCC"] = new(14, "BCC", "BCC_bogen"),
        };

    public static bool TryResolve(string? vsaCode, out DetectClass detectClass)
    {
        detectClass = default!;
        if (string.IsNullOrWhiteSpace(vsaCode))
            return false;
        var rawCode = vsaCode.Trim().ToUpperInvariant();
        if (string.Equals(rawCode, "SONST", StringComparison.Ordinal))
            return Classes.TryGetValue("SONST", out detectClass!);
        if (rawCode.StartsWith("SONST", StringComparison.Ordinal))
            return false;
        var code = new string(rawCode.TakeWhile(char.IsLetterOrDigit).ToArray());
        return code.Length >= 3 && Classes.TryGetValue(code[..3], out detectClass!);
    }
}

internal static class InputValidation
{
    public static string RequireSha256(string? value, string field)
        => TryNormalizeSha256(value)
           ?? throw new InvalidDataException($"{field} muss ein gültiger SHA-256 sein.");

    public static string? TryNormalizeSha256(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        var normalized = value.Trim().ToLowerInvariant();
        return normalized.Length == 64 && normalized.All(Uri.IsHexDigit)
            ? normalized
            : null;
    }

    public static string RequireHolding(string? value, string field)
        => TryNormalizeHolding(value)
           ?? throw new InvalidDataException($"{field} muss ein numerisches Schachtpaar sein.");

    public static string? TryNormalizeHolding(string? value)
    {
        var normalized = EvalContaminationGuard.NormalizeHaltungKey(value);
        if (string.IsNullOrWhiteSpace(normalized))
            return null;
        var parts = normalized.Split('-', StringSplitOptions.None);
        return parts.Length == 2
               && parts.All(part => part.Length > 0 && part.All(char.IsAsciiDigit))
            ? normalized
            : null;
    }
}

internal static class HoldingKeys
{
    public static string Physical(string holdingKey)
    {
        var parts = holdingKey.Split('-', StringSplitOptions.None);
        if (parts.Length != 2)
            throw new InvalidDataException("Die Haltung besitzt kein gültiges Schachtpaar.");
        return string.CompareOrdinal(parts[0], parts[1]) <= 0
            ? $"{parts[0]}|{parts[1]}"
            : $"{parts[1]}|{parts[0]}";
    }
}

internal static class ErrorCodes
{
    public static string For(Exception exception)
        => exception switch
        {
            FileNotFoundException => "source_not_found",
            DirectoryNotFoundException => "source_not_found",
            UnauthorizedAccessException => "source_access_denied",
            InvalidDataException => "source_validation_failed",
            IOException => "source_io_error",
            _ => "import_failed",
        };
}
