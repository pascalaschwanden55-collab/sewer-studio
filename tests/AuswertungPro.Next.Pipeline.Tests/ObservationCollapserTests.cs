using AuswertungPro.Next.Application.Reports;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.Pipeline.Tests;

public sealed class ObservationCollapserTests
{
    [Fact]
    public void Collapse_folds_quantifier_row_into_text_row_same_code_and_meter()
    {
        var text = new ProtocolEntry { Code = "BCCBY", MeterStart = 0, Beschreibung = "Bogen nach rechts" };
        var quant = new ProtocolEntry
        {
            Code = "BCCBY",
            MeterStart = 0,
            Beschreibung = "",
            CodeMeta = new ProtocolEntryCodeMeta
            {
                Code = "BCCBY",
                Parameters = { ["Quantifizierung1"] = "45" }
            }
        };

        var result = ObservationCollapser.Collapse(new[] { text, quant });

        var entry = Assert.Single(result);
        Assert.Equal("Bogen nach rechts", entry.Beschreibung);
        Assert.Equal("45", entry.CodeMeta!.Parameters["Quantifizierung1"]);
    }

    [Fact]
    public void Collapse_folds_dash_continuation_into_text_row()
    {
        var text = new ProtocolEntry { Code = "BCD", MeterStart = 0, Beschreibung = "Rohranfang" };
        var dash = new ProtocolEntry { Code = "BCD", MeterStart = 0, Beschreibung = "–" };

        var result = ObservationCollapser.Collapse(new[] { text, dash });

        var entry = Assert.Single(result);
        Assert.Equal("Rohranfang", entry.Beschreibung);
    }

    [Fact]
    public void Collapse_keeps_distinct_observations_at_different_meter()
    {
        var a = new ProtocolEntry { Code = "BAB", MeterStart = 1.2, Beschreibung = "Riss" };
        var b = new ProtocolEntry { Code = "BAB", MeterStart = 5.0, Beschreibung = "Riss" };

        var result = ObservationCollapser.Collapse(new[] { a, b });

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void Collapse_keeps_two_distinct_descriptions_same_code_and_meter_no_loss()
    {
        var a = new ProtocolEntry { Code = "BAB", MeterStart = 1.2, Beschreibung = "Laengsriss oben" };
        var b = new ProtocolEntry { Code = "BAB", MeterStart = 1.2, Beschreibung = "Querriss unten" };

        var result = ObservationCollapser.Collapse(new[] { a, b });

        Assert.Equal(2, result.Count);
        Assert.Contains(result, e => e.Beschreibung == "Laengsriss oben");
        Assert.Contains(result, e => e.Beschreibung == "Querriss unten");
    }

    [Fact]
    public void Collapse_keeps_thin_rows_with_different_clock_positions_no_loss()
    {
        // Zwei Beobachtungen gleichen Codes am gleichen Meter, beide ohne Freitext,
        // aber unterschiedliche Uhrlage (3 Uhr vs. 9 Uhr) -> muessen BEIDE erhalten bleiben.
        var a = new ProtocolEntry
        {
            Code = "BCA",
            MeterStart = 3,
            Beschreibung = "",
            CodeMeta = new ProtocolEntryCodeMeta { Code = "BCA", Parameters = { ["ClockPos1"] = "3" } }
        };
        var b = new ProtocolEntry
        {
            Code = "BCA",
            MeterStart = 3,
            Beschreibung = "",
            CodeMeta = new ProtocolEntryCodeMeta { Code = "BCA", Parameters = { ["ClockPos1"] = "9" } }
        };

        var result = ObservationCollapser.Collapse(new[] { a, b });

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void Collapse_keeps_thin_rows_with_different_quantifier_no_loss()
    {
        var a = new ProtocolEntry
        {
            Code = "BAA",
            MeterStart = 7,
            Beschreibung = "",
            CodeMeta = new ProtocolEntryCodeMeta { Code = "BAA", Parameters = { ["Quantifizierung1"] = "20" } }
        };
        var b = new ProtocolEntry
        {
            Code = "BAA",
            MeterStart = 7,
            Beschreibung = "",
            CodeMeta = new ProtocolEntryCodeMeta { Code = "BAA", Parameters = { ["Quantifizierung1"] = "40" } }
        };

        var result = ObservationCollapser.Collapse(new[] { a, b });

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void Collapse_keeps_thin_rows_with_different_named_parameter_no_loss()
    {
        // Divergierender benannter Katalog-Parameter (Breite) unter eigenem Schluessel,
        // nicht Q1/Q2/Uhr -> beide Beobachtungen muessen erhalten bleiben.
        var a = new ProtocolEntry
        {
            Code = "BAJ",
            MeterStart = 4,
            Beschreibung = "",
            CodeMeta = new ProtocolEntryCodeMeta { Code = "BAJ", Parameters = { ["Breite"] = "10" } }
        };
        var b = new ProtocolEntry
        {
            Code = "BAJ",
            MeterStart = 4,
            Beschreibung = "",
            CodeMeta = new ProtocolEntryCodeMeta { Code = "BAJ", Parameters = { ["Breite"] = "20" } }
        };

        var result = ObservationCollapser.Collapse(new[] { a, b });

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void Collapse_still_folds_thin_rows_with_identical_quantifier()
    {
        // Gleiche Uhrlage/Quantifizierung + eine Textzeile -> weiterhin zu EINEM Eintrag falten.
        var text = new ProtocolEntry { Code = "BAA", MeterStart = 7, Beschreibung = "Verformung" };
        var quant = new ProtocolEntry
        {
            Code = "BAA",
            MeterStart = 7,
            Beschreibung = "",
            CodeMeta = new ProtocolEntryCodeMeta { Code = "BAA", Parameters = { ["Quantifizierung1"] = "20" } }
        };

        var result = ObservationCollapser.Collapse(new[] { text, quant });

        var entry = Assert.Single(result);
        Assert.Equal("Verformung", entry.Beschreibung);
        Assert.Equal("20", entry.CodeMeta!.Parameters["Quantifizierung1"]);
    }

    [Fact]
    public void Collapse_unions_photos_and_timecode_when_folding()
    {
        var a = new ProtocolEntry { Code = "BAF", MeterStart = 2, Beschreibung = "Korrosion" };
        a.FotoPaths.Add("Fotos/a.jpg");
        var b = new ProtocolEntry { Code = "BAF", MeterStart = 2, Beschreibung = "", Mpeg = "00:00:21" };
        b.FotoPaths.Add("Fotos/b.jpg");

        var result = ObservationCollapser.Collapse(new[] { a, b });

        var entry = Assert.Single(result);
        Assert.Equal(new[] { "Fotos/a.jpg", "Fotos/b.jpg" }, entry.FotoPaths);
        Assert.Equal("00:00:21", entry.Mpeg);
    }

    [Fact]
    public void Collapse_keeps_same_file_name_from_different_photo_folders()
    {
        var a = new ProtocolEntry { Code = "BAF", MeterStart = 2, Beschreibung = "Korrosion" };
        a.FotoPaths.Add("Fotos/Haltungen/H1/schaden.jpg");
        var b = new ProtocolEntry { Code = "BAF", MeterStart = 2, Beschreibung = "" };
        b.FotoPaths.Add("Fotos/Haltungen/H2/schaden.jpg");

        var result = ObservationCollapser.Collapse(new[] { a, b });

        var entry = Assert.Single(result);
        Assert.Equal(
            new[] { "Fotos/Haltungen/H1/schaden.jpg", "Fotos/Haltungen/H2/schaden.jpg" },
            entry.FotoPaths);
    }

    [Fact]
    public void Collapse_does_not_mutate_input_entries()
    {
        var text = new ProtocolEntry { Code = "BAF", MeterStart = 2, Beschreibung = "Korrosion" };
        text.FotoPaths.Add("Fotos/a.jpg");
        var quant = new ProtocolEntry
        {
            Code = "BAF",
            MeterStart = 2,
            Beschreibung = "",
            CodeMeta = new ProtocolEntryCodeMeta { Code = "BAF", Parameters = { ["Quantifizierung1"] = "30" } }
        };
        quant.FotoPaths.Add("Fotos/b.jpg");

        var result = ObservationCollapser.Collapse(new[] { text, quant });

        // Basiseintrag im Ergebnis ist ein Klon; die Eingabe bleibt unverändert.
        Assert.NotSame(text, Assert.Single(result));
        Assert.Equal(new[] { "Fotos/a.jpg" }, text.FotoPaths);
        Assert.Null(text.CodeMeta);
    }

    [Fact]
    public void Collapse_passthrough_for_empty_and_single()
    {
        Assert.Empty(ObservationCollapser.Collapse(new ProtocolEntry[0]));

        var only = new ProtocolEntry { Code = "BCD", MeterStart = 0, Beschreibung = "Rohranfang" };
        var single = ObservationCollapser.Collapse(new[] { only });
        Assert.Same(only, Assert.Single(single));
    }
}
