using System.Xml.Linq;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Import.Xtf;
using static AuswertungPro.Next.Infrastructure.Tests.XtfDssExportTests;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class XtfDssVerbundTests
{
    private const string Knoten = "chTEST0000000010", Bauwerk = "chTEST0000000011", Deckel = "chTEST0000000012", Ereignis = "chTEST0000000013";
    [Fact]
    public void Deckel_Koten_Einstieg_und_zwei_Ereignisse_bleiben_getrennt_ohne_lokale_TID()
    {
        var p = Verbund();
        WithExport(p, doc =>
        {
            Assert.Equal("451.125", Wert(doc, "Deckel", "Kote"));
            Assert.Equal("448.360", Wert(doc, "Abwasserknoten", "Sohlenkote"));
            Assert.Equal("Guss_mit_Betonfuellung", Wert(doc, "Deckel", "Material"));
            Assert.Equal("Steigeisen", Wert(doc, "Einstiegshilfe", "Art"));
            var events = doc.Descendants().Where(e => e.Name.LocalName.EndsWith(".Unterhalt")).ToArray();
            Assert.Equal(2, events.Length);
            Assert.Contains(events, e => e.Elements().Any(f => f.Name.LocalName == "Art" && f.Value == "Sanierung_Renovierung"));
            Assert.Contains(events, e => e.Elements().Any(f => f.Name.LocalName == "Art" && f.Value == "Sanierung_Reparatur"));
            var assocs = doc.Descendants().Where(e => e.Name.LocalName.EndsWith("Assoc")).ToArray();
            Assert.Equal(2, assocs.Length);
            Assert.Equal(Owner, Objekt(doc, "Unterhalt", Ereignis).Elements().Single(e => e.Name.LocalName == "Ausfuehrende_FirmaRef").Attribute("REF")!.Value);
            Assert.All(assocs, e => Assert.Null(e.Attribute("TID")));
            Assert.DoesNotContain("lokal:", doc.ToString());
            Assert.Equal("20250917", Objekt(doc, "Unterhalt", Ereignis).Elements().Single(e => e.Name.LocalName == "Zeitpunkt").Value);
        });
    }
    [Fact]
    public void Pflichtfeld_leeren_sperrt_Export_und_schreibt_keinen_Ersatzwert()
    {
        var p = Verbund();
        p.Objektakten.Single(a => a.Art == "deckel").Werte["deckel.bezeichnung"] = new() { Text = "", VonHand = true };
        var result = new XtfNeuExportService().Erzeuge(new(p, "", NurPruefen: true));
        Assert.False(result.Ok); Assert.Contains("Bezeichnung", result.Fehler);
    }
    [Fact]
    public void Fehlende_interne_Referenz_sperrt_Export_externe_Organisation_bleibt_explizit()
    {
        var p = Projekt();
        p.Objektakten[0].Quellen.RemoveAll(q => q.Klasse == "Organisation");
        var ok = new XtfNeuExportService().Erzeuge(new(p, "", NurPruefen: true));
        Assert.True(ok.Ok, ok.Fehler); Assert.Contains("externe Organisationsverweise", ok.Bericht);
        p.Objektakten[0].Quellen.RemoveAll(q => q.Kennung == Von);
        var error = new XtfNeuExportService().Erzeuge(new(p, "", NurPruefen: true));
        Assert.False(error.Ok); Assert.Contains("Bezugsobjekt", error.Fehler);
    }
    [Fact]
    public void Ungleiche_Originalbelege_werden_nicht_willkuerlich_ausgewaehlt()
    {
        var p = Projekt();
        p.Objektakten[0].Quellen.Add(Quelle("Haltung", Haltung, new() { ["Bezeichnung"] = "Widerspruch" }));
        var r = new XtfNeuExportService().Erzeuge(new(p, "", NurPruefen: true));
        Assert.False(r.Ok); Assert.Contains("widersprüchliche Quellbelege", r.Fehler);
    }
    [Fact]
    public void Vorhandene_Zieldatei_bleibt_unberuehrt()
    {
        var plan = AuswertungPro.Next.Application.Xtf.Dss.DssExportPlanBuilder.Build(Projekt());
        var path = Path.Combine(Path.GetTempPath(), "SewerStudio-Dss-Original-" + Guid.NewGuid() + ".xtf");
        try
        {
            File.WriteAllText(path, "Original");
            Assert.False(XtfNeuWriter.Schreibe(plan, path).Ok);
            Assert.Equal("Original", File.ReadAllText(path));
        }
        finally { File.Delete(path); }
    }
    [Fact]
    public void Geaendertes_Profil_erhaelt_eigene_Kennung_Originalprofil_bleibt_unveraendert()
    {
        var p = Projekt(); var h = p.Data[0];
        const string profil = "chTEST0000000020";
        p.Objektakten[0].Quellen.Single(q => q.Klasse == "Haltung").Referenzen["RohrprofilRef"] = profil;
        p.Objektakten[0].Quellen.Add(Quelle("Rohrprofil", profil, new() { ["Bezeichnung"] = "Kreis", ["Profiltyp"] = "Kreisprofil", ["HoehenBreitenverhaeltnis"] = "1.00" }));
        h.SetFieldValue(FieldKeys.ProfileType, "Eiprofil", FieldSource.Manual, true);
        h.SetFieldValue(FieldKeys.NominalDiameterMm, "600", FieldSource.Manual, true);
        h.SetFieldValue(FieldKeys.ClearWidthMm, "400", FieldSource.Manual, true);
        WithExport(p, doc =>
        {
            var orig = Objekt(doc, "Rohrprofil", profil);
            Assert.Equal("Kreisprofil", orig.Elements().Single(e => e.Name.LocalName == "Profiltyp").Value);
            var neu = Objekt(doc, "Haltung").Elements().Single(e => e.Name.LocalName == "RohrprofilRef").Attribute("REF")!.Value;
            Assert.NotEqual(profil, neu);
            Assert.Equal("1.50", Objekt(doc, "Rohrprofil", neu).Elements().Single(e => e.Name.LocalName == "HoehenBreitenverhaeltnis").Value);
        });
    }
    [Fact]
    public void Aktueller_Datenlieferant_und_Erhebungsjahr_ersetzen_gezielt_den_Quellstand()
    {
        var p = Projekt();
        p.Data[0].SetFieldValue(FieldKeys.DataSupplier, "Kanton Uri", FieldSource.Manual, true);
        p.Data[0].SetFieldValue(FieldKeys.InspectionYear, "17.09.2025", FieldSource.Manual, true);
        WithExport(p, doc =>
        {
            var kanal = Objekt(doc, "Kanal");
            Assert.Equal("2025", Wert(doc, "Kanal", "Zustandserhebung_Jahr"));
            Assert.Equal(Owner, kanal.Elements().Single(e => e.Name.LocalName == "EigentuemerRef").Attribute("REF")!.Value);
            var firma = kanal.Elements().Single(e => e.Name.LocalName == "DatenlieferantRef").Attribute("REF")!.Value;
            Assert.NotEqual(Owner, firma);
            Assert.Equal("Kanton Uri", Objekt(doc, "Organisation", firma).Elements().Single(e => e.Name.LocalName == "Bezeichnung").Value);
        });
    }
    [Fact]
    public void Geaendertes_Ereignisdatum_gewinnt_gegen_die_importierte_Zweitanzeige()
    {
        var p = Verbund();
        p.Objektakten.Add(new() { Art = "sanierung", Bezuege = [p.SchaechteData[0].Id],
            Quellen = [p.Objektakten[1].Quellen.Single(q => q.Klasse == "Unterhalt")], Werte = new()
            {
                ["sanierung.s_year"] = new() { Text = "20250917" },
                ["sanierung.beginn"] = new() { Text = "11.09.2026", VonHand = true }
            } });
        WithExport(p, doc => Assert.Equal("20260911", Objekt(doc, "Unterhalt", Ereignis).Elements().Single(e => e.Name.LocalName == "Zeitpunkt").Value));
    }
    [Fact]
    public void Wieder_importiertes_eigenes_Profil_wird_bei_neuem_Datenlieferanten_nicht_ueberschrieben()
    {
        var p = Projekt(); var h = p.Data[0];
        h.SetFieldValue(FieldKeys.ProfileType, "Eiprofil", FieldSource.Manual, true);
        h.SetFieldValue(FieldKeys.NominalDiameterMm, "600", FieldSource.Manual, true);
        h.SetFieldValue(FieldKeys.ClearWidthMm, "400", FieldSource.Manual, true);
        var ersterPlan = AuswertungPro.Next.Application.Xtf.Dss.DssExportPlanBuilder.Build(p);
        var vorher = ersterPlan.Objekte.Single(o => o.Klasse == "Rohrprofil");
        p.Objektakten[0].Quellen.Add(Quelle("Rohrprofil", vorher.Tid, vorher.Felder.ToDictionary(f => f.Key, f => f.Value)));
        p.Objektakten[0].Quellen.Single(q => q.Klasse == "Haltung").Referenzen["RohrprofilRef"] = vorher.Tid;
        h.SetFieldValue(FieldKeys.DataSupplier, "Kanton Uri", FieldSource.Manual, true);
        WithExport(p, doc =>
        {
            var neu = Objekt(doc, "Haltung").Elements().Single(e => e.Name.LocalName == "RohrprofilRef").Attribute("REF")!.Value;
            Assert.NotEqual(vorher.Tid, neu);
            var lieferant = Objekt(doc, "Rohrprofil", neu).Elements().Single(e => e.Name.LocalName == "DatenlieferantRef").Attribute("REF")!.Value;
            Assert.NotEqual(Owner, lieferant);
            Assert.Equal(Owner, Objekt(doc, "Rohrprofil", vorher.Tid).Elements().Single(e => e.Name.LocalName == "DatenlieferantRef").Attribute("REF")!.Value);
        });
    }
    internal static Project Verbund()
    {
        var p = Projekt(); var s = new SchachtRecord { Geonis = new() { Knoten = Knoten, Bauwerk = Bauwerk } };
        s.SetFieldValue("Schachtnummer", "B", FieldSource.Kataster, false); p.SchaechteData.Add(s);
        var quelle = Quelle("Abwasserknoten", Knoten, new() { ["Bezeichnung"] = "B", ["Sohlenkote"] = "448.360" }, new() { ["AbwasserbauwerkRef"] = Bauwerk });
        p.Objektakten.Add(new() { Id = s.Id, Art = "schacht", Quellen =
        [quelle, Quelle("Normschacht", Bauwerk, new() { ["Bezeichnung"] = "B" }, new() { ["EigentuemerRef"] = Owner }),
            Quelle("Deckel", Deckel, new() { ["Bezeichnung"] = "Deckel B", ["Kote"] = "451.125", ["Material"] = "Guss_mit_Betonfuellung" }, new() { ["AbwasserbauwerkRef"] = Bauwerk }),
            Quelle("Einstiegshilfe", "chTEST0000000014", new() { ["Bezeichnung"] = "Einstieg B", ["Art"] = "Steigeisen" }, new() { ["AbwasserbauwerkRef"] = Bauwerk }),
            Quelle("Unterhalt", Ereignis, new() { ["Bezeichnung"] = "Liner 2025", ["Art"] = "Sanierung_Renovierung", ["Zeitpunkt"] = "20250917" }),
            new() { System = "GeoShop-XTF", Modell = "DSS_2020_1_LV95", Klasse = "Erhaltungsereignis_AbwasserbauwerkAssoc", Kennung = "lokal:ereignis", IstLokaleKennung = true,
                Referenzen = new() { ["AbwasserbauwerkRef"] = Bauwerk, ["Erhaltungsereignis_AbwasserbauwerkAssocRef"] = Ereignis } },
            new() { System = "GeoShop-XTF", Modell = "DSS_2020_1_LV95", Klasse = "Erhaltungsereignis_Ausfuehrende_FirmaAssoc", Kennung = "lokal:firma", IstLokaleKennung = true,
                Referenzen = new() { ["Ausfuehrende_FirmaRef"] = Owner, ["Erhaltungsereignis_Ausfuehrende_FirmaAssocRef"] = Ereignis } }
        ] });
        p.Objektakten.Add(new() { Art = "deckel", Bezuege = [s.Id], Quellen = [p.Objektakten[1].Quellen.Single(q => q.Klasse == "Deckel")] });
        p.Objektakten.Add(new() { Art = "sanierung", Bezuege = [s.Id], Werte = new()
        { ["sanierung.s_name"] = new() { Text = "Reparatur 2026", VonHand = true }, ["sanierung.s_art"] = new() { Text = "Reparatur", VonHand = true },
          ["sanierung.beginn"] = new() { Text = "11.09.2026", VonHand = true } } });
        return p;
    }
}
