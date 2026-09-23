using System.Text.Json.Nodes;

namespace AuswertungPro.Next.Domain.Models;

/// <summary>
/// Schutz eines Katasterwerts (Herkunft <see cref="FieldSource.Kataster"/>: aus GeoShop, QGIS oder dem WebGIS).
///
/// Entscheid Pascal 23.09.2026 abends (ersetzt die Regel vom 14.09.2026): Die Daten der Kanalfirma sind
/// der Ist-Zustand. Ein Import ersetzt deshalb einen Katasterwert — gleich ueber welchen Weg er schreibt
/// (auch der einfache Schreibweg am Schacht, den z.B. die KINS-Anreicherung nimmt). Eine Handkorrektur
/// schuetzt der Datensatz selbst (UserEdited) und bleibt immer stehen. Nur ein Wert unbekannter Herkunft
/// ersetzt keinen Katasterwert; das wird als Konflikt vermerkt.
///
/// Ausnahme LAGE (Koordinaten): Die Katasterkoordinate ist vermessen, die des Protokolls meist Handy-GPS.
/// Ein Import fuellt eine leere Lage, ersetzt aber keine vermessene — nur ein neuerer Katasterstand
/// (Kataster, Grundbuch) oder eine Handkorrektur (Regel seit 19.09.2026, beibehalten 23.09.2026).
/// </summary>
public static class KatasterFeldschutz
{
    private static readonly string[] Lagefelder = { "Koordinate_East", "Koordinate_North" };

    public static bool Pruefe(string feld, FieldMetadata? meta, string bisher, string? neu, FieldSource quelle, bool hand)
    {
        if (meta?.Source != FieldSource.Kataster || hand) return false;
        var geschuetzt = quelle == FieldSource.Unknown
            || (IstLage(feld) && quelle is not (FieldSource.Kataster or FieldSource.Grundbuch));
        if (!geschuetzt) return false;
        if (bisher != (neu ?? ""))
            meta.Conflict = new JsonObject { ["Reason"] = "KatasterwertGeschuetzt", ["ExistingValue"] = bisher,
                ["IncomingValue"] = neu ?? "", ["IncomingSource"] = quelle.ToString() };
        return true; // Auch bei gleichem Text die bekannte Herkunft erhalten.
    }

    private static bool IstLage(string feld)
        => System.Array.Exists(Lagefelder, f => string.Equals(f, feld, System.StringComparison.OrdinalIgnoreCase));
}
