using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace AuswertungPro.Next.Infrastructure.Import.Ibak;

/// <summary>
/// Rein statische Schema-Heuristiken für IBAK Firebird-Datenbanken.
/// Kein Datenbankzugriff — alle Methoden arbeiten ausschließlich auf
/// bereits geladenen Tabellen-/Spaltenlisten und Dateinamen.
/// </summary>
internal static class IbakFdbSchemaHeuristics
{
    /// <summary>
    /// Bewertet die gegebenen Tabellen und gibt die wahrscheinlichste
    /// Foto-Tabelle zurück, oder null wenn keine Tabelle den Mindestscore (6) erreicht.
    /// </summary>
    internal static string? PickPhotoTable(List<string> tables, Dictionary<string, List<string>> columns)
    {
        string? best = null;
        var bestScore = 0;

        foreach (var t in tables)
        {
            if (!columns.TryGetValue(t, out var cols))
                continue;

            var score = 0;
            var nameUpper = t.ToUpperInvariant();
            if (nameUpper.Contains("PHOTO") || nameUpper.Contains("FOTO") || nameUpper.Contains("BILD") || nameUpper.Contains("IMAGE") || nameUpper.Contains("PIC"))
                score += 6;
            if (nameUpper.Contains("MEDIA"))
                score += 3;

            if (cols.Any(c => ContainsAny(c, "FILE", "FILENAME", "PATH", "NAME", "DATEI")))
                score += 4;
            if (cols.Any(c => ContainsAny(c, "HALT", "HOLD", "LINE", "SECTION", "ROHR", "PIPE", "OBJ", "OBJECT")))
                score += 2;

            if (score > bestScore)
            {
                bestScore = score;
                best = t;
            }
        }

        return bestScore >= 6 ? best : null;
    }

    /// <summary>
    /// Sucht die erste Spalte in <paramref name="cols"/>, die einen der gegebenen Schlüssel enthält (Groß-/Kleinschreibung ignoriert).
    /// </summary>
    internal static string? FindColumn(List<string> cols, params string[] keys)
    {
        foreach (var key in keys)
        {
            var col = cols.FirstOrDefault(c => c.Contains(key, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(col))
                return col;
        }
        return null;
    }

    /// <summary>
    /// Gibt true zurück, wenn <paramref name="text"/> mindestens einen der Schlüssel enthält (Groß-/Kleinschreibung ignoriert).
    /// </summary>
    internal static bool ContainsAny(string text, params string[] keys)
    {
        foreach (var key in keys)
            if (text.Contains(key, StringComparison.OrdinalIgnoreCase))
                return true;
        return false;
    }

    /// <summary>
    /// Extrahiert den normalisierten Haltungsschlüssel aus einem IBAK-Fotoname
    /// (unterstuetzt L__, L_, H__ und H_ Praefixe).
    /// </summary>
    internal static string ExtractHoldingFromPhoto(string fileName)
    {
        var m = Regex.Match(fileName, @"^(?:L__|L_|H__|H_)(.+?)_(\d+)\.(jpg|jpeg|png|bmp)$", RegexOptions.IgnoreCase);
        if (m.Success)
            return Common.HoldingKeyNormalizer.NormalizeIbak(m.Groups[1].Value);
        return "";
    }

    /// <summary>
    /// Extrahiert den numerischen Foto-Index aus einem Dateinamen (_NNN.jpg).
    /// Gibt int.MaxValue zurück, wenn kein Index gefunden wird.
    /// </summary>
    internal static int ExtractPhotoIndex(string fileName)
    {
        var m = Regex.Match(fileName, @"_(\d+)\.(jpg|jpeg|png|bmp)$", RegexOptions.IgnoreCase);
        if (m.Success && int.TryParse(m.Groups[1].Value, out var n))
            return n;
        return int.MaxValue;
    }
}
