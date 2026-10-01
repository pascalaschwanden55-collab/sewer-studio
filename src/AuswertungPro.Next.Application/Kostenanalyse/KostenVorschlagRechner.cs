using System;
using System.Collections.Generic;
using System.Linq;

namespace AuswertungPro.Next.Application.Kostenanalyse;

/// <summary>
/// Baut aus den Nachbarn ein Massnahmenpaket mit Mengen.
///
/// Zwei bewusste Entscheidungen:
/// - Median statt Mittelwert: Ein einzelner Ausreisser (9 Manschetten in einer Haltung)
///   darf den Vorschlag nicht kippen.
/// - Nur Positionen mit Mehrheit: Aus sieben verschiedenen Paketen entstuende sonst ein
///   Sammelsurium, das so nie jemand bestellt haette.
/// </summary>
public static class KostenVorschlagRechner
{
    /// <summary>Einheiten, die auf die Haltungslaenge umgerechnet werden.</summary>
    private static readonly HashSet<string> Metereinheiten =
        new(StringComparer.OrdinalIgnoreCase) { "m", "lfm", "m1" };

    /// <summary>Einheiten, die als ganze Stuecke gelten.</summary>
    private static readonly HashSet<string> Stueckeinheiten =
        new(StringComparer.OrdinalIgnoreCase) { "stk", "st", "stck", "stueck", "stück" };

    public static IReadOnlyList<MassnahmePosition> Rechne(
        KostenfallMerkmale ziel,
        IReadOnlyList<Kostenfall> nachbarn)
    {
        ArgumentNullException.ThrowIfNull(ziel);
        ArgumentNullException.ThrowIfNull(nachbarn);

        if (nachbarn.Count == 0)
            return [];

        var ergebnis = new List<MassnahmePosition>();
        var reihenfolge = new List<string>();
        var werte = new Dictionary<string, List<decimal>>(StringComparer.OrdinalIgnoreCase);
        var einheiten = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var nachbar in nachbarn)
        {
            foreach (var position in nachbar.Positionen)
            {
                if (!werte.TryGetValue(position.ItemKey, out var liste))
                {
                    liste = [];
                    werte[position.ItemKey] = liste;
                    einheiten[position.ItemKey] = position.Einheit;
                    reihenfolge.Add(position.ItemKey);
                }

                liste.Add(NormalisiereMenge(position, nachbar.Merkmale, ziel, einheiten[position.ItemKey]));
            }
        }

        foreach (var key in reihenfolge)
        {
            var liste = werte[key];

            // Strenge Mehrheit: genau die Haelfte reicht nicht.
            if (liste.Count * 2 <= nachbarn.Count)
                continue;

            var einheit = einheiten[key];
            var menge = Median(liste);

            menge = Stueckeinheiten.Contains(einheit)
                ? Math.Ceiling(menge)
                : Math.Round(menge, 2, MidpointRounding.AwayFromZero);

            if (menge <= 0m)
                continue;

            ergebnis.Add(new MassnahmePosition(key, menge, einheit));
        }

        return ergebnis;
    }

    /// <summary>
    /// Meterpositionen werden auf die Ziel-Laenge hochgerechnet. Fehlt beim Nachbarn die
    /// Laenge, wird seine Menge unveraendert uebernommen statt durch null zu teilen.
    /// </summary>
    private static decimal NormalisiereMenge(
        MassnahmePosition position,
        KostenfallMerkmale nachbar,
        KostenfallMerkmale ziel,
        string einheit)
    {
        if (!Metereinheiten.Contains(einheit))
            return position.Menge;

        if (nachbar.LaengeM <= 0d || ziel.LaengeM <= 0d)
            return position.Menge;

        var anteil = position.Menge / (decimal)nachbar.LaengeM;
        return anteil * (decimal)ziel.LaengeM;
    }

    private static decimal Median(List<decimal> werte)
    {
        var sortiert = werte.OrderBy(w => w).ToList();
        var mitte = sortiert.Count / 2;

        return sortiert.Count % 2 == 1
            ? sortiert[mitte]
            : (sortiert[mitte - 1] + sortiert[mitte]) / 2m;
    }
}
