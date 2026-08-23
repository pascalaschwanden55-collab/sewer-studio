using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace AuswertungPro.Next.Application.Dossiers.Lookup;

/// <summary>
/// Liest die Parzellennummer aus einem Haltungsnamen.
///
/// Im Bestand heissen die Knoten privater Hausanschluesse nach ihrer Parzelle:
/// "439.01-36051" laeuft auf Parzelle 439. Der Kanton fuehrt diese Leitungen in
/// seiner oeffentlichen Netzebene groesstenteils NICHT — diese Regel ist deshalb
/// der einzige Weg, sie einer Parzelle zuzuordnen, und kostet keine Abfrage.
///
/// Die Regel liefert nur einen KANDIDATEN. Ob es die Parzelle wirklich gibt,
/// muss der Parzellendienst bestaetigen.
/// </summary>
public static class ParcelNumberFromHoldingName
{
    // Mindestens zwei Ziffern vor dem Punkt: "7.34854" ist ein Schachtname aus
    // dem Bestand, keine Parzelle.
    private static readonly Regex KnotenMitParzelle = new(
        @"^(\d{2,5})\.\d{1,3}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static IReadOnlyList<string> Extract(string? holdingName)
    {
        if (string.IsNullOrWhiteSpace(holdingName))
            return Array.Empty<string>();

        var treffer = new List<string>();

        foreach (var teil in holdingName.Split('-', StringSplitOptions.RemoveEmptyEntries))
        {
            var match = KnotenMitParzelle.Match(teil.Trim());
            if (!match.Success)
                continue;

            var nummer = match.Groups[1].Value;
            if (!treffer.Contains(nummer, StringComparer.Ordinal))
                treffer.Add(nummer);
        }

        return treffer;
    }

    public static IReadOnlyList<string> ExtractAll(IEnumerable<string?> holdingNames)
    {
        ArgumentNullException.ThrowIfNull(holdingNames);

        var alle = new List<string>();
        foreach (var name in holdingNames)
        {
            foreach (var nummer in Extract(name))
            {
                if (!alle.Contains(nummer, StringComparer.Ordinal))
                    alle.Add(nummer);
            }
        }

        return alle;
    }
}
