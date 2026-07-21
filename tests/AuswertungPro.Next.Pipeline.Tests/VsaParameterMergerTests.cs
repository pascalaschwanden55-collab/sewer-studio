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
    public void Merge_leerer_Wert_ueberschreibt_vorhandenen_Alias_nicht()
    {
        var parameters = EmptyDict();
        parameters["Distance"] = "bestehend";

        VsaParameterMerger.Merge(parameters,
            vsaDistanz: "  ", vsaVideo: null, vsaUhrVon: null, vsaUhrBis: null,
            vsaQ1: null, vsaQ2: null, vsaStrecke: null, vsaVerbindung: false,
            vsaAnsicht: null, vsaEz: null, vsaSchachtbereich: null, vsaAnmerkung: null);

        Assert.Equal("bestehend", parameters["Distance"]);
        Assert.DoesNotContain("vsa.distanz", parameters.Keys);
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

    [Fact]
    public void NormalizeAliases_bereinigt_Eingabe_ohne_sie_zu_mutieren()
    {
        var input = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [" custom "] = " value ",
            [" "] = "ignoriert",
            ["leer"] = "   ",
            ["null"] = null!
        };

        var result = VsaParameterMerger.NormalizeAliases(input, string.Empty);

        Assert.Equal("value", result["custom"]);
        Assert.DoesNotContain(string.Empty, result.Keys);
        Assert.DoesNotContain("leer", result.Keys);
        Assert.DoesNotContain("null", result.Keys);
        Assert.True(result.ContainsKey("CUSTOM"));
        Assert.Contains(" custom ", input.Keys);
        Assert.Equal("   ", input["leer"]);
    }

    [Fact]
    public void NormalizeAliases_spiegelt_alle_kanonischen_und_alten_Gruppen()
    {
        var input = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Code"] = "BAB",
            ["Distance"] = "1.2",
            ["TimeCtr"] = "01:02",
            ["ClockPos1"] = "3",
            ["ClockPos2"] = "9",
            ["Quantifizierung1"] = "10",
            ["Quantifizierung2"] = "20"
        };

        var result = VsaParameterMerger.NormalizeAliases(input, string.Empty);

        AssertAliasGroup(result, "BAB", "vsa.code", "Code");
        AssertAliasGroup(result, "1.2", "vsa.distanz", "Distance");
        AssertAliasGroup(result, "01:02", "vsa.video", "TimeCtr");
        AssertAliasGroup(result, "3", "vsa.uhr.von", "ClockPos1");
        AssertAliasGroup(result, "9", "vsa.uhr.bis", "ClockPos2");
        AssertAliasGroup(result, "10", "vsa.q1", "Q1", "Quantifizierung1");
        AssertAliasGroup(result, "20", "vsa.q2", "Q2", "Quantifizierung2");
    }

    [Fact]
    public void NormalizeAliases_bevorzugt_kanonische_Werte_bei_Konflikten()
    {
        var input = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["vsa.distanz"] = "kanonisch",
            ["Distance"] = "alt",
            ["vsa.q1"] = "kanonisch-q1",
            ["Q1"] = "alt-q1",
            ["Quantifizierung1"] = "aelter-q1"
        };

        var result = VsaParameterMerger.NormalizeAliases(input, string.Empty);

        AssertAliasGroup(result, "kanonisch", "vsa.distanz", "Distance");
        AssertAliasGroup(result, "kanonisch-q1", "vsa.q1", "Q1", "Quantifizierung1");
    }

    [Fact]
    public void NormalizeAliases_ueberschreibt_Codegruppe_mit_getrimmtem_ausgewaehltem_Code()
    {
        var input = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["vsa.code"] = "ALT-KANONISCH",
            ["Code"] = "ALT"
        };

        var result = VsaParameterMerger.NormalizeAliases(input, " BABAC ");

        AssertAliasGroup(result, "BABAC", "vsa.code", "Code");
    }

    [Fact]
    public void NormalizeAliases_spiegelt_Code_Alias_wenn_kein_Code_uebergeben_wird()
    {
        var input = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["vsa.code"] = "   ",
            ["Code"] = " ALT "
        };

        var result = VsaParameterMerger.NormalizeAliases(input, "   ");

        AssertAliasGroup(result, "ALT", "vsa.code", "Code");
    }

    [Fact]
    public void NormalizeAliases_behaelt_erste_Key_Schreibweise_bei_IgnoreCase_Kollision()
    {
        // Parameters are serialized with their stored key spelling, so this is
        // part of the persisted-data compatibility contract.
        var input = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [" Distance "] = "zuerst",
            ["distance"] = "spaeter"
        };

        var result = VsaParameterMerger.NormalizeAliases(input, string.Empty);

        Assert.Contains(result.Keys, key => string.Equals(key, "Distance", StringComparison.Ordinal));
        Assert.DoesNotContain(result.Keys, key => string.Equals(key, "distance", StringComparison.Ordinal));
        AssertAliasGroup(result, "spaeter", "vsa.distanz", "Distance");
    }

    private static void AssertAliasGroup(
        IReadOnlyDictionary<string, string> parameters,
        string expected,
        params string[] keys)
    {
        foreach (var key in keys)
            Assert.Equal(expected, parameters[key]);
    }
}
