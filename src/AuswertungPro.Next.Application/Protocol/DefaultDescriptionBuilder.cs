namespace AuswertungPro.Next.Application.Protocol;

/// <summary>
/// Baut eine lesbare Standardbeschreibung fuer einen Protokolleintrag aus Code-Definition und Parameterwerten.
/// </summary>
public static class DefaultDescriptionBuilder
{
    /// <summary>
    /// Erzeugt eine Standardbeschreibung aus Katalog-Definition + Parameterwerten + optionaler Streckenmeter.
    /// Gibt den Titel der Definition zurueck, wenn keine Parameter vorliegen.
    /// </summary>
    public static string Build(
        CodeDefinition def,
        IReadOnlyDictionary<string, string>? parameters,
        double? meterStart,
        double? meterEnd)
    {
        var title = def.Title ?? string.Empty;
        var parts = new List<string>();

        if (parameters is not null && parameters.Count > 0)
        {
            foreach (var p in def.Parameters)
            {
                if (!parameters.TryGetValue(p.Name, out var value) || string.IsNullOrWhiteSpace(value))
                    continue;
                var unit = string.IsNullOrWhiteSpace(p.Unit) ? "" : $" {p.Unit}";
                parts.Add($"{p.Name}={value}{unit}".Trim());
            }
        }

        if (def.RequiresRange && meterStart.HasValue && meterEnd.HasValue)
        {
            parts.Add($"Strecke {meterStart:0.00}-{meterEnd:0.00} m");
        }

        if (parts.Count == 0)
            return title;

        return $"{title} ({string.Join(", ", parts)})";
    }
}
