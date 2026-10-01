using System.Collections.Generic;
using System.IO;
using System.Linq;
using AuswertungPro.Next.Application.WebGis;
using Xunit;
using static AuswertungPro.Next.Infrastructure.Tests.TestRepoPaths;

namespace AuswertungPro.Next.Infrastructure.Tests.WebGis;

/// <summary>
/// Wartbarkeitsaudit 30.09.2026, WG-A: Jede WebGIS-refId steht genau einmal im Quelltext. Breite und Hoehe der
/// Haltung standen doppelt (Handwertkarte und Holen), die Baujahr-/Ersatzjahr-Kennung nur in der Schutzliste —
/// eine Korrektur an einer Stelle haette Senden und Holen still verschieden lesen lassen.
/// </summary>
public sealed class WebGisKennungenTests
{
    private static WebGisLesestand Stand(params (string RefId, string Key, string Text)[] werte)
    {
        var s = new WebGisLesestand { GlobalId = "G1", Bezeichnung = "H1" };
        foreach (var (refId, key, text) in werte)
        {
            s.Felder[refId] = key;
            s.Kataloge[refId] = new List<(string, string)> { (key, text) };
        }
        return s;
    }

    private static WebGisImportEingabe HaltungMitLeeremDn()
    {
        var e = new WebGisImportEingabe { Objektart = WebGisObjektart.Haltung, Bezeichnung = "H1", GespeicherteGlobalId = "G1" };
        e.Felder["DN_mm"] = new WebGisImportFeld("", false);
        return e;
    }

    [Fact]
    public void Holen_liest_breite_und_hoehe_unter_denselben_refids_wie_die_handwertkarte()
    {
        // Festhaltend (gruen auf dem Stand vor WG-A): Das Holen nimmt die DN nur, wenn Breite = Hoehe. Die refIds
        // kommen hier ausschliesslich aus der Handwertkarte — liest das Holen andere, fehlt die DN.
        var breite = WebGisHandwertKarte.Finde(WebGisObjektart.Haltung, "Lichte_Breite_mm")!.RefId;
        var hoehe = WebGisHandwertKarte.Finde(WebGisObjektart.Haltung, "Lichte_Hoehe_mm")!.RefId;

        var rund = WebGisImportPlanBuilder.Baue(HaltungMitLeeremDn(), Stand((breite, "300", "300"), (hoehe, "300", "300")));
        var oval = WebGisImportPlanBuilder.Baue(HaltungMitLeeremDn(), Stand((breite, "600", "600"), (hoehe, "900", "900")));

        Assert.Equal("300", rund.Aenderungen.Single(a => a.Feld == "DN_mm").Neu);
        Assert.DoesNotContain(oval.Aenderungen, a => a.Feld == "DN_mm");
        Assert.Contains("DN: Breite 600 und Höhe 900 im WebGIS verschieden — DN nicht übernommen.", oval.Hinweise);
    }

    [Fact]
    public void Dn_und_lichte_breite_senden_auf_dieselbe_refid()
    {
        Assert.Equal(
            WebGisHandwertKarte.Finde(WebGisObjektart.Haltung, "Lichte_Breite_mm")!.RefId,
            WebGisHandwertKarte.Finde(WebGisObjektart.Haltung, "DN_mm")!.RefId);
    }

    [Fact]
    public void Handwertkarte_nimmt_breite_und_hoehe_aus_der_feldkarte()
    {
        // Die Werte selbst bleiben unveraendert (Hoehe ist laut Doku offen, Wert trotzdem nicht aendern).
        Assert.Equal("902695a4-5f44-e910-b2da-471c17085822", WebGisFeldkarte.HaltungBreiteRef);
        Assert.Equal("d06f8d1f-8a09-1b22-4380-088a7ee42507", WebGisFeldkarte.HaltungHoeheRef);
        Assert.Equal("e2fddd0d-b1f0-bc99-bc54-95bc6d2d5b1a", WebGisFeldkarte.HaltungBaujahrErsatzjahrRef);

        Assert.Equal(WebGisFeldkarte.HaltungBreiteRef, WebGisHandwertKarte.Finde(WebGisObjektart.Haltung, "Lichte_Breite_mm")!.RefId);
        Assert.Equal(WebGisFeldkarte.HaltungBreiteRef, WebGisHandwertKarte.Finde(WebGisObjektart.Haltung, "DN_mm")!.RefId);
        Assert.Equal(WebGisFeldkarte.HaltungHoeheRef, WebGisHandwertKarte.Finde(WebGisObjektart.Haltung, "Lichte_Hoehe_mm")!.RefId);
    }

    [Theory]
    [InlineData("902695a4")]
    [InlineData("d06f8d1f")]
    [InlineData("e2fddd0d")]
    public void Refid_steht_im_quelltext_genau_einmal(string kennung)
    {
        // Waechter: Eine zweite Kopie der Kennung (etwa wieder eine private Konstante im Holen) faellt hier auf.
        var src = Path.Combine(RepoRoot(), "src");
        var treffer = Directory.EnumerateFiles(src, "*.cs", SearchOption.AllDirectories)
            .Where(p => !p.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", System.StringComparison.OrdinalIgnoreCase)
                        && !p.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", System.StringComparison.OrdinalIgnoreCase))
            .SelectMany(p => File.ReadLines(p).Select((zeile, i) => (Datei: Path.GetRelativePath(src, p), Nr: i + 1, Zeile: zeile)))
            .Where(z => z.Zeile.Contains(kennung, System.StringComparison.OrdinalIgnoreCase))
            .Select(z => $"{z.Datei}:{z.Nr}")
            .ToList();

        Assert.True(treffer.Count == 1, $"{kennung} steht {treffer.Count}-mal in src: {string.Join(", ", treffer)}");
        Assert.EndsWith("WebGisFeldkarte.cs", treffer[0].Split(':')[0], System.StringComparison.Ordinal);
    }

    [Fact]
    public void Baujahr_ersatzjahr_der_haltung_bleibt_geschuetzt()
    {
        // Festhaltend: Wert und Anzeigename der Schutzliste bleiben zeichengleich.
        Assert.Equal("Baujahr/Ersatzjahr", WebGisGeschuetzteFelder.NieSchreiben(WebGisObjektart.Haltung)["e2fddd0d-b1f0-bc99-bc54-95bc6d2d5b1a"]);
        Assert.False(WebGisGeschuetzteFelder.NieSchreiben(WebGisObjektart.Schacht).ContainsKey("e2fddd0d-b1f0-bc99-bc54-95bc6d2d5b1a"));
    }
}
