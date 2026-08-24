using System;
using System.Collections.Generic;
using System.Linq;

namespace AuswertungPro.Next.Application.Dossiers.Lookup;

/// <summary>
/// Die eine Regel, welche Leitungen einer Parzelle vorgeschlagen und welche
/// davon angehakt werden.
///
/// Sie entscheidet, was am Ende im Eigentuemerbrief steht — und sie lag bis
/// heute zweimal im Code: einmal fuer die Einzelabfrage, einmal fuer den
/// Stapel. Die zwei Fassungen waren bereits verschieden (der Stapel
/// entdoppelte die Kantonsliste nicht und verglich Projektnamen ohne Trimmen).
///
/// Die Zusammenfuehrung nimmt jeweils die vorsichtigere Fassung.
///
/// Zwei Herkuenfte, in dieser Reihenfolge:
/// <list type="number">
/// <item><b>Lage</b> — der Kanton fuehrt die Leitung auf dieser Parzelle.
/// Angehakt nur, wenn sie privat ist UND im Projekt liegt.</item>
/// <item><b>Name</b> — der Kanton kennt sie nicht, aber ihr Knotenname nennt
/// die Parzelle. Das sind die privaten Hausanschluesse; sie stammen aus dem
/// Projekt und sind deshalb immer angehakt.</item>
/// </list>
/// </summary>
public static class ProposedHoldingComposer
{
    public static IReadOnlyList<ProposedHolding> Compose(
        IReadOnlyList<NetworkHolding>? nachLage,
        IReadOnlyList<string>? projectHoldingNames,
        string? parcelNumber)
    {
        var namen = (projectHoldingNames ?? Array.Empty<string>())
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Select(n => n.Trim())
            .ToList();

        // Getrimmt vergleichen: sonst waere dieselbe Leitung je nach Weg
        // einmal angehakt und einmal nicht.
        var imProjekt = new HashSet<string>(namen, StringComparer.OrdinalIgnoreCase);

        var ergebnis = new List<ProposedHolding>();
        var gesehen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var leitung in nachLage ?? Array.Empty<NetworkHolding>())
        {
            var bezeichnung = (leitung?.Designation ?? string.Empty).Trim();

            // Der Kartendienst liefert je Geometrieteil einen Treffer;
            // dieselbe Leitung darf im Brief nur einmal stehen.
            if (bezeichnung.Length == 0 || !gesehen.Add(bezeichnung))
                continue;

            var bekannt = imProjekt.Contains(bezeichnung);

            ergebnis.Add(new ProposedHolding(
                bezeichnung,
                leitung!.IsPrivate,
                bekannt,
                Preselected: bekannt && leitung.IsPrivate,
                Origin: "Lage"));
        }

        foreach (var name in ParcelHoldingAndShaftMatcher.HoldingsByName(namen, parcelNumber))
        {
            if (!gesehen.Add(name))
                continue;

            // Ueber den Namen gefunden heisst: privater Hausanschluss. Der
            // Kanton fuehrt diese Leitungen nicht, deshalb steht hier keine
            // Eigentumsangabe zur Verfuegung — angenommen wird privat.
            ergebnis.Add(new ProposedHolding(
                name,
                IsPrivate: true,
                InProject: true,
                Preselected: true,
                Origin: "Name"));
        }

        return ergebnis;
    }
}
