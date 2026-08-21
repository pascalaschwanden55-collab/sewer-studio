using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

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
    /// Nur eindeutige Treffer werden zurueckgegeben; eine Doppelablage derselben Datei
    /// gilt dabei als eindeutig (siehe <see cref="ResolveFile"/>).
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
            .Where(k => HoldingTextNormalizer.ContainsKeyAtBoundary(
                Path.GetFileNameWithoutExtension(k),
                key))
            .Select(k => ResolveFile(index, k))
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .ToList()!;
    }

    /// <summary>
    /// Gibt den absoluten Pfad des Dateinamens zurueck, sofern dieser eindeutig ist.
    ///
    /// Mehrere Pfade zum selben Dateinamen sind nur dann eine echte Mehrdeutigkeit,
    /// wenn sich der INHALT unterscheidet. Kundenexporte legen dieselbe Datei
    /// regelmaessig doppelt ab — WinCan schreibt die Section-PDFs sowohl nach
    /// DISK1\Section_PDF als auch nach Projects\...\Misc\Docu\Section_PDF. Frueher
    /// verwarf diese Methode dabei JEDEN Treffer; im Projekt Hellgasse gingen so
    /// alle 38 Haltungsprotokolle verloren, obwohl beide Kopien byte-identisch sind.
    /// </summary>
    private static string? ResolveFile(Dictionary<string, List<string>> index, string fileName)
    {
        if (!index.TryGetValue(fileName, out var list) || list.Count == 0)
            return null;

        if (list.Count == 1)
            return list[0];

        return SindInhaltsgleich(list) ? list[0] : null;
    }

    /// <summary>
    /// Prueft, ob alle Pfade auf denselben Dateiinhalt zeigen. Zuerst die Groesse
    /// (billig), erst bei Gleichstand der SHA-256. Ein Lesefehler ergibt bewusst
    /// "nicht gleich": im Zweifel bleibt es bei der bisherigen Mehrdeutigkeit,
    /// statt eine womoeglich falsche Datei anzuhaengen.
    /// </summary>
    private static bool SindInhaltsgleich(List<string> pfade)
    {
        long? groesse = null;
        foreach (var pfad in pfade)
        {
            long aktuell;
            try
            {
                var info = new FileInfo(pfad);
                if (!info.Exists)
                    return false;
                aktuell = info.Length;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return false;
            }

            if (groesse is null)
                groesse = aktuell;
            else if (groesse != aktuell)
                return false;
        }

        string? hash = null;
        foreach (var pfad in pfade)
        {
            var aktuell = BerechneHash(pfad);
            if (aktuell is null)
                return false;

            if (hash is null)
                hash = aktuell;
            else if (!string.Equals(hash, aktuell, StringComparison.Ordinal))
                return false;
        }

        return true;
    }

    private static string? BerechneHash(string pfad)
    {
        try
        {
            using var stream = File.OpenRead(pfad);
            return Convert.ToHexString(SHA256.HashData(stream));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
