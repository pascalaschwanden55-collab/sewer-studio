using System.Globalization;
using AuswertungPro.Next.Application.Lookup;
using AuswertungPro.Next.Application.Xtf.Dss;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.UseCases.Objektakten;

/// <summary>Belegte DSS-Attribute ohne den Leerwertfilter des QGIS-Nachschlagens zuordnen.</summary>
public static class GeoShopAttributZuordnung
{
    public static GeoShopBauteil ErgaenzeBestandsfelder(GeoShopBauteil quelle, BauteilArt art)
    {
        if (quelle.Quellen is not { Count: > 0 } quellen) return quelle;
        var haltung = art == BauteilArt.Haltung;
        var primaer = quellen.SingleOrDefault(q => q.Kennung == (haltung ? quelle.Kennungen.Haltung : quelle.Kennungen.Knoten));
        var bauwerk = quellen.SingleOrDefault(q => q.Kennung == (haltung ? quelle.Kennungen.Kanal : quelle.Kennungen.Bauwerk));
        var felder = new Dictionary<string, string>(quelle.Felder, StringComparer.Ordinal);
        foreach (var f in FieldCatalog.Objektfelder.Felder.Where(f => f.Art == (haltung ? "haltung" : "schacht") && f.Speicherfeld is not null))
        {
            // Haltungsnamen bleiben auch bei umgekehrter Inspektionsrichtung unveraendert.
            if (f.Id == "haltung.name") continue;
            var wert = Lies(f, primaer, bauwerk);
            // XTF-Masse/Kosten nicht durch den auf zwei Stellen gerundeten QGIS-Anzeigewert ersetzen.
            if (f.Speicherfeld is FieldKeys.HoldingLengthMeters or FieldKeys.GrossCost
                or FieldKeys.NominalDiameterMm or FieldKeys.ShaftDimension1Mm or FieldKeys.ShaftDimension2Mm
                && decimal.TryParse(wert, NumberStyles.Float, CultureInfo.InvariantCulture, out var zahl) && zahl >= 0)
            {
                felder[f.Speicherfeld] = zahl.ToString(CultureInfo.InvariantCulture);
                continue;
            }
            if (felder.ContainsKey(f.Speicherfeld!)) continue;
            if (f.Id is "haltung.owner" or "schacht.eigentuemer") wert = Organisation(quellen, wert);
            if (!string.IsNullOrWhiteSpace(wert))
                felder[f.Speicherfeld!] = ObjektaktenBearbeitung.Normalisiere(f, wert);
        }
        void Ergaenze(string feld, string? wert)
        {
            if (!string.IsNullOrWhiteSpace(wert)) felder.TryAdd(feld, wert);
        }
        Ergaenze(FieldKeys.GrossCost, Wert(bauwerk, "Bruttokosten"));
        Ergaenze(FieldKeys.Street, Wert(bauwerk, "Standortname"));
        Ergaenze(FieldKeys.DataOwner, Organisation(quellen, Wert(primaer, "DatenherrRef") ?? Wert(bauwerk, "DatenherrRef")));
        Ergaenze(FieldKeys.DataSupplier, Organisation(quellen, Wert(primaer, "DatenlieferantRef") ?? Wert(bauwerk, "DatenlieferantRef")));
        Ergaenze(FieldKeys.CadastreLastChange, Datum(Wert(primaer, "Letzte_Aenderung") ?? Wert(bauwerk, "Letzte_Aenderung")));
        return quelle with { Felder = felder };
    }

    internal static string? Lies(ObjektFeldDefinition feld, ObjektQuellbeleg? primaer,
        ObjektQuellbeleg? bauwerk, ObjektQuellbeleg? von = null, ObjektQuellbeleg? nach = null)
    {
        if (feld.Id is "schacht.geaendert_am" or "haltung.changed")
            return Datum(Wert(primaer, "Letzte_Aenderung") ?? Wert(bauwerk, "Letzte_Aenderung"));
        if (feld.Id == "haltung.aatype")
        {
            var hierarchie = Wert(bauwerk, "FunktionHierarchisch");
            return hierarchie?.Split('.')[0] is "PAA" or "SAA" && hierarchie.Contains('.')
                ? hierarchie.Split('.')[0] : null;
        }
        if (DssFeldZuordnung.Ziel(feld) is not { } ziel) return null;
        var q = ziel.Klasse switch
        {
            "von" => von, "nach" => nach,
            _ when primaer?.Klasse == ziel.Klasse => primaer,
            _ when bauwerk?.Klasse == ziel.Klasse => bauwerk,
            // Gemeinsame Bauwerksattribute sind auch an den DSS-Unterklassen gueltig.
            "Normschacht" when ziel.Attribut is not ("Funktion" or "Material" or "Dimension1" or "Dimension2") => bauwerk,
            _ => null
        };
        return Wert(q, ziel.Attribut);
    }

    private static string? Organisation(IEnumerable<ObjektQuellbeleg> quellen, string? id)
        => quellen.SingleOrDefault(q => q.Kennung == id && q.Klasse == "Organisation")?.Werte.GetValueOrDefault("Bezeichnung") ?? id;

    private static string? Wert(ObjektQuellbeleg? q, string attribut)
        => q?.Werte.GetValueOrDefault(attribut) ?? q?.Referenzen.GetValueOrDefault(attribut);

    private static string? Datum(string? wert) => DateTime.TryParseExact(wert, "yyyyMMdd", CultureInfo.InvariantCulture,
        DateTimeStyles.None, out var datum) ? datum.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : wert;
}
