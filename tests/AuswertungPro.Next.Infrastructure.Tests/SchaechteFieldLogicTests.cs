using System.Collections.Generic;
using AuswertungPro.Next.Application.DataPage;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>
/// Charakterisierungs-Tests fuer SchaechteFieldLogic (reine Logik, kein IO).
/// </summary>
public sealed class SchaechteFieldLogicTests
{
    // -----------------------------------------------------------------------
    // NormalizeKey
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("", "")]
    [InlineData("   ", "")]
    [InlineData("Pruefung", "pruefung")]
    [InlineData("Prüfung", "pruefung")]
    [InlineData("Schächte", "schaechte")]
    [InlineData("Überprüfung", "ueberpruefung")]
    [InlineData("Straße", "strasse")]
    [InlineData("EigentÃ¼mer", "eigentuemer")]
    [InlineData("SCHACHT_NR", "schacht_nr")]
    public void NormalizeKey_NormalisierUmlauteUndGrossKlein(string input, string expected)
        => Assert.Equal(expected, SchaechteFieldLogic.NormalizeKey(input));

    // -----------------------------------------------------------------------
    // ResolveFieldValue
    // -----------------------------------------------------------------------

    private static SchachtRecord RecordWith(params (string key, string value)[] fields)
    {
        var r = new SchachtRecord();
        foreach (var (k, v) in fields)
            r.Fields[k] = v;
        return r;
    }

    [Fact]
    public void ResolveFieldValue_Sanieren_FindetSpalte()
    {
        var r = RecordWith(("Sanieren_JaNein", "Ja"), ("Anderes", "x"));
        Assert.Equal("Ja", SchaechteFieldLogic.ResolveFieldValue(r, "sanieren"));
    }

    [Fact]
    public void ResolveFieldValue_Pruefungsresultat_FindetUeberDichtheit()
    {
        var r = RecordWith(("Dichtheit_Resultat", "bestanden"));
        Assert.Equal("bestanden", SchaechteFieldLogic.ResolveFieldValue(r, "pruefungsresultat"));
    }

    [Fact]
    public void ResolveFieldValue_Referenzpruefung_FindetBeideTeile()
    {
        var r = RecordWith(("Referenzpruefung", "Nein"));
        Assert.Equal("Nein", SchaechteFieldLogic.ResolveFieldValue(r, "referenzpruefung"));
    }

    [Fact]
    public void ResolveFieldValue_AusgefuehrtDurch_FindetSpalte()
    {
        var r = RecordWith(("Ausgefuehrt_durch", "Kanalsanierer"));
        Assert.Equal("Kanalsanierer", SchaechteFieldLogic.ResolveFieldValue(r, "ausgefuehrt_durch"));
    }

    [Fact]
    public void ResolveFieldValue_UnbekanntesFeld_GibtLeer()
    {
        var r = RecordWith(("NR", "1"));
        Assert.Equal("", SchaechteFieldLogic.ResolveFieldValue(r, "sanieren"));
    }

    // -----------------------------------------------------------------------
    // ResolveNrColumnName
    // -----------------------------------------------------------------------

    [Fact]
    public void ResolveNrColumnName_AusColumns_GibtErsteTreffer()
    {
        var cols = new List<string> { "Schachtnummer", "SchachtNr", "Strasse" };
        var result = SchaechteFieldLogic.ResolveNrColumnName(cols, System.Array.Empty<SchachtRecord>());
        Assert.Equal("SchachtNr", result);
    }

    [Fact]
    public void ResolveNrColumnName_AusRecordFields_WennColumnsLeer()
    {
        var r = RecordWith(("SchachtNR", "1"), ("Strasse", "Hauptgasse"));
        var result = SchaechteFieldLogic.ResolveNrColumnName(
            System.Array.Empty<string>(),
            new[] { r });
        Assert.Equal("SchachtNR", result);
    }

    [Fact]
    public void ResolveNrColumnName_GibtNull_WennKeinNrFeld()
    {
        var result = SchaechteFieldLogic.ResolveNrColumnName(
            new[] { "Strasse", "DN" },
            System.Array.Empty<SchachtRecord>());
        Assert.Null(result);
    }

    // Echte Datenfelder wie "Innen-Nr" duerfen beim Durchnummerieren nicht
    // mit 1, 2, 3 ueberschrieben werden: Die echte Nummernspalte gewinnt.
    [Fact]
    public void ResolveNrColumnName_bevorzugt_die_echte_Nummernspalte_vor_einem_Namensfetzen()
    {
        var cols = new List<string> { "Innen-Nr", "Strasse", "NR." };

        var result = SchaechteFieldLogic.ResolveNrColumnName(cols, System.Array.Empty<SchachtRecord>());

        Assert.Equal("NR.", result);
    }

    [Theory]
    [InlineData("NR")]
    [InlineData("NR.")]
    [InlineData("Nr.")]
    [InlineData(" nr ")]
    public void ResolveNrColumnName_erkennt_die_gaengigen_Schreibweisen(string spalte)
    {
        var result = SchaechteFieldLogic.ResolveNrColumnName(
            new List<string> { "Strasse", spalte },
            System.Array.Empty<SchachtRecord>());

        Assert.Equal(spalte, result);
    }

    [Fact]
    public void ResolveNrColumnName_waehlt_bei_zwei_Schreibweisen_die_grossgeschriebene()
    {
        var cols = new List<string> { "Nr.", "NR." };

        var result = SchaechteFieldLogic.ResolveNrColumnName(cols, System.Array.Empty<SchachtRecord>());

        Assert.Equal("NR.", result);
    }

    [Fact]
    public void ResolveNrColumnName_bevorzugt_die_Spaltenliste_vor_den_Recordfeldern()
    {
        var record = RecordWith(("Innen-Nr", "74466"), ("NR.", "1"));

        var result = SchaechteFieldLogic.ResolveNrColumnName(new List<string> { "NR." }, new[] { record });

        Assert.Equal("NR.", result);
    }

    [Fact]
    public void ResolveNrColumnName_nimmt_aus_den_Recordfeldern_ebenfalls_die_echte_Spalte()
    {
        var record = RecordWith(("Innen-Nr", "74466"), ("NR.", "1"));

        var result = SchaechteFieldLogic.ResolveNrColumnName(System.Array.Empty<string>(), new[] { record });

        Assert.Equal("NR.", result);
    }

    // -----------------------------------------------------------------------
    // MatchesSearch
    // -----------------------------------------------------------------------

    [Fact]
    public void MatchesSearch_LeererSuchbegriff_GibtImmerTrue()
    {
        var r = RecordWith(("Strasse", "Testgasse"));
        Assert.True(SchaechteFieldLogic.MatchesSearch(r, ""));
        Assert.True(SchaechteFieldLogic.MatchesSearch(r, "   "));
    }

    [Fact]
    public void MatchesSearch_TrefferImWert_GibtTrue()
    {
        var r = RecordWith(("Strasse", "Hauptgasse 10"));
        Assert.True(SchaechteFieldLogic.MatchesSearch(r, "hauptgasse"));
    }

    [Fact]
    public void MatchesSearch_TrefferImSchluessel_GibtTrue()
    {
        var r = RecordWith(("Sanieren_JaNein", "Ja"));
        Assert.True(SchaechteFieldLogic.MatchesSearch(r, "Sanieren"));
    }

    [Fact]
    public void MatchesSearch_KeinTreffer_GibtFalse()
    {
        var r = RecordWith(("Strasse", "Hauptgasse"));
        Assert.False(SchaechteFieldLogic.MatchesSearch(r, "xyz"));
    }

    // -----------------------------------------------------------------------
    // BuildSearchResultInfo
    // -----------------------------------------------------------------------

    [Fact]
    public void BuildSearchResultInfo_LeerBeiLeeremSuchbegriff()
        => Assert.Equal("", SchaechteFieldLogic.BuildSearchResultInfo(5, 10, ""));

    [Fact]
    public void BuildSearchResultInfo_ZeigtNvonM()
        => Assert.Equal("3 von 10 Schaechten", SchaechteFieldLogic.BuildSearchResultInfo(3, 10, "test"));
}
