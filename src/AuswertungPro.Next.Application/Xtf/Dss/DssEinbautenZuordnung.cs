using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.Xtf.Dss;

/// <summary>Belegte Sachfelder der Einbauten; die Originalklasse entscheidet bei gemeinsamen Masken.</summary>
public static class DssEinbautenZuordnung
{
    public static string? Art(string klasse) => klasse switch
    {
        "FoerderAggregat" => "pumpe", "Absperr_Drosselorgan" => "absperr_drossel",
        "Leapingwehr" or "Streichwehr" => "ueberlauf",
        "Einstiegshilfe" or "Trockenwetterfallrohr" => "bauwerksteil", _ => null
    };

    public static string? Elternrolle(string klasse) => klasse switch
    {
        "Einstiegshilfe" or "Trockenwetterfallrohr" => "AbwasserbauwerkRef",
        "FoerderAggregat" or "Absperr_Drosselorgan" or "Leapingwehr" or "Streichwehr" => "AbwasserknotenRef", _ => null
    };

    internal static bool IstAkte(string art) => art is "pumpe" or "absperr_drossel" or "ueberlauf" or "bauwerksteil";

    private static readonly Dictionary<string, string> Sachfelder = new()
    {
        ["bezeichnung"] = "Bezeichnung", ["bemerkung"] = "Bemerkung", ["fabrikat"] = "Fabrikat",
        ["steuerung"] = "Steuerung", ["verstellbarkeit"] = "Verstellbarkeit", ["signaluebermittlung"] = "Signaluebermittlung",
        ["antrieb"] = "Antrieb", ["funktion"] = "Funktion", ["einleitstelle"] = "Einleitstelle",
        ["qan_dim_l_s"] = "Qan_dim", ["aufstellung"] = "AufstellungAntrieb", ["aufstellung_foerderaggregat"] = "AufstellungFoerderaggregat",
        ["kote_start"] = "KoteStart", ["kote_stop"] = "KoteStop",
        ["foerderstrom_min_einzel_l_s"] = "FoerderstromMin_einzel", ["foerderstrom_max_einzel_l_s"] = "FoerderstromMax_einzel",
        ["oeffnung_ist_mm"] = "Drosselorgan_Oeffnung_Ist", ["querschnitt_m²"] = "Querschnitt",
        ["oeffnung_ist_optimiert_mm"] = "Drosselorgan_Oeffnung_Ist_optimiert", ["wirksamer_querschnitt_m²"] = "Wirksamer_QS",
        ["oeffnungsform"] = "Oeffnungsform", ["breite"] = "Breite", ["laenge_m"] = "Laenge",
        ["ueberfallkante"] = "Ueberfallkante", ["kote_min"] = "KoteMin", ["kote_max"] = "KoteMax",
        ["ueberfalllaenge_m"] = "HydrUeberfalllaenge", ["durchmesser"] = "Durchmesser", ["instandstellung"] = "Instandstellung"
    };

    internal static (string Klasse, string Attribut)? Ziel(ObjektFeldDefinition feld, string klasse)
    {
        // Schreibgeschützte Angaben dieser Masken stammen vom zugehörigen Schacht.
        // Beispielsweise ist dessen Bemerkung nicht die Bemerkung des Pumpenobjekts.
        if (Art(klasse) != feld.Art || feld.NurLesen) return null;
        var name = feld.Id[(feld.Art.Length + 1)..];
        var attribut = name switch
        {
            "bauwerksart" when klasse == "FoerderAggregat" => "Bauart",
            "bauwerksart" when klasse == "Absperr_Drosselorgan" => "Art",
            "bauwerksart" when klasse == "Streichwehr" => "Wehr_Art",
            "subart" when klasse == "Einstiegshilfe" => "Art",
            "knoten" when klasse is "FoerderAggregat" or "Absperr_Drosselorgan" => "AbwasserknotenRef",
            "knoten_von" when klasse is "Leapingwehr" or "Streichwehr" => "AbwasserknotenRef",
            "knoten_bis" when klasse is "Leapingwehr" or "Streichwehr" => "UeberlaufNachRef",
            _ => Sachfelder.GetValueOrDefault(name)
        };
        return attribut is not null && (attribut.EndsWith("Ref", StringComparison.Ordinal)
            || DssExportSchema.Felder(klasse)?.ContainsKey(attribut) == true) ? (klasse, attribut) : null;
    }

    internal static string? Klassenanzeige(string klasse) => klasse switch
    {
        "Leapingwehr" => "Leapingwehr", "Einstiegshilfe" => "Einstiegshilfe",
        "Trockenwetterfallrohr" => "Trockenwetterfallrohr", _ => null
    };

    internal static void PruefeKlasse(ObjektAkte akte, string klasse)
    {
        if (Art(klasse) != akte.Art) throw new InvalidOperationException($"DSS: {akte}: Original-Objektklasse {klasse} passt nicht zur Maske.");
        var feld = akte.Art == "bauwerksteil" ? "bauwerksteil.art" : "ueberlauf.bauwerksart";
        if (!akte.Werte.TryGetValue(feld, out var wert)) return;
        if (klasse == "Streichwehr")
        {
            if (wert.Text is "Unbekannt" or "" or "Streichwehr, hochgezogen" or "Streichwehr, niedrig") return;
        }
        else if (Klassenanzeige(klasse) is not { } erwartet || wert.Text == erwartet) return;
        throw new InvalidOperationException($"DSS: {akte}: Auswahl „{wert.Text}“ würde die Original-Objektklasse {klasse} ändern. Originalkennung und Beziehungen zuerst fachlich klären.");
    }
}
