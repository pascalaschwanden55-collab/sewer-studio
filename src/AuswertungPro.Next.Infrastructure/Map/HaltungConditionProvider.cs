using System.Collections.Generic;
using System.Globalization;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Infrastructure.Map;

/// <summary>Projekt-Haltungen → Zustand-Rohwert je Haltungsname (null = nicht inspiziert).</summary>
public static class HaltungConditionProvider
{
    public static IReadOnlyDictionary<string, int?> Build(IEnumerable<HaltungRecord> records)
    {
        var map = new Dictionary<string, int?>(StringComparer.OrdinalIgnoreCase);
        foreach (var r in records)
        {
            var name = r.GetFieldValue("Haltungsname");
            if (string.IsNullOrWhiteSpace(name)) continue;
            var roh = r.GetFieldValue("Zustandsklasse");
            map[name] = int.TryParse(roh, NumberStyles.Integer, CultureInfo.InvariantCulture, out var z) ? z : null;
        }
        return map;
    }
}
