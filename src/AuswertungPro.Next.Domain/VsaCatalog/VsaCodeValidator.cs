using System.Text.RegularExpressions;

namespace AuswertungPro.Next.Domain.VsaCatalog;

/// <summary>
/// Strenger Eintrittsfilter fuer Trainingslabels aus freiem PDF-Text.
/// UI-/KI-Resolver duerfen fallback-toleranter sein; dieser Validator nicht.
/// </summary>
public static partial class VsaCodeValidator
{
    public static bool IsKnownCode(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return false;

        var normalized = code.Trim().Replace(".", "").ToUpperInvariant();
        if (!CodePattern().IsMatch(normalized))
            return false;

        var groupKey = normalized[..2];
        if (!VsaCodeTree.Groups.TryGetValue(groupKey, out var group))
            return false;

        var mainKey = normalized[..3];
        return group.Codes.ContainsKey(mainKey);
    }

    [GeneratedRegex("^[A-Z]{3,8}$")]
    private static partial Regex CodePattern();
}
