using AuswertungPro.Next.Application.Reports;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.Pipeline.Tests;

public sealed class InspectionSegmenterTests
{
    [Fact]
    public void Segments_without_abort_code_returns_single_untitled_segment()
    {
        var entries = new[]
        {
            new ProtocolEntry { Code = "BCD", MeterStart = 0 },
            new ProtocolEntry { Code = "BAB", MeterStart = 5 }
        };

        var segments = InspectionSegmenter.Segments(entries);

        var only = Assert.Single(segments);
        Assert.Null(only.Title);
        Assert.Equal(2, only.Entries.Count);
    }

    [Fact]
    public void Segments_splits_at_first_abort_code_into_main_and_gegeninspektion()
    {
        var abort = new ProtocolEntry { Code = "BDCAD", MeterStart = 13.9, Beschreibung = "Kamera kommt nicht weiter" };
        var entries = new[]
        {
            new ProtocolEntry { Code = "BCD", MeterStart = 0 },
            new ProtocolEntry { Code = "BAB", MeterStart = 5 },
            abort,
            new ProtocolEntry { Code = "BAF", MeterStart = 18 },
            new ProtocolEntry { Code = "BCE", MeterStart = 23 }
        };

        var segments = InspectionSegmenter.Segments(entries);

        Assert.Equal(2, segments.Count);

        // Hauptinspektion enthält den Abbruch-Eintrag als Abschluss.
        Assert.Null(segments[0].Title);
        Assert.Equal(3, segments[0].Entries.Count);
        Assert.Same(abort, segments[0].Entries[2]);

        // Gegeninspektion = alles danach.
        Assert.Equal("Gegeninspektion", segments[1].Title);
        Assert.Equal(2, segments[1].Entries.Count);
        Assert.Equal("BAF", segments[1].Entries[0].Code);
    }

    [Fact]
    public void Segments_returns_single_segment_when_abort_is_last_entry()
    {
        var entries = new[]
        {
            new ProtocolEntry { Code = "BCD", MeterStart = 0 },
            new ProtocolEntry { Code = "BDCAD", MeterStart = 13.9 }
        };

        var segments = InspectionSegmenter.Segments(entries);

        var only = Assert.Single(segments);
        Assert.Null(only.Title);
        Assert.Equal(2, only.Entries.Count);
    }

    [Fact]
    public void Segments_empty_returns_single_empty_segment()
    {
        var segments = InspectionSegmenter.Segments(new ProtocolEntry[0]);

        var only = Assert.Single(segments);
        Assert.Null(only.Title);
        Assert.Empty(only.Entries);
    }
}
