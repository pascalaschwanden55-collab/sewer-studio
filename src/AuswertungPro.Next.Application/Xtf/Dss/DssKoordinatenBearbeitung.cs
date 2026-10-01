using System.Globalization;
using System.Xml;
using System.Xml.Linq;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Application.UseCases;

namespace AuswertungPro.Next.Application.Xtf.Dss;

internal static class DssKoordinatenBearbeitung
{
    public static void Uebernehme(Project projekt, ObjektAkte akte, DssExportObjekt objekt)
    {
        if (akte.Art is not ("schacht" or "deckel" or "haltungspunkt")) return;
        akte.Werte.TryGetValue(akte.Art + ".rechtswert", out var ost);
        akte.Werte.TryGetValue(akte.Art + ".hochwert", out var nord);
        var ostBehalten = ost is not null && (ost.VonHand || GeoShopImportVergleich.BehaeltAktenwert(projekt, akte, akte.Art + ".rechtswert", ost.Text));
        var nordBehalten = nord is not null && (nord.VonHand || GeoShopImportVergleich.BehaeltAktenwert(projekt, akte, akte.Art + ".hochwert", nord.Text));
        if (!ostBehalten && !nordBehalten) return;
        XNamespace ns = "http://www.interlis.ch/INTERLIS2.3";
        XElement? lage = null;
        if (objekt.Strukturen.TryGetValue("Lage", out var xml))
        {
            using var reader = XmlReader.Create(new StringReader(xml), new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 4_000_000 });
            lage = XElement.Load(reader);
        }
        var c1 = ostBehalten ? ost!.Text : lage?.Element(ns + "COORD")?.Element(ns + "C1")?.Value;
        var c2 = nordBehalten ? nord!.Text : lage?.Element(ns + "COORD")?.Element(ns + "C2")?.Value;
        if (ostBehalten && nordBehalten && c1 == "" && c2 == "")
        { objekt.Strukturen.Remove("Lage"); objekt.Geometrie = null; objekt.Werte["Letzte_Aenderung"] = DateTime.Today.ToString("yyyyMMdd"); return; }
        string Zahl(string? s, decimal min, decimal max)
        {
            if (!decimal.TryParse(s?.Trim().Replace(',', '.'), NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var n)
                || n < min || n > max || decimal.Round(n, 3) != n)
                throw new InvalidOperationException($"DSS: {objekt.Klasse}: vollständige LV95-Koordinaten mit höchstens drei Nachkommastellen erforderlich.");
            return n.ToString("0.000", CultureInfo.InvariantCulture);
        }
        var coord = new XElement(ns + "COORD", new XElement(ns + "C1", Zahl(c1, 2480000, 2840000)), new XElement(ns + "C2", Zahl(c2, 1070000, 1300000)));
        objekt.Strukturen["Lage"] = new XElement(ns + "Lage", coord).ToString(SaveOptions.DisableFormatting);
        objekt.Geometrie = null;
        objekt.Werte["Letzte_Aenderung"] = DateTime.Today.ToString("yyyyMMdd");
    }
}
