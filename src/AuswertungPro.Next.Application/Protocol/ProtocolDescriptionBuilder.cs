namespace AuswertungPro.Next.Application.Protocol;

/// <summary>
/// Baut die lesbare Standardbeschreibung fuer einen Protokolleintrag aus Code-Definition,
/// Parameterwerten (inkl. DataKey-Lookup und WinCan-Aliasen) und Streckenmeter.
/// Logik aus ObservationCatalogViewModel.BuildDefaultDescription extrahiert, verhaltensneutral.
/// Hinweis: Unterscheidet sich von <see cref="DefaultDescriptionBuilder"/> (der Name=Wert-Stil verwendet).
/// </summary>
public static class ProtocolDescriptionBuilder
{
    /// <summary>
    /// Erzeugt eine lesbare Standardbeschreibung.
    /// Parameter werden per DataKey (oder Name als Fallback) gesucht und als "{Wert}{Einheit}" formatiert.
    /// Uhrzeiten und Quantifizierungen werden aus VSA/WinCan-Aliasschluesseln angehaengt.
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
            // Code-spezifische Parameter (Hoehe, Breite, Kruemmungswinkel, etc.)
            foreach (var p in def.Parameters)
            {
                string? value = null;
                var key = p.DataKey ?? p.Name;
                if (!parameters.TryGetValue(key, out value) || string.IsNullOrWhiteSpace(value))
                {
                    if (!parameters.TryGetValue(p.Name, out value) || string.IsNullOrWhiteSpace(value))
                        continue;
                }
                var unit = string.IsNullOrWhiteSpace(p.Unit) ? "" : $"{p.Unit}";
                parts.Add($"{value}{unit}".Trim());
            }

            // Uhrzeiten (von/bis)
            var uhrVon = GetFirstParameter(parameters, "vsa.uhr.von", "ClockPos1");
            var uhrBis = GetFirstParameter(parameters, "vsa.uhr.bis", "ClockPos2");
            if (!string.IsNullOrWhiteSpace(uhrVon) && !string.IsNullOrWhiteSpace(uhrBis))
                parts.Add($"von {uhrVon} Uhr bis {uhrBis} Uhr");
            else if (!string.IsNullOrWhiteSpace(uhrVon))
                parts.Add($"bei {uhrVon} Uhr");

            // Quantifizierung
            var q1 = GetFirstParameter(parameters, "vsa.q1", "Q1", "Quantifizierung1");
            if (!string.IsNullOrWhiteSpace(q1))
                parts.Add($"{q1}%");
            var q2 = GetFirstParameter(parameters, "vsa.q2", "Q2", "Quantifizierung2");
            if (!string.IsNullOrWhiteSpace(q2))
                parts.Add($"{q2}%");
        }

        if (def.RequiresRange && meterStart.HasValue && meterEnd.HasValue)
            parts.Add($"Strecke {meterStart:0.00}-{meterEnd:0.00} m");

        if (parts.Count == 0)
            return title;

        return $"{title}: {string.Join(", ", parts)}";
    }

    /// <summary>
    /// Gibt den ersten nicht-leeren Wert fuer eine der gegebenen Schuessel zurueck.
    /// </summary>
    public static string? GetFirstParameter(IReadOnlyDictionary<string, string>? parameters, params string[] keys)
    {
        if (parameters is null || keys.Length == 0)
            return null;

        foreach (var key in keys)
        {
            if (string.IsNullOrWhiteSpace(key))
                continue;
            if (!parameters.TryGetValue(key, out var value))
                continue;
            if (string.IsNullOrWhiteSpace(value))
                continue;
            return value.Trim();
        }

        return null;
    }
}
