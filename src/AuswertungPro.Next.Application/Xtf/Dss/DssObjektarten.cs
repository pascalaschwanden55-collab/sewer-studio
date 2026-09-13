using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.Xtf.Dss;

/// <summary>Aktenarten, deren aktuelle Eingaben der DSS-Schreibweg wirklich verarbeitet.</summary>
internal static class DssObjektarten
{
    internal static string? Klasse(string art) => art switch
    {
        "haltung" => "Haltung", "schacht" => "Abwasserknoten", "deckel" => "Deckel",
        "sanierung" or "unterhalt" => "Unterhalt", "haltungspunkt" => "Haltungspunkt",
        "pumpe" => "FoerderAggregat", "absperr_drossel" => "Absperr_Drosselorgan",
        "ueberlauf" => "Ueberlauf", "bauwerksteil" => "BauwerksTeil", _ => null
    };

    internal static string Bezeichnung(ObjektAkte akte)
    {
        var feld = akte.Art == "sanierung" ? "sanierung.s_name" : akte.Art + ".bezeichnung";
        var name = akte.Werte.GetValueOrDefault(feld)?.Text;
        if (string.IsNullOrWhiteSpace(name)) name = akte.Quellen.Select(q => q.Werte.GetValueOrDefault("Bezeichnung")).FirstOrDefault(n => !string.IsNullOrWhiteSpace(n));
        return $"{akte.Art} «{(string.IsNullOrWhiteSpace(name) ? akte.Id.ToString() : name)}»";
    }
}
