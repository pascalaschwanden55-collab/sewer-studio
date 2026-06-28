using System.Collections.Generic;
using System.Linq;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Infrastructure.Output.Offers;

// ---------------------------------------------------------------------------
// Kategorien fuer Spezial-Statistiken in der Kostenzusammenstellung
// ---------------------------------------------------------------------------

/// <summary>Kategorien fuer aggregierte Mengenstatistiken (Liner, Manschetten).</summary>
public enum SpecialStatsCategory
{
    None = 0,
    InlinerGfk = 1,
    InlinerNadelfilz = 2,
    Manschette = 3,
    Linerendmanschette = 4
}

/// <summary>Konfigurationseintrag fuer eine Spezial-Statistik-Kategorie.</summary>
public sealed record SpecialStatsConfig(
    SpecialStatsCategory Category,
    string Label,
    string DefaultUnit);

/// <summary>Laufzeit-Aggregations-Eimer fuer eine Kategorie (Menge, Betrag, Einheiten).</summary>
public sealed class SpecialStatsBucket
{
    public string DefaultUnit { get; set; } = "";
    public decimal TotalQty { get; set; }
    public decimal TotalNet { get; set; }
    public HashSet<string> Units { get; } = new(System.StringComparer.OrdinalIgnoreCase);
}

// ---------------------------------------------------------------------------
// Reiner Klassifikator (kein IO, kein Dokument-Objekt)
// ---------------------------------------------------------------------------

/// <summary>
/// Pure-static Klassifikator: ordnet eine <see cref="CostLine"/> einer
/// <see cref="SpecialStatsCategory"/> zu und stellt Hilfsmethoden fuer
/// Einheiten-Normalisierung und Anzeige-Einheit bereit.
/// </summary>
public static class SpecialStatsClassifier
{
    /// <summary>Vordefinierte Konfigurationen in Ausgabe-Reihenfolge.</summary>
    public static readonly SpecialStatsConfig[] SpecialStatsConfigs =
    [
        new(SpecialStatsCategory.InlinerGfk,          "Inliner GFK",              "m"),
        new(SpecialStatsCategory.InlinerNadelfilz,    "Inliner Nadelfilz",        "m"),
        new(SpecialStatsCategory.Manschette,          "Manschetten",              "stk"),
        new(SpecialStatsCategory.Linerendmanschette,  "Linerendmanschetten (LEM)", "stk")
    ];

    /// <summary>
    /// Erstellt ein frisches Dictionary mit je einem leeren Eimer pro Kategorie.
    /// </summary>
    public static Dictionary<SpecialStatsCategory, SpecialStatsBucket> CreateSpecialStatsBuckets()
    {
        var dict = new Dictionary<SpecialStatsCategory, SpecialStatsBucket>();
        foreach (var cfg in SpecialStatsConfigs)
            dict[cfg.Category] = new SpecialStatsBucket { DefaultUnit = cfg.DefaultUnit };
        return dict;
    }

    /// <summary>
    /// Versucht, eine Kategorie fuer die uebergebene Kostenzeile zu ermitteln.
    /// Gibt <c>true</c> zurueck, wenn eine Kategorie erkannt wurde.
    /// </summary>
    public static bool TryResolveSpecialStatsCategory(CostLine line, out SpecialStatsCategory category)
    {
        category = SpecialStatsCategory.None;
        if (line is null)
            return false;

        var key      = (line.ItemKey ?? "").Trim();
        var text     = (line.Text    ?? "").Trim();
        var combined = key + " " + text;

        // Reihenfolge ist intentional: LEM vor Manschette, GFK vor Nadelfilz
        if (ContainsToken(combined, "LINERENDMANSCHETTE") ||
            ContainsToken(combined, " ENDMANSCHETTE")     ||
            ContainsToken(combined, " LEM"))
        {
            category = SpecialStatsCategory.Linerendmanschette;
            return true;
        }

        if (ContainsToken(combined, "SCHLAUCHLINER_GFK") ||
            (ContainsToken(combined, "GFK") && ContainsToken(combined, "LINER")) ||
            (ContainsToken(combined, "GFK") && ContainsToken(combined, "SCHLAUCHLINER")))
        {
            category = SpecialStatsCategory.InlinerGfk;
            return true;
        }

        if (ContainsToken(combined, "SCHLAUCHLINER_NADELFILZ") ||
            ContainsToken(combined, "NADELFILZ_LINER")         ||
            (ContainsToken(combined, "NADELFILZ") && ContainsToken(combined, "LINER")) ||
            (ContainsToken(combined, "NADELFILZ") && ContainsToken(combined, "SCHLAUCHLINER")))
        {
            category = SpecialStatsCategory.InlinerNadelfilz;
            return true;
        }

        if (ContainsToken(combined, "MANSCHETTE"))
        {
            category = SpecialStatsCategory.Manschette;
            return true;
        }

        return false;
    }

    /// <summary>Prueft, ob <paramref name="text"/> das angegebene Token enthaelt (Gross-/Kleinschreibung ignoriert).</summary>
    public static bool ContainsToken(string text, string token)
    {
        if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(token))
            return false;
        return text.Contains(token.Trim(), System.StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Normalisiert eine Einheit: Trim + Lowercase. Leer bleibt leer.</summary>
    public static string NormalizeUnit(string? unit)
    {
        var raw = (unit ?? "").Trim();
        if (raw.Length == 0)
            return "";
        return raw.ToLowerInvariant();
    }

    /// <summary>
    /// Bestimmt die Anzeige-Einheit fuer einen Eimer:
    /// kein Eintrag → DefaultUnit, genau eine Einheit → diese, mehrere → "variabel".
    /// </summary>
    public static string ResolveDisplayUnit(SpecialStatsBucket bucket)
    {
        if (bucket.Units.Count == 0)
            return bucket.DefaultUnit;

        if (bucket.Units.Count == 1)
            return bucket.Units.First();

        return "variabel";
    }
}
