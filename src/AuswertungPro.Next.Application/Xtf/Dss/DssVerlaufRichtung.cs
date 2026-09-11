using System.Xml;
using System.Xml.Linq;

namespace AuswertungPro.Next.Application.Xtf.Dss;

internal static class DssVerlaufRichtung
{
    public static string Drehe(string xml)
    {
        using var reader = XmlReader.Create(new StringReader(xml), new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 4_000_000 });
        var e = XElement.Load(reader); var ns = e.Name.Namespace;
        var linie = e.Element(ns + "POLYLINE") ?? throw new InvalidOperationException("DSS: Gegenrichtung benötigt einen einfachen Verlauf.");
        var teile = linie.Elements().ToArray();
        if (teile.Length < 2 || teile[0].Name.LocalName != "COORD" || teile.Any(p => p.Name.LocalName is not ("COORD" or "ARC")))
            throw new InvalidOperationException("DSS: Dieser Verlauf kann nicht sicher in die Gegenrichtung gedreht werden.");
        XElement Endpunkt(XElement p) => new(ns + "COORD", p.Elements().Where(c => c.Name.LocalName is "C1" or "C2" or "C3").Select(c => new XElement(c)));
        var umgekehrt = new List<XElement> { Endpunkt(teile[^1]) };
        for (var i = teile.Length - 1; i > 0; i--)
        {
            var ende = Endpunkt(teile[i - 1]);
            if (teile[i].Name.LocalName == "COORD") umgekehrt.Add(ende);
            else
            {
                var bogen = new XElement(teile[i]);
                foreach (var c in ende.Elements()) bogen.SetElementValue(c.Name, c.Value);
                umgekehrt.Add(bogen);
            }
        }
        linie.ReplaceNodes(umgekehrt); return e.ToString(SaveOptions.DisableFormatting);
    }
}
