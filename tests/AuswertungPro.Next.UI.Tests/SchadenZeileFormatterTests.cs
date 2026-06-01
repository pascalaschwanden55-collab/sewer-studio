using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.DataPage;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

public sealed class SchadenZeileFormatterTests
{
    private static ProtocolEntry Entry(string code, string beschreibung, double? mStart, double? mEnd = null, bool strecke = false, bool deleted = false)
        => new() { Code = code, Beschreibung = beschreibung, MeterStart = mStart, MeterEnd = mEnd, IsStreckenschaden = strecke, IsDeleted = deleted };

    [Fact]
    public void Format_Punktschaden_ShowsSingleMeter()
    {
        var z = SchadenZeileFormatter.Format(Entry("BCD", "Rohranfang", 0.0));
        Assert.Equal("0.00 m", z.Meter);
        Assert.Equal("BCD", z.Code);
        Assert.Equal("Rohranfang", z.Klartext);
        Assert.Equal("Bestand", z.Kategorie);
    }

    [Fact]
    public void Format_Punktschaden_MarksEstimatedAiMeter()
    {
        var entry = Entry("BAB", "Riss", 1.2);
        entry.Ai = new ProtocolEntryAiMeta
        {
            MeterSource = "LinearEstimate",
            IsMeterEstimated = true
        };

        var z = SchadenZeileFormatter.Format(entry);

        Assert.Equal("ca. 1.20 m", z.Meter);
    }

    [Fact]
    public void Format_Streckenschaden_ShowsMeterRange()
    {
        var z = SchadenZeileFormatter.Format(Entry("BBA", "Wurzeln", 2.50, 8.10, strecke: true));
        Assert.Equal("2.50–8.10 m", z.Meter);
        Assert.Equal("Betrieb", z.Kategorie);
    }

    [Fact]
    public void Format_KlartextFallsBackToCode_WhenBeschreibungEmpty()
    {
        var z = SchadenZeileFormatter.Format(Entry("BAB", "", 1.0));
        Assert.Equal("BAB", z.Klartext);
        Assert.Equal("Zustand", z.Kategorie);
    }

    [Theory]
    [InlineData("BAB", "Zustand")]
    [InlineData("BBA", "Betrieb")]
    [InlineData("BCD", "Bestand")]
    [InlineData("BDDC", "Betrieb")]
    [InlineData("XYZ", "")]
    public void Kategorie_DerivedFromCodeGroup(string code, string expected)
    {
        Assert.Equal(expected, SchadenZeileFormatter.Format(Entry(code, "x", 0.0)).Kategorie);
    }

    [Fact]
    public void FormatList_SkipsDeletedAndEmptyCode()
    {
        var entries = new[]
        {
            Entry("BCD", "Rohranfang", 0.0),
            Entry("BBA", "Wurzeln", 2.0, deleted: true),
            Entry("", "kein Code", 3.0),
        };
        var rows = SchadenZeileFormatter.FormatList(entries);
        Assert.Single(rows);
        Assert.Equal("BCD", rows[0].Code);
    }
}
