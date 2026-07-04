using System;

namespace AuswertungPro.Next.Application.Costs;

public static class UnitKinds
{
    public static bool IsLength(string? unit)
        => IsAny(unit, "m", "meter", "metre", "lfm");

    public static bool IsHour(string? unit)
        => IsAny(unit, "h", "std", "stunde", "stunden");

    public static bool IsPiece(string? unit)
        => IsAny(unit, "stk", "stck", "stueck", "pcs");

    private static bool IsAny(string? unit, params string[] values)
    {
        var normalized = Normalize(unit);
        if (normalized.Length == 0)
            return false;

        foreach (var value in values)
        {
            if (normalized.Equals(Normalize(value), StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static string Normalize(string? unit)
        => (unit ?? "")
            .Trim()
            .TrimEnd('.')
            .Replace("\u00fc", "ue", StringComparison.OrdinalIgnoreCase);
}
