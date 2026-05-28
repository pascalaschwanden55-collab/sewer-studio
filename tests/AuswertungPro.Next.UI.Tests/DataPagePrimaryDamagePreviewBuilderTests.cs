using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.DataPage;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

public sealed class DataPagePrimaryDamagePreviewBuilderTests
{
    [Fact]
    public void Build_uses_structured_findings_before_raw_field()
    {
        var record = new HaltungRecord();
        record.SetFieldValue("Primaere_Schaeden", "BAJA @ 99m alter Text", FieldSource.Manual, userEdited: true);
        record.VsaFindings.Add(new VsaFinding
        {
            KanalSchadencode = "baja",
            MeterStart = 1.234,
            Raw = "BAJA @ 1,23m Versatz Q1=20"
        });

        var preview = DataPagePrimaryDamagePreviewBuilder.Build(record, ResolveTitle);

        Assert.Equal("1.23m BAJA Verschobene Rohrverbindung (Versatz) Q1=20", preview);
    }

    [Fact]
    public void BuildLinesFromFindings_deduplicates_by_code_and_meter()
    {
        var record = new HaltungRecord();
        record.VsaFindings.Add(new VsaFinding
        {
            KanalSchadencode = "BBBA",
            MeterStart = 4.2,
            Raw = "BBBA @ 4.20m Inkrustation"
        });
        record.VsaFindings.Add(new VsaFinding
        {
            KanalSchadencode = "BBBA",
            MeterStart = 4.2,
            Raw = "BBBA @ 4.20m Inkrustation doppelt"
        });

        var lines = DataPagePrimaryDamagePreviewBuilder.BuildLinesFromFindings(record, ResolveTitle);

        var line = Assert.Single(lines);
        Assert.Equal("4.20m BBBA Inkrustation", line);
    }

    [Fact]
    public void BuildLinesFromRaw_formats_code_lines_and_preserves_non_code_lines()
    {
        const string raw = """
            BAJA @ 12,5m Versatz Q1=20 Q2=A
            Kommentar ohne Code
            """;

        var lines = DataPagePrimaryDamagePreviewBuilder.BuildLinesFromRaw(raw, ResolveTitle);

        Assert.Equal(new[]
        {
            "12.50m BAJA Verschobene Rohrverbindung (Versatz) Q1=20 Q2=A",
            "Kommentar ohne Code"
        }, lines);
    }

    private static string? ResolveTitle(string code)
        => code switch
        {
            "BAJA" => "Verschobene Rohrverbindung",
            "BBBA" => "Inkrustation",
            _ => null
        };
}
