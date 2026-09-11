using System.Xml;
using System.Xml.Linq;
using System.Globalization;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Application.Lookup;

namespace AuswertungPro.Next.Application.Xtf.Dss;

internal static class DssExportPruefung
{
    internal static readonly IReadOnlyDictionary<string, string[]> Associationen = new Dictionary<string, string[]>
    {
        ["Erhaltungsereignis_AbwasserbauwerkAssoc"] = ["AbwasserbauwerkRef", "Erhaltungsereignis_AbwasserbauwerkAssocRef"],
        ["Erhaltungsereignis_Ausfuehrende_FirmaAssoc"] = ["Ausfuehrende_FirmaRef", "Erhaltungsereignis_Ausfuehrende_FirmaAssocRef"]
    };
    internal static readonly string[] Bauwerke = ["Kanal", "Normschacht", "Spezialbauwerk", "Versickerungsanlage", "Einleitstelle"];
    private static readonly Dictionary<string, string[]> Ziele = new()
    {
        ["DatenherrRef"] = ["Organisation"], ["DatenlieferantRef"] = ["Organisation"],
        ["EigentuemerRef"] = ["Organisation"], ["BetreiberRef"] = ["Organisation"], ["Ausfuehrende_FirmaRef"] = ["Organisation"],
        ["AbwasserbauwerkRef"] = Bauwerke, ["vonHaltungspunktRef"] = ["Haltungspunkt"], ["nachHaltungspunktRef"] = ["Haltungspunkt"],
        ["RohrprofilRef"] = ["Rohrprofil"], ["AbwassernetzelementRef"] = ["Haltung", "Abwasserknoten"],
        ["Hydr_GeometrieRef"] = ["Hydr_Geometrie"],
        ["Erhaltungsereignis_AbwasserbauwerkAssocRef"] = ["Unterhalt"], ["Erhaltungsereignis_Ausfuehrende_FirmaAssocRef"] = ["Unterhalt"]
    };
    public static void Pruefe(Dictionary<string, DssExportObjekt> objekte, List<string> hinweise)
    {
        var externe = new HashSet<string>();
        var unique = new HashSet<string>(StringComparer.Ordinal);
        foreach (var o in objekte.Values)
        {
            if (!o.OhneTid && !SiaObjektkennung.IstGueltig(o.Tid)) Fehler(o, "ungültige Originalkennung");
            var assoc = Associationen.ContainsKey(o.Klasse);
            if (o.OhneTid && !assoc) Fehler(o, "lokale Kennung darf nicht als Objekt-ID hinausgehen");
            var schema = DssExportSchema.Felder(o.Klasse);
            if (schema is not null)
            {
                foreach (var (k, v) in o.Werte.ToArray())
                {
                    var wert = DssExportSchema.Normalisiere(o.Klasse, k, v);
                    if (wert is null) o.Werte.Remove(k); else o.Werte[k] = wert;
                }
                foreach (var f in schema.Where(f => f.Value.Required))
                    if (!o.Werte.ContainsKey(f.Key) && !o.Strukturen.ContainsKey(f.Key)) Fehler(o, $"Pflichtfeld {f.Key} fehlt");
                foreach (var (k, v) in o.Strukturen)
                {
                    if (schema.GetValueOrDefault(k)?.Kind != "Structure") Fehler(o, $"unbekannte Struktur {k}");
                    PruefeStruktur(o, k, v);
                }
                if (o.Geometrie is { } g && (schema.GetValueOrDefault(g.Feldname)?.Kind != "Structure"
                    || g.Punkte.Count == 0 || g.Feldname == "Verlauf" && g.Punkte.Count < 2
                    || g.Feldname == "Lage" && g.Punkte.Count != 1
                    || g.Punkte.Any(p => !double.IsFinite(p.Ost) || !double.IsFinite(p.Nord) || p.Ost < 2480000 || p.Ost > 2840000 || p.Nord < 1070000 || p.Nord > 1300000)))
                    Fehler(o, "ungültige LV95-Geometrie");
            }
            else if (!assoc || o.Werte.Count > 0 || o.Strukturen.Count > 0) Fehler(o, "unbekannte Klasse oder Assoziationsfelder");

            foreach (var pflicht in PflichtRefs(o.Klasse)) if (!o.Refs.ContainsKey(pflicht)) Fehler(o, $"Pflichtverweis {pflicht} fehlt");
            foreach (var (rolle, tid) in o.Refs)
            {
                if (!ErlaubteRefs(o.Klasse).Contains(rolle) || !Ziele.TryGetValue(rolle, out var klassen)) Fehler(o, $"nicht zugeordnete Beziehung {rolle}");
                if (!SiaObjektkennung.IstGueltig(tid)) Fehler(o, $"ungültiger Verweis {rolle}: {tid}");
                if (objekte.TryGetValue(tid, out var ziel))
                {
                    if (!Ziele[rolle].Contains(ziel.Klasse)) Fehler(o, $"{rolle} zeigt auf {ziel.Klasse}");
                }
                else if (Ziele[rolle].SequenceEqual(new[] { "Organisation" })) externe.Add(tid);
                else Fehler(o, $"Bezugsobjekt {rolle} ({tid}) fehlt. GeoShop-Verbund erneut ergänzen");
            }
            if (!assoc)
            {
                var gruppe = Bauwerke.Contains(o.Klasse) ? "Abwasserbauwerk" : o.Klasse is "Haltung" or "Abwasserknoten" ? "Abwassernetzelement" : o.Klasse is "Deckel" or "Einstiegshilfe" ? "BauwerksTeil" : o.Klasse;
                var key = gruppe + "|" + o.Werte.GetValueOrDefault("Bezeichnung") + "|" + (o.Klasse == "Unterhalt" ? o.Werte.GetValueOrDefault("Zeitpunkt") : o.Refs.GetValueOrDefault("DatenherrRef"));
                if (o.Klasse != "Organisation" && !unique.Add(key)) Fehler(o, "Bezeichnung ist beim gleichen Datenherrn nicht eindeutig");
            }
        }
        if (externe.Count > 0) hinweise.Add($"{externe.Count} externe Organisationsverweise bleiben mit Original-TID erhalten: {string.Join(", ", externe.Order())}. Die Organisationsstammdaten fehlen in GeoShop und müssen im Zielkataster vorhanden sein; Namen und Rollen werden nicht erfunden.");
    }
    private static IEnumerable<string> PflichtRefs(string klasse)
    {
        if (Associationen.TryGetValue(klasse, out var rollen)) return rollen;
        if (klasse == "Organisation") return [];
        return new[] { "DatenherrRef", "DatenlieferantRef" }.Concat(klasse switch
        {
            "Haltung" => ["vonHaltungspunktRef", "nachHaltungspunktRef"],
            "Deckel" or "Einstiegshilfe" => ["AbwasserbauwerkRef"],
            _ when Bauwerke.Contains(klasse) => ["EigentuemerRef"],
            _ => Array.Empty<string>()
        });
    }
    private static IEnumerable<string> ErlaubteRefs(string klasse) => PflichtRefs(klasse).Concat(klasse switch
    {
        "Haltung" => ["AbwasserbauwerkRef", "RohrprofilRef"], "Abwasserknoten" => ["AbwasserbauwerkRef", "Hydr_GeometrieRef"],
        "Haltungspunkt" => ["AbwassernetzelementRef"],
        "Unterhalt" => ["Ausfuehrende_FirmaRef"],
        _ when Bauwerke.Contains(klasse) => ["BetreiberRef"], _ => Array.Empty<string>()
    });
    private static void PruefeStruktur(DssExportObjekt o, string feld, string xml)
    {
        using var reader = XmlReader.Create(new StringReader(xml), new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 4_000_000 });
        var e = XElement.Load(reader);
        if (e.Name.LocalName != feld || e.Name.NamespaceName != "http://www.interlis.ch/INTERLIS2.3"
            || e.DescendantsAndSelf().Any(x => x.Name.Namespace != e.Name.Namespace || x.Attributes().Any(a => a.Name.LocalName is "REF" or "TID"))) Fehler(o, $"ungültige Geometriestruktur {feld}");
        var child = e.Elements().ToArray();
        var erwartet = feld == "Lage" ? "COORD" : feld == "Verlauf" ? "POLYLINE" : "SURFACE";
        if (child.Length != 1 || child[0].Name.LocalName != erwartet) Fehler(o, $"falscher Geometrietyp für {feld}");
        foreach (var punkt in e.Descendants().Where(x => x.Name.LocalName is "COORD" or "ARC"))
        {
            foreach (var (k, min, max) in new[] { ("C1", 2480000m, 2840000m), ("C2", 1070000m, 1300000m) }
                .Concat(punkt.Name.LocalName == "ARC" ? [("A1", 2480000m, 2840000m), ("A2", 1070000m, 1300000m)] : Array.Empty<(string, decimal, decimal)>()))
            {
                var values = punkt.Elements(e.Name.Namespace + k).ToArray();
                if (values.Length != 1 || !decimal.TryParse(values[0].Value, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var n)
                    || n < min || n > max || decimal.Round(n, 3) != n) Fehler(o, $"ungültige LV95-Koordinate {feld}.{k}");
            }
        }
    }
    private static void Fehler(DssExportObjekt o, string text) => throw new InvalidOperationException($"DSS: {o.Klasse} {o.Werte.GetValueOrDefault("Bezeichnung", o.Tid)}: {text}.");
}
