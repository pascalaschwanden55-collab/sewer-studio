using AuswertungPro.Next.Application.Protocol;

namespace AuswertungPro.Next.Pipeline.Tests;

/// <summary>
/// Charakterisierungs-Tests fuer VsaParameterMerger (IST-Verhalten aus ObservationCatalogViewModel.MergeVsaParameters).
/// </summary>
public sealed class VsaParameterMergerTests
{
    private static Dictionary<string, string> EmptyDict()
        => new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    [Fact]
    public void Merge_distanz_schreibt_vsa_und_wincan_alias()
    {
        var parameters = EmptyDict();
        VsaParameterMerger.Merge(parameters,
            vsaDistanz: "12.50", vsaVideo: null, vsaUhrVon: null, vsaUhrBis: null,
            vsaQ1: null, vsaQ2: null, vsaStrecke: null, vsaVerbindung: false,
            vsaAnsicht: null, vsaEz: null, vsaSchachtbereich: null, vsaAnmerkung: null);

        Assert.Equal("12.50", parameters["vsa.distanz"]);
        Assert.Equal("12.50", parameters["Distance"]);
    }

    [Fact]
    public void Merge_uhr_von_bis_schreibt_vsa_und_clockpos_alias()
    {
        var parameters = EmptyDict();
        VsaParameterMerger.Merge(parameters,
            vsaDistanz: null, vsaVideo: null, vsaUhrVon: "8", vsaUhrBis: "3",
            vsaQ1: null, vsaQ2: null, vsaStrecke: null, vsaVerbindung: false,
            vsaAnsicht: null, vsaEz: null, vsaSchachtbereich: null, vsaAnmerkung: null);

        Assert.Equal("8", parameters["vsa.uhr.von"]);
        Assert.Equal("8", parameters["ClockPos1"]);
        Assert.Equal("3", parameters["vsa.uhr.bis"]);
        Assert.Equal("3", parameters["ClockPos2"]);
    }

    [Fact]
    public void Merge_q1_schreibt_alle_drei_aliase()
    {
        var parameters = EmptyDict();
        VsaParameterMerger.Merge(parameters,
            vsaDistanz: null, vsaVideo: null, vsaUhrVon: null, vsaUhrBis: null,
            vsaQ1: "25", vsaQ2: null, vsaStrecke: null, vsaVerbindung: false,
            vsaAnsicht: null, vsaEz: null, vsaSchachtbereich: null, vsaAnmerkung: null);

        Assert.Equal("25", parameters["vsa.q1"]);
        Assert.Equal("25", parameters["Q1"]);
        Assert.Equal("25", parameters["Quantifizierung1"]);
    }

    [Fact]
    public void Merge_verbindung_true_schreibt_ja()
    {
        var parameters = EmptyDict();
        VsaParameterMerger.Merge(parameters,
            vsaDistanz: null, vsaVideo: null, vsaUhrVon: null, vsaUhrBis: null,
            vsaQ1: null, vsaQ2: null, vsaStrecke: null, vsaVerbindung: true,
            vsaAnsicht: null, vsaEz: null, vsaSchachtbereich: null, vsaAnmerkung: null);

        Assert.Equal("ja", parameters["vsa.verbindung"]);
    }

    [Fact]
    public void Merge_verbindung_false_schreibt_nichts()
    {
        var parameters = EmptyDict();
        VsaParameterMerger.Merge(parameters,
            vsaDistanz: null, vsaVideo: null, vsaUhrVon: null, vsaUhrBis: null,
            vsaQ1: null, vsaQ2: null, vsaStrecke: null, vsaVerbindung: false,
            vsaAnsicht: null, vsaEz: null, vsaSchachtbereich: null, vsaAnmerkung: null);

        Assert.False(parameters.ContainsKey("vsa.verbindung"));
    }

    [Fact]
    public void Merge_leere_werte_werden_nicht_geschrieben()
    {
        var parameters = EmptyDict();
        VsaParameterMerger.Merge(parameters,
            vsaDistanz: "", vsaVideo: "  ", vsaUhrVon: null, vsaUhrBis: null,
            vsaQ1: null, vsaQ2: null, vsaStrecke: null, vsaVerbindung: false,
            vsaAnsicht: null, vsaEz: null, vsaSchachtbereich: null, vsaAnmerkung: null);

        Assert.Empty(parameters);
    }

    [Fact]
    public void Merge_anmerkung_schreibt_nur_vsa_anmerkung()
    {
        var parameters = EmptyDict();
        VsaParameterMerger.Merge(parameters,
            vsaDistanz: null, vsaVideo: null, vsaUhrVon: null, vsaUhrBis: null,
            vsaQ1: null, vsaQ2: null, vsaStrecke: null, vsaVerbindung: false,
            vsaAnsicht: null, vsaEz: null, vsaSchachtbereich: null, vsaAnmerkung: "Bemerkung");

        Assert.Equal("Bemerkung", parameters["vsa.anmerkung"]);
        // Keine weiteren Eintraege
        Assert.Single(parameters);
    }

    [Fact]
    public void Merge_wert_wird_getrimmt()
    {
        var parameters = EmptyDict();
        VsaParameterMerger.Merge(parameters,
            vsaDistanz: "  5.00  ", vsaVideo: null, vsaUhrVon: null, vsaUhrBis: null,
            vsaQ1: null, vsaQ2: null, vsaStrecke: null, vsaVerbindung: false,
            vsaAnsicht: null, vsaEz: null, vsaSchachtbereich: null, vsaAnmerkung: null);

        Assert.Equal("5.00", parameters["vsa.distanz"]);
    }
}
