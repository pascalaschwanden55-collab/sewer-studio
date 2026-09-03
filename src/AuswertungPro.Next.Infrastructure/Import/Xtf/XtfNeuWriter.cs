using System.Xml.Linq;
using AuswertungPro.Next.Application.Xtf;

namespace AuswertungPro.Next.Infrastructure.Import.Xtf;

/// <summary>Ergebnis eines Schreibversuchs.</summary>
public sealed record XtfNeuErgebnis(bool Ok, string? Fehler, string? Datei);

/// <summary>
/// Schreibt eine NEUE SIA405-XTF aus einem fertigen Plan.
///
/// Der Schreiber entscheidet nichts: Welche Objekte entstehen, welche Werte sie tragen
/// und wie sie zusammenhaengen, hat <see cref="XtfNeuPlanBuilder"/> bereits festgelegt.
/// Hier wird nur noch XML daraus.
///
/// Aufbau und Schreibweise folgen dem echten Kantonsexport von Abwasser Uri: zwei Modelle
/// im Kopf, zwei Behaelter in der Datensektion (Fachdaten und Administration), Koordinaten
/// als <c>COORD</c> mit <c>C1</c>/<c>C2</c> in LV95.
///
/// Ein vorhandenes Ziel wird nie ueberschrieben.
/// </summary>
public static class XtfNeuWriter
{
    private const string Ns = "http://www.interlis.ch/INTERLIS2.3";
    private const string Fach = "SIA405_ABWASSER_2020_LV95.SIA405_Abwasser";
    private const string Verwaltung = "SIA405_Base_Abwasser_LV95.Administration";

    /// <summary>
    /// Feldreihenfolgen je Klasse — erst die geerbten Felder der Oberklassen, dann die
    /// eigenen, jeweils wie im Modell. INTERLIS gibt sie vor.
    ///
    /// Bewusst eine eigene Liste neben der des <see cref="XtfRevisionWriter"/>: Dieser
    /// Weg erzeugt andere Klassen, und der geprueefte Revisionsweg soll dafuer nicht
    /// angefasst werden. Wer hier etwas aendert, aendert dort nichts — und umgekehrt.
    /// </summary>
    private static readonly Dictionary<string, string[]> Reihenfolgen = new(StringComparer.Ordinal)
    {
        ["Kanal"] =
        [
            "Letzte_Aenderung", "Baujahr", "BaulicherZustand", "Bemerkung", "Bezeichnung",
            "Bruttokosten", "Sanierungsbedarf", "Status",
            "Bettung_Umhuellung", "FunktionHierarchisch", "FunktionHydraulisch",
            "Nutzungsart_Ist", "Verbindungsart"
        ],
        ["Haltung"] =
        [
            "Letzte_Aenderung", "Bemerkung", "Bezeichnung",
            "LaengeEffektiv", "Lichte_Hoehe", "Material", "Lagebestimmung", "Verlauf"
        ],
        ["Haltungspunkt"] = ["Letzte_Aenderung", "Bezeichnung", "Lage"],
        ["Abwasserknoten"] = ["Letzte_Aenderung", "Bemerkung", "Bezeichnung", "Lage"],
        ["Normschacht"] =
        [
            "Letzte_Aenderung", "Baujahr", "BaulicherZustand", "Bemerkung", "Bezeichnung",
            "Sanierungsbedarf", "Status",
            "Dimension1", "Dimension2", "Funktion", "Material"
        ],
        ["Rohrprofil"] = ["Letzte_Aenderung", "Bemerkung", "Bezeichnung", "HoehenBreitenverhaeltnis", "Profiltyp"],
        ["Organisation"] =
        [
            "Letzte_Aenderung", "Bemerkung", "Bezeichnung", "Gemeindenummer",
            "Kurzbezeichnung", "Organisationstyp", "Status", "UID"
        ]
    };

    public static XtfNeuErgebnis Schreibe(XtfNeuPlan plan, string ziel, DateOnly? stand = null)
    {
        ArgumentNullException.ThrowIfNull(plan);

        if (string.IsNullOrWhiteSpace(ziel))
            return new XtfNeuErgebnis(false, "Es wurde kein Zielpfad angegeben.", null);

        if (plan.Leer)
            return new XtfNeuErgebnis(false, "Das Projekt enthaelt nichts zum Exportieren.", null);

        if (File.Exists(ziel))
            return new XtfNeuErgebnis(false, $"Die Datei \"{ziel}\" gibt es bereits.", null);

        try
        {
            var doc = BaueDokument(plan, stand ?? DateOnly.FromDateTime(DateTime.Now));
            var ordner = Path.GetDirectoryName(ziel);
            if (!string.IsNullOrEmpty(ordner))
                Directory.CreateDirectory(ordner);

            // Ueber eine Nebendatei veroeffentlichen: Ein abgebrochener Lauf hinterlaesst
            // keine halbe XTF, die jemand fuer vollstaendig halten koennte.
            var temp = ziel + ".tmp";
            doc.Save(temp);
            File.Move(temp, ziel);

            return new XtfNeuErgebnis(true, null, ziel);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException
                                      or System.Xml.XmlException)
        {
            return new XtfNeuErgebnis(false, ex.Message, null);
        }
    }

    private static XDocument BaueDokument(XtfNeuPlan plan, DateOnly stand)
    {
        XNamespace ns = Ns;
        var datum = stand.ToString("yyyyMMdd");

        var fachdaten = new XElement(ns + Fach, new XAttribute("BID", "chB0000000000001"));
        var verwaltung = new XElement(ns + Verwaltung, new XAttribute("BID", "chB0000000000002"));

        foreach (var objekt in plan.Objekte)
        {
            var ziel = objekt.ImTopicAdministration ? verwaltung : fachdaten;
            var praefix = objekt.ImTopicAdministration ? Verwaltung : Fach;
            ziel.Add(BaueObjekt(ns, praefix, objekt, datum));
        }

        var datensektion = new XElement(ns + "DATASECTION", fachdaten);
        if (verwaltung.HasElements)
            datensektion.Add(verwaltung);

        return new XDocument(
            new XDeclaration("1.0", "utf-8", null),
            new XElement(ns + "TRANSFER",
                new XElement(ns + "HEADERSECTION",
                    new XAttribute("SENDER", "SewerStudio"),
                    new XAttribute("VERSION", "2.3"),
                    new XElement(ns + "MODELS",
                        new XElement(ns + "MODEL",
                            new XAttribute("NAME", "SIA405_ABWASSER_2020_LV95"),
                            new XAttribute("URI", "http://www.sia.ch/405"),
                            new XAttribute("VERSION", "26.06.2021")),
                        new XElement(ns + "MODEL",
                            new XAttribute("NAME", "SIA405_Base_Abwasser_LV95"),
                            new XAttribute("URI", "http://www.vsa.ch/models"),
                            new XAttribute("VERSION", "03.11.2020"))),
                    new XElement(ns + "ALIAS",
                        new XElement(ns + "ENTRIES",
                            new XAttribute("FOR", "SIA405_ABWASSER_2020_LV95"))),
                    new XElement(ns + "COMMENT",
                        $"Vollstaendiger Neu-Export aus SewerStudio: {plan.Haltungen} Haltungen, " +
                        $"{plan.Schaechte} Schaechte.")),
                datensektion));
    }

    private static XElement BaueObjekt(XNamespace ns, string praefix, XtfNeuObjekt objekt, string datum)
    {
        var element = new XElement(ns + $"{praefix}.{objekt.Klasse}",
            new XAttribute("TID", objekt.Tid));

        var felder = new List<KeyValuePair<string, string>>
        {
            new("Letzte_Aenderung", datum)
        };
        felder.AddRange(objekt.Felder);

        foreach (var (name, wert) in Sortiere(objekt.Klasse, felder))
            element.Add(new XElement(ns + name, wert));

        if (objekt.Geometrie is not null)
            element.Add(BaueGeometrie(ns, objekt.Geometrie));

        foreach (var verweis in objekt.Verweise)
            element.Add(new XElement(ns + verweis.Name, new XAttribute("REF", verweis.ZielTid)));

        return element;
    }

    /// <summary>
    /// Die Sachfelder in Modellreihenfolge. Ein Feld ohne bekannten Platz landet hinten,
    /// aber vor Geometrie und Verweisen — dort stoert es die Reihenfolge am wenigsten.
    /// </summary>
    private static IEnumerable<KeyValuePair<string, string>> Sortiere(
        string klasse, List<KeyValuePair<string, string>> felder)
    {
        if (!Reihenfolgen.TryGetValue(klasse, out var reihenfolge))
            return felder;

        return felder
            .Select(f => (Feld: f, Platz: Array.IndexOf(reihenfolge, f.Key)))
            .OrderBy(x => x.Platz < 0 ? int.MaxValue : x.Platz)
            .Select(x => x.Feld);
    }

    private static XElement BaueGeometrie(XNamespace ns, XtfNeuGeometrie geometrie)
    {
        var huelle = new XElement(ns + geometrie.Feldname);

        if (!geometrie.IstLinie)
        {
            huelle.Add(BaueKoordinate(ns, geometrie.Punkte[0]));
            return huelle;
        }

        var linie = new XElement(ns + "POLYLINE");
        foreach (var punkt in geometrie.Punkte)
            linie.Add(BaueKoordinate(ns, punkt));

        huelle.Add(linie);
        return huelle;
    }

    private static XElement BaueKoordinate(XNamespace ns, XtfPunkt punkt)
        => new(ns + "COORD",
            new XElement(ns + "C1", punkt.OstText),
            new XElement(ns + "C2", punkt.NordText));
}
