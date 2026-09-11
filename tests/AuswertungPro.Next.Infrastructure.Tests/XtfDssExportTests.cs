using System.Xml.Linq;
using AuswertungPro.Next.Application.Xtf;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Import.Xtf;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class XtfDssExportTests
{
    [Fact]
    public void GeoShop_NeuExport_enthaelt_Normfelder_Originalkennungen_und_beide_Bemerkungen()
    {
        var p = Projekt();
        WithExport(p, doc =>
        {
            Assert.Contains("DSS_2020_1_LV95", doc.ToString());
            Assert.Equal("Polyesterharz_Glasfaserlaminat", Wert(doc, "Haltung", "Reliner_Material"));
            Assert.Equal("10.00", Wert(doc, "Kanal", "Spuelintervall"));
            Assert.Equal("Leitungstext", Wert(doc, "Haltung", "Bemerkung"));
            Assert.Equal("Bauwerkstext", Wert(doc, "Kanal", "Bemerkung"));
            Assert.Equal("448.910", Objekt(doc, "Haltungspunkt", Von).Elements().Single(e => e.Name.LocalName == "Kote").Value);
            Assert.Equal(Owner, Objekt(doc, "Kanal").Elements().Single(e => e.Name.LocalName == "BetreiberRef").Attribute("REF")!.Value);
            Assert.Equal(Haltung, Objekt(doc, "Haltung").Attribute("TID")!.Value);
        });
    }

    [Fact]
    public void Handaenderung_und_bewusstes_Leeren_schlagen_Importwerte_ohne_Projektmutation()
    {
        var p = Projekt();
        p.Data[0].SetFieldValue(FieldKeys.HoldingLengthMeters, "42,15", FieldSource.Manual, true);
        p.Objektakten[0].Werte["haltung.reliner_material"] = new() { Text = "", VonHand = true };
        p.Objektakten[0].Werte["haltung.fromlevel"] = new() { Text = "449,123", VonHand = true };
        WithExport(p, doc =>
        {
            Assert.Equal("42.15", Wert(doc, "Haltung", "LaengeEffektiv"));
            Assert.DoesNotContain(Objekt(doc, "Haltung").Elements(), e => e.Name.LocalName == "Reliner_Material");
            Assert.Equal("449.123", Objekt(doc, "Haltungspunkt", Von).Elements().Single(e => e.Name.LocalName == "Kote").Value);
        });
        Assert.Equal("Polyesterharz_Glasfaserlaminat", p.Objektakten[0].Quellen.Single(q => q.Klasse == "Haltung").Werte["Reliner_Material"]);
    }

    [Theory]
    [InlineData("haltung.reliner_material", "nicht_im_Modell")]
    [InlineData("haltung.fromlevel", "99999")]
    [InlineData("haltung.reliner_nennweite", "3.5")]
    public void Ungueltige_Werte_sperren_die_Lieferung_statt_alte_Werte_zu_exportieren(string feld, string text)
    {
        var p = Projekt();
        p.Objektakten[0].Werte[feld] = new() { Text = text, VonHand = true };
        var result = new XtfNeuExportService().Erzeuge(new(p, "", NurPruefen: true));
        Assert.False(result.Ok);
        Assert.Contains("DSS", result.Fehler);
    }

    [Fact]
    public void Beschaedigter_Quellwert_wird_vor_Planung_abgewiesen()
    {
        var p = Projekt();
        p.Objektakten[0].Quellen[0].Werte["Material"] = null!;
        var r = new XtfNeuExportService().Erzeuge(new(p, "", NurPruefen: true));
        Assert.False(r.Ok); Assert.Contains("beschädigt", r.Fehler);
    }
    internal const string Haltung = "chTEST0000000001", Kanal = "chTEST0000000002", Von = "chTEST0000000003", Nach = "chTEST0000000004", Owner = "chTEST0000000005";
    internal static Project Projekt()
    {
        var p = new Project { Name = "DSS-Probe" };
        var h = new HaltungRecord { Geonis = new() { Haltung = Haltung, Kanal = Kanal, VonPunkt = Von, NachPunkt = Nach } };
        h.SetFieldValue(FieldKeys.HoldingName, "A-B", FieldSource.Kataster, false);
        h.SetFieldValue(FieldKeys.Owner, "Privat", FieldSource.Kataster, false);
        p.Data.Add(h);
        p.Objektakten.Add(new() { Id = h.Id, Art = "haltung", Quellen =
        [
            Quelle("Haltung", Haltung, new() { ["Bezeichnung"] = "A-B", ["Bemerkung"] = "Leitungstext", ["LaengeEffektiv"] = "40.00", ["Reliner_Material"] = "Polyesterharz_Glasfaserlaminat" },
                new() { ["AbwasserbauwerkRef"] = Kanal, ["vonHaltungspunktRef"] = Von, ["nachHaltungspunktRef"] = Nach }),
            Quelle("Kanal", Kanal, new() { ["Bezeichnung"] = "A-B", ["Bemerkung"] = "Bauwerkstext", ["Spuelintervall"] = "10.00" },
                new() { ["EigentuemerRef"] = Owner, ["BetreiberRef"] = Owner }),
            Quelle("Haltungspunkt", Von, new() { ["Bezeichnung"] = "A-B_von", ["Kote"] = "448.910" }),
            Quelle("Haltungspunkt", Nach, new() { ["Bezeichnung"] = "A-B_nach", ["Kote"] = "448.360" }),
            new() { System = "GeoShop-XTF", Modell = "SIA405_Base_Abwasser_1_LV95", Klasse = "Organisation", Kennung = Owner,
                Werte = new() { ["Bezeichnung"] = "Privat", ["Organisationstyp"] = "Privat", ["Status"] = "aktiv", ["Letzte_Aenderung"] = "20260101" } }
        ] });
        return p;
    }

    internal static ObjektQuellbeleg Quelle(string klasse, string id, Dictionary<string,string> werte, Dictionary<string,string>? refs = null)
    {
        werte["Letzte_Aenderung"] = "20260101";
        refs ??= new(); refs["DatenherrRef"] = Owner; refs["DatenlieferantRef"] = Owner;
        return new() { System = "GeoShop-XTF", Modell = "DSS_2020_1_LV95", Klasse = klasse, Kennung = id, Werte = werte, Referenzen = refs };
    }

    internal static void WithExport(Project p, Action<XDocument> check)
    {
        var dir = Path.Combine(Path.GetTempPath(), "SewerStudio-Dss-Test-" + Guid.NewGuid().ToString("N"));
        try
        {
            var result = new XtfNeuExportService().Erzeuge(new(p, dir, MitZusatzangaben: false));
            Assert.True(result.Ok, result.Fehler + "\n" + result.Bericht);
            check(XDocument.Load(result.Datei!));
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }
    internal static XElement Objekt(XDocument doc, string klasse, string? tid = null) => doc.Descendants().Single(e => e.Name.LocalName.EndsWith("." + klasse, StringComparison.Ordinal) && (tid == null || e.Attribute("TID")?.Value == tid));
    internal static string Wert(XDocument doc, string klasse, string feld) => Objekt(doc, klasse).Elements().Single(e => e.Name.LocalName == feld).Value;
}
