using AuswertungPro.Next.Application.Kostenanalyse;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.Kostenanalyse;

public sealed class KostenfallMerkmalLeserTests
{
    private static HaltungRecord Haltung(string dn, string laenge, params ProtocolEntry[] eintraege)
    {
        var record = new HaltungRecord();
        record.SetFieldValue("DN_mm", dn, FieldSource.Manual, userEdited: false);
        record.SetFieldValue("Haltungslaenge_m", laenge, FieldSource.Manual, userEdited: false);
        record.Protocol = new ProtocolDocument
        {
            Current = new ProtocolRevision { Entries = [.. eintraege] }
        };
        return record;
    }

    private static ProtocolEntry E(string code, bool strecke = false, bool geloescht = false)
        => new() { Code = code, IsStreckenschaden = strecke, IsDeleted = geloescht };

    [Fact]
    public void Liest_Durchmesser_und_Laenge()
    {
        var merkmale = KostenfallMerkmalLeser.Lies(Haltung("300", "42.5", E("BAF01")));

        Assert.Equal(300, merkmale.DnMm);
        Assert.Equal(42.5, merkmale.LaengeM);
    }

    [Fact]
    public void Fasst_Schaeden_auf_den_Hauptcode_zusammen_und_zaehlt_sie()
    {
        var merkmale = KostenfallMerkmalLeser.Lies(
            Haltung("300", "40", E("BAF01"), E("BAFCE"), E("BAJ02")));

        Assert.Equal(new[] { "BAF", "BAJ" }, merkmale.Schadensarten);
        Assert.Equal(2, Assert.Single(merkmale.Schaeden, s => s.Hauptcode == "BAF").Anzahl);
    }

    [Fact]
    public void Bauteile_sind_keine_Schaeden()
    {
        var merkmale = KostenfallMerkmalLeser.Lies(
            Haltung("300", "40", E("BCD"), E("BCE"), E("BDA"), E("000M"), E("BAF01")));

        Assert.Equal(new[] { "BAF" }, merkmale.Schadensarten);
    }

    [Fact]
    public void Anschluesse_sind_ein_eigenes_Merkmal_kein_Schaden()
    {
        var merkmale = KostenfallMerkmalLeser.Lies(
            Haltung("300", "40", E("BCAEA"), E("BCAAB"), E("BAF01")));

        Assert.Equal(2, merkmale.AnschlussAnzahl);
        Assert.DoesNotContain("BCA", merkmale.Schadensarten);
    }

    [Fact]
    public void Boegen_werden_gezaehlt()
    {
        var merkmale = KostenfallMerkmalLeser.Lies(
            Haltung("300", "40", E("BCCAA"), E("BCCBB"), E("BAF01")));

        Assert.Equal(2, merkmale.BogenAnzahl);
        Assert.True(merkmale.HatBogen);
    }

    [Fact]
    public void Streckenschaeden_werden_gekennzeichnet()
    {
        var merkmale = KostenfallMerkmalLeser.Lies(
            Haltung("300", "40", E("BAF01", strecke: true), E("BAJ02")));

        Assert.True(Assert.Single(merkmale.Schaeden, s => s.Hauptcode == "BAF").HatStrecke);
        Assert.False(Assert.Single(merkmale.Schaeden, s => s.Hauptcode == "BAJ").HatStrecke);
    }

    [Fact]
    public void Geloeschte_Eintraege_zaehlen_nicht()
    {
        var merkmale = KostenfallMerkmalLeser.Lies(
            Haltung("300", "40", E("BAF01"), E("BAB01", geloescht: true)));

        Assert.Equal(new[] { "BAF" }, merkmale.Schadensarten);
    }

    [Fact]
    public void Komma_als_Dezimaltrenner_wird_gelesen()
    {
        // Auf de-DE wuerde "42,5" sonst still zu 425 werden.
        Assert.Equal(42.5, KostenfallMerkmalLeser.Lies(Haltung("300", "42,5", E("BAF01"))).LaengeM);
    }
}
