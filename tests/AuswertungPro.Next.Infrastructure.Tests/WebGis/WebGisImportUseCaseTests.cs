using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.WebGis;
using AuswertungPro.Next.Domain.Models;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.WebGis;

/// <summary>Holen WebGIS -> SewerStudio als ganzer Ablauf: Felder und Sanierungsmassnahmen (23.09.2026).</summary>
public sealed class WebGisImportUseCaseTests
{
    private const string StatusRef = "7ecf9743-e8df-c35d-fdce-1a188b72bef8";

    private sealed class FakeClient : IGeonisWebGisClient
    {
        public Func<string, WebGisLesestand?> Massnahme { get; set; } = _ => null;
        public int MassnahmenGelesen { get; private set; }

        public int NamensSuchen { get; private set; }
        public int IdLesungen { get; private set; }

        /// <summary>Steht fuer «Geändert am (UTC)» der Maske: aendert sich, wenn jemand im WebGIS bearbeitet.</summary>
        public string AenderungsDatum { get; set; } = "2026-09-20T08:00:00Z";

        public Task<WebGisLesestand?> LeseAsync(WebGisObjektart art, string bezeichnung, CancellationToken ct = default)
        {
            NamensSuchen++;
            return Task.FromResult<WebGisLesestand?>(Stand("G-" + bezeichnung, bezeichnung));
        }

        /// <summary>Wie der Server: hinter «G-&lt;Name&gt;» steht das Objekt mit diesem Namen.</summary>
        public Task<WebGisLesestand?> LeseUeberGlobalIdAsync(WebGisObjektart art, string globalId, CancellationToken ct = default)
        {
            IdLesungen++;
            return Task.FromResult<WebGisLesestand?>(
                globalId.StartsWith("G-", StringComparison.Ordinal) ? Stand(globalId, globalId[2..]) : null);
        }

        private WebGisLesestand Stand(string globalId, string name)
        {
            var s = new WebGisLesestand { GlobalId = globalId, Bezeichnung = name };
            s.Felder[GeaendertAmRef] = AenderungsDatum;
            s.Felder[StatusRef] = "1";
            s.Kataloge[StatusRef] = new List<(string, string)> { ("1", "In Betrieb") };
            s.Sanierungen.Add(new WebGisSanierungZeile { GlobalId = "M1", Art = "Renovierung", Status = "Ausgeführt", Verfahren = "Schlauchverfahren" });
            return s;
        }

        public Task<WebGisLesestand?> LeseMassnahmeAsync(string globalId, CancellationToken ct = default)
        {
            MassnahmenGelesen++;
            return Task.FromResult(Massnahme(globalId));
        }

        public Task<WebGisSchreibErgebnis> SchreibeAsync(WebGisObjektart art, string globalId, IReadOnlyDictionary<string, string> felder, CancellationToken ct = default, IReadOnlyDictionary<string, string?>? erwarteterStand = null)
            => throw new InvalidOperationException("Holen darf nie ins WebGIS schreiben.");
        public Task<WebGisSanierungKatalog?> LeseSanierungKatalogAsync(WebGisObjektart art, string elternGlobalId, CancellationToken ct = default)
            => Task.FromResult<WebGisSanierungKatalog?>(null);
        public Task<WebGisSchreibErgebnis> ErstelleSanierungAsync(WebGisObjektart art, string elternGlobalId, IReadOnlyDictionary<string, string> felder, CancellationToken ct = default)
            => throw new InvalidOperationException("Holen darf nie ins WebGIS schreiben.");
        public Task<IReadOnlyList<(string Key, string Text)>?> LeseKatalogListeAsync(WebGisObjektart art, string refId, string filter, string? subtyp = null, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<(string Key, string Text)>?>(null);
    }

    private static WebGisLesestand Renovierung()
    {
        var s = new WebGisLesestand { GlobalId = "M1", Bezeichnung = "" };
        void Combo(string refId, string key, string text)
        {
            s.Felder[refId] = key;
            s.Kataloge[refId] = new List<(string, string)> { (key, text) };
        }
        Combo(WebGisSanierungFeldkarte.ArtRef, "4", "Renovierung");
        Combo(WebGisSanierungFeldkarte.StatusRef, "1", "Ausgeführt");
        Combo(WebGisSanierungFeldkarte.VerfahrenRef, "27", "Schlauchverfahren");
        return s;
    }

    private static (Project, HaltungRecord) Projekt()
    {
        var h = new HaltungRecord();
        h.SetFieldValue(FieldKeys.HoldingName, "80480-80478", FieldSource.Manual, false);
        var p = new Project();
        p.Data.Add(h);
        return (p, h);
    }

    [Fact]
    public async Task Holen_fuellt_felder_und_legt_fehlende_massnahmen_an()
    {
        var (p, h) = Projekt();
        var client = new FakeClient { Massnahme = gid => gid == "M1" ? Renovierung() : null };
        var useCase = new WebGisImportUseCase(client);

        var plan = await useCase.BauePlanAsync(p);
        WebGisImportUseCase.Uebernimm(plan, p);

        Assert.Equal("In Betrieb", h.GetFieldValue(FieldKeys.OperatingStatus)); // WebGIS-Text woertlich (Schritt A, 23.09.2026)
        Assert.Equal("G-80480-80478", h.WebGisGlobalId);
        var akte = Assert.Single(p.Objektakten);
        Assert.Equal("Renovierung", akte.Werte[WebGisSanierungFeldkarte.AkteArt].Text);
        Assert.Contains(h.Id, akte.Bezuege);

        // Zweiter Lauf: die Massnahme ist da und wird nicht einmal mehr gelesen.
        var gelesenVorher = client.MassnahmenGelesen;
        var zweiter = await useCase.BauePlanAsync(p);
        Assert.Empty(zweiter.Sanierungen);
        Assert.Equal(gelesenVorher, client.MassnahmenGelesen);
    }

    [Fact]
    public async Task Eindeutige_globalid_wird_auch_ohne_feldaenderung_gespeichert()
    {
        var (projekt, haltung) = Projekt();
        var schacht = new SchachtRecord();
        schacht.SetFieldValue("Schachtnummer", "80461", FieldSource.Manual, false);
        projekt.SchaechteData.Add(schacht);
        haltung.SetFieldValue(FieldKeys.OperatingStatus, "in_Betrieb", FieldSource.Manual, true);
        schacht.SetFieldValue("Status", "in_Betrieb", FieldSource.Manual, true);

        var plan = await new WebGisImportUseCase(new FakeClient()).BauePlanAsync(projekt);
        Assert.Equal(2, WebGisImportUseCase.Uebernimm(plan, projekt));
        Assert.Equal("G-80480-80478", haltung.WebGisGlobalId);
        Assert.Equal("G-80461", schacht.WebGisGlobalId);
        Assert.Equal("", haltung.GetFieldValue(FieldKeys.CadastreObjectId));

        var gespeichert = JsonSerializer.Deserialize<Project>(JsonSerializer.Serialize(projekt));
        Assert.Equal(haltung.WebGisGlobalId, gespeichert!.Data.Single().WebGisGlobalId);
        Assert.Equal(schacht.WebGisGlobalId, gespeichert.SchaechteData.Single().WebGisGlobalId);
    }

    [Fact]
    public async Task Mit_gespeicherter_globalid_holt_das_programm_ohne_namenssuche()
    {
        var (projekt, haltung) = Projekt();
        haltung.WebGisGlobalId = "G-80480-80478";
        var client = new FakeClient();

        var plan = await new WebGisImportUseCase(client).BauePlanAsync(projekt);

        var pos = plan.Positionen.Single();
        Assert.Empty(pos.Sperren);
        Assert.Equal("G-80480-80478", pos.GlobalId);
        Assert.Equal(0, client.NamensSuchen);
        Assert.Equal(1, client.IdLesungen);
    }

    [Fact]
    public async Task Nicht_lesbare_gespeicherte_globalid_sperrt_beim_holen_ohne_namenssuche()
    {
        var (projekt, haltung) = Projekt();
        haltung.WebGisGlobalId = "UNBEKANNT";
        var client = new FakeClient();

        var plan = await new WebGisImportUseCase(client).BauePlanAsync(projekt);

        Assert.Contains("gespeicherten GlobalID", Assert.Single(plan.Positionen.Single().Sperren));
        Assert.Equal(0, client.NamensSuchen);
    }

    [Fact]
    public async Task Nicht_lesbare_massnahme_wird_gemeldet_nicht_erfunden()
    {
        var (p, _) = Projekt();
        var plan = await new WebGisImportUseCase(new FakeClient()).BauePlanAsync(p);

        Assert.Empty(plan.Sanierungen);
        Assert.Contains(plan.Positionen.Single().Hinweise, h => h.Contains("Sanierungsmassnahme") && h.Contains("nicht lesbar"));
        Assert.Empty(p.Objektakten);
    }

    // ---- Eindeutig heisst auch beim Holen: ein WebGIS-Objekt gehoert zu genau EINEM Datensatz. ----

    [Fact]
    public async Task Zwei_datensaetze_auf_dasselbe_webgis_objekt_bekommen_beim_holen_nichts()
    {
        var (projekt, erste) = Projekt();
        var zweite = new HaltungRecord();
        zweite.SetFieldValue(FieldKeys.HoldingName, "80480-80478", FieldSource.Manual, false); // doppelt im Projekt
        projekt.Data.Add(zweite);
        var client = new FakeClient { Massnahme = gid => gid == "M1" ? Renovierung() : null };

        var plan = await new WebGisImportUseCase(client).BauePlanAsync(projekt);
        WebGisImportUseCase.Uebernimm(plan, projekt);

        Assert.All(plan.Positionen, p => Assert.Contains(p.Sperren, s => s.Contains("dasselbe WebGIS-Objekt")));
        Assert.Empty(plan.Sanierungen);
        Assert.Null(erste.WebGisGlobalId);
        Assert.Null(zweite.WebGisGlobalId);
        Assert.Equal("", erste.GetFieldValue(FieldKeys.OperatingStatus));
    }

    [Fact]
    public void Eine_globalid_wird_nie_an_einen_zweiten_datensatz_vergeben()
    {
        var (projekt, neu) = Projekt();
        var vorhanden = new HaltungRecord { WebGisGlobalId = "G-80480-80478" };
        vorhanden.SetFieldValue(FieldKeys.HoldingName, "Andere", FieldSource.Manual, false);
        projekt.Data.Add(vorhanden);
        var pos = new WebGisImportPosition
        {
            Objektart = WebGisObjektart.Haltung, Bezeichnung = "80480-80478", RecordId = neu.Id, GlobalId = "G-80480-80478",
        };
        pos.Aenderungen.Add(new WebGisImportAenderung { Feld = WebGisImportPlanBuilder.FeldWebGisGlobalId, Neu = "G-80480-80478", Grund = "Test" });
        var plan = new WebGisImportPlan();
        plan.Positionen.Add(pos);

        WebGisImportUseCase.Uebernimm(plan, projekt);

        Assert.Null(neu.WebGisGlobalId);
    }

    [Fact]
    public async Task Eine_nach_der_vorschau_entstandene_dublette_stoppt_die_ganze_position()
    {
        // Audit A07 (23.09.2026): Nicht nur die Kennung, auch Fachwerte und Akten bleiben aus.
        var (p, h) = Projekt();
        var useCase = new WebGisImportUseCase(new FakeClient());
        var plan = await useCase.BauePlanAsync(p);
        var gid = plan.Positionen.Find(x => x.RecordId == h.Id)!.GlobalId;
        Assert.NotNull(gid);
        var zweiter = new HaltungRecord { WebGisGlobalId = gid };
        zweiter.SetFieldValue(FieldKeys.HoldingName, "Andere", FieldSource.Manual, false);
        p.Data.Add(zweiter);

        var ergebnis = await useCase.UebernimmGeprueftAsync(plan, p);

        Assert.Equal(1, ergebnis.Gestoppt);
        Assert.Null(h.WebGisGlobalId);
        Assert.NotEqual("In Betrieb", h.GetFieldValue(FieldKeys.OperatingStatus));
        Assert.Contains(plan.Positionen.Find(x => x.RecordId == h.Id)!.Sperren, s => s.Contains(WebGisEindeutigkeit.Kennwort));
    }

    // ---- Entscheid Pascal 23.09.2026 abends: Hat sich ein Objekt im WebGIS zwischen Vorschau und
    // «Übernehmen» geaendert (Aenderungsdatum), wird es NICHT uebernommen, sondern neu geprueft. ----

    private const string GeaendertAmRef = "ffffffff-0000-0000-0000-00000000aa01";

    [Fact]
    public async Task Unveraendertes_objekt_wird_nach_erneutem_lesen_uebernommen()
    {
        var (p, h) = Projekt();
        var useCase = new WebGisImportUseCase(new FakeClient());
        var plan = await useCase.BauePlanAsync(p);

        var ergebnis = await useCase.UebernimmGeprueftAsync(plan, p);

        Assert.Equal(0, ergebnis.Gestoppt);
        Assert.True(ergebnis.Uebernommen > 0);
        Assert.Equal("In Betrieb", h.GetFieldValue(FieldKeys.OperatingStatus));
    }

    [Fact]
    public async Task Im_webgis_seit_der_vorschau_geaendertes_objekt_wird_nicht_uebernommen()
    {
        var (p, h) = Projekt();
        var client = new FakeClient();
        var useCase = new WebGisImportUseCase(client);
        var plan = await useCase.BauePlanAsync(p);

        client.AenderungsDatum = "2026-09-23T17:45:00Z"; // jemand hat im WebGIS bearbeitet
        var ergebnis = await useCase.UebernimmGeprueftAsync(plan, p);

        Assert.Equal(1, ergebnis.Gestoppt);
        Assert.Equal(0, ergebnis.Uebernommen);
        Assert.Equal("", h.GetFieldValue(FieldKeys.OperatingStatus));
        Assert.Null(h.WebGisGlobalId);
        Assert.Contains(plan.Positionen.Single().Sperren, s => s.Contains("seit der Vorschau geändert"));
    }

    [Fact]
    public async Task Im_webgis_geaenderte_massnahme_wird_nicht_angelegt()
    {
        static WebGisLesestand RenovierungVom(string datum)
        {
            var m = Renovierung();
            m.Felder[GeaendertAmRef] = datum;
            return m;
        }
        var (p, _) = Projekt();
        var datum = "2026-09-20T08:00:00Z";
        var client = new FakeClient { Massnahme = gid => gid == "M1" ? RenovierungVom(datum) : null };
        var useCase = new WebGisImportUseCase(client);
        var plan = await useCase.BauePlanAsync(p);
        Assert.Single(plan.Sanierungen);

        datum = "2026-09-23T17:45:00Z";
        var ergebnis = await useCase.UebernimmGeprueftAsync(plan, p);

        Assert.Empty(p.Objektakten);
        Assert.True(ergebnis.Gestoppt >= 1);
    }
}
