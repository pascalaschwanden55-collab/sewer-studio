using System.Xml;
using System.Xml.Linq;
using AuswertungPro.Next.Application.Lookup;
using AuswertungPro.Next.Application.Xtf.Dss;
using AuswertungPro.Next.Application.Xtf.Lieferung;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Infrastructure.Import.Xtf.Lieferung;

internal static class XtfLieferungsXml
{
    internal static readonly XNamespace Ns = "http://www.interlis.ch/INTERLIS2.3";
    internal static XElement Element(string xml)
    {
        using var r = XmlReader.Create(new StringReader(xml), new XmlReaderSettings
            { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 16_000_000 });
        return XElement.Load(r);
    }
    internal static ObjektQuellbeleg Beleg(XElement e)
    {
        var namen = e.Name.LocalName.Split('.');
        if (e.Name.Namespace != Ns || namen.Length != 3) throw new InvalidDataException("Objekt ohne vollständigen INTERLIS-Klassennamen.");
        if (namen[0] == DssExportSchema.Modell && namen[1] != "Siedlungsentwaesserung"
            || namen[0] == DssExportSchema.Basis && namen[1] != "Administration")
            throw new InvalidDataException("Objekt gehört nicht zum angegebenen INTERLIS-Thema.");
        var q = new ObjektQuellbeleg { System = "GeoShop-XTF", Modell = namen[0], Klasse = namen[2], Kennung = e.Attribute("TID")?.Value ?? "", IstLokaleKennung = e.Attribute("TID") is null };
        foreach (var f in e.Elements())
        {
            if (f.Name.Namespace != Ns) throw new InvalidDataException($"Feld {f.Name.LocalName} hat einen fremden XML-Namensraum und kann nicht verlustfrei übernommen werden.");
            if (f.Attribute("REF") is { } r) q.Referenzen.Add(f.Name.LocalName, r.Value);
            else if (f.HasElements) q.Strukturen.Add(f.Name.LocalName, f.ToString(SaveOptions.DisableFormatting));
            else q.Werte.Add(f.Name.LocalName, f.Value);
        }
        return q;
    }
    internal static IReadOnlyList<XtfLieferungsFeld> Felder(XElement e)
    {
        var q = Beleg(e); var schema = DssExportSchema.Felder(q.Klasse); var felder = new List<XtfLieferungsFeld>();
        foreach (var name in (schema?.Keys ?? []).Concat(q.Werte.Keys).Concat(q.Strukturen.Keys).Distinct())
        {
            var def = schema?.GetValueOrDefault(name);
            if (def?.Kind == "Structure")
            {
                if (name is "Lage" or "TextPos")
                {
                    foreach (var achse in new[] { "C1", "C2" })
                        felder.Add(new(name + "." + achse, name + (achse == "C1" ? " – Rechtswert" : " – Hochwert"),
                            e.Element(Ns + name)?.Element(Ns + "COORD")?.Element(Ns + achse)?.Value ?? "", "Number", def.Required, true, [], "LV95, Meter"));
                }
                else felder.Add(new(name, name, e.Element(Ns + name) is { } g ? $"{g.Descendants().Count(x => x.Name.LocalName is "COORD" or "ARC")} Geometriepunkte vorhanden" : "Keine Geometrie", "Structure", def.Required, false, [], "Originalgeometrie bleibt erhalten."));
                continue;
            }
            felder.Add(new(name, name.Replace('_', ' '), q.Werte.GetValueOrDefault(name, ""), def?.Kind ?? "Text",
                def?.Required == true, def is not null && name != "Letzte_Aenderung",
                def?.Kind == "Enum" ? (def.Required ? def.Values : new[] { "" }.Concat(def.Values).ToArray()) : [],
                name == "Letzte_Aenderung" ? "Wird bei einer Änderung aktualisiert." : def is null ? "Modellzuordnung fehlt." : ""));
        }
        foreach (var rolle in XtfLieferungsNorm.Rollen(q.Klasse).Concat(q.Referenzen.Keys).Distinct())
            felder.Add(new(rolle, rolle.EndsWith("Ref", StringComparison.Ordinal) ? rolle[..^3] + " (Kennung)" : rolle,
                q.Referenzen.GetValueOrDefault(rolle, ""), "Ref", XtfLieferungsNorm.Pflichtrolle(q.Klasse, rolle),
                XtfLieferungsNorm.Rollen(q.Klasse).Contains(rolle), [], "Originalkennung des Bezugsobjekts."));
        return felder;
    }

    internal static XElement Aendere(XElement e, IReadOnlyDictionary<string, string> aenderungen, Func<string, string?> zielklasse)
    {
        var q = Beleg(e); var erlaubt = Felder(e).ToDictionary(f => f.Schluessel);
        var result = new XElement(e);
        foreach (var (key, eingabe) in aenderungen)
        {
            if (eingabe is null || !erlaubt.TryGetValue(key, out var feld) || !feld.Bearbeitbar)
                throw new InvalidOperationException($"Feld {key} ist nicht zur Bearbeitung freigegeben.");
            if (eingabe == feld.Wert) continue;
            if (key.Contains('.')) continue; // Koordinatenpaare werden unten gemeinsam geprüft.
            var wert = feld.Typ == "Ref" ? eingabe.Trim() : DssExportSchema.Normalisiere(q.Klasse, key, eingabe) ?? "";
            if (feld.Pflicht && wert.Length == 0) throw new InvalidOperationException($"Pflichtfeld {feld.Label} darf nicht leer sein.");
            if (feld.Typ == "Ref" && wert.Length > 0)
            {
                if (!SiaObjektkennung.IstGueltig(wert)) throw new InvalidOperationException($"{feld.Label}: ungültige Kennung.");
                var klasse = zielklasse(wert);
                if (klasse is null ? !XtfLieferungsNorm.ExterneOrganisation(key) : !XtfLieferungsNorm.PassendesZiel(key, klasse))
                    throw new InvalidOperationException($"{feld.Label}: Bezug fehlt, ist mehrdeutig oder hat die falsche Objektart.");
            }
            var element = result.Element(Ns + key);
            if (wert.Length == 0) element?.Remove();
            else
            {
                if (element is null) { element = new XElement(Ns + key); result.Add(element); }
                if (feld.Typ == "Ref") element.SetAttributeValue("REF", wert); else element.Value = wert;
            }
        }
        foreach (var name in new[] { "Lage", "TextPos" })
        {
            if (!aenderungen.Keys.Any(k => k == name + ".C1" || k == name + ".C2")) continue;
            var x = aenderungen.GetValueOrDefault(name + ".C1", erlaubt.GetValueOrDefault(name + ".C1")?.Wert ?? "");
            var y = aenderungen.GetValueOrDefault(name + ".C2", erlaubt.GetValueOrDefault(name + ".C2")?.Wert ?? "");
            var geo = result.Element(Ns + name);
            if (geo is not null && (geo.DescendantsAndSelf().Any(n => n.Attributes().Any(a => !a.IsNamespaceDeclaration))
                || geo.Elements().Count() != 1 || geo.Element(Ns + "COORD") is not { } originalCoord
                || originalCoord.Elements().Any(n => n.Name != Ns + "C1" && n.Name != Ns + "C2")
                || originalCoord.Elements().GroupBy(n => n.Name).Any(g => g.Count() > 1)))
                throw new InvalidOperationException($"{name}: zusätzliche Geometrieangaben können hier nicht verlustfrei bearbeitet werden.");
            if (x.Length == 0 && y.Length == 0 && !erlaubt[name + ".C1"].Pflicht) geo?.Remove();
            else
            {
                var coord = new XElement(Ns + name, new XElement(Ns + "COORD", new XElement(Ns + "C1", XtfLieferungsNorm.Koordinate("C1", x)), new XElement(Ns + "C2", XtfLieferungsNorm.Koordinate("C2", y))));
                if (geo is null) result.Add(coord); else geo.ReplaceWith(coord);
            }
        }
        if (!XNode.DeepEquals(e, result) && DssExportSchema.Felder(q.Klasse)?.ContainsKey("Letzte_Aenderung") == true)
            result.SetElementValue(Ns + "Letzte_Aenderung", DateTime.Today.ToString("yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture));
        return result;
    }

    internal static XElement FuerExport(XElement e)
    {
        var q = Beleg(e); var result = new XElement(e.Name, e.Attributes());
        foreach (var name in DssExportSchema.Felder(q.Klasse)?.Keys ?? [])
        {
            var feld = e.Element(Ns + name); if (feld is null) continue;
            if (feld.HasElements) result.Add(new XElement(feld));
            else if (DssExportSchema.Normalisiere(q.Klasse, name, feld.Value) is { } norm)
                result.Add(new XElement(feld.Name, feld.Attributes(), norm));
        }
        foreach (var feld in e.Elements().Where(f => f.Attribute("REF") is not null)) result.Add(new XElement(feld));
        return result;
    }
}
