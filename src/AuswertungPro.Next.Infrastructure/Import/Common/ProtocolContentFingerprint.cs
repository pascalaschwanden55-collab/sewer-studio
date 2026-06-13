using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.Infrastructure.Import.Common;

/// <summary>
/// Audit I1: Re-Import derselben Quelle soll idempotent sein. Vor dem Historisieren
/// wird der fachliche Inhalt der aktuellen Revision mit den neu importierten Eintraegen
/// verglichen — ist er identisch, wird KEINE neue Revision erzeugt (sonst waechst die
/// History bei jedem Re-Import um eine inhaltsgleiche Kopie).
///
/// Bewusst NICHT im Fingerprint:
/// - EntryId (jeder Import erzeugt neue GUIDs),
/// - FotoPaths wortwoertlich (MediaDistributionService schreibt sie nach dem Import
///   auf projekt-relative Pfade um — nur die ANZAHL zaehlt als Inhalt),
/// - CreatedAt/Comment der Revision (Metadaten, kein Inhalt).
/// </summary>
internal static class ProtocolContentFingerprint
{
    /// <summary>
    /// True, wenn die aktuelle Revision fachlich denselben Inhalt hat wie die
    /// neu importierten Eintraege (reihenfolge-unabhaengig).
    /// </summary>
    internal static bool HasSameContent(ProtocolRevision? current, IReadOnlyList<ProtocolEntry>? incoming)
    {
        if (current is null)
            return false;

        var a = Compute(current.Entries);
        var b = Compute(incoming ?? Array.Empty<ProtocolEntry>());
        return string.Equals(a, b, StringComparison.Ordinal);
    }

    /// <summary>
    /// Kanonischer Fingerprint ueber den fachlichen Inhalt der Eintraege.
    /// Sortiert, damit die Reihenfolge der Quelle keine Rolle spielt.
    /// </summary>
    internal static string Compute(IEnumerable<ProtocolEntry> entries)
    {
        var lines = entries
            .Select(CanonicalLine)
            .OrderBy(l => l, StringComparer.Ordinal)
            .ToList();

        var sb = new StringBuilder();
        foreach (var line in lines)
            sb.Append(line).Append('\n');
        return sb.ToString();
    }

    private static string CanonicalLine(ProtocolEntry e)
    {
        return string.Join('|',
            (e.Code ?? string.Empty).Trim(),
            (e.Beschreibung ?? string.Empty).Trim(),
            FormatMeter(e.MeterStart),
            FormatMeter(e.MeterEnd),
            e.IsStreckenschaden ? "1" : "0",
            (e.Mpeg ?? string.Empty).Trim(),
            e.Zeit?.Ticks.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
            e.IsDeleted ? "1" : "0",
            e.FotoPaths?.Count.ToString(CultureInfo.InvariantCulture) ?? "0");
    }

    private static string FormatMeter(double? value)
        => value?.ToString("R", CultureInfo.InvariantCulture) ?? string.Empty;
}
