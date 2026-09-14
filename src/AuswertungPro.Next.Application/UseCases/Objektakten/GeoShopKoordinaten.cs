using System.Globalization;
using System.Xml;
using System.Xml.Linq;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.UseCases.Objektakten;

/// <summary>Nur ein vollstaendiges, gueltiges LV95-Paar wird aus einer Originalstruktur gelesen.</summary>
internal static class GeoShopKoordinaten
{
    public static (string Rechts, string Hoch)? Lies(ObjektQuellbeleg? quelle)
    {
        if (quelle?.Strukturen.GetValueOrDefault("Lage") is not { } text) return null;
        try
        {
            using var reader = XmlReader.Create(new StringReader(text), new XmlReaderSettings
            { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 65536 });
            var xml = XElement.Load(reader);
            var punkte = xml.Descendants().Where(e => e.Name.LocalName == "COORD").ToArray();
            if (punkte.Length != 1) return null;
            string? Wert(string name) => punkte[0].Elements().SingleOrDefault(e => e.Name.LocalName == name)?.Value;
            if (!decimal.TryParse(Wert("C1"), NumberStyles.Float, CultureInfo.InvariantCulture, out var x)
                || !decimal.TryParse(Wert("C2"), NumberStyles.Float, CultureInfo.InvariantCulture, out var y)
                || x is < 2480000 or > 2840000 || y is < 1070000 or > 1300000
                || decimal.Round(x, 3) != x || decimal.Round(y, 3) != y) return null;
            return (x.ToString("0.000", CultureInfo.InvariantCulture), y.ToString("0.000", CultureInfo.InvariantCulture));
        }
        catch (Exception ex) when (ex is XmlException or InvalidOperationException) { return null; }
    }
}
