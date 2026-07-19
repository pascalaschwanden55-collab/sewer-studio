using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>Tests fuer die Abbildung ProtocolEntry (Codierfenster) -> Pruefplatz-Felder.</summary>
public sealed class WorkbenchCodeSelectionMapperTests
{
    [Fact]
    public void FromProtocolEntry_uebernimmt_Code_Uhrlage_und_gueltige_Stufe()
    {
        var entry = new ProtocolEntry
        {
            Code = "BABBC",
            CodeMeta = new ProtocolEntryCodeMeta
            {
                Code = "BABBC",
                Severity = "3",
                Parameters = { ["vsa.uhr.von"] = "3" },
            },
        };

        var sel = WorkbenchCodeSelectionMapper.FromProtocolEntry(entry);

        Assert.Equal("BABBC", sel.Code);
        Assert.Equal(3.0, sel.ClockPosition);
        Assert.Equal(3, sel.Severity);
    }

    [Fact]
    public void FromProtocolEntry_ohne_Uhr_und_Stufe_liefert_nur_Code()
    {
        var entry = new ProtocolEntry { Code = "BAB", CodeMeta = new ProtocolEntryCodeMeta { Code = "BAB" } };

        var sel = WorkbenchCodeSelectionMapper.FromProtocolEntry(entry);

        Assert.Equal("BAB", sel.Code);
        Assert.Null(sel.ClockPosition);
        Assert.Null(sel.Severity);
    }

    [Fact]
    public void FromProtocolEntry_parst_Uhr_mit_Minutenteil()
    {
        var entry = new ProtocolEntry
        {
            Code = "BAB",
            CodeMeta = new ProtocolEntryCodeMeta { Parameters = { ["vsa.uhr.von"] = "12:00" } },
        };

        var sel = WorkbenchCodeSelectionMapper.FromProtocolEntry(entry);

        Assert.Equal(12.0, sel.ClockPosition);
    }

    [Fact]
    public void FromProtocolEntry_ignoriert_ungueltige_Stufe()
    {
        var entry = new ProtocolEntry
        {
            Code = "BAB",
            CodeMeta = new ProtocolEntryCodeMeta { Severity = "9" },   // ausserhalb 1..5
        };

        var sel = WorkbenchCodeSelectionMapper.FromProtocolEntry(entry);

        Assert.Null(sel.Severity);
    }
}
