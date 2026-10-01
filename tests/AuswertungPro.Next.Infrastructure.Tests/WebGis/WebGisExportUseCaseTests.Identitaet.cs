using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.WebGis;
using AuswertungPro.Next.Domain.Models;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.WebGis;

/// <summary>
/// Entscheid Pascal 23.09.2026: Was zurueckgeschrieben wird, muss eindeutig identifiziert sein, und
/// Eigentum, Betreiber, Laenge, Baujahr, GlobalID und Objekt-ID werden im WebGIS nie ueberschrieben.
/// Dazu: Weicht das WebGIS-Objekt (samt Aenderungsdatum) vom gelesenen Stand ab, wird neu geprueft.
/// </summary>
public sealed partial class WebGisExportUseCaseTests
{
    private const string EigentuemerHaltungRef = "fadff6f2-c674-9327-36d8-b2ac79b704cd";
    private const string GeaendertAmRef = "ffffffff-0000-0000-0000-00000000aa01"; // steht fuer «Geändert am (UTC)»

    private static HaltungRecord Haltung(string name, string? globalId = null)
    {
        var h = new HaltungRecord { WebGisGlobalId = globalId };
        h.SetFieldValue(FieldKeys.HoldingName, name, FieldSource.Manual, false);
        h.SetFieldValue(FieldKeys.ConditionClass, "4", FieldSource.Manual, true);
        return h;
    }

    private static WebGisLesestand StandMitDatum(string gid, string name, string geaendertAm)
        => new()
        {
            GlobalId = gid, Bezeichnung = name,
            Felder = new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                [WebGisFeldkarte.HaltungZustandRef] = "102",
                [GeaendertAmRef] = geaendertAm,
            },
        };

    [Fact]
    public async Task Zwei_projekt_haltungen_auf_dasselbe_webgis_objekt_werden_beide_gesperrt()
    {
        // Dieselbe Haltung zweimal im Projekt (etwa doppelt importiert): Beide faenden dasselbe
        // WebGIS-Objekt, und das zweite Schreiben mischte sich in das erste.
        var projekt = new Project();
        projekt.Data.Add(Haltung("80638-80631"));
        projekt.Data.Add(Haltung("80638-80631"));
        var client = new FakeClient { Lese = (_, name) => HaltungStand("G1", name) };

        var plan = await new WebGisExportUseCase(client).BauePlanAsync(projekt);

        Assert.All(plan.Positionen, p =>
        {
            Assert.False(p.Schreibbar);
            Assert.Contains(p.Sperren, s => s.Contains("dasselbe WebGIS-Objekt"));
        });
    }

    [Fact]
    public async Task Gleiche_gespeicherte_globalid_an_zwei_haltungen_sperrt_beide()
    {
        var projekt = new Project();
        projekt.Data.Add(Haltung("80638-80631", "G1"));
        projekt.Data.Add(Haltung("80640-80638", "g1")); // Gross/Klein ist dieselbe GlobalID
        var client = new FakeClient { LeseUeberId = (_, id) => HaltungStand(id.ToUpperInvariant(), "80638-80631") };

        var plan = await new WebGisExportUseCase(client).BauePlanAsync(projekt);

        Assert.All(plan.Positionen, p => Assert.Contains(p.Sperren, s => s.Contains("dasselbe WebGIS-Objekt")));
    }

    [Fact]
    public async Task Doppelt_zugeordnete_haltung_bekommt_keine_sanierungsmassnahme()
    {
        static Project Projekt(bool doppelt)
        {
            var projekt = new Project();
            var h = Haltung("80638-80631");
            projekt.Data.Add(h);
            if (doppelt) projekt.Data.Add(Haltung("80638-80631"));
            projekt.Objektakten.Add(new ObjektAkte
            {
                Art = WebGisSaniertKriterium.ArtSanierung, Bezuege = [h.Id],
                Werte = new() { [WebGisSaniertKriterium.StatusFeld] = new() { Text = WebGisSaniertKriterium.StatusAusgefuehrt } },
            });
            return projekt;
        }
        var client = new FakeClient { Lese = (_, name) => HaltungStand("G1", name) };

        var einfach = await new WebGisExportUseCase(client).BauePlanAsync(Projekt(doppelt: false));
        var doppelt = await new WebGisExportUseCase(client).BauePlanAsync(Projekt(doppelt: true));

        Assert.Single(einfach.Sanierungen); // Gegenprobe: ohne Doppel wird die Massnahme geplant
        Assert.Empty(doppelt.Sanierungen);
    }

    [Fact]
    public async Task Verschiedene_haltungen_mit_verschiedenen_globalids_bleiben_schreibbar()
    {
        var projekt = new Project();
        projekt.Data.Add(Haltung("H1"));
        projekt.Data.Add(Haltung("H2"));
        var client = new FakeClient { Lese = (_, name) => HaltungStand("G-" + name, name) };

        var plan = await new WebGisExportUseCase(client).BauePlanAsync(projekt);

        Assert.All(plan.Positionen, p => Assert.True(p.Schreibbar, string.Join(" | ", p.Sperren)));
    }

    [Fact]
    public async Task Geaendertes_aenderungsdatum_im_webgis_sperrt_das_schreiben()
    {
        // Jemand hat das Objekt im WebGIS bearbeitet (ein anderes Feld als die geplanten) —
        // das Aenderungsdatum weicht vom gelesenen ab. Nicht blind schreiben, neu pruefen.
        var datum = "2026-09-20T08:00:00Z";
        var client = new FakeClient { LeseUeberId = (_, id) => StandMitDatum(id, "80638-80631", datum) };
        var useCase = new WebGisExportUseCase(client);
        var plan = await useCase.BauePlanAsync(ProjektMitHaltung("80638-80631", "G1"));
        Assert.True(plan.Positionen.Single().Schreibbar);

        datum = "2026-09-23T17:45:00Z";
        await useCase.FuehreAusAsync(plan, probelauf: false);

        var pos = plan.Positionen.Single();
        Assert.False(pos.Geschrieben);
        Assert.Contains("seit der Prüfung geändert", pos.SchreibFehler);
        Assert.Empty(client.Geschrieben);
    }

    [Fact]
    public async Task Unveraendertes_webgis_objekt_wird_geschrieben()
    {
        var client = new FakeClient { LeseUeberId = (_, id) => StandMitDatum(id, "80638-80631", "2026-09-20T08:00:00Z") };
        var useCase = new WebGisExportUseCase(client);
        var plan = await useCase.BauePlanAsync(ProjektMitHaltung("80638-80631", "G1"));

        await useCase.FuehreAusAsync(plan, probelauf: false);

        Assert.True(plan.Positionen.Single().Geschrieben, plan.Positionen.Single().SchreibFehler);
    }

    [Fact]
    public async Task Vorschau_gilt_nicht_mehr_wenn_das_webgis_objekt_seither_geaendert_wurde()
    {
        // «Jetzt schreiben» baut den Plan frisch und vergleicht ihn mit der bestaetigten Vorschau.
        // Ein anderes Aenderungsdatum heisst: neue Vorschau zeigen, nicht schreiben.
        var datum = "2026-09-20T08:00:00Z";
        var client = new FakeClient { LeseUeberId = (_, id) => StandMitDatum(id, "80638-80631", datum) };
        var useCase = new WebGisExportUseCase(client);
        var projekt = ProjektMitHaltung("80638-80631", "G1");
        var vorschau = await useCase.BauePlanAsync(projekt);

        Assert.True(WebGisPlanVergleich.Gleich(vorschau, await useCase.BauePlanAsync(projekt)));
        datum = "2026-09-23T17:45:00Z";
        Assert.False(WebGisPlanVergleich.Gleich(vorschau, await useCase.BauePlanAsync(projekt)));
    }

    [Fact]
    public async Task Baujahr_des_schachts_geht_nur_in_ein_leeres_webgis_feld()
    {
        var schacht = new SchachtRecord();
        schacht.SetFieldValue("Schachtnummer", "525145", FieldSource.Manual, false);
        schacht.SetFieldValue("Baujahr", "1963", FieldSource.Manual, true);
        var projekt = new Project();
        projekt.SchaechteData.Add(schacht);
        string? imWebGis = "";
        var client = new FakeClient
        {
            Lese = (_, name) => new WebGisLesestand
            {
                GlobalId = "G1", Bezeichnung = name,
                Felder = new Dictionary<string, string?>(StringComparer.Ordinal) { [WebGisFeldkarte.SchachtBaujahrRef] = imWebGis },
            },
        };

        var leer = await new WebGisExportUseCase(client).BauePlanAsync(projekt);
        imWebGis = "1970";
        var gefuellt = await new WebGisExportUseCase(client).BauePlanAsync(projekt);

        Assert.Contains(leer.Positionen.Single().Aenderungen, a => a.RefId == WebGisFeldkarte.SchachtBaujahrRef && a.Neu == "1963");
        Assert.DoesNotContain(gefuellt.Positionen.Single().Aenderungen, a => a.RefId == WebGisFeldkarte.SchachtBaujahrRef);
    }

    [Fact]
    public async Task Geschuetztes_feld_im_plan_wird_nicht_gesendet()
    {
        // Fiele je ein Eigentuemerfeld in den Plan (Fehler in einer Feldkarte), sperrt die letzte
        // Pruefung vor dem Senden das ganze Objekt.
        var client = new FakeClient { LeseUeberId = (_, id) => HaltungStand(id, "80638-80631") };
        var plan = new WebGisExportPlan();
        var pos = new WebGisExportPosition
        {
            Objektart = WebGisObjektart.Haltung, Bezeichnung = "80638-80631", GlobalId = "G1", GespeicherteGlobalId = "G1",
        };
        pos.Aenderungen.Add(new WebGisFeldAenderung { RefId = WebGisFeldkarte.HaltungZustandRef, Feld = "Zustand", Alt = "102", Neu = "104" });
        pos.Aenderungen.Add(new WebGisFeldAenderung { RefId = EigentuemerHaltungRef, Feld = "Eigentümer", Alt = null, Neu = "x" });
        plan.Positionen.Add(pos);

        await new WebGisExportUseCase(client).FuehreAusAsync(plan, probelauf: false);

        Assert.False(pos.Geschrieben);
        Assert.Contains("Eigentümer", pos.SchreibFehler);
        Assert.Empty(client.Geschrieben);
    }
}
