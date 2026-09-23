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
        var h = Assert.Single(WebGisImportBericht.Hinweise(plan));
        Assert.Equal(id, h.RecordId);
    }
}
