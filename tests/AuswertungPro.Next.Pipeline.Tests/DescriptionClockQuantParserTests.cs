using AuswertungPro.Next.Application.Protocol;

namespace AuswertungPro.Next.Pipeline.Tests;

/// <summary>
/// Charakterisierungs-Tests fuer DescriptionClockQuantParser (IST-Verhalten aus ObservationCatalogViewModel).
/// </summary>
public sealed class DescriptionClockQuantParserTests
{
    [Fact]
    public void Parse_uhrzeiten_von_bis_aus_text()
    {
        string? uhrVon = null, uhrBis = null, q1 = null, q2 = null;
        DescriptionClockQuantParser.TryParseFromDescription(
            "Riss von 8 Uhr bis 3 Uhr, 10%",
            ref uhrVon, ref uhrBis, ref q1, ref q2);

        Assert.Equal("8", uhrVon);
        Assert.Equal("3", uhrBis);
    }

    [Fact]
    public void Parse_quantifizierung_aus_text()
    {
        string? uhrVon = null, uhrBis = null, q1 = null, q2 = null;
        DescriptionClockQuantParser.TryParseFromDescription(
            "Ablagerung 25%",
            ref uhrVon, ref uhrBis, ref q1, ref q2);

        Assert.Equal("25", q1);
        Assert.Null(q2);
    }

    [Fact]
    public void Parse_zwei_quantifizierungen_aus_text()
    {
        string? uhrVon = null, uhrBis = null, q1 = null, q2 = null;
        DescriptionClockQuantParser.TryParseFromDescription(
            "Riss 10%, 20%",
            ref uhrVon, ref uhrBis, ref q1, ref q2);

        Assert.Equal("10", q1);
        Assert.Equal("20", q2);
    }

    [Fact]
    public void Parse_komma_in_quantifizierung_wird_normalisiert()
    {
        string? uhrVon = null, uhrBis = null, q1 = null, q2 = null;
        DescriptionClockQuantParser.TryParseFromDescription(
            "Ablagerung 12,5%",
            ref uhrVon, ref uhrBis, ref q1, ref q2);

        Assert.Equal("12.5", q1);
    }

    [Fact]
    public void Parse_ueberschreibt_vorhandene_uhrwerte_nicht()
    {
        string? uhrVon = "12", uhrBis = "6", q1 = null, q2 = null;
        DescriptionClockQuantParser.TryParseFromDescription(
            "Riss von 8 Uhr bis 3 Uhr",
            ref uhrVon, ref uhrBis, ref q1, ref q2);

        // Vorhandene Werte bleiben erhalten
        Assert.Equal("12", uhrVon);
        Assert.Equal("6", uhrBis);
    }

    [Fact]
    public void Parse_ueberschreibt_vorhandene_q1_nicht()
    {
        string? uhrVon = null, uhrBis = null, q1 = "50", q2 = null;
        DescriptionClockQuantParser.TryParseFromDescription(
            "10%",
            ref uhrVon, ref uhrBis, ref q1, ref q2);

        Assert.Equal("50", q1);
    }

    [Fact]
    public void Parse_leerer_text_aendert_nichts()
    {
        string? uhrVon = null, uhrBis = null, q1 = null, q2 = null;
        DescriptionClockQuantParser.TryParseFromDescription(
            string.Empty,
            ref uhrVon, ref uhrBis, ref q1, ref q2);

        Assert.Null(uhrVon);
        Assert.Null(uhrBis);
        Assert.Null(q1);
        Assert.Null(q2);
    }

    [Fact]
    public void Parse_text_ohne_treffer_aendert_nichts()
    {
        string? uhrVon = null, uhrBis = null, q1 = null, q2 = null;
        DescriptionClockQuantParser.TryParseFromDescription(
            "Riss ohne spezifische Werte",
            ref uhrVon, ref uhrBis, ref q1, ref q2);

        Assert.Null(uhrVon);
        Assert.Null(uhrBis);
        Assert.Null(q1);
        Assert.Null(q2);
    }

    [Fact]
    public void Parse_partial_uhrzeit_nur_von_wenn_bis_fehlt_im_muster()
    {
        // "von 8 Uhr" ohne "bis" -> kein Match (Pattern braucht beide)
        string? uhrVon = null, uhrBis = null, q1 = null, q2 = null;
        DescriptionClockQuantParser.TryParseFromDescription(
            "Riss von 8 Uhr",
            ref uhrVon, ref uhrBis, ref q1, ref q2);

        Assert.Null(uhrVon);
        Assert.Null(uhrBis);
    }
}
