using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.DataPage;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

public sealed class DataPageVideoOverlayBuilderTests
{
    [Fact]
    public void Build_returns_null_when_pipe_length_is_missing()
    {
        var record = new HaltungRecord();
        record.Protocol = new ProtocolDocument
        {
            Current = new ProtocolRevision
            {
                Entries =
                {
                    new ProtocolEntry { Code = "BAJA", MeterStart = 1.2 }
                }
            }
        };

        var overlay = DataPageVideoOverlayBuilder.Build(record);

        Assert.Null(overlay);
    }

    [Fact]
    public void Build_uses_active_protocol_entries_before_vsa_findings()
    {
        var record = new HaltungRecord();
        record.SetFieldValue("Haltungslaenge_m", "12,5", FieldSource.Manual, userEdited: true);
        record.Protocol = new ProtocolDocument
        {
            Current = new ProtocolRevision
            {
                Entries =
                {
                    new ProtocolEntry
                    {
                        Code = "BAJA",
                        Beschreibung = "Versatz",
                        MeterStart = 2.1,
                        MeterEnd = 2.4,
                        IsStreckenschaden = true
                    },
                    new ProtocolEntry
                    {
                        Code = "BAB",
                        MeterStart = 3.0,
                        IsDeleted = true
                    }
                }
            }
        };
        record.VsaFindings.Add(new VsaFinding
        {
            KanalSchadencode = "BBA",
            MeterStart = 9.9,
            Raw = "Wurzeln"
        });

        var overlay = DataPageVideoOverlayBuilder.Build(record);

        Assert.NotNull(overlay);
        Assert.Equal(12.5, overlay.PipeLengthMeters);
        var marker = Assert.Single(overlay.Markers);
        Assert.Equal("BAJA", marker.Code);
        Assert.Equal("Versatz", marker.Description);
        Assert.Equal(2.1, marker.MeterStart);
        Assert.Equal(2.4, marker.MeterEnd);
        Assert.True(marker.IsStreckenschaden);
    }

    [Fact]
    public void Build_falls_back_to_vsa_findings_when_protocol_has_no_entries()
    {
        var record = new HaltungRecord();
        record.SetFieldValue("Haltungslaenge_m", "10", FieldSource.Manual, userEdited: true);
        record.VsaFindings.Add(new VsaFinding
        {
            KanalSchadencode = "BBA",
            MeterStart = 1.5,
            MeterEnd = 2.0,
            Raw = "Wurzeln"
        });
        record.VsaFindings.Add(new VsaFinding
        {
            KanalSchadencode = "BBB",
            Raw = "ohne Meter"
        });

        var overlay = DataPageVideoOverlayBuilder.Build(record);

        Assert.NotNull(overlay);
        var marker = Assert.Single(overlay.Markers);
        Assert.Equal("BBA", marker.Code);
        Assert.Equal("Wurzeln", marker.Description);
        Assert.Equal(1.5, marker.MeterStart);
        Assert.Equal(2.0, marker.MeterEnd);
        Assert.True(marker.IsStreckenschaden);
    }
}
