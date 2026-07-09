using AuswertungPro.Next.Application.Reports;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.Pipeline.Tests;

public sealed class CounterInspectionStationingNormalizerTests
{
    [Fact]
    public void NormalizeForExport_mirrors_counter_inspection_to_full_holding_axis()
    {
        var entries = new[]
        {
            new ProtocolEntry { Code = "BCD", MeterStart = 0, Beschreibung = "Rohranfang" },
            new ProtocolEntry { Code = "BDCAD", MeterStart = 13.9, Beschreibung = "Kamera kommt nicht weiter, Gegeninspektion" },
            new ProtocolEntry { Code = "BCD", MeterStart = 0, Beschreibung = "Rohranfang Gegeninspektion" },
            new ProtocolEntry { Code = "AEDXQ", MeterStart = 10.6, Beschreibung = "Rohrmaterialwechsel" },
            new ProtocolEntry { Code = "BDBD", MeterStart = 23.1, Beschreibung = "Gegeninspektion erfolgreich" }
        };

        var result = CounterInspectionStationingNormalizer.NormalizeForExport(entries, 37.0);

        Assert.Equal(new[] { 0.0, 13.9, 13.9, 26.4, 37.0 }, result.Select(e => e.MeterStart!.Value).ToArray());
        Assert.Equal(new[] { "BCD", "BDCAD", "BDBD", "AEDXQ", "BCD" }, result.Select(e => e.Code).ToArray());
    }

    [Fact]
    public void NormalizeForExport_does_not_change_already_normalized_counter_axis()
    {
        var entries = new[]
        {
            new ProtocolEntry { Code = "BCD", MeterStart = 0 },
            new ProtocolEntry { Code = "BDCAD", MeterStart = 13.9 },
            new ProtocolEntry { Code = "BDBD", MeterStart = 13.9 },
            new ProtocolEntry { Code = "AEDXQ", MeterStart = 26.4 },
            new ProtocolEntry { Code = "BCD", MeterStart = 37.0 }
        };

        var result = CounterInspectionStationingNormalizer.NormalizeForExport(entries, 37.0);

        Assert.Equal(new[] { 0.0, 13.9, 13.9, 26.4, 37.0 }, result.Select(e => e.MeterStart!.Value).ToArray());
        Assert.Equal(new[] { "BCD", "BDCAD", "BDBD", "AEDXQ", "BCD" }, result.Select(e => e.Code).ToArray());
    }

    [Fact]
    public void NormalizeForExport_mirrors_counter_inspection_and_keeps_gap_when_total_is_not_reached()
    {
        var entries = new[]
        {
            new ProtocolEntry { Code = "BCD", MeterStart = 0 },
            new ProtocolEntry { Code = "BDCAD", MeterStart = 10.0 },
            new ProtocolEntry { Code = "BCD", MeterStart = 0 },
            new ProtocolEntry { Code = "BDBD", MeterStart = 20.0 }
        };

        var result = CounterInspectionStationingNormalizer.NormalizeForExport(entries, 37.0);

        Assert.Equal(new[] { 0.0, 10.0, 17.0, 37.0 }, result.Select(e => e.MeterStart!.Value).ToArray());
        Assert.Equal(new[] { "BCD", "BDCAD", "BDBD", "BCD" }, result.Select(e => e.Code).ToArray());
    }

    [Fact]
    public void NormalizeForExport_mirrors_meter_end_only_when_it_is_a_real_distance()
    {
        var entries = new[]
        {
            new ProtocolEntry { Code = "BDCAD", MeterStart = 13.9 },
            new ProtocolEntry { Code = "BDBD", MeterStart = 23.1, MeterEnd = 23.1 },
            new ProtocolEntry { Code = "BAFKE", MeterStart = 11.3, MeterEnd = 3 }
        };

        var result = CounterInspectionStationingNormalizer.NormalizeForExport(entries, 37.0);

        Assert.Equal(25.7, result[1].MeterStart);
        Assert.Equal(3, result[1].MeterEnd);
        Assert.Equal(13.9, result[2].MeterStart);
        Assert.Equal(13.9, result[2].MeterEnd);
    }

    [Fact]
    public void NormalizeForExport_clones_entries()
    {
        var counter = new ProtocolEntry { Code = "BDBD", MeterStart = 23.1 };
        var entries = new[]
        {
            new ProtocolEntry { Code = "BAB", MeterStart = 13.9 },
            counter
        };

        var result = CounterInspectionStationingNormalizer.NormalizeForExport(entries, 37.0);

        Assert.NotSame(counter, result[1]);
        Assert.Equal(23.1, counter.MeterStart);
        Assert.Equal(23.1, result[1].MeterStart);
    }
}
