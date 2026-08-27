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
        record.SetFieldValue("Laenge_m", "17,25", FieldSource.Xtf, userEdited: false);

        var resolved = CodingHaltungslaengeResolver.TryEnsureFromKnownSources(record, overlayPipeLengthMeters: 22);

        Assert.True(resolved);
        Assert.Equal("17,25", record.GetFieldValue("Haltungslaenge_m"));
        Assert.Equal(FieldSource.Xtf, record.FieldMeta["Haltungslaenge_m"].Source);
    }

    [Fact]
    public void TryEnsureFromKnownSources_does_not_treat_overlay_as_independent_length_source()
    {
        var record = new HaltungRecord();

        var resolved = CodingHaltungslaengeResolver.TryEnsureFromKnownSources(record, overlayPipeLengthMeters: 22.345);

        Assert.False(resolved);
        Assert.Equal("", record.GetFieldValue("Haltungslaenge_m"));
    }

    [Fact]
    public void TryEnsureFromKnownSources_does_not_treat_damage_meter_as_holding_length()
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

        Assert.False(resolved);
        Assert.Equal("", record.GetFieldValue("Haltungslaenge_m"));
    }

    [Fact]
    public void TryEnsureFromKnownSources_uses_unique_active_pipe_end_and_marks_source()
    {
        var record = RecordWithEntries(
            new ProtocolEntry { Code = "BABBB", MeterStart = 9.876 },
            new ProtocolEntry { Code = "BCE", MeterStart = 22.345 });

        var resolved = CodingHaltungslaengeResolver.TryEnsureFromKnownSources(record, overlayPipeLengthMeters: null);

        Assert.True(resolved);
        Assert.Equal("22.34", record.GetFieldValue("Haltungslaenge_m"));
        Assert.Equal(FieldSource.Protocol, record.FieldMeta["Haltungslaenge_m"].Source);
        Assert.False(record.FieldMeta["Haltungslaenge_m"].UserEdited);
    }

    [Fact]
    public void TryEnsureFromKnownSources_ignores_deleted_pipe_end()
    {
        var record = RecordWithEntries(
            new ProtocolEntry { Code = "BCE", MeterStart = 22.345, IsDeleted = true });

        var resolved = CodingHaltungslaengeResolver.TryEnsureFromKnownSources(record, overlayPipeLengthMeters: null);

        Assert.False(resolved);
        Assert.Equal("", record.GetFieldValue("Haltungslaenge_m"));
    }

    [Fact]
    public void TryEnsureFromKnownSources_rejects_conflicting_active_pipe_ends()
    {
        var record = RecordWithEntries(
            new ProtocolEntry { Code = "BCE", MeterStart = 22.34 },
            new ProtocolEntry { Code = "BCE", MeterStart = 25.67 });

        var resolved = CodingHaltungslaengeResolver.TryEnsureFromKnownSources(record, overlayPipeLengthMeters: null);

        Assert.False(resolved);
        Assert.Equal("", record.GetFieldValue("Haltungslaenge_m"));
    }

    [Fact]
    public void TryEnsureFromKnownSources_rejects_pipe_end_when_abort_is_also_active()
    {
        var record = RecordWithEntries(
            new ProtocolEntry { Code = "BDCZ", MeterStart = 14.20 },
            new ProtocolEntry { Code = "BCE", MeterStart = 22.34 });

        var resolved = CodingHaltungslaengeResolver.TryEnsureFromKnownSources(record, overlayPipeLengthMeters: null);

        Assert.False(resolved);
        Assert.Equal("", record.GetFieldValue("Haltungslaenge_m"));
    }

    [Fact]
    public void TryEnsureFromKnownSources_returns_false_without_known_sources()
    {
        var record = new HaltungRecord();

        Assert.False(CodingHaltungslaengeResolver.TryEnsureFromKnownSources(record, overlayPipeLengthMeters: null));
    }

    private static HaltungRecord RecordWithEntries(params ProtocolEntry[] entries)
        => new()
        {
            Protocol = new ProtocolDocument
            {
                Current = new ProtocolRevision
                {
                    Entries = entries.ToList()
                }
            }
        };
}
