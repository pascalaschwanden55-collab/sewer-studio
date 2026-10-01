namespace AuswertungPro.Next.Application.Xtf;

/// <summary>
/// Ergebnis einer beidseitig eindeutigen Zuordnung.
/// </summary>
public sealed record XtfPaarung<TLinks, TRechts>(
    IReadOnlyDictionary<TLinks, TRechts> Zugeordnet,
    IReadOnlyList<TLinks> Mehrdeutig,
    IReadOnlyList<TLinks> OhneTreffer,
    IReadOnlyList<TRechts> NichtVerwendet)
    where TLinks : notnull
    where TRechts : notnull;

/// <summary>
/// Ordnet zwei Listen einander zu und laesst dabei nur **beidseitig eindeutige** Paare gelten:
/// Genau ein Rechts passt zum Links UND genau ein Links passt zu diesem Rechts.
///
/// Der Grund ist die Regel des ganzen XTF-Wegs: Mehrdeutiges wird nie geraten, sondern dem
/// Menschen vorgelegt. Nebeneffekt und ebenso wichtig: Das Ergebnis haengt nicht davon ab,
/// in welcher Reihenfolge die Eintraege stehen.
/// </summary>
public static class XtfEindeutigeZuordnung
{
    public static XtfPaarung<TLinks, TRechts> Bilde<TLinks, TRechts>(
        IReadOnlyList<TLinks> links,
        IReadOnlyList<TRechts> rechts,
        Func<TLinks, TRechts, bool> passt)
        where TLinks : notnull
        where TRechts : notnull
    {
        ArgumentNullException.ThrowIfNull(links);
        ArgumentNullException.ThrowIfNull(rechts);
        ArgumentNullException.ThrowIfNull(passt);

        var moeglich = links.ToDictionary(
            l => l,
            l => rechts.Where(r => passt(l, r)).ToList());

        var zugeordnet = new Dictionary<TLinks, TRechts>();
        var mehrdeutig = new List<TLinks>();
        var ohneTreffer = new List<TLinks>();

        foreach (var l in links)
        {
            var passende = moeglich[l];
            if (passende.Count == 0)
            {
                ohneTreffer.Add(l);
                continue;
            }

            if (passende.Count > 1)
            {
                mehrdeutig.Add(l);
                continue;
            }

            // Gegenrichtung: beansprucht noch jemand anderes dasselbe Gegenstueck?
            var kandidat = passende[0];
            var mitbewerber = moeglich.Count(kv => kv.Value.Any(r => Equals(r, kandidat)));
            if (mitbewerber > 1)
            {
                mehrdeutig.Add(l);
                continue;
            }

            zugeordnet[l] = kandidat;
        }

        var verwendet = new HashSet<TRechts>(zugeordnet.Values);
        var nichtVerwendet = rechts.Where(r => !verwendet.Contains(r)).ToList();

        return new XtfPaarung<TLinks, TRechts>(zugeordnet, mehrdeutig, ohneTreffer, nichtVerwendet);
    }
}
