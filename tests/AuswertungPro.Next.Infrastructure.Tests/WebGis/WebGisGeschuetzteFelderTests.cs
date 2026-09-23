using System;
using System.Collections.Generic;
using System.Linq;
using AuswertungPro.Next.Application.WebGis;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.WebGis;

/// <summary>
/// Entscheid Pascal 23.09.2026: Beim Zurueckschreiben werden Eigentum, Betreiber, Haltungslaenge,
/// Baujahr, GlobalID und Objekt-ID im WebGIS nie ueberschrieben. Die Feldliste des Exports haelt
/// das schon ein; diese Regel ist die zweite, harte Sperre direkt vor dem Senden.
/// refIds aus der Masken-Inventur v2 (Buerglen, __WebGIS_Export/Feldzuordnung_SewerStudio_WebGIS_v2.md).
/// </summary>
public sealed class WebGisGeschuetzteFelderTests
{
    private const string HaltungEigentuemer = "fadff6f2-c674-9327-36d8-b2ac79b704cd";
    private const string HaltungBetreiber = "bd6d1330-f106-9eb3-c079-22a2accd845c";
    private const string HaltungObjectId = "8410243c-beab-5ecb-b4e7-bc7907b9ee31";
    private const string SchachtEigentuemer = "e2987817-9bdd-2cef-4617-729126d465a1";
    private const string SchachtBetreiber = "1187d930-1cdb-d29b-2065-499d273dbeba";
    private const string SchachtObjectId = "9bc2e78d-3a34-e836-5db6-357202362d20";

    private static IReadOnlyList<string> Pruefe(WebGisObjektart art, string refId, string neu, string? aktuell = null)
        => WebGisGeschuetzteFelder.Verstoesse(art, new Dictionary<string, string> { [refId] = neu }, _ => aktuell);

    [Theory]
    [InlineData(WebGisObjektart.Haltung, HaltungEigentuemer, "Eigentümer")]
    [InlineData(WebGisObjektart.Haltung, HaltungBetreiber, "Betreiber")]
    [InlineData(WebGisObjektart.Haltung, HaltungObjectId, "OBJECTID")]
    [InlineData(WebGisObjektart.Haltung, WebGisFeldkarte.HaltungLaengeRohrRef, "Haltungslänge")]
    [InlineData(WebGisObjektart.Haltung, WebGisFeldkarte.HaltungLaengeGeomRef, "Länge")]
    [InlineData(WebGisObjektart.Haltung, WebGisFeldkarte.HaltungBezeichnungRef, "Bezeichnung")]
    [InlineData(WebGisObjektart.Schacht, SchachtEigentuemer, "Eigentümer")]
    [InlineData(WebGisObjektart.Schacht, SchachtBetreiber, "Betreiber")]
    [InlineData(WebGisObjektart.Schacht, SchachtObjectId, "OBJECTID")]
    [InlineData(WebGisObjektart.Schacht, WebGisFeldkarte.SchachtBezeichnungRef, "Bezeichnung")]
    public void Geschuetztes_feld_wird_nie_gesendet(WebGisObjektart art, string refId, string anzeige)
    {
        var verstoss = Assert.Single(Pruefe(art, refId, "X", aktuell: null));

        Assert.Contains(anzeige, verstoss);
        Assert.Contains("nie", verstoss);
    }

    [Theory]
    [InlineData(WebGisObjektart.Haltung)]
    [InlineData(WebGisObjektart.Schacht)]
    public void Baujahr_das_im_webgis_steht_wird_nie_ueberschrieben(WebGisObjektart art)
    {
        var verstoss = Assert.Single(Pruefe(art, WebGisFeldkarte.BaujahrRef(art), "1970", aktuell: "1963"));

        Assert.Contains("Baujahr", verstoss);
        Assert.Contains("1963", verstoss);
    }

    [Fact]
    public void Leeres_baujahr_der_haltung_darf_gefuellt_werden()
        => Assert.Empty(Pruefe(WebGisObjektart.Haltung, WebGisFeldkarte.HaltungBaujahrRef, "1970", aktuell: " "));

    [Fact]
    public void Unbekanntes_feld_wird_nie_gesendet()
    {
        // Auch ein Feld, das niemand als geschuetzt kennt (z.B. die GlobalID-Komponente einer
        // kuenftigen Maske), geht nicht hinaus: Freigegeben ist nur, was der Export planen kann.
        var verstoss = Assert.Single(Pruefe(WebGisObjektart.Haltung, "00000000-0000-0000-0000-000000000001", "X"));

        Assert.Contains("nicht freigegeben", verstoss);
    }

    [Theory]
    [InlineData(WebGisObjektart.Haltung)]
    [InlineData(WebGisObjektart.Schacht)]
    public void Feste_exportfelder_sind_freigegeben(WebGisObjektart art)
    {
        var felder = new Dictionary<string, string>
        {
            [WebGisFeldkarte.ZustandRef(art)] = "104",
            [WebGisFeldkarte.SanierungsbedarfRef(art)] = "106",
            [WebGisFeldkarte.BemerkungRef(art)] = "Text",
        };

        Assert.Empty(WebGisGeschuetzteFelder.Verstoesse(art, felder, _ => null));
    }

    [Fact]
    public void Jede_handwert_zuordnung_ist_freigegeben_und_keine_ist_geschuetzt()
    {
        // Waechter: Wer der Handwertkarte je ein Eigentuemer-, Laengen- oder Kennungsfeld hinzufuegt,
        // macht diesen Test rot — die Sperre vor dem Senden haette es sonst still abgewiesen.
        foreach (var f in WebGisHandwertKarte.Felder)
            foreach (var refId in new[] { f.RefId, f.HauptRefId }.Where(r => r is not null))
            {
                Assert.False(WebGisGeschuetzteFelder.NieSchreiben(f.Objektart).ContainsKey(refId!), $"{f.SewerStudioFeld} -> {refId}");
                Assert.False(WebGisGeschuetzteFelder.NurWennLeer(f.Objektart).ContainsKey(refId!), $"{f.SewerStudioFeld} -> {refId}");
                Assert.True(WebGisGeschuetzteFelder.IstFreigegeben(f.Objektart, refId!), $"{f.SewerStudioFeld} -> {refId}");
            }
    }

    [Theory]
    [InlineData(WebGisObjektart.Haltung)]
    [InlineData(WebGisObjektart.Schacht)]
    public void Kein_geschuetztes_feld_ist_zugleich_freigegeben(WebGisObjektart art)
    {
        foreach (var refId in WebGisGeschuetzteFelder.NieSchreiben(art).Keys)
            Assert.False(WebGisGeschuetzteFelder.IstFreigegeben(art, refId), refId);
    }
}
