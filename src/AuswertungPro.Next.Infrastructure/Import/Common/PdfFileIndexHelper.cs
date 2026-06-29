using System;
using System.Collections.Generic;
using System.Linq;

namespace AuswertungPro.Next.Infrastructure.Import.Common;

/// <summary>
/// Gemeinsamer Helfer fuer die PDF-Treffer-Suche im Datei-Index.
/// Wird von WinCan (LinkSectionPdf, LinkNodePdf) und IBAK (LinkHoldingPdf) genutzt,
/// um trivialen LINQ-Duplikat-Code zu vermeiden.
/// </summary>
internal static class PdfFileIndexHelper
{
    /// <summary>
    /// Sucht alle PDF-Eintraege im Index, deren Dateiname <paramref name="key"/> enthaelt,
    /// und loest jeden Eintrag ueber den Index auf.
    /// Nur eindeutige Treffer (genau ein Pfad pro Dateiname) werden zurueckgegeben.
    /// Kein IO — der Index ist ein In-Memory-Dictionary.
    /// </summary>
    /// <param name="index">Datei-Index: Dateiname -> Liste absoluter Pfade</param>
    /// <param name="key">Haltungs- oder Schachtnummer, nach der im Dateinamen gesucht wird</param>
    /// <returns>Liste aufgeloester absoluter Pfade (leer, wenn keine Treffer)</returns>
    public static List<string> ResolvePdfMatches(
        Dictionary<string, List<string>> index,
        string key)
    {
        return index.Keys
            .Where(k => k.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            .Where(k => k.Contains(key, StringComparison.OrdinalIgnoreCase))
            .Select(k => ResolveFile(index, k))
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .ToList()!;
    }

    /// <summary>
    /// Gibt den absoluten Pfad des Dateinamens zurueck, sofern dieser im Index
    /// genau einmal vorhanden ist. Bei Mehrdeutigkeit (mehrere Pfade) wird null zurueckgegeben.
    /// </summary>
    private static string? ResolveFile(Dictionary<string, List<string>> index, string fileName)
    {
        if (!index.TryGetValue(fileName, out var list) || list.Count == 0)
            return null;

        if (list.Count == 1)
            return list[0];

        return null;
    }
}
