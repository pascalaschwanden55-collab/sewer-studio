using System.Xml.Linq;
using System.Text.Json;
using AuswertungPro.Next.Application.Xtf;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Import.Xtf;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class XtfDssAenderungsExportTests
{
    [Fact]
    public void Widersprechende_Bauwerkskennung_wird_nicht_durch_eine_Bezeichnung_ueberstimmt()
    {
        var p = XtfDssVerbundTests.Verbund();
        p.SchaechteData[0].Geonis!.Bauwerk = "chTEST0000000099";
        var r = new XtfNeuExportService().Erzeuge(new(p, "", NurPruefen: true, NurAenderungen: true));
        Assert.False(r.Ok); Assert.Contains("chTEST0000000099", r.Fehler); Assert.Contains("widerspricht", r.Fehler);
    }

    [Fact]
    public void Zusatzangaben_bewahren_Form_Rotation_leere_Handfelder_Originalcodes_und_Listen()
    {
        var p = XtfDssVerbundTests.Verbund(); var s = p.SchaechteData[0];
        s.SetFieldValue(FieldKeys.ShaftShape, "Oval", FieldSource.Manual, true);
        s.SetFieldValue(FieldKeys.RehabilitationNeed, "Saniert", FieldSource.Manual, true);
        s.SetFieldValue("Material", "Beton, Fertigteil", FieldSource.Manual, true);
        var a = p.Objektakten.Single(a => a.Id == s.Id);
        a.Werte["schacht.rotation"] = new() { Text = "169.5", VonHand = true };
        a.Werte["schacht.bezeichnung_historisch"] = new() { Text = "", VonHand = true };
        a.Werte["schacht.form"] = new() { Text = "Oval", Originalcode = "103", Bestandswert = "Oval", VonHand = true };
        a.Unterlisten["kontrolle"] = [new() { ["Bemerkung"] = "Prüfung erfüllt" }];
        var vorher = JsonSerializer.Serialize(p);
        WithExport(p, xml =>
        {
            var paket = Assert.Single(xml.Descendants(), e => e.Name.LocalName.EndsWith(".Zusatzangabe")
                && Feld(e, "ObjektTid") == s.Geonis!.Knoten && Feld(e, "Feld") == "Erfasste_Angaben");
            using var json = JsonDocument.Parse(Feld(paket, "Wert")!);
            var daten = json.RootElement.GetProperty("Objekte")[0];
            Assert.Equal("Oval", daten.GetProperty("Felder").GetProperty(FieldKeys.ShaftShape).GetProperty("Wert").GetString());
            Assert.Equal("169.5", daten.GetProperty("Objektfelder").GetProperty("schacht.rotation").GetProperty("Wert").GetString());
            Assert.Equal("", daten.GetProperty("Objektfelder").GetProperty("schacht.bezeichnung_historisch").GetProperty("Wert").GetString());
            Assert.Equal("103", daten.GetProperty("Objektfelder").GetProperty("schacht.form").GetProperty("Originalcode").GetString());
            Assert.Equal("Prüfung erfüllt", daten.GetProperty("Unterlisten").GetProperty("kontrolle")[0].GetProperty("Bemerkung").GetString());
            Assert.Equal("Beton", Feld(Objekt(xml, "Normschacht", s.Geonis!.Bauwerk!), "Material"));
            Assert.DoesNotContain(xml.Descendants(), e => e.Name.LocalName == "Sanierungsbedarf" && e.Value == "Saniert");
            Assert.DoesNotContain(xml.Descendants(), e => e.Name.LocalName is "Rotation" or "Form");
        });
        Assert.Equal(vorher, JsonSerializer.Serialize(p));
    }

    [Fact]
    public void Jahresangabe_erfindet_keinen_Tag_und_Neubauten_erhalten_stabile_Kennungen()
    {
        var p = XtfDssVerbundTests.Verbund(); var a = p.Objektakten.Single(a => a.Art == "sanierung");
        a.Werte.Remove("sanierung.beginn");
        a.Werte["sanierung.s_year"] = new() { Text = "2026", VonHand = true };
        string? tid = null;
        WithExport(p, xml =>
        {
            var ereignis = Assert.Single(xml.Descendants(), e => e.Name.LocalName.EndsWith(".Unterhalt") && Feld(e, "Bezeichnung") == "Reparatur 2026");
            Assert.Null(Feld(ereignis, "Zeitpunkt")); tid = (string?)ereignis.Attribute("TID");
            Assert.Contains(xml.Descendants().Where(e => e.Name.LocalName.EndsWith(".Aenderung")), e => Feld(e, "ObjektTid") == tid
                && Feld(e, "Feld") == "Beziehung:Erhaltungsereignis_AbwasserbauwerkAssoc");
        });
        WithExport(p, xml => Assert.NotNull(Objekt(xml, "Unterhalt", tid!)));
    }

    [Fact]
    public void Ungueltiger_Quellcode_bleibt_im_Zusatzmodell_und_wird_nicht_als_Normcode_geraten()
    {
        var p = XtfDssVerbundTests.Verbund();
        p.Objektakten.Single(a => a.Art == "schacht").Quellen.Single(q => q.Klasse == "Einstiegshilfe").Werte["Art"] = "1";
        WithExport(p, xml =>
        {
            Assert.Null(Feld(Objekt(xml, "Einstiegshilfe", "chTEST0000000014"), "Art"));
            var paket = Assert.Single(xml.Descendants(), e => e.Name.LocalName.EndsWith(".Zusatzangabe") && Feld(e, "ObjektTid") == "chTEST0000000014");
            using var json = JsonDocument.Parse(Feld(paket, "Wert")!);
            Assert.Equal("1", json.RootElement.GetProperty("Objekte")[0].GetProperty("Quellabweichungen").GetProperty("Art").GetString());
            Assert.DoesNotContain(xml.Descendants().Where(e => e.Name.LocalName.EndsWith(".Aenderung")), e => Feld(e, "ObjektTid") == "chTEST0000000014" && Feld(e, "Feld") == "Art");
        });
    }

    [Fact]
    public void Konflikt_meldet_beide_Originalkennungen_und_schreibt_nichts()
    {
        var p = XtfDssVerbundTests.Verbund();
        var quellen = p.Objektakten[0].Quellen.Where(q => q.Klasse == "Haltungspunkt").ToArray();
        quellen[1].Werte["Bezeichnung"] = quellen[0].Werte["Bezeichnung"];
        var result = new XtfNeuExportService().Erzeuge(new(p, "", NurPruefen: true, NurAenderungen: true));
        Assert.False(result.Ok); Assert.Null(result.Datei);
        Assert.Contains(quellen[0].Kennung, result.Fehler); Assert.Contains(quellen[1].Kennung, result.Fehler);
    }

    [Theory]
    [InlineData("Nicht mehr funktionstüchtig (Z0)", "Z0")]
    [InlineData("Starke Mängel (Z1)", "Z1")]
    [InlineData("Mittlere Mängel (Z2)", "Z2")]
    [InlineData("Leichte Mängel (Z3)", "Z3")]
    [InlineData("Keine Mängel (Z4)", "Z4")]
    public void Zustandstexte_aus_Alle_Angaben_schreiben_den_richtigen_Normcode(string text, string norm)
    {
        var p = XtfDssVerbundTests.Verbund(); var s = p.SchaechteData[0];
        s.SetFieldValue(FieldKeys.ConditionClass, text, FieldSource.Manual, true);
        WithExport(p, xml => Assert.Equal(norm, Feld(Objekt(xml, "Normschacht", s.Geonis!.Bauwerk!), "BaulicherZustand")));
    }

    [Fact]
    public void Originalkennungen_und_Alle_Angaben_gehen_auch_in_die_Aenderungslieferung()
    {
        var p = XtfDssVerbundTests.Verbund();
        var s = p.SchaechteData[0];
        s.SetFieldValue(FieldKeys.Owner, XtfDssExportTests.Owner, FieldSource.Kataster, false);
        s.SetFieldValue(FieldKeys.DataOwner, XtfDssExportTests.Owner, FieldSource.Kataster, false);
        s.SetFieldValue(FieldKeys.DataSupplier, XtfDssExportTests.Owner, FieldSource.Kataster, false);
        s.SetFieldValue(FieldKeys.ConditionClass, "4", FieldSource.Manual, true);
        p.Objektakten.Single(a => a.Art == "deckel").Werte["deckel.hoehe"] = new() { Text = "452.250", VonHand = true };
        WithExport(p, xml =>
        {
            var bw = Objekt(xml, "Normschacht", s.Geonis!.Bauwerk!);
            Assert.Equal("Z4", Feld(bw, "BaulicherZustand"));
            Assert.Equal(XtfDssExportTests.Owner, bw.Elements().Single(e => e.Name.LocalName == "EigentuemerRef").Attribute("REF")!.Value);
            Assert.Equal("452.250", Feld(Objekt(xml, "Deckel", "chTEST0000000012"), "Kote"));
            Assert.Contains(xml.Descendants(), e => e.Name.LocalName == "DSS_2020_1_LV95.Siedlungsentwaesserung.Normschacht");
            Assert.Contains(xml.Descendants().Where(e => e.Name.LocalName.EndsWith(".Aenderung")),
                e => Feld(e, "ObjektTid") == "chTEST0000000012" && Feld(e, "Feld") == "Kote");
        });
    }

    [Fact]
    public void Geleerte_Bemerkung_erzeugt_einen_Auftrag_am_Originalbauwerk()
    {
        var p = XtfDssVerbundTests.Verbund();
        var s = p.SchaechteData[0];
        p.Objektakten.Single(a => a.Id == s.Id).Quellen.Single(q => q.Klasse == "Normschacht").Werte["Bemerkung"] = "Alter Text";
        s.SetFieldValue(FieldKeys.Remarks, "", FieldSource.Manual, true);
        WithExport(p, xml =>
        {
            Assert.Null(Feld(Objekt(xml, "Normschacht", s.Geonis!.Bauwerk!), "Bemerkung"));
            Assert.Contains(xml.Descendants().Where(e => e.Name.LocalName.EndsWith(".Aenderung")),
                e => Feld(e, "ObjektTid") == s.Geonis.Bauwerk && Feld(e, "Feld") == "Bemerkung");
        });
    }

    private static string? Feld(XElement e, string name) => e.Elements().SingleOrDefault(f => f.Name.LocalName == name)?.Value;
    private static XElement Objekt(XDocument xml, string klasse, string tid) => Assert.Single(xml.Descendants(),
        e => e.Name.LocalName.EndsWith("." + klasse) && (string?)e.Attribute("TID") == tid);
    internal static void WithExport(Project p, Action<XDocument> pruefen)
    {
        var dir = Path.Combine(Path.GetTempPath(), "SewerStudio-Dss-Delta-" + Guid.NewGuid());
        try
        {
            var result = new XtfNeuExportService().Erzeuge(new(p, dir, NurAenderungen: true));
            Assert.True(result.Ok, result.Fehler + "\n" + result.Bericht);
            pruefen(XDocument.Load(result.Datei!));
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }
}
