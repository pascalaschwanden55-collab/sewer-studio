using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

using AuswertungPro.Next.Application.Protocol;
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

            if (!IsMergeableGroup(group))
            {
                // Divergierende Beobachtungen am selben Code+Meter (unterschiedliche Beschreibung,
                // Uhrlage oder Quantifizierung) -> alle behalten (kein Datenverlust).
                result.AddRange(group);
                continue;
            }

            // Eine logische Beobachtung über mehrere Zeilen verteilt -> in einen Klon falten.
            var baseEntry = group.FirstOrDefault(IsSubstantive) ?? group[0];
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

        // Beim Falten einer Beobachtung nur exakt gleiche Pfade deduplizieren.
        // Gleichnamige Fotos in verschiedenen Ordnern koennen unterschiedliche Bilder sein.
        MergeExactPhotoPaths(target, other);

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

    /// <summary>
    /// Eine Gruppe (gleicher Code + Meter) ist nur dann zusammenführbar, wenn jede
    /// diskriminierende Dimension (Beschreibung, Uhrlage von/bis, Quantifizierung 1/2)
    /// höchstens EINEN distinkten nicht-leeren Wert trägt. Andernfalls sind es echte,
    /// unterschiedliche Beobachtungen -> nicht falten (kein Datenverlust).
    /// </summary>
    private static bool IsMergeableGroup(List<ProtocolEntry> group)
    {
        if (DistinctNonEmpty(group, NormDesc) > 1)
            return false;

        // Bekannte semantische Dimensionen mit Alias-Normalisierung (Uhrlage/Quantifizierung):
        // derselbe Wert unter unterschiedlichen Schlüsseln zählt als EIN Wert.
        if (DistinctNonEmpty(group, e => ParamValue(e, "vsa.uhr.von", "ClockPos1", "Uhr_von")) > 1
            || DistinctNonEmpty(group, e => ParamValue(e, "vsa.uhr.bis", "ClockPos2", "Uhr_bis")) > 1
            || DistinctNonEmpty(group, e => ParamValue(e, "Quantifizierung1", "vsa.q1", "Q1")) > 1
            || DistinctNonEmpty(group, e => ParamValue(e, "Quantifizierung2", "vsa.q2", "Q2")) > 1)
            return false;

        // Jeder weitere Parameter-Schlüssel ist ebenfalls eine Unterscheidungs-Dimension
        // (z.B. Breite/Höhe unter eigenem Schlüssel) -> divergierende Werte blocken die Faltung.
        var keys = group
            .Where(e => e.CodeMeta?.Parameters is { Count: > 0 })
            .SelectMany(e => e.CodeMeta!.Parameters.Keys)
            .Distinct(StringComparer.OrdinalIgnoreCase);
        foreach (var key in keys)
        {
            if (DistinctNonEmpty(group, e => ParamValueByKey(e, key)) > 1)
                return false;
        }

        return true;
    }

    private static string? ParamValueByKey(ProtocolEntry e, string key)
    {
        var parameters = e.CodeMeta?.Parameters;
        return parameters is not null && parameters.TryGetValue(key, out var value) ? value : null;
    }

    private static int DistinctNonEmpty(List<ProtocolEntry> group, Func<ProtocolEntry, string?> selector)
        => group.Select(selector)
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Select(v => v!.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();

    private static string? ParamValue(ProtocolEntry e, params string[] keys)
    {
        var parameters = e.CodeMeta?.Parameters;
        return parameters is null ? null : ProtocolDescriptionBuilder.GetFirstParameter(parameters, keys);
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

    private static void MergeExactPhotoPaths(ProtocolEntry target, ProtocolEntry other)
    {
        if (other.FotoPaths.Count == 0)
            return;

        var existing = new HashSet<string>(
            target.FotoPaths.Select(NormalizePhotoPathKey),
            StringComparer.OrdinalIgnoreCase);

        foreach (var path in other.FotoPaths)
        {
            if (string.IsNullOrWhiteSpace(path))
                continue;

            var key = NormalizePhotoPathKey(path);
            if (existing.Add(key))
                target.FotoPaths.Add(path);
        }
    }

    private static string NormalizePhotoPathKey(string path)
        => path.Replace('\\', '/').Trim().ToUpperInvariant();

}
