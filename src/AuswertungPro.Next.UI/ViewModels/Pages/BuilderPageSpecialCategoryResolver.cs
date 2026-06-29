namespace AuswertungPro.Next.UI.ViewModels.Pages;

public enum SpecialStatsCategory
{
    None = 0,
    InlinerGfk = 1,
    InlinerNadelfilz = 2,
    Manschette = 3,
    Linerendmanschette = 4
}

public static class BuilderPageSpecialCategoryResolver
{
    public static bool TryResolve(string combinedText, out SpecialStatsCategory category)
    {
        category = SpecialStatsCategory.None;

        if (ContainsToken(combinedText, "LINERENDMANSCHETTE") ||
            ContainsToken(combinedText, "ENDMANSCHETTE") ||
            ContainsToken(combinedText, "LEM"))
        {
            category = SpecialStatsCategory.Linerendmanschette;
            return true;
        }

        if (ContainsToken(combinedText, "SCHLAUCHLINER_GFK") ||
            (ContainsToken(combinedText, "GFK") && ContainsToken(combinedText, "LINER")) ||
            (ContainsToken(combinedText, "GFK") && ContainsToken(combinedText, "SCHLAUCHLINER")))
        {
            category = SpecialStatsCategory.InlinerGfk;
            return true;
        }

        if (ContainsToken(combinedText, "SCHLAUCHLINER_NADELFILZ") ||
            ContainsToken(combinedText, "NADELFILZ_LINER") ||
            (ContainsToken(combinedText, "NADELFILZ") && ContainsToken(combinedText, "LINER")) ||
            (ContainsToken(combinedText, "NADELFILZ") && ContainsToken(combinedText, "SCHLAUCHLINER")))
        {
            category = SpecialStatsCategory.InlinerNadelfilz;
            return true;
        }

        if (ContainsToken(combinedText, "MANSCHETTE"))
        {
            category = SpecialStatsCategory.Manschette;
            return true;
        }

        return false;
    }

    public static string GetLabel(SpecialStatsCategory category)
        => category switch
        {
            SpecialStatsCategory.InlinerGfk => "Inliner GFK",
            SpecialStatsCategory.InlinerNadelfilz => "Inliner Nadelfilz",
            SpecialStatsCategory.Manschette => "Manschetten",
            SpecialStatsCategory.Linerendmanschette => "Linerendmanschetten (LEM)",
            _ => "Sonstiges"
        };

    public static int GetOrder(SpecialStatsCategory category)
        => category switch
        {
            SpecialStatsCategory.InlinerGfk => 0,
            SpecialStatsCategory.InlinerNadelfilz => 1,
            SpecialStatsCategory.Manschette => 2,
            SpecialStatsCategory.Linerendmanschette => 3,
            _ => 99
        };

    public static string NormalizeUnit(string? unit, SpecialStatsCategory category)
    {
        var normalized = (unit ?? "").Trim().ToLowerInvariant();
        if (normalized.Length > 0)
        {
            return normalized;
        }

        return category switch
        {
            SpecialStatsCategory.InlinerGfk => "m",
            SpecialStatsCategory.InlinerNadelfilz => "m",
            SpecialStatsCategory.Manschette => "stk",
            SpecialStatsCategory.Linerendmanschette => "stk",
            _ => "stk"
        };
    }

    private static bool ContainsToken(string text, string token)
        => !string.IsNullOrWhiteSpace(text) &&
           !string.IsNullOrWhiteSpace(token) &&
           text.Contains(token, StringComparison.OrdinalIgnoreCase);
}
