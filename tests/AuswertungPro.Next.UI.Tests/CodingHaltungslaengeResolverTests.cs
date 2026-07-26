using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingHaltungslaengeResolverTests
{
    [Theory]
    [InlineData("12.3")]
    [InlineData("12,3")]
    public void HasValidLength_accepts_positive_dot_or_comma_values(string raw)
    {
        var record = new HaltungRecord();
        record.SetFieldValue("Haltungslaenge_m", raw, FieldSource.Manual, userEdited: true);

        Assert.True(CodingHaltungslaengeResolver.HasValidLength(record, "Haltungslaenge_m"));
    }

    [Fact]
    public void TryEnsureFromKnownSources_keeps_existing_haltungslaenge()
    {
        var record = new HaltungRecord();
        record.SetFieldValue("Haltungslaenge_m", "10.5", FieldSource.Manual, userEdited: true);

        var resolved = CodingHaltungslaengeResolver.TryEnsureFromKnownSources(record, overlayPipeLengthMeters: 22);

        Assert.True(resolved);
        Assert.Equal("10.5", record.GetFieldValue("Haltungslaenge_m"));
    }

    [Fact]
    public void TryEnsureFromKnownSources_copies_laenge_m_before_other_fallbacks()
    {
        var record = new HaltungRecord();
        record.SetFieldValue("Laenge_m", "17,25", FieldSource.Legacy, userEdited: false);

        var resolved = CodingHaltungslaengeResolver.TryEnsureFromKnownSources(record, overlayPipeLengthMeters: 22);

        Assert.True(resolved);
        Assert.Equal("17,25", record.GetFieldValue("Haltungslaenge_m"));
    }

    [Fact]
    public void TryEnsureFromKnownSources_uses_overlay_length_when_fields_are_missing()
    {
        var record = new HaltungRecord();

        var resolved = CodingHaltungslaengeResolver.TryEnsureFromKnownSources(record, overlayPipeLengthMeters: 22.345);

        Assert.True(resolved);
        Assert.Equal("22.34", record.GetFieldValue("Haltungslaenge_m"));
    }

    [Fact]
    public void TryEnsureFromKnownSources_uses_max_protocol_meter_as_last_known_source()
    {
        var record = new HaltungRecord
        {
            Protocol = new ProtocolDocument
            {
                Current = new ProtocolRevision
                {
                    Entries =
                    {
                        new ProtocolEntry { MeterStart = 3.1 },
                        new ProtocolEntry { MeterStart = 9.876 },
                        new ProtocolEntry { MeterStart = -1 }
                    }
                }
            }
        };

        var resolved = CodingHaltungslaengeResolver.TryEnsureFromKnownSources(record, overlayPipeLengthMeters: null);

        Assert.True(resolved);
        Assert.Equal("9.88", record.GetFieldValue("Haltungslaenge_m"));
    }

    [Fact]
    public void TryEnsureFromKnownSources_returns_false_without_known_sources()
    {
        var record = new HaltungRecord();

        Assert.False(CodingHaltungslaengeResolver.TryEnsureFromKnownSources(record, overlayPipeLengthMeters: null));
    }
}
