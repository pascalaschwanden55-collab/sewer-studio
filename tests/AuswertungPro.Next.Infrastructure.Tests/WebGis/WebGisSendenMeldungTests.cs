using System;
using AuswertungPro.Next.Application.WebGis;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.WebGis;

/// <summary>
/// Wartbarkeitsaudit 30.09.2026, WG-D/WG-E: der berechnete Schreibausgang einer Position und die Meldung nach einem
/// Schreiblauf — ohne Export-Seite pruefbar. Die Texte sind zeichengleich die, die bis dahin in
/// <c>ExportWebGisBereich.SchreibeAsync</c> standen.
/// </summary>
public sealed class WebGisSendenMeldungTests
{
    // ---- Ausgang einer Position (WG-D) ----

    [Theory]
    [InlineData(false, false, false, false, WebGisSchreibAusgang.Offen)]
    [InlineData(false, false, false, true, WebGisSchreibAusgang.Fehler)]
    [InlineData(false, true, false, true, WebGisSchreibAusgang.Fehler)] // bestaetigt, Feld verworfen
    [InlineData(true, false, false, false, WebGisSchreibAusgang.Geschrieben)]
    [InlineData(true, false, true, false, WebGisSchreibAusgang.Geschrieben)]
    [InlineData(true, true, false, false, WebGisSchreibAusgang.VomServerBestaetigt)]
    [InlineData(true, true, true, false, WebGisSchreibAusgang.Nachgeprueft)]
    [InlineData(true, true, true, true, WebGisSchreibAusgang.Nachgeprueft)] // geschrieben geht vor Fehler (wie Bericht und Log)
    public void Ausgang_eines_objekts(bool geschrieben, bool bestaetigt, bool nachgeprueft, bool fehler, WebGisSchreibAusgang erwartet)
    {
        var p = new WebGisExportPosition
        {
            Objektart = WebGisObjektart.Haltung, Bezeichnung = "H",
            Geschrieben = geschrieben, VomServerBestaetigt = bestaetigt, Nachgeprueft = nachgeprueft,
            SchreibFehler = fehler ? "x" : null,
        };
        Assert.Equal(erwartet, p.Ausgang);
    }

    [Theory]
    [InlineData(false, false, false, false, WebGisSchreibAusgang.Offen)]
    [InlineData(false, false, true, false, WebGisSchreibAusgang.Fehler)]
    [InlineData(true, false, false, false, WebGisSchreibAusgang.VomServerBestaetigt)]
    [InlineData(true, true, false, false, WebGisSchreibAusgang.Nachgeprueft)]
    [InlineData(true, false, false, true, WebGisSchreibAusgang.Ungeklaert)]
    [InlineData(true, true, false, true, WebGisSchreibAusgang.Ungeklaert)] // ungeklaert geht vor (wie Bericht und Log)
    public void Ausgang_einer_massnahme(bool geschrieben, bool nachgeprueft, bool fehler, bool ungeklaert, WebGisSchreibAusgang erwartet)
    {
        var s = new WebGisSanierungPosition
        {
            Objektart = WebGisObjektart.Schacht, ElternBezeichnung = "S",
            Geschrieben = geschrieben, Nachgeprueft = nachgeprueft, SchreibFehler = fehler ? "x" : null,
            Ungeklaert = ungeklaert ? "y" : null,
        };
        Assert.Equal(erwartet, s.Ausgang);
    }

    // ---- Meldung nach dem Lauf (WG-E) ----

    private static WebGisExportPlan PlanMitGeschriebenemObjekt()
    {
        var plan = new WebGisExportPlan();
        var p = new WebGisExportPosition { Objektart = WebGisObjektart.Haltung, Bezeichnung = "H1", GlobalId = "G1" };
        p.Aenderungen.Add(new WebGisFeldAenderung { RefId = "r", Feld = "Zustand", Alt = "102", Neu = "104" });
        p.VomServerBestaetigt = true;
        p.Geschrieben = true;
        p.Nachgeprueft = true;
        plan.Positionen.Add(p);
        return plan;
    }

    private const string ErgebnisEinObjekt = "WebGIS: 1 Objekte geschrieben, 0 Sanierungsmassnahmen angelegt und nachgeprüft.";

    [Fact]
    public void Abgeschlossener_lauf_ohne_fehler_meldet_erfolg()
    {
        var m = WebGisSendenAblauf.Melde(WebGisSendenAblauf.Ausgang.Abgeschlossen, PlanMitGeschriebenemObjekt());

        Assert.Equal(ErgebnisEinObjekt, m.Ergebnis);
        Assert.Equal("", m.Zusatz);
        Assert.Equal(ErgebnisEinObjekt, m.Text);
        Assert.False(m.Fehlgeschlagen);
        Assert.Equal("Ergebnis", m.Berichtsart);
    }

    [Theory]
    [InlineData(WebGisSendenAblauf.Ausgang.LogAusgefallen,
        "\nACHTUNG: Das Änderungslog fiel während des Laufs aus — Lauf gestoppt. Bereits bestätigte Änderungen stehen im Bericht; vor einem neuen Versuch im WebGIS nachsehen.")]
    [InlineData(WebGisSendenAblauf.Ausgang.ProjektGewechselt,
        "\nACHTUNG: Das Projekt war nicht mehr offen — Lauf vor dem nächsten Schreiben gestoppt.")]
    [InlineData(WebGisSendenAblauf.Ausgang.Ungeklaert,
        "\nACHTUNG: Eine Sanierungsmassnahme wurde vom Server bestätigt, liess sich aber nicht sicher nachprüfen — Lauf gestoppt. Vor einem neuen Versuch im WebGIS nachsehen, nicht erneut anlegen.")]
    public void Gestoppter_lauf_meldet_achtung_und_warnung(WebGisSendenAblauf.Ausgang ausgang, string zusatz)
    {
        var m = WebGisSendenAblauf.Melde(ausgang, PlanMitGeschriebenemObjekt());

        Assert.Equal(zusatz, m.Zusatz);
        Assert.Equal(ErgebnisEinObjekt + zusatz, m.Text);
        Assert.True(m.Fehlgeschlagen);
        Assert.Equal("Ergebnis-abgebrochen", m.Berichtsart);
    }

    [Fact]
    public void Fehler_oder_ungeklaerter_ausgang_ist_fehlgeschlagen_bestaetigt_ohne_gegenprobe_nicht()
    {
        var mitFehler = PlanMitGeschriebenemObjekt();
        mitFehler.Positionen.Add(new WebGisExportPosition { Objektart = WebGisObjektart.Haltung, Bezeichnung = "H2", SchreibFehler = "x" });
        var mitMassnahmeFehler = PlanMitGeschriebenemObjekt();
        mitMassnahmeFehler.Sanierungen.Add(new WebGisSanierungPosition { Objektart = WebGisObjektart.Haltung, ElternBezeichnung = "H1", SchreibFehler = "x" });
        var mitUngeklaert = PlanMitGeschriebenemObjekt();
        mitUngeklaert.Sanierungen.Add(new WebGisSanierungPosition { Objektart = WebGisObjektart.Haltung, ElternBezeichnung = "H1", Geschrieben = true, Ungeklaert = "y" });
        var nurBestaetigt = PlanMitGeschriebenemObjekt();
        nurBestaetigt.Positionen[0].Nachgeprueft = false;
        nurBestaetigt.Sanierungen.Add(new WebGisSanierungPosition { Objektart = WebGisObjektart.Haltung, ElternBezeichnung = "H1", Geschrieben = true });

        Assert.True(WebGisSendenAblauf.Melde(WebGisSendenAblauf.Ausgang.Abgeschlossen, mitFehler).Fehlgeschlagen);
        Assert.True(WebGisSendenAblauf.Melde(WebGisSendenAblauf.Ausgang.Abgeschlossen, mitMassnahmeFehler).Fehlgeschlagen);
        Assert.True(WebGisSendenAblauf.Melde(WebGisSendenAblauf.Ausgang.Abgeschlossen, mitUngeklaert).Fehlgeschlagen);
        Assert.False(WebGisSendenAblauf.Melde(WebGisSendenAblauf.Ausgang.Abgeschlossen, nurBestaetigt).Fehlgeschlagen);
        Assert.Equal("Ergebnis", WebGisSendenAblauf.Melde(WebGisSendenAblauf.Ausgang.Abgeschlossen, mitFehler).Berichtsart);
    }

    [Fact]
    public void Ergebnisfenster_nennt_log_lauf_und_bericht()
    {
        var m = WebGisSendenAblauf.Melde(WebGisSendenAblauf.Ausgang.ProjektGewechselt, PlanMitGeschriebenemObjekt());

        Assert.Equal(ErgebnisEinObjekt + m.Zusatz + "\nLog: C:\\P\\__WebGIS_Export\\log.txt (Lauf L1)\nBericht: C:\\P\\b.txt",
            m.Ergebnisfenster("C:\\P\\__WebGIS_Export\\log.txt", "L1", "C:\\P\\b.txt"));
        Assert.Equal(ErgebnisEinObjekt + m.Zusatz + "\nLog: C:\\P\\__WebGIS_Export\\log.txt (Lauf L1)",
            m.Ergebnisfenster("C:\\P\\__WebGIS_Export\\log.txt", "L1", null));
    }

    [Fact]
    public void Fehlende_startzeile_nennt_die_logdatei()
        => Assert.Equal("Das Änderungslog ist nicht schreibbar (C:\\P\\log.txt) — nichts ins WebGIS geschrieben.",
            WebGisSendenAblauf.KeinStartbelegText("C:\\P\\log.txt"));
}
