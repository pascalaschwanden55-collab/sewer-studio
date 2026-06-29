using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.Infrastructure.Import.Common;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.Import;

/// <summary>
/// Charakterisierungstests fuer PrimaryDamagesTextBuilder.
/// Sichert das IST-Verhalten der BuildPrimaryDamagesText-Methoden aus WinCan und IBAK.
/// </summary>
public class PrimaryDamagesTextBuilderTests
{
    private static ProtocolEntry Entry(string code, string desc, double? meter = null)
        => new() { Code = code, Beschreibung = desc, MeterStart = meter, Source = ProtocolEntrySource.Imported };

    [Fact]
    public void Build_LeereEintraege_GibtNullZurueck()
    {
        var result = PrimaryDamagesTextBuilder.Build([], skipAePrefix: false);
        Assert.Null(result);
    }

    [Fact]
    public void Build_EinfacherEintrag_FormatKorrekt()
    {
        var entries = new[] { Entry("BBC", "Harte Ablagerungen", 1.5) };
        var result = PrimaryDamagesTextBuilder.Build(entries, skipAePrefix: false);
        Assert.NotNull(result);
        Assert.Contains("1.50m", result!);
        Assert.Contains("BBC", result);
        Assert.Contains("Harte Ablagerungen", result);
    }

    [Fact]
    public void Build_StreckenMarker_WirdAufgeloest()
    {
        var entries = new[] { Entry("A01", "BBC Harte Ablagerungen", 2.0) };
        var result = PrimaryDamagesTextBuilder.Build(entries, skipAePrefix: false);
        Assert.NotNull(result);
        // Marker wird zu BBC aufgeloest
        Assert.Contains("BBC", result!);
        Assert.DoesNotContain("A01", result);
    }

    [Fact]
    public void Build_DedupliziertGleicheCodePlusMeter()
    {
        var entries = new[]
        {
            Entry("BAB", "Riss laengs", 3.0),
            Entry("BAB", "Riss laengs", 3.0)
        };
        var result = PrimaryDamagesTextBuilder.Build(entries, skipAePrefix: false);
        // Nur einmal in der Ausgabe
        var count = result!.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length;
        Assert.Equal(1, count);
    }

    [Fact]
    public void Build_SkipAePrefix_True_HeaderCodesUebersprungen()
    {
        var entries = new[]
        {
            Entry("AEC", "Rohrprofilwechsel", 0.0),
            Entry("AED", "Rohrmaterialwechsel", 0.0),
            Entry("BBC", "Ablagerung", 5.0)
        };
        var result = PrimaryDamagesTextBuilder.Build(entries, skipAePrefix: true);
        Assert.NotNull(result);
        Assert.DoesNotContain("AEC", result!);
        Assert.DoesNotContain("AED", result);
        Assert.Contains("BBC", result);
    }

    [Fact]
    public void Build_SkipAePrefix_False_HeaderCodesEnthalten()
    {
        var entries = new[]
        {
            Entry("AEC", "Rohrprofilwechsel", 0.0),
            Entry("BBC", "Ablagerung", 5.0)
        };
        var result = PrimaryDamagesTextBuilder.Build(entries, skipAePrefix: false);
        Assert.NotNull(result);
        Assert.Contains("AEC", result!);
        Assert.Contains("BBC", result);
    }

    [Fact]
    public void Build_MehrereEintraege_MehrereZeilen()
    {
        var entries = new[]
        {
            Entry("BAB", "Riss", 1.0),
            Entry("BBC", "Ablagerung", 3.0)
        };
        var result = PrimaryDamagesTextBuilder.Build(entries, skipAePrefix: false);
        Assert.NotNull(result);
        var lines = result!.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(2, lines.Length);
    }

    [Fact]
    public void Build_KeinMeter_KeinMeterPrefix()
    {
        var entries = new[] { Entry("BCD", "Rohranfang", null) };
        var result = PrimaryDamagesTextBuilder.Build(entries, skipAePrefix: false);
        Assert.NotNull(result);
        Assert.DoesNotContain("m ", result!.TrimStart());
        Assert.Contains("BCD", result);
    }
}
