using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.DataPage;

namespace AuswertungPro.Next.UI.Tests;

public sealed class DataPageStartFilterTests
{
    [Fact]
    public void FromDashboardZustand_mappt_zustand()
    {
        var filter = DataPageStartFilter.FromDashboardZustand("0");

        Assert.Equal("Zustandsklasse", filter.FieldName);
        Assert.Equal("0", filter.Value);
    }

    [Fact]
    public void FromDashboardZustand_entfernt_z_prefix()
    {
        var filter = DataPageStartFilter.FromDashboardZustand("Z2");

        Assert.Equal("Zustandsklasse", filter.FieldName);
        Assert.Equal("2", filter.Value);
    }

    [Fact]
    public void Matches_prueft_dn_und_schadenscode()
    {
        var record = new HaltungRecord();
        record.SetFieldValue("DN_mm", "DN 300", FieldSource.Manual, false);
        record.SetFieldValue("Primaere_Schaeden", "BAB Riss\nBCA Anschluss", FieldSource.Manual, false);

        Assert.True(DataPageStartFilter.FromDashboardDn("300").Matches(record));
        Assert.True(DataPageStartFilter.FromDashboardSchaden("BAB").Matches(record));
        Assert.False(DataPageStartFilter.FromDashboardSchaden("BBB").Matches(record));
    }

    [Fact]
    public void Matches_prueft_protokoll_codes()
    {
        var record = new HaltungRecord
        {
            Protocol = new ProtocolDocument
            {
                Current = new ProtocolRevision
                {
                    Entries =
                    [
                        new ProtocolEntry { Code = "BAB01" },
                        new ProtocolEntry { Code = "BCA02" }
                    ]
                }
            }
        };

        Assert.True(DataPageStartFilter.FromDashboardSchaden("BAB").Matches(record));
        Assert.True(DataPageStartFilter.FromDashboardSchaden("BCA").Matches(record));
        Assert.False(DataPageStartFilter.FromDashboardSchaden("BBB").Matches(record));
    }
}
