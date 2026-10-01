using System;
using System.Linq;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.WebGis;
using AuswertungPro.Next.Domain.Models;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.WebGis;

/// <summary>
/// Entscheid Pascal 23.09.2026 abends: Die Daten der Kanalfirma sind der Ist-Zustand; weicht ein Wert vom
/// WebGIS ab, erscheint er in der Vorschau als Vorschlag «Kanalfirma weicht vom WebGIS ab» — jede Zeile
/// einzeln zum Anhaken, nie automatisch. Nur Angehaktes geht ins WebGIS.
/// </summary>
public sealed partial class WebGisExportUseCaseTests
{
    private static Project ProjektMitSchachtform(string form, FieldSource quelle, bool hand = false)
    {
        var s = new SchachtRecord();
        s.SetFieldValue("Schachtnummer", "525145", FieldSource.Manual, false);
        s.SetFieldValue("Schachtform", form, quelle, hand);
        var p = new Project();
        p.SchaechteData.Add(s);
        return p;
    }

    [Fact]
    public void Abweichender_wert_der_kanalfirma_wird_vorgeschlagen_nicht_geschrieben()
    {
        var e = Schacht();
        e.Kanalfirmenwerte["Schachtform"] = "Oval";

        var pos = WebGisExportPlanBuilder.Baue(e, SchachtStand(form: "1"));

        Assert.DoesNotContain(pos.Aenderungen, a => a.RefId == FormRef);
        var v = Assert.Single(pos.Vorschlaege);
        Assert.Equal("Form", v.Anzeige);
        Assert.Equal("Rund", v.AltText);
        Assert.Equal("Oval", v.NeuText);
        Assert.False(v.Gewaehlt); // nie automatisch
        Assert.Equal("102", Assert.Single(v.Aenderungen).Neu);
    }

    [Fact]
    public void Gleicher_wert_der_kanalfirma_ist_kein_vorschlag()
    {
        var e = Schacht();
        e.Kanalfirmenwerte["Schachtform"] = "Rund";

        Assert.Empty(WebGisExportPlanBuilder.Baue(e, SchachtStand(form: "1")).Vorschlaege);
    }

    [Fact]
    public void Handwert_geht_dem_vorschlag_vor()
    {
        var e = Schacht(("Schachtform", "Oval"));
        e.Kanalfirmenwerte["Schachtform"] = "Rund";

        var pos = WebGisExportPlanBuilder.Baue(e, SchachtStand(form: "0"));

        Assert.Equal("102", Assert.Single(pos.Aenderungen, a => a.RefId == FormRef).Neu);
        Assert.Empty(pos.Vorschlaege);
    }

    [Theory]
    [InlineData(FieldSource.Xtf405)]
    [InlineData(FieldSource.Legacy)]
    [InlineData(FieldSource.Pdf)]
    public async Task Wert_der_kanalfirma_wird_vorgeschlagen(FieldSource quelle)
    {
        var client = new FakeClient { Lese = (_, _) => SchachtStand(form: "1") };

        var plan = await new WebGisExportUseCase(client).BauePlanAsync(ProjektMitSchachtform("Oval", quelle));

        Assert.Single(plan.Positionen.Single().Vorschlaege);
    }

    [Theory]
    [InlineData(FieldSource.Kataster, false)] // aus GeoShop/QGIS: kein Ist-Zustand der Kanalfirma
    [InlineData(FieldSource.Manual, true)]    // von Hand: geht als Handwert, nicht als Vorschlag
    public async Task Nur_werte_der_kanalfirma_werden_vorgeschlagen(FieldSource quelle, bool hand)
    {
        var client = new FakeClient { Lese = (_, _) => SchachtStand(form: "1") };

        var plan = await new WebGisExportUseCase(client).BauePlanAsync(ProjektMitSchachtform("Oval", quelle, hand));

        Assert.Empty(plan.Positionen.Single().Vorschlaege);
    }

    [Fact]
    public async Task Nur_angehakte_vorschlaege_werden_ins_webgis_geschrieben()
    {
        var client = new FakeClient { Lese = (_, _) => SchachtStand(form: "1") };
        var useCase = new WebGisExportUseCase(client);
        var plan = await useCase.BauePlanAsync(ProjektMitSchachtform("Oval", FieldSource.Xtf405));
        var pos = plan.Positionen.Single();

        WebGisVorschlagAuswahl.UebernimmGewaehlte(plan);
        await useCase.FuehreAusAsync(plan, probelauf: false);
        Assert.Empty(client.Geschrieben); // nichts angehakt: nichts geschrieben

        pos.Vorschlaege.Single().Gewaehlt = true;
        WebGisVorschlagAuswahl.UebernimmGewaehlte(plan);
        await useCase.FuehreAusAsync(plan, probelauf: false);

        Assert.Equal("102", Assert.Single(client.Geschrieben).Felder[FormRef]);
        Assert.True(pos.Geschrieben, pos.SchreibFehler);
    }

    [Fact]
    public async Task Auswahl_gilt_auch_im_frisch_geprueften_plan()
    {
        // «Jetzt schreiben» baut den Plan frisch; die Haken der Vorschau muessen mitkommen, und beide
        // Plaene muessen dann gleich sein — sonst schriebe das Programm nie.
        var client = new FakeClient { Lese = (_, _) => SchachtStand(form: "1") };
        var useCase = new WebGisExportUseCase(client);
        var projekt = ProjektMitSchachtform("Oval", FieldSource.Xtf405);
        var vorschau = await useCase.BauePlanAsync(projekt);
        vorschau.Positionen.Single().Vorschlaege.Single().Gewaehlt = true;

        var auswahl = WebGisVorschlagAuswahl.Gewaehlte(vorschau);
        var frisch = await useCase.BauePlanAsync(projekt);
        WebGisVorschlagAuswahl.Waehle(frisch, auswahl);
        WebGisVorschlagAuswahl.UebernimmGewaehlte(vorschau);
        WebGisVorschlagAuswahl.UebernimmGewaehlte(frisch);

        Assert.True(frisch.Positionen.Single().Schreibbar);
        Assert.True(WebGisPlanVergleich.Gleich(vorschau, frisch));
    }

    [Fact]
    public async Task Ein_haken_mehr_oder_weniger_ist_ein_anderer_plan()
    {
        // Der Planvergleich sieht die Haken, ohne dass sie vorher in die Vorschau uebernommen werden.
        var client = new FakeClient { Lese = (_, _) => SchachtStand(form: "1") };
        var useCase = new WebGisExportUseCase(client);
        var projekt = ProjektMitSchachtform("Oval", FieldSource.Xtf405);
        var mitHaken = await useCase.BauePlanAsync(projekt);
        var ohneHaken = await useCase.BauePlanAsync(projekt);
        mitHaken.Positionen.Single().Vorschlaege.Single().Gewaehlt = true;

        Assert.False(WebGisPlanVergleich.Gleich(mitHaken, ohneHaken));
        Assert.False(mitHaken.NichtsZuSchreiben);
        Assert.True(ohneHaken.NichtsZuSchreiben);

        WebGisVorschlagAuswahl.UebertrageAuf(mitHaken, ohneHaken);
        Assert.True(WebGisPlanVergleich.Gleich(mitHaken, ohneHaken));
        WebGisVorschlagAuswahl.UebernimmGewaehlte(ohneHaken);
        Assert.True(WebGisPlanVergleich.Gleich(mitHaken, ohneHaken)); // uebernommen oder nur angehakt: derselbe Plan
    }

    [Fact]
    public async Task Ausfuehren_nimmt_angehakte_vorschlaege_mit()
    {
        // Das Fenster setzt nur den Haken; das Ausfuehren macht ihn zur Aenderung.
        var client = new FakeClient { Lese = (_, _) => SchachtStand(form: "1") };
        var useCase = new WebGisExportUseCase(client);
        var plan = await useCase.BauePlanAsync(ProjektMitSchachtform("Oval", FieldSource.Xtf405));
        plan.Positionen.Single().Vorschlaege.Single().Gewaehlt = true;

        await useCase.FuehreAusAsync(plan, probelauf: false);

        Assert.Equal("102", Assert.Single(client.Geschrieben).Felder[FormRef]);
    }

    [Fact]
    public async Task Frischer_plan_uebernimmt_die_haken_der_vorschau()
    {
        var client = new FakeClient { Lese = (_, _) => SchachtStand(form: "1") };
        var useCase = new WebGisExportUseCase(client);
        var projekt = ProjektMitSchachtform("Oval", FieldSource.Xtf405);
        var vorschau = await useCase.BauePlanAsync(projekt);
        vorschau.Positionen.Single().Vorschlaege.Single().Gewaehlt = true;

        var frisch = await useCase.BaueFrischenPlanAsync(projekt, vorschau);
        var ohneVorschau = await useCase.BaueFrischenPlanAsync(projekt, bestaetigt: null);

        Assert.True(frisch.Positionen.Single().Vorschlaege.Single().Gewaehlt);
        Assert.True(WebGisPlanVergleich.Gleich(vorschau, frisch));
        Assert.False(ohneVorschau.Positionen.Single().Vorschlaege.Single().Gewaehlt);
        Assert.True(ohneVorschau.NichtsZuSchreiben);
    }

    [Fact]
    public void Uebernahme_der_auswahl_ist_wiederholbar()
    {
        var e = Schacht();
        e.Kanalfirmenwerte["Schachtform"] = "Oval";
        var plan = new WebGisExportPlan();
        plan.Positionen.Add(WebGisExportPlanBuilder.Baue(e, SchachtStand(form: "1")));
        plan.Positionen[0].Vorschlaege[0].Gewaehlt = true;

        WebGisVorschlagAuswahl.UebernimmGewaehlte(plan);
        WebGisVorschlagAuswahl.UebernimmGewaehlte(plan);

        Assert.Single(plan.Positionen[0].Aenderungen, a => a.RefId == FormRef);
    }

    [Fact]
    public void Uebersicht_zeigt_die_vorschlaege_zum_anhaken()
    {
        var e = Schacht();
        e.Kanalfirmenwerte["Schachtform"] = "Oval";
        var plan = new WebGisExportPlan();
        plan.Positionen.Add(WebGisExportPlanBuilder.Baue(e, SchachtStand(form: "1")));

        var zeile = Assert.Single(WebGisUebersicht.Aus(plan).Vorschlaege);
        Assert.Equal("Schacht 525145", zeile.Objekt);
        Assert.Equal("Form", zeile.Feld);
        Assert.Equal("Rund", zeile.Alt);
        Assert.Equal("Oval", zeile.Neu);

        zeile.Gewaehlt = true; // das Haekchen im Fenster
        Assert.True(plan.Positionen[0].Vorschlaege[0].Gewaehlt);
        Assert.Empty(WebGisUebersicht.Aus(plan, ergebnis: true).Vorschlaege);
    }

    [Fact]
    public void Nur_vorschlaege_heisst_nicht_nichts_zu_tun()
    {
        // Sonst meldete das Fenster «Nichts zu übertragen», obwohl Abweichungen zur Auswahl stehen.
        var e = Schacht();
        e.Kanalfirmenwerte["Schachtform"] = "Oval";
        var plan = new WebGisExportPlan();
        plan.Positionen.Add(WebGisExportPlanBuilder.Baue(e, SchachtStand(form: "1")));

        var u = WebGisUebersicht.Aus(plan);

        Assert.Equal(0, u.ObjekteMitAenderung);
        Assert.False(u.NichtsZuTun);
    }

    [Fact]
    public void Bericht_nennt_die_vorschlaege_der_kanalfirma()
    {
        var e = Schacht();
        e.Kanalfirmenwerte["Schachtform"] = "Oval";
        var plan = new WebGisExportPlan();
        plan.Positionen.Add(WebGisExportPlanBuilder.Baue(e, SchachtStand(form: "1")));

        var text = WebGisExportBericht.Details(plan, mitErgebnis: false);

        Assert.Contains("Kanalfirma", text);
        Assert.Contains("Rund → Oval", text);
    }
}
