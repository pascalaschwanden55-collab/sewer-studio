using System.Xml;
using System.Xml.Linq;
using AuswertungPro.Next.Application.Xtf;
using AuswertungPro.Next.Application.Xtf.Dss;

namespace AuswertungPro.Next.Infrastructure.Import.Xtf;

/// <summary>INTERLIS-2.3-Schreibweise für den DSS-Vertrag; eigene Datei, keine Änderung am WebGIS.</summary>
internal static class XtfDssWriter
{
    private static readonly XNamespace Ns = "http://www.interlis.ch/INTERLIS2.3";
    public static XDocument BaueDokument(XtfNeuPlan plan)
    {
        var fach = new XElement(Ns + DssExportSchema.Fach, new XAttribute("BID", "chB0000000000001"));
        var admin = new XElement(Ns + DssExportSchema.Verwaltung, new XAttribute("BID", "chB0000000000002"));
        foreach (var o in plan.Objekte.Where(o => !o.ImTopicZusatz))
        {
            var basket = o.ImTopicAdministration ? admin : fach;
            var e = new XElement(Ns + (basket.Name.LocalName + "." + o.Klasse));
            if (!o.OhneTid) e.Add(new XAttribute("TID", o.Tid));
            var felder = o.Felder.ToDictionary(p => p.Key, p => new XElement(Ns + p.Key, p.Value));
            if (o.Strukturen is not null) foreach (var (k, xml) in o.Strukturen)
            {
                using var r = XmlReader.Create(new StringReader(xml), new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 4_000_000 });
                felder.Add(k, XElement.Load(r));
            }
            if (o.Geometrie is { } g && !felder.ContainsKey(g.Feldname))
            {
                var coords = g.Punkte.Select(p => new XElement(Ns + "COORD", new XElement(Ns + "C1", p.OstText), new XElement(Ns + "C2", p.NordText))).ToArray();
                felder.Add(g.Feldname, new XElement(Ns + g.Feldname, g.IstLinie ? new XElement(Ns + "POLYLINE", coords) : coords.Single()));
            }
            foreach (var f in DssExportSchema.Felder(o.Klasse)?.Keys ?? Enumerable.Empty<string>()) if (felder.TryGetValue(f, out var wert)) e.Add(wert);
            foreach (var r in o.Verweise) e.Add(new XElement(Ns + r.Name, new XAttribute("REF", r.ZielTid)));
            basket.Add(e);
        }
        return new XDocument(new XDeclaration("1.0", "utf-8", null), new XElement(Ns + "TRANSFER",
            new XElement(Ns + "HEADERSECTION", new XAttribute("VERSION", "2.3"), new XAttribute("SENDER", "SewerStudio"),
                new XElement(Ns + "MODELS", Modell(DssExportSchema.Modell), Modell(DssExportSchema.Basis)),
                new XElement(Ns + "COMMENT", "DSS-Neuexport aus gespeichertem Projektstand.\n" + string.Join("\n", plan.Hinweise))),
            new XElement(Ns + "DATASECTION", fach, admin.HasElements ? admin : null)));
    }
    private static XElement Modell(string name) => new(Ns + "MODEL", new XAttribute("NAME", name), new XAttribute("VERSION", "18.10.2023"), new XAttribute("URI", "http://www.vsa.ch/models"));
    public static void SchreibeModelle(string ordner)
    {
        var assembly = typeof(XtfDssWriter).Assembly;
        foreach (var name in assembly.GetManifestResourceNames().Where(n => n.StartsWith("Dss.", StringComparison.Ordinal) && n.EndsWith(".ili", StringComparison.Ordinal)))
        {
            using var input = assembly.GetManifestResourceStream(name)!;
            using var data = new MemoryStream(); input.CopyTo(data);
            var bytes = data.ToArray(); var path = Path.Combine(ordner, name[4..]);
            if (File.Exists(path))
            {
                if (!File.ReadAllBytes(path).AsSpan().SequenceEqual(bytes)) throw new IOException($"Das vorhandene Modell {Path.GetFileName(path)} hat einen anderen Stand. Bitte einen neuen Ausgabeordner wählen.");
                continue;
            }
            using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None); file.Write(bytes);
        }
    }
}
