using System.Text.Json.Nodes;

namespace AuswertungPro.Next.Domain.Models;

/// <summary>Ein Protokollimport darf bestaetigte Kataster-Stammdaten nicht unbemerkt zuruecksetzen.</summary>
public static class KatasterFeldschutz
{
    public static bool Pruefe(FieldMetadata? meta, string bisher, string? neu, FieldSource quelle, bool hand)
    {
        if (meta?.Source != FieldSource.Kataster || hand || quelle is FieldSource.Kataster or FieldSource.Grundbuch) return false;
        if (bisher != (neu ?? ""))
            meta.Conflict = new JsonObject { ["Reason"] = "KatasterwertGeschuetzt", ["ExistingValue"] = bisher,
                ["IncomingValue"] = neu ?? "", ["IncomingSource"] = quelle.ToString() };
        return true; // Auch bei gleichem Text die staerkere Herkunft erhalten.
    }
}
