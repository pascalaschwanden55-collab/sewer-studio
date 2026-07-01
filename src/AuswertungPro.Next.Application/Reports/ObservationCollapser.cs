using System.Collections.Generic;
using System.Globalization;
using System.Linq;

using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.Application.Reports;

/// <summary>
/// Faltet redundante Fortsetzungs-/Quantifizierungszeilen einer Beobachtung zu EINEM Eintrag
/// zusammen (Merge statt Drop). Hintergrund: WinCan/XTF liefern pro Beobachtung oft mehrere
/// Roh-Findings (Text-Zeile + separate Quantifizierungs- oder „–"-Zeile gleichen Codes am
/// gleichen Meter). Das aufgeblähte diese Liste gegenüber dem Originalprotokoll.
///
/// Invariante (kein Datenverlust): Tragen zwei Einträge gleichen Codes am gleichen Meter
/// UNTERSCHIEDLICHE, inhaltliche Beschreibungen, bleiben BEIDE erhalten. Zusammengeführt wird
/// nur, wenn höchstens eine inhaltliche Beschreibung existiert; alle übrigen Zeilen sind dann
/// leer/„–"/reine Quantifizierung und werden in den Basiseintrag gefaltet (Beschreibung,
/// Fotos, MPEG/Zeit, Quantifizierungs-Parameter vereint).
///
/// Arbeitet ausschliesslich auf tiefen Klonen des Basiseintrags — geteilte Referenzen
/// (z.B. aus <see cref="ProtocolDocument"/>) werden nie mutiert.
/// </summary>
public static class ObservationCollapser
{
    public static List<ProtocolEntry> Collapse(IReadOnlyList<ProtocolEntry>? entries)
    {
        if (entries is null || entries.Count <= 1)
            return entries?.ToList() ?? new List<ProtocolEntry>();

        // Gruppen in Reihenfolge des ersten Auftretens (stabil).
        var order = new List<string>();
        var groups = new Dictionary<string, List<ProtocolEntry>>(System.StringComparer.Ordinal);
        foreach (var e in entries)
        {
            var key = GroupKey(e);
            if (!groups.TryGetValue(key, out var bucket))
            {
                bucket = new List<ProtocolEntry>();
                groups[key] = bucket;
                order.Add(key);
            }
            bucket.Add(e);
        }

        var result = new List<ProtocolEntry>();
        foreach (var key in order)
        {
            var group = groups[key];
            if (group.Count == 1)
            {
                result.Add(group[0]);
                continue;
            }

            var substantive = group.Where(IsSubstantive).ToList();
            var distinctDescriptions = substantive
                .Select(NormDesc)
                .Distinct(System.StringComparer.OrdinalIgnoreCase)
                .Count();

            if (distinctDescriptions >= 2)
            {
                // Echte, unterschiedliche Beobachtungen am selben Code+Meter -> alle behalten.
                result.AddRange(group);
                continue;
            }

            // Eine logische Beobachtung über mehrere Zeilen verteilt -> in einen Klon falten.
            var baseEntry = substantive.FirstOrDefault() ?? group[0];
            var merged = ProtocolEntryCloner.CloneLegacyProtocolEntry(baseEntry);
            foreach (var other in group)
            {
                if (ReferenceEquals(other, baseEntry))
                    continue;
                FoldInto(merged, other);
            }
            result.Add(merged);
        }

        return result;
    }

    private static void FoldInto(ProtocolEntry target, ProtocolEntry other)
    {
        // Beschreibung: leere Basis nimmt inhaltliche Beschreibung des Partners an.
        if (!IsSubstantive(target) && IsSubstantive(other))
            target.Beschreibung = other.Beschreibung;

        // Fotos vereinen (pfadnormalisiert, keine Duplikate).
        foreach (var path in other.FotoPaths)
        {
            if (!target.FotoPaths.Any(x => PathEquals(x, path)))
                target.FotoPaths.Add(path);
        }

        // Timecode/Zeit übernehmen, wenn Basis leer.
        if (string.IsNullOrWhiteSpace(target.Mpeg) && !string.IsNullOrWhiteSpace(other.Mpeg))
            target.Mpeg = other.Mpeg;
        if (!target.Zeit.HasValue && other.Zeit.HasValue)
            target.Zeit = other.Zeit;

        // Quantifizierung/Parameter vereinen.
        if (other.CodeMeta is not null)
        {
            target.CodeMeta ??= new ProtocolEntryCodeMeta { Code = target.Code };
            foreach (var kv in other.CodeMeta.Parameters)
            {
                if (string.IsNullOrWhiteSpace(kv.Value))
                    continue;
                if (!target.CodeMeta.Parameters.TryGetValue(kv.Key, out var existing) || string.IsNullOrWhiteSpace(existing))
                    target.CodeMeta.Parameters[kv.Key] = kv.Value;
            }
            if (string.IsNullOrWhiteSpace(target.CodeMeta.Severity) && !string.IsNullOrWhiteSpace(other.CodeMeta.Severity))
                target.CodeMeta.Severity = other.CodeMeta.Severity;
            if (target.CodeMeta.Count is null && other.CodeMeta.Count is not null)
                target.CodeMeta.Count = other.CodeMeta.Count;
            if (string.IsNullOrWhiteSpace(target.CodeMeta.Notes) && !string.IsNullOrWhiteSpace(other.CodeMeta.Notes))
                target.CodeMeta.Notes = other.CodeMeta.Notes;
        }
    }

    private static string GroupKey(ProtocolEntry e)
    {
        var code = (e.Code ?? string.Empty).Trim().ToUpperInvariant();
        var start = e.MeterStart.HasValue ? e.MeterStart.Value.ToString("0.00", CultureInfo.InvariantCulture) : "∅";
        var end = e.MeterEnd.HasValue ? e.MeterEnd.Value.ToString("0.00", CultureInfo.InvariantCulture) : "∅";
        return code + "|" + start + "|" + end;
    }

    private static string NormDesc(ProtocolEntry e)
        => ProtocolZustandText.NormalizeZustandDescription(e.Beschreibung, e.Code) ?? string.Empty;

    private static bool IsSubstantive(ProtocolEntry e)
        => !string.IsNullOrWhiteSpace(NormDesc(e));

    private static bool PathEquals(string a, string b)
        => string.Equals(
            (a ?? string.Empty).Replace('\\', '/'),
            (b ?? string.Empty).Replace('\\', '/'),
            System.StringComparison.OrdinalIgnoreCase);
}
