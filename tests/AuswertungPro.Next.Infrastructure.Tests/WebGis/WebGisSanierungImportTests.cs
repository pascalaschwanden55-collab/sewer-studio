using System;
using System.Collections.Generic;
using System.Linq;
using AuswertungPro.Next.Application.WebGis;
using AuswertungPro.Next.Domain.Models;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.WebGis;

/// <summary>
/// Sanierungsmassnahmen aus dem WebGIS holen (23.09.2026, Wunsch Pascal): jede Massnahme, die in
/// SewerStudio noch keine Akte hat, wird als Sanierungsakte angelegt — Klartext ueber den Katalog der
/// Akte, nie geraten; dieselbe Massnahme nie doppelt.
/// </summary>
public sealed class WebGisSanierungImportTests
{
    private static WebGisLesestand Massnahme(string art = "Renovierung", string status = "Ausgeführt",
        string verfahren = "Schlauchverfahren", string? jahr = "01.01.2026")
    {
        var s = new WebGisLesestand { GlobalId = "M1", Bezeichnung = "" };
        void Combo(string refId, string key, string text)
        {
            s.Felder[refId] = key;
            s.Kataloge[refId] = new List<(string, string)> { (key, text) };
        }
        Combo(WebGisSanierungFeldkarte.ArtRef, "4", art);
        Combo(WebGisSanierungFeldkarte.StatusRef, "1", status);
        Combo(WebGisSanierungFeldkarte.VerfahrenRef, "27", verfahren);
        Combo(WebGisSanierungFeldkarte.UmfangRef, "1", "Gesamt");
        s.Felder[WebGisSanierungFeldkarte.SanierungsjahrRef] = jahr;
        s.Felder[WebGisSanierungFeldkarte.BezeichnungRef] = "Liner 2026";
        return s;
    }

    private static string? Wert(WebGisSanierungImport imp, string feldId) => imp.Werte.FirstOrDefault(w => w.FeldId == feldId)?.Text;

    [Fact]
    public void Massnahme_wird_auf_die_felder_der_akte_abgebildet()
    {
        var imp = WebGisSanierungImportRegel.Baue(WebGisObjektart.Haltung, Guid.NewGuid(), "H1", "M1", Massnahme());

        Assert.True(imp.Uebernehmbar);
        Assert.Equal("Renovierung", Wert(imp, WebGisSanierungFeldkarte.AkteArt));
        Assert.Equal("Ausgeführt", Wert(imp, WebGisSanierungFeldkarte.AkteStatus));
        Assert.Equal("Schlauchverfahren", Wert(imp, WebGisSanierungFeldkarte.AkteVerfahren));
        Assert.Equal("Gesamt", Wert(imp, WebGisSanierungFeldkarte.AkteUmfang));
        Assert.Equal("2026", Wert(imp, WebGisSanierungFeldkarte.AkteJahr));
        Assert.Equal("Liner 2026", Wert(imp, WebGisSanierungFeldkarte.AkteName));
    }

    [Fact]
    public void Verfahren_muss_zur_art_passen()
    {
        // Schlauchverfahren gehoert zur Renovierung, nicht zur Reparatur.
        var imp = WebGisSanierungImportRegel.Baue(WebGisObjektart.Haltung, Guid.NewGuid(), "H1", "M1",
            Massnahme(art: "Reparatur", verfahren: "Schlauchverfahren"));

        Assert.Null(Wert(imp, WebGisSanierungFeldkarte.AkteVerfahren));
        Assert.Contains(imp.Hinweise, h => h.Contains("Schlauchverfahren"));
    }

    [Fact]
    public void Ohne_bekannte_art_wird_nichts_angelegt()
    {
        var imp = WebGisSanierungImportRegel.Baue(WebGisObjektart.Haltung, Guid.NewGuid(), "H1", "M1", Massnahme(art: "Erfunden"));

        Assert.False(imp.Uebernehmbar);
        Assert.Contains(imp.Sperren, s => s.Contains("Erfunden"));
    }

    [Theory]
    [InlineData("2026-01-01T00:00:00.000Z", "2026")]
    [InlineData("1767225600000", "2026")] // Millisekunden seit 1970 (01.01.2026 UTC)
    [InlineData("", null)]
    public void Sanierungsjahr_wird_aus_dem_datum_gelesen(string datum, string? jahr)
    {
        var imp = WebGisSanierungImportRegel.Baue(WebGisObjektart.Haltung, Guid.NewGuid(), "H1", "M1", Massnahme(jahr: datum));

        Assert.Equal(jahr, Wert(imp, WebGisSanierungFeldkarte.AkteJahr));
    }

    [Fact]
    public void Anlegen_schreibt_eine_akte_mit_webgis_beleg_und_ohne_handmarke()
    {
        var h = new HaltungRecord();
        var p = new Project();
        p.Data.Add(h);
        var imp = WebGisSanierungImportRegel.Baue(WebGisObjektart.Haltung, h.Id, "H1", "M1", Massnahme());

        Assert.True(WebGisSanierungImportRegel.LegeAn(p, imp));

        var akte = Assert.Single(p.Objektakten);
        Assert.Equal("sanierung", akte.Art);
        Assert.Equal(new[] { h.Id }, akte.Bezuege);
        Assert.Equal("Ausgeführt", akte.Werte[WebGisSanierungFeldkarte.AkteStatus].Text);
        Assert.Equal("1", akte.Werte[WebGisSanierungFeldkarte.AkteStatus].Originalcode);
        Assert.All(akte.Werte.Values, w => Assert.False(w.VonHand));
        Assert.Contains(akte.Quellen, q => q.System == "WebGIS" && q.Kennung == "M1");
        Assert.True(WebGisSaniertKriterium.IstSaniert(p.Objektakten, h.Id));
    }

    [Fact]
    public void Dieselbe_massnahme_wird_nicht_doppelt_angelegt()
    {
        var h = new HaltungRecord();
        var p = new Project();
        p.Data.Add(h);
        var imp = WebGisSanierungImportRegel.Baue(WebGisObjektart.Haltung, h.Id, "H1", "M1", Massnahme());
        WebGisSanierungImportRegel.LegeAn(p, imp);

        var zeileGleicheKennung = new WebGisSanierungZeile { GlobalId = "M1" };
        var zeileGleicherInhalt = new WebGisSanierungZeile { GlobalId = "M9", Art = "Renovierung", Status = "Ausgeführt", Verfahren = "Schlauchverfahren" };
        var zeileAnders = new WebGisSanierungZeile { GlobalId = "M2", Art = "Reparatur", Status = "Ausgeführt", Verfahren = "Vermörtelung" };

        Assert.True(WebGisSanierungImportRegel.SchonVorhanden(p.Objektakten, h.Id, zeileGleicheKennung));
        Assert.True(WebGisSanierungImportRegel.SchonVorhanden(p.Objektakten, h.Id, zeileGleicherInhalt));
        Assert.False(WebGisSanierungImportRegel.SchonVorhanden(p.Objektakten, h.Id, zeileAnders));
        Assert.False(WebGisSanierungImportRegel.LegeAn(p, imp)); // zweites Anlegen derselben Massnahme: nichts
        Assert.Single(p.Objektakten);
    }
}
