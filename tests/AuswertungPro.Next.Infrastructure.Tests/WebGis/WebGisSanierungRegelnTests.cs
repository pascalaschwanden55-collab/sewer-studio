using System;
using AuswertungPro.Next.Application.WebGis;
using AuswertungPro.Next.Domain.Models;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.WebGis;

public sealed class WebGisSanierungRegelnTests
{
    private static WebGisSanierungKatalog Katalog()
    {
        var k = new WebGisSanierungKatalog();
        k.Setze(WebGisSanierungFeldkarte.ArtRef, new[] { ("2", "Reparatur"), ("4", "Renovierung") });
        k.Setze(WebGisSanierungFeldkarte.StatusRef, new[] { ("0", "Unbekannt"), ("1", "Ausgeführt") });
        k.Setze(WebGisSanierungFeldkarte.VerfahrenRef, new[] { ("13", "Kurzrohrverfahren"), ("27", "Schlauchverfahren") });
        k.Setze(WebGisSanierungFeldkarte.VerfahrenRef, new[] { ("13", "Kurzrohrverfahren"), ("27", "Schlauchverfahren") }, "4");
        k.Setze(WebGisSanierungFeldkarte.VerfahrenRef, new[] { ("23", "Roboterverfahren"), ("33", "Vermörtelung") }, "2");
        k.Setze(WebGisSanierungFeldkarte.UmfangRef, new[] { ("1", "Gesamt"), ("2", "Partiell"), ("5", "Punktuell, Abzweig/Stutzen") });
        k.Setze(WebGisSanierungFeldkarte.ProfiltypRef, new[] { ("1", "Kreisprofil"), ("13", "Oval") });
        k.Setze(WebGisSanierungFeldkarte.FabrikatRef, new[] { ("0", "unbekannt"), ("9", "Saertex-Liner Typ S") });
        k.Setze(WebGisSanierungFeldkarte.HerstellerRef, new[] { ("33", "Epp / Jauch GmbH"), ("38", "GKS Cahenzli AG") });
        return k;
    }

    private static ObjektAkte Akte(params (string Key, string Text)[] werte)
    {
        var a = new ObjektAkte { Art = "sanierung" };
        foreach (var (k, t) in werte) a.Werte[k] = new ObjektFeldWert { Text = t };
        return a;
    }

    private static WebGisLesestand Stand() => new() { GlobalId = "P1", Bezeichnung = "80480-80478" };

    [Fact]
    public void Katalog_findet_schluessel_per_klartext_unabhaengig_von_schreibweise()
    {
        var k = Katalog();
        Assert.Equal("27", k.Schluessel(WebGisSanierungFeldkarte.VerfahrenRef, " schlauchverfahren "));
        Assert.Equal("5", k.Schluessel(WebGisSanierungFeldkarte.UmfangRef, "Punktuell,Abzweig / Stutzen"));
        Assert.Equal("33", k.Schluessel(WebGisSanierungFeldkarte.HerstellerRef, "Epp/Jauch GmbH"));
        Assert.Null(k.Schluessel(WebGisSanierungFeldkarte.VerfahrenRef, "Vermörtelung"));
        Assert.Null(k.Schluessel(WebGisSanierungFeldkarte.VerfahrenRef, "Vermörtelung", "4"));
        Assert.Equal("33", k.Schluessel(WebGisSanierungFeldkarte.VerfahrenRef, "Vermörtelung", "2"));
        Assert.Null(k.Schluessel(WebGisSanierungFeldkarte.VerfahrenRef, ""));
    }

    [Fact]
    public void Jahr_wird_zum_ersten_januar_utc()
    {
        Assert.Equal("2026-01-01T00:00:00.000Z", WebGisSanierungFeldkarte.JahrAlsDatum("2026"));
        Assert.Null(WebGisSanierungFeldkarte.JahrAlsDatum("26"));
        Assert.Null(WebGisSanierungFeldkarte.JahrAlsDatum(""));
    }

    [Fact]
    public void Vollstaendige_akte_wird_1zu1_uebersetzt()
    {
        var akte = Akte(
            (WebGisSanierungFeldkarte.AkteArt, "Renovierung"),
            (WebGisSanierungFeldkarte.AkteStatus, "Ausgeführt"),
            (WebGisSanierungFeldkarte.AkteVerfahren, "Schlauchverfahren"),
            (WebGisSanierungFeldkarte.AkteUmfang, "Gesamt"),
            (WebGisSanierungFeldkarte.AkteJahr, "2026"),
            (WebGisSanierungFeldkarte.AkteProfiltyp, "Kreisprofil"),
            (WebGisSanierungFeldkarte.AkteFabrikat, "Saertex-Liner Typ S"),
            (WebGisSanierungFeldkarte.AkteHersteller, "GKS Cahenzli AG"));

        var pos = WebGisSanierungPlanBuilder.Baue(akte, WebGisObjektart.Haltung, "80480-80478", Stand(), Katalog());

        Assert.True(pos.Schreibbar);
        Assert.Equal("4", pos.Felder[WebGisSanierungFeldkarte.ArtRef]);
        Assert.Equal("1", pos.Felder[WebGisSanierungFeldkarte.StatusRef]);
        Assert.Equal("27", pos.Felder[WebGisSanierungFeldkarte.VerfahrenRef]);
        Assert.Equal("1", pos.Felder[WebGisSanierungFeldkarte.UmfangRef]);
        Assert.Equal("2026-01-01T00:00:00.000Z", pos.Felder[WebGisSanierungFeldkarte.SanierungsjahrRef]);
        Assert.Equal("1", pos.Felder[WebGisSanierungFeldkarte.ProfiltypRef]);
        Assert.Equal("9", pos.Felder[WebGisSanierungFeldkarte.FabrikatRef]);
        Assert.Equal("38", pos.Felder[WebGisSanierungFeldkarte.HerstellerRef]);
        Assert.Equal(8, pos.Felder.Count);
    }

    [Fact]
    public void Verfahren_wird_je_art_aufgeloest_reparatur_vermoertelung()
    {
        var akte = Akte(
            (WebGisSanierungFeldkarte.AkteArt, "Reparatur"),
            (WebGisSanierungFeldkarte.AkteStatus, "Ausgeführt"),
            (WebGisSanierungFeldkarte.AkteVerfahren, "Vermörtelung"),
            (WebGisSanierungFeldkarte.AkteJahr, "2026"));

        var pos = WebGisSanierungPlanBuilder.Baue(akte, WebGisObjektart.Schacht, "60291", Stand(), Katalog());

        Assert.True(pos.Schreibbar);
        Assert.Equal("2", pos.Felder[WebGisSanierungFeldkarte.ArtRef]);
        Assert.Equal("33", pos.Felder[WebGisSanierungFeldkarte.VerfahrenRef]);
    }

    [Fact]
    public void Fehlender_katalogwert_sperrt_statt_zu_raten()
    {
        // Vermoertelung ist kein Renovierungsverfahren -> im WebGIS fuer Art Renovierung nicht waehlbar.
        var akte = Akte(
            (WebGisSanierungFeldkarte.AkteArt, "Renovierung"),
            (WebGisSanierungFeldkarte.AkteStatus, "Ausgeführt"),
            (WebGisSanierungFeldkarte.AkteVerfahren, "Vermörtelung"),
            (WebGisSanierungFeldkarte.AkteJahr, "2026"));

        var pos = WebGisSanierungPlanBuilder.Baue(akte, WebGisObjektart.Haltung, "H", Stand(), Katalog());

        Assert.False(pos.Schreibbar);
        Assert.Contains(pos.Sperren, s => s.Contains("Verfahren 'Vermörtelung'") && s.Contains("Renovierung"));
        Assert.False(pos.Felder.ContainsKey(WebGisSanierungFeldkarte.VerfahrenRef));
    }

    [Fact]
    public void Ohne_art_wird_gesperrt()
    {
        var akte = Akte(
            (WebGisSanierungFeldkarte.AkteStatus, "Ausgeführt"),
            (WebGisSanierungFeldkarte.AkteVerfahren, "Schlauchverfahren"));

        var pos = WebGisSanierungPlanBuilder.Baue(akte, WebGisObjektart.Haltung, "80462-80461", Stand(), Katalog());

        Assert.False(pos.Schreibbar);
        Assert.Contains(pos.Sperren, s => s.Contains("Art fehlt"));
    }

    [Fact]
    public void Leere_felder_bleiben_weg_und_bereits_vorhandene_massnahme_wird_nicht_doppelt_angelegt()
    {
        var akte = Akte(
            (WebGisSanierungFeldkarte.AkteArt, "Renovierung"),
            (WebGisSanierungFeldkarte.AkteStatus, "Ausgeführt"),
            (WebGisSanierungFeldkarte.AkteVerfahren, "Schlauchverfahren"),
            (WebGisSanierungFeldkarte.AkteProfiltyp, ""));

        var frei = WebGisSanierungPlanBuilder.Baue(akte, WebGisObjektart.Schacht, "S", Stand(), Katalog());
        Assert.True(frei.Schreibbar);
        Assert.Equal(3, frei.Felder.Count);

        var stand = Stand();
        stand.Sanierungen.Add(new WebGisSanierungZeile { Art = "Renovierung", Status = "Ausgeführt", Verfahren = "Schlauchverfahren", GlobalId = "x" });
        var doppelt = WebGisSanierungPlanBuilder.Baue(akte, WebGisObjektart.Schacht, "S", stand, Katalog());
        Assert.False(doppelt.Schreibbar);
        Assert.Contains(doppelt.Sperren, s => s.Contains("bereits vorhanden"));
    }

    [Fact]
    public void Ohne_stand_oder_katalog_wird_gesperrt()
    {
        var akte = Akte((WebGisSanierungFeldkarte.AkteStatus, "Ausgeführt"));
        Assert.False(WebGisSanierungPlanBuilder.Baue(akte, WebGisObjektart.Haltung, "H", null, Katalog()).Schreibbar);
        Assert.False(WebGisSanierungPlanBuilder.Baue(akte, WebGisObjektart.Haltung, "H", Stand(), null).Schreibbar);
    }
}
