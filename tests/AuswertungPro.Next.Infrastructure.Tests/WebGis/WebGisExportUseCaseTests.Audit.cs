using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.WebGis;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.WebGis;

/// <summary>
/// Gesamtaudit 23.09.2026 (A04-A06): Die Schutzregeln muessen bis zum letzten Schreibschritt gelten, eine
/// gesperrte Haltung bekommt keine Massnahme, und ein vom Server bestaetigtes Schreiben bleibt bestaetigt,
/// auch wenn danach die Gegenprobe scheitert.
/// </summary>
public sealed partial class WebGisExportUseCaseTests
{
    private static WebGisLesestand HaltungMitZustand(string zustand, string gid = "G1", string name = "H1")
        => new()
        {
            GlobalId = gid, Bezeichnung = name,
            Felder = new Dictionary<string, string?>(StringComparer.Ordinal) { [WebGisFeldkarte.HaltungZustandRef] = zustand },
        };

    /// <summary>Plan mit einer Haltung (optional mit Zustandsaenderung) und einer Massnahme daran.</summary>
    private static (WebGisExportPlan Plan, WebGisExportPosition Eltern, WebGisSanierungPosition Massnahme) PlanMitMassnahme(bool mitFeldaenderung)
    {
        var elternId = Guid.NewGuid();
        var eltern = new WebGisExportPosition
        {
            Objektart = WebGisObjektart.Haltung, Bezeichnung = "H1", GlobalId = "G1", RecordId = elternId,
            GelesenerStand = new Dictionary<string, string?>(StringComparer.Ordinal) { [WebGisFeldkarte.HaltungZustandRef] = "102" },
        };
        if (mitFeldaenderung)
            eltern.Aenderungen.Add(new WebGisFeldAenderung { RefId = WebGisFeldkarte.HaltungZustandRef, Feld = "Zustand", Alt = "102", Neu = "104" });
        var san = new WebGisSanierungPosition
        {
            Objektart = WebGisObjektart.Haltung, ElternBezeichnung = "H1", ElternGlobalId = "G1", ElternRecordId = elternId,
        };
        san.Felder["x"] = "1";
        var plan = new WebGisExportPlan();
        plan.Positionen.Add(eltern);
        plan.Sanierungen.Add(san);
        return (plan, eltern, san);
    }

    [Fact]
    public async Task Der_bestaetigte_stand_geht_bis_zum_letzten_schreibschritt_mit()
    {
        var (plan, eltern, _) = PlanMitMassnahme(mitFeldaenderung: true);
        var client = new FakeClient { Lese = (_, _) => HaltungMitZustand("102") };

        await new WebGisExportUseCase(client).FuehreAusAsync(plan, probelauf: false);

        Assert.True(eltern.Geschrieben, eltern.SchreibFehler);
        Assert.NotNull(client.LetzterErwarteterStand);
        Assert.Equal("102", client.LetzterErwarteterStand![WebGisFeldkarte.HaltungZustandRef]);
    }

    [Fact]
    public async Task Gesperrte_haltung_bekommt_keine_massnahme()
    {
        var (plan, eltern, san) = PlanMitMassnahme(mitFeldaenderung: true);
        var client = new FakeClient { Lese = (_, _) => HaltungMitZustand("105") }; // im WebGIS inzwischen geaendert

        await new WebGisExportUseCase(client).FuehreAusAsync(plan, probelauf: false);

        Assert.False(eltern.Geschrieben);
        Assert.NotNull(eltern.SchreibFehler);
        Assert.Equal(0, client.MassnahmenAngelegt);
        Assert.False(san.Geschrieben);
        Assert.Contains("nicht angelegt", san.SchreibFehler);
    }

    [Fact]
    public async Task Nur_massnahme_und_geaendertes_elternobjekt_legt_nichts_an()
    {
        var (plan, _, san) = PlanMitMassnahme(mitFeldaenderung: false);
        var client = new FakeClient { Lese = (_, _) => HaltungMitZustand("105") };

        await new WebGisExportUseCase(client).FuehreAusAsync(plan, probelauf: false);

        Assert.Equal(0, client.MassnahmenAngelegt);
        Assert.Contains("seit der Prüfung geändert", san.SchreibFehler);
    }

    [Fact]
    public async Task Nur_massnahme_und_unveraendertes_elternobjekt_legt_sie_an()
    {
        var (plan, _, san) = PlanMitMassnahme(mitFeldaenderung: false);
        var client = new FakeClient { Lese = (_, _) => HaltungMitZustand("102") };

        await new WebGisExportUseCase(client).FuehreAusAsync(plan, probelauf: false);

        Assert.Equal(1, client.MassnahmenAngelegt);
        Assert.True(san.Geschrieben, san.SchreibFehler);
    }

    [Fact]
    public async Task Eben_geschriebenes_elternobjekt_bekommt_seine_massnahme()
    {
        // Die eigene Schreibung aendert den Stand; sie darf die Massnahme nicht sperren.
        var (plan, eltern, san) = PlanMitMassnahme(mitFeldaenderung: true);
        var client = new FakeClient { Lese = (_, _) => HaltungMitZustand("102") };

        await new WebGisExportUseCase(client).FuehreAusAsync(plan, probelauf: false);

        Assert.True(eltern.Geschrieben, eltern.SchreibFehler);
        Assert.Equal(1, client.MassnahmenAngelegt);
        Assert.True(san.Geschrieben, san.SchreibFehler);
    }

    [Fact]
    public async Task Vom_server_bestaetigt_aber_ein_feld_verworfen_bekommt_trotzdem_die_massnahme()
    {
        // Der Server nimmt an und verwirft ein Feld (Tiefe ohne Koten): Das Objekt ist nicht gesperrt.
        var (plan, eltern, san) = PlanMitMassnahme(mitFeldaenderung: true);
        var client = new FakeClient { Lese = (_, _) => HaltungMitZustand("102"), SchreibenWirkt = false };

        await new WebGisExportUseCase(client).FuehreAusAsync(plan, probelauf: false);

        Assert.Contains("nicht übernommen", eltern.SchreibFehler);
        Assert.Equal(1, client.MassnahmenAngelegt);
        Assert.True(san.Geschrieben, san.SchreibFehler);
    }

    [Fact]
    public async Task Zweite_massnahme_desselben_objekts_wird_ebenfalls_angelegt()
    {
        var (plan, _, san) = PlanMitMassnahme(mitFeldaenderung: false);
        var zweite = new WebGisSanierungPosition
        {
            Objektart = WebGisObjektart.Haltung, ElternBezeichnung = "H1", ElternGlobalId = "G1", ElternRecordId = san.ElternRecordId,
        };
        zweite.Felder["x"] = "2";
        plan.Sanierungen.Add(zweite);
        var client = new FakeClient { Lese = (_, _) => HaltungMitZustand("102") };

        await new WebGisExportUseCase(client).FuehreAusAsync(plan, probelauf: false);

        Assert.Equal(2, client.MassnahmenAngelegt);
    }

    [Fact]
    public async Task Abgelaufene_sitzung_bei_der_nachkontrolle_laesst_das_bestaetigte_schreiben_stehen()
    {
        var pos = ZustandsPosition("H1", "G1");
        var plan = new WebGisExportPlan();
        plan.Positionen.Add(pos);
        var lesungen = 0;
        var client = new FakeClient
        {
            Lese = (_, _) => ++lesungen == 1 ? HaltungMitZustand("102") : throw new WebGisSitzungException("Token abgelaufen"),
        };
        var gemeldet = new List<(bool Geschrieben, string? Fehler)>();

        await Assert.ThrowsAsync<WebGisSitzungException>(() => new WebGisExportUseCase(client).FuehreAusAsync(
            plan, probelauf: false, nachObjekt: p => gemeldet.Add((p.Geschrieben, p.SchreibFehler))));

        Assert.True(pos.Geschrieben);
        Assert.Null(pos.SchreibFehler);
        Assert.Contains(pos.Hinweise, h => h.Contains("nicht nachgeprüft"));
        Assert.Equal(new[] { (true, (string?)null) }, gemeldet);
    }

    [Fact]
    public async Task Zeitueberschreitung_meldet_die_position_und_bricht_ab()
    {
        var p1 = ZustandsPosition("H1", "G1");
        var p2 = ZustandsPosition("H2", "G2");
        var plan = new WebGisExportPlan();
        plan.Positionen.Add(p1); plan.Positionen.Add(p2);
        var client = new FakeClient
        {
            Lese = (_, b) => HaltungMitZustand("102", b == "H1" ? "G1" : "G2", b),
            Schreibe = (_, _) => throw new TaskCanceledException("The request was canceled due to the configured HttpClient.Timeout"),
        };
        var gemeldet = new List<string>();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new WebGisExportUseCase(client).FuehreAusAsync(
            plan, probelauf: false, nachObjekt: p => gemeldet.Add(p.Bezeichnung)));

        Assert.Equal(new[] { "H1" }, gemeldet);
        Assert.Contains("Zeitüberschreitung", p1.SchreibFehler);
    }

    // ---- A09: Material der Kanalfirma in einer anderen WebGIS-Gruppe ----

    private static List<(string, string)>? BetonListe(string refId, string filter)
        => refId == MaterialDetailRef && filter == "1"
            ? new List<(string, string)> { ("101", "Beton, unbekannt"), ("104", "Beton, Fertigteil") }
            : null;

    [Fact]
    public async Task Material_der_kanalfirma_in_anderer_gruppe_wird_nachgeladen_und_vorgeschlagen()
    {
        var e = Schacht();
        e.Kanalfirmenwerte["Material"] = "Beton, Fertigteil";
        var stand = SchachtStandMitMaterial(("301", "Kunststoff, PVC")); // die Maske zeigt nur Kunststoff
        var client = new FakeClient { KatalogListe = (_, refId, filter) => BetonListe(refId, filter) };

        await new WebGisExportUseCase(client).ErgaenzeGruppenKatalogeAsync(e, stand);
        var pos = WebGisExportPlanBuilder.Baue(e, stand);

        Assert.DoesNotContain(pos.Aenderungen, a => a.RefId == MaterialDetailRef || a.RefId == MaterialHauptRef);
        var v = Assert.Single(pos.Vorschlaege);
        Assert.False(v.Gewaehlt);
        Assert.Contains(v.Aenderungen, a => a.RefId == MaterialDetailRef && a.Neu == "104");
        Assert.Contains(v.Aenderungen, a => a.RefId == MaterialHauptRef && a.Neu == "1");
    }

    [Fact]
    public async Task Gruppenlisten_werden_im_selben_lauf_nur_einmal_geladen()
    {
        var client = new FakeClient { KatalogListe = (_, refId, filter) => BetonListe(refId, filter) };
        var useCase = new WebGisExportUseCase(client);
        for (var i = 0; i < 3; i++)
        {
            var e = Schacht();
            e.Kanalfirmenwerte["Material"] = "Beton, Fertigteil";
            await useCase.ErgaenzeGruppenKatalogeAsync(e, SchachtStandMitMaterial(("301", "Kunststoff, PVC")));
        }

        Assert.Equal(1, client.KatalogListenAufrufe);
    }

    [Fact]
    public async Task Abbruch_durch_den_benutzer_bleibt_ein_abbruch()
    {
        var pos = ZustandsPosition("H1", "G1");
        var plan = new WebGisExportPlan();
        plan.Positionen.Add(pos);
        using var cts = new System.Threading.CancellationTokenSource();
        var client = new FakeClient
        {
            Lese = (_, _) => { cts.Cancel(); throw new OperationCanceledException(cts.Token); },
        };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new WebGisExportUseCase(client).FuehreAusAsync(plan, probelauf: false, ct: cts.Token));

        Assert.Null(pos.SchreibFehler); // kein Zeitueberschreitungstext bei bewusstem Abbruch
    }
}
