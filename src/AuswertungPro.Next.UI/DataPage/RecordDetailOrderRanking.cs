namespace AuswertungPro.Next.UI.DataPage;

/// <summary>
/// Gemeinsame Sortierregel fuer persoenliche Reihenfolgen in der Detailansicht:
/// einmal fuer die Feldkarten innerhalb einer Spalte, einmal fuer die Spalten selbst.
///
/// Beides folgt derselben Regel, damit es sich gleich verhaelt: Bekanntes kommt in der
/// gespeicherten Reihenfolge, Unbekanntes bleibt an dem Eintrag haengen, hinter dem es
/// bisher stand - es rutscht nicht ans Ende.
/// </summary>
internal static class RecordDetailOrderRanking
{
    /// <summary>
    /// Baut aus der gespeicherten Reihenfolge die Rangliste. Leere Namen und
    /// Wiederholungen werden uebergangen: die erste Nennung gewinnt, damit eine
    /// versehentlich doppelte Zeile die Reihenfolge nicht kippt.
    /// </summary>
    internal static Dictionary<string, int> BuildRankLookup(IReadOnlyList<string> savedOrder)
    {
        var rankByKey = new Dictionary<string, int>(savedOrder.Count, StringComparer.Ordinal);
        foreach (var key in savedOrder)
        {
            if (string.IsNullOrEmpty(key))
                continue;

            rankByKey.TryAdd(key, rankByKey.Count);
        }

        return rankByKey;
    }

    /// <summary>
    /// Sortiert nach der Rangliste. Ein Eintrag, den die Rangliste nicht kennt (neu
    /// hinzugekommen oder ohne Schluessel), bekommt den Rang seines letzten bekannten
    /// Vorgaengers und bleibt dadurch an dessen Seite stehen.
    /// </summary>
    internal static List<T> StableOrder<T>(
        IReadOnlyList<T> entries,
        Func<T, string> keySelector,
        IReadOnlyDictionary<string, int> rankByKey)
    {
        var ranked = new List<(int Rank, int Tiebreak, T Entry)>(entries.Count);
        var lastKnownRank = -1;
        var tiebreak = 0;

        foreach (var entry in entries)
        {
            var key = keySelector(entry);
            if (!string.IsNullOrEmpty(key) && rankByKey.TryGetValue(key, out var rank))
            {
                lastKnownRank = rank;
                tiebreak = 0;
                ranked.Add((rank, 0, entry));
                continue;
            }

            tiebreak++;
            ranked.Add((lastKnownRank, tiebreak, entry));
        }

        return ranked
            .OrderBy(x => x.Rank)
            .ThenBy(x => x.Tiebreak)
            .Select(x => x.Entry)
            .ToList();
    }

    /// <summary>
    /// Rechnet aus Aufnahme- und Zielposition die Zielstelle fuer ein Verschieben aus.
    /// <paramref name="insertAfter"/> heisst: hinter dem Ziel abgelegt.
    /// Liefert -1, wenn sich dadurch nichts aendert oder die Angaben nicht passen.
    /// </summary>
    internal static int ResolveDropTarget(int fromIndex, int targetIndex, bool insertAfter, int count)
    {
        if (count <= 0)
            return -1;
        if (fromIndex < 0 || fromIndex >= count || targetIndex < 0 || targetIndex >= count)
            return -1;

        var insertAt = insertAfter ? targetIndex + 1 : targetIndex;

        // Der Eintrag wird erst entnommen und dann eingesetzt: alles hinter ihm rutscht auf.
        var toIndex = insertAt > fromIndex ? insertAt - 1 : insertAt;

        if (toIndex == fromIndex)
            return -1;

        return Math.Clamp(toIndex, 0, count - 1);
    }

    /// <summary>Verschiebt einen Eintrag von <paramref name="fromIndex"/> nach <paramref name="toIndex"/>.</summary>
    internal static List<T>? Move<T>(IReadOnlyList<T> entries, int fromIndex, int toIndex)
    {
        if (fromIndex == toIndex)
            return null;
        if (fromIndex < 0 || fromIndex >= entries.Count || toIndex < 0 || toIndex >= entries.Count)
            return null;

        var reordered = new List<T>(entries);
        var moved = reordered[fromIndex];
        reordered.RemoveAt(fromIndex);
        reordered.Insert(toIndex, moved);
        return reordered;
    }
}
