namespace AuswertungPro.Next.Domain.Models;

/// <summary>
/// Pure static Helfer fuer den zustandsfreien Map-Kern der Umbenennungs-Logik.
/// Kein Zugriff auf Projekt-State, kein I/O.
/// </summary>
public static class RenameMapNormalizer
{
    /// <summary>
    /// Bereinigt eine Umbenennungs-Map in-place:
    /// Entfernt Selbst- und Leer-Eintraege, kollabiert Ketten auf ihr Endziel.
    /// Gibt true zurueck, wenn die Map veraendert wurde.
    /// </summary>
    public static bool NormalizeMap(Dictionary<string, string> map)
    {
        var changed = false;
        var keys = map.Keys.ToList();

        foreach (var key in keys)
        {
            if (!map.TryGetValue(key, out var targetRaw))
                continue;

            var target = NormalizeToken(targetRaw);
            if (string.IsNullOrWhiteSpace(target))
            {
                map.Remove(key);
                changed = true;
                continue;
            }

            var resolved = ResolveValue(map, target);
            if (string.IsNullOrWhiteSpace(resolved)
                || string.Equals(key, resolved, StringComparison.OrdinalIgnoreCase))
            {
                map.Remove(key);
                changed = true;
                continue;
            }

            if (!string.Equals(target, resolved, StringComparison.OrdinalIgnoreCase))
            {
                map[key] = resolved;
                changed = true;
            }
        }

        return changed;
    }

    /// <summary>
    /// Loest einen Wert entlang der Kette auf (zyklussicher via visited-Set).
    /// </summary>
    public static string ResolveValue(IReadOnlyDictionary<string, string> map, string value)
    {
        var current = NormalizeToken(value);
        if (string.IsNullOrWhiteSpace(current))
            return string.Empty;

        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        while (map.TryGetValue(current, out var nextRaw))
        {
            var next = NormalizeToken(nextRaw);
            if (string.IsNullOrWhiteSpace(next)
                || string.Equals(current, next, StringComparison.OrdinalIgnoreCase)
                || !visited.Add(current))
                break;

            current = next;
        }

        return current;
    }

    /// <summary>
    /// Trimmt einen Token-Wert; null wird als leerer String behandelt.
    /// </summary>
    public static string NormalizeToken(string? value)
        => (value ?? string.Empty).Trim();
}
