using System.Xml.Linq;
using AuswertungPro.Next.Application.Xtf;

namespace AuswertungPro.Next.Infrastructure.Import.Xtf;

internal static class XtfZusatzWriter
{
    internal static void ErgaenzeDokument(XDocument doc, XtfNeuPlan plan)
    {
        var zusatz = plan.Objekte.Where(o => o.ImTopicZusatz).ToArray();
        if (zusatz.Length == 0) return;
        var ids = plan.Objekte.Where(o => !o.ImTopicZusatz).Select(o => o.Tid).ToHashSet(StringComparer.Ordinal);
        XNamespace ns = "http://www.interlis.ch/INTERLIS2.3";
        var basket = new XElement(ns + XtfZusatzangaben.Topic, new XAttribute("BID", "chB0000000000003"));
        var paare = new HashSet<(string, string)>();
        foreach (var objekt in zusatz)
        {
            var felder = objekt.Felder.ToDictionary(f => f.Key, f => f.Value, StringComparer.Ordinal);
            if (objekt.Klasse == "Aenderung")
            {
                if (felder.Count != 3 || !felder.TryGetValue("ObjektTid", out var ziel) || !ids.Contains(ziel)
                    || !felder.TryGetValue("Feld", out var zielFeld) || string.IsNullOrWhiteSpace(zielFeld)
                    || !felder.TryGetValue("GeaendertAm", out var zeit)
                    || !DateTimeOffset.TryParseExact(zeit, "O", System.Globalization.CultureInfo.InvariantCulture,
                        System.Globalization.DateTimeStyles.None, out _))
                    throw new InvalidDataException("Ein Aenderungsauftrag hat kein gueltiges Ziel oder Aenderungsdatum.");
                basket.Add(new XElement(ns + (XtfZusatzangaben.Topic + ".Aenderung"), new XAttribute("TID", objekt.Tid),
                    new XElement(ns + "ObjektTid", ziel), new XElement(ns + "Feld", zielFeld), new XElement(ns + "GeaendertAm", zeit)));
                continue;
            }
            if (objekt.Klasse != "Zusatzangabe" || felder.Count != 3
                || !felder.TryGetValue("ObjektTid", out var tid) || !ids.Contains(tid)
                || !felder.TryGetValue("Feld", out var feld) || !XtfZusatzangaben.IstErlaubt(feld)
                || !felder.TryGetValue("Wert", out var wert) || string.IsNullOrWhiteSpace(wert)
                || !paare.Add((tid, feld)))
                throw new InvalidDataException("Eine Zusatzangabe hat kein eindeutiges Ziel oder ein unzulaessiges Feld.");
            basket.Add(new XElement(ns + XtfZusatzangaben.Klasse, new XAttribute("TID", objekt.Tid),
                new XElement(ns + "ObjektTid", tid), new XElement(ns + "Feld", feld), new XElement(ns + "Wert", wert)));
        }
        doc.Root!.Element(ns + "HEADERSECTION")!.Element(ns + "MODELS")!.Add(
            new XElement(ns + "MODEL", new XAttribute("NAME", XtfZusatzangaben.Modell),
                new XAttribute("URI", "mailto:Pascal.Aschwanden@abwasser-uri.ch"), new XAttribute("VERSION", "2026-09-07")));
        doc.Root.Element(ns + "DATASECTION")!.Add(basket);
    }

    internal static void SchreibeModell(string ordner)
    {
        var dateiname = XtfZusatzangaben.Modell + ".ili";
        using var quelle = typeof(XtfZusatzWriter).Assembly.GetManifestResourceStream(
            "AuswertungPro.Next.Infrastructure.Import.Xtf.Models." + dateiname)
            ?? throw new IOException("Das Zusatzmodell fehlt im Programm.");
        using var speicher = new MemoryStream();
        quelle.CopyTo(speicher);
        var bytes = speicher.ToArray();
        var ziel = Path.Combine(ordner, dateiname);
        if (File.Exists(ziel))
        {
            if (!File.ReadAllBytes(ziel).AsSpan().SequenceEqual(bytes))
                throw new IOException("Im Zielordner liegt bereits ein anderes Zusatzmodell. Bitte einen neuen Exportordner verwenden.");
            return;
        }
        var temp = ziel + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var datei = new FileStream(temp, FileMode.CreateNew, FileAccess.Write)) datei.Write(bytes);
            File.Move(temp, ziel);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
}
