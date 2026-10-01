using System.Globalization;

namespace AuswertungPro.Next.Infrastructure.Dossiers;

internal static class DossierConditionClassValue
{
    internal static int? Normalize(string? value)
    {
        var text = (value ?? string.Empty).Trim();
        if (text.StartsWith('Z') || text.StartsWith('z'))
            text = text[1..].Trim();

        return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            && parsed is >= 0 and <= 4
                ? parsed
                : null;
    }
}
