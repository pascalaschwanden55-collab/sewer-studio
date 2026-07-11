using System;
using System.Collections.Generic;
using System.Globalization;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Mapping;

/// <summary>Projekt-Haltungen -> Nennweite (DN in mm) je Haltungsname, fuer die Linienbreite
/// auf der Karte (Muster wie HaltungConditionProvider).</summary>
public static class HaltungDnProvider
{
    public static IReadOnlyDictionary<string, int?> Build(IEnumerable<HaltungRecord> records)
    {
        var map = new Dictionary<string, int?>(StringComparer.OrdinalIgnoreCase);
        foreach (var r in records)
        {
            var name = r.GetFieldValue("Haltungsname");
            if (string.IsNullOrWhiteSpace(name)) continue;
            var roh = r.GetFieldValue("DN_mm");
            map[name] = int.TryParse(roh, NumberStyles.Integer, CultureInfo.InvariantCulture, out var dn) ? dn : null;
        }
        return map;
    }
}
