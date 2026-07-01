using System;
using System.Collections.Generic;
using System.Linq;

using AuswertungPro.Next.Application.Protocol;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.Application.Reports;

/// <summary>
/// Baut den Zustandstext einer Beobachtung als Klartext: menschliche Beschreibung plus
/// über den Code-Katalog benannte, mit Einheit versehene Quantifizierer
/// (z.B. "Bogen nach rechts, Winkel = 45°" statt rohem "Q1=45").
///
/// Ohne Katalog oder für unbekannte Codes ist das Ergebnis verhaltensneutral identisch zu
/// <see cref="ProtocolZustandText.BuildObservationZustandTextLong"/> (Rohverhalten bleibt).
/// </summary>
public static class ObservationZustandBuilder
{
    public static string Build(ProtocolEntry entry, ICodeCatalogProvider? catalog)
    {
        var parts = new List<string>();

        var human = ProtocolZustandText.NormalizeZustandDescription(entry.Beschreibung, entry.Code);
        if (!string.IsNullOrWhiteSpace(human))
            parts.Add(human);

        parts.AddRange(BuildCatalogQuantifiers(entry, catalog));

        if (parts.Count == 0)
            return ProtocolZustandText.BuildObservationZustandTextLong(entry);

        return string.Join(", ", parts);
    }

    private static IEnumerable<string> BuildCatalogQuantifiers(ProtocolEntry entry, ICodeCatalogProvider? catalog)
    {
        var result = new List<string>();
        var parameters = entry.CodeMeta?.Parameters;
        if (catalog is null || parameters is null || parameters.Count == 0)
            return result;

        if (string.IsNullOrWhiteSpace(entry.Code) || !catalog.TryGet(entry.Code, out var def))
            return result;

        var consumed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Code-spezifische Parameter mit Namen + Einheit ("Winkel = 45°", "Breite = 10mm").
        foreach (var p in def.Parameters)
        {
            var key = string.IsNullOrWhiteSpace(p.DataKey) ? p.Name : p.DataKey!;
            var value = ProtocolDescriptionBuilder.GetFirstParameter(parameters, key, p.Name);
            if (string.IsNullOrWhiteSpace(value))
                continue;

            consumed.Add(key);
            consumed.Add(p.Name);

            var label = string.IsNullOrWhiteSpace(p.Name) ? key : p.Name;
            var unit = p.Unit ?? string.Empty;
            result.Add($"{label} = {value}{unit}");
        }

        // Uhrlage.
        var uhrVon = ProtocolDescriptionBuilder.GetFirstParameter(parameters, "vsa.uhr.von", "ClockPos1", "Uhr_von");
        var uhrBis = ProtocolDescriptionBuilder.GetFirstParameter(parameters, "vsa.uhr.bis", "ClockPos2", "Uhr_bis");
        if (!string.IsNullOrWhiteSpace(uhrVon) && !string.IsNullOrWhiteSpace(uhrBis)
            && !string.Equals(uhrVon, uhrBis, StringComparison.OrdinalIgnoreCase))
            result.Add($"Lage {uhrVon}–{uhrBis} Uhr");
        else if (!string.IsNullOrWhiteSpace(uhrVon))
            result.Add($"Lage {uhrVon} Uhr");

        // Rohe Quantifizierung nur, wenn kein benannter Parameter sie bereits abgedeckt hat.
        if (!ConsumedAny(consumed, "Quantifizierung1", "vsa.q1", "Q1"))
        {
            var q1 = ProtocolDescriptionBuilder.GetFirstParameter(parameters, "Quantifizierung1", "vsa.q1", "Q1");
            if (!string.IsNullOrWhiteSpace(q1))
                result.Add($"Q1 = {q1}");
        }
        if (!ConsumedAny(consumed, "Quantifizierung2", "vsa.q2", "Q2"))
        {
            var q2 = ProtocolDescriptionBuilder.GetFirstParameter(parameters, "Quantifizierung2", "vsa.q2", "Q2");
            if (!string.IsNullOrWhiteSpace(q2))
                result.Add($"Q2 = {q2}");
        }

        return result;
    }

    private static bool ConsumedAny(HashSet<string> consumed, params string[] keys)
        => keys.Any(consumed.Contains);
}
