using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.WebGis;
using AuswertungPro.Next.Domain.Models;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.WebGis;

/// <summary>
/// Wunsch Pascal 28.09.2026: Vergleichsliste SewerStudio | WebGIS | was passiert, und ein Objekt einzeln schreiben,
/// ohne jedes Mal die ganze Liste neu zu suchen. Die Liste zeigt nur, was der Plan tatsächlich schreibt.
/// </summary>
public sealed partial class WebGisExportUseCaseTests
{
    private static WebGisLesestand StandMitLaenge(string gid, string name)
    {
        var s = HaltungStand(gid, name);
        s.Felder[WebGisFeldkarte.HaltungLaengeGeomRef] = "41.87";
        return s;
    }

    private static Project ProjektMitZweiHaltungen(string gidZwei = "G2")
    {
        var p = ProjektMitHaltung("H1", "G1");
        p.Data[0].SetFieldValue("Haltungslaenge_m", "42.10", FieldSource.Manual, false);
        p.Data[0].SetFieldValue("Rohrmaterial", "Steinzeug", FieldSource.Xtf, false);
        var zweite = new HaltungRecord { WebGisGlobalId = gidZwei };
        zweite.SetFieldValue(FieldKeys.HoldingName, "H2", FieldSource.Manual, false);
        p.Data.Add(zweite);
        return p;
    }

    [Fact]
    public async Task Vergleichsliste_zeigt_jedes_feld_mit_beiden_werten_und_was_passiert()
    {
        var p = ProjektMitZweiHaltungen();
        var client = new FakeClient { LeseUeberId = (_, gid) => StandMitLaenge(gid, gid == "G1" ? "H1" : "H2") };

        var plan = await new WebGisExportUseCase(client).BauePlanAsync(p);
        var zeilen = plan.Positionen.Single(x => x.Bezeichnung == "H1").Vergleich;

        var zustand = zeilen.Single(z => z.Feld == "Zustand");
        Assert.Equal(WebGisVergleichsArt.Aendern, zustand.Art);
        Assert.Equal("102", zustand.WebGis);
        Assert.Equal(WebGisFeldkarte.ZustandText(104), zustand.Nachher);

        var laenge = zeilen.Single(z => z.Feld == "Länge");
        Assert.Equal(WebGisVergleichsArt.BleibtImWebGis, laenge.Art);
        Assert.Equal("42.10", laenge.SewerStudio);
        Assert.Equal("41.87", laenge.WebGis);
        Assert.Contains("nie", laenge.Grund);

        // Weicht ab, ist aber weder Handwert noch Vorschlag: steht da, geht aber nicht hinaus.
        var material = zeilen.Single(z => z.Feld == "Material");
        Assert.False(material.WirdGeschrieben);
        Assert.Equal("Steinzeug", material.SewerStudio);

        // Die Liste verspricht genau das, was der Plan schreibt.
        var geplant = plan.Positionen.Single(x => x.Bezeichnung == "H1").Aenderungen.Select(a => a.RefId).ToHashSet();
        Assert.All(zeilen.Where(z => z.WirdGeschrieben), z => Assert.Contains(z.RefId!, geplant));
    }

    [Fact]
    public async Task Einzelpruefung_liest_nur_das_eine_objekt()
    {
        var p = ProjektMitZweiHaltungen();
        var client = new FakeClient { LeseUeberId = (_, gid) => StandMitLaenge(gid, gid == "G1" ? "H1" : "H2") };

        var plan = await new WebGisExportUseCase(client).BaueEinzelPlanAsync(p, WebGisObjektart.Haltung, p.Data[0].Id, null);

        Assert.Single(plan.Positionen);
        Assert.Equal("H1", plan.Positionen[0].Bezeichnung);
        Assert.Equal(1, client.IdLesungen);
        Assert.True(plan.Positionen[0].Schreibbar);
    }

    [Fact]
    public async Task Einzelpruefung_sperrt_wenn_ein_zweiter_datensatz_auf_dasselbe_objekt_zeigt()
    {
        var p = ProjektMitZweiHaltungen(gidZwei: "G1"); // H2 traegt dieselbe GlobalID
        var client = new FakeClient { LeseUeberId = (_, gid) => StandMitLaenge(gid, "H1") };

        var plan = await new WebGisExportUseCase(client).BaueEinzelPlanAsync(p, WebGisObjektart.Haltung, p.Data[0].Id, null);

        Assert.False(plan.Positionen[0].Schreibbar);
        Assert.Contains(plan.Positionen[0].Sperren, s => s.Contains("zweiter Datensatz"));
    }

    [Fact]
    public async Task Einzeln_schreiben_aendert_nur_dieses_objekt_und_zeigt_das_ergebnis()
    {
        var p = ProjektMitZweiHaltungen();
        var client = new FakeClient { LeseUeberId = (_, gid) => StandMitLaenge(gid, gid == "G1" ? "H1" : "H2") };
        var useCase = new WebGisExportUseCase(client);
        var gesamt = await useCase.BauePlanAsync(p);

        var einzel = await useCase.BaueEinzelPlanAsync(p, WebGisObjektart.Haltung, p.Data[0].Id, gesamt);
        Assert.True(WebGisPlanVergleich.Gleich(WebGisPlanAusschnitt.Von(gesamt, WebGisObjektart.Haltung, p.Data[0].Id), einzel));

        var vorher = WebGisVergleichsanzeige.Schreibliste(einzel);
        Assert.Contains(vorher, z => z.Feld == "Zustand" && z.Ergebnis == "geplant" && z.Objekt == "Haltung H1");
        Assert.DoesNotContain(vorher, z => z.Objekt == "Haltung H2");

        await useCase.FuehreAusAsync(einzel, probelauf: false);
        WebGisPlanAusschnitt.Ersetze(gesamt, einzel, WebGisObjektart.Haltung, p.Data[0].Id);

        Assert.Single(client.Geschrieben);
        Assert.Equal("G1", client.Geschrieben[0].GlobalId);
        var nachher = WebGisVergleichsanzeige.Schreibliste(einzel, nachDemSchreiben: true);
        Assert.Contains(nachher, z => z.Feld == "Zustand" && z.Ergebnis == "bestätigt");
        Assert.Equal(new[] { "H1", "H2" }, gesamt.Positionen.Select(x => x.Bezeichnung));
        var objekt = WebGisVergleichsanzeige.Objekte(gesamt).Single(o => o.Name == "H1");
        Assert.Equal("geschrieben", objekt.Chip);
        Assert.Contains(objekt.Zeilen, z => z.Feld == "Zustand" && z.Aktion == "im WebGIS bestätigt");
    }

    [Fact]
    public void Haken_eines_vorschlags_zaehlt_als_aenderung_und_kommt_im_plan_an()
    {
        var pos = new WebGisExportPosition { Objektart = WebGisObjektart.Schacht, Bezeichnung = "80478", GlobalId = "G", RecordId = Guid.NewGuid() };
        var vorschlag = new WebGisVorschlag
        {
            Feld = "Tiefe", Anzeige = "Tiefe [m]", AltText = "1.85", NeuText = "1.96",
            Aenderungen = { new WebGisFeldAenderung { RefId = "r-tiefe", Feld = "Tiefe [m]", Alt = "1.85", Neu = "1.96" } },
        };
        pos.Vorschlaege.Add(vorschlag);
        pos.Vergleich.Add(new WebGisFeldVergleich
        {
            Feld = "Tiefe [m]", SewerStudio = "1.96", WebGis = "1.85", Art = WebGisVergleichsArt.Vorschlag,
            Nachher = "1.96", RefId = "r-tiefe", Vorschlag = vorschlag,
        });
        var plan = new WebGisExportPlan();
        plan.Positionen.Add(pos);

        var objekt = WebGisVergleichsanzeige.Objekte(plan).Single();
        Assert.Equal("keine Änderung", objekt.Chip);

        objekt.Zeilen.Single().Mitschreiben = true;

        Assert.True(vorschlag.Gewaehlt);
        Assert.Equal("1 Änderung", objekt.Chip);
        Assert.Single(WebGisVergleichsanzeige.Schreibliste(plan));
    }
}
