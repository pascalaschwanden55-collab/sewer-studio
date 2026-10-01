using System;
using System.Collections.Generic;

namespace AuswertungPro.Next.Application.UseCases.BendSuggestions;

/// <summary>
/// Fachliche Grenze fuer den protokollbasierten Bogen-Vergleich.
/// Das Modell erkennt nur die gemeinsame Klasse BCC_bogen. Fuer die Messung
/// werden deshalb alle acht gueltigen BCC-Untercodes gemeinsam betrachtet.
/// </summary>
public static class BendProtocolPositionPolicy
{
    private static readonly HashSet<string> SupportedCodeSet = new(
        new[]
        {
            "BCCAA",
            "BCCAB",
            "BCCAY",
            "BCCBA",
            "BCCBB",
            "BCCBY",
            "BCCYA",
            "BCCYB"
        },
        StringComparer.Ordinal);

    private static readonly IReadOnlyList<string> SupportedCodeList =
        Array.AsReadOnly(
        new[]
        {
            "BCCAA",
            "BCCAB",
            "BCCAY",
            "BCCBA",
            "BCCBB",
            "BCCBY",
            "BCCYA",
            "BCCYB"
        });

    /// <summary>Die acht gemeinsam gemessenen VSA-Untercodes.</summary>
    public static IReadOnlyList<string> SupportedCodes => SupportedCodeList;

    /// <summary>
    /// True nur fuer einen der acht vollstaendigen BCC-Untercodes.
    /// Der nackte Hauptcode und unbekannte Varianten werden nicht geraten.
    /// </summary>
    public static bool IsSupportedCode(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return false;

        return SupportedCodeSet.Contains(code.Trim().ToUpperInvariant());
    }

    /// <summary>
    /// Der bekannte Rohcode BCC.YB ist kein gueltiger Punkttrenner-Code.
    /// Die betroffene Haltung wird fuer die Messung vollstaendig ausgeschlossen,
    /// statt den Eintrag still zu BCCYB umzudeuten.
    /// </summary>
    public static bool ExcludesHolding(string? rawCode)
        => string.Equals(rawCode?.Trim(), "BCC.YB", StringComparison.OrdinalIgnoreCase);
}
