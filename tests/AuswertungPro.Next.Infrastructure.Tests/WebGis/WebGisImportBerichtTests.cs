using System;
using AuswertungPro.Next.Application.WebGis;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.WebGis;

/// <summary>Vorschau des Holens im bestehenden Vorschaufenster (23.09.2026).</summary>
public sealed class WebGisImportBerichtTests
{
    [Fact]
    public void Vorschau_zeigt_felder_massnahmen_und_hinweise()
    {
        var plan = new WebGisImportPlan();
        var pos = new WebGisImportPosition { Objektart = WebGisObjektart.Haltung, Bezeichnung = "80480-80478" };
        pos.Aenderungen.Add(new WebGisImportAenderung { Feld = "Status", Alt = null, Neu = "in_Betrieb", Grund = "leer" });
        pos.Hinweise.Add("Status: «x» passt zu keinem SewerStudio-Wert");
        plan.Positionen.Add(pos);
        var imp = new WebGisSanierungImport
        {
            Objektart = WebGisObjektart.Haltung, ElternBezeichnung = "80480-80478", WebGisGlobalId = "M1", ElternRecordId = Guid.NewGuid(),
        };
        imp.Werte.Add(new WebGisAkteWert(WebGisSanierungFeldkarte.AkteArt, "Renovierung", null, null));
        plan.Sanierungen.Add(imp);

        var v = WebGisImportBericht.Vorschau(plan);

        Assert.False(v.IstFehler);
        Assert.Contains(v.Zeilen, z => z.Objekt.Contains("80480-80478") && z.Feld == "Status" && z.Neu == "in_Betrieb");
        Assert.Contains(v.Zeilen, z => z.Feld.Contains("Sanierungsmassnahme") && z.Neu.Contains("Renovierung"));
        Assert.Contains(v.Warnungen, w => w.Contains("passt zu keinem"));
        Assert.Contains("1 Sanierungsmassnahme", v.Zusammenfassung);
    }

    [Fact]
    public void Ohne_etwas_zu_holen_ist_die_vorschau_leer_markiert()
    {
        var v = WebGisImportBericht.Vorschau(new WebGisImportPlan());

        Assert.True(v.IstFehler);
        Assert.Empty(v.Zeilen);
    }

    [Fact]
    public void Zeilen_tragen_objektart_und_datensatz_fuer_den_doppelklick()
    {
        var id = Guid.NewGuid();
        var plan = new WebGisImportPlan();
        var pos = new WebGisImportPosition { Objektart = WebGisObjektart.Schacht, Bezeichnung = "80461", RecordId = id };
        pos.Aenderungen.Add(new WebGisImportAenderung { Feld = "schacht.materialgruppe", Neu = "Beton", Grund = "x" });
        pos.Hinweise.Add("Material: «Beton, vorgespannt» passt zu keinem SewerStudio-Wert");
        plan.Positionen.Add(pos);

        var z = Assert.Single(WebGisImportBericht.Zeilen(plan));
        Assert.Equal(WebGisObjektart.Schacht, z.Objektart);
        Assert.Equal(id, z.RecordId);
        Assert.Equal("Materialgruppe", z.Feld);
        var h = Assert.Single(WebGisImportBericht.NichtZugeordnet(plan));
        Assert.Equal(id, h.RecordId);
    }

    // Wunsch Pascal 23.09.2026: Gesperrtes und nicht Zugeordnetes muss klar gekennzeichnet sein —
    // nicht in einer zugeklappten Hinweisliste unter einem gruenen Band.

    private static WebGisImportPlan PlanMitSperreUndHinweis()
    {
        var plan = new WebGisImportPlan();
        var gefunden = new WebGisImportPosition { Objektart = WebGisObjektart.Haltung, Bezeichnung = "80638-80631", RecordId = Guid.NewGuid() };
        gefunden.Aenderungen.Add(new WebGisImportAenderung { Feld = WebGisImportPlanBuilder.FeldWebGisGlobalId, Neu = "G1", Grund = "x" });
        gefunden.Hinweise.Add("Rohrmaterial: «Schleuderbeton (SBR)» passt zu keinem SewerStudio-Wert — nicht übernommen.");
        plan.Positionen.Add(gefunden);
        var gesperrt = new WebGisImportPosition { Objektart = WebGisObjektart.Haltung, Bezeichnung = "81156-81157", RecordId = Guid.NewGuid() };
        gesperrt.Sperren.Add("Im WebGIS nicht eindeutig gefunden (kein oder mehrdeutiger Treffer).");
        plan.Positionen.Add(gesperrt);
        return plan;
    }

    [Fact]
    public void Nicht_zugeordnet_nennt_gesperrte_objekte_zuerst_mit_grund()
    {
        var liste = WebGisImportBericht.NichtZugeordnet(PlanMitSperreUndHinweis());

        Assert.Equal(2, liste.Count);
        Assert.Equal("Haltung 81156-81157", liste[0].Objekt);
        Assert.Equal(WebGisImportBericht.ArtObjektGesperrt, liste[0].Feld);
        Assert.Contains("nicht eindeutig gefunden", liste[0].Neu);
        Assert.Equal("Haltung 80638-80631", liste[1].Objekt);
        Assert.Equal(WebGisImportBericht.ArtWertNichtUebernommen, liste[1].Feld);
        Assert.Contains("Schleuderbeton", liste[1].Neu);
    }

    [Fact]
    public void Gesperrte_sanierungsmassnahme_steht_bei_den_gesperrten()
    {
        var plan = PlanMitSperreUndHinweis();
        var imp = new WebGisSanierungImport
        {
            Objektart = WebGisObjektart.Schacht, ElternBezeichnung = "80409", WebGisGlobalId = "M1", ElternRecordId = Guid.NewGuid(),
        };
        imp.Sperren.Add("Art der Massnahme fehlt im WebGIS — Massnahme nicht übernommen.");
        plan.Sanierungen.Add(imp);

        var liste = WebGisImportBericht.NichtZugeordnet(plan);

        Assert.Equal(WebGisImportBericht.ArtMassnahmeGesperrt, liste[1].Feld);
        Assert.Equal("Schacht 80409", liste[1].Objekt);
        Assert.Equal(WebGisImportBericht.ArtWertNichtUebernommen, liste[2].Feld);
    }

    [Fact]
    public void Kopf_warnt_bei_gesperrten_objekten_und_nicht_zugeordneten_werten()
    {
        var kopf = WebGisImportBericht.Kopf(PlanMitSperreUndHinweis());

        Assert.True(kopf.Warnung);
        Assert.Contains("1 Objekt gesperrt", kopf.Warntext);
        Assert.Contains("1 Wert nicht zugeordnet", kopf.Warntext);
        Assert.Contains("nicht übernommen", kopf.Warntext);
    }

    [Fact]
    public void Kopf_zaehlt_in_der_mehrzahl()
    {
        var plan = PlanMitSperreUndHinweis();
        plan.Positionen[0].Hinweise.Add("DN: Breite 300 und Höhe 450 im WebGIS verschieden — DN nicht übernommen.");
        var zweite = new WebGisImportPosition { Objektart = WebGisObjektart.Schacht, Bezeichnung = "81157" };
        zweite.Sperren.Add("Im WebGIS nicht eindeutig gefunden (kein oder mehrdeutiger Treffer).");
        plan.Positionen.Add(zweite);

        var kopf = WebGisImportBericht.Kopf(plan);

        Assert.Contains("2 Objekte gesperrt", kopf.Warntext);
        Assert.Contains("2 Werte nicht zugeordnet", kopf.Warntext);
    }

    [Fact]
    public void Bericht_nennt_gesperrtes_und_nicht_zugeordnetes_zuoberst()
    {
        // Lauf 19:26 in Zone 1.15: die 10 gesperrten Haltungen standen mitten zwischen 172 anderen.
        var plan = PlanMitSperreUndHinweis();
        plan.Hinweise.Add("2 Objekte gelesen: 1 mit Uebernahme, 1 gesperrt.");

        var text = WebGisImportBericht.Details(plan);

        var block = text.IndexOf("NICHT ZUGEORDNET", StringComparison.Ordinal);
        var gesperrt = text.IndexOf("GESPERRT   Haltung 81156-81157", StringComparison.Ordinal);
        var wert = text.IndexOf("WERT       Haltung 80638-80631", StringComparison.Ordinal);
        var ersteObjektzeile = text.IndexOf("\nHaltung 80638-80631", StringComparison.Ordinal);
        Assert.True(block >= 0, text);
        Assert.True(block < gesperrt && gesperrt < wert && wert < ersteObjektzeile, text);
        Assert.Contains("(2)", text[block..gesperrt]);
    }

    [Fact]
    public void Bericht_ohne_sperren_hat_keinen_roten_block()
        => Assert.DoesNotContain("NICHT ZUGEORDNET", WebGisImportBericht.Details(new WebGisImportPlan()));

    [Fact]
    public void Kopf_ohne_sperren_und_hinweise_ist_keine_warnung()
    {
        var plan = new WebGisImportPlan();
        var pos = new WebGisImportPosition { Objektart = WebGisObjektart.Haltung, Bezeichnung = "H1" };
        pos.Aenderungen.Add(new WebGisImportAenderung { Feld = "Status", Neu = "In Betrieb", Grund = "x" });
        plan.Positionen.Add(pos);

        var kopf = WebGisImportBericht.Kopf(plan);

        Assert.False(kopf.Warnung);
        Assert.Equal("", kopf.Warntext);
        Assert.Equal(WebGisImportBericht.Vorschau(plan).Zusammenfassung, kopf.Zusammenfassung);
        Assert.Empty(WebGisImportBericht.NichtZugeordnet(plan));
    }
}
