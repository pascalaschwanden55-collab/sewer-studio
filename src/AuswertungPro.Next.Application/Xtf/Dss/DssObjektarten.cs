using AuswertungPro.Next.Application.UseCases;
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

    /// <summary>
    /// Eigene Eingaben an einer Akte: Handwerte oder Unterlisten. Aus GeoShop uebernommene
    /// Anzeigen ohne Handmarke zaehlen nicht. Ein Haltungspunkt einer fremden Leitung am
    /// Projektknoten wird nur mit eigenen Eingaben geliefert (Buerglen 14.09.2026: zwei
    /// Quellpunkte «A76157», der fremde sperrte die ganze Lieferung). Ein im GeoShop-Vergleich
    /// bewusst behaltener Wert, der von der aktuellen Quelle abweicht, ist ebenfalls eine eigene Eingabe.
    /// </summary>
    internal static bool HatEigeneEingaben(Project projekt, ObjektAkte akte)
        => akte.Unterlisten.Count > 0 || akte.Werte.Any(w => w.Value.VonHand
            || GeoShopImportVergleich.BehaeltAktenwert(projekt, akte, w.Key, w.Value.Text));

    internal static string Bezeichnung(ObjektAkte akte)
    {
        var feld = akte.Art == "sanierung" ? "sanierung.s_name" : akte.Art + ".bezeichnung";
        var name = akte.Werte.GetValueOrDefault(feld)?.Text;
        // Eine Akte traegt den ganzen GeoShop-Verbund als Quellen. Nur die Quelle der
        // eigenen Klasse traegt den eigenen Namen; sonst hiess ein Buerglen-Schacht nach
        // der ersten Haltung im Verbund und der Hinweis fuehrte in die Irre.
        if (string.IsNullOrWhiteSpace(name))
            name = akte.Quellen.Where(q => q.Klasse == Klasse(akte.Art) || DssEinbautenZuordnung.Art(q.Klasse) == akte.Art)
                .Select(q => q.Werte.GetValueOrDefault("Bezeichnung")).FirstOrDefault(n => !string.IsNullOrWhiteSpace(n));
        return $"{akte.Art} «{(string.IsNullOrWhiteSpace(name) ? akte.Id.ToString() : name)}»";
    }
}
