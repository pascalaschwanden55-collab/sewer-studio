namespace AuswertungPro.Next.Application.Ai;

/// <summary>
/// Fachregeln fuer Live-Codier-Deduplication, bewusst frei von UI-Abhaengigkeiten.
/// </summary>
public static class CodingDedupPolicy
{
    public static bool IsOneTimeCode(string? code)
    {
        var main = MainCode(code);
        return main is "BCD" or "BCE" or "BDC";
    }

    public static bool CodesMatch(string? existingCode, string? newCode)
    {
        if (string.IsNullOrWhiteSpace(existingCode) || string.IsNullOrWhiteSpace(newCode))
            return false;

        if (string.Equals(existingCode, newCode, StringComparison.OrdinalIgnoreCase))
            return true;

        var existingMain = MainCode(existingCode);
        var newMain = MainCode(newCode);
        return existingMain is not null
            && newMain is not null
            && string.Equals(existingMain, newMain, StringComparison.OrdinalIgnoreCase);
    }

    private static string? MainCode(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return null;

        var trimmed = code.Trim();
        return trimmed.Length >= 3 ? trimmed[..3].ToUpperInvariant() : trimmed.ToUpperInvariant();
    }
}
