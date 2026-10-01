using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.DataPage;

namespace AuswertungPro.Next.UI.Tests;

public sealed class DataPageCombinedFilterTests
{
    [Fact]
    public void Passt_kombiniert_suche_chips_und_dashboardfilter_mit_und()
    {
        var passend = Haltung("Alphaweg", "2", "video.mp4", "BAB Riss", "DN 300");
        var falscheSuche = Haltung("Betaweg", "2", "video.mp4", "BAB Riss", "DN 300");
        var falscherChip = Haltung("Alphaweg", "3", "video.mp4", "BAB Riss", "DN 300");
        var falscherStartfilter = Haltung("Alphaweg", "2", "video.mp4", "BAB Riss", "DN 400");
        var filter = new DataPageCombinedFilter(
            "Alpha",
            new DataPageFilter("2", NurMitVideo: true, NurMitSchaeden: true),
            DataPageStartFilter.FromDashboardDn("300"));

        Assert.True(filter.IstAktiv);
        Assert.True(filter.Passt(passend));
        Assert.False(filter.Passt(falscheSuche));
        Assert.False(filter.Passt(falscherChip));
        Assert.False(filter.Passt(falscherStartfilter));
    }

    [Fact]
    public void Suche_leeren_erhaelt_chip_und_dashboardfilter()
    {
        var filter = Ausgangsfilter().WithSearchText(string.Empty);

        Assert.True(filter.IstAktiv);
        Assert.True(filter.ChipFilter.IstAktiv);
        Assert.NotNull(filter.StartFilter);
    }

    [Fact]
    public void Chipfilter_leeren_erhaelt_suche_und_dashboardfilter()
    {
        var filter = Ausgangsfilter().WithChipFilter(DataPageFilter.Aus);

        Assert.True(filter.IstAktiv);
        Assert.Equal("Alpha", filter.SearchText);
        Assert.NotNull(filter.StartFilter);
    }

    [Fact]
    public void Dashboardfilter_leeren_erhaelt_suche_und_chips()
    {
        var filter = Ausgangsfilter().WithoutStartFilter();

        Assert.True(filter.IstAktiv);
        Assert.Equal("Alpha", filter.SearchText);
        Assert.True(filter.ChipFilter.IstAktiv);
    }

    [Fact]
    public void Widersprechende_zustandsfilter_liefern_keinen_treffer()
    {
        var filter = new DataPageCombinedFilter(
            null,
            new DataPageFilter("2", NurMitVideo: false, NurMitSchaeden: false),
            DataPageStartFilter.FromDashboardZustand("3"));

        Assert.False(filter.Passt(Haltung("Alphaweg", "2", "", "", "DN 300")));
        Assert.False(filter.Passt(Haltung("Alphaweg", "3", "", "", "DN 300")));
    }

    [Fact]
    public void LeererGesamtfilter_ist_inaktiv_und_laesst_haltungen_durch()
    {
        var filter = new DataPageCombinedFilter(" ", DataPageFilter.Aus, null);

        Assert.False(filter.IstAktiv);
        Assert.True(filter.Passt(new HaltungRecord()));
        Assert.False(filter.Passt(null));
    }

    private static DataPageCombinedFilter Ausgangsfilter()
        => new(
            "Alpha",
            new DataPageFilter("2", NurMitVideo: true, NurMitSchaeden: false),
            DataPageStartFilter.FromDashboardDn("300"));

    private static HaltungRecord Haltung(
        string strasse,
        string zk,
        string link,
        string schaeden,
        string dn)
    {
        var record = new HaltungRecord();
        record.SetFieldValue("Strasse", strasse, FieldSource.Manual, false);
        record.SetFieldValue("Zustandsklasse", zk, FieldSource.Manual, false);
        record.SetFieldValue("Link", link, FieldSource.Manual, false);
        record.SetFieldValue("Primaere_Schaeden", schaeden, FieldSource.Manual, false);
        record.SetFieldValue("DN_mm", dn, FieldSource.Manual, false);
        return record;
    }
}
